using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Net;
using PvPAdventure.Core.Utilities;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Portals;

public static class PortalNetHandler
{
    private enum PortalPacketType : byte
    {
        PortalCreatorUse,
        PortalHitFx,
        PortalDamageCredit,
        PortalCreationCancel,
        PortalCreationResult
    }

    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        PortalPacketType type = (PortalPacketType)reader.ReadByte();

        switch (type)
        {
            case PortalPacketType.PortalCreatorUse:
                ReceivePortalCreatorUse(reader, whoAmI);
                break;

            case PortalPacketType.PortalHitFx:
                ReceivePortalHitFx(reader, whoAmI);
                break;

            case PortalPacketType.PortalDamageCredit:
                ReceivePortalDamageCredit(reader, whoAmI);
                break;

            case PortalPacketType.PortalCreationCancel:
                ReceivePortalCreationCancel(reader, whoAmI);
                break;

            case PortalPacketType.PortalCreationResult:
                ReceivePortalCreationResult(reader);
                break;

            default:
                Log.Warn($"[Portal] Unknown packet type={(byte)type}");
                break;
        }
    }

    public static void SendPortalCreationCancel(int requestId)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.UsePortal);
        packet.Write((byte)PortalPacketType.PortalCreationCancel);
        packet.Write((byte)Main.myPlayer);
        packet.Write(requestId);
        packet.Send();
    }

    private static void ReceivePortalCreationCancel(BinaryReader reader, int whoAmI)
    {
        byte playerId = reader.ReadByte();
        int requestId = reader.ReadInt32();

        if (Main.netMode != NetmodeID.Server)
            return;

        if (playerId != whoAmI)
        {
            Log.Chat($"Portal creation cancel rejected: sender mismatch (packet={playerId}, caller={whoAmI})");
            return;
        }

        PortalSystem.ClearCreationProjectiles(playerId, requestId);
    }

    internal static void SendPortalCreationResult(int playerId, int requestId, bool completed)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.UsePortal);
        packet.Write((byte)PortalPacketType.PortalCreationResult);
        packet.Write((byte)playerId);
        packet.Write(requestId);
        packet.Write(completed);
        packet.Send();
    }

    private static void ReceivePortalCreationResult(BinaryReader reader)
    {
        int playerId = reader.ReadByte();
        int requestId = reader.ReadInt32();
        bool completed = reader.ReadBoolean();

        if (Main.netMode == NetmodeID.MultiplayerClient)
            PortalSystem.FinishCreationLocally(playerId, requestId, completed);
    }

    public static void SendPortalDamageCredit(int npcIndex)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.UsePortal);
        packet.Write((byte)PortalPacketType.PortalDamageCredit);
        packet.Write((short)npcIndex);
        packet.Send();
    }

    private static void ReceivePortalDamageCredit(BinaryReader reader, int whoAmI)
    {
        int npcIndex = reader.ReadInt16();

        if (Main.netMode != NetmodeID.Server)
            return;

        if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            return;

        NPC npc = Main.npc[npcIndex];

        if (npc?.active != true || npc.ModNPC is not PortalNPC portal)
            return;

        portal.SetLastDamagePlayerFromNetwork(whoAmI);
    }

    public static void SendPortalHitFx(int npcIndex, bool killed)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return;

        SendPortalHitFx(npcIndex, killed, -1);
    }

    private static void SendPortalHitFx(int npcIndex, bool killed, int ignoreClient)
    {
        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.UsePortal);
        packet.Write((byte)PortalPacketType.PortalHitFx);
        packet.Write((short)npcIndex);
        packet.Write(killed);
        packet.Send(ignoreClient: ignoreClient);
    }

    private static void ReceivePortalHitFx(BinaryReader reader, int whoAmI)
    {
        int npcIndex = reader.ReadInt16();
        bool killed = reader.ReadBoolean();

        if (Main.netMode == NetmodeID.Server)
        {
            SendPortalHitFx(npcIndex, killed, whoAmI);
            return;
        }

        if (npcIndex < 0 || npcIndex >= Main.maxNPCs)
            return;

        NPC npc = Main.npc[npcIndex];

        if (npc?.active == true && npc.ModNPC is PortalNPC portal)
            portal.PlayHitFxFromNetwork(killed);
    }

    public static void SendPortalCreatorUse(int slot, int requestId)
    {
        if (!TravelRules.Enabled)
            return;

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        // These travel over the same ordered connection. Validate against the movement
        // state that started this attempt, rather than the previous tick's moving state.
        NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Main.myPlayer);

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.UsePortal);
        packet.Write((byte)PortalPacketType.PortalCreatorUse);
        packet.Write((byte)Main.myPlayer);
        packet.Write((byte)slot);
        packet.Write(requestId);
        packet.Send();
    }

    private static void ReceivePortalCreatorUse(BinaryReader reader, int whoAmI)
    {
        byte playerId = reader.ReadByte();
        byte slot = reader.ReadByte();
        int requestId = reader.ReadInt32();

        if (Main.netMode == NetmodeID.Server)
        {
            if (playerId != whoAmI)
            {
                Log.Chat($"Portal creation request rejected: sender mismatch (packet={playerId}, caller={whoAmI})");
                return;
            }

            if (!TryGetPortalCreator(playerId, slot, out Player player, out _))
            {
                Log.Chat($"Portal creation request rejected: invalid portal creator request (player={playerId}, slot={slot})");
                SendPortalCreationResult(playerId, requestId, completed: false);
                return;
            }

            if (!PortalCreatorItem.CanCreatePortal(player, false))
            {
                Log.Chat($"Portal creation request rejected: player {playerId} failed portal validation");
                SendPortalCreationResult(playerId, requestId, completed: false);
                return;
            }

            Log.Chat("Portal creation request received");
            if (PortalSystem.StartPortalCreation(player, requestId))
                SendPortalCreatorUse(playerId, slot, requestId, whoAmI);
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient || playerId == Main.myPlayer)
            return;

        if (!TryGetRemotePlayer(playerId, slot, out Player remotePlayer, out Item item))
            return;

        PortalCreatorItem.SetPortalCreationTime(item);
        remotePlayer.GetModPlayer<PortalPlayer>().CreationAttempt.Track(requestId);

        remotePlayer.selectedItem = slot;
        remotePlayer.itemAnimation = item.useAnimation;
        remotePlayer.itemAnimationMax = item.useAnimation;
        remotePlayer.itemTime = item.useTime;
        remotePlayer.itemTimeMax = item.useTime;
    }

    private static void SendPortalCreatorUse(int playerId, int slot, int requestId, int ignoreClient)
    {
        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.UsePortal);
        packet.Write((byte)PortalPacketType.PortalCreatorUse);
        packet.Write((byte)playerId);
        packet.Write((byte)slot);
        packet.Write(requestId);
        packet.Send(ignoreClient: ignoreClient);
    }

    private static bool TryGetPortalCreator(int playerId, int slot, out Player player, out Item item)
    {
        player = null;
        item = null;

        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId] is not { active: true } foundPlayer)
            return false;

        if (slot < 0 || slot >= foundPlayer.inventory.Length)
            return false;

        item = foundPlayer.inventory[slot];

        if (item?.ModItem is not PortalCreatorItem)
            return false;

        player = foundPlayer;
        return true;
    }

    private static bool TryGetRemotePlayer(int playerId, int slot, out Player player, out Item item)
    {
        player = null;
        item = null;

        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId] is not { active: true } foundPlayer)
            return false;

        if (slot < 0 || slot >= foundPlayer.inventory.Length)
            return false;

        player = foundPlayer;
        item = foundPlayer.inventory[slot];

        if (item?.ModItem is not PortalCreatorItem)
            item.SetDefaults(ModContent.ItemType<PortalCreatorItem>());

        return true;
    }
}
