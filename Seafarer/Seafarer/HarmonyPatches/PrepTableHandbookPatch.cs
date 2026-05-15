using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Seafarer;

// Inject "Created at prep table" rows into the handbook page of any item that
// is an output (or extra output) of a registered prep table recipe.
[HarmonyPatch(typeof(CollectibleBehaviorHandbookTextAndExtraInfo), nameof(CollectibleBehaviorHandbookTextAndExtraInfo.GetHandbookInfo))]
public static class PrepTableHandbookPatch
{
    [HarmonyPostfix]
    public static void Postfix(
        ItemSlot inSlot,
        ICoreClientAPI capi,
        ItemStack[] allStacks,
        ActionConsumable<string> openDetailPageFor,
        ref RichTextComponentBase[] __result)
    {
        var stack = inSlot?.Itemstack;
        if (stack?.Collectible == null) return;

        var registry = capi.ModLoader.GetModSystem<PrepTableRecipeRegistry>();
        if (registry == null || registry.Recipes.Count == 0) return;

        var matches = new List<PrepTableRecipe>();
        foreach (var recipe in registry.Recipes)
        {
            if (IsOutputOf(stack, recipe, registry)) matches.Add(recipe);
        }
        if (matches.Count == 0) return;

        var components = new List<RichTextComponentBase>(__result);

        bool haveText = true;
        CollectibleBehaviorHandbookTextAndExtraInfo.AddHeading(
            components, capi, "seafarer:handbook-preptable-createdat", ref haveText);
        components.Add(new ClearFloatTextComponent(capi, 2));

        foreach (var recipe in matches)
        {
            components.AddRange(PrepTableHandbookRenderer.BuildRecipeRow(
                capi, recipe, allStacks, registry, openDetailPageFor));
        }

        __result = components.ToArray();
    }

    private static bool IsOutputOf(ItemStack stack, PrepTableRecipe recipe, PrepTableRecipeRegistry registry)
    {
        if (registry.MatchesCode(stack, recipe.Output.Code)) return true;
        if (recipe.ExtraOutputs != null)
        {
            foreach (var extra in recipe.ExtraOutputs)
            {
                if (registry.MatchesCode(stack, extra.Code)) return true;
            }
        }
        return false;
    }
}
