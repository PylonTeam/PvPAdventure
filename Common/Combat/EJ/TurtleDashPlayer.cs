using PvPAdventure.Content.Buffs;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static Terraria.ModLoader.BackupIO;

namespace PvPAdventure.Common.Combat.EJ;

public class HeavyArmorPlayer : ModPlayer
{
    private bool isWearingFullHeavyarmor = false;
    private int dashingTimer = 0;

    // Detect if player is in a dash state
    public bool IsInADashState => (Player.dashDelay == -1 || dashingTimer > 0) && Player.grapCount <= 0;

    public override void PostUpdateEquips()
    {
        bool wearingTurtle = Player.armor[0].type == ItemID.TurtleHelmet &&
                             Player.armor[1].type == ItemID.TurtleScaleMail &&
                             Player.armor[2].type == ItemID.TurtleLeggings;

        bool wearingBeetleShell = Player.armor[0].type == ItemID.BeetleHelmet &&
                                  Player.armor[1].type == ItemID.BeetleShell &&
                                  Player.armor[2].type == ItemID.BeetleLeggings;

        isWearingFullHeavyarmor = wearingTurtle; //|| wearingBeetleShell;

        if (isWearingFullHeavyarmor)
        {
            //Player.AddBuff(ModContent.BuffType<BROISACHOJ>(), 1 * 60 * 60);

            Player.GetAttackSpeed<MeleeDamageClass>() -= 0.2f;
        }
    }

    public override void PostUpdate()
    {

        if (Player.dashDelay == -1)
        {
            dashingTimer = 10;
        }
        else if (dashingTimer > 0)
        {
            dashingTimer--;
        }

        // Apply dash speed reduction if player has the debuff and is dashing
        if (Player.HasBuff(ModContent.BuffType<BROISACHOJ>()) && IsInADashState)
        {
            float dashSpeedReduction = Player.velocity.X * 0.022f;
            Player.velocity.X -= dashSpeedReduction;
        }
        //thanks mr fargo
    }

}