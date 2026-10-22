using PvPAdventure.Common.Game.GameReporters;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game.StatTrackers;

/// <summary>Connects accepted Framework PvP damage to the actual sender and registered rifle shot.</summary>
internal sealed class SniperAchievementSystem : ModSystem
{
    [ThreadStatic] private static IncomingPacket? incoming;
    private readonly record struct IncomingPacket(int Sender, int Message);
    internal static int IncomingSender => incoming?.Sender ?? -1;
    internal static int IncomingMessage => incoming?.Message ?? -1;

    public override void Load() => On_MessageBuffer.GetData += ReadPacket;
    public override void Unload()
    {
        On_MessageBuffer.GetData -= ReadPacket;
        incoming = null;
    }

    private static void ReadPacket(On_MessageBuffer.orig_GetData orig, MessageBuffer buffer,
        int start, int length, out int messageType)
    {
        IncomingPacket? previous = incoming;
        incoming = Main.netMode == NetmodeID.Server
            ? new IncomingPacket(buffer.whoAmI, buffer.readBuffer[start]) : null;
        try { orig(buffer, start, length, out messageType); }
        finally { incoming = previous; }
    }

    internal static bool TryGetMatch(out string token)
    {
        token = "";
        if (Main.netMode != NetmodeID.Server || !Main.dedServ || !global::Pylon.Pylon.IsOfficial ||
            ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Playing)
            return false;

        token = ModContent.GetInstance<AdventureMatchReportingSystem>().CurrentMatchToken;
        return !string.IsNullOrEmpty(token);
    }

    internal static bool TryGetParticipant(int slot, out Player player, out ulong steamId)
    {
        player = null;
        steamId = 0;
        if ((uint)slot >= Main.maxPlayers) return false;
        player = Main.player[slot];
        return player?.active == true && player.hostile && player.team is >= 1 and <= 5 &&
            PylonService.TryGetSteamId(player, out steamId);
    }

    internal static void RecordAcceptedHit(Player victim, Player.HurtInfo info)
    {
        if (!info.PvP || info.Damage <= 0 || info.DamageSource == null ||
            IncomingMessage != MessageID.PlayerHurtV2 || !TryGetMatch(out string matchToken))
            return;

        var source = info.DamageSource;
        if (source.SourcePlayerIndex != IncomingSender || source.SourceProjectileLocalIndex < 0 ||
            source.SourceItem?.type != ItemID.SniperRifle ||
            !TryGetParticipant(IncomingSender, out Player shooter, out ulong shooterId) ||
            !TryGetParticipant(victim.whoAmI, out Player currentVictim, out ulong victimId) ||
            !ReferenceEquals(victim, currentVictim) || shooter.team == victim.team || shooterId == victimId)
            return;

        // Framework sends PvP hurt from the attacker. On that owning client, NewProjectile assigns
        // identity = local slot. Resolve owner + identity here; the server's projectile array slot differs.
        foreach (Projectile projectile in Main.ActiveProjectiles)
        {
            if (projectile.owner == IncomingSender && projectile.identity == source.SourceProjectileLocalIndex &&
                projectile.type == source.SourceProjectileType &&
                projectile.TryGetGlobalProjectile(out SniperMultiHitTracker tracker))
            {
                tracker.RecordHit(projectile, shooter, shooterId, victimId, matchToken);
                return;
            }
        }
    }
}

internal sealed class SniperAchievementPlayer : ModPlayer
{
    // OnHitPlayer is never called by Terraria's PvP projectile path. OnHurt also includes lethal hits,
    // whereas PostHurt runs only when the victim survives.
    public override void OnHurt(Player.HurtInfo info) => SniperAchievementSystem.RecordAcceptedHit(Player, info);
}
