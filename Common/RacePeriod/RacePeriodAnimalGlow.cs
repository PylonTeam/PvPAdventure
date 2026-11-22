using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Frame-local, hollow alpha masks keep the halo soft without sampling adjacent animation rows.</summary>
internal static class RacePeriodAnimalGlow
{
    private const int Padding = 8;
    private static readonly float[] kernel = CreateKernel();
    private static readonly Dictionary<(Texture2D, Rectangle), Texture2D> masks = new();
    private static readonly HashSet<Texture2D> glowTextures = new();

    internal static bool IsGlowTexture(Texture2D texture) => texture != null && glowTextures.Contains(texture);

    internal static void Append(ref PlayerDrawSet drawInfo, int start)
    {
        Player player = drawInfo.drawPlayer;
        Color team = player.team > 0 && player.team < Main.teamColor.Length ? Main.teamColor[player.team] : Color.White;
        float breath = MathHelper.SmoothStep(0f, 1f, RacePeriodPulse.GetWave(player));
        List<DrawData> glow = null;
        for (int i = start; i < drawInfo.DrawDataCache.Count; i++)
        {
            DrawData part = drawInfo.DrawDataCache[i];
            if (part.shader != RacePeriodGhostShader.BodyShader || part.texture == null || part.useDestinationRectangle)
                continue;

            Texture2D mask = GetMask(part.texture, part.sourceRect ?? part.texture.Bounds);
            if (mask == null)
                continue;
            part.texture = mask;
            part.sourceRect = null;
            part.origin += new Vector2(Padding);
            part.shader = 0;
            part.color = team * (part.color.A / 255f * MathHelper.Lerp(0.75f, 0.78f, breath));
            (glow ??= new()).Add(part);
        }
        if (glow != null)
            drawInfo.DrawDataCache.InsertRange(start, glow);
    }

    private static Texture2D GetMask(Texture2D texture, Rectangle frame)
    {
        if (texture.IsDisposed || texture is RenderTarget2D || texture.Format != SurfaceFormat.Color ||
            frame.Width <= 0 || frame.Height <= 0 || !texture.Bounds.Contains(frame))
            return null;
        if (masks.TryGetValue((texture, frame), out Texture2D cached))
            return cached;

        Color[] source = new Color[frame.Width * frame.Height];
        texture.GetData(0, frame, source, 0, source.Length);
        int width = frame.Width + Padding * 2, height = frame.Height + Padding * 2;
        float[] alpha = new float[width * height], horizontal = new float[width * height];
        for (int y = 0; y < frame.Height; y++)
            for (int x = 0; x < frame.Width; x++)
                alpha[(y + Padding) * width + x + Padding] = source[y * frame.Width + x].A / 255f;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                for (int offset = -Padding; offset <= Padding; offset++)
                    if ((uint)(x + offset) < width)
                        horizontal[y * width + x] += alpha[y * width + x + offset] * kernel[offset + Padding];

        Color[] pixels = new Color[alpha.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float blurred = 0f;
                for (int offset = -Padding; offset <= Padding; offset++)
                    if ((uint)(y + offset) < height)
                        blurred += horizontal[(y + offset) * width + x] * kernel[offset + Padding];
                // Leave the body transparent to this layer; only its own shader controls its pulse.
                pixels[y * width + x] = Color.White * (blurred * (1f - alpha[y * width + x]));
            }

        Texture2D mask = new(texture.GraphicsDevice, width, height);
        mask.SetData(pixels);
        masks.Add((texture, frame), mask);
        glowTextures.Add(mask);
        return mask;
    }

    private static float[] CreateKernel()
    {
        float[] weights = new float[Padding * 2 + 1];
        float sum = 0f;
        for (int i = -Padding; i <= Padding; i++)
            sum += weights[i + Padding] = MathF.Exp(-(i * i) / 18f);
        for (int i = 0; i < weights.Length; i++)
            weights[i] /= sum;
        return weights;
    }

    internal static void Unload()
    {
        Texture2D[] textures = glowTextures.ToArray();
        masks.Clear();
        glowTextures.Clear();
        Main.QueueMainThreadAction(() =>
        {
            foreach (Texture2D texture in textures)
                texture.Dispose();
        });
    }
}
