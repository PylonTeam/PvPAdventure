using Microsoft.Xna.Framework;
using PvPAdventure.Content.Portals;
using ErkySSC.Common.SSC;
using PvPAdventure.Core.Net;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Team = Terraria.Enums.Team;

namespace PvPAdventure.Common.Travel.Beds;

internal enum TeamBedPacketType : byte
{
    BedUpdate,
    PlayerSpawn,
    DestroyAttempt,
    BedDestroyFx,
    StateRequest,
}

public static class TeamBedNetHandler
{
    public static void HandlePacket(BinaryReader reader, int whoAmI)
    {
        TeamBedPacketType type = (TeamBedPacketType)reader.ReadByte();

        switch (type)
        {
            case TeamBedPacketType.BedUpdate:
                ReceiveBedUpdate(reader);
                break;

            case TeamBedPacketType.PlayerSpawn:
                ReceivePlayerSpawn(reader, whoAmI);
                break;

            case TeamBedPacketType.DestroyAttempt:
                ReceiveDestroyAttempt(reader, whoAmI);
                break;

            case TeamBedPacketType.BedDestroyFx:
                ReceiveBedDestructionFx(reader);
                break;

            case TeamBedPacketType.StateRequest:
                ReceiveStateRequest(whoAmI);
                break;
        }
    }

    public static void SendStateRequest()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.TeamBed);
        packet.Write((byte)TeamBedPacketType.StateRequest);
        packet.Send();
    }

    private static void ReceiveStateRequest(int whoAmI)
    {
        if (Main.netMode != NetmodeID.Server || whoAmI < 0 || whoAmI >= Main.maxPlayers)
            return;

        ModContent.GetInstance<TeamBedSystem>().SendAllStateToClient(whoAmI);
    }

    public static void SendBedDestructionFx(float worldX, float worldY, bool killed)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            PortalNPC.PlayPortalFx(new(worldX, worldY), killed);
            return;
        }

        if (Main.netMode != NetmodeID.Server)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.TeamBed);
        packet.Write((byte)TeamBedPacketType.BedDestroyFx);
        packet.Write(worldX);
        packet.Write(worldY);
        packet.Write(killed);
        packet.Send();
    }

    private static void ReceiveBedDestructionFx(BinaryReader reader)
    {
        float worldX = reader.ReadSingle();
        float worldY = reader.ReadSingle();
        bool killed = reader.ReadBoolean();

        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        PortalNPC.PlayPortalFx(new(worldX, worldY), killed);
    }

    private static void ReceiveBedUpdate(BinaryReader reader)
    {
        Point origin = new(reader.ReadInt32(), reader.ReadInt32());
        Team team = (Team)reader.ReadByte();

        if (Main.netMode == NetmodeID.MultiplayerClient)
            ModContent.GetInstance<TeamBedSystem>().SetFromNet(origin, team);
    }

    private static void ReceivePlayerSpawn(BinaryReader reader, int whoAmI)
    {
        int playerId = reader.ReadByte();
        int spawnX = reader.ReadInt32();
        int spawnY = reader.ReadInt32();

        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId] is not { active: true } player)
            return;

        if (!IsSafeSpawnPair(spawnX, spawnY))
            return;

        if (Main.netMode == NetmodeID.Server)
        {
            // SSC owns client bed changes and saving; Adventure cannot bypass its admission checks.
            if (playerId != whoAmI || SSCBedSystem.IsEnabled)
                return;

            if (spawnX >= 0)
            {
                if (!Player.CheckSpawn(spawnX, spawnY))
                    return;

                bool changed = player.SpawnX != spawnX || player.SpawnY != spawnY;
                Vector2 spawnPosition = new(spawnX * 16f + 8f, spawnY * 16f);
                if (changed && Vector2.DistanceSquared(player.Center, spawnPosition) > 160f * 160f)
                    return;
            }
        }
        else if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            return;
        }

        // These methods update Terraria's per-world bed history as well as the live spawn fields.
        if (spawnX == -1)
            player.RemoveSpawn();
        else
            player.ChangeSpawn(spawnX, spawnY);

        if (Main.netMode != NetmodeID.Server)
            return;

        TeamBedSystem.SendPlayerSpawn(playerId, spawnX, spawnY, ignoreClient: whoAmI);
        ModContent.GetInstance<TeamBedSystem>().UpdateFromPlayer(player);
    }

    private static bool IsSafeSpawnPair(int spawnX, int spawnY) =>
        (spawnX == -1 && spawnY == -1) ||
        (spawnX >= 0 && spawnY >= 0 && WorldGen.InWorld(spawnX, spawnY, 10));

    public static void SendDestroyAttempt(Point origin)
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.TeamBed);
        packet.Write((byte)TeamBedPacketType.DestroyAttempt);
        packet.Write(origin.X);
        packet.Write(origin.Y);
        packet.Send();
    }

    private static void ReceiveDestroyAttempt(BinaryReader reader, int whoAmI)
    {
        Point origin = new(reader.ReadInt32(), reader.ReadInt32());

        if (Main.netMode != NetmodeID.Server)
            return;

        ModContent.GetInstance<TeamBedSystem>().SetCurrentBedTarget(whoAmI, origin);
    }
}
