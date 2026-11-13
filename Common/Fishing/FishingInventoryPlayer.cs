using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Fishing;

internal sealed class FishingInventoryPlayer : ModPlayer
{
    private const int MouseItemSlot = 58;
    private int castSlot;
    private Item castRod;

    public override void Load() => On_Player.dropItemCheck += KeepRodSelected;

    public override void Unload() => On_Player.dropItemCheck -= KeepRodSelected;

    internal void RecordCast(Item rod)
    {
        if (Player.selectedItem == MouseItemSlot || !ReferenceEquals(Player.HeldItem, rod))
            return;

        castSlot = Player.selectedItem;
        castRod = rod;
    }

    private static void KeepRodSelected(On_Player.orig_dropItemCheck orig, Player player)
    {
        if (player.whoAmI != Main.myPlayer || Main.mouseItem.IsAir)
        {
            orig(player);
            return;
        }

        var fishing = player.GetModPlayer<FishingInventoryPlayer>();
        if (fishing.castRod == null)
        {
            orig(player);
            return;
        }

        if (!fishing.IsCastingRodSelected() || PlayerInput.InBuildingMode || PlayerInput.ShouldFastUseItem)
        {
            fishing.EndCast();
            orig(player);
            return;
        }

        // Let vanilla process the cursor stack, including returning or dropping it.
        orig(player);
        if (player.selectedItem != MouseItemSlot || !fishing.HasBobber())
            return;

        player.selectedItem = fishing.castSlot;
        // The cursor item is being organized. Keep raw mouse input available to the UI.
        if (player.controlUseItem)
            player.delayUseItem = true;
        player.controlUseItem = false;
        player.controlUseTile = false;
    }

    public override bool CanUseItem(Item item) =>
        Player.whoAmI != Main.myPlayer || Main.mouseItem.IsAir || !ReferenceEquals(item, castRod);

    public override bool PreItemCheck()
    {
        // Hotbar and radial selection can change the active item after dropItemCheck.
        if (castRod != null && !IsCastingRodSelected())
            EndCast();
        return true;
    }

    public override void PostUpdate()
    {
        if (castRod != null && !HasBobber())
            castRod = null;
    }

    private bool IsCastingRodSelected() => Player.selectedItem == castSlot &&
        ReferenceEquals(Player.inventory[castSlot], castRod) && castRod.fishingPole > 0;

    private bool HasBobber()
    {
        foreach (Projectile projectile in Main.ActiveProjectiles)
            if (projectile.owner == Player.whoAmI && projectile.bobber)
                return true;
        return false;
    }

    private void EndCast()
    {
        castRod = null;
        Player.RemoveAllFishingBobbers();
    }
}

internal sealed class FishingInventoryBobber : GlobalProjectile
{
    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (projectile.bobber && projectile.owner == Main.myPlayer &&
            source is EntitySource_ItemUse { Entity: Player player, Item.fishingPole: > 0 } itemUse)
        {
            player.GetModPlayer<FishingInventoryPlayer>().RecordCast(itemUse.Item);
        }
    }
}
