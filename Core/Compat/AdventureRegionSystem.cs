using System;
using System.IO;
using System.Linq;
using ErkySSC.Common.RegionProtection;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using PvPAdventure.Common.Game;
using PvPFramework.Common.RacePeriod;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Default;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Core.Compat;

/// <summary>Adventure supplies match state; ErkySSC owns the region and all protection.</summary>
public sealed class AdventureRegionSystem : ModSystem
{
    public const string RegionKey = "PvPAdventure.Spawnbox";
    private Point lastWorldSpawn;
    public static ProtectedRegion SpawnRegion => RegionSystem.Instance.FindManaged(RegionKey);
    public static Rectangle TileArea => SpawnRegion?.Settings.TileArea ?? Rectangle.Empty;
    public static Rectangle WorldArea => SpawnRegion?.Settings.WorldArea ?? Rectangle.Empty;
    public static bool Contains(Player player) => player?.active == true && WorldArea.Intersects(player.Hitbox);
    private static bool Waiting => ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Waiting;

    public override void PostSetupContent()
    {
        RegionSystem.Instance.RegisterManaged(RegionKey, new(CreateRegion, () => Waiting,
            player => player.GetModPlayer<RacePeriodPlayer>().IsRacing ? 22 : 0));
        RacePeriodRules.WaitingProvider = () => Waiting && SpawnRegion != null;
        RacePeriodRules.EntryAreaProvider = Contains;
        RacePeriodRules.KeepDrawLayerProvider = layer => layer is RegionTeamColorLayer;
    }

    public override void Unload()
    {
        RegionSystem.Instance.UnregisterManaged(RegionKey);
        RacePeriodRules.WaitingProvider = null;
        RacePeriodRules.EntryAreaProvider = null;
        RacePeriodRules.KeepDrawLayerProvider = null;
    }

    public override void ClearWorld() => lastWorldSpawn = Point.Zero;

    public override void PreUpdateEntities()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        // A changed world spawn moves the managed region by the same amount, retaining edits.
        Point spawn = new(Main.spawnTileX, Main.spawnTileY);
        if (lastWorldSpawn != Point.Zero && spawn != lastWorldSpawn && SpawnRegion is { } region)
            RegionSystem.Instance.UpdateRegion(region.Id, region.Settings with
            {
                X = region.Settings.X + spawn.X - lastWorldSpawn.X,
                Y = region.Settings.Y + spawn.Y - lastWorldSpawn.Y
            });
        lastWorldSpawn = spawn;
    }

    public static void UpdateMatchState() => RegionSystem.Instance.RefreshManagedRegions();

    private static RegionSeed CreateRegion()
    {
        // Removed ModSystems are retained by tModLoader here, including cloud-world saves.
        // Read only the old system's payload; leave it available as a rollback backup.
        TagCompound legacy = ModContent.GetInstance<UnloadedSystem>().data?
            .FirstOrDefault(entry => entry.GetString("mod") == "PvPFramework" &&
                entry.GetString("name") == "SpawnBoxSystem")?.GetCompound("data");
        return FromLegacy(legacy, ReadLegacyConfig());
    }

    internal static RegionSeed FromLegacy(TagCompound legacy, JObject config)
    {
        JObject defaults = config?["SpawnBox"] as JObject;
        bool saved = legacy?.ContainsKey("SpawnBoxWidth") == true;
        int width = saved ? legacy.GetInt("SpawnBoxWidth") : (int?)defaults?["DefaultWidth"] ?? 50;
        int height = saved ? legacy.GetInt("SpawnBoxHeight") : (int?)defaults?["DefaultHeight"] ?? 50;
        int offsetX = saved ? legacy.GetInt("SpawnBoxXOffset") : (int?)defaults?["DefaultXOffset"] ?? 0;
        int offsetY = saved ? legacy.GetInt("SpawnBoxYOffset") : (int?)defaults?["DefaultYOffset"] ?? 0;
        int thickness = saved && legacy.ContainsKey("SpawnBoxThickness") ? legacy.GetInt("SpawnBoxThickness") : (int?)defaults?["DefaultThickness"] ?? 1;
        RegionSettings settings = RegionSystem.Defaults() with
        {
            Width = Math.Clamp(width, 5, 50), Height = Math.Clamp(height, 5, 50),
            X = Main.spawnTileX + Math.Clamp(offsetX, -20, 20),
            Y = Main.spawnTileY + Math.Clamp(offsetY, -20, 20)
        };
        return new(settings.Clamped(Main.maxTilesX, Main.maxTilesY), new RegionOptions
        {
            SpawnProtection = true, BorderThickness = Math.Clamp(thickness, 1, 10),
            ShowPlayerNames = (bool?)config?["ShowSpawnboxPlayerNames"] ?? true,
            PulseTeamColor = (bool?)config?["PulsingTeamColor"] ?? true
        });
    }

    private static JObject ReadLegacyConfig()
    {
        try
        {
            string path = Path.Combine(Main.SavePath, "ModConfigs", "PvPFramework_ServerConfig.json");
            return File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
        {
            Log.Warn($"Could not read legacy region defaults: {ex.Message}");
            return null;
        }
    }
}
