# Manual Test Plan — 2026-04-18 Session

Covers changes made this session: Dawn Marie quest fixes, gardening training,
Grow Pot (dwarf fruit trees), buccaneer cutlass smithing, and wild-crop worldgen.

All tests assume a dev client with `--addOrigin` pointing at the Seafarer repo
and Progression Framework loaded.

---

## 1. Dawn Marie — orchard quest

**Setup.** Spawn near Dawn Marie (or `/tp` to her). Creative-give yourself
fruit tree cuttings via `/giveblock fruittree-cutting 1 @s {type:"pinkapple"}`
(repeat for redapple, yellowapple, cherry, peach, pear, orange, mango,
breadfruit, lychee, pomegranate). The block code is literally `fruittree-cutting`
— species lives entirely in the `type` attribute on the stack.

- [ ] Talking to Dawn Marie exposes the "Need any help with the orchard?" option.
- [ ] Accepting starts quest `seafarer:dawnmarie-orchard` (check `L` ledger tab).
- [ ] With a **pinkapple cutting** held, the "I have a pink apple cutting" option appears.
- [ ] With a **redapple cutting** held, only the redapple option appears (other species hidden).
- [ ] Repeat visibility check for each of the 11 species — each option only shows
      when its exact species is held (verifies the dialogue's `attributes.type` gating).
- [ ] Delivering a species:
  - Consumes exactly one cutting.
  - Awards **+20 gardener XP** (ledger > training).
  - Adds that species's cutting to Dawn Marie's sell list (open trade UI after).
- [ ] Species-specific shop entries carry the correct `attributes.type` — buying one
      and placing it should grow the right fruit species.
- [ ] After **6** distinct species delivered, the quest auto-completes.
- [ ] Completion adds `seafarer:grow-pot-brown-fired` to her sell list.
- [ ] Completion bumps `global.rebuilding-tier` by 1; if it reaches the threshold,
      `global.rebuilding-complete` flips.

---

## 2. Dawn Marie — plantation quest

**Setup.** `/giveitem seeds-rice 10` (repeat for soybean, amaranth, cassava,
peanut, pineapple, sunflower, rye, parsnip, turnip, spelt, onion, flax, carrot).

- [ ] "Need any help with the plantation?" option available.
- [ ] Accept → quest active in ledger.
- [ ] For each of the 14 seed types, holding ≥10 of that seed exposes the matching
      "I have X seeds" dialogue option.
- [ ] Delivery takes exactly 10 seeds, awards **+10 gardener XP**, and adds that seed
      to Dawn Marie's sell list.
- [ ] After 6 distinct crops delivered, quest completes.
- [ ] Completion adds `seafarer:trainingbook-gardener` (Gardener's Almanac) to her sell list.
- [ ] `rebuilding-tier` bumps.

---

## 3. Gardening training + book

- [ ] Ledger > Training shows a new "Gardening" profession (green) with a "Gardener" training bar.
- [ ] Gardener XP earned from orchard/plantation deliveries accrues correctly.
- [ ] Reaching 100 XP triggers level-up toast and grants the `gardener` trait.
- [ ] Buying the **Gardener's Almanac** from Dawn Marie places it in inventory.
- [ ] Right-click-held Almanac → awards 100 gardener XP, consumes the book.
- [ ] Book renders in hand (placeholder texture — copied from bearhunter; visual
      is expected to match bearhunter until real art is dropped in at
      `textures/item/lore/trainingbook-gardener.png`).

---

## 4. Grow Pot — crafting and firing

**Clayforming.** Hold blue/fire/red clay, place a clay-forming block.

- [ ] New "Grow Pot" pattern appears in the clayforming recipe list for all
      three clay colors.
- [ ] Pattern is 12×12 base + 5 rim layers (shallower than the vanilla 8-layer planter).
- [ ] Output is `seafarer:grow-pot-{blue,fire,red}-raw` depending on clay used.
- [ ] Raw pot is ground-storable, pickup on right-click.

**Firing.** Pit kiln + beehive kiln both.

- [ ] **Pit kiln** (charcoal pit) — red-raw → `grow-pot-earthyorange-fired`;
      blue-raw → `grow-pot-blue-fired`; fire-raw → `grow-pot-fire-fired`.
- [ ] **Beehive kiln** — placement slot determines color:
  - red-raw → tan / orange / red / brown (4 possible outputs)
  - blue-raw → cream / gray / black / black
  - fire-raw → fire / fire / fire / fire
- [ ] Fired pots placeable on ground, wall, table.
- [ ] All 10 color variants display correctly (different ceramic tints, same planter silhouette with soil).

---

## 5. Grow Pot — planting dwarf fruit trees (Harmony-patched)

**Setup.** Place a fired grow-pot, e.g. `grow-pot-brown-fired`. Hold any fruit
tree cutting stack.

- [ ] Right-click grow-pot with cutting in hand:
  - One cutting consumed.
  - A `fruittree-cutting-ud` block spawns **above** the pot (pos + up).
  - Plant sound plays.
- [ ] The cutting's stack carries `dwarf: true` plus timing diffs (verify via
      block-entity debug, F3 debug screen with entitydebug on, or by observing behavior).
- [ ] Tree growth is visibly smaller than a normally-planted cutting:
  - Trunk tops out at ~1 block tall (vs ~3 for normal).
  - Few to no horizontal side branches.
  - Very small crown (~3–5 foliage blocks total).
- [ ] Fruiting cycle completes faster than normal:
  - Compare side-by-side: plant a dwarf and a normal cutting at the same time; dwarf
    transitions Flowering → Fruiting → Ripe roughly **1.5 days** sooner per phase.
- [ ] Harvest yield per ripe cycle is substantially lower (fewer foliage blocks =
      fewer harvest spots; this is the "much lower yield" design).
- [ ] Test with at least 3 species (e.g. pinkapple, cherry, olive) — dwarfing works
      for all; species attribute still drives fruit drop table.
- [ ] **Regression check**: plant a cutting on normal fertile dirt (not in a pot) —
      tree should grow to normal size. The Harmony patch only fires when `dwarf: true`
      is set on the parent stack.

---

## 6. Cutlass smithing (buccaneer-gated)

**Setup.** Creative-give copper ingots and a smithing anvil + hammer.

- [ ] **Without** buccaneer trait (fresh player): placing a copper ingot on the
      anvil does **not** show a "Cutlass" smithing recipe option.
- [ ] Read the Buccaneer's Field Guide → gain `buccaneer` trait.
- [ ] **With** buccaneer trait: "Cutlass" pattern now available in smithing UI.
- [ ] Test smithing with each allowed metal: copper, tinbronze, bismuthbronze,
      blackbronze, iron, meteoriciron, steel.
- [ ] Output is `seafarer:bladehead-cutlass-{metal}` (sabre silhouette with
      transparent handle, metal-tinted blade).
- [ ] **Quench test**: while hot, iron/meteoriciron/steel bladeheads can be
      quenched in water — resulting item carries quench buff attribute.
- [ ] **Grid hafting**: place `bladehead-cutlass-{metal}` above a `stick` in
      crafting grid (1×2 vertical) → outputs `seafarer:cutlass-{metal}` with
      `applyquenchablebuffs: true` (quench buff transfers).
- [ ] Finished cutlass is usable as a melee weapon (attack animation, durability,
      attack power matches the item's existing per-metal stats).
- [ ] **Known gap**: gold and silver cutlass variants have no smithing path yet
      (item exists, no recipe).

---

## 7. Worldgen — wild crops

**Setup.** `/wgen regen` or start a fresh world. Use `/tp` to regions with
different climate to spot-check each patch.

- [ ] **Chili bushes** (`chilibush-wild-free`) spawn in warm drier regions
      (~20–40°C, rainfall 0.3–0.7). Patches of 1–3 bushes.
- [ ] **Tomato bushes** (`tomatobush-wild-free`) spawn in warm temperate regions
      (~12–32°C, rainfall 0.4–0.75). Small patches.
- [ ] **Corn** (`crop-corn-{4,6,8,9,11}`) spawns in grasslands (~15–35°C,
      rainfall 0.4–0.8) in mixed-stage patches of ~13 blocks.
- [ ] **Sugar cane** (`crop-sugarcane-{3,5,7,8}`) spawns in hot wet tropical
      lowlands (~25–45°C, rainfall ≥0.6) in patches of ~8 blocks.
- [ ] Each wild crop is harvestable — chili/tomato yield fruits + seeds,
      corn/sugarcane yield crop drop at appropriate stage.
- [ ] Verify no cross-contamination: chili doesn't appear in cold regions,
      sugar cane doesn't appear in arid regions, etc.

---

## 8. Open items / known caveats (not for this test pass)

These were flagged during the session but not yet fixed — capture them for a
follow-up plan.

- **Dawn Marie quest prereqs**: `dawnmarie-orchard` and `dawnmarie-plantation`
  are currently `autoEnable: true`. Spec calls for `rebuilding > 3` gating.
- **Stale design docs**: `Seafarer/quests/dawnmarie.md` and
  `Seafarer/quests/quest-tree.md` still document the "In Need of Trade Goods"
  quest under Dawn Marie. That quest has moved to Morgan; the specs should
  be updated.
- **Gardener book texture**: currently a copy of `trainingbook-bearhunter.png`.
  Replace with a gardener-specific texture when art is available.
- **Grow pot clayforming visual**: recipe pattern is a direct shortened variant
  of the planter. If you want a more distinctive voxel silhouette, edit
  `recipes/clayforming/grow-pot.json`.
- **Premiumfish validator error**: pre-existing `food.ef_protein` error is
  unrelated to this session's changes; leave until the expandedfoods patch
  work is revisited.
