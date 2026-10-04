using Microsoft.Xna.Framework;
using PvPAdventure.Core.Utilities;
using System;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using static Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions;

namespace PvPAdventure.Common.Travel;

/// <summary>
/// Helper class for sending chat messages when players teleport.
/// Mostly used for spawn selector.
/// </summary>
public static class TeleportChat
{
    // List of colors depending on biomes
    private static string GetColoredBiomeName(Player player)
    {
        string biomeName = BiomeHelper.GetBiomeDisplayName(player);

        // Capitalize "The"
        if (!string.IsNullOrEmpty(biomeName))
        {
            if (biomeName.StartsWith("the ", StringComparison.OrdinalIgnoreCase))
                biomeName = "The " + biomeName.Substring(4);
            else if (char.IsLower(biomeName[0]))
                biomeName = char.ToUpper(biomeName[0]) + biomeName.Substring(1);
        }

        SpawnConditionBestiaryInfoElement biome = BiomeHelper.GetBiomeVisual(player).BestiaryBiome;

        Color color;

        // Map biomes to thematic colors
        if (biome == BiomeHelper.ShimmerBiome) color = new Color(220, 180, 255); // Iridescent pink/purple
        else if (biome == BiomeHelper.ForestBiome || biome == Biomes.Surface) color = new Color(50, 200, 50); // Green
        else if (biome == Biomes.TheUnderworld) color = new Color(255, 100, 0); // Orange/Red
        else if (biome == Biomes.TheDungeon) color = new Color(100, 100, 255); // Deep Blue
        else if (biome == Biomes.TheCorruption || biome == Biomes.UndergroundCorruption || biome == Biomes.CorruptIce || biome == Biomes.CorruptDesert || biome == Biomes.CorruptUndergroundDesert) color = new Color(150, 100, 200); // Purple
        else if (biome == Biomes.TheCrimson || biome == Biomes.UndergroundCrimson || biome == Biomes.CrimsonIce || biome == Biomes.CrimsonDesert || biome == Biomes.CrimsonUndergroundDesert) color = new Color(220, 50, 50); // Red
        else if (biome == Biomes.TheHallow || biome == Biomes.UndergroundHallow || biome == Biomes.HallowIce || biome == Biomes.HallowDesert || biome == Biomes.HallowUndergroundDesert) color = new Color(255, 150, 200); // Pink
        else if (biome == Biomes.Jungle || biome == Biomes.UndergroundJungle) color = new Color(140, 220, 50); // Lime Green
        else if (biome == Biomes.Snow || biome == Biomes.UndergroundSnow) color = new Color(150, 255, 255); // Cyan/Ice
        else if (biome == Biomes.Desert || biome == Biomes.UndergroundDesert) color = new Color(220, 200, 100); // Sand
        else if (biome == Biomes.Ocean) color = new Color(50, 150, 255); // Water Blue
        else if (biome == Biomes.SurfaceMushroom || biome == Biomes.UndergroundMushroom) color = new Color(50, 100, 255); // Bright Blue
        else if (biome == Biomes.Sky) color = new Color(150, 200, 255); // Sky Blue
        else if (biome == Biomes.Graveyard) color = new Color(150, 150, 150); // Gray
        else if (biome == Biomes.SpiderNest) color = new Color(100, 80, 60); // Brown
        else if (biome == Biomes.Granite) color = new Color(80, 80, 150); // Dark Blue
        else if (biome == Biomes.Marble) color = new Color(220, 220, 220); // Off-White
        else if (biome == Biomes.Caverns) color = new Color(120, 120, 120); // Stone Gray
        else if (biome == Biomes.Underground) color = new Color(150, 110, 80); // Dirt Brown
        else color = new Color(150, 200, 50); // Fallback color

        // Convert Color to Terraria's hex format: [c/RRGGBB:Text]
        string hex = color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        return $"[c/{hex}:{biomeName}]";
    }

    public static void Announce(Player player, TravelType type, int targetIdx = -1)
    {
        if (player?.active != true)
            return;

        string destination = GetDestination(player, type, targetIdx);

        if (destination == "")
            return;

        SendSystemTeamMessage(player, $"{player.name} has teleported to {destination}", Color.Yellow);
    }

    

    public static void AnnouncePortalDestroyed(Player owner, string fallbackOwnerName)
    {
        if (owner?.active != true)
            return;

        //string biome = BiomeHelper.GetBiomeDisplayName(owner);

        string name = !string.IsNullOrWhiteSpace(fallbackOwnerName) ? fallbackOwnerName : owner.name;

        Color destructionChatColor = Color.Red;
        SendSystemTeamMessage(owner, $"{name}'s portal has been destroyed!", destructionChatColor); 
    }

    public static void AnnounceBedDestroyed(Player owner, string fallbackOwnerName)
    {
        if (owner?.active != true)
            return;
        string name = !string.IsNullOrWhiteSpace(fallbackOwnerName) ? fallbackOwnerName : owner.name;

        Color destructionChatColor = Color.Red;
        SendSystemTeamMessage(owner, $"{name}'s bed has been destroyed!", destructionChatColor);
    }

    public static void AnnounceBedSet(Player player)
    {
        if (player?.active != true)
            return;

        //string biome = BiomeHelper.GetBiomeDisplayName(player);
        string coloredBiome = GetColoredBiomeName(player);

        SendSystemTeamMessage(player, $"{player.name} has set their bed in {coloredBiome}", Color.White);
    }

    public static void AnnouncePortalOpened(Player player)
    {
        if (player?.active != true)
            return;

        //string biome = BiomeHelper.GetBiomeDisplayName(player);
        string coloredBiome = GetColoredBiomeName(player);

        //Color creationChatColor = new Color(100, 150, 200);

        SendSystemTeamMessage(player, $"{player.name} has opened a portal in {coloredBiome}", Color.White);
    }

    private static string GetDestination(Player player, TravelType type, int targetIdx)
    {
        return type switch
        {
            TravelType.World => "world spawn",
            TravelType.Bed => GetOwnedDestination(player, targetIdx, "bed"),
            TravelType.Portal => GetOwnedDestination(player, targetIdx, "portal"),
            TravelType.Random => "a random location",
            _ => ""
        };
    }

    private static string GetOwnedDestination(Player player, int targetIdx, string place)
    {
        if (player?.active != true)
            return "";

        if (targetIdx == player.whoAmI)
            return $"their own {place}";

        if (targetIdx < 0 || targetIdx >= Main.maxPlayers)
            return "";

        return Main.player[targetIdx] is { active: true } target ? $"{target.name}'s {place}" : "";
    }

    private static string GetOwnedDestination(int targetIdx, string place)
    {
        if (targetIdx < 0 || targetIdx >= Main.maxPlayers)
            return "";

        return Main.player[targetIdx] is { active: true } target ? $"{target.name}'s {place}" : "";
    }

    public static void SendSystemTeamMessage(Player player, string text, Color color, string selfText = null)
    {
        if (player == null || !player.active)
            return;

        selfText ??= text;

        if (Main.netMode == NetmodeID.SinglePlayer)
        {
            Main.NewText(selfText, color);
            return;
        }

        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            if (player.whoAmI == Main.myPlayer)
                Main.NewText(selfText, color);

            return;
        }

        if (player.team == 0)
        {
            ChatHelper.SendChatMessageToClient(NetworkText.FromLiteral(selfText), color, player.whoAmI);
            return;
        }

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player teammate = Main.player[i];

            if (teammate == null || !teammate.active || teammate.team != player.team)
                continue;

            string targetText = i == player.whoAmI ? selfText : text;
            //NetworkText message = NetworkText.FromLiteral(ChatPrefixFormatter.TeamChannelMarker + targetText);
            NetworkText message = NetworkText.FromLiteral(targetText);

            ChatHelper.SendChatMessageToClient(message, color, i);
        }
    }
}
