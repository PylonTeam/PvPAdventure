using System;
using Microsoft.Xna.Framework;
using PvPAdventure.Common.RacePeriod;
using PvPAdventure.Common.Travel.Beds;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Compat;

[Autoload(Side = ModSide.Client)]
internal sealed class TeamOutlinesIntegration : ModSystem
{
    private Mod outlines;

    public override void PostSetupContent()
    {
        if (!ModLoader.TryGetMod("TeamOutlines", out outlines)) return;
        outlines.Call("RegisterPlayerExclusion", Mod, (Func<Player, DrawData, bool>)ExcludeRaceTrail);
        outlines.Call("RegisterBedTeamResolver", Mod, (Func<Point, Team?>)BedTeam);
    }

    public override void Unload()
    {
        outlines?.Call("ClearRegistrations", Mod);
        outlines = null;
    }

    private static bool ExcludeRaceTrail(Player player, DrawData data) =>
        RacePeriodTrailTextures.IsTrailTexture(data.texture);

    private static Team? BedTeam(Point origin) =>
        ModContent.GetInstance<TeamBedSystem>().TryGetTeam(origin, out Team team) ? team : null;
}
