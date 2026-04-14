using System.IO;
using Vintagestory.API.Common;

namespace SaltAndSand;

public class GriddleRecipe : IByteSerializable
{
    public GriddleIngredient[] Ingredients { get; set; } = [];
    public JsonItemStackOutput Output { get; set; } = new();
    public float CookingTemp { get; set; } = 150f;
    public float CookingDuration { get; set; } = 30f;
    public string MinMaterial { get; set; } = "clay";

    public int MinMaterialTier => MaterialToTier(MinMaterial);

    public static int MaterialToTier(string material)
    {
        return material switch
        {
            "clay" => 0,
            "copper" => 1,
            "bronze" or "tinbronze" or "bismuthbronze" or "blackbronze" => 2,
            "iron" or "steel" => 3,
            _ => 0
        };
    }

    public GriddleIngredient? GetPrimaryIngredient()
    {
        foreach (var ing in Ingredients)
        {
            if (ing.Litres <= 0) return ing;
        }
        return Ingredients.Length > 0 ? Ingredients[0] : null;
    }

    public GriddleIngredient? GetOilIngredient()
    {
        foreach (var ing in Ingredients)
        {
            if (ing.Litres > 0) return ing;
        }
        return null;
    }

    public bool RequiresOil => GetOilIngredient() != null;

    public void ToBytes(BinaryWriter writer)
    {
        writer.Write(Ingredients.Length);
        foreach (var ing in Ingredients)
        {
            writer.Write(ing.Type);
            writer.Write(ing.Code);
            writer.Write(ing.Name ?? "");
            writer.Write(ing.Litres);
            writer.Write(ing.Quantity);
        }
        writer.Write(Output.Type);
        writer.Write(Output.Code);
        writer.Write(Output.Quantity);
        writer.Write(CookingTemp);
        writer.Write(CookingDuration);
        writer.Write(MinMaterial);
    }

    public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
    {
        int count = reader.ReadInt32();
        Ingredients = new GriddleIngredient[count];
        for (int i = 0; i < count; i++)
        {
            Ingredients[i] = new GriddleIngredient
            {
                Type = reader.ReadString(),
                Code = reader.ReadString(),
                Name = reader.ReadString(),
                Litres = reader.ReadSingle(),
                Quantity = reader.ReadInt32()
            };
            if (Ingredients[i].Name == "") Ingredients[i].Name = null;
        }
        Output = new JsonItemStackOutput
        {
            Type = reader.ReadString(),
            Code = reader.ReadString(),
            Quantity = reader.ReadInt32()
        };
        CookingTemp = reader.ReadSingle();
        CookingDuration = reader.ReadSingle();
        MinMaterial = reader.ReadString();
    }
}

/// <summary>
/// Output stack definition that preserves the raw code string for wildcard substitution.
/// Unlike JsonItemStack, this doesn't try to resolve at load time.
/// </summary>
public class JsonItemStackOutput
{
    public string Type { get; set; } = "item";
    public string Code { get; set; } = "";
    public int Quantity { get; set; } = 1;
}

public class GriddleIngredient
{
    public string Type { get; set; } = "item";
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public float Litres { get; set; }
    public int Quantity { get; set; } = 1;
}
