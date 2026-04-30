# Tortuga as Story Structure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move Tortuga out of Seafarer's custom ocean worldgen and into base-game story structures so the 201×56×196 schematic can place without cross-chunk write truncation, and re-wire `map-tortuga` to resolve from the story-structure dictionary.

**Architecture:** Three JSON-only changes. Remove the Tortuga entry from `oceanstructures.json`. Add a matching entry to `storystructures.json` with `placement: "surface"`, `UseWorldgenHeight: true`, `ExcludeSchematicSizeProtect: true`, and wide `landformRadius`. Switch `map-tortuga` from Seafarer's `ItemOceanLocatorMap` to base-game `ItemLocatorMap` with restructured `locatorProps` attributes.

**Tech Stack:** VS JSON5 asset format, base-game `GenStoryStructures` and `ItemLocatorMap`

**Spec:** `docs/superpowers/specs/2026-04-20-tortuga-as-story-structure-design.md`

**Project note:** VS mod, no automated test suite. Verification is `dotnet build` (asset copy), `python3 validate-assets.py`, and manual in-game check.

**Build command (from repo root):** `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`

---

## File Map

| File | Responsibility | Change |
|---|---|---|
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Ocean structure registry — keep wreck entries, remove Tortuga | Modify |
| `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` | Story structure registry — add Tortuga | Modify |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` | Map item definition — switch class and restructure attributes | Modify |

---

### Task 1: Remove Tortuga from `oceanstructures.json`

Drop the third structure entry. Ocean placement can't handle its size.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`

- [ ] **Step 1: Delete the Tortuga structure entry**

Open `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`. Locate the `tortuga` entry (the third object in the `structures` array, identified by `"code": "tortuga"` and `"schematics": ["costal/tortuga"]`).

Remove the entire `{ ... }` block for `tortuga`, including the preceding comma on the prior entry's closing brace. After the edit, the `structures` array should contain exactly two entries: `wreck-crimsonrose` and `wreck-one`.

The final file structure should be:

```json5
{
  "structures": [
    {
      "code": "wreck-crimsonrose",
      "schematics": ["underwater/wreck-crimson-rose"],
      "placement": "underwater",
      "chance": 0.015,
      "minWaterDepth": 10,
      "maxWaterDepth": 50,
      "offsetY": 0,
      "maxCount": 1,
      "suppressTrees": true,
      "randomRotation": true
    },
    {
      "code": "wreck-one",
      "schematics": ["underwater/wreck-one"],
      "placement": "underwater",
      "chance": 0.015,
      "minWaterDepth": 10,
      "maxWaterDepth": 50,
      "offsetY": 0,
      "suppressTrees": true,
      "randomRotation": true
    }
  ]
}
```

(Note: preserve any user-made tweaks to the wreck entries in the current working copy. The content above reflects the last-known-good structure; only the Tortuga block is being removed.)

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json
git commit -m "$(cat <<'EOF'
refactor(worldgen): remove Tortuga from oceanstructures.json

The 201x196 footprint cannot be placed by GenOceanStructures because
ChunkColumnGeneration's block accessor only writes to chunks mid-
generation; Tortuga's placement spills into neighboring chunks that
are still pending, silently dropping block-sets. Move to story
structures in a follow-up commit.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Add Tortuga to `storystructures.json`

Register Tortuga as a story structure with the exact field set worked out in the spec.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json`

- [ ] **Step 1: Add the Tortuga structure entry**

Open `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json`. Locate the `structures` array (currently contains only the `potatoking` entry).

Add the Tortuga entry as a second array element, after the `potatoking` entry. The full updated `structures` array should look like:

```json5
"structures": [
  {
    "code": "potatoking",
    "group": "storystructure",
    "name": "Potato King's House",
    "schematics": ["surface/potato-king-house"],
    "placement": "surface",
    "UseWorldgenHeight": true,
    "dependsOnStructure": "spawn",
    "minSpawnDistX": -2500,
    "maxSpawnDistX":  2500,
    "minSpawnDistZ": -2500,
    "maxSpawnDistZ":  2500,
    "requireLandform": "veryflat",
    "landformRadius": 80,
    "generateGrass": true,
    "skipGenerationCategories": {
      "structures": 80,
      "trees": 50,
      "shrubs": 50,
      "hotsprings": 100,
      "patches": 30
    }
  },
  {
    "code": "tortuga",
    "group": "storystructure",
    "name": "Tortuga",
    "schematics": ["costal/tortuga"],
    "placement": "surface",
    "UseWorldgenHeight": true,
    "ExcludeSchematicSizeProtect": true,
    "dependsOnStructure": "spawn",
    "minSpawnDistX": -3000,
    "maxSpawnDistX":  3000,
    "minSpawnDistZ": -3000,
    "maxSpawnDistZ":  3000,
    "requireLandform": "flat",
    "landformRadius": 250,
    "generateGrass": true,
    "skipGenerationCategories": {
      "structures": 300,
      "trees": 200,
      "shrubs": 150,
      "hotsprings": 250,
      "patches": 100,
      "pond": 200,
      "rivulets": 100
    }
  }
]
```

Preserve the existing `schematicYOffsets` and `rocktypeRemapGroups` top-level keys exactly as they are. Only the `structures` array content changes.

Note: we deliberately do NOT add a `"surface/costal/tortuga"` entry to `schematicYOffsets` — that dictionary is dead code in VS 1.22-rc.10 (the loader reads it into `scfg.SchematicYOffsets` but nothing else consumes that field). Any Y tuning must go through the schematic's internal OffsetY field instead.

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Run asset validator**

Run: `python3 validate-assets.py 2>&1 | tail -6`
Expected: baseline (1 pre-existing `premiumfish` error; 0 new).

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json
git commit -m "$(cat <<'EOF'
feat(worldgen): register Tortuga as a story structure

Story structures use api.Event.WorldgenHook rather than per-chunk
ChunkColumnGeneration - the hook fires once per structure after the
game pre-loads every chunk in the target region, so cross-chunk writes
for the 201x196 footprint succeed.

ExcludeSchematicSizeProtect bypasses base-game size-protection cap
(same flag BetterRuins uses for its 201-dim sunrift). Placement uses
UseWorldgenHeight to sit on terrain; landformRadius 250 on "flat"
lets the placer find a coastal plain.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Switch `map-tortuga` to `ItemLocatorMap`

Base-game `ItemLocatorMap.OnHeldInteractStart` calls `storyStructures.Structures.Get(code)` — a direct dictionary lookup with no range limit. This is the correct class now that Tortuga is a story structure.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json`

- [ ] **Step 1: Rewrite the item definition**

Open `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` and replace its entire contents with:

```json
{
    "code": "map-tortuga",
    "class": "ItemLocatorMap",
    "maxstacksize": 1,
    "attributes": {
        "displaycaseable": true,
        "shelvable": true,
        "readable": true,
        "editable": false,
        "maxPageCount": 1,
        "locatorProps": {
            "schematiccode": "tortuga",
            "waypointtext": "location-tortuga",
            "waypointicon": "x",
            "waypointcolor": [0.95, 0.75, 0.2, 1],
            "randomX": 15,
            "randomZ": 15
        }
    },
    "shape": { "base": "game:item/clutter/fishing/bottlemessage" },
    "creativeinventory": { "general": ["*"], "items": ["*"], "seafarer": ["*"] }
}
```

Key differences from the previous file:
- `"class": "ItemOceanLocatorMap"` → `"class": "ItemLocatorMap"`
- `"locatorPropsbyType": { "*": {...} }` (wildcard-keyed) → `"locatorProps": {...}` (flat singular, read by base class)
- Removed `"searchRange": 10000` — base `ItemLocatorMap` doesn't read this; its player-use path is unranged
- `"schematiccode": "tortuga"` unchanged (matches `structure.code` in storystructures.json)

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Run asset validator**

Run: `python3 validate-assets.py 2>&1 | tail -6`
Expected: baseline.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json
git commit -m "$(cat <<'EOF'
feat(item): switch map-tortuga to base-game ItemLocatorMap class

Tortuga is now registered as a story structure (see storystructures.json)
so its location lives in storyStructures.Structures - the dictionary that
base-game ItemLocatorMap.OnHeldInteractStart reads via Structures.Get(code).

Restructure locatorPropsbyType wildcard to flat locatorProps (base class
uses the singular form). Drop searchRange; base class does not use it.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Manual in-game verification

Run the test plan from the spec. No automation available.

**Files:** None modified.

- [ ] **Step 1: Create a fresh world**

Fresh world is required — any existing world with the old Tortuga placement won't show the new behavior. Use `/wgen story rmsc tortuga` in an existing world to reset tracking if you must, but a new world is cleaner for this test.

- [ ] **Step 2: Check server log on player join**

Expected: no `FailedToGenerateLocation` warning naming `tortuga`. If present, the landform+radius combo couldn't find a valid spot. Fallbacks: widen `landformRadius` from 250 to 400 in storystructures.json, or broaden the spawn-distance band.

- [ ] **Step 3: Teleport to the placed structure**

Run: `/tpstoryloc tortuga`
Expected: teleport to the placed island. The full 201×196 footprint should be present. Walk the perimeter and check for block gaps / truncation. The `Tried to set block outside generating chunks` warning should NOT appear in the log during this session — that warning was the core symptom of the size issue and absence confirms the fix.

- [ ] **Step 4: Map activation from spawn**

Teleport back to spawn: `/tp ~-3000 ~ ~-3000` (or as far as possible). Give yourself a map: `/giveitem seafarer:map-tortuga 1`. Right-click the map.
Expected: "Approximate location of Tortuga added to your world map" — activates from anywhere (base `ItemLocatorMap` uses an unranged dictionary lookup for story structures).

- [ ] **Step 5: Regression — fishing/panning drops still work**

In creative, spawn saltwater or reef fish (`/entity spawn item-fish-saltwater-salmon-pink-adult`) and kill for ~30–40 drops, or pan sand repeatedly. Confirm `map-tortuga` occasionally drops at the configured rates (fishing ~1.5%, panning ~0.15%). This verifies the `class: "ItemLocatorMap"` change didn't break the item's appearance in drop tables.

- [ ] **Step 6: Regression — `map-crimsonrose` still works**

Give yourself `map-crimsonrose` in creative. Right-click it far from any wreck. Expected: "No location found on this map" (confirms `ItemOceanLocatorMap` is still loaded and functional). Teleport near a crimson-rose wreck and right-click again; expected: waypoint added (base 2000-block search range).

- [ ] **Step 7: Record result**

If all checks pass:

```bash
git commit --allow-empty -m "$(cat <<'EOF'
test(worldgen): manually verified Tortuga placement + map activation

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

If anything fails, note the failure mode (FailedToGenerateLocation, perimeter gaps, map unresponsive, regression in crimsonrose or drops) before iterating.

---

## Self-review checklist

- **Spec coverage:** Remove from oceanstructures (Task 1), add to storystructures (Task 2), switch map class (Task 3), manual verification (Task 4). All spec sections covered.
- **No placeholders:** Every step has complete, exact content. No "TBD" / "TODO" / "similar to Task N". Each JSON block is ready to copy verbatim.
- **Type consistency:** `"tortuga"` spelled the same in Task 2 (`code`, `schematiccode` in schematics path) and Task 3 (`locatorProps.schematiccode`). `"costal/tortuga"` schematic path matches the existing file on disk (not renamed, per spec). `storystructures.json` field names (`UseWorldgenHeight`, `ExcludeSchematicSizeProtect`) match base-game Pascal-case exactly.
