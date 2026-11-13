using ErkySSC.Common.Security;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Game.GameReporters;
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
}

internal readonly record struct AnglerQuestView(
    int Revision, int Fish, int Completed, int HardmodeCompleted, AnglerQuestNotice Notice);

internal sealed class AnglerQuestSystem : ModSystem
{
    private enum PacketKind : byte { Request, Snapshot }

    private sealed class Quest
    {
        public int Revision;
        public int Fish;
        public int Completed;
        public int HardmodeCompleted;
    }

    private const int RewardInventorySlots = 50;
    private const float AnglerChatRange = 192f;
    private readonly Dictionary<string, Quest> quests = new(StringComparer.OrdinalIgnoreCase);
    private int nextRevision;
    private ulong nextRefreshTick;
    private static AnglerQuestSystem Instance => ModContent.GetInstance<AnglerQuestSystem>();

    internal static bool IsPlaying => ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Playing;
    internal static AnglerQuestView LocalView { get; private set; }

    public override void Load() => On_Main.AnglerQuestSwap += ResetDailyQuests;

    public override void Unload() => On_Main.AnglerQuestSwap -= ResetDailyQuests;

    public override void ClearWorld()
    {
        quests.Clear();
        LocalView = default;
        nextRevision = 0;
        nextRefreshTick = 0;
    }

    public override void PostUpdateEverything()
    {
        if (!IsPlaying)
        {
            LocalView = default;
            nextRefreshTick = 0;
            return;
        }

        if (Main.netMode == NetmodeID.Server || Main.gameMenu || !Main.LocalPlayer.active ||
            LocalView.Revision != 0 || Main.GameUpdateCount < nextRefreshTick)
            return;

        // Obtain the personal quest even if a player starts fishing before speaking to the Angler.
        nextRefreshTick = Main.GameUpdateCount + 180;
        Request(AnglerQuestAction.Refresh);
    }

    internal static void BeginMatch()
    {
        EndMatch();
        if (Main.netMode != NetmodeID.MultiplayerClient)
            SetDailyQuest(AnglerFishing.NextQuest());
    }

    internal static void EndMatch()
    {
        Instance.quests.Clear();
        Instance.nextRefreshTick = 0;
        LocalView = default;
    }

    internal static bool TryGetActiveQuest(Player player, out int fishType)
    {
        fishType = 0;
        if (!IsPlaying || !player.active)
            return false;

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (player.whoAmI != Main.myPlayer || LocalView.Revision == 0)
                return false;
            fishType = LocalView.Fish;
            return true;
        }

        fishType = Instance.GetQuest(player).Fish;
        return true;
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

        if (action == AnglerQuestAction.Refresh)
        {
            SendView(player, GetQuest(player));
            return;
        }
        if (action != AnglerQuestAction.Interact || !CanTalkToAngler(player, argument))
            return;

        Quest quest = GetQuest(player);
        int slot = player.FindItem(quest.Fish);
        bool firstTurnInBeforeSync = revision == 0 && quest.Completed == 0;
        if ((revision != quest.Revision && !firstTurnInBeforeSync) || slot < 0)
        {
            SendView(player, quest, dialogueNpc: argument);
            return;
        }

        var changedSlots = new HashSet<int> { slot };
        if (--player.inventory[slot].stack == 0)
            player.inventory[slot].TurnToAir();
        quest.Completed++;
        if (Main.hardMode)
            quest.HardmodeCompleted++;
        player.anglerQuestsFinished++;

        GiveRewards(player, Main.npc[argument], quest, changedSlots);
        DealQuest(quest);
        SendView(player, quest, AnglerQuestNotice.Delivered, argument, changedSlots);
    }

    private Quest GetQuest(Player player)
    {
        string key = PlayerKey(player);
        if (!quests.TryGetValue(key, out Quest quest))
        {
            quest = new Quest { Fish = AnglerFishing.DailyQuest, Revision = ++nextRevision };
            quests.Add(key, quest);
        }
        return quest;
    }

    private void ResetDailyQuests(On_Main.orig_AnglerQuestSwap orig)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || !IsPlaying)
        {
            orig();
            return;
        }

        int dailyFish = AnglerFishing.NextQuest(AnglerFishing.DailyQuest);
        SetDailyQuest(dailyFish);
        foreach (Quest quest in quests.Values)
        {
            quest.Fish = dailyFish;
            quest.Revision = ++nextRevision;
        }
        foreach (Player player in Main.ActivePlayers)
            SendView(player, GetQuest(player));
    }

    private static void SetDailyQuest(int fish)
    {
        Main.anglerQuest = Array.IndexOf(Main.anglerQuestItemNetIDs, fish);
        Main.anglerWhoFinishedToday.Clear();
        Main.anglerQuestFinished = false;
        NetMessage.SendAnglerQuest(-1);
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

    private void DealQuest(Quest quest)
    {
        quest.Fish = AnglerFishing.NextQuest(quest.Fish);
        quest.Revision = ++nextRevision;
    }

    private static void GiveRewards(Player player, NPC angler, Quest quest, HashSet<int> changedSlots)
    {
        var source = new EntitySource_Gift(angler);
        foreach (AnglerReward reward in AnglerRewards.GetDeliveryRewards(quest.Completed, quest.HardmodeCompleted, Main.hardMode))
        {
            var item = new Item(reward.ItemType, reward.Stack);
            for (int slot = 0; slot < RewardInventorySlots && item.maxStack > 1 && item.stack > 0; slot++)
            {
                Item existing = player.inventory[slot];
                if (existing.type == item.type && existing.stack < existing.maxStack &&
                    ItemLoader.TryStackItems(existing, item, out int transferred) && transferred > 0)
                    changedSlots.Add(slot);
            }

            if (item.stack <= 0)
                continue;

            int emptySlot = Array.FindIndex(player.inventory, 0, RewardInventorySlots, static existing => existing.IsAir);
            if (emptySlot >= 0)
            {
                player.inventory[emptySlot] = item;
                changedSlots.Add(emptySlot);
                continue;
            }

            int drop = Item.NewItem(source, player.Hitbox, item.type, item.stack, noBroadcast: true, noGrabDelay: true);
            if (Main.netMode == NetmodeID.Server && drop < Main.maxItems)
            {
                Main.item[drop].playerIndexTheItemIsReservedFor = player.whoAmI;
                NetMessage.SendData(MessageID.SyncItem, number: drop, number2: 1f);
                NetMessage.SendData(MessageID.ItemOwner, number: drop);
            }
        }
    }

    private static void SendView(Player player, Quest quest, AnglerQuestNotice notice = AnglerQuestNotice.None,
        int dialogueNpc = -1, HashSet<int> changedSlots = null)
    {
        var view = new AnglerQuestView(quest.Revision, quest.Fish, quest.Completed, quest.HardmodeCompleted, notice);
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
        packet.Send(player.whoAmI);
    }

    private static void ReadView(BinaryReader reader)
    {
        var view = new AnglerQuestView(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
            (AnglerQuestNotice)reader.ReadByte());
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
        ApplyView(view, dialogueNpc);
    }

    private static void ApplyView(AnglerQuestView view, int dialogueNpc)
    {
        if (view.Notice == AnglerQuestNotice.Delivered)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                Main.LocalPlayer.anglerQuestsFinished++;
            AchievementsHelper.HandleAnglerService();
            SoundEngine.PlaySound(SoundID.Coins);
        }

        if (IsPlaying)
        {
            LocalView = view;
            AnglerQuestNpc.ShowQuest(dialogueNpc, view);
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
