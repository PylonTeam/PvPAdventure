using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria.DataStructures;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Whole-mask outline passes follow the player's black diamond and team cardinal shape.</summary>
internal static class RacePeriodTrailOutlines
{
    /// <summary>Widths are screen-space cardinal distances, independent of each sprite's scale.</summary>
    internal static IReadOnlyList<Vector2> BlackOffsets(float width)
    {
        width = Bounded(width, 4f, 8f);
        if (width == 0f)
            return Array.Empty<Vector2>();
        float half = width / 2f;
        // Same order and Manhattan-radius-two diamond as LegacyPlayerRenderer.CreateOutlines.
        return new Vector2[]
        {
            new(-width, 0), new(-half, -half), new(-half, half), new(0, -width),
            new(0, width), new(half, -half), new(half, half), new(width, 0)
        };
    }

    internal static IReadOnlyList<Vector2> TeamOffsets(float width)
    {
        width = Bounded(width, 2f, 6f);
        return width == 0f ? Array.Empty<Vector2>() : new Vector2[]
        {
            new(-width, 0), new(0, -width), new(0, width), new(width, 0)
        };
    }

    /// <summary>Team borders retain the fill hue and fade with the same premultiplied alpha.</summary>
    internal static Color TeamColor(Color fillColor, float opacity, bool normalize = false)
    {
        float strength = Bounded(opacity, 1f, 1f);
        Color target = new(Channel(Math.Min(fillColor.R, fillColor.A) * strength),
            Channel(Math.Min(fillColor.G, fillColor.A) * strength),
            Channel(Math.Min(fillColor.B, fillColor.A) * strength), Channel(fillColor.A * strength));
        return normalize ? PerPassColor(target, 4) : target;
    }

    /// <summary>The configured border color's alpha also scales with the fading fill's alpha.</summary>
    internal static Color BlackColor(Color fillColor, Color configuredColor, float opacity, bool normalize = false)
    {
        byte alpha = Channel(fillColor.A * (configuredColor.A / 255f) * Bounded(opacity, 0.6f, 1f));
        Color target = new(Channel(configuredColor.R / 255f * alpha),
            Channel(configuredColor.G / 255f * alpha), Channel(configuredColor.B / 255f * alpha), alpha);
        return normalize ? PerPassColor(target, 8) : target;
    }

    /// <summary>Append all outer passes, all inner passes, then complete fill copies for one echo.</summary>
    internal static void Append(List<DrawData> output, IReadOnlyList<DrawData> silhouettes,
        Vector2 offset, Color fillColor, RacePeriodTrailSettings settings)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(silhouettes);
        ArgumentNullException.ThrowIfNull(settings);
        // Capture before appending so even a caller using its output as the source cannot recurse.
        int count = silhouettes.Count;
        if (count == 0 || fillColor.A == 0)
            return;
        fillColor = TeamColor(fillColor, 1f);

        if (settings.IsBlackOutlineEnabled)
            AppendPasses(output, silhouettes, count, offset, BlackOffsets(settings.BlackOutlineWidth),
                BlackColor(fillColor, settings.BlackOutlineColor, settings.BlackOutlineOpacity, settings.NormalizeOutlineOpacity));
        if (settings.IsTeamOutlineEnabled)
            AppendPasses(output, silhouettes, count, offset, TeamOffsets(settings.TeamOutlineWidth),
                TeamColor(fillColor, settings.TeamOutlineOpacity, settings.NormalizeOutlineOpacity));
        for (int part = 0; part < count; part++)
            output.Add(Copy(silhouettes[part], offset, fillColor));
    }

    private static void AppendPasses(List<DrawData> output, IReadOnlyList<DrawData> silhouettes,
        int count, Vector2 offset, IReadOnlyList<Vector2> outlineOffsets, Color color)
    {
        if (color.A == 0)
            return;
        foreach (Vector2 outlineOffset in outlineOffsets)
            for (int part = 0; part < count; part++)
                output.Add(Copy(silhouettes[part], offset + outlineOffset, color));
    }

    private static DrawData Copy(DrawData original, Vector2 offset, Color color)
    {
        original.position += offset;
        if (original.useDestinationRectangle)
            original.destinationRectangle.Offset((int)MathF.Round(offset.X), (int)MathF.Round(offset.Y));
        original.color = color;
        original.shader = 0;
        // Source frame, origin, scale, rotation and facing always remain whole and unchanged.
        return original;
    }

    private static float Bounded(float value, float fallback, float maximum) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, 0f, maximum);

    private static Color PerPassColor(Color target, int passes)
    {
        if (target.A == 0)
            return Color.Transparent;
        if (target.A == 255)
            return target;
        // Overlapping opaque masks compose back to the requested family opacity rather than
        // multiplying it by the number of offsets. Byte colors retain a small rounding error.
        float alpha = target.A / 255f;
        float passAlpha = (float)(1d - Math.Pow(1d - alpha, 1d / passes));
        float strength = passAlpha / alpha;
        byte opacity = Channel(passAlpha * 255f);
        return new Color(Math.Min(opacity, Channel(target.R * strength)),
            Math.Min(opacity, Channel(target.G * strength)), Math.Min(opacity, Channel(target.B * strength)), opacity);
    }

    private static byte Channel(float value) => (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
}
