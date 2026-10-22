using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Misc;

public class LihzahrdAltarChanges : GlobalTile
{
    public override void SetStaticDefaults()
    {
        TileID.Sets.PreventsTileRemovalIfOnTopOfIt[TileID.LihzahrdAltar] = true;
        TileID.Sets.PreventsTileHammeringIfOnTopOfIt[TileID.LihzahrdAltar] = true;
    }

    public override bool CanKillTile(int i, int j, int type, ref bool blockDamaged)
    {
        if (type == TileID.LihzahrdAltar)
        {
            return false;
        }

        return base.CanKillTile(i, j, type, ref blockDamaged);
    }
}
