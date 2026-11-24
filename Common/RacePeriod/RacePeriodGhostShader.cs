using ErkySSC.Common.RegionProtection.VFX;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.RuntimeDetour;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace PvPAdventure.Common.RacePeriod;

[Autoload(Side = ModSide.Client)]
internal sealed class RacePeriodGhostShader : ModSystem
{
    internal const int BodyShader = int.MinValue;

    private delegate void SetShaderForData(Player player, int headShader, ref DrawData data);
    private delegate void ShaderHook(SetShaderForData original, Player player, int headShader, ref DrawData data);

    private ArmorShaderData shader;
    private EffectParameter wave;
    private Hook hook;

    public override void Load()
    {
        Asset<Effect> effect = Mod.Assets.Request<Effect>("Assets/Effects/SpiritAnimalGhost", AssetRequestMode.ImmediateLoad);
        shader = new ArmorShaderData(effect, "GhostPass").UseOpacity(RacePeriodPulse.BrightBodyOpacity);
        effect.Value.Parameters["uDimOpacity"].SetValue(RacePeriodPulse.DimBodyOpacity);
        wave = effect.Value.Parameters["uWave"];
        var method = typeof(PlayerDrawHelper).GetMethod(nameof(PlayerDrawHelper.SetShaderForData),
            [typeof(Player), typeof(int), typeof(DrawData).MakeByRefType()]);
        hook = new Hook(method, (ShaderHook)Apply);
    }

    private void Apply(SetShaderForData original, Player player, int headShader, ref DrawData data)
    {
        if (data.shader == BodyShader)
        {
            wave.SetValue(RacePeriodPulse.GetWave(player));
            Color team = player.team > 0 && player.team < Main.teamColor.Length
                ? Main.teamColor[player.team] : Color.White;
            shader.UseColor(team).Apply(player, data);
        }
        else
            original(player, headShader, ref data);
    }

    internal static void AppendGlow(ref PlayerDrawSet drawInfo, int start)
    {
        if (drawInfo.DrawDataCache.Count <= start)
            return;

        float pulse = RacePeriodPulse.GetWave(drawInfo.drawPlayer);
        float rimOpacity = RacePeriodPulse.GetOutlineOpacity(pulse);
        ModContent.GetInstance<SpawnRegionGlow>().UseMountGlow(ref drawInfo, new MountGlow(BodyShader, pulse, rimOpacity));
    }

    public override void Unload()
    {
        hook?.Dispose();
        hook = null;
        shader = null;
        wave = null;
        Main.QueueMainThreadAction(RacePeriodAnimalTextures.Dispose);
    }
}
