using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.NPCs;

/// <summary>
/// Makes Demon Eye aggro during the day, and also spawn in surface graveyard biomes
/// </summary>
public class GraveyardDemonEye : GlobalNPC
{
    public override bool InstancePerEntity => true;

    public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
    {
        if (!spawnInfo.Player.ZoneGraveyard)
            return;
        if (spawnInfo.SpawnTileY > (int)Main.worldSurface)
            return;

        pool.Clear();
        pool[NPCID.DemonEye] = 1f;
    }

    public override void OnSpawn(NPC npc, IEntitySource source)
    {
        if (npc.type == NPCID.DemonEye)
            return;
        if (source is not EntitySource_SpawnNPC)
            return;

        int closestPlayer = Player.FindClosest(npc.position, npc.width, npc.height);
        Player player = Main.player[closestPlayer];

        if (!player.ZoneGraveyard)
            return;
        if (npc.position.Y / 16f > Main.worldSurface)
            return;

        npc.active = false;
    }

    private bool _spoofedDaytime;

    public override bool PreAI(NPC npc)
    {
        _spoofedDaytime = false;

        if (npc.type != NPCID.DemonEye || !Main.dayTime)
            return true;

        int closestPlayer = Player.FindClosest(npc.position, npc.width, npc.height);
        if (!Main.player[closestPlayer].ZoneGraveyard)
            return true;

        Main.dayTime = false;
        _spoofedDaytime = true;
        return true;
    }

    public override void PostAI(NPC npc)
    {
        if (_spoofedDaytime)
        {
            Main.dayTime = true;
            _spoofedDaytime = false;
        }
    }
}