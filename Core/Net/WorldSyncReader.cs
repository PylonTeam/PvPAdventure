using System;
using System.IO;
using Terraria.Enums;

namespace PvPAdventure.Core.Net;

// NetReceive gets a separate, length-delimited stream from tML's BinaryIO.SafeRead.
// Validate it before committing any state: SafeRead catches IOExceptions but does
// not roll back fields or dictionaries already changed by the receiver.
internal static class WorldSyncReader
{
    internal static int ReadCount(BinaryReader reader, int minimumEntryBytes, int maximum = int.MaxValue)
    {
        int count = reader.ReadInt32();
        long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        if (count < 0 || count > maximum || count > remaining / minimumEntryBytes)
            throw new IOException("Invalid collection size in Adventure world sync.");
        return count;
    }

    internal static Team ReadTeam(BinaryReader reader)
    {
        Team team = (Team)reader.ReadInt32();
        if (!Enum.IsDefined(team))
            throw new IOException("Invalid team in Adventure world sync.");
        return team;
    }

    internal static void EnsureComplete(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new IOException("Unexpected trailing data in Adventure world sync.");
    }
}
