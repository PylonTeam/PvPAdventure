using Microsoft.Xna.Framework.Graphics;
using Pylon.Core.Utilities;
using ReLogic.Content;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Portals;

internal static class PortalAssets
{
    private static readonly Texture2D[] CreatorTextures = new Texture2D[6];
    private static readonly Texture2D[] PortalTextures = new Texture2D[6];
    private static readonly Texture2D[] PortalMinimapTextures = new Texture2D[6];
    private static readonly Texture2D[] PortalOutlinesTextures = new Texture2D[6];

    public static Texture2D GetPortalOutlinesTexture(int team)
    {
        int index = GetTeamIndex(team);

        return PortalOutlinesTextures[index] ??= ModContent.Request<Texture2D>(
            GetPortalOutlinesTexturePath(index),
            AssetRequestMode.ImmediateLoad
        ).Value;
    }

    public static Texture2D GetCreatorTexture(int team)
    {
        int index = GetTeamIndex(team);

        return CreatorTextures[index] ??= ModContent.Request<Texture2D>(
            GetCreatorTexturePath(index),
            AssetRequestMode.ImmediateLoad
        ).Value;
    }

    /// <summary>
    /// The portal gun skin variant for a team, or null when the texture has not finished loading.
    /// Callers fall back to <see cref="GetCreatorTexture"/>.
    /// </summary>
    public static Texture2D GetPortalGunSkinTexture(int team)
    {
        Asset<Texture2D> asset = GetTeamIndex(team) switch
        {
            0 => Pylon.Core.Utilities.Ass.PortalGunSkinWhite,
            1 => Pylon.Core.Utilities.Ass.PortalGunSkinRed,
            2 => Pylon.Core.Utilities.Ass.PortalGunSkinGreen,
            3 => Pylon.Core.Utilities.Ass.PortalGunSkinBlue,
            4 => Pylon.Core.Utilities.Ass.PortalGunSkinYellow,
            5 => Pylon.Core.Utilities.Ass.PortalGunSkinPink,
            _ => null
        };

        return asset is { IsLoaded: true } ? asset.Value : null;
    }

    public static Texture2D GetPortalTexture(int team)
    {
        int index = GetTeamIndex(team);

        return PortalTextures[index] ??= ModContent.Request<Texture2D>(
            GetPortalTexturePath(index),
            AssetRequestMode.ImmediateLoad
        ).Value;
    }

    public static Texture2D GetPortalMinimapTexture(int team)
    {
        int index = GetTeamIndex(team);

        return PortalMinimapTextures[index] ??= ModContent.Request<Texture2D>(
            GetPortalMinimapTexturePath(index),
            AssetRequestMode.ImmediateLoad
        ).Value;
    }

    private static int GetTeamIndex(int team)
    {
        if (team >= 1 && team <= 5)
            return team;

        return 0;
    }

    private static string GetCreatorTexturePath(int team)
    {
        //return "PvPAdventure/Assets/Items/AdventureMirror";

        return team switch
        {
            1 => "PvPAdventure/Assets/Portals/PortalCreator_Red",
            2 => "PvPAdventure/Assets/Portals/PortalCreator_Green",
            3 => "PvPAdventure/Assets/Portals/PortalCreator_Blue",
            4 => "PvPAdventure/Assets/Portals/PortalCreator_Yellow",
            5 => "PvPAdventure/Assets/Portals/PortalCreator_Pink",
            _ => "PvPAdventure/Assets/Portals/PortalCreator_NoTeam"
        };
    }

    private static string GetPortalTexturePath(int team)
    {
        return team switch
        {
            1 => "PvPAdventure/Assets/Portals/Portal_Red",
            2 => "PvPAdventure/Assets/Portals/Portal_Green",
            3 => "PvPAdventure/Assets/Portals/Portal_Blue",
            4 => "PvPAdventure/Assets/Portals/Portal_Yellow",
            5 => "PvPAdventure/Assets/Portals/Portal_Pink",
            _ => "PvPAdventure/Assets/Portals/Portal_NoTeam"
        };
    }

    private static string GetPortalOutlinesTexturePath(int team)
    {
        return team switch
        {
            1 => "PvPAdventure/Assets/Portals/Portal_Red_Outlines",
            2 => "PvPAdventure/Assets/Portals/Portal_Green_Outlines",
            3 => "PvPAdventure/Assets/Portals/Portal_Blue_Outlines",
            4 => "PvPAdventure/Assets/Portals/Portal_Yellow_Outlines",
            5 => "PvPAdventure/Assets/Portals/Portal_Pink_Outlines",
            _ => "PvPAdventure/Assets/Portals/Portal_NoTeam_Outlines"
        };
    }

    private static string GetPortalMinimapTexturePath(int team)
    {
        return team switch
        {
            1 => "PvPAdventure/Assets/Portals/PortalMinimap_Red",
            2 => "PvPAdventure/Assets/Portals/PortalMinimap_Green",
            3 => "PvPAdventure/Assets/Portals/PortalMinimap_Blue",
            4 => "PvPAdventure/Assets/Portals/PortalMinimap_Yellow",
            5 => "PvPAdventure/Assets/Portals/PortalMinimap_Pink",
            _ => "PvPAdventure/Assets/Portals/PortalMinimap_NoTeam"
        };
    }
}
