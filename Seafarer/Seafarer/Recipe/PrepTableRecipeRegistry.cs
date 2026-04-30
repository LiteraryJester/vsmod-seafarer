using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Seafarer;

public class PrepTableRecipeRegistry : ModSystem
{
    public List<PrepTableRecipe> Recipes { get; private set; } = new();

    public override double ExecuteOrder() => 1.0;

    public override void Start(ICoreAPI api)
    {
        // Register the recipe registry — VS handles server→client sync automatically
        var registry = api.RegisterRecipeRegistry<RecipeRegistryGeneric<PrepTableRecipe>>("preptablerecipes");
        Recipes = registry.Recipes;
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        // Only load recipes on the server — they sync to client via the registry
        if (api is not ICoreServerAPI sapi) return;
        LoadRecipes(sapi);
    }

    private void LoadRecipes(ICoreServerAPI sapi)
    {
        Recipes.Clear();

        var assets = sapi.Assets.GetMany("recipes/onpreptable/", "seafarer");
        foreach (var asset in assets)
        {
            try
            {
                var recipe = asset.ToObject<PrepTableRecipe>();
                if (recipe?.Output != null && recipe.Input?.Code != null)
                {
                    Recipes.Add(recipe);
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Error("Failed to load prep table recipe {0}: {1}", asset.Location, e.Message);
            }
        }

        sapi.Logger.Notification("Loaded {0} prep table recipes", Recipes.Count);
    }

    /// <summary>
    /// Find a matching recipe for the given input and prep table state.
    /// </summary>
    public PrepTableRecipe? FindMatch(
        ItemStack heldItem,
        int barrelSaltCount,
        ItemStack? barrelLiquid,
        ItemStack? toolSlotItem)
    {
        foreach (var recipe in Recipes)
        {
            if (MatchesRecipe(recipe, heldItem, barrelSaltCount, barrelLiquid, toolSlotItem))
                return recipe;
        }
        return null;
    }

    private bool MatchesRecipe(
        PrepTableRecipe recipe,
        ItemStack heldItem,
        int barrelSaltCount,
        ItemStack? barrelLiquid,
        ItemStack? toolSlotItem)
    {
        // Check input matches held item
        if (!MatchesCode(heldItem, recipe.Input.Code)) return false;
        if (heldItem.StackSize < recipe.Input.Quantity) return false;

        // Check barrel has enough of the required item
        if (recipe.BarrelConsumes != null)
        {
            int available = GetBarrelItemCount(recipe.BarrelConsumes.Code, barrelSaltCount, barrelLiquid);
            if (available < recipe.BarrelConsumes.Quantity) return false;
        }

        // Check tool slot requirements
        if (recipe.ToolSlotRequires != null)
        {
            if (toolSlotItem == null) return false;
            if (!MatchesCode(toolSlotItem, recipe.ToolSlotRequires)) return false;
        }

        return true;
    }

    /// <summary>
    /// Check how much of a given item code is available in the barrel.
    /// </summary>
    public int GetBarrelItemCount(string code, int barrelSaltCount, ItemStack? barrelLiquid)
    {
        if (barrelSaltCount > 0 && MatchesCodeString("game:salt", code))
        {
            return barrelSaltCount;
        }

        if (barrelLiquid != null && MatchesCode(barrelLiquid, code))
        {
            return barrelLiquid.StackSize;
        }

        return 0;
    }

    public bool MatchesCodeString(string code, string pattern)
    {
        if (pattern.Contains('*'))
        {
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "(.+)") + "$";
            return Regex.IsMatch(code, regexPattern);
        }
        return code == pattern;
    }

    public bool MatchesCode(ItemStack stack, string pattern)
    {
        if (stack == null) return false;

        string codeWithDomain = stack.Collectible.Code.Domain + ":" + stack.Collectible.Code.Path;
        string codePath = stack.Collectible.Code.Path;

        if (pattern.Contains('*'))
        {
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "(.+)") + "$";
            return Regex.IsMatch(codeWithDomain, regexPattern)
                || Regex.IsMatch(codePath, regexPattern);
        }

        return codeWithDomain == pattern || codePath == pattern;
    }

    public string? GetWildcardCapture(ItemStack stack, string pattern)
    {
        if (stack == null || !pattern.Contains('*')) return null;

        string codeWithDomain = stack.Collectible.Code.Domain + ":" + stack.Collectible.Code.Path;
        string codePath = stack.Collectible.Code.Path;
        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "(.+)") + "$";

        var match = Regex.Match(codeWithDomain, regexPattern);
        if (match.Success && match.Groups.Count > 1) return match.Groups[1].Value;

        match = Regex.Match(codePath, regexPattern);
        if (match.Success && match.Groups.Count > 1) return match.Groups[1].Value;

        return null;
    }

    public string ResolveOutputCode(PrepTableRecipe recipe, ItemStack inputStack)
    {
        string outputCode = recipe.Output.Code;

        if (recipe.Input.Name != null)
        {
            string? captured = GetWildcardCapture(inputStack, recipe.Input.Code);
            if (captured != null)
            {
                outputCode = outputCode.Replace("{" + recipe.Input.Name + "}", captured);
            }
        }

        return outputCode;
    }
}
