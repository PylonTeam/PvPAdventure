using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Combat.EJ;

/// <summary>
/// Modifies the damage of certain projectiles against specific bosses.
/// </summary>
public class CursedDartDestroyer : GlobalProjectile
{
    public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (projectile.type == ProjectileID.CursedDartFlame &&
            (target.type == NPCID.TheDestroyer || target.type == NPCID.TheDestroyerBody || target.type == NPCID.TheDestroyerTail))
        {
            modifiers.SourceDamage *= 0.66f;
        }
    }
}
public class NoMoreDynaWoF : GlobalProjectile
{
    public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
    {
        if ((projectile.type == ProjectileID.Dynamite || projectile.type == ProjectileID.StickyDynamite) &&
            (target.type == NPCID.WallofFlesh || target.type == NPCID.WallofFleshEye))
        {
            modifiers.SourceDamage *= 0f;
        }
    }
}


public class EmpressProjectiles : GlobalProjectile
{
    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
    {
        return entity.type == ProjectileID.FairyQueenLance ||
               entity.type == ProjectileID.FairyQueenSunDance ||
               entity.type == ProjectileID.HallowBossRainbowStreak ||
               entity.type == ProjectileID.HallowBossSplitShotCore ||
               entity.type == ProjectileID.HallowBossLastingRainbow;
    }
    public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
    {
        modifiers.SourceDamage *= 0.75f;
        if (Main.dayTime)
        {
            modifiers.SourceDamage *= 0.0085f;
        }
    }
}
