using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.Config;

namespace PvPAdventure.Core.Config;

// Defaults imported from PvPAdventure_ServerConfig.json. Only values represented by this mod's ServerConfig are included.
internal static class ServerConfigDefaults
{
    internal const float BoundSpawnChance = 0.5f;
    internal const int PlayerKillPoints = 1;
    internal const int TeamStartingPoints = 12;
    internal const int StartingCoins = 10;

    internal const bool IsRandomTeleportEnabled = false;
    internal const int TravelPortalCreationTimePreHardmodeSeconds = 10;
    internal const int TeleportCooldownSeconds = 0;

    internal const int LifeFruitChanceDenominator = 1;
    internal const int LifeFruitExpertChanceDenominator = 1;
    internal const int LifeFruitMinimumDistanceBetween = 1;
    internal const int PlanteraBulbChanceDenominator = 12;
    internal const int ChlorophyteSpreadChanceModifier = 100;
    internal const int ChlorophyteGrowChanceModifier = 70;
    internal const int ChlorophyteGrowLimitModifier = 60000;

    internal static List<ServerConfig.ShopItem> CreateShopItems() =>
    [
        ShopItem(ItemID.Wood, Item.buyPrice(silver: 2)),
        ShopItem(ItemID.MiningPotion, Item.buyPrice(silver: 750)),
        ShopItem(ItemID.Torch, Item.buyPrice(silver: 1)),
        ShopItem(ItemID.WoodenBoomerang, Item.buyPrice(silver: 350)),
        ShopItem(ItemID.Umbrella, Item.buyPrice(silver: 159)),
        ShopItem(ItemID.StoneBlock, Item.buyPrice(copper: 125)),
        ShopItem(ItemID.Blowpipe, Item.buyPrice(silver: 200)),
        ShopItem(ItemID.Seed, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.BlandWhip, Item.buyPrice(silver: 500)),
        ShopItem(ItemID.BabyBirdStaff, Item.buyPrice(silver: 170)),
        ShopItem(ItemID.Shackle, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.ClimbingClaws, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.Flipper, Item.buyPrice(silver: 60)),
        ShopItem(ItemID.BandofStarpower, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.NaturesGift, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.IronBar, Item.buyPrice(silver: 75)),
        ShopItem(ItemID.IronskinPotion, Item.buyPrice(silver: 500)),
        ShopItem(ItemID.ThrowingKnife, Item.buyPrice(silver: 35)),
        ShopItem(ItemID.Shuriken, Item.buyPrice(silver: 16)),
        ShopItem(ItemID.LesserHealingPotion, Item.buyPrice(silver: 300)),
        ShopItem(ItemID.ShinePotion, Item.buyPrice(silver: 125)),
        ShopItem(ItemID.NightOwlPotion, Item.buyPrice(silver: 160)),
        ShopItem(ItemID.SwiftnessPotion, Item.buyPrice(silver: 120)),
        ShopItem(ItemID.HeartreachPotion, Item.buyPrice(silver: 185)),
        ShopItem(ItemID.CalmingPotion, Item.buyPrice(silver: 52)),
        ShopItem(ItemID.RegenerationPotion, Item.buyPrice(silver: 500)),
        ShopItem(ItemID.SpelunkerPotion, Item.buyPrice(copper: 80000)),
        ShopItem(ItemID.BuilderPotion, Item.buyPrice(silver: 250)),
        ShopItem(ItemID.Cobweb, Item.buyPrice(silver: 3)),
        ShopItem(ItemID.Grenade, Item.buyPrice(silver: 250)),
        ShopItem(ItemID.Aglet, Item.buyPrice(silver: 70)),
        ShopItem(ItemID.Trident, Item.buyPrice(silver: 350)),
        ShopItem(ItemID.Toolbox, Item.buyPrice(silver: 300)),
        ShopItem(ItemID.PortableStool, Item.buyPrice(silver: 80)),
        ShopItem(ItemID.Bottle, Item.buyPrice(silver: 10)),
        ShopItem(ItemID.Mushroom, Item.buyPrice(silver: 17)),
        ShopItem(ItemID.WandofSparking, Item.buyPrice(silver: 875)),
        ShopItem(ItemID.HunterPotion, Item.buyPrice(silver: 175)),
        ShopItem(ItemID.Rope, Item.buyPrice(copper: 50)),
        ShopItem(ItemID.WaterWalkingBoots, Item.buyPrice(silver: 125)),
        ShopItem(ItemID.MoonLordLegs, Item.buyPrice(silver: 999)),
        ShopItem(ItemID.Bomb, Item.buyPrice(silver: 70)),
        ShopItem(ItemID.Worm, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.EnchantedNightcrawler, Item.buyPrice(silver: 50)),
        ShopItem(ItemID.GoldWorm, Item.buyPrice(silver: 1000)),
        ShopItem(ItemID.Glowstick, Item.buyPrice(silver: 10)),
        ShopItem(ItemID.GillsPotion, Item.buyPrice(silver: 100)),
        ShopItem(ItemID.TrapsightPotion, Item.buyPrice(silver: 100)),
        ShopItem(ItemID.FlipperPotion, Item.buyPrice(silver: 30)),
        ShopItem(ItemID.Dynamite, Item.buyPrice(silver: 300)),
        ShopItem(ItemID.WhoopieCushion, Item.buyPrice(silver: 200)),
        ShopItem(ItemID.Radar, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.Rambutan, Item.buyPrice(silver: 278)),
        ShopItem(ItemID.Spear, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.CopperBar, Item.buyPrice(copper: 1300)),
        ShopItem(ItemID.TinBar, Item.buyPrice(copper: 1600)),
        ShopItem(ItemID.BlinkrootSeeds, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.DaybloomSeeds, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.DeathweedSeeds, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.MoonglowSeeds, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.ShiverthornSeeds, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.WaterleafSeeds, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.FireblossomSeeds, Item.buyPrice(silver: 15)),
        ShopItem(ItemID.Blinkroot, Item.buyPrice(silver: 200)),
        ShopItem(ItemID.Daybloom, Item.buyPrice(silver: 100)),
        ShopItem(ItemID.Waterleaf, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.Shiverthorn, Item.buyPrice(silver: 20)),
        ShopItem(ItemID.Fireblossom, Item.buyPrice(silver: 500)),
        ShopItem(ItemID.Deathweed, Item.buyPrice(silver: 500)),
        ShopItem(ItemID.Moonglow, Item.buyPrice(silver: 300)),
        ShopItem(ItemID.Cactus, Item.buyPrice(silver: 20)),
        ShopItem("PvPAdventure/PowerBomb", Item.buyPrice(silver: 1000)),
        ShopItem(ItemID.FloatingTube, Item.buyPrice(silver: 50))
    ];

    internal static Dictionary<NPCDefinition, ServerConfig.PointsConfig.NpcPoints> CreateNpcPoints() => new()
    {
        [Npc("Terraria/CultistBoss")] = new() { First = 10, Additional = 10, Repeatable = true },
        [Npc("Terraria/Plantera")] = new() { First = 3, Additional = 3, Repeatable = true },
    };

    internal static Dictionary<int, ServerConfig.InvasionSizeValue> CreateInvasionSizes() => new()
    {
        [InvasionID.GoblinArmy] = new() { Value = 400 },
        [InvasionID.PirateInvasion] = new() { Value = 150 },
        [InvasionID.MartianMadness] = new() { Value = 500 },
    };

    private static ServerConfig.ShopItem ShopItem(int type, int price) => new()
    {
        Item = new ItemDefinition(type),
        Price = price
    };

    private static ServerConfig.ShopItem ShopItem(string fullName, int price) => new()
    {
        Item = ParseItem(fullName),
        Price = price
    };

    private static ItemDefinition ParseItem(string fullName)
    {
        int separator = fullName.IndexOf('/');
        return new ItemDefinition(fullName[..separator], fullName[(separator + 1)..]);
    }

    private static NPCDefinition Npc(string fullName)
    {
        int separator = fullName.IndexOf('/');
        return new NPCDefinition(fullName[..separator], fullName[(separator + 1)..]);
    }
}
