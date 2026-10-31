using PvPAdventure.Common.Game.GameReporters;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using PylonMatch.Common.EndScreen;
using PylonMatch.Common.Scoreboard;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

internal static class AdventureEndScreenExtension
{
    public static EndScreenSummary CreateSummary(string presentationKey)
    {
        EndScreenSummary summary = new()
        {
            PresentationKey = presentationKey ?? ""
        };
        PointsManager points = ModContent.GetInstance<PointsManager>();

        IEnumerable<Player> activePlayers = Main.player.Where(player => player?.active == true);
        Team[] activeTeams = activePlayers.Select(player => (Team)player.team)
            .Where(team => team != Team.None).Distinct().OrderBy(team => team).ToArray();

        foreach (Team team in activeTeams)
            summary.Scores.Add(new TeamScoreEntry(team, points.Points.TryGetValue(team, out int score) ? score : 0));

        foreach (Team team in activeTeams)
            summary.Players.AddRange(AssignRoles(activePlayers
                .Where(player => (Team)player.team == team)
                .Select(CreatePlayerStats)
                .OrderByDescending(player => player.Kills)
                .ThenBy(player => player.Deaths)
                .ThenByDescending(player => player.DamageDealt)
                .ToList()));

        foreach (Player player in activePlayers)
            summary.PlayerRewards[(byte)player.whoAmI] = MatchRewardCalculator.Calculate(
                MatchRewardCalculator.CreateContext(player, points));

        return summary;
    }

    private static EndScreenPlayerStats CreatePlayerStats(Player player)
    {
        ScoreboardEntry core = ScoreboardService.GetPlayerStats(player);
        Dictionary<string, uint> stats = StatsReporter.CopyStats(player);
        Dictionary<string, IDictionary<int, uint>> itemStats = StatsReporter.CopyItemStats(player);
        uint Stat(string key) => stats.TryGetValue(key, out uint value) ? value : 0u;

        return new EndScreenPlayerStats(
            (byte)player.whoAmI, (Team)player.team, player.name,
            core.Kills, core.Deaths, Clamp(core.Damage), Stat(StatsReporter.DamageTaken),
            Stat(StatsReporter.TilesMined), Stat(StatsReporter.TilesPlaced),
            Stat(StatsReporter.ConsumablesUsed), Stat(StatsReporter.LavaDeaths),
            Stat(StatsReporter.FoodEaten), Stat(StatsReporter.BossDamageDealt), Stat(StatsReporter.PortalKills),
            CountDifferentWeapons(itemStats), Stat(StatsReporter.LostHoney));
    }

    private static List<EndScreenPlayerStats> AssignRoles(List<EndScreenPlayerStats> players)
    {
        Dictionary<byte, (string Title, string Value)> roles = [];
        AwardHighest(players, roles, player => player.BossDamageDealt, "Boss Breaker", player => $"{Short(player.BossDamageDealt)} boss dmg");
        AwardHighest(players, roles, player => player.PortalKills, "Portal Breaker", player => $"{player.PortalKills} {Plural(player.PortalKills, "portal")}");
        AwardHighest(players, roles, player => player.DifferentWeaponsUsed, "The Arsenal", player => $"{player.DifferentWeaponsUsed} {Plural(player.DifferentWeaponsUsed, "weapon")}");
        AwardHighest(players, roles, player => player.LavaDeaths, "Lava Magnet", player => $"{player.LavaDeaths} lava {Plural(player.LavaDeaths, "death")}");
        AwardHighest(players, roles, player => player.FoodEaten, "Feastmaster", player => $"{player.FoodEaten} food eaten");
        AwardHighest(players, roles, player => player.LostHoney, "Honey Spiller", player => $"{player.LostHoney} honey lost");

        EndScreenPlayerStats survivor = players.Where(player => !roles.ContainsKey(player.PlayerIndex))
            .OrderBy(player => player.Deaths).ThenByDescending(player => player.Kills).FirstOrDefault();
        if (survivor != null)
            roles[survivor.PlayerIndex] = ("Survivor", $"{survivor.Deaths} {Plural((uint)survivor.Deaths, "death")}");

        return players.Select(player => roles.TryGetValue(player.PlayerIndex, out var role)
            ? player with { RoleTitle = role.Title, RoleValue = role.Value }
            : player with { RoleTitle = "Adventurer", RoleValue = "Ready for more" }).ToList();
    }

    private static void AwardHighest(
        List<EndScreenPlayerStats> players,
        Dictionary<byte, (string Title, string Value)> roles,
        Func<EndScreenPlayerStats, uint> value,
        string title,
        Func<EndScreenPlayerStats, string> text)
    {
        EndScreenPlayerStats winner = players
            .Where(player => !roles.ContainsKey(player.PlayerIndex) && value(player) > 0)
            .OrderByDescending(value).ThenByDescending(player => player.Kills).ThenBy(player => player.Deaths)
            .FirstOrDefault();
        if (winner != null)
            roles[winner.PlayerIndex] = (title, text(winner));
    }

    private static uint CountDifferentWeapons(Dictionary<string, IDictionary<int, uint>> itemStats)
    {
        HashSet<int> weapons = [];
        foreach (string key in new[] { StatsReporter.DamageDealt, StatsReporter.BossDamageDealt })
            if (itemStats.TryGetValue(key, out IDictionary<int, uint> byItem))
                foreach ((int itemId, uint amount) in byItem)
                    if (amount > 0 && itemId > ItemID.None && itemId < ItemLoader.ItemCount)
                        weapons.Add(itemId);
        return (uint)weapons.Count;
    }

    private static uint Clamp(long value) => (uint)Math.Clamp(value, 0L, uint.MaxValue);
    private static string Short(uint value) => value >= 1000 ? $"{value / 1000f:0.0}k" : value.ToString();
    private static string Plural(uint value, string word) => value == 1 ? word : word + "s";
}
