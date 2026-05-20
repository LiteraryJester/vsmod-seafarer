# Long-Term Food Compatibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When the Long-Term Food mod is installed, make Seafarer cede corn/potato/sugarcane to it, with all of Seafarer's food chains sourced from LTF crops.

**Architecture:** Five JSON patches under `assets/seafarer/patches/`, every entry gated by `dependsOn` so they are inert unless `longtermfood` is installed. Four new files redirect crops/recipes; one existing file is edited to invert-gate Seafarer's wild-potato worldgen. No C# changes.

**Tech Stack:** Vintage Story JSON5 mod patches. LTF modid is `longtermfood`; its asset domain is `pemmican:`.

**Testing note:** These are data-only mod patches — there is no unit-test harness. Each task verifies by (a) parsing the file as JSON5, and (b) running the project asset validator `python3 validate-assets.py`, which must report `Errors: 0`. The design spec is `docs/superpowers/specs/2026-05-16-longtermfood-compat-design.md`.

**Working directory for all commands:** repo root `/mnt/d/Development/vs/vsmod-seafarer`.

---

## File Structure

- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-disable.json` — disables Seafarer's corn/potato/sugarcane crop blocks, hides their food items, switches off wild corn/sugarcane worldgen.
- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-corn.json` — routes LTF corn into Seafarer's corn chain (drying → `driedcorn`, burrito filling).
- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-sugarcane.json` — makes LTF sugarcane juiceable into `seafarer:canejuiceportion`.
- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-recipes.json` — adds LTF corn/potato to cooking-pot and ExpandedFoods ingredient lists.
- Modify: `Seafarer/Seafarer/assets/seafarer/patches/worldgen-blockpatches-potato.json` — invert-gate so Seafarer's wild-potato worldgen runs only when LTF is absent.

---

## Task 1: Disable Seafarer's crops when LTF is present

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-disable.json`

- [ ] **Step 1: Create the patch file**

Create `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-disable.json` with exactly this content:

```json
[
    // ==================================================================
    // Long-Term Food compat. When `longtermfood` is installed, Seafarer
    // cedes corn/potato/sugarcane to it: disable Seafarer's own crop
    // blocks, hide the raw food items from creative + handbook, and switch
    // off Seafarer's wild corn/sugarcane worldgen. LTF's own crops and
    // worldgen take over. Every entry is dependsOn `longtermfood`, so this
    // whole file is inert when LTF is absent.
    // ==================================================================

    // --- Disable Seafarer's crop blocks ---
    { "op": "add", "path": "/enabled", "value": false,
        "file": "seafarer:blocktypes/plant/crop/corn.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "add", "path": "/enabled", "value": false,
        "file": "seafarer:blocktypes/plant/crop/potato.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "add", "path": "/enabled", "value": false,
        "file": "seafarer:blocktypes/plant/crop/sugarcane.json",
        "dependsOn": [{ "modid": "longtermfood" }] },

    // --- Hide the raw food items from creative menu + handbook ---
    { "op": "add", "path": "/creativeinventory", "value": {},
        "file": "seafarer:itemtypes/food/corn.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/attributes", "value": { "handbook": { "exclude": true } },
        "file": "seafarer:itemtypes/food/corn.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "add", "path": "/creativeinventory", "value": {},
        "file": "seafarer:itemtypes/food/potato.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/attributes", "value": { "handbook": { "exclude": true } },
        "file": "seafarer:itemtypes/food/potato.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "add", "path": "/creativeinventory", "value": {},
        "file": "seafarer:itemtypes/food/sugarcane.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/attributes", "value": { "handbook": { "exclude": true } },
        "file": "seafarer:itemtypes/food/sugarcane.json",
        "dependsOn": [{ "modid": "longtermfood" }] },

    // --- Switch off wild corn (index 2) + sugarcane (index 3) worldgen ---
    { "op": "replace", "path": "/2/chance", "value": 0,
        "file": "seafarer:worldgen/blockpatches/crops.json",
        "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "replace", "path": "/3/chance", "value": 0,
        "file": "seafarer:worldgen/blockpatches/crops.json",
        "dependsOn": [{ "modid": "longtermfood" }] }
]
```

Note: `seafarer:worldgen/blockpatches/crops.json` index 2 is the wild-corn entry and index 3 is the wild-sugarcane entry — verify this ordering still holds by opening that file before committing (the file currently lists chili=0, tomato=1, corn=2, sugarcane=3).

- [ ] **Step 2: Verify the file parses as JSON5**

Run: `python3 -c "import json5; json5.load(open('Seafarer/Seafarer/assets/seafarer/patches/longtermfood-disable.json')); print('OK')"`
Expected: `OK`

- [ ] **Step 3: Run the asset validator**

Run: `python3 validate-assets.py`
Expected: final summary shows `Errors: 0` (warnings about pre-existing potato/sugarcane crop *shapes* are unrelated and acceptable).

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/longtermfood-disable.json
git commit -m "feat: disable Seafarer corn/potato/sugarcane when Long-Term Food installed

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Route LTF corn into Seafarer's corn chain

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-corn.json`

- [ ] **Step 1: Create the patch file**

Create `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-corn.json` with exactly this content:

```json
[
    // ==================================================================
    // Long-Term Food compat. Routes LTF corn (pemmican:vegetable-corn-*)
    // into Seafarer's corn chain: a Dry transition produces
    // seafarer:driedcorn, which already feeds nixtamal -> masa ->
    // tortilladough -> tortilla and the cornbeer barrel recipe. Raw corn
    // is also made usable as a burrito filling.
    // ==================================================================
    {
        "file": "pemmican:itemtypes/crops/vegetable.json",
        "op": "addmerge",
        "path": "/transitionablePropsByType",
        "value": {
            "*-corn-raw": [
                {
                    "type": "Dry",
                    "freshHours": { "avg": 72 },
                    "transitionHours": { "avg": 48 },
                    "transitionedStack": { "type": "item", "code": "seafarer:driedcorn" },
                    "transitionRatio": 1
                },
                {
                    "type": "Perish",
                    "freshHours": { "avg": 2160 },
                    "transitionHours": { "avg": 96 },
                    "transitionedStack": { "type": "item", "code": "game:rot" },
                    "transitionRatio": 0.5
                }
            ]
        },
        "dependsOn": [{ "modid": "longtermfood" }]
    },
    {
        "file": "pemmican:itemtypes/crops/vegetable.json",
        "op": "addmerge",
        "path": "/attributesByType/*-corn-raw",
        "value": { "inBurritoProperties": { "partType": "Filling" } },
        "dependsOn": [{ "modid": "longtermfood" }]
    },
    {
        "file": "pemmican:itemtypes/crops/vegetable.json",
        "op": "addmerge",
        "path": "/attributesByType/vegetable-corn-charred",
        "value": { "inBurritoProperties": { "partType": "Filling" } },
        "dependsOn": [{ "modid": "longtermfood" }]
    }
]
```

- [ ] **Step 2: Verify the file parses as JSON5**

Run: `python3 -c "import json5; json5.load(open('Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-corn.json')); print('OK')"`
Expected: `OK`

- [ ] **Step 3: Run the asset validator**

Run: `python3 validate-assets.py`
Expected: `Errors: 0`.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-corn.json
git commit -m "feat: route Long-Term Food corn into Seafarer's corn chain

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Make LTF sugarcane juiceable

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-sugarcane.json`

- [ ] **Step 1: Verify the target file and harvest form**

Open `existing mods/Long-term_food_v0.6.3/assets/pemmican/blocktypes/plant/crop/sugarcane.json` and confirm: the file exists, and that the block (or its harvested/dropped form) is what a player would put in a fruit press. The reference mod `seafarerxltf` places `juiceableProperties` on this crop block; this task mirrors that. If the block clearly is not the juiced form (e.g. it drops a separate item), stop and flag for design review before proceeding.

- [ ] **Step 2: Create the patch file**

Create `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-sugarcane.json` with exactly this content:

```json
[
    // ==================================================================
    // Long-Term Food compat. Makes LTF sugarcane juiceable into Seafarer's
    // cane juice, which already feeds the molasses and sugar chains.
    // Mirrors seafarerxltf's pemmican-sugarcane-juicing.json.
    // ==================================================================
    {
        "file": "pemmican:blocktypes/plant/crop/sugarcane.json",
        "op": "addmerge",
        "path": "/attributes",
        "value": {
            "juiceableProperties": {
                "litresPerItem": 0.15625,
                "liquidStack": { "type": "item", "code": "seafarer:canejuiceportion", "stacksize": 1 },
                "pressedStack": { "type": "item", "code": "seafarer:canemush", "stacksize": 1 }
            }
        },
        "dependsOn": [{ "modid": "longtermfood" }]
    }
]
```

- [ ] **Step 3: Verify the file parses as JSON5**

Run: `python3 -c "import json5; json5.load(open('Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-sugarcane.json')); print('OK')"`
Expected: `OK`

- [ ] **Step 4: Run the asset validator**

Run: `python3 validate-assets.py`
Expected: `Errors: 0`.

- [ ] **Step 5: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/longtermfood-crops-sugarcane.json
git commit -m "feat: make Long-Term Food sugarcane juiceable into Seafarer cane juice

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Add LTF corn/potato to cooking recipes

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-recipes.json`

Context: Seafarer's `cooking-coconut.json` adds `seafarer:corn` and `cooking-potato.json` adds `seafarer:potato` to the same vanilla soup/stew validStacks slots. `expandedfoods-tropical.json` adds them to ExpandedFoods filling slots. This task adds the LTF equivalents (`pemmican:vegetable-corn-raw`, `pemmican:vegetable-potato-raw`) to all those same slots. The cooking-pot entries carry the same `shapeElement` strings as the existing entries.

- [ ] **Step 1: Create the patch file**

Create `Seafarer/Seafarer/assets/seafarer/patches/longtermfood-recipes.json` with exactly this content:

```json
[
    // ==================================================================
    // Long-Term Food compat. Adds LTF corn/potato to every additive
    // ingredient list where Seafarer adds seafarer:corn / seafarer:potato:
    // vanilla soup/vegetablestew/meatystew, and (when ExpandedFoods is also
    // present) EF stuffed-pepper and dumpling filling slots.
    // ==================================================================

    // --- Vanilla soup: vegetable-base slots 1-3 ---
    { "op": "addmerge", "path": "/ingredients/1/validStacks/-", "file": "game:recipes/cooking/soup.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable base 1/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/1/validStacks/-", "file": "game:recipes/cooking/soup.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable base 1/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/2/validStacks/-", "file": "game:recipes/cooking/soup.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable base 2/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/2/validStacks/-", "file": "game:recipes/cooking/soup.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable base 2/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/3/validStacks/-", "file": "game:recipes/cooking/soup.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable base 3/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/3/validStacks/-", "file": "game:recipes/cooking/soup.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable base 3/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },

    // --- Vanilla vegetable stew: vegetable slots 0-3 ---
    { "op": "addmerge", "path": "/ingredients/0/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable 1/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/0/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable 1/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/1/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable 2/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/1/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable 2/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/2/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable 3/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/2/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable 3/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/3/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable 4/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/3/validStacks/-", "file": "game:recipes/cooking/vegetablestew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable 4/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },

    // --- Vanilla meaty stew: vegetable slots 5-6 ---
    { "op": "addmerge", "path": "/ingredients/5/validStacks/-", "file": "game:recipes/cooking/meatystew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable 1/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/5/validStacks/-", "file": "game:recipes/cooking/meatystew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable 1/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/6/validStacks/-", "file": "game:recipes/cooking/meatystew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-corn-raw", "shapeElement": "bowl/vegetable 2/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },
    { "op": "addmerge", "path": "/ingredients/6/validStacks/-", "file": "game:recipes/cooking/meatystew.json",
      "value": { "type": "item", "code": "pemmican:vegetable-potato-raw", "shapeElement": "bowl/vegetable 2/*" },
      "dependsOn": [{ "modid": "longtermfood" }] },

    // --- ExpandedFoods stuffed pepper: corn into filling slots 0/3/5/8 ---
    { "op": "addeach", "side": "server", "path": "/0/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-corn-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },
    { "op": "addeach", "side": "server", "path": "/3/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-corn-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },
    { "op": "addeach", "side": "server", "path": "/5/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-corn-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },
    { "op": "addeach", "side": "server", "path": "/8/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-corn-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },

    // --- ExpandedFoods stuffed pepper: potato into filling slots 0/3/5/8 ---
    { "op": "addeach", "side": "server", "path": "/0/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-potato-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },
    { "op": "addeach", "side": "server", "path": "/3/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-potato-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },
    { "op": "addeach", "side": "server", "path": "/5/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-potato-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },
    { "op": "addeach", "side": "server", "path": "/8/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/stuffedpepper.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-potato-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },

    // --- ExpandedFoods dumpling: potato into filling slots 1/3 ---
    { "op": "addeach", "side": "server", "path": "/1/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/dumpling.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-potato-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] },
    { "op": "addeach", "side": "server", "path": "/3/ingredients/1/inputs/-", "file": "expandedfoods:recipes/kneading/dumpling.json",
      "value": [ { "type": "item", "code": "pemmican:vegetable-potato-raw", "quantity": 1 } ],
      "dependsOn": [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }] }
]
```

- [ ] **Step 2: Verify the file parses as JSON5**

Run: `python3 -c "import json5; json5.load(open('Seafarer/Seafarer/assets/seafarer/patches/longtermfood-recipes.json')); print('OK')"`
Expected: `OK`

- [ ] **Step 3: Cross-check paths against the existing patches**

Run: `python3 -c "import json5; d=json5.load(open('Seafarer/Seafarer/assets/seafarer/patches/expandedfoods-tropical.json')); [print(p['file'].split('/')[-1], p['path']) for p in d if 'corn' in str(p.get('value','')) or 'potato' in str(p.get('value',''))]"`
Expected: the printed `file`/`path` pairs match the EF entries written above (stuffedpepper 0/3/5/8 and dumpling 1/3). If they differ, correct `longtermfood-recipes.json` to match.

- [ ] **Step 4: Run the asset validator**

Run: `python3 validate-assets.py`
Expected: `Errors: 0`.

- [ ] **Step 5: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/longtermfood-recipes.json
git commit -m "feat: add Long-Term Food corn/potato to Seafarer cooking recipes

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Invert-gate Seafarer's wild-potato worldgen

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/patches/worldgen-blockpatches-potato.json`

Seafarer's wild-potato worldgen appends a blockpatch referencing `seafarer:crop-potato-*`, which Task 1 disables when LTF is present. Add an inverted `dependsOn` so this worldgen applies only when `longtermfood` is **absent**.

- [ ] **Step 1: Edit the file**

The file currently is:

```json
[
	{
		"file": "game:worldgen/blockpatches/crop.json",
		"op": "addmerge",
		"path": "/-",
		"value": {
			"comment": "Small patches of wild potatoes",
			"blockCodes": [
				"seafarer:crop-potato-2",
				"seafarer:crop-potato-4",
				"seafarer:crop-potato-6"
			],
			"minTemp": 0,
			"maxTemp": 35,
			"minRain": 0.35,
			"maxRain": 0.75,
			"maxForest": 0.5,
			"quantity": { "avg": 5, "var": 5 },
			"chance": 0.025
		}
	}
]
```

Add a `dependsOn` line so the single object becomes:

```json
[
	{
		"file": "game:worldgen/blockpatches/crop.json",
		"op": "addmerge",
		"path": "/-",
		"dependsOn": [{ "modid": "longtermfood", "invert": true }],
		"value": {
			"comment": "Small patches of wild potatoes",
			"blockCodes": [
				"seafarer:crop-potato-2",
				"seafarer:crop-potato-4",
				"seafarer:crop-potato-6"
			],
			"minTemp": 0,
			"maxTemp": 35,
			"minRain": 0.35,
			"maxRain": 0.75,
			"maxForest": 0.5,
			"quantity": { "avg": 5, "var": 5 },
			"chance": 0.025
		}
	}
]
```

- [ ] **Step 2: Verify the file parses as JSON5**

Run: `python3 -c "import json5; json5.load(open('Seafarer/Seafarer/assets/seafarer/patches/worldgen-blockpatches-potato.json')); print('OK')"`
Expected: `OK`

- [ ] **Step 3: Run the asset validator**

Run: `python3 validate-assets.py`
Expected: `Errors: 0`.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/patches/worldgen-blockpatches-potato.json
git commit -m "feat: skip Seafarer wild-potato worldgen when Long-Term Food installed

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Final validation

**Files:** none (verification only)

- [ ] **Step 1: Run the validate-mod-assets skill**

Invoke the `validate-mod-assets` skill, scoped to the four new patch files and the modified `worldgen-blockpatches-potato.json`. Confirm 0 errors. Warnings about pre-existing unrelated assets (potato/sugarcane crop shapes) are acceptable.

- [ ] **Step 2: Build the mod**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: `Build succeeded.` with `0 Error(s)` (this confirms no asset packaging breakage; the patches themselves are data-only).

- [ ] **Step 3: In-game smoke test (manual)**

With Long-Term Food installed: confirm Seafarer's corn/potato/sugarcane are absent from creative menu, handbook, and worldgen; confirm LTF corn dries into `seafarer:driedcorn`, LTF sugarcane juices into `seafarer:canejuiceportion`, and LTF corn/potato work in soup/stew. With Long-Term Food absent: confirm Seafarer's own corn/potato/sugarcane still generate and craft normally.

---

## Self-Review

- **Spec coverage:** disable file → Task 1; corn chain → Task 2; sugarcane juicing → Task 3; recipe redirect → Task 4; potato worldgen invert-gate → Task 5; validation → Task 6. All spec sections covered.
- **Placeholders:** none — every patch file's full content is inline.
- **Consistency:** modid `longtermfood` and domain `pemmican:` used consistently; item codes `pemmican:vegetable-corn-raw` / `pemmican:vegetable-potato-raw` / `seafarer:driedcorn` / `seafarer:canejuiceportion` / `seafarer:canemush` consistent across tasks; worldgen indices (corn=2, sugarcane=3) match `crops.json`.
