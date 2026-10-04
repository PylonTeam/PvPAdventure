using Terraria;
using Terraria.GameContent.Creative;
using Terraria.GameContent.NetModules;
using Terraria.ID;
using Terraria.Net;

namespace PvPAdventure.Common.Game;

/// <summary>Adventure's lobby NPC cleanup and world-time policy. Framework handles player staging.</summary>
internal static class AdventureWorldRules
{
    public static void EnterLobby()
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        foreach (NPC npc in Main.ActiveNPCs)
        {
            if (npc.townNPC || npc.isLikeATownNPC || npc.type == NPCID.TargetDummy) continue;
            npc.life = 0;
            npc.netSkip = -1;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, number: npc.whoAmI);
        }
        SetTimeFrozen(true);
    }

    public static void SetTimeFrozen(bool frozen)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient) return;
        var power = CreativePowerManager.Instance.GetPower<CreativePowers.FreezeTime>();
        power.SetPowerInfo(frozen);
        if (Main.netMode != NetmodeID.Server) return;
        var packet = NetCreativePowersModule.PreparePacket(power.PowerId, 1);
        packet.Writer.Write(power.Enabled);
        NetManager.Instance.Broadcast(packet);
    }
}
