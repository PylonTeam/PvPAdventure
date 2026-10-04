using PvPAdventure.Core.Config.ConfigElements;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PvPAdventure.Common.Travel.UI;
using System.ComponentModel;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using PvPFramework.Core.Configs.ConfigElements;

namespace PvPAdventure.Core.Config;

public class ClientConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    public enum TravelUIPosition
    {
        Top,
        Bottom,
    }

    public enum TravelUISize
    {
        VerySmall,
        Small,
        Medium,
        Big,
    }

    public enum ScorelineSize
    {
        Small,
        Medium,
        Large,
        VeryLarge,
    }

    [Header("Display")]
    [HeaderIcon(nameof(PvPFramework.Core.Utilities.Ass.ConfigUI))]
    [BackgroundColor(36, 104, 118)]
    [DefaultValue(TravelUISize.Small)]
    [JsonConverter(typeof(StringEnumConverter))]
    public TravelUISize PortalTravelUISize = TravelUISize.Small;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(TravelUIPosition.Top)]
    [JsonConverter(typeof(StringEnumConverter))]
    public TravelUIPosition PortalTravelUIPosition = TravelUIPosition.Top;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(ScorelineSize.Medium)]
    [JsonConverter(typeof(StringEnumConverter))]
    public ScorelineSize ScorelineUISize = ScorelineSize.Medium;

    [Header("Warnings")]
    [BackgroundColor(36, 104, 118)]
    [DefaultValue(true)]
    public bool ShowPortalWarnings = true;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(true)]
    public bool ShowSpawnMountWarnings = true;

    [Header("Chat")]
    [BackgroundColor(70, 92, 126)]
    [DefaultValue(false)]
    public bool ShowDebugMessages = false;

    public override void OnChanged()
    {
        base.OnChanged();
        Log.Chat("Client config changed");

        // Rebuild travel UI
        ModContent.GetInstance<TravelUISystem>()?.travelUIState?.ForceRebuildNextUpdate();
    }
}
