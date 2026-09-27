using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Core.Config;
using PvPAdventure.Core.Net;
using PvPAdventure.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;

namespace PvPAdventure.Common.Bounties;

[Autoload(Side = ModSide.Both)]
public class BountyManager : ModSystem
{
    // Any interaction with claims will increment this, ensuring the client is interacting with the correct state.
    public int TransactionId { get; private set; }
    public bool CollectedAllMechanicalBossSouls => NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
    private readonly Dictionary<Team, IList<Page>> _bounties = new();
    public IReadOnlyDictionary<Team, IList<Page>> Bounties => _bounties;

    public UIBountyShop UiBountyShop { get; private set; }

    public sealed class Page(IList<Item[]> bounties)
    {
        public IList<Item[]> Bounties { get; } = bounties;
    }

    public sealed class Transaction(int id, byte team, byte pageIndex, byte bountyIndex)
    {
        public int Id { get; } = id;
        public byte Team { get; } = team;
        public byte PageIndex { get; } = pageIndex;
        public byte BountyIndex { get; } = bountyIndex;

        public static Transaction Deserialize(BinaryReader reader)
        {
            var transactionId = reader.ReadInt32();
            var team = reader.ReadByte();
            var pageIndex = reader.ReadByte();
            var bountyIndex = reader.ReadByte();

            return new(transactionId, team, pageIndex, bountyIndex);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Team);
            writer.Write(PageIndex);
            writer.Write(BountyIndex);
        }
    }

    public class ClaimEntitySource : IEntitySource
    {
        public string Context => null;
    }
    private class BountyItemIcon : UIElement
    {
        private readonly Item _item;

        public BountyItemIcon(Item item)
        {
            _item = item;

            Width.Set(58f, 0f);
            Height.Set(58f, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();

            Rectangle background = new(
                (int)dimensions.X,
                (int)dimensions.Y,
                (int)dimensions.Width,
                (int)dimensions.Height);

            //Utils.DrawInvBG(
            //    spriteBatch,
            //    background,
            //    new Color(55, 67, 119) * 0.95f);

            float oldScale = Main.inventoryScale;

            try
            {
                Main.inventoryScale = 0.8f;

                Item item = _item;
                Vector2 position =
                    dimensions.Center() -
                    new Vector2(52f, 52f) * Main.inventoryScale * 0.5f;

                ItemSlot.Draw(
                    spriteBatch,
                    ref item,
                    ItemSlot.Context.ChestItem,
                    position);
            }
            finally
            {
                Main.inventoryScale = oldScale;
            }
        }
    }

    private class BountyRow : UIElement
    {
        private readonly Item[] _items;
        private readonly Action _claim;

        public BountyRow(Item[] items, Action claim)
        {
            _items = items;
            _claim = claim;

            Width.Set(0f, 1f);
            Height.Set(54f, 0f);

            SetPadding(5f);

            BountyItemIcon icon = new(items[0])
            {
                Left = { Pixels = 4f },
                VAlign = 0.5f
            };

            Append(icon);

            UIText itemName = new(GetDisplayName(items), 1.05f)
            {
                Left = { Pixels = 78f },
                VAlign = 0.5f,
                TextOriginX = 0f,
                IgnoresMouseInteraction = true
            };

            Append(itemName);

            UIKeybindingSimpleListItem claimButton = new(
    () => "Claim",
    new Color(73, 94, 171) * 0.9f)
            {
                Width = { Pixels = 78f },
                Height = { Pixels = 38f },

                HAlign = 1f,
                VAlign = 0.5f,

                // Creates space between the button and the row/list right edge.
                Left = { Pixels = -8f }
            };

            claimButton.OnLeftClick += (_, _) => _claim();

            Append(claimButton);

            //claimButton.OnMouseOver += (_, _) =>
            //{
            //    claimButton.BackgroundColor = new Color(95, 118, 205);
            //};

            //claimButton.OnMouseOut += (_, _) =>
            //{
            //    claimButton.BackgroundColor = new Color(73, 94, 171) * 0.95f;
            //};

            claimButton.OnLeftClick += (_, _) => _claim();

            Append(claimButton);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();

            Rectangle rectangle = new(
                (int)dimensions.X,
                (int)dimensions.Y,
                (int)dimensions.Width,
                (int)dimensions.Height);

            Color color = IsMouseHovering
                ? new Color(82, 99, 164) * 0.95f
                : new Color(65, 78, 137) * 0.92f;

            Utils.DrawInvBG(spriteBatch, rectangle, color);
        }

        private static string GetDisplayName(Item[] items)
        {
            if (items.Length == 1)
                return ItemName(items[0]);

            return string.Join(" + ", items.Select(ItemName));
        }

        private static string ItemName(Item item)
        {
            string name = item.Name;

            return item.stack > 1
                ? $"{name} (x{item.stack})"
                : name;
        }
    }

    private class BountyShardCounter : UIElement
    {
        private readonly Func<int> _getShards;

        public BountyShardCounter(Func<int> getShards)
        {
            _getShards = getShards;

            Width.Set(120f, 0f);
            Height.Set(36f, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dimensions = GetDimensions();

            Texture2D icon =
               Ass.Shards is { IsLoaded: true } asset
                    ? asset.Value
                    : null;

            string value = _getShards().ToString();

            const float iconSize = 24f;
            const float textScale = 1.05f;
            const float gap = 7f;

            float textWidth =
                FontAssets.MouseText.Value.MeasureString(value).X *
                textScale;

            float groupWidth =
                (icon != null ? iconSize + gap : 0f) +
                textWidth;

            float x =
                dimensions.X +
                dimensions.Width -
                groupWidth;

            float centerY = dimensions.Center().Y;

            if (icon != null)
            {
                float scale =
                    iconSize /
                    Math.Max(icon.Width, icon.Height);

                spriteBatch.Draw(
                    icon,
                    new Vector2(x + iconSize / 2f, centerY),
                    null,
                    Color.White,
                    0f,
                    icon.Size() / 2f,
                    scale,
                    SpriteEffects.None,
                    0f);

                x += iconSize + gap;
            }

            Utils.DrawBorderString(
                spriteBatch,
                value,
                new Vector2(x, centerY),
                Color.White,
                textScale,
                0f,
                0.5f);

            if (IsMouseHovering)
            {
                Main.LocalPlayer.mouseInterface = true;
                Main.instance.MouseText(
                    $"{_getShards()} bounty shards");
            }
        }
    }

    public class UIBountyShop(BountyManager bountyManager) : UIState
    {
        private const float ShopWidth = 470f;
        private const float ShopHeight = 420f;
        private const float HeaderHeight = 52f;
        private const float RowSpacing = 6f;

        public override void OnInitialize()
        {
            Invalidate();
        }

        public void Invalidate()
        {
            RemoveAllChildren();

            UIElement root = new()
            {
                HAlign = 0.5f,
                VAlign = 0.5f,
                Width = { Pixels = ShopWidth },
                Height = { Pixels = ShopHeight }
            };

            UIPanel background = new()
            {
                Width = { Percent = 1f },
                Height = { Percent = 1f },
                BackgroundColor = new Color(27, 35, 72) * 0.98f,
                BorderColor = new Color(8, 12, 31)
            };

            root.Append(background);

            UITextPanel<string> title = new(
                "Bounty Shop",
                textScale: 1.1f,
                large: true)
            {
                HAlign = 0.5f,
                Top = { Pixels = -35f },
                PaddingLeft = 24f,
                PaddingRight = 24f,
                PaddingTop = 10f,
                PaddingBottom = 10f,
                BackgroundColor = new Color(67, 82, 146),
                BorderColor = new Color(12, 17, 43)
            };

            root.Append(title);

            Team team = (Team)Main.LocalPlayer.team;

            UIText teamText = new(
                team == Team.None
                    ? "No Team"
                    : $"{team} Team",
                0.95f)
            {
                Left = { Pixels = 24f },
                Top = { Pixels = 24f }
            };

            root.Append(teamText);

            BountyShardCounter shardCounter = new(() =>
            {
                Team currentTeam = (Team)Main.LocalPlayer.team;

                return bountyManager.Bounties.TryGetValue(
                    currentTeam,
                    out IList<Page> pages)
                    ? pages.Count
                    : 0;
            })
            {
                HAlign = 1f,
                Top = { Pixels = 14f },
                Left = { Pixels = -30f }
            };

            root.Append(shardCounter);

            UIList bountyList = new()
            {
                Top = { Pixels = HeaderHeight },
                Left = { Pixels = 10f },
                Width =
            {
                Pixels = -46f,
                Percent = 1f
            },
                Height =
            {
                Pixels = -HeaderHeight - 10f,
                Percent = 1f
            },
                ListPadding = RowSpacing
            };

            UIScrollbar scrollbar = new()
            {
                Top = { Pixels = HeaderHeight + 2f },
                Left = { Pixels = -20f },
                HAlign = 1f,
                Height =
            {
                Pixels = -HeaderHeight - 12f,
                Percent = 1f
            }
            };

            bountyList.SetScrollbar(scrollbar);

            root.Append(bountyList);
            root.Append(scrollbar);

            PopulateBounties(bountyList);

            Append(root);
        }

        private void PopulateBounties(UIList bountyList)
        {
            Team team = (Team)Main.LocalPlayer.team;

            if (!bountyManager.Bounties.TryGetValue(
                    team,
                    out IList<Page> pages) ||
                pages.Count == 0)
            {
                bountyList.Add(new UIText(
                    "Your team has no bounty shards.",
                    1f)
                {
                    HAlign = 0.5f,
                    Top = { Pixels = 20f }
                });

                return;
            }

            Page page = pages[0];

            for (byte i = 0; i < page.Bounties.Count; i++)
            {
                byte bountyIndex = i;
                Item[] items = page.Bounties[i];

                BountyRow row = new(
                    items,
                    () => ClaimBounty(bountyIndex));

                bountyList.Add(row);
            }
        }

        private void ClaimBounty(byte bountyIndex)
        {
            Team team = (Team)Main.LocalPlayer.team;

            if (team == Team.None)
                return;

            ModPacket packet = bountyManager.Mod.GetPacket();

            packet.Write(
                (byte)AdventurePacketIdentifier.BountyTransaction);

            new Transaction(
                bountyManager.TransactionId,
                (byte)team,
                0,
                bountyIndex)
                .Serialize(packet);

            packet.Send();
        }
    }

    public override void Load()
    {
        if (!Main.dedServ)
            UiBountyShop = new UIBountyShop(this);
    }

    public override void ClearWorld()
    {
        foreach (var team in Enum.GetValues<Team>())
            _bounties[team] = new List<Page>();
    }

    public override void NetSend(BinaryWriter writer)
    {
        writer.Write(Bounties.Count);

        foreach (var (team, pages) in Bounties)
        {
            writer.Write((int)team);
            writer.Write(pages.Count);

            foreach (var page in pages)
            {
                writer.Write(page.Bounties.Count);

                foreach (var bounty in page.Bounties)
                {
                    writer.Write(bounty.Length);
                    foreach (var item in bounty)
                        ItemIO.Send(item, writer, true);
                }
            }
        }

        writer.Write(TransactionId);
    }

    public override void NetReceive(BinaryReader reader)
    {
        var snapshot = new Dictionary<Team, IList<Page>>();

        var numberOfTeams = WorldSyncReader.ReadCount(reader, 8, Enum.GetValues<Team>().Length);
        for (var i = 0; i < numberOfTeams; i++)
        {
            var team = WorldSyncReader.ReadTeam(reader);
            var numberOfPages = WorldSyncReader.ReadCount(reader, sizeof(int));
            var pages = new List<Page>();
            if (!snapshot.TryAdd(team, pages))
                throw new IOException("Duplicate bounty team in Adventure world sync.");

            for (var j = 0; j < numberOfPages; j++)
            {
                var numberOfBounties = WorldSyncReader.ReadCount(reader, sizeof(int));
                var page = new Page(new List<Item[]>());

                for (var k = 0; k < numberOfBounties; k++)
                {
                    // Every ItemIO item occupies at least its Int16 net ID.
                    var numberOfItems = WorldSyncReader.ReadCount(reader, sizeof(short));
                    var items = new Item[numberOfItems];

                    for (var l = 0; l < numberOfItems; l++)
                        items[l] = ItemIO.Receive(reader, true);

                    page.Bounties.Add(items);
                }

                pages.Add(page);
            }
        }

        int transactionId = reader.ReadInt32();
        WorldSyncReader.EnsureComplete(reader);
        foreach (Team team in Enum.GetValues<Team>())
            snapshot.TryAdd(team, new List<Page>());

        _bounties.Clear();
        foreach (var (team, pages) in snapshot)
            _bounties.Add(team, pages);
        TransactionId = transactionId;
        UiBountyShop?.Invalidate();
    }

    /// <summary>
    /// Grants <paramref name="count"/> bounty shards (pages) to a team and reports the team's new
    /// shard total. Returns false if the team is <see cref="Team.None"/> or nothing is claimable
    /// right now, so callers can report why nothing happened.
    /// </summary>
    public bool TryAddBountyShards(Team team, int count, out int totalShards)
    {
        totalShards = 0;

        if (team == Team.None || count <= 0)
            return false;

        List<Item[]> eligibleBounties = GetEligibleBounties();

        if (eligibleBounties.Count == 0)
            return false;

        if (!_bounties.TryGetValue(team, out IList<Page> pages))
        {
            pages = new List<Page>();
            _bounties[team] = pages;
        }

        // Every page owns its own Item instances so bulk-added shards never share mutated state.
        for (int i = 0; i < count; i++)
            pages.Add(new Page(CloneBounties(eligibleBounties)));

        UiBountyShop?.Invalidate();

        totalShards = pages.Count;
        return true;
    }

    private List<Item[]> GetEligibleBounties() =>
        ModContent.GetInstance<ServerConfig>().Bounties.ClaimableItems
            .Where(IsBountyAvailable)
            .Select(bounty => bounty.Items)
            .Select(items => items.Select(item => new Item(item.Item.Type, item.Stack, item.Prefix.Type)).ToArray())
            .ToList();

    private static IList<Item[]> CloneBounties(IList<Item[]> source)
    {
        List<Item[]> clone = new(source.Count);

        foreach (Item[] bounty in source)
        {
            Item[] bountyClone = new Item[bounty.Length];

            for (int i = 0; i < bounty.Length; i++)
                bountyClone[i] = bounty[i].Clone();

            clone.Add(bountyClone);
        }

        return clone;
    }

    public void Award(Player killer, Player victim)
    {
        var team = (Team)killer.team;

        var eligibleBounties = GetEligibleBounties();

        if (eligibleBounties.Count == 0)
            return;

        _bounties[team].Add(new Page(eligibleBounties));

        var firstPersonMessage = NetworkText.FromLiteral("+1 Bounty Shard");
        var thirdPersonMessage =
            NetworkText.FromLiteral(
                $"{team} Team awarded +1 Bounty Shard for defeating [c/{Main.teamColor[victim.team].Hex3()}:{victim.name}]!");

        NetMessage.SendData(MessageID.CombatTextString,
            text: firstPersonMessage,
            number: (int)Main.teamColor[(int)team].PackedValue,
            number2: killer.position.X,
            number3: killer.position.Y - 20.0f
        );

        NetMessage.SendData(MessageID.WorldData);

        foreach (var player in Main.ActivePlayers)
        {
            if ((Team)player.team == team)
                ChatHelper.SendChatMessageToClient(firstPersonMessage, Main.teamColor[(int)team], player.whoAmI);
            else
                ChatHelper.SendChatMessageToClient(thirdPersonMessage, Main.teamColor[(int)team], player.whoAmI);
        }
    }

    // FIXME: We could be MUCH smarter.
    public override bool HijackGetData(ref byte messageType, ref BinaryReader reader, int playerNumber)
    {
        if (!Main.dedServ && messageType is MessageID.PlayerTeam)
            Main.QueueMainThreadAction(() => UiBountyShop.Invalidate());

        return false;
    }

    // FIXME: We could be MUCH smarter.
    public override bool HijackSendData(int whoAmI, int msgType, int remoteClient, int ignoreClient, NetworkText text,
        int number,
        float number2, float number3, float number4, int number5, int number6, int number7)
    {
        if (!Main.dedServ && msgType == MessageID.PlayerTeam)
            Main.QueueMainThreadAction(() => UiBountyShop.Invalidate());

        return false;
    }

    public void IncrementTransactionId() => TransactionId++;

    private bool IsBountyAvailable(ServerConfig.BountiesConfig.Bounty bounty)
    {
        // This set requires pre-hardmode, but the world is hardmode.
        if (bounty.Conditions.WorldProgression == ServerConfig.Condition.WorldProgressionState.PreHardmode &&
            Main.hardMode)
            return false;

        // This set requires hardmode, but the world is pre-hardmode.
        if (bounty.Conditions.WorldProgression == ServerConfig.Condition.WorldProgressionState.Hardmode &&
            !Main.hardMode)
            return false;

        // This set requires Skeletron Prime to be defeated, but it is not.
        if (bounty.Conditions.SkeletronPrimeDefeated && !NPC.downedMechBoss3)
            return false;

        // This set requires The Twins to be defeated, but it is not.
        if (bounty.Conditions.TwinsDefeated && !NPC.downedMechBoss2)
            return false;

        // This set requires The Destroyer to be defeated, but it is not.
        if (bounty.Conditions.DestroyerDefeated && !NPC.downedMechBoss1)
            return false;

        // This set requires Plantera to be defeated, but it is not.
        if (bounty.Conditions.PlanteraDefeated && !NPC.downedPlantBoss)
            return false;

        // This set requires Golem to be defeated, but it is not.
        if (bounty.Conditions.GolemDefeated && !NPC.downedGolemBoss)
            return false;

        // This set requires Golem to be defeated, but it is not.
        if (bounty.Conditions.SkeletronDefeated && !NPC.downedBoss3)
            return false;

        // This set requires all mechanical boss souls to have been collected, but it is not.
        if (bounty.Conditions.CollectedAllMechanicalBossSouls && !CollectedAllMechanicalBossSouls)
            return false;

        return true;
    }
}
