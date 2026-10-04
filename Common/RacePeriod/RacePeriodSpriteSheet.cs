using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

#nullable enable

namespace PvPAdventure.Common.RacePeriod;

/// <summary>
/// A contiguous animation range inside a vertically stacked RacePeriod sprite sheet. Running
/// animations can advance from horizontal speed, matching Terraria's mount animation behaviour.
/// </summary>
public readonly record struct RacePeriodAnimationRange(
    int StartFrame,
    int FrameCount,
    float FrameDelay,
    bool ScaleWithHorizontalSpeed = false)
{
    public bool IsValidFor(int totalFrames) =>
        StartFrame >= 0 && FrameCount > 0 && FrameDelay > 0f &&
        StartFrame + FrameCount <= totalFrames;
}

/// <summary>
/// Optional mount-only artwork supplied by another mod. Body and foreground use the same vertical
/// frame layout; the foreground is drawn second. When this is absent, RacePeriod uses Mount.Draw.
/// </summary>
public readonly record struct RacePeriodSpriteSheet(
    Asset<Texture2D> BodyTexture,
    Asset<Texture2D>? ForegroundTexture,
    int TotalFrames,
    RacePeriodAnimationRange Standing,
    RacePeriodAnimationRange Running,
    RacePeriodAnimationRange Airborne,
    RacePeriodAnimationRange PoweredAirborne,
    Vector2 DrawOffset,
    int FrameCropBottom = 2,
    float Scale = 1f,
    int AnchorPlayerHeight = 42)
{
    public bool IsValid =>
        BodyTexture is not null && TotalFrames > 0 && FrameCropBottom >= 0 && Scale > 0f &&
        AnchorPlayerHeight > 0 &&
        Standing.IsValidFor(TotalFrames) && Running.IsValidFor(TotalFrames) &&
        Airborne.IsValidFor(TotalFrames) && PoweredAirborne.IsValidFor(TotalFrames);
}
