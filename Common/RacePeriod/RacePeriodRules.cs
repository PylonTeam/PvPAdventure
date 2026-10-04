using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using PvPAdventure.Core.Config;

#nullable enable

namespace PvPAdventure.Common.RacePeriod;

/// <summary>
/// Adventure race rules. Pylon provides a mount per player; invalid or missing selections
/// always become the bunny so RacePeriod never depends on an inventory item.
/// </summary>
public static class RacePeriodRules
{
    public static Func<bool>? WaitingProvider { get; set; }
    public static Func<Player, bool>? EntryAreaProvider { get; set; }
    public static Func<PlayerDrawLayer, bool>? KeepDrawLayerProvider { get; set; }

    public const int FallbackMountType = MountID.Bunny;
    public const float DefaultRunSpeed = 8f;
    public static float FullRunSpeed =>
        ModContent.GetInstance<ServerConfig>()?.RacePeriod?.RacePeriodRunSpeed ?? DefaultRunSpeed;
    public static float BaseRunSpeed => FullRunSpeed * 22f / 40f;
    public const float RunAcceleration = 0.13f;
    public const float SlowRunAcceleration = RunAcceleration / 5f;
    public const float RunSlowdown = 0.6f;

    public const int MaxAirJumps = 20;
    public const float AirJumpHeightMultiplier = 2.5f;
    public const int AirJumpCooldownFrames = 40;

    /// <summary>
    /// Jump parity is read from the fallback animal's own mount data, so every spirit animal jumps
    /// exactly like the bunny always did and a new animal can never bring its own jump stats.
    /// </summary>
    private static Mount.MountData FallbackMountData => Mount.mounts[FallbackMountType];

    public static float JumpSpeed => FallbackMountData.jumpSpeed;

    public static int JumpHeight => FallbackMountData.jumpHeight;

    /// <summary>
    /// Optional integration point for a game mode or cosmetic mod. PvP Adventure assigns this to
    /// PvPHub's synchronized SpiritAnimalPlayer selection during Load and clears it during Unload.
    /// </summary>
    public static Func<Player, int>? MountTypeProvider { get; set; }

    /// <summary>
    /// Optional client-side artwork provider. Returning null keeps vanilla Mount.Draw; returning a
    /// sheet replaces only the visuals while the selected mount continues to provide mount state.
    /// </summary>
    public static Func<Player, RacePeriodSpriteSheet?>? SpriteSheetProvider { get; set; }

    public static int GetMountType(Player player)
    {
        int mountType = MountTypeProvider?.Invoke(player) ?? FallbackMountType;
        return mountType >= 0 && mountType < MountLoader.MountCount
            ? mountType
            : FallbackMountType;
    }

    public static RacePeriodSpriteSheet? GetSpriteSheet(Player player)
    {
        RacePeriodSpriteSheet? spriteSheet = SpriteSheetProvider?.Invoke(player);
        return spriteSheet is { IsValid: true } ? spriteSheet : null;
    }
}
