From the PvPAdventure directory, compile without packaging or installing the mod:

```powershell
dotnet msbuild PvPAdventure.csproj -t:Compile -p:Configuration=Release -p:BuildProjectReferences=false
dotnet run --project Tests/WorldSync/WorldSync.csproj -- 'D:\Steam\steamapps\common\tModLoader' '..'
```

The dependency mods must already have Release/net8.0 builds. Supply your local
tModLoader and ModSources paths to the runner if they differ. It loads the actual
compiled Adventure mod, uses its real NetSend/NetReceive methods, and checks every
truncation boundary, trailing data, invalid counts/teams/phase, omitted team
defaults, unchanged state after failure, and recovery with valid snapshots.
No game, server, or network connection is started.
