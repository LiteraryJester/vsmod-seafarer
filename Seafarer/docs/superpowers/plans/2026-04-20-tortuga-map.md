# Tortuga Map Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `seafarer:map-tortuga`, a "message in a bottle" item that marks the world-singleton Tortuga port hub on the player's waypoint map, obtainable via fishing saltwater/reef fish and panning sand/gravel.

**Architecture:** Reuse the existing `ItemOceanLocatorMap` class with one extension — read an optional `searchRange` attribute (default 350) so singleton structures can be located from anywhere in the world. Add a JSON item definition using the base-game `bottlemessage` shape. Two JSON patches add the bottle to fish drops and pan drops.

**Tech Stack:** C# / .NET 10.0, Vintage Story JSON asset format, VS JSON patching

**Spec:** `docs/superpowers/specs/2026-04-20-tortuga-map-design.md`

**Project note:** This is a Vintage Story mod with no automated test suite. Validation is `dotnet build` for compile-correctness, `python3 validate-assets.py` for asset integrity, and manual in-game verification.

**Build command (from repo root):** `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`

---

## File Map

| File | Responsibility | Change |
|---|---|---|
| `Seafarer/Seafarer/Item/ItemOceanLocatorMap.cs` | Map item behavior — add `searchRange` attribute support | Modify |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` | Tortuga map item definition | Create |
| `Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-fishing.json` | Append bottle drop to saltwater + reef fish | Create |
| `Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-panning.json` | Append bottle drop to pan (sand/gravel) | Create |
| `Seafarer/Seafarer/assets/seafarer/lang/en.json` | Item name, waypoint label, description | Modify |

---

### Task 1: Add `searchRange` attribute to `ItemOceanLocatorMap`

Extend the map item class to read an optional search range from item attributes, replacing the hardcoded 350. Default behavior (and existing map items) unchanged.

**Files:**
- Modify: `Seafarer/Seafarer/Item/ItemOceanLocatorMap.cs`

- [ ] **Step 1: Add `searchRange` field and load it in `OnLoaded`**

Open `Seafarer/Seafarer/Item/ItemOceanLocatorMap.cs`. Locate the private field block (lines 14–15):

```csharp
        private ModSystemStructureLocator strucLocSys;
        private LocatorProps props;
```

Replace with:

```csharp
        private ModSystemStructureLocator strucLocSys;
        private LocatorProps props;
        private int searchRange;
```

Then locate `OnLoaded` (lines 17–22). Replace the whole method with:

```csharp
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            strucLocSys = api.ModLoader.GetModSystem<ModSystemStructureLocator>();
            props = Attributes["locatorProps"].AsObject<LocatorProps>();
            searchRange = Attributes["searchRange"].AsInt(350);
        }
```

- [ ] **Step 2: Use `searchRange` instead of the hardcoded 350**

In the same file, locate line 56 (inside `OnHeldInteractStart`):

```csharp
                var loc = strucLocSys.FindFreshStructureLocation(props.SchematicCode, byEntity.Pos.AsBlockPos, 350);
```

Replace with:

```csharp
                var loc = strucLocSys.FindFreshStructureLocation(props.SchematicCode, byEntity.Pos.AsBlockPos, searchRange);
```

- [ ] **Step 3: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds, 0 errors (pre-existing warnings OK).

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/Item/ItemOceanLocatorMap.cs
git commit -m "$(cat <<'EOF'
feat(item): allow ItemOceanLocatorMap to override structure search range

Existing maps keep the 350-block default; new maps pointing at
singleton/rare structures can set searchRange in item attributes.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Create the `map-tortuga` item definition

Add the new item JSON using the base-game bottlemessage shape and pointing at the `tortuga` ocean structure code.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json`

- [ ] **Step 1: Create the item definition**

Create the file with this content:

```json5
{
    "code": "map-tortuga",
    "class": "ItemOceanLocatorMap",
    "maxstacksize": 1,
    "attributes": {
        "displaycaseable": true,
        "shelvable": true,
        "readable": true,
        "editable": false,
        "maxPageCount": 1,
        "searchRange": 10000,
        "locatorPropsbyType": {
            "*": {
                "schematiccode": "tortuga",
                "waypointtext": "location-tortuga",
                "waypointicon": "x",
                "waypointcolor": [0.95, 0.75, 0.2, 1],
                "randomX": 15,
                "randomZ": 15
            }
        }
    },
    "shape": { "base": "game:item/clutter/fishing/bottlemessage" },
    "creativeinventory": { "general": ["*"], "items": ["*"], "seafarer": ["*"] }
}
```

- [ ] **Step 2: Build to verify the asset is picked up**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json
git commit -m "$(cat <<'EOF'
feat(item): add map-tortuga message-in-a-bottle item

Uses ItemOceanLocatorMap with searchRange=10000 to locate the
world-singleton Tortuga port hub from anywhere in the world.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Add fishing-drop patch

Append the map to saltwater + reef fish drops at 1.5% chance. Freshwater fish excluded by thematic intent.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-fishing.json`

- [ ] **Step 1: Create the patch file**

Create the file with this content:

```json5
[
    {
        "op": "add",
        "path": "/drops/-",
        "file": "game:entity/animal/fish/saltwater-adult.json",
        "value": { "type": "item", "code": "seafarer:map-tortuga", "chance": { "avg": 0.015, "var": 0 } }
    },
    {
        "op": "add",
        "path": "/drops/-",
        "file": "game:entity/animal/fish/reef-adult.json",
        "value": { "type": "item", "code": "seafarer:map-tortuga", "chance": { "avg": 0.015, "var": 0 } }
    }
]
```

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-fishing.json
git commit -m "$(cat <<'EOF'
feat(patches): add map-tortuga as rare drop when fishing saltwater/reef fish

1.5% per-catch chance on saltwater-adult and reef-adult fish entities.
Freshwater fish excluded — thematically a bottle from the sea.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Add panning-drop patch

Append the map to the pan's `@(sand|gravel|sandwavy)-.*` drop pool at 0.15% chance.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-panning.json`

- [ ] **Step 1: Create the patch file**

Create the file with this content:

```json5
[
    {
        "op": "add",
        "path": "/attributes/panningDrops/@(sand|gravel|sandwavy)-.*/-",
        "file": "game:blocktypes/wood/pan.json",
        "value": { "type": "item", "code": "seafarer:map-tortuga", "chance": { "avg": 0.0015, "var": 0 }, "manMade": true }
    }
]
```

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/tortuga-map-panning.json
git commit -m "$(cat <<'EOF'
feat(patches): add map-tortuga as rare panning drop on sand/gravel

0.15% chance per pan operation on sand/gravel/sandwavy surfaces.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Add lang entries

Item name, waypoint label, description.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json`

- [ ] **Step 1: Locate the crimson-rose lang entry**

Open `Seafarer/Seafarer/assets/seafarer/lang/en.json`. Search for `item-map-crimsonrose`. You'll find:

```json
	"item-map-crimsonrose": "Map to the Crimson Rose",
```

- [ ] **Step 2: Add the Tortuga lang entries adjacent to the crimsonrose line**

Immediately after the `"item-map-crimsonrose": ...` line, add:

```json
	"item-map-tortuga": "Message in a Bottle — Tortuga",
	"location-tortuga": "Tortuga",
	"seafarer:itemdesc-map-tortuga": "A weathered scroll tucked inside a sea-tumbled bottle, its ink speaking of a far-off port called Tortuga.",
```

(Ensure a trailing comma on the last line if more entries follow in the file.)

- [ ] **Step 3: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Run asset validator**

Run: `python3 validate-assets.py 2>&1 | tail -10`
Expected: Same result as baseline (1 pre-existing `premiumfish` error; 0 new errors). If a new error relating to `map-tortuga` appears, fix it before committing.

- [ ] **Step 5: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/lang/en.json
git commit -m "$(cat <<'EOF'
feat(lang): add en strings for map-tortuga + location-tortuga waypoint

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Manual in-game verification

Checklist for the user to run. No automation available.

**Files:** None modified.

- [ ] **Step 1: Launch the game with the mod and create or load a world with Tortuga already generated**

If you already verified Tortuga worldgen in a prior session, use that same world.

- [ ] **Step 2: Verify the item exists in creative inventory**

Open creative inventory, filter to the seafarer tab. Expected: `Message in a Bottle — Tortuga` is listed; icon renders as a bottle with a scroll inside.

- [ ] **Step 3: Open an unused bottle (long-range locator)**

With the bottle in hand, right-click (use item). Expected: chat notification "Approximate location of Tortuga added to your world map." Open the map (M) — a waypoint marker icon `x` in amber/gold appears within ~15 blocks of Tortuga.

- [ ] **Step 4: Open an already-used bottle**

With the same bottle still in hand, right-click again. Expected: "Location already marked on your map."

- [ ] **Step 5: Verify `map-crimsonrose` unchanged**

Give yourself a `map-crimsonrose` from creative. Right-click in a biome far from any shipwreck. Expected: "No location found on this map." (Confirms the default-350 search range still applies to unmodified maps.)

- [ ] **Step 6: Spot-check fishing drop**

In creative, spawn saltwater fish entities (or use a fishing pole in a saltwater biome) and kill/catch ~30–40. Expected: at least one `map-tortuga` drops alongside the fishraw items. If zero drops after 100+ catches, inspect the patch file.

- [ ] **Step 7: Spot-check panning drop**

Use a pan on wet sand/gravel for ~5 minutes of repeated panning. Expected: at least one `map-tortuga` drops eventually (target rate is 0.15%, so this is a low-volume test). Skipping is acceptable if time-constrained — the patch path being correct is verified by asset validator loading without errors.

- [ ] **Step 8: Record result**

If all checks pass:

```bash
git commit --allow-empty -m "$(cat <<'EOF'
test(map): manually verified map-tortuga locator, fishing drop, panning patch

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

If any check fails, note the failure mode and iterate before claiming done.

---

## Self-review checklist

- **Spec coverage:** Item definition (Task 2), search-range code change (Task 1), fishing patch (Task 3), panning patch (Task 4), lang entries (Task 5), manual verification (Task 6). All spec sections covered.
- **No placeholders:** Every code block is complete; no "TBD" / "TODO" / "implement later" / "similar to Task N". Transform blocks intentionally omitted (spec notes VS defaults apply, tune later if needed).
- **Type consistency:** `searchRange` field spelled identically in Task 1 steps 1 and 2. Item code `seafarer:map-tortuga` spelled identically in Tasks 2, 3, 4, 5. Lang key `location-tortuga` in both Task 2 (waypointtext reference) and Task 5. `schematiccode: "tortuga"` matches the ocean structure code added in the prior Tortuga worldgen feature.
