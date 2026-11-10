using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pylon.Common.MainMenu.Shop;
using Pylon.Common.Skins;
using Terraria;

namespace PvPAdventure.Common.Travel.Portals;

/// <summary>
/// Resolves the portal gun skin for a portal creator item. The skin rides on the item itself through
/// Pylon's skin data, so remote players' items carry it too and every client draws the same thing.
/// </summary>
internal static class PortalCreatorSkin
{
    private static readonly ProductKey PortalGun = new("portal_creator", "portal_gun");

    /// <summary>
    /// True when <paramref name="item"/> has the portal gun skin equipped and a texture exists for
    /// <paramref name="team"/>. Teams without a variant fall through so the plain creator is drawn.
    /// </summary>
    public static bool TryGetTexture(Item item, int team, out Texture2D texture)
    {
        texture = null;

        if (!SkinRegistry.TryGetSkin(item, out ShopProduct skin) || skin.Key != PortalGun)
            return false;

        texture = PortalAssets.GetPortalGunSkinTexture(team);
        return texture is not null;
    }

    /// <summary>
    /// Remaps a frame and origin authored against <paramref name="from"/> onto <paramref name="to"/>,
    /// so a skin may have different dimensions than the texture it replaces.
    /// </summary>
    public static (Rectangle? Frame, Vector2 Origin) Rescale(Rectangle? frame, Vector2 origin, Texture2D from, Texture2D to)
    {
        float sx = to.Width / (float)from.Width;
        float sy = to.Height / (float)from.Height;

        Rectangle? scaledFrame = frame is { } source
            ? new Rectangle((int)(source.X * sx), (int)(source.Y * sy), (int)(source.Width * sx), (int)(source.Height * sy))
            : null;
        return (scaledFrame, origin * new Vector2(sx, sy));
    }
}
