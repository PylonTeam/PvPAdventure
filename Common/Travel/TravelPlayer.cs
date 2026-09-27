using Microsoft.Xna.Framework;
using PvPFramework.Common.Combat;
using PvPAdventure.Common.Travel.Portals;
using PvPAdventure.Content.Portals;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel;

internal class TravelPlayer : ModPlayer
{
    public int TeleportCooldownFrames;

    public override void PostUpdate()
    {
        if (TeleportCooldownFrames > 0)
            TeleportCooldownFrames--;
    }

    public override void OnHurt(Player.HurtInfo info)
    {
        if (Player.whoAmI != Main.myPlayer)
            return;

        if (!PortalSystem.IsCreatingPortal(Player))
            return;

        if (!ShouldCancelPortalUse(info))
            return;

        Player.GetModPlayer<PortalPlayer>().CancelCreation();
    }

    private static bool ShouldCancelPortalUse(Player.HurtInfo info)
    {
        if (info.PvP)
        {
            Log.Chat("PvP damage while creating portal, cancelling portal use");
            return true;
        }

        bool isBossProjectile = info.DamageSource is not null
            && info.DamageSource.TryGetCausingEntity(out Entity entity)
            && entity is Projectile projectile
            && CombatManager.IsBossProjectile(projectile);

        if (isBossProjectile)
            Log.Chat("Boss projectile while creating portal, cancelling portal use");

        return isBossProjectile;
    }

    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        if (Player.whoAmI == Main.myPlayer)
            TravelTeleportSystem.ClearSelection();

        if (Main.netMode != NetmodeID.MultiplayerClient)
            PortalSystem.ClearPortal(Player.whoAmI);
    }

    public override void UpdateDead()
    {
        if (Player.whoAmI != Main.myPlayer)
        {
            base.UpdateDead();
            return;
        }

        // Uhh
        if (!TravelTeleportSystem.ShouldUseDeathTravelSelection(Player))
        {
            base.UpdateDead();
            return;
        }

        if (Player.respawnTimer > 2)
        {
            base.UpdateDead();
            return;
        }

        TravelTeleportSystem.EnsureDeathTravelSelection(Player);

        base.UpdateDead();
    }
}
