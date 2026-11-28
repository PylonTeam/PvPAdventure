using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

internal readonly record struct CountdownPortalFrame(
    Vector2 Center, float Scale, int Second, float Phase, float Opacity, float Age, int ReleaseAge,
    CountdownClashTeams Teams);

internal sealed class CountdownEffects : IDisposable
{
    internal const int ImpactTick = 18;
    internal const int CrossDustTick = 22;
    internal const int ReleaseDuration = 72;
    private const int SwordEntryTick = 4;
    private const int SwordSettleDuration = 6;
    private const int SwordHoldDuration = 3;
    private const int FadeTick = ImpactTick + SwordSettleDuration + SwordHoldDuration;
    private const int StarCrestDelayTicks = 3;
    private const int GlowSize = 48;
    private const float GateWidth = 108f;
    private const float GateHeight = 110f;
    private const float ArtPixel = 2f;
    private static readonly Color Violet = new(130, 73, 224);
    private static readonly Color Lilac = new(222, 183, 255);
    private static readonly Color Ivory = new(247, 229, 255);
    private static readonly Color GoColor = new(229, 207, 255);
    private static readonly Color ImpactColor = new(162, 87, 255);
    private static readonly Color DigitOutline = new(20, 12, 32);
    private static readonly Color LightDigitGold = new(255, 246, 208);
    private static readonly Color MediumDigitGold = new(255, 229, 134);
    private static readonly Color FinalDigitGold = new(255, 199, 58);
    private static readonly Color LeftFallback = new(151, 88, 242);

    private readonly CountdownClash clash = new();
    private Effect gateEffect;
    private Texture2D circleGlow;
    private Texture2D lineGlow;

    internal void Draw(in CountdownPortalFrame frame, string goLabel)
    {
        bool releasing = frame.ReleaseAge >= 0;
        float ticks = Math.Max(0, frame.ReleaseAge);
        float opacity = frame.Opacity * (1f - MathHelper.SmoothStep(0f, 1f, (ticks - 2f) / 16f));

        if (opacity > .001f)
        {
            float handoff = releasing ? 1f : frame.Second == 1
                ? MathHelper.SmoothStep(0f, 1f, (frame.Phase - .78f) / .22f)
                : 0f;
            Vector2 gateSize = RoundToArtGrid(new Vector2(GateWidth, GateHeight) * frame.Scale);
            Vector2 gateCenter = SnapWorldArtPosition(frame.Center - gateSize * .5f) + gateSize * .5f;
            DrawOrbit(frame, opacity, front: false);
            DrawTeamGate(frame, gateCenter, gateSize, opacity, handoff);
            DrawDigit(frame, gateCenter, ticks);
            DrawOrbit(frame, opacity, front: true);
        }

        if (!releasing) return;

        if (ticks is > 0f and < 19f)
            DrawHandoff(frame, ticks);
        DrawRelease(frame, ticks, goLabel);
    }

    internal static Vector2 OrbitPoint(Vector2 center, float scale, float phase) =>
        OrbitPointAtAngle(OrbitCenter(center, scale), OrbitRadius(scale), OrbitAngle(phase));

    internal static Vector2 ImpactPoint(Vector2 center, float scale) =>
        center - Vector2.UnitY * (20f * scale);

    internal static float StarLight(float phase) =>
        MathHelper.SmoothStep(0f, 1f, (MathF.Sin(OrbitAngle(phase)) - .72f) / .28f);

    private static float OrbitAngle(float phase)
    {
        // The star reaches the front as the digit changes, then crests three ticks later.
        float rotation = (phase - StarCrestDelayTicks / 60f) * MathHelper.TwoPi;
        return MathHelper.PiOver2 + rotation - .40f * MathF.Sin(rotation);
    }

    private static Vector2 OrbitCenter(Vector2 center, float scale) => center + Vector2.UnitY * (14f * scale);

    private static Vector2 OrbitRadius(float scale) => new Vector2(33f, 9f) * scale;

    private static Vector2 OrbitPointAtAngle(Vector2 center, Vector2 radius, float angle) =>
        center + angle.ToRotationVector2() * radius;

    private static float StarVisibility(float depth) =>
        MathHelper.SmoothStep(.02f, 1f, (depth + .28f) / 1.05f);

    private static Color PortalTint(CountdownClashTeams teams) =>
        teams.HasViewerTint ? teams.Left.ColorAlongBlade(0f) : Violet;

    private void DrawOrbit(in CountdownPortalFrame frame, float opacity, bool front)
    {
        const int segments = 24;
        const float step = MathHelper.Pi / segments;
        float start = front ? 0f : MathHelper.Pi;
        float glint = OrbitAngle(frame.Phase);
        Color aura = PortalTint(frame.Teams);
        Color trail = Color.Lerp(Lilac, aura, .38f);
        Vector2 center = OrbitCenter(frame.Center, frame.Scale);
        Vector2 radius = OrbitRadius(frame.Scale);
        Vector2 last = OrbitPointAtAngle(center, radius, start);

        for (int i = 1; i <= segments; i++)
        {
            float angle = start + i * step;
            float midpoint = angle - step * .5f;
            float gap = MathHelper.WrapAngle(midpoint - glint);
            float streak = MathHelper.SmoothStep(0f, 1f, 1f + gap / 1.28f)
                * (1f - MathHelper.SmoothStep(0f, 1f, gap / .10f));
            float depth = MathHelper.SmoothStep(.12f, 1f, (MathF.Sin(midpoint) + .38f) / 1.38f);
            Vector2 next = OrbitPointAtAngle(center, radius, angle);
            DrawLine(last, next, (3.5f + 4.2f * streak) * frame.Scale,
                Glow(aura, opacity * depth * (.12f + .52f * streak)));
            DrawLine(last, next, (1.45f + 1.2f * streak) * frame.Scale,
                Glow(trail, opacity * depth * (.13f + .65f * streak)));
            last = next;
        }

        for (int i = 1; i <= 4; i++)
        {
            float angle = glint - .16f * i;
            float depth = MathF.Sin(angle);
            if ((depth >= 0f) != front) continue;

            DrawArtPixel(OrbitPointAtAngle(center, radius, angle),
                Glow(trail, opacity * StarVisibility(depth) * (.68f - .12f * i)));
        }

        float starDepth = MathF.Sin(glint);
        if ((starDepth >= 0f) != front) return;

        float starOpacity = opacity * StarVisibility(starDepth);
        float crest = StarLight(frame.Phase);
        Vector2 head = OrbitPointAtAngle(center, radius, glint);
        DrawBloom(head, (13f + 10f * crest) * frame.Scale, Glow(aura, starOpacity * (.52f + .37f * crest)));
        DrawSparkle(head, Glow(Ivory, starOpacity * .94f), .25f + .60f * crest);
    }

    private void DrawBeatPulse(in CountdownPortalFrame frame, Vector2 center)
    {
        const float pulseDuration = .18f;
        if (frame.Phase >= pulseDuration) return;

        float progress = frame.Phase / pulseDuration;
        float energy = (1f - progress) * (1f - progress) * frame.Opacity;
        float spread = MathHelper.SmoothStep(0f, 1f, progress);
        float scale = frame.Scale;
        Color aura = PortalTint(frame.Teams);
        Color ring = Color.Lerp(Lilac, aura, .32f);
        DrawBloom(center, 91f * scale, Glow(aura, energy * .47f));

        Vector2 radius = Vector2.Lerp(new Vector2(20f, 21f), new Vector2(41f, 38f), spread) * scale;
        const int segments = 32;
        Vector2 last = center + Vector2.UnitX * radius;
        for (int i = 1; i <= segments; i++)
        {
            float angle = MathHelper.TwoPi * i / segments;
            Vector2 next = center + angle.ToRotationVector2() * radius;
            DrawLine(last, next, 2.1f * scale, Glow(ring, energy * .60f));
            last = next;
        }
    }

    private void DrawDigit(in CountdownPortalFrame frame, Vector2 gateCenter, float ticks)
    {
        float fade = frame.Second > 0 ? 1f : 1f - MathHelper.SmoothStep(0f, 1f, ticks / 6f);
        if (fade <= 0f) return;

        float accent = frame.Second > 0 ? 1f - MathHelper.SmoothStep(0f, 1f, frame.Phase / .20f) : 0f;
        var font = FontAssets.DeathText.Value;
        int second = Math.Max(frame.Second, 1);
        string digit = second.ToString();
        Vector2 size = font.MeasureString(digit);
        float scale = Math.Min(29f * frame.Scale / font.MeasureString("8").X, 39f * frame.Scale / size.X);
        float opacity = frame.Opacity * fade;
        Color ink = DigitInk(second, frame.Second > 0 ? frame.Phase : 1f, accent);
        if (frame.ReleaseAge < 0)
            DrawBeatPulse(frame, gateCenter);
        DrawOutlinedText(digit, gateCenter, DigitOrigin(font, digit), scale, 1.85f * frame.Scale,
            ink * opacity, DigitOutline * (.96f * opacity));
    }

    private static Color DigitInk(int second, float phase, float accent)
    {
        if (second > 4)
            return Color.Lerp(Ivory, Color.White, accent);
        if (second == 4)
            return Color.White;

        float duration = second switch
        {
            3 => .15f,
            2 => .10f,
            _ => 4f / 60f
        };
        return Color.Lerp(DigitTone(second + 1), DigitTone(second),
            MathHelper.SmoothStep(0f, 1f, phase / duration));
    }

    private static Color DigitTone(int second) => second switch
    {
        1 => FinalDigitGold,
        2 => MediumDigitGold,
        3 => LightDigitGold,
        4 => Color.White,
        _ => Ivory
    };

    private static Vector2 DigitOrigin(DynamicSpriteFont font, string digit)
    {
        Vector2 min = new(float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity);
        float penX = 0f;
        for (int i = 0; i < digit.Length; i++)
        {
            if (!font.SpriteCharacters.TryGetValue(digit[i], out var glyph))
                glyph = font.DefaultCharacterData;

            // Match the font renderer's placement and center the drawn glyphs within the flash.
            penX += i == 0 ? Math.Max(0f, glyph.Kerning.X) : font.CharacterSpacing + glyph.Kerning.X;
            Vector2 corner = new(penX + glyph.Padding.X, glyph.Padding.Y);
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner + new Vector2(glyph.Glyph.Width, glyph.Glyph.Height));
            penX += glyph.Kerning.Y + glyph.Kerning.Z;
        }

        return (min + max) * .5f;
    }

    private void DrawTeamGate(in CountdownPortalFrame frame, Vector2 center, Vector2 gateSize, float opacity, float handoff)
    {
        gateEffect ??= ModContent.Request<Effect>("PvPAdventure/Assets/Countdown/ReturnGateTeamColor",
            AssetRequestMode.ImmediateLoad).Value;
        EffectParameterCollection parameters = gateEffect.Parameters;
        parameters["uTime"].SetValue(frame.Age);
        parameters["uHandoff"].SetValue(handoff);
        parameters["uShine"].SetValue(StarLight(frame.Phase));
        parameters["uTeamStrength"].SetValue(frame.Teams.HasViewerTint ? 1f : 0f);
        parameters["uArtCells"].SetValue(gateSize / ArtPixel);
        parameters["uLeftHilt"].SetValue(BladeColor(frame.Teams.Left, LeftFallback, 0f).ToVector3());
        parameters["uLeftMiddle"].SetValue(BladeColor(frame.Teams.Left, LeftFallback, .5f).ToVector3());
        parameters["uLeftTip"].SetValue(BladeColor(frame.Teams.Left, LeftFallback, 1f).ToVector3());
        parameters["uRightHilt"].SetValue(BladeColor(frame.Teams.Right, GoColor, 0f).ToVector3());
        parameters["uRightMiddle"].SetValue(BladeColor(frame.Teams.Right, GoColor, .5f).ToVector3());
        parameters["uRightTip"].SetValue(BladeColor(frame.Teams.Right, GoColor, 1f).ToVector3());

        SpriteBatch batch = Main.spriteBatch;
        Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
        Texture2D quad = TextureAssets.MagicPixel.Value;
        Vector2 size = quad.Size();
        Vector2 scale = gateSize / size;
        batch.End();
        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, gateEffect, zoom);
        try
        {
            batch.Draw(quad, center, null, Color.White * opacity,
                0f, size * .5f, scale, SpriteEffects.None, 0f);
        }
        finally
        {
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, zoom);
        }
    }

    private static Color BladeColor(CountdownBladeTeams blade, Color fallback, float distance) =>
        blade.Hilt == 0 ? fallback : blade.ColorAlongBlade(distance);

    private void DrawHandoff(in CountdownPortalFrame frame, float ticks)
    {
        float travel = MathHelper.SmoothStep(0f, 1f, ticks / 14f);
        float fade = MathHelper.SmoothStep(0f, 1f, ticks / 5f)
            * (1f - MathHelper.SmoothStep(0f, 1f, (ticks - 8f) / 11f));
        float separation = MathHelper.Lerp(13f, SwordOffset(ticks), travel) * frame.Scale;
        Vector2 center = frame.Center + Vector2.UnitY * (MathHelper.Lerp(2f, -8f, travel) * frame.Scale);
        float tintShift = .65f * travel;
        // Sample the palette at its shader breakpoints for the same color as the receding gate.
        Color left = Color.Lerp(BladeColor(frame.Teams.Left, LeftFallback, .5f),
            BladeColor(frame.Teams.Left, LeftFallback, 1f), tintShift);
        Color right = Color.Lerp(BladeColor(frame.Teams.Right, GoColor, .5f),
            BladeColor(frame.Teams.Right, GoColor, 1f), tintShift);
        DrawHandoffSpark(center - Vector2.UnitX * separation, frame.Scale, left, fade);
        DrawHandoffSpark(center + Vector2.UnitX * separation, frame.Scale, right, fade);
    }

    private void DrawHandoffSpark(Vector2 center, float scale, Color color, float fade)
    {
        DrawBloom(center, 21f * scale, Glow(color, .54f * fade));
        DrawSparkle(center, Glow(Color.Lerp(color, Ivory, .43f), .67f * fade), .28f);
    }

    private void DrawRelease(in CountdownPortalFrame frame, float ticks, string label)
    {
        float exit = MathHelper.SmoothStep(0f, 1f, (ticks - FadeTick) / (ReleaseDuration - FadeTick));
        float opacity = 1f - exit;
        float scale = frame.Scale;
        float flash = ticks >= ImpactTick
            ? 1f - MathHelper.SmoothStep(0f, 1f, (ticks - ImpactTick) / (.16f * ReleaseDuration))
            : 0f;
        Vector2 hit = ImpactPoint(frame.Center, scale);

        if (flash > 0f)
        {
            Color impact = Color.Lerp(ImpactColor, PortalTint(frame.Teams), .62f);
            DrawBloom(hit, 73f * scale, Glow(impact, .33f * flash));
            DrawBloom(hit, 36f * scale, Glow(GoColor, .20f * flash));
        }

        DrawSwords(frame, scale, ticks, opacity);

        if (flash > 0f)
            DrawSparkle(hit, Glow(Color.Cornsilk, .76f * flash), flash);

        float labelOpacity = opacity * MathHelper.SmoothStep(0f, 1f, (ticks - 8f) / 6f);
        if (labelOpacity > 0f)
            DrawGo(frame.Center, scale, label, labelOpacity, flash);
    }

    private void DrawSwords(in CountdownPortalFrame frame, float scale, float ticks, float opacity)
    {
        if (ticks <= SwordEntryTick || opacity <= 0f) return;

        float reveal = MathHelper.SmoothStep(0f, 1f, (ticks - SwordEntryTick) / 11f);
        float trail = .19f * SwordApproach(ticks)
            * (1f - MathHelper.SmoothStep(0f, 1f, (ticks - ImpactTick) / 4f));
        SpriteBatch batch = Main.spriteBatch;
        Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, zoom);
        try
        {
            if (trail > .012f)
                DrawSwordsAt(frame, scale, ticks - 2f, opacity * reveal * trail);
            DrawSwordsAt(frame, scale, ticks, opacity * reveal);
        }
        finally
        {
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, zoom);
        }
    }

    private void DrawSwordsAt(in CountdownPortalFrame frame, float scale, float ticks, float opacity)
    {
        float approach = SwordApproach(ticks);
        Vector2 center = frame.Center + Vector2.UnitY * (MathHelper.Lerp(4f, 0f, approach) * scale);
        clash.DrawPair(center, SwordOffset(ticks) * scale, opacity, frame.Teams);
    }

    private static float SwordApproach(float ticks)
    {
        float amount = MathHelper.Clamp((ticks - SwordEntryTick) / (ImpactTick - SwordEntryTick), 0f, 1f);
        return amount * amount;
    }

    private static float SwordSettle(float ticks) =>
        MathHelper.SmoothStep(0f, 1f, (ticks - ImpactTick) / SwordSettleDuration);

    private static float SwordOffset(float ticks) =>
        MathHelper.Lerp(56f, 18f, SwordApproach(ticks)) * (1f - SwordSettle(ticks));

    private static void DrawGo(Vector2 center, float scale, string label, float opacity, float flash)
    {
        var font = FontAssets.DeathText.Value;
        Vector2 size = font.MeasureString(label);
        center += Vector2.UnitY * (44f * scale);
        float glyphScale = 63f * scale / Math.Max(size.X, 1f);
        Color outline = Color.Black * (.98f * opacity);
        Color fill = Color.Lerp(GoColor, Color.White, .70f * flash) * opacity;
        DrawOutlinedText(label, center, size * .5f, glyphScale, 2.45f * scale, fill, outline);
    }

    private static void DrawOutlinedText(string label, Vector2 center, Vector2 origin,
        float scale, float stroke, Color fill, Color outline)
    {
        var font = FontAssets.DeathText.Value;
        SpriteBatch batch = Main.spriteBatch;
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = (i * MathHelper.PiOver4).ToRotationVector2() * stroke;
            batch.DrawString(font, label, center + offset, outline, 0f, origin, scale, SpriteEffects.None, 0f);
        }
        batch.DrawString(font, label, center, fill, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private static Color Glow(Color color, float opacity) => new Color(color.R, color.G, color.B, (byte)0) * opacity;

    private static Vector2 RoundToArtGrid(Vector2 position) =>
        new Vector2(MathF.Round(position.X / ArtPixel), MathF.Round(position.Y / ArtPixel)) * ArtPixel;

    private static Vector2 SnapWorldArtPosition(Vector2 screenPosition) =>
        RoundToArtGrid(screenPosition + Main.screenPosition) - Main.screenPosition;

    private static void DrawArtPixel(Vector2 center, Color color) =>
        DrawArtCell(SnapWorldArtPosition(center - new Vector2(ArtPixel * .5f)), color);

    private static void DrawArtCell(Vector2 corner, Color color) =>
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, corner, new Rectangle(0, 0, 1, 1),
            color, 0f, Vector2.Zero, ArtPixel, SpriteEffects.None, 0f);

    private static void DrawSparkle(Vector2 center, Color color, float tips)
    {
        Vector2 corner = SnapWorldArtPosition(center - new Vector2(ArtPixel * .5f));
        Vector2 across = Vector2.UnitX * ArtPixel;
        Vector2 down = Vector2.UnitY * ArtPixel;
        DrawArtCell(corner, color);
        DrawArtCell(corner - down, color * .76f);
        DrawArtCell(corner + down, color * .64f);
        DrawArtCell(corner - across, color * .54f);
        DrawArtCell(corner + across, color * .54f);
        DrawArtCell(corner - down * 2f, color * (.48f * tips));
        DrawArtCell(corner + down * 2f, color * (.31f * tips));
        DrawArtCell(corner - across * 2f, color * (.23f * tips));
        DrawArtCell(corner + across * 2f, color * (.23f * tips));
    }

    private void DrawLine(Vector2 from, Vector2 to, float width, Color color)
    {
        lineGlow ??= CreateGlowMask(1, GlowSize);
        Vector2 travel = to - from;
        Main.spriteBatch.Draw(lineGlow, from, null, color, travel.ToRotation(), new Vector2(0f, GlowSize * .5f),
            new Vector2(travel.Length() + .5f, width / GlowSize), SpriteEffects.None, 0f);
    }

    private void DrawBloom(Vector2 center, float diameter, Color color)
    {
        circleGlow ??= CreateGlowMask(GlowSize, GlowSize);
        Main.spriteBatch.Draw(circleGlow, center, null, color, 0f, new Vector2(GlowSize * .5f),
            diameter / GlowSize, SpriteEffects.None, 0f);
    }

    private static Texture2D CreateGlowMask(int width, int height)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector2 position = new((x + .5f) / width * 2f - 1f, (y + .5f) / height * 2f - 1f);
            float fade = Math.Max(0f, 1f - position.LengthSquared());
            pixels[y * width + x] = Color.White * (fade * fade * fade);
        }

        Texture2D texture = new(Main.instance.GraphicsDevice, width, height);
        texture.SetData(pixels);
        return texture;
    }

    public void Dispose()
    {
        circleGlow?.Dispose();
        lineGlow?.Dispose();
        clash.Dispose();
    }
}
