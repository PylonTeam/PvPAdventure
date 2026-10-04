using Microsoft.Xna.Framework;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Live-editable visual tuning; the server config exposes only the trail toggle.</summary>
internal sealed class RacePeriodTrailSettings
{
    internal bool IsRacePeriodTrailEnabled;
    internal bool IsAfterimageTrailEnabled;
    internal bool IsWispTrailEnabled;
    internal bool IsSpeedStreakTrailEnabled;
    internal bool IsSoftGlowEnabled;
    internal bool IsTeamOutlineEnabled;
    internal bool IsBlackOutlineEnabled;
    internal bool NormalizeOutlineOpacity;
    internal float TeamOutlineWidth;
    internal float BlackOutlineWidth;
    internal float TeamOutlineOpacity;
    internal float BlackOutlineOpacity;
    internal Color BlackOutlineColor;
    internal float Intensity;
    internal float Opacity;
    internal float Glow;
    internal float GlowRadius;
    internal bool UseTeamColor;
    internal bool UseWorldLighting;
    internal float MinimumLighting;
    internal Color CustomColor;
    internal float ColorSaturation;
    internal float FadeExponent;
    internal int SampleCount;
    internal float LengthPixels;
    internal float StartDistancePixels;
    internal float MovementThreshold;
    internal float MovementFadeInMilliseconds;
    internal bool ScaleWithSpeed;
    internal float FullIntensitySpeed;
    internal bool IsJumpTrailEnabled;
    internal float JumpIntensityMultiplier;
    internal float EffectDensity;
    internal float MaxBrightness;
    internal bool IsCameraLagEnabled;
    internal float CameraVerticalResponse;
    internal float CameraMaxLagPixels;
    internal float CameraTeleportResetDistance;

    // EDIT Create() values, then apply C# Hot Reload; no full mod reload needed after initial build.
    // A supported attached debug session is required; saving the file alone does not apply edits.
    // Keep defaults here in the method body so the next update/draw reads the edited values.
    internal static RacePeriodTrailSettings Create() => new()
    {
        // Optional styles. Wisps and streaks can be enabled separately or combined with afterimages.
        IsRacePeriodTrailEnabled = true,
        IsAfterimageTrailEnabled = true,
        IsWispTrailEnabled = false,
        IsSpeedStreakTrailEnabled = false,
        IsSoftGlowEnabled = false,

        // Same diamond/cardinal silhouette passes as PvPFramework's player outline.
        // Widths are cardinal distances in screen pixels; black is outside the team edge.
        // Team edges use UseTeamColor/CustomColor below; the outer color is independent.
        IsTeamOutlineEnabled = true,
        IsBlackOutlineEnabled = true,
        // Share each outline's opacity between its overlapping copies for a gentler fade-in.
        // Disable to use PvPFramework's unadjusted opacity on every individual outline copy.
        NormalizeOutlineOpacity = true,
        TeamOutlineWidth = 2f,
        BlackOutlineWidth = 4f,
        TeamOutlineOpacity = 1f,
        BlackOutlineOpacity = 0.6f,
        BlackOutlineColor = Color.Black,

        // Per-image opacity. RGB stays premultiplied by alpha: these are solid echoes, not light.
        Intensity = 1f,
        Opacity = 0.36f,
        Glow = 0f,
        GlowRadius = 1.2f,
        UseTeamColor = true,
        UseWorldLighting = true,
        MinimumLighting = 0.10f,
        CustomColor = new Color(180, 140, 255),
        ColorSaturation = 1f,
        FadeExponent = 1.25f,
        MaxBrightness = 1f,

        // Distinct Chaos-style copies. Eight copies span the last 56 pixels of movement.
        SampleCount = 8,
        LengthPixels = 56f,
        StartDistancePixels = 6f,
        MovementThreshold = 0.3f,
        // Fade opacity in after starting or restarting. Set to zero for instant appearance.
        // Full strength is reached after this time even when moving slowly.
        MovementFadeInMilliseconds = 100f,

        // Movement response. These change visuals without changing race physics.
        ScaleWithSpeed = false,
        FullIntensitySpeed = 8f,
        IsJumpTrailEnabled = true,
        JumpIntensityMultiplier = 1f,
        EffectDensity = 1f,

        // Vertical camera lag is independent of the trail toggle and never changes player physics.
        IsCameraLagEnabled = true,
        CameraVerticalResponse = 12f,
        CameraMaxLagPixels = 64f,
        CameraTeleportResetDistance = 256f
    };
}
