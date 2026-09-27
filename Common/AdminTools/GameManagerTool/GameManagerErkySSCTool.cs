using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Game;
using PvPAdventure.Core.Net;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.AdminTools.GameManagerTool;

[Autoload(Side = ModSide.Client)]
internal sealed class GameManagerErkySSCTool : ModSystem
{
    private const string Owner = "PvPAdventure.GameManager";

    public override void PostSetupContent()
    {
        // Register during mod loading so ErkySSC also creates the Controls entries.
        RegisterErkySSCQuickbarEntries();
    }

    public override void OnWorldLoad()
    {
        // Refresh the callbacks and icons without creating duplicate keybinds.
        RegisterErkySSCQuickbarEntries();
    }

    public override void Unload()
    {
        if (ModLoader.TryGetMod("ErkySSC", out Mod erky))
            erky.Call("ClearAdminQuickbarEntries", Owner);
    }

    private static void RegisterErkySSCQuickbarEntries()
    {
        if (!ModLoader.TryGetMod("ErkySSC", out Mod erky))
            return;

        Asset<Texture2D> startIcon = Ass.IconStartGame;

        erky.Call(
            "RegisterAdminQuickbarEntry",
            Owner,
            "open_game_timer",
            "PvP Adventure : Game Manager",
            "Open the game manager",
            startIcon,
            new Action(ToggleDialog),
            new Func<string>(MainActionText),
            new Func<Color>(() => Color.White),
            true,
            20,
            "Ctrl+G"
        );

        // Omitting a default key leaves these actions available for rebinding in Controls.
        erky.Call(
            "RegisterAdminQuickbarEntry",
            Owner,
            "quick_start_game",
            "PvP Adventure : Quick Start Game",
            $"Immediately start a {GameManager.MaxGameDurationFrames / (60 * 60)}-minute game with no countdown",
            startIcon,
            new Action(QuickStartGame),
            null,
            null,
            true,
            21
        );

        erky.Call(
            "RegisterAdminQuickbarEntry",
            Owner,
            "quick_end_game",
            "PvP Adventure : Quick End Game",
            "Immediately end the game or cancel its countdown without confirmation",
            Ass.IconEndGame,
            new Action(QuickEndGame),
            null,
            null,
            true,
            22
        );
    }

    private static void QuickStartGame()
    {
        GameManager gm = ModContent.GetInstance<GameManager>();

        if (gm.CurrentPhase == GameManager.Phase.Playing)
        {
            Main.NewText(Language.GetTextValue("Mods.PvPAdventure.Tools.DLStartGameTool.AlreadyInProgress"), Color.Red);
            return;
        }

        if (gm._startGameCountdown.HasValue)
        {
            Main.NewText(Language.GetTextValue("Mods.PvPAdventure.Tools.DLStartGameTool.CannotStart"), Color.Red);
            return;
        }

        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            gm.StartGame(GameManager.MaxGameDurationFrames, countdownTimeInSeconds: 0);
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.GameManager);
            packet.Write((byte)GameManagerNetHandler.GameManagerPacketType.StartGame);
            packet.Write(GameManager.MaxGameDurationFrames);
            packet.Write(0);
            packet.Send();
        }
    }

    private static void QuickEndGame()
    {
        GameManager gm = ModContent.GetInstance<GameManager>();

        if (gm.CurrentPhase != GameManager.Phase.Playing && !gm._startGameCountdown.HasValue)
        {
            Main.NewText(Language.GetTextValue("Mods.PvPAdventure.Tools.DLEndGameTool.GameNotStartedYet"), Color.Red);
            return;
        }

        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            gm.EndGame();
        }
        else if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
            packet.Write((byte)AdventurePacketIdentifier.GameManager);
            packet.Write((byte)GameManagerNetHandler.GameManagerPacketType.EndGame);
            packet.Send();
        }
    }

    private static string MainActionText()
    {
        GameManagerUISystem ui = ModContent.GetInstance<GameManagerUISystem>();
        return ui?.IsActive() == true ? "Close" : "Open";
    }

    private static void ToggleDialog()
    {
        GameManagerUISystem ui = ModContent.GetInstance<GameManagerUISystem>();
        if (ui == null)
        {
            Main.NewText("Failed to open GameManagerUISystem.", Color.Red);
            return;
        }

        if (ui.IsActive())
        {
            ui.Hide();
            return;
        }

        GameManager gm = ModContent.GetInstance<GameManager>();

        if (gm.CurrentPhase == GameManager.Phase.Playing)
        {
            ui.ShowExtendGameDialog();
            return;
        }

        ui.ShowStartDialog();
    }

}
