using System;
using Microsoft.Xna.Framework;

namespace PvPAdventure.Utilities.Xna;

internal static class PixelArtScale
{
    // Scale2x chooses the original texel for each quarter without adding intermediate colors.
    internal static (int TopLeft, int TopRight, int BottomLeft, int BottomRight) RefinePixel(
        ReadOnlySpan<Color> pixels, int center, int left, int right, int above, int below)
    {
        if (pixels[above] == pixels[below] || pixels[left] == pixels[right])
            return (center, center, center, center);

        return (
            pixels[left] == pixels[above] ? left : center,
            pixels[above] == pixels[right] ? right : center,
            pixels[left] == pixels[below] ? left : center,
            pixels[below] == pixels[right] ? right : center
        );
    }
}
