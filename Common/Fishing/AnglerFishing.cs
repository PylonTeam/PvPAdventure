using System;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Fishing;

internal sealed class AnglerFishing : ModSystem
{
    private static readonly int[] OpeningQuests = [ItemID.Bunnyfish, ItemID.DynamiteFish, ItemID.ZombieFish];

    public override void Load() => On_Projectile.FishingCheck_ProbeForQuestFish += ProbeQuestFish;

    public override void Unload() => On_Projectile.FishingCheck_ProbeForQuestFish -= ProbeQuestFish;

    internal static int DailyQuest => Main.anglerQuestItemNetIDs[Main.anglerQuest];

    internal static int NextQuest(int previousFish = ItemID.None)
    {
        // Start with surface catches, then follow Terraria's quest list order.
        int[] questItems = Main.anglerQuestItemNetIDs;
        int[] rotation = OpeningQuests.Intersect(questItems).Concat(questItems.Except(OpeningQuests)).ToArray();
        int current = Array.IndexOf(rotation, previousFish);
        for (int offset = 1; offset <= rotation.Length; offset++)
        {
            int fish = rotation[(current + offset) % rotation.Length];
            if (IsQuestAvailable(fish))
                return fish;
        }

        return DailyQuest;
    }

    private static bool IsQuestAvailable(int fish)
    {
        bool hardmode = Main.hardMode;
        bool crimson = WorldGen.crimson;
        bool defeatedBoss = NPC.downedBoss1 || NPC.downedBoss2 || NPC.downedBoss3 ||
            hardmode || NPC.downedSlimeKing || NPC.downedQueenBee;

        // Match Main.AnglerQuestSwap's world progression and evil-biome requirements.
        bool notAvailable = fish switch
        {
            ItemID.Cursedfish => !hardmode || crimson,
            ItemID.Ichorfish => !hardmode || !crimson,
            ItemID.BloodyManowar => !crimson,
            ItemID.EaterofPlankton or ItemID.InfectedScabbardfish => crimson,
            ItemID.Hungerfish or ItemID.MirageFish or ItemID.Pixiefish or ItemID.UnicornFish or
                ItemID.CapnTunabeard or ItemID.Derpfish or ItemID.Fishron => !hardmode,
            ItemID.Wyverntail => !hardmode || !defeatedBoss,
            ItemID.Angelfish or ItemID.Cloudfish => !defeatedBoss,
            _ => false,
        };
        ItemLoader.IsAnglerQuestAvailable(fish, ref notAvailable);
        return !notAvailable;
    }

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
