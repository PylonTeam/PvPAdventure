#if DEBUG
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PvPAdventure.Common.Bounties;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Travel.UI;
using PvPFramework.Common.Game;
using PvPAdventure.Core.Utilities;
using PvPAdventure.UI;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Debug;

// Every PvP Adventure debug keybind lives here so they're all discoverable in one place and their
// feedback is tagged with this file when it prints to chat (Log.Chat uses [CallerFilePath]).
//
//   Shift+NumPad1  toggle debug stats
//   Shift+NumPad2  start a match
//   Shift+NumPad3  end a match or cancel its countdown
//   Shift+NumPad4  grant bounty shards
//   F5             rebuild debug UI (travel UI + draggable panels)
//
// The race period toggle lives in Adventure's DebugRacePeriodKeybinds.
[Autoload(Side = ModSide.Client)]
internal sealed class DebugKeybinds : ModSystem
{
    internal static readonly Color MessageColor = new(255, 190, 70);

    private const int BountyShardsPerPress = 500;
    private const int StartGameCountdownSeconds = 0;

    private bool numPad1Released = true;
    private bool numPad2Released = true;
    private bool numPad3Released = true;
    private bool numPad4Released = true;
    private bool f5Released = true;

    public override void OnWorldLoad()
    {
        numPad1Released = true;
        numPad2Released = true;
        numPad3Released = true;
        numPad4Released = true;
        f5Released = true;
    }

    public override void PostUpdateEverything()
    {
        if (Main.gameMenu)
            return;

        // Call for every key each frame -- each helper tracks that key's own release state.
        if (PressedWithShift(Keys.NumPad1, ref numPad1Released))
            DebugStatsSystem.ToggleFromKeybind();

        if (PressedWithShift(Keys.NumPad2, ref numPad2Released))
            StartGame();

        if (PressedWithShift(Keys.NumPad3, ref numPad3Released))
            EndGame();

        if (PressedWithShift(Keys.NumPad4, ref numPad4Released))
            AddBountyShards();

        if (JustPressed(Keys.F5, ref f5Released))
            RebuildDebugUI();
    }

    /// <summary>Starts a full-length match immediately, mirroring the Game Manager's start button.</summary>
    private static void StartGame()
    {
        GameManager gameManager = ModContent.GetInstance<GameManager>();

        if (!gameManager.IsSelected || GameSession.Instance.IsRunning)
        {
            Log.Chat("Shift+NumPad2: select Adventure and wait for the current event to finish.");
            return;
        }

        if (!ErkySSC.Common.AdminTools.AdminUISystem.CanUseAdminUI()) return;
        GameSessionNetHandler.Start(GameSession.MaxDurationTicks, StartGameCountdownSeconds);

        Log.Chat("Shift+NumPad2: starting a match.");
    }

    /// <summary>Ends a running match or cancels an active start countdown.</summary>
    private static void EndGame()
    {
        GameManager gameManager = ModContent.GetInstance<GameManager>();

        if (!gameManager.IsSelected || !GameSession.Instance.IsRunning)
        {
            Log.Chat("Shift+NumPad3: no match or countdown to end.");
            return;
        }

        if (!ErkySSC.Common.AdminTools.AdminUISystem.CanUseAdminUI()) return;
        GameSessionNetHandler.End();

        Log.Chat("Shift+NumPad3: ending the match or active countdown.");
    }

    /// <summary>Forces the debug-refreshable UI to rebuild, so layout tweaks show without a reload.</summary>
    private static void RebuildDebugUI()
    {
        ModContent.GetInstance<TravelUISystem>()?.travelUIState?.ForceRebuildNextUpdate();
        UIDraggablePanel.RequestDebugRebuild();
    }

    private static void AddBountyShards()
    {
        Team team = (Team)Main.LocalPlayer.team;

        if (team == Team.None)
        {
            Log.Chat("Shift+NumPad4: you are Team.None. Join a team first.");
            return;
        }

        if (!ModContent.GetInstance<BountyManager>()
            .TryAddBountyShards(team, BountyShardsPerPress, out int totalShards))
        {
            Log.Chat("Shift+NumPad4: no eligible bounties (check conditions/config).");
            return;
        }

        Log.Chat($"+{BountyShardsPerPress} bounty shards to {team}. Shard count now: {totalShards}");
    }

    /// <summary>Edge-detects a key. Always consumes the press, so modifier checks layer on top.</summary>
    private static bool JustPressed(
        Keys key,
        ref bool released)
    {
        if (Main.keyState.IsKeyUp(key))
        {
            released = true;
            return false;
        }

        if (!released || !Main.keyState.IsKeyDown(key))
            return false;

        released = false;
        return true;
    }

    private static bool PressedWithShift(
        Keys key,
        ref bool released)
    {
        if (!JustPressed(key, ref released))
            return false;

        bool shift =
            Main.keyState.IsKeyDown(Keys.LeftShift) ||
            Main.keyState.IsKeyDown(Keys.RightShift);

        bool control =
            Main.keyState.IsKeyDown(Keys.LeftControl) ||
            Main.keyState.IsKeyDown(Keys.RightControl);

        bool alt =
            Main.keyState.IsKeyDown(Keys.LeftAlt) ||
            Main.keyState.IsKeyDown(Keys.RightAlt);

        return shift && !control && !alt;
    }
}
#endif
