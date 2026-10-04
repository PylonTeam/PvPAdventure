using ErkySSC.Common.AdminTools;
using PvPAdventure.Common.Game.GameReporters;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Core.Compat;
using PvPFramework.Common.Game;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

/// <summary>Adventure's event adapter. Framework owns lifecycle, clock, lobby staging and admin controls.</summary>
public sealed class GameManager : GameEvent
{
    private static AdventureMatchReportingSystem Reporting => ModContent.GetInstance<AdventureMatchReportingSystem>();
    public override string Id => "pvpa";
    public override string DisplayName => "PvP Adventure";
    public override int Priority => 100;
    public override string StartingRegionKey => AdventureRegionSystem.RegionKey;
    public override bool ManagesStartingRegion => true;
    public override bool ManagesLobbyStaging => false;
    public override IReadOnlyList<AdminToolOption> ManagerOptions => TeamPointsSettings.Options;
    public Phase CurrentPhase => !IsSelected ? Phase.Inactive : Session.IsPlaying ? Phase.Playing : Phase.Waiting;
    public enum Phase { Waiting, Playing, Inactive }
    internal string CurrentMatchToken => Reporting.CurrentMatchToken;

    public override void OnSelected()
    {
        base.OnSelected();
        AdventureWorldRules.EnterLobby();
    }

    protected override void OnStarted()
    {
        Reporting.Begin();
        AdventureWorldRules.SetTimeFrozen(false);
    }

    protected override void OnEnded()
    {
        Reporting.Complete();
        AdventureWorldRules.EnterLobby();
    }

    public override void Tick() => Reporting.Tick();

    public override void OnDeselected()
    {
        Reporting.Abort();
        AdventureWorldRules.SetTimeFrozen(false);
        base.OnDeselected();
    }
}
