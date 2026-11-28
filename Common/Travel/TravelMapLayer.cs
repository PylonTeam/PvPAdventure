using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Travel.Portals;
using PvPAdventure.Content.Portals;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Travel;

/// <summary>
/// Draw beds, world spawn and portals on the map overlay, and handle clicks for teleportation.
/// Override SpawnMapLayer to prevent spawning vanilla world spawn and beds.
/// </summary>
[Autoload(Side = ModSide.Client)]
public class TravelMapLayer : ModSystem
{
    public override void Load()
    {
        On_SpawnMapLayer.Draw += OnSpawnMapLayer;
    }

    public override void Unload()
    {
        On_SpawnMapLayer.Draw -= OnSpawnMapLayer;
    }

    private void OnSpawnMapLayer(On_SpawnMapLayer.orig_Draw orig, SpawnMapLayer self, ref MapOverlayDrawContext context, ref string text)
    {
        Player local = Main.LocalPlayer;

        if (local?.active != true)
            return;

        TravelTarget? hoveredTarget = null;
        DrawWorldSpawn(local, ref context, ref text, ref hoveredTarget);
        DrawBeds(local, ref context, ref text, ref hoveredTarget);
        DrawPortals(local, ref context, ref text, ref hoveredTarget);
        DrawSpawnboxPortal(local, ref context, ref text, ref hoveredTarget);

        if (hoveredTarget is { } target && TryClick(local))
            TravelTeleportSystem.ActivateTarget(target);
    }

    private static void DrawWorldSpawn(Player local, ref MapOverlayDrawContext context, ref string text, ref TravelTarget? hoveredTarget)
    {
        TravelTarget target = new(TravelType.World, -1, GetPlayerTopLeftAtTile(local, Main.spawnTileX, Main.spawnTileY), "World Spawn", "World", true);
        bool canTeleport = CanExecuteMapTeleport(local, target);
        Color color = GetMapPointColor(canTeleport);

        MapOverlayDrawContext.DrawResult result = context.Draw(TextureAssets.SpawnPoint.Value, new Vector2(Main.spawnTileX, Main.spawnTileY), color, new SpriteFrame(1, 1), 1f, 1.8f, Alignment.Bottom);

        if (!result.IsMouseOver)
            return;

        BlockMapInput(local);
        text = GetMapHoverText(local, target, Language.GetTextValue("Mods.PvPAdventure.Travel.TeleportToWorldSpawn"), canTeleport);

        if (canTeleport)
            hoveredTarget ??= target;
    }

    private static void DrawBeds(Player local, ref MapOverlayDrawContext context, ref string text, ref TravelTarget? hoveredTarget)
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];

            if (player?.active != true || !IsFriendlyPlayer(local, player))
                continue;

            if (player.SpawnX < 0 || player.SpawnY < 0 || !Player.CheckSpawn(player.SpawnX, player.SpawnY))
                continue;

            string bedName = Language.GetTextValue("Mods.PvPAdventure.Travel.TeammatesBed", player.name);
            TravelTarget target = new(TravelType.Bed, player.whoAmI, GetPlayerTopLeftAtTile(local, player.SpawnX, player.SpawnY), bedName, "Bed", true);
            Vector2 tilePos = new(player.SpawnX, player.SpawnY);
            bool canTeleport = CanExecuteMapTeleport(local, target);
            Color color = GetMapPointColor(canTeleport);

            MapOverlayDrawContext.DrawResult result = context.Draw(TextureAssets.Item[ItemID.Bed].Value, tilePos, color, new SpriteFrame(1, 1), 1f, 1.8f, Alignment.Bottom);

            if (!result.IsMouseOver)
                continue;

            BlockMapInput(local);

            string teleportText = player.whoAmI == local.whoAmI
                ? Language.GetTextValue("Mods.PvPAdventure.Travel.TeleportToMyBed")
                : Language.GetTextValue("Mods.PvPAdventure.Travel.TeleportToTeammatesBed", player.name);

            text = GetMapHoverText(local, target, teleportText, canTeleport);

            if (canTeleport)
                hoveredTarget ??= target;
        }
    }

    private static void DrawPortals(Player local, ref MapOverlayDrawContext context, ref string text, ref TravelTarget? hoveredTarget)
    {
        foreach (PortalNPC portal in PortalSystem.ActivePortals())
        {
            if (!PortalSystem.IsFriendlyPortal(local, portal))
                continue;

            string ownerName = GetPlayerName(portal.OwnerIndex);
            string portalName = Language.GetTextValue("Mods.PvPAdventure.Travel.TeammatesPortal", ownerName);
            TravelTarget target = new(TravelType.Portal, portal.OwnerIndex, GetPlayerTopLeftAtWorldBottom(local, portal.WorldPosition), portalName, "Portal", true);
            bool canTeleport = CanExecuteMapTeleport(local, target);
            Color color = GetMapPointColor(canTeleport);

            MapOverlayDrawContext.DrawResult result = context.Draw(PortalAssets.GetPortalMinimapTexture(portal.OwnerTeam), portal.WorldPosition / 16f, color, new SpriteFrame(1, 1), 1f, 1.8f, Alignment.Bottom);

            if (!result.IsMouseOver)
                continue;

            BlockMapInput(local);

            string teleportText = portal.OwnerIndex == local.whoAmI
                ? Language.GetTextValue("Mods.PvPAdventure.Travel.TeleportToMyPortal")
                : Language.GetTextValue("Mods.PvPAdventure.Travel.TeleportToTeammatesPortal", ownerName);

            text = GetMapHoverText(local, target, teleportText, canTeleport);

            if (canTeleport)
                hoveredTarget ??= target;
        }
    }

    private static void DrawSpawnboxPortal(Player local, ref MapOverlayDrawContext context, ref string text, ref TravelTarget? hoveredTarget)
    {
        if (!SpawnboxPortal.IsTeamMember(local) || !SpawnboxPortal.TryGetAnchor(out Vector2 bottom))
            return;

        string name = Language.GetTextValue("Mods.PvPAdventure.Travel.SpawnboxPortal");
        TravelTarget target = new(TravelType.SpawnboxPortal, -1, GetPlayerTopLeftAtWorldBottom(local, bottom), name, "Portal", true);
        bool canTeleport = CanExecuteMapTeleport(local, target);
        MapOverlayDrawContext.DrawResult result = context.Draw(PortalAssets.GetPortalMinimapTexture(local.team),
            bottom / 16f, GetMapPointColor(canTeleport), new SpriteFrame(1, 1), 1f, 1.8f, Alignment.Bottom);

        if (!result.IsMouseOver)
            return;

        BlockMapInput(local);
        text = GetMapHoverText(local, target, Language.GetTextValue("Mods.PvPAdventure.Travel.TeleportToSpawnboxPortal"), canTeleport);
        // This marker takes precedence over overlapping spawn and player markers, even while sealed.
        hoveredTarget = canTeleport ? target : null;
    }

    private static bool TryClick(Player local)
    {
        if (!Main.mouseLeft || !Main.mouseLeftRelease)
            return false;

        BlockMapInput(local);
        local.releaseUseItem = false;
        Main.mouseLeftRelease = false;
        Main.mapFullscreen = false;
        return true;
    }

    private static void BlockMapInput(Player local)
    {
        local.mouseInterface = true;
        local.controlUseItem = false;
    }

    private static bool IsFriendlyPlayer(Player local, Player player)
    {
        return player.whoAmI == local.whoAmI || local.team > 0 && player.team == local.team;
    }

    private static string GetPlayerName(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < Main.maxPlayers && Main.player[playerIndex]?.active == true
            ? Main.player[playerIndex].name
            : "Unknown Player";
    }

    private static Vector2 GetPlayerTopLeftAtTile(Player player, int tileX, int tileY)
    {
        return new Vector2(tileX * 16f + 8f - player.width * 0.5f, tileY * 16f - player.height);
    }

    private static Vector2 GetPlayerTopLeftAtWorldBottom(Player player, Vector2 worldBottom)
    {
        return new Vector2(worldBottom.X - player.width * 0.5f, worldBottom.Y - player.height);
    }

    private static bool CanExecuteMapTeleport(Player local, TravelTarget target)
    {
        return (target.Type == TravelType.SpawnboxPortal || TravelRegions.IsInTravelRegion(local)) &&
            !TravelTeleportSystem.IsWaitingForPortalCreator(local) &&
            TravelTeleportSystem.CanTeleport(local, target, out _);
    }

    private static Color GetMapPointColor(bool canTeleport) => canTeleport ? Color.White : Color.White * 0.5f;

    private static string GetMapHoverText(Player local, TravelTarget target, string teleportText, bool canTeleport)
    {
        if (canTeleport)
            return teleportText;

        return TravelTeleportSystem.TeleportCooldownSecondsLeft(local) > 0 ? TravelTeleportSystem.TeleportCooldownText(local) : target.Name;
    }
}
