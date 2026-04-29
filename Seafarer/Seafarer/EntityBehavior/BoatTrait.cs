using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Seafarer;

public enum TraitRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public static class TraitRarityExtensions
{
    public static string LangKey(this TraitRarity r) => r switch
    {
        TraitRarity.Common => "common",
        TraitRarity.Uncommon => "uncommon",
        TraitRarity.Rare => "rare",
        TraitRarity.Epic => "epic",
        TraitRarity.Legendary => "legendary",
        _ => "common"
    };
}

public class BoatTrait
{
    public string Code { get; set; } = "";
    public string Source { get; set; } = "";
    public TraitRarity Rarity { get; set; } = TraitRarity.Common;
    public float SpeedBonus { get; set; }
    public float HealthBonus { get; set; }
    public float StormDamageScale { get; set; } = 1f;
    public string? DropItem { get; set; }
}

public static class BoatTraitRegistry
{
    private static readonly Dictionary<string, BoatTrait> traits = new();

    public static BoatTrait? Get(string code)
    {
        if (string.IsNullOrEmpty(code)) return null;
        return traits.TryGetValue(code, out var t) ? t : null;
    }

    public static void Load(ICoreAPI api)
    {
        traits.Clear();

        var asset = api.Assets.TryGet(new AssetLocation("seafarer:config/boat-traits.json"));
        if (asset == null)
        {
            api.Logger.Warning("[seafarer] boat-traits.json missing; trait system disabled.");
            return;
        }

        JsonObject root;
        try
        {
            root = new JsonObject(Newtonsoft.Json.Linq.JToken.Parse(asset.ToText()));
        }
        catch (System.Exception e)
        {
            api.Logger.Warning("[seafarer] boat-traits.json parse failed: {0}", e.Message);
            return;
        }

        var traitsNode = root["traits"];
        if (!traitsNode.Exists || traitsNode.Token is not Newtonsoft.Json.Linq.JObject obj) return;

        foreach (var prop in obj.Properties())
        {
            var jt = new JsonObject(prop.Value);
            var rarityStr = jt["rarity"].AsString("common").ToLowerInvariant();
            var rarity = rarityStr switch
            {
                "uncommon" => TraitRarity.Uncommon,
                "rare" => TraitRarity.Rare,
                "epic" => TraitRarity.Epic,
                "legendary" => TraitRarity.Legendary,
                _ => TraitRarity.Common
            };

            traits[prop.Name] = new BoatTrait
            {
                Code = prop.Name,
                Source = jt["source"].AsString("sail"),
                Rarity = rarity,
                SpeedBonus = jt["speedBonus"].AsFloat(0f),
                HealthBonus = jt["healthBonus"].AsFloat(0f),
                StormDamageScale = jt["stormDamageScale"].AsFloat(1f),
                DropItem = jt["dropItem"].Exists ? jt["dropItem"].AsString() : null
            };
        }

        api.Logger.Notification("[seafarer] loaded {0} boat traits.", traits.Count);
    }
}
