using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using PvPAdventure.Common.Fishing;
using PvPFramework.Core.Configs.ConfigElements;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.Config;
using FrameworkAssets = PvPFramework.Core.Utilities.Ass;

namespace PvPAdventure.Core.Config;

public class AnglerReward
{
    public ItemDefinition Item = new();

    [Range(1, 9999)]
    [DefaultValue(1)]
    public int Stack = 1;

    internal static AnglerReward FromItem(int type, int stack = 1) => new() { Item = new(type), Stack = stack };
}

public sealed class AnglerDelivery : AnglerReward
{
    [CustomModConfigItem(typeof(AnglerQuestFishElement))]
    public ItemDefinition QuestFish = new();

    internal int GetQuestFish(int fallback)
    {
        int type = QuestFish?.Type ?? ItemID.None;
        return type > ItemID.None && type < ItemID.Count && Array.IndexOf(Main.anglerQuestItemNetIDs, type) >= 0
            ? type : fallback;
    }

    internal static AnglerDelivery FromOrder(AnglerOrder order) => new()
    {
        QuestFish = new(order.Fish),
        Item = new(order.Reward),
        Stack = order.Stack,
    };

    internal static AnglerDelivery Restore(AnglerDelivery delivery, AnglerOrder order)
    {
        delivery ??= FromOrder(order);
        if (delivery.QuestFish == null || delivery.QuestFish.Type == ItemID.None)
            delivery.QuestFish = new(order.Fish);
        return delivery;
    }
}

public sealed class AnglerPreHardmodeRewards
{
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery1 = Default(0);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery2 = Default(1);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery3 = Default(2);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery4 = Default(3);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery5 = Default(4);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery6 = Default(5);

    internal AnglerDelivery Get(int index) => index switch
    {
        0 => Delivery1, 1 => Delivery2, 2 => Delivery3,
        3 => Delivery4, 4 => Delivery5, 5 => Delivery6,
        _ => null,
    };

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => RestoreMissingValues();

    internal void RestoreMissingValues()
    {
        Delivery1 = AnglerDelivery.Restore(Delivery1, AnglerOrders.GetDefaultPreHardmode(0));
        Delivery2 = AnglerDelivery.Restore(Delivery2, AnglerOrders.GetDefaultPreHardmode(1));
        Delivery3 = AnglerDelivery.Restore(Delivery3, AnglerOrders.GetDefaultPreHardmode(2));
        Delivery4 = AnglerDelivery.Restore(Delivery4, AnglerOrders.GetDefaultPreHardmode(3));
        Delivery5 = AnglerDelivery.Restore(Delivery5, AnglerOrders.GetDefaultPreHardmode(4));
        Delivery6 = AnglerDelivery.Restore(Delivery6, AnglerOrders.GetDefaultPreHardmode(5));
    }

    private static AnglerDelivery Default(int index) => AnglerDelivery.FromOrder(AnglerOrders.GetDefaultPreHardmode(index));
}

public sealed class AnglerHardmodeRewards
{
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery1 = Default(0);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery2 = Default(1);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery3 = Default(2);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery4 = Default(3);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery5 = Default(4);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerDelivery Delivery6 = Default(5);

    internal AnglerDelivery Get(int index) => index switch
    {
        0 => Delivery1, 1 => Delivery2, 2 => Delivery3,
        3 => Delivery4, 4 => Delivery5, 5 => Delivery6,
        _ => null,
    };

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => RestoreMissingValues();

    internal void RestoreMissingValues()
    {
        Delivery1 = AnglerDelivery.Restore(Delivery1, AnglerOrders.GetDefaultHardmode(0));
        Delivery2 = AnglerDelivery.Restore(Delivery2, AnglerOrders.GetDefaultHardmode(1));
        Delivery3 = AnglerDelivery.Restore(Delivery3, AnglerOrders.GetDefaultHardmode(2));
        Delivery4 = AnglerDelivery.Restore(Delivery4, AnglerOrders.GetDefaultHardmode(3));
        Delivery5 = AnglerDelivery.Restore(Delivery5, AnglerOrders.GetDefaultHardmode(4));
        Delivery6 = AnglerDelivery.Restore(Delivery6, AnglerOrders.GetDefaultHardmode(5));
    }

    private static AnglerDelivery Default(int index) => AnglerDelivery.FromOrder(AnglerOrders.GetDefaultHardmode(index));
}

public sealed class AnglerGearMilestones
{
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerReward Delivery1 = Default(0);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerReward Delivery2 = Default(1);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerReward Delivery3 = Default(2);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerReward Delivery4 = Default(3);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerReward Delivery5 = Default(4);
    [Expand(false), CustomModConfigItem(typeof(AnglerRewardElement))]
    public AnglerReward Delivery6 = Default(5);

    internal AnglerReward Get(int index) => index switch
    {
        0 => Delivery1, 1 => Delivery2, 2 => Delivery3,
        3 => Delivery4, 4 => Delivery5, 5 => Delivery6,
        _ => null,
    };

    internal void RestoreMissingValues()
    {
        Delivery1 ??= Default(0);
        Delivery2 ??= Default(1);
        Delivery3 ??= Default(2);
        Delivery4 ??= Default(3);
        Delivery5 ??= Default(4);
        Delivery6 ??= Default(5);
    }

    private static AnglerReward Default(int index) => AnglerReward.FromItem(AnglerOrders.GetDefaultMilestone(index));
}

public sealed class AnglerRewardsConfig
{
    [ConfigIcon(nameof(FrameworkAssets.IconCheckGreen), nameof(FrameworkAssets.IconXGray), grayWhenOff: true)]
    [DefaultValue(true)]
    public bool Enabled = true;

    [RequiresField(nameof(Enabled))]
    [ConfigIcon("Images/UI/Bestiary/Icon_Tags_Shadow", FrameColumns = 16, FrameRows = 5)]
    [Expand(false, false)]
    public AnglerPreHardmodeRewards PreHardmode = new();

    [RequiresField(nameof(Enabled))]
    [ConfigIcon("Images/NPC_Head_Boss_22")]
    [Expand(false, false)]
    public AnglerHardmodeRewards Hardmode = new();

    [RequiresField(nameof(Enabled))]
    [ConfigIcon(nameof(FrameworkAssets.IconCheckGreen), nameof(FrameworkAssets.IconXGray), grayWhenOff: true)]
    [DefaultValue(true)]
    public bool GearMilestonesEnabled = true;

    [RequiresField(nameof(GearMilestonesActive))]
    [ConfigIcon(ItemID.AnglerHat)]
    [Expand(false, false)]
    public AnglerGearMilestones GearMilestones = new();

    [JsonIgnore]
    public bool GearMilestonesActive => Enabled && GearMilestonesEnabled;

    internal AnglerDelivery GetPreHardmode(int index) => PreHardmode?.Get(index);
    internal AnglerDelivery GetHardmode(int index) => Hardmode?.Get(index);
    internal AnglerReward GetMilestone(int index) => GearMilestones?.Get(index);

    internal void RestoreMissingValues()
    {
        PreHardmode ??= new();
        PreHardmode.RestoreMissingValues();
        Hardmode ??= new();
        Hardmode.RestoreMissingValues();
        GearMilestones ??= new();
        GearMilestones.RestoreMissingValues();
    }
}
