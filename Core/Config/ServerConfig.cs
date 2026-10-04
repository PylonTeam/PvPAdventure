using PvPAdventure.Core.Config.ConfigElements;
using PvPFramework.Core.Configs.ConfigElements;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

public sealed class ServerConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [Header("Points")]
    [HeaderIcon(nameof(Ass.IconPointsSetter))]
    [BackgroundColor(150, 104, 38)]
    [Expand(false, false)]
    public PointsConfig Points = new();

    [BackgroundColor(150, 104, 38)]
    [Expand(false, false)]
    public BountiesConfig Bounties = new();

    [Header("Gameplay")]
    [ConfigIcon(ItemID.GoldChest, placement: ConfigIconPlacement.Inside)]
    [BackgroundColor(205, 110, 60)]
    [Expand(false, false)]
    public ShakingChestConfig ShakingChest = new();

    [ConfigIcon(nameof(Ass.ConfigBed), placement: ConfigIconPlacement.Inside)]
    [BackgroundColor(205, 110, 60)]
    [Expand(false, false)]
    public TravelSystemConfig TravelSystem = new();

    [ConfigIcon(ItemID.FuzzyCarrot, placement: ConfigIconPlacement.Inside)]
    [BackgroundColor(205, 110, 60)]
    [Expand(false, false)]
    public RacePeriodConfig RacePeriod = new();

    [Header("NPCs")]
    [HeaderIcon(267)]
    [ConfigIcon(nameof(Ass.ConfigBoundNPC))]
    [BackgroundColor(58, 108, 72)]
    [DefaultValue(ServerConfigDefaults.BoundSpawnChance)]
    public float BoundSpawnChance = ServerConfigDefaults.BoundSpawnChance;

    [Header("World")]
    [HeaderIcon(ItemID.WorldGlobe)]
    [BackgroundColor(72, 104, 72)]
    [Expand(false, false)]
    [CustomModConfigItem(typeof(InvasionDictionaryElement))]
    public Dictionary<int, InvasionSizeValue> InvasionSizes = ServerConfigDefaults.CreateInvasionSizes();

    [BackgroundColor(72, 104, 72)]
    [DefaultValue(true)]
    public bool IncreaseRainFrequency = true;

    [BackgroundColor(72, 104, 72)]
    [DefaultValue(true)]
    public bool DisableLunarApocalypse = true;

    [BackgroundColor(72, 104, 72)]
    [DefaultValue(true)]
    public bool StartHardmodeGoblinInvasion = true;

    [BackgroundColor(72, 104, 72)]
    [DefaultValue(true)]
    public bool BroadcastWeatherMessages = true;

    [Header("WorldGen")]
    [HeaderIcon(ItemID.WorldGlobe)]
    [BackgroundColor(114, 90, 46)]
    [Expand(false, false)]
    public WorldGenerationConfig WorldGeneration = new();

    public override void OnLoaded()
    {
        Points ??= new();
        Points.Npc ??= ServerConfigDefaults.CreateNpcPoints();
        Bounties ??= new();
        Bounties.ClaimableItems ??= ServerConfigDefaults.CreateClaimableBounties();
        ShakingChest ??= new();
        ShakingChest.ShopItems ??= ServerConfigDefaults.CreateShopItems();
        TravelSystem ??= new();
        RacePeriod ??= new();
        WorldGeneration ??= new();
        InvasionSizes ??= ServerConfigDefaults.CreateInvasionSizes();
    }

    public sealed class ShakingChestConfig
    {
        [Range(0, 9999)]
        [DefaultValue(ServerConfigDefaults.StartingCoins)]
        public int StartingCoins = ServerConfigDefaults.StartingCoins;

        [Expand(false, false)]
        [CustomModConfigItem(typeof(ShopItemListElement))]
        public List<ShopItem> ShopItems = ServerConfigDefaults.CreateShopItems();
    }

    public sealed class ShopItem
    {
        public ItemDefinition Item = new();

        [Range(0, 100000000)]
        public int Price;
    }

    public override bool AcceptClientChanges(ModConfig pendingConfig, int whoAmI, ref NetworkText message)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return true;

        if (!ModLoader.TryGetMod("ErkySSC", out Mod erkySsc))
        {
            message = NetworkText.FromLiteral("Server config changes require ErkySSC admin permissions.");
            return false;
        }

        bool isAdmin = false;

        try
        {
            if (erkySsc.Call("IsAdmin", whoAmI) is bool value)
                isAdmin = value;
        }
        catch (Exception exception)
        {
            Log.Chat($"Failed to check ErkySSC admin permission for config change. whoAmI={whoAmI}, error={exception.Message}");
        }

        if (!isAdmin)
        {
            message = NetworkText.FromLiteral("You must be an ErkySSC admin to modify this config.");
            return false;
        }

        message = NetworkText.FromLiteral("Saved!");
        return true;
    }

    public sealed class PointsConfig
    {
        [Expand(false, false)]
        [CustomModConfigItem(typeof(DefinitionDictionaryElement))]
        public Dictionary<NPCDefinition, NpcPoints> Npc = ServerConfigDefaults.CreateNpcPoints();

        [Expand(false, false)]
        public NpcPoints Boss = new()
        {
            First = 2,
            Additional = 1
        };

        [DefaultValue(ServerConfigDefaults.PlayerKillPoints)]
        public int PlayerKill = ServerConfigDefaults.PlayerKillPoints;

        public sealed class NpcPoints
        {
            public int First;
            public int Additional;
            public bool Repeatable;
        }

        [DefaultValue(ServerConfigDefaults.TeamStartingPoints)]
        public int TeamStartingPoints = ServerConfigDefaults.TeamStartingPoints;

        [DefaultValue(0)]
        public int BedKill;

        [DefaultValue(0)]
        public int PortalKill;
    }

    public sealed class BountiesConfig
    {
        [Expand(false, false)]
        public List<Bounty> ClaimableItems = ServerConfigDefaults.CreateClaimableBounties();

        [DefaultValue(ServerConfigDefaults.AwardBountyEveryKill)]
        public bool AwardBountyEveryKill = ServerConfigDefaults.AwardBountyEveryKill;

        public sealed class Bounty
        {
            public List<ConfigItem> Items = [];
            public Condition Conditions = new();
        }
    }

    public sealed class RacePeriodConfig
    {
        [ConfigIcon(nameof(PvPFramework.Core.Utilities.Ass.IconCheckGreen), nameof(PvPFramework.Core.Utilities.Ass.IconXGray), grayWhenOff: true)]
        [DefaultValue(true)]
        public bool RacePeriodEnabled = true;

        [RequiresField(nameof(RacePeriodEnabled))]
        [Range(1f, 20f)]
        [Increment(0.25f)]
        [DefaultValue(8f)]
        [Slider]
        public float RacePeriodRunSpeed = 8f;

        [RequiresField(nameof(RacePeriodEnabled))]
        [ConfigIcon(nameof(PvPFramework.Core.Utilities.Ass.IconCheckGreen), nameof(PvPFramework.Core.Utilities.Ass.IconXGray), grayWhenOff: true)]
        [DefaultValue(true)]
        public bool RacePeriodTrailEnabled = true;
    }

    public sealed class TravelSystemConfig
    {
        [ConfigIcon(nameof(PvPFramework.Core.Utilities.Ass.IconCheckGreen), nameof(PvPFramework.Core.Utilities.Ass.IconXGray), grayWhenOff: true)]
        [DefaultValue(true)]
        public bool IsTravelSystemEnabled = true;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [ConfigIcon(nameof(Ass.ConfigMapWorldSpawn))]
        [DefaultValue(true)]
        public bool IsWorldSpawnTeleportEnabled = true;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [ConfigIcon(nameof(Ass.ConfigPlayerHead))]
        [DefaultValue(true)]
        public bool IsTeammateSpawnTeleportEnabled = true;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [ConfigIcon(nameof(Ass.IconQuestionMark))]
        [DefaultValue(ServerConfigDefaults.IsRandomTeleportEnabled)]
        public bool IsRandomTeleportEnabled = ServerConfigDefaults.IsRandomTeleportEnabled;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [DefaultValue(true)]
        public bool AllowSpectating = true;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [Range(0, 60)]
        [DefaultValue(ServerConfigDefaults.TravelPortalCreationTimePreHardmodeSeconds)]
        public int TravelPortalCreationTimePreHardmodeSeconds = ServerConfigDefaults.TravelPortalCreationTimePreHardmodeSeconds;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [Range(0, 60)]
        [DefaultValue(10)]
        public int TravelPortalCreationTimeHardmodeSeconds = 10;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [Range(0, 60)]
        [DefaultValue(8)]
        public int TravelRegionRadiusTiles = 8;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [Range(-60, 60)]
        [DefaultValue(30)]
        public int PortalCreationOffset = 30;

        [RequiresField(nameof(IsTravelSystemEnabled))]
        [Range(0, 60)]
        [DefaultValue(ServerConfigDefaults.TeleportCooldownSeconds)]
        public int TeleportCooldownSeconds = ServerConfigDefaults.TeleportCooldownSeconds;
    }

    public sealed class WorldGenerationConfig
    {
        [ConfigIcon(ItemID.LifeFruit)]
        [DefaultValue(ServerConfigDefaults.LifeFruitChanceDenominator)]
        public int LifeFruitChanceDenominator = ServerConfigDefaults.LifeFruitChanceDenominator;

        [ConfigIcon(ItemID.LifeFruit)]
        [DefaultValue(ServerConfigDefaults.LifeFruitExpertChanceDenominator)]
        public int LifeFruitExpertChanceDenominator = ServerConfigDefaults.LifeFruitExpertChanceDenominator;

        [ConfigIcon(ItemID.LifeFruit)]
        [DefaultValue(ServerConfigDefaults.LifeFruitMinimumDistanceBetween)]
        public int LifeFruitMinimumDistanceBetween = ServerConfigDefaults.LifeFruitMinimumDistanceBetween;

        [ConfigIcon(nameof(Ass.ConfigPlanterasBulb))]
        [DefaultValue(ServerConfigDefaults.PlanteraBulbChanceDenominator)]
        public int PlanteraBulbChanceDenominator = ServerConfigDefaults.PlanteraBulbChanceDenominator;

        [ConfigIcon(ItemID.ChlorophyteOre)]
        [DefaultValue(ServerConfigDefaults.ChlorophyteSpreadChanceModifier)]
        public int ChlorophyteSpreadChanceModifier = ServerConfigDefaults.ChlorophyteSpreadChanceModifier;

        [ConfigIcon(ItemID.ChlorophyteOre)]
        [Range(1, 1000)]
        [DefaultValue(ServerConfigDefaults.ChlorophyteGrowChanceModifier)]
        public int ChlorophyteGrowChanceModifier = ServerConfigDefaults.ChlorophyteGrowChanceModifier;

        [ConfigIcon(ItemID.ChlorophyteOre)]
        [Range(1, 999999)]
        [DefaultValue(ServerConfigDefaults.ChlorophyteGrowLimitModifier)]
        public int ChlorophyteGrowLimitModifier = ServerConfigDefaults.ChlorophyteGrowLimitModifier;
    }

    public sealed class InvasionSizeValue
    {
        [Range(0, 1000)]
        public int Value;
    }

    public sealed class ConfigItem
    {
        public ItemDefinition Item = new();
        public PrefixDefinition Prefix = new();
        private int stack = 1;

        public int Stack
        {
            get => stack;
            set => stack = Math.Clamp(value, 1, new Item(Item.Type, 1, Prefix.Type).maxStack);
        }
    }

    public sealed class Condition
    {
        public enum WorldProgressionState
        {
            Any,
            PreHardmode,
            Hardmode
        }

        public WorldProgressionState WorldProgression;
        public bool SkeletronPrimeDefeated;
        public bool TwinsDefeated;
        public bool DestroyerDefeated;
        public bool PlanteraDefeated;
        public bool GolemDefeated;
        public bool SkeletronDefeated;
        public bool CollectedAllMechanicalBossSouls;
    }
}
