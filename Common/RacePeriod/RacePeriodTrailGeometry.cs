using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.DataStructures;

namespace PvPAdventure.Common.RacePeriod;

internal readonly record struct TrailBounds(float Left, float Top, float Right, float Bottom)
{
    internal bool Intersects(TrailBounds other) =>
        Left < Right && Top < Bottom && other.Left < other.Right && other.Top < other.Bottom &&
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    internal TrailBounds Union(TrailBounds other) => Union(this, other);

    internal static TrailBounds Union(TrailBounds first, TrailBounds second) => new(
        Math.Min(first.Left, second.Left), Math.Min(first.Top, second.Top),
        Math.Max(first.Right, second.Right), Math.Max(first.Bottom, second.Bottom));
}

/// <summary>Measures complete animal sprites for optional movement accents; never clips trail pixels.</summary>
internal static class RacePeriodTrailGeometry
{
    internal static TrailBounds Bounds(in DrawData data)
    {
        if (!TrySource(data, out Rectangle source))
            return new(data.position.X, data.position.Y, data.position.X, data.position.Y);

        GetTransform(data, source, out Vector2 anchor, out Vector2 scale);
        float cosine = MathF.Cos(data.rotation), sine = MathF.Sin(data.rotation);
        Vector2 a = Transform(Vector2.Zero, data.origin, scale, anchor, cosine, sine);
        Vector2 b = Transform(new(source.Width, 0f), data.origin, scale, anchor, cosine, sine);
        Vector2 c = Transform(new(0f, source.Height), data.origin, scale, anchor, cosine, sine);
        Vector2 d = Transform(new(source.Width, source.Height), data.origin, scale, anchor, cosine, sine);
        return new(Math.Min(Math.Min(a.X, b.X), Math.Min(c.X, d.X)),
            Math.Min(Math.Min(a.Y, b.Y), Math.Min(c.Y, d.Y)),
            Math.Max(Math.Max(a.X, b.X), Math.Max(c.X, d.X)),
            Math.Max(Math.Max(a.Y, b.Y), Math.Max(c.Y, d.Y)));
    }

    private static bool TrySource(in DrawData data, out Rectangle source)
    {
        if (data.sourceRect is { } rectangle)
            source = rectangle;
        else if (data.texture is { } texture)
            source = new(0, 0, texture.Width, texture.Height);
        else
        {
            source = default;
            return false;
        }

        return source.Width > 0 && source.Height > 0;
    }

    private static void GetTransform(in DrawData data, Rectangle source, out Vector2 anchor, out Vector2 scale)
    {
        if (data.useDestinationRectangle)
        {
            anchor = new(data.destinationRectangle.X, data.destinationRectangle.Y);
            scale = new((float)data.destinationRectangle.Width / source.Width,
                (float)data.destinationRectangle.Height / source.Height);
        }
        else
        {
            anchor = data.position;
            scale = data.scale;
        }
    }

    private static Vector2 Transform(Vector2 point, Vector2 origin, Vector2 scale, Vector2 anchor,
        float cosine, float sine)
    {
        Vector2 local = (point - origin) * scale;
        return anchor + new Vector2(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine);
    }
}
