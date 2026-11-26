using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using PvPFramework.Core.Configs.ConfigElements;
using Terraria.ID;
using Terraria.ModLoader.Config;
using FrameworkAssets = PvPFramework.Core.Utilities.Ass;

namespace PvPAdventure.Core.Config;

public sealed class FishingPoolEntry
{
    public ItemDefinition Item = new();

    [Range(0, FishingSystemConfig.MaxWeight)]
    [DefaultValue(1)]
    public int Weight = 1;
}

public sealed class FishingRewardPoolConfig
{
    [Range(0, FishingSystemConfig.MaxWeight)]
    public int Weight;

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [Expand(false, false)]
    [CustomModConfigItem(typeof(FishingItemListElement))]
    public List<FishingPoolEntry> Items = [];
}

public sealed class FishingCratePoolConfig
{
    internal const int DefaultWeight = 45;

    [Range(0, FishingSystemConfig.MaxWeight)]
    [DefaultValue(DefaultWeight)]
    public int Weight = DefaultWeight;

    [JsonIgnore, ShowDespiteJsonIgnore, CustomModConfigItem(typeof(FishingCrateTiersElement))]
    public string PreHardmode => FishingConfigText.CrateTiers(false);

    [JsonIgnore, ShowDespiteJsonIgnore, CustomModConfigItem(typeof(FishingCrateTiersElement))]
    public string Hardmode => FishingConfigText.CrateTiers(true);
}

public sealed class FishingSystemConfig
{
    internal const int MaxWeight = 1000;

    [ConfigIcon(nameof(FrameworkAssets.IconCheckGreen), nameof(FrameworkAssets.IconXGray), grayWhenOff: true)]
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public bool RerollCatchesEnabled
    {
        get => rerollCatchesEnabled ?? true;
        set => rerollCatchesEnabled = value;
    }

    [RequiresField(nameof(RerollCatchesEnabled))]
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [Expand(false, false)]
    public List<ItemDefinition> RerolledCatches { get; set; } = CreateRerolledCatches();

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.WoodenCrate)]
    [Expand(false, false)]
    [CustomModConfigItem(typeof(FishingCratePoolElement))]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public FishingCratePoolConfig Crates { get; set; } = new();

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.BombFish)]
    [Expand(false, false)]
    [CustomModConfigItem(typeof(FishingPoolElement))]
    public FishingRewardPoolConfig Utility { get; set; } = CreateUtility();

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.ArmoredCavefish)]
    [Expand(false, false)]
    [CustomModConfigItem(typeof(FishingPoolElement))]
    public FishingRewardPoolConfig PotionFish { get; set; } = CreatePotionFish();

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.MasterBait)]
    [Expand(false, false)]
    [CustomModConfigItem(typeof(FishingPoolElement))]
    public FishingRewardPoolConfig FishingSupplies { get; set; } = CreateFishingSupplies();

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.AlchemyTable)]
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public bool EnableAlchemyTableBoost
    {
        get => alchemyTableBoost ?? true;
        set => alchemyTableBoost = value;
    }

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.FrostDaggerfish)]
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public bool EnableFrostDaggerfishBoost
    {
        get => frostDaggerfishBoost ?? true;
        set => frostDaggerfishBoost = value;
    }

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.CrystalSerpent)]
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public bool EnableCrystalSerpentBoost
    {
        get => crystalSerpentBoost ?? true;
        set => crystalSerpentBoost = value;
    }

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.Toxikarp)]
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public bool EnableToxikarpBoost
    {
        get => toxikarpBoost ?? true;
        set => toxikarpBoost = value;
    }

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.Bladetongue)]
    [DefaultValue(true)]
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Include)]
    public bool EnableBladetongueBoost
    {
        get => bladetongueBoost ?? true;
        set => bladetongueBoost = value;
    }

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.Honeyfin)]
    [DefaultValue(true)]
    public bool EnableHoneyBoost { get; set; } = true;

    [RequiresField(nameof(RerollCatchesEnabled))]
    [ConfigIcon(ItemID.ObsidianSwordfish)]
    [DefaultValue(true)]
    public bool EnableLavaBoost { get; set; } = true;

    private int? legacyCratesWeight;
    private bool? legacyEnabled;
    private bool? legacyWaterBoosts;
    private bool? rerollCatchesEnabled;
    private bool? alchemyTableBoost;
    private bool? frostDaggerfishBoost;
    private bool? crystalSerpentBoost;
    private bool? toxikarpBoost;
    private bool? bladetongueBoost;

    [JsonProperty("CratesWeight")]
    private int? LegacyCratesWeight { set => legacyCratesWeight = value; }

    [JsonProperty("Enabled")]
    private bool? LegacyEnabled { set => legacyEnabled = value; }

    [JsonProperty("EnableTargetedWaterBoosts")]
    private bool? LegacyWaterBoosts { set => legacyWaterBoosts = value; }

    // Reset tracked fields so tModLoader can distinguish omitted options from saved values.
    [OnDeserializing]
    private void BeginMigration(StreamingContext _)
    {
        Crates = null;
        rerollCatchesEnabled = null;
        alchemyTableBoost = null;
        frostDaggerfishBoost = null;
        crystalSerpentBoost = null;
        toxikarpBoost = null;
        bladetongueBoost = null;
    }

    [OnDeserialized]
    private void FinishMigration(StreamingContext _)
    {
        Crates ??= new() { Weight = legacyCratesWeight ?? FishingCratePoolConfig.DefaultWeight };

        if (!rerollCatchesEnabled.HasValue && legacyEnabled == false)
        {
            EnableHoneyBoost = false;
            EnableLavaBoost = false;
        }
        rerollCatchesEnabled ??= legacyEnabled;
        alchemyTableBoost ??= legacyWaterBoosts;
        frostDaggerfishBoost ??= legacyWaterBoosts;
        crystalSerpentBoost ??= legacyWaterBoosts;
        toxikarpBoost ??= legacyWaterBoosts;
        bladetongueBoost ??= legacyWaterBoosts;

        legacyCratesWeight = null;
        legacyEnabled = null;
        legacyWaterBoosts = null;
    }

    internal static int ClampWeight(int weight) => Math.Clamp(weight, 0, MaxWeight);

    internal double GetPoolShare(int weight)
    {
        int total = ClampWeight(Crates?.Weight ?? 0) + ClampWeight(Utility?.Weight ?? 0) +
            ClampWeight(PotionFish?.Weight ?? 0) + ClampWeight(FishingSupplies?.Weight ?? 0);
        return total == 0 ? 0 : (double)ClampWeight(weight) / total;
    }

    internal void RestoreMissingValues()
    {
        RerolledCatches ??= CreateRerolledCatches();
        Crates ??= new();
        Utility ??= CreateUtility();
        Utility.Items ??= CreateUtility().Items;
        PotionFish ??= CreatePotionFish();
        PotionFish.Items ??= CreatePotionFish().Items;
        FishingSupplies ??= CreateFishingSupplies();
        FishingSupplies.Items ??= CreateFishingSupplies().Items;
    }

    private static List<ItemDefinition> CreateRerolledCatches() =>
    [
        new(ItemID.OldShoe), new(ItemID.TinCan), new(ItemID.FishingSeaweed), new(ItemID.Bass),
        new(ItemID.Trout), new(ItemID.AtlanticCod), new(ItemID.Shrimp), new(ItemID.SpecularFish),
        new(ItemID.Stinkfish), new(ItemID.PrincessFish), new(ItemID.DoubleCod), new(ItemID.Hemopiranha),
        new(ItemID.NeonTetra),
    ];

    private static FishingRewardPoolConfig CreateUtility() => CreatePool(22,
        (ItemID.BombFish, 22), (ItemID.FrogLeg, 18), (ItemID.BalloonPufferfish, 14),
        (ItemID.ReaverShark, 12), (ItemID.SawtoothShark, 12), (ItemID.Swordfish, 12), (ItemID.Rockfish, 10));

    private static FishingRewardPoolConfig CreatePotionFish() => CreatePool(20,
        (ItemID.ArmoredCavefish, 24), (ItemID.VariegatedLardfish, 18), (ItemID.Ebonkoi, 6), (ItemID.Prismite, 20));

    private static FishingRewardPoolConfig CreateFishingSupplies() => CreatePool(13,
        (ItemID.JourneymanBait, 28), (ItemID.MasterBait, 18), (ItemID.CratePotion, 20),
        (ItemID.SonarPotion, 18), (ItemID.FishingPotion, 16));

    private static FishingRewardPoolConfig CreatePool(int weight, params (int Item, int Weight)[] entries)
    {
        var items = new List<FishingPoolEntry>(entries.Length);
        foreach (var entry in entries)
            items.Add(new() { Item = new ItemDefinition(entry.Item), Weight = entry.Weight });
        return new() { Weight = weight, Items = items };
    }
}
