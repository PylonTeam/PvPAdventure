using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using PylonMatch.Common.Game;
using PvPAdventure.Core.Compat;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace PvPAdventure.Common.Game;

[Autoload(Side = ModSide.Client)]
internal sealed class AdventureCountdown : ModSystem
{
    private const int TicksPerSecond = 60;
    private const int FirstCueSecond = 10;
    private const int NativeCueSeconds = 3;
    private const int FadeInTicks = 26;
    private const int OpeningShakeTick = 15;
    // The approved cue starts with an eight-tick lead-in to the steel strike.
    private const int ClashAudioLeadTicks = 8;
    private static readonly Color PortalViolet = new(130, 73, 224);
    private static readonly SoundStyle FinalBeatSound = new("Terraria/Sounds/Item_180")
        { PitchVariance = 0f, MaxInstances = 0 };
    // Steel recording: Sword Clash by u_fe12rqkbth.
    private static readonly SoundStyle ClashSound = new("PvPAdventure/Assets/Countdown/CountdownClash") { Volume = .39f };
    private readonly CountdownEffects effects = new();
    private int releaseAge = CountdownEffects.ReleaseDuration;
    private int countdownAge;
    private int lastSecond;
    private CountdownClashTeams teams;

    private static int RemainingSecond => GameSession.Instance is { StageId: "countdown" } session && session.IsSelected("pvpa")
        ? Math.Max(1, (session.RemainingTicks + TicksPerSecond - 1) / TicksPerSecond)
        : 0;

    public override void Load() => GameSession.StageChanged += OnStageChanged;

    public override void Unload()
    {
        GameSession.StageChanged -= OnStageChanged;
        Main.QueueMainThreadAction(effects.Dispose);
    }

    public override void OnWorldUnload() => Reset();

    private void OnStageChanged(GameEvent owner, string previous, string next)
    {
        if (owner?.Id == "pvpa" && previous == "countdown" && next == "playing")
        {
            teams = CountdownClashTeams.Capture();
            releaseAge = 0;
        }
        else
        {
            Reset();
        }
    }

    private void Reset()
    {
        countdownAge = lastSecond = 0;
        releaseAge = CountdownEffects.ReleaseDuration;
        teams = default;
    }

    public override void PostUpdateEverything()
    {
        if (releaseAge < CountdownEffects.ReleaseDuration)
        {
            releaseAge++;
            if (TryGetPortal(out Vector2 center, out float scale))
                UpdateRelease(center, scale);
            return;
        }

        int second = RemainingSecond;
        if (second == 0) return;

        countdownAge++;
        teams = CountdownClashTeams.Capture();
        if (TryGetPortal(out Vector2 portalCenter, out float portalScale))
            UpdatePortal(portalCenter, portalScale, second);
        lastSecond = second;
    }

    private void UpdatePortal(Vector2 center, float scale, int second)
    {
        float phase = CountdownPhase(second);
        // An initial observation can be a client joining midway through a digit.
        if (second <= FirstCueSecond && (lastSecond == second + 1 ||
            (lastSecond == 0 && phase <= 2f / TicksPerSecond)))
            PlayCountdownCue(second);

        float beat = 1f - MathHelper.SmoothStep(0f, 1f, phase * 4.1f);
        float opacity = MathHelper.SmoothStep(0f, 1f, countdownAge / (float)FadeInTicks);
        Lighting.AddLight(center, new Vector3(.25f, .11f, .54f) * (opacity * (.57f + .33f * beat)));
        Vector2 orbitPoint = CountdownEffects.OrbitPoint(center, scale, phase);
        Lighting.AddLight(orbitPoint, new Vector3(.49f, .30f, .65f) * (opacity * .56f * CountdownEffects.StarLight(phase)));

        Vector2 side = Vector2.UnitX * (20f * scale);
        float spill = opacity * (.14f + .10f * beat);
        Lighting.AddLight(center - side, TeamColor(teams.Left, .42f).ToVector3() * spill);
        Lighting.AddLight(center + side, TeamColor(teams.Right, .42f).ToVector3() * spill);

        if (countdownAge >= 12)
            new PotionOfReturnGateHelper(PotionOfReturnGateHelper.GateType.ExitPoint, center, opacity).SpawnReturnPortalDust();
        if (countdownAge == 12 || (lastSecond != 0 && lastSecond != second))
            EmitSparks(orbitPoint, scale, 8, 1.2f, teamBurst: false);
        if (countdownAge == OpeningShakeTick)
            ShakeCamera(2.5f);
    }

    private static void PlayCountdownCue(int second)
    {
        if (second > NativeCueSeconds)
        {
            SoundEngine.PlaySound(new SoundStyle($"PvPAdventure/Assets/Countdown/countdown_{second}"));
            return;
        }

        float progress = (NativeCueSeconds - second) / (float)(NativeCueSeconds - 1);
        SoundEngine.PlaySound(FinalBeatSound with
        {
            Volume = MathHelper.Lerp(.60f, .74f, progress),
            Pitch = MathHelper.Lerp(-.10f, .06f, progress)
        });
    }

    private void UpdateRelease(Vector2 center, float scale)
    {
        int sinceImpact = releaseAge - CountdownEffects.ImpactTick;
        Vector2 hit = CountdownEffects.ImpactPoint(center, scale);
        if (sinceImpact is >= 0 and < 16)
        {
            float flash = 1f - MathHelper.SmoothStep(0f, 1f, sinceImpact / 16f);
            Lighting.AddLight(hit, new Vector3(.46f, .26f, .69f) * flash);
        }

        if (releaseAge == CountdownEffects.ImpactTick - ClashAudioLeadTicks)
            SoundEngine.PlaySound(ClashSound, hit);
        if (releaseAge == CountdownEffects.ImpactTick)
            ShakeCamera(4.6f);

        if (releaseAge == CountdownEffects.CrossDustTick)
            EmitSparks(center, scale, 16, 2.9f, teamBurst: true);
    }

    private static float CountdownPhase(int second) =>
        MathHelper.Clamp((second * TicksPerSecond - GameSession.Instance.RemainingTicks) / (float)TicksPerSecond, 0f, 1f);

    private static void ShakeCamera(float strength)
    {
        Player player = Main.LocalPlayer;
        if (player.dead || player.ghost || !AdventureRegionSystem.Contains(player)) return;
        Main.instance.CameraModifiers.Add(new PunchCameraModifier(
            player.Center, new Vector2(.35f, -.94f), strength, 16f, 12, 1200f, "PvPAdventure Countdown"));
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int layer = layers.FindIndex(entry => entry.Name == "ErkySSC: Region Manager");
        if (layer >= 0)
            layers.Insert(layer + 1, new LegacyGameInterfaceLayer("PvPAdventure: Countdown", Draw, InterfaceScaleType.Game));
    }

    private static bool TryGetPortal(out Vector2 center, out float scale)
    {
        Rectangle area = AdventureRegionSystem.WorldArea;
        scale = MathHelper.Clamp(Math.Min(area.Width / 800f, area.Height / 480f), .5f, 1f);
        float radius = 64f * scale;
        center = new Vector2(area.Center.X, area.Top + 77f * scale);
        if (area.IsEmpty || Main.mapFullscreen)
            return false;

        // Keep the countdown inside the actual spawnbox and visible while the player is in it.
        if (AdventureRegionSystem.Contains(Main.LocalPlayer))
        {
            Matrix zoom = Main.GameViewMatrix.ZoomMatrix;
            Vector2 screen = Vector2.Transform(center - Main.screenPosition, zoom);
            float inset = 14f + radius * Math.Abs(zoom.M11);
            if (Math.Abs(zoom.M11) > .001f && Main.screenWidth > inset * 2f)
                center.X += (MathHelper.Clamp(screen.X, inset, Main.screenWidth - inset) - screen.X) / zoom.M11;
            float minimumY = 56f + radius * .49f * Math.Abs(zoom.M22);
            if (Math.Abs(zoom.M22) > .001f && screen.Y < minimumY)
                center.Y += (minimumY - screen.Y) / zoom.M22;

            center.X = MathHelper.Clamp(center.X, area.Left + radius * 1.20f, area.Right - radius * 1.20f);
            center.Y = MathHelper.Clamp(center.Y, area.Top + 77f * scale, area.Bottom - radius * 1.15f);
        }

        Vector2 onScreen = center - Main.screenPosition;
        float margin = radius * 2f;
        return onScreen.X >= -margin && onScreen.X <= Main.screenWidth + margin &&
            onScreen.Y >= -margin && onScreen.Y <= Main.screenHeight + margin;
    }

    private bool Draw()
    {
        bool release = releaseAge < CountdownEffects.ReleaseDuration;
        int second = RemainingSecond;
        if (Main.gameMenu || (!release && second == 0) || !TryGetPortal(out Vector2 center, out float scale))
            return true;

        float phase = release ? releaseAge % TicksPerSecond / (float)TicksPerSecond : CountdownPhase(second);
        float opacity = MathHelper.SmoothStep(.24f, 1f, countdownAge / (float)FadeInTicks);
        float age = (countdownAge + (release ? releaseAge : 0)) / (float)TicksPerSecond;
        CountdownPortalFrame frame = new(center - Main.screenPosition, scale, second, phase,
            opacity, age, release ? releaseAge : -1, teams);
        effects.Draw(frame, release ? Language.GetTextValue("Mods.PvPAdventure.Countdown.Go") : string.Empty);
        return true;
    }

    private static Color TeamColor(CountdownBladeTeams blade, float distance) =>
        blade.Hilt == 0 ? PortalViolet : blade.ColorAlongBlade(distance);

    private void EmitSparks(Vector2 center, float scale, int count, float speed, bool teamBurst)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
            Vector2 point = center + direction * (Main.rand.NextFloat(2f, 11f) * scale);
            int kind = teamBurst ? DustID.FireworksRGB : i % 3 == 0 ? DustID.GemSapphire : DustID.GemAmethyst;
            CountdownBladeTeams blade = direction.X < 0f ? teams.Left : teams.Right;
            Color color = teamBurst ? TeamColor(blade, i / (float)(count - 1)) : default;
            Vector2 velocity = direction * Main.rand.NextFloat(.65f, 1.25f) * speed;
            Dust spark = Dust.NewDustPerfect(point, kind, velocity, 65, color, Main.rand.NextFloat(.58f, .93f));
            spark.noGravity = true;
            spark.noLight = true;
        }
    }
}
