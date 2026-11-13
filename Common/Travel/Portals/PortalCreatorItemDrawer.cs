using Microsoft.Xna.Framework.Graphics;
using PvPAdventure.Content.Portals;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Portals;

/// <summary>
/// Draws the held item for the portal creator item with the team-specific texture.
/// </summary>
[Autoload(Side = ModSide.Client)]
internal sealed class PortalCreatorItemDrawer : ModSystem
{
    public override void Load() => On_PlayerDrawLayers.DrawPlayer_27_HeldItem += DrawPortalCreatorItemWithTeamColor;

    public override void Unload() => On_PlayerDrawLayers.DrawPlayer_27_HeldItem -= DrawPortalCreatorItemWithTeamColor;

    private static void DrawPortalCreatorItemWithTeamColor(On_PlayerDrawLayers.orig_DrawPlayer_27_HeldItem orig, ref PlayerDrawSet drawInfo)
    {
        Item item = drawInfo.drawPlayer.HeldItem;
        if (item?.ModItem is not PortalCreatorItem)
        {
            orig(ref drawInfo);
            return;
        }

        Main.instance.LoadItem(item.type);
        Texture2D vanilla = TextureAssets.Item[item.type].Value;

        int team = drawInfo.drawPlayer.team;
        Texture2D replacement = PortalCreatorSkin.TryGetTexture(item, team, out Texture2D skin)
            ? skin
            : PortalAssets.GetCreatorTexture(team);

        int start = drawInfo.DrawDataCache.Count;
        orig(ref drawInfo);

        for (int i = start; i < drawInfo.DrawDataCache.Count; i++)
        {
            DrawData data = drawInfo.DrawDataCache[i];
            if (data.texture != vanilla)
                continue;

            data.texture = replacement;
            (data.sourceRect, data.origin) = PortalCreatorSkin.Rescale(data.sourceRect, data.origin, vanilla, replacement);
            drawInfo.DrawDataCache[i] = data;
        }
    }
}
