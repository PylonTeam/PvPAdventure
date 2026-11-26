using System;
using System.Collections.Generic;
using System.Linq;
using PvPAdventure.Common.Fishing;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace PvPAdventure.Core.Config;

internal sealed class FishingCratePoolElement : ObjectElement
{
    public override void OnBind()
    {
        base.OnBind();
        if (Item is FishingSystemConfig fishing)
            TextDisplayFunction = () => FishingConfigText.PoolHeader(Label,
                fishing.GetPoolShare((Value as FishingCratePoolConfig)?.Weight ?? 0));
    }
}

internal sealed class FishingCrateTiersElement : ConfigElement<string>
{
    public override void OnBind()
    {
        base.OnBind();
        string label = $"[c/FFFFFF:{Label}]    {Value}";
        TextDisplayFunction = () => label;
    }
}

internal sealed class FishingPoolElement : ObjectElement
{
    public override void OnBind()
    {
        base.OnBind();
        if (Item is FishingSystemConfig fishing)
            TextDisplayFunction = () => FishingConfigText.PoolHeader(Label,
                fishing.GetPoolShare((Value as FishingRewardPoolConfig)?.Weight ?? 0));
    }
}

internal sealed class FishingItemListElement : ListElement
{
    private sealed class EntrySlot(List<FishingPoolEntry> entries, int index)
    {
        // This property omits the outer list's custom element attribute, which would recurse.
        [Expand(false)]
        public FishingPoolEntry Entry
        {
            get => entries[index];
            set => entries[index] = value;
        }
    }

    protected override void SetupList()
    {
        DataList.Clear();
        if (Data is not List<FishingPoolEntry> entries || entries.Count == 0)
            return;

        PropertyFieldWrapper entryMember = ConfigManager.GetFieldsAndProperties(new EntrySlot(entries, 0))
            .First(member => member.Name == nameof(EntrySlot.Entry));
        int top = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            int slot = index;
            var (container, row) = UIModConfig.WrapIt(DataList, ref top, entryMember, new EntrySlot(entries, slot), slot);
            row.Left.Pixels += 24f;
            row.Width.Pixels -= 30f;
            if (row is ConfigElement configRow)
                configRow.TextDisplayFunction = () => Describe(entries[slot], FishingConfigText.PoolItemTotal(entries));

            UIModConfigHoverImage delete = new(DeleteTexture, Language.GetTextValue("tModLoader.ModConfigRemove"));
            delete.Top.Set(4f, 0f);
            delete.OnLeftClick += (_, _) =>
            {
                entries.RemoveAt(slot);
                SetupList();
                Interface.modConfig.RecalculateChildren();
                Interface.modConfig.SetPendingChanges();
            };
            container.Append(delete);
        }
    }

    private static string Describe(FishingPoolEntry entry, int total)
    {
        string item = FishingConfigText.Item(entry?.Item?.Type ?? ItemID.None);
        double share = FishingConfigText.PoolItemShare(entry, total);
        return $"{item} ({share:0.#%})";
    }
}

internal sealed class AnglerRewardElement : ObjectElement
{
    public override void OnBind()
    {
        base.OnBind();

        AnglerOrder order = GetOrder();
        TextDisplayFunction = () =>
        {
            AnglerReward reward = Value as AnglerReward;
            string prize = FishingConfigText.Item(reward?.Item?.Type ?? ItemID.None, reward?.Stack ?? 1);
            if (reward is not AnglerDelivery delivery)
                return $"{Label}    {prize}";

            int fish = delivery.GetQuestFish(order.Fish);
            return $"{Label}    {FishingConfigText.Item(fish)} → {prize}";
        };

        string gate = GetProgressionLabel(order.Requirement);
        if (gate.Length > 0)
        {
            Func<string> original = TooltipFunction;
            TooltipFunction = () =>
            {
                string description = original?.Invoke();
                return string.IsNullOrEmpty(description) ? gate : $"{description}\n{gate}";
            };
        }
    }

    private AnglerOrder GetOrder()
    {
        int delivery = MemberInfo.Name switch
        {
            nameof(AnglerPreHardmodeRewards.Delivery1) => 0,
            nameof(AnglerPreHardmodeRewards.Delivery2) => 1,
            nameof(AnglerPreHardmodeRewards.Delivery3) => 2,
            nameof(AnglerPreHardmodeRewards.Delivery4) => 3,
            nameof(AnglerPreHardmodeRewards.Delivery5) => 4,
            nameof(AnglerPreHardmodeRewards.Delivery6) => 5,
            _ => -1,
        };
        if (delivery < 0)
            return default;

        return Item switch
        {
            AnglerPreHardmodeRewards => AnglerOrders.GetDefaultPreHardmode(delivery),
            AnglerHardmodeRewards => AnglerOrders.GetDefaultHardmode(delivery),
            _ => default,
        };
    }

    private static string GetProgressionLabel(AnglerProgression requirement) => requirement switch
    {
        AnglerProgression.MechanicalBoss => Language.GetTextValue("Mods.PvPAdventure.FishingConfig.RequiresMechanicalBoss"),
        AnglerProgression.Plantera => Language.GetTextValue("Mods.PvPAdventure.FishingConfig.RequiresBoss", Lang.GetNPCNameValue(NPCID.Plantera)),
        AnglerProgression.Golem => Language.GetTextValue("Mods.PvPAdventure.FishingConfig.RequiresBoss", Lang.GetNPCNameValue(NPCID.Golem)),
        _ => "",
    };
}

internal sealed class AnglerQuestFishElement : ItemDefinitionElement
{
    protected override List<DefinitionOptionElement<ItemDefinition>> CreateDefinitionOptionElementList()
    {
        var options = new List<DefinitionOptionElement<ItemDefinition>>(Main.anglerQuestItemNetIDs.Length);
        foreach (int fish in Main.anglerQuestItemNetIDs)
        {
            if (fish <= ItemID.None || fish >= ItemID.Count)
                continue;

            var option = new ItemDefinitionOptionElement(new ItemDefinition(fish), OptionScale);
            option.OnLeftClick += (_, _) =>
            {
                Value = option.Definition;
                UpdateNeeded = true;
                SelectionExpanded = false;
            };
            options.Add(option);
        }
        return options;
    }
}

internal static class FishingConfigText
{
    internal static string PoolHeader(string label, double share) => $"{label} ({share:0.#%})";

    internal static int PoolItemWeight(FishingPoolEntry entry) => entry?.Item?.Type is > ItemID.None
        ? FishingSystemConfig.ClampWeight(entry.Weight)
        : 0;

    internal static int PoolItemTotal(IEnumerable<FishingPoolEntry> entries) => entries?.Sum(PoolItemWeight) ?? 0;

    internal static double PoolItemShare(FishingPoolEntry entry, int total) => total > 0
        ? (double)PoolItemWeight(entry) / total
        : 0;

    internal static string CrateTiers(bool hardmode)
    {
        ReadOnlySpan<WeightedCatch> tiers = FishingCatchRules.GetCrateTiers(hardmode);
        int weight = 0;
        foreach (WeightedCatch tier in tiers)
            weight += tier.Weight;

        var entries = new string[tiers.Length];
        for (int i = 0; i < tiers.Length; i++)
        {
            WeightedCatch tier = tiers[i];
            bool biome = tier.ItemType == ItemID.None;
            int icon = biome ? ItemID.WorldGlobe : tier.ItemType;
            string share = $"{((double)tier.Weight / weight):0.#%}";
            if (biome)
                share = $"{Language.GetTextValue("Mods.PvPAdventure.FishingConfig.BiomeCrate")} {share}";

            entries[i] = $"[i:{icon}] [c/FFFFFF:{share}]";
        }
        return string.Join("    ", entries);
    }

    internal static string Item(int type, int stack = 1)
    {
        if (type <= ItemID.None)
            return Language.GetTextValue("Mods.PvPAdventure.FishingConfig.NoItem");

        if (stack > 1 && ContentSamples.ItemsByType.TryGetValue(type, out Item sample))
            stack = Math.Min(stack, sample.maxStack);
        string item = $"[i:{type}] {Lang.GetItemNameValue(type)}";
        return stack > 1 ? $"{item} ×{stack}" : item;
    }
}
