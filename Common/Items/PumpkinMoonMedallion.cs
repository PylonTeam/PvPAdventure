using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Items
{
    /// <summary>
    /// Turns the Pumpkin Moon Medallion into a boss summon that spawns the mourning wood in forests and the pumpking anywhere else.
    /// </summary>
    public class PumpkinMoonMedallionTweaks : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation) =>
            entity.type == ItemID.PumpkinMoonMedallion;

        public override void Load()
        {
            On_Player.ItemCheck_UseEventItems += SkipVanillaMedallion;
        }

        private static void SkipVanillaMedallion(On_Player.orig_ItemCheck_UseEventItems orig, Player self, Item sItem)
        {
            if (sItem.type == ItemID.PumpkinMoonMedallion)
                return;
            orig(self, sItem);
        }

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MPAllowedEnemies[NPCID.MourningWood] = true;
            NPCID.Sets.MPAllowedEnemies[NPCID.Pumpking] = true;
        }

        private static int GetBossType(Player player) =>
            player.ZoneForest ? NPCID.MourningWood : NPCID.Pumpking;

        public override bool CanUseItem(Item item, Player player)
        {
            if (Main.dayTime)
                return false;

            if (!player.ZoneOverworldHeight)
                return false;

            return !NPC.AnyNPCs(GetBossType(player));
        }

        public override bool? UseItem(Item item, Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return true;

            int bossType = GetBossType(player);
            SoundEngine.PlaySound(SoundID.Roar, player.position);

            if (Main.netMode != NetmodeID.MultiplayerClient)
                NPC.SpawnOnPlayer(player.whoAmI, bossType);
            else
                NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: bossType);

            return true;
        }
    }
}