using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
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
    private const int GateFrames = 8;
    private const int GlowSize = 48;
    private static readonly Color Violet = new(130, 73, 224);
    private static readonly Color Lilac = new(222, 183, 255);
    private static readonly Color Ivory = new(247, 229, 255);
    private static readonly Color GoColor = new(229, 207, 255);
    private static readonly Color ImpactColor = new(162, 87, 255);
    private static readonly Color DigitOutline = new(20, 12, 32);
    private static readonly Color LeftFallback = new(151, 88, 242);

    private readonly CountdownClash clash = new();
    private Effect gateEffect;
    private Texture2D circleGlow;
    private Texture2D lineGlow;

    internal void Draw(in CountdownPortalFrame frame, string goLabel)
    {
        bool releasing = frame.ReleaseAge >= 0;
        float ticks = Math.Max(0, frame.ReleaseAge);
        float recede = MathHelper.SmoothStep(0f, 1f, ticks / 20f);
        float opacity = frame.Opacity * (1f - MathHelper.SmoothStep(0f, 1f, (ticks - 2f) / 16f));
        CountdownPortalFrame gate = frame with { Scale = frame.Scale * MathHelper.Lerp(1f, .23f, recede) };

        if (opacity > .001f)
        {
            float handoff = releasing ? 1f : frame.Second == 1
                ? MathHelper.SmoothStep(0f, 1f, (frame.Phase - .78f) / .22f)
                : 0f;
            float sway = frame.Age * (MathHelper.TwoPi * .43f);
            float opening = MathHelper.Lerp(.79f, 1f, frame.Opacity);
            Vector2 stretch = new Vector2(MathHelper.Lerp(.74f, 1f, MathF.Abs(MathF.Cos(sway))), 1f)
                * (1.43f * gate.Scale * opening);

            DrawOrbit(gate, opacity, front: false);
            DrawHalo(gate.Center, gate.Scale, opacity * (.14f + .12f * StarLight(frame.Phase)));
            DrawTeamGate(gate, stretch, opacity, handoff);
            DrawDigit(frame, ticks);
            DrawOrbit(gate, opacity, front: true);
        }

        if (!releasing) return;

        if (ticks is > 0f and < 19f)
            DrawHandoff(frame, ticks);
        DrawRelease(frame, ticks, goLabel);
    }

    internal static Vector2 OrbitPoint(Vector2 center, float scale, float phase) =>
        OrbitPointAtAngle(OrbitCenter(center, scale), OrbitRadius(scale), OrbitAngle(phase));

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

    private void DrawOrbit(in CountdownPortalFrame frame, float opacity, bool front)
    {
        const int segments = 24;
        const float step = MathHelper.Pi / segments;
        float start = front ? 0f : MathHelper.Pi;
        float glint = OrbitAngle(frame.Phase);
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
                Glow(Violet, opacity * depth * (.12f + .52f * streak)));
            DrawLine(last, next, (1.45f + 1.2f * streak) * frame.Scale,
                Glow(Lilac, opacity * depth * (.13f + .65f * streak)));
            last = next;
        }

        for (int i = 1; i <= 4; i++)
        {
            float angle = glint - .16f * i;
            float depth = MathF.Sin(angle);
            if ((depth >= 0f) != front) continue;

            DrawPixel(OrbitPointAtAngle(center, radius, angle), 1.4f * frame.Scale,
                Glow(Lilac, opacity * StarVisibility(depth) * (.68f - .12f * i)));
        }

        float starDepth = MathF.Sin(glint);
        if ((starDepth >= 0f) != front) return;

        float starOpacity = opacity * StarVisibility(starDepth);
        float crest = StarLight(frame.Phase);
        Vector2 head = OrbitPointAtAngle(center, radius, glint);
        DrawBloom(head, (13f + 10f * crest) * frame.Scale, Glow(Violet, starOpacity * (.52f + .37f * crest)));
        DrawSparkle(head, (8f + 6f * crest) * frame.Scale, Glow(Ivory, starOpacity), .08f);
        DrawPixel(head, 2.1f * frame.Scale, Glow(Ivory, starOpacity * .94f));
    }

    private static void DrawDigit(in CountdownPortalFrame frame, float ticks)
    {
        float fade = frame.Second > 0 ? 1f : 1f - MathHelper.SmoothStep(0f, 1f, ticks / 6f);
        if (fade <= 0f) return;

        float shine = frame.Second > 0 ? 1f - MathHelper.SmoothStep(0f, 1f, frame.Phase * 4.1f) : 0f;
        var font = FontAssets.DeathText.Value;
        string digit = Math.Max(frame.Second, 1).ToString();
        Vector2 center = frame.Center + Vector2.UnitY * (5f * frame.Scale + 2f);
        Vector2 size = font.MeasureString(digit);
        float scale = Math.Min(24f * frame.Scale / font.MeasureString("8").X, 32f * frame.Scale / size.X)
            * (1f + .05f * shine);
        float opacity = frame.Opacity * fade;
        Color ink = Color.Lerp(Ivory, Color.White, .85f * shine);
        Utils.DrawBorderStringFourWay(Main.spriteBatch, font, digit, center.X, center.Y, ink * opacity,
            DigitOutline * (.86f * opacity), size * .5f, scale);
    }

    private static void DrawHalo(Vector2 center, float scale, float opacity)
    {
        Texture2D halo = TextureAssets.Extra[ExtrasID.PortalGateHalo].Value;
        Vector2 origin = new(halo.Width * .5f, halo.Height * .55f);
        Main.spriteBatch.Draw(halo, center, null, Glow(Violet, opacity), 0f, origin,
            new Vector2(.60f, .85f) * scale, SpriteEffects.None, 0f);
    }

    private void DrawTeamGate(in CountdownPortalFrame frame, Vector2 stretch, float opacity, float handoff)
    {
        gateEffect ??= ModContent.Request<Effect>("PvPAdventure/Assets/Countdown/ReturnGateTeamColor",
            AssetRequestMode.ImmediateLoad).Value;
        Texture2D texture = TextureAssets.Extra[ExtrasID.PotionOfReturnGateOut].Value;
        int height = texture.Height / GateFrames;
        int index = (int)(frame.Age * 12f) % GateFrames;
        Rectangle source = new(0, height * (GateFrames - 1 - index), texture.Width, height);

        EffectParameterCollection parameters = gateEffect.Parameters;
        parameters["uFrame"].SetValue(new Vector4(0f, source.Y / (float)texture.Height, 1f, height / (float)texture.Height));
        parameters["uFrameSize"].SetValue(source.Size());
        parameters["uTime"].SetValue(frame.Age);
        parameters["uHandoff"].SetValue(handoff);
        parameters["uShine"].SetValue(StarLight(frame.Phase));
        parameters["uLeftHilt"].SetValue(BladeColor(frame.Teams.Left, LeftFallback, 0f).ToVector3());
        parameters["uLeftMiddle"].SetValue(BladeColor(frame.Teams.Left, LeftFallback, .5f).ToVector3());
        parameters["uLeftTip"].SetValue(BladeColor(frame.Teams.Left, LeftFallback, 1f).ToVector3());
        parameters["uRightHilt"].SetValue(BladeColor(frame.Teams.Right, GoColor, 0f).ToVector3());
        parameters["uRightMiddle"].SetValue(BladeColor(frame.Teams.Right, GoColor, .5f).ToVector3());
        parameters["uRightTip"].SetValue(BladeColor(frame.Teams.Right, GoColor, 1f).ToVector3());

        SpriteBatch batch = Main.spriteBatch;
        Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
        Vector2 viewportScale = new(zoom.M11, zoom.M22);
        Vector2 size = source.Size() * stretch * viewportScale;
        Vector2 center = Vector2.Transform(frame.Center, zoom);
        // Snap both screen-space edges before applying the atlas shader.
        Vector2 topLeft = RoundPoint(center - size * .5f);
        Vector2 bottomRight = RoundPoint(center + size * .5f);
        Vector2 position = Vector2.Transform(topLeft, Matrix.Invert(zoom));
        Vector2 drawScale = (bottomRight - topLeft) / viewportScale / source.Size();

        batch.End();
        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, gateEffect, zoom);
        try
        {
            batch.Draw(texture, position, source, Color.White * (.93f * opacity),
                0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
        }
        finally
        {
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, zoom);
        }
    }

    private static Color BladeColor(CountdownBladeTeams blade, Color fallback, float distance) =>
        blade.Hilt == 0 ? fallback : blade.ColorAlongBlade(distance);

    private static Vector2 RoundPoint(Vector2 point) => new(MathF.Round(point.X), MathF.Round(point.Y));

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
        DrawHandoffSpark(center - Vector2.UnitX * separation, frame.Scale, left, fade, -.32f);
        DrawHandoffSpark(center + Vector2.UnitX * separation, frame.Scale, right, fade, .32f);
    }

    private void DrawHandoffSpark(Vector2 center, float scale, Color color, float fade, float tilt)
    {
        DrawBloom(center, 21f * scale, Glow(color, .54f * fade));
        DrawSparkle(center, 9f * scale, Glow(Color.Lerp(color, Ivory, .43f), .67f * fade), tilt);
    }

    private void DrawRelease(in CountdownPortalFrame frame, float ticks, string label)
    {
        float exit = MathHelper.SmoothStep(0f, 1f, (ticks - FadeTick) / (ReleaseDuration - FadeTick));
        float opacity = 1f - exit;
        float scale = frame.Scale * MathHelper.Lerp(1f, .27f, exit);
        float flash = ticks >= ImpactTick
            ? 1f - MathHelper.SmoothStep(0f, 1f, (ticks - ImpactTick) / (.16f * ReleaseDuration))
            : 0f;
        Vector2 hit = frame.Center - Vector2.UnitY * (20f * scale);

        if (flash > 0f)
        {
            DrawBloom(hit, 94f * scale, Glow(ImpactColor, .58f * flash));
            DrawBloom(hit, 49f * scale, Glow(GoColor, .29f * flash));
        }

        DrawSwords(frame, scale, ticks, opacity);

        if (flash > 0f)
            DrawSparkle(hit, 32f * scale, Glow(Color.Cornsilk, .96f * flash), .40f);

        float labelOpacity = opacity * MathHelper.SmoothStep(0f, 1f, (ticks - 7f) / 11f);
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
        float settle = SwordSettle(ticks);
        float radius = (MathHelper.Lerp(33f, 74f, approach) - 5f * settle) * scale;
        float tilt = MathHelper.Lerp(-.32f, -.133f, approach) * (1f - settle);
        Vector2 center = frame.Center + Vector2.UnitY * (MathHelper.Lerp(4f, 0f, approach) * scale);
        clash.DrawPair(center, radius, SwordOffset(ticks) * scale, tilt, opacity, frame.Teams);
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
        float pulseScale = scale * (1f + .09f * flash);
        float glyphScale = 63f * pulseScale / Math.Max(size.X, 1f);
        Color outline = Color.Black * (.98f * opacity);
        Color fill = Color.Lerp(GoColor, Color.White, .70f * flash) * opacity;
        SpriteBatch batch = Main.spriteBatch;
        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = (i * MathHelper.PiOver4).ToRotationVector2() * (2.45f * pulseScale);
            batch.DrawString(font, label, center + offset, outline, 0f, size * .5f, glyphScale, SpriteEffects.None, 0f);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector2 offset = (i * MathHelper.PiOver2).ToRotationVector2() * (.66f * pulseScale);
            batch.DrawString(font, label, center + offset, fill, 0f, size * .5f, glyphScale, SpriteEffects.None, 0f);
        }
        batch.DrawString(font, label, center, fill, 0f, size * .5f, glyphScale, SpriteEffects.None, 0f);
    }

    private static Color Glow(Color color, float opacity) => new Color(color.R, color.G, color.B, (byte)0) * opacity;

    private static void DrawPixel(Vector2 center, float size, Color color) =>
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, center, new Rectangle(0, 0, 1, 1), color,
            0f, new Vector2(.5f), size, SpriteEffects.None, 0f);

    private static void DrawSparkle(Vector2 center, float size, Color color, float rotation)
    {
        Texture2D ray = TextureAssets.Extra[ExtrasID.SharpTears].Value;
        Vector2 origin = ray.Size() * .5f;
        Vector2 stretch = new Vector2(.45f, 1f) * (size / (ray.Height * .55f));
        Main.spriteBatch.Draw(ray, center, null, color, rotation, origin, stretch, SpriteEffects.None, 0f);
        Main.spriteBatch.Draw(ray, center, null, color * .76f, rotation + MathHelper.PiOver2, origin, stretch * .79f,
            SpriteEffects.None, 0f);
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
