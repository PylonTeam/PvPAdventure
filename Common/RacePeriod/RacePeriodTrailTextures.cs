using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Neutral animal silhouettes keep original sprite pigments out of the team trail.</summary>
[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodTrailTextures : ModSystem
{
    private static readonly Dictionary<Texture2D, Texture2D> masks = new();
    private static readonly Dictionary<Texture2D, Color[]> sourcePixels = new();
    private readonly record struct GlowKey(Texture2D Texture, Rectangle Source, int RadiusSteps);
    private readonly record struct GlowFrame(Texture2D Texture, int Padding);
    private static readonly Dictionary<GlowKey, GlowFrame> glows = new();
    private static readonly HashSet<Texture2D> trailTextures = new();
    private static Texture2D wisp;
    private static Texture2D streak;

    internal static bool IsTrailTexture(Texture2D texture) =>
        texture != null && trailTextures.Contains(texture);

    internal static Texture2D Wisp => wisp ??= CreateWisp();
    internal static Texture2D Streak => streak ??= CreateTexture(1, 1, [Color.White]);

    internal static Texture2D Mask(Texture2D texture)
    {
        if (masks.TryGetValue(texture, out Texture2D mask))
            return mask;

        Color[] pixels = CreateMaskPixels(GetSourcePixels(texture));
        mask = CreateTexture(texture.Width, texture.Height, pixels, texture.GraphicsDevice);
        masks.Add(texture, mask);
        return mask;
    }

    /// <summary>A complete padded frame lets soft glow extend beyond sprite edges without cuts.</summary>
    internal static DrawData SoftGlow(DrawData original, float radius)
    {
        Texture2D texture = original.texture;
        if (texture == null || texture.IsDisposed)
            return original;
        Rectangle source = original.sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
        if (!ValidSource(texture.Width, texture.Height, source))
            return original;

        int radiusSteps = QuantizedRadiusSteps(radius);
        GlowKey key = new(texture, source, radiusSteps);
        if (!glows.TryGetValue(key, out GlowFrame frame))
        {
            Color[] pixels = BlurFramePixels(GetSourcePixels(texture), texture.Width, texture.Height,
                source, radiusSteps / 4f, out int padding);
            Texture2D glow = CreateTexture(source.Width + 2 * padding, source.Height + 2 * padding,
                pixels, texture.GraphicsDevice);
            frame = new GlowFrame(glow, padding);
            glows.Add(key, frame);
        }

        Vector2 anchor = original.position;
        Vector2 scale = original.scale;
        if (original.useDestinationRectangle)
        {
            anchor = new(original.destinationRectangle.X, original.destinationRectangle.Y);
            scale = new((float)original.destinationRectangle.Width / source.Width,
                (float)original.destinationRectangle.Height / source.Height);
        }
        return new DrawData(frame.Texture, anchor,
            new Rectangle(0, 0, frame.Texture.Width, frame.Texture.Height), original.color,
            original.rotation, original.origin + new Vector2(frame.Padding), scale, original.effect)
        {
            shader = 0,
            ignorePlayerRotation = original.ignorePlayerRotation
        };
    }

    private static Color[] GetSourcePixels(Texture2D texture)
    {
        if (sourcePixels.TryGetValue(texture, out Color[] pixels))
            return pixels;
        pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        sourcePixels.Add(texture, pixels);
        return pixels;
    }

    /// <summary>Preserves every source alpha pixel in a neutral premultiplied white silhouette.</summary>
    internal static Color[] CreateMaskPixels(Color[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        Color[] mask = new Color[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte alpha = pixels[i].A;
            mask[i] = new Color(alpha, alpha, alpha, alpha);
        }
        return mask;
    }

    /// <summary>Blurs only this frame's alpha in a larger transparent canvas, never adjacent frames.</summary>
    internal static Color[] BlurFramePixels(Color[] pixels, int textureWidth, int textureHeight,
        Rectangle source, float radius, out int padding)
    {
        float sigma = QuantizedRadiusSteps(radius) / 4f;
        int support = (int)MathF.Ceiling(3f * sigma);
        padding = support + 1;
        if (pixels == null || (long)textureWidth * textureHeight > pixels.Length ||
            !ValidSource(textureWidth, textureHeight, source))
            return Array.Empty<Color>();

        int width = source.Width + 2 * padding;
        int height = source.Height + 2 * padding;
        float[] kernel = new float[2 * support + 1];
        float normalization = 0f;
        for (int offset = -support; offset <= support; offset++)
        {
            float weight = MathF.Exp(-offset * offset / (2f * sigma * sigma));
            kernel[offset + support] = weight;
            normalization += weight;
        }
        for (int i = 0; i < kernel.Length; i++)
            kernel[i] /= normalization;

        float[] horizontal = new float[width * height];
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                float alpha = pixels[(source.Y + y) * textureWidth + source.X + x].A / 255f;
                if (alpha == 0f) continue;
                int destination = (y + padding) * width + x + padding;
                for (int offset = -support; offset <= support; offset++)
                    horizontal[destination + offset] += alpha * kernel[offset + support];
            }

        Color[] blurred = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float alpha = 0f;
                for (int offset = -support; offset <= support; offset++)
                {
                    int sampleY = y + offset;
                    if ((uint)sampleY < (uint)height)
                        alpha += horizontal[sampleY * width + x] * kernel[offset + support];
                }
                byte value = (byte)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255);
                blurred[y * width + x] = new Color(value, value, value, value);
            }
        return blurred;
    }

    private static int QuantizedRadiusSteps(float radius) =>
        (int)MathF.Round(Math.Clamp(float.IsFinite(radius) ? radius : 1f, 0.5f, 4f) * 4f);

    private static bool ValidSource(int width, int height, Rectangle source) =>
        width > 0 && height > 0 && source.Width > 0 && source.Height > 0 && source.X >= 0 && source.Y >= 0 &&
        (long)source.X + source.Width <= width && (long)source.Y + source.Height <= height;

    private static Texture2D CreateWisp()
    {
        const int size = 16;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f));
                float alpha = MathF.Pow(Math.Clamp(1f - distance / (size / 2f), 0f, 1f), 2f);
                pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
        return CreateTexture(size, size, pixels);
    }

    private static Texture2D CreateTexture(int width, int height, Color[] pixels, GraphicsDevice graphicsDevice = null)
    {
        Texture2D texture = new(graphicsDevice ?? Main.instance.GraphicsDevice, width, height);
        texture.SetData(pixels);
        sourcePixels.Add(texture, pixels);
        trailTextures.Add(texture);
        return texture;
    }

    public override void Unload()
    {
        Texture2D[] textures = trailTextures.ToArray();
        masks.Clear();
        sourcePixels.Clear();
        glows.Clear();
        trailTextures.Clear();
        wisp = null;
        streak = null;
        Main.QueueMainThreadAction(() =>
        {
            foreach (Texture2D texture in textures)
                texture.Dispose();
        });
    }
}
