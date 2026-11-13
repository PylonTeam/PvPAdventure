using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>A world-space ribbon and fading poses that follow the animal's recent movement.</summary>
internal sealed class RacePeriodTrail
{
    private const int Capacity = 65;
    private const int PoseCapacity = 24;
    private const int PoweredBoostTicks = 18;
    private const float TeleportDistance = 192f;
    private const float AnchorJumpDistance = 16f;
    private const float MinimumSegmentLength = 0.01f;
    private static readonly Color NeutralMist = new(164, 209, 238);

    private readonly record struct WakePoint(Vector2 Position, ulong Tick, double Distance,
        float Speed, bool Airborne, bool StartsSpan);

    private sealed class WakePose
    {
        internal readonly List<DrawData> Layers = new(4);
        internal WakePoint Point;
        internal Color Lighting;
    }

    private readonly WakePoint[] points = new WakePoint[Capacity];
    private readonly List<DrawData> strips = new(Capacity);

    private WakePose[] poses;
    private int nextPose;
    private int poseCount;
    private ulong? poweredJumpTick;
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
        poweredJumpTick = null;
        ClearPoses();
        strips.Clear();
    }

    internal void TriggerPoweredJump(ulong tick) => poweredJumpTick = tick;

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
            ClearPoses();
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

    internal void Draw(ref PlayerDrawSet drawInfo, int start, ulong tick, Color team,
        RacePeriodTrailSettings settings, Color lighting, float breath)
    {
        if (!settings.IsRacePeriodTrailEnabled || count == 0 || start >= drawInfo.DrawDataCache.Count)
            return;

        CapturePose(ref drawInfo, start, tick, lighting);
        Color tint = drawInfo.drawPlayer.team > 0 ? Color.Lerp(team, NeutralMist, 0.16f) : NeutralMist;
        Append(drawInfo.DrawDataCache, start, tick, Main.screenPosition, tint, settings,
            RacePeriodTrailTextures.Streak, lighting, breath, drawInfo.drawPlayer.gravDir);
    }

    internal void Append(List<DrawData> drawData, int start, ulong tick, Vector2 screenPosition,
        Color team, RacePeriodTrailSettings settings, Texture2D streak, Color? lighting = null,
        float breath = 0.5f, float gravity = 1f)
    {
        if (!settings.IsRacePeriodTrailEnabled || count == 0 || streak == null || start >= drawData.Count)
            return;

        strips.Clear();
        int lifetime = Math.Clamp(settings.WakeLifetimeTicks, 1, 60);
        float maxLength = Bound(settings.WakeLengthPixels, 64f, 4f, 128f);
        float fullSpeed = Bound(settings.FullIntensitySpeed, 8f, 1f, 20f);
        float airStrength = Bound(settings.JumpIntensityMultiplier, 1f, 0f, 3f);
        breath = Math.Clamp(breath, 0f, 1f);
        float pulse = MathHelper.Lerp(0.53f, 1.15f, breath);
        float powered = PoweredBoost(tick, settings);
        float rise = -gravity * (4f + 1.8f * breath);
        float maxWidth = Bound(settings.WakeWidthPixels, 8.5f, 0.25f, 16f)
            * (MathHelper.Lerp(0.85f, 1.08f, breath) + powered * 0.5f);
        float opacity = (Bound(settings.WakeOpacity, 0.65f, 0f, 1f) + powered * 0.45f) * pulse;
        float echoOpacity = (Bound(settings.WakeEchoOpacity, 0.14f, 0f, 1f) + powered * 0.22f) * pulse;

        AddEchoes(tick, screenPosition, team, settings, lifetime, maxLength, fullSpeed, airStrength, rise, echoOpacity);
        int firstStrip = strips.Count;
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

                Vector2 startPosition = newer.Position + Vector2.UnitY * Rise(distance, maxLength, rise);
                Vector2 endPosition = older.Position + Vector2.UnitY * Rise(olderDistance, maxLength, rise);
                Vector2 segment = (endPosition - startPosition) * extent;
                float length = segment.Length();
                if (length >= MinimumSegmentLength)
                {
                    float midAge = MathHelper.Lerp(age, olderAge, extent * 0.5f);
                    float midDistance = MathHelper.Lerp(distance, olderDistance, extent * 0.5f);
                    float taper = (1f - midAge / lifetime) * (1f - midDistance / maxLength);
                    float speed = MathHelper.Lerp(newer.Speed, older.Speed, extent * 0.5f);
                    float strength = SpeedStrength(speed, fullSpeed);
                    float width = maxWidth * strength * MathF.Sqrt(taper);
                    Color color = Tint(team, opacity * strength * taper * (newer.Airborne ? airStrength : 1f),
                        settings, lighting);

                    if (color.A != 0)
                    {
                        Vector2 at = startPosition + segment * 0.5f - screenPosition;
                        float joinedLength = length + Math.Min(0.25f, length * 0.1f);
                        strips.Add(new DrawData(streak, at, null, color, MathF.Atan2(segment.Y, segment.X),
                            textureSize * 0.5f, new Vector2(joinedLength / textureSize.X, width / textureSize.Y),
                            SpriteEffects.None) { ignorePlayerRotation = true });
                    }
                }
            }

            newer = older;
        }

        // On overlapping paths the brighter, newer span stays above the fading wake.
        strips.Reverse(firstStrip, strips.Count - firstStrip);
        drawData.InsertRange(start, strips);
    }

    private void CapturePose(ref PlayerDrawSet drawInfo, int start, ulong tick, Color lighting)
    {
        // Only record a fresh movement sample, even if multiple world passes draw the player.
        if (tick < head.Tick || tick - head.Tick > 1 ||
            (poseCount > 0 && PoseAt(poseCount - 1).Point.Tick == head.Tick))
            return;

        poses ??= new WakePose[PoseCapacity];
        WakePose pose = poses[nextPose] ??= new WakePose();
        pose.Point = head;
        pose.Lighting = lighting;
        pose.Layers.Clear();

        Vector2 pivot = drawInfo.Position + drawInfo.rotationOrigin;
        Matrix rotation = Matrix.CreateRotationZ(drawInfo.rotation);
        for (int i = start; i < drawInfo.DrawDataCache.Count; i++)
        {
            DrawData copy = drawInfo.DrawDataCache[i];
            if (copy.texture is null || copy.useDestinationRectangle || copy.color.A == 0)
                continue;
            Texture2D mask = RacePeriodTrailTextures.Mask(copy.texture);
            if (mask is null)
                continue;

            copy.position += Main.screenPosition;
            if (!copy.ignorePlayerRotation)
            {
                copy.position = pivot + Vector2.Transform(copy.position - pivot, rotation);
                copy.rotation += drawInfo.rotation;
            }
            copy.texture = mask;
            copy.shader = 0;
            copy.ignorePlayerRotation = true;
            pose.Layers.Add(copy);
        }

        nextPose = (nextPose + 1) % PoseCapacity;
        poseCount = Math.Min(poseCount + 1, PoseCapacity);
    }

    private void AddEchoes(ulong tick, Vector2 screenPosition, Color team, RacePeriodTrailSettings settings,
        int lifetime, float maxLength, float fullSpeed, float airStrength, float rise, float opacity)
    {
        for (int i = 0; i < poseCount; i++)
        {
            WakePose pose = PoseAt(i);
            float age = Age(tick, pose.Point.Tick);
            float distance = (float)(head.Distance - pose.Point.Distance);
            if (age >= lifetime || distance <= 0f || distance >= maxLength)
                continue;

            float taper = (1f - age / lifetime) * (1f - distance / maxLength);
            float separation = Math.Clamp(distance / 12f, 0f, 1f);
            float alpha = opacity * taper * taper * separation * SpeedStrength(pose.Point.Speed, fullSpeed)
                * (pose.Point.Airborne ? airStrength : 1f);
            Vector2 offset = Vector2.UnitY * Rise(distance, maxLength, rise) - screenPosition;
            foreach (DrawData source in pose.Layers)
            {
                Color color = Tint(team, alpha * source.color.A / 255f, settings, pose.Lighting);
                if (color.A == 0)
                    continue;
                DrawData echo = source;
                echo.position += offset;
                echo.color = color;
                strips.Add(echo);
            }
        }
    }

    private WakePose PoseAt(int index) => poses[(nextPose - poseCount + index + PoseCapacity) % PoseCapacity];

    private void ClearPoses()
    {
        for (int i = 0; i < poseCount; i++)
            PoseAt(i).Layers.Clear();
        nextPose = poseCount = 0;
    }

    private float PoweredBoost(ulong tick, RacePeriodTrailSettings settings)
    {
        if (!settings.IsJumpTrailEnabled || poweredJumpTick is not { } start || tick < start || tick - start >= PoweredBoostTicks)
            return 0f;
        float fade = 1f - (tick - start) / (float)PoweredBoostTicks;
        return Bound(settings.PoweredEchoOpacity, 0.22f, 0f, 1f) * fade * fade;
    }

    private static float Rise(float distance, float length, float rise)
    {
        float progress = Math.Clamp(distance / length, 0f, 1f);
        return rise * progress * progress;
    }

    private static float SpeedStrength(float speed, float fullSpeed) =>
        MathHelper.Lerp(0.37f, 1f, Math.Clamp(speed / fullSpeed, 0f, 1f));

    internal static Color Tint(Color team, float alpha, RacePeriodTrailSettings settings, Color? lighting = null)
    {
        Vector3 hue = (settings.UseTeamColor ? team : settings.CustomColor).ToVector3();
        float gray = Vector3.Dot(hue, new Vector3(0.2126f, 0.7152f, 0.0722f));
        hue = Vector3.Clamp(Vector3.Lerp(new Vector3(gray), hue, Bound(settings.ColorSaturation, 1f, 0f, 2f)),
            Vector3.Zero, Vector3.One);
        if (settings.UseWorldLighting && lighting is { } ambient)
        {
            float minimum = Bound(settings.MinimumLighting, 0.1f, 0f, 1f);
            Vector3 light = ambient.ToVector3();
            float brightness = MathF.Max(light.X, MathF.Max(light.Y, light.Z));
            float darkness = 1f - MathHelper.SmoothStep(0f, 1f, brightness * 4f);
            minimum = Math.Min(1f, minimum * (1f + 0.44f * darkness));
            hue *= Vector3.Clamp(light, new Vector3(minimum), Vector3.One);
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
