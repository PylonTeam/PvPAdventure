using Microsoft.Xna.Framework.Input;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Content.Portals;
using GameKeybinds = PylonMatch.Core.Input.Keybinds;
using System.Collections.Generic;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Input;

[Autoload(Side = ModSide.Client)]
public class Keybinds : ModSystem
{
    public ModKeybind UsePortalCreator { get; private set; }

    #region Portal creator label
    public static string UsePortalCreatorLabel
    {
        get
        {
            ModKeybind keybind = ModContent.GetInstance<Keybinds>().UsePortalCreator;

            if (keybind is null)
                return null;

            List<string> keys = keybind.GetAssignedKeys();
            keys.RemoveAll(static key => string.IsNullOrWhiteSpace(key));

            return keys.Count > 0 ? string.Join(" / ", keys) : null;
        }
    }
    #endregion

    public override void Load()
    {
        UsePortalCreator = KeybindLoader.RegisterKeybind(Mod, "UsePortalCreator", Keys.P);
    }
}

internal class KeybindsPlayer : ModPlayer
{
    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        var keybinds = ModContent.GetInstance<Keybinds>();

        // PvP Adventure extends Pylon Match's scoreboard with boss completion progress.
        var scoreboardKey = ModContent.GetInstance<GameKeybinds>().Scoreboard;
        if (scoreboardKey?.JustPressed == true)
            ModContent.GetInstance<PointsManager>().BossCompletion.Active = true;
        else if (scoreboardKey?.JustReleased == true)
            ModContent.GetInstance<PointsManager>().BossCompletion.Active = false;

        // UsePortalCreator keybind
        if (keybinds.UsePortalCreator.JustPressed)
        {
            //Log.Chat("Portal creator item keybind pressed");
            PortalCreatorItem.TryUse(Player);
        }
    }
}
