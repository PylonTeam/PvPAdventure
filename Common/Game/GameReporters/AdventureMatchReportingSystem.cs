using PvPAdventure.Common.Game.MatchReplays;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using PvPFramework.Common.EndScreen;
using PvPFramework.Common.Game;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game.GameReporters;

/// <summary>Adventure match capture, replay ownership, reporting, and delayed result presentation.</summary>
internal sealed class AdventureMatchReportingSystem : ModSystem
{
    private AdventureMatchSession activeMatch;
    private EndScreenSummary pendingEndScreen;
    private ulong presentAfter;
    public string CurrentMatchToken => activeMatch?.Token ?? "";

    public void Begin()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        ClearPending();
        foreach (Player player in Main.ActivePlayers)
            if (player.TryGetModPlayer(out MatchStatsPlayer stats)) stats.ResetMatchStats();
        ModContent.GetInstance<PointsManager>().ResetForMatch();
        activeMatch = new AdventureMatchSession(DateTime.UtcNow);
        activeMatch.CaptureActivePlayers(discoverPlayers: true);
        ModContent.GetInstance<ReeseReplayControlSystem>().StartMatchRecording(activeMatch.Token);
        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
        Log.Info($"Started authoritative Adventure match. MatchToken={activeMatch.Token}");
    }

    public void Complete()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || activeMatch == null) return;
        AdventureMatchSession match = activeMatch;
        activeMatch = null;
        CompletedAdventureMatch completed = null;
        try { completed = match.Complete(DateTime.UtcNow, ModContent.GetInstance<PointsManager>()); }
        catch (Exception ex) { Log.Error($"Failed to freeze completed Adventure match. MatchToken={match.Token}, Error={ex}"); }
        try
        {
            // Framework has already respawned/staged players and synchronized Waiting.
            pendingEndScreen = AdventureEndScreenExtension.CreateSummary(match.Token);
            presentAfter = Main.GameUpdateCount + 20;
        }
        catch (Exception ex) { Log.Error($"Failed to build the Adventure end screen: {ex}"); }
        string replay = ModContent.GetInstance<ReeseReplayControlSystem>().StopMatchRecording(match.Token);
        if (completed != null) MatchReporter.PostCompletedMatchSafe(completed, replay);
    }

    public void Abort()
    {
        AdventureMatchSession match = activeMatch;
        activeMatch = null;
        ClearPending();
        if (match != null) ModContent.GetInstance<ReeseReplayControlSystem>().StopMatchRecording(match.Token);
    }

    public void CaptureDisconnectingPlayer(Player player)
    {
        if (Main.netMode == NetmodeID.Server) activeMatch?.CaptureDisconnectingPlayer(player);
    }

    public void Tick()
    {
        GameSession session = GameSession.Instance;
        if (!session.IsSelected("pvpa")) return;
        if (session.IsPlaying)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
                activeMatch?.CaptureActivePlayers(discoverPlayers: Main.GameUpdateCount % 60 == 0);
        }
        if (pendingEndScreen == null || Main.netMode == NetmodeID.MultiplayerClient) return;
        if (session.IsRunning) { ClearPending(); return; }
        if (Main.GameUpdateCount < presentAfter) return;
        EndScreenSummary summary = pendingEndScreen;
        ClearPending();
        try { EndScreenService.Present(summary); }
        catch (Exception ex) { Log.Error($"Failed to present the Adventure end screen: {ex}"); }
    }

    public override void PostUpdateEverything()
    {
        GameSession session = GameSession.Instance;
        if (!Main.dedServ && session.IsSelected("pvpa") && session.IsPlaying && Main.GameUpdateCount % 30 == 0)
            EndScreenService.CaptureLivePlayers();
    }

    private void ClearPending() { pendingEndScreen = null; presentAfter = 0; }
    public override void ClearWorld() => Abort();
    public override void OnWorldUnload() => Abort();
}
