using System.Reflection;
using System.Runtime.Loader;

// Resolve the actual game/mod assemblies before JIT-loading test code. No game,
// graphics device, Steam session, server or network connection is started.
string tmlDirectory = Path.GetFullPath(args[0]);
string sources = Path.GetFullPath(args[1]);
string[] dependencies = Directory.GetFiles(Path.Combine(tmlDirectory, "Libraries"), "*.dll", SearchOption.AllDirectories)
    .Concat(Directory.GetFiles(Path.Combine(sources, "ErkySSC/lib"), "*.dll")).ToArray();
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string platform = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
    string[] matches = dependencies.Where(p => Path.GetFileNameWithoutExtension(p) == name.Name).ToArray();
    string path = matches.FirstOrDefault(p => p.Replace('\\', '/').Contains($"/runtimes/{platform}/lib/"))
        ?? matches.FirstOrDefault(p => !p.Replace('\\', '/').Contains("/runtimes/"));
    return path == null ? null : context.LoadFromAssemblyPath(path);
};
Assembly tml = Assembly.LoadFrom(Path.Combine(tmlDirectory, "tModLoader.dll"));
tml.GetType("Terraria.Program").GetField("SavePath").SetValue(null, AppContext.BaseDirectory);
foreach (string mod in new[] { "ErkySSC", "PvPFramework", "Pylon" })
    Assembly.LoadFrom(Path.Combine(sources, mod, "bin/Release/net8.0", mod + ".dll"));
Assembly.LoadFrom(Path.Combine(sources, "PvPAdventure/obj/Release/net8.0/PvPAdventure.dll"));
WorldSyncTests.Run();
