using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Game.StatTrackers;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Common.Travel.Beds;
using PvPFramework.Common.Scoreboard;
using PvPFramework.Common.RacePeriod;
using Pylon.Common.SpiritAnimals;
using PvPFramework.Common.Visualization.TileOutlines;
using ReLogic.Content;
using AdventureAssets = PvPAdventure.Core.Utilities.Ass;
using FrameworkAssets = PvPFramework.Core.Utilities.Ass;
using System.Collections.Generic;
using System.Globalization;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.GameContent.UI.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Compat;

// Wires PvP Adventure's match rules into PvP Framework's shared systems.
public class PvPFrameworkIntegration : ModSystem
{
    public override void Load()
    {
        // Pylon owns cosmetic selection and synchronization; Framework owns race mechanics.
        // The provider keeps that boundary data-driven and Bunny remains Framework's fallback.
        RacePeriodRules.MountTypeProvider = static player =>
            player.GetModPlayer<SpiritAnimalPlayer>().MountType;
        RacePeriodRules.SpriteSheetProvider = ResolveSpiritAnimalSpriteSheet;

        // PvP Framework draws bed outlines; Adventure supplies the synchronized team ownership.
        BedOutlineTile.TeamResolver = ResolveBedTeam;

        // Award team points when a team lands the killing blow on a boss/scoring NPC.
        PvPFramework.Common.Combat.TeamBoss.TeamBossNPC.NpcKilledByPlayer += AwardNpcKill;
        PvPFramework.Common.Combat.TeamBoss.TeamBossNPC.BossDamageDealt += RecordBossDamage;

        ScoreboardPlayerInfoService.RowsProvider = BuildScoreboardInfoRows;
    }

    public override void Unload()
    {
        PvPFramework.Common.Combat.TeamBoss.TeamBossNPC.NpcKilledByPlayer -= AwardNpcKill;
        PvPFramework.Common.Combat.TeamBoss.TeamBossNPC.BossDamageDealt -= RecordBossDamage;
        ScoreboardPlayerInfoService.RowsProvider = null;
        BedOutlineTile.TeamResolver = null;
        RacePeriodRules.MountTypeProvider = null;
        RacePeriodRules.SpriteSheetProvider = null;
    }

    private static Team? ResolveBedTeam(Point origin) =>
        ModContent.GetInstance<TeamBedSystem>().TryGetTeam(origin, out Team team) ? team : null;

    private static RacePeriodSpriteSheet? ResolveSpiritAnimalSpriteSheet(Player player)
    {
        // Dedicated servers own selection and physics but never load or draw cosmetic textures.
        SpiritAnimalPlayer spiritAnimal = player.GetModPlayer<SpiritAnimalPlayer>();
        if (Main.dedServ || !spiritAnimal.TryGetCustomSpriteSheet(out SpiritAnimalSpriteSheet sheet))
            return null;

        Asset<Texture2D> foreground = string.IsNullOrWhiteSpace(sheet.ForegroundTexturePath)
            ? null
            : ModContent.Request<Texture2D>(sheet.ForegroundTexturePath);

        return new RacePeriodSpriteSheet(
            BodyTexture: ModContent.Request<Texture2D>(sheet.BodyTexturePath),
            ForegroundTexture: foreground,
            TotalFrames: sheet.TotalFrames,
            Standing: ConvertAnimation(sheet.Standing),
            Running: ConvertAnimation(sheet.Running),
            Airborne: ConvertAnimation(sheet.Airborne),
            PoweredAirborne: ConvertAnimation(sheet.PoweredAirborne),
            DrawOffset: sheet.DrawOffset.ToVector2(),
            FrameCropBottom: sheet.FrameCropBottom,
            Scale: sheet.Scale,
            AnchorPlayerHeight: sheet.AnchorPlayerHeight);
    }

    private static RacePeriodAnimationRange ConvertAnimation(SpiritAnimalAnimationRange animation) =>
        new(animation.StartFrame, animation.FrameCount, animation.FrameDelay,
            animation.ScaleWithHorizontalSpeed);

    private static void AwardNpcKill(Player player, NPC npc) =>
        ModContent.GetInstance<PointsManager>().AwardNpcKillToTeam((Team)player.team, npc);

    private static void RecordBossDamage(Player player, uint damage, int itemType) =>
        MatchStatsPlayer.RecordServerStat(player, MatchStatKey.BossDamageDealt, damage, itemType);

    private static IReadOnlyList<ScoreboardInfoRow> BuildScoreboardInfoRows(
        Player player,
        ScoreboardEntry scoreboard)
    {
        int[] itemIcons =
        [
            ItemID.LavaBucket,
            ItemID.GoldCrown,
            ItemID.PlatinumCrown,
            ItemID.Skull,
            ItemID.SuspiciousLookingEye,
            ItemID.PickaxeAxe,
            ItemID.DirtBlock,
            ItemID.HoneyBucket
        ];

        foreach (int itemId in itemIcons)
            Main.instance.LoadItem(itemId);

        MatchStatsPlayer stats = player.GetModPlayer<MatchStatsPlayer>();
        long pointBalance =
            (long)stats.GetStat(MatchStatKey.PointKills) -
            stats.GetStat(MatchStatKey.PointDeaths);

        return
        [
            Row(Stat(FrameworkAssets.Attack.Value, "Damage dealt", scoreboard.Damage, 1f, 0), Stat(FrameworkAssets.Defense.Value, "Damage taken", scoreboard.DamageTaken, 1f, 0)),
            Row(ItemStat(ItemID.GoldCrown, "Current streak", scoreboard.CurrentStreak, 1f, -2), ItemStat(ItemID.PlatinumCrown, "Best streak", scoreboard.BestStreak, 1f, -2)),
            Row(ItemStat(ItemID.PickaxeAxe, "Tiles mined", stats.GetStat(MatchStatKey.TilesMined), 1f, 0), ItemStat(ItemID.DirtBlock, "Tiles placed", stats.GetStat(MatchStatKey.TilesPlaced), 1f, 0)),
            Row(ItemStat(ItemID.LavaBucket, "Lava touched", stats.GetStat(MatchStatKey.LavaTouched), 1f, -2), ItemStat(ItemID.HoneyBucket, "Honey lost", stats.GetStat(MatchStatKey.LostHoney), 1f, -2)),
            Row(Stat(PortalMinimapIcon(player), "Portal kills", stats.GetStat(MatchStatKey.PortalKills), 1f, -2), ItemStat(ItemID.SuspiciousLookingEye, "Boss damage", stats.GetStat(MatchStatKey.BossDamageDealt), 1f, -2)),
            Row(Stat(TextureAssets.Item[ItemID.Skull].Value, "K/D", Kd(scoreboard), 1f, 0), Stat(AdventureAssets.IconPointsSetter.Value, "Points positive", Signed(pointBalance), 1f, 0))
        ];
    }

    private static ScoreboardInfoRow Row(ScoreboardInfoStat left, ScoreboardInfoStat right) => new(left, right);

    private static Texture2D PortalMinimapIcon(Player player)
    {
        string team = (Team)player.team switch
        {
            Team.Red => "Red",
            Team.Green => "Green",
            Team.Blue => "Blue",
            Team.Yellow => "Yellow",
            Team.Pink => "Pink",
            _ => "NoTeam"
        };

        return ModContent.Request<Texture2D>(
            $"PvPAdventure/Assets/Portals/PortalMinimap_{team}").Value;
    }

    private static ScoreboardInfoStat ItemStat(int itemId, string label, long value, float iconScale = 1f, int iconY = 0)
    {
        Texture2D texture = TextureAssets.Item[itemId].Value;
        Rectangle? frame = Main.itemAnimations[itemId]?.GetFrame(texture);
        return new(texture, label, value.ToString("N0"), frame, iconScale, iconY);
    }

    private static ScoreboardInfoStat Stat(Texture2D icon, string label, long value, float iconScale = 1f, int iconY = 0) => Stat(icon, label, value.ToString("N0"), iconScale, iconY);

    private static ScoreboardInfoStat Stat(Texture2D icon, string label, string value, float iconScale = 1f, int iconY = 0) => new(icon, label, value, IconScale: iconScale, IconYOffset: iconY);

    private static string Signed(long value) => value > 0 ? $"+{value:N0}" : value.ToString("N0");

    private static string Kd(ScoreboardEntry stats)
    {
        double ratio = stats.Deaths == 0 ? stats.Kills : stats.Kills / (double)stats.Deaths;
        return $"{stats.Kills:N0}/{stats.Deaths:N0} ({ratio.ToString("0.00", CultureInfo.InvariantCulture)} KD)";
    }

}

// Awards team points for PvP kills that happen during a match.
public class AdventureMatchPlayer : ModPlayer
{
    public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
    {
        // Points are decided server-side, and only while a match is being played.
        if (Main.netMode == NetmodeID.MultiplayerClient ||
            ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Playing)
            return;

        // Use the framework's attribution so team points and the scoreboard always credit the same
        // player: the one who landed the killing blow, falling back to the last attacker only for
        // indirect kills like DoTs or knockback into a hazard.
        int killerId = Player.GetModPlayer<PvPFramework.Common.Combat.RecentDamagePlayer>()
            .ResolveKiller(damageSource.SourcePlayerIndex);

        if (killerId < 0 || killerId >= Main.maxPlayers || killerId == Player.whoAmI)
            return;

        Player killer = Main.player[killerId];
        if (killer?.active != true)
            return;

        ModContent.GetInstance<PointsManager>().AwardPlayerKillToTeam(killer, Player);
        BroadcastPlayerKill(killer, Player, damageSource);
    }

    private static void BroadcastPlayerKill(Player killer, Player victim, PlayerDeathReason damageSource)
    {
        Item sourceItem = damageSource.SourceItem;
        if (sourceItem == null || sourceItem.IsAir)
            sourceItem = new Item(ItemID.Skull);

        string message =
            $"[c/{Main.teamColor[killer.team].Hex3()}:{killer.name}] " +
            $"{ItemTagHandler.GenerateTag(sourceItem)} " +
            $"[c/{Main.teamColor[victim.team].Hex3()}:{victim.name}]";

        if (Main.netMode == NetmodeID.Server)
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), Color.White);
        else
            Main.NewText(message, Color.White);
    }
}
