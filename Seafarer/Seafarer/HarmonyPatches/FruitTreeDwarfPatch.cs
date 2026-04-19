using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Seafarer;

// The engine defines RootSizeMul as NatFloat.createUniform(1f, 0) — var=0 means
// ClampToRange collapses any stack-level rootSizeMulDiff back to exactly 1.0,
// so we can't shrink a fruit tree through JSON attributes alone. This postfix
// runs after RegisterTreeType has populated propsByType and overrides the size
// when the parent cutting stack carries "dwarf: true".
[HarmonyPatch(typeof(FruitTreeRootBH), nameof(FruitTreeRootBH.RegisterTreeType))]
public static class FruitTreeDwarfPatch
{
    private const float DwarfRootSizeMul = 0.4f;

    // Engine default is 30 game-days before a cutting can flower. Dwarfs are
    // already small and stylised as "bonsai" — skip most of the juvenile
    // period so potted trees produce fruit on a timeframe that feels rewarding.
    private const double DwarfNonFloweringYoungDays = 3;

    private static readonly AccessTools.FieldRef<FruitTreeRootBH, ItemStack> parentStackRef
        = AccessTools.FieldRefAccess<FruitTreeRootBH, ItemStack>("parentPlantStack");

    [HarmonyPostfix]
    public static void Postfix(FruitTreeRootBH __instance, string treeType)
    {
        if (treeType == null) return;

        var stack = parentStackRef(__instance);
        if (stack?.Attributes?.GetBool("dwarf") != true) return;

        if (!__instance.propsByType.TryGetValue(treeType, out var props)) return;

        // Smaller canopy → fewer harvest blocks → the "lower yield" half of
        // the dwarf design. Also bypasses the engine's RootSizeMul NatFloat clamp
        // (base range var=0 collapses any stack-level diff back to 1.0).
        props.RootSizeMul = DwarfRootSizeMul;

        __instance.nonFloweringYoungDays = DwarfNonFloweringYoungDays;
    }
}
