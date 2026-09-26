using Microsoft.Xna.Framework;
using PvPAdventure.Core.Compat;
using PvPAdventure.Common.Travel.Beds;
using PvPAdventure.Common.Travel.Portals;
using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ModLoader;
using Team = Terraria.Enums.Team;

namespace PvPAdventure.Common.Travel;

internal static class TravelRegions
{
    private static float RegionRadiusWorld => ModContent.GetInstance<ServerConfig>().TravelSystem.TravelRegionRadiusTiles * 16f;

    public static bool CanUseTravelUI(Player player)
    {
        return IsValidPlayer(player) && (IsCreatingPortal(player) || IsInTravelRegion(player));
    }

    public static bool IsInTravelRegion(Player player)
    {
        return IsValidPlayer(player) &&
            (IsNearWorldSpawn(player) || IsNearOwnBed(player) || IsNearTeamBed(player) || IsNearFriendlyPortal(player));
    }

    public static bool IsInWorldSpawn(Player player) => IsValidPlayer(player) && IsNearWorldSpawn(player);

    private static bool IsValidPlayer(Player player)
    {
        return player?.active == true && !player.dead && !player.ghost;
    }

    private static bool IsCreatingPortal(Player player)
    {
        return player.itemAnimation > 0 && player.HeldItem?.ModItem is PortalCreatorItem;
    }

    private static bool IsNearWorldSpawn(Player player)
    {
        return AdventureRegionSystem.Contains(player);
    }

    private static bool IsNearOwnBed(Player player)
    {
        return player.SpawnX >= 0 &&
            player.SpawnY >= 0 &&
            Player.CheckSpawn(player.SpawnX, player.SpawnY) &&
            IsNear(player.Center, TileCenter(player.SpawnX, player.SpawnY));
    }

    private static bool IsNearTeamBed(Player player)
    {
        if (player.team <= 0)
            return false;

        foreach ((Point origin, Team team) in ModContent.GetInstance<TeamBedSystem>().ActiveBeds())
        {
            if ((int)team == player.team && IsNear(player.Center, new Vector2((origin.X + 2f) * 16f, (origin.Y + 1f) * 16f)))
                return true;
        }

        return false;
    }

    private static bool IsNearFriendlyPortal(Player player)
    {
        foreach (PortalNPC portal in PortalSystem.ActivePortals())
        {
            if (PortalSystem.IsFriendlyPortal(player, portal) && IsNear(player.Center, portal.WorldPosition))
                return true;
        }

        return false;
    }

    private static bool IsNear(Vector2 a, Vector2 b)
    {
        float radius = RegionRadiusWorld;
        return Vector2.DistanceSquared(a, b) <= radius * radius;
    }

    private static Vector2 TileCenter(int x, int y)
    {
        return new Vector2(x * 16f + 8f, y * 16f + 8f);
    }
}
