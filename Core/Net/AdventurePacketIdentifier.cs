namespace PvPAdventure.Core.Net;

public enum AdventurePacketIdentifier : byte
{
    BountyTransaction,
    TeamBed,
    ReservedLegacyGameManager, // Keep the old ID unused; game controls now belong to Framework.
    TravelTeleport, // teleport between beds/portals/world spawn, play sound/vfx, etc
    UsePortal, // use portal creator item to create a portal, sync to everyone
    MatchStatDelta,
    Hellhex,
    MatchStatsSnapshot,
    ShakingChest,
    TeamPoints,
}
