using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Keeps the Spirit Animal body and trail on the same breathing cycle.</summary>
internal static class RacePeriodPulse
{
    private const float PeriodSeconds = 3.6f;
    private const float PlayerPhaseOffset = 0.65f;

    internal static float GetWave(Player player)
    {
        float angle = Main.GlobalTimeWrappedHourly * (MathHelper.TwoPi / PeriodSeconds)
            + player.whoAmI * PlayerPhaseOffset;
        return 0.5f + 0.5f * MathF.Sin(angle);
    }
}
