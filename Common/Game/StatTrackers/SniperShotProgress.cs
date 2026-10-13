using System.Collections.Generic;

namespace PvPAdventure.Common.Game.StatTrackers;

/// <summary>A shot can qualify once, using two different accounts in its original match and connection.</summary>
internal sealed class SniperShotProgress(
    string matchToken, ulong shooterId, object connection, int owner, int identity, int projectileType, uint generation)
{
    private readonly HashSet<ulong> victims = [];
    private bool awarded;

    public bool TryRecordHit(string currentMatch, ulong currentShooter, object currentConnection,
        int currentOwner, int currentIdentity, int currentType, uint currentGeneration, ulong victimId)
    {
        if (awarded || string.IsNullOrEmpty(matchToken) || shooterId == 0 || generation == 0 ||
            connection == null || currentMatch != matchToken || currentShooter != shooterId ||
            !ReferenceEquals(currentConnection, connection) || currentOwner != owner ||
            currentIdentity != identity || currentType != projectileType || currentGeneration != generation ||
            victimId == 0 || victimId == shooterId || !victims.Add(victimId) || victims.Count < 2)
            return false;

        awarded = true;
        return true;
    }
}
