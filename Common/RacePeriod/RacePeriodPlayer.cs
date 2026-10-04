using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Core.Config;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

/// <summary>
/// Before a match starts, everyone inside the race entry area becomes their selected race mount. The
/// mount and its protections persist into the match until that player dismounts or dies. The
/// server config controls only whether the feature is enabled; all mounts share fixed rules.
/// </summary>
public sealed class RacePeriodPlayer : ModPlayer
{
    private bool raceMountApplied;
    private int raceMountType = -1;
    private RacePeriodSpriteSheet? raceSpriteSheet;
    private bool wasHookPressed;
    private bool wasJumpPressed;
    private bool wasAirborne;
    private int airJumpCount;
    private bool isGrounded;
    private int jumpCooldown;
    private bool wasInRaceWindow;

    /// <summary>True after pre-game entry, until the player dismounts or dies.</summary>
    public bool InRacePeriod => raceMountApplied && FeatureEnabled;

    /// <summary>True while this player is riding the mount captured on race entry.</summary>
    public bool IsRacing => InRacePeriod && Player.mount.Active && Player.mount.Type == raceMountType;

    internal bool UsingPoweredAirAnimation => airJumpCount > 0 && !isGrounded;

    internal bool TryGetSpriteSheet(out RacePeriodSpriteSheet spriteSheet)
    {
        if (raceSpriteSheet is { } value)
        {
            spriteSheet = value;
            return true;
        }

        spriteSheet = default;
        return false;
    }

    private static bool FeatureEnabled =>
        ModContent.GetInstance<ServerConfig>()?.RacePeriod?.RacePeriodEnabled == true &&
        ModContent.GetInstance<GameManager>().IsSelected;

    private static bool InRaceWindow => RacePeriodRules.WaitingProvider?.Invoke() == true;
    private bool RaceWindowActive => FeatureEnabled && InRaceWindow && RacePeriodRules.EntryAreaProvider?.Invoke(Player) == true;

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (!IsRacing || !drawInfo.headOnlyRender)
            return;

        // HeadOnlySetup subtracts the mount's HeightMapOffset, but ResetMountHitbox removes
        // its height boost. Cancel both height-dependent terms so portraits line up with an
        // unmounted player, including the frame when SetMount has just changed the height.
        drawInfo.Position.Y += Player.HeightMapOffset - (Player.height - Player.defaultHeight);
    }

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        // Head-only passes power map/UI portraits and should stay readable. World afterimages are
        // hidden with the rider so no player body is ever visible behind the spirit animal.
        if (!IsRacing || drawInfo.headOnlyRender)
            return;

        foreach (PlayerDrawLayer layer in PlayerDrawLayerLoader.Layers)
        {
            // The custom layer below calls vanilla Mount.Draw for all four texture slots. Hide the
            // whole rider (including held items/accessories) and every original mount layer.
            if (layer is not RacePeriodMountLayer && RacePeriodRules.KeepDrawLayerProvider?.Invoke(layer) != true)
                layer.Hide();
        }
    }

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool pvp) =>
        InRacePeriod;

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) =>
        ClearRaceState();

    public override void Load() => On_Player.QuickMount += BlockMountToggleWhileWaiting;

    public override void Unload() => On_Player.QuickMount -= BlockMountToggleWhileWaiting;

    private bool MountLocked => RaceWindowActive;

    private static void BlockMountToggleWhileWaiting(On_Player.orig_QuickMount orig, Player self)
    {
        if (self.TryGetModPlayer(out RacePeriodPlayer race) && race.MountLocked)
        {
            if (self.whoAmI == Main.myPlayer)
                WarnLocked("MountLocked");

            return;
        }

        orig(self);
    }

    public override void SetControls()
    {
        if (Player.whoAmI != Main.myPlayer)
            return;

        if (InRacePeriod)
        {
            // A race animal only runs and jumps; these two inputs are what fire mount abilities.
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        bool pressed = Player.controlHook;
        if (pressed && MountLocked)
        {
            Player.controlHook = false;
            if (!wasHookPressed)
                WarnLocked("HookLocked");
        }

        wasHookPressed = pressed;
    }

    private static void WarnLocked(string messageKey)
    {
        if (ModContent.GetInstance<ClientConfig>()?.ShowSpawnMountWarnings != true)
            return;

        PopupText.NewText(new AdvancedPopupRequest
        {
            Color = Color.Crimson,
            Text = Language.GetTextValue($"Mods.PvPAdventure.RacePeriod.{messageKey}"),
            Velocity = new Vector2(0f, -4f),
            DurationInFrames = 60
        }, Main.LocalPlayer.Top + new Vector2(0f, -40f));
    }

    public override void PreUpdate()
    {
        isGrounded = Player.velocity.Y == 0f;
        bool jumpJustPressed = Player.controlJump && !wasJumpPressed;

        if (wasAirborne && isGrounded)
            airJumpCount = 0;

        if (jumpCooldown > 0)
            jumpCooldown--;

        bool cooldownGate = airJumpCount == 0 || jumpCooldown == 0;
        bool underJumpLimit = airJumpCount <= RacePeriodRules.MaxAirJumps;
        bool wantsAirJump = InRacePeriod && !isGrounded && jumpJustPressed && cooldownGate && underJumpLimit;

        if (wantsAirJump)
        {
            if (airJumpCount == 0)
            {
                Player.velocity.Y = -RacePeriodRules.JumpSpeed;
                Player.jump = RacePeriodRules.JumpHeight;
            }
            else
            {
                Player.velocity.X *= 0.66f;
                Player.velocity.Y = -RacePeriodRules.JumpSpeed * RacePeriodRules.AirJumpHeightMultiplier;
                Player.jump = 0;
                SpawnJumpPoof();

                if (airJumpCount > 1)
                    jumpCooldown = RacePeriodRules.AirJumpCooldownFrames;
            }

            airJumpCount++;
        }

        wasJumpPressed = Player.controlJump;
        wasAirborne = !isGrounded;
    }

    /// <summary>
    /// Every animal races in the player's own hitbox, whatever its artwork is. Vanilla sizes the
    /// player from the mount's height boost, which would leave a horse rider 14px taller than a
    /// bunny rider and too big for gaps the bunny clears. Undoing it keeps the feet planted exactly
    /// the way Player.ResizeHitbox does when a boost goes away, and both draw paths anchor the
    /// animal to the player's bottom, so no artwork moves with it.
    /// </summary>
    private void ResetMountHitbox()
    {
        int boost = Player.height - Player.defaultHeight;
        if (boost == 0)
            return;

        Player.position.Y += boost;
        Player.height = Player.defaultHeight;
    }

    private void SpawnJumpPoof()
    {
        for (int i = 0; i < 6; i++)
        {
            Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height,
                DustID.Cloud, 0f, 0f, 100, default, 1.5f);
            dust.velocity *= 0.4f;
            dust.velocity.Y -= Main.rand.NextFloat(0.5f, 1.5f);
            dust.noGravity = false;
            dust.fadeIn = 0.5f;
        }
    }

    public override void PostUpdateRunSpeeds()
    {
        if (!InRacePeriod)
            return;

        ResetMountHitbox();

        float fullSpeed = RacePeriodRules.FullRunSpeed;
        float baseSpeed = RacePeriodRules.BaseRunSpeed;
        float fastAccel = RacePeriodRules.RunAcceleration;
        float currentAbsSpeed = Math.Abs(Player.velocity.X);

        if (isGrounded)
        {
            bool pressingOpposite = (Player.controlRight && Player.velocity.X < 0f)
                                 || (Player.controlLeft && Player.velocity.X > 0f);

            Player.maxRunSpeed = fullSpeed;
            Player.accRunSpeed = fullSpeed;
            Player.runAcceleration = pressingOpposite || currentAbsSpeed < baseSpeed
                ? fastAccel
                : RacePeriodRules.SlowRunAcceleration;
            Player.runSlowdown = RacePeriodRules.RunSlowdown;
        }
        else if (airJumpCount <= 1)
        {
            Player.maxRunSpeed = Math.Max(baseSpeed, currentAbsSpeed);
            Player.accRunSpeed = Player.maxRunSpeed;
            Player.runAcceleration = fastAccel;
            Player.runSlowdown = 0f;
        }
        else
        {
            float floorCap = fullSpeed * 5f / 40f;
            Player.maxRunSpeed = Math.Max(floorCap, currentAbsSpeed);
            Player.accRunSpeed = Player.maxRunSpeed;
            Player.runAcceleration = fastAccel;
        }

        Player.noFallDmg = true;

        // Vanilla has copied the mount's own jump stats onto the player by now, so overwriting them
        // here makes the ground jump match the air jumps above for every animal.
        Player.jumpSpeed = RacePeriodRules.JumpSpeed;
        Player.jumpHeight = RacePeriodRules.JumpHeight;
    }

    public override void PostUpdate()
    {
        if (!IsRacing)
            return;

        // Flight is refilled on the ground and spent in the air, so clearing it at the end of every
        // tick removes flight and hover from whatever animal a game mode supplies. Charged
        // abilities go the same way. The shared MountData every player renders from is untouched.
        Mount mount = Player.mount;
        mount._flyTime = 0;
        mount._abilityCharge = 0;
        mount._abilityCharging = false;
        mount._abilityActive = false;
    }

    public override void PostUpdateMiscEffects()
    {
        bool isMounted = raceMountType >= 0 && Player.mount.Active && Player.mount.Type == raceMountType;

        if (!FeatureEnabled)
        {
            if (raceMountApplied && isMounted)
                Player.mount.Dismount(Player);

            ClearRaceState();
            wasInRaceWindow = false;
            return;
        }

        // A window that reopens means the previous match ended. Drop the mount and its protections
        // so neither can carry into the next race and the fresh selection is read on re-entry.
        bool inWindow = InRaceWindow;
        if (inWindow && !wasInRaceWindow && raceMountApplied)
        {
            if (isMounted)
                Player.mount.Dismount(Player);

            ClearRaceState();
            isMounted = false;
        }

        wasInRaceWindow = inWindow;

        if (raceMountApplied)
        {
            // A server-verified Spirit Animal can arrive just after the player entered the box.
            // Follow that selection during pre-game, but freeze it once the gates open.
            if (RaceWindowActive)
            {
                int selectedMountType = RacePeriodRules.GetMountType(Player);
                RacePeriodSpriteSheet? selectedSpriteSheet = RacePeriodRules.GetSpriteSheet(Player);
                if (selectedMountType != raceMountType || selectedSpriteSheet != raceSpriteSheet)
                {
                    if (isMounted && selectedMountType != raceMountType)
                        Player.mount.Dismount(Player);

                    raceMountType = selectedMountType;
                    raceSpriteSheet = selectedSpriteSheet;
                    if (!Player.mount.Active || Player.mount.Type != raceMountType)
                        Player.mount.SetMount(raceMountType, Player);

                    isMounted = true;
                }
            }

            if (!isMounted)
                ClearRaceState();
            return;
        }

        // Selection is captured once on entry. Changing cosmetics cannot swap a live race mount.
        if (!RaceWindowActive)
            return;

        raceMountType = RacePeriodRules.GetMountType(Player);
        raceSpriteSheet = RacePeriodRules.GetSpriteSheet(Player);
        if (!Player.mount.Active || Player.mount.Type != raceMountType)
            Player.mount.SetMount(raceMountType, Player);

        raceMountApplied = true;
    }

    private void ClearRaceState()
    {
        raceMountApplied = false;
        raceMountType = -1;
        raceSpriteSheet = null;
        airJumpCount = 0;
        jumpCooldown = 0;
    }
}

/// <summary>Locks down item usage while a player is riding out the race period.</summary>
public sealed class RacePeriodItemBlock : GlobalItem
{
    private static bool Restricted(Player player) =>
        player.GetModPlayer<RacePeriodPlayer>().InRacePeriod;

    public override bool CanUseItem(Item item, Player player) => !Restricted(player);

    public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded) =>
        !Restricted(player);

    public override bool? CanBeChosenAsAmmo(Item ammo, Item weapon, Player player) =>
        Restricted(player) ? false : null;
}

/// <summary>Keeps enemies from spawning on players stuck in the race period.</summary>
public sealed class RacePeriodSpawnSuppressor : GlobalNPC
{
    public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
    {
        if (player.GetModPlayer<RacePeriodPlayer>().InRacePeriod)
            maxSpawns = 0;
    }
}
