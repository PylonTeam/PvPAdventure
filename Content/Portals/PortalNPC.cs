using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Common.Travel;
using PvPAdventure.Common.Travel.Beds;
using PvPAdventure.Common.Travel.Portals;
using PvPAdventure.Core.Config;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Map;
using Terraria.ModLoader;

namespace PvPAdventure.Content.Portals;

public sealed class PortalNPC : ModNPC
{
    public static int PortalMaxHealth => NPC.downedPlantBoss ? 300 : Main.hardMode ? 150 : 50;
    public const int PortalWidth = 36;
    public const int PortalHeight = 46;
    private string ownerName = string.Empty;

    public override string Texture => "PvPAdventure/Assets/Portals/Portal_NoTeam";
    public override LocalizedText DisplayName => base.DisplayName;
    public override bool CheckActive() => false;

    private string GetOwnerName()
    {
        if (!string.IsNullOrWhiteSpace(ownerName))
            return ownerName;

        if (TryGetOwner(out Player owner) && !string.IsNullOrWhiteSpace(owner.name))
            return owner.name;

        return "Unknown Player";
    }

    public int OwnerIndex
    {
        get => (int)NPC.ai[0];
        private set => NPC.ai[0] = value;
    }

    public int OwnerTeam
    {
        get => Math.Clamp((int)NPC.ai[1], 0, Main.teamColor.Length - 1);
        private set => NPC.ai[1] = Math.Clamp(value, 0, Main.teamColor.Length - 1);
    }

    public Vector2 WorldPosition => NPC.Bottom;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 8;
        NPCID.Sets.ImmuneToAllBuffs[Type] = true;
    }

    public override void SetDefaults()
    {
        NPC.width = PortalWidth;
        NPC.height = PortalHeight;
        NPC.lifeMax = PortalMaxHealth;
        NPC.life = NPC.lifeMax;
        NPC.damage = 0;
        NPC.defense = 0;
        NPC.knockBackResist = 0f;
        NPC.aiStyle = -1;
        NPC.noGravity = true;
        NPC.noTileCollide = true;
        NPC.npcSlots = 0f;
        NPC.value = 0f;
        NPC.chaseable = false;
        NPC.friendly = false;
        NPC.netAlways = true;
        NPC.ShowNameOnHover = true;
    }

    internal void Initialize(Player owner, Vector2 worldPos)
    {
        OwnerIndex = owner.whoAmI;
        OwnerTeam = owner.team;
        ownerName = owner.name ?? string.Empty;

        NPC.GivenName = $"{GetOwnerName()}'s Portal";
        NPC.lifeMax = PortalMaxHealth;
        NPC.life = NPC.lifeMax;
        NPC.position = worldPos - new Vector2(NPC.width * 0.5f, NPC.height);
        NPC.velocity = Vector2.Zero;
        NPC.netUpdate = true;
    }

    public override void AI()
    {
        NPC.velocity = Vector2.Zero;

        if (Main.netMode != NetmodeID.Server)
        {
            if (NPC.localAI[0] == 0f) // creation-burst flag
            {
                NPC.localAI[0] = 1f;
                PortalDrawer.SpawnFinalPortalCreationBurst(NPC.Center, OwnerTeam);
            }

            if (NPC.localAI[1] > 0f) // local-only visual timer
                NPC.localAI[1]--;

            PortalDrawer.SpawnPortalDust(NPC.Bottom, 1f);

            Player player = Main.LocalPlayer;

            if (NPC.Hitbox.Contains(Main.MouseWorld.ToPoint()) && player.Distance(NPC.Center) < 200f)
            {
                player.noThrow = 2;
                player.cursorItemIconEnabled = true;
                player.cursorItemIconID = -1;

                if (Main.mouseRight && Main.mouseRightRelease)
                {
                    Main.mouseRightRelease = false;
                    player.TryOpeningFullscreenMap();
                }
            }
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (!TryGetOwner(out Player owner))
        {
            PortalSystem.ClearPortal(OwnerIndex);
            return;
        }

        if (ownerName != owner.name)
        {
            ownerName = owner.name ?? string.Empty;
            NPC.GivenName = $"{GetOwnerName()}'s Portal";
            NPC.netUpdate = true;
        }

        if (owner.dead)
        {
            PortalSystem.ClearPortal(owner.whoAmI);
            return;
        }

        if (OwnerTeam != owner.team)
        {
            OwnerTeam = owner.team;
            NPC.netUpdate = true;
        }

        int currentMaxHealth = PortalMaxHealth;
        if (NPC.lifeMax != currentMaxHealth)
        {
            NPC.lifeMax = currentMaxHealth;
            NPC.life = NPC.lifeMax;
            NPC.netUpdate = true;
        }
    }

    public override void FindFrame(int frameHeight)
    {
        int frameCount = Math.Max(1, Main.npcFrameCount[Type]);
        int frame = (int)(Main.GameUpdateCount / 5 % frameCount);
        NPC.frame = new Rectangle(0, frame * frameHeight, NPC.width, frameHeight);
    }

    public override void OnKill()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        PortalSystem.ClearCreationProjectiles(OwnerIndex);

        if (Main.netMode == NetmodeID.Server)
            TeamBedNetHandler.SendBedDestructionFx(WorldPosition.X, WorldPosition.Y, killed: true);

        if (TryGetOwner(out Player owner))
            TeleportChat.AnnouncePortalDestroyed(owner, GetOwnerName());

        // Award points
        if (_lastDamagePlayer >= 0 && _lastDamagePlayer < Main.maxPlayers)
        {
            Player last = Main.player[_lastDamagePlayer];
            if (last != null && last.active && (Team)last.team != Team.None)
            {
                MatchStatsPlayer.RecordServerStat(last, MatchStatKey.PortalKills);

                var cfg = ModContent.GetInstance<ServerConfig>();
                int portalPoints = cfg.Points.PortalKill;
                if (portalPoints != 0)
                {
                    ModContent.GetInstance<PointsManager>().AwardPointsToTeam((Team)last.team, portalPoints, NPC.Center);
                }
            }
        }
    }

    private bool TryGetOwner(out Player owner)
    {
        owner = OwnerIndex >= 0 && OwnerIndex < Main.maxPlayers ? Main.player[OwnerIndex] : null;
        return owner?.active == true;
    }

    #region Hit / damage from melee & projectiles
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool CanBeHitByNPC(NPC attacker) => false;
    private int _lastDamagePlayer = -1; // the whoAmI of the last player that damaged this portal
    private const int MeleeHitCooldown = 20;
    private const int ProjectileHitCooldown = 10;
    private const int PortalHitFlashFrames = 6;

    private static void NormalizePortalHit(ref NPC.HitModifiers modifiers)
    {
        modifiers.DefenseEffectiveness *= 0f;
        modifiers.DamageVariationScale *= 0f;
        modifiers.DisableCrit();
        modifiers.DisableKnockback();
        //modifiers.HideCombatText();
    }

    public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers)
    {
        NormalizePortalHit(ref modifiers);
    }

    public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
    {
        NormalizePortalHit(ref modifiers);
    }

    public override bool? CanBeHitByItem(Player player, Item item)
    {
        if (item == null || item.IsAir || item.damage <= 0 || item.noMelee)
            return false;

        if (player?.active != true || player.whoAmI < 0 || player.whoAmI >= Main.maxPlayers)
            return false;

        if (IsFriendlyPlayer(player))
            return false;

        return NPC.immune[player.whoAmI] <= 0;
    }

    public override bool? CanBeHitByProjectile(Projectile projectile)
    {
        if (projectile == null || !projectile.active || !projectile.friendly || projectile.hostile || projectile.npcProj || projectile.damage <= 0)
            return false;

        if (projectile.type is ProjectileID.Bee or ProjectileID.GiantBee)
            return false;

        if (projectile.owner < 0 || projectile.owner >= Main.maxPlayers)
            return false;

        if (Main.player[projectile.owner] is not { active: true } owner || IsFriendlyPlayer(owner))
            return false;

        return NPC.immune[projectile.owner] <= 0;
    }

    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        if (player != null)
            _lastDamagePlayer = player.whoAmI;
        PortalNetHandler.SendPortalDamageCredit(NPC.whoAmI);

        NPC.immune[player.whoAmI] = MeleeHitCooldown;
        PlayHitFxAndSync(NPC.life <= 0);
    }

    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (projectile != null)
            _lastDamagePlayer = projectile.owner;
        PortalNetHandler.SendPortalDamageCredit(NPC.whoAmI);

        NPC.immune[projectile.owner] = ProjectileHitCooldown;
        PlayHitFxAndSync(NPC.life <= 0);
    }

    private void TriggerHitFlash()
    {
        if (Main.dedServ || NPC.life <= 0)
            return;

        NPC.localAI[1] = PortalHitFlashFrames;
    }

    private void PlayHitFxAndSync(bool killed)
    {
        PlayHitFxFromNetwork(killed);
        PortalNetHandler.SendPortalHitFx(NPC.whoAmI, killed);
    }

    internal void PlayHitFxFromNetwork(bool killed)
    {
        TriggerHitFlash();
        PlayPortalFx(WorldPosition, killed);
    }

    public static void PlayPortalFx(Vector2 worldPos, bool killed, int damage = 0)
    {
        if (Main.dedServ)
            return;

        //if (damage > 0)
        //{
        //    CombatText.NewText(GetPortalHitbox(worldPos), CombatText.DamagedHostile, damage);
        //}

        if (!killed)
        {
            SoundEngine.PlaySound(SoundID.NPCHit4, worldPos);
            return;
        }

        SoundEngine.PlaySound(SoundID.NPCDeath6, worldPos);

        for (int i = 0; i < 42; i++)
        {
            Vector2 velocity = Main.rand.NextVector2Circular(3.5f, 3.5f);
            Dust.NewDustPerfect(worldPos + Main.rand.NextVector2Circular(24f, 36f), DustID.MagicMirror, velocity, 120, Color.White, Main.rand.NextFloat(1.1f, 1.8f));
        }
    }

    internal void SetLastDamagePlayerFromNetwork(int playerId)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId] is not { active: true } player)
            return;

        if (IsFriendlyPlayer(player))
            return;

        _lastDamagePlayer = playerId;
    }

    private bool IsFriendlyPlayer(Player player)
    {
        // Return false to allow testing damage in debug mode, even if the player is friendly.
//#if DEBUG
        //return false;
//#endif

        if (player?.active != true)
            return false;

        if (player.whoAmI == OwnerIndex)
            return true;

        return player.team > 0 && player.team == OwnerTeam;
    }

    #endregion

    
    #region Drawing
    public override bool PreDraw(SpriteBatch sb, Vector2 screenPos, Color drawColor)
    {
        //return true;
        Texture2D texture = PortalAssets.GetPortalTexture(OwnerTeam);

        Rectangle frame = NPC.frame;
        Vector2 origin = frame.Size() * 0.5f;
        Vector2 position = NPC.Center - screenPos;

        var config = ModContent.GetInstance<ServerConfig>();
        //const float interactionRange = 160; // TODO: Use config value here, similar to beds!
        float interactionRange = config.TravelSystem.TravelRegionRadiusTiles * 16;
        float interactionRangeSquared = interactionRange * interactionRange;
        bool isMyTeam = Main.LocalPlayer.team == OwnerTeam;

        float distanceSquared = Vector2.DistanceSquared(Main.LocalPlayer.Center, NPC.Center);
        bool withinRange = distanceSquared < interactionRangeSquared;
        float opacity = (isMyTeam && !withinRange) ? 0.5f : 1f;
        SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        // Draw portal
        sb.Draw(texture, position, frame, Color.White * opacity, NPC.rotation, origin, NPC.scale, effects, 0f);

        // Draw hitflash
        float t = NPC.localAI[1] / PortalHitFlashFrames; // 1 at start, 0 at end
        float hitFlash;
        if (t > 0.75f)
            hitFlash = (1f - t) / 0.25f;        // fade in: 0→1
        else
            hitFlash = t / 0.75f;               // fade out: 1→0

        if (hitFlash > 0f)
        {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color flashColor = Color.White * hitFlash;

            sb.Draw(texture, position, frame, flashColor, NPC.rotation, origin, 1, effects, 0f);
            sb.Draw(texture, position, frame, flashColor, NPC.rotation, origin, 1, effects, 0f);
            sb.Draw(texture, position, frame, flashColor, NPC.rotation, origin, 1, effects, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Draw healthbar
        PortalDrawer.DrawPortalHealthBar(sb, NPC.Center + new Vector2(0f, 24f * NPC.scale), NPC.life, NPC.lifeMax, NPC.scale, 1f);

        // Draw hover icon if player is within range
        if (!NPC.Hitbox.Contains(Main.MouseWorld.ToPoint()))
            return false;

        if (!withinRange)
            return false;

        if (isMyTeam && TryGetOwner(out Player owner))
            PortalDrawer.DrawHoverIcon(sb, owner, NPC.Center, Main.teamColor[OwnerTeam], 1f);

        return false;
    }

    public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

    #endregion

    #region Netcode
    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(ownerName ?? string.Empty);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        ownerName = reader.ReadString();
        NPC.GivenName = $"{GetOwnerName()}'s Portal";
    }
    #endregion
}
