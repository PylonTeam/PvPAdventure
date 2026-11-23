using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace PvPAdventure.Common.Fishing;

internal static class FishingCatchBoosts
{
    private const double WeaponCap = 0.03;
    private const double FrostDaggerfishCap = 0.06;

    public static bool TryReplacePotionFish(FishingCatchContext context, double potionFishShare, out int item)
    {
        item = ItemID.None;
        Player player = context.Player;
        if (context.Attempt.fishingLevel <= 0 ||
            (!(player.ZoneDungeon && NPC.downedBoss3) &&
             !(context.IsHardmode && (player.ZoneCorrupt || player.ZoneCrimson || player.ZoneHallow))))
            return false;

        var odds = FishingCatchOdds.ForWater(context);
        Span<(int Item, double Extra)> boosts =
        [
            (ItemID.AlchemyTable, odds.AlchemyTable * 4),
            (ItemID.CrystalSerpent, ExtraChance(odds.CrystalSerpent, 5, WeaponCap)),
            (ItemID.Toxikarp, ExtraChance(odds.Toxikarp, 5, WeaponCap)),
            (ItemID.Bladetongue, ExtraChance(odds.Bladetongue, 5, WeaponCap)),
        ];
        double budget = odds.Filler * potionFishShare;
        double requested = 0;
        foreach (var boost in boosts)
            requested += boost.Extra;
        if (budget <= 0 || requested <= 0)
            return false;

        // Allocate equally relative to each target if an overlapping biome exhausts the donor.
        double roll = Main.rand.NextDouble() * Math.Max(budget, requested);
        foreach (var boost in boosts)
        {
            if (roll < boost.Extra)
            {
                item = boost.Item;
                return true;
            }
            roll -= boost.Extra;
        }
        return false;
    }

    public static bool TryReplaceBombFish(FishingCatchContext context, double bombFishShare)
    {
        if (context.Attempt.fishingLevel <= 0 || !context.Player.ZoneSnow ||
            (context.Player.ZoneDungeon && NPC.downedBoss3))
            return false;

        var odds = FishingCatchOdds.ForWater(context);
        double extra = ExtraChance(odds.FrostDaggerfish, 1.5, FrostDaggerfishCap);
        return extra > 0 && Main.rand.NextDouble() * (odds.Filler * bombFishShare) < extra;
    }

    public static bool TryFillEmptyLiquidRoll(FishingCatchContext context, out int item)
    {
        item = ItemID.None;
        FishingAttempt attempt = context.Attempt;
        if (attempt.fishingLevel <= 0)
            return false;

        double empty;
        double native;
        if (attempt.inLava && attempt.CanFishInLava && context.IsHardmode)
        {
            item = ItemID.ObsidianSwordfish;
            double noCrate = 1 - FishingCatchOdds.CrateChance(context.Player) / 6;
            double rareLoot = FishingCatchOdds.Rarity(attempt.fishingLevel, 4500, 6) / 3;
            native = noCrate * rareLoot / 4;
            empty = noCrate * (1 - rareLoot) * (1 - FishingCatchOdds.Rarity(attempt.fishingLevel, 2250, 5)) *
                (1 - FishingCatchOdds.Rarity(attempt.fishingLevel, 1050, 4));
        }
        else if (!attempt.inLava && attempt.inHoney)
        {
            item = ItemID.Honeyfin;
            double rare = FishingCatchOdds.Rarity(attempt.fishingLevel, 1050, 4);
            double uncommon = FishingCatchOdds.Rarity(attempt.fishingLevel, 300, 3);
            native = rare + (1 - rare) * uncommon / 2;
            double quest = attempt.questFish == ItemID.BumblebeeTuna ? (1 - rare) * uncommon / 2 : 0;
            empty = 1 - native - quest;
        }
        else
            return false;

        return empty > 0 && Main.rand.NextDouble() * empty < native * 4;
    }

    private static double ExtraChance(double native, double multiplier, double cap) =>
        Math.Max(0, Math.Min(native * (multiplier - 1), cap - native));
}

/// <summary>Projects Terraria 1.4.4 water rolls onto the buff targets and their filler budget.</summary>
internal struct FishingCatchOdds
{
    public double Filler { get; private set; }
    public double AlchemyTable { get; private set; }
    public double CrystalSerpent { get; private set; }
    public double Toxikarp { get; private set; }
    public double Bladetongue { get; private set; }
    public double FrostDaggerfish { get; private set; }

    private double remaining;

    private readonly record struct Quality(bool Common, bool Uncommon, bool Rare, bool VeryRare, bool Legendary);

    public static double Rarity(int power, int scale, int minimum) => 1d / Math.Max(minimum, scale / power);
    public static double CrateChance(Player player) => player.cratePotion ? 0.25 : 0.1;

    public static FishingCatchOdds ForWater(FishingCatchContext context)
    {
        Player player = context.Player;
        FishingAttempt attempt = context.Attempt;
        bool dungeon = player.ZoneDungeon && NPC.downedBoss3;
        bool allowEvil = !(Main.remixWorld && attempt.heightLevel == 0);
        bool corrupt = !dungeon && allowEvil && player.ZoneCorrupt;
        bool crimson = !dungeon && allowEvil && player.ZoneCrimson;
        bool desert = !dungeon && player.ZoneDesert;
        bool snow = !dungeon && player.ZoneSnow;
        double jungle = dungeon || !player.ZoneJungle ? 0 : Main.notTheBeesWorld && !Main.remixWorld ? 0.5 : 1;
        double ocean = FishingCatchRules.IsOceanUtilityPond(attempt) ? 1 :
            Main.remixWorld && attempt.heightLevel == 1 && attempt.Y >= Main.rockLayer ? 1d / 3 : 0;
        double enemy = !Main.bloodMoon || Main.dayTime ? 0 :
            attempt.playerFishingConditions.PoleItemType == ItemID.BloodFishingRod ? 1d / 3 : 1d / 6;
        int power = attempt.fishingLevel;
        double junk = attempt.waterTilesCount < attempt.waterNeededToFish && power < 49
            ? Math.Pow((49 - power) / 50d, 2) : 0;
        double water = (1 - enemy) * (1 - junk) * (1 - CrateChance(player));
        FishingCatchOdds odds = new() { Filler = enemy + (1 - enemy) * junk * (7d / 8) };
        Span<double> chances = [Rarity(power, 150, 2), Rarity(power, 300, 3), Rarity(power, 1050, 4),
            Rarity(power, 2250, 5), Rarity(power, 4500, 6)];

        for (int mask = 0; mask < 32; mask++)
        {
            Quality q = new((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0, (mask & 16) != 0);
            odds.remaining = water;
            for (int i = 0; i < chances.Length; i++)
                odds.remaining *= (mask & (1 << i)) != 0 ? chances[i] : 1 - chances[i];

            if (q.Legendary)
            {
                if (Main.bloodMoon)
                    odds.Keep(NPC.combatBookWasUsed ? 0.5 : 2d / 3);
                odds.Keep(1 - 0.8 * 0.8 * 0.9);
            }
            else if (!q.VeryRare && q.Uncommon)
                odds.Keep(0.2);

            if (dungeon)
            {
                if (q.VeryRare)
                    odds.AlchemyTable += odds.Take(1d / 7);
            }
            else
            {
                bool scalyTruffle = snow && attempt.heightLevel == 3 && q.Legendary && context.IsHardmode;
                if (corrupt && crimson)
                {
                    odds.remaining *= 0.5;
                    double region = odds.remaining;
                    odds.Corruption(q, context.IsHardmode, scalyTruffle);
                    double corruptRemaining = odds.remaining;
                    odds.remaining = region;
                    odds.Crimson(q, attempt.questFish, context.IsHardmode, scalyTruffle);
                    odds.remaining += corruptRemaining;
                }
                else if (corrupt)
                    odds.Corruption(q, context.IsHardmode, scalyTruffle);
                else if (crimson)
                    odds.Crimson(q, attempt.questFish, context.IsHardmode, scalyTruffle);
                else if (player.ZoneHallow)
                    odds.Hallow(q, attempt, context.IsHardmode, scalyTruffle, desert);

                if (player.ZoneGlowshroom && q.Uncommon && attempt.questFish == ItemID.AmanitaFungifin)
                    odds.Keep();
                odds.SnowAndJungle(q, attempt, snow, jungle);
            }

            if (ocean > 0)
            {
                double inland = odds.remaining * (1 - ocean);
                odds.remaining *= ocean;
                odds.Ocean(q, attempt.questFish);
                odds.remaining = inland;
            }
            if (!desert)
                odds.Inland(q, attempt);
        }
        return odds;
    }

    private double Take(double chance)
    {
        double taken = remaining * chance;
        remaining -= taken;
        return taken;
    }

    private void Keep(double chance = 1) => Take(chance);
    private void Reroll(double chance = 1) => Filler += Take(chance);

    // Quest gates match the engine location tests in FishingCheck_RollItemDrop.
    private void Corruption(Quality q, bool hardmode, bool scalyTruffle)
    {
        if (scalyTruffle)
            Keep(2d / 3);
        if (q.Legendary && hardmode)
            Toxikarp += Take(0.5);
        if (q.Rare || q.Uncommon)
            Keep(); // Clubberfish, evil quest fish, or Ebonkoi.
    }

    private void Crimson(Quality q, int quest, bool hardmode, bool scalyTruffle)
    {
        if (scalyTruffle)
            Keep(2d / 3);
        if (q.Legendary && hardmode)
            Bladetongue += Take(0.5);
        if (q.Uncommon)
        {
            if (quest is ItemID.BloodyManowar or ItemID.Ichorfish)
                Keep();
            else
                Reroll(); // Hemopiranha.
        }
        else if (q.Common)
            Keep();
    }

    private void Hallow(Quality q, FishingAttempt attempt, bool hardmode, bool scalyTruffle, bool desert)
    {
        if (desert)
            Keep(0.5);
        if (scalyTruffle)
            Keep(2d / 3);
        if (q.Legendary && hardmode)
        {
            CrystalSerpent += Take(0.5);
            Keep(2d / 3); // Lady of the Lake.
        }

        bool deep = attempt.heightLevel > 1;
        int quest = attempt.questFish;
        if ((deep && q.VeryRare) || (q.Uncommon && (deep ? quest == ItemID.MirageFish : quest == ItemID.Pixiefish)))
            Keep();
        if (q.Rare)
        {
            if (hardmode)
                Keep();
            else
                Reroll(); // Prismite before Hardmode.
        }
        if (q.Uncommon)
        {
            if (quest == ItemID.UnicornFish)
                Keep();
            else
                Reroll(); // Princess Fish.
        }
    }

    private void SnowAndJungle(Quality q, FishingAttempt attempt, bool snow, double jungle)
    {
        if (remaining <= 0)
            return;
        double region = remaining;
        remaining = region * (1 - jungle);
        if (snow)
            Snow(q, attempt);
        double withoutJungle = remaining;

        remaining = region * jungle * (snow ? 0.5 : 1);
        Jungle(q, attempt);
        double withoutSnow = remaining;
        remaining = snow ? region * jungle * 0.5 : 0;
        if (snow && jungle > 0)
        {
            Snow(q, attempt);
            Jungle(q, attempt);
        }
        remaining += withoutJungle + withoutSnow;
    }

    private void Snow(Quality q, FishingAttempt attempt)
    {
        int layer = attempt.heightLevel;
        int quest = attempt.questFish;
        if (q.Uncommon && (quest == ItemID.Pengfish && layer < 2 || quest == ItemID.TundraTrout && layer == 1 ||
            quest is ItemID.Fishron or ItemID.MutantFlinxfin && layer >= 2))
            Keep();

        if (q.Common)
            FrostDaggerfish += Take(1d / 12);
        if (q.Uncommon)
        {
            FrostDaggerfish += Take(1d / 6);
            Keep(); // Frost Minnow.
        }
        else if (q.Common)
            Reroll(); // Atlantic Cod.
        else if (layer > 1)
            Reroll(1d / 3); // Specular Fish.
    }

    private void Jungle(Quality q, FishingAttempt attempt)
    {
        int layer = attempt.heightLevel;
        int quest = attempt.questFish;
        if (q.Uncommon)
        {
            if (layer == 1 && quest is ItemID.Catfish or ItemID.Derpfish or ItemID.TropicalBarracuda || layer >= 1 && quest == ItemID.Mudfish || layer > 1)
                Keep(); // Quest fish or Variegated Lardfish.
            else
                Reroll(); // Double Cod.
        }
        else if (q.Common)
            Reroll(); // Neon Tetra.
    }

    private void Ocean(Quality q, int quest)
    {
        if (q.VeryRare)
            Keep(); // Sharks.
        if (q.Rare)
            Keep(1 - 0.8 * (2d / 3)); // Pink Jellyfish and Swordfish.
        if (q.Uncommon && quest is ItemID.CapnTunabeard or ItemID.Clownfish)
            Keep();

        if (q.Uncommon || !q.Common)
            Reroll(); // Shrimp or Trout.
        else
            Keep();
    }

    private void Inland(Quality q, FishingAttempt attempt)
    {
        int layer = attempt.heightLevel;
        int quest = attempt.questFish;
        if (q.Uncommon &&
            (layer < 2 && quest is ItemID.Harpyfish or ItemID.FallenStarfish or ItemID.TheFishofCthulu ||
             layer == 0 || // Sky quest fish and Damselfish.
             layer is 1 or 2 && quest == ItemID.Dirtfish ||
             layer == 1 && quest is ItemID.Bunnyfish or ItemID.DynamiteFish or ItemID.ZombieFish))
            Keep();

        if (layer > 1)
        {
            if (q.Rare)
                Keep(0.2); // Jellyfish.
            if (q.Legendary)
                Keep(2d / 3); // Golden Carp.
            if (q.VeryRare)
                Keep(0.5); // Rockfish.
            if (q.Rare)
                Reroll(); // Stinkfish.
            if (q.Uncommon)
            {
                if (quest is ItemID.Bonefish or ItemID.Batfish or ItemID.Jewelfish or ItemID.Spiderfish || layer > 2 && quest is ItemID.Hungerfish or ItemID.DemonicHellfish or ItemID.GuideVoodooFish or ItemID.Fishotron)
                    Keep();
                Keep(0.75); // Armored Cavefish.
            }

            double cavefishOrSpecular = q.Uncommon || q.Common ? 1 : 0.25;
            Reroll(cavefishOrSpecular * 0.75);
            Keep(cavefishOrSpecular * 0.25 / (1 - cavefishOrSpecular * 0.75));
        }

        if (q.Uncommon && quest == ItemID.Slimefish || attempt.waterTilesCount > 1000 && q.Common)
            Keep(); // Slimefish or Salmon.
        Reroll(); // Bass.
    }
}
