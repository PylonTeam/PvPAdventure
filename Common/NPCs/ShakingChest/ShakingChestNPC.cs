using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Compat;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.NPCs;

public sealed class ShakingChestNPC : GlobalNPC
{
    internal const int TargetType = NPCID.BoundTownSlimeOld;
    private const string ShopName = "Shop";
    private const float DisplayScale = 4f;
    private const int SpawnBoxPadding = 8;
    private const int HoldDurationTicks = 60 * 10;
    private const int RoamDurationTicks = 60 * 3;
    private const int ReturnTimeoutTicks = 60 * 5;
    private const float RoamRangeTiles = 3f;
    private const float RoamRange = RoamRangeTiles * 16f;
    private const float ReturnSpeed = 2f;

    private enum AnchorState : byte
    {
        Holding,
        Roaming,
        Returning
    }

    private AnchorState _anchorState = AnchorState.Holding;
    private int _anchorTimer = HoldDurationTicks;

    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) =>
        entity.type == TargetType;

    private static ServerConfig.ShakingChestConfig Config =>
        ModContent.GetInstance<ServerConfig>().ShakingChest;

    public override void SetStaticDefaults() =>
        new NPCShop(TargetType, ShopName).Register();

    public override void SetDefaults(NPC npc)
    {
        if (npc.type != TargetType)
            return;

        npc.townNPC = true;
        npc.friendly = true;
        npc.dontTakeDamage = true;
        npc.immortal = true;
        npc.homeless = true;
        npc.aiStyle = NPCAIStyleID.Slime;
        npc.scale = DisplayScale;
    }

    public override bool? CanChat(NPC npc) => npc.type == TargetType ? true : null;

    public override void GetChat(NPC npc, ref string chat)
    {
        if (npc.type == TargetType)
            chat = "...";
    }

    public override void OnChatButtonClicked(NPC npc, bool firstButton)
    {
        if (npc.type != TargetType || !firstButton)
            return;

        Main.playerInventory = true;
        Main.stackSplit = 9999;
        Main.npcChatText = "";
        Main.SetNPCShopIndex(1);
        ShakingChestUI.Open(CreateShopItems(), npc);
        Main.LocalPlayer.currentShoppingSettings.PriceAdjustment = 1d;
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    private static Item[] CreateShopItems() => Config.ShopItems
        .Select(entry => CreateItem(entry?.Item, 1, 0, entry?.Price ?? 0))
        .Where(item => !item.IsAir)
        .ToArray();

    public static void RefundPlayer(Player player)
    {
        Clear(player.inventory);
        Clear(player.armor);
        Clear(player.dye);
        Clear(player.miscEquips);
        Clear(player.miscDyes);
        player.trashItem = new Item();
        Main.mouseItem = new Item();

        for (int i = 0; i < Player.MaxBuffs; i++)
        {
            player.buffType[i] = 0;
            player.buffTime[i] = 0;
        }

        if (!ErkySSCCompat.TryApplyStartingItems(player))
        {
            ApplyFallbackStartingItems(player);
            AddStartingCoins(player);
        }

        Recipe.FindRecipes();
        ShakingChestLoadouts.Sync(player);
    }

    private static void ApplyFallbackStartingItems(Player player)
    {
        player.inventory[0] = new Item(ItemID.CopperShortsword);
        player.inventory[1] = new Item(ItemID.CopperPickaxe);
        player.inventory[2] = new Item(ItemID.CopperAxe);
        player.inventory[4] = new Item(ItemID.Bed);
        player.inventory[5] = new Item(ModContent.ItemType<PortalCreatorItem>());
    }

    private static void AddStartingCoins(Player player)
    {
        if (Config.StartingCoins <= 0)
            return;

        int slot = System.Array.FindIndex(player.inventory, item => item.IsAir);
        if (slot >= 0)
            player.inventory[slot] = new Item(ItemID.GoldCoin) { stack = Config.StartingCoins };
    }

    private static Item CreateItem(
        Terraria.ModLoader.Config.ItemDefinition definition,
        int stack,
        int prefix,
        int price = 0)
    {
        if (definition == null || definition.IsUnloaded || definition.Type <= ItemID.None)
            return new Item();

        return new Item(definition.Type, stack, prefix)
        {
            shopCustomPrice = price
        };
    }

    private static void Clear(IEnumerable<Item> items)
    {
        foreach (Item item in items)
            item?.TurnToAir();
    }

    public override void PostAI(NPC npc)
    {
        if (npc.type != TargetType)
            return;

        if (Main.netMode != NetmodeID.MultiplayerClient &&
            ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Playing)
        {
            ShakingChestNetHandler.SendDisappearFx(npc);
            npc.active = false;
            npc.life = 0;
            if (Main.dedServ)
                NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
            return;
        }

        UpdateAnchor(npc);
        ConfineToSpawnBox(npc);
    }

    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        binaryWriter.Write((byte)_anchorState);
        binaryWriter.Write((short)_anchorTimer);
    }

    public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
    {
        _anchorState = (AnchorState)binaryReader.ReadByte();
        _anchorTimer = binaryReader.ReadInt16();
    }

    // The chest stands perfectly still on the spawn box's center line. Every HoldDurationTicks it is
    // handed back to the slime AI for a short window, but only within RoamRangeTiles of that line,
    // and is then leashed home before holding again. Only the server drives the state machine;
    // clients mirror it through SendExtraAI so both sides pin the chest to the same spot.
    private void UpdateAnchor(NPC npc)
    {
        if (!TryGetAnchorX(npc, out float anchorX))
            return;

        bool authority = Main.netMode != NetmodeID.MultiplayerClient;

        if (_anchorTimer > 0)
            _anchorTimer--;

        switch (_anchorState)
        {
            case AnchorState.Roaming:
                LimitRoaming(npc, anchorX);
                if (authority && _anchorTimer <= 0)
                    SetAnchorState(npc, AnchorState.Returning, ReturnTimeoutTicks);
                break;

            case AnchorState.Returning:
                if (!MoveTowardsAnchor(npc, anchorX) && _anchorTimer > 0)
                    break;

                StandStill(npc, anchorX);
                if (authority)
                    SetAnchorState(npc, AnchorState.Holding, HoldDurationTicks);
                break;

            default:
                StandStill(npc, anchorX);
                if (authority && _anchorTimer <= 0)
                    SetAnchorState(npc, AnchorState.Roaming, RoamDurationTicks);
                break;
        }
    }

    private void SetAnchorState(NPC npc, AnchorState state, int duration)
    {
        _anchorState = state;
        _anchorTimer = duration;
        npc.netUpdate = true;
    }

    private static bool TryGetAnchorX(NPC npc, out float anchorX)
    {
        anchorX = 0f;

        Rectangle tileArea = AdventureRegionSystem.TileArea;
        if (tileArea.IsEmpty)
            return false;

        anchorX = AdventureRegionSystem.WorldArea.Center.X - npc.width / 2f;
        return true;
    }

    private static void StandStill(NPC npc, float anchorX)
    {
        npc.position.X = anchorX;
        npc.velocity.X = 0f;

        // Cancel the slime AI's hops so it settles on the ground and stays put.
        if (npc.velocity.Y < 0f)
            npc.velocity.Y = 0f;
    }

    private static void LimitRoaming(NPC npc, float anchorX)
    {
        float minX = anchorX - RoamRange;
        float maxX = anchorX + RoamRange;

        if (npc.position.X < minX)
        {
            npc.position.X = minX;
            npc.velocity.X = System.Math.Abs(npc.velocity.X);
            npc.direction = 1;
        }
        else if (npc.position.X > maxX)
        {
            npc.position.X = maxX;
            npc.velocity.X = -System.Math.Abs(npc.velocity.X);
            npc.direction = -1;
        }
    }

    private static bool MoveTowardsAnchor(NPC npc, float anchorX)
    {
        float distance = anchorX - npc.position.X;
        if (System.Math.Abs(distance) <= ReturnSpeed)
        {
            StandStill(npc, anchorX);
            return true;
        }

        npc.direction = System.Math.Sign(distance);
        npc.velocity.X = npc.direction * ReturnSpeed;
        return false;
    }

    internal static void PlayDisappearFx(
        Vector2 position,
        int width,
        int height,
        Vector2 velocity)
    {
        if (Main.dedServ)
            return;

        SoundEngine.PlaySound(SoundID.NPCDeath6, position + new Vector2(width, height) / 2f);

        for (int i = 0; i < 100; i++)
        {
            Dust dust = Dust.NewDustDirect(
                new Vector2(position.X - 20f, position.Y),
                width + 40,
                height,
                DustID.RainbowMk2,
                0f,
                0f,
                60,
                new Color(130, 60, 255, 70));

            dust.scale += Main.rand.Next(-10, 21) * 0.01f;
            dust.noGravity = true;
            dust.velocity += velocity * 0.8f;
            dust.velocity *= Main.rand.NextFloat();
            dust.velocity.Y += 2f * Main.rand.NextFloatDirection();
            dust.noLight = true;

            if (Main.rand.Next(3) == 0)
            {
                Dust whiteDust = Dust.CloneDust(dust);
                whiteDust.color = Color.White;
                whiteDust.scale *= 0.5f;
                whiteDust.alpha = 0;
            }
        }
    }

    private static void ConfineToSpawnBox(NPC npc)
    {
        Rectangle tileArea = AdventureRegionSystem.TileArea;
        if (tileArea.IsEmpty)
            return;

        Rectangle area = AdventureRegionSystem.WorldArea;
        float minX = area.Left + SpawnBoxPadding;
        float maxX = area.Right - SpawnBoxPadding - npc.width;
        float minY = area.Top + SpawnBoxPadding;
        float maxY = area.Bottom - SpawnBoxPadding - npc.height;

        // A chest that spawned above the box can land on the outer face of its top border. Move it
        // wholly inside before applying the normal horizontal leash so gravity can find the floor.
        if (maxY >= minY && (npc.position.Y < minY || npc.position.Y > maxY))
        {
            PlaceInsideSpawnBox(npc, area);
            return;
        }

        if (maxX < minX)
        {
            npc.Center = new Vector2(area.Center.X, npc.Center.Y);
            npc.velocity.X = 0f;
            return;
        }

        if (npc.position.X < minX)
        {
            npc.position.X = minX;
            npc.velocity.X = System.Math.Abs(npc.velocity.X);
            npc.direction = 1;
            npc.netUpdate = true;
        }
        else if (npc.position.X > maxX)
        {
            npc.position.X = maxX;
            npc.velocity.X = -System.Math.Abs(npc.velocity.X);
            npc.direction = -1;
            npc.netUpdate = true;
        }
    }

    internal static void PlaceInsideSpawnBox(NPC npc, Rectangle area)
    {
        float x = area.Center.X - npc.width / 2f;
        float minY = area.Top + SpawnBoxPadding;
        float maxY = area.Bottom - SpawnBoxPadding - npc.height;

        npc.position.X = x;
        npc.position.Y = maxY < minY
            ? area.Center.Y - npc.height / 2f
            : FindOpenSpawnY(npc, x, minY, maxY);
        npc.velocity = Vector2.Zero;

        if (Main.netMode != NetmodeID.MultiplayerClient)
            npc.netUpdate = true;
    }

    private static float FindOpenSpawnY(NPC npc, float x, float minY, float maxY)
    {
        float desiredY = System.Math.Clamp(Main.spawnTileY * 16f - npc.height, minY, maxY);
        if (!Collision.SolidCollision(new Vector2(x, desiredY), npc.width, npc.height))
            return desiredY;

        // Search downward first so the chest stays near the normal ground level, then upward if the
        // terrain at the spawn-box center is occupied.
        int searchDistance = (int)System.Math.Ceiling(maxY - minY);
        for (int offset = 8; offset <= searchDistance; offset += 8)
        {
            float below = desiredY + offset;
            if (below <= maxY &&
                !Collision.SolidCollision(new Vector2(x, below), npc.width, npc.height))
                return below;

            float above = desiredY - offset;
            if (above >= minY &&
                !Collision.SolidCollision(new Vector2(x, above), npc.width, npc.height))
                return above;
        }

        return minY;
    }

    public override void ModifyHoverBoundingBox(NPC npc, ref Rectangle boundingBox)
    {
        if (npc.type == TargetType)
            boundingBox = npc.Hitbox;
    }
}
