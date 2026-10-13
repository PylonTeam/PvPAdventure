using PvPAdventure.Common.Game.GameReporters;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace PvPAdventure.Common.Game.StatTrackers;

/// <summary>Preserves the firing weapon and server-owned progress for one rifle projectile.</summary>
public sealed class SniperMultiHitTracker : GlobalProjectile
{
    private static uint nextGeneration;
    private bool sourceKnown;
    private bool sniperShot;
    private uint generation;
    private SniperShotProgress progress;

    public override bool InstancePerEntity => true;

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        sourceKnown = true;
        // Tag the original weapon, independently of ammo. High-velocity bullets are also used by other guns.
        sniperShot = source is EntitySource_ItemUse { Item.type: ItemID.SniperRifle } &&
            projectile.owner == Main.myPlayer && projectile.friendly && !projectile.hostile;
        if (sniperShot)
        {
            generation = ++nextGeneration;
            if (generation == 0) generation = ++nextGeneration;
        }
    }

    public override void SendExtraAI(Projectile projectile, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        bitWriter.WriteBit(sniperShot);
        if (sniperShot) binaryWriter.Write(generation);
    }

    public override void ReceiveExtraAI(Projectile projectile, BitReader bitReader, BinaryReader binaryReader)
    {
        bool incomingSniper = bitReader.ReadBit();
        uint incomingGeneration = incomingSniper ? binaryReader.ReadUInt32() : 0;
        if (sourceKnown)
        {
            // A later position update cannot turn another weapon's projectile into a fresh rifle shot.
            if (sniperShot != incomingSniper || generation != incomingGeneration) progress = null;
            return;
        }

        sourceKnown = true;
        sniperShot = incomingSniper;
        generation = incomingGeneration;
        // Remote SyncProjectile does not call OnSpawn. Register at its first ExtraAI reception instead.
        if (!sniperShot || generation == 0 || !projectile.friendly || projectile.hostile ||
            projectile.damage <= 0 || SniperAchievementSystem.IncomingMessage != MessageID.SyncProjectile ||
            SniperAchievementSystem.IncomingSender != projectile.owner ||
            !SniperAchievementSystem.TryGetMatch(out string matchToken) ||
            !SniperAchievementSystem.TryGetParticipant(projectile.owner, out Player shooter, out ulong steamId) ||
            shooter.HeldItem.type != ItemID.SniperRifle)
            return;

        object connection = Netplay.Clients[projectile.owner].Socket;
        if (connection != null)
            progress = new SniperShotProgress(matchToken, steamId, connection,
                projectile.owner, projectile.identity, projectile.type, generation);
    }

    internal void RecordHit(Projectile projectile, Player shooter, ulong shooterId,
        ulong victimId, string matchToken)
    {
        if (progress?.TryRecordHit(matchToken, shooterId, Netplay.Clients[shooter.whoAmI].Socket,
                projectile.owner, projectile.identity, projectile.type, generation, victimId) == true)
            AchievementReporter.OnSniperDoubleHit(shooter);
    }

    public override void OnKill(Projectile projectile, int timeLeft) => progress = null;
}
