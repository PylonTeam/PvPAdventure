using System;
using ErkySSC.Common.RegionProtection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Game;
using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Compat;
using PvPAdventure.Core.Config;
using PvPAdventure.Utilities.Xna;
using PylonMatch.Common.Game;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Portals;

[Autoload(Side = ModSide.Client)]
internal sealed class SpawnboxPortal : ModSystem
{
    private const int FadeInTicks = 45;
    private const int Scale = 2;
    private const int Frames = 8;
    private const int ArtPixelSize = 2;
    private readonly Color[][] teamAtlasPixels = new Color[6][];
    private int[] portalSourceSamples;
    private Texture2D worldAtlas;
    private AdventurePaletteMode worldAtlasMode;
    private int worldAtlasTeam;
    private CountdownClashTeams worldAtlasTeams;
    private int age;

    private static Rectangle ActiveBounds => TravelRules.Enabled &&
        GameSession.Instance.IsSelected("pvpa") && GameSession.Instance.IsPlaying
            ? AdventureRegionSystem.WorldArea
            : Rectangle.Empty;

    internal static bool TryGetAnchor(out Vector2 bottom)
    {
        Rectangle bounds = ActiveBounds;
        bottom = bounds.Center.ToVector2();
        return !bounds.IsEmpty;
    }

    internal static bool IsTeamMember(Player player) => player?.active == true && player.team is >= 1 and <= 5;

    internal static bool CanUse(Player player) => !ActiveBounds.IsEmpty && IsTeamMember(player) &&
        !player.dead && !player.ghost && ModContent.GetInstance<ServerConfig>().TravelSystem.IsTeammateSpawnTeleportEnabled &&
        TravelRegions.IsInTravelRegion(player);

    internal static bool TryGetDestination(Player player, out Vector2 position)
    {
        position = default;
        if (!CanUse(player))
            return false;

        Rectangle bounds = ActiveBounds;
        return RegionTeleportSystem.TryFindLandingInside(player, bounds.Center.ToVector2(), bounds, out position);
    }

    public override void Load() => GameSession.StageChanged += ResetFade;

    public override void Unload()
    {
        GameSession.StageChanged -= ResetFade;
        if (worldAtlas is { } atlas)
            Main.QueueMainThreadAction(atlas.Dispose);
    }

    public override void OnWorldUnload() => age = 0;

    private void ResetFade(GameEvent owner, string previous, string next) => age = 0;

    public override void PostUpdateNPCs()
    {
        if (!TryGetAnchor(out Vector2 bottom))
        {
            age = 0;
            return;
        }

        if (age < FadeInTicks)
            age++;

        Player player = Main.LocalPlayer;
        if (Main.mapFullscreen || player.mouseInterface || !IsTeamMember(player) || player.dead || player.ghost)
            return;

        int width = PortalNPC.PortalWidth * Scale;
        int height = PortalNPC.PortalHeight * Scale;
        Rectangle hitbox = new((int)bottom.X - width / 2, (int)bottom.Y - height, width, height);
        PortalDrawer.HandleMapInteraction(player, hitbox);
    }

    public override void PostDrawTiles()
    {
        if (age == 0 || Main.mapFullscreen || !TryGetAnchor(out Vector2 bottom))
            return;

        Texture2D texture = GetWorldPortalTexture();
        int frameHeight = texture.Height / Frames;
        int frameIndex = (int)(Main.GameUpdateCount / 5 % Frames);
        Rectangle frame = new(0, frameIndex * frameHeight, texture.Width, frameHeight);
        Vector2 origin = new(frame.Width * .5f, frame.Height);
        float opacity = MathHelper.SmoothStep(0f, 1f, age / (float)FadeInTicks);
        SpriteBatch batch = Main.spriteBatch;
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        batch.Draw(texture, bottom - Main.screenPosition, frame, Color.White * opacity,
            0f, origin, 1f, SpriteEffects.None, 0f);
        batch.End();
    }

    private Texture2D GetWorldPortalTexture()
    {
        AdventurePaletteMode mode = ModContent.GetInstance<ClientConfig>().WorldPortalPalette;
        CountdownClashTeams teams = CountdownClashTeams.CaptureForPalette(mode);
        int team = teams.Left.Hilt;
        if (worldAtlas != null && mode == worldAtlasMode && team == worldAtlasTeam && teams == worldAtlasTeams)
            return worldAtlas;

        Texture2D neutral = PortalAssets.GetPortalTexture(0);
        Color[] teamPixels = GetTeamPixels(team);
        portalSourceSamples ??= CreatePortalSourceSamples(neutral.Width, neutral.Height, GetTeamPixels(0));
        bool blendTeams = mode == AdventurePaletteMode.ParticipatingTeams && teams.Right.Hilt != 0;
        if (blendTeams)
        {
            LoadBladeSprites(teams.Left);
            LoadBladeSprites(teams.Right);
        }

        // Keep the same world footprint, with two world pixels per refined art cell.
        int width = neutral.Width * Scale;
        int height = neutral.Height * Scale;
        int frameHeight = height / Frames;
        int sampleWidth = width / ArtPixelSize;
        Color[] output = new Color[width * height];
        Span<Color> colors = stackalloc Color[teamAtlasPixels.Length];
        for (int y = 0; y < height; y += ArtPixelSize)
        {
            float row = (y % frameHeight + ArtPixelSize * .5f) / frameHeight;
            float distance = 1f - row;
            float vertical = row - .5f;
            float bend = vertical * (.25f - .18f * MathF.Abs(vertical));
            for (int x = 0; x < width; x += ArtPixelSize)
            {
                int source = portalSourceSamples[y / ArtPixelSize * sampleWidth + x / ArtPixelSize];
                Color pixel = teamPixels[source];
                if (blendTeams)
                {
                    if (pixel.A == 0)
                        continue;

                    for (int index = 1; index < teamAtlasPixels.Length; index++)
                    {
                        if (teamAtlasPixels[index] is { } pixels)
                            colors[index] = pixels[source];
                    }

                    float column = (x + ArtPixelSize * .5f) / width;
                    float side = MathHelper.SmoothStep(0f, 1f, (column + bend - .08f) / .84f);
                    Color left = teams.Left.BlendAlongBlade(distance, colors);
                    Color right = teams.Right.BlendAlongBlade(distance, colors);
                    pixel = Color.Lerp(left, right, side);
                }

                int target = y * width + x;
                output[target] = pixel;
                output[target + 1] = pixel;
                output[target + width] = pixel;
                output[target + width + 1] = pixel;
            }
        }

        worldAtlas ??= new Texture2D(neutral.GraphicsDevice, width, height);
        worldAtlas.SetData(output);
        worldAtlasMode = mode;
        worldAtlasTeam = team;
        worldAtlasTeams = teams;
        return worldAtlas;
    }

    private static int[] CreatePortalSourceSamples(int width, int height, ReadOnlySpan<Color> neutral)
    {
        int frameHeight = height / Frames;
        int sampleWidth = width / ArtPixelSize * Scale;
        int[] samples = new int[sampleWidth * (height / ArtPixelSize * Scale)];
        int nextRow = width * ArtPixelSize;
        for (int y = 0; y < height; y += ArtPixelSize)
        {
            int frameRow = y % frameHeight;
            for (int x = 0; x < width; x += ArtPixelSize)
            {
                int center = y * width + x;
                int left = x == 0 ? center : center - ArtPixelSize;
                int right = x + ArtPixelSize == width ? center : center + ArtPixelSize;
                int above = frameRow == 0 ? center : center - nextRow;
                int below = frameRow + ArtPixelSize == frameHeight ? center : center + nextRow;
                // Every team keeps the same Portal Gun silhouette.
                var refined = PixelArtScale.RefinePixel(neutral, center, left, right, above, below);
                int target = y / ArtPixelSize * Scale * sampleWidth + x / ArtPixelSize * Scale;
                samples[target] = refined.TopLeft;
                samples[target + 1] = refined.TopRight;
                samples[target + sampleWidth] = refined.BottomLeft;
                samples[target + sampleWidth + 1] = refined.BottomRight;
            }
        }

        return samples;
    }

    private void LoadBladeSprites(CountdownBladeTeams blade)
    {
        GetTeamPixels(blade.Hilt);
        if (blade.Tip != 0)
            GetTeamPixels(blade.Tip);
        if (blade.Middle != 0)
            GetTeamPixels(blade.Middle);
    }

    private Color[] GetTeamPixels(int team)
    {
        if (teamAtlasPixels[team] is { } pixels)
            return pixels;

        Texture2D texture = PortalAssets.GetPortalTexture(team);
        pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        return teamAtlasPixels[team] = pixels;
    }
}
