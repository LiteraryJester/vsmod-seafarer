# Long-Term Food compatibility — design

**Date:** 2026-05-16
**Status:** Approved design, pending implementation plan

## Goal

When the **Long-Term Food** mod (modid `longtermfood`, asset domain `pemmican:`)
is installed, Seafarer cedes corn, potato, and sugarcane entirely to it. LTF
becomes the only obtainable version of those three crops; every Seafarer food
chain that depends on them (tortilla, cornbeer, molasses, sugar, burritos,
stews) continues to work, sourced from LTF crops instead.

When LTF is absent, Seafarer behaves exactly as it does today — nothing changes.

This folds the behaviour of the standalone reference mod `seafarerxltf`
(`existing mods/seafarerxltf`, modid `seafarerltfcompat`) into Seafarer itself.
That standalone mod becomes redundant once this ships.

## Background

- **modid vs domain:** LTF's modid is `longtermfood`; its assets live under the
  `pemmican:` domain. `dependsOn` checks use `longtermfood`; asset/item codes
  use `pemmican:`.
- **LTF crop items:** `pemmican:vegetable-corn-raw`, `pemmican:vegetable-corn-charred`,
  `pemmican:vegetable-potato-raw`; sugarcane is the crop block
  `pemmican:blocktypes/plant/crop/sugarcane.json`.
- **Key insight:** Seafarer's recipe chains enter at *intermediate* items, not
  raw crops:
  - corn → (Dry transition) → `seafarer:driedcorn` → nixtamal → masa →
    tortilladough → tortilla; and `driedcorn` → cornbeer.
  - sugarcane → (juiceable) → `seafarer:canejuiceportion` → molasses, sugar.
  - potato is used directly, only as an additive ingredient in cooking-pot
    recipes.

  Therefore wiring LTF crops to *produce those intermediates* (drying, juicing)
  redirects every downstream recipe automatically, with no recipe edits.

- **Raw-item references** that do need handling are all **additive ingredient
  lists** (`validStacks/-`, `inputs/-`), so redirecting them means adding LTF
  equivalents alongside — no recipe duplication, no `invert` needed:
  - `patches/cooking-potato.json` — `seafarer:potato` into vanilla soup /
    vegetablestew / meatystew.
  - `patches/cooking-coconut.json` — `seafarer:corn` into the same soup /
    vegetablestew / meatystew slots.
  - `patches/expandedfoods-tropical.json` — `seafarer:corn` / `seafarer:potato`
    into ExpandedFoods stuffed-pepper and dumpling filling slots.

## Decisions

- **Soft-disable** (chosen): when LTF is present, disable Seafarer's `corn` /
  `potato` / `sugarcane` *crop blocks* and *all wild worldgen*, and hide the raw
  food *items* from the creative menu and handbook — but leave the item
  definitions intact. This makes LTF the only obtainable version while keeping
  every existing recipe reference valid (no broken refs, no load errors) and not
  destroying items in existing saves. The hidden item definitions are invisible
  to players.
- **Packaged inside Seafarer:** new patch files in `assets/seafarer/patches/`,
  every entry gated by `dependsOn: [{ "modid": "longtermfood" }]` so they are
  inert unless LTF is installed.

## Implementation

Four new patch files plus one edit to an existing file. All new files live in
`Seafarer/Seafarer/assets/seafarer/patches/`.

### 1. `longtermfood-disable.json` (new)

All entries `dependsOn: [{ "modid": "longtermfood" }]`.

- **Disable crop blocks:** `add /enabled = false` on
  `seafarer:blocktypes/plant/crop/corn.json`, `potato.json`, `sugarcane.json`.
- **Hide raw items:** on `seafarer:itemtypes/food/corn.json`, `potato.json`,
  `sugarcane.json`:
  - `add /creativeinventory = {}` (`add` overwrites or creates, so it is safe
    whether or not the key already exists)
  - `addmerge /attributes = { "handbook": { "exclude": true } }`
- **Disable wild corn + sugarcane worldgen:** on
  `seafarer:worldgen/blockpatches/crops.json`, `replace /2/chance = 0`
  (corn entry) and `replace /3/chance = 0` (sugarcane entry). Mirrors
  seafarerxltf `disable-seafarer-wildcrops.json`.

### 2. `longtermfood-crops-corn.json` (new)

Wires LTF corn into Seafarer's corn chain. Mirrors seafarerxltf
`pemmican-corn-drying.json` + `pemmican-corn-burrito.json`. All entries
`dependsOn: [{ "modid": "longtermfood" }]`, target
`pemmican:itemtypes/crops/vegetable.json`.

- `addmerge /transitionablePropsByType` with key `*-corn-raw`: a `Dry`
  transition (freshHours 72, transitionHours 48) → `seafarer:driedcorn`, plus a
  `Perish` transition → `game:rot`.
- `addmerge /attributesByType/*-corn-raw` = `{ "inBurritoProperties": { "partType": "Filling" } }`.
- `addmerge /attributesByType/vegetable-corn-charred` = same `inBurritoProperties`.

This makes LTF corn dry into `seafarer:driedcorn`, which already feeds nixtamal
→ masa → tortilladough → tortilla and the cornbeer barrel recipe — so no recipe
edits are needed for the tortilla or cornbeer chains.

### 3. `longtermfood-crops-sugarcane.json` (new)

Adds juiceable properties to LTF sugarcane. Mirrors seafarerxltf
`pemmican-sugarcane-juicing.json` verbatim. One entry,
`dependsOn: [{ "modid": "longtermfood" }]`, target
`pemmican:blocktypes/plant/crop/sugarcane.json`:

- `addmerge /attributes` = `{ "juiceableProperties": { "litresPerItem": 0.15625,
  "liquidStack": seafarer:canejuiceportion, "pressedStack": seafarer:canemush } }`.

`seafarer:canejuiceportion` already feeds the molasses and sugar chains, so no
recipe edits are needed there.

*Caveat:* this copies the reference mod's choice of putting `juiceableProperties`
on the sugarcane *crop block*. Mirrored as-is on the assumption seafarerxltf is
tested; flagged for verification during implementation.

### 4. `longtermfood-recipes.json` (new)

Redirects the additive raw-item ingredient lists. Adds LTF equivalents to every
path where Seafarer currently adds `seafarer:corn` / `seafarer:potato`. Source
of truth for the exact paths: `cooking-coconut.json`, `cooking-potato.json`,
`expandedfoods-tropical.json`.

- **Vanilla cooking pots** — `dependsOn: [{ "modid": "longtermfood" }]`.
  For each path below, `addmerge` a `validStacks/-` entry for
  `pemmican:vegetable-corn-raw` and one for `pemmican:vegetable-potato-raw`,
  carrying the same `shapeElement` value used by the existing
  `cooking-coconut.json` / `cooking-potato.json` entry:
  - `game:recipes/cooking/soup.json` — ingredients 1, 2, 3
  - `game:recipes/cooking/vegetablestew.json` — ingredients 0, 1, 2, 3
  - `game:recipes/cooking/meatystew.json` — ingredients 5, 6
- **ExpandedFoods stuffed pepper / dumpling** —
  `dependsOn: [{ "modid": "longtermfood" }, { "modid": "expandedfoods" }]`
  (both required), `side: "server"`, `op: addeach` onto `inputs/-`:
  - corn (`pemmican:vegetable-corn-raw`) →
    `expandedfoods:recipes/kneading/stuffedpepper.json` paths
    `/0`, `/3`, `/5`, `/8` `/ingredients/1/inputs/-`
  - potato (`pemmican:vegetable-potato-raw`) → stuffedpepper `/0`, `/3`, `/5`,
    `/8` and `expandedfoods:recipes/kneading/dumpling.json` `/1`, `/3`
    `/ingredients/1/inputs/-`

### 5. `worldgen-blockpatches-potato.json` (edit existing)

Seafarer's wild-potato worldgen `addmerge`s an entry referencing
`seafarer:crop-potato-*`, which is disabled when LTF is present. Add
`"dependsOn": [{ "modid": "longtermfood", "invert": true }]` to its single
patch entry so the wild-potato worldgen applies **only when LTF is absent**.
(`invert` on `PatchModDependence` is confirmed supported.)

## Result

- **With LTF:** LTF is the only obtainable corn/potato/sugarcane; LTF's own
  worldgen handles wild spawns; Seafarer's tortilla, cornbeer, molasses, sugar,
  burrito, and stew chains all work off LTF crops.
- **Without LTF:** unchanged.
- No recipe duplication, no broken references, existing saves unaffected.

## Out of scope

- Disabling or altering LTF's own content.
- Any change to Seafarer's intermediate/derived items (`driedcorn`, `masa`,
  `nixtamal`, `tortilla`, `cornbeer`, `canejuiceportion`, `molasses`, sugar) —
  they are kept and simply sourced from LTF crops.
- The standalone `seafarerxltf` mod — it becomes redundant; retiring it is a
  separate task.

## Validation

- `python3 validate-assets.py` — 0 errors.
- `validate-mod-assets` skill checks on the new/changed patch files.
- In-game: with LTF installed, confirm Seafarer corn/potato/sugarcane are absent
  from creative + handbook + worldgen, and that LTF corn dries to `driedcorn`,
  LTF sugarcane juices to `canejuiceportion`, and LTF crops work in the stew
  recipes. With LTF absent, confirm Seafarer's crops still generate and work.
