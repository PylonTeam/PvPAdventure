using Microsoft.Xna.Framework;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Live-editable visual tuning; the server config exposes only the trail toggle.</summary>
internal sealed class RacePeriodTrailSettings
{
    internal bool IsRacePeriodTrailEnabled;
    internal bool UseTeamColor;
    internal bool UseWorldLighting;
    internal float MinimumLighting;
    internal Color CustomColor;
    internal float ColorSaturation;
    internal float MovementThreshold;
    internal float FullIntensitySpeed;
    internal bool IsJumpTrailEnabled;
    internal float JumpIntensityMultiplier;
    internal float WakeSpacingPixels;
    internal float WakeLengthPixels;
    internal int WakeLifetimeTicks;
    internal float WakeWidthPixels;
    internal float WakeOpacity;
    internal float WakeEchoOpacity;
    internal float PoweredEchoOpacity;
    internal bool IsCameraLagEnabled;
    internal float CameraVerticalResponse;
    internal float CameraMaxLagPixels;
    internal float CameraTeleportResetDistance;

    // EDIT Create() values, then apply C# Hot Reload; no full mod reload needed after initial build.
    // A supported attached debug session is required; saving the file alone does not apply edits.
    // Keep defaults here in the method body so the next update/draw reads the edited values.
    internal static RacePeriodTrailSettings Create() => new()
    {
        IsRacePeriodTrailEnabled = true,
        UseTeamColor = true,
        UseWorldLighting = true,
        MinimumLighting = 0.25f,
        CustomColor = new Color(180, 140, 255),
        ColorSaturation = 1f,

        // The wake is spaced in world pixels and fades over 200 ms at 60 updates per second.
        WakeSpacingPixels = 4f,
        WakeLengthPixels = 64f,
        WakeLifetimeTicks = 12,
        WakeWidthPixels = 8.5f,
        WakeOpacity = 0.65f,
        WakeEchoOpacity = 0.14f,
        PoweredEchoOpacity = 0.22f,
        MovementThreshold = 0.3f,
        FullIntensitySpeed = 8f,
        IsJumpTrailEnabled = true,
        JumpIntensityMultiplier = 1f,

        // Vertical camera lag is independent of the trail toggle and never changes player physics.
        IsCameraLagEnabled = true,
        CameraVerticalResponse = 12f,
        CameraMaxLagPixels = 64f,
        CameraTeleportResetDistance = 256f
    };
}
