using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace PvPAdventure.Common.Statistics.UI;

[Autoload(Side = ModSide.Client)]
public class Scoreline : ModSystem
{
    private readonly Interface _interface = new();

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (!layers.Contains(_interface))
        {
            var layerIndex = layers.FindIndex(layer => layer.Name == "Vanilla: Inventory");

            if (layerIndex != -1)
                layers.Insert(layerIndex + 1, _interface);
        }
    }

    public sealed class Interface() : GameInterfaceLayer("PvPAdventure: Scoreline", InterfaceScaleType.None)
    {
        private float _colorModulate = 1.0f;

        protected override bool DrawSelf()
        {
            ClientConfig config = ModContent.GetInstance<ClientConfig>();
            if (!config.Scoreline)
                return true;

            float scale = config.ScorelineUISize switch
            {
                ClientConfig.ScorelineSize.Small => 0.9f,
                ClientConfig.ScorelineSize.Medium => 1f,
                ClientConfig.ScorelineSize.Large => 1.15f,
                ClientConfig.ScorelineSize.VeryLarge => 1.5f,
                _ => 1f
            };

            int timerWidth = (int)(100 * scale);
            int timerHeight = (int)(40 * scale);

            int pointWidth = (int)(50 * scale);
            int pointHeight = (int)(30 * scale);

            var teamsWithPlayers = Main.player
                .Where(player => player.active)
                .Where(player => (Team)player.team != Team.None)
                .GroupBy(player => player.team)
                .Select(grouping => (Team)grouping.Key)
                .ToHashSet();

            var bounding = new Rectangle(
                (Main.screenWidth / 2) - (timerWidth / 2),
                0,
                timerWidth,
                timerHeight);

            // Determine how large we are going to end up being
            var widthOfAllPoints = ((teamsWithPlayers.Count + 2 - 1) / 2) * pointWidth;

            // Inflate so that we expand from the center
            bounding.Inflate(widthOfAllPoints, 0);

            // Inflate a bit just for the sake of having a bit of padding for the cursor
            bounding.Inflate((int)(16 * scale), (int)(16 * scale));

            var target = bounding.Contains(Main.mouseX, Main.mouseY) ? 0.25f : 1.0f;
            _colorModulate = (float)Utils.Lerp(_colorModulate, target, 1.0f / 16.0f);

            Utils.DrawInvBG(Main.spriteBatch,
                new((Main.screenWidth / 2) - (timerWidth / 2), 0, timerWidth, timerHeight),
                Main.teamColor[(int)Team.None] * 0.7f * _colorModulate);

            var gm = ModContent.GetInstance<GameManager>();

            if (gm.CurrentPhase == GameManager.Phase.Playing)
            {
                var timer = TimeSpan.FromSeconds(gm.TimeRemaining / 60.0);
                var text = timer.ToString(@"h\:mm\:ss");
                var metrics = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One * scale);
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch,
                    FontAssets.MouseText.Value, text,
                    new((int)((Main.screenWidth / 2.0f) - (metrics.X / 2.0f)), 10f * scale),
                    Color.White * _colorModulate, 0f, Vector2.Zero, Vector2.One * scale);
            }
            //else if (gm._startGameCountdown.HasValue)
            //{
            //    int secondsLeft = (int)Math.Ceiling(gm._startGameCountdown.Value / 60.0);
            //    var text = $"{secondsLeft}...";
            //    var metrics = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One);
            //    ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch,
            //        FontAssets.MouseText.Value, text,
            //        new((int)((Main.screenWidth / 2.0f) - (metrics.X / 2.0f)), 10.0f),
            //        Color.Green * _colorModulate, 0f, Vector2.Zero, Vector2.One);
            //}

            // Start from the furthest left
            var offset = -widthOfAllPoints;
            // Iterate teams in a standardize order
            foreach (var team in Enum.GetValues<Team>())
            {
                if (team == Team.None)
                    continue;

                if (!teamsWithPlayers.Contains(team))
                    continue;

                Utils.DrawInvBG(Main.spriteBatch,
                    new((Main.screenWidth / 2) - (timerWidth / 2) + offset, 0, pointWidth, pointHeight),
                    Main.teamColor[(int)team] * 0.7f * _colorModulate);

                var text = ModContent.GetInstance<PointsManager>().Points.TryGetValue(team, out int points)
                    ? points.ToString()
                    : "—";
                var metrics = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One * scale);
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch,
                    FontAssets.MouseText.Value,
                    text,
                    new((int)((Main.screenWidth / 2.0f) + offset - (pointWidth / 2.0f) - (metrics.X / 2)), 6f * scale),
                    Color.White * _colorModulate,
                    0.0f,
                    Vector2.Zero,
                    Vector2.One * scale);

                // Work our way to the right
                offset += pointWidth;
                // Skip over the timer that we are centered around
                if (offset == 0)
                    offset = timerWidth;
            }

            return base.DrawSelf();
        }
    }
}
