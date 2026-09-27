using PvPAdventure.Common.Travel;
using PvPAdventure.Content.Portals;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Portals;

internal sealed class PortalPlayer : ModPlayer
{
    internal PortalCreationAttempt CreationAttempt { get; private set; } = new();

    public override void OnEnterWorld() => CreationAttempt = new();

    public override void PostUpdate()
    {
        if (Player.whoAmI == Main.myPlayer && CreationAttempt.Pending &&
            (Player.dead || Player.ghost || PortalCreatorItem.IsPortalCreationInterrupted(Player)))
        {
            CancelCreation();
        }
    }

    internal void CancelCreation()
    {
        int requestId = CreationAttempt.RequestId;
        if (!CreationAttempt.Pending)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            // Clear the prediction immediately, even if the spawn packet has not arrived yet.
            PortalSystem.FinishCreationLocally(Player.whoAmI, requestId, completed: false);
            PortalNetHandler.SendPortalCreationCancel(requestId);
        }
        else
        {
            PortalSystem.ClearCreationProjectiles(Player.whoAmI, requestId);
        }
    }

    public override bool CanHitNPC(NPC target)
    {
        if (IsFriendlyPortalTarget(Player, target))
            return false;

        return true;
    }

    public override bool? CanHitNPCWithItem(Item item, NPC target)
    {
        if (IsFriendlyPortalTarget(Player, target))
            return false;

        return null;
    }

    public override bool? CanHitNPCWithProj(Projectile proj, NPC target)
    {
        if (IsFriendlyPortalTarget(Player, target))
            return false;

        return null;
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (!info.PvP || !PortalSystem.IsCreatingPortal(Player))
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI != Main.myPlayer)
            return;

        if (Player.whoAmI == Main.myPlayer)
            CancelCreation();
        else if (Main.netMode != NetmodeID.MultiplayerClient)
            PortalSystem.ClearCreationProjectiles(Player.whoAmI);
    }

    public override void Load()
    {
        On_Player.DashMovement += OnPlayerDashMovement;
        On_Player.ApplyDamageToNPC += OnPlayerApplyDamageToNPC;
        On_Player.StrikeNPCDirect += OnPlayerStrikeNPCDirect;
    }

    public override void Unload()
    {
        On_Player.DashMovement -= OnPlayerDashMovement;
        On_Player.ApplyDamageToNPC -= OnPlayerApplyDamageToNPC;
        On_Player.StrikeNPCDirect -= OnPlayerStrikeNPCDirect;
    }

    private static void OnPlayerDashMovement(On_Player.orig_DashMovement orig, Player self)
    {
        List<NPC> ignoredFriendlyPortals = IgnoreFriendlyPortalsForDashCollision(self);

        try
        {
            orig(self);
        }
        finally
        {
            RestoreFriendlyPortalsAfterDashCollision(ignoredFriendlyPortals);
        }
    }

    private static List<NPC> IgnoreFriendlyPortalsForDashCollision(Player player)
    {
        if (player?.active != true)
            return null;

        List<NPC> ignoredFriendlyPortals = null;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc?.active != true ||
                npc.dontTakeDamage ||
                npc.ModNPC is not PortalNPC portal ||
                !PortalSystem.IsFriendlyPortal(player, portal))
            {
                continue;
            }

            ignoredFriendlyPortals ??= new List<NPC>();
            ignoredFriendlyPortals.Add(npc);
            // Vanilla dash impact checks dontTakeDamage before applying the EoC bounceback.
            npc.dontTakeDamage = true;
        }

        return ignoredFriendlyPortals;
    }

    private static void RestoreFriendlyPortalsAfterDashCollision(List<NPC> ignoredFriendlyPortals)
    {
        if (ignoredFriendlyPortals == null)
            return;

        foreach (NPC npc in ignoredFriendlyPortals)
        {
            if (npc != null)
                npc.dontTakeDamage = false;
        }
    }

    private static void OnPlayerApplyDamageToNPC(
        On_Player.orig_ApplyDamageToNPC orig,
        Player self,
        NPC npc,
        int damage,
        float knockback,
        int direction,
        bool crit,
        DamageClass damageType,
        bool damageVariation)
    {
        if (IsFriendlyPortalTarget(self, npc))
            return;

        orig(self, npc, damage, knockback, direction, crit, damageType, damageVariation);
    }

    private static void OnPlayerStrikeNPCDirect(
        On_Player.orig_StrikeNPCDirect orig,
        Player self,
        NPC npc,
        NPC.HitInfo hit)
    {
        if (IsFriendlyPortalTarget(self, npc))
            return;

        orig(self, npc, hit);
    }

    private static bool IsFriendlyPortalTarget(Player player, NPC target)
    {
        return target?.ModNPC is PortalNPC portal && PortalSystem.IsFriendlyPortal(player, portal);
    }
}
