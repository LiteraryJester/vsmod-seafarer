using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Seafarer;

internal static class PrepTableHandbookRenderer
{
    private const int IconSize = 40;
    private const int RowBottomMargin = 10;
    private const int FirstSlotIndent = 2;

    public static List<RichTextComponentBase> BuildRecipeRow(
        ICoreClientAPI capi,
        PrepTableRecipe recipe,
        ItemStack[] allStacks,
        PrepTableRecipeRegistry registry,
        ActionConsumable<string> openDetailPageFor)
    {
        var row = new List<RichTextComponentBase>();

        AddSlot(capi, row, ResolveByPattern(allStacks, recipe.Input.Code, registry, recipe.Input.Quantity),
            openDetailPageFor, firstIndent: FirstSlotIndent);

        if (recipe.ToolSlotRequires != null)
        {
            AddJoiner(capi, row, " + ");
            AddSlot(capi, row, ResolveByPattern(allStacks, recipe.ToolSlotRequires, registry, 1),
                openDetailPageFor, showStackSize: false);
        }

        if (recipe.BarrelConsumes != null)
        {
            AddJoiner(capi, row, " + ");
            AddSlot(capi, row, ResolveByPattern(allStacks, recipe.BarrelConsumes.Code, registry, recipe.BarrelConsumes.Quantity),
                openDetailPageFor);
        }

        AddJoiner(capi, row, " = ");
        var output = ResolveOutput(capi.World, recipe.Output);
        if (output != null) AddSlot(capi, row, [output], openDetailPageFor);

        if (recipe.ExtraOutputs != null)
        {
            foreach (var extra in recipe.ExtraOutputs)
            {
                var stack = ResolveOutput(capi.World, extra);
                if (stack == null) continue;
                AddJoiner(capi, row, " + ");
                AddSlot(capi, row, [stack], openDetailPageFor);
            }
        }

        row.Add(new ClearFloatTextComponent(capi, RowBottomMargin));
        return row;
    }

    private static void AddJoiner(ICoreClientAPI capi, List<RichTextComponentBase> row, string text)
    {
        var c = new RichTextComponent(capi, text, CairoFont.WhiteMediumText())
        {
            VerticalAlign = EnumVerticalAlign.Middle
        };
        row.Add(c);
    }

    private static void AddSlot(
        ICoreClientAPI capi,
        List<RichTextComponentBase> row,
        ItemStack[] stacks,
        ActionConsumable<string> openDetailPageFor,
        int firstIndent = 0,
        bool showStackSize = true)
    {
        if (stacks.Length == 0) return;

        var c = new SlideshowItemstackTextComponent(capi, stacks, IconSize, EnumFloat.Inline,
            cs => openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(cs)))
        {
            ShowStackSize = showStackSize,
            PaddingLeft = firstIndent
        };
        row.Add(c);
    }

    private static ItemStack[] ResolveByPattern(
        ItemStack[] allStacks, string pattern, PrepTableRecipeRegistry registry, int stampedQuantity)
    {
        var matches = new List<ItemStack>();
        foreach (var s in allStacks)
        {
            if (s?.Collectible == null) continue;
            if (!registry.MatchesCode(s, pattern)) continue;
            var clone = s.Clone();
            clone.StackSize = stampedQuantity;
            matches.Add(clone);
        }
        return matches.ToArray();
    }

    private static ItemStack? ResolveOutput(IWorldAccessor world, PrepTableOutput output)
    {
        var loc = new AssetLocation(output.Code);
        CollectibleObject? coll = output.Type == "block"
            ? world.GetBlock(loc)
            : world.GetItem(loc);
        if (coll == null) return null;

        var stack = new ItemStack(coll, output.Quantity);
        if (output.LiquidFill != null && coll is BlockLiquidContainerBase lc)
        {
            var liquidItem = world.GetItem(new AssetLocation(output.LiquidFill.Code));
            if (liquidItem != null)
            {
                lc.TryPutLiquid(stack, new ItemStack(liquidItem, 1), output.LiquidFill.Litres);
            }
        }
        return stack;
    }
}
