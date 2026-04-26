# Outrigger Boat Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a buildable, sailable outrigger canoe to the Seafarer mod, gated behind the existing Drake-quest outrigger schematic, using the base game's `EntityBoatConstruction` system via a Harmony patch on its private `Spawn` method.

**Architecture:** One Harmony prefix patch on `EntityBoatConstruction.Spawn` reads a `boattype` discriminator from the construction entity's JSON `attributes` and spawns `boat-{boattype}-{wood}` instead of the hardcoded `boat-sailed-{wood}`. The construction entity itself uses the base game class with `attributes.boattype: "outrigger"`. A `ItemOutriggerRollers : ItemRoller` subclass overrides `OnHeldInteractStart` so the seafarer rollers item summons the outrigger construction. Two stub classes (`EntityOutriggerBoat`, `ItemOutriggerBoat`) reserve namespaces for future fishing-tuned behavior.

**Tech Stack:** C# (.NET 10), Vintagestory.API + VSSurvivalMod (`EntityBoatConstruction`, `EntityBoat`, `ItemBoat`, `ItemRoller`), HarmonyLib for the patch, JSON5 for asset definitions, `python3 validate-assets.py` for asset validation.

**Reference spec:** `docs/superpowers/specs/2026-04-26-outrigger-boat-design.md`. Read it before starting any task.

---

## File Structure

**New C#:**
- `Seafarer/Seafarer/HarmonyPatches/EntityBoatConstructionSpawnPatch.cs` — Harmony prefix
- `Seafarer/Seafarer/Entity/EntityOutriggerBoat.cs` — stub
- `Seafarer/Seafarer/Item/ItemOutriggerBoat.cs` — stub
- `Seafarer/Seafarer/Item/ItemOutriggerRollers.cs` — `ItemRoller` subclass

**Modified C#:**
- `Seafarer/Seafarer/SeafarerModSystem.cs` — register the three stub/subclass classes (Harmony patch is auto-discovered via existing `harmony.PatchAll` call)

**New JSON assets** (under `Seafarer/Seafarer/assets/seafarer/`):
- `entities/nonliving/outrigger-construction.json`
- `entities/nonliving/boat-outrigger.json`
- `itemtypes/boats/boat-outrigger.json`
- `itemtypes/boats/boat-outrigger-rollers.json`
- `recipes/grid/outrigger-rollers.json`
- `shapes/entity/nonliving/boat/outrigger-construction.json` (placeholder)
- `shapes/entity/nonliving/boat/boat-outrigger.json` (placeholder)

**Modified JSON:**
- `assets/seafarer/lang/en.json` — add ~14 entries

---

## Test/Validation Strategy

Vintage Story mods don't have unit tests. Validation is:
1. **`dotnet build Seafarer/Seafarer.csproj`** — must compile cleanly. Run after every C# change.
2. **`python3 validate-assets.py`** — JSON, lang, texture, shape reference validator. Run after every JSON change.
3. **Manual in-game smoke test** — at the end (Task 16). Creative inventory + `/admin` commands.

Each task ends with the appropriate validation and a commit. Treat a failing validation like a failing test: fix before moving on.

**Commit message convention** (from `git log --oneline`): `feat(outrigger): ...` for feature work, `chore(outrigger): ...` for non-functional changes. Always include `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>` per `CLAUDE.md`.

---

## Task 1: Add `EntityOutriggerBoat` stub class

**Files:**
- Create: `Seafarer/Seafarer/Entity/EntityOutriggerBoat.cs`

- [ ] **Step 1: Write the file**

```csharp
using Vintagestory.GameContent;

namespace Seafarer;

// Stub subclass reserved for future outrigger-specific behavior.
// Currently inherits all behavior from EntityBoat.
public class EntityOutriggerBoat : EntityBoat
{
}
```

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: PASS — `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/Entity/EntityOutriggerBoat.cs
git commit -m "$(cat <<'EOF'
feat(outrigger): add EntityOutriggerBoat stub class

Reserved namespace for future fishing-tuned behavior. Inherits all behavior
from base game EntityBoat for now.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Add `ItemOutriggerBoat` stub class

**Files:**
- Create: `Seafarer/Seafarer/Item/ItemOutriggerBoat.cs`

- [ ] **Step 1: Write the file**

```csharp
using Vintagestory.GameContent;

namespace Seafarer;

// Stub subclass reserved for future outrigger-specific item interactions
// (e.g., custom held-display tooltip, alternate placement rules).
// Currently inherits all behavior from ItemBoat.
public class ItemOutriggerBoat : ItemBoat
{
}
```

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/Item/ItemOutriggerBoat.cs
git commit -m "$(cat <<'EOF'
feat(outrigger): add ItemOutriggerBoat stub class

Reserved namespace for future custom interactions. Inherits all behavior
from base game ItemBoat for now.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Add `ItemOutriggerRollers` subclass

**Files:**
- Create: `Seafarer/Seafarer/Item/ItemOutriggerRollers.cs`

Base game `ItemRoller.OnHeldInteractStart` is hardcoded to spawn `boatconstruction-sailed-oak` (see `/mnt/d/Development/vs/vssurvivalmod/Systems/Boats/ItemRoller.cs:181`). We override the method, copy the placement-validation flow, and swap the spawned entity name to `outrigger-construction-oak`. The "oak" suffix is just the construction's default material variant — the actual launched boat material is captured at the keel stage from the player's planks.

- [ ] **Step 1: Write the file**

```csharp
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Seafarer;

// Subclasses base game ItemRoller. Overrides OnHeldInteractStart to spawn
// the outrigger-construction entity instead of the hardcoded
// boatconstruction-sailed-oak that the base method spawns.
public class ItemOutriggerRollers : ItemRoller
{
    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (blockSel == null) return;
        var player = (byEntity as EntityPlayer)?.Player;

        if (slot.StackSize < 5)
        {
            (api as Vintagestory.API.Client.ICoreClientAPI)?.TriggerIngameError(
                this, "need5", Lang.Get("Need 5 outrigger rollers to place a boat construction site"));
            return;
        }
        if (!SuitableLocation(player, blockSel))
        {
            (api as Vintagestory.API.Client.ICoreClientAPI)?.TriggerIngameError(
                this, "unsuitableLocation",
                Lang.Get("Requires a suitable location near water to place a boat construction site. Boat will roll towards the blue highlighted area. Use tool mode to rotate"));
            return;
        }

        const string defaultMaterial = "oak";
        int orient = GetOrient(player);

        var type = byEntity.World.GetEntityType(
            new AssetLocation("seafarer", "outrigger-construction-" + defaultMaterial));
        if (type == null)
        {
            api.Logger.Error("[seafarer] outrigger-construction-{0} entity type not found.", defaultMaterial);
            return;
        }

        slot.TakeOut(5);
        slot.MarkDirty();

        var entity = byEntity.World.ClassRegistry.CreateEntity(type);
        entity.Pos.SetPos(blockSel.Position.ToVec3d().AddCopy(0.5, 1, 0.5));
        entity.Pos.Yaw = -GameMath.PIHALF + orient * GameMath.PIHALF;

        byEntity.World.SpawnEntity(entity);

        api.World.PlaySoundAt(new AssetLocation("sounds/block/planks"), byEntity, player);

        handling = EnumHandHandling.PreventDefault;
    }

    // Replicates the private SuitableLocation check from base ItemRoller —
    // base-game checks: solid ground below the site, free air above, water in front.
    // We delegate to the base game's siteListByFacing / waterEdgeByFacing static
    // tables which ItemRoller populates in OnLoaded — so they are valid by the
    // time this runs (item is loaded before any interaction).
    private bool SuitableLocation(IPlayer forPlayer, BlockSelection blockSel)
    {
        int orient = GetOrient(forPlayer);
        var siteList = siteListByFacing[orient];
        var waterEdgeList = waterEdgeByFacing[orient];

        var ba = api.World.BlockAccessor;
        bool placeable = true;
        var cpos = blockSel.Position;

        BlockPos minGround = siteList[0].AddCopy(0, 1, 0).Add(cpos);
        BlockPos maxGround = siteList[1].AddCopy(-1, 0, -1).Add(cpos);
        maxGround.Y = minGround.Y;

        ba.WalkBlocks(minGround, maxGround, (block, x, y, z) => {
            if (!block.SideIsSolid(new BlockPos(x, y, z), BlockFacing.UP.Index))
                placeable = false;
        });
        if (!placeable) return false;

        BlockPos minAir = siteList[0].AddCopy(0, 2, 0).Add(cpos);
        BlockPos maxAir = siteList[1].AddCopy(-1, 1, -1).Add(cpos);
        ba.WalkBlocks(minAir, maxAir, (block, x, y, z) => {
            var cboxes = block.GetCollisionBoxes(ba, new BlockPos(x, y, z));
            if (cboxes != null && cboxes.Length > 0) placeable = false;
        });

        BlockPos minWater = waterEdgeList[0].AddCopy(0, 1, 0).Add(cpos);
        BlockPos maxWater = waterEdgeList[1].AddCopy(-1, 0, -1).Add(cpos);
        WalkBlocks(minWater, maxWater, (block, x, y, z) => {
            if (!block.IsLiquid()) placeable = false;
        }, BlockLayersAccess.Fluid);

        return placeable;
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: PASS

If there are compilation errors about `siteListByFacing` / `waterEdgeByFacing` / `GetOrient` / `WalkBlocks` not being accessible: those are `public static` on `ItemRoller`, so they should resolve. If `WalkBlocks` is not in scope (it's a public *instance* method on ItemRoller, inherited), use `this.WalkBlocks(...)`. If `BlockAccessor.WalkBlocks` is the version we need (the standard one without a layer arg), the second `WalkBlocks` call (the one with `BlockLayersAccess.Fluid`) needs the inherited method — confirm by inspecting `ItemRoller.cs` lines 240–262. Adjust accordingly.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/Item/ItemOutriggerRollers.cs
git commit -m "$(cat <<'EOF'
feat(outrigger): add ItemOutriggerRollers (ItemRoller subclass)

Overrides OnHeldInteractStart so placing the rollers spawns
seafarer:outrigger-construction-oak instead of the base-game-hardcoded
boatconstruction-sailed-oak. Reuses the base-class placement-validation
tables (siteListByFacing / waterEdgeByFacing).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Add the Harmony patch on `EntityBoatConstruction.Spawn`

**Files:**
- Create: `Seafarer/Seafarer/HarmonyPatches/EntityBoatConstructionSpawnPatch.cs`

This is the heart of the design — a prefix on the private `Spawn` method that reads `boattype` from the construction entity's JSON attributes and spawns `boat-{boattype}-{wood}` for any non-default boat type. For `boattype: "sailed"` (the base-game default), returns true and lets the original run unchanged. Uses Harmony's `AccessTools.FieldRefAccess` to reach the protected `rcc` field and the private `launchingEntity` / `launchStartPos` fields.

- [ ] **Step 1: Write the file**

```csharp
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
        if (!rcc.StoredWildCards.TryGetValue("wood", out string wood))
        {
            __instance.Api.Logger.Warning(
                "[seafarer] EntityBoatConstructionSpawnPatch: no wood wildcard on {0} — boat not spawned.",
                __instance.Code);
            return false;
        }

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
                "[seafarer] EntityBoatConstructionSpawnPatch: entity {0} not found — no boat spawned. Check that the boat-outrigger entity JSON is loaded.",
                entityCode);
            return false; // skip original to avoid spawning the wrong boat type
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
```

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: PASS

If `RightClickConstruction` is not resolvable, it's in `Vintagestory.GameContent` — already imported. Verify by checking `/mnt/d/Development/vs/vssurvivalmod/Systems/Boats/EntityBoatConstruction.cs:31` for the field type.

If `__instance.Api` is the wrong property, swap to `__instance.World.Api`.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/HarmonyPatches/EntityBoatConstructionSpawnPatch.cs
git commit -m "$(cat <<'EOF'
feat(outrigger): Harmony prefix on EntityBoatConstruction.Spawn

Reads attributes.boattype from the construction entity. For non-default
("sailed") boat types, spawns seafarer:boat-{boattype}-{wood} ourselves
using AccessTools.FieldRefAccess to reach the protected rcc and private
launchingEntity / launchStartPos fields, then returns false to skip the
original. For boattype: sailed, returns true and the original runs
unchanged.

This sidesteps base-game's hardcoded "boat-sailed-{wood}" spawn name
without needing to subclass (Spawn is private, not overridable). Future
Seafarer boats can plug in by declaring their own boattype attribute.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Wire registrations into `SeafarerModSystem`

**Files:**
- Modify: `Seafarer/Seafarer/SeafarerModSystem.cs:54-58` (add three `api.Register*Class` calls inside `Start`; the Harmony patch will be auto-discovered by the existing `harmony.PatchAll` call at line 58)

- [ ] **Step 1: Read the current Start method**

Open `Seafarer/Seafarer/SeafarerModSystem.cs` and locate the block of `api.Register*` calls in `Start()` (lines 28–55). Find the existing line:

```csharp
api.RegisterEntity("EntityProjectileBarbed", typeof(EntityProjectileBarbed));
api.RegisterItemClass("ItemOceanLocatorMap", typeof(ItemOceanLocatorMap));
```

- [ ] **Step 2: Insert three new registrations after the existing ones, before the `harmony = ...` line**

Apply this Edit:

```csharp
// old_string:
api.RegisterEntity("EntityProjectileBarbed", typeof(EntityProjectileBarbed));
            api.RegisterItemClass("ItemOceanLocatorMap", typeof(ItemOceanLocatorMap));

            harmony = new Harmony(HarmonyId);

// new_string:
api.RegisterEntity("EntityProjectileBarbed", typeof(EntityProjectileBarbed));
            api.RegisterItemClass("ItemOceanLocatorMap", typeof(ItemOceanLocatorMap));
            api.RegisterEntity("EntityOutriggerBoat", typeof(EntityOutriggerBoat));
            api.RegisterItemClass("ItemOutriggerBoat", typeof(ItemOutriggerBoat));
            api.RegisterItemClass("ItemOutriggerRollers", typeof(ItemOutriggerRollers));

            harmony = new Harmony(HarmonyId);
```

(The Harmony patch is auto-discovered by the existing `harmony.PatchAll(typeof(SeafarerModSystem).Assembly)` at the next line — no extra wiring needed.)

- [ ] **Step 3: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/SeafarerModSystem.cs
git commit -m "$(cat <<'EOF'
feat(outrigger): register outrigger entity, item, and rollers classes

Registers EntityOutriggerBoat, ItemOutriggerBoat, and ItemOutriggerRollers
under their JSON class names. The Harmony patch on
EntityBoatConstruction.Spawn is picked up automatically by the existing
harmony.PatchAll call.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Create the construction entity JSON

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/entities/nonliving/outrigger-construction.json`

The construction entity uses the base-game class `EntityBoatConstruction`. The Harmony patch reads `attributes.boattype: "outrigger"` from this JSON to know what boat to spawn at launch time.

- [ ] **Step 1: Write the file (JSON5 — trailing commas / unquoted keys allowed)**

```jsonc
{
    code: "outrigger-construction",
    class: "EntityBoatConstruction",
    tags: ["inanimate", "structure"],
    variantgroups: [
        { code: "material", states: ["seasoned", "varnished"], loadFromProperties: "block/wood" }
    ],
    skipVariants: ["*-aged", "*-veryaged", "*-rotten", "*-veryrotten"],
    attributes: {
        boatprefix: "seafarer:boat",
        boattype: "outrigger",
        relayRopeInteractions: true,
        stages: [
            { addElements: ["ORIGIN/ORIGIN0rollers"] },
            {
                requireStacks: [
                    { type: "item", code: "game:firewood", quantity: 8 },
                    { type: "item", code: "game:plank-*", name: "seafarer-outrigger-ingredient-planks", quantity: 12, storeWildCard: "wood" }
                ],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN1Keel", "ORIGIN/ORIGIN1props"]
            },
            {
                requireStacks: [{ type: "block", code: "game:supportbeam-{wood}", quantity: 10 }],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN2Ribs"]
            },
            {
                requireStacks: [{ type: "item", code: "game:plank-{wood}", quantity: 16 }],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN3HullLower"]
            },
            {
                requireStacks: [{ type: "item", code: "game:plank-{wood}", quantity: 16 }],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN4HullUpper"]
            },
            {
                requireStacks: [{ type: "block", code: "game:supportbeam-{wood}", quantity: 12 }],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN5Floats"]
            },
            {
                requireStacks: [
                    { type: "block", code: "game:supportbeam-{wood}", quantity: 8 },
                    { type: "item", code: "game:rope", quantity: 6 }
                ],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN6Crossbeams"]
            },
            {
                requireStacks: [{ type: "block", code: "game:supportbeam-{wood}", quantity: 8 }],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN7Mast"]
            },
            {
                requireStacks: [{ type: "item", code: "game:rope", quantity: 12 }],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN8Rigging"]
            },
            {
                requireStacks: [
                    { type: "block", code: "game:linen-normal-down", quantity: 16 },
                    { type: "item", code: "game:rope", quantity: 4 }
                ],
                addElements: ["ORIGIN/ORIGINBoat/ORIGIN9Sail"]
            },
            { requireStacks: [], actionLangCode: "outrigger-launch" },
            { removeElements: ["ORIGIN/ORIGIN1props"] }
        ]
    },
    behaviorConfigs: {
        selectionboxes: { selectionBoxes: ["SeleAP"] }
    },
    client: {
        size: 1,
        renderer: "Shape",
        shape: { base: "entity/nonliving/boat/outrigger-construction" },
        textures: {
            "material": { base: "game:block/wood/debarked/{material}" },
            "wood":     { base: "game:block/wood/debarked/{material}" },
            "planks":   { base: "game:block/wood/planks/{material}1" },
            "plain":    { base: "game:block/cloth/linen/plain" },
            "rope":     { base: "game:item/resource/rope" },
            "firewood": { base: "game:block/wood/firewood/north" }
        },
        animations: [
            { code: "launch", animation: "launch", animationSpeed: 1, weight: 10, blendMode: "AddAverage" }
        ],
        behaviors: [
            { code: "interpolateposition" },
            { code: "selectionboxes" },
            { code: "floatupwhenstuck" }
        ]
    },
    server: {
        behaviors: [
            { code: "selectionboxes" },
            { code: "floatupwhenstuck" }
        ]
    },
    sounds: {}
}
```

- [ ] **Step 2: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors. Warnings about missing shape files are expected — we add them in Task 13.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/entities/nonliving/outrigger-construction.json
git commit -m "$(cat <<'EOF'
feat(outrigger): construction entity with 10-stage build

Uses base-game EntityBoatConstruction class with attributes.boattype:
"outrigger" so the Harmony patch can detect it at launch and spawn
seafarer:boat-outrigger-{wood}. Stages: rollers, keel (with plank
wildcard capture for material), ribs, lower/upper hull, floats,
crossbeams, mast, rigging, sail, launch.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Create the launched-boat entity JSON

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json`

- [ ] **Step 1: Write the file**

```jsonc
{
    code: "boat",
    class: "EntityOutriggerBoat",
    tags: ["inanimate", "vehicle"],
    weight: 800,
    variantgroups: [
        { code: "type", states: ["outrigger"] },
        { code: "material", states: ["seasoned", "varnished"], loadFromProperties: "block/wood" }
    ],
    skipVariants: ["*-aged", "*-veryaged", "*-rotten", "*-veryrotten"],
    attributes: {
        disabledweatherVaneAnimCode: "weathervane",
        deconstructible: true,
        deconstructDrops: [
            { type: "item",  code: "game:plank-{material}",      quantity: 32 },
            { type: "block", code: "game:supportbeam-{material}", quantity: 24 },
            { type: "item",  code: "game:rope",                   quantity: 12 },
            { type: "block", code: "game:linen-normal-down",      quantity: 8 }
        ],
        shouldSwivelFromMotion: false,
        speedMultiplier: 1.4,
        swimmingOffsetY: 0.7,
        unfurlSails: true,
        mountAnimations: { idle: "sitboatidle", ready: "", forwards: "", backwards: "" }
    },
    hitboxSize: { x: 4, y: 1.0, z: 3 },
    behaviorConfigs: {
        ellipsoidalrepulseagents: {
            offset: { x: 0, z: 0 },
            radius: { x: 2.5, y: 1.5, z: 3.0 }
        },
        passivephysicsmultibox: {
            collisionBoxes: [
                { x1: -0.5, y1: 0, z1: -2.5, x2:  0.5, y2: 1.0, z2:  2.5 },
                { x1: -2.0, y1: 0, z1: -1.5, x2: -1.4, y2: 0.4, z2:  1.5 },
                { x1:  1.4, y1: 0, z1: -1.5, x2:  2.0, y2: 0.4, z2:  1.5 }
            ],
            groundDragFactor: 1,
            airDragFallingFactor: 0.5,
            gravityFactor: 1.0
        },
        creaturecarrier: {
            seats: [
                { apName: "ForeSeatAP", controllable: true, mountOffset: { x: 0, z:  1.4 }, bodyYawLimit: 0.4, eyeHeight: 1 },
                { apName: "AftSeatAP",  controllable: true, mountOffset: { x: 0, z: -1.4 }, bodyYawLimit: 0.4, eyeHeight: 1 }
            ]
        },
        rideableaccessories: {
            dropContentsOnDeath: true,
            wearableSlots: [
                { code: "Oar Storage",   forCategoryCodes: ["oar"],         attachmentPointCode: "OarAP",         stepParentTo: { "": { elementName: "OarStorage" } } },
                { code: "Sail Recolor",  forCategoryCodes: ["sailrecolor"], attachmentPointCode: "SailExtraStorageAP" },
                { code: "Mast Lantern",  forCategoryCodes: ["lantern"],     attachmentPointCode: "MastLanternAP", stepParentTo: { "": { elementName: "MastLantern" } } },
                {
                    code: "Cargo Fore",
                    forCategoryCodes: ["chest", "basket", "storagevessel", "crate"],
                    attachmentPointCode: "CargoForeAP",
                    behindSlots: ["Cargo Mid"],
                    stepParentTo: { "": { elementName: "CargoFore" } }
                },
                {
                    code: "Cargo Mid",
                    forCategoryCodes: ["chest", "basket", "storagevessel", "crate"],
                    attachmentPointCode: "CargoMidAP",
                    behindSlots: ["Cargo Aft"],
                    stepParentTo: { "": { elementName: "CargoMid" } }
                },
                {
                    code: "Cargo Aft",
                    forCategoryCodes: ["chest", "basket", "storagevessel", "crate"],
                    attachmentPointCode: "CargoAftAP",
                    stepParentTo: { "": { elementName: "CargoAft" } }
                },
                { code: "ForeSeatAP", forCategoryCodes: [] },
                { code: "AftSeatAP",  forCategoryCodes: [] }
            ]
        },
        selectionboxes: {
            selectionBoxes: [
                "ForeSeatAP", "AftSeatAP", "OarAP", "SailExtraStorageAP",
                "MastLanternAP", "CargoForeAP", "CargoMidAP", "CargoAftAP"
            ]
        }
    },
    client: {
        size: 1,
        renderer: "Shape",
        shape: { base: "entity/nonliving/boat/boat-outrigger", ignoreElements: ["hideWater"] },
        animations: [
            { code: "turnLeft",  animation: "turnLeft",  animationSpeed: 1, easeInSpeed: 2, easeOutSpeed: 2 },
            { code: "turnRight", animation: "turnRight", animationSpeed: 1, easeInSpeed: 2, easeOutSpeed: 2 },
            { code: "weathervane", animation: "weathervane", animationSpeed: 0, weight: 1, blendMode: "AddAverage" }
        ],
        textures: {
            "material":    { base: "game:block/wood/debarked/{material}" },
            "wood":        { base: "game:block/wood/debarked/{material}" },
            "planks":      { base: "game:block/wood/planks/{material}1" },
            "plain":       { base: "game:block/cloth/linen/plain" },
            "rope":        { base: "game:item/resource/rope" },
            "transparent": { base: "game:block/transparent" }
        },
        behaviors: [
            { code: "ellipsoidalrepulseagents" },
            { code: "passivephysicsmultibox" },
            { code: "interpolateposition" },
            { code: "hidewatersurface", hideWaterElement: "ORIGIN/hideWater/*" },
            { code: "selectionboxes" },
            { code: "rideableaccessories" },
            { code: "creaturecarrier" }
        ]
    },
    server: {
        behaviors: [
            { code: "ellipsoidalrepulseagents" },
            { code: "passivephysicsmultibox" },
            { code: "selectionboxes" },
            { code: "rideableaccessories" },
            { code: "creaturecarrier" }
        ]
    },
    sounds: {}
}
```

- [ ] **Step 2: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors. Shape-file warnings still expected.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/entities/nonliving/boat-outrigger.json
git commit -m "$(cat <<'EOF'
feat(outrigger): launched-boat entity with 14 material variants

Uses class EntityOutriggerBoat (stub). speedMultiplier: 1.4
(faster than sailed boat's 1.2), 2 controllable seats, 3-box
collision (main hull + 2 outrigger floats), 6 wearable slots
(oar, sail recolor, lantern, 3 cargo). No writingsurface, no
shields, no figurehead — keeps the silhouette clean.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Create the boat item JSON

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/itemtypes/boats/boat-outrigger.json`

- [ ] **Step 1: Write the file**

```jsonc
{
    code: "boat",
    class: "ItemOutriggerBoat",
    variantgroups: [
        { code: "type", states: ["outrigger"] },
        { code: "material", states: ["seasoned", "varnished"], loadFromProperties: "block/wood" }
    ],
    skipVariants: ["*-aged", "*-veryaged", "*-rotten", "*-veryrotten"],
    shapeByType: {
        "boat-outrigger-*": { base: "entity/nonliving/boat/boat-outrigger", ignoreElements: ["hideWater"] }
    },
    texturesByType: {
        "*": {
            "material":    { base: "game:block/wood/debarked/{material}" },
            "wood":        { base: "game:block/wood/debarked/{material}" },
            "planks":      { base: "game:block/wood/planks/{material}1" },
            "plain":       { base: "game:block/cloth/linen/plain" },
            "rope":        { base: "game:item/resource/rope" },
            "transparent": { base: "game:block/transparent" }
        }
    },
    attributes: {
        handbook: {
            groupBy: ["boat-{type}-*"],
            extraSections: [
                { title: "handbook-seafarer-outrigger-craftinfo", text: "handbook-seafarer-outrigger-craftinfo" }
            ]
        }
    },
    heldTpIdleAnimation: "boatholdoverhead",
    maxstacksize: 1,
    materialDensity: 1,
    liquidSelectable: 1,
    creativeinventory: { "general": ["*"], "items": ["*"], "tools": ["*"], "seafarer": ["*"] },
    guiTransform: {
        translation: { x: 4.0, y: 7.0, z: 0.0 },
        rotation: { x: -32.0, y: -35.0, z: -180.0 },
        origin: { x: 0.5, y: 0.0, z: 0.5 },
        scale: 0.5
    },
    groundTransform: {
        translation: { x: 0, y: 0, z: 0 },
        rotation: { x: 0, y: 0, z: 0 },
        origin: { x: 0.5, y: 0, z: 0.5 },
        scale: 3.0
    },
    tpHandTransform: {
        translation: { x: -1.33, y: -0.16, z: -0.5 },
        rotation: { x: 6, y: -20, z: 0 },
        origin: { x: 0.5, y: 0, z: 0.5 },
        scale: 1
    }
}
```

- [ ] **Step 2: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/itemtypes/boats/boat-outrigger.json
git commit -m "$(cat <<'EOF'
feat(outrigger): boat-outrigger item with handbook entry

Uses class ItemOutriggerBoat (stub). Same material variants as the
entity. Handbook groups all materials under one entry and points to
Drake's quest in the extra sections.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Create the rollers item JSON

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/itemtypes/boats/boat-outrigger-rollers.json`

- [ ] **Step 1: Write the file**

```jsonc
{
    code: "boat-outrigger-rollers",
    class: "ItemOutriggerRollers",
    maxstacksize: 5,
    attributes: {
        placeSound: "block/planks"
    },
    shape: { base: "game:item/roller" },
    creativeinventory: { "general": ["*"], "items": ["*"], "seafarer": ["*"] },
    guiTransform: {
        translation: { x: 3, y: 0, z: 0 },
        rotation: { x: -22.5, y: -37, z: 180 },
        origin: { x: 0.43, y: 0.15, z: 0.5 },
        scale: 1.43
    },
    combustibleProps: {
        burnTemperature: 700,
        burnDuration: 24
    },
    materialDensity: 700,
    groundTransform: {
        translation: { x: 0, y: 0, z: 0 },
        rotation: { x: 0, y: 0, z: 0 },
        origin: { x: 0.5, y: 0, z: 0.5 },
        scale: 4
    },
    tpHandTransform: {
        translation: { x: -1.1, y: -1, z: -0.8 },
        scale: 0.5
    }
}
```

- [ ] **Step 2: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/itemtypes/boats/boat-outrigger-rollers.json
git commit -m "$(cat <<'EOF'
feat(outrigger): rollers item that summons the construction entity

Single item (no material variants). Uses class ItemOutriggerRollers
(subclass of base game ItemRoller). Reuses the base game roller
shape; on placement, spawns seafarer:outrigger-construction-oak.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Create the rollers grid recipe

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/recipes/grid/outrigger-rollers.json`

The schematic is consumed in the recipe pattern but `noConsumeOnCrafting: true` on the schematic itself makes the engine return it to the player after crafting.

- [ ] **Step 1: Write the file**

```jsonc
[
    {
        ingredientPattern: "S,F,R",
        ingredients: {
            "S": { type: "item", code: "seafarer:schematic-outrigger" },
            "F": { type: "item", code: "game:firewood", quantity: 5 },
            "R": { type: "item", code: "game:rope",     quantity: 4 }
        },
        width: 1,
        height: 3,
        shapeless: true,
        output: { type: "item", code: "seafarer:boat-outrigger-rollers", quantity: 5 }
    }
]
```

- [ ] **Step 2: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/recipes/grid/outrigger-rollers.json
git commit -m "$(cat <<'EOF'
feat(outrigger): grid recipe — schematic + firewood + rope -> 5 rollers

Shapeless recipe. The schematic itself has noConsumeOnCrafting: true
on its existing item definition, so it is returned to the player's
inventory after crafting.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 11: Add language entries

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json` (append entries inside the existing JSON object)

- [ ] **Step 1: Read the current end of `en.json` to find the right insertion point**

Run: `tail -5 Seafarer/Seafarer/assets/seafarer/lang/en.json`
Expected: ends with `}` after a `"key": "value"` entry. The new entries must be inserted before the closing `}` and the previous-last entry must end with a comma (it does not currently — the new entries fix this implicitly).

- [ ] **Step 2: Use Edit to splice the new entries in before the closing brace**

Find the last existing `"key": "value"` line in the file (no trailing comma). Append a comma to it, then insert these lines before the closing `}`:

```jsonc
    "item-boat-outrigger-*": "Outrigger Canoe",
    "entity-boat-outrigger-*": "Outrigger",
    "entity-outrigger-construction": "Outrigger (under construction)",
    "itemdesc-boat-outrigger": "A fast, lightweight twin-hulled fishing boat. Two seats and a single sail.",
    "item-boat-outrigger-rollers": "Outrigger Building Rollers",
    "itemdesc-boat-outrigger-rollers": "Place on water-adjacent ground to begin building an outrigger.",
    "outrigger-launch": "Launch the outrigger",
    "outrigger-stage-1": "Lay the keel (firewood + planks)",
    "outrigger-stage-2": "Install the ribs",
    "outrigger-stage-3": "Plank the lower hull",
    "outrigger-stage-4": "Plank the upper hull",
    "outrigger-stage-5": "Attach the outrigger floats",
    "outrigger-stage-6": "Lash the outrigger crossbeams",
    "outrigger-stage-7": "Raise the mast",
    "outrigger-stage-8": "Rig the lines",
    "outrigger-stage-9": "Hoist the sail",
    "seafarer-outrigger-ingredient-planks": "outrigger planks (any wood)",
    "handbook-seafarer-outrigger-craftinfo": "The outrigger schematic is a quest reward from Drake (deliver 160 seasoned planks). Drake also resells the schematic. Craft the schematic with rope and firewood to get rollers, then place the rollers near water to begin construction.",
    "creature-seafarer:boat-outrigger-*-selectionbox-ForeSeatAP": "Front seat",
    "creature-seafarer:boat-outrigger-*-selectionbox-AftSeatAP": "Rear seat",
    "creature-seafarer:boat-outrigger-*-selectionbox-OarAP": "Oar rest",
    "creature-seafarer:boat-outrigger-*-selectionbox-SailExtraStorageAP": "Sail mount",
    "creature-seafarer:boat-outrigger-*-selectionbox-MastLanternAP": "Mast lantern",
    "creature-seafarer:boat-outrigger-*-selectionbox-CargoForeAP": "Fore cargo",
    "creature-seafarer:boat-outrigger-*-selectionbox-CargoMidAP": "Mid cargo",
    "creature-seafarer:boat-outrigger-*-selectionbox-CargoAftAP": "Aft cargo"
```

- [ ] **Step 3: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/lang/en.json
git commit -m "$(cat <<'EOF'
feat(outrigger): English language entries for outrigger items and stages

Adds 26 entries: item/entity display names, item descriptions, the
launch action label, per-stage build instructions, the keel-stage
plank ingredient label, the handbook craftinfo blurb pointing
to Drake's quest, and 8 selection-box tooltip strings for the
launched boat's interactable points.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 12: Create the placeholder construction shape

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/shapes/entity/nonliving/boat/outrigger-construction.json`

This is a **minimal placeholder** that proves the wiring works. Each stage element is a small named cube. Real visual fidelity is an artist task scoped separately. Required structure:

- Root element `ORIGIN` containing a child `ORIGINBoat` and child `ORIGIN0rollers`, plus a sibling `ORIGIN1props`.
- Inside `ORIGINBoat`: 9 child elements named `ORIGIN1Keel`, `ORIGIN2Ribs`, `ORIGIN3HullLower`, `ORIGIN4HullUpper`, `ORIGIN5Floats`, `ORIGIN6Crossbeams`, `ORIGIN7Mast`, `ORIGIN8Rigging`, `ORIGIN9Sail`.
- One attachment point: `Center` (referenced by the Spawn-time `getCenterPos` in our Harmony patch — placed roughly where the boat's centroid will be).
- One animation: `launch` (just a 1-second ease that the construction entity plays at stage 10).

- [ ] **Step 1: Write the file**

```jsonc
{
    "textureWidth": 16,
    "textureHeight": 16,
    "textures": {
        "material": "game:block/wood/debarked/oak",
        "planks": "game:block/wood/planks/oak1",
        "plain": "game:block/cloth/linen/plain",
        "rope": "game:item/resource/rope",
        "firewood": "game:block/wood/firewood/north"
    },
    "elements": [
        {
            "name": "ORIGIN",
            "from": [0, 0, 0], "to": [0, 0, 0],
            "rotationOrigin": [0, 0, 0],
            "faces": {
                "north": {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "east":  {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "south": {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "west":  {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "up":    {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "down":  {"texture": "#planks", "uv": [0, 0, 1, 1]}
            },
            "attachmentpoints": [
                { "code": "Center", "posX": 0, "posY": 8, "posZ": 0, "rotationX": 0, "rotationY": 0, "rotationZ": 0 }
            ],
            "children": [
                {
                    "name": "ORIGIN0rollers",
                    "from": [-24, 0, -2], "to": [24, 2, 2],
                    "faces": {
                        "north": {"texture": "#material", "uv": [0, 0, 4, 2]},
                        "east":  {"texture": "#material", "uv": [0, 0, 4, 2]},
                        "south": {"texture": "#material", "uv": [0, 0, 4, 2]},
                        "west":  {"texture": "#material", "uv": [0, 0, 4, 2]},
                        "up":    {"texture": "#material", "uv": [0, 0, 4, 2]},
                        "down":  {"texture": "#material", "uv": [0, 0, 4, 2]}
                    }
                },
                {
                    "name": "ORIGIN1props",
                    "from": [-2, 2, -2], "to": [2, 6, 2],
                    "faces": {
                        "north": {"texture": "#firewood", "uv": [0, 0, 1, 1]},
                        "east":  {"texture": "#firewood", "uv": [0, 0, 1, 1]},
                        "south": {"texture": "#firewood", "uv": [0, 0, 1, 1]},
                        "west":  {"texture": "#firewood", "uv": [0, 0, 1, 1]},
                        "up":    {"texture": "#firewood", "uv": [0, 0, 1, 1]},
                        "down":  {"texture": "#firewood", "uv": [0, 0, 1, 1]}
                    }
                },
                {
                    "name": "ORIGINBoat",
                    "from": [0, 2, 0], "to": [0, 2, 0],
                    "faces": {
                        "north": {"texture": "#planks", "uv": [0, 0, 1, 1]},
                        "east":  {"texture": "#planks", "uv": [0, 0, 1, 1]},
                        "south": {"texture": "#planks", "uv": [0, 0, 1, 1]},
                        "west":  {"texture": "#planks", "uv": [0, 0, 1, 1]},
                        "up":    {"texture": "#planks", "uv": [0, 0, 1, 1]},
                        "down":  {"texture": "#planks", "uv": [0, 0, 1, 1]}
                    },
                    "children": [
                        { "name": "ORIGIN1Keel",       "from": [-20,  0, -1], "to": [20,  2,  1], "faces": { "north": {"texture": "#material","uv":[0,0,4,1]}, "east":{"texture":"#material","uv":[0,0,4,1]}, "south":{"texture":"#material","uv":[0,0,4,1]}, "west":{"texture":"#material","uv":[0,0,4,1]}, "up":{"texture":"#material","uv":[0,0,4,1]}, "down":{"texture":"#material","uv":[0,0,4,1]} } },
                        { "name": "ORIGIN2Ribs",       "from": [-18,  2, -3], "to": [18,  4,  3], "faces": { "north": {"texture": "#material","uv":[0,0,4,1]}, "east":{"texture":"#material","uv":[0,0,4,1]}, "south":{"texture":"#material","uv":[0,0,4,1]}, "west":{"texture":"#material","uv":[0,0,4,1]}, "up":{"texture":"#material","uv":[0,0,4,1]}, "down":{"texture":"#material","uv":[0,0,4,1]} } },
                        { "name": "ORIGIN3HullLower",  "from": [-18,  4, -4], "to": [18,  6,  4], "faces": { "north": {"texture": "#planks",  "uv":[0,0,4,1]}, "east":{"texture":"#planks","uv":[0,0,4,1]}, "south":{"texture":"#planks","uv":[0,0,4,1]}, "west":{"texture":"#planks","uv":[0,0,4,1]}, "up":{"texture":"#planks","uv":[0,0,4,1]}, "down":{"texture":"#planks","uv":[0,0,4,1]} } },
                        { "name": "ORIGIN4HullUpper",  "from": [-18,  6, -4], "to": [18,  8,  4], "faces": { "north": {"texture": "#planks",  "uv":[0,0,4,1]}, "east":{"texture":"#planks","uv":[0,0,4,1]}, "south":{"texture":"#planks","uv":[0,0,4,1]}, "west":{"texture":"#planks","uv":[0,0,4,1]}, "up":{"texture":"#planks","uv":[0,0,4,1]}, "down":{"texture":"#planks","uv":[0,0,4,1]} } },
                        { "name": "ORIGIN5Floats",     "from": [-12,  4, -10], "to": [12,  6, -8], "faces": { "north": {"texture": "#material","uv":[0,0,4,1]}, "east":{"texture":"#material","uv":[0,0,4,1]}, "south":{"texture":"#material","uv":[0,0,4,1]}, "west":{"texture":"#material","uv":[0,0,4,1]}, "up":{"texture":"#material","uv":[0,0,4,1]}, "down":{"texture":"#material","uv":[0,0,4,1]} } },
                        { "name": "ORIGIN6Crossbeams", "from": [ -1,  6, -10], "to": [ 1,  8, 10], "faces": { "north": {"texture": "#material","uv":[0,0,1,1]}, "east":{"texture":"#material","uv":[0,0,1,1]}, "south":{"texture":"#material","uv":[0,0,1,1]}, "west":{"texture":"#material","uv":[0,0,1,1]}, "up":{"texture":"#material","uv":[0,0,1,1]}, "down":{"texture":"#material","uv":[0,0,1,1]} } },
                        { "name": "ORIGIN7Mast",       "from": [ -1,  8, -1], "to": [ 1, 30,  1], "faces": { "north": {"texture": "#material","uv":[0,0,1,3]}, "east":{"texture":"#material","uv":[0,0,1,3]}, "south":{"texture":"#material","uv":[0,0,1,3]}, "west":{"texture":"#material","uv":[0,0,1,3]}, "up":{"texture":"#material","uv":[0,0,1,1]}, "down":{"texture":"#material","uv":[0,0,1,1]} } },
                        { "name": "ORIGIN8Rigging",    "from": [-10, 25, -1], "to": [10, 26,  1], "faces": { "north": {"texture": "#rope",    "uv":[0,0,2,1]}, "east":{"texture":"#rope","uv":[0,0,2,1]}, "south":{"texture":"#rope","uv":[0,0,2,1]}, "west":{"texture":"#rope","uv":[0,0,2,1]}, "up":{"texture":"#rope","uv":[0,0,2,1]}, "down":{"texture":"#rope","uv":[0,0,2,1]} } },
                        { "name": "ORIGIN9Sail",       "from": [-12, 12, -1], "to": [12, 28,  0], "faces": { "north": {"texture": "#plain",   "uv":[0,0,4,4]}, "east":{"texture":"#plain","uv":[0,0,4,4]}, "south":{"texture":"#plain","uv":[0,0,4,4]}, "west":{"texture":"#plain","uv":[0,0,4,4]}, "up":{"texture":"#plain","uv":[0,0,4,4]}, "down":{"texture":"#plain","uv":[0,0,4,4]} } }
                    ]
                }
            ]
        }
    ],
    "animations": [
        {
            "name": "launch",
            "code": "launch",
            "quantityframes": 30,
            "onActivityStopped": "Stop",
            "onAnimationEnd": "Stop",
            "keyframes": [
                { "frame": 0, "elements": {} },
                { "frame": 29, "elements": { "ORIGIN": { "offsetZ": -10 } } }
            ]
        }
    ]
}
```

- [ ] **Step 2: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors. The earlier shape-missing warning for `outrigger-construction.json` is gone.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/shapes/entity/nonliving/boat/outrigger-construction.json
git commit -m "$(cat <<'EOF'
feat(outrigger): placeholder construction shape with all stage elements

Minimal placeholder — each stage element is a single labelled cube so the
construction stage system can add/remove them visibly. Includes the Center
attachment point used by the Harmony Spawn patch and a launch animation
that slides the boat off the rollers. Real outrigger geometry is a
separate artist task.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 13: Create the placeholder launched-boat shape

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/shapes/entity/nonliving/boat/boat-outrigger.json`

Required structure:
- Root element `ORIGIN` with attachment points `ForeSeatAP`, `AftSeatAP`, `OarAP`, `SailExtraStorageAP`, `MastLanternAP`, `CargoForeAP`, `CargoMidAP`, `CargoAftAP`.
- Container child elements named `OarStorage`, `MastLantern`, `CargoFore`, `CargoMid`, `CargoAft` (the `stepParentTo` targets in the entity JSON).
- One animation each: `turnLeft`, `turnRight`, `weathervane`.
- A `hideWater/water1` element so `hidewatersurface` behavior has something to attach to.

- [ ] **Step 1: Write the file**

```jsonc
{
    "textureWidth": 16,
    "textureHeight": 16,
    "textures": {
        "material": "game:block/wood/debarked/oak",
        "planks": "game:block/wood/planks/oak1",
        "plain": "game:block/cloth/linen/plain",
        "rope": "game:item/resource/rope",
        "transparent": "game:block/transparent"
    },
    "elements": [
        {
            "name": "ORIGIN",
            "from": [0, 0, 0], "to": [0, 0, 0],
            "rotationOrigin": [0, 0, 0],
            "faces": {
                "north": {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "east":  {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "south": {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "west":  {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "up":    {"texture": "#planks", "uv": [0, 0, 1, 1]},
                "down":  {"texture": "#planks", "uv": [0, 0, 1, 1]}
            },
            "attachmentpoints": [
                { "code": "ForeSeatAP",         "posX":   0, "posY": 6,  "posZ":  18, "rotationX": 0, "rotationY":   0, "rotationZ": 0 },
                { "code": "AftSeatAP",          "posX":   0, "posY": 6,  "posZ": -18, "rotationX": 0, "rotationY": 180, "rotationZ": 0 },
                { "code": "OarAP",              "posX":   8, "posY": 8,  "posZ":   0, "rotationX": 0, "rotationY":   0, "rotationZ": 0 },
                { "code": "SailExtraStorageAP", "posX":   0, "posY": 24, "posZ":   0, "rotationX": 0, "rotationY":   0, "rotationZ": 0 },
                { "code": "MastLanternAP",      "posX":   0, "posY": 30, "posZ":   0, "rotationX": 0, "rotationY":   0, "rotationZ": 0 },
                { "code": "CargoForeAP",        "posX":   0, "posY": 6,  "posZ":  10, "rotationX": 0, "rotationY":   0, "rotationZ": 0 },
                { "code": "CargoMidAP",         "posX":   0, "posY": 6,  "posZ":   0, "rotationX": 0, "rotationY":   0, "rotationZ": 0 },
                { "code": "CargoAftAP",         "posX":   0, "posY": 6,  "posZ": -10, "rotationX": 0, "rotationY":   0, "rotationZ": 0 }
            ],
            "children": [
                { "name": "MainHull",   "from": [-4,  2, -20], "to": [ 4,  6, 20], "faces": { "north": {"texture": "#planks","uv":[0,0,2,1]}, "east":{"texture":"#planks","uv":[0,0,4,1]}, "south":{"texture":"#planks","uv":[0,0,2,1]}, "west":{"texture":"#planks","uv":[0,0,4,1]}, "up":{"texture":"#planks","uv":[0,0,2,4]}, "down":{"texture":"#planks","uv":[0,0,2,4]} } },
                { "name": "PortFloat",  "from": [-16, 2, -12], "to": [-12, 4,  12], "faces": { "north": {"texture": "#material","uv":[0,0,1,1]}, "east":{"texture":"#material","uv":[0,0,3,1]}, "south":{"texture":"#material","uv":[0,0,1,1]}, "west":{"texture":"#material","uv":[0,0,3,1]}, "up":{"texture":"#material","uv":[0,0,1,3]}, "down":{"texture":"#material","uv":[0,0,1,3]} } },
                { "name": "StarFloat",  "from": [12,  2, -12], "to": [16,  4,  12], "faces": { "north": {"texture": "#material","uv":[0,0,1,1]}, "east":{"texture":"#material","uv":[0,0,3,1]}, "south":{"texture":"#material","uv":[0,0,1,1]}, "west":{"texture":"#material","uv":[0,0,3,1]}, "up":{"texture":"#material","uv":[0,0,1,3]}, "down":{"texture":"#material","uv":[0,0,1,3]} } },
                { "name": "Crossbeams", "from": [-16, 6,  -8], "to": [ 16, 7,   8], "faces": { "north": {"texture": "#material","uv":[0,0,4,1]}, "east":{"texture":"#material","uv":[0,0,2,1]}, "south":{"texture":"#material","uv":[0,0,4,1]}, "west":{"texture":"#material","uv":[0,0,2,1]}, "up":{"texture":"#material","uv":[0,0,4,2]}, "down":{"texture":"#material","uv":[0,0,4,2]} } },
                { "name": "Mast",       "from": [-1,  6,  -1], "to": [  1, 30,  1], "faces": { "north": {"texture": "#material","uv":[0,0,1,3]}, "east":{"texture":"#material","uv":[0,0,1,3]}, "south":{"texture":"#material","uv":[0,0,1,3]}, "west":{"texture":"#material","uv":[0,0,1,3]}, "up":{"texture":"#material","uv":[0,0,1,1]}, "down":{"texture":"#material","uv":[0,0,1,1]} } },
                { "name": "Sail",       "from": [-12, 12, -1], "to": [ 12, 28,  0], "faces": { "north": {"texture": "#plain",   "uv":[0,0,4,4]}, "east":{"texture":"#plain","uv":[0,0,1,4]}, "south":{"texture":"#plain","uv":[0,0,4,4]}, "west":{"texture":"#plain","uv":[0,0,1,4]}, "up":{"texture":"#plain","uv":[0,0,4,1]}, "down":{"texture":"#plain","uv":[0,0,4,1]} } },
                { "name": "OarStorage",  "from": [ 4,  6,  -2], "to": [  6,  7,  2], "faces": { "north": {"texture": "#material","uv":[0,0,1,1]}, "east":{"texture":"#material","uv":[0,0,1,1]}, "south":{"texture":"#material","uv":[0,0,1,1]}, "west":{"texture":"#material","uv":[0,0,1,1]}, "up":{"texture":"#material","uv":[0,0,1,1]}, "down":{"texture":"#material","uv":[0,0,1,1]} } },
                { "name": "MastLantern", "from": [-1, 28,  -1], "to": [  1, 30,  1], "faces": { "north": {"texture": "#material","uv":[0,0,1,1]}, "east":{"texture":"#material","uv":[0,0,1,1]}, "south":{"texture":"#material","uv":[0,0,1,1]}, "west":{"texture":"#material","uv":[0,0,1,1]}, "up":{"texture":"#material","uv":[0,0,1,1]}, "down":{"texture":"#material","uv":[0,0,1,1]} } },
                { "name": "CargoFore",   "from": [-3,  6,   8], "to": [  3,  8, 12], "faces": { "north": {"texture": "#planks","uv":[0,0,1,1]}, "east":{"texture":"#planks","uv":[0,0,1,1]}, "south":{"texture":"#planks","uv":[0,0,1,1]}, "west":{"texture":"#planks","uv":[0,0,1,1]}, "up":{"texture":"#planks","uv":[0,0,1,1]}, "down":{"texture":"#planks","uv":[0,0,1,1]} } },
                { "name": "CargoMid",    "from": [-3,  6,  -2], "to": [  3,  8,  2], "faces": { "north": {"texture": "#planks","uv":[0,0,1,1]}, "east":{"texture":"#planks","uv":[0,0,1,1]}, "south":{"texture":"#planks","uv":[0,0,1,1]}, "west":{"texture":"#planks","uv":[0,0,1,1]}, "up":{"texture":"#planks","uv":[0,0,1,1]}, "down":{"texture":"#planks","uv":[0,0,1,1]} } },
                { "name": "CargoAft",    "from": [-3,  6, -12], "to": [  3,  8, -8], "faces": { "north": {"texture": "#planks","uv":[0,0,1,1]}, "east":{"texture":"#planks","uv":[0,0,1,1]}, "south":{"texture":"#planks","uv":[0,0,1,1]}, "west":{"texture":"#planks","uv":[0,0,1,1]}, "up":{"texture":"#planks","uv":[0,0,1,1]}, "down":{"texture":"#planks","uv":[0,0,1,1]} } },
                {
                    "name": "hideWater",
                    "from": [0, 0, 0], "to": [0, 0, 0],
                    "faces": {
                        "north": {"texture": "#transparent","uv":[0,0,1,1]},
                        "east":  {"texture": "#transparent","uv":[0,0,1,1]},
                        "south": {"texture": "#transparent","uv":[0,0,1,1]},
                        "west":  {"texture": "#transparent","uv":[0,0,1,1]},
                        "up":    {"texture": "#transparent","uv":[0,0,1,1]},
                        "down":  {"texture": "#transparent","uv":[0,0,1,1]}
                    },
                    "children": [
                        {
                            "name": "water1",
                            "from": [-18, 1, -22], "to": [18, 3, 22],
                            "faces": {
                                "north": {"texture": "#transparent","uv":[0,0,1,1]},
                                "east":  {"texture": "#transparent","uv":[0,0,1,1]},
                                "south": {"texture": "#transparent","uv":[0,0,1,1]},
                                "west":  {"texture": "#transparent","uv":[0,0,1,1]},
                                "up":    {"texture": "#transparent","uv":[0,0,1,1]},
                                "down":  {"texture": "#transparent","uv":[0,0,1,1]}
                            }
                        }
                    ]
                }
            ]
        }
    ],
    "animations": [
        {
            "name": "turnLeft",  "code": "turnLeft",
            "quantityframes": 20, "onActivityStopped": "Stop", "onAnimationEnd": "Stop",
            "keyframes": [
                { "frame": 0,  "elements": { "ORIGIN": { "rotationZ":  0 } } },
                { "frame": 19, "elements": { "ORIGIN": { "rotationZ": 10 } } }
            ]
        },
        {
            "name": "turnRight", "code": "turnRight",
            "quantityframes": 20, "onActivityStopped": "Stop", "onAnimationEnd": "Stop",
            "keyframes": [
                { "frame": 0,  "elements": { "ORIGIN": { "rotationZ":   0 } } },
                { "frame": 19, "elements": { "ORIGIN": { "rotationZ": -10 } } }
            ]
        },
        {
            "name": "weathervane", "code": "weathervane",
            "quantityframes": 360,
            "keyframes": [
                { "frame": 0,   "elements": { "Sail": { "rotationY":   0 } } },
                { "frame": 359, "elements": { "Sail": { "rotationY": 359 } } }
            ]
        }
    ]
}
```

- [ ] **Step 2: Validate assets**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -30`
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/shapes/entity/nonliving/boat/boat-outrigger.json
git commit -m "$(cat <<'EOF'
feat(outrigger): placeholder launched-boat shape

Main hull + 2 outrigger floats + crossbeams + mast + sail. All 8
attachment points and 5 stepParentTo container elements are present
so creaturecarrier and rideableaccessories slots resolve. Includes
turnLeft/turnRight/weathervane animations and the hideWater element.
Real geometry/UVs are an artist task.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 14: Final asset and build validation

- [ ] **Step 1: Run the asset validator across the whole repo**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -50`
Expected: `0 errors`. Warnings acceptable only if they predate this work — diff against the last commit's run if uncertain.

- [ ] **Step 2: Run the full build**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 3: If both pass, the code is ready for in-game smoke test (Task 15). No commit at this step (nothing changed); proceed.**

---

## Task 15: In-game smoke test

**Goal:** prove that the Harmony patch fires, that all stages can be advanced, and that the launched outrigger boat is mountable and steerable.

This task does not modify code — it's a manual validation. Record findings in the commit message of the *next* task (or on stop, if smoke test reveals a bug, file the bug as a follow-up task at the bottom of this plan and address before claiming done).

- [ ] **Step 1: Launch Vintage Story with the dev mod loaded**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj && cd "$VINTAGE_STORY" && ./Vintagestory --addOrigin /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/bin/Debug/Mods/mod`

(If this is wrong for your environment, ask the user how they normally launch the mod — `build.sh` may handle this.)

- [ ] **Step 2: Create a creative-mode world near a coastline**

In-game:
1. Single Player → New World → Creative mode → biome that has a beach/water at spawn.
2. Wait for world to generate.

- [ ] **Step 3: Verify the outrigger items appear in creative inventory**

Open inventory → Seafarer creative tab. Confirm presence of:
- `Outrigger Building Rollers` (the rollers item)
- `Outrigger Canoe` (the boat item, with material variants)
- `Outrigger Schematic` (already existed)

If any of these are missing or have raw lang keys ("item-boat-outrigger-rollers" instead of "Outrigger Building Rollers"), check the lang/en.json change from Task 11.

- [ ] **Step 4: Place the rollers and verify the construction entity appears**

1. Take 5 outrigger rollers from creative inventory.
2. Stand near water on flat ground.
3. Right-click on the ground (use tool mode to rotate facing if needed).
4. Confirm the placeholder construction entity (an oak-textured stack of cubes resembling a sketched-out boat with rollers underneath) appears.

If `outrigger-construction-oak entity type not found` shows in the log: check that the entity JSON's variantgroups produce an `oak` variant. The `material` variantgroup uses `loadFromProperties: "block/wood"` plus `["seasoned","varnished"]` — `oak` should be in `block/wood`.

- [ ] **Step 5: Walk through stages 1–9**

For each stage, take the required materials from creative inventory and right-click on the construction entity. Confirm:
- The interaction prompt names the right material and quantity.
- After supplying all materials, the next stage element appears (visible cube grows on the construction).
- The stage advances (info text shows "Build stage X of 11").

The materials per stage (default `wood = oak`):
- Stage 1: 8 firewood + 12 plank-oak
- Stage 2: 10 supportbeam-oak
- Stage 3: 16 plank-oak
- Stage 4: 16 plank-oak
- Stage 5: 12 supportbeam-oak
- Stage 6: 8 supportbeam-oak + 6 rope
- Stage 7: 8 supportbeam-oak
- Stage 8: 12 rope
- Stage 9: 16 linen-normal-down + 4 rope

- [ ] **Step 6: Trigger launch and confirm the Harmony patch worked**

After stage 9 (sail attached), right-click once more to trigger the launch animation. After the animation completes:
- Confirm the construction entity is gone.
- Confirm a `boat-outrigger-oak` entity has appeared in the water (placeholder shape — main hull + 2 floats + mast).
- Open the in-game console / log file (`%appdata%/VintagestoryData/Logs/server-debug.log`). Search for `[seafarer] EntityBoatConstructionSpawnPatch`. If you see the "entity not found, falling back" warning, the patch did NOT spawn the outrigger — check spelling of the entity code in the patch vs the entity JSON.

- [ ] **Step 7: Mount and ride the outrigger**

1. Right-click the boat to mount the fore seat.
2. Use WASD — confirm forward motion (and that it's noticeably faster than a base sailed boat — speedMultiplier 1.4 vs 1.2).
3. Mouse-look — confirm you can turn.
4. Dismount, attach a basket to one of the cargo slots, attach a lantern to the mast lantern slot, attach an oar. Confirm all slots accept the right items.

- [ ] **Step 8: Test the recipe**

In creative inventory: get 1 `seafarer:schematic-outrigger`, 5 `game:firewood`, 4 `game:rope`. Open the crafting grid, place ingredients shapelessly. Confirm: 5 outrigger rollers come out AND the schematic returns to the player's inventory.

If schematic does NOT return: open `seafarer:schematic-outrigger.json` and verify `noConsumeOnCrafting: true` is in `attributes`. If still consumed, the recipe may need explicit `recipeAttributes` — check the spec's Open Question on this and apply the fix.

- [ ] **Step 9: Test the deconstruct refund**

In creative: place 5 fresh outrigger rollers near water. Approach the spawned construction with empty hand + sneak (Shift). Right-click. Confirm: the construction disappears and the player receives 5 `roller` items (the base game roller, not the outrigger rollers). This is the expected behavior per the spec.

- [ ] **Step 10: Document smoke test results**

Edit this plan file: under each step you completed in Task 15, change `- [ ]` to `- [x]`. If any step revealed a bug, add a follow-up task at the bottom of this plan describing the fix needed, and resolve before declaring this plan done.

---

## Task 16: Final commit, summary

- [ ] **Step 1: Stage and commit any test-driven fixes from Task 15 (if needed)**

If the smoke test surfaced bugs, fix them (incrementally, with small commits). For each fix:

```bash
git add <fixed-files>
git commit -m "fix(outrigger): <one-line description>"
```

- [ ] **Step 2: Confirm no uncommitted changes**

Run: `git status`
Expected: `nothing to commit, working tree clean`

- [ ] **Step 3: Tag the work in the plan file as complete**

Edit the very top of this plan file, under the goal, add: `**Status: Implemented YYYY-MM-DD**` (with today's date). Commit the plan update:

```bash
git add docs/superpowers/plans/2026-04-26-outrigger-boat.md
git commit -m "$(cat <<'EOF'
docs(outrigger): mark implementation plan complete

All 16 tasks done. Outrigger boat is buildable end-to-end via the
Drake schematic, the construction system spawns it correctly via
the Harmony patch, and the launched boat is mountable, steerable,
and accepts cargo / oar / lantern / sail-recolor accessories.

Placeholder shapes are in place — replacing them with real outrigger
geometry is a separate artist task.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Follow-up (out of scope for this plan)

- **Real outrigger shape art**: replace the placeholder shapes in `outrigger-construction.json` and `boat-outrigger.json` with proper VS-Modeler geometry. The element name contract (stage names, attachment-point names, container names listed in this plan and the spec) must be preserved. When creating the real construction shape, also add a `Selebox` root element with `code: "SeleAP"` so the construction's `behaviorConfigs.selectionboxes.selectionBoxes: ["SeleAP"]` resolves to a precise interaction box (currently silently ignored — players click the entity's hitbox).
- **Seafarer-specific deconstruct refund**: a second Harmony patch on `EntityBoatConstruction.OnInteract` could refund `boat-outrigger-rollers` instead of base game `roller`s when the player deconstructs at stage 0. The spec flags this as a deferred follow-up.
- **Sail-recolor pattern asset**: ship a Seafarer-themed `sailrecolor` pattern that fits the Asian-fishing-village aesthetic (painted sail). Currently the slot accepts any base-game `sailrecolor` item.
- **Handbook image**: add an outrigger picture to the handbook entry once the real shape exists.
