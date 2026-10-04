using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>
/// Tracks custom Spirit Animal animation while RacePeriod is active.
/// </summary>
public sealed class RacePeriodVisuals : ModPlayer
{
    private enum SpriteAnimationState : byte
    {
        None,
        Standing,
        Running,
        Airborne,
        PoweredAirborne
    }

    private SpriteAnimationState spriteAnimationState;
    private RacePeriodSpriteSheet lastSpriteSheet;
    private bool hasLastSpriteSheet;
    private int spriteFrame;
    private float spriteFrameCounter;
    internal RacePeriodTrail Trail { get; private set; } = new();

    public override ModPlayer Clone(Player newEntity)
    {
        RacePeriodVisuals clone = (RacePeriodVisuals)base.Clone(newEntity);
        // Character previews and copied players must never share or display live movement history.
        clone.Trail = new RacePeriodTrail();
        return clone;
    }

    internal bool TryGetCustomDraw(out RacePeriodSpriteSheet spriteSheet, out int frame)
    {
        if (Player.TryGetModPlayer(out RacePeriodPlayer race) && race.IsRacing &&
            race.TryGetSpriteSheet(out spriteSheet))
        {
            frame = spriteFrame;
            return true;
        }

        spriteSheet = default;
        frame = 0;
        return false;
    }

    public override void PostUpdate()
    {
        if (!Player.active || Player.dead || !Player.TryGetModPlayer(out RacePeriodPlayer race) || !race.IsRacing)
        {
            Trail.Reset();
            ResetSpriteAnimation();
            return;
        }

        var trailSettings = RacePeriodRules.TrailSettings;
        if (!Main.dedServ && trailSettings.IsRacePeriodTrailEnabled)
            Trail.Record(Player.position, Player.velocity.Y != 0f, trailSettings);
        else
            Trail.Reset();

        if (!race.TryGetSpriteSheet(out RacePeriodSpriteSheet spriteSheet))
        {
            ResetSpriteAnimation();
            return;
        }

        SpriteAnimationState state;
        RacePeriodAnimationRange animation;
        if (Player.velocity.Y != 0f)
        {
            bool powered = race.UsingPoweredAirAnimation;
            state = powered ? SpriteAnimationState.PoweredAirborne : SpriteAnimationState.Airborne;
            animation = powered ? spriteSheet.PoweredAirborne : spriteSheet.Airborne;
        }
        else if (Math.Abs(Player.velocity.X) > 0.05f)
        {
            state = SpriteAnimationState.Running;
            animation = spriteSheet.Running;
        }
        else
        {
            state = SpriteAnimationState.Standing;
            animation = spriteSheet.Standing;
        }

        if (!hasLastSpriteSheet || lastSpriteSheet != spriteSheet || spriteAnimationState != state)
        {
            hasLastSpriteSheet = true;
            lastSpriteSheet = spriteSheet;
            spriteAnimationState = state;
            spriteFrame = animation.StartFrame;
            spriteFrameCounter = 0f;
            return;
        }

        spriteFrameCounter += animation.ScaleWithHorizontalSpeed
            ? Math.Abs(Player.velocity.X)
            : 1f;

        while (spriteFrameCounter > animation.FrameDelay)
        {
            spriteFrameCounter -= animation.FrameDelay;
            spriteFrame++;
            if (spriteFrame >= animation.StartFrame + animation.FrameCount)
                spriteFrame = animation.StartFrame;
        }
    }

    private void ResetSpriteAnimation()
    {
        spriteAnimationState = SpriteAnimationState.None;
        hasLastSpriteSheet = false;
        spriteFrame = 0;
        spriteFrameCounter = 0f;
    }
}

/// <summary>
/// Draws only the active Spirit Animal. Custom sheets are drawn directly; animals without one use
/// all four slots of Terraria's Mount.Draw. The rider stays completely hidden in both paths.
/// </summary>
internal sealed class RacePeriodMountLayer : PlayerDrawLayer
{
    // Do not use AfterParent(MountBack): RacePeriod deliberately hides MountBack, and tModLoader
    // propagates a parent's hidden state to every BeforeParent/AfterParent child. Between gives the
    // replacement animal an independent slot while keeping it near the normal mount position.
    public override Position GetDefaultPosition() =>
        new Between(PlayerDrawLayers.MountBack, PlayerDrawLayers.Carpet);

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) =>
        drawInfo.shadow == 0f && !drawInfo.headOnlyRender &&
        drawInfo.drawPlayer.TryGetModPlayer(out RacePeriodPlayer race) && race.IsRacing;

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        int start = drawInfo.DrawDataCache.Count;
        DrawAnimal(ref drawInfo);
        Player player = drawInfo.drawPlayer;
        if (player.TryGetModPlayer(out RacePeriodVisuals visuals))
        {
            Color teamColor = player.team >= 0 && player.team < Main.teamColor.Length
                ? Main.teamColor[player.team] : Color.White;
            var settings = RacePeriodRules.TrailSettings;
            if (visuals.Trail.IsMoving(settings))
                visuals.Trail.Append(drawInfo.DrawDataCache, start, player.position, teamColor,
                    settings, RacePeriodTrailTextures.Mask,
                    settings.IsWispTrailEnabled ? RacePeriodTrailTextures.Wisp : null,
                    settings.IsSpeedStreakTrailEnabled ? RacePeriodTrailTextures.Streak : null,
                    Main.GlobalTimeWrappedHourly, RacePeriodTrailTextures.SoftGlow,
                    Lighting.GetColor(player.Center.ToTileCoordinates()));
        }
    }

    private static void DrawAnimal(ref PlayerDrawSet drawInfo)
    {
        if (drawInfo.drawPlayer.TryGetModPlayer(out RacePeriodVisuals visuals) &&
            visuals.TryGetCustomDraw(out RacePeriodSpriteSheet spriteSheet, out int frame))
        {
            // A malformed or unavailable body falls through to the physical mount's vanilla art.
            // An optional malformed foreground is simply skipped so it cannot hide the animal.
            if (DrawCustomSheet(ref drawInfo, spriteSheet, spriteSheet.BodyTexture.Value, frame))
            {
                if (spriteSheet.ForegroundTexture is { } foreground)
                    DrawCustomSheet(ref drawInfo, spriteSheet, foreground.Value, frame);

                return;
            }
        }

        Mount mount = drawInfo.drawPlayer.mount;
        for (int drawType = 0; drawType < 4; drawType++)
        {
            mount.Draw(drawInfo.DrawDataCache, drawType, drawInfo.drawPlayer, drawInfo.Position,
                drawInfo.colorMount, drawInfo.playerEffect, drawInfo.shadow);
        }
    }

    private static bool DrawCustomSheet(
        ref PlayerDrawSet drawInfo,
        RacePeriodSpriteSheet spriteSheet,
        Texture2D texture,
        int frame)
    {
        if (texture.Height % spriteSheet.TotalFrames != 0)
            return false;

        int frameHeight = texture.Height / spriteSheet.TotalFrames;
        int sourceHeight = frameHeight - spriteSheet.FrameCropBottom;
        if (frameHeight <= 0 || sourceHeight <= 0 || frame < 0 || frame >= spriteSheet.TotalFrames)
            return false;

        Rectangle source = new(0, frame * frameHeight, texture.Width, sourceHeight);
        Player player = drawInfo.drawPlayer;
        float xOffset = player.direction <= 0 ? -spriteSheet.DrawOffset.X : spriteSheet.DrawOffset.X;
        // The physical mount can change Player.height (Bunny raises it from 42 to 62). Anchor from
        // the player's grounded bottom using the artwork's intended player height, otherwise every
        // custom sheet is lifted by half of the physics mount's height boost.
        Vector2 position = new(
            (int)(drawInfo.Position.X - Main.screenPosition.X + player.width / 2f + xOffset),
            (int)(drawInfo.Position.Y - Main.screenPosition.Y + player.mount.PlayerOffset +
                  player.height - spriteSheet.AnchorPlayerHeight / 2f + spriteSheet.DrawOffset.Y));
        Vector2 origin = new(texture.Width / 2f, frameHeight / 2f);

        DrawData data = new(texture, position, source, drawInfo.colorMount, 0f, origin,
            spriteSheet.Scale, drawInfo.playerEffect)
        {
            shader = Mount.currentShader
        };
        drawInfo.DrawDataCache.Add(data);
        return true;
    }
}
