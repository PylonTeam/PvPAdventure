// Engine boundaries only: the item, creation projectile, attempt tracker, packet
// reader/writer and portal system under test are compiled from production sources.
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Content.Portals;
using Terraria.ModLoader;

namespace Microsoft.Xna.Framework
{
    public struct Vector2(float x, float y)
    {
        public float X = x, Y = y;
        public static Vector2 Zero => new(0, 0);
        public float LengthSquared() => X * X + Y * Y;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator *(Vector2 a, float b) => new(a.X * b, a.Y * b);
    }
    public struct Color
    {
        public static Color White => new();
        public static Color operator *(Color a, float b) => a;
    }
    public struct Rectangle
    {
        public Vector2 Size() => new(40, 60);
    }
    public static class MathHelper
    {
        public static float Clamp(float x, float min, float max) => Math.Clamp(x, min, max);
        public static float Lerp(float min, float max, float t) => min + (max - min) * t;
        public static float ToRadians(float x) => x * MathF.PI / 180;
    }
}
namespace Microsoft.Xna.Framework.Graphics
{
    public class Texture2D { public Rectangle Frame(int a = 1, int b = 1, int c = 0, int d = 0) => new(); }
    public enum SpriteEffects { None }
    public class SpriteBatch { public void Draw(Texture2D a, Vector2 b, Rectangle? c, Color d, float e, Vector2 f, float g, SpriteEffects h, float i) { } }
}
namespace Terraria
{
    public static class Main
    {
        public const int maxPlayers = 255, maxProjectiles = 1000, maxNPCs = 200;
        public static int netMode, myPlayer;
        public static bool hardMode, dedServ;
        public static uint GameUpdateCount;
        public static Color[] teamColor = new Color[6];
        public static int[] npcFrameCount = new int[100];
        public static Player[] player;
        public static Projectile[] projectile;
        public static NPC[] npc;
        public static Player LocalPlayer => player[myPlayer];
        public static RandomStub rand = new();
        public static Vector2 screenPosition;
        public static SpriteBatch spriteBatch = new();
        public static Dust[] dust = [new()];
    }
    public sealed class RandomStub { public bool NextBool() => false; }
    public class Dust
    {
        public bool noGravity;
        public static int NewDust(Vector2 a, int b, int c, int d, float e, float f, int g, Color h, float i) => 0;
    }
    public class Item
    {
        public ModItem ModItem;
        public int width, height, useTime, useAnimation, useStyle, rare, value, type;
        public byte prefix;
        public bool noUseGraphic, favorited;
        public object UseSound;
        public Vector2 Center;
        public static int buyPrice(int gold) => gold;
        public void SetDefaults(int value) { type = value; ModItem = new PortalCreatorItem { Item = this }; ModItem.SetDefaults(); }
    }
    public class Player
    {
        public bool active, dead, ghost, CCed, controlUseItem, releaseUseItem, channel,
            controlLeft, controlRight, controlJump, controlUp, controlDown, controlHook, controlMount, solarDashing;
        public int whoAmI, team, direction = 1, selectedItem, itemAnimation, itemAnimationMax, itemTime, itemTimeMax, reuseDelay,
            dashDelay, eocDash, width = 20, height = 40;
        public Vector2 position, velocity, itemLocation;
        public Vector2 Bottom => position + new Vector2(width / 2, height);
        public Item[] inventory = Enumerable.Range(0, 59).Select(_ => new Item()).ToArray();
        public Item HeldItem => inventory[selectedItem];
        public readonly Dictionary<Type, object> ModPlayers = new();
        public T GetModPlayer<T>() where T : new()
        {
            if (!ModPlayers.TryGetValue(typeof(T), out object value)) ModPlayers[typeof(T)] = value = new T();
            return (T)value;
        }
        public object GetSource_Misc(string value) => value;
        // A controlled ItemCheck seam models accepted use or early engine/hook exit.
        public Action<Player> ItemCheckAction;
        public void ItemCheck() => ItemCheckAction?.Invoke(this);
        public enum CompositeArmStretchAmount { Full }
        public void SetCompositeArmFront(bool a, CompositeArmStretchAmount b, float c) { }
    }
    public class Projectile
    {
        public bool active, netUpdate, tileCollide, ignoreWater, friendly, hostile, netImportant;
        public int width, height, timeLeft, aiStyle, penetrate, owner, identity;
        public float rotation, scale = 1;
        public Vector2 position, velocity;
        public Vector2 Bottom => position + new Vector2(width * 0.5f, height);
        public Vector2 Center => position + new Vector2(width * 0.5f, height * 0.5f);
        public float[] ai = new float[3], localAI = new float[3];
        public ModProjectile ModProjectile;
        public static bool FailSpawn;
        public static int NewProjectile(object source, Vector2 position, Vector2 velocity, int type, int damage, float knockback, int owner, float ai0, float ai1, float ai2)
        {
            if (FailSpawn) return Main.maxProjectiles;
            int slot = Array.FindIndex(Main.projectile, p => !p.active);
            if (slot < 0) return Main.maxProjectiles;
            Projectile p = new() { active = true, position = position, velocity = velocity, owner = owner, identity = slot, ai = [ai0, ai1, ai2] };
            p.ModProjectile = new PortalCreationProjectile { Projectile = p };
            p.ModProjectile.SetDefaults();
            Main.projectile[slot] = p;
            return slot;
        }
        public void Kill() => active = false;
    }
    public class NPC
    {
        public bool active;
        public int life;
        public object ModNPC;
        public static int NewNPC(object source, int x, int y, int type)
        {
            int slot = Array.FindIndex(Main.npc, n => !n.active);
            if (slot < 0) return Main.maxNPCs;
            Main.npc[slot] = new() { active = true, life = 100, ModNPC = new PortalNPC() };
            return slot;
        }
    }
    public static class NetMessage
    {
        public static readonly List<(int Message, float Number, float Number2)> Sent = new();
        public static Action<int, int> Sending;
        public static void SendData(int message, int remote = -1, int ignore = -1, object text = null, int number = 0, float number2 = 0, float number3 = 0)
        { Sent.Add((message, number, number2)); Sending?.Invoke(message, number); }
    }
}
namespace Terraria.ID
{
    public static class NetmodeID { public const int SinglePlayer = 0, MultiplayerClient = 1, Server = 2; }
    public static class MessageID { public const int SyncEquipment = 5, PlayerControls = 13, SyncProjectile = 27, KillProjectile = 29, SyncNPC = 23; }
    public static class ItemUseStyleID { public const int HoldUp = 4; }
    public static class ItemRarityID { public const int Blue = 1; }
    public static class DustID { public const int MagicMirror = 15; }
    public static class SoundID { public static object Item6 = new(); }
}
namespace Terraria.Localization { }
namespace Terraria.ModLoader
{
    public class ModItem
    {
        public Terraria.Item Item = new(); public object Mod => null;
        public virtual string Texture => "";
        public virtual void SetDefaults() { }
        public virtual bool ConsumeItem(Terraria.Player player) => false;
        public virtual bool? UseItem(Terraria.Player player) => null;
        public virtual bool CanRightClick() => false;
        public virtual bool AltFunctionUse(Terraria.Player player) => false;
        public virtual void RightClick(Terraria.Player player) { }
        public virtual void UseStyle(Terraria.Player player, Rectangle rect) { }
        public virtual void UseItemFrame(Terraria.Player player) { }
        public virtual bool CanUseItem(Terraria.Player player) => true;
        public virtual bool PreDrawInInventory(SpriteBatch a, Vector2 b, Rectangle c, Color d, Color e, Vector2 f, float g) => false;
        public virtual bool PreDrawInWorld(SpriteBatch a, Color b, Color c, ref float d, ref float e, int f) => false;
        public virtual void ModifyTooltips(List<TooltipLine> lines) { }
        public virtual void UpdateInventory(Terraria.Player player) { }
    }
    public class ModProjectile
    {
        public Terraria.Projectile Projectile;
        public virtual string Texture => "";
        public virtual void SendExtraAI(BinaryWriter writer) { }
        public virtual void ReceiveExtraAI(BinaryReader reader) { }
        public virtual void SetDefaults() { }
        public virtual void AI() { }
        public virtual bool PreDraw(ref Color color) => false;
    }
    public class TooltipLine { public TooltipLine(object mod, string name, string text) { } }
    public static class ModContent
    {
        private static readonly Dictionary<Type, object> Instances = new();
        public static T GetInstance<T>() where T : new()
        {
            if (!Instances.TryGetValue(typeof(T), out object instance)) Instances[typeof(T)] = instance = new T();
            return (T)instance;
        }
        public static int ProjectileType<T>() => 1;
        public static int NPCType<T>() => 1;
        public static int ItemType<T>() => 1;
    }
    public class ModPacket : BinaryWriter
    {
        public static readonly List<(byte[] Bytes, int Remote, int Ignore)> Sent = new();
        public static Action<byte[]> Sending;
        public ModPacket() : base(new MemoryStream()) { }
        public void Send(int toClient = -1, int ignoreClient = -1)
        { byte[] data = ((MemoryStream)BaseStream).ToArray(); Sent.Add((data, toClient, ignoreClient)); Sending?.Invoke(data); }
    }
}
namespace PvPAdventure { public class PvPAdventure { public ModPacket GetPacket() => new(); } }
namespace PvPAdventure.Core.Config
{
    public class ServerConfig
    {
        public TravelSystemConfig TravelSystem = new();
        public class TravelSystemConfig
        {
            public int TravelPortalCreationTimePreHardmodeSeconds = 1, TravelPortalCreationTimeHardmodeSeconds = 1;
            public float PortalCreationOffset = 60;
        }
    }
}
namespace PvPAdventure.Core.Input
{
    public class KeybindsPlayer { }
    public static class Keybinds { public static string UsePortalCreatorLabel => "P"; }
}
namespace PvPAdventure.Core.Utilities { public static class Log { public static void Chat(string message) { } public static void Warn(string message) { } } }
namespace PvPAdventure.Common.Game
{
    public class GameManager { public enum Phase { Waiting, Playing } public Phase CurrentPhase = Phase.Playing; }
}
namespace PvPAdventure.Common.Travel
{
    public static class TravelRules { public static bool Enabled = true; }
    public static class TravelRegions { public static bool InSpawn; public static bool IsInWorldSpawn(Terraria.Player player) => InSpawn; }
    public static class TravelTeleportSystem { public static void ClearSelection() { } }
    public static class TeleportChat { public static void AnnouncePortalOpened(Terraria.Player player) { } }
    public static class PopupTextHelper
    {
        public static readonly List<string> Warnings = new();
        public static void NewLocalizedText(Terraria.Player p, string key, Color color) => Warnings.Add(key);
        public static void NewText(Terraria.Player p, string key, Color color) { }
    }
}
namespace PvPAdventure.Common.Travel.Portals
{
    public class PortalPlayer { internal PortalCreationAttempt CreationAttempt { get; } = new(); public void CancelCreation() { } }
    public static class PortalAssets { public static Texture2D GetPortalTexture(int team) => new(); public static Texture2D GetCreatorTexture(int team) => new(); }
    public static class PortalCreatorSkin
    {
        public static bool TryGetTexture(Terraria.Item item, int team, out Texture2D texture) { texture = null; return false; }
        public static (Rectangle, Vector2) Rescale(Rectangle a, Vector2 b, Texture2D c, Texture2D d) => (a, b);
    }
    public static class PortalDrawer
    {
        public static void SpawnPortalFadeInDust(Vector2 p, int team) { }
        public static void SpawnPortalDust(Vector2 p, float progress) { }
        public static void DrawPortalOutline(SpriteBatch a, int b, Vector2 c, Rectangle d, float e, Vector2 f, float g, float h) { }
        public static void DrawPortalHealthBar(SpriteBatch a, Vector2 b, int c, int d, float e, float f) { }
    }
}
namespace PvPAdventure.Content.Portals
{
    public class PortalNPC
    {
        public const int PortalWidth = 40, PortalHeight = 60, PortalMaxHealth = 100;
        public int OwnerIndex, OwnerTeam;
        public void Initialize(Terraria.Player player, Vector2 position) { OwnerIndex = player.whoAmI; OwnerTeam = player.team; }
        public void SetLastDamagePlayerFromNetwork(int player) { }
        public void PlayHitFxFromNetwork(bool killed) { }
    }
}
