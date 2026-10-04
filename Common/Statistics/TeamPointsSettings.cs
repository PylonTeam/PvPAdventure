using ErkySSC.Common.AdminTools;
using System;
using System.Collections.Generic;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Statistics;

/// <summary>Adventure's team scores, edited inside Framework's shared Game Manager.</summary>
internal static class TeamPointsSettings
{
    public static IReadOnlyList<AdminToolOption> Options
    {
        get
        {
            PointsManager points = ModContent.GetInstance<PointsManager>();
            List<AdminToolOption> options = [new("Team points", "")];
            foreach (Team team in Enum.GetValues<Team>())
            {
                if (team == Team.None) continue;
                Team selected = team;
                options.Add(AdminToolOption.IntegerInput($"{team} team", "Click the score, enter its new value, and press Enter",
                    () => points._points.GetValueOrDefault(selected), value => TeamPointsNetHandler.Set(selected, value)) with
                {
                    Icon = new AdminIcon(TextureAssets.Pvp[1], AdminIconFrame.Strip(6, (int)team))
                });
            }
            return options;
        }
    }
}
