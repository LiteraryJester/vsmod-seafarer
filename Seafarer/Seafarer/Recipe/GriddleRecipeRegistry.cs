using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace Seafarer;

public class GriddleRecipeRegistry : ModSystem
{
    public List<GriddleRecipe> Recipes { get; private set; } = new();

    public override double ExecuteOrder() => 1.0;

    public override void Start(ICoreAPI api)
    {
        // Register the recipe registry — VS handles server→client sync automatically
        var registry = api.RegisterRecipeRegistry<RecipeRegistryGeneric<GriddleRecipe>>("griddlerecipes");
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

        // Scan all loaded domains so other mods can register griddle recipes
        // by dropping files at assets/<theirmodid>/recipes/ongriddle/*.json.
        var assets = sapi.Assets.GetMany("recipes/ongriddle/");
        foreach (var asset in assets)
        {
            try
            {
                var recipe = asset.ToObject<GriddleRecipe>();
                if (recipe?.Output != null && recipe.Ingredients?.Length > 0)
                {
                    Recipes.Add(recipe);
                }
            }
            catch (Exception e)
            {
                sapi.Logger.Error("Failed to load griddle recipe {0}: {1}", asset.Location, e.Message);
            }
        }

        sapi.Logger.Notification("Loaded {0} griddle recipes", Recipes.Count);
    }

    public GriddleRecipe? GetMatchingRecipe(ItemStack? input, ItemStack? oilStack, string griddleMaterial)
    {
        if (input == null) return null;

        int griddleTier = GriddleRecipe.MaterialToTier(griddleMaterial);
        GriddleRecipe? bestMatch = null;
        bool bestHasOil = false;

        foreach (var recipe in Recipes)
        {
            if (recipe.MinMaterialTier > griddleTier) continue;

            var primary = recipe.GetPrimaryIngredient();
            if (primary == null) continue;
            if (!MatchesIngredient(input, primary)) continue;

            bool recipeNeedsOil = recipe.RequiresOil;
            if (recipeNeedsOil)
            {
                var oilIng = recipe.GetOilIngredient();
                if (!HasSufficientOil(oilStack, oilIng)) continue;
            }

            // Prefer oil recipes when oil is available
            if (bestMatch == null || (recipeNeedsOil && !bestHasOil))
            {
                bestMatch = recipe;
                bestHasOil = recipeNeedsOil;
            }
        }

        return bestMatch;
    }

    public bool MatchesIngredient(ItemStack stack, GriddleIngredient ingredient)
    {
        if (stack == null || ingredient == null) return false;

        // Get the full code with domain (e.g. "game:dough-spelt")
        string itemCodeWithDomain = stack.Collectible.Code.Domain + ":" + stack.Collectible.Code.Path;
        // Also get just the path (e.g. "dough-spelt")
        string itemCodePath = stack.Collectible.Code.Path;
        string pattern = ingredient.Code;

        if (pattern.Contains('*'))
        {
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "(.+)") + "$";
            // Try matching with domain first, then without
            return Regex.IsMatch(itemCodeWithDomain, regexPattern)
                || Regex.IsMatch(itemCodePath, regexPattern);
        }

        return itemCodeWithDomain == pattern || itemCodePath == pattern;
    }

    public string? GetWildcardMatch(ItemStack stack, GriddleIngredient ingredient)
    {
        if (stack == null || ingredient == null) return null;

        string itemCodeWithDomain = stack.Collectible.Code.Domain + ":" + stack.Collectible.Code.Path;
        string itemCodePath = stack.Collectible.Code.Path;
        string pattern = ingredient.Code;

        if (pattern.Contains('*'))
        {
            string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", "(.+)") + "$";

            // Try with domain first
            var match = Regex.Match(itemCodeWithDomain, regexPattern);
            if (match.Success && match.Groups.Count > 1)
                return match.Groups[1].Value;

            // Try without domain
            match = Regex.Match(itemCodePath, regexPattern);
            if (match.Success && match.Groups.Count > 1)
                return match.Groups[1].Value;
        }

        return null;
    }

    public string ResolveOutputCode(GriddleRecipe recipe, ItemStack input)
    {
        string outputCode = recipe.Output.Code;

        var primary = recipe.GetPrimaryIngredient();
        if (primary?.Name != null)
        {
            string? captured = GetWildcardMatch(input, primary);
            if (captured != null)
            {
                outputCode = outputCode.Replace("{" + primary.Name + "}", captured);
            }
        }

        return outputCode;
    }

    private bool HasSufficientOil(ItemStack? oilStack, GriddleIngredient? oilIngredient)
    {
        if (oilStack == null || oilStack.StackSize <= 0) return false;
        if (oilIngredient == null) return false;
        return MatchesIngredient(oilStack, oilIngredient);
    }
}
