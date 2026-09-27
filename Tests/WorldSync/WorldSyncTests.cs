using System.Reflection;
using System.Runtime.CompilerServices;
using PvPAdventure.Common.Bounties;
using PvPAdventure.Common.Game;
using PvPAdventure.Common.Statistics;
using PvPAdventure.Core.Config;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;

internal static class WorldSyncTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int checks;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Run()
    {
        Main.netMode = NetmodeID.MultiplayerClient;
        Main.dedServ = false;
        var config = new ServerConfig();
        config.Points.TeamStartingPoints = 100;
        typeof(ModContent).Assembly.GetType("Terraria.ModLoader.ContentInstance")
            .GetMethod("Register", All).Invoke(null, [config]);

        var points = new PointsManager();
        points.ResetForMatch();
        points._points[Team.Red] = 937;
        points.DownedNpcs[Team.Red].Add(NPCID.EyeofCthulhu);
        ExerciseTruncation(points);
        Reject(points, Bytes(w => w.Write(-1)));
        Reject(points, Bytes(w => w.Write(int.MaxValue)));
        Reject(points, Bytes(w => { w.Write(1); w.Write(99); w.Write(123); w.Write(0); }));
        Reject(points, Bytes(w => { w.Write(2); w.Write((int)Team.Red); w.Write(1); w.Write((int)Team.Red); w.Write(2); w.Write(0); }));
        Reject(points, Bytes(w => { w.Write(0); w.Write(1); w.Write((int)Team.Red); w.Write(int.MaxValue); }));

        Receive(points, Bytes(w => { w.Write(1); w.Write((int)Team.Blue); w.Write(543); w.Write(0); }));
        Check(points.Points[Team.Blue] == 543 && points.Points[Team.Red] == 100 && points.Points[Team.None] == 0,
            "sparse score snapshots supply defaults for every team");
        Check(Enum.GetValues<Team>().All(t => points.DownedNpcs.ContainsKey(t)), "sparse boss snapshots include every team");
        Receive(points, Bytes(w => { w.Write(0); w.Write(0); }));
        Check(points.Points[Team.Blue] == 100, "empty valid snapshots reset stale scores");

        var bounties = new BountyManager();
        Receive(bounties, BountyPayload(23));
        ExerciseTruncation(bounties);
        Reject(bounties, Bytes(w => w.Write(-1)));
        Reject(bounties, Bytes(w => { w.Write(1); w.Write((int)Team.Red); w.Write(-1); w.Write(24); }));
        Reject(bounties, Bytes(w => { w.Write(1); w.Write((int)Team.Red); w.Write(1); w.Write(1); w.Write(int.MaxValue); w.Write(24); }));
        Check(bounties.TransactionId == 23 && bounties.Bounties[Team.Red].Count == 1, "bad bounties preserve transaction and pages");
        Receive(bounties, Bytes(w => { w.Write(0); w.Write(24); }));
        Check(bounties.TransactionId == 24 && Enum.GetValues<Team>().All(t => bounties.Bounties[t].Count == 0),
            "valid bounty snapshot after failures clears old pages and includes every team");

        var game = new GameManager();
        Receive(game, Bytes(w => { w.Write(900); w.Write((int)GameManager.Phase.Playing); w.Write(true); w.Write(42); }));
        ExerciseTruncation(game);
        Reject(game, Bytes(w => { w.Write(100); w.Write(99); w.Write(false); }));
        Receive(game, Bytes(w => { w.Write(800); w.Write((int)GameManager.Phase.Waiting); w.Write(false); }));
        Check(game.TimeRemaining == 800 && game.CurrentPhase == GameManager.Phase.Waiting && game._startGameCountdown == null,
            "valid match snapshot replaces phase, clock and countdown after failures");
        ExerciseTruncation(game);

        var progression = (ModSystem)Activator.CreateInstance(typeof(GameManager).Assembly
            .GetType("PvPAdventure.Common.World.BespokeProgression"), true);
        Receive(progression, Bytes(w => { w.Write(6); w.Write(true); w.Write(true); w.Write(false); }));
        ExerciseTruncation(progression);

        Console.WriteLine($"{checks} world-sync regression checks passed against the compiled mod and real tModLoader types.");
    }

    private static byte[] BountyPayload(int transaction) => Bytes(w =>
    {
        w.Write(1); // One team.
        w.Write((int)Team.Red);
        w.Write(1); // One page.
        w.Write(1); // One bounty.
        w.Write(0); // No items needed to exercise the nested framing.
        w.Write(transaction);
    });

    private static void ExerciseTruncation(ModSystem system)
    {
        byte[] snapshot = Send(system);
        // Covers empty payloads and failure after every successfully read field.
        for (int length = 0; length < snapshot.Length; length++)
            Reject(system, snapshot[..length]);
        Reject(system, [.. snapshot, 0xff]);
        Receive(system, snapshot);
        Check(Send(system).SequenceEqual(snapshot), $"{system.GetType().Name}: valid round trip after rejected packets");
    }

    private static void Reject(ModSystem system, byte[] payload)
    {
        byte[] before = Send(system);
        try
        {
            Receive(system, payload);
            throw new Exception($"{system.GetType().Name} accepted invalid {payload.Length}-byte payload");
        }
        catch (IOException)
        {
            Check(Send(system).SequenceEqual(before), $"{system.GetType().Name}: invalid packet changed state");
        }
    }

    private static byte[] Send(ModSystem system) => Bytes(system.NetSend);

    private static void Receive(ModSystem system, byte[] payload)
    {
        using var reader = new BinaryReader(new MemoryStream(payload, writable: false));
        system.NetReceive(reader);
    }

    private static byte[] Bytes(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        write(writer);
        return stream.ToArray();
    }

    private static void Check(bool passed, string description)
    {
        if (!passed) throw new Exception(description);
        checks++;
    }
}
