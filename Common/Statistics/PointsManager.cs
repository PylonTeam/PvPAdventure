using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Bounties;
using PvPAdventure.Common.Game.StatTrackers;
using BossDisplaySettings = PvPFramework.Common.NPCs.BossDisplaySettings;
using PvPAdventure.Common.NPCs;
using PvPAdventure.Core.Config;
using PvPFramework.Common.Scoreboard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Chat;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace PvPAdventure.Common.Statistics;

[Autoload(Side = ModSide.Both)]
public class PointsManager : ModSystem
{
    public readonly Dictionary<Team, int> _points = new();
    private readonly Dictionary<Team, ISet<short>> _downedNpcs = new();

    public BossCompletionInterfaceLayer BossCompletion { get; private set; }

    public IReadOnlyDictionary<Team, int> Points => _points;
    public IReadOnlyDictionary<Team, ISet<short>> DownedNpcs => _downedNpcs;

    public override void Load()
    {
        if (!Main.dedServ)
        {
            BossCompletion = new(this) { Active = false };
        }
    }

    public override void ClearWorld() => ResetForMatch();

    /// <summary>Resets all score and boss-completion state that belongs to one match.</summary>
    public void ResetForMatch()
    {
        _points.Clear();
        _downedNpcs.Clear();

        int startingPoints = ModContent.GetInstance<ServerConfig>().Points.TeamStartingPoints;

        foreach (Team team in Enum.GetValues<Team>())
        {
            if (team == Team.None)
                _points[team] = 0;
            else
                _points[team] = startingPoints;

            _downedNpcs[team] = new HashSet<short>();
        }
    }

    public void AwardPointsToTeam(Team team, int points, Vector2 at, string defeated = null)
    {
        if (team == Team.None || points == 0) return;

        _points[team] += points;
        VisualizePointChange(points, team, at, defeated);

        if (Main.dedServ)
            NetMessage.SendData(MessageID.WorldData);
    }

    public override void SaveWorldData(TagCompound tag)
    {
        int teamCount = Enum.GetValues<Team>().Length;

        var points = new int[teamCount];
        foreach (Team team in Enum.GetValues<Team>())
            points[(int)team] = _points[team];

        var downedNpcs = new int[teamCount][];
        foreach (Team team in Enum.GetValues<Team>())
            downedNpcs[(int)team] = _downedNpcs[team].Select(id => (int)id).ToArray();

        tag["points"] = points;
        tag["downedNpcs"] = downedNpcs.ToList();
    }

    public override void LoadWorldData(TagCompound tag)
    {
        // Backwards compat: if this world predates the system, fall back to config defaults.
        if (!tag.ContainsKey("points") || !tag.ContainsKey("downedNpcs"))
        {
            ClearWorld();
            return;
        }

        var points = (int[])tag["points"];
        for (int i = 0; i < points.Length; i++)
            _points[(Team)i] = points[i];

        var downedNpcs = (List<int[]>)tag["downedNpcs"];
        for (int i = 0; i < downedNpcs.Count; i++)
            _downedNpcs[(Team)i] = downedNpcs[i].Select(id => (short)id).ToHashSet();

        // Ensure all enum values exist even if save data is from an older version (added teams, etc.)
        int startingPoints = ModContent.GetInstance<ServerConfig>().Points.TeamStartingPoints;
        foreach (Team team in Enum.GetValues<Team>())
        {
            if (!_points.ContainsKey(team))
            {
                if (team == Team.None)
                    _points[team] = 0;
                else
                    _points[team] = startingPoints;
            }

            if (!_downedNpcs.ContainsKey(team))
                _downedNpcs[team] = new HashSet<short>();
        }
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(_points.Count);
        foreach (var (team, teamPoints) in _points)
        {
            writer.Write((int)team);
            writer.Write(teamPoints);
        }

        writer.Write(_downedNpcs.Count);
        foreach (var (team, downedNpcs) in _downedNpcs)
        {
            writer.Write((int)team);
            writer.Write(downedNpcs.Count);
            foreach (var id in downedNpcs)
                writer.Write(id);
        }
    }

    public override void NetReceive(BinaryReader reader)
    {
        _points.Clear();
        _downedNpcs.Clear();

        var numberOfPointEntries = reader.ReadInt32();
        for (var i = 0; i < numberOfPointEntries; i++)
        {
            var team = (Team)reader.ReadInt32();
            var points = reader.ReadInt32();
            _points[team] = points;
        }

        var numberOfDownedNpcsEntries = reader.ReadInt32();
        for (var i = 0; i < numberOfDownedNpcsEntries; i++)
        {
            var team = (Team)reader.ReadInt32();
            _downedNpcs[team] = new HashSet<short>();

            var numberOfIdEntries = reader.ReadInt32();
            for (var j = 0; j < numberOfIdEntries; j++)
                _downedNpcs[team].Add(reader.ReadInt16());
        }

    }

    public override void ModifyInterfaceLayers(
    List<GameInterfaceLayer> layers)
    {
        if (BossCompletion == null ||
            layers.Contains(BossCompletion))
        {
            return;
        }

        int playerInfoIndex = layers.FindIndex(layer => layer.Name == "PvPFramework: Scoreboard Player Info");

        if (playerInfoIndex != -1)
        {
            // Scoreboard has already updated Bounds, but player info has
            // not drawn yet.
            layers.Insert(
                playerInfoIndex,
                BossCompletion);

            return;
        }

        int scoreboardIndex = layers.FindIndex(layer => layer.Name == "PvPFramework: Scoreboard");
        if (scoreboardIndex != -1)
        {
            layers.Insert(
                scoreboardIndex + 1,
                BossCompletion);
        }
    }

    private void VisualizePointChange(int change, Team team, Vector2 at, string defeated = null)
    {
        var firstPersonMessage = NetworkText.FromLiteral(FormatPointsVisually(change));
        NetworkText thirdPersonMessage = null;

        if (change > 0 && defeated != null)
            thirdPersonMessage =
                NetworkText.FromLiteral(
                    $"{team} Team awarded +{change} point{(change == 1 ? "" : "s")} for defeating {defeated}!");

        NetMessage.SendData(MessageID.CombatTextString,
            text: firstPersonMessage,
            number: (int)Main.teamColor[(int)team].PackedValue,
            number2: at.X,
            number3: at.Y
        );

        foreach (var player in Main.ActivePlayers)
        {
            if ((Team)player.team == team)
                ChatHelper.SendChatMessageToClient(firstPersonMessage, Main.teamColor[(int)team], player.whoAmI);
            else if (thirdPersonMessage != null)
                ChatHelper.SendChatMessageToClient(thirdPersonMessage, Main.teamColor[(int)team], player.whoAmI);
        }
    }

    public void AwardNpcKillToTeam(Team team, NPC npc)
    {
        var config = ModContent.GetInstance<ServerConfig>();

        // Is this NPC assigned custom point values?
        if (!config.Points.Npc.TryGetValue(new NPCDefinition(npc.type), out var points))
        {
            // No, but they might be a boss.
            if (!npc.boss)
                return;

            points = config.Points.Boss;
        }

        var hasBeenDownedByThisTeam = _downedNpcs[team].Contains((short)npc.type);

        // This team has already downed this NPC, and it is not repeatable, don't award any points.
        if (hasBeenDownedByThisTeam && !points.Repeatable)
            return;

        var hasBeenDownedByAnyTeam = hasBeenDownedByThisTeam ||
                                     _downedNpcs.Values.Any(downedNpcs => downedNpcs.Contains((short)npc.type));

        var pointsToAward = hasBeenDownedByAnyTeam ? points.Additional : points.First;
        _points[team] += pointsToAward;

        _downedNpcs[team].Add((short)npc.type);

        // If this is part of the Eater of Worlds, mark ALL parts as defeated.
        // This specialization is not needed for The Destroyer -- only it's head is ever marked as a boss.
        if (BossPartUtilities.IsPartOfEaterOfWorlds((short)npc.type))
        {
            _downedNpcs[team].Add(NPCID.EaterofWorldsHead);
            _downedNpcs[team].Add(NPCID.EaterofWorldsBody);
            _downedNpcs[team].Add(NPCID.EaterofWorldsTail);
        }

        string fullName;

        if (npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism)
        {
            _downedNpcs[team].Add(NPCID.Spazmatism);
            _downedNpcs[team].Add(NPCID.Retinazer);
            fullName = "The Twins";
        }
        else
        {
            fullName = npc.FullName;
        }

        VisualizePointChange(pointsToAward, team, npc.position, $"[c/F58522:{fullName}]");

        if (Main.dedServ)
            NetMessage.SendData(MessageID.WorldData);
    }

    public void AwardPlayerKillToTeam(Player killer, Player victim)
    {
        var config = ModContent.GetInstance<ServerConfig>();
        var killerTeam = (Team)killer.team;

        // Even if certain oddities allowed this to happen, no point exchanging would actually occur.
        if (killerTeam == (Team)victim.team)
            return;

        var victimTeamPoints = _points[(Team)victim.team];
        var killerTeamPints = _points[killerTeam];

        // Find the lowest denomination of points we can take (can't take more than the other team has!)
        var pointsToTrade = Math.Min(victimTeamPoints, config.Points.PlayerKill);

        if (pointsToTrade > 0)
        {
            _points[(Team)victim.team] -= pointsToTrade;
            _points[killerTeam] += pointsToTrade;

            MatchStatsPlayer.RecordServerStat(killer, MatchStatKey.PointKills);
            MatchStatsPlayer.RecordServerStat(victim, MatchStatKey.PointDeaths);

            VisualizePointChange(pointsToTrade, (Team)killer.team, killer.position,
                $"[c/{Main.teamColor[victim.team].Hex3()}:{victim.name}]");
            VisualizePointChange(-pointsToTrade, (Team)victim.team, victim.position);

            if (Main.dedServ)
                NetMessage.SendData(MessageID.WorldData);
        }

        if (config.Bounties.AwardBountyEveryKill || victimTeamPoints > killerTeamPints)
            ModContent.GetInstance<BountyManager>().Award(killer, victim);

    }

    public void SetTeamPoints(Team team, int points)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || team == Team.None || !Enum.IsDefined(team)) return;
        _points[team] = points;

        if (Main.netMode == NetmodeID.Server)
        {
            NetMessage.SendData(MessageID.WorldData);
        }
    }

    public class BossCompletionInterfaceLayer(PointsManager pointsManager)
        : GameInterfaceLayer("PvPAdventure: Boss Completion", InterfaceScaleType.None)
    {
        protected override bool DrawSelf()
        {
            const int scoreboardPadding = 30;
            const int horizontalSpaceBetweenBossHeads = 50;
            const int verticalSeparationBetweenBossHeadAndTeamIcon = 40;
            const int verticalSpaceBetweenTeamIcons = 35;
            const int containerPadding = 35;
            const int teamIconXOffset = 8;
            const int bossHeadTeamIconVisualSeparatorYOffset = 28;
            var teamIconsTexture = TextureAssets.Pvp[1].Value;

            var bosses = BossDisplaySettings.Order
                // Remove invalid/unloaded NPCs
                .Where(id => id != -1)
                .ToList();
            var numberOfBosses = bosses.Count;

            var onlyDisplayWorldEvilBoss = BossDisplaySettings.OnlyDisplayWorldEvilBoss &&
                                           bosses.Contains(NPCID.EaterofWorldsHead) &&
                                           bosses.Contains(NPCID.BrainofCthulhu);

            // We might have both evil bosses in this list, but we actually just want to display the one that
            // pertains to this world.
            if (onlyDisplayWorldEvilBoss)
                numberOfBosses -= 1;

            // FIXME: The left and right padding visually based on the team icons is somewhat off
            //        (too much right padding from team icon)
            // FIXME: Constant height is good, but should be calculated based on number of teams
            //        (which is also a constant), which would remove the "none" team and the height for it's indicator.
            var containerSize =
                new Vector2((containerPadding + (numberOfBosses * horizontalSpaceBetweenBossHeads)) - teamIconXOffset,
                    245);

            // Cast to int so no strange aliasing occurs on anything we render -- we only work with whole numbers in the end.
            //int hoveredPlayerOffset = ScoreboardUISystem.HoveringAnotherPlayer ? 200 : 0;
            var containerPosition = new Vector2((int)((Main.screenWidth / 2.0f) - (containerSize.X / 2.0f)),
                ScoreboardUISystem.Bounds.Bottom + scoreboardPadding);

            Utils.DrawInvBG(Main.spriteBatch, containerPosition.X, containerPosition.Y, containerSize.X,
                containerSize.Y);

            var nextBossHeadPosition = new Vector2(containerPosition.X + containerPadding,
                containerPosition.Y + containerPadding);

            // FIXME: This alpha doesn't seem to work.
            var visualSeparatorColor = Color.White with { A = 60 };
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value,
                new Rectangle((int)(containerPosition.X + 2),
                    (int)(nextBossHeadPosition.Y + bossHeadTeamIconVisualSeparatorYOffset), (int)(containerSize.X - 4),
                    2), visualSeparatorColor);

            foreach (var bossId in bosses)
            {
                if (onlyDisplayWorldEvilBoss)
                {
                    if (bossId == NPCID.BrainofCthulhu && !WorldGen.crimson ||
                        bossId == NPCID.EaterofWorldsHead && WorldGen.crimson)
                        continue;
                }

                // FIXME: Stupid hack for Golem head texture
                var headId = NPCID.Sets.BossHeadTextures[bossId == NPCID.Golem ? NPCID.GolemHead : bossId];

                var hasAnyTeamDownedThisBoss = pointsManager
                    .DownedNpcs
                    .Values
                    .Any(downedNpcs => downedNpcs.Contains(bossId));

                // If we don't have a texture, we just won't render one, but we'll keep the gap and indicators.
                if (headId != -1)
                    Main.BossNPCHeadRenderer.DrawWithOutlines(null, headId, nextBossHeadPosition,
                        hasAnyTeamDownedThisBoss ? Color.White : Color.Gray, 0.0f, 1.0f, SpriteEffects.None);

                var nextTeamIconPosition = new Vector2(nextBossHeadPosition.X - teamIconXOffset,
                    nextBossHeadPosition.Y + verticalSeparationBetweenBossHeadAndTeamIcon);

                foreach (var team in Enum.GetValues<Team>())
                {
                    if (team == Team.None)
                        continue;

                    if (!pointsManager.DownedNpcs[team].Contains(bossId))
                        continue;

                    Main.spriteBatch.Draw(teamIconsTexture, nextTeamIconPosition,
                        teamIconsTexture.Frame(6, 1, (int)team), Color.White);

                    nextTeamIconPosition.Y += verticalSpaceBetweenTeamIcons;
                }

                nextBossHeadPosition.X += horizontalSpaceBetweenBossHeads;
            }

            return base.DrawSelf();
        }
    }

    public class SetPointsCommand : ModCommand
    {
        public override void Action(CommandCaller caller, string input, string[] args)
        {
            // You can only use this command from chat in singleplayer.
            if (caller.CommandType == CommandType.Chat && Main.netMode != NetmodeID.SinglePlayer)
                return;

            if (args.Length < 2)
                return;

            if (!Enum.TryParse(args[0], true, out Team team) || (int)team >= Enum.GetValues<Team>().Length)
            {
                caller.Reply("Invalid team.", Color.Red);
                return;
            }

            if (!int.TryParse(args[1], out var points))
            {
                caller.Reply("Invalid points.", Color.Red);
                return;
            }

            var pointsManager = ModContent.GetInstance<PointsManager>();
            pointsManager._points[team] = points;

            if (Main.dedServ)
                NetMessage.SendData(MessageID.WorldData);
        }

        public override string Command => "setpoints";
        public override CommandType Type => CommandType.Chat | CommandType.Console;
    }

    private static string FormatPointsVisually(int points) =>
        $"{points:+0;-#} point{(Math.Abs(points) == 1 ? "" : "s")}";
}
