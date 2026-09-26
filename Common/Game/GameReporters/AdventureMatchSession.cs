using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using PvPFramework.Common.Scoreboard;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Enums;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game.GameReporters;

/// <summary>
/// Server-owned state for one match. The session keeps authenticated participants after they
/// disconnect and accumulates per-connection deltas so reconnecting players are not counted twice.
/// </summary>
internal sealed class AdventureMatchSession
{
    private readonly Dictionary<ulong, ParticipantState> participants = [];
    private readonly Dictionary<int, LivePlayerSegment> liveSegments = [];

    public AdventureMatchSession(DateTime startUtc)
    {
        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        Token = Guid.NewGuid().ToString("N");
    }

    public string Token { get; }
    public DateTime StartUtc { get; }

    /// <summary>
    /// Samples known players every tick. Authenticated-player discovery is intentionally less
    /// frequent because failed Steam identity lookups are noisy while a player is joining.
    /// </summary>
    public void CaptureActivePlayers(bool discoverPlayers)
    {
        HashSet<int> activeSlots = [];

        foreach (Player player in Main.ActivePlayers)
        {
            if (player?.active != true || player.whoAmI is < 0 or >= Main.maxPlayers)
                continue;

            int slot = player.whoAmI;
            activeSlots.Add(slot);

            if (liveSegments.TryGetValue(slot, out LivePlayerSegment segment))
            {
                if (discoverPlayers && PylonService.TryGetSteamId(player, out ulong currentSteamId) &&
                    currentSteamId != segment.SteamId)
                {
                    Log.Warn($"Player slot {slot} changed authenticated identity during a match. " +
                             $"OldSteamId={segment.SteamId}, NewSteamId={currentSteamId}");
                    liveSegments.Remove(slot);
                    TryEnroll(player, currentSteamId);
                    continue;
                }

                segment.Capture(player, participants[segment.SteamId]);
                continue;
            }

            if (!discoverPlayers || (Team)player.team == Team.None ||
                !PylonService.TryGetSteamId(player, out ulong steamId))
                continue;

            TryEnroll(player, steamId);
        }

        foreach (int slot in liveSegments.Keys.Where(slot => !activeSlots.Contains(slot)).ToArray())
            liveSegments.Remove(slot);
    }

    public void CaptureDisconnectingPlayer(Player player)
    {
        if (player == null)
            return;

        if (liveSegments.Remove(player.whoAmI, out LivePlayerSegment segment))
        {
            if (participants.TryGetValue(segment.SteamId, out ParticipantState participant))
                segment.Capture(player, participant);
            return;
        }

        // Covers a player who authenticated, played, and disconnected between discovery samples.
        if (PylonService.TryGetSteamId(player, out ulong steamId))
        {
            TryEnroll(player, steamId);
            if (liveSegments.Remove(player.whoAmI, out segment) &&
                participants.TryGetValue(segment.SteamId, out ParticipantState participant))
            {
                segment.Capture(player, participant);
            }
        }
    }

    public CompletedAdventureMatch Complete(DateTime endUtc, PointsManager pointsManager)
    {
        CaptureActivePlayers(discoverPlayers: true);

        Dictionary<Team, CompletedAdventureTeam> teams = BuildTeams(pointsManager);
        HashSet<Team> participatingTeams = participants.Values
            .Select(participant => participant.Team)
            .Where(team => team != Team.None)
            .ToHashSet();
        bool hasWinner = TryGetSingleWinningTeam(teams, participatingTeams, out Team winningTeam);
        Dictionary<ulong, CompletedAdventurePlayer> players = [];

        foreach ((ulong steamId, ParticipantState participant) in participants)
        {
            int teamPoints = teams.TryGetValue(participant.Team, out CompletedAdventureTeam team)
                ? team.Points
                : 0;
            MatchRewardContext rewardContext = new(
                participant.Team,
                teamPoints,
                participant.Kills,
                participant.Deaths);

            players[steamId] = participant.ToCompleted(
                MatchRewardCalculator.Calculate(rewardContext),
                hasWinner && participant.Team == winningTeam);
        }

        return new CompletedAdventureMatch(
            Token,
            StartUtc,
            DateTime.SpecifyKind(endUtc, DateTimeKind.Utc),
            players,
            teams);
    }

    private void TryEnroll(Player player, ulong steamId)
    {
        Team playerTeam = (Team)player.team;
        if (steamId == 0 || playerTeam == Team.None || !Enum.IsDefined(playerTeam))
            return;

        bool isNewParticipant = !participants.TryGetValue(steamId, out ParticipantState participant);
        if (isNewParticipant)
        {
            participant = new ParticipantState(player.name, playerTeam);
            participants.Add(steamId, participant);
            Log.Info($"Enrolled match participant. MatchToken={Token}, Player={player.name}, SteamId={steamId}, Team={playerTeam}");
        }

        participant.UpdateIdentity(player);
        LivePlayerSegment segment = new(steamId, player, startFromZero: isNewParticipant);
        liveSegments[player.whoAmI] = segment;
        segment.Capture(player, participant);
    }

    private static Dictionary<Team, CompletedAdventureTeam> BuildTeams(PointsManager pointsManager)
    {
        Dictionary<Team, CompletedAdventureTeam> result = [];

        foreach (Team team in Enum.GetValues<Team>())
        {
            if (team == Team.None)
                continue;

            int points = pointsManager?.Points.TryGetValue(team, out int currentPoints) == true
                ? currentPoints
                : 0;
            List<short> bosses = pointsManager?.DownedNpcs.TryGetValue(team, out ISet<short> downedNpcs) == true
                ? downedNpcs.OrderBy(id => id).ToList()
                : [];
            result[team] = new CompletedAdventureTeam(points, bosses);
        }

        return result;
    }

    private static bool TryGetSingleWinningTeam(
        IReadOnlyDictionary<Team, CompletedAdventureTeam> teams,
        IReadOnlySet<Team> participatingTeams,
        out Team winningTeam)
    {
        winningTeam = Team.None;
        if (participatingTeams.Count == 0)
            return false;

        KeyValuePair<Team, CompletedAdventureTeam>[] candidates = teams
            .Where(pair => participatingTeams.Contains(pair.Key))
            .ToArray();
        if (candidates.Length == 0)
            return false;

        int highestPoints = candidates.Max(pair => pair.Value.Points);
        Team[] leaders = candidates
            .Where(pair => pair.Value.Points == highestPoints)
            .Select(pair => pair.Key)
            .ToArray();

        if (leaders.Length != 1)
            return false;

        winningTeam = leaders[0];
        return true;
    }

    private sealed class ParticipantState(string name, Team team)
    {
        private bool loggedTeamChange;

        public string Name { get; private set; } = name;
        public Team Team { get; } = team;
        public int Kills { get; private set; }
        public int Deaths { get; private set; }
        public Dictionary<string, uint> Stats { get; } = [];
        public Dictionary<string, Dictionary<int, uint>> ItemStats { get; } = [];

        public void UpdateIdentity(Player player)
        {
            if (!string.IsNullOrWhiteSpace(player.name))
                Name = player.name;

            Team currentTeam = (Team)player.team;
            if (!loggedTeamChange && currentTeam != Team.None && currentTeam != Team)
            {
                loggedTeamChange = true;
                Log.Warn($"Match participant changed teams; reporting the original team. " +
                         $"Player={Name}, OriginalTeam={Team}, CurrentTeam={currentTeam}");
            }
        }

        public void AddScore(int kills, int deaths)
        {
            Kills = AddClamped(Kills, kills);
            Deaths = AddClamped(Deaths, deaths);
        }

        public void AddStat(string key, uint amount)
        {
            if (string.IsNullOrWhiteSpace(key) || amount == 0)
                return;

            Stats.TryGetValue(key, out uint current);
            Stats[key] = AddClamped(current, amount);
        }

        public void AddItemStat(string statKey, int itemKey, uint amount)
        {
            if (string.IsNullOrWhiteSpace(statKey) || amount == 0)
                return;

            if (!ItemStats.TryGetValue(statKey, out Dictionary<int, uint> byItem))
            {
                byItem = [];
                ItemStats[statKey] = byItem;
            }

            byItem.TryGetValue(itemKey, out uint current);
            byItem[itemKey] = AddClamped(current, amount);
        }

        public CompletedAdventurePlayer ToCompleted(uint reward, bool winner)
        {
            Dictionary<string, IDictionary<int, uint>> itemStats = [];
            foreach ((string statKey, Dictionary<int, uint> byItem) in ItemStats)
                itemStats[statKey] = new Dictionary<int, uint>(byItem);

            return new CompletedAdventurePlayer(
                Name,
                Team,
                Kills,
                Deaths,
                reward,
                winner,
                new Dictionary<string, uint>(Stats),
                itemStats);
        }

        private static int AddClamped(int left, int right) =>
            (int)Math.Min(int.MaxValue, Math.Max(0L, (long)left + right));

        private static uint AddClamped(uint left, uint right) =>
            uint.MaxValue - left < right ? uint.MaxValue : left + right;
    }

    private sealed class LivePlayerSegment
    {
        private int lastKills;
        private int lastDeaths;
        private Dictionary<string, uint> lastStats;
        private Dictionary<string, IDictionary<int, uint>> lastItemStats;

        public LivePlayerSegment(ulong steamId, Player player, bool startFromZero)
        {
            SteamId = steamId;

            if (startFromZero)
            {
                lastStats = [];
                lastItemStats = [];
                return;
            }

            ScoreboardEntry score = ScoreboardService.GetPlayerStats(player);
            lastKills = Math.Max(0, score.Kills);
            lastDeaths = Math.Max(0, score.Deaths);
            lastStats = StatsReporter.CopyStats(player);
            lastItemStats = StatsReporter.CopyItemStats(player);
        }

        public ulong SteamId { get; }

        public void Capture(Player player, ParticipantState participant)
        {
            participant.UpdateIdentity(player);

            ScoreboardEntry score = ScoreboardService.GetPlayerStats(player);
            int currentKills = Math.Max(0, score.Kills);
            int currentDeaths = Math.Max(0, score.Deaths);
            participant.AddScore(Delta(currentKills, lastKills), Delta(currentDeaths, lastDeaths));
            lastKills = currentKills;
            lastDeaths = currentDeaths;

            Dictionary<string, uint> currentStats = StatsReporter.CopyStats(player);
            foreach ((string key, uint value) in currentStats)
            {
                lastStats.TryGetValue(key, out uint previous);
                participant.AddStat(key, Delta(value, previous));
            }
            lastStats = currentStats;

            Dictionary<string, IDictionary<int, uint>> currentItemStats = StatsReporter.CopyItemStats(player);
            foreach ((string statKey, IDictionary<int, uint> byItem) in currentItemStats)
            {
                lastItemStats.TryGetValue(statKey, out IDictionary<int, uint> previousByItem);
                foreach ((int itemKey, uint value) in byItem)
                {
                    uint previous = 0;
                    previousByItem?.TryGetValue(itemKey, out previous);
                    participant.AddItemStat(statKey, itemKey, Delta(value, previous));
                }
            }
            lastItemStats = currentItemStats;
        }

        private static int Delta(int current, int previous) =>
            current >= previous ? current - previous : current;

        private static uint Delta(uint current, uint previous) =>
            current >= previous ? current - previous : current;
    }
}

internal sealed record CompletedAdventureMatch(
    string Token,
    DateTime StartUtc,
    DateTime EndUtc,
    IReadOnlyDictionary<ulong, CompletedAdventurePlayer> Players,
    IReadOnlyDictionary<Team, CompletedAdventureTeam> Teams);

internal sealed record CompletedAdventurePlayer(
    string Name,
    Team Team,
    int Kills,
    int Deaths,
    uint Reward,
    bool Winner,
    IReadOnlyDictionary<string, uint> Stats,
    IReadOnlyDictionary<string, IDictionary<int, uint>> ItemStats);

internal sealed record CompletedAdventureTeam(int Points, IReadOnlyList<short> Bosses);

internal sealed class AdventureMatchParticipantPlayer : ModPlayer
{
    public override void PlayerDisconnect()
    {
        if (Main.netMode != Terraria.ID.NetmodeID.Server)
            return;

        ModContent.GetInstance<GameManager>()?.CaptureDisconnectingPlayer(Player);
    }
}
