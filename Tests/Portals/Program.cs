using Microsoft.Xna.Framework;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Travel;
using PvPAdventure.Common.Travel.Portals;
using PvPAdventure.Content.Portals;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Game = Terraria.Main;

internal static class Program
{
    private static int checks, failed;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    public static int Main()
    {
        (string Name, Action Test)[] cases =
        [
            ("item-use hook rejection never fabricates a local countdown", RejectedEngineUse),
            ("engine animation without accepted use is cleared", EngineAnimationWithoutRequest),
            ("residual item cooldown never fabricates a local countdown", ResidualItemCooldown),
            ("pending request cannot be restarted after the local countdown", PendingRequestCannotRestart),
            ("zero-frame single-player creation finishes once", ZeroFrameSinglePlayer),
            ("synchronous creation failure does not revive item animation", FailedCreationDoesNotAnimate),
            ("direct item use reports server-side spawn failure", DirectUseReportsSpawnFailure),
            ("ordered controls then request survive simulated latency", DelayedRoundTrips),
            ("recently moved portal creator reaches server before use", MovedCreatorSynchronizesBeforeUse),
            ("server rejection clears pending client state", ServerRejectsAndClientClears),
            ("cancel and retry ignore the previous delayed result", CancelRetryCorrelation),
            ("late spawn after a cancellation disappears locally", LateVisualAfterCancel),
            ("movement during creation cancels on the server", ServerMovementCancellation),
            ("server projectile ownership cannot collide with player identities", ServerProjectileIdentity),
            ("client authority never creates server entities", ClientCannotCreateEntities)
        ];
        foreach ((string name, Action test) in cases)
        {
            try { Reset(); test(); Console.WriteLine($"PASS {name}"); }
            catch (Exception error) { failed++; Console.Error.WriteLine($"FAIL {name}: {error.Message}"); }
        }
        Console.WriteLine($"{cases.Length - failed}/{cases.Length} scenarios passed; {checks} assertions executed.");
        return failed == 0 ? 0 : 1;
    }

    private static void Reset()
    {
        TravelRules.Enabled = true;
        TravelRegions.InSpawn = false;
        ModContent.GetInstance<GameManager>().CurrentPhase = GameManager.Phase.Playing;
        ModContent.GetInstance<ServerConfig>().TravelSystem.TravelPortalCreationTimePreHardmodeSeconds = 1;
        Game.hardMode = false;
        Projectile.FailSpawn = false;
        NetMessage.Sent.Clear();
        ModPacket.Sent.Clear();
        NetMessage.Sending = null;
        ModPacket.Sending = null;
        PopupTextHelper.Warnings.Clear();
        new Endpoint(NetmodeID.MultiplayerClient).Bind();
    }

    private sealed class Endpoint
    {
        public readonly int Mode;
        public readonly Player[] Players = Enumerable.Range(0, Game.maxPlayers).Select(i => new Player { whoAmI = i }).ToArray();
        public readonly Projectile[] Projectiles = Enumerable.Range(0, Game.maxProjectiles).Select(_ => new Projectile()).ToArray();
        public readonly NPC[] Npcs = Enumerable.Range(0, Game.maxNPCs).Select(_ => new NPC()).ToArray();
        public Player Owner => Players[0];
        public Endpoint(int mode)
        {
            Mode = mode;
            Owner.active = true;
            Owner.team = 1;
            Owner.inventory[12].SetDefaults(1);
            Owner.ItemCheckAction = AcceptedEngineUse;
        }
        public void Bind()
        {
            Game.netMode = Mode;
            Game.myPlayer = Mode == NetmodeID.Server ? Game.maxPlayers : 0;
            Game.dedServ = Mode == NetmodeID.Server;
            Game.player = Players;
            Game.projectile = Projectiles;
            Game.npc = Npcs;
        }
        public void Tick(int frames)
        {
            Bind();
            for (int frame = 0; frame < frames; frame++)
            {
                if (Owner.itemTime > 0) Owner.itemTime--;
                if (Owner.itemAnimation > 0) Owner.itemAnimation--;
                foreach (Projectile p in Projectiles.Where(p => p.active).ToArray()) p.ModProjectile.AI();
            }
        }
    }

    // Installed tML's ItemCheck decrements itemTime, starts itemAnimation, then
    // invokes UseItem only when ItemTimeIsZero && itemAnimation > 0. A PreItemCheck
    // hook may skip the entire body. We inject those engine outcomes explicitly.
    private static void AcceptedEngineUse(Player player)
    {
        if (player.itemTime > 0) player.itemTime--;
        if (!player.HeldItem.ModItem.CanUseItem(player)) return;
        player.itemAnimation = player.HeldItem.useAnimation;
        player.itemAnimationMax = player.HeldItem.useAnimation;
        if (player.itemTime == 0 && player.itemAnimation > 0)
        {
            bool? accepted = player.HeldItem.ModItem.UseItem(player);
            // The real engine applies useTime after UseItem.
            if (accepted != false)
            {
                player.itemTime = player.HeldItem.useTime;
                player.itemTimeMax = player.HeldItem.useTime;
            }
        }
    }

    private static void RejectedEngineUse()
    {
        Player player = Game.LocalPlayer;
        player.ItemCheckAction = _ => { };
        PortalCreatorItem.TryUse(player);
        Check(ModPacket.Sent.Count == 0, "A rejected engine use must not send a creation request.");
        Check(!player.GetModPlayer<PortalPlayer>().CreationAttempt.Pending, "A rejected engine use cannot become pending.");
        Check(player.itemAnimation == 0 && player.itemTime == 0,
            $"Rejected ItemCheck fabricated countdown: animation={player.itemAnimation}, itemTime={player.itemTime}, requests={ModPacket.Sent.Count}.");
        Check(player.selectedItem == 0, "Rejected use restores the previously selected inventory slot.");
        Check(!player.controlUseItem && !player.releaseUseItem && !player.channel, "Rejected use clears the manual-use input flags.");
    }

    private static void ResidualItemCooldown()
    {
        Player player = Game.LocalPlayer;
        player.itemTime = 5;
        PortalCreatorItem.TryUse(player);
        Check(ModPacket.Sent.Count == 0, "Residual item cooldown must not send a request before accepted use.");
        Check(player.itemAnimation == 0 && player.itemTime <= 5,
            $"Residual cooldown was replaced by fake portal countdown: animation={player.itemAnimation}, itemTime={player.itemTime}.");
        player.itemTime = 0; // prior item's cooldown has now elapsed
        PortalCreatorItem.TryUse(player);
        Check(ModPacket.Sent.Count == 1 && player.GetModPlayer<PortalPlayer>().CreationAttempt.Pending,
            "Retry after the prior cooldown expires dispatches one accepted portal request.");
    }

    private static void EngineAnimationWithoutRequest()
    {
        Player player = Game.LocalPlayer;
        player.ItemCheckAction = p =>
        {
            p.itemAnimation = p.itemAnimationMax = p.HeldItem.useAnimation;
            // A hook/engine path started the visual but did not invoke UseItem.
        };
        PortalCreatorItem.TryUse(player);
        Check(ModPacket.Sent.Count == 0 && !player.GetModPlayer<PortalPlayer>().CreationAttempt.Pending,
            "Engine-only animation does not imply a dispatched request.");
        Check(player.itemAnimation == 0 && player.itemTime == 0 && player.selectedItem == 0,
            "Animation without a new request ID must be cleared and previous item restored.");
    }

    private static void PendingRequestCannotRestart()
    {
        Player player = Game.LocalPlayer;
        PortalCreatorItem.TryUse(player);
        int request = player.GetModPlayer<PortalPlayer>().CreationAttempt.RequestId;
        Check(ModPacket.Sent.Count == 1, "Accepted use sends exactly one request.");
        player.itemAnimation = player.itemTime = 0; // countdown expired before round trip returned
        PortalCreatorItem.TryUse(player);
        Check(ModPacket.Sent.Count == 1 && player.GetModPlayer<PortalPlayer>().CreationAttempt.RequestId == request,
            "A second hotkey press while waiting restarted the server countdown.");
        Check(!player.HeldItem.ModItem.CanUseItem(player), "Normal hotbar use is also blocked while awaiting this attempt.");
        Check(player.HeldItem.ModItem.UseItem(player) == false && ModPacket.Sent.Count == 1,
            "A repeated engine UseItem callback cannot dispatch a duplicate pending request.");
    }

    private static void ZeroFrameSinglePlayer()
    {
        Endpoint local = new(NetmodeID.SinglePlayer); local.Bind();
        ModContent.GetInstance<ServerConfig>().TravelSystem.TravelPortalCreationTimePreHardmodeSeconds = 0;
        PortalCreatorItem.TryUse(local.Owner);
        Check(local.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.Pending, "A zero-frame creation still has a correlated attempt before AI.");
        local.Tick(1);
        Check(local.Npcs.Count(n => n.active) == 1, "Zero-frame creation must make exactly one portal.");
        Check(!local.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.Pending, "Completion clears pending state.");
        Check(local.Owner.itemAnimation == 0 && local.Owner.itemTime == 0, "Completion resets local use state.");
    }

    private static void FailedCreationDoesNotAnimate()
    {
        Endpoint local = new(NetmodeID.SinglePlayer); local.Bind();
        Projectile.FailSpawn = true;
        PortalCreatorItem.TryUse(local.Owner);
        Check(!local.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.Pending, "Failed spawn reports cancellation synchronously.");
        Check(local.Owner.itemAnimation == 0 && local.Owner.itemTime == 0, "Synchronous failure must not be overwritten by a full local countdown.");
    }

    private static void DirectUseReportsSpawnFailure()
    {
        Endpoint local = new(NetmodeID.SinglePlayer); local.Bind();
        local.Owner.selectedItem = 12;
        Projectile.FailSpawn = true;
        Check(local.Owner.HeldItem.ModItem.UseItem(local.Owner) == false, "Failed single-player spawn must return false to the engine's normal hotbar path.");
        Check(!local.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.Pending, "Direct failure leaves no pending attempt.");
    }

    private static void Deliver(byte[] data, int sender = 0)
    {
        using BinaryReader reader = new(new MemoryStream(data));
        reader.ReadByte(); // outer AdventurePacketIdentifier is dispatched by the mod
        PortalNetHandler.HandlePacket(reader, sender);
        Check(reader.BaseStream.Position == reader.BaseStream.Length, "Handler consumes exactly its packet payload.");
    }
    private static byte[] Result() => ModPacket.Sent.Last(p => p.Bytes[1] == 4).Bytes;
    private static PortalCreationProjectile Creation() => (PortalCreationProjectile)Game.projectile.First(p => p.active).ModProjectile;
    private static byte[] Extra(PortalCreationProjectile creation)
    {
        using MemoryStream stream = new(); using BinaryWriter writer = new(stream);
        creation.SendExtraAI(writer); return stream.ToArray();
    }
    private static PortalCreationProjectile ReceiveVisual(byte[] extra, int slot = 31)
    {
        Projectile p = new() { active = true, owner = Game.maxPlayers, ai = [0, 1, 0] };
        PortalCreationProjectile creation = new() { Projectile = p }; p.ModProjectile = creation;
        creation.SetDefaults();
        using BinaryReader reader = new(new MemoryStream(extra)); creation.ReceiveExtraAI(reader);
        Game.projectile[slot] = p;
        return creation;
    }

    private static void DelayedRoundTrips()
    {
        // One-way delay of 0, 100, 500 and 1500 ms at 60 Hz. These exercise real
        // production serialization/handlers with deterministic queued transport,
        // rather than claiming to emulate the full Terraria network stack.
        foreach (int delayFrames in new[] { 0, 6, 30, 90 })
        {
            Reset(); Endpoint client = new(NetmodeID.MultiplayerClient), server = new(NetmodeID.Server);
            client.Bind();
            List<string> order = new();
            Vector2 controlsVelocity = new(100, 100);
            NetMessage.Sending = (message, player) =>
            {
                if (message == MessageID.PlayerControls) { order.Add("controls"); controlsVelocity = Game.player[player].velocity; }
            };
            ModPacket.Sending = bytes => order.Add("request");
            PortalCreatorItem.TryUse(client.Owner);
            byte[] request = ModPacket.Sent.Single().Bytes;
            Check(order.SequenceEqual(new[] { "controls", "request" }), "Fresh movement must be queued before portal request.");
            NetMessage.Sending = null; ModPacket.Sending = null;
            client.Tick(delayFrames);
            server.Bind(); server.Owner.velocity = new(8, 0); // stale motion from previous input packet
            server.Owner.velocity = controlsVelocity; // same ordered transport delivers controls first
            Deliver(request);
            Check(server.Projectiles.Count(p => p.active) == 1, $"Server starts after delayed fresh controls ({delayFrames} frames).");
            byte[] visual = Extra(Creation());
            server.Tick(60);
            byte[] result = Result();
            Check(server.Npcs.Count(n => n.active) == 1, $"Server created portal after {delayFrames}-frame delay.");
            client.Bind(); ReceiveVisual(visual); client.Tick(delayFrames + 60);
            Deliver(result);
            Check(!client.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.Pending, "Delayed completion clears owner pending.");
            Check(client.Owner.itemAnimation == 0 && client.Projectiles.All(p => !p.active), "Delayed completion clears countdown and visual.");
            Check(client.Npcs.All(n => !n.active), "Client handlers never locally manufacture the authoritative NPC.");
        }
    }

    private static void MovedCreatorSynchronizesBeforeUse()
    {
        Endpoint client = new(NetmodeID.MultiplayerClient), server = new(NetmodeID.Server);
        client.Bind();
        List<string> order = new();
        NetMessage.Sending = (message, player) =>
        {
            if (message == MessageID.SyncEquipment) order.Add("equipment");
            if (message == MessageID.PlayerControls) order.Add("controls");
        };
        ModPacket.Sending = _ => order.Add("request");
        PortalCreatorItem.TryUse(client.Owner); byte[] request = ModPacket.Sent.Last().Bytes;
        bool equipmentQueued = NetMessage.Sent.Any(p => p.Message == MessageID.SyncEquipment && p.Number == 0 && p.Number2 == 12);
        NetMessage.Sending = null; ModPacket.Sending = null;
        server.Bind(); server.Owner.inventory[12] = new();
        if (equipmentQueued) server.Owner.inventory[12].SetDefaults(client.Owner.inventory[12].type); // ordered SyncEquipment
        Deliver(request);
        Check(server.Projectiles.Count(p => p.active) == 1,
            $"Just-moved item failed server validation: equipment packet={equipmentQueued}, server creation projectiles={server.Projectiles.Count(p => p.active)}.");
        Check(order.SequenceEqual(new[] { "equipment", "controls", "request" }), "Inventory and movement must both arrive before the use request.");
        Check(equipmentQueued, "Inventory sync identifies the owner and newly selected inventory slot.");
    }

    private static void ServerRejectsAndClientClears()
    {
        Endpoint client = new(NetmodeID.MultiplayerClient), server = new(NetmodeID.Server);
        client.Bind(); PortalCreatorItem.TryUse(client.Owner); byte[] request = ModPacket.Sent.Single().Bytes;
        server.Bind(); server.Owner.inventory[12] = new(); Deliver(request); byte[] result = Result();
        Check(server.Projectiles.All(p => !p.active), "Invalid inventory is rejected by server.");
        client.Bind(); Deliver(result);
        Check(!client.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.Pending && client.Owner.itemAnimation == 0,
            "Rejection clears client's pending animation.");
        Check(PopupTextHelper.Warnings.Contains("Mods.PvPAdventure.PortalCreator.Cancelled"), "Rejection gives local cancellation feedback.");
    }

    private static void CancelRetryCorrelation()
    {
        Endpoint client = new(NetmodeID.MultiplayerClient), server = new(NetmodeID.Server);
        client.Bind(); PortalCreatorItem.TryUse(client.Owner); byte[] request1 = ModPacket.Sent.Last().Bytes;
        int id1 = client.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.RequestId;
        PortalSystem.FinishCreationLocally(0, id1, false); PortalNetHandler.SendPortalCreationCancel(id1);
        byte[] cancel1 = ModPacket.Sent.Last().Bytes;
        PortalCreatorItem.TryUse(client.Owner); byte[] request2 = ModPacket.Sent.Last().Bytes;
        int id2 = client.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.RequestId;
        Check(id2 != id1, "Retry gets a distinct ID.");
        server.Bind(); Deliver(request1); Deliver(cancel1); byte[] result1 = Result(); Deliver(request2);
        Check(Creation().RequestId == id2, "Server retains the retry after prior cancel.");
        Deliver(cancel1);
        Check(Creation().RequestId == id2, "Repeated old cancellation cannot kill the retry.");
        client.Bind(); Deliver(result1);
        Check(client.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.IsPending(id2) && client.Owner.itemAnimation > 0,
            "Old result cannot reset the retry animation or pending ID.");
        server.Tick(60); byte[] result2 = Result(); client.Bind(); Deliver(result2);
        Check(!client.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.Pending && server.Npcs.Count(n => n.active) == 1,
            "Retry completes exactly once.");
    }

    private static void LateVisualAfterCancel()
    {
        Endpoint client = new(NetmodeID.MultiplayerClient), server = new(NetmodeID.Server);
        client.Bind(); PortalCreatorItem.TryUse(client.Owner); byte[] request = ModPacket.Sent.Last().Bytes;
        int id = client.Owner.GetModPlayer<PortalPlayer>().CreationAttempt.RequestId;
        PortalSystem.FinishCreationLocally(0, id, false);
        server.Bind(); Deliver(request); byte[] visual = Extra(Creation());
        client.Bind(); PortalCreationProjectile late = ReceiveVisual(visual);
        NetMessage.Sent.Clear(); late.AI();
        Check(!late.Projectile.active, "Late server visual cannot revive a locally canceled attempt.");
        Check(NetMessage.Sent.All(p => p.Message != MessageID.KillProjectile), "Local cleanup must never send KillProjectile back to the server.");
    }

    private static void ServerMovementCancellation()
    {
        Endpoint server = new(NetmodeID.Server); server.Bind();
        PortalSystem.StartPortalCreation(server.Owner, 7); server.Owner.controlRight = true; server.Tick(1);
        Check(server.Projectiles.All(p => !p.active) && server.Npcs.All(n => !n.active), "Movement cancels server creation before an NPC is made.");
        Check(Result().Last() == 0, "Server broadcasts failed result for movement cancellation.");
    }

    private static void ServerProjectileIdentity()
    {
        Endpoint server = new(NetmodeID.Server); server.Bind(); PortalSystem.StartPortalCreation(server.Owner, 1234567);
        PortalCreationProjectile creation = Creation();
        Check(creation.Projectile.owner == Game.maxPlayers && creation.OwnerIndex == 0,
            "Server visual uses server owner sentinel while preserving player identity in AI.");
        byte[] extra = Extra(creation);
        Endpoint client = new(NetmodeID.MultiplayerClient); client.Bind(); PortalCreationProjectile decoded = ReceiveVisual(extra);
        Check(decoded.RequestId == 1234567, "Integer request ID round trips without float truncation.");
    }

    private static void ClientCannotCreateEntities()
    {
        Check(!PortalSystem.StartPortalCreation(Game.LocalPlayer, 1), "Client cannot spawn server creation projectile.");
        Check(!PortalSystem.CreateOrReplacePortal(Game.LocalPlayer, Vector2.Zero), "Client cannot spawn authoritative portal NPC.");
    }
}
