using Microsoft.Xna.Framework;
using MonoMod.Cil;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.Events;
using Terraria.Localization;
using Terraria.ModLoader;
using PvPAdventure.Core.Config;


namespace PvPAdventure.Common.World;

internal class WorldChatMessages : ModSystem
{
    public override void Load()
    {
        // Broadcast a message when rain starts.
        On_Main.StartRain += OnMainStartRain;

        // Broadcast a message when a sandstorm starts.
        On_Sandstorm.StartSandstorm += OnSandstormStartSandstorm;

        IL_NPC.CountKillForBannersAndDropThem += SkipBannerAnnouncements;
    }

    public override void Unload()
    {
        On_Main.StartRain -= OnMainStartRain;
        On_Sandstorm.StartSandstorm -= OnSandstormStartSandstorm;
        IL_NPC.CountKillForBannersAndDropThem -= SkipBannerAnnouncements;
    }

    private void SkipBannerAnnouncements(ILContext il)
    {
        var cursor = new ILCursor(il);
        if (!cursor.TryGotoNext(instruction => instruction.MatchLdstr("Game.EnemiesDefeatedAnnouncement")))
        {
            Mod.Logger.Warn("Could not locate the enemy banner announcement.");
            return;
        }

        var announcementStart = cursor.Next;
        if (!cursor.TryGotoNext(instruction => instruction.MatchLdstr("Game.EnemiesDefeatedByAnnouncement"))
            || !cursor.TryGotoNext(instruction => instruction.MatchCall<Main>(nameof(Main.NewText)))
            || !cursor.TryGotoNext(instruction => instruction.MatchCall(typeof(ChatHelper), nameof(ChatHelper.BroadcastChatMessage)))
            || cursor.Next?.Next is not { } dropStart
            || !dropStart.MatchLdloc(0)
            || dropStart.Next?.MatchCall<Item>(nameof(Item.BannerToItem)) != true)
        {
            Mod.Logger.Warn("Could not locate the end of the enemy banner announcement block.");
            return;
        }

        // Run the skip from either path into the announcement and resume at the banner drop.
        cursor.Goto(announcementStart, MoveType.AfterLabel);
        cursor.EmitBr(dropStart);
    }

    private void OnSandstormStartSandstorm(On_Sandstorm.orig_StartSandstorm orig)
    {
        orig();
        if (ModContent.GetInstance<ServerConfig>().BroadcastWeatherMessages)
        {
            ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.PvPAdventure.Sandstorm"), Color.White);
        }
    }

    private void OnMainStartRain(On_Main.orig_StartRain orig)
    {
        orig();
        if (ModContent.GetInstance<ServerConfig>().BroadcastWeatherMessages)
        {
            ChatHelper.BroadcastChatMessage(NetworkText.FromKey("Mods.PvPAdventure.Rain"), Color.White);
        }
    }
}
