using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Supplies the same eased pulse to the world sprite, outline, and trail.</summary>
internal static class RacePeriodPulse
{
    private const float FadeSeconds = 1.5f;
    private const float BrightHoldSeconds = 0.6f;
    private const float PeriodSeconds = 2f * FadeSeconds + BrightHoldSeconds;
    private const float PlayerPhaseOffsetSeconds = 0.31f;
    internal const float DimBodyOpacity = 0.25f;
    internal const float BrightBodyOpacity = 0.90f;

    internal static float GetOutlineOpacity(float wave) => MathHelper.Lerp(0.5f, 1f, wave);

    internal static float GetWave(Player player)
    {
        float time = (Main.GlobalTimeWrappedHourly + player.whoAmI * PlayerPhaseOffsetSeconds) % PeriodSeconds;
        float fade = MathHelper.Clamp(MathF.Min(time, PeriodSeconds - time) / FadeSeconds, 0f, 1f);
        // Zero slope and acceleration where either fade meets the hold or turns around.
        return fade * fade * fade * (fade * (fade * 6f - 15f) + 10f);
    }
}
