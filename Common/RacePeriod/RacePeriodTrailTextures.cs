using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ErkySSC.Common.RegionProtection.VFX;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Neutral textures for the team wake and powered jump poses.</summary>
[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodTrailTextures : ModSystem
{
    private static readonly Dictionary<Texture2D, Texture2D> masks = new();
    private static readonly HashSet<Texture2D> trailTextures = new();
    private static Texture2D streak;

    internal static bool IsTrailTexture(Texture2D texture) =>
        texture != null && trailTextures.Contains(texture);

    public override void PostSetupContent() =>
        ModContent.GetInstance<SpawnRegionGlow>().RegisterTextureExclusion(IsTrailTexture);

    internal static Texture2D Streak =>
        streak ??= CreateTexture(Main.instance.GraphicsDevice, 1, 1, [Color.White]);

    internal static Texture2D Mask(Texture2D texture)
    {
        if (masks.TryGetValue(texture, out Texture2D mask))
            return mask;

        Color[] pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        for (int i = 0; i < pixels.Length; i++)
        {
            // AlphaBlend expects the neutral mask to be premultiplied.
            byte alpha = pixels[i].A;
            pixels[i] = new Color(alpha, alpha, alpha, alpha);
        }

        mask = CreateTexture(texture.GraphicsDevice, texture.Width, texture.Height, pixels);
        masks.Add(texture, mask);
        return mask;
    }

    private static Texture2D CreateTexture(GraphicsDevice graphicsDevice, int width, int height, Color[] pixels)
    {
        Texture2D texture = new(graphicsDevice, width, height);
        texture.SetData(pixels);
        trailTextures.Add(texture);
        return texture;
    }

    public override void Unload()
    {
        ModContent.GetInstance<SpawnRegionGlow>()?.UnregisterTextureExclusion(IsTrailTexture);
        Texture2D[] textures = trailTextures.ToArray();
        masks.Clear();
        trailTextures.Clear();
        streak = null;
        Main.QueueMainThreadAction(() =>
        {
            foreach (Texture2D texture in textures)
                texture.Dispose();
        });
    }
}
