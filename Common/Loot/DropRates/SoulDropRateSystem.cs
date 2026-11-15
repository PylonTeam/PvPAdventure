using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System.Reflection;
using Terraria.ModLoader;

namespace PvPAdventure.Common.Loot.DropRates;

public class SoulDropRateSystem : ModSystem
{
    private static ILHook globalRulesHook;

    private const int NewSoulDropRate = 3; // denominator (1 in 3)

    public override void PostSetupContent()
    {
        MethodInfo method = typeof(Terraria.GameContent.ItemDropRules.ItemDropDatabase).GetMethod(
            "RegisterGlobalRules",
            BindingFlags.NonPublic | BindingFlags.Instance);

        globalRulesHook = new ILHook(method, SoulDropILEdit);
    }

    public override void Unload()
    {
        globalRulesHook?.Dispose();
        globalRulesHook = null;
    }

    private static void SoulDropILEdit(ILContext il)
    {
        ILCursor cursor = new ILCursor(il);
        int replacedCount = 0;

        while (cursor.TryGotoNext(MoveType.After,
            i => i.MatchLdcI4(520) || i.MatchLdcI4(521),
            i => i.MatchLdcI4(5)))
        {
            cursor.Prev.OpCode = OpCodes.Ldc_I4;
            cursor.Prev.Operand = NewSoulDropRate;
            replacedCount++;
        }

        if (replacedCount != 2)
        {
            ModContent.GetInstance<PvPAdventure>().Logger.Error(
                $"Expected to replace 2 soul drop rates (Light and Night), but replaced {replacedCount}.");
        }
    }
}