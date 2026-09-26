using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game.GameReporters;
using PvPAdventure.Common.Game.MatchReplays;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using PvPFramework.Common.EndScreen;
using PvPFramework.Common.Scoreboard;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Chat;
using Terraria.Enums;
using Terraria.GameContent.Creative;
using Terraria.GameContent.NetModules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Net;

namespace PvPAdventure.Common.Game;

[Autoload(Side = ModSide.Both)]
public class GameManager : ModSystem
{
    private const int FramesPerSecond = 60;
    private const int CountdownAnnouncementBufferFrames = 2;
    internal const int MaxGameDurationFrames = 195 * 60 * FramesPerSecond;
    internal const int MaxCountdownSeconds = 300;

    /// <summary>
    /// Frames to wait between force-respawning everyone and sending out the end screen. Clients
    /// clone each player when the snapshot arrives, so this gives the respawns time to round-trip
    /// (client spawn -> server -> other clients) and leaves the cards drawing living players.
    /// </summary>
    private const int EndScreenRespawnSettleFrames = 20;

    /// <summary>
    /// How often, in frames, to refresh the end screen's picture of each player while playing.
    /// Half a second is close enough to "just before the game ended" and keeps the cloning cheap.
    /// </summary>
    private const int EndScreenCaptureInterval = FramesPerSecond / 2;

    public int TimeRemaining { get; set; }
    public int? _startGameCountdown = null;
    private Phase _currentPhase;
    private AdventureMatchSession _activeMatch;

    // End screen built at match end but held back a few frames; see EndScreenRespawnSettleFrames.
    private EndScreenSummary _pendingEndScreen;
    private int _pendingEndScreenDelay;
    public DateTime? MatchStartTime { get; private set; }
    public DateTime? MatchEndTime { get; private set; }
    internal string CurrentMatchToken => _activeMatch?.Token ?? "";

    public Phase CurrentPhase
    {
        get => _currentPhase;
        set
        {
            if (_currentPhase == value)
                return;

            Phase oldPhase = _currentPhase;
            _currentPhase = value;

            // The phase itself is already committed above. Never let a throwing side effect
            // (freeze toggle, replay, teleports, end screen...) abort before the sync below,
            // or clients would freeze on the last countdown value they received ("1s") while
            // the server silently entered the new phase.
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                try
                {
                    OnPhaseChange(oldPhase, value);
                }
                catch (Exception e)
                {
                    Log.Error($"OnPhaseChange({oldPhase} -> {value}) threw; phase still synced. {e}");
                }
            }

            if (Main.dedServ)
                NetMessage.SendData(MessageID.WorldData);
        }
    }

    public enum Phase
    {
        Waiting,
        Playing,
    }

    /// <summary>
    /// Advances the match clock independently of Terraria's world clock. The waiting phase freezes
    /// world time, and admin time controls may also suppress or scale the time-update hook.
    /// </summary>
    public override void PostUpdateEverything()
    {
        TickPendingEndScreen();

        switch (CurrentPhase)
        {
            case Phase.Waiting:
                {
                    if (Main.netMode == NetmodeID.MultiplayerClient)
                        break;

                    if (_startGameCountdown.HasValue)
                    {
                        _startGameCountdown--;

                        if (_startGameCountdown <= 0)
                        {
                            _startGameCountdown = null;
                            CurrentPhase = Phase.Playing;

                            if (Main.dedServ)
                                NetMessage.SendData(MessageID.WorldData); // sync phase + reset countdown
                        }
                        else
                        {
                            // Every second
                            if (_startGameCountdown % FramesPerSecond == 0)
                            {
                                int secondsLeft = _startGameCountdown.Value / FramesPerSecond;

                                if (secondsLeft % 10 == 0 && secondsLeft > 0)
                                {
                                    ChatHelper.BroadcastChatMessage(
                                        NetworkText.FromLiteral($"The game will begin in {secondsLeft} seconds"),
                                        Color.Green);
                                }
                                else if (secondsLeft <= 3 && secondsLeft > 0)
                                {
                                    ChatHelper.BroadcastChatMessage(
                                        NetworkText.FromLiteral($"{secondsLeft}..."),
                                        Color.Crimson);
                                }

                                if (Main.dedServ)
                                    NetMessage.SendData(MessageID.WorldData);
                            }
                        }
                    }

                    break;
                }

            case Phase.Playing:
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        bool discoverPlayers = Main.GameUpdateCount % FramesPerSecond == 0;
                        _activeMatch?.CaptureActivePlayers(discoverPlayers);
                    }

                    // Keep a recent picture of how everyone looks mid-match for the end screen.
                    // Capturing when the summary arrives would catch the post-match cleanup instead:
                    // by then everyone is back at spawn, revived and sitting on the race mount.
                    if (!Main.dedServ && Main.GameUpdateCount % EndScreenCaptureInterval == 0)
                        EndScreenService.CaptureLivePlayers();

                    if (--TimeRemaining <= 0)
                    {
                        CurrentPhase = Phase.Waiting;
                    }

                    break;
                }
        }
    }

    private void ResetMatchState()
    {
        MatchStartTime = null;
        MatchEndTime = null;
    }

    /// <summary>Sends the held-back end screen once the post-match respawns have had time to settle.</summary>
    private void TickPendingEndScreen()
    {
        if (_pendingEndScreen == null || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (--_pendingEndScreenDelay > 0)
            return;

        EndScreenSummary summary = _pendingEndScreen;
        ClearPendingEndScreen();

        try
        {
            EndScreenService.Present(summary);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to present the match end screen: {ex}");
        }
    }

    private void ClearPendingEndScreen()
    {
        _pendingEndScreen = null;
        _pendingEndScreenDelay = 0;
    }

    private static void ResetActivePlayerMatchState()
    {
        ScoreboardService.ResetAllPlayers();
        EndScreenService.ClearLiveCaptures();

        foreach (Player player in Main.ActivePlayers)
        {
            if (player.TryGetModPlayer(out MatchStatsPlayer matchStats))
                matchStats.ResetMatchStats();
        }
    }

    public void StartGame(int time, int countdownTimeInSeconds = 10)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || CurrentPhase == Phase.Playing ||
            _startGameCountdown.HasValue)
            return;

        if (time <= 0)
        {
            Log.Warn($"Ignored a game start with a non-positive duration. Time={time}");
            return;
        }

        EndScreenService.Hide();
        TimeRemaining = Math.Clamp(time, 1, MaxGameDurationFrames);
        countdownTimeInSeconds = Math.Clamp(countdownTimeInSeconds, 0, MaxCountdownSeconds);

        Log.Info($"Starting game. DurationFrames={TimeRemaining}, CountdownSeconds={countdownTimeInSeconds}");

        // A zero-second countdown is an immediate start, not a one-tick countdown state. This is
        // particularly useful for the debug start key and avoids leaving clients displaying 0s.
        if (countdownTimeInSeconds == 0)
        {
            _startGameCountdown = null;
            CurrentPhase = Phase.Playing;
            return;
        }

        _startGameCountdown = ToCountdownFrames(countdownTimeInSeconds);

        if (Main.dedServ)
            NetMessage.SendData(MessageID.WorldData);

        ChatHelper.BroadcastChatMessage(
            NetworkText.FromLiteral($"The game will begin in {_startGameCountdown / FramesPerSecond} seconds."), Color.Green);
    }
    public void EndGame()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        _startGameCountdown = null;
        TimeRemaining = 0;
        CurrentPhase = Phase.Waiting;
    }

    public void AdjustTimeRemaining(int deltaFrames)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            return;
        }

        if (CurrentPhase != Phase.Playing && !_startGameCountdown.HasValue)
        {
            return;
        }

        int oldFrames = TimeRemaining;

        long requestedFrames = (long)oldFrames + deltaFrames;
        TimeRemaining = (int)Math.Clamp(requestedFrames, 0L, MaxGameDurationFrames);

        if (TimeRemaining <= 0)
        {
            _startGameCountdown = null;
            CurrentPhase = Phase.Waiting;
        }

        int appliedDeltaFrames = TimeRemaining - oldFrames;

        string toText = FormatHHMMSSFromFrames(TimeRemaining);
        string actionText = appliedDeltaFrames < 0 ? "shortened" : "extended";
        string deltaText = FormatDeltaMMSSFromFrames(appliedDeltaFrames);
        string deltaHex = appliedDeltaFrames < 0 ? "FF0000" : (appliedDeltaFrames > 0 ? "00FF00" : "FFFFFF");
        string deltaTagged = $"[c/{deltaHex}:{deltaText}]";
        string msg = $"Game {actionText} by {deltaTagged}. (New time: {toText})";

        if (Main.dedServ)
        {
            NetMessage.SendData(MessageID.WorldData);
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(msg), Color.White);
        }
        else if (Main.netMode == NetmodeID.SinglePlayer)
        {
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(msg), Color.White);
        }
    }

    /// <summary>Sets the active countdown to a new value in seconds. Server/SP only.</summary>
    public void SetCountdown(int newSeconds)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (!_startGameCountdown.HasValue)
            return;

        _startGameCountdown = ToCountdownFrames(Math.Clamp(newSeconds, 0, MaxCountdownSeconds));

        if (Main.dedServ)
            NetMessage.SendData(MessageID.WorldData);
    }

    private static int ToCountdownFrames(int seconds)
    {
        if (seconds <= 0)
        {
            return 0;
        }

        return seconds * FramesPerSecond + CountdownAnnouncementBufferFrames;
    }

    private static string FormatHHMMSSFromFrames(int frames)
    {
        if (frames < 0)
        {
            frames = 0;
        }

        const int FramesPerSecond = 60;
        int totalSeconds = frames / FramesPerSecond;

        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }

    private static string FormatDeltaMMSSFromFrames(int deltaFrames)
    {
        if (deltaFrames == 0)
        {
            return "+0:00";
        }

        string sign = deltaFrames > 0 ? "+" : "-";

        int absFrames = deltaFrames > 0 ? deltaFrames : -deltaFrames;
        const int FramesPerSecond = 60;
        int totalSeconds = absFrames / FramesPerSecond;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        return $"{sign}{minutes}:{seconds:00}";
    }

    //private static void BroadcastEndGameSummary()
    //{
    //    // Only the server (or singleplayer) should broadcast.
    //    if (Main.netMode == NetmodeID.MultiplayerClient)
    //    {
    //        return;
    //    }

    //    var pm = ModContent.GetInstance<PointsManager>();

    //    for (int i = 0; i < 1; i++)
    //    {
    //        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("-----------------------------"), Color.White);
    //    }

    //    ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("The game has ended!"), Color.White);

    //    var scoredTeams = pm.Points
    //        .Where(kvp => kvp.Key != Team.None)
    //        .Where(kvp => kvp.Value > 0)
    //        .OrderByDescending(kvp => kvp.Value)
    //        .ToList();

    //    if (scoredTeams.Count == 0)
    //    {
    //        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("No teams scored any points."), Color.White);
    //        return;
    //    }

    //    var maxPoints = scoredTeams[0].Value;
    //    var winningTeams = scoredTeams
    //        .Where(kvp => kvp.Value == maxPoints)
    //        .Select(kvp => kvp.Key)
    //        .ToList();

    //    if (winningTeams.Count == 1)
    //    {
    //        var team = winningTeams[0];

    //        ChatHelper.BroadcastChatMessage(
    //            NetworkText.FromLiteral($"{team} Team wins with {maxPoints} points!"),
    //            Main.teamColor[(int)team]);
    //    }
    //    else
    //    {
    //        // Multiple winners
    //        var winnersText = string.Join(", ", winningTeams.Select(t => $"{t} Team"));

    //        ChatHelper.BroadcastChatMessage(
    //            NetworkText.FromLiteral($"Tie! {winnersText} lead with {maxPoints} points."),
    //            Color.White);
    //    }

    //    int rank = 1;

    //    // Print team summary row, containing team points and MVP.
    //    foreach (var (team, points) in scoredTeams)
    //    {
    //        Player bestPlayer = null;
    //        var bestKills = -1;
    //        var bestDeaths = int.MaxValue;
    //        var bestKd = -1f;

    //        foreach (var player in Main.ActivePlayers)
    //        {
    //            if ((Team)player.team != team)
    //            {
    //                continue;
    //            }

    //            var ap = player.GetModPlayer<StatisticsPlayer>();
    //            var kills = ap.Kills;
    //            var deaths = ap.Deaths;

    //            float kd = deaths <= 0 ? kills : kills / (float)deaths;

    //            var isBetter = false;

    //            if (kills > bestKills)
    //            {
    //                isBetter = true;
    //            }
    //            else if (kills == bestKills)
    //            {
    //                if (deaths < bestDeaths)
    //                {
    //                    isBetter = true;
    //                }
    //                else if (deaths == bestDeaths && kd > bestKd)
    //                {
    //                    isBetter = true;
    //                }
    //            }

    //            if (isBetter)
    //            {
    //                bestPlayer = player;
    //                bestKills = kills;
    //                bestDeaths = deaths;
    //                bestKd = kd;
    //            }
    //        }

    //        string teamSummaryRow = $"{rank}. {team} Team: {points} points.";

    //        if (bestPlayer != null)
    //        {
    //            var bestAp = bestPlayer.GetModPlayer<StatisticsPlayer>();
    //            teamSummaryRow += $" MVP: {bestPlayer.name} (K/D: {bestAp.Kills}/{bestAp.Deaths})";
    //        }

    //        ChatHelper.BroadcastChatMessage(
    //            text: NetworkText.FromLiteral(teamSummaryRow),
    //            color: Main.teamColor[(int)team]);

    //        rank++;
    //    }
    //}

    internal void CaptureDisconnectingPlayer(Player player)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        _activeMatch?.CaptureDisconnectingPlayer(player);
    }

    // NOTE: This is not called on multiplayer clients (see CurrentPhase property).
    private void OnPhaseChange(Phase oldPhase, Phase newPhase)
    {
        Core.Compat.AdventureRegionSystem.UpdateMatchState();
        Log.Chat("New GamePhase: " + newPhase + ", (old: " + oldPhase + ")");

        // Only save when a real match ends (Playing → Waiting transition)
        if (oldPhase == Phase.Playing && newPhase == Phase.Waiting)
        {
            //BroadcastEndGameSummary();

            // Get everyone back on their feet before the match is closed out, so nobody is stuck
            // on a respawn timer and the end screen captures living players for its cards.
            PvPFramework.Common.Combat.RespawnService.RespawnAll();

            string completingMatchToken = _activeMatch?.Token;
            CompletedAdventureMatch completedMatch = CompleteActiveMatch();

            // Sync Waiting before the custom end-screen packet. Otherwise clients can receive
            // the snapshot while still locally in Playing and immediately hide it.
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);

            try
            {
                // Build now, while match state is still intact, but hold the send back a few frames
                // so the respawns above have landed on every client before they snapshot players.
                _pendingEndScreen = AdventureEndScreenExtension.CreateSummary(
                    completedMatch?.Token ?? completingMatchToken);
                _pendingEndScreenDelay = EndScreenRespawnSettleFrames;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to build the match end screen: {ex}");
            }

            string replayFilePath = string.IsNullOrWhiteSpace(completingMatchToken)
                ? null
                : ModContent.GetInstance<ReeseReplayControlSystem>().StopMatchRecording(completingMatchToken);

            if (completedMatch != null)
                MatchReporter.PostCompletedMatchSafe(completedMatch, replayFilePath);

            ResetMatchState();
        }

        switch (newPhase)
        {
            case Phase.Waiting:
                {
                    // Remove everything that is hostile
                    foreach (var npc in Main.ActiveNPCs)
                    {
                        if (npc.townNPC || npc.isLikeATownNPC || npc.type == NPCID.TargetDummy)
                            continue;

                        npc.life = 0;
                        npc.netSkip = -1;

                        if (Main.dedServ)
                            NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
                    }

                    // Teleport all players to spawn
                    var spawnPosition = new Vector2(Main.spawnTileX, Main.spawnTileY - 3).ToWorldCoordinates();
                    foreach (var player in Main.ActivePlayers)
                    {
                        if (Main.dedServ)
                            NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, player.whoAmI, spawnPosition.X,
                                spawnPosition.Y, 2);
                    }

                    UpdateFreezeTime(true);

                    break;
                }
            case Phase.Playing:
                {
                    BeginActiveMatch();
                    UpdateFreezeTime(false);

                    break;
                }
        }
    }

    private void BeginActiveMatch()
    {
        ResetActivePlayerMatchState();
        ModContent.GetInstance<PointsManager>().ResetForMatch();

        MatchStartTime = DateTime.UtcNow;
        MatchEndTime = null;
        _activeMatch = new AdventureMatchSession(MatchStartTime.Value);
        _activeMatch.CaptureActivePlayers(discoverPlayers: true);

        ModContent.GetInstance<ReeseReplayControlSystem>().StartMatchRecording(_activeMatch.Token);
        Log.Info($"Started authoritative match session. MatchToken={_activeMatch.Token}, Start={MatchStartTime:O}");
    }

    private CompletedAdventureMatch CompleteActiveMatch()
    {
        MatchEndTime = DateTime.UtcNow;
        AdventureMatchSession session = _activeMatch;
        _activeMatch = null;

        if (session == null)
        {
            Log.Error("Match entered the end phase without an active reporting session.");
            return null;
        }

        try
        {
            return session.Complete(MatchEndTime.Value, ModContent.GetInstance<PointsManager>());
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to freeze completed match state. MatchToken={session.Token}, Error={ex}");
            return null;
        }
    }

    private void UpdateFreezeTime(bool value)
    {
        var freezeTimeModule = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>();
        freezeTimeModule.SetPowerInfo(value);

        if (Main.dedServ)
        {
            var packet = NetCreativePowersModule.PreparePacket(freezeTimeModule.PowerId, 1);
            packet.Writer.Write(freezeTimeModule.Enabled);
            NetManager.Instance.Broadcast(packet);
        }
    }

    public override void OnWorldLoad()
    {
        _currentPhase = Phase.Waiting;
        _activeMatch = null;
        _startGameCountdown = null;
        TimeRemaining = 0;
        ClearPendingEndScreen();
        ResetMatchState();

        if (Main.netMode != NetmodeID.MultiplayerClient)
            OnPhaseChange(Phase.Waiting, Phase.Waiting);
    }

    public override void ClearWorld()
    {
        // A world unload/server shutdown is not a completed match. Drop the in-memory session
        // without entering the normal Playing -> Waiting reporting path.
        _activeMatch = null;
        _startGameCountdown = null;
        TimeRemaining = 0;
        _currentPhase = Phase.Waiting;
        ClearPendingEndScreen();
        ResetMatchState();
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(TimeRemaining);
        writer.Write((int)CurrentPhase);
        writer.Write(_startGameCountdown.HasValue);
        if (_startGameCountdown.HasValue)
            writer.Write(_startGameCountdown.Value);
    }

    public override void NetReceive(BinaryReader reader)
    {
        TimeRemaining = reader.ReadInt32();
        CurrentPhase = (Phase)reader.ReadInt32();
        if (reader.ReadBoolean())
            _startGameCountdown = reader.ReadInt32();
        else
            _startGameCountdown = null;
    }
}
