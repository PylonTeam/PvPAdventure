using PylonMatch.Common.Game;
using System;
using System.Collections.Generic;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Statistics;

/// <summary>Adventure's team scores, edited inside PylonMatch's shared Game Manager.</summary>
internal static class TeamPointsSettings
{
    public static IReadOnlyList<GameOption> Options
    {
        get
        {
            PointsManager points = ModContent.GetInstance<PointsManager>();
            List<GameOption> options = [new("Team Points", "")];
            foreach (Team team in Enum.GetValues<Team>())
            {
                if (team == Team.None) continue;
                Team selected = team;
                options.Add(GameOption.IntegerInput($"{team} team", "Click the score, enter its new value, and press Enter",
                    () => points._points.GetValueOrDefault(selected), value => TeamPointsNetHandler.Set(selected, value)) with
                {
                    Icon = new GameIcon(TextureAssets.Pvp[1], GameIconFrame.Strip(6, (int)team))
                });
            }
            return options;
        }
    }
}
