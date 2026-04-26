using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

// Prefix on EntityBoatConstruction.Spawn (private). Reads attributes.boattype
// from the construction entity's JSON; for non-default ("sailed") values,
// spawns boat-{boattype}-{wood} ourselves and returns false to skip the
// original. This replaces the base game's hardcoded "boat-sailed-{wood}"
// spawn name without subclassing (Spawn is private, can't be overridden).
[HarmonyPatch(typeof(EntityBoatConstruction), "Spawn")]
public static class EntityBoatConstructionSpawnPatch
{
    private static readonly AccessTools.FieldRef<EntityBoatConstruction, RightClickConstruction> rccRef
        = AccessTools.FieldRefAccess<EntityBoatConstruction, RightClickConstruction>("rcc");
    private static readonly AccessTools.FieldRef<EntityBoatConstruction, EntityAgent> launchingEntityRef
        = AccessTools.FieldRefAccess<EntityBoatConstruction, EntityAgent>("launchingEntity");
    private static readonly AccessTools.FieldRef<EntityBoatConstruction, Vec3f> launchStartPosRef
        = AccessTools.FieldRefAccess<EntityBoatConstruction, Vec3f>("launchStartPos");

    [HarmonyPrefix]
    public static bool Prefix(EntityBoatConstruction __instance)
    {
        var boatType = __instance.Properties.Attributes?["boattype"]?.AsString("sailed") ?? "sailed";
        if (boatType == "sailed") return true; // let the original run unchanged

        var rcc = rccRef(__instance);
        if (!rcc.StoredWildCards.TryGetValue("wood", out string wood)) return false;

        // Replicate getCenterPos (private) inline.
        Vec3f nowOff = null;
        var apap = __instance.AnimManager.Animator?.GetAttachmentPointPose("Center");
        if (apap != null)
        {
            var mat = new Matrixf();
            mat.RotateY(__instance.Pos.Yaw + GameMath.PIHALF);
            apap.Mul(mat);
            nowOff = mat.TransformVector(new Vec4f(0, 0, 0, 1)).XYZ;
        }
        var launchStartPos = launchStartPosRef(__instance);
        Vec3f offset = nowOff == null ? new Vec3f() : nowOff - launchStartPos;

        var entityCode = new AssetLocation("seafarer", $"boat-{boatType}-{wood}");
        var type = __instance.World.GetEntityType(entityCode);
        if (type == null)
        {
            __instance.Api.Logger.Warning(
                "[seafarer] EntityBoatConstructionSpawnPatch: entity {0} not found, falling back to base Spawn.",
                entityCode);
            return true; // fall through to original (will spawn boat-sailed-{wood})
        }

        var entity = __instance.World.ClassRegistry.CreateEntity(type);

        if ((int)System.Math.Abs(__instance.Pos.Yaw * GameMath.RAD2DEG) == 90
            || (int)System.Math.Abs(__instance.Pos.Yaw * GameMath.RAD2DEG) == 270)
        {
            offset.X *= 1.1f;
        }
        offset.Y = 0.5f;
        entity.Pos.SetFrom(__instance.Pos).Add(offset);
        entity.Pos.Motion.Add(offset.X / 50.0, 0, offset.Z / 50.0);

        var launchingEntity = launchingEntityRef(__instance);
        var plr = (launchingEntity as EntityPlayer)?.Player;
        if (plr != null)
        {
            entity.WatchedAttributes.SetString("createdByPlayername", plr.PlayerName);
            entity.WatchedAttributes.SetString("createdByPlayerUID", plr.PlayerUID);
        }

        __instance.World.SpawnEntity(entity);
        return false; // skip original
    }
}
