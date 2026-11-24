using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Fishing;

internal sealed class AnglerFishing : ModSystem
{
    public override void Load() => On_Projectile.FishingCheck_ProbeForQuestFish += ProbeQuestFish;

    public override void Unload() => On_Projectile.FishingCheck_ProbeForQuestFish -= ProbeQuestFish;

    private static void ProbeQuestFish(On_Projectile.orig_FishingCheck_ProbeForQuestFish orig,
        Projectile bobber, ref FishingAttempt attempt)
    {
        if (!AnglerQuestSystem.IsPlaying)
        {
            orig(bobber, ref attempt);
            return;
        }

        Player player = Main.player[bobber.owner];
        attempt.questFish = NPC.AnyNPCs(NPCID.Angler) &&
            AnglerQuestSystem.TryGetActiveQuest(player, out int fish) && !player.HasItem(fish)
            ? fish
            : -1;
    }
}
