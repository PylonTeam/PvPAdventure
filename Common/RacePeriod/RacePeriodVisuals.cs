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
    private RacePeriodTrail wake = new();
    private RacePeriodJumpEchoes jumpEchoes = new();
    private int trailMountType = -1;
    private RacePeriodSpriteSheet? trailSpriteSheet;

    public override ModPlayer Clone(Player newEntity)
    {
        RacePeriodVisuals clone = (RacePeriodVisuals)base.Clone(newEntity);
        // Character previews and copied players must never share or display live movement history.
        clone.wake = new RacePeriodTrail();
        clone.jumpEchoes = new RacePeriodJumpEchoes();
        clone.trailMountType = -1;
        clone.trailSpriteSheet = null;
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
            ResetTrail();
            ResetSpriteAnimation();
            return;
        }

        bool hasCustom = race.TryGetSpriteSheet(out RacePeriodSpriteSheet spriteSheet);
        UpdateTrail(hasCustom ? spriteSheet : null);
        if (!hasCustom)
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

    private void UpdateTrail(RacePeriodSpriteSheet? spriteSheet)
    {
        if (Main.dedServ)
            return;

        RacePeriodTrailSettings settings = RacePeriodRules.TrailSettings;
        if (!settings.IsRacePeriodTrailEnabled)
        {
            ResetTrail();
            return;
        }

        if (trailMountType != Player.mount.Type || trailSpriteSheet != spriteSheet)
        {
            ResetTrail();
            trailMountType = Player.mount.Type;
            trailSpriteSheet = spriteSheet;
        }

        Vector2 feet = Player.gravDir < 0f ? Player.Top : Player.Bottom;
        int heading = Player.velocity.X > 0f ? 1 : Player.velocity.X < 0f ? -1 : Player.direction;
        Vector2 anchor = feet + new Vector2(-14f * heading, Player.gfxOffY - 16f * Player.gravDir);
        ulong tick = Main.GameUpdateCount;
        if (wake.Record(tick, Player.position, anchor, Player.velocity.Y != 0f, settings))
            jumpEchoes.Reset();
    }

    internal void TriggerPoweredJumpEchoes() => jumpEchoes.Trigger(Main.GameUpdateCount);

    internal void DrawTrail(ref PlayerDrawSet drawInfo, int start)
    {
        RacePeriodTrailSettings settings = RacePeriodRules.TrailSettings;
        if (!settings.IsRacePeriodTrailEnabled || start >= drawInfo.DrawDataCache.Count)
            return;

        Color team = Player.team >= 0 && Player.team < Main.teamColor.Length
            ? Main.teamColor[Player.team] : Color.White;
        Color lighting = Lighting.GetColor(Player.Center.ToTileCoordinates());
        ulong tick = Main.GameUpdateCount;
        jumpEchoes.Draw(ref drawInfo, start, tick, team, lighting, settings);
        wake.Append(drawInfo.DrawDataCache, start, tick, Main.screenPosition, team, settings,
            RacePeriodTrailTextures.Streak, lighting);
    }

    private void ResetTrail()
    {
        wake.Reset();
        jumpEchoes.Reset();
        trailMountType = -1;
        trailSpriteSheet = null;
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
        RacePeriodSpriteSheet? customSheet = DrawAnimal(ref drawInfo);
        RacePeriodAnimalLayout.Apply(ref drawInfo, start, customSheet);
        if (drawInfo.drawPlayer.TryGetModPlayer(out RacePeriodVisuals visuals))
            visuals.DrawTrail(ref drawInfo, start);
    }

    private static RacePeriodSpriteSheet? DrawAnimal(ref PlayerDrawSet drawInfo)
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

                return spriteSheet;
            }
        }

        Mount mount = drawInfo.drawPlayer.mount;
        for (int drawType = 0; drawType < 4; drawType++)
        {
            mount.Draw(drawInfo.DrawDataCache, drawType, drawInfo.drawPlayer, drawInfo.Position,
                drawInfo.colorMount, drawInfo.playerEffect, drawInfo.shadow);
        }

        return null;
    }

    private static bool DrawCustomSheet(
        ref PlayerDrawSet drawInfo,
        RacePeriodSpriteSheet spriteSheet,
        Texture2D texture,
        int frame)
    {
        if (texture is null || texture.IsDisposed || texture.Height % spriteSheet.TotalFrames != 0)
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
