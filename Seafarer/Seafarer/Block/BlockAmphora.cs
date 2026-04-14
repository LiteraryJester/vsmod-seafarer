using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Seafarer;

public class BlockAmphora : BlockLiquidContainerTopOpened
{
    public static bool IsSealed(ItemStack? stack)
    {
        return stack?.Attributes?.GetBool("sealed") == true;
    }

    private bool HasLiquidContent(ItemStack? stack)
    {
        return stack != null && GetContent(stack) != null;
    }

    // When held in hand — prevent liquid transfer if sealed, sneak+click to unseal
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (IsSealed(slot.Itemstack))
        {
            if (byEntity.Controls.ShiftKey)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    slot.Itemstack!.Attributes.SetBool("sealed", false);
                    slot.MarkDirty();
                    api.World.PlaySoundAt(new AssetLocation("sounds/effect/seal-break"), byEntity, null, true, 16f);
                }
                handling = EnumHandHandling.PreventDefault;
                return;
            }

            if (api.Side == EnumAppSide.Client)
            {
                (api as ICoreClientAPI)?.TriggerIngameError(this, "amphorasealed", Lang.Get("seafarer:amphora-is-sealed"));
            }

            handling = EnumHandHandling.PreventDefault;
            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    // Spoilage modifier when amphora is inside another container
    public override float GetContainingTransitionModifierContained(IWorldAccessor world, ItemSlot inSlot, EnumTransitionType transType)
    {
        if (transType == EnumTransitionType.Perish)
        {
            if (IsSealed(inSlot.Itemstack))
            {
                return 0.1f; // Sealed: 90% slower spoilage
            }
            return 0.75f; // Unsealed: same as storage vessel
        }

        return base.GetContainingTransitionModifierContained(world, inSlot, transType);
    }

    // Show sealed status in item tooltip
    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        if (IsSealed(inSlot.Itemstack))
        {
            dsc.AppendLine("<font color=\"#88cc88\">" + Lang.Get("seafarer:amphora-sealed-status") + "</font>");
        }
    }
}
