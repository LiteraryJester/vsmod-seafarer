using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Seafarer;

// FruitTreeGrowingBranchBH.TryGrow does branchBlock.TypeProps[TreeType] without
// TryGetValue, so a cutting whose TreeType is not registered on its block crashes
// the server tick loop on every tick. Older Seafarer builds planted bdorchard
// cuttings as game:fruittree-cutting and produced exactly this state — the worlds
// they left behind keep crashing even after the BlockGrowPot fix. Mark such
// blocks dead so the tick short-circuits and the player can break them.
[HarmonyPatch(typeof(FruitTreeGrowingBranchBH), "TryGrow")]
public static class FruitTreeOrphanPatch
{
    [HarmonyPrefix]
    public static bool Prefix(FruitTreeGrowingBranchBH __instance)
    {
        if (__instance.Blockentity is not BlockEntityFruitTreeBranch be) return true;
        if (string.IsNullOrEmpty(be.TreeType)) return true;

        var branchBlock = AccessTools.Field(typeof(FruitTreeGrowingBranchBH), "branchBlock")
            .GetValue(__instance) as BlockFruitTreeBranch;
        if (branchBlock?.TypeProps == null) return true;
        if (branchBlock.TypeProps.ContainsKey(be.TreeType)) return true;

        be.FoliageState = EnumFoliageState.Dead;
        be.MarkDirty(true);
        return false;
    }
}
