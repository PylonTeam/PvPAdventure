using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.DataStructures;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Distinct team-colored animal afterimages, fading along recent movement without emitting light.</summary>
internal sealed class RacePeriodTrail
{
    internal const int CacheLength = 160;
    private readonly Vector2[] positions = new Vector2[CacheLength];
    private int count;
    private Vector2 movement;
    private bool inAir;
    private float movingSeconds;

    internal void Reset()
    {
        count = 0;
        movement = Vector2.Zero;
        inAir = false;
        movingSeconds = 0f;
    }

    internal void Record(Vector2 position, bool airborne = false,
        RacePeriodTrailSettings settings = null, float elapsedSeconds = 1f / 60f)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
        {
            Reset();
            return;
        }
        movement = count > 0 ? position - positions[0] : Vector2.Zero;
        if (movement.LengthSquared() > 192f * 192f)
            Reset();
        inAir = airborne;
        for (int i = Math.Min(count, CacheLength - 1); i > 0; i--)
            positions[i] = positions[i - 1];
        positions[0] = position;
        count = Math.Min(count + 1, CacheLength);
        settings ??= RacePeriodTrailSettings.Create();
        if (IsMoving(settings))
            movingSeconds = Math.Min(10f, movingSeconds +
                Math.Clamp(float.IsFinite(elapsedSeconds) ? elapsedSeconds : 0f, 0f, 0.25f));
        else
            movingSeconds = 0f;
    }

    internal bool IsMoving(RacePeriodTrailSettings settings) =>
        settings.IsRacePeriodTrailEnabled && count > 1 &&
        movement.LengthSquared() >= MathF.Pow(Math.Clamp(settings.MovementThreshold, 0.05f, 6f), 2f) &&
        (!inAir || settings.IsJumpTrailEnabled);

    internal float Activation(RacePeriodTrailSettings settings)
    {
        if (!IsMoving(settings))
            return 0f;
        float milliseconds = Math.Clamp(float.IsFinite(settings.MovementFadeInMilliseconds)
            ? settings.MovementFadeInMilliseconds : 100f, 0f, 2000f);
        if (milliseconds == 0f)
            return 1f;
        float progress = Math.Clamp(movingSeconds * 1000f / milliseconds, 0f, 1f);
        return progress * progress * (3f - 2f * progress);
    }

    private static float Fade(int sample, RacePeriodTrailSettings settings) =>
        MathF.Pow(Math.Clamp(1f - sample / (float)Math.Clamp(settings.SampleCount, 2, 48), 0f, 1f),
            Math.Clamp(settings.FadeExponent, 0.25f, 4f));

    internal static Color ColorAt(Color team, int sample, RacePeriodTrailSettings settings,
        float speed, bool airborne, Color? lighting = null, float activation = 1f)
    {
        float strength = Math.Clamp(settings.Intensity, 0f, 3f);
        if (settings.ScaleWithSpeed)
            strength *= Math.Clamp(speed / Math.Clamp(settings.FullIntensitySpeed, 1f, 20f), 0f, 1f);
        if (airborne)
            strength *= Math.Clamp(settings.JumpIntensityMultiplier, 0f, 3f);
        float fade = Fade(sample, settings);
        Vector3 hue = (settings.UseTeamColor ? team : settings.CustomColor).ToVector3();
        float gray = Vector3.Dot(hue, new Vector3(0.2126f, 0.7152f, 0.0722f));
        hue = Vector3.Clamp(Vector3.Lerp(new Vector3(gray), hue,
            Math.Clamp(settings.ColorSaturation, 0f, 2f)), Vector3.Zero, Vector3.One);
        if (settings.UseWorldLighting && lighting is { } ambient)
            hue *= Vector3.Clamp(ambient.ToVector3(), new Vector3(Math.Clamp(settings.MinimumLighting, 0f, 1f)), Vector3.One);
        float opacity = Math.Clamp(settings.Opacity, 0f, 1f);
        float alpha = Math.Clamp(opacity * strength * fade, 0f, 1f);
        // Each echo has its own opacity like Chaos Elemental. Never divide it by sample count,
        // and never let RGB exceed alpha: daylight stays readable without additive night glow.
        float brightness = Math.Min(alpha, Math.Clamp(settings.MaxBrightness, 0f, 1f));
        float startFade = Math.Clamp(float.IsFinite(activation) ? activation : 0f, 0f, 1f);
        return new Color(new Vector4(hue * brightness * startFade, alpha * startFade));
    }

    internal List<Vector2> SampleOffsets(Vector2 currentPosition, RacePeriodTrailSettings settings)
    {
        List<Vector2> samples = new();
        int sampleCount = Math.Clamp(settings.SampleCount, 2, 48);
        float start = Math.Clamp(settings.StartDistancePixels, 0.25f, 12f);
        float length = Math.Clamp(settings.LengthPixels, 4f, 128f);
        Vector2 previous = currentPosition;
        int history = 0;
        float travelled = 0f;
        for (int sample = 0; sample < sampleCount; sample++)
        {
            float distance = start + sample * length / (sampleCount - 1f);
            while (history < count)
            {
                Vector2 next = positions[history];
                float segmentLength = Vector2.Distance(previous, next);
                if (segmentLength > 0f && travelled + segmentLength >= distance)
                {
                    samples.Add(Vector2.Lerp(previous, next, (distance - travelled) / segmentLength) - currentPosition);
                    break;
                }
                travelled += segmentLength;
                previous = next;
                history++;
            }
            if (history >= count)
                break;
        }
        return samples;
    }

    internal void Append(List<DrawData> drawData, int start, Vector2 currentPosition, Color team,
        RacePeriodTrailSettings settings, Func<Texture2D, Texture2D> mask,
        Texture2D wisp = null, Texture2D streak = null, float time = 0f,
        Func<DrawData, float, DrawData> softGlow = null, Color? lighting = null)
    {
        int end = drawData.Count;
        if (!IsMoving(settings) || start >= end ||
            (!settings.IsAfterimageTrailEnabled && !settings.IsWispTrailEnabled && !settings.IsSpeedStreakTrailEnabled))
            return;

        TrailBounds body = RacePeriodTrailGeometry.Bounds(drawData[start]);
        for (int source = start + 1; source < end; source++)
            body = body.Union(RacePeriodTrailGeometry.Bounds(drawData[source]));
        Vector2 center = new((body.Left + body.Right) / 2f, (body.Top + body.Bottom) / 2f);
        float speed = movement.Length();
        float activation = Activation(settings);
        Vector2 direction = movement / speed;
        Vector2 side = new(-direction.Y, direction.X);
        float density = Math.Clamp(settings.EffectDensity, 0f, 3f);
        List<Vector2> offsets = SampleOffsets(currentPosition, settings);
        List<DrawData> trail = new();
        List<DrawData> silhouettes = new();
        if (settings.IsAfterimageTrailEnabled)
            for (int source = start; source < end; source++)
            {
                DrawData copy = drawData[source];
                if (settings.IsSoftGlowEnabled && settings.Glow > 0f && softGlow != null)
                    copy = softGlow(copy, settings.GlowRadius);
                else
                    copy.texture = mask(copy.texture);
                copy.shader = 0;
                silhouettes.Add(copy);
            }

        // Oldest silhouettes first: the trail blends towards the unchanged live animal last.
        for (int sample = offsets.Count - 1; sample >= 0; sample--)
        {
            Vector2 offset = offsets[sample];
            Color color = ColorAt(team, sample, settings, speed, inAir, lighting, activation);
            if (color == Color.Transparent)
                continue;
            // Whole-frame outline passes stay in the trail cache, beneath the live animal.
            // Their neutral textures remain excluded from PvPFramework's later outline pass.
            RacePeriodTrailOutlines.Append(trail, silhouettes, offset, color, settings);
            for (int detail = 0; detail < (int)MathF.Ceiling(density); detail++)
            {
                if (detail >= (int)density && (sample * 0.618034f % 1f) >= density % 1f)
                    continue;
                if (settings.IsWispTrailEnabled && wisp != null)
                {
                    float wave = MathF.Sin(time * 3f + sample * 1.7f + detail * 2.1f);
                    Vector2 at = center + offset + side * wave * (4f + detail * 3f);
                    float size = 6f + 2f * MathF.Cos(time * 2f + sample);
                    trail.Add(new DrawData(wisp, at, null, color, 0f,
                        new Vector2(wisp.Width, wisp.Height) / 2f, size / wisp.Width, SpriteEffects.None));
                }
                if (settings.IsSpeedStreakTrailEnabled && streak != null && sample + 1 < offsets.Count)
                {
                    Vector2 tail = offsets[sample + 1];
                    Vector2 segment = tail - offset;
                    float length = segment.Length();
                    if (length < 0.1f)
                        continue;
                    Vector2 at = center + (offset + tail) / 2f + side * (detail - (density - 1f) / 2f) * 6f;
                    trail.Add(new DrawData(streak, at, null, color, MathF.Atan2(segment.Y, segment.X),
                        new Vector2(0.5f), new Vector2(length, 1.2f), SpriteEffects.None));
                }
            }
        }
        drawData.InsertRange(start, trail);
    }
}
