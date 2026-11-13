using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Fishing;

/// <summary>
/// Converts junk, filler fish, and fishable NPCs into PvPAdventure rewards.
/// Useful catches keep their original item, including biome fish and crates.
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
/// Data-driven fishing catch replacement rules.
/// - Tune constants and weighted pools here.
/// - Keep hook code tiny; keep reward decisions here.
/// </summary>
public static class FishingCatchRules
{
    // --- Change global feature gates here ---
    private const bool RequirePvPAdventurePlaying = false;
    private const bool ModifyLavaFishing = false;
    private const bool ModifyHoneyFishing = false;

    private static readonly WeightedCatch[] PreHardmodeCrates =
    [
        new(ItemID.WoodenCrate, 55),
        new(ItemID.IronCrate, 32),
        new(ItemID.GoldenCrate, 13),
    ];

    private static readonly WeightedCatch[] HardmodeCrates =
    [
        new(ItemID.WoodenCrateHard, 55),
        new(ItemID.IronCrateHard, 32),
        new(ItemID.GoldenCrateHard, 13),
    ];

    private static readonly WeightedCatch[] PotionFish =
    [
        new(ItemID.ArmoredCavefish, 24),
        new(ItemID.Prismite, 20),
        new(ItemID.VariegatedLardfish, 18),
        new(ItemID.SpecularFish, 16),
        new(ItemID.DoubleCod, 10),
        new(ItemID.Ebonkoi, 6),
        new(ItemID.Hemopiranha, 6),
    ];

    private static readonly WeightedCatch[] UtilityFishingLoot =
    [
        new(ItemID.BombFish, 22),
        new(ItemID.FrogLeg, 18),
        new(ItemID.BalloonPufferfish, 14),
        new(ItemID.ReaverShark, 12),
        new(ItemID.SawtoothShark, 12),
        new(ItemID.Swordfish, 12),
        new(ItemID.Rockfish, 10),
    ];

    private static readonly WeightedCatch[] FishingSupplies =
    [
        new(ItemID.JourneymanBait, 28),
        new(ItemID.MasterBait, 18),
        new(ItemID.CratePotion, 20),
        new(ItemID.SonarPotion, 18),
        new(ItemID.FishingPotion, 16),
    ];

    private static readonly WeightedCatch[] PreHardmodeBars =
    [
        new(ItemID.IronBar, 22),
        new(ItemID.LeadBar, 22),
        new(ItemID.SilverBar, 18),
        new(ItemID.TungstenBar, 18),
        new(ItemID.GoldBar, 10),
        new(ItemID.PlatinumBar, 10),
    ];

    private static readonly WeightedCatch[] HardmodeBars =
    [
        new(ItemID.CobaltBar, 18),
        new(ItemID.PalladiumBar, 18),
        new(ItemID.MythrilBar, 16),
        new(ItemID.OrichalcumBar, 16),
        new(ItemID.AdamantiteBar, 16),
        new(ItemID.TitaniumBar, 16),
    ];

    public static void Apply(FishingCatchContext context, ref int itemDrop, ref int npcSpawn, ref AdvancedPopupRequest sonar, ref Vector2 sonarPosition)
    {
        if (!ShouldUseAdventureFishingRules(context))
            return;

        // --- Change fishable NPC replacement here ---
        if (TryRollAdventureNpcCatch(context, out int npcType))
        {
            npcSpawn = npcType;
            itemDrop = -1;
            sonar.Text = Lang.GetNPCNameValue(npcType);
            sonar.Color = Color.LightGreen;
            sonar.Velocity = Vector2.Zero;
            sonar.DurationInFrames = 180;
            sonarPosition = context.Player.Center - new Vector2(0f, 64f);
            return;
        }

        if (context.IsVanillaNpcCatch || !context.IsVanillaItemCatch || IsFillerCatch(context.VanillaItemDrop))
            SetItemCatch(RollGoodCatch(context), ref itemDrop, ref npcSpawn);
    }

    private static bool ShouldUseAdventureFishingRules(FishingCatchContext context)
    {
        if (!context.HasPlayer)
            return false;

        if (RequirePvPAdventurePlaying && ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Playing)
            return false;

        if (context.Attempt.inLava && !ModifyLavaFishing)
            return false;

        if (context.Attempt.inHoney && !ModifyHoneyFishing)
            return false;

        return context.IsWater || ModifyLavaFishing || ModifyHoneyFishing;
    }

    internal static bool IsFillerCatch(int itemType) => itemType is
        ItemID.OldShoe or ItemID.TinCan or ItemID.FishingSeaweed or ItemID.Bass;

    private static int RollGoodCatch(FishingCatchContext context)
    {
        // --- Change overall reward category weights here ---
        WeightedCatch[] categories =
        [
            new(0, 45), // Crates
            new(1, 20), // Potion fish
            new(2, 15), // Fishing utility loot
            new(3, 12), // Fishing supplies
            new(4, 8),  // Bars
        ];

        return WeightedCatchPool.Roll(categories) switch
        {
            0 => RollCrate(context),
            1 => WeightedCatchPool.Roll(PotionFish),
            2 => WeightedCatchPool.Roll(UtilityFishingLoot),
            3 => WeightedCatchPool.Roll(FishingSupplies),
            4 => WeightedCatchPool.Roll(context.IsHardmode ? HardmodeBars : PreHardmodeBars),
            _ => RollCrate(context),
        };
    }

    private static int RollCrate(FishingCatchContext context)
    {
        // --- Change crate pool here ---
        return WeightedCatchPool.Roll(context.IsHardmode ? HardmodeCrates : PreHardmodeCrates);
    }

    private static bool TryRollAdventureNpcCatch(FishingCatchContext context, out int npcType)
    {
        npcType = NPCID.None;

        // --- Change NPC catch availability here ---
        // Example:
        // if (!context.Attempt.rare && !context.Attempt.veryrare)
        //     return false;

        // --- Change NPC catch chance here ---
        // Example:
        // if (Main.rand.NextBool(100))
        // {
        //     npcType = NPCID.Goldfish;
        //     return true;
        // }

        return false;
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
    public static int Roll(WeightedCatch[] entries)
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
