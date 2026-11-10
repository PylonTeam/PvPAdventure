using Humanizer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Core.Debug;

#if DEBUG
public class DebugFishingCatchPanel : UIPanel
{
    public const string GroupHeader = "Fishing Catches";
    public const int PanelWidth = 300;
    public const int PanelHeight = 420;

    private readonly UIList list = new() { Top = { Pixels = 92 }, Width = { Pixels = -24, Percent = 1 }, Height = { Pixels = -92, Percent = 1 }, ListPadding = 4 };
    private readonly UIScrollbar scrollbar = new() { Left = { Pixels = -20, Percent = 1 }, Top = { Pixels = 92 }, Width = { Pixels = 20 }, Height = { Pixels = -92, Percent = 1 } };
    private readonly UIText status = new("Not calculated", 0.7f) { Top = { Pixels = 64 }, Width = { Pixels = -28, Percent = 1 } };

    public DebugFishingCatchPanel()
    {
        Width.Set(PanelWidth, 0);
        Height.Set(PanelHeight, 0);
        BackgroundColor = new Color(44, 57, 105, 220);
        BorderColor = Color.Black;
        SetPadding(8);

        UITextPanel<string> calc = new("Calculate", 0.8f) { Top = { Pixels = 28 }, Width = { Pixels = 130 }, Height = { Pixels = 30 } };
        calc.OnLeftClick += (_, _) =>
        {
            Log.Chat("Fishing calculate clicked.");
            Main.mouseLeftRelease = false;
            RecalculateFishingCatches();
        };

        list.SetScrollbar(scrollbar);
        Append(new UIText(GroupHeader, 0.9f));
        Append(calc);
        Append(status);
        Append(list);
        Append(scrollbar);
    }

    public void RecalculateFishingCatches()
    {
        if (DebugFishingCatchStats.CurrentlyTesting)
        {
            Log.Chat("Fishing calculate ignored because a test is already running.");
            return;
        }

        DebugFishingCatchStats.Recalculate();
        Refresh();
        DebugStatsSystem.RefreshFishingPanelLayout();
    }

    public void Refresh()
    {
        list.Clear();
        list.ViewPosition = 0f;
        scrollbar.ViewPosition = 0f;
        status.SetText(DebugFishingCatchStats.LastStatus);

        Log.Chat($"Fishing UI refresh. catches={DebugFishingCatchStats.Catches.Count}, status={DebugFishingCatchStats.LastStatus}");

        foreach (DebugFishingCatch entry in DebugFishingCatchStats.Catches)
            list.Add(new DebugFishingCatchRow(entry));

        list.Recalculate();
        scrollbar.Recalculate();
        Recalculate();
        Parent?.Recalculate();

        Log.Chat($"Fishing UI refreshed. totalHeight={list.GetTotalHeight()}, panel={GetDimensions().ToRectangle()}");
    }
}

public class DebugFishingCatchRow : UIElement
{
    private readonly DebugFishingCatch entry;

    public DebugFishingCatchRow(DebugFishingCatch entry)
    {
        this.entry = entry;
        Width.Set(0f, 1f);
        Height.Set(56f, 0f);
    }

    protected override void DrawSelf(SpriteBatch sb)
    {
        Rectangle r = GetDimensions().ToRectangle();

        if (r.Width <= 0 || r.Height <= 0)
            return;

        Rectangle icon = new(r.X + 4, r.Y + 4, 48, r.Height - 8);
        sb.Draw(TextureAssets.MagicPixel.Value, r, (entry.IsNpc ? Color.LightCoral : entry.IsItem ? Color.LightBlue : Color.Gray) * 0.35f);
        sb.Draw(TextureAssets.MagicPixel.Value, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), Color.Black * 0.65f);

        if (entry.IsNpc)
            DrawNpcIcon(sb, icon, entry.Type);
        else if (entry.IsItem)
            DrawItemIcon(sb, icon, entry.Type);
        else
            Utils.DrawBorderString(sb, "Ø", icon.Center.ToVector2() - new Vector2(6f, 10f), Color.White, 0.85f);

        Vector2 pos = new(r.X + 60f, r.Y + 7f);
        Utils.DrawBorderString(sb, entry.DisplayName(), pos, Color.White, 0.78f);
        Utils.DrawBorderString(sb, $"{entry.Chance:P2}   ({entry.Count})", pos + new Vector2(0f, 22f), Color.LightGray, 0.72f);
    }

    private static void DrawNpcIcon(SpriteBatch sb, Rectangle box, int type)
    {
        Main.instance.LoadNPC(type);
        Texture2D texture = TextureAssets.Npc[type].Value;
        int frameHeight = texture.Height / Math.Max(1, Main.npcFrameCount[type]);
        DrawIcon(sb, box, texture, new Rectangle(0, 0, texture.Width, frameHeight));
    }

    private static void DrawItemIcon(SpriteBatch sb, Rectangle box, int type)
    {
        Main.instance.LoadItem(type);
        Texture2D texture = TextureAssets.Item[type].Value;
        DrawIcon(sb, box, texture, Main.itemAnimations[type]?.GetFrame(texture) ?? texture.Bounds);
    }

    private static void DrawIcon(SpriteBatch sb, Rectangle box, Texture2D texture, Rectangle source)
    {
        float scale = Math.Min(box.Width / (float)source.Width, box.Height / (float)source.Height);
        scale = Math.Min(scale, 1.6f);
        Vector2 size = source.Size() * scale;
        sb.Draw(texture, box.Center.ToVector2() - size * 0.5f, source, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}

public readonly record struct DebugFishingCatch(int Id, int Count, float Chance)
{
    public bool IsNothing => Id == 0; public bool IsItem => Id > 0; public bool IsNpc => Id < 0; public int Type => Math.Abs(Id);
    public string DisplayName()
    {
        if (IsNothing) return "Nothing";
        if (IsItem) { Item item = new(); item.SetDefaults(Type); return item.ModItem == null ? Lang.GetItemNameValue(Type) : $"{Lang.GetItemNameValue(Type)} [{item.ModItem.Mod.Name}]"; }
        NPC npc = new(); npc.SetDefaults(Type); return npc.ModNPC == null ? Lang.GetNPCNameValue(Type) : $"{Lang.GetNPCNameValue(Type)} [{npc.ModNPC.Mod.Name}]";
    }
}

public static class DebugFishingCatchStats
{
    private const int Tries = 1000;
    private static readonly Dictionary<int, int> catches = [];

    public static readonly List<DebugFishingCatch> Catches = [];
    public static int Total, PoolSize;
    public static bool Lava, Honey, CurrentlyTesting;
    public static string LastStatus = "Not calculated";

    public static void Recalculate()
    {
        Catches.Clear();
        Total = PoolSize = 0;
        Lava = Honey = false;

        Player player = Main.LocalPlayer;
        Log.Chat($"Fishing test started. player={player?.name ?? "null"}, heldItem={player?.HeldItem?.Name ?? "null"}, pole={player?.HeldItem?.fishingPole ?? 0}");

        if (!ValidateSpawnPosition(player, out Vector2 spawn))
        {
            Log.Chat($"Fishing test failed. reason={LastStatus}");
            return;
        }

        CalculateCatches(spawn, player.HeldItem.shoot, out PoolSize, out Lava, out Honey);

        foreach ((_, int count) in catches)
            Total += count;

        if (Total <= 0)
        {
            LastStatus = $"No catches found. poolSize={PoolSize}, lava={Lava}, honey={Honey}";
            Log.Chat($"Fishing test finished. {LastStatus}");
            return;
        }

        foreach ((int id, int count) in catches)
            Catches.Add(new(id, count, count / (float)Total));

        Catches.Sort((a, b) => b.Chance.CompareTo(a.Chance));

        PlayerFishingConditions conditions = player.GetFishingConditions();
        string liquid = Lava ? "lava" : Honey ? "honey" : "water";
        LastStatus = $"{Total} tests, {conditions.FinalFishingLevel} power, {PoolSize} {liquid} pool";

        Log.Chat($"Fishing test finished. {LastStatus}, uniqueCatches={Catches.Count}, top={TopCatchSummary()}");
    }

    private static bool ValidateSpawnPosition(Player player, out Vector2 spawnPosition)
    {
        spawnPosition = Vector2.Zero;

        if (player?.active != true)
        {
            LastStatus = "Player is not active";
            return false;
        }

        if (player.HeldItem.fishingPole <= 0)
        {
            LastStatus = $"Player needs to hold a fishing pole. item={player.HeldItem.Name}, type={player.HeldItem.type}";
            return false;
        }

        bool hasBait = false;
        for (int i = 0; i < Main.InventorySlotsTotal; i++)
        {
            Item item = player.inventory[i];
            if (!item.IsAir && item.bait > 0 && item.type != ItemID.TruffleWorm)
            {
                hasBait = true;
                break;
            }
        }

        if (!hasBait)
        {
            LastStatus = "Player needs bait in inventory";
            return false;
        }

        int startX = (int)(player.Left.X / 16);
        int endX = startX + 1;
        int startY = (int)(player.Center.Y / 16);
        int x = startX;
        int y = startY;
        const int maxYRange = 30;
        bool inLiquidAndAirAbove = false;
        bool inAirAndLiquidBelow = false;

        if (Framing.GetTileSafely(startX, startY).LiquidAmount > 0)
        {
            while (WorldGen.InWorld(x, y, 40) && y >= startY - maxYRange)
            {
                for (x = startX; x <= endX; x++)
                {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasUnactuatedTile && tile.LiquidAmount == 0)
                    {
                        inLiquidAndAirAbove = true;
                        break;
                    }

                    if (x == endX)
                        y--;
                }

                if (inLiquidAndAirAbove)
                    break;
            }
        }
        else
        {
            while (WorldGen.InWorld(x, y, 40) && y < startY + maxYRange)
            {
                for (x = startX; x <= endX; x++)
                {
                    Tile tile = Framing.GetTileSafely(x, y);
                    if (!tile.HasUnactuatedTile && tile.LiquidAmount > 0)
                    {
                        inAirAndLiquidBelow = true;
                        y--;
                        break;
                    }

                    if (x == endX)
                        y++;
                }

                if (inAirAndLiquidBelow)
                    break;
            }
        }

        if (!inAirAndLiquidBelow && !inLiquidAndAirAbove)
        {
            LastStatus = $"Player needs to be within {maxYRange} tiles above or below liquid";
            return false;
        }

        spawnPosition = new Vector2(x, y) * 16f;
        LastStatus = "Spawn position found";
        Log.Chat($"Fishing test spawn found. spawn={spawnPosition}, tile=({x},{y})");
        return true;
    }

    private static void CalculateCatches(Vector2 spawnPosition, int bobberType, out int poolSize, out bool lava, out bool honey)
    {
        poolSize = 0;
        lava = false;
        honey = false;

        Player player = Main.LocalPlayer;
        Vector2 originalCenter = player.Center;
        bool originalWet = player.wet;
        bool originalHoneyWet = player.honeyWet;
        bool originalLavaWet = player.lavaWet;
        Projectile bobber = null;

        try
        {
            int index = Projectile.NewProjectile(null, spawnPosition, Vector2.UnitY * 8f, bobberType, 0, 0f, Main.myPlayer);
            if (index >= Main.maxProjectiles)
            {
                LastStatus = $"Failed to spawn bobber. index={index}";
                return;
            }

            bobber = Main.projectile[index];
            catches.Clear();
            CurrentlyTesting = true;

            int failureCount = 0;
            bool measuredPool = false;

            player.Center = spawnPosition - new Vector2(0f, bobber.height * 2f);
            player.wet = false;
            player.honeyWet = false;
            player.lavaWet = false;

            Log.Chat($"Fishing test bobber spawned. index={index}, bobberType={bobberType}, spawn={spawnPosition}");

            for (int i = 0; i < Tries; i++)
            {
                failureCount++;

                if (!bobber.wet)
                {
                    bobber.Update(index);
                    i--;
                }
                else
                {
                    bobber.AI();

                    if (!measuredPool)
                    {
                        poolSize = GetPoolStats(bobber.Center, out lava, out honey);
                        measuredPool = true;
                        Log.Chat($"Fishing test pool measured. poolSize={poolSize}, lava={lava}, honey={honey}, bobberCenter={bobber.Center}");
                    }
                }

                if (i > 0 && i % 250 == 0)
                    Log.Chat($"Fishing test progress. tries={i}/{Tries}, uniqueCatches={catches.Count}");

                if (failureCount >= Tries * 2)
                    break;
            }

            Log.Chat($"Fishing test simulation done. uniqueCatches={catches.Count}, failureCount={failureCount}");
        }
        finally
        {
            player.Center = originalCenter;
            player.wet = originalWet;
            player.honeyWet = originalHoneyWet;
            player.lavaWet = originalLavaWet;
            CurrentlyTesting = false;

            if (bobber != null)
            {
                bobber.ai[1] = 0f;
                bobber.Kill();
            }

            Log.Chat("Fishing test cleanup complete.");
        }
    }

    private static int GetPoolStats(Vector2 startPosition, out bool lava, out bool honey)
    {
        int startX = (int)(startPosition.X / 16f);
        int startY = (int)(startPosition.Y / 16f);

        if (Main.tile[startX, startY].LiquidAmount < 0)
            startY++;

        lava = false;
        honey = false;

        int leftStart = startX;
        int rightEnd = startX;

        while (leftStart > 10 && Framing.GetTileSafely(leftStart, startY).LiquidAmount > 0 && !WorldGen.SolidTile(leftStart, startY))
            leftStart--;

        for (; rightEnd < Main.maxTilesX - 10 && Framing.GetTileSafely(rightEnd, startY).LiquidAmount > 0 && !WorldGen.SolidTile(rightEnd, startY); rightEnd++)
        {
        }

        int poolSize = 0;

        for (int x = leftStart; x <= rightEnd; x++)
        {
            int y = startY;

            while (Framing.GetTileSafely(x, y).LiquidAmount > 0 && !WorldGen.SolidTile(x, y) && y < Main.maxTilesY - 10)
            {
                poolSize++;
                y++;

                Tile tile = Framing.GetTileSafely(x, y);
                if (tile.LiquidType == LiquidID.Lava)
                    lava = true;
                else if (tile.LiquidType == LiquidID.Honey)
                    honey = true;
            }
        }

        return honey ? (int)(poolSize * 1.5f) : poolSize;
    }

    private static string TopCatchSummary()
    {
        if (Catches.Count == 0)
            return "none";

        return string.Join(", ", Catches.Take(5).Select(c => $"{c.DisplayName()}={c.Chance:P1}"));
    }

    public static void RecordCatch(int id)
    {
        catches.TryGetValue(id, out int count);
        catches[id] = count + 1;
    }
}
[Autoload(Side = ModSide.Client)]
public class DebugFishingCatchProjectile : GlobalProjectile
{
    public override void Load() => On_Projectile.AI_061_FishingBobber_DoASplash += StopSplash;
    public override void Unload() => On_Projectile.AI_061_FishingBobber_DoASplash -= StopSplash;
    private void StopSplash(On_Projectile.orig_AI_061_FishingBobber_DoASplash orig, Projectile self) { if (!DebugFishingCatchStats.CurrentlyTesting) orig(self); }

    public override void AI(Projectile projectile)
    {
        if (!DebugFishingCatchStats.CurrentlyTesting)
            return;

        if (!projectile.bobber)
            return;

        if (!projectile.wet)
            return;

        Player player = Main.player[projectile.owner];

        projectile.ai[0] = 0f;
        // FishingCheck leaves the previous result untouched when there is no bite.
        projectile.ai[1] = 0f;
        projectile.localAI[1] = 0f;

        if (Main.myPlayer == projectile.owner)
        {
            projectile.FishingCheck();

            if (projectile.localAI[1] == 240f)
            {
                PlayerFishingConditions conditions = player.GetFishingConditions();

                int enoughLavaStuff = 0;
                if (ItemID.Sets.IsLavaBait[conditions.BaitItemType])
                    enoughLavaStuff++;

                if (ItemID.Sets.CanFishInLava[conditions.PoleItemType])
                    enoughLavaStuff++;

                if (player.accLavaFishing)
                    enoughLavaStuff++;

                if (enoughLavaStuff >= 2)
                    projectile.localAI[1] = 0f;
            }
        }

        bool catchNpc = false;

        if (projectile.ai[0] == 0f && projectile.localAI[1] != 0f)
        {
            projectile.ai[0] = 1f;

            if (projectile.localAI[1] < 0f)
            {
                catchNpc = true;
                projectile.ai[0] = 3f;
            }
            else
            {
                projectile.ai[1] = projectile.localAI[1];
            }
        }

        int type = (int)(catchNpc ? projectile.localAI[1] : projectile.ai[1]);

        if (projectile.ai[1] > 0f && projectile.localAI[1] >= 0f)
            projectile.localAI[1] = -1f;

        if (!catchNpc && type < 0)
            return;

        DebugFishingCatchStats.RecordCatch(type);
    }
}
#endif
