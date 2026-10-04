using Microsoft.Xna.Framework;
using PvPAdventure.Common.Statistics;
using ErkySSC.Common.SSC;
using PvPAdventure.Common.Travel.Portals;
using PvPAdventure.Core.Config;
using PvPAdventure.Core.Utilities;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Team = Terraria.Enums.Team;

namespace PvPAdventure.Common.Travel.Beds;

[Autoload(Side = ModSide.Both)]
internal sealed class TeamBedSystem : ModSystem
{
    private readonly Dictionary<Point, Team> bedTeams = [];
    private readonly Dictionary<Point, int> bedOwners = [];
    private readonly Dictionary<int, BedState> lastStates = [];
    private int updateTimer;
    private readonly Dictionary<int, (Point Origin, ulong Tick)> currentBedTarget = [];
    private const ulong BedTargetTtlTicks = 45;

    public void SetCurrentBedTarget(int playerId, Point origin)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        currentBedTarget[playerId] = (origin, Main.GameUpdateCount);
    }

    public void ClearCurrentBedTarget(int playerId)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        currentBedTarget.Remove(playerId);
    }

    public IEnumerable<(Point Origin, Team Team)> ActiveBeds()
    {
        foreach ((Point origin, Team team) in bedTeams)
            if (team != Team.None)
                yield return (origin, team);
    }

    public bool TryGetTeam(Point origin, out Team team) => bedTeams.TryGetValue(origin, out team);

    

    public override void PostUpdatePlayers()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (++updateTimer < 15)
            return;

        updateTimer = 0;

        for (int i = 0; i < Main.maxPlayers; i++)
            if (Main.player[i]?.active == true)
                UpdateFromPlayer(Main.player[i]);
    }

    public void SetFromNet(Point origin, Team team)
    {
        if (team == Team.None)
        {
            bedTeams.Remove(origin);
            bedOwners.Remove(origin);
            return;
        }

        bedTeams[origin] = team;
    }

    private static bool BedTileExists(Point origin)
    {
        for (int x = origin.X; x < origin.X + 4; x++)
        {
            for (int y = origin.Y; y < origin.Y + 2; y++)
            {
                if ((uint)x >= (uint)Main.maxTilesX || (uint)y >= (uint)Main.maxTilesY)
                    continue;

                Tile tile = Main.tile[x, y];

                if (tile != null && tile.HasTile && tile.TileType == TileID.Beds)
                    return true;
            }
        }

        return false;
    }

    public void UpdateFromPlayer(Player player)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient || player?.active != true)
            return;

        int playerId = player.whoAmI;
        BedState current = GetState(player);

        if (lastStates.TryGetValue(playerId, out BedState previous) && previous.Equals(current))
            return;

        lastStates[playerId] = current;

        if (previous.HasOrigin && !current.HasOrigin)
        {
            if (BedTileExists(previous.Origin))
                ClearIfOwner(previous.Origin, playerId);
            else
                HandleBedTileDestroyed(previous.Origin, playerId);

            return;
        }

        if (previous.HasOrigin && previous.Origin != current.Origin)
            ClearIfOwner(previous.Origin, playerId);

        if (!current.HasOrigin)
            return;

        Team team = (Team)player.team;

        if (team == Team.None)
        {
            ClearIfOwner(current.Origin, playerId);
            return;
        }

        ClaimBed(player, current.Origin, team);
    }

    public void SendAllBedsToClient(int toClient)
    {
        foreach ((Point origin, Team team) in bedTeams)
            SendBedUpdate(origin, team, toClient);
    }

    public void SendAllStateToClient(int toClient)
    {
        SendAllBedsToClient(toClient);

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];

            if (player?.active == true)
                SendPlayerSpawn(i, player.SpawnX, player.SpawnY, toClient: toClient);
        }
    }

    private void ClaimBed(Player player, Point origin, Team team)
    {
        int playerId = player.whoAmI;

        bool hadOwner = bedOwners.TryGetValue(origin, out int previousOwnerId);
        bool sameOwner = hadOwner && previousOwnerId == playerId;
        bool sameTeamOwner = hadOwner && IsSameTeam(previousOwnerId, team);
        bool ownerAlreadyHere = sameOwner;
        bool alreadyClaimed = bedTeams.TryGetValue(origin, out Team oldTeam) && oldTeam == team && sameOwner;

        if (alreadyClaimed)
            return;

        if (hadOwner && !sameOwner && !sameTeamOwner)
            ClearDestroyedBedOwnerSpawn(origin, previousOwnerId, announceDestroyed: true);

        bedTeams[origin] = team;

        if (!hadOwner || !sameTeamOwner)
            bedOwners[origin] = playerId;

        SendBedUpdate(origin, team);
        if (!ownerAlreadyHere)
            TeleportChat.AnnounceBedSet(player);
    }

    private static bool IsSameTeam(int playerId, Team team)
    {
        if (playerId < 0 || playerId >= Main.maxPlayers || Main.player[playerId] is not { active: true } player)
            return false;

        return player.team > 0 && (Team)player.team == team;
    }

    private static void AwardBedKillPoints(Team team, Point origin)
    {
        var cfg = ModContent.GetInstance<ServerConfig>();
        int bedPoints = cfg.Points.BedKill;

        if (bedPoints == 0 || team == Team.None)
            return;

        Vector2 pos = new(origin.X * 16f + 8f, origin.Y * 16f + 8f);
        ModContent.GetInstance<PointsManager>().AwardPointsToTeam(team, bedPoints, pos, "[c/F58522:Bed]");
    }

    private void ClearIfOwner(Point origin, int playerId)
    {
        if (!bedOwners.TryGetValue(origin, out int owner) || owner != playerId)
            return;

        bool removed = bedTeams.Remove(origin);
        bedOwners.Remove(origin);

        if (removed)
            SendBedUpdate(origin, Team.None);
    }

    private void ClearDestroyedBedOwnerSpawn(Point origin, int ownerId, bool announceDestroyed)
    {
        if (ownerId < 0 || ownerId >= Main.maxPlayers || Main.player[ownerId] is not { active: true } owner)
            return;

        if (!SSCBedSystem.SetSpawn(owner, -1, -1))
            return;

        Log.Chat($"Cleared owner bed spawn pos=({origin.X},{origin.Y}) ownerId={ownerId}, announce={announceDestroyed}");

        lastStates[ownerId] = GetState(owner);

        SendPlayerSpawn(ownerId, -1, -1);

        if (announceDestroyed)
            TeleportChat.AnnounceBedDestroyed(owner, owner.name);
    }

    private void HandleBedTileDestroyed(Point origin, int fallbackOwnerId = -1)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        bool hasOwner = bedOwners.TryGetValue(origin, out int ownerId);
        if (!hasOwner)
            ownerId = fallbackOwnerId;

        if (ownerId < 0)
            return;

        Team destroyedTeam = bedTeams.TryGetValue(origin, out Team team) ? team : Team.None;
        Team destroyerTeam = GetDestroyerTeam(origin, destroyedTeam);
        bool enemyDestroyed = destroyerTeam != Team.None;

        bool removed = bedTeams.Remove(origin);
        bedOwners.Remove(origin);
        ClearTargetsForOrigin(origin);

        if (removed)
            SendBedUpdate(origin, Team.None);

        ClearDestroyedBedOwnerSpawn(origin, ownerId, announceDestroyed: enemyDestroyed);

        if (!enemyDestroyed)
            return;

        AwardBedKillPoints(destroyerTeam, origin);

        Vector2 bedWorldPos = new(origin.X * 16f + 8f, origin.Y * 16f + 8f);
        TeamBedNetHandler.SendBedDestructionFx(bedWorldPos.X, bedWorldPos.Y, killed: true);
    }

    private void ClearTargetsForOrigin(Point origin)
    {
        List<int> stale = [];

        foreach ((int playerId, var target) in currentBedTarget)
        {
            if (target.Origin == origin)
                stale.Add(playerId);
        }

        foreach (int playerId in stale)
            currentBedTarget.Remove(playerId);
    }

    private Team GetDestroyerTeam(Point origin, Team destroyedTeam)
    {
        foreach ((int playerId, var target) in currentBedTarget)
        {
            if (target.Origin != origin)
                continue;

            if (Main.GameUpdateCount < target.Tick || Main.GameUpdateCount - target.Tick > BedTargetTtlTicks)
                continue;

            if (playerId < 0 || playerId >= Main.maxPlayers)
                continue;

            Player player = Main.player[playerId];
            if (player?.active != true)
                continue;

            Team playerTeam = (Team)player.team;
            if (playerTeam == Team.None || playerTeam == destroyedTeam)
                continue;

            return playerTeam;
        }

        return Team.None;
    }

    internal static bool TryGetBedOriginFromTile(int x, int y, out Point origin)
    {
        origin = default;

        if ((uint)x >= (uint)Main.maxTilesX || (uint)y >= (uint)Main.maxTilesY)
            return false;

        Tile tile = Main.tile[x, y];

        if (tile == null || !tile.HasTile || tile.TileType != TileID.Beds)
            return false;

        origin = new Point(x - tile.TileFrameX / 18 % 4, y - tile.TileFrameY / 18 % 2);
        return true;
    }

    private static BedState GetState(Player player)
    {
        Point origin = new(-1, -1);
        bool hasSpawn = player.SpawnX >= 0 && player.SpawnY >= 0 && Player.CheckSpawn(player.SpawnX, player.SpawnY);
        bool hasOrigin = hasSpawn && TryFindBedOrigin(player.SpawnX, player.SpawnY, out origin);

        if (!hasOrigin)
            origin = new Point(-1, -1);

        return new BedState(player.SpawnX, player.SpawnY, player.team, origin, hasOrigin);
    }

    private static void SendBedUpdate(Point origin, Team team, int toClient = -1)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)Core.Net.AdventurePacketIdentifier.TeamBed);
        packet.Write((byte)TeamBedPacketType.BedUpdate);
        packet.Write(origin.X);
        packet.Write(origin.Y);
        packet.Write((byte)team);
        packet.Send(toClient);
    }

    internal static void SendPlayerSpawn(int playerId, int spawnX, int spawnY, int toClient = -1, int ignoreClient = -1)
    {
        if (Main.netMode != NetmodeID.Server)
            return;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)Core.Net.AdventurePacketIdentifier.TeamBed);
        packet.Write((byte)TeamBedPacketType.PlayerSpawn);
        packet.Write((byte)playerId);
        packet.Write(spawnX);
        packet.Write(spawnY);
        packet.Send(toClient, ignoreClient);
    }

    private static bool TryFindBedOrigin(int sx, int sy, out Point origin)
    {
        origin = default;
        int best = int.MaxValue;
        HashSet<Point> seen = [];

        for (int dx = -8; dx <= 8; dx++)
        {
            for (int dy = -8; dy <= 8; dy++)
            {
                int x = sx + dx;
                int y = sy + dy;

                if ((uint)x >= (uint)Main.maxTilesX || (uint)y >= (uint)Main.maxTilesY)
                    continue;

                if (!TryGetBedOriginFromTile(x, y, out Point candidate))
                    continue;

                if (!seen.Add(candidate))
                    continue;

                int distX = candidate.X + 2 - sx;
                int distY = candidate.Y + 1 - sy;
                int dist = distX * distX + distY * distY;

                if (dist >= best)
                    continue;

                best = dist;
                origin = candidate;
            }
        }

        return best != int.MaxValue;
    }

    private readonly struct BedState(int spawnX, int spawnY, int team, Point origin, bool hasOrigin)
    {
        public readonly int SpawnX = spawnX;
        public readonly int SpawnY = spawnY;
        public readonly int Team = team;
        public readonly Point Origin = origin;
        public readonly bool HasOrigin = hasOrigin;

        public bool Equals(BedState other) => SpawnX == other.SpawnX && SpawnY == other.SpawnY && Team == other.Team && Origin == other.Origin && HasOrigin == other.HasOrigin;
    }

    #region Unload
    public override void OnWorldUnload()
    {
        bedTeams.Clear();
        bedOwners.Clear();
        lastStates.Clear();
        currentBedTarget.Clear();
        updateTimer = 0;
    }
    #endregion

}
