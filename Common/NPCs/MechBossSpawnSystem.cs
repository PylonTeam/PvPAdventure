using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.NPCs;

/// <summary>
/// - Automatically spawns an undefeated mechanical boss at night
/// - The bosses spawn at dungeon entrance
/// </summary>
public class MechBossSpawnSystem : ModSystem
{
    private bool hasAnnouncedTonight = false;
    private bool hasSpawnedTonight = false;
    private bool wasDay = true;
    private int selectedBoss = -1;

    public override void PostUpdateWorld()
    {
        bool isNight = !Main.dayTime;

        if (isNight && wasDay)
        {
            hasAnnouncedTonight = false;
            hasSpawnedTonight = false;
            selectedBoss = -1;
            wasDay = false;
        }

        if (!isNight && !wasDay)
        {
            wasDay = true;
            hasAnnouncedTonight = false;
            hasSpawnedTonight = false;
            selectedBoss = -1;
        }

        if (!ShouldSpawnMechBoss())
            return;

        if (Main.time >= 0 && Main.time < 100 && !hasAnnouncedTonight)
        {
            selectedBoss = ChooseMechanicalBoss();
            if (selectedBoss != -1)
            {
                ShowAnnouncementMessage(selectedBoss);
                hasAnnouncedTonight = true;
            }
        }

        if (Main.time >= 3600 && Main.time < 3700 && hasAnnouncedTonight && !hasSpawnedTonight)
        {
            if (selectedBoss != -1)
            {
                SpawnMechanicalBoss(selectedBoss);
                hasSpawnedTonight = true;
            }
        }
    }

    private bool ShouldSpawnMechBoss()
    {
        return !Main.dayTime &&
               Main.hardMode &&
               !NPC.AnyNPCs(NPCID.TheDestroyer) &&
               !NPC.AnyNPCs(NPCID.Retinazer) &&
               !NPC.AnyNPCs(NPCID.Spazmatism) &&
               !NPC.AnyNPCs(NPCID.SkeletronPrime) &&
               HasUndefeatedMechBoss() &&
               AnyValidPlayer();
    }

    private bool HasUndefeatedMechBoss()
    {
        return !NPC.downedMechBoss1 || !NPC.downedMechBoss2 || !NPC.downedMechBoss3;
    }

    private bool AnyValidPlayer()
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];
            if (player.active && !player.dead)
                return true;
        }
        return false;
    }

    private int ChooseMechanicalBoss()
    {
        var availableBosses = new List<int>();

        if (!NPC.downedMechBoss1) availableBosses.Add(NPCID.TheDestroyer);
        if (!NPC.downedMechBoss2) availableBosses.Add(NPCID.Retinazer);
        if (!NPC.downedMechBoss3) availableBosses.Add(NPCID.SkeletronPrime);

        return availableBosses.Count > 0 ? availableBosses[Main.rand.Next(availableBosses.Count)] : -1;
    }

    private void ShowAnnouncementMessage(int bossType)
    {
        string message;
        switch (bossType)
        {
            case NPCID.TheDestroyer:
                message = "You feel vibrations from deep below...";
                break;
            case NPCID.Retinazer:
                message = "This is going to be a terrible night...";
                break;
            case NPCID.SkeletronPrime:
                message = "The air is getting colder around you...";
                break;
            default:
                return;
        }

        ShowMessage(message, new Color(50, 255, 130));
    }

    private void SpawnMechanicalBoss(int bossType)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        // Dungeon entrance in world coordinates
        int spawnX = Main.dungeonX * 16 + 8;
        int spawnY = Main.dungeonY * 16 - 10 * 16;

        string spawnMessage;

        switch (bossType)
        {
            case NPCID.TheDestroyer:
                spawnMessage = "Uh oh!";
                SpawnBossAt(spawnX, spawnY, NPCID.TheDestroyer);
                break;

            case NPCID.Retinazer:
                spawnMessage = "Better watch out!";
                SpawnBossAt(spawnX - 48, spawnY, NPCID.Retinazer);
                SpawnBossAt(spawnX + 48, spawnY, NPCID.Spazmatism);
                break;

            case NPCID.SkeletronPrime:
                spawnMessage = "Yikes!";
                SpawnBossAt(spawnX, spawnY, NPCID.SkeletronPrime);
                break;

            default:
                return;
        }

        ShowMessage(spawnMessage, new Color(175, 75, 255));

        Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, new Vector2(spawnX, spawnY));
    }

    private static void SpawnBossAt(int x, int y, int type)
    {
        int index = NPC.NewNPC(new EntitySource_WorldEvent(), x, y, type);
        if (index < 0 || index >= Main.maxNPCs)
            return;

        NPC npc = Main.npc[index];
        npc.TargetClosest();
        npc.netUpdate = true;

        if (Main.netMode == NetmodeID.Server)
            NetMessage.SendData(MessageID.SyncNPC, number: index);
    }

    private void ShowMessage(string message, Color color)
    {
        if (Main.netMode == NetmodeID.Server)
        {
            ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), color);
        }
        else
        {
            Main.NewText(message, color.R, color.G, color.B);
        }
    }

    public override void PostSetupContent()
    {
        hasAnnouncedTonight = false;
        hasSpawnedTonight = false;
        wasDay = true;
        selectedBoss = -1;
    }
}