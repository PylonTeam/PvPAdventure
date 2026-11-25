namespace PvPAdventure.Core.Net;

public enum AdventurePacketIdentifier : byte
{
    TeamBed = 1, // Packet ID 0 is retired.
    ReservedLegacyGameManager, // Keep the old ID unused; game controls now belong to Framework.
    TravelTeleport, // teleport between beds/portals/world spawn, play sound/vfx, etc
    UsePortal, // use portal creator item to create a portal, sync to everyone
    MatchStatDelta,
    Hellhex,
    MatchStatsSnapshot,
    ShakingChest,
    TeamPoints,
    AnglerQuest,
}
