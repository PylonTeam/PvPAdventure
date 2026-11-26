using ErkySSC.Common.Security;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Game.GameReporters;
using PvPAdventure.Core.Config;
using PvPAdventure.Core.Net;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Achievements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.Fishing;

internal enum AnglerQuestAction : byte
{
    Refresh,
    Interact,
}

internal enum AnglerQuestNotice : byte
{
    None,
    Delivered,
    PreHardmodeChapterFinished,
    PreHardmodeChapterFinishedCustom,
}

internal readonly record struct AnglerQuestView(
    int Revision, int Fish, int Completed, int HardmodeCompleted, AnglerQuestStatus Status, AnglerQuestNotice Notice,
    int ChapterReward = ItemID.None);

internal sealed class AnglerQuestSystem : ModSystem
{
    private enum PacketKind : byte { Request, Snapshot }

    private sealed class Quest
    {
        public int Revision;
        public int PreHardmodeCompleted;
        public int HardmodeCompleted;
        public bool InHardmode;
        public AnglerOffer Offer;
        public AnglerOrder CarryOver;

        public int Completed => PreHardmodeCompleted + HardmodeCompleted;
    }

    private sealed class RewardSchedule
    {
        private readonly AnglerOrder[] early = new AnglerOrder[AnglerOrders.ChapterLength];
        private readonly AnglerOrder[] late = new AnglerOrder[AnglerOrders.ChapterLength];
        private readonly (int Type, int Stack)[] milestones = new (int, int)[AnglerOrders.ChapterLength];

        public RewardSchedule(AnglerRewardsConfig config)
        {
            for (int index = 0; index < AnglerOrders.ChapterLength; index++)
            {
                AnglerOrder defaultEarly = AnglerOrders.GetDefaultPreHardmode(index);
                AnglerOrder defaultLate = AnglerOrders.GetDefaultHardmode(index);
                early[index] = ResolveOrder(config.GetPreHardmode(index), defaultEarly);
                late[index] = ResolveOrder(config.GetHardmode(index), defaultLate);
                if (config.GearMilestonesEnabled)
                    milestones[index] = Resolve(config.GetMilestone(index), AnglerOrders.GetDefaultMilestone(index));
            }
        }

        public AnglerOrder GetChapterOrder(bool hardmode, int index) =>
            hardmode ? late[index] : early[index];

        public (int Type, int Stack) GetMilestone(int deliveries) =>
            deliveries >= 1 && deliveries <= milestones.Length ? milestones[deliveries - 1] : default;

        private static AnglerOrder ResolveOrder(AnglerDelivery delivery, AnglerOrder defaultOrder)
        {
            int fish = delivery?.GetQuestFish(defaultOrder.Fish) ?? defaultOrder.Fish;
            (int reward, int stack) = Resolve(delivery, defaultOrder.Reward, defaultOrder.Stack);
            return defaultOrder with { Fish = fish, Reward = reward, Stack = stack };
        }

        private static (int Type, int Stack) Resolve(AnglerReward reward, int defaultType, int defaultStack = 1)
        {
            if (reward?.Item is not { IsUnloaded: false } item)
                return (defaultType, defaultStack);
            return (item.Type, Math.Clamp(reward.Stack, 1, 9999));
        }
    }

    private const int RewardInventorySlots = 50;
    private const float AnglerChatRange = 192f;
    private static readonly AnglerQuestView DisabledView = new(1, 0, 0, 0, AnglerQuestStatus.Disabled, AnglerQuestNotice.None);
    private readonly Dictionary<string, Quest> quests = new(StringComparer.OrdinalIgnoreCase);
    private int nextRevision;
    private ulong nextRefreshTick;
    private AnglerProgression lastProgression;
    private RewardSchedule matchRewards;
    private bool personalOrdersEnabled;
    private bool requireCurrentQuestRevision;
    private static AnglerQuestSystem Instance => ModContent.GetInstance<AnglerQuestSystem>();
    private static AnglerRewardsConfig Config => ModContent.GetInstance<ServerConfig>().AnglerRewards;
    private RewardSchedule Rewards => matchRewards ??= new(Config);

    internal static bool IsPlaying => ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Playing;
    internal static bool UsesPersonalOrders
    {
        get
        {
            if (!IsPlaying)
                return false;
            if (Main.netMode != NetmodeID.MultiplayerClient)
                return Instance.personalOrdersEnabled;
            return LocalView.Revision == 0 ? Config.Enabled : LocalView.Status != AnglerQuestStatus.Disabled;
        }
    }
    internal static AnglerQuestView LocalView { get; private set; }

    public override void ClearWorld()
    {
        quests.Clear();
        LocalView = default;
        nextRevision = 0;
        nextRefreshTick = 0;
        lastProgression = default;
        matchRewards = null;
        personalOrdersEnabled = false;
        requireCurrentQuestRevision = false;
    }

    public override void PostUpdateEverything()
    {
        if (!IsPlaying)
        {
            LocalView = default;
            nextRefreshTick = 0;
            return;
        }

        if (Main.netMode != NetmodeID.MultiplayerClient)
            UpdateMatchViews();

        if (Main.netMode == NetmodeID.Server || Main.gameMenu || !Main.LocalPlayer.active ||
            LocalView.Revision != 0 || Main.GameUpdateCount < nextRefreshTick)
            return;

        // Obtain the personal quest even if a player starts fishing before speaking to the Angler.
        nextRefreshTick = Main.GameUpdateCount + 180;
        Request(AnglerQuestAction.Refresh);
    }

    private void UpdateMatchViews()
    {
        bool enabled = Config.Enabled;
        bool toggled = enabled != personalOrdersEnabled;
        AnglerProgression progression = AnglerOrders.CurrentProgression;
        if (!toggled && (!enabled || lastProgression == progression))
            return;

        personalOrdersEnabled = enabled;
        lastProgression = progression;
        if (toggled)
        {
            // A turn-in sent before the toggle must wait for a fresh quest view.
            requireCurrentQuestRevision = true;
            if (enabled)
                foreach (Quest quest in quests.Values)
                    quest.Revision = ++nextRevision;
        }

        foreach (Player player in Main.ActivePlayers)
        {
            if (enabled)
                SendView(player, GetQuest(player));
            else
                SendView(player, DisabledView);
        }
    }

    internal static void BeginMatch()
    {
        EndMatch();
        Instance.matchRewards = new RewardSchedule(Config);
        Instance.personalOrdersEnabled = Config.Enabled;
        Instance.lastProgression = AnglerOrders.CurrentProgression;
    }

    internal static void EndMatch()
    {
        Instance.quests.Clear();
        Instance.nextRefreshTick = 0;
        Instance.matchRewards = null;
        Instance.personalOrdersEnabled = false;
        Instance.requireCurrentQuestRevision = false;
        LocalView = default;
    }

    internal static bool TryGetActiveQuest(Player player, out int fishType)
    {
        fishType = 0;
        if (!UsesPersonalOrders || !player.active)
            return false;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (player.whoAmI != Main.myPlayer || LocalView.Revision == 0)
                return false;
            fishType = LocalView.Fish;
            return fishType > 0;
        }

        fishType = Instance.GetQuest(player).Offer.Order.Fish;
        return fishType > 0;
    }

    internal static void Request(AnglerQuestAction action, int revision = 0, int argument = 0)
    {
        if (!IsPlaying)
            return;
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Instance.HandleRequest(Main.LocalPlayer, action, revision, argument);
            return;
        }
        if (Main.netMode != NetmodeID.MultiplayerClient)
            return;

        ModPacket packet = CreatePacket(PacketKind.Request);
        packet.Write((byte)action);
        packet.Write(revision);
        packet.Write(argument);
        packet.Send();
    }

    internal static void HandlePacket(BinaryReader reader, int sender)
    {
        var kind = (PacketKind)reader.ReadByte();
        if (kind == PacketKind.Request && Main.netMode == NetmodeID.Server && sender >= 0 && sender < Main.maxPlayers)
        {
            var action = (AnglerQuestAction)reader.ReadByte();
            int revision = reader.ReadInt32();
            int argument = reader.ReadInt32();
            Instance.HandleRequest(Main.player[sender], action, revision, argument);
        }
        else if (kind == PacketKind.Snapshot && Main.netMode == NetmodeID.MultiplayerClient)
        {
            ReadView(reader);
        }
    }

    private void HandleRequest(Player player, AnglerQuestAction action, int revision, int argument)
    {
        if (!IsPlaying || !player.active)
            return;

        if (!personalOrdersEnabled)
        {
            SendView(player, DisabledView);
            return;
        }
        if (action == AnglerQuestAction.Refresh)
        {
            SendView(player, GetQuest(player));
            return;
        }
        if (action != AnglerQuestAction.Interact || !CanTalkToAngler(player, argument))
            return;

        Quest quest = GetQuest(player);
        AnglerOrder order = quest.Offer.Order;
        int slot = order.Fish > 0 ? player.FindItem(order.Fish) : -1;
        bool carryingEarlyOrder = slot < 0 && quest.InHardmode && quest.CarryOver.Fish > 0;
        if (carryingEarlyOrder)
        {
            order = quest.CarryOver;
            slot = player.FindItem(order.Fish);
        }

        bool firstTurnInBeforeSync = !requireCurrentQuestRevision && revision == 0 && quest.Completed == 0;
        if ((revision != quest.Revision && !firstTurnInBeforeSync) || slot < 0)
        {
            SendView(player, quest, dialogueNpc: argument);
            return;
        }

        bool repeat = quest.InHardmode && !carryingEarlyOrder && AnglerOrders.IsHardmodeRepeat(quest.HardmodeCompleted);
        var changedSlots = new HashSet<int> { slot };
        if (--player.inventory[slot].stack == 0)
            player.inventory[slot].TurnToAir();
        if (quest.InHardmode && !carryingEarlyOrder)
            quest.HardmodeCompleted++;
        else
            quest.PreHardmodeCompleted++;
        if (carryingEarlyOrder)
            quest.CarryOver = default;
        player.anglerQuestsFinished++;

        NPC angler = Main.npc[argument];
        GiveReward(player, angler, order.Reward, order.Stack, changedSlots);
        if (!repeat)
        {
            (int type, int stack) = Rewards.GetMilestone(quest.Completed);
            GiveReward(player, angler, type, stack, changedSlots);
        }
        RefreshOffer(player, quest, delivered: true);
        AnglerQuestNotice notice = AnglerQuestNotice.Delivered;
        if (quest.Offer.Status == AnglerQuestStatus.WaitForHardmode)
        {
            notice = order.Reward == ItemID.GoldenFishingRod
                ? AnglerQuestNotice.PreHardmodeChapterFinished
                : AnglerQuestNotice.PreHardmodeChapterFinishedCustom;
        }
        SendView(player, quest, notice, argument, changedSlots, order.Reward);
    }

    private Quest GetQuest(Player player)
    {
        string key = PlayerKey(player);
        if (!quests.TryGetValue(key, out Quest quest))
        {
            quest = new Quest();
            quests.Add(key, quest);
        }
        RefreshOffer(player, quest);
        return quest;
    }

    private void RefreshOffer(Player player, Quest quest, bool delivered = false)
    {
        AnglerProgression progression = AnglerOrders.CurrentProgression;
        bool hardmode = progression.HasFlag(AnglerProgression.Hardmode);
        if (hardmode && !quest.InHardmode && quest.Offer.Status == AnglerQuestStatus.Active &&
            player.HasItem(quest.Offer.Order.Fish))
        {
            quest.CarryOver = quest.Offer.Order;
        }
        quest.InHardmode = hardmode;

        AnglerOffer next = AnglerOrders.GetOffer(progression, quest.PreHardmodeCompleted, quest.HardmodeCompleted);
        if (next.Status == AnglerQuestStatus.Active && !(hardmode && AnglerOrders.IsHardmodeRepeat(quest.HardmodeCompleted)))
        {
            int index = hardmode ? quest.HardmodeCompleted : quest.PreHardmodeCompleted;
            next = next with { Order = Rewards.GetChapterOrder(hardmode, index) };
        }
        if (quest.Revision == 0 || quest.Offer != next || delivered)
        {
            quest.Offer = next;
            quest.Revision = ++nextRevision;
        }
    }

    private static string PlayerKey(Player player)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
            return "";
        if (JoinAdmissionSystem.TryGetVerifiedSteamId(player.whoAmI, out ulong steamId) ||
            PylonService.TryGetSteamId(player, out steamId))
            return $"steam:{steamId}";
        // Local and community servers may allow unauthenticated connections.
        return $"name:{player.name}";
    }

    private static bool CanTalkToAngler(Player player, int index)
    {
        return !player.dead && index >= 0 && index < Main.maxNPCs &&
            Main.npc[index] is { active: true, type: NPCID.Angler } angler &&
            Vector2.DistanceSquared(player.Center, angler.Center) <= AnglerChatRange * AnglerChatRange;
    }

    private static void GiveReward(Player player, NPC angler, int type, int stack, HashSet<int> changedSlots)
    {
        if (type <= ItemID.None || stack <= 0)
            return;

        var item = new Item(type);
        item.stack = Math.Min(stack, Math.Max(1, item.maxStack));
        for (int slot = 0; slot < RewardInventorySlots && item.maxStack > 1 && item.stack > 0; slot++)
        {
            Item existing = player.inventory[slot];
            if (existing.type == item.type && existing.stack < existing.maxStack &&
                ItemLoader.TryStackItems(existing, item, out int transferred) && transferred > 0)
                changedSlots.Add(slot);
        }

        if (item.stack <= 0)
            return;

        int emptySlot = Array.FindIndex(player.inventory, 0, RewardInventorySlots, static existing => existing.IsAir);
        if (emptySlot >= 0)
        {
            player.inventory[emptySlot] = item;
            changedSlots.Add(emptySlot);
            return;
        }

        int drop = Item.NewItem(new EntitySource_Gift(angler), player.Hitbox, item.type, item.stack, noBroadcast: true, noGrabDelay: true);
        if (Main.netMode == NetmodeID.Server && drop < Main.maxItems)
        {
            Main.item[drop].playerIndexTheItemIsReservedFor = player.whoAmI;
            NetMessage.SendData(MessageID.SyncItem, number: drop, number2: 1f);
            NetMessage.SendData(MessageID.ItemOwner, number: drop);
        }
    }

    private static void SendView(Player player, Quest quest, AnglerQuestNotice notice = AnglerQuestNotice.None,
        int dialogueNpc = -1, HashSet<int> changedSlots = null, int chapterReward = ItemID.None)
    {
        var view = new AnglerQuestView(quest.Revision, quest.Offer.Order.Fish,
            quest.Completed, quest.HardmodeCompleted, quest.Offer.Status, notice,
            notice == AnglerQuestNotice.PreHardmodeChapterFinishedCustom ? chapterReward : ItemID.None);
        SendView(player, view, dialogueNpc, changedSlots);
    }

    private static void SendView(Player player, AnglerQuestView view, int dialogueNpc = -1,
        HashSet<int> changedSlots = null)
    {
        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            if (changedSlots?.Count > 0)
                Recipe.FindRecipes(canDelayCheck: true);
            ApplyView(view, dialogueNpc);
            return;
        }

        ModPacket packet = CreatePacket(PacketKind.Snapshot);
        packet.Write(view.Revision);
        packet.Write(view.Fish);
        packet.Write(view.Completed);
        packet.Write(view.HardmodeCompleted);
        packet.Write((byte)view.Status);
        packet.Write((byte)view.Notice);
        packet.Write((short)dialogueNpc);
        packet.Write((byte)(changedSlots?.Count ?? 0));
        if (changedSlots != null)
        {
            foreach (int slot in changedSlots)
            {
                packet.Write((byte)slot);
                ItemIO.Send(player.inventory[slot], packet, writeStack: true);
                NetMessage.SendData(MessageID.SyncEquipment, -1, player.whoAmI, number: player.whoAmI, number2: slot);
            }
        }
        if (view.Notice == AnglerQuestNotice.PreHardmodeChapterFinishedCustom)
            packet.Write(view.ChapterReward);
        packet.Send(player.whoAmI);
    }

    private static void ReadView(BinaryReader reader)
    {
        var view = new AnglerQuestView(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
            (AnglerQuestStatus)reader.ReadByte(), (AnglerQuestNotice)reader.ReadByte());
        int dialogueNpc = reader.ReadInt16();
        int changedSlots = reader.ReadByte();
        for (int i = 0; i < changedSlots; i++)
        {
            int slot = reader.ReadByte();
            Item item = ItemIO.Receive(reader, readStack: true);
            if (slot >= Main.LocalPlayer.inventory.Length)
                continue;
            Main.LocalPlayer.inventory[slot] = item;
            Main.clientPlayer.inventory[slot] = item.Clone();
        }
        if (changedSlots > 0)
            Recipe.FindRecipes(canDelayCheck: true);
        if (view.Notice == AnglerQuestNotice.PreHardmodeChapterFinishedCustom)
            view = view with { ChapterReward = reader.ReadInt32() };
        ApplyView(view, dialogueNpc);
    }

    private static void ApplyView(AnglerQuestView view, int dialogueNpc)
    {
        if (view.Notice != AnglerQuestNotice.None)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                Main.LocalPlayer.anglerQuestsFinished++;
            AchievementsHelper.HandleAnglerService();
            SoundEngine.PlaySound(SoundID.Coins);
        }

        if (IsPlaying)
        {
            bool modeChanged = LocalView.Revision != 0 &&
                (LocalView.Status == AnglerQuestStatus.Disabled) != (view.Status == AnglerQuestStatus.Disabled);
            LocalView = view;
            AnglerQuestNpc.ShowQuest(modeChanged ? Main.LocalPlayer.talkNPC : dialogueNpc, view);
        }
    }

    private static ModPacket CreatePacket(PacketKind kind)
    {
        ModPacket packet = Instance.Mod.GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.AnglerQuest);
        packet.Write((byte)kind);
        return packet;
    }
}
