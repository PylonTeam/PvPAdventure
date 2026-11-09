using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Captures the drawn animal at takeoff and again a few ticks into a powered jump.</summary>
internal sealed class RacePeriodJumpEchoes
{
    private const int EchoDelayTicks = 5;
    private const int EchoLifetimeTicks = 12;

    private sealed class Pose
    {
        internal readonly List<DrawData> Layers = new(2);
        internal ulong Tick;
        internal Color Lighting;
    }

    private readonly Pose[] poses = [new(), new()];
    private readonly List<DrawData> drawCopies = new(4);
    private int nextPose;
    private ulong nextCaptureTick;
    private int capturesRemaining;

    internal void Trigger(ulong tick)
    {
        nextCaptureTick = tick;
        capturesRemaining = poses.Length;
    }

    internal void Reset()
    {
        capturesRemaining = 0;
        nextPose = 0;
        foreach (Pose pose in poses)
            pose.Layers.Clear();
        drawCopies.Clear();
    }

    internal void Draw(ref PlayerDrawSet drawInfo, int start, ulong tick, Color team, Color lighting,
        RacePeriodTrailSettings settings)
    {
        if (!settings.IsJumpTrailEnabled || settings.PoweredEchoOpacity <= 0f)
        {
            Reset();
            return;
        }

        if (capturesRemaining > 0 && tick >= nextCaptureTick)
        {
            if (tick - nextCaptureTick >= EchoLifetimeTicks)
                capturesRemaining = 0;
            else
            {
                Capture(ref drawInfo, start, tick, lighting);
                capturesRemaining--;
                nextCaptureTick = tick + EchoDelayTicks;
            }
        }

        drawCopies.Clear();
        for (int i = 0; i < poses.Length; i++)
        {
            Pose pose = poses[(nextPose + i) % poses.Length];
            if (pose.Layers.Count == 0)
                continue;
            if (tick < pose.Tick || tick - pose.Tick >= EchoLifetimeTicks)
            {
                pose.Layers.Clear();
                continue;
            }

            float fade = 1f - (tick - pose.Tick) / (float)EchoLifetimeTicks;
            Color color = RacePeriodTrail.Tint(team, settings.PoweredEchoOpacity * fade * fade, settings,
                pose.Lighting);
            if (color.A == 0)
                continue;
            foreach (DrawData layer in pose.Layers)
            {
                DrawData copy = layer;
                copy.position -= Main.screenPosition;
                copy.color = color;
                drawCopies.Add(copy);
            }
        }

        drawInfo.DrawDataCache.InsertRange(start, drawCopies);
    }

    private void Capture(ref PlayerDrawSet drawInfo, int start, ulong tick, Color lighting)
    {
        Pose pose = poses[nextPose];
        pose.Layers.Clear();
        pose.Tick = tick;
        pose.Lighting = lighting;

        Vector2 pivot = drawInfo.Position + drawInfo.rotationOrigin;
        Matrix rotation = Matrix.CreateRotationZ(drawInfo.rotation);
        for (int i = start; i < drawInfo.DrawDataCache.Count; i++)
        {
            DrawData copy = drawInfo.DrawDataCache[i];
            if (copy.texture is null || copy.texture.IsDisposed || copy.useDestinationRectangle || copy.color.A == 0)
                continue;

            copy.position += Main.screenPosition;
            // Save the original world pose before Terraria applies the live player's rotation.
            if (!copy.ignorePlayerRotation)
            {
                copy.position = pivot + Vector2.Transform(copy.position - pivot, rotation);
                copy.rotation += drawInfo.rotation;
            }

            copy.texture = RacePeriodTrailTextures.Mask(copy.texture);
            copy.shader = 0;
            copy.ignorePlayerRotation = true;
            pose.Layers.Add(copy);
        }

        nextPose = (nextPose + 1) % poses.Length;
    }
}
