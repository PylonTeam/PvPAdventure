using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Travel;
using PvPAdventure.Common.Travel.Portals;
using PvPAdventure.Core.Config;
using PvPAdventure.Core.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Content.Portals;

/// <summary>
/// An item that creates a portal for players to teleport between locations in the world.
/// Thin trigger item only.
/// </summary>
public class PortalCreatorItem : ModItem
{
    public override string Texture => "PvPAdventure/Assets/Portals/PortalCreator_NoTeam";

    public static int GetCreationTimeFrames()
    {
        ServerConfig.TravelSystemConfig config = ModContent.GetInstance<ServerConfig>().TravelSystem;
        int seconds = config.TravelPortalCreationTimePreHardmodeSeconds;

        if (Main.hardMode)
            seconds = config.TravelPortalCreationTimeHardmodeSeconds;

        return Math.Max(0, seconds * 60);
    }

    #region Item defaults
    /// <summary>
    /// Initialize the time it takes to create portal
    /// </summary>
    public static void SetPortalCreationTime(Item item)
    {
        int recallFrames = GetCreationTimeFrames();
        int animationFrames = Math.Max(1, recallFrames + 3);
        item.useTime = animationFrames;
        item.useAnimation = animationFrames;
    }

    public override void SetDefaults()
    {
        SetPortalCreationTime(Item);

        Item.width = 42;
        Item.height = 46;

        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.UseSound = SoundID.Item6;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.buyPrice(gold: 0);
        Item.noUseGraphic = false;
    }
    public override bool ConsumeItem(Player player) => false;

    public static Vector2 GetPortalWorldPosition(Player player)
    {
        float distance = ModContent.GetInstance<ServerConfig>().TravelSystem.PortalCreationOffset;
        return player.Bottom + new Vector2(player.direction * distance, 0f);
    }

    #endregion

    public override bool? UseItem(Player player)
    {
        if (player?.whoAmI != Main.myPlayer)
            return true;

        if (!CanCreatePortal(player, true))
            return false;

        int requestId = player.GetModPlayer<PortalPlayer>().CreationAttempt.Begin();
        if (Main.netMode == NetmodeID.MultiplayerClient)
            PortalNetHandler.SendPortalCreatorUse(player.selectedItem, requestId);
        else
            PortalSystem.StartPortalCreation(player, requestId);

        return true;
    }

    public static void ResetUseState(Player player)
    {
        player.controlUseItem = false;
        player.releaseUseItem = false;
        player.channel = false;
        player.itemAnimation = 0;
        player.itemAnimationMax = 0;
        player.itemTime = 0;
        player.itemTimeMax = 0;
        player.reuseDelay = 0;
    }

    public static bool IsPortalCreationInterrupted(Player player)
    {
        if (player?.active != true)
            return true;

        return player.controlLeft
            || player.controlRight
            || player.controlJump
            || player.controlUp
            || player.controlDown
            || player.controlHook
            || player.controlMount
            || player.dashDelay == -1
            || player.eocDash > 0
            || player.solarDashing;
    }

    public override bool CanRightClick() => true;
    public override bool AltFunctionUse(Player player) => false;
    public override void RightClick(Player player) => TryUse(player);

    /// <summary>
    /// Tries to use the item. Called by <see cref="KeybindsPlayer"/> and ourselves.
    /// </summary>
    public static void TryUse(Player player)
    {
        if (player?.whoAmI != Main.myPlayer)
            return;

        if (!TravelRules.Enabled)
        {
            Warning(player, "Mods.PvPAdventure.PortalCreator.TravelDisabledInConfig");
            return;
        }

        for (int i = 0; i < player.inventory.Length; i++)
        {
            if (player.inventory[i].ModItem is PortalCreatorItem creator)
            {
                creator.TryStartUse(player, i);
                return;
            }
        }
    }

    private void TryStartUse(Player player, int itemSlotIndex)
    {
        SetPortalCreationTime(player.inventory[itemSlotIndex]);

        if (!CanUseItem(player) || player.CCed || player.itemAnimation > 0 || player.reuseDelay > 0)
            return;

        BeginUse(player, itemSlotIndex);
    }

    private bool BeginUse(Player player, int index)
    {
        if (!TravelRules.Enabled)
            return false;

        Item item = player.inventory[index];

        player.selectedItem = index;
        player.controlUseItem = true;
        player.releaseUseItem = true;

        player.ItemCheck();

        player.itemAnimation = item.useAnimation;
        player.itemAnimationMax = item.useAnimation;
        player.itemTime = item.useTime;
        player.itemTimeMax = item.useTime;

        player.controlUseItem = false;
        player.releaseUseItem = false;
        player.channel = false;

        return player.itemAnimation > 0;
    }

    public override void UseStyle(Player player, Rectangle heldItemFrame)
    {
        base.UseStyle(player, heldItemFrame);

        player.controlUseItem = false;
        player.channel = false;

        Vector2 offset = new(player.direction * -9f, -9f);
        player.itemLocation += offset;

        if (IsPortalCreationInterrupted(player))
        {
            if (player.whoAmI == Main.myPlayer)
                player.GetModPlayer<PortalPlayer>().CancelCreation();

            ResetUseState(player);
            return;
        }

        DrawPortalCreatorUseVisuals(player, Math.Max(0, player.itemAnimation - 3));
    }

    public override void UseItemFrame(Player player)
    {
        return;
        if (Main.dedServ ||
            !PortalCreatorSkin.TryGetTexture(Item, player.team, out _))
            return;

        const float armAngleDegrees = -90f;

        player.SetCompositeArmFront(
            true,
            Player.CompositeArmStretchAmount.Full,
            MathHelper.ToRadians(armAngleDegrees) * player.direction);
    }

    #region Drawing
    public override bool PreDrawInInventory(SpriteBatch sb, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
    {
        const float itemScale = 0.6f;

        int team = Main.LocalPlayer?.team ?? 0;
        Texture2D texture = PortalAssets.GetCreatorTexture(team);
        Rectangle source = frame;
        Vector2 pivot = origin;

        if (PortalCreatorSkin.TryGetTexture(Item, team, out Texture2D skin))
        {
            (source, pivot) = PortalCreatorSkin.Rescale(frame, origin, texture, skin);
            texture = skin;
        }

        sb.Draw(texture, position, source, drawColor, 0f, pivot, itemScale, SpriteEffects.None, 0f);
        return false;
    }

    public override bool PreDrawInWorld(SpriteBatch sb, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
    {
        int team = Main.LocalPlayer?.team ?? 0;
        Texture2D texture = PortalCreatorSkin.TryGetTexture(Item, team, out Texture2D skin)
            ? skin
            : PortalAssets.GetCreatorTexture(team);

        Rectangle frame = texture.Frame();
        Vector2 origin = frame.Size() * 0.5f;
        Vector2 position = Item.Center - Main.screenPosition;

        sb.Draw(texture, position, frame, lightColor, rotation, origin, scale, SpriteEffects.None, 0f);
        return false;
    }
    #endregion

    #region Popup text
    public override bool CanUseItem(Player player)
    {
        SetPortalCreationTime(Item);
        return CanCreatePortal(player, true);
    }

    public static bool CanCreatePortal(Player player, bool warn)
    {
        if (player?.active != true)
            return false;

        if (!TravelRules.Enabled)
        {
            if (warn) Warning(player, "Mods.PvPAdventure.PortalCreator.TravelDisabledInConfig");
            return false;
        }

        if (player.dead || player.ghost)
            return false;

        if (ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Playing)
        {
            if (warn) Warning(player, "Mods.PvPAdventure.PortalCreator.GameNotStarted");
            return false;
        }

        if (TravelRegions.IsInWorldSpawn(player))
        {
            if (warn) Warning(player, "Mods.PvPAdventure.PortalCreator.CannotUseInSpawn");
            return false;
        }

        if (player.velocity.LengthSquared() > 0.1f)
        {
            if (warn) Warning(player, "Mods.PvPAdventure.PortalCreator.CannotUseWhileMoving");
            return false;
        }

        return true;
    }
    internal static void Warning(Player player, string localizationKey, Color color = default)
    {
        PopupTextHelper.NewLocalizedText(player, localizationKey, color);
    }
    #endregion

    #region Visual effects
    // Visual dust effects during the countdown
    private static void DrawPortalCreatorUseVisuals(Player player, int framesLeft)
    {
        if (framesLeft <= 0)
            return;

        Color color = Main.teamColor[player.team];

        if (Main.rand.NextBool())
        {
            int dust = Dust.NewDust(player.position, player.width, player.height, DustID.MagicMirror, player.velocity.X * 0.5f, player.velocity.Y * 0.5f, 150, color, 1.5f);
            Main.dust[dust].noGravity = true;
        }

        if (framesLeft % 60 != 0)
            return;

        int secondsLeft = (framesLeft + 59) / 60;

        if (secondsLeft < 1)
            return;

        PopupTextHelper.NewText(player, secondsLeft.ToString(), color);
    }
    #endregion

    #region Misc

    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        //int seconds = ModContent.GetInstance<ServerConfig>().TravelPortalCreationTimeSeconds;
        int seconds = PortalCreatorItem.GetCreationTimeFrames() / 60;

        string key = Keybinds.UsePortalCreatorLabel;

        tooltips.Add(new TooltipLine(Mod, "PortalCreatorAdventure", "Opens up a portal to go on adventures"));
        tooltips.Add(new TooltipLine(Mod, "PortalCreatorEffect", $"Takes {seconds} second{(seconds == 1 ? "" : "s")} to use"));
        tooltips.Add(new TooltipLine(Mod, "PortalCreatorCancel", "Moving cancels use"));
        tooltips.Add(new TooltipLine(Mod, "PortalCreatorControls", key is null ? "[c/555555:Bind a key to use]" : $"Press {key} to use"));
    }

    public override void UpdateInventory(Player player)
    {
        // Force favorite every tick
        // A little redundant now that we disallowed unfavorite in ItemSlotHooks left click hook
        Item.favorited = true;
    }
    #endregion
}
