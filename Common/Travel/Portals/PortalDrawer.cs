using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace PvPAdventure.Common.Travel.Portals;

public static class PortalDrawer
{
    internal static void HandleMapInteraction(Player player, Rectangle hitbox)
    {
        if (!hitbox.Contains(Main.MouseWorld.ToPoint()) || player.Distance(hitbox.Center.ToVector2()) >= 200f)
            return;

        player.noThrow = 2;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = -1;

        if (!Main.mouseRight || !Main.mouseRightRelease)
            return;

        Main.mouseRightRelease = false;
        player.TryOpeningFullscreenMap();
    }

    public static void DrawPortalOutline(SpriteBatch sb, int team, Vector2 position, Rectangle frame, float rotation, Vector2 origin, float scale, float opacity, SpriteEffects effects = SpriteEffects.None)
    {
        if (scale <= 0f || opacity <= 0f) return;

        sb.Draw(PortalAssets.GetPortalOutlinesTexture(team), position, frame, Color.White * opacity, rotation, origin, scale, effects, 0f);
    }

    public static void DrawPortalHealthBar(SpriteBatch sb, Vector2 worldPos, int health, int maxHealth, float scale, float alpha)
    {
        if (maxHealth <= 0)
            return;

        float healthRatio = (float)health / maxHealth;
        if (healthRatio > 1f)
            healthRatio = 1f;

        int barPixels = (int)(36f * healthRatio);
        if (barPixels < 3)
            barPixels = 3;

        healthRatio -= 0.1f;
        float green = healthRatio > 0.5f ? 255f : 255f * healthRatio * 2f;
        float red = healthRatio > 0.5f ? 255f * (1f - healthRatio) * 2f : 255f;
        float colorScale = alpha * 0.95f;

        red = MathHelper.Clamp(red * colorScale, 0f, 255f);
        green = MathHelper.Clamp(green * colorScale, 0f, 255f);
        float alphaByte = MathHelper.Clamp(255f * colorScale, 0f, 255f);
        Color barColor = new((byte)red, (byte)green, 0, (byte)alphaByte);

        Vector2 screenOrigin = new(worldPos.X - 18f * scale - Main.screenPosition.X, worldPos.Y - Main.screenPosition.Y);
        Texture2D backTex = TextureAssets.Hb2.Value;
        Texture2D fillTex = TextureAssets.Hb1.Value;

        if (barPixels < 34)
        {
            if (barPixels < 36)
                sb.Draw(backTex, screenOrigin + new Vector2(barPixels * scale, 0f), new Rectangle(2, 0, 2, backTex.Height), barColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (barPixels < 34)
                sb.Draw(backTex, screenOrigin + new Vector2((barPixels + 2) * scale, 0f), new Rectangle(barPixels + 2, 0, 36 - barPixels - 2, backTex.Height), barColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (barPixels > 2)
                sb.Draw(fillTex, screenOrigin, new Rectangle(0, 0, barPixels - 2, fillTex.Height), barColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            sb.Draw(fillTex, screenOrigin + new Vector2((barPixels - 2) * scale, 0f), new Rectangle(32, 0, 2, fillTex.Height), barColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            return;
        }

        if (barPixels < 36)
            sb.Draw(backTex, screenOrigin + new Vector2(barPixels * scale, 0f), new Rectangle(barPixels, 0, 36 - barPixels, backTex.Height), barColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        sb.Draw(fillTex, screenOrigin, new Rectangle(0, 0, barPixels, fillTex.Height), barColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
    public static void SpawnPortalDust(Vector2 worldPos, float progress = 1f, int dustMultiplier = 1)
    {
        progress = MathHelper.Clamp(progress, 0f, 1f);

        for (int i = 0; i < dustMultiplier; i++)
        {
            PotionOfReturnGateHelper gate = new(
                PotionOfReturnGateHelper.GateType.EntryPoint,
                worldPos,
                progress
            );

            gate.SpawnReturnPortalDust();
        }
    }

    public static void SpawnPortalFadeInDust(Vector2 worldPos, int team)
    {
        Color color = team > 0 && team < Main.teamColor.Length
            ? Main.teamColor[team]
            : Color.White;

        worldPos.Y -= 20;

        for (int i = 0; i < 32; i++)
        {
            Vector2 direction = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            Vector2 position = worldPos + direction * Main.rand.NextFloat(36f, 68f);
            Dust dust = Dust.NewDustPerfect(position, DustID.FireworksRGB, -direction * Main.rand.NextFloat(2f, 5.5f), 90, color, Main.rand.NextFloat(1.1f, 1.8f));
            dust.noGravity = true;
            dust.fadeIn = Main.rand.NextFloat(0.4f, 1f);
        }
    }

    public static void SpawnFinalPortalCreationBurst(Vector2 worldPos, int team)
    {
        Color color = team > 0 && team < Main.teamColor.Length
            ? Main.teamColor[team]
            : Color.White;

        const int dustCount = 56;

        for (int i = 0; i < dustCount; i++)
        {
            float angle = MathHelper.TwoPi * i / dustCount + Main.rand.NextFloat(-0.06f, 0.06f);
            Vector2 direction = angle.ToRotationVector2();

            const float minStartRadius = 8f;
            const float maxStartRadius = 22f;
            const float minVelocity = 4.2f;
            const float maxVelocity = 8.2f;

            Vector2 position = worldPos + direction * Main.rand.NextFloat(minStartRadius, maxStartRadius);
            Vector2 velocity = direction * Main.rand.NextFloat(minVelocity, maxVelocity);

            Dust dust = Dust.NewDustPerfect(
                position,
                DustID.FireworksRGB,
                velocity,
                80,
                color,
                Main.rand.NextFloat(1.15f, 1.9f)
            );

            dust.noGravity = true;
            dust.fadeIn = Main.rand.NextFloat(0.5f, 1.1f);
        }
    }

    public static void DrawHoverIcon(SpriteBatch sb, Player player, Vector2 worldPos, Color outlineColor, float alpha)
    {
        const float pulseSpeed = 6f; // Lower = slower pulse.
        const float pulseScaleVariance = 0.15f; // Higher = larger size change.

        // Draw animated portal. Keep this commented out for now.
        //Texture2D iconTexture = GetPortalAsset(player).Value;
        //Rectangle source = GetPortalFrameRectangle(iconTexture);

        Texture2D iconTexture = PortalAssets.GetPortalMinimapTexture(player.team);
        Rectangle source = iconTexture.Bounds;

        //Vector2 origin = new(source.Width * 0.5f, source.Height * 0.5f); // center
        Vector2 origin = Vector2.Zero;
        Vector2 drawPos = new(Main.mouseX, Main.mouseY);
        Vector2 extraOffset = new(22f, -10);
        drawPos += extraOffset;

        float pulse = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * pulseSpeed);
        float iconScale = 1f + pulse * pulseScaleVariance;
        Color drawColor = Color.White * alpha;

        //DrawTextureOutline(sb, iconTexture, drawPos, source, origin, Color.Black * alpha, iconScale, 2f);
        //DrawTextureOutline(sb, iconTexture, drawPos, source, origin, outlineColor * alpha, iconScale, 1f);
        sb.Draw(iconTexture, drawPos, source, drawColor, 0f, origin, iconScale, SpriteEffects.None, 0f);
    }

    //private static void DrawTextureOutline(SpriteBatch sb, Texture2D texture, Vector2 position, Rectangle source, Vector2 origin, Color color, float scale, float distance)
    //{
    //    Vector2[] offsets =
    //    [
    //        new Vector2(-distance, 0f),
    //            new Vector2(distance, 0f),
    //            new Vector2(0f, -distance),
    //            new Vector2(0f, distance),
    //            new Vector2(-distance, -distance),
    //            new Vector2(-distance, distance),
    //            new Vector2(distance, -distance),
    //            new Vector2(distance, distance)
    //    ];

    //    for (int i = 0; i < offsets.Length; i++)
    //        sb.Draw(texture, position + offsets[i], source, color, 0f, origin, scale, SpriteEffects.None, 0f);
    //}
}
