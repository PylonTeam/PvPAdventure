using PvPAdventure.Common.Game.StatTrackers;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game.GameReporters;

/// <summary>Shared match-stat keys and payload helpers for Pylon reporting.</summary>
internal static class StatsReporter
{
    public const string DamageDealt = "damage_dealt";
    public const string DamageTaken = "damage_taken";
    public const string ConsumablesUsed = "consumables_used";
    public const string TilesPlaced = "tiles_placed";
    public const string TilesMined = "tiles_mined";
    public const string MiningToolsUsed = "mining_tools_used";
    public const string LavaDeaths = "lava_deaths";
    public const string FoodEaten = "food_eaten";
    public const string BossDamageDealt = "boss_damage_dealt";
    public const string PortalKills = "portal_kills";
    public const string LostHoney = "lost_honey";
    public const string PointKills = "point_kills";
    public const string PointDeaths = "point_deaths";

    public static Dictionary<string, uint> CopyStats(Player player) =>
        player == null ? [] : player.GetModPlayer<MatchStatsPlayer>().CopyStats();

    public static Dictionary<string, IDictionary<int, uint>> CopyItemStats(Player player) =>
        player == null ? [] : player.GetModPlayer<MatchStatsPlayer>().CopyItemStats();

    internal static string GetStatKey(MatchStatKey statKey) => statKey switch
    {
        MatchStatKey.DamageDealt => DamageDealt,
        MatchStatKey.DamageTaken => DamageTaken,
        MatchStatKey.ConsumablesUsed => ConsumablesUsed,
        MatchStatKey.TilesPlaced => TilesPlaced,
        MatchStatKey.TilesMined => TilesMined,
        MatchStatKey.MiningToolsUsed => MiningToolsUsed,
        MatchStatKey.LavaTouched => LavaDeaths,
        MatchStatKey.FoodEaten => FoodEaten,
        MatchStatKey.BossDamageDealt => BossDamageDealt,
        MatchStatKey.PortalKills => PortalKills,
        MatchStatKey.LostHoney => LostHoney,
        MatchStatKey.PointKills => PointKills,
        MatchStatKey.PointDeaths => PointDeaths,
        _ => ""
    };

    internal static bool IsValidClientDelta(MatchStatKey statKey, int itemKey, uint amount)
    {
        if (amount != 1)
            return false;

        return statKey switch
        {
            MatchStatKey.ConsumablesUsed or MatchStatKey.TilesPlaced or MatchStatKey.MiningToolsUsed or MatchStatKey.FoodEaten
                => IsValidItemId(itemKey),
            MatchStatKey.TilesMined => itemKey >= 0 && itemKey < TileLoader.TileCount,
            MatchStatKey.LostHoney => itemKey < 0,
            _ => false
        };
    }

    internal static bool IsValidItemId(int itemId) => itemId > 0 && itemId < ItemLoader.ItemCount;
}
