//using Terraria;
//using Terraria.ID;
//using Terraria.ModLoader;
//using Microsoft.Xna.Framework;

//namespace PvPAdventure.Common.Combat.EJ;
///// <summary>
///// Creates a healing aura around players using the shiny stone
///// </summary>
//internal class ShinyStonePlayer : ModPlayer
//{
//    private int _healPulseTimer = 0;
//    private int _receiveHealCooldown = 0;
//    private int _visualTimer = 0;
//    private int _stillTimer = 0;

//    private const int PulseInterval = 180;
//    private const int HealAmount = 1;
//    private const int HealInterval = 4;
//    private const float PulseRange = 20 * 16f;
//    private const int RampUpDuration = 600;

//    private bool IsActive => Player.shinyStone && Player.velocity.X == 0f && Player.velocity.Y == 0f && Player.itemAnimation == 0;

//    private int CurrentHealInterval
//    {
//        get
//        {
//            float ramp = (float)_stillTimer / RampUpDuration;
//            float slowest = HealInterval * 3f;
//            return (int)MathHelper.Lerp(slowest, HealInterval, ramp);
//        }
//    }

//    public override void PostUpdate()
//    {
//        if (IsActive)
//        {
//            _stillTimer = System.Math.Min(_stillTimer + 1, RampUpDuration);

//            _visualTimer++;
//            if (_visualTimer >= PulseInterval)
//                _visualTimer = 0;

//            SpawnCircleParticles();

//            if (_visualTimer == 0)
//                SpawnBurstParticles();
//        }
//        else
//        {
//            _stillTimer = 0;
//            _visualTimer = 0;
//        }

//        if (Player.whoAmI != Main.myPlayer)
//            return;

//        if (_receiveHealCooldown > 0)
//            _receiveHealCooldown--;

//        CheckForNearbyShinyStoneHealer();

//        if (!IsActive)
//        {
//            _healPulseTimer = 0;
//            return;
//        }

//        _healPulseTimer++;

//        if (_healPulseTimer >= CurrentHealInterval)
//            _healPulseTimer = 0;
//    }

//    private void CheckForNearbyShinyStoneHealer()
//    {
//        if (Player.team == 0)
//            return;

//        if (_receiveHealCooldown > 0)
//            return;

//        for (int i = 0; i < Main.maxPlayers; i++)
//        {
//            Player other = Main.player[i];

//            if (!other.active || other.dead)
//                continue;

//            if (other.team != Player.team)
//                continue;

//            var otherModPlayer = other.GetModPlayer<ShinyStonePlayer>();

//            if (!otherModPlayer.IsActive)
//                continue;

//            if (Vector2.Distance(Player.Center, other.Center) > PulseRange)
//                continue;

//            int healAmount = HealAmount;

//            if (Player.statLife + healAmount > Player.statLifeMax2)
//                healAmount = Player.statLifeMax2 - Player.statLife;

//            if (healAmount <= 0)
//                continue;

//            Player.Heal(healAmount);
//            _receiveHealCooldown = otherModPlayer.CurrentHealInterval;
//            break;
//        }
//    }

//    private void SpawnCircleParticles()
//    {
//        int numDust = 3;
//        for (int d = 0; d < numDust; d++)
//        {
//            double angle = Main.rand.NextDouble() * MathHelper.TwoPi;
//            Vector2 offset = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle)) * PulseRange;
//            Vector2 spawnPos = Player.Center + offset;

//            int dust = Dust.NewDust(spawnPos, 0, 0, DustID.Torch, 0f, 0f, 0, new Color(255, 140, 0), 1.2f);
//            Main.dust[dust].noGravity = true;
//            Main.dust[dust].velocity = Vector2.Zero;
//            Main.dust[dust].fadeIn = 0f;
//        }
//    }

//    private void SpawnBurstParticles()
//    {
//        int numDust = 40;
//        for (int d = 0; d < numDust; d++)
//        {
//            double angle = d * (MathHelper.TwoPi / numDust);
//            Vector2 direction = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
//            Vector2 spawnPos = Player.Center + direction * PulseRange;

//            int dust = Dust.NewDust(spawnPos, 0, 0, DustID.Torch, 0f, 0f, 0, new Color(255, 180, 0), 1.8f);
//            Main.dust[dust].noGravity = true;
//            Main.dust[dust].velocity = -direction * 4f;
//        }
//    }
//}