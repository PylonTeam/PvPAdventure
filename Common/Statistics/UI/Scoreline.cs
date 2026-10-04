using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Core.Config;
using PvPFramework.Common.Game;
using Terraria;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.UI.Chat;

namespace PvPAdventure.Common.Statistics.UI;

/// <summary>Adventure points beside Framework's shared timer.</summary>
[Autoload(Side = ModSide.Client)]
public class Scoreline : ModSystem
{
    private readonly Interface _interface = new();

    private static float Scale => ModContent.GetInstance<ClientConfig>().ScorelineUISize switch
    {
        ClientConfig.ScorelineSize.Small => .9f,
        ClientConfig.ScorelineSize.Large => 1.15f,
        ClientConfig.ScorelineSize.VeryLarge => 1.5f,
        _ => 1f
    };

    public static Rectangle GetHoverBounds(Rectangle timer)
    {
        int teams = Main.player.Where(player => player?.active == true && (Team)player.team != Team.None)
            .Select(player => player.team).Distinct().Count();
        timer.Inflate((teams + 1) / 2 * (int)(50 * Scale), 0);
        timer.Height = System.Math.Max(timer.Height, (int)(30 * Scale));
        return timer;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int index = layers.FindIndex(layer => layer.Name == "Vanilla: Mouse Text");
        if (index >= 0 && !layers.Contains(_interface)) layers.Insert(index, _interface);
    }

    public sealed class Interface() : GameInterfaceLayer("PvPAdventure: Team Points", InterfaceScaleType.UI)
    {
        protected override bool DrawSelf()
        {
            if (Main.gameMenu || !ModContent.GetInstance<GameManager>().IsSelected) return true;

            float scale = Scale;
            int pointWidth = (int)(50 * scale);
            int pointHeight = (int)(30 * scale);
            var teams = Main.player.Where(player => player?.active == true && (Team)player.team != Team.None)
                .Select(player => (Team)player.team).Distinct().OrderBy(team => team).ToArray();
            Rectangle timer = GameTimerUISystem.PanelBounds;
            int leftCount = (teams.Length + 1) / 2;
            float colorModulate = GameTimerUISystem.ColorModulate;

            for (int index = 0; index < teams.Length; index++)
            {
                Team team = teams[index];
                int x = index < leftCount ? timer.X - (leftCount - index) * pointWidth
                    : timer.Right + (index - leftCount) * pointWidth;
                Rectangle panel = new(x, timer.Y, pointWidth, pointHeight);
                Utils.DrawInvBG(Main.spriteBatch, panel, Main.teamColor[(int)team] * .7f * colorModulate);
                string text = ModContent.GetInstance<PointsManager>().Points[team].ToString();
                Vector2 size = ChatManager.GetStringSize(FontAssets.MouseText.Value, text, Vector2.One * scale);
                ChatManager.DrawColorCodedStringWithShadow(Main.spriteBatch, FontAssets.MouseText.Value, text,
                    new Vector2(panel.Center.X - size.X / 2f, panel.Y + 6f * scale), Color.White * colorModulate,
                    0f, Vector2.Zero, Vector2.One * scale);
            }
            return true;
        }
    }
}
