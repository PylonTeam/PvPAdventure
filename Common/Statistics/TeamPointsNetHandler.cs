using ErkySSC.Common.AdminTools;
using ErkySSC.Common.Security;
using PvPAdventure.Core.Net;
using PvPFramework.Common.Game;
using System;
using System.IO;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Statistics;

/// <summary>Only Adventure's team-point edits need Adventure packets; game controls use Framework.</summary>
internal static class TeamPointsNetHandler
{
    public static void Set(Team team, int value)
    {
        if (!AdminToolSecurity.CanUseLocalTools() || !GameSession.Instance.IsSelected("pvpa") || !ValidTeam(team)) return;
        if (Main.netMode != NetmodeID.MultiplayerClient)
        {
            ModContent.GetInstance<PointsManager>().SetTeamPoints(team, value);
            return;
        }
        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.TeamPoints);
        packet.Write((byte)team);
        packet.Write(value);
        packet.Send();
    }

    public static void HandlePacket(BinaryReader reader, int sender)
    {
        if (Main.netMode != NetmodeID.Server || sender < 0 || sender >= Main.maxPlayers ||
            !AdminToolSecurity.CanUseTools(Main.player[sender]) || !GameSession.Instance.IsSelected("pvpa") ||
            JoinAdmissionSystem.RequiresActiveFeatureGate(sender) && !JoinAdmissionSystem.IsActive(sender)) return;
        Team team = (Team)reader.ReadByte();
        int value = reader.ReadInt32();
        if (ValidTeam(team)) ModContent.GetInstance<PointsManager>().SetTeamPoints(team, value);
    }

    private static bool ValidTeam(Team team) => team != Team.None && Enum.IsDefined(team);
}
