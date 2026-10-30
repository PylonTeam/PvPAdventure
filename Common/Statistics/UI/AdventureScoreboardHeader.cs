using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Bounties;
using PvPAdventure.Core.Utilities;
using PylonMatch.Common.Scoreboard;
using System;
using System.Collections.Generic;
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
        int points = ModContent.GetInstance<PointsManager>().Points.TryGetValue(team, out int p) ? p : 0;
        int shards = ModContent.GetInstance<BountyManager>().Bounties.TryGetValue(team, out IList<BountyManager.Page> pages) ? pages.Count : 0;
        int half = corner.Width / 2;

        Rectangle pointsCell = new(corner.X, corner.Y, half, corner.Height);
        Rectangle shardsCell = new(corner.X + half, corner.Y, corner.Width - half, corner.Height);

        Texture2D pointsIcon = Ass.IconPointsSetter is { IsLoaded: true } pointsAsset ? pointsAsset.Value : null;
        Texture2D shardsIcon = Ass.Shards is { IsLoaded: true } shardsAsset ? shardsAsset.Value : null;

        DrawIconValue(Main.spriteBatch, pointsCell, pointsIcon, points.ToString(), 1.375f);
        DrawIconValue(Main.spriteBatch, shardsCell, shardsIcon, shards.ToString(), 1.55f);

        Point mouse = ScoreboardTeamHeader.Cursor;

        if (pointsCell.Contains(mouse))
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.instance.MouseText($"{points} points");
        }
        else if (shardsCell.Contains(mouse))
        {
            Main.LocalPlayer.mouseInterface = true;
            Main.instance.MouseText($"{shards} bounty shards");
        }
    }

    private static void DrawIconValue(SpriteBatch sb, Rectangle cell, Texture2D icon, string value, float iconScaleMultiplier = 1f)
    {
        const float iconSize = 15f;
        const float valueScale = 1.05f;

        if (icon != null)
        {
            float scale = iconSize / Math.Max(icon.Width, icon.Height) * iconScaleMultiplier;
            sb.Draw(icon, new Vector2(cell.Center.X, cell.Y + 8f), null, Color.White * .75f, 0f, icon.Size() / 2f, scale, SpriteEffects.None, 0f);
        }

        Utils.DrawBorderString(sb, value, new Vector2(cell.Center.X, cell.Y + 14f), Color.White, valueScale, .5f, 0f);
    }
}