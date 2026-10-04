using Microsoft.Xna.Framework;
using PvPAdventure.Common.Travel;
using System;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Frame-rate independent vertical following; horizontal camera movement stays untouched.</summary>
internal sealed class RacePeriodCameraLag
{
    private bool initialized;
    private float screenY;
    private float previousTargetY;
    private Vector2 previousPlayerPosition;

    internal void Reset() => initialized = false;

    internal Vector2 Step(Vector2 target, Vector2 playerPosition, float elapsedSeconds, bool enabled,
        float response, float maxLag, float teleportResetDistance)
    {
        if (!enabled || !Finite(target) || !Finite(playerPosition))
        {
            Reset();
            return target;
        }

        response = Math.Clamp(float.IsFinite(response) ? response : 12f, 1f, 60f);
        maxLag = Math.Clamp(float.IsFinite(maxLag) ? maxLag : 64f, 0f, 256f);
        teleportResetDistance = Math.Clamp(float.IsFinite(teleportResetDistance) ? teleportResetDistance : 256f, 16f, 4096f);
        if (!initialized || Vector2.DistanceSquared(playerPosition, previousPlayerPosition) >=
            teleportResetDistance * teleportResetDistance || MathF.Abs(target.Y - previousTargetY) >= teleportResetDistance)
        {
            initialized = true;
            screenY = target.Y;
        }
        else
        {
            float seconds = Math.Clamp(float.IsFinite(elapsedSeconds) ? elapsedSeconds : 0f, 0f, 0.25f);
            float blend = 1f - MathF.Exp(-response * seconds);
            screenY += (target.Y - screenY) * blend;
            screenY = Math.Clamp(screenY, target.Y - maxLag, target.Y + maxLag);
        }

        previousTargetY = target.Y;
        previousPlayerPosition = playerPosition;
        return new Vector2(target.X, screenY);
    }

    internal void ConstrainToRendered(float renderedY)
    {
        // Vanilla truncates to screen pixels after ModifyScreenPosition. Keep fractional easing
        // through that normal rounding; only a real world-bound clamp changes our history.
        if (initialized && float.IsFinite(renderedY) && MathF.Abs(renderedY - screenY) > 1f)
            screenY = renderedY;
    }

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}

/// <summary>Softens abrupt tile-height changes while preserving vanilla lead, zoom and bounds.</summary>
[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodCamera : ModSystem
{
    private readonly RacePeriodCameraLag lag = new();
    private bool appliedThisDraw;
    private bool restorePreviousTarget;
    private float previousVanillaY;
    private float previousRenderedY;
    private Point viewport;
    private Vector2 zoom;
    private float frameSeconds = 1f / 60f;
    private double previousDrawSeconds = double.NaN;

    private static bool Spectating => TravelSpectateSystem.HasTargetHover || TravelSpectateSystem.MapRestore;

    public override void Load()
    {
        Main.OnPreDraw += BeforeDraw;
        On_Main.DoDraw_UpdateCameraPosition += UpdateVanillaCamera;
    }

    public override void Unload()
    {
        Main.OnPreDraw -= BeforeDraw;
        On_Main.DoDraw_UpdateCameraPosition -= UpdateVanillaCamera;
        ResetCamera();
    }

    public override void OnWorldUnload() => ResetCamera();

    internal void ResetCamera()
    {
        lag.Reset();
        appliedThisDraw = false;
        restorePreviousTarget = false;
        viewport = default;
        zoom = default;
        previousDrawSeconds = double.NaN;
    }

    private void BeforeDraw(GameTime gameTime)
    {
        double total = gameTime.TotalGameTime.TotalSeconds;
        double elapsed = double.IsFinite(previousDrawSeconds) ? total - previousDrawSeconds : gameTime.ElapsedGameTime.TotalSeconds;
        frameSeconds = (float)Math.Clamp(double.IsFinite(elapsed) ? elapsed : 1d / 60d, 0d, 0.25d);
        previousDrawSeconds = total;
    }

    private void UpdateVanillaCamera(On_Main.orig_DoDraw_UpdateCameraPosition orig)
    {
        // Vanilla uses last frame's screenPosition for cameraLerp before it calculates the new
        // target. Restore only our Y adjustment here so the eased result cannot feed back into
        // vanilla's lead. screenLastPosition was already captured before this camera hook.
        if (!Main.gameMenu && !Spectating && restorePreviousTarget &&
            MathF.Abs(Main.screenPosition.Y - previousRenderedY) < 0.01f)
            Main.screenPosition.Y = previousVanillaY;
        restorePreviousTarget = false;
        appliedThisDraw = false;
        if (Main.gameMenu || Spectating)
            ResetCamera();

        orig();
        if (appliedThisDraw && !Spectating)
        {
            previousRenderedY = Main.screenPosition.Y;
            restorePreviousTarget = true;
            lag.ConstrainToRendered(previousRenderedY);
        }
    }

    public override void ModifyScreenPosition()
    {
        Player player = Main.LocalPlayer;
        RacePeriodTrailSettings settings = RacePeriodTrailSettings.Create();
        if (Main.gameMenu || player?.active != true || player.dead || player.ghost || Spectating ||
            !settings.IsCameraLagEnabled || !player.TryGetModPlayer(out RacePeriodPlayer race) || !race.IsRacing)
        {
            ResetCamera();
            return;
        }

        Point currentViewport = new(Main.screenWidth, Main.screenHeight);
        Vector2 currentZoom = Main.GameViewMatrix.Zoom;
        if (currentViewport != viewport || currentZoom != zoom)
        {
            lag.Reset();
            viewport = currentViewport;
            zoom = currentZoom;
        }

        Vector2 target = Main.screenPosition;
        previousVanillaY = target.Y;
        Main.screenPosition = lag.Step(target, player.position, frameSeconds, settings.IsCameraLagEnabled,
            settings.CameraVerticalResponse, settings.CameraMaxLagPixels, settings.CameraTeleportResetDistance);
        appliedThisDraw = true;
        // Main applies its own pixel rounding and zoom-aware world bounds after this hook.
    }
}

[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodCameraRespawn : ModPlayer
{
    public override void OnRespawn()
    {
        if (Player.whoAmI == Main.myPlayer)
            ModContent.GetInstance<RacePeriodCamera>().ResetCamera();
    }
}
