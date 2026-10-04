using Pylon.Common.MainMenu.API;
using Pylon.Common.MainMenu.API.MatchHistory;
using PvPFramework.Common.EndScreen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using CompletedMatchPayload = Pylon.Common.MainMenu.API.MatchHistory.MatchApi.CompletedMatchPayload;
using MatchPayload = Pylon.Common.MainMenu.API.MatchHistory.MatchApi.MatchPayload;
using MatchPlayerPayload = Pylon.Common.MainMenu.API.MatchHistory.MatchApi.MatchPlayerPayload;
using MatchTeamPayload = Pylon.Common.MainMenu.API.MatchHistory.MatchApi.MatchTeamPayload;

namespace PvPAdventure.Common.Game.GameReporters;

internal static class MatchReporter
{
    private const string GameMode = "pvpa";

    public static void PostCompletedMatchSafe(CompletedAdventureMatch completedMatch, string replayFilePath = null)
    {
        if (Main.netMode != NetmodeID.Server || completedMatch == null)
            return;

        if (!string.IsNullOrWhiteSpace(replayFilePath) && !File.Exists(replayFilePath))
        {
            Log.Warn($"Replay file missing. Falling back to match/v1. Path={replayFilePath}");
            replayFilePath = null;
        }

        List<GemRecipient> gemRecipients = [];

        try
        {
            MatchPayload payload = BuildMatchPayload(completedMatch);
            SavePermanentBackupSafe(payload, completedMatch.Token);
            gemRecipients = CaptureActiveGemRecipients(payload, completedMatch.Token);
            LogMatchPayloadDetails(payload, completedMatch.Token);
            bool isValid = IsValidPayload(payload);
            LogMatchEndSummary(payload, replayFilePath, isValid);

            if (!isValid)
            {
                ReportGemFailure(
                    completedMatch.Token,
                    gemRecipients,
                    400,
                    "The match payload was invalid, so Tavernkeep did not add the gems.");
                return;
            }

            // Intentionally no automatic retry: Tavernkeep currently has no idempotency key, so an
            // ambiguous retry could duplicate the match, achievements, and gem rewards.
            _ = PostMatchSafeAsync(payload, replayFilePath, completedMatch.Token, gemRecipients);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to build or queue match payload. MatchToken={completedMatch.Token}, Error={ex}");
            ReportGemFailureToActivePlayers(
                completedMatch.Token,
                0,
                $"The match could not be prepared for upload: {ex.Message}");
        }
    }

    private static void SavePermanentBackupSafe(MatchPayload payload, string matchToken)
    {
        try
        {
            string backupPath = MatchSubmission.SaveBackup(payload, matchToken, "PvPAdventureMatchesBackupDoNotDelete");
            Log.Chat($"Saved permanent match JSON backup. MatchToken={matchToken}, Path={backupPath}");
        }
        catch (Exception ex)
        {
            // A backup failure must not prevent the normal API submission attempt.
            Log.Error($"Failed to save permanent match JSON backup. MatchToken={matchToken}, Error={ex}");
        }
    }

    private static async Task PostMatchSafeAsync(
        MatchPayload payload,
        string replayFilePath,
        string presentationKey,
        IReadOnlyList<GemRecipient> gemRecipients)
    {
        try
        {
            ApiResult<CompletedMatchPayload> result = await MatchSubmission.SubmitAsync(
                presentationKey, payload, replayFilePath, "PvPAdventureMatchesBackupDoNotDelete")
                .ConfigureAwait(false);
            string version = string.IsNullOrWhiteSpace(replayFilePath) ? "v1" : "v2";

            if (!result.IsSuccess)
            {
                int statusCode = (int)result.Status;
                string message = statusCode == 401
                    ? $"Match {version} post failed: 401 Unauthorized. Error={result.ErrorMessage}"
                    : $"Match {version} post failed: Status={statusCode}. Error={result.ErrorMessage}";
                WriteMatchPostConsole(WithRequestSummary(message, result.RequestSummary));
                Log.Error(message + " The request was not retried because match posting is not idempotent.");
                ReportGemFailure(
                    presentationKey,
                    gemRecipients,
                    statusCode,
                    result.ErrorMessage ?? "Tavernkeep rejected the match upload.");
                return;
            }

            long matchId = result.Data?.Id ?? 0;
            if (matchId <= 0)
            {
                WriteMatchPostConsole(WithRequestSummary(
                    $"Match {version} post succeeded, but the backend returned no match id.", result.RequestSummary));
            }
            else
            {
                WriteMatchPostConsole(WithRequestSummary(
                    $"Match {version} post succeeded. MatchId={matchId}", result.RequestSummary));
                Log.Info($"Posted match successfully. MatchId={matchId}");
            }

            // Tavernkeep increments gems inside the same transaction as the match. Only a successful
            // match response reaches this point, so the following profile reads are authoritative totals.
            await PublishConfirmedGemTotalsAsync(presentationKey, gemRecipients).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            WriteMatchPostConsole($"Match post failed with an unexpected error: {ex.GetType().Name}: {ex.Message}");
            Log.Error($"Unexpected error while posting match; request was not retried: {ex}");
            ReportGemFailure(
                presentationKey,
                gemRecipients,
                0,
                $"Unexpected {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static List<GemRecipient> CaptureActiveGemRecipients(
        MatchPayload payload,
        string presentationKey)
    {
        List<GemRecipient> recipients = [];
        HashSet<ulong> capturedSteamIds = [];

        foreach (Player player in Main.ActivePlayers)
        {
            if (player?.active != true || (Team)player.team == Team.None)
                continue;

            if (!PylonService.TryGetSteamId(player, out ulong steamId) ||
                !payload.Players.ContainsKey(steamId))
            {
                QueueGemResult(
                    presentationKey,
                    player.whoAmI,
                    EndScreenGemResult.Failed(
                        401,
                        "Steam authentication was unavailable, so this player was not included in the match upload."));
                continue;
            }

            if (capturedSteamIds.Add(steamId))
                recipients.Add(new GemRecipient(steamId, player.whoAmI));
        }

        return recipients;
    }

    private static async Task PublishConfirmedGemTotalsAsync(
        string presentationKey,
        IReadOnlyList<GemRecipient> recipients)
    {
        Task[] requests = recipients.Select(recipient =>
            PublishConfirmedGemTotalAsync(presentationKey, recipient)).ToArray();
        await Task.WhenAll(requests).ConfigureAwait(false);
    }

    private static async Task PublishConfirmedGemTotalAsync(
        string presentationKey,
        GemRecipient recipient)
    {
        try
        {
            ApiResult<long> result = await PylonService
                .GetTotalGemsAsync(recipient.SteamId)
                .ConfigureAwait(false);

            EndScreenGemResult gemResult = result.IsSuccess
                ? EndScreenGemResult.Confirmed(result.Data, (int)result.Status)
                : EndScreenGemResult.TotalUnavailable(
                    (int)result.Status,
                    CleanReason(result.ErrorMessage, "Tavernkeep did not return the confirmed gem balance."));
            QueueGemResult(presentationKey, recipient.PlayerIndex, gemResult);
        }
        catch (Exception ex)
        {
            QueueGemResult(
                presentationKey,
                recipient.PlayerIndex,
                EndScreenGemResult.TotalUnavailable(
                    0,
                    CleanReason(ex.Message, $"Unexpected {ex.GetType().Name} while loading the gem balance.")));
        }
    }

    private static void ReportGemFailure(
        string presentationKey,
        IEnumerable<GemRecipient> recipients,
        int statusCode,
        string reason)
    {
        EndScreenGemResult result = EndScreenGemResult.Failed(
            statusCode,
            CleanReason(reason, "Tavernkeep did not confirm the gem update."));

        foreach (GemRecipient recipient in recipients)
            QueueGemResult(presentationKey, recipient.PlayerIndex, result);
    }

    private static void ReportGemFailureToActivePlayers(
        string presentationKey,
        int statusCode,
        string reason)
    {
        EndScreenGemResult result = EndScreenGemResult.Failed(
            statusCode,
            CleanReason(reason, "Tavernkeep did not confirm the gem update."));

        foreach (Player player in Main.ActivePlayers)
            if (player?.active == true && (Team)player.team != Team.None)
                QueueGemResult(presentationKey, player.whoAmI, result);
    }

    private static void QueueGemResult(
        string presentationKey,
        int playerIndex,
        EndScreenGemResult result)
    {
        Main.QueueMainThreadAction(() =>
            EndScreenService.ReportGemResult(presentationKey, playerIndex, result));
    }

    private static string CleanReason(string reason, string fallback)
    {
        string result = string.IsNullOrWhiteSpace(reason) ? fallback : reason;
        result = result.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return result.Length <= 300 ? result : result[..300] + "...";
    }

    private static MatchPayload BuildMatchPayload(CompletedAdventureMatch completedMatch)
    {
        PylonService.LogMatchPostAuthPreflight();

        Dictionary<ulong, MatchPlayerPayload> players = [];
        foreach ((ulong steamId, CompletedAdventurePlayer player) in completedMatch.Players)
        {
            Dictionary<string, uint> stats = new(player.Stats);
            Dictionary<string, IDictionary<int, uint>> itemStats = [];
            foreach ((string statKey, IDictionary<int, uint> byItem) in player.ItemStats)
                itemStats[statKey] = new Dictionary<int, uint>(byItem);
            stats.TryGetValue(StatsReporter.BossDamageDealt, out uint bossDamage);

            players[steamId] = new MatchPlayerPayload(
                player.Name,
                (uint)player.Team,
                player.Reward,
                player.Kills,
                player.Deaths,
                player.Winner,
                stats,
                itemStats,
                null, // Omit CTG-only captures from PvP Adventure payloads.
                bossDamage);

            Log.Info($"Match player: Name={player.Name}, SteamId={steamId}, Team={player.Team}, " +
                     $"Winner={player.Winner}, Kills={player.Kills}, Deaths={player.Deaths}, " +
                     $"Reward={player.Reward}, Stats={stats.Count}, ItemStats={itemStats.Count}");
        }

        return new MatchPayload(
            DateTime.SpecifyKind(completedMatch.StartUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(completedMatch.EndUtc, DateTimeKind.Utc),
            GameMode,
            players,
            new Dictionary<string, string>(), // Tavernkeep requires metrics; an empty object adds no metric rows.
            BuildTeamsList(completedMatch.Teams, completedMatch.Players.Values),
            null);
    }

    private static List<MatchTeamPayload?> BuildTeamsList(
        IReadOnlyDictionary<Team, CompletedAdventureTeam> teams,
        IEnumerable<CompletedAdventurePlayer> players)
    {
        int lastTeamId = Math.Max(0, teams.Keys.Select(team => (int)team).DefaultIfEmpty(0).Max());
        List<MatchTeamPayload?> result = Enumerable.Repeat<MatchTeamPayload?>(null, lastTeamId + 1).ToList();
        Dictionary<Team, uint> bossDamageByTeam = [];

        foreach (CompletedAdventurePlayer player in players)
        {
            player.Stats.TryGetValue(StatsReporter.BossDamageDealt, out uint bossDamage);
            bossDamageByTeam.TryGetValue(player.Team, out uint current);
            bossDamageByTeam[player.Team] = uint.MaxValue - current < bossDamage
                ? uint.MaxValue
                : current + bossDamage;
        }

        foreach ((Team team, CompletedAdventureTeam teamResult) in teams)
        {
            if (team == Team.None)
                continue;

            int teamId = (int)team;
            while (result.Count <= teamId)
                result.Add(null);
            bossDamageByTeam.TryGetValue(team, out uint bossDamage);
            result[teamId] = new MatchTeamPayload(teamResult.Points, teamResult.Bosses.ToList(), null, bossDamage);
        }

        return result;
    }

    private static void LogMatchPayloadDetails(MatchPayload payload, string matchToken)
    {
        Log.Info($"Match ended. Token={matchToken}, Start={payload.Start:O}, End={payload.End:O}, " +
                 $"Players={payload.Players.Count}, Teams={payload.Teams.Count}, " +
                 $"Team0Null={payload.Teams.Count > 0 && payload.Teams[0] == null}");

        for (int i = 0; i < payload.Teams.Count; i++)
            if (payload.Teams[i] is MatchTeamPayload team)
                Log.Info($"Team {i}: {team.Points} points, {team.Bosses.Count} bosses");
    }

    private static void LogMatchEndSummary(MatchPayload payload, string replayFilePath, bool willPost)
    {
        bool isOfficial = Main.dedServ && global::Pylon.Pylon.IsOfficial;
        bool hasReplay = !string.IsNullOrWhiteSpace(replayFilePath);
        string endpoint = hasReplay ? "match/v2" : "match/v1";
        string post = willPost ? endpoint + " queued" : "skipped (invalid payload)";
        int winners = payload.Players.Values.Count(player => player.Winner);
        int durationSeconds = Math.Max(0, (int)(payload.End - payload.Start).TotalSeconds);
        string duration = durationSeconds >= 3600
            ? $"{durationSeconds / 3600}:{durationSeconds / 60 % 60:00}:{durationSeconds % 60:00}"
            : $"{durationSeconds / 60}:{durationSeconds % 60:00}";

        Log.Chat($"Match report: official={(isOfficial ? "yes" : "no")}, post={post}, " +
                 $"players={payload.Players.Count}, winners={winners}, replay={(hasReplay ? "yes" : "no")}, " +
                 $"duration={duration}");
    }

    private static bool IsValidPayload(MatchPayload payload)
    {
        if (payload.End < payload.Start)
        {
            Log.Error("Refusing to post malformed match: end time precedes start time.");
            return false;
        }

        if (payload.Players.Count == 0)
        {
            Log.Warn("Refusing to post malformed match: no authenticated participants in payload.");
            return false;
        }

        if (payload.Teams.Count == 0 || payload.Teams[0] != null)
        {
            Log.Error("Refusing to post malformed match: team 0 must exist and be null.");
            return false;
        }

        foreach ((ulong steamId, MatchPlayerPayload player) in payload.Players)
        {
            if (steamId == 0 || player.Team == 0 || player.Team >= payload.Teams.Count ||
                payload.Teams[(int)player.Team] == null || player.Kills < 0 || player.Deaths < 0)
            {
                Log.Error($"Refusing to post malformed match player. SteamId={steamId}, Team={player.Team}, " +
                          $"Kills={player.Kills}, Deaths={player.Deaths}");
                return false;
            }
        }

        return true;
    }

    private static string WithRequestSummary(string message, string requestSummary) =>
        string.IsNullOrWhiteSpace(requestSummary) ? message : $"{message} ({requestSummary})";

    private static void WriteMatchPostConsole(string message) =>
        Console.WriteLine($"[PvPAdventure/OfficialMatchReporter] {message}");

    private readonly record struct GemRecipient(ulong SteamId, int PlayerIndex);
}
