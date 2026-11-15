using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
namespace PvPAdventure.Common.NPCs;

/// <summary>
/// Makes bosses not do anything for a period after they spawn by replacing their AI with the LunaticDevote AI and preventing them from getting hit
/// </summary>
internal class BossPausePeriod : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private static readonly Dictionary<int, int> PauseDurations = new()
    {
        { NPCID.Plantera,          60 * 60 },
        { NPCID.CultistBoss,       60 * 60 },
        { NPCID.SkeletronHead,     60 * 60 },
        { NPCID.WallofFlesh,       60 * 60 },
    };

    // Optional extra conditions
    private static readonly Dictionary<int, Func<bool>> PauseConditions = new()
    {
        // Wall of Flesh is only paused until it has been defeated once
        { NPCID.WallofFlesh, () => !Main.hardMode },
    };

    private int _pauseTimer;
    private int _originalAiStyle;
    private bool _hasOriginalAiStyle;
    private bool _isPaused;
    private bool _timedPause; // true while a countdown should keep ticking (the initial spawn-window pause); false for an externally-forced pause with no timer

    public bool IsPaused => _isPaused;

    public override void OnSpawn(NPC npc, Terraria.DataStructures.IEntitySource source)
    {
        if (!PauseDurations.TryGetValue(npc.type, out int duration))
            return;

        if (PauseConditions.TryGetValue(npc.type, out Func<bool> condition) && !condition())
            return;

        _pauseTimer = duration;
        _timedPause = true;
        CaptureOriginalAiStyle(npc);
        _isPaused = true;
        ApplyPause(npc);
    }

    // (see BossDespawnRework)
    private static readonly HashSet<int> ExternallyManagedTypes = new() { NPCID.CultistBoss };

    public override void AI(NPC npc)
    {
        if (!_isPaused || !_timedPause)
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        _pauseTimer--;
        if (_pauseTimer <= 0)
        {
            if (ExternallyManagedTypes.Contains(npc.type))
            {
                _timedPause = false;
                npc.netUpdate = true;
            }
            else
            {
                RemovePause(npc);
                NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
            }
        }
    }

    public override bool? CanBeHitByItem(NPC npc, Player player, Item item)
        => _isPaused ? false : null;

    public override bool? CanBeHitByProjectile(NPC npc, Projectile projectile)
        => _isPaused ? false : null;

    public void ForcePause(NPC npc)
    {
        if (_isPaused)
            return;

        CaptureOriginalAiStyle(npc);
        _timedPause = false;
        _isPaused = true;
        ApplyPause(npc);
        npc.netUpdate = true;
    }

    public void ForceUnpause(NPC npc)
    {
        if (!_isPaused || _timedPause)
            return;

        RemovePause(npc);
        npc.netUpdate = true;
    }

    private void CaptureOriginalAiStyle(NPC npc)
    {
        if (_hasOriginalAiStyle)
            return;

        _originalAiStyle = npc.aiStyle;
        _hasOriginalAiStyle = true;
    }

    private void ApplyPause(NPC npc)
    {
        npc.aiStyle = NPCAIStyleID.LunaticDevote;
        npc.dontTakeDamage = true;
    }

    private void RemovePause(NPC npc)
    {
        npc.aiStyle = _originalAiStyle;
        npc.dontTakeDamage = false;
        _isPaused = false;
    }

    public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter binaryWriter)
    {
        bitWriter.WriteBit(_isPaused);
        bitWriter.WriteBit(_timedPause);
        binaryWriter.Write(_pauseTimer);
        binaryWriter.Write(_originalAiStyle);
    }

    public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader binaryReader)
    {
        bool wasPaused = _isPaused;
        _isPaused = bitReader.ReadBit();
        _timedPause = bitReader.ReadBit();
        _pauseTimer = binaryReader.ReadInt32();
        _originalAiStyle = binaryReader.ReadInt32();
        _hasOriginalAiStyle = true;

        if (_isPaused && !wasPaused)
            ApplyPause(npc);
        else if (!_isPaused && wasPaused)
            RemovePause(npc);
    }
}