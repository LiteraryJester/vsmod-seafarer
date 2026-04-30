# Potato King's House Worldgen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Generate the Potato King's House (27×23×29 surface schematic) as a world-singleton landmark within ±2500 blocks of spawn, and wire the existing `map-potato` item to locate it.

**Architecture:** Piggyback on base-game `GenStoryStructures`. Create `seafarer:worldgen/storystructures.json` — the game's asset loader merges all `worldgen/storystructures.json` files across domains. Point the existing `map-potato` item's `schematiccode` at `"potato-king-house"` so the structure-locator matcher (which splits the stored `GeneratedStructure.Code` on `/` and checks `parts[1]`) finds it.

**Tech Stack:** VS JSON5 asset format, base-game `GenStoryStructures` system

**Spec:** `docs/superpowers/specs/2026-04-20-potato-king-worldgen-design.md`

**Project note:** Vintage Story mod, no automated test suite. Verification is `dotnet build` (asset copy), `python3 validate-assets.py`, and manual in-game check.

**Build command (from repo root):** `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`

---

## File Map

| File | Responsibility | Change |
|---|---|---|
| `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` | Register the Potato King's House as a story structure (singleton, landform-filtered) | Create |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json` | Update `schematiccode` so the existing map points at the new structure | Modify |

---

### Task 1: Create the story-structures config

Add one entry that registers `potato-king-house` with spawn-distance and landform filters.

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json`

- [ ] **Step 1: Create the config file**

Write this exact content to `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json`:

```json5
{
    "schematicYOffsets": {
        "surface/potato-king-house": -1
    },
    "structures": [
        {
            "code": "potatoking",
            "group": "storystructure",
            "name": "Potato King's House",
            "schematics": ["surface/potato-king-house"],
            "placement": "surface",
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
        }
    ]
}
```

- [ ] **Step 2: Build to verify the asset is picked up**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds, 0 errors (pre-existing warnings OK).

- [ ] **Step 3: Run asset validator**

Run: `python3 validate-assets.py 2>&1 | tail -6`
Expected: Same baseline result as prior tasks (1 pre-existing `premiumfish` error, 0 new errors related to `storystructures.json` or `potato-king-house`). If any new error references our file, fix before committing.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json
git commit -m "$(cat <<'EOF'
feat(worldgen): register Potato King's House as singleton story structure

Generates within +/-2500 blocks of world spawn on veryflat landforms,
alongside Tortuga's 500-3000 radial band. Base-game GenStoryStructures
handles singleton tracking and save-load persistence.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Point `map-potato` at the Potato King's House

Change a single field so the existing map (given by Celeste's quest dialogue) resolves to the new structure.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json`

- [ ] **Step 1: Replace the `schematiccode` value**

Open `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json`. Locate the line:

```json
                "schematiccode": "buriedtreasurechest",
```

Replace it with:

```json
                "schematiccode": "potato-king-house",
```

The change is exactly one line. The rest of the file (waypointtext, icon, color, class, transforms) stays identical.

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Run asset validator**

Run: `python3 validate-assets.py 2>&1 | tail -6`
Expected: Baseline (1 pre-existing error, 0 new).

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-potato.json
git commit -m "$(cat <<'EOF'
fix(item): point map-potato at potato-king-house structure

The map previously referenced 'buriedtreasurechest' which is not
generated by any Seafarer worldgen, so the item never resolved to
a waypoint. Now matches the story-structure code registered in
storystructures.json.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Manual in-game verification

No automation available. Checklist for the user to run.

**Files:** None modified.

- [ ] **Step 1: Launch the game with the mod and create a new world**

Start fresh so the story-structure placement runs.

- [ ] **Step 2: Check the server log at world init for placement outcome**

Expected on success: no `FailedToGenerateLocation` warning for `potatoking` when a privileged player joins. If that warning appears, the ±2500 band + `veryflat` landform filter didn't match any terrain near spawn. Widen the band or fall back to `flat` landform as noted in the spec's Known Tensions section.

- [ ] **Step 3: Teleport to the placed structure**

In chat, run: `/tpstoryloc potatoking`
Expected: teleport to the Potato King's House. Verify the structure is the expected 27×23×29 schematic (not a base-game story structure with a similar code).

If the command is unknown, try `/wgen storystruc tp potatoking` or check `/help | grep story` for the current command name in 1.22.

- [ ] **Step 4: Use `map-potato` within 300 blocks of the house**

Give yourself `map-potato` from creative: `/giveitem seafarer:map-potato 1`.
Right-click the map. Expected: chat message "Approximate location of Potato King's Map added to your world map"; the map (M key) shows the waypoint icon within ~15 blocks of the house.

- [ ] **Step 5: Regression — use `map-potato` far from the house**

Teleport to `~+2000 ~ ~` (relative X move of 2000 blocks). Right-click the map. Expected: "No location found on this map" (the hardcoded 350-block search range in base-game `ItemLocatorMap` is out of range). This confirms we did not accidentally change the base-game class.

- [ ] **Step 6: Regression — Celeste dialogue still gifts `map-potato`**

Step through Celeste's existing quest path that delivers the map. Confirm the inventory item is still `seafarer:map-potato`. (No dialogue changes were made, so this should pass trivially.)

- [ ] **Step 7: Record result**

If all checks pass:

```bash
git commit --allow-empty -m "$(cat <<'EOF'
test(worldgen): manually verified potato-king-house placement + map wiring

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

If anything fails, note the failure mode (placement failure, wrong landform, map still says "no location found" near the house, etc.) before iterating.

---

## Self-review checklist

- **Spec coverage:** storystructures.json config (Task 1), map-potato schematiccode update (Task 2), manual verification including placement + map activation + regression checks (Task 3). All spec sections covered. No gaps.
- **No placeholders:** Every step has complete, exact content. No "TBD" / "TODO" / "implement later" / "similar to Task N". Each code block is ready to copy.
- **Type consistency:** `code: "potatoking"` spelled the same in Task 1 JSON and Task 3 `/tpstoryloc` command. `schematiccode: "potato-king-house"` spelled the same in Task 2 and matched by the spec's explanation (parts[1] after split on `/`). Schematic path `"surface/potato-king-house"` spelled the same in Task 1's `schematics` array and `schematicYOffsets` dictionary key.
