using System;
using Terraria.ID;

namespace PvPAdventure.Common.Fishing;

internal struct AnglerAccessoryRewards
{
    private const int FirstEligibleDelivery = 4;
    private const int BonusChancePercent = 35;
    private const int DeliveriesPerGuaranteedReward = 3;

    private static readonly int[] Components =
    [
        ItemID.HighTestFishingLine,
        ItemID.TackleBox,
        ItemID.AnglerEarring,
    ];

    private int awardedMask;
    private int consecutiveMisses;

    internal int Roll(int completedDeliveries, Func<int, int> next)
    {
        int completeMask = (1 << Components.Length) - 1;
        if (completedDeliveries < FirstEligibleDelivery || awardedMask == completeMask)
            return ItemID.None;

        if (consecutiveMisses < DeliveriesPerGuaranteedReward - 1 && next(100) >= BonusChancePercent)
        {
            consecutiveMisses++;
            return ItemID.None;
        }

        Span<int> missingIndices = stackalloc int[Components.Length];
        int missingCount = 0;
        for (int index = 0; index < Components.Length; index++)
        {
            if ((awardedMask & (1 << index)) == 0)
                missingIndices[missingCount++] = index;
        }

        int awardedIndex = missingIndices[missingCount == 1 ? 0 : next(missingCount)];
        // Count earned rewards independently of inventory, which can be traded or stored.
        awardedMask |= 1 << awardedIndex;
        consecutiveMisses = 0;
        return Components[awardedIndex];
    }
}
