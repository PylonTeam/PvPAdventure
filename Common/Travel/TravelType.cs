namespace PvPAdventure.Common.Travel;

/// <summary>
/// Specifies the travel destination types for a player.
/// These are available from:
/// - the fullscreen map, 
/// - from travel regions (world spawn, my bed, team beds, portals, team portals), 
/// - when using the portal creator item, 
/// - and when dead
/// </summary>
public enum TravelType : byte
{
    None,
    World,
    Bed,
    Portal,
    Random,
    SpawnboxPortal,
}
