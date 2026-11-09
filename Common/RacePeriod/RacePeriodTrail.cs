using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.DataStructures;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>A world-space ribbon that tapers as its samples age.</summary>
internal sealed class RacePeriodTrail
{
    private const int Capacity = 65;
    private const float TeleportDistance = 192f;
    private const float AnchorJumpDistance = 16f;
    private const float MinimumSegmentLength = 0.01f;

    private readonly record struct WakePoint(Vector2 Position, ulong Tick, double Distance,
        float Speed, bool Airborne, bool StartsSpan);

    private readonly WakePoint[] points = new WakePoint[Capacity];
    private readonly List<DrawData> strips = new(Capacity);

    private int first;
    private int count;
    private bool hasPreviousPosition;
    private ulong previousTick;
    private Vector2 previousPosition;
    private Vector2 previousDirection;
    private WakePoint head;
    private float distanceSinceSample;

    internal void Reset()
    {
        first = 0;
        count = 0;
        hasPreviousPosition = false;
        previousDirection = Vector2.Zero;
        head = default;
        distanceSinceSample = 0f;
        strips.Clear();
    }

    /// <returns>Whether a position discontinuity cleared the visual history.</returns>
    internal bool Record(ulong tick, Vector2 playerPosition, Vector2 worldAnchor, bool airborne,
        RacePeriodTrailSettings settings)
    {
        if (!settings.IsRacePeriodTrailEnabled)
        {
            Reset();
            return false;
        }
        if (!Finite(playerPosition) || !Finite(worldAnchor))
        {
            Reset();
            return true;
        }
        if (hasPreviousPosition && tick == previousTick)
            return false;

        Vector2 movement = hasPreviousPosition ? playerPosition - previousPosition : Vector2.Zero;
        float speed = movement.Length();
        bool discontinuity = hasPreviousPosition && (tick < previousTick || speed > TeleportDistance);
        if (discontinuity)
            Reset();

        bool hasMotionHistory = hasPreviousPosition;
        hasPreviousPosition = true;
        previousTick = tick;
        previousPosition = playerPosition;
        if (!hasMotionHistory || speed < Bound(settings.MovementThreshold, 0.3f, 0.05f, 6f) ||
            (airborne && !settings.IsJumpTrailEnabled))
        {
            CommitHead();
            return discontinuity;
        }

        Vector2 direction = movement / speed;
        Vector2 step = worldAnchor - head.Position;
        bool split = count == 0 || tick - head.Tick > 3 ||
            Vector2.Dot(direction, previousDirection) < -0.15f ||
            (step - movement).LengthSquared() > AnchorJumpDistance * AnchorJumpDistance;
        previousDirection = direction;

        if (split)
        {
            CommitHead();
            head = new WakePoint(worldAnchor, tick, head.Distance, speed, airborne, StartsSpan: true);
            Add(head);
            return discontinuity;
        }

        float stepLength = step.Length();
        if (stepLength < MinimumSegmentLength)
            return discontinuity;

        float spacing = Bound(settings.WakeSpacingPixels, 4f, 2f, 12f);
        for (float travelled = Math.Max(0f, spacing - distanceSinceSample);
             travelled <= stepLength; travelled += spacing)
        {
            Add(new WakePoint(Vector2.Lerp(head.Position, worldAnchor, travelled / stepLength),
                tick, head.Distance + travelled, speed, airborne, StartsSpan: false));
        }

        head = new WakePoint(worldAnchor, tick, head.Distance + stepLength, speed, airborne, StartsSpan: false);
        distanceSinceSample = (float)(head.Distance - At(count - 1).Distance);
        return discontinuity;
    }

    internal void Append(List<DrawData> drawData, int start, ulong tick, Vector2 screenPosition,
        Color team, RacePeriodTrailSettings settings, Texture2D streak, Color? lighting = null)
    {
        if (!settings.IsRacePeriodTrailEnabled || count == 0 || streak == null || start >= drawData.Count)
            return;

        strips.Clear();
        int lifetime = Math.Clamp(settings.WakeLifetimeTicks, 1, 60);
        float maxLength = Bound(settings.WakeLengthPixels, 64f, 4f, 128f);
        float maxWidth = Bound(settings.WakeWidthPixels, 6.5f, 0.25f, 16f);
        float opacity = Bound(settings.WakeOpacity, 0.71f, 0f, 1f);
        float fullSpeed = Bound(settings.FullIntensitySpeed, 8f, 1f, 20f);
        float airStrength = Bound(settings.JumpIntensityMultiplier, 1f, 0f, 3f);
        Vector2 textureSize = new(streak.Width, streak.Height);
        WakePoint newer = head;
        int index = distanceSinceSample >= MinimumSegmentLength ? count - 1 : count - 2;

        while (index >= 0)
        {
            float age = Age(tick, newer.Tick);
            float distance = (float)(head.Distance - newer.Distance);
            if (age >= lifetime || distance >= maxLength)
                break;

            WakePoint older = At(index--);
            if (!newer.StartsSpan)
            {
                float olderAge = Age(tick, older.Tick);
                float olderDistance = (float)(head.Distance - older.Distance);
                float extent = 1f;
                if (olderAge > lifetime)
                    extent = (lifetime - age) / (olderAge - age);
                if (olderDistance > maxLength)
                    extent = Math.Min(extent, (maxLength - distance) / (olderDistance - distance));

                Vector2 segment = (older.Position - newer.Position) * extent;
                float length = segment.Length();
                if (length >= MinimumSegmentLength)
                {
                    float midAge = MathHelper.Lerp(age, olderAge, extent * 0.5f);
                    float midDistance = MathHelper.Lerp(distance, olderDistance, extent * 0.5f);
                    float taper = (1f - midAge / lifetime) * (1f - midDistance / maxLength);
                    float speed = MathHelper.Lerp(newer.Speed, older.Speed, extent * 0.5f);
                    float strength = MathHelper.Lerp(0.37f, 1f, Math.Clamp(speed / fullSpeed, 0f, 1f));
                    float width = maxWidth * strength * MathF.Sqrt(taper);
                    Color color = Tint(team, opacity * strength * taper * (newer.Airborne ? airStrength : 1f),
                        settings, lighting);

                    if (color.A != 0)
                    {
                        Vector2 at = newer.Position + segment * 0.5f - screenPosition;
                        strips.Add(new DrawData(streak, at, null, color, MathF.Atan2(segment.Y, segment.X),
                            textureSize * 0.5f, new Vector2(length / textureSize.X, width / textureSize.Y),
                            SpriteEffects.None) { ignorePlayerRotation = true });
                    }
                }
            }

            newer = older;
        }

        // On overlapping paths the brighter, newer span stays above the fading wake.
        strips.Reverse();
        drawData.InsertRange(start, strips);
    }

    internal static Color Tint(Color team, float alpha, RacePeriodTrailSettings settings, Color? lighting = null)
    {
        Vector3 hue = (settings.UseTeamColor ? team : settings.CustomColor).ToVector3();
        float gray = Vector3.Dot(hue, new Vector3(0.2126f, 0.7152f, 0.0722f));
        hue = Vector3.Clamp(Vector3.Lerp(new Vector3(gray), hue, Bound(settings.ColorSaturation, 1f, 0f, 2f)),
            Vector3.Zero, Vector3.One);
        if (settings.UseWorldLighting && lighting is { } ambient)
        {
            float minimum = Bound(settings.MinimumLighting, 0.1f, 0f, 1f);
            hue *= Vector3.Clamp(ambient.ToVector3(), new Vector3(minimum), Vector3.One);
        }

        alpha = Bound(alpha, 0f, 0f, 1f);
        return new Color(new Vector4(hue * alpha, alpha));
    }

    private void CommitHead()
    {
        if (distanceSinceSample >= MinimumSegmentLength)
            Add(head);
        distanceSinceSample = 0f;
    }

    private void Add(WakePoint point)
    {
        if (count == Capacity)
        {
            points[first] = point;
            first = (first + 1) % Capacity;
        }
        else
        {
            points[(first + count) % Capacity] = point;
            count++;
        }
    }

    private WakePoint At(int index) => points[(first + index) % Capacity];

    private static float Age(ulong tick, ulong emitted) => tick >= emitted ? tick - emitted : 0f;

    private static bool Finite(Vector2 point) => float.IsFinite(point.X) && float.IsFinite(point.Y);

    private static float Bound(float value, float fallback, float min, float max) =>
        Math.Clamp(float.IsFinite(value) ? value : fallback, min, max);
}
