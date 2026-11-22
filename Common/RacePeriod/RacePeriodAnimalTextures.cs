using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Composites optional foreground parts before the body opacity and silhouette are applied.</summary>
internal static class RacePeriodAnimalTextures
{
    private static readonly Dictionary<(Texture2D Body, Texture2D Foreground), Texture2D> composites = new();

    internal static void CombineWolfLayers(List<DrawData> draws, int start)
    {
        Mount.MountData wolf = Mount.mounts[MountID.Wolf];
        Texture2D body = wolf.frontTexture.Value;
        Texture2D foreleg = wolf.frontTextureExtra.Value;
        if (body is null || foreleg is null)
            return;

        int first = -1;
        bool hasBody = false, hasForeleg = false;
        for (int i = start; i < draws.Count; i++)
        {
            DrawData draw = draws[i];
            if (draw.texture != body && draw.texture != foreleg)
                continue;
            if (draw.useDestinationRectangle || (first >= 0 && !SamePlacement(draws[first], draw)))
                return;

            hasBody |= draw.texture == body;
            hasForeleg |= draw.texture == foreleg;
            if (first < 0)
                first = i;
        }

        if (!hasBody || !hasForeleg)
            return;
        Texture2D composite = GetBody(body, foreleg);
        if (composite == body)
            return;

        // Draw the complete pose once so the outline cannot trace the foreleg over the torso.
        DrawData merged = draws[first];
        merged.texture = composite;
        draws[first] = merged;
        for (int i = draws.Count - 1; i >= start; i--)
        {
            Texture2D texture = draws[i].texture;
            if (i != first && (texture == body || texture == foreleg || texture == Asset<Texture2D>.DefaultValue || texture is null))
                draws.RemoveAt(i);
        }
    }

    private static bool SamePlacement(DrawData left, DrawData right) =>
        left.position == right.position && left.sourceRect == right.sourceRect && left.color == right.color &&
        left.rotation == right.rotation && left.origin == right.origin && left.scale == right.scale &&
        left.effect == right.effect && left.shader == right.shader && left.ignorePlayerRotation == right.ignorePlayerRotation;

    internal static Texture2D GetBody(Texture2D body, Texture2D? foreground)
    {
        if (foreground is null || body.IsDisposed || foreground.IsDisposed || body.Bounds != foreground.Bounds ||
            body.Format != SurfaceFormat.Color || foreground.Format != SurfaceFormat.Color)
            return body;

        var key = (body, foreground);
        if (composites.TryGetValue(key, out Texture2D composite))
            return composite;

        Color[] pixels = new Color[body.Width * body.Height];
        Color[] overlay = new Color[pixels.Length];
        body.GetData(pixels);
        foreground.GetData(overlay);
        for (int i = 0; i < pixels.Length; i++)
        {
            Color front = overlay[i];
            if (front.A == 0)
                continue;

            Color back = pixels[i] * (1f - front.A / 255f);
            pixels[i] = new Color(front.R + back.R, front.G + back.G, front.B + back.B, front.A + back.A);
        }

        composite = new Texture2D(body.GraphicsDevice, body.Width, body.Height);
        composite.SetData(pixels);
        composites.Add(key, composite);
        return composite;
    }

    internal static void Dispose()
    {
        foreach (Texture2D texture in composites.Values)
            texture.Dispose();
        composites.Clear();
    }
}
