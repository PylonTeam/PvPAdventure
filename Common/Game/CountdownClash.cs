using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Core.Config;
using PvPAdventure.Utilities.Xna;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game;

internal readonly record struct CountdownBladeTeams(int Hilt, int Tip = 0, int Middle = 0)
{
    internal Color ColorAlongBlade(float distance) => BlendAlongBlade(distance, Main.teamColor);

    internal Color BlendAlongBlade(float distance, ReadOnlySpan<Color> colors)
    {
        Color hilt = colors[Hilt];
        if (Tip == 0) return hilt;

        Color tip = colors[Tip];
        if (Middle == 0)
            return Color.Lerp(hilt, tip, MathHelper.SmoothStep(0f, 1f, (distance - .32f) / .36f));

        Color middle = colors[Middle];
        return distance < .5f
            ? Color.Lerp(hilt, middle, MathHelper.SmoothStep(0f, 1f, (distance - .16f) / .34f))
            : Color.Lerp(middle, tip, MathHelper.SmoothStep(0f, 1f, (distance - .50f) / .34f));
    }
}

internal readonly record struct CountdownClashTeams(CountdownBladeTeams Left, CountdownBladeTeams Right)
{
    private const int LastTeam = 5;

    internal bool HasViewerTint => Left.Hilt != 0 && Left == Right;

    internal static int ViewerTeam => Main.LocalPlayer is { active: true, ghost: false, team: >= 1 and <= LastTeam } viewer
        ? viewer.team
        : 0;

    internal static CountdownClashTeams Capture() =>
        CaptureForPalette(ModContent.GetInstance<ClientConfig>().CountdownPalette);

    internal static CountdownClashTeams CaptureForPalette(AdventurePaletteMode mode)
    {
        int viewerTeam = ViewerTeam;
        if (mode == AdventurePaletteMode.ParticipatingTeams)
        {
            int roster = 0;
            foreach (Player player in Main.ActivePlayers)
            {
                if (!player.ghost && player.team is >= 1 and <= LastTeam)
                    roster |= 1 << player.team;
            }

            return ForRoster(roster, viewerTeam);
        }

        CountdownBladeTeams blade = new(viewerTeam);
        return new(blade, blade);
    }

    internal static CountdownClashTeams ForRoster(int teamMask, int localTeam)
    {
        Span<int> order = stackalloc int[LastTeam];
        int count = 0;
        if (localTeam is >= 1 and <= LastTeam && (teamMask & (1 << localTeam)) != 0)
            order[count++] = localTeam;
        for (int team = 1; team <= LastTeam; team++)
        {
            if (team != localTeam && (teamMask & (1 << team)) != 0)
                order[count++] = team;
        }

        return count switch
        {
            0 => default,
            1 => new(new(order[0]), default),
            2 => new(new(order[0]), new(order[1])),
            3 => new(new(order[0]), new(order[1], order[2])),
            4 => new(new(order[0], order[1]), new(order[2], order[3])),
            _ => new(new(order[0], order[1]), new(order[2], Tip: order[4], Middle: order[3]))
        };
    }
}

/// <summary>Animates the two swords from the vanilla PvP icon and tints their steel.</summary>
internal sealed class CountdownClash : IDisposable
{
    private const int LogicalSize = 18;
    private const int RefinedSize = LogicalSize * 2;
    private const float WorldPixelSize = 2f;

    // The neutral active frame uses a two-pixel grid. Keep the topmost sword's pixels by row.
    private static ReadOnlySpan<byte> FrontSwordBounds =>
    [
        0, 0, 4, 4, 3, 5, 2, 5, 2, 6, 2, 6, 3, 7, 3, 8, 4, 9,
        5, 10, 6, 13, 7, 14, 8, 15, 10, 15, 10, 15, 11, 16, 14, 16, 0, 0
    ];
    private Sword left;
    private Sword right;

    internal void Draw(Vector2 center, float radius, float travel, float opacity, CountdownClashTeams teams)
    {
        float offset = radius * .81f * (1f - travel);

        SpriteBatch batch = Main.spriteBatch;
        Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
        batch.End();
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, zoom);
        try
        {
            DrawPair(center, offset, opacity, teams);
        }
        finally
        {
            batch.End();
            batch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, zoom);
        }
    }

    internal void DrawPair(Vector2 center, float offset, float opacity, CountdownClashTeams teams)
    {
        if (left == null) SplitIcon();

        Vector2 separation = Vector2.UnitX * offset;
        DrawSword(left.GetTexture(teams.Left), center - separation, opacity);
        DrawSword(right.GetTexture(teams.Right), center + separation, opacity);
    }

    private void SplitIcon()
    {
        Texture2D atlas = TextureAssets.Pvp[0].Value;
        // tModLoader inserts a hover cell after each base-game column.
        int columns = atlas.Width * 2 >= atlas.Height ? 4 : 2;
        int stride = atlas.Width / columns;
        int pixelSize = Math.Max(1, stride / (LogicalSize + 1));
        int size = LogicalSize * pixelSize;
        Rectangle frame = new(atlas.Width / 2, 0, size, size);
        Color[] native = new Color[size * size];
        atlas.GetData(0, frame, native, 0, native.Length);

        Color[] combined = new Color[LogicalSize * LogicalSize];
        for (int y = 0; y < LogicalSize; y++)
        for (int x = 0; x < LogicalSize; x++)
            combined[y * LogicalSize + x] = native[(y * pixelSize + pixelSize / 2) * size + x * pixelSize + pixelSize / 2];

        Color[] front = new Color[combined.Length];
        Color[] back = new Color[combined.Length];
        ReadOnlySpan<byte> bounds = FrontSwordBounds;
        for (int y = 0; y < LogicalSize; y++)
        {
            int row = y * 2;
            int start = y * LogicalSize + bounds[row];
            int count = bounds[row + 1] - bounds[row] + 1;
            Array.Copy(combined, start, front, start, count);
        }

        for (int y = 0; y < LogicalSize; y++)
        for (int x = 0; x < LogicalSize; x++)
        {
            int index = y * LogicalSize + x;
            // Keep the visible rear sword from the frame and use the mirrored steel under the crossing.
            back[index] = front[index].A == 0 ? combined[index] : front[y * LogicalSize + LogicalSize - 1 - x];
        }

        left = new Sword(atlas.GraphicsDevice, back, 1);
        right = new Sword(atlas.GraphicsDevice, front, -1);
    }

    private static int[] RefinedArtSources(Color[] steel)
    {
        int[] sources = new int[RefinedSize * RefinedSize];
        for (int y = 0; y < LogicalSize; y++)
        for (int x = 0; x < LogicalSize; x++)
        {
            int center = y * LogicalSize + x;
            var (topLeft, topRight, bottomLeft, bottomRight) = PixelArtScale.RefinePixel(steel, center,
                x > 0 ? center - 1 : center,
                x < LogicalSize - 1 ? center + 1 : center,
                y > 0 ? center - LogicalSize : center,
                y < LogicalSize - 1 ? center + LogicalSize : center);
            int output = y * 2 * RefinedSize + x * 2;
            sources[output] = topLeft;
            sources[output + 1] = topRight;
            sources[output + RefinedSize] = bottomLeft;
            sources[output + RefinedSize + 1] = bottomRight;
        }
        return sources;
    }

    private static void DrawSword(Texture2D sprite, Vector2 position, float opacity)
    {
        Vector2 cells = (position + Main.screenPosition) / WorldPixelSize;
        position = new Vector2(MathF.Round(cells.X), MathF.Round(cells.Y)) * WorldPixelSize - Main.screenPosition;
        Main.spriteBatch.Draw(sprite, position, null, Color.White * opacity, 0f, new Vector2(RefinedSize * .5f),
            WorldPixelSize, SpriteEffects.None, 0f);
    }

    public void Dispose()
    {
        left?.Dispose();
        right?.Dispose();
    }

    private sealed class Sword(GraphicsDevice device, Color[] steel, int direction) : IDisposable
    {
        private readonly int[] artSources = RefinedArtSources(steel);
        private Texture2D colored;
        private Color[] working;
        private Color[] refined;
        private float[] bladeDistance;
        private CountdownBladeTeams currentTeams;

        internal Texture2D GetTexture(CountdownBladeTeams teams)
        {
            if (colored != null && currentTeams == teams) return colored;

            working ??= new Color[steel.Length];
            Array.Copy(steel, working, steel.Length);
            if (teams.Hilt != 0)
            {
                bladeDistance ??= MeasureBlade();
                TintBlade(teams);
            }

            refined ??= new Color[artSources.Length];
            for (int i = 0; i < refined.Length; i++)
                refined[i] = working[artSources[i]];

            colored ??= new Texture2D(device, RefinedSize, RefinedSize);
            colored.SetData(refined);
            currentTeams = teams;
            return colored;
        }

        private void TintBlade(CountdownBladeTeams teams)
        {
            for (int i = 0; i < steel.Length; i++)
            {
                float distance = bladeDistance[i];
                if (distance < 0f) continue;

                Color source = steel[i];
                Color hue = teams.ColorAlongBlade(distance);
                float value = source.R / 255f;
                Color shadow = Color.Lerp(new Color(20, 22, 30), hue, .29f);
                Color highlight = Color.Lerp(hue, Color.White, .66f);
                Color tinted = value < .69f
                    ? Color.Lerp(shadow, hue, value / .69f)
                    : Color.Lerp(hue, highlight, (value - .69f) / .31f);
                working[i] = tinted * (source.A / 255f);
            }
        }

        private float[] MeasureBlade()
        {
            float[] distance = new float[steel.Length];
            float minimum = float.MaxValue;
            float maximum = float.MinValue;
            for (int i = 0; i < steel.Length; i++)
            {
                Color pixel = steel[i];
                // The blade is grey, with a charcoal outline. Brass and leather keep their native colors.
                if (pixel.A == 0 || pixel.R < 89 || pixel.R != pixel.G || pixel.G != pixel.B)
                {
                    distance[i] = float.NaN;
                    continue;
                }

                float along = direction * (i % LogicalSize) - 1.14f * (i / LogicalSize);
                distance[i] = along;
                minimum = Math.Min(minimum, along);
                maximum = Math.Max(maximum, along);
            }

            float length = Math.Max(1f, maximum - minimum);
            for (int i = 0; i < distance.Length; i++)
                distance[i] = float.IsNaN(distance[i]) ? -1f : (distance[i] - minimum) / length;
            return distance;
        }

        public void Dispose() => colored?.Dispose();
    }
}
