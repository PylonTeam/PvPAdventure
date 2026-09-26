using System.Reflection;
using log4net;
using Microsoft.Xna.Framework;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using PvPAdventure.Common.Game;
using PvPAdventure.Core.Compat;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.NPCs;

public sealed class ShakingChestSystem : ModSystem
{
    private const int RespawnCheckInterval = 300;

    private delegate void SetChatButtonsDelegate(ref string button, ref string button2);

    private static ILog _logger;
    private static Hook _chatButtonsHook;
    private int _respawnTimer;

    public override void Load()
    {
        _logger = Mod.Logger;
        MethodInfo method = typeof(NPCLoader).GetMethod(
            "SetChatButtons",
            BindingFlags.Public | BindingFlags.Static);
        _chatButtonsHook = new Hook(method, SetChatButtons);

        IL_Main.HoverOverNPCs += PatchBoundSlimeHover;
        On_Main.TryFreeingElderSlime += PreventFreeingTownChest;
    }

    public override void Unload()
    {
        _chatButtonsHook?.Dispose();
        _chatButtonsHook = null;
        IL_Main.HoverOverNPCs -= PatchBoundSlimeHover;
        On_Main.TryFreeingElderSlime -= PreventFreeingTownChest;
        _logger = null;
    }

    private static void SetChatButtons(
        SetChatButtonsDelegate orig,
        ref string button,
        ref string button2)
    {
        orig(ref button, ref button2);

        int talkNpc = Main.LocalPlayer.talkNPC;
        if (talkNpc >= 0 && Main.npc[talkNpc].type == ShakingChestNPC.TargetType)
        {
            button = Language.GetTextValue("LegacyInterface.28");
            button2 = "";
        }
    }

    private static bool PreventFreeingTownChest(
        On_Main.orig_TryFreeingElderSlime orig,
        int npcIndex) =>
        Main.npc[npcIndex].townNPC ? false : orig(npcIndex);

    private static void PatchBoundSlimeHover(ILContext il)
    {
        ILCursor cursor = new(il);
        if (!cursor.TryGotoNext(
            MoveType.Before,
            instruction => instruction.MatchLdfld<NPC>(nameof(NPC.type)),
            instruction => instruction.MatchLdcI4(ShakingChestNPC.TargetType)) ||
            !cursor.TryGotoNext(instruction => instruction.MatchLdcI4(ShakingChestNPC.TargetType)))
        {
            _logger?.Warn("Could not patch shaking chest hover behavior.");
            return;
        }

        cursor.Next.Operand = -1;
    }

    public override void OnWorldLoad()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient &&
            ModContent.GetInstance<GameManager>().CurrentPhase == GameManager.Phase.Waiting)
            EnsureExists();
    }

    public override void PostWorldGen()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient)
            Spawn();
    }

    public override void PostUpdateTime()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (ModContent.GetInstance<GameManager>().CurrentPhase != GameManager.Phase.Waiting)
        {
            _respawnTimer = 0;
            return;
        }

        ClearGroundItems();
        if (++_respawnTimer >= RespawnCheckInterval)
        {
            _respawnTimer = 0;
            EnsureExists();
        }
    }

    private static void ClearGroundItems()
    {
        for (int i = 0; i < Main.maxItems; i++)
        {
            if (!Main.item[i].active)
                continue;

            Main.item[i] = new Item();
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncItem, number: i);
        }
    }

    private static void EnsureExists()
    {
        if (!NPC.AnyNPCs(ShakingChestNPC.TargetType))
            Spawn();
    }

    private static void Spawn()
    {
        Rectangle area = AdventureRegionSystem.WorldArea;
        int x = area.IsEmpty ? Main.spawnTileX * 16 : area.Center.X;
        int y = (Main.spawnTileY - 1) * 16;

        int npcIndex = NPC.NewNPC(
            Entity.GetSource_NaturalSpawn(),
            x,
            y,
            ShakingChestNPC.TargetType);

        if (!area.IsEmpty && npcIndex >= 0 && npcIndex < Main.maxNPCs)
            ShakingChestNPC.PlaceInsideSpawnBox(Main.npc[npcIndex], area);
    }
}
