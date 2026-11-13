using System.Collections.Generic;
using ErkySSC.Common.Visualization;
using Microsoft.Xna.Framework.Graphics;
using Pylon.Common.SpiritAnimals;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

#nullable enable

namespace PvPAdventure.Common.RacePeriod;

/// <summary>Supplies race animal portraits to the world map's existing player markers.</summary>
[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodMapHeads : ModSystem
{
    private readonly record struct AnimalArt(int MountType, Asset<Texture2D>? Body);

    private readonly Dictionary<AnimalArt, Asset<Texture2D>> heads = new();

    public override void PostSetupContent()
    {
        foreach (SpiritAnimalDefinition animal in SpiritAnimalCatalog.All)
        {
            if (!ModContent.RequestIfExists($"{Mod.Name}/Assets/SpiritAnimalMapHeads/{animal.ProductName}",
                    out Asset<Texture2D> head, AssetRequestMode.ImmediateLoad))
                continue;

            Asset<Texture2D>? body = animal.CustomSpriteSheet is { } sheet
                ? ModContent.Request<Texture2D>(sheet.BodyTexturePath) : null;
            heads.Add(new AnimalArt(animal.MountType, body), head);
        }

        ModContent.GetInstance<MapPlayerHeadControlSystem>().MapHeadTextureProvider = GetHead;
    }

    public override void Unload()
    {
        MapPlayerHeadControlSystem map = ModContent.GetInstance<MapPlayerHeadControlSystem>();
        if (map?.MapHeadTextureProvider == GetHead)
            map.MapHeadTextureProvider = null;
        heads.Clear();
    }

    private Texture2D? GetHead(Player player)
    {
        if (player.ghost || player.dead || !player.TryGetModPlayer(out RacePeriodPlayer race) || !race.IsRacing)
            return null;

        Asset<Texture2D>? body = race.TryGetSpriteSheet(out RacePeriodSpriteSheet sheet) ? sheet.BodyTexture : null;
        return heads.TryGetValue(new AnimalArt(player.mount.Type, body), out Asset<Texture2D>? head) ? head.Value : null;
    }
}
