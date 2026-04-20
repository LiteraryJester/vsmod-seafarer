using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Seafarer
{
    public class ItemOceanLocatorMap : Item
    {
        private ModSystemStructureLocator strucLocSys;
        private LocatorProps props;
        private int searchRange;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            strucLocSys = api.ModLoader.GetModSystem<ModSystemStructureLocator>();
            props = Attributes["locatorProps"].AsObject<LocatorProps>();
            searchRange = Attributes["searchRange"].AsInt(2000);
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            if (slot.Empty) return;

            handling = EnumHandHandling.Handled;
            var player = (byEntity as EntityPlayer)?.Player as IServerPlayer;
            if (player == null) return;

            var wml = api.ModLoader.GetModSystem<WorldMapManager>()
                .MapLayers.FirstOrDefault(ml => ml is WaypointMapLayer) as WaypointMapLayer;

            var attr = slot.Itemstack.Attributes;
            Vec3d pos = null;

            if (attr.HasAttribute("position"))
            {
                var struc = strucLocSys.GetStructure(new StructureLocation()
                {
                    Position = attr.GetVec3i("position"),
                    RegionX = attr.GetInt("regionX"),
                    RegionZ = attr.GetInt("regionZ")
                });
                if (struc != null)
                {
                    var c = struc.Location.Center;
                    pos = new Vec3d(c.X + 0.5, c.Y + 0.5, c.Z + 0.5);
                }
            }

            if (pos == null)
            {
                var loc = strucLocSys.FindFreshStructureLocation(props.SchematicCode, byEntity.Pos.AsBlockPos, searchRange);
                if (loc != null)
                {
                    attr.SetVec3i("position", loc.Position);
                    attr.SetInt("regionX", loc.RegionX);
                    attr.SetInt("regionZ", loc.RegionZ);
                    strucLocSys.ConsumeStructureLocation(loc);
                    slot.MarkDirty();

                    var struc = strucLocSys.GetStructure(loc);
                    if (struc != null)
                    {
                        var c = struc.Location.Center;
                        pos = new Vec3d(c.X + 0.5, c.Y + 0.5, c.Z + 0.5);
                    }
                }
            }

            if (pos == null)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("No location found on this map"), EnumChatType.Notification);
                return;
            }

            if (props.Offset != null)
            {
                pos.Add(props.Offset);
            }

            if (!attr.HasAttribute("randomX") && (props.RandomX > 0 || props.RandomZ > 0))
            {
                var rnd = new Random(api.World.Seed + Code.GetHashCode());
                attr.SetFloat("randomX", (float)rnd.NextDouble() * props.RandomX * 2 - props.RandomX);
                attr.SetFloat("randomZ", (float)rnd.NextDouble() * props.RandomZ * 2 - props.RandomZ);
                slot.MarkDirty();
            }

            pos.X += attr.GetFloat("randomX");
            pos.Z += attr.GetFloat("randomZ");

            if (byEntity.World.Config.GetBool("allowMap", true) != true || wml == null)
            {
                var vec = pos.Sub(byEntity.Pos.XYZ);
                vec.Y = 0;
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("{0} blocks distance", (int)vec.Length()), EnumChatType.Notification);
                return;
            }

            var puid = (byEntity as EntityPlayer).PlayerUID;
            if (wml.Waypoints.Where(wp => wp.OwningPlayerUid == puid).FirstOrDefault(wp => wp.Position == pos) != null)
            {
                player.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("Location already marked on your map"), EnumChatType.Notification);
                return;
            }

            wml.AddWaypoint(new Waypoint()
            {
                Color = ColorUtil.ColorFromRgba(
                    (int)(props.WaypointColor[0] * 255),
                    (int)(props.WaypointColor[1] * 255),
                    (int)(props.WaypointColor[2] * 255),
                    (int)(props.WaypointColor[3] * 255)),
                Icon = props.WaypointIcon,
                Pinned = true,
                Position = pos,
                OwningPlayerUid = puid,
                Title = Lang.Get(props.WaypointText),
            }, player);

            player.SendMessage(GlobalConstants.GeneralChatGroup,
                Lang.Get("Approximate location of {0} added to your world map", Lang.Get(props.WaypointText)),
                EnumChatType.Notification);
        }
    }
}
