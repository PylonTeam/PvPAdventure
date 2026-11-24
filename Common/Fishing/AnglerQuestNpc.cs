using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Fishing;

internal sealed class AnglerQuestNpc : GlobalNPC
{
    public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.Angler;

    public override bool PreChatButtonClicked(NPC npc, bool firstButton)
    {
        if (!firstButton || !AnglerQuestSystem.IsPlaying)
            return true;

        SoundEngine.PlaySound(SoundID.MenuTick);
        AnglerQuestSystem.Request(AnglerQuestAction.Interact, AnglerQuestSystem.LocalView.Revision, npc.whoAmI);
        return false;
    }

    internal static void ShowQuest(int npcIndex, AnglerQuestView view)
    {
        if (npcIndex < 0 || npcIndex >= Main.maxNPCs || Main.LocalPlayer.talkNPC != npcIndex ||
            Main.npc[npcIndex] is not { active: true, type: NPCID.Angler })
            return;

        bool earlyChapterFinished = view.Notice == AnglerQuestNotice.PreHardmodeChapterFinished;
        string dialogue = earlyChapterFinished ? "PreHardmodeChapterFinished" : view.Status switch
        {
            AnglerQuestStatus.WaitForHardmode => "WaitForHardmode",
            AnglerQuestStatus.WaitForMechanicalBoss => "WaitForMechanicalBoss",
            AnglerQuestStatus.WaitForPlantera => "WaitForPlantera",
            AnglerQuestStatus.WaitForGolem => "WaitForGolem",
            AnglerQuestStatus.Complete => "Complete",
            _ => null,
        };
        if (dialogue != null)
        {
            Main.npcChatCornerItem = earlyChapterFinished ? ItemID.GoldenFishingRod : ItemID.None;
            Main.npcChatText = Language.GetTextValue("Mods.PvPAdventure.Angler." + dialogue);
            return;
        }

        int index = Array.IndexOf(Main.anglerQuestItemNetIDs, view.Fish);
        if (index < 0)
            return;

        int worldQuest = Main.anglerQuest;
        bool worldQuestFinished = Main.anglerQuestFinished;
        try
        {
            // The native dialogue also sets the native quest fish icon.
            Main.anglerQuest = index;
            Main.anglerQuestFinished = false;
            Main.npcChatText = Lang.AnglerQuestChat();
        }
        finally
        {
            Main.anglerQuest = worldQuest;
            Main.anglerQuestFinished = worldQuestFinished;
        }
    }
}
