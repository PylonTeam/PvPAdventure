using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Uniform sprite scale and the standing pose's depth below the collision feet.</summary>
public readonly record struct RacePeriodArtLayout(float Scale, float FloorDepth);

internal static class RacePeriodAnimalLayout
{
    private const float AnimalScaleMultiplier = 1.05f;

    internal static void Apply(ref PlayerDrawSet drawInfo, int firstDrawIndex, RacePeriodSpriteSheet? drawnCustomSheet)
    {
        Player player = drawInfo.drawPlayer;
        // These layouts describe normal-gravity art. Keep the existing inverted mount placement.
        if (player.gravDir != 1f)
            return;

        RacePeriodArtLayout? layout = drawnCustomSheet is { } custom
            ? custom.ArtLayout
            : GetVanillaLayout(player.mount.Type);
        if (layout is not { } art || art.Scale <= 0f || !float.IsFinite(art.Scale) || !float.IsFinite(art.FloorDepth))
            return;

        Vector2 feet = player.Bottom + new Vector2(0f, player.gfxOffY) - Main.screenPosition;
        float floorDepth = art.FloorDepth;
        if (drawnCustomSheet is null)
        {
            // Mount.Draw still centers on the current hitbox height, which may briefly include
            // the mount's boost on entry. The custom renderer already uses the player's bottom.
            floorDepth -= player.height / 2 - Player.defaultHeight / 2;
        }

        float scale = art.Scale * AnimalScaleMultiplier;
        Vector2 floorOffset = new(0f, floorDepth);
        for (int i = firstDrawIndex; i < drawInfo.DrawDataCache.Count; i++)
        {
            DrawData data = drawInfo.DrawDataCache[i];
            if (data.useDestinationRectangle)
                continue;

            data.position = feet + (data.position - feet - floorOffset) * scale;
            data.scale *= scale;
            drawInfo.DrawDataCache[i] = data;
        }
    }

    private static RacePeriodArtLayout? GetVanillaLayout(int mountType) => mountType switch
    {
        MountID.Bunny => new RacePeriodArtLayout(0.8f, 12f),
        MountID.PaintedHorse or MountID.MajesticHorse or MountID.DarkHorse => new RacePeriodArtLayout(0.65f, 19f),
        MountID.Wolf => new RacePeriodArtLayout(0.9f, 2f),
        _ => null
    };
}
