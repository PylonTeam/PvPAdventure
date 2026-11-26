using PvPAdventure.Core.Config;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace PvPAdventure.Common.Fishing;

internal static class FishingCatchBoosts
{
    private const double WeaponCap = 0.03;
    private const double FrostDaggerfishCap = 0.06;
    private const double BonusMultiplier = 5;

    public static bool TryReplacePotionFish(FishingCatchContext context, FishingSystemConfig config, double potionFishShare, out int item)
    {
        item = ItemID.None;
        Player player = context.Player;
        bool alchemyTable = config.EnableAlchemyTableBoost && player.ZoneDungeon && NPC.downedBoss3;
        bool crystalSerpent = config.EnableCrystalSerpentBoost && context.IsHardmode && player.ZoneHallow;
        bool toxikarp = config.EnableToxikarpBoost && context.IsHardmode && player.ZoneCorrupt;
        bool bladetongue = config.EnableBladetongueBoost && context.IsHardmode && player.ZoneCrimson;
        if (context.Attempt.fishingLevel <= 0 || !(alchemyTable || crystalSerpent || toxikarp || bladetongue))
            return false;

        var odds = FishingCatchOdds.ForWater(context, config);
        Span<(int Item, double Extra)> boosts =
        [
            (ItemID.AlchemyTable, alchemyTable ? odds.AlchemyTable * (BonusMultiplier - 1) : 0),
            (ItemID.CrystalSerpent, crystalSerpent ? ExtraChance(odds.CrystalSerpent, BonusMultiplier, WeaponCap) : 0),
            (ItemID.Toxikarp, toxikarp ? ExtraChance(odds.Toxikarp, BonusMultiplier, WeaponCap) : 0),
            (ItemID.Bladetongue, bladetongue ? ExtraChance(odds.Bladetongue, BonusMultiplier, WeaponCap) : 0),
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

    public static bool TryReplaceBombFish(FishingCatchContext context, FishingSystemConfig config, double bombFishShare)
    {
        if (context.Attempt.fishingLevel <= 0 || !context.Player.ZoneSnow ||
            (context.Player.ZoneDungeon && NPC.downedBoss3))
            return false;

        var odds = FishingCatchOdds.ForWater(context, config);
        double extra = ExtraChance(odds.FrostDaggerfish, 1.5, FrostDaggerfishCap);
        double budget = odds.Filler * bombFishShare;
        return extra > 0 && budget > 0 && Main.rand.NextDouble() * budget < extra;
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

        return empty > 0 && Main.rand.NextDouble() * empty < native * (BonusMultiplier - 1);
    }

    private static double ExtraChance(double native, double multiplier, double cap) =>
        Math.Max(0, Math.Min(native * (multiplier - 1), cap - native));
}

/// <summary>Projects base Terraria water rolls onto the buff targets and the server's reroll budget.</summary>
internal struct FishingCatchOdds
{
    public double Filler { get; private set; }
    public double AlchemyTable { get; private set; }
    public double CrystalSerpent { get; private set; }
    public double Toxikarp { get; private set; }
    public double Bladetongue { get; private set; }
    public double FrostDaggerfish { get; private set; }

    private double remaining;
    private HashSet<int> rerolledItems;

    private readonly record struct Quality(bool Common, bool Uncommon, bool Rare, bool VeryRare, bool Legendary);

    public static double Rarity(int power, int scale, int minimum) => 1d / Math.Max(minimum, scale / power);
    public static double CrateChance(Player player) => player.cratePotion ? 0.25 : 0.1;

    public static FishingCatchOdds ForWater(FishingCatchContext context) =>
        ForWater(context, FishingCatchRules.Config);

    internal static FishingCatchOdds ForWater(FishingCatchContext context, FishingSystemConfig config)
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
        double water = (1 - enemy) * (1 - junk);
        double crateChance = CrateChance(player);
        bool rerollsCrates = false;
        FishingCatchOdds odds = new() { rerolledItems = [] };
        if (config.RerollCatchesEnabled)
        {
            odds.Filler = enemy;
            if (config.RerolledCatches is not null)
            {
                foreach (var item in config.RerolledCatches)
                {
                    int type = item?.Type ?? ItemID.None;
                    if (type <= ItemID.None || type == attempt.questFish)
                        continue;
                    odds.rerolledItems.Add(type);
                    if (type < ItemID.Sets.IsFishingCrate.Length && ItemID.Sets.IsFishingCrate[type])
                        rerollsCrates = true;
                }
            }
            if (!context.IsHardmode)
                odds.rerolledItems.Add(ItemID.Prismite);
        }
        odds.remaining = (1 - enemy) * junk;
        odds.Catch(ItemID.JojaCola, 1d / 8);
        odds.Catch(ItemID.OldShoe, 1d / 3);
        odds.Catch(ItemID.TinCan, 1d / 2);
        odds.Catch(ItemID.FishingSeaweed);

        Span<double> chances = [Rarity(power, 150, 2), Rarity(power, 300, 3), Rarity(power, 1050, 4),
            Rarity(power, 2250, 5), Rarity(power, 4500, 6)];

        for (int mask = 0; mask < 32; mask++)
        {
            Quality q = new((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0, (mask & 16) != 0);
            odds.remaining = water;
            for (int i = 0; i < chances.Length; i++)
                odds.remaining *= (mask & (1 << i)) != 0 ? chances[i] : 1 - chances[i];

            double itemChance = odds.remaining * (1 - crateChance);
            if (rerollsCrates)
            {
                odds.remaining *= crateChance;
                odds.Crates(context, q, dungeon, corrupt, crimson, jungle);
            }
            odds.remaining = itemChance;

            if (q.Legendary)
            {
                if (Main.bloodMoon)
                {
                    if (!NPC.combatBookWasUsed)
                        odds.Catch(ItemID.CombatBook, 1d / 3);
                    odds.Catch(ItemID.DreadoftheRedSea, 0.5);
                }
                odds.Catch(ItemID.FrogLeg, 0.2);
                odds.Catch(ItemID.BalloonPufferfish, 0.2);
                odds.Catch(ItemID.ZephyrFish, 0.1);
            }
            else if (!q.VeryRare && q.Uncommon)
                odds.Catch(ItemID.BombFish, 0.2);

            if (dungeon)
            {
                if (q.VeryRare)
                    odds.AlchemyTable += odds.Catch(ItemID.AlchemyTable, 1d / 7);
            }
            else
            {
                bool scalyTruffle = snow && attempt.heightLevel == 3 && q.Legendary && context.IsHardmode;
                if (corrupt && crimson)
                {
                    odds.remaining *= 0.5;
                    double region = odds.remaining;
                    odds.Corruption(q, attempt.questFish, context.IsHardmode, scalyTruffle);
                    double corruptRemaining = odds.remaining;
                    odds.remaining = region;
                    odds.Crimson(q, attempt.questFish, context.IsHardmode, scalyTruffle);
                    odds.remaining += corruptRemaining;
                }
                else if (corrupt)
                    odds.Corruption(q, attempt.questFish, context.IsHardmode, scalyTruffle);
                else if (crimson)
                    odds.Crimson(q, attempt.questFish, context.IsHardmode, scalyTruffle);
                else if (player.ZoneHallow)
                    odds.Hallow(q, attempt, context.IsHardmode, scalyTruffle, desert);

                if (player.ZoneGlowshroom && q.Uncommon && attempt.questFish == ItemID.AmanitaFungifin)
                    odds.Catch(attempt.questFish);
                odds.SnowAndJungle(q, attempt, snow, jungle);
            }

            if (ocean > 0)
            {
                double inland = odds.remaining * (1 - ocean);
                odds.remaining *= ocean;
                odds.Ocean(q, attempt.questFish);
                odds.remaining = inland;
            }
            if (desert)
                odds.Desert(q, attempt.questFish);
            else
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

    private double Catch(int item, double chance = 1)
    {
        double amount = Take(chance);
        if (amount > 0 && rerolledItems.Contains(item))
            Filler += amount;
        return amount;
    }

    private void Crates(FishingCatchContext context, Quality q, bool dungeon, bool corrupt, bool crimson, double jungle)
    {
        Player player = context.Player;
        FishingAttempt attempt = context.Attempt;
        bool hardmode = context.IsHardmode;
        if (q.Rare)
        {
            if (dungeon)
                Catch(hardmode ? ItemID.DungeonFishingCrateHard : ItemID.DungeonFishingCrate);
            if (player.ZoneBeach)
                Catch(hardmode ? ItemID.OceanCrateHard : ItemID.OceanCrate);
            else if (Main.remixWorld && attempt.heightLevel == 1 && attempt.Y >= Main.rockLayer)
                Catch(hardmode ? ItemID.OceanCrateHard : ItemID.OceanCrate, 0.5);

            if (corrupt)
                Catch(hardmode ? ItemID.CorruptFishingCrateHard : ItemID.CorruptFishingCrate, crimson ? 0.5 : 1);
            if (crimson)
                Catch(hardmode ? ItemID.CrimsonFishingCrateHard : ItemID.CrimsonFishingCrate);
            if (player.ZoneHallow)
                Catch(hardmode ? ItemID.HallowedFishingCrateHard : ItemID.HallowedFishingCrate);
            if (jungle > 0)
                Catch(hardmode ? ItemID.JungleFishingCrateHard : ItemID.JungleFishingCrate, jungle);
            if (player.ZoneSnow)
                Catch(hardmode ? ItemID.FrozenCrateHard : ItemID.FrozenCrate);
            if (player.ZoneDesert)
                Catch(hardmode ? ItemID.OasisCrateHard : ItemID.OasisCrate);
            if (attempt.heightLevel == 0)
                Catch(hardmode ? ItemID.FloatingIslandFishingCrateHard : ItemID.FloatingIslandFishingCrate);
        }
        if (q.VeryRare || q.Legendary)
            Catch(hardmode ? ItemID.GoldenCrateHard : ItemID.GoldenCrate);
        else if (q.Uncommon)
            Catch(hardmode ? ItemID.IronCrateHard : ItemID.IronCrate);
        else
            Catch(hardmode ? ItemID.WoodenCrateHard : ItemID.WoodenCrate);
    }

    private void Desert(Quality q, int quest)
    {
        if (q.Uncommon)
            Catch(quest is ItemID.ScarabFish or ItemID.ScorpioFish ? quest : ItemID.Oyster);
        else
        {
            Catch(ItemID.RockLobster, 1d / 3);
            Catch(ItemID.Flounder);
        }
    }

    // Quest gates match the engine location tests in FishingCheck_RollItemDrop.
    private void Corruption(Quality q, int quest, bool hardmode, bool scalyTruffle)
    {
        if (scalyTruffle)
            Catch(ItemID.ScalyTruffle, 2d / 3);
        if (q.Legendary && hardmode)
            Toxikarp += Catch(ItemID.Toxikarp, 0.5);
        if (q.Rare)
            Catch(ItemID.PurpleClubberfish);
        else if (q.Uncommon)
            Catch(quest is ItemID.Cursedfish or ItemID.InfectedScabbardfish or ItemID.EaterofPlankton ? quest : ItemID.Ebonkoi);
    }

    private void Crimson(Quality q, int quest, bool hardmode, bool scalyTruffle)
    {
        if (scalyTruffle)
            Catch(ItemID.ScalyTruffle, 2d / 3);
        if (q.Legendary && hardmode)
            Bladetongue += Catch(ItemID.Bladetongue, 0.5);
        if (q.Uncommon)
            Catch(quest is ItemID.BloodyManowar or ItemID.Ichorfish ? quest : ItemID.Hemopiranha);
        else if (q.Common)
            Catch(ItemID.CrimsonTigerfish);
    }

    private void Hallow(Quality q, FishingAttempt attempt, bool hardmode, bool scalyTruffle, bool desert)
    {
        if (desert)
        {
            double region = remaining;
            remaining *= 0.5;
            Desert(q, attempt.questFish);
            remaining = region * 0.5;
        }
        if (scalyTruffle)
            Catch(ItemID.ScalyTruffle, 2d / 3);
        if (q.Legendary && hardmode)
        {
            CrystalSerpent += Catch(ItemID.CrystalSerpent, 0.5);
            Catch(ItemID.LadyOfTheLake, 2d / 3);
        }

        bool deep = attempt.heightLevel > 1;
        int quest = attempt.questFish;
        if (deep && q.VeryRare)
            Catch(ItemID.ChaosFish);
        else if (q.Uncommon && (deep ? quest == ItemID.MirageFish : quest == ItemID.Pixiefish))
            Catch(quest);
        else if (q.Rare)
            Catch(ItemID.Prismite);
        else if (q.Uncommon)
            Catch(quest == ItemID.UnicornFish ? quest : ItemID.PrincessFish);
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
            Catch(quest);

        if (q.Common)
            FrostDaggerfish += Catch(ItemID.FrostDaggerfish, 1d / 12);
        if (q.Uncommon)
        {
            FrostDaggerfish += Catch(ItemID.FrostDaggerfish, 1d / 6);
            Catch(ItemID.FrostMinnow);
        }
        else if (q.Common)
            Catch(ItemID.AtlanticCod);
        else if (layer > 1)
            Catch(ItemID.SpecularFish, 1d / 3);
    }

    private void Jungle(Quality q, FishingAttempt attempt)
    {
        int layer = attempt.heightLevel;
        int quest = attempt.questFish;
        if (q.Uncommon)
        {
            if (layer == 1 && quest is ItemID.Catfish or ItemID.Derpfish or ItemID.TropicalBarracuda || layer >= 1 && quest == ItemID.Mudfish)
                Catch(quest);
            else
                Catch(layer > 1 ? ItemID.VariegatedLardfish : ItemID.DoubleCod);
        }
        else if (q.Common)
            Catch(ItemID.NeonTetra);
    }

    private void Ocean(Quality q, int quest)
    {
        if (q.VeryRare)
        {
            Catch(ItemID.ReaverShark, 0.5);
            Catch(ItemID.SawtoothShark);
        }
        if (q.Rare)
        {
            Catch(ItemID.PinkJellyfish, 0.2);
            Catch(ItemID.Swordfish, 1d / 3);
        }
        if (q.Uncommon)
            Catch(quest is ItemID.CapnTunabeard or ItemID.Clownfish ? quest : ItemID.Shrimp);
        else if (q.Common)
        {
            Catch(ItemID.RedSnapper, 0.5);
            Catch(ItemID.Tuna);
        }
        else
            Catch(ItemID.Trout);
    }

    private void Inland(Quality q, FishingAttempt attempt)
    {
        int layer = attempt.heightLevel;
        int quest = attempt.questFish;
        if (q.Uncommon)
        {
            if (layer < 2 && quest is ItemID.Harpyfish or ItemID.FallenStarfish or ItemID.TheFishofCthulu ||
                layer == 0 && quest is ItemID.Cloudfish or ItemID.Wyverntail or ItemID.Angelfish ||
                layer is 1 or 2 && quest == ItemID.Dirtfish ||
                layer == 1 && quest is ItemID.Bunnyfish or ItemID.DynamiteFish or ItemID.ZombieFish)
                Catch(quest);
            if (layer == 0)
                Catch(ItemID.Damselfish);
        }

        if (layer > 1)
        {
            if (q.Rare)
            {
                double jellyfishRoll = remaining;
                remaining *= 0.2;
                if (Main.hardMode)
                    Catch(ItemID.GreenJellyfish, 0.5);
                Catch(ItemID.BlueJellyfish);
                remaining = jellyfishRoll * 0.8;
            }
            if (q.Legendary)
                Catch(ItemID.GoldenCarp, 2d / 3);
            if (q.VeryRare)
                Catch(ItemID.Rockfish, 0.5);
            if (q.Rare)
                Catch(ItemID.Stinkfish);
            if (q.Uncommon)
            {
                if (quest is ItemID.Bonefish or ItemID.Batfish or ItemID.Jewelfish or ItemID.Spiderfish || layer > 2 && quest is ItemID.Hungerfish or ItemID.DemonicHellfish or ItemID.GuideVoodooFish or ItemID.Fishotron)
                    Catch(quest);
                Catch(ItemID.ArmoredCavefish, 0.75);
            }

            double region = remaining;
            double cavefishOrSpecular = q.Uncommon || q.Common ? 1 : 0.25;
            remaining *= cavefishOrSpecular;
            Catch(ItemID.ArmoredCavefish, 0.25);
            Catch(ItemID.SpecularFish);
            remaining = region * (1 - cavefishOrSpecular);
        }

        if (q.Uncommon && quest == ItemID.Slimefish)
            Catch(quest);
        else if (attempt.waterTilesCount > 1000 && q.Common)
            Catch(ItemID.Salmon);
        else
            Catch(ItemID.Bass);
    }
}
