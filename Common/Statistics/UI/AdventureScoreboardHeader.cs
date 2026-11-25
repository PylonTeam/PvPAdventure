using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Core.Utilities;
using PylonMatch.Common.Scoreboard;
using System;
using Terraria;
using Terraria.Enums;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Statistics.UI;

[Autoload(Side = ModSide.Client)]
public class AdventureScoreboardHeader : ModSystem
{
    public override void Load() => ScoreboardTeamHeader.DrawExtra = DrawTeamExtra;
    public override void Unload() => ScoreboardTeamHeader.DrawExtra = null;

    private static void DrawTeamExtra(Rectangle corner, Team team)
    {
        const float iconSize = 20.625f;
        const float valueScale = 1.05f;

        int points = ModContent.GetInstance<PointsManager>().Points.TryGetValue(team, out int p) ? p : 0;
        if (Ass.IconPointsSetter is { IsLoaded: true } pointsAsset)
        {
            Texture2D icon = pointsAsset.Value;
            float scale = iconSize / Math.Max(icon.Width, icon.Height);
            Main.spriteBatch.Draw(icon, new Vector2(corner.Center.X, corner.Y + 8f), null,
                Color.White * .75f, 0f, icon.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        Utils.DrawBorderString(Main.spriteBatch, points.ToString(),
            new Vector2(corner.Center.X, corner.Y + 14f), Color.White, valueScale, .5f, 0f);

        if (corner.Contains(ScoreboardTeamHeader.Cursor))
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.instance.MouseText($"{points} points");
        }
    }
}
