using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace SaltAndSand;

public class BlockBurrito : Block
{
    public string State => Variant["state"];

    private const float InitialServings = 1.0f;

    #region Held interaction — griddle placement + eating

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
    {
        // Griddle placement
        if (blockSel != null && firstEvent && byEntity is EntityPlayer entityPlayer)
        {
            var world = byEntity.World;
            var targetBlock = world.BlockAccessor.GetBlock(blockSel.Position);

            if (targetBlock is BlockGriddleHearth)
            {
                var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGriddleHearth;
                if (be != null && be.OnInteract(entityPlayer.Player, blockSel))
                {
                    handHandling = EnumHandHandling.PreventDefault;
                    return;
                }
            }
        }

        // Eating — only cooked burritos, not sneaking, not targeting a block
        if (State == "cooked" && !byEntity.Controls.ShiftKey && blockSel == null)
        {
            var nutrition = slot.Itemstack != null ? GetNutritionProperties(byEntity.World, slot.Itemstack, byEntity) : null;
            if (nutrition != null && slot.Itemstack != null && GetServings(slot.Itemstack) > 0)
            {
                byEntity.World.RegisterCallback(_ =>
                {
                    if (byEntity.Controls.HandUse == EnumHandInteract.HeldItemInteract)
                    {
                        byEntity.PlayEntitySound("eat", (byEntity as EntityPlayer)?.Player);
                    }
                }, 500);

                handHandling = EnumHandHandling.PreventDefault;
                return;
            }
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling);
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        if (State != "cooked" || byEntity.Controls.ShiftKey) return false;

        if (byEntity.World is IClientWorldAccessor)
        {
            return secondsUsed <= 1.5f;
        }

        return true;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        if (State != "cooked") return;
        if (byEntity.World.Side == EnumAppSide.Client || secondsUsed < 1.45f) return;
        if (slot.Itemstack is not { } foodStack) return;
        if (byEntity is not EntityPlayer { Player: { } player }) return;

        // Build per-ingredient nutrition list (tortilla base + each filling)
        var nutriList = GetPerIngredientNutrition(byEntity.World, foodStack);
        if (nutriList.Count == 0) return;

        float servingsLeft = GetServings(foodStack);
        if (servingsLeft <= 0) return;

        var ebh = byEntity.GetBehavior<EntityBehaviorHunger>();
        if (ebh == null) return;

        // Total satiety across all ingredients to calculate serving fraction
        float totalMealSat = 0;
        foreach (var n in nutriList) totalMealSat += n.Satiety;

        float satiablePoints = ebh.MaxSaturation - ebh.Saturation;
        float servingsNeeded = GameMath.Clamp(satiablePoints / Math.Max(1, totalMealSat), 0, 1);
        float servingsToEat = Math.Min(servingsLeft, servingsNeeded);

        if (servingsToEat <= 0) servingsToEat = Math.Min(servingsLeft, 0.1f);

        // Apply each ingredient's nutrition with its own food category
        float totalHealth = 0;
        foreach (var n in nutriList)
        {
            float sat = servingsToEat * n.Satiety;
            float satLossDelay = Math.Min(1.3f, servingsToEat * 3) * 10 + sat / 70f * 60f;
            byEntity.ReceiveSaturation(sat, n.FoodCategory, satLossDelay, 1f);
            totalHealth += servingsToEat * n.Health;
        }

        if (totalHealth != 0)
        {
            byEntity.ReceiveDamage(new DamageSource
            {
                Source = EnumDamageSource.Internal,
                Type = totalHealth > 0 ? EnumDamageType.Heal : EnumDamageType.Poison
            }, Math.Abs(totalHealth));
        }

        servingsLeft = Math.Max(0, servingsLeft - servingsToEat);

        if (servingsLeft <= 0)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
        }
        else
        {
            SetServings(foodStack, servingsLeft);
            slot.MarkDirty();
        }
    }

    #endregion

    #region Servings helpers

    public static float GetServings(ItemStack stack)
    {
        return stack.Attributes.GetFloat("quantityServings", InitialServings);
    }

    public static void SetServings(ItemStack stack, float servings)
    {
        stack.Attributes.SetFloat("quantityServings", servings);
    }

    #endregion

    #region Block interaction — assembly

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var be = GetBlockEntity<BlockEntityBurrito>(blockSel.Position);
        return be?.OnInteract(byPlayer) == true || base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack? byItemStack = null)
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);

        var be = GetBlockEntity<BlockEntityBurrito>(blockPos);
        if (be != null && byItemStack?.Attributes != null)
        {
            be.LoadFromItemStack(byItemStack);
        }
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer,
        float dropQuantityMultiplier = 1)
    {
        var be = GetBlockEntity<BlockEntityBurrito>(pos);
        if (be != null)
        {
            var stack = be.CreateBurritoStack();
            if (stack != null)
            {
                world.SpawnItemEntity(stack, pos.ToVec3d().Add(0.5, 0.25, 0.5));
            }
        }

        world.BlockAccessor.SetBlock(0, pos);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer,
        float dropQuantityMultiplier = 1)
    {
        return [];
    }

    #endregion

    #region Nutrition

    /// <summary>
    /// Returns per-ingredient nutrition list: tortilla base + each filling with its own food category.
    /// </summary>
    private List<FoodNutritionProperties> GetPerIngredientNutrition(IWorldAccessor world, ItemStack itemstack)
    {
        var list = new List<FoodNutritionProperties>();
        if (State != "cooked") return list;

        // Tortilla base
        list.Add(new FoodNutritionProperties
        {
            Satiety = 240,
            FoodCategory = EnumFoodCategory.Grain,
            Health = 1
        });

        var contents = itemstack.Attributes?.GetTreeAttribute("contents");
        if (contents == null) return list;

        for (int i = 0; i < BlockEntityBurrito.MaxFillings; i++)
        {
            var filling = (contents["slot" + i] as ItemstackAttribute)?.value;
            if (filling == null) continue;

            filling.ResolveBlockOrItem(world);
            var nutrition = filling.Collectible?.NutritionProps;
            if (nutrition != null)
            {
                list.Add(new FoodNutritionProperties
                {
                    Satiety = nutrition.Satiety,
                    FoodCategory = nutrition.FoodCategory,
                    Health = nutrition.Health
                });
            }
        }

        return list;
    }

    /// <summary>
    /// Returns combined nutrition for tooltip display. Actual eating uses GetPerIngredientNutrition.
    /// </summary>
    public override FoodNutritionProperties? GetNutritionProperties(IWorldAccessor world, ItemStack itemstack,
        Entity forEntity)
    {
        var list = GetPerIngredientNutrition(world, itemstack);
        if (list.Count == 0) return null;

        float satiety = 0;
        float health = 0;
        foreach (var n in list)
        {
            satiety += n.Satiety;
            health += n.Health;
        }

        return new FoodNutritionProperties
        {
            Satiety = satiety,
            FoodCategory = EnumFoodCategory.Grain,
            Health = health
        };
    }

    #endregion

    #region Display

    public override string GetHeldItemName(ItemStack itemStack)
    {
        return Lang.Get("seafarer:block-burrito-" + Variant["state"]);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world,
        bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        var contents = inSlot.Itemstack?.Attributes?.GetTreeAttribute("contents");
        if (contents != null)
        {
            bool hasFillings = false;
            for (int i = 0; i < BlockEntityBurrito.MaxFillings; i++)
            {
                var filling = (contents["slot" + i] as ItemstackAttribute)?.value;
                if (filling == null) continue;
                filling.ResolveBlockOrItem(world);
                if (!hasFillings)
                {
                    dsc.AppendLine(Lang.Get("seafarer:burrito-fillings"));
                    hasFillings = true;
                }

                dsc.AppendLine("  " + filling.GetName());
            }
        }

        if (State == "cooked" && inSlot.Itemstack != null)
        {
            float servings = GetServings(inSlot.Itemstack);
            if (servings < InitialServings)
            {
                int pct = (int)(servings / InitialServings * 100);
                dsc.AppendLine(Lang.Get("seafarer:burrito-remaining", pct));
            }
        }
    }

    #endregion
}
