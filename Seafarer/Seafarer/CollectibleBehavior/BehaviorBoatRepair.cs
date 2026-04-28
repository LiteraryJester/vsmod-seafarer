using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

public class BehaviorBoatRepair : CollectibleBehavior
{
    public const string Code = "boatrepair";

    private const float UseDurationSeconds = 1.5f;

    private float hpPerLitre = 20f;
    private float litresPerUse = 0.25f;

    public BehaviorBoatRepair(CollectibleObject collObj) : base(collObj) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        hpPerLitre = properties["hpPerLitre"].AsFloat(hpPerLitre);
        litresPerUse = properties["litresPerUse"].AsFloat(litresPerUse);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (entitySel?.Entity == null) return;
        var ship = entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>();
        if (ship == null) return;

        var healthBh = entitySel.Entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh == null) return;

        if (healthBh.Health >= healthBh.MaxHealth)
        {
            if (byEntity is EntityPlayer entityPlayer && byEntity.World.Side == EnumAppSide.Client)
            {
                (entityPlayer.Player as IClientPlayer)?.ShowChatNotification(
                    Lang.Get("seafarer:boatrepair-fullhp"));
            }
            handling = EnumHandling.PreventSubsequent;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }

        if (GetAvailableLitres(slot) < litresPerUse) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;

        if (byEntity.World.Side == EnumAppSide.Client)
        {
            byEntity.World.PlaySoundAt(
                new AssetLocation("game:sounds/player/gluerepair" + ((byEntity.World.Rand.Next(4)) + 1)),
                byEntity, byEntity is EntityPlayer ep ? ep.Player : null);
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (entitySel?.Entity == null) return false;
        if (entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>() == null) return false;

        handling = EnumHandling.PreventSubsequent;
        return secondsUsed < UseDurationSeconds;
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        if (secondsUsed < UseDurationSeconds - 0.05f) return;
        if (byEntity.World.Side != EnumAppSide.Server) return;
        if (entitySel?.Entity == null) return;

        var ship = entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>();
        if (ship == null) return;
        var healthBh = entitySel.Entity.GetBehavior<EntityBehaviorHealth>();
        if (healthBh == null) return;
        if (healthBh.Health >= healthBh.MaxHealth) return;

        if (!TryDrainLitres(slot, byEntity, litresPerUse)) return;

        float restored = hpPerLitre * litresPerUse;
        healthBh.Health = System.Math.Min(healthBh.Health + restored, healthBh.MaxHealth);
        entitySel.Entity.WatchedAttributes.MarkPathDirty("health");

        handling = EnumHandling.PreventSubsequent;
    }

    private float GetAvailableLitres(ItemSlot slot)
    {
        var stack = slot.Itemstack;
        if (stack?.Collectible is BlockLiquidContainerBase container)
        {
            var content = container.GetContent(stack);
            if (content == null) return 0f;
            var props = BlockLiquidContainerBase.GetContainableProps(content);
            if (props == null) return 0f;
            return content.StackSize / props.ItemsPerLitre;
        }
        return 0f;
    }

    private bool TryDrainLitres(ItemSlot slot, EntityAgent byEntity, float litres)
    {
        var stack = slot.Itemstack;
        if (stack?.Collectible is not BlockLiquidContainerBase container) return false;

        var content = container.GetContent(stack);
        if (content == null) return false;
        var props = BlockLiquidContainerBase.GetContainableProps(content);
        if (props == null) return false;

        int needed = (int)System.Math.Ceiling(litres * props.ItemsPerLitre);
        if (content.StackSize < needed) return false;

        content.StackSize -= needed;
        if (content.StackSize <= 0)
        {
            container.SetContent(stack, null);
        }
        else
        {
            container.SetContent(stack, content);
        }
        slot.MarkDirty();
        return true;
    }
}
