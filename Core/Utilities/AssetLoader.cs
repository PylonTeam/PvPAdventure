using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace PvPAdventure.Core.Utilities;

/// <summary>
/// Provides static access to miscallaneous texture assets within the PvPAdventure mod.
/// Automatically initializes when the mod system loads.
/// All asset fields are intended for global access throughout the mod.
/// </summary>
public static class Ass
{
    // Map backgrounds
    public static Asset<Texture2D>[] MapBG;

    // Spawn selector assets
    public static Asset<Texture2D> IconDead; // 32x32
    public static Asset<Texture2D> IconForbidden;
    public static Asset<Texture2D> IconQuestionMark;
    public static Asset<Texture2D> Shimmer;

    // Admin tools assets
    public static Asset<Texture2D> IconReset;
    public static Asset<Texture2D> IconResize;
    public static Asset<Texture2D> IconTime;
    public static Asset<Texture2D> IconStartGame;
    public static Asset<Texture2D> IconEndGame;
    public static Asset<Texture2D> Stopwatch;

    // Config icons
    public static Asset<Texture2D> ConfigBed;
    //public static Asset<Texture2D> ConfigBedOutline;
    public static Asset<Texture2D> ConfigBoundNPC;
    //public static Asset<Texture2D> ConfigBoundNPCOutline;
    //public static Asset<Texture2D> ConfigChat;
    public static Asset<Texture2D> ConfigMapWorldSpawn;
    public static Asset<Texture2D> ConfigPlanterasBulb;
    public static Asset<Texture2D> ConfigPlayerHead;
    //public static Asset<Texture2D> ConfigPlayerOutline;
    //public static Asset<Texture2D> ConfigProjectile;
    //public static Asset<Texture2D> ConfigProjectileOutline;
    //public static Asset<Texture2D> ConfigPvP;
    //public static Asset<Texture2D> ConfigTreasureBag;
    //public static Asset<Texture2D> ConfigTreasureBagOutline;

    // Scoreboard
    public static Asset<Texture2D> IconPointsSetter;

    // Flag
    public static bool Initialized { get; set; }

    /// <summary>
    /// Initializes static assets
    /// Automatically runs once the mod system loads via <see cref="AssetLoader"/>
    /// </summary>
    static Ass()
    {
        if (Main.dedServ)
        {
            Initialized = true;
            return;
        }

        const string ModName = "PvPAdventure";

        // Custom initialize MapBGs
        MapBG = new Asset<Texture2D>[42];
        for (int i = 1; i <= 42; i++)
            MapBG[i - 1] = ModContent.Request<Texture2D>($"{ModName}/Assets/Custom/MapBGs/MapBG{i}", AssetRequestMode.AsyncLoad);


        // Use a tuple to store both the field name and the path we searched for
        List<(string AssetName, string Path)> missingAssets = [];

        FieldInfo[] fields = typeof(Ass).GetFields(BindingFlags.Public | BindingFlags.Static);

        foreach (FieldInfo field in fields)
        {
            if (field.FieldType != typeof(Asset<Texture2D>))
                continue;

            field.SetValue(null, RequestTexture(field.Name, $"{ModName}/Assets/Custom/{field.Name}", missingAssets));
        }

        // Check if any assets failed to load
        if (missingAssets.Count > 0)
        {
            // Try to get all valid asset paths in the mod to use for Levenshtein comparison
            string[] validKeys = [];
            if (ModLoader.TryGetMod(ModName, out Mod myMod))
            {
                validKeys = myMod.GetFileNames().ToArray();
            }

            throw new MissingAssetException(missingAssets, validKeys);
        }

        Initialized = true;
    }

    private static Asset<Texture2D> RequestTexture(string assetName, string path, List<(string, string)> missingAssets)
    {
        if (!ModContent.HasAsset(path))
        {
            missingAssets.Add((assetName, path));
            return null; // Return null temporarily, we will crash shortly
        }

        return ModContent.Request<Texture2D>(path, AssetRequestMode.AsyncLoad);
    }
}

/// <summary>
/// Initializes asset loading for the mod when the system is loaded with all assets in <see cref="Ass"/>
/// </summary>
public class AssetLoader : ModSystem
{
    public override void Load() => _ = Ass.Initialized;
}

internal sealed class MissingAssetException : Exception
{
    public override string HelpLink => "https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-FAQ#terrariamodloadermodgettexturestring-name-error";

    public MissingAssetException(List<(string AssetName, string Path)> missingAssets, ICollection<string> validKeys)
        : base(BuildErrorMessage(missingAssets, validKeys))
    {
    }

    private static string BuildErrorMessage(List<(string AssetName, string Path)> missingAssets, ICollection<string> validKeys)
    {
        string message = $"Missing {missingAssets.Count} asset{(missingAssets.Count == 1 ? "" : "s")}:\n";

        foreach (var missing in missingAssets)
            message += $"Failed to load Ass.{missing.AssetName}: \"{missing.Path}\"\n";

        List<string> suggestions = [];

        if (validKeys != null && validKeys.Count > 0)
        {
            string[] keys = validKeys.ToArray();

            foreach (var missing in missingAssets)
            {
                string closestMatch = LevenshteinDistance.FolderAwareEditDistance(missing.Path, keys);
                if (!string.IsNullOrEmpty(closestMatch))
                    suggestions.Add($"Possible misspelling: \"{missing.Path}\" -> \"{closestMatch}\"");
            }
        }

        if (suggestions.Count > 0)
        {
            message += "-------------------------\n";
            message += "More info on how you can fix the missing assets:\n";
            message += string.Join("\n", suggestions);
        }

        return message;
    }
}

static class LevenshteinDistance
{
    internal static string FolderAwareEditDistance(string source, string[] targets)
    {
        if (targets.Length == 0) return null;
        var separator = '/';
        var sourceParts = source.Split(separator);

        var sourceFolders = Enumerable.Reverse(sourceParts).Skip(1).ToList();
        var sourceFile = sourceParts.Last();

        int missingFolderPenalty = 4;
        int extraFolderPenalty = 3;

        var scores = targets.Select(target => {
            var targetParts = target.Split(separator);

            var targetFolders = Enumerable.Reverse(targetParts).Skip(1).ToList();
            var targetFile = targetParts.Last();

            var commonFolders = sourceFolders.Where(x => targetFolders.Contains(x));
            var reducedSourceFolders = sourceFolders.Except(commonFolders).ToList();
            var reducedTargetFolders = targetFolders.Except(commonFolders).ToList();

            int score = 0;
            int folderDiff = reducedSourceFolders.Count - reducedTargetFolders.Count;
            if (folderDiff > 0)
                score += folderDiff * missingFolderPenalty;
            else if (folderDiff < 0)
                score += -folderDiff * extraFolderPenalty;

            if (reducedSourceFolders.Count > 0 && reducedSourceFolders.Count >= reducedTargetFolders.Count)
            {
                foreach (var item in reducedTargetFolders)
                {
                    int min = Int32.MaxValue;
                    foreach (var item2 in reducedSourceFolders)
                    {
                        min = Math.Min(min, LevenshteinDistance.Compute(item, item2));
                    }
                    score += min;
                }
            }
            else if (reducedSourceFolders.Count > 0)
            {
                foreach (var item in reducedSourceFolders)
                {
                    int min = Int32.MaxValue;
                    foreach (var item2 in reducedTargetFolders)
                    {
                        min = Math.Min(min, LevenshteinDistance.Compute(item, item2));
                    }
                    score += min;
                }
            }
            score += LevenshteinDistance.Compute(targetFile, sourceFile);

            return new
            {
                Target = target,
                Score = score
            };
        });
        return scores.OrderBy(x => x.Score).First().Target;
    }

    public static int Compute(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 2;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 2, d[i, j - 1] + 2),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}
