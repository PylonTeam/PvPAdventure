#if DEBUG
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Debug;

[Autoload(Side = ModSide.Client)]
internal sealed class DebugPlayer : ModPlayer
{
    private const string Banner =
        "--------- DEBUG KEYBINDS (PVPADVENTURE) -----------\n" +
        "Shift+Numpad1: Toggle stats\n" +
        "Shift+Numpad2: Start match immediately\n" +
        "Shift+Numpad3: End match / cancel countdown\n" +
        "F5: Rebuild debug UI";

    public override void OnEnterWorld()
    {
        if (Main.dedServ || Player.whoAmI != Main.myPlayer)
            return;

        Main.NewText(Banner, DebugKeybinds.MessageColor);
    }
}
#endif
