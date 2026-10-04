using PvPAdventure.Core.Net;
using Microsoft.Xna.Framework;
using ErkySSC.Common.SSC;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Travel.Beds;

internal sealed class TeamBedPlayer : ModPlayer
{
    private int lastSpawnX = int.MinValue;
    private int lastSpawnY = int.MinValue;

    public override void Load()
    {
        On_Player.ItemCheck_UseMiningTools += OnPlayerItemCheckUseMiningTools;
    }

    public override void Unload()
    {
        On_Player.ItemCheck_UseMiningTools -= OnPlayerItemCheckUseMiningTools;
    }

    public override void OnEnterWorld()
    {
        // Request beds claimed before this client connected after its world is ready to receive them.
        if (Main.netMode == NetmodeID.MultiplayerClient && Player.whoAmI == Main.myPlayer)
            TeamBedNetHandler.SendStateRequest();
    }

    public override void PostUpdate()
    {
        if (Main.netMode != NetmodeID.MultiplayerClient || Player.whoAmI != Main.myPlayer || SSCBedSystem.IsEnabled)
            return;

        if (Player.SpawnX == lastSpawnX && Player.SpawnY == lastSpawnY)
            return;

        lastSpawnX = Player.SpawnX;
        lastSpawnY = Player.SpawnY;

        ModPacket packet = ModContent.GetInstance<PvPAdventure>().GetPacket();
        packet.Write((byte)AdventurePacketIdentifier.TeamBed);
        packet.Write((byte)TeamBedPacketType.PlayerSpawn);
        packet.Write((byte)Main.myPlayer);
        packet.Write(Player.SpawnX);
        packet.Write(Player.SpawnY);
        packet.Send();
    }

    private void OnPlayerItemCheckUseMiningTools(On_Player.orig_ItemCheck_UseMiningTools orig, Player self, Item sitem)
    {
        RecordDestroyAttemptIfNeeded(self, sitem);
        orig(self, sitem);
    }

    private void RecordDestroyAttemptIfNeeded(Player player, Item item)
    {
        if (Main.netMode == NetmodeID.Server)
            return;

        var tbs = ModContent.GetInstance<TeamBedSystem>();

        // Clear first unconditionally, re-set below if targeting a bed
        if (Main.netMode == NetmodeID.SinglePlayer)
            tbs.ClearCurrentBedTarget(player.whoAmI);

        if (item == null || item.IsAir || item.pick <= 0 && item.axe <= 0 && item.hammer <= 0)
            return;

        if (!TeamBedSystem.TryGetBedOriginFromTile(Player.tileTargetX, Player.tileTargetY, out Point origin))
            return;

        if (Main.netMode == NetmodeID.MultiplayerClient)
            TeamBedNetHandler.SendDestroyAttempt(origin);
        else
            tbs.SetCurrentBedTarget(player.whoAmI, origin);
    }
}
