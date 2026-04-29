using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Seafarer;

public class BehaviorBoatSail : CollectibleBehavior
{
    public const string Code = "boatsail";

    private string traitCode = "";

    public BehaviorBoatSail(CollectibleObject collObj) : base(collObj) { }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        traitCode = properties["trait"].AsString("");
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (entitySel?.Entity == null) return;

        // Ctrl+right-click to apply a sail; bare right-click belongs to vanilla
        // boat interactions (mount, accessories, etc).
        if (!byEntity.Controls.CtrlKey) return;

        var ship = entitySel.Entity.GetBehavior<EntityBehaviorShipMechanics>();
        if (ship == null) return;

        if (!ship.HasSailSlot)
        {
            if (byEntity.World.Side == EnumAppSide.Client && byEntity is EntityPlayer ep1)
            {
                (ep1.Player as IClientPlayer)?.ShowChatNotification(Lang.Get("seafarer:boat-no-sail-slot"));
            }
            handling = EnumHandling.PreventSubsequent;
            handHandling = EnumHandHandling.PreventDefault;
            return;
        }

        if (string.IsNullOrEmpty(traitCode)) return;
        var trait = BoatTraitRegistry.Get(traitCode);
        if (trait == null) return;

        handling = EnumHandling.PreventSubsequent;
        handHandling = EnumHandHandling.PreventDefault;

        if (byEntity.World.Side != EnumAppSide.Server) return;

        // Drop the previous sail's cloth if any.
        var oldCode = ship.RemoveTrait("sail");
        if (oldCode != null)
        {
            var oldTrait = BoatTraitRegistry.Get(oldCode);
            if (oldTrait?.DropItem != null)
            {
                var item = byEntity.World.GetItem(new AssetLocation(oldTrait.DropItem));
                if (item != null)
                {
                    byEntity.World.SpawnItemEntity(
                        new ItemStack(item, 1),
                        entitySel.Entity.Pos.XYZ.AddCopy(0, 0.2, 0));
                }
            }
        }

        ship.ApplyAndCreditDelta("sail", trait.Code);
        slot.TakeOut(1);
        slot.MarkDirty();

        byEntity.World.PlaySoundAt(
            new AssetLocation("game:sounds/block/cloth"),
            byEntity, byEntity is EntityPlayer ep2 ? ep2.Player : null);

        if (byEntity is EntityPlayer ep3)
        {
            (ep3.Player as IServerPlayer)?.SendMessage(
                GlobalConstants.GeneralChatGroup,
                Lang.Get("seafarer:sail-applied"),
                EnumChatType.Notification);
        }
    }
}
