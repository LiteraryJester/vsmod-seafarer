# Unseasoned plank intermediate step — design

## Goal

Insert a saw step between a fresh wood plank and the seasoned plank. Today
`plank-{wood}` air-dries directly to `plank-seasoned` over 168 in-game hours.
After this change, the flow is:

```
plank-{wood}  ── (grid: + saw) ─►  plank-unseasoned  ── (dry 168h) ─►  plank-seasoned
```

`plank-unseasoned` is a dead-end item with no use other than sitting in
inventory/storage until it dries to seasoned. It has no block forms and is not
accepted in any recipe.

## Constraints

- Mirror the existing `plank-seasoned`/`plank-varnished` model: `unseasoned`
  is added as a *state* to the plank item's variant group, producing a single
  generic `game:plank-unseasoned` item (no per-wood variants). Wood type is
  lost at the saw step, just as it is today at the dry step.
- All work is JSON. No C# changes.
- No migration: existing in-progress drying planks in saved worlds simply
  stop drying. Players must re-process them through the saw to enter the new
  pipeline. This is acceptable for a small mod update.
- Preserve all existing exclusions: `seasoned`, `varnished`, `aged`,
  `veryaged` planks do not dry and are not valid saw inputs.

## Changes

### 1. `assets/seafarer/patches/plank-variants.json`

Add `unseasoned` to the plank *item* variant group only — not to any of the
plank-derived blocks. This keeps `plank-unseasoned` from having a placeable or
craftable block form.

Lines added next to the existing `seasoned`/`varnished` entries for
`game:itemtypes/resource/plank.json`:

```json
{ "op": "add", "path": "/variantgroups/0/states/-", "value": "unseasoned",
    "file": "game:itemtypes/resource/plank.json" },
```

Extend the `textures/wood/baseByType` map with a `*-unseasoned` entry. Use
`block/wood/planks/oak1` as a placeholder texture (warm, neutral, reads as
"plank"). Replace later if/when a dedicated texture is made.

```json
"*-unseasoned": "block/wood/planks/oak1",
```

The replace patch already lists `*-seasoned`, `*-varnished`, `*-aged`,
`*-veryaged`, then `*`. Insert `*-unseasoned` *before* `*` (wildcard match is
first-found).

### 2. `assets/seafarer/patches/plank-seasoning.json`

Rewrite the `transitionablePropsByType` map so only `*-unseasoned` carries the
Dry transition. Every other plank state — including plain `plank-{wood}` — is
an empty array.

```json
"transitionablePropsByType": {
    "*-unseasoned": [{
        "type": "Dry",
        "freshHours": { "avg": 0 },
        "transitionHours": { "avg": 168 },
        "transitionedStack": { "type": "item", "code": "game:plank-seasoned" },
        "transitionRatio": 1
    }],
    "*": []
}
```

Drop the `*-seasoned`/`*-varnished`/`*-aged`/`*-veryaged` keys — they collapse
into `*` since `*` is now also empty.

### 3. `assets/seafarer/recipes/grid/plank-unseasoned.json` (new)

Shapeless 1×2 grid recipe. Saw is a tool ingredient (loses 1 durability via
`isTool: true`). The wood-plank input wildcard skips any already-processed
states so the player can't loop unseasoned/seasoned/varnished planks back
through the recipe.

```json
[
    {
        "ingredientPattern": "SP",
        "ingredients": {
            "S": { "type": "item", "tags": ["tool-saw"], "isTool": true },
            "P": {
                "type": "item",
                "code": "game:plank-*",
                "name": "wood",
                "skipVariants": ["seasoned", "varnished", "unseasoned"]
            }
        },
        "width": 2,
        "height": 1,
        "shapeless": true,
        "output": { "type": "item", "code": "game:plank-unseasoned", "quantity": 1 }
    }
]
```

### 4. `assets/seafarer/lang/en.json`

Add:

```json
"item-plank-unseasoned": "Unseasoned plank"
```

## Out of scope

- Adding `unseasoned` to any plank-derived block (`planks`, `plankslab`,
  `plankstairs`, `door`, `ladder`, `trapdoor`, `boatseat`, `oar`, fences,
  roofing, path, supportbeam). Unseasoned planks intentionally have no block
  form and no other recipe uses.
- Any change to the existing `plank-seasoned`/`plank-varnished` pipeline.
- Migration of mid-transition planks in existing save files.
- Custom unseasoned texture art — placeholder reuses `oak1`.

## Validation

After the changes:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
python3 validate-assets.py
```

Build:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

In-game smoke test:

1. Spawn a fresh wood plank — confirm it no longer shows a drying timer in
   the handbook / item tooltip.
2. Open the handbook, search "unseasoned plank" — confirm the grid recipe
   appears and accepts any wood plank.
3. Craft an unseasoned plank with a saw — confirm 1:1 output and saw
   durability decreased by 1.
4. Leave the unseasoned plank in inventory — confirm it shows a 168h dry
   transition and yields `plank-seasoned` when complete.
5. Confirm `plank-unseasoned` does not appear as a valid input in any other
   handbook recipe.
