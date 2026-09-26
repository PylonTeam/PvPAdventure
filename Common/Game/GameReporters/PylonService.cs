using Pylon.Common.Authentication;
using Pylon.Common.MainMenu.API;
using Pylon.Common.MainMenu.API.Achievements;
using Pylon.Common.MainMenu.API.MatchHistory;
using Pylon.Common.MainMenu.API.Profile;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Game.GameReporters;

/// <summary>Typed access to the strongly referenced Pylon services used by PvPAdventure.</summary>
internal static class PylonService
{
    public static void LogMatchPostAuthPreflight()
    {
        bool isOfficial = Main.dedServ && global::Pylon.Pylon.IsOfficial;

        Log.Info(
            $"Pylon match auth preflight: NetMode={Main.netMode}, DedServ={Main.dedServ}, PylonOfficial={isOfficial}, ActivePlayers={CountActivePlayers()}");

        if (Main.netMode != NetmodeID.Server)
            Log.Warn("Pylon match posting is disabled because this instance is not a dedicated server.");

        if (!isOfficial)
            Log.Warn("Pylon Steam IDs are unavailable because this server is not official. Start the dedicated server with -official to enable Pylon server authentication.");
    }

    public static bool TryGetSteamId(Player player, out ulong steamId)
    {
        steamId = 0;

        if (player?.active != true || Main.netMode != NetmodeID.Server || player.whoAmI is < 0 or >= Main.maxPlayers)
            return false;

        try
        {
            ulong? identity = ModContent.GetInstance<SteamAuthentication>()
                .GetAuthenticatedIdentity((byte)player.whoAmI);


            if (identity is not ulong id || id == 0)
            {
                if (Main.GameUpdateCount % 60 == 5)
                {
                    Log.Warn(
                    $"Pylon Steam ID lookup failed. " +
                    $"Player={DescribePlayer(player)}, " +
                    $"Result={identity?.ToString() ?? "<null>"}");
                }

                    
                return false;
            }

            if (id > long.MaxValue)
            {
                if (Main.GameUpdateCount % 60 == 5)
                {
                    Log.Warn(
                    $"Pylon Steam ID lookup rejected identity above Int64 range. " +
                    $"Player={DescribePlayer(player)}, SteamId={id}, " +
                    $"Int64Max={long.MaxValue}");
                }
                    
                return false;
            }

            steamId = id;

            if (Main.GameUpdateCount % 60 == 5)
            {
                Log.Debug($"Pylon Steam ID lookup succeeded. Player={DescribePlayer(player)}, SteamId={steamId}");
            }
            return true;
        }
        catch (System.Exception ex)
        {
            if (Main.GameUpdateCount % 60 == 5)
            {
                Log.Warn($"Pylon Steam ID lookup failed. Player={DescribePlayer(player)}, Error={ex}");
            }
            return false;
        }
    }

    public static Task<ApiResult<MatchApi.CompletedMatchPayload>> PostMatchAsync(
        MatchApi.MatchPayload payload,
        string replayFilePath = null,
        CancellationToken cancellationToken = default)
    {
        return string.IsNullOrWhiteSpace(replayFilePath)
            ? MatchApi.PostOfficialMatchAsync(payload, cancellationToken)
            : MatchApi.PostOfficialMatchV2Async(payload, replayFilePath, cancellationToken);
    }

    public static Task<ApiResult<long>> GetTotalGemsAsync(
        ulong steamId,
        CancellationToken cancellationToken = default)
    {
        return GemBalanceApi.GetTotalGemsAsync(steamId, cancellationToken);
    }

    public static Task<ApiResult<ApiAchievement>> ProgressAchievementAsync(
        ulong steamId,
        string achievementName,
        string gameMode,
        uint delta = 1,
        CancellationToken cancellationToken = default)
    {
        return AchievementsApi.ProgressAchievementAsync(
            steamId,
            new AchievementRef(achievementName, gameMode),
            delta,
            cancellationToken);
    }

    private static int CountActivePlayers()
    {
        int count = 0;
        foreach (Player player in Main.ActivePlayers)
            if (player?.active == true)
                count++;
        return count;
    }

    private static string DescribePlayer(Player player)
    {
        string clientState = "";
        if (Main.netMode == NetmodeID.Server && player.whoAmI >= 0 && player.whoAmI < Netplay.Clients.Length)
        {
            RemoteClient client = Netplay.Clients[player.whoAmI];
            clientState = $", ClientActive={client?.IsActive}, ClientState={client?.State}";
        }

        return $"Name={player.name}, WhoAmI={player.whoAmI}, Active={player.active}, Team={player.team}{clientState}";
    }
}
