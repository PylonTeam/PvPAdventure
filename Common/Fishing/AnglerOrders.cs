using System;
using Terraria;
using Terraria.ID;

namespace PvPAdventure.Common.Fishing;

[Flags]
internal enum AnglerProgression : byte
{
    PreHardmode = 0,
    Hardmode = 1,
    MechanicalBoss = 2,
    Plantera = 4,
    Golem = 8,
}

internal enum AnglerQuestStatus : byte
{
    None,
    Active,
    WaitForHardmode,
    WaitForMechanicalBoss,
    WaitForPlantera,
    WaitForGolem,
    Complete,
}

internal readonly record struct AnglerOrder(int Fish, int Reward, int Stack = 1,
    AnglerProgression Requirement = AnglerProgression.PreHardmode);

internal readonly record struct AnglerOffer(AnglerQuestStatus Status, AnglerOrder Order = default);

internal static class AnglerOrders
{
    private static readonly AnglerOrder[] PreHardmodeOrders =
    [
        new(ItemID.Bunnyfish, ItemID.FuzzyCarrot),
        new(ItemID.Spiderfish, ItemID.FishHook),
        new(ItemID.TundraTrout, ItemID.FlurryBoots),
        new(ItemID.Catfish, ItemID.FrogLeg),
        new(ItemID.DemonicHellfish, ItemID.Lavafly, 5),
        new(ItemID.Clownfish, ItemID.GoldenFishingRod),
    ];

    private static readonly AnglerOrder[] HardmodeOrders =
    [
        new(ItemID.Pixiefish, ItemID.GreaterHealingPotion, 3),
        new(ItemID.CapnTunabeard, ItemID.FinWings),
        new(ItemID.Hungerfish, ItemID.HotlineFishingHook),
        new(ItemID.Fishron, ItemID.ScalyTruffle, Requirement: AnglerProgression.MechanicalBoss),
        new(ItemID.Mudfish, ItemID.ThornHook, Requirement: AnglerProgression.Plantera),
        new(ItemID.AmanitaFungifin, ItemID.TruffleWorm, Requirement: AnglerProgression.Golem),
    ];

    private static readonly AnglerOrder[] HardmodeRepeatOrders =
    [
        new(ItemID.Bunnyfish, ItemID.MasterBait, 3),
        new(ItemID.TundraTrout, ItemID.SonarPotion),
        new(ItemID.Mudfish, ItemID.FishingPotion),
        new(ItemID.Pixiefish, ItemID.MasterBait, 3),
        new(ItemID.Clownfish, ItemID.SonarPotion),
        new(ItemID.AmanitaFungifin, ItemID.FishingPotion),
    ];

    internal static int GetArmorReward(int completedDeliveries) => completedDeliveries switch
    {
        1 => ItemID.AnglerHat,
        3 => ItemID.AnglerVest,
        5 => ItemID.AnglerPants,
        _ => ItemID.None,
    };

    internal static AnglerProgression CurrentProgression
    {
        get
        {
            if (!Main.hardMode)
                return AnglerProgression.PreHardmode;

            var progress = AnglerProgression.Hardmode;
            if (NPC.downedMechBossAny || NPC.downedPlantBoss)
                progress |= AnglerProgression.MechanicalBoss;
            if (NPC.downedPlantBoss)
                progress |= AnglerProgression.Plantera;
            if (NPC.downedGolemBoss)
                progress |= AnglerProgression.Golem;
            return progress;
        }
    }

    internal static AnglerOffer GetOffer(AnglerProgression progress, int preHardmodeCompleted, int hardmodeCompleted)
    {
        bool hardmode = progress.HasFlag(AnglerProgression.Hardmode);
        AnglerOrder[] chapter = hardmode ? HardmodeOrders : PreHardmodeOrders;
        int completed = hardmode ? hardmodeCompleted : preHardmodeCompleted;
        if (completed >= chapter.Length)
        {
            if (!hardmode)
                return new(AnglerQuestStatus.WaitForHardmode);

            int repeatIndex = (completed - chapter.Length) % HardmodeRepeatOrders.Length;
            return new(AnglerQuestStatus.Active, HardmodeRepeatOrders[repeatIndex]);
        }

        AnglerOrder order = chapter[completed];
        if ((progress & order.Requirement) == order.Requirement)
            return new(AnglerQuestStatus.Active, order);

        return new(order.Requirement switch
        {
            AnglerProgression.MechanicalBoss => AnglerQuestStatus.WaitForMechanicalBoss,
            AnglerProgression.Plantera => AnglerQuestStatus.WaitForPlantera,
            AnglerProgression.Golem => AnglerQuestStatus.WaitForGolem,
            _ => AnglerQuestStatus.None,
        });
    }
}
