using Microsoft.Xna.Framework;
using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Utilities;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Portals;

public static class PortalSystem
{
    public static bool StartPortalCreation(Player player, int requestId)
    {
        if (!TravelRules.Enabled)
            return false;

        if (player?.active != true || Main.netMode == NetmodeID.MultiplayerClient)
            return false;

        RemoveCreationProjectiles(player.whoAmI);

        Vector2 worldPos = PortalCreatorItem.GetPortalWorldPosition(player);
        int creationFrames = PortalCreatorItem.GetCreationTimeFrames();
        int ownerTeam = player.team;

        int index = Projectile.NewProjectile(
            player.GetSource_Misc("PortalCreation"),
            worldPos,
            Vector2.Zero,
            ModContent.ProjectileType<PortalCreationProjectile>(),
            0,
            0f,
            // The server creates and destroys this visual. A player-owned identity can
            // collide with that client's projectiles and echo stale updates back to the server.
            Main.maxPlayers,
            creationFrames,
            ownerTeam,
            player.whoAmI
        );

        if (index < 0 || index >= Main.maxProjectiles || Main.projectile[index].ModProjectile is not PortalCreationProjectile creation)
        {
            FinishCreation(player.whoAmI, requestId, completed: false);
            return false;
        }

        creation.Initialize(worldPos, creationFrames, ownerTeam, requestId);
        //Log.Chat($"Portal creation at {worldPos}, team={ownerTeam}, frames={creationFrames}");

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, index);

        return true;
    }

    public static bool CreateOrReplacePortal(Player owner, Vector2 worldPos)
    {
        if (!TravelRules.Enabled)
            return false;

        if (owner?.active != true || Main.netMode == NetmodeID.MultiplayerClient)
            return false;

        RemovePortals(owner.whoAmI);

        int index = NPC.NewNPC(owner.GetSource_Misc("PortalCreation"), (int)worldPos.X, (int)worldPos.Y, ModContent.NPCType<PortalNPC>());

        if (index < 0 || index >= Main.maxNPCs || Main.npc[index].ModNPC is not PortalNPC portal)
            return false;

        portal.Initialize(owner, worldPos);

        Log.Chat($"Portal created at tile position {(int)worldPos.X / 16}, {(int)worldPos.Y / 16}");
        TeleportChat.AnnouncePortalOpened(owner);

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, index);

        return true;
    }

    public static void ClearPortal(int ownerIndex)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        RemovePortals(ownerIndex);
        ClearCreationProjectiles(ownerIndex);
    }

    public static void ClearCreationProjectiles(int ownerIndex, int? requestId = null)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        RemoveCreationProjectiles(ownerIndex, requestId);
    }

    internal static void FinishCreation(int ownerIndex, int requestId, bool completed)
    {
        if (Main.netMode == NetmodeID.Server)
            PortalNetHandler.SendPortalCreationResult(ownerIndex, requestId, completed);
        else
            FinishCreationLocally(ownerIndex, requestId, completed);
    }

    internal static void FinishCreationLocally(int ownerIndex, int requestId, bool completed)
    {
        if (Main.netMode == NetmodeID.Server || ownerIndex < 0 || ownerIndex >= Main.maxPlayers)
            return;

        // Use the attempt ID, not an array slot: packets can arrive after a cancel/retry.
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (projectile?.active == true && projectile.ModProjectile is PortalCreationProjectile creation &&
                creation.OwnerIndex == ownerIndex && creation.RequestId == requestId)
            {
                // Only remove the visual locally. Never send a client KillProjectile for it.
                projectile.active = false;
            }
        }

        Player player = Main.player[ownerIndex];
        if (player?.active != true || !player.GetModPlayer<PortalPlayer>().CreationAttempt.Finish(requestId))
            return;

        if (player.HeldItem?.ModItem is PortalCreatorItem)
            PortalCreatorItem.ResetUseState(player);

        if (!completed && ownerIndex == Main.myPlayer)
        {
            PortalCreatorItem.Warning(player, "Mods.PvPAdventure.PortalCreator.Cancelled");
            TravelTeleportSystem.ClearSelection();
        }
    }

    public static IEnumerable<PortalNPC> ActivePortals()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc?.active == true && npc.ModNPC is PortalNPC portal)
                yield return portal;
        }
    }

    public static bool IsFriendlyPortal(Player player, PortalNPC portal)
    {
        if (player?.active != true || portal == null)
            return false;

        if (portal.OwnerIndex == player.whoAmI)
            return true;

        return player.team > 0 && player.team == portal.OwnerTeam;
    }

    public static bool IsCreatingPortal(Player player)
    {
        if (player?.active != true)
            return false;

        if (player.GetModPlayer<PortalPlayer>().CreationAttempt.Pending)
            return true;

        if (player.itemAnimation > 0 && player.HeldItem?.ModItem is PortalCreatorItem)
            return true;

        return IsCreatingPortal(player.whoAmI);
    }

    public static bool IsCreatingPortal(int ownerIndex)
    {
        if (ownerIndex < 0 || ownerIndex >= Main.maxPlayers)
            return false;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (projectile?.active == true && projectile.ModProjectile is PortalCreationProjectile creation && creation.OwnerIndex == ownerIndex)
                return true;
        }

        return false;
    }

    private static void RemovePortals(int ownerIndex)
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc?.active != true || npc.ModNPC is not PortalNPC portal || portal.OwnerIndex != ownerIndex)
                continue;

            npc.active = false;
            npc.life = 0;

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, i);
        }
    }

    private static void RemoveCreationProjectiles(int ownerIndex, int? requestId = null)
    {
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (projectile?.active != true || projectile.ModProjectile is not PortalCreationProjectile creation ||
                creation.OwnerIndex != ownerIndex || (requestId.HasValue && creation.RequestId != requestId.Value))
                continue;

            creation.Finish(completed: false);
        }
    }
}
