using System.Collections.Generic;
using Terraria.ID;

namespace PvPAdventure.Common.Fishing;

internal readonly record struct AnglerReward(int ItemType, int Stack = 1);

internal static class AnglerRewards
{
    private static readonly AnglerReward[][] Milestones =
    [
        [],
        [new(ItemID.HighTestFishingLine), new(ItemID.FuzzyCarrot)],
        [new(ItemID.AnglerHat), new(ItemID.TackleBox)],
        [new(ItemID.AnglerVest), new(ItemID.AnglerEarring)],
        [new(ItemID.AnglerPants), new(ItemID.FishermansGuide)],
        [new(ItemID.BottomlessBucket), new(ItemID.WeatherRadio)],
        [new(ItemID.GoldenFishingRod), new(ItemID.Sextant)],
        [new(ItemID.FishHook), new(ItemID.FishingBobber)],
        [new(ItemID.GoldenBugNet), new(ItemID.SuperAbsorbantSponge)],
        [new(ItemID.FishMinecart), new(ItemID.BottomlessHoneyBucket), new(ItemID.HoneyAbsorbantSponge)],
    ];

    private static readonly AnglerReward[][] Collections =
    [
        [
            new(ItemID.SeashellHairpin), new(ItemID.MermaidAdornment), new(ItemID.MermaidTail),
            new(ItemID.LifePreserver), new(ItemID.ShipsWheel), new(ItemID.CompassRose), new(ItemID.WallAnchor),
            new(ItemID.ShipInABottle), new(ItemID.TreasureMap), new(ItemID.SeaweedPlanter),
        ],
        [
            new(ItemID.FishCostumeMask), new(ItemID.FishCostumeShirt), new(ItemID.FishCostumeFinskirt),
            new(ItemID.GoldfishTrophy), new(ItemID.BunnyfishTrophy), new(ItemID.SwordfishTrophy), new(ItemID.SharkteethTrophy),
        ],
        [
            new(ItemID.PillaginMePixels), new(ItemID.NotSoLostInParadise), new(ItemID.Crustography), new(ItemID.Fangs),
            new(ItemID.WhatLurksBelow), new(ItemID.CouchGag), new(ItemID.SilentFish), new(ItemID.TheDuke),
        ],
    ];

    public static List<AnglerReward> GetDeliveryRewards(int completed, int hardmodeCompleted, bool deliveredInHardmode)
    {
        var rewards = new List<AnglerReward>();
        if (completed <= 0)
            return rewards;

        if (completed < Milestones.Length)
            rewards.AddRange(Milestones[completed]);

        int priorHardmodeCompleted = hardmodeCompleted - (deliveredInHardmode ? 1 : 0);
        if (ReachedMilestone(completed, hardmodeCompleted, priorHardmodeCompleted, 8, 2))
            rewards.Add(new(ItemID.FinWings));
        if (ReachedMilestone(completed, hardmodeCompleted, priorHardmodeCompleted, 9, 3))
            rewards.Add(new(ItemID.HotlineFishingHook));

        int collectionIndex = completed - Milestones.Length;
        if (collectionIndex >= 0 && collectionIndex < Collections.Length)
            rewards.AddRange(Collections[collectionIndex]);

        rewards.Add(((completed - 1) % 4) switch
        {
            1 => new(ItemID.FishingPotion),
            2 => new(ItemID.SonarPotion),
            3 => new(ItemID.CratePotion),
            _ => new(deliveredInHardmode ? ItemID.MasterBait : ItemID.JourneymanBait, 3),
        });

        return rewards;
    }

    private static bool ReachedMilestone(int completed, int hardmodeCompleted, int priorHardmodeCompleted,
        int deliveriesRequired, int hardmodeDeliveriesRequired)
    {
        return completed >= deliveriesRequired && hardmodeCompleted >= hardmodeDeliveriesRequired &&
            (completed == deliveriesRequired || priorHardmodeCompleted < hardmodeDeliveriesRequired);
    }
}
