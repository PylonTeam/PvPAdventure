using PvPAdventure.Core.Config.ConfigElements;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PvPAdventure.Common.Travel.UI;
using System;
using System.ComponentModel;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using PvPFramework.Core.Configs.ConfigElements;

namespace PvPAdventure.Core.Config;

public class ClientConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [DefaultValue(true)]
    public bool ShowSpawnMountWarnings = true;

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

    [Header("UI")]
    [HeaderIcon(nameof(PvPFramework.Core.Utilities.Ass.ConfigUI))]
    [BackgroundColor(36, 104, 118)]
    [DefaultValue(true)]
    public bool TravelUI = true;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(TravelUISize.Small)]
    [JsonConverter(typeof(StringEnumConverter))]
    [RequiresField(nameof(TravelUI))]
    public TravelUISize PortalTravelUISize = TravelUISize.Small;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(TravelUIPosition.Top)]
    [JsonConverter(typeof(StringEnumConverter))]
    [RequiresField(nameof(TravelUI))]
    public TravelUIPosition PortalTravelUIPosition = TravelUIPosition.Top;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(true)]
    public bool ShowPortalWarnings = true;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(true)]
    public bool Scoreline = true;

    [BackgroundColor(36, 104, 118)]
    [DefaultValue(ScorelineSize.Medium)]
    [JsonConverter(typeof(StringEnumConverter))]
    [RequiresField(nameof(Scoreline))]
    public ScorelineSize ScorelineUISize = ScorelineSize.Medium;



    [BackgroundColor(70, 92, 126)]
    [DefaultValue(true)]
    public bool ShowTeleportPlayerMessages = true;

    [BackgroundColor(70, 92, 126)]
    [DefaultValue(false)]
    public bool ShowDebugMessages = false;

    #region NestedConfigTypes
    #endregion

    #region Methods
    public override void OnChanged()
    {
        base.OnChanged();
        Log.Chat("Client config changed");

        // Rebuild travel UI
        var travelUISystem = ModContent.GetInstance<TravelUISystem>();
        if (travelUISystem != null)
        {
            travelUISystem?.travelUIState?.ForceRebuildNextUpdate();
        }
    }
    #endregion
}
