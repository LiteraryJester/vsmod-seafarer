using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace SaltAndSand;

public class PrepTableRecipe : IByteSerializable
{
    public string Code { get; set; } = "";
    public PrepTableInput Input { get; set; } = new();
    public string? ToolSlotRequires { get; set; }
    public PrepTableOutput Output { get; set; } = new();
    public PrepTableOutput[]? ExtraOutputs { get; set; }
    public PrepTableBarrelConsume? BarrelConsumes { get; set; }
    public string? Sound { get; set; }

    public void ToBytes(BinaryWriter writer)
    {
        writer.Write(Code);
        writer.Write(Input.Code);
        writer.Write(Input.Name ?? "");
        writer.Write(Input.Quantity);
        writer.Write(ToolSlotRequires ?? "");
        writer.Write(Output.Type);
        writer.Write(Output.Code);
        writer.Write(Output.Quantity);
        writer.Write(Output.LiquidFill != null);
        if (Output.LiquidFill != null)
        {
            writer.Write(Output.LiquidFill.Code);
            writer.Write(Output.LiquidFill.Litres);
        }
        writer.Write(ExtraOutputs?.Length ?? 0);
        if (ExtraOutputs != null)
        {
            foreach (var extra in ExtraOutputs)
            {
                writer.Write(extra.Type);
                writer.Write(extra.Code);
                writer.Write(extra.Quantity);
            }
        }
        writer.Write(BarrelConsumes != null);
        if (BarrelConsumes != null)
        {
            writer.Write(BarrelConsumes.Code);
            writer.Write(BarrelConsumes.Quantity);
        }
        writer.Write(Sound ?? "");
    }

    public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
    {
        Code = reader.ReadString();
        Input = new PrepTableInput
        {
            Code = reader.ReadString(),
            Name = reader.ReadString(),
            Quantity = reader.ReadInt32()
        };
        if (Input.Name == "") Input.Name = null;

        string toolReq = reader.ReadString();
        ToolSlotRequires = toolReq == "" ? null : toolReq;

        Output = new PrepTableOutput
        {
            Type = reader.ReadString(),
            Code = reader.ReadString(),
            Quantity = reader.ReadInt32()
        };

        if (reader.ReadBoolean())
        {
            Output.LiquidFill = new PrepTableLiquidFill
            {
                Code = reader.ReadString(),
                Litres = reader.ReadSingle()
            };
        }

        int extraCount = reader.ReadInt32();
        if (extraCount > 0)
        {
            ExtraOutputs = new PrepTableOutput[extraCount];
            for (int i = 0; i < extraCount; i++)
            {
                ExtraOutputs[i] = new PrepTableOutput
                {
                    Type = reader.ReadString(),
                    Code = reader.ReadString(),
                    Quantity = reader.ReadInt32()
                };
            }
        }

        if (reader.ReadBoolean())
        {
            BarrelConsumes = new PrepTableBarrelConsume
            {
                Code = reader.ReadString(),
                Quantity = reader.ReadInt32()
            };
        }

        string sound = reader.ReadString();
        Sound = sound == "" ? null : sound;
    }
}

public class PrepTableInput
{
    public string Code { get; set; } = "";
    public string? Name { get; set; }
    public int Quantity { get; set; } = 1;
}

public class PrepTableOutput
{
    public string Type { get; set; } = "item";
    public string Code { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public PrepTableLiquidFill? LiquidFill { get; set; }
}

public class PrepTableLiquidFill
{
    public string Code { get; set; } = "";
    public float Litres { get; set; } = 1f;
}

public class PrepTableBarrelConsume
{
    public string Code { get; set; } = "";
    public int Quantity { get; set; } = 1;
}
