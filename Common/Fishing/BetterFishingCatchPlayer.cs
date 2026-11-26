using Microsoft.Xna.Framework;
using PvPAdventure.Core.Config;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Fishing;

/// <summary>
/// Converts junk, filler fish, and fishable NPCs into PvPAdventure rewards.
/// Native catches keep their original item. Targeted boosts use filler or empty rolls.
/// </summary>
public class BetterFishingCatchPlayer : ModPlayer
{
    public override void CatchFish(FishingAttempt attempt, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        FishingCatchContext context = new(Player, attempt, itemDrop, npcSpawn);
        FishingCatchRules.Apply(context, ref itemDrop, ref npcSpawn, ref sonar, ref sonarPosition);
    }
}

/// <summary>
/// Small immutable catch context to avoid passing long argument lists through fishing rules.
/// </summary>
public readonly record struct FishingCatchContext(Player Player, FishingAttempt Attempt, int VanillaItemDrop, int VanillaNpcSpawn)
{
    public bool HasPlayer => Player?.active == true;
    public bool IsWater => !Attempt.inLava && !Attempt.inHoney;
    public bool IsHardmode => Main.hardMode;
    public bool IsVanillaNpcCatch => VanillaNpcSpawn > 0;
    public bool IsVanillaItemCatch => VanillaItemDrop > 0;
}

/// <summary>
/// Applies the server's fishing settings to the owner's catch.
/// </summary>
public static class FishingCatchRules
{
    private const int BiomeCrateWeight = 10;
    private static readonly FishingSystemConfig DefaultConfig = new();

    internal static FishingSystemConfig Config => ModContent.GetInstance<ServerConfig>()?.FishingSystem ?? DefaultConfig;

    private enum Pool
    {
        Crates = 1,
        PotionFish,
        Utility,
        Supplies,
    }

    private static readonly WeightedCatch[] PreHardmodeCrates =
    [
        new(ItemID.WoodenCrate, 50),
        new(ItemID.IronCrate, 30),
        new(ItemID.GoldenCrate, 10),
        new(ItemID.None, BiomeCrateWeight),
    ];

    private static readonly WeightedCatch[] HardmodeCrates =
    [
        new(ItemID.WoodenCrateHard, 45),
        new(ItemID.IronCrateHard, 35),
        new(ItemID.GoldenCrateHard, 10),
        new(ItemID.None, BiomeCrateWeight),
    ];

    public static void Apply(FishingCatchContext context, ref int itemDrop, ref int npcSpawn,
        ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition) =>
        Apply(context, Config, ref itemDrop, ref npcSpawn);

    internal static void Apply(FishingCatchContext context, FishingSystemConfig config, ref int itemDrop, ref int npcSpawn)
    {
        if (!context.HasPlayer || !config.RerollCatchesEnabled)
            return;

        if (!context.IsWater)
        {
            bool boostEnabled = context.Attempt.inLava ? config.EnableLavaBoost : config.EnableHoneyBoost;
            if (boostEnabled && !context.IsVanillaNpcCatch && !context.IsVanillaItemCatch &&
                FishingCatchBoosts.TryFillEmptyLiquidRoll(context, out int liquidCatch))
                SetItemCatch(liquidCatch, ref itemDrop, ref npcSpawn);
            return;
        }

        if (!context.IsVanillaNpcCatch && context.IsVanillaItemCatch && context.VanillaItemDrop == context.Attempt.questFish)
            return;

        bool blockPrismite = !context.IsHardmode && context.VanillaItemDrop == ItemID.Prismite;
        bool reroll = context.IsVanillaNpcCatch ||
            (context.IsVanillaItemCatch && (blockPrismite || IsFillerCatch(context.VanillaItemDrop, config)));
        if (reroll)
        {
            int replacement = RollGoodCatch(context, config);
            if (replacement > ItemID.None)
            {
                SetItemCatch(replacement, ref itemDrop, ref npcSpawn);
                return;
            }
        }

        if (blockPrismite)
            itemDrop = ItemID.None;
    }

    internal static bool IsFillerCatch(int itemType) => IsFillerCatch(itemType, Config);

    private static bool IsFillerCatch(int itemType, FishingSystemConfig config)
    {
        if (itemType <= ItemID.None || config.RerolledCatches is null)
            return false;
        foreach (var item in config.RerolledCatches)
        {
            if (item?.Type == itemType)
                return true;
        }
        return false;
    }

    private static int RollGoodCatch(FishingCatchContext context, FishingSystemConfig config)
    {
        bool ocean = IsOceanUtilityPond(context.Attempt);
        bool underground = context.Attempt.heightLevel > 1;
        bool blockPrismite = !context.IsHardmode;

        var potionFish = new EligiblePool(config.PotionFish, ocean, underground, blockPrismite);
        var utility = new EligiblePool(config.Utility, ocean, underground, blockPrismite);
        var supplies = new EligiblePool(config.FishingSupplies, ocean, underground, blockPrismite);
        int cratesWeight = FishingSystemConfig.ClampWeight(config.Crates?.Weight ?? 0);
        int totalWeight = cratesWeight + potionFish.Weight + utility.Weight + supplies.Weight;
        if (totalWeight == 0)
            return ItemID.None;

        ReadOnlySpan<WeightedCatch> pools =
        [
            new((int)Pool.Crates, cratesWeight),
            new((int)Pool.PotionFish, potionFish.Weight),
            new((int)Pool.Utility, utility.Weight),
            new((int)Pool.Supplies, supplies.Weight),
        ];
        switch ((Pool)WeightedCatchPool.Roll(pools))
        {
            case Pool.Crates:
                return RollCrate(context);
            case Pool.PotionFish:
                if (FishingCatchBoosts.TryReplacePotionFish(context, config, (double)potionFish.Weight / totalWeight, out int bonus))
                    return bonus;
                return potionFish.Roll();
            case Pool.Utility:
                int item = utility.Roll();
                if (item == ItemID.BombFish && config.EnableFrostDaggerfishBoost)
                {
                    double share = (double)utility.Weight / totalWeight * utility.BombFishShare;
                    if (FishingCatchBoosts.TryReplaceBombFish(context, config, share))
                        return ItemID.FrostDaggerfish;
                }
                return item;
            case Pool.Supplies:
                return supplies.Roll();
            default:
                return ItemID.None;
        }
    }

    internal static bool IsOceanUtilityPond(FishingAttempt attempt) =>
        attempt.heightLevel <= 1 && attempt.waterTilesCount > 1000 &&
        (attempt.X < 380 || attempt.X > Main.maxTilesX - 380);

    private readonly struct EligiblePool
    {
        private readonly FishingRewardPoolConfig pool;
        private readonly bool ocean;
        private readonly bool underground;
        private readonly bool blockPrismite;
        private readonly int totalItemWeight;
        private readonly int bombFishWeight;

        public int Weight => totalItemWeight > 0 ? FishingSystemConfig.ClampWeight(pool.Weight) : 0;
        public double BombFishShare => (double)bombFishWeight / totalItemWeight;

        public EligiblePool(FishingRewardPoolConfig pool, bool ocean, bool underground, bool blockPrismite)
        {
            this.pool = pool;
            this.ocean = ocean;
            this.underground = underground;
            this.blockPrismite = blockPrismite;

            if (pool?.Items is null || pool.Weight <= 0)
                return;
            foreach (var entry in pool.Items)
            {
                int item = entry?.Item?.Type ?? ItemID.None;
                if (!CanCatch(item))
                    continue;
                int weight = FishingSystemConfig.ClampWeight(entry.Weight);
                totalItemWeight += weight;
                if (item == ItemID.BombFish)
                    bombFishWeight += weight;
            }
        }

        public int Roll()
        {
            if (totalItemWeight <= 0)
                return ItemID.None;
            int roll = Main.rand.Next(totalItemWeight);
            foreach (var entry in pool.Items)
            {
                int item = entry?.Item?.Type ?? ItemID.None;
                if (!CanCatch(item))
                    continue;
                int weight = FishingSystemConfig.ClampWeight(entry.Weight);
                if (roll < weight)
                    return item;
                roll -= weight;
            }
            return ItemID.None;
        }

        private bool CanCatch(int item) => item switch
        {
            ItemID.ReaverShark or ItemID.SawtoothShark or ItemID.Swordfish => ocean,
            ItemID.Rockfish => underground,
            ItemID.Prismite => !blockPrismite,
            _ => item > ItemID.None,
        };
    }

    internal static ReadOnlySpan<WeightedCatch> GetCrateTiers(bool hardmode) =>
        hardmode ? HardmodeCrates : PreHardmodeCrates;

    private static int RollCrate(FishingCatchContext context)
    {
        int crate = WeightedCatchPool.Roll(GetCrateTiers(context.IsHardmode));
        return crate == ItemID.None ? RollBiomeCrate(context) : crate;
    }

    private static int RollBiomeCrate(FishingCatchContext context)
    {
        // Match the native water-crate biome priority and special seed rules.
        Player player = context.Player;
        FishingAttempt attempt = context.Attempt;
        bool hardmode = context.IsHardmode;
        if (player.ZoneDungeon && NPC.downedBoss3)
            return hardmode ? ItemID.DungeonFishingCrateHard : ItemID.DungeonFishingCrate;
        if (player.ZoneBeach || (Main.remixWorld && attempt.heightLevel == 1 && attempt.Y >= Main.rockLayer && Main.rand.Next(2) == 0))
            return hardmode ? ItemID.OceanCrateHard : ItemID.OceanCrate;

        if (!(Main.remixWorld && attempt.heightLevel == 0))
        {
            bool corrupt = player.ZoneCorrupt;
            bool crimson = player.ZoneCrimson;
            if (corrupt && (!crimson || Main.rand.Next(2) == 0))
                return hardmode ? ItemID.CorruptFishingCrateHard : ItemID.CorruptFishingCrate;
            if (crimson)
                return hardmode ? ItemID.CrimsonFishingCrateHard : ItemID.CrimsonFishingCrate;
        }

        if (player.ZoneHallow)
            return hardmode ? ItemID.HallowedFishingCrateHard : ItemID.HallowedFishingCrate;
        if (player.ZoneJungle && (!Main.notTheBeesWorld || Main.remixWorld || Main.rand.Next(2) != 0))
            return hardmode ? ItemID.JungleFishingCrateHard : ItemID.JungleFishingCrate;
        if (player.ZoneSnow)
            return hardmode ? ItemID.FrozenCrateHard : ItemID.FrozenCrate;
        if (player.ZoneDesert)
            return hardmode ? ItemID.OasisCrateHard : ItemID.OasisCrate;
        if (attempt.heightLevel == 0)
            return hardmode ? ItemID.FloatingIslandFishingCrateHard : ItemID.FloatingIslandFishingCrate;

        // Send the biome share to the common crate when no region qualifies.
        return GetCrateTiers(hardmode)[0].ItemType;
    }

    private static void SetItemCatch(int itemType, ref int itemDrop, ref int npcSpawn)
    {
        itemDrop = itemType;
        npcSpawn = -1;
    }
}

/// <summary>
/// Represents one weighted item/catch entry.
/// </summary>
public readonly record struct WeightedCatch(int ItemType, int Weight);

/// <summary>
/// Minimal weighted random helper for fishing reward pools.
/// </summary>
public static class WeightedCatchPool
{
    public static int Roll(WeightedCatch[] entries) => Roll(entries.AsSpan());

    internal static int Roll(ReadOnlySpan<WeightedCatch> entries)
    {
        int total = 0;

        for (int i = 0; i < entries.Length; i++)
            total += Math.Max(0, entries[i].Weight);

        if (total <= 0)
            return ItemID.None;

        int roll = Main.rand.Next(total);

        for (int i = 0; i < entries.Length; i++)
        {
            int weight = Math.Max(0, entries[i].Weight);

            if (roll < weight)
                return entries[i].ItemType;

            roll -= weight;
        }

        return entries[^1].ItemType;
    }
}
