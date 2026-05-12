# Unseasoned plank intermediate step — implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Insert a saw-based intermediate step between fresh wood planks and seasoned planks. After the change, `plank-{wood}` no longer auto-dries; the player must process it through a saw to obtain `plank-unseasoned`, which then dries to `plank-seasoned` over 168 in-game hours.

**Architecture:** Pure JSON asset change to the Seafarer mod. Mirrors the existing `plank-seasoned`/`plank-varnished` model — `unseasoned` is added as a single generic state on the plank *item* variant group only. No new C# code, no new block forms, no migration of existing saves.

**Tech Stack:** Vintage Story 1.21.0+ asset JSON5, JSON-patch ops in `assets/seafarer/patches/`, grid recipe in `assets/seafarer/recipes/grid/`, lang entries in `assets/seafarer/lang/en.json`. Validation via `validate-assets.py`.

**Reference spec:** `Seafarer/docs/superpowers/specs/2026-05-12-unseasoned-plank-step-design.md`.

**Working directory for commands:** `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/` (the inner project dir that contains `validate-assets.py`, `assets/`, and `Seafarer.csproj`). All `python3 validate-assets.py` and `dotnet build` invocations below assume that cwd.

---

### Task 1: Add `unseasoned` variant + texture to plank item

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/patches/plank-variants.json`

This adds the variant identity. After this task, `game:plank-unseasoned` is a real item code with a texture, but there is no way to obtain it in-game yet (no recipe), and it won't dry yet (still using the old `*` Dry transition which won't match).

- [ ] **Step 1: Verify the current patch structure**

Open `Seafarer/Seafarer/assets/seafarer/patches/plank-variants.json`. The first three patches target `game:itemtypes/resource/plank.json`:
1. Adds `seasoned` to variantgroups/0/states
2. Adds `varnished` to variantgroups/0/states
3. Replaces `/textures/wood/baseByType` with a six-entry map

Confirm those three patches exist as the first entries before continuing.

- [ ] **Step 2: Insert the `unseasoned` state add op**

Add a third state-add op directly after the `varnished` one (before the `replace` op for textures). The new entry should match the existing style exactly:

```json
{ "op": "add", "path": "/variantgroups/0/states/-", "value": "unseasoned",
    "file": "game:itemtypes/resource/plank.json" },
```

- [ ] **Step 3: Add `*-unseasoned` to the textures map**

In the same file, find the `replace` op on `/textures/wood/baseByType`. Add a new entry `*-unseasoned` to the value object, placed *before* the final `*` catch-all (wildcards are first-match). Use the vanilla oak1 texture as the placeholder per the spec:

```json
"value": {
    "*-seasoned": "seafarer:block/wood/planks/seasoned1",
    "*-varnished": "seafarer:block/wood/planks/varnished1",
    "*-aged": "block/wood/planks/aged/aged1",
    "*-veryaged": "block/wood/planks/aged/veryaged1",
    "*-agedebony": "block/wood/planks/aged/ebony1",
    "*-unseasoned": "block/wood/planks/oak1",
    "*": "block/wood/planks/{wood}1"
},
```

(The only line added is `"*-unseasoned": "block/wood/planks/oak1",`.)

- [ ] **Step 4: Run asset validator**

Run: `python3 validate-assets.py`
Expected: exits 0 with 0 errors. Warnings unrelated to the plank patches are acceptable.

- [ ] **Step 5: Run a quick build to confirm assets parse**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj`
Expected: `Build succeeded` with no errors. (Asset JSON errors do not break the build but may show as warnings on first game launch — the validator is the primary check here.)

- [ ] **Step 6: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/patches/plank-variants.json
git commit -m "add unseasoned variant to plank item"
```

---

### Task 2: Add lang entry for `plank-unseasoned`

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json`

Lang must exist so the handbook and tooltip display a sensible name. After this task, the item shows as "Unseasoned plank" instead of a missing-translation placeholder.

- [ ] **Step 1: Locate the existing plank lang entries**

In `Seafarer/Seafarer/assets/seafarer/lang/en.json`, search for `"item-plank-seasoned"`. It lives around line 795 alongside `item-plank-varnished` and their `itemdesc-*` companions.

- [ ] **Step 2: Add the new entries**

Insert two new lines directly after the existing `itemdesc-plank-varnished` entry, matching the surrounding indentation (tab):

```json
"item-plank-unseasoned": "Unseasoned plank",
"itemdesc-plank-unseasoned": "Freshly sawn lumber. Cannot be used for anything yet — leave it in storage to air-dry into seasoned planks.",
```

Make sure the line *before* your insertion ends with a comma. The original block ends with `itemdesc-plank-varnished` followed by other entries, so a comma is already present — but verify.

- [ ] **Step 3: Run asset validator**

Run: `python3 validate-assets.py`
Expected: exits 0 with 0 errors. The validator checks lang well-formedness and may also check that every mod item code has a matching lang key.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/lang/en.json
git commit -m "add lang entries for plank-unseasoned"
```

---

### Task 3: Move dry transition from `*` to `*-unseasoned`

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/patches/plank-seasoning.json`

This is the behavior change. After this commit, plain `plank-{wood}` no longer auto-dries — but `plank-unseasoned` does. Since Task 4 hasn't added the saw recipe yet, the pipeline will be temporarily broken at the end of this commit (no way to obtain unseasoned planks). That's expected and gets resolved in Task 4.

- [ ] **Step 1: Replace the entire `value` of the `add` op**

Rewrite `plank-seasoning.json` so the patch body looks like this. Comments at the top stay (helpful) but should be updated to describe the new flow:

```json
[
    // Add drying transition to unseasoned planks only.
    // plank-{wood} (raw, fresh-cut) no longer auto-dries — the player must
    // saw it into plank-unseasoned first (see recipes/grid/plank-unseasoned.json).
    // plank-unseasoned then air-dries into plank-seasoned over ~1 in-game week (168 hours).
    // All other plank states (seasoned, varnished, aged, veryaged) carry no transition.
    // VS ByType matching picks the first match, so *-unseasoned must come before *.
    {
        "op": "add",
        "path": "/transitionablePropsByType",
        "value": {
            "*-unseasoned": [{
                "type": "Dry",
                "freshHours": { "avg": 0 },
                "transitionHours": { "avg": 168 },
                "transitionedStack": { "type": "item", "code": "game:plank-seasoned" },
                "transitionRatio": 1
            }],
            "*": []
        },
        "file": "game:itemtypes/resource/plank.json"
    }
]
```

Key changes from the previous version:
- The `*-seasoned`, `*-varnished`, `*-aged`, `*-veryaged` keys are gone (they collapse into the final `*: []`).
- The Dry-transition block moved from `*` to `*-unseasoned`.
- `*: []` is now the catch-all (no transitions for any non-unseasoned plank).

- [ ] **Step 2: Run asset validator**

Run: `python3 validate-assets.py`
Expected: exits 0 with 0 errors.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/patches/plank-seasoning.json
git commit -m "move plank dry transition to plank-unseasoned only"
```

---

### Task 4: Add saw recipe `plank → plank-unseasoned`

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/recipes/grid/plank-unseasoned.json`

This closes the pipeline: `plank-{wood}` + saw → `plank-unseasoned` (which dries via Task 3).

- [ ] **Step 1: Create the recipe file**

Create `Seafarer/Seafarer/assets/seafarer/recipes/grid/plank-unseasoned.json` with this exact content:

```json
[
    {
        ingredientPattern: "SP",
        ingredients: {
            "S": { type: "item", tags: ["tool-saw"], isTool: true },
            "P": {
                type: "item",
                code: "game:plank-*",
                name: "wood",
                skipVariants: ["seasoned", "varnished", "unseasoned"]
            }
        },
        width: 2,
        height: 1,
        shapeless: true,
        output: { type: "item", code: "game:plank-unseasoned", quantity: 1 }
    }
]
```

JSON5 style (unquoted keys, trailing commas where convenient) matches the convention used in `recipes/grid/supportbeam-seasoned.json` already in the mod. `skipVariants` blocks already-processed states from being recycled through the saw.

- [ ] **Step 2: Run asset validator**

Run: `python3 validate-assets.py`
Expected: exits 0 with 0 errors. The validator checks recipe input/output codes resolve.

- [ ] **Step 3: Build the mod**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/recipes/grid/plank-unseasoned.json
git commit -m "add saw recipe: plank-{wood} -> plank-unseasoned"
```

---

### Task 5: In-game smoke test

**Files:** none (verification only).

This task is manual. The agentic worker should report each check to the user; the user runs the game and confirms. Do not mark this task complete until all five checks pass.

- [ ] **Step 1: Launch Vintage Story with the mod**

User launches the game with the Seafarer mod loaded (dev `--addOrigin` setup per repo CLAUDE.md). Use creative mode for ease of testing.

- [ ] **Step 2: Verify raw plank no longer shows drying**

Spawn `game:plank-oak` (any wood) into inventory. Hover the stack and confirm the tooltip does **not** show a transition timer ("dries in …"). Open the handbook entry for the plank and confirm no Dry transition is listed.
Expected: no drying behavior on raw planks.

- [ ] **Step 3: Verify the saw recipe appears and works**

Open the handbook, search "unseasoned plank". Expected: the `plank-unseasoned` page shows a grid recipe with saw + any wood plank → 1 unseasoned plank. In creative mode, take a saw (e.g., `tool-saw-copper`) and a stack of plank-oak. Place them in a 2-slot crafting layout (shapeless). Expected: output is `plank-unseasoned`, saw durability dropped by 1.

- [ ] **Step 4: Verify unseasoned plank dries**

With the freshly-crafted unseasoned plank in inventory, hover and confirm the tooltip shows a transition timer counting down to seasoned over ~168 in-game hours. Use a time-skip command (`/time set` or wait) to accelerate. Expected: after 168 in-game hours the stack converts to `plank-seasoned`.

- [ ] **Step 5: Verify unseasoned plank is a dead-end input**

In the handbook, view the `plank-unseasoned` page and check the "Used in" / recipe references. Expected: no recipes accept `plank-unseasoned` as an ingredient. Confirm specifically that the existing supportbeam-seasoned and varnished-plank recipes still require `plank-seasoned` and are unaffected.

- [ ] **Step 6: Mark plan complete**

Once all five smoke checks pass, the implementation is verified. No final commit needed for this task (it is verification only). The user should run `python3 validate-assets.py` one final time on the full repo to confirm no validator regressions.

---

## Notes for the implementer

- **Do not edit anything under `Seafarer/Releases/`.** That directory mirrors source assets but appears to be a packaged copy. Source-of-truth is `Seafarer/Seafarer/assets/`. If a build step regenerates `Releases/`, that happens through the Cake build, not by hand.
- **Do not add `unseasoned` to any plank-derived block file.** The spec is explicit: no block forms. If a future task asks for placeable unseasoned planks, that is a separate feature.
- **Wildcard ordering matters in JSON `ByType` maps.** VS reads keys in document order and uses first-match. The `*-unseasoned` key must come *before* `*`. Same applies inside the textures map.
- If `validate-assets.py` reports `unknown item code game:plank-unseasoned` at any point, it means the variant patch (Task 1) hasn't applied — re-check the patch file for syntax errors and confirm the new state was added to `variantgroups/0/states`.
