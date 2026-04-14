# Salt and Sand - Feature List & Research

> A Vintage Story mod about surviving, exploring, and trading across coastal and island-based maps.
> Progression through stone age, bronze age, iron age, and steampunk-inspired ages.
> Encourages seafaring trade, traveling villages/camps, and community building.

---

## Implementation Status

> **Legend:** DONE = fully implemented | PARTIAL = some parts implemented | PLANNED = not yet started

| Feature | Status | Notes |
|---------|--------|-------|
| 1.1 Salting & Drying | **DONE** | Drying rack, salted meat, dried salted meat all implemented |
| 1.2 Fermenting | PLANNED | |
| 1.3 Flatbread & Ship's Bread | PLANNED | |
| 1.4 New Meals | PLANNED | |
| 1.5 Clay Griddle (Comal) | PLANNED | |
| 2. Crops | **PARTIAL** | 9 of 15 crops done (corn, chili, coca, ginger, tea, sugarcane, hemp, agave, taro) |
| 3. Alcohol & Preserved Fruits | PLANNED | |
| 4.1 Fermentation Jar | PLANNED | |
| 4.2 Amphora | **DONE** | Liquid container + sealed storage variants |
| 4.3 Waterskin | **DONE** | 4L liquid container |
| 4.4 Salt Extractor | **DONE** | Solar evaporation salt pan |
| 4.5 Hemp Sack | PLANNED | |
| 4.6 Cargo Net | PLANNED | |
| 5. Pests | PLANNED | |
| 6.1 Log Barge | **DONE** | 2 seats, 6 expansion slots, raft sail |
| 6.2 Sampan | PLANNED | |
| 6.3 Catamaran | PLANNED | |
| 7. Ship Room System | PLANNED | |
| 8. Hydration & Exposure | **PARTIAL** | Exposure system (heatstroke/frostbite) done. Hydration deferred to HydrateOrDiedrate. |
| 9. Walkable Boats | PLANNED | |
| 10. Rift/Treasure Sand | PLANNED | |
| 11. Mod Compatibility | PLANNED | |
| 12. Tortuga Village | PLANNED | Pirate port with 4 named merchant NPCs, worldgen structure |
| 13. Fishing Bounty System | PLANNED | Harbormaster NPC, weekly bounties, tokens, ranks, leaderboard |

---

## Table of Contents

- [1. Food & Preservation](#1-food--preservation)
- [1.5 Clay Griddle (Comal)](#15-clay-griddle-comal)
- [2. New Crops & Plants](#2-new-crops--plants)
- [3. Alcohol & Preserved Fruits](#3-alcohol--preserved-fruits)
  - [3.1 New Alcoholic Beverages](#31-new-alcoholic-beverages)
  - [3.2 Preserved Fruit in Alcohol](#32-preserved-fruit-in-alcohol)
- [4. Storage & Containers](#4-storage--containers)
  - [4.1 Fermentation Jar](#41-clay-fermentation-jar-onggi--tinaja)
  - [4.2 Amphora](#42-amphora-trade-vessel)
  - [4.3 Waterskin](#43-waterskin)
  - [4.4 Salt Extractor](#44-salt-extractor-evaporation-pan)
  - [4.5 Hemp Sack](#45-hemp-sack-cargo-sack)
  - [4.6 Cargo Net](#46-cargo-net)
- [5. Pests](#5-pests)
- [6. Ships & Boats](#6-ships--boats)
- [7. Ship Room System](#7-ship-room-system)
- [8. Hydration & Exposure System](#8-hydration--exposure-system)
- [9. Walkable Boats](#9-walkable-boats)
- [10. Rift/Treasure Sand](#10-rifttreasure-sand)
- [11. Mod Compatibility](#11-mod-compatibility)
- [12. Tortuga Village](#12-tortuga-village)
  - [12.1 Provisioner](#121-provisioner)
  - [12.2 Cartographer](#122-cartographer)
  - [12.3 Shipwright](#123-shipwright)
  - [12.4 Treasure Merchant](#124-treasure-merchant)
- [13. Fishing Bounty System](#13-fishing-bounty-system)
  - [13.1 Harbormaster NPC](#131-harbormaster-npc)
  - [13.2 Weekly Bounties](#132-weekly-bounties)
  - [13.3 Points & Ranking](#133-points--ranking)
  - [13.4 Bounty Tokens & Reward Shop](#134-bounty-tokens--reward-shop)
  - [13.5 Leaderboard](#135-leaderboard)

---

## 1. Food & Preservation

> **Design Philosophy:** Extend rather than reinvent. The `butchering` and `expandedfoods` (+ `aculinaryartillery`) mods already provide excellent meat processing, smoking, and basic preservation systems. SaltAndSand should depend on these mods and build on top of them, adding the seafaring/coastal preservation layer they don't cover.
>
> **Proposed dependencies:** `butchering`, `expandedfoods` (which itself requires `aculinaryartillery`)

### What Already Exists Across Base Game + Mods

| System | Provider | Details |
|--------|----------|---------|
| **Raw meat types** | Base game | redmeat, bushmeat, poultry, fish (raw/cooked/cured variants) |
| **Smoking rack** | `butchering` | BlockMeatHook — transforms raw/cured meat to smoked variants. Smoked-none: 800h fresh. Smoked-cured: 9,200h fresh (~1 year) |
| **Butcher infrastructure** | `butchering` | Butcher tables (primitive/simple/advanced), wall/ceiling hooks, offal/blood/sinew processing |
| **Sausages** | `butchering` | Blood sausage, black pudding (raw/cooked/smoked variants) |
| **Salt curing (barrel)** | Base game | 2 salt + raw meat, 480h seal → cured meat (8,760h fresh) |
| **Brine + pickling** | Base game | Water + salt → brine. Brine + vegetable, 336h seal → pickled vegetable (1,800h fresh) |
| **Hardtack** | `expandedfoods` | Multi-stage baking: raw→bake1→bake2→bake3→bake4. Final: 11,664h fresh (~1.3 years) |
| **Pemmican** | `expandedfoods` | Meat + fat + dried fruit via kneading. 3,160-6,320h fresh |
| **Fermented fish** | `expandedfoods` | 8 salt + 64 fish, 336h barrel seal → fermented fish slop → juiceable to fish sauce |
| **Fish sauce** | `expandedfoods` | Liquid from pressing fermented fish. 4,320h fresh |
| **Soy processing** | `expandedfoods` | Soak → culture → slurry → press for soy sauce (17,520h fresh) |
| **Vinegar** | `expandedfoods` | Cider → vinegar (336h barrel). Vinegar pickling (336h) |
| **Dried/dehydrated fruit** | `expandedfoods` | Transition-based drying for all fruit types |
| **Salted meat nuggets** | `expandedfoods` | Smashed/raw/cooked variants with extended freshness |
| **Lime-preserved eggs** | `expandedfoods` | 672h barrel seal, very long preservation |

### What's Missing — Our Niche

The existing mods don't cover:
1. **Drying rack infrastructure** — no physical block for drying (only passive transitions)
2. **Multi-step salt-then-dry process** — curing and drying are separate, never combined as a pipeline
3. **Fermentation as a second step after brining** — pickling is a terminal state, never feeds into further fermentation
4. **Garum / high-grade fish sauce** — ExpandedFoods has basic fish sauce but no quality tiers or the historical multi-month process
5. **Flatbread / ship's biscuit alternatives** — hardtack exists but no unleavened flatbread tradition
6. **Cultural preservation recipes** — no kimchi, no olive fermentation, no coconut processing, no nixtamalization
7. **Fermented condiments as cooking multipliers** — no system where preserved ingredients boost meal nutrition
8. **Container-specific fermentation** — no mechanic where the vessel type affects the product

---

### 1.1 Salting and Drying — DONE

**Historical Basis:** Nearly every seafaring culture independently developed salt-curing followed by air/sun drying as the primary meat preservation method. Mediterranean salt fish packed in amphorae (Phoenician trade from 5th century BCE), Southeast Asian dried seafood traditions (weight reduction for transport), and Mesoamerican salt-and-sun-dried techniques all follow the same basic pattern: salt draws moisture, then air exposure finishes dehydration.

#### Process (Game Mechanic)

**Step 1 — Salting (Barrel Recipe, extends base game curing)**
```
Input:  1 salt + 3 meat/fish (any raw type from base game or butchering)
Seal:   240 hours (10 days — faster than full cure since we're not done yet)
Output: salted-{meattype} item (intermediate product)
```
This is a lighter salt application than the base game's 2:1 heavy cure. It's preparation for drying, not a standalone preservation method.

**Step 2 — Drying (Drying Rack block)**
```
Input:  salted-{meattype} placed on drying rack
Time:   48-96 hours depending on climate (hotter/drier = faster)
Output: dried-salted-{meattype}
```
The drying rack is a new block (see below). Items placed on it slowly transition via `transitionableProps` type `"Dry"`.

**Step 3 — Use**
- Eat directly: moderate satiety, low health bonus
- Use in cooking: **nutrition multiplier** when combined with other ingredients in stews, porridge, or flatbread wraps
- Store: target 6,000-8,000h fresh (250-333 days) — competitive with smoked-cured from butchering mod but achieved through a different, more salt-efficient process

#### Drying Rack Block — DONE

**Historical basis:** Universal across cultures — wooden frames with string/rope for hanging meat strips, fish fillets, fruit, and herbs. Southeast Asian coconut copra racks, Mediterranean fish-drying frames, Mesoamerican chili drying racks.

**Implementation:**
- New block type: `BlockDryingRack` / `DryingRackEntity`
- Visual: Wooden frame with rope lines (similar to butchering mod's smoking rack aesthetic)
- Capacity: 4 item slots displayed visually on the rack
- Mechanic: Items with `dryableProps` attribute slowly convert over time
- Climate sensitivity: drying speed modified by temperature and humidity (rain slows/stops drying)
- **Not a smoking rack** — no fire/fuel required, pure air drying
- Can dry: salted meats, salted fish, fruit (for dried fruit), chilies, coconut meat, seaweed, herbs

**What we extend from butchering:**
- Follow `BlockMeatHook` patterns for item display transforms (`onHookTransform`)
- Use `transformsWhenDriedByType` attribute (paralleling butchering's `transformsWhenSmokedByType`)
- Compatible item categories — anything that works on butchering hooks can potentially go on drying racks

#### New Item Types

| Item | Fresh Hours | In Meals | Notes |
|------|-------------|----------|-------|
| `salted-redmeat` | 720h (intermediate) | N/A | Goes to drying rack |
| `salted-bushmeat` | 720h (intermediate) | N/A | Goes to drying rack |
| `salted-poultry` | 720h (intermediate) | N/A | Goes to drying rack |
| `salted-fish` | 720h (intermediate) | N/A | Goes to drying rack |
| `dried-salted-redmeat` | 7,200h | 400 sat | Long-voyage staple |
| `dried-salted-bushmeat` | 6,000h | 200 sat | Common preserved meat |
| `dried-salted-poultry` | 6,000h | 250 sat | Light preserved meat |
| `dried-salted-fish` | 8,000h | 300 sat | Best preservation, historical staple |

#### Key Reference Files
- `butchering:blocktypes/smokingrack.json` — model for our drying rack block structure
- `butchering:itemtypes/food/smoked.json` — model for `transformsWhenSmokedByType`, we create parallel `transformsWhenDriedByType`
- `survival/recipes/barrel/curedmeat.json` — base game barrel recipe format for our salting step
- `expandedfoods:itemtypes/food/pemmican.json` — example of multi-step preservation item

---

### 1.2 Fermenting

**Historical Basis:** Fermentation is the second-oldest preservation technique after drying. Every seafaring culture developed fermentation traditions tied to their local ingredients:
- **Mediterranean:** Garum (fish sauce, 2-3 months), olive brine fermentation (3-6 months), wine→vinegar
- **East Asian:** Doenjang/miso (soybean paste, 6-24 months), fish sauce/nam pla (6-24 months), kimchi (days to months)
- **Southeast Asian:** Belacan/shrimp paste (iterative grind-and-ferment), tapai (rice fermentation, 2-4 days)
- **Mesoamerican:** Cacao fermentation (1 week), chili fermentation, pulque (agave)

#### Design: Fermentation as a Second Step After Brining

The base game already has brining (water + salt → brine) and pickling (brine + vegetable, 336h). We add fermentation as a **continuation** of that pipeline — pickled/brined foods can be further fermented for longer periods to produce superior products.

```
BASE GAME PIPELINE (unchanged):
  water + salt → brine (instant)
  brine + vegetable → pickled vegetable (336h)

SALTANDSAND EXTENSION:
  pickled vegetable + salt → fermented vegetable (672h)
  pickled legume (soybean) + salt → fermented soybean paste (1,008h)
  brine + raw fish + salt → fermented fish (672h) → press → garum (fish sauce)
  brine + chili (new crop) + salt → fermented chili paste (672h)
  brine + olive + salt → fermented olive (504h) — deeper ferment than base game pickle
```

The key insight: **fermentation takes pickled/brined items as INPUT**, making it a clear progression rather than a parallel system. Players who already understand pickling naturally discover fermentation as the next step.

#### Fermentation Recipes (Barrel Format)

**Fermented Vegetables** (general)
```json
{
  "code": "fermentedvegetable",
  "sealHours": 672,
  "ingredients": [
    { "type": "item", "code": "pickledvegetable-*", "name": "type", "quantity": 4 },
    { "type": "item", "code": "salt", "quantity": 2 }
  ],
  "output": { "type": "item", "code": "seafarer:fermentedvegetable-{type}", "stackSize": 4 }
}
```

**Fermented Soybean Paste (Doenjang-style)**
```json
{
  "code": "fermentedsoybean",
  "sealHours": 1008,
  "ingredients": [
    { "type": "item", "code": "pickledlegume-soybean", "quantity": 8 },
    { "type": "item", "code": "salt", "quantity": 4 }
  ],
  "output": { "type": "item", "code": "seafarer:fermentedsoybean", "stackSize": 8 }
}
```

**Garum / Fish Sauce (High-Grade)**

ExpandedFoods already adds basic fish sauce (8 salt + 64 fish, 336h). Our version is a **premium tier** that takes longer but produces a superior product:
```json
{
  "code": "garum",
  "sealHours": 1344,
  "ingredients": [
    { "type": "item", "code": "expandedfoods:fermentedfish-slop", "quantity": 16 },
    { "type": "item", "code": "salt", "quantity": 4 },
    { "type": "item", "code": "brineportion", "litres": 2, "consumeLitres": 2 }
  ],
  "output": { "type": "item", "code": "seafarer:garumportion", "litres": 4 }
}
```
This takes ExpandedFoods' fermented fish slop as input and does a second, longer fermentation (1,344h = 56 days) to produce garum — a premium trade good. Historically, top-grade garum was one of the most expensive liquids in the Roman world.

**Fermented Chili Paste**
```json
{
  "code": "fermentedchili",
  "sealHours": 672,
  "ingredients": [
    { "type": "item", "code": "brineportion", "litres": 1, "consumeLitres": 1 },
    { "type": "item", "code": "seafarer:chili-*", "name": "chilitype", "quantity": 8 },
    { "type": "item", "code": "salt", "quantity": 2 }
  ],
  "output": { "type": "item", "code": "seafarer:fermentedchilipaste", "stackSize": 4 }
}
```

**Fermented Olives (Deep Brine Cure)**
```json
{
  "code": "fermentedolive",
  "sealHours": 504,
  "ingredients": [
    { "type": "item", "code": "pickledvegetable-olive", "quantity": 8 },
    { "type": "item", "code": "salt", "quantity": 2 }
  ],
  "output": { "type": "item", "code": "seafarer:fermentedolive", "stackSize": 8 }
}
```

#### Fermented Item Properties

| Item | Fresh Hours | In Meals | Notes |
|------|-------------|----------|-------|
| `fermentedvegetable-{type}` | 4,320h | 200 sat + bonus | Kimchi-equivalent, anti-scurvy |
| `fermentedsoybean` | 8,640h | 150 sat + 1 health | Miso/doenjang-equivalent, protein-rich |
| `garumportion` (liquid) | 8,640h | 100 sat in meals | Premium condiment, high trade value |
| `fermentedchilipaste` | 6,000h | 80 sat + 1 health | Spicy condiment, antimicrobial |
| `fermentedolive` | 6,000h | 120 sat | Superior to pickled olive |

#### Cooking Multiplier System

**Core mechanic:** When fermented/preserved items are used as ingredients in cooking recipes (stews, porridge, etc.), they provide a **nutrition bonus** beyond their standalone value. This reflects the historical reality that preserved condiments made bland shipboard staples (rice, hardtack, flatbread) into nutritious meals.

Implementation approach: Patch base game and ExpandedFoods cooking recipes to accept our fermented items as valid ingredients with higher `satiety` values in the `nutritionPropsByType` `inMeal` section.

---

### 1.3 Flatbread & Ship's Bread

**Historical Basis:** Every seafaring culture had an unleavened, twice-baked bread designed for long voyages:
- **Mediterranean:** Panis buccellatus (Roman army bread, twice-baked), matzah (unleavened), pita precursors
- **Northern European:** Hardtack / ship's biscuit (baked 4x, prepared 6 months before sailing)
- **Mesoamerican:** Tortillas from nixtamalized corn (masa + comal griddle)
- **South/Southeast Asian:** Roti, chapati, naan (flatbreads cooked on hot surfaces)

ExpandedFoods already has **hardtack** (multi-stage baking, up to 11,664h fresh). We add **flatbread** as a complementary item — simpler to make, lower tech requirement, different ingredient options.

#### Flatbread vs Hardtack (Differentiation)

| Property | Flatbread (ours) | Hardtack (ExpandedFoods) |
|----------|-----------------|--------------------------|
| Ingredients | Flour + water + salt + optional oil | Flour + water + salt + optional egg yolk |
| Cooking method | **Clay griddle** (comal) or clay oven | Requires clay oven, multi-stage baking |
| Baking stages | Single bake (or optional 2nd bake in oven) | 4 progressive baking stages |
| Preservation | 500h (single) / 2,000h (twice-baked) | Up to 11,664h (quad-baked) |
| Nutrition | Higher per-serving (200-250 sat) | Lower per-serving but lasts far longer |
| Best use | Daily eating, meal ingredient, wraps | Long-term emergency rations |
| Tech tier | **Stone age (clay griddle)** | Requires clay oven |
| Meal role | Base for wraps/pizza, dipping in stew | Eaten alone or soaked in liquid |

#### Flatbread Recipe

**Kneading recipe** (consistent with ExpandedFoods kneading system):
```
Input:  flour-{grain} (1) + waterportion (0.1L) + salt (1)
Output: flatbreaddough-{grain}
```

**Optional enriched variant:**
```
Input:  flour-{grain} (1) + waterportion (0.1L) + salt (1) + seafarer:oliveoil (0.1L)
Output: flatbreaddough-{grain}-oiled
```

**Cooking** (uses the clay griddle — see section 1.5):
- Clay griddle (comal): produces `flatbread-{grain}` (500h fresh, 200 satiety) — **primary method, stone-age accessible**
- Clay oven: produces `flatbread-{grain}-twice` (2,000h fresh, 180 satiety — drier, less nutritious but longer lasting)
- Metal griddle (copper/iron tier): produces same item but faster cooking time

#### Tortilla (Corn Flatbread with Nixtamalization)

**Historical basis:** Mesoamerican nixtamalization — corn cooked in alkaline water (wood ash lye) to unlock niacin and reduce mycotoxins. Without this process, corn-dependent populations develop pellagra. The tortilla was cooked on a clay comal (griddle), which is the origin of our clay griddle block.

**Process:**
1. Corn + wood ash + water → nixtamal (barrel recipe, 24h seal — quick alkaline soak)
2. Nixtamal ground to masa (grinding recipe)
3. Masa shaped into tortillas (kneading recipe)
4. Cook on **clay griddle** → tortilla

This is a unique multi-step chain that makes corn a valuable but skill-intensive crop. The wood ash requirement ties into the firepit/charcoal system already in game.

---

### 1.4 New Meals

**Historical Basis:** Seafaring meals were defined by combining preserved staples with whatever fresh ingredients were available. Roman sailors ate bucellatum dipped in posca (vinegar water) with garum-dressed vegetables. Asian sailors combined rice with fish sauce and fermented vegetables. The pattern is universal: bland preserved base + flavorful preserved condiment + occasional fresh supplement.

#### Meal Design Philosophy

Meals that use SaltAndSand preserved ingredients should be **nutritionally superior** to meals made from fresh ingredients alone. This rewards the time investment in preservation and creates a reason to maintain a preserved food supply rather than just eating fresh.

#### New Meal Recipes

**Seafarer's Stew** (cooking recipe)
```
Input: dried-salted-{meat} (1) + fermentedvegetable-{type} (1) + waterportion (2) + optional grain (1)
Output: seafarerstew (4 servings)
Nutrition: 350 satiety per serving + 1 health (bonus from preserved ingredients)
Preservation: 288h fresh (12 days — longer than base stew's 144h due to preserved ingredients)
```

**Flatbread Wrap** (grid recipe)
```
Input: flatbread-{grain} (1) + any cooked meat OR fermented vegetable OR cheese (1)
Output: flatbreadwrap-{filling} (2)
Nutrition: 300-400 satiety depending on filling
Preservation: 96-240h depending on filling freshness
```

**Garum Rice / Garum Porridge** (cooking recipe)
```
Input: grain (2-4) + garumportion (0.1L) + waterportion (1) + optional vegetable/meat
Output: garumporridge (4 servings)
Nutrition: 280 satiety per serving + 1 health (garum bonus)
```

**Pizza** (cooking recipe — the ancient secret)

Historical basis: Flatbread with toppings dates to ancient Mediterranean — Roman placenta (layered cheese/honey cake), focaccia precursors, and Egyptian flatbreads with toppings.

```
Input: flatbreaddough-{grain} (1) + cheese (1) + oliveoil (0.1L) + vegetable/meat/fermented toppings (1-2)
Cook:  Clay oven at 200C
Output: pizza-{grain} (4 servings)
Nutrition: 450 satiety per serving + 2 health (highest meal nutrition in the mod)
Preservation: 48h fresh (eat it while it's hot)
```

Pizza is intentionally the best food in the mod but requires multiple advanced ingredients (flatbread dough, cheese, olive oil, toppings) that each have their own production chains. It's the endgame meal that rewards having all systems working together.

#### Meal Nutrition Bonus Table

| Ingredient Type | Standalone Satiety | Bonus When In Meals | Rationale |
|----------------|-------------------|---------------------|-----------|
| Dried-salted meat/fish | 150-250 | +50-100 bonus | Rehydrates and flavors the meal |
| Fermented vegetables | 120-200 | +50 bonus + health | Vitamins, anti-scurvy |
| Garum/fish sauce | 50-100/L | +80 bonus + health | Umami, protein, flavor |
| Fermented chili paste | 80 | +40 bonus + health | Capsaicin, antimicrobial |
| Fermented soybean paste | 150 | +60 bonus + health | Complete protein source |
| Olive oil | 30/L | +30 bonus | Calories, fat-soluble vitamins |
| Flatbread (as base) | 200-250 | Serves as meal container | Holds other ingredients |

#### Compatibility with Existing Meal System

- New meals use the same cooking recipe format as base game (`survival/recipes/cooking/`)
- Cooked meals stored in crocks/pots (base game system)
- Our preserved ingredients patched into existing stew/porridge recipes as valid inputs via `dependsOn` patches
- ExpandedFoods' simmering system (from ACulinaryArtillery) can be used for slow-cooked variants

---

### 1.5 Clay Griddle (Comal)

**Historical Basis:** The flat cooking surface is one of humanity's oldest and most universal cooking tools, developed independently by nearly every culture:
- **Mesoamerican comal** (~700 BCE) — flat clay disc for tortillas, roasting chilies, toasting seeds. The word comes from Nahuatl *comalli*. Clay throughout the pre-Columbian period (no metal required).
- **South Asian tava** (~2600 BCE clay, ~1500 BCE iron) — flat or slightly concave griddle for roti, chapati, dosa. Clearest material progression from clay to iron to steel.
- **Middle Eastern saj** — convex iron dome for paper-thin markook/lavash. Chosen by Bedouin specifically for portability over fixed clay ovens (*tabun*).
- **Polynesian hot stones** — heated basalt used as cooking surfaces. The oldest form of "griddle" — predates any manufactured cookware.
- **Roman craticula** — iron gridiron placed over hearth coals. Every *contubernium* (8-man military unit) carried portable cooking equipment.

#### Why This Block Exists

The base game has a gap between the **firepit** (open flame, spit roasting, heating pots) and the **clay oven** (enclosed dry-heat baking). ACulinaryArtillery fills part of this with vessels (cauldrons, saucepans, frying pans), but nothing provides a **flat dry-heat cooking surface**. The griddle sits in this gap:

| Capability | Firepit | Clay Griddle | Clay Oven |
|---|---|---|---|
| Flatbread / tortillas | No | **Yes** | Yes (but overkill) |
| Toast seeds & spices | No | **Yes** | No |
| Roast / blister chilies | No | **Yes** | No |
| Sear meat / fish (direct contact) | Spit only | **Yes** | No |
| Dry / toast dried coconut | No | **Yes** | No |
| Bake loaf bread | No | No | **Yes** |
| Multi-stage baking (hardtack) | No | No | **Yes** |
| Heat pots / containers | **Yes** | No | No |
| Cook stews / soups | **Yes** (in pot) | No | No |
| Pizza | No | No | **Yes** |

#### Block Specification

**Block name:** `seafarer:claygriddle`

**Variants by material (progression tiers):**

| Variant | Code | Age | Properties |
|---------|------|-----|------------|
| Clay griddle | `claygriddle-clay` | Stone age | Base cook speed. Fragile (can break if dropped). Clayforming recipe. |
| Stone griddle | `claygriddle-stone` | Stone age | Slower heating but better heat retention. Knapping recipe from granite/basalt. |
| Copper griddle | `claygriddle-copper` | Copper age | 1.5x cook speed. Slight concavity allows minimal oil use. Smithing recipe. |
| Bronze griddle | `claygriddle-bronze` | Bronze age | 1.75x cook speed. More durable. Smithing recipe. |
| Iron griddle | `claygriddle-iron` | Iron age | 2x cook speed. Enables light frying (oil-based recipes). Smithing recipe. |

**Block properties:**
```
Class: BlockGriddle (new class, extends BlockCookingContainer pattern)
Entity: GriddleEntity
Behaviors: HorizontalOrientable, Ignitable, RightClickPickup, GroundStorable
Placement: On top of firepit (stacks on lit firepit) OR freestanding on ground near heat source
Cooking slots: 4 (items placed visually on the flat surface)
Max temperature: 400°C (clay/stone), 600°C (metal variants)
Fuel: None — requires adjacent or underlying heat source (firepit, campfire, charcoal)
```

**Placement mechanic:** The griddle is placed on top of a lit firepit. It draws heat from the firepit below. Without a heat source underneath, it does nothing. This mirrors how a real comal sits over a fire — the griddle itself is not the heat source.

**Visual:** Flat disc or slightly concave surface. Items placed on it are visually displayed on the surface (similar to how items display on ground storage). Clay variant is tan/terracotta colored, stone is dark gray, metals have their material appearance.

#### Crafting Recipes

**Clay Griddle** (clayforming — stone age):
```
Clayforming recipe: flat disc shape from clay
Fire in pit kiln or regular kiln → claygriddle-clay
```

**Stone Griddle** (knapping — stone age):
```
Knapping recipe from granite or basalt block
Output: claygriddle-stone
```

**Copper Griddle** (smithing — copper age):
```
Smithing recipe: copper ingot → claygriddle-copper
```

**Bronze Griddle** (smithing — bronze age):
```
Smithing recipe: tinbronze/bismuthbronze/blackbronze ingot → claygriddle-bronze
```

**Iron Griddle** (smithing — iron age):
```
Smithing recipe: iron ingot → claygriddle-iron
```

#### Griddle-Exclusive Recipes

These recipes **require** the griddle — they cannot be made on a firepit or in an oven:

**Flatbread** (primary recipe):
```
Input:  flatbreaddough-{grain} (1) placed on griddle over lit firepit
Time:   ~30 seconds (clay) to ~15 seconds (iron)
Output: flatbread-{grain}
Props:  500h fresh, 200 satiety
```

**Tortilla** (corn flatbread):
```
Input:  tortilladough (1) placed on griddle
Time:   ~30 seconds (clay) to ~15 seconds (iron)
Output: tortilla
Props:  400h fresh, 180 satiety, +1 health (nixtamalization nutrition bonus)
```

**Toasted Seeds & Spices**:
```
Input:  seeds or spice item (1) placed on griddle
Time:   ~20 seconds
Output: toasted-{seed/spice} — enhanced nutrition, used as cooking ingredient
```

**Roasted Chilies**:
```
Input:  chili-{type} (1) placed on griddle
Time:   ~25 seconds
Output: roastedchili-{type} — blistered/charred, used in fermented chili paste or meals
```

**Seared Meat / Fish**:
```
Input:  any raw meat or fish item (1) placed on griddle
Time:   ~45 seconds (clay) to ~20 seconds (iron)
Output: seared-{meattype} — quick-cooked, moderate preservation (240h fresh)
Notes:  Faster than firepit spit roasting but produces a different product.
        Seared items have slightly less satiety than fully cooked but cook faster.
        Good for quick camp meals during travel.
```

**Dried Coconut Toasting**:
```
Input:  driedcoconut (1) placed on griddle
Time:   ~20 seconds
Output: toastedcoconut — enhanced flavor, used in cooking recipes and pemmican variants
```

#### Griddle-Optional Recipes (can also use oven)

These can be made on either griddle or oven, but the griddle is available earlier:

**Flatbread (twice-baked)** — oven only for second bake:
```
Step 1: flatbreaddough on griddle → flatbread (single bake)
Step 2: flatbread in clay oven → flatbread-twice (2,000h fresh, drier)
```

#### Metal Griddle Bonus — Light Frying

Copper/bronze/iron griddles unlock **oil-based griddle cooking** that clay and stone cannot do (oil would soak into porous clay):

**Oiled Flatbread** (copper+ griddle):
```
Input:  flatbreaddough-{grain} (1) + oliveoil or coconutoil (0.05L)
Time:   ~25 seconds
Output: flatbread-{grain}-oiled — 600h fresh, 250 satiety (richer, better preservation from oil)
```

**Pan-Seared Fish** (iron griddle):
```
Input:  fish-raw (1) + oliveoil or coconutoil (0.05L)
Time:   ~30 seconds
Output: panseared-fish — 200h fresh, 180 satiety (quick, flavorful preparation)
```

#### Integration with Ship Systems

The clay griddle is designed to be **ship-friendly**:
- Small footprint, single block
- `GroundStorable` and `RightClickPickup` behaviors — portable
- Can be placed on a ship's galley room firepit
- Low fuel requirements (uses existing firepit heat)
- Enables the core seafaring food loop: dried/salted provisions → rehydrate in stew on firepit → cook fresh flatbread on griddle → wrap with fermented condiments

#### Compatibility Notes

- **ACulinaryArtillery:** Their frying pan is a vessel (holds liquid/oil inside); our griddle is a surface (items sit on top). Different mechanics, no overlap. Their saucepan handles simmering recipes; our griddle handles dry-surface cooking.
- **ExpandedFoods:** Their recipes use oven and firepit. We patch their dough items to be griddle-cookable where appropriate (e.g., their hardtack-raw could optionally be started on a griddle).
- **Butchering:** Their smoking rack and our drying rack are vertical hanging; the griddle is horizontal surface cooking. Complementary.
- **CarryOn:** Patch griddle to be carryable via `dependsOn: carryon`.

---

## 2. New Crops & Plants

> **Design Philosophy:** Every crop earns its place by filling multiple gameplay roles — food, crafting material, trade good, and/or medicinal. Crops are grouped by the culture/biome they represent, supporting the mod's theme of cross-cultural seafaring trade. We focus on crops that are historically essential to coastal, island, and river-trading civilizations.

### What Already Exists

**Base game crops (17):** Amaranth, bell pepper, cabbage, carrot, cassava, flax, onion, parsnip, peanut, pineapple, rice, rye, soybean, spelt, sunflower, turnip, pumpkin

**Base game fruit trees (12):** Red/pink/yellow apple, cherry, peach, pear, orange, mango, olive, breadfruit, lychee, pomegranate

**Base game wild plants:** 5 berry bushes (blueberry, cranberry, red/black/white currant), saguaro cactus fruit, 36 mushroom species, 9 herbs (basil, chamomile, cilantro, lavender, marjoram, mint, saffron, sage, thyme), 4 aquatic plants (cattail, papyrus, tule, brown sedge)

**ExpandedFoods adds:** Seaweed (mash/washed/sheet/dried/edible), acorns, 11 nut types, 7 food oils (flax, rice, seed, soy, sunflower, peanut, olive), tree saps (birch, maple), resins, salt crystals. Also patches base game olives for oil pressing.

**Key findings:**
- **Olive tree EXISTS** in base game (evergreen fruit tree, mintemp 22°C). Olives harvested as vegetable. ExpandedFoods adds olive oil pressing. We do NOT need to add olive trees — we extend what's there.
- **Breadfruit EXISTS** in base game (tropical tree, mintemp 28°C). We can extend rather than add.
- **Olive oil EXISTS** in ExpandedFoods (`foodoilportion-olive`). We use it, not recreate it.
- **No spice crops** exist beyond herbs. No pepper, cinnamon, cloves, nutmeg, ginger, or vanilla.
- **No stimulant crops** exist. No tea, coca, or coffee.
- **No rope/fiber crop** beyond flax. No hemp, agave, or coir (coconut fiber).
- **No tropical staple crops** beyond cassava, rice, and pineapple. No corn, taro, sugar cane, banana.
- **No citrus** beyond orange. No lemons or limes.
- **No date palm** or desert-adapted fruit trees.

---

### 2.1 Core Crops (Original Plan + Essential Additions)

#### Chilies (Capsicum spp.) — DONE

**Cultures:** Mesoamerican (Maya, Aztec). Earliest evidence ~7500 BCE in Mexico. Spread globally post-1492.

**Why it matters:** Dried chilies last for years. Capsaicin is antimicrobial (helps preserve other foods). Lightweight, compact trade cargo. Used in nearly every seafaring culture's cuisine after the Columbian Exchange.

| Property | Value |
|----------|-------|
| Type | Ground crop (bush), annual |
| Climate | Tropical-warm (coldDamageBelow 8°C, heatDamageAbove 40°C) |
| Growth stages | 12 (similar to bell pepper) |
| Growth time | ~2 months |
| Nutrient | Nitrogen (N) |
| Harvest | `seafarer:chili` item (multiple variants: mild, hot, fiery?) |

**Products & uses:**
- Eat fresh (low satiety, minor health buff from capsaicin)
- Roast on clay griddle → `roastedchili` (cooking ingredient, enhanced flavor)
- Dry on drying rack → `driedchili` (long preservation, trade good, cooking spice)
- Smoke on smoking rack (butchering mod) → `smokedchili` / chipotle (historically: thick-walled chilies can't air-dry, MUST be smoked — Nahuatl *chilpoctli*)
- Ferment in barrel → `fermentedchilipaste` (see section 1.2)
- Cooking ingredient: adds +40 satiety bonus + health when used in meals

**Differs from bell pepper:** Bell pepper is mild, used as vegetable. Chilies are hot, used as spice/preservative/medicine. Different item category.

---

#### Coconut Palm (Cocos nucifera)

**Cultures:** Polynesian, Southeast Asian, South Asian. One of the key "canoe plants" — Austronesian sailors spread coconuts across the entire Pacific. Coconuts float and naturally colonize coastlines.

**Why it matters:** The ultimate multi-use seafaring plant. Fresh water, high-calorie food, oil for cooking/waterproofing, coir fiber for rope, shell for containers, wood for construction.

| Property | Value |
|----------|-------|
| Type | Tall palm tree (fruit tree system) |
| Climate | Tropical (mintemp 26°C, maxtemp 50°C) |
| Cycle | Evergreen |
| Propagation | Planting whole coconut (not cutting) |
| Fruit yield | avg 4, var 3 |
| Time to fruit | Long (equivalent to 1+ year in-game) |
| Harvest | `seafarer:coconut` item |

**Products & uses (8+ products from one tree):**

| Product | Process | Use |
|---------|---------|-----|
| Coconut water | Crack open fresh coconut | Drink (hydration, minor satiety) |
| Coconut meat | Crack open fresh coconut | Food (high satiety, cooking ingredient) |
| Dried coconut (copra) | Dry meat on drying rack | Long-lasting food, oil extraction input |
| Toasted coconut | Toast copra on griddle | Cooking ingredient, pemmican variant |
| Coconut oil | Press copra (barrel or press) | Cooking oil, lamp fuel, waterproofing, medicine |
| Coconut milk | Barrel recipe: grated meat + water | Cooking liquid (stews, curries) |
| Coir fiber | Process husk (grid recipe) | Rope crafting material (alternative to flax/hemp) |
| Coconut shell | Byproduct of cracking | Bowl/container (small ground-storable vessel) |
| Palm wood | Chop tree | Building material |

**Note:** Coconut oil should use the same `foodoilportion` liquid system as ExpandedFoods' existing oils. Add as variant: `foodoilportion-coconut`.

---

#### Corn / Maize (Zea mays) — DONE

**Cultures:** Mesoamerican (Maya, Aztec — the Popol Vuh says humans were made from corn). Supported massive coastal and riverine civilizations.

**Why it matters:** Sacred staple crop. Dried corn stores indefinitely. Ground into flour for tortillas, fermented into chicha beer. Nixtamalization (cooking with wood ash lye) unlocks niacin — without it, corn-dependent populations develop pellagra.

| Property | Value |
|----------|-------|
| Type | Tall ground crop (stalk), annual |
| Climate | Warm temperate-tropical (coldDamageBelow 6°C, heatDamageAbove 42°C) |
| Growth stages | 10 |
| Growth time | ~2.5 months |
| Nutrient | Nitrogen (N) |
| Harvest | `seafarer:corn` grain item |

**Products & uses:**
- Raw grain (low nutrition if not nixtamalized — possible debuff for eating raw corn long-term?)
- Nixtamalization: corn + wood ash + water → nixtamal (barrel, 24h seal) → grind to masa → tortillas on griddle
- Corn flour: grind raw corn (for non-nixtamalized recipes)
- Chicha: fermented corn beverage (barrel recipe, see Alcohol section)
- Corn husks: wrapping material for tamales or food storage
- Animal feed

**Unique mechanic:** Nixtamalization chain makes corn more complex than other grains but more rewarding. The wood ash requirement ties into existing firepit/charcoal system.

---

#### Figs (Ficus carica)

**Cultures:** Mediterranean, Middle Eastern. One of the oldest cultivated fruits (~9400 BCE, older than grain agriculture).

**Why it matters:** Dried figs are among the oldest preserved foods. High sugar content (63%+ when dried) means they dry naturally and last months to years. Calorie-dense, portable, nutritious. Major Mediterranean trade commodity.

| Property | Value |
|----------|-------|
| Type | Fruit tree, deciduous |
| Climate | Mediterranean-warm (mintemp 10°C, maxtemp 40°C) |
| Cycle | Deciduous (vernalization required) |
| Propagation | Cutting |
| Fruit yield | avg 6, var 4 |
| Harvest | `seafarer:fig` fruit item |

**Products & uses:**
- Eat fresh (moderate satiety, +1 health)
- Dry on drying rack → `driedfig` (very long preservation: 8,000h+, high satiety)
- Cooking ingredient (stews, porridge, flatbread accompaniment)
- Preserve in alcohol (see section 3)
- Excellent sailor provisions alongside hardtack and dried meat

---

#### Lemons & Limes (Citrus)

**Cultures:** Southeast Asian origin, spread by Arab traders to Mediterranean. British Royal Navy mandated lime juice rations — hence "limeys."

**Why it matters:** **Scurvy prevention** is the defining maritime use. Citric acid is antimicrobial — used for food preservation and cleaning. Juice as flavoring and cooking acid.

| Property | Value |
|----------|-------|
| Type | Small evergreen fruit tree |
| Climate | Subtropical-tropical (lemon: mintemp 18°C; lime: mintemp 22°C) |
| Cycle | Evergreen |
| Propagation | Cutting |
| Fruit yield | avg 6, var 4 |
| Harvest | `seafarer:lemon` / `seafarer:lime` fruit items |

**Products & uses:**
- Eat fresh (low satiety, +2 health — anti-scurvy)
- Juice: press for `lemonjuiceportion` / `limejuiceportion` liquid
  - Drink: hydration + health buff (anti-scurvy mechanic)
  - Cooking acid: use in barrel recipes for ceviche-style preservation (acid-curing fish)
  - Cleaning: minor use
- Preserved lemons: salt + lemon in barrel → long-lasting condiment (Mediterranean/Middle Eastern tradition)
- Trade good (moderate value, high demand in cold climates where scurvy is a risk)

**Anti-scurvy mechanic:** Long voyages without citrus/fermented vegetables cause a gradual health debuff (scurvy). Consuming citrus or fermented vegetables prevents/cures it. Ties into hydration/exposure system (section 8).

---

### 2.2 Ginger & Stimulant Crops

#### Ginger (Zingiber officinale) — DONE

**Cultures:** Austronesian (Maritime Southeast Asia origin). In the 5th century, ginger was grown in pots ON SHIPS to provide fresh food and prevent scurvy. Medicinally treats nausea/seasickness — perhaps the most directly "seafaring" plant.

| Property | Value |
|----------|-------|
| Type | Ground crop, grows from rhizome |
| Climate | Tropical-subtropical (coldDamageBelow 10°C, heatDamageAbove 44°C) |
| Growth stages | 8 |
| Growth time | ~3 months |
| Nutrient | Potassium (K) |
| Harvest | `seafarer:ginger` root item |

**Products & uses:**
- Fresh ginger root: cooking ingredient, anti-nausea medicine
- Dried ginger: drying rack → long-lasting spice, trade good
- Ginger tea: ginger + hot water → **anti-seasickness buff** (unique thematic mechanic)
- Candied ginger: ginger + sugar/honey → preserved sweet, sailor's treat
- Pickled ginger: barrel recipe → sushi accompaniment, long preservation
- Cooking spice: +30 sat bonus in meals
- **Can be grown in pots on ships** — historically accurate, ties into ship grow beds (section 7)

---

#### Coca (Erythroxylum coca) — DONE

**Cultures:** Andean (Inca, pre-Inca, 8,000+ years). Leaves chewed with lime (calcium hydroxite) for endurance. Workers and messengers relied on it for long treks. Public officials paid in coca leaves. Extremely high cultural/economic value.

| Property | Value |
|----------|-------|
| Type | Shrub (1-3m tall), perennial |
| Climate | Tropical highland/warm valley (coldDamageBelow 12°C, heatDamageAbove 38°C) |
| Growth stages | 6 |
| Growth time | ~2 months to first harvest, then 3-4 harvests per year |
| Nutrient | Nitrogen (N) |
| Harvest | `seafarer:cocaleaf` — leaves picked, bush keeps producing |

**Products & uses:**
- Chewed leaf (with lime/wood ash): **temporary buff** — stamina boost + hunger suppression + slight speed boost, followed by mild fatigue debuff when it wears off. Equivalent to a strong cup of coffee historically.
- Coca tea: milder, longer-lasting buff + minor health regen (medicinal)
- **Very high trade value** per weight — ideal lightweight cargo
- Grows only in specific warm biomes — trade incentive

**Unique mechanic:** Multiple harvests from same bush per growing season (historically accurate — 3-4 harvests/year). The leaf is chewed with lime (grid recipe: cocaleaf + calcium from wood ash or ground limestone) to activate.

---

#### Tea (Camellia sinensis) — DONE

**Cultures:** Chinese, Japanese, East Asian. Tea literally shaped maritime history — clipper ships were designed to race tea from China to London. The East India Company held a tea monopoly. Tea trade spawned the Opium Wars.

| Property | Value |
|----------|-------|
| Type | Evergreen shrub, leaves harvested repeatedly |
| Climate | Subtropical (coldDamageBelow 4°C, heatDamageAbove 36°C, prefers humid hills) |
| Growth stages | 6 |
| Growth time | ~3 months to first harvest, then continuous |
| Nutrient | Phosphorus (P) |
| Harvest | `seafarer:tealeaf` — leaves picked, bush keeps producing |

**Products & uses:**
- Green tea: dry leaves (minimal processing) → brew with hot water → mild stamina/focus buff
- Black tea: ferment leaves (oxidation) → dry → brew → stronger buff, longer lasting
- Tea as trade good: dried tea leaves are lightweight and extremely valuable
- Cooking ingredient: minor use in some recipes
- **Boiled water bonus:** Tea requires boiled water, which is also safe to drink — ties into hydration system

**Processing:**
- Fresh leaves → dry on drying rack → green tea (simple)
- Fresh leaves → ferment (barrel, short time ~48h) → dry → black tea (better buff, higher trade value)

---

#### Sugar Cane (Saccharum officinarum) — DONE

**Cultures:** Austronesian (originated New Guinea, spread by canoe voyagers ~3000 BP). By the 1700s, sugar was THE most important internationally traded commodity. Drove Caribbean colonization. Rum was the universal sailor's drink.

| Property | Value |
|----------|-------|
| Type | Tall perennial grass/cane, harvested by cutting stalks |
| Climate | Tropical (coldDamageBelow 10°C, heatDamageAbove 48°C) |
| Growth stages | 8 |
| Growth time | ~4 months |
| Nutrient | Potassium (K) |
| Harvest | `seafarer:sugarcane` stalk item |

**Products & uses:**
- Raw cane: chew for small satiety/energy boost
- Sugar: process cane (press + boil) → `seafarer:sugar` item
  - Cooking ingredient (jams, candied fruit, baking)
  - Food preservation (sugar preserves fruit, similar to salt preserving meat)
  - Trade good (high value)
- Molasses: byproduct of sugar processing → cooking, animal feed
- **Rum:** Ferment molasses or cane juice (barrel recipe) → distill → rum (see Alcohol section)
- Bagasse (crushed cane): fuel source

---

### 2.4 Fiber & Rope Crops

> **Critical for seafaring.** A fully-rigged Age of Sail ship carried 50-100 TONS of hemp rope and canvas. The base game has flax for linen, but a seafaring mod needs dedicated rope/sail fiber crops.

#### Hemp (Cannabis sativa — fiber cultivar) — DONE

**Cultures:** Universal maritime cultures. Columbus sailed on hemp sails. Britain's Royal Navy got 90% of its rope from Russian hemp. The word "canvas" comes from "cannabis."

| Property | Value |
|----------|-------|
| Type | Tall annual stalk crop, fast-growing |
| Climate | Temperate-subtropical (coldDamageBelow -4°C, heatDamageAbove 38°C — very adaptable) |
| Growth stages | 8 |
| Growth time | ~1.5 months (fast!) |
| Nutrient | Nitrogen (N) |
| Harvest | `seafarer:hempstalk` + `seafarer:hempseed` |

**Products & uses:**
- Hemp fiber: process stalks (retting in water barrel, then break/beat) → `seafarer:hempfiber`
  - Rope crafting (stronger/more durable than flax rope)
  - Canvas/sailcloth (for sails — key ship construction material)
  - Clothing, bags, sacks
- Hemp seed: food (grain-like, moderate satiety), hemp seed oil (pressing)
- **Essential for ship building** — provides the primary rope and sail material

**Design note:** This is arguably the most important crop in the mod for enabling the shipbuilding gameplay loop. Without a dedicated rope fiber crop, large ships can't be built or maintained.

---

#### Agave (Agave spp.) — DONE

**Cultures:** Mesoamerican. The maguey was "one of the most sacred and important plants in ancient Mexico." Fiber, food, drink, needles, and paper from one plant.

| Property | Value |
|----------|-------|
| Type | Large succulent rosette, slow-growing, flowers once then dies |
| Climate | Arid-subtropical (coldDamageBelow 2°C, heatDamageAbove 50°C — desert adapted) |
| Growth stages | 6 (slow growth, 1+ year equivalent) |
| Growth time | Very long |
| Nutrient | None (grows in poor soil) |
| Harvest | `seafarer:agaveleaf` (leaves) or `seafarer:agaveheart` (destructive harvest of piña) |

**Products & uses:**
- Sisal fiber: process leaves → `seafarer:agavefiber` — rope crafting (alternative to hemp in arid biomes)
- Pulque: tap sap from living plant (aguamiel) → ferment → mildly alcoholic drink
- Roasted heart (piña): destructive harvest, cook in pit → sweet, high-satiety food
- Needles: byproduct, usable as sewing needles or small tools
- **Fills the arid biome niche** — grows where nothing else will, provides rope fiber in desert coastal areas

---

### 2.5 Tropical Staple Crops

#### Taro (Colocasia esculenta) — DONE

**Cultures:** Polynesian (kalo — considered THE most important canoe crop). Hawaiian creation myth says taro is the elder brother of humanity.

**Why it matters:** Grows in flooded/wet conditions where other crops can't. Starchy corm is a calorie-dense staple. Leaves also edible (cooked). Fermented into poi (preserved paste).

| Property | Value |
|----------|-------|
| Type | Ground crop, grows in wet/flooded conditions |
| Climate | Tropical-subtropical (coldDamageBelow 10°C, heatDamageAbove 44°C) |
| Growth stages | 8 |
| Growth time | ~3 months |
| Nutrient | Potassium (K) |
| Special | **Requires waterlogged soil** (unique — grows in swamps, marshes, flooded paddies like rice) |
| Harvest | `seafarer:taro` corm + `seafarer:taroleaf` |

**Products & uses:**
- Cooked taro: boil or roast → high satiety starchy food (comparable to potato)
- Taro leaf: cook (must be cooked — raw is toxic) → vegetable
- Poi: mash cooked taro → ferment (barrel, 72h) → `seafarer:poi` — preserved paste, lasts very long, Polynesian staple
- Taro flour: dry and grind → baking ingredient

**Unique mechanic:** Wetland crop — only grows in waterlogged soil blocks. Fills a farming niche no other crop covers. Encourages players to farm coastal marshes and river deltas.

---

#### Date Palm (Phoenix dactylifera)

**Cultures:** Middle Eastern, North African. Cultivated since ~7000 BCE in Mesopotamia. Dried dates were "desert bread."

**Why it matters:** Dried dates have 63-64% sugar content → near-indefinite shelf life. Perfect ship provisions. One of the best dried-food-per-weight ratios of any fruit.

| Property | Value |
|----------|-------|
| Type | Tall palm tree |
| Climate | Arid-subtropical (mintemp 18°C, maxtemp 50°C — hot, dry) |
| Cycle | Evergreen |
| Propagation | Seed or offshoot |
| Fruit yield | avg 8, var 4 (high yield) |
| Harvest | `seafarer:date` fruit item |

**Products & uses:**
- Fresh dates: high satiety, perishable
- Dried dates: dry on rack → **extremely long preservation** (12,000h+ target — longest of any food), high satiety, the ultimate voyage provision
- Date syrup: press → liquid sweetener (alternative to honey)
- Date wine: ferment → alcoholic beverage (Middle Eastern/North African tradition)
- Palm fronds: thatching material, weaving

---

#### Banana / Plantain (Musa spp.)

**Cultures:** Southeast Asian origin, Polynesian canoe crop. Spread to Africa and Americas by Austronesian voyagers.

| Property | Value |
|----------|-------|
| Type | Large herbaceous plant (dies after fruiting, new shoots regrow) |
| Climate | Tropical (coldDamageBelow 12°C, heatDamageAbove 48°C) |
| Cycle | Single fruit bunch, then new shoot |
| Propagation | Shoot/sucker (not seed) |
| Fruit yield | avg 6, var 3 |
| Harvest | `seafarer:banana` (sweet) / `seafarer:plantain` (starchy, must be cooked) |

**Products & uses:**
- Fresh banana: eat raw, moderate satiety, perishable
- Cooked plantain: must be cooked (griddle, firepit, or oven) → high satiety starchy food
- Dried banana: drying rack → long preservation
- Banana leaf: harvested from plant → food wrapping (extends preservation of wrapped items), cooking wrapper (tamale-style steaming)
- Banana wine: ferment → mild alcohol

---

### 2.6 Extensions to Existing Plants

#### Seaweed / Kelp (Extending ExpandedFoods)

ExpandedFoods already adds seaweed with mash/washed/sheet/dried/edible variants. We extend it:

**What we add:**
- Seaweed farming mechanic: place seaweed on underwater blocks in coastal water, grows and can be harvested
- Kelp variety for cold water (ExpandedFoods seaweed + our kelp = coverage of all climates)
- Seaweed as fertilizer: compost seaweed for soil nutrient restoration
- Seaweed wrapping: wrap food items for minor preservation bonus (historical Japanese/Korean practice)

#### Bamboo (Already in Base Game)

Base game already has bamboo (leaves, fences, gates, bamboo shoots as food). We can extend with patches:
- Bamboo trellises for climbing crops (if pepper vines added later)
- Bamboo as ship construction material (lighter alternative to logs)

---

### 2.7 Crop Summary Table (15 New Crops)

| Crop | Type | Climate | Primary Role | Secondary Role |
|------|------|---------|-------------|----------------|
| **Chili** | Ground crop | Tropical-warm | Spice, preservation | Medicine, trade |
| **Coconut** | Palm tree | Tropical | Food, oil, rope (coir) | Water, fuel, building |
| **Corn** | Ground crop | Warm-tropical | Grain (tortillas) | Alcohol (chicha), feed |
| **Fig** | Fruit tree | Mediterranean | Dried provisions | Trade |
| **Lemon** | Fruit tree | Subtropical | Anti-scurvy, acid | Preservation, trade |
| **Lime** | Fruit tree | Subtropical | Anti-scurvy, acid | Preservation, trade |
| **Ginger** | Ground crop | Tropical-sub | Anti-nausea, spice | Medicine, tea, trade |
| **Coca** | Shrub | Tropical | Stimulant buff | Trade, medicine |
| **Tea** | Shrub | Subtropical | Beverage buff | Trade (very high value) |
| **Sugar Cane** | Tall grass | Tropical | Sugar, rum | Preservation, trade |
| **Hemp** | Stalk crop | Temperate-sub | Rope, sails, canvas | Seed oil, food |
| **Agave** | Succulent | Arid | Rope fiber, pulque | Food (roasted heart) |
| **Taro** | Wetland crop | Tropical-sub | Starchy staple | Poi (preserved paste) |
| **Date Palm** | Palm tree | Arid | Dried provisions | Wine, syrup, thatch |
| **Banana** | Herbaceous | Tropical | Food staple | Leaf wrapping, wine |

### 2.8 What We Extend (Not New)

These already exist in base game or mods. We add new recipes/uses, not new plants:

| Plant | Exists In | What We Add |
|-------|-----------|-------------|
| **Olive** | Base game (tree + vegetable) | Fermented olive recipe (section 1.2), extend oil uses |
| **Breadfruit** | Base game (tropical tree) | Dried breadfruit recipe, pit fermentation (masi) |
| **Soybean** | Base game (crop) | Fermented soybean paste (section 1.2) |
| **Rice** | Base game (crop) | Rice wine, tapai fermentation |
| **Bamboo** | Base game (plant + shoots) | Extend with ship construction uses, trellises |
| **Seaweed** | ExpandedFoods | Farming mechanic, kelp variant, fertilizer |
| **Flax** | Base game (crop) | Linen sails (complementary to hemp canvas) |
| **All base herbs** | Base game (9 herbs) | Use as cooking ingredients in our recipes |
| **All base fruits** | Base game (12 trees) | Preserve in alcohol recipes (section 3) |

---

### 2.9 Derived Products Summary

| Product | Source Crop | Process | Section |
|---------|-----------|---------|---------|
| Olive oil | Olive (exists) | Press (ExpandedFoods) | Uses in 1.3, 1.4, 1.5 |
| Coconut oil | Coconut (new) | Dry → press | 2.1 |
| Hemp rope | Hemp (new) | Ret → break → twist | 2.4, 6.x |
| Hemp canvas/sails | Hemp (new) | Fiber → weave | 2.4, 6.x |
| Coir rope | Coconut (new) | Process husk | 2.1 |
| Agave fiber/rope | Agave (new) | Process leaves | 2.4 |
| Sugar | Sugar cane (new) | Press → boil | 2.3 |
| Molasses | Sugar cane (new) | Byproduct of sugar | 2.3 |
| Masa (corn flour) | Corn (new) | Nixtamalize → grind | 1.3, 2.1 |
| Dried chilies | Chili (new) | Drying rack | 1.1, 2.1 |
| Chipotle | Chili (new) | Smoking rack (butchering) | 2.1 |
| Poi | Taro (new) | Cook → mash → ferment | 2.5 |
| Green/black tea | Tea (new) | Dry / ferment+dry | 2.2 |
| Dried dates | Date palm (new) | Drying rack | 2.5 |
| Dried figs | Fig (new) | Drying rack | 2.1 |
| Ginger tea | Ginger (new) | Ginger + hot water | 2.2 |
| Banana leaf wrap | Banana (new) | Harvest leaves | 2.5 |

---

## 3. Alcohol & Preserved Fruits

> **Design Philosophy:** The base game and ExpandedFoods already provide a comprehensive alcohol system — cider/wine fermentation, multi-stage aging, spirit distillation, and named variants (brandy, whiskey, vodka, sake). SaltAndSand should **not** reinvent this. Instead, we add only the drinks tied to our new crops and seafaring theme, plus alcohol-based fruit preservation that nobody covers yet.

### What Already Exists Across Base Game + Mods

| System | Provider | Details |
|--------|----------|---------|
| **Cider/Wine fermentation** | Base game | Fruit juice → cider (168h seal), grain + water → grain cider (336h seal), honey → mead (336h seal) |
| **Wine aging** | `expandedfoods` | Cider cures → strong wine (1,008h) → potent/fine wine (2,016h). Spoils to vinegar if not stored cool |
| **Spirit distillation** | Base game | Cider → spirit (0.05-0.1 ratio). Spirit → pure alcohol (0.5 ratio) |
| **Spirit aging** | `expandedfoods` | Spirit cures → strong/aged spirit → potent/vintage spirit. Named variants: Brandy (fruit), Whiskey (rye/amaranth), Vodka (spelt/cassava), Sake (rice), Double Mead |
| **Intoxication** | Base game | Per-litre property — cider 0.15, spirit 1.5, strong spirit 2.0, potent spirit 2.5, pure alcohol 3.0 |
| **Vinegar** | `expandedfoods` | Wine → vinegar (336h barrel), fruit → vinegar (336h), fast vinegar with starter (168h) |
| **Dried fruit** | `expandedfoods` | Transition-based drying for all fruits. Grid recipe or press byproduct |
| **Candied fruit** | `expandedfoods` | Simmer fruit + fruit syrup (200°C). Medium-long shelf life |
| **Fruit syrup** | `expandedfoods` | Simmer water + fruit (150°C, 48h). 17,520h shelf life |
| **Compote** | `expandedfoods` | Cooking meal — fruit syrup base + fruits + optional wine/honey/nuts |
| **Jam** | Base game | Cooking pot — honey + fruit. 1,080h fresh |
| **Gelatin** | `expandedfoods` | Multi-stage bone broth chain. Can be flavored with any juice/syrup |
| **Vinegar pickling** | `expandedfoods` | Vinegar + vegetable/egg/legume (336h barrel seal) |

### What's Missing — Our Niche

The existing systems cover European-style wines, grain beers, distilled spirits, dried fruit, candied fruit, and vinegar preservation thoroughly. What they **don't** cover:

1. **New World & tropical alcohols** — rum, chicha (corn beer), pulque (agave), coconut toddy/arrack, coca wine
2. **Sailor's drinks** — grog (watered rum), switchel (vinegar + ginger drink), shrub (drinking vinegar)
3. **Alcohol-preserved fruit** — rumtopf, brandied cherries, fruits in spirits (no mod covers this)
4. **Salt-preserved citrus** — Moroccan preserved lemons (ties into our salting system)
5. **Fermented fruit drinks** — tepache (fermented pineapple/fruit rind), tuba (coconut water wine)

---

### 3.1 New Alcoholic Beverages

> We add only drinks that require our new crops or serve a seafaring gameplay purpose. We do **not** add new wines, beers, or spirits that ExpandedFoods already covers.

#### Rum
- **Source**: Sugar cane (Section 2.2) — our crop, our drink
- **Pipeline**: Sugar cane → press → cane juice. Two paths:
  - **Light rum**: Cane juice + water in barrel (336h seal) → `seafarer:ciderportion-cane` → distill → `seafarer:spiritportion-rum`
  - **Dark rum**: Cane juice → boil → molasses. Molasses + water in barrel (336h seal) → `seafarer:ciderportion-molasses` → distill → `seafarer:spiritportion-darkrum`
- **Aging**: Uses ExpandedFoods' existing cure/aging transitions — rum ages like any spirit (strong → potent/vintage)
- **Intoxication**: Same as base game spirit (1.5/L), aging increases it
- **Historical context**: Caribbean rum trade drove the entire Atlantic triangle trade. Naval rations included daily rum allowance.

```json5
// Barrel recipe: Light rum wash
{
  code: "seafarer:canewash",
  sealHours: 336,
  ingredients: [
    { type: "item", code: "seafarer:cane-juice", litres: 5, consumeLitres: 5 },
    { type: "item", code: "waterportion", litres: 5, consumeLitres: 5 }
  ],
  output: { type: "item", code: "seafarer:ciderportion-cane", litres: 5 }
}
```

#### Chicha (Corn Beer)
- **Source**: Corn (Section 2.1) — traditional Mesoamerican fermented beverage
- **Pipeline**: Corn flour + water in barrel (336h seal) → `seafarer:ciderportion-corn`
- **Alternative historical method**: Chewed corn (masticación) — simplified to corn + saliva substitute (could use the existing honey as enzyme source for gameplay)
- **Gameplay**: Low intoxication (0.15/L like cider), high satiety — it's a food-drink hybrid. Sailors drank chicha because it provided calories AND hydration.
- **Historical context**: Inca Empire's most important ceremonial drink. Brewed by chosen women (acllas). Consumed daily across Mesoamerica and Andes.

```json5
// Barrel recipe: Chicha
{
  code: "seafarer:chicha",
  sealHours: 336,
  ingredients: [
    { type: "item", code: "seafarer:flour-corn", quantity: 4 },
    { type: "item", code: "waterportion", litres: 10, consumeLitres: 10 }
  ],
  output: { type: "item", code: "seafarer:ciderportion-corn", litres: 10 }
}
```

#### Pulque (Fermented Agave)
- **Source**: Agave (Section 2.4) — heart/piña yields aguamiel (honey water)
- **Pipeline**: Agave heart → press/scrape → aguamiel. Aguamiel in barrel (168h seal) → `seafarer:ciderportion-agave` (pulque)
- **Properties**: Low intoxication (0.15/L), high satiety (100/L), provides Fruit nutrition. Spoils fast — must be consumed quickly or distilled.
- **Distillation**: Pulque → distill → `seafarer:spiritportion-mezcal` (historical precursor to tequila)
- **Historical context**: Sacred Aztec drink, called "the drink of the gods." Only elders and priests allowed to drink freely. Aguamiel was also consumed fresh as a nutritious non-alcoholic beverage.

#### Coconut Toddy & Arrack
- **Source**: Coconut palm (Section 2.1) — sap tapping
- **Pipeline**: Coconut palm → tap with knife (like resin tapping) → coconut sap. Sap in barrel (168h seal) → `seafarer:ciderportion-coconut` (toddy)
- **Distillation**: Toddy → distill → `seafarer:spiritportion-arrack`
- **Properties**: Toddy — low intoxication, mild nutrition. Arrack — standard spirit intoxication.
- **Historical context**: Southeast Asian and Indian Ocean staple. Arab and Portuguese traders spread arrack across maritime trade routes. Dutch VOC traded arrack as currency.

#### Grog (Watered Rum)
- **Source**: Rum (above) + water — the classic naval ration drink
- **Pipeline**: Barrel recipe — spirit (rum or any spirit) + water + optional lime juice
- **Properties**: Lower intoxication than straight rum (~0.5/L), provides hydration, lime variant gives anti-scurvy benefit
- **Historical context**: Admiral Vernon ordered rum diluted with water in 1740 to reduce drunkenness. Lime added later — this is where "limey" comes from. Standard Royal Navy ration until 1970.
- **Gameplay purpose**: Makes rum stretch further, provides a practical daily-use drink for sea voyages. The lime variant ties into our citrus crops (Section 2.1) and scurvy prevention.

```json5
// Barrel recipe: Grog
{
  code: "seafarer:grog",
  sealHours: 1, // instant mix, no fermentation needed
  ingredients: [
    { type: "item", code: "game:spiritportion-*", litres: 1, consumeLitres: 1 },
    { type: "item", code: "waterportion", litres: 3, consumeLitres: 3 }
  ],
  output: { type: "item", code: "seafarer:grogportion", litres: 4 }
}
// Variant with lime
{
  code: "seafarer:grog-lime",
  sealHours: 1,
  ingredients: [
    { type: "item", code: "game:spiritportion-*", litres: 1, consumeLitres: 1 },
    { type: "item", code: "waterportion", litres: 3, consumeLitres: 3 },
    { type: "item", code: "seafarer:fruit-lime", quantity: 1 }
  ],
  output: { type: "item", code: "seafarer:grogportion-lime", litres: 4 }
}
```

#### Switchel (Haymaker's Punch)
- **Source**: Vinegar (ExpandedFoods) + ginger (Section 2.2) + honey (base game) + water
- **Pipeline**: Barrel recipe — mix ingredients (no fermentation, short seal)
- **Properties**: Non-alcoholic (or very low). Provides hydration, minor healing from ginger, energy from honey. Think of it as a working-man's energy drink.
- **Historical context**: Caribbean and American colonial staple. Field workers and sailors drank switchel because it rehydrated better than plain water. Vinegar provided electrolytes.
- **Gameplay purpose**: Non-alcoholic option for hydration system (Section 8). Uses our ginger + existing vinegar and honey.

```json5
// Barrel recipe: Switchel
{
  code: "seafarer:switchel",
  sealHours: 24,
  ingredients: [
    { type: "item", code: "expandedfoods:vinegarportion", litres: 0.5, consumeLitres: 0.5 },
    { type: "item", code: "seafarer:ginger", quantity: 1 },
    { type: "item", code: "honeyportion", litres: 1, consumeLitres: 1 },
    { type: "item", code: "waterportion", litres: 5, consumeLitres: 5 }
  ],
  output: { type: "item", code: "seafarer:switchelportion", litres: 5 }
}
```

#### Summary: New Beverages

| Beverage | Source Crop | Type | Intoxication | Our Crop? | Seal Time |
|----------|-----------|------|-------------|-----------|-----------|
| **Light Rum** | Sugar cane | Spirit (distilled) | 1.5/L | Yes | 336h wash |
| **Dark Rum** | Molasses | Spirit (distilled) | 1.5/L | Yes | 336h wash |
| **Chicha** | Corn | Cider/beer | 0.15/L | Yes | 336h |
| **Pulque** | Agave | Cider/wine | 0.15/L | Yes | 168h |
| **Mezcal** | Agave (pulque) | Spirit (distilled) | 1.5/L | Yes | — |
| **Coconut Toddy** | Coconut | Cider/wine | 0.15/L | Yes | 168h |
| **Arrack** | Coconut (toddy) | Spirit (distilled) | 1.5/L | Yes | — |
| **Grog** | Rum + water | Mixed drink | ~0.5/L | Partial | 1h (mix) |
| **Switchel** | Ginger + vinegar | Non-alcoholic | 0 | Partial | 24h (mix) |

**Design notes:**
- All fermented drinks output as `ciderportion-*` variants so they automatically work with ExpandedFoods' aging system (strong wine → potent wine) and the base game's distillation system
- All distilled drinks output as `spiritportion-*` variants for the same reason — they'll age through ExpandedFoods' strong → vintage pipeline without extra work
- Grog and switchel are mixed drinks, not fermented — short/instant seal times, new item types
- We do **not** add beer, wine, brandy, whiskey, vodka, sake, or mead — these all exist already

---

### 3.2 Preserved Fruit in Alcohol

> Nobody covers this yet. Preserving fruit in spirits is one of the oldest maritime preservation techniques — ships carried brandied cherries, rum-soaked raisins, and rumtopf (mixed fruit in rum) as both food and morale.

#### Rumtopf (Fruit in Rum/Spirits)
- **Concept**: Layer seasonal fruits in a vessel with sugar and spirits. Seal and wait. The alcohol preserves the fruit for months/years.
- **Pipeline**: Barrel recipe — spirit (any) + sugar/honey + fruit → preserved fruit in alcohol
- **Shelf life**: 8,760h (1 year) — among the longest non-dried preservation times
- **Output**: `seafarer:preservedfruit-{fruit}` — edible as-is (minor intoxication) or used as cooking ingredient

```json5
// Barrel recipe: Preserved fruit in spirits (rumtopf-style)
{
  code: "seafarer:preservedfruit",
  sealHours: 672, // 28 days — fruit absorbs alcohol
  ingredients: [
    { type: "item", code: "game:spiritportion-*", litres: 2, consumeLitres: 2 },
    { type: "item", code: "honeyportion", litres: 1, consumeLitres: 1 },
    { type: "item", code: "fruit-*", name: "fruit", quantity: 4 }
  ],
  output: { type: "item", code: "seafarer:preservedfruit-{fruit}", stackSize: 4 }
}
```

**Properties:**
- Satiety: 120 per item (fruit nutrition + sugar + alcohol calories)
- Food category: Fruit
- Intoxication: 0.05 per item (trace alcohol)
- Fresh hours: 8,760 (1 year)
- Can be used in: pies, compote (extends ExpandedFoods' compote), fruitcake, desserts

#### Brandied Cherries / Brandied Fruit
- **Variant of rumtopf** using specifically brandy (fruit spirit) for higher-quality preservation
- Same barrel recipe but requires `spiritportion-cherry`, `spiritportion-peach`, etc. (matching fruit to spirit)
- **Bonus**: Matched fruit+spirit gives +50% shelf life (13,140h) — the historically accurate approach
- Output: `seafarer:brandiedfruit-{fruit}`

#### Preserved Lemons (Salt + Citrus)
- **Ties into our salting system** (Section 1.1) rather than the alcohol system
- **Pipeline**: Barrel recipe — salt + lemon + lemon juice → preserved lemons
- **Shelf life**: 8,760h (1 year)
- **Historical context**: Moroccan/North African staple. Used across Mediterranean and Indian Ocean trade routes as both food and scurvy prevention.
- **Cooking use**: Adds flavor bonus when used in stews, fish dishes, flatbread wraps. Acts as a "condiment multiplier" like garum (Section 1.2).

```json5
// Barrel recipe: Preserved lemons
{
  code: "seafarer:preservedlemon",
  sealHours: 504, // 21 days
  ingredients: [
    { type: "item", code: "saltportion", litres: 2, consumeLitres: 2 },
    { type: "item", code: "seafarer:fruit-lemon", quantity: 4 },
    { type: "item", code: "seafarer:juiceportion-lemon", litres: 2, consumeLitres: 2 }
  ],
  output: { type: "item", code: "seafarer:preservedlemon", stackSize: 4 }
}
```

#### Preserved Limes
- Same process as preserved lemons but with limes
- **Gameplay**: Preserved limes are the definitive anti-scurvy provision — long-lasting citrus for ocean voyages
- **Shelf life**: 8,760h (1 year)

#### Summary: Preserved Fruit Items

| Item | Method | Seal Time | Shelf Life | Cooking Use |
|------|--------|-----------|------------|-------------|
| **Preserved fruit** (rumtopf) | Spirit + honey + fruit | 672h | 8,760h | Pies, compote, desserts |
| **Brandied fruit** | Matching spirit + fruit | 672h | 13,140h | Premium cooking ingredient |
| **Preserved lemon** | Salt + lemon + juice | 504h | 8,760h | Condiment multiplier in savory dishes |
| **Preserved lime** | Salt + lime + juice | 504h | 8,760h | Anti-scurvy provision |

#### What We Extend vs What We Add

| Existing System | How We Extend It |
|----------------|-----------------|
| ExpandedFoods dried fruit | Our new fruits (fig, coconut, banana, date) get drying transitions via patches |
| ExpandedFoods candied fruit | Our new fruits get candied recipes via patches |
| ExpandedFoods fruit syrup | Our citrus fruits get syrup recipes via patches |
| ExpandedFoods compote | Preserved fruit (rumtopf) accepted as compote ingredient via patch |
| Base game jam | Our new fruits accepted in jam cooking recipes via patches |
| Base game cider/spirit system | Our new crops produce fermentable juice/wash that flows into existing aging/distillation pipeline |
| ExpandedFoods vinegar pickling | Our new vegetables accepted in vinegar pickling via patches |

**What is entirely new (no patching — new items/recipes):**
- Rum, chicha, pulque, mezcal, toddy, arrack (new barrel recipes + new crop inputs)
- Grog, switchel (new mixed drink items)
- Preserved fruit in alcohol / rumtopf (new barrel recipes + new item type)
- Preserved lemons/limes (new barrel recipes using our salting system + new citrus crops)

---

## 4. Storage & Containers

> **Design Philosophy:** Same as food — extend, don't reinvent. ACulinaryArtillery already adds glass/clay bottles with bottle racks. HydrateOrDieDrate already adds kegs and tuns. The base game barrel (50L) remains the largest standard liquid vessel. We add only containers that fill genuine gaps: stone-age clay fermentation vessels, portable amphorae for trade/transport, seafarer's dry goods storage, and salt production infrastructure.

### What Already Exists Across Base Game + Mods

| Container | Provider | Type | Capacity | Notes |
|-----------|----------|------|----------|-------|
| **Barrel** | Base game | Liquid/sealed | 50L | `BlockBarrel` — fermentation, brining, sealing recipes. Wood construction. |
| **Trunk** | Base game | Solid storage | 36 slots | Largest solid container. 2-block multiblock. |
| **Crate** | Base game | Solid storage | 20-25 slots | Labelable, boatable. Wood typed. |
| **Chest** | Base game | Solid storage | 16 slots | Boatable, lockable. |
| **Storage vessel** | Base game | Solid storage | 12 slots | Clay, boatable. Spoil reduction (0.75x veg, 0.5x grain). 9 color variants. |
| **Reed basket** | Base game | Solid storage | 8 slots | Boatable. Reed/papyrus/vine variants. Can hold creatures. |
| **Bucket** | Base game | Liquid | 10L | Portable, milking container. |
| **Jug** | Base game | Liquid | 3L | Small portable liquid. |
| **Crock** | Base game | Meal | 4 servings | Sealed meal storage. Shelvable. |
| **Clay pot** | Base game | Cooking/meal | 4 cooking slots / 6 servings | Cooking container, wearable. |
| **Backpack** | Base game | Wearable | 6-8 slots | Back slot. Normal and sturdy variants. |
| **Linen sack** | Base game | Wearable | 5 slots | Back slot. Ground-storable. |
| **Glass bottle** | `aculinaryartillery` | Liquid | 1L | Corked/open. Bottle rack display. Multiple colors. |
| **Clay bottle** | `aculinaryartillery` | Liquid | 1L | Fired clay. Bottle rack compatible. |
| **Bottle rack** | `aculinaryartillery` | Display | 16 bottles | Wall-mounted. Corner variant. |
| **Keg** | `hydrateordiedrate` | Liquid | Medium | Tapped/untapped. Lockable. `HoD:BlockKeg`. |
| **Tun** | `hydrateordiedrate` | Liquid | Large | 2x2x2 multiblock. Lockable. Food spoilage modifiers. |
| **Cart storage** | `cartwrightscaravan` | Wearable | 1-32 slots | Barrel/crate/trunk variants mounted on carts. |

### What's Missing — Our Niche

1. **Stone-age fermentation vessel** — barrel requires coopering (bronze age+). Clay can be worked from day one. No clay container supports barrel-style sealed recipes.
2. **Trade amphora** — the iconic Mediterranean/ancient trade container. Portable (back slot), narrow-necked, sealed. Smaller than barrel but carriable.
3. **Waterskin/canteen** — primitive portable liquid for personal carry. Leather, available before jugs.
4. **Salt production** — no dedicated salt extraction exists. Barrel evaporation is the only method.
5. **Cargo net** — rope-based hanging storage for boats. No mod covers boat-specific storage rigging.
6. **Canvas/hemp sack** — our hemp crop enables a new sack type for dry goods transport.

We do **not** add: glass bottles (ACA has them), kegs (HoD has them), tuns (HoD has them).

---

### 4.1 Clay Fermentation Jar (Onggi / Tinaja)

> A clay vessel that functions like a small barrel — supports sealed recipes (brining, fermenting, salting). Available in the stone age before coopering unlocks the wood barrel.

**Historical context:**
- **Onggi** (Korea): Unglazed clay fermentation jars used for kimchi, doenjang, gochujang, and soy sauce for thousands of years. The porous clay allows controlled airflow ("breathing") that aids fermentation.
- **Tinaja** (Mediterranean/Mesoamerica): Large clay jars used for wine, olive oil, and water storage since Phoenician times. Spanish tinajas stored wine in cellars. Mexican tinajas kept water cool via evaporative cooling.
- **Amphora** (Greece/Rome): The standard trade container of the ancient world — but those were transport vessels (see 4.2). Fermentation happened in large stationary jars called *dolia* (Rome) or *pithoi* (Greece).

**Specifications:**
- **Class**: Extends `BlockBarrel` (or custom subclass) — must support sealed barrel recipes
- **Capacity**: 25L (half a barrel — balanced for stone-age access)
- **Construction**: Clayforming recipe → fire in pit kiln or beehive kiln
- **Sealed recipe support**: All barrel recipes work at reduced scale (25L vs 50L)
- **Food preservation**: Contents spoil 0.75x rate (clay insulation, like storage vessel)
- **Placement**: Ground block, not portable once placed (too large/heavy)
- **Shape**: Wide-mouthed jar with lid. Rounded body, flat base. Based on Korean onggi proportions.
- **Variants**: 9 colors (matching base game clay palette: blue, fire, black, brown, cream, gray, orange, red, tan)

```json5
// Block definition sketch
{
  code: "fermentjar",
  class: "BlockBarrel", // or custom subclass
  variantgroups: [{ code: "color", states: ["blue","fire","black","brown","cream","gray","orange","red","tan"] }],
  attributes: {
    capacityLitres: 25,
    // Inherits sealed recipe system from BlockBarrel
  },
  behaviors: [
    { name: "Lockable" },
    { name: "UnstableFalling" },
    { name: "BoatableGenericTypedContainer" }
  ],
  // Spoil rate modifier
  storageFlags: 289, // same as storage vessel
  spoilSpeedMulByFoodCat: {
    vegetable: 0.75,
    grain: 0.5,
    protein: 0.85,
    dairy: 0.85,
    fruit: 0.85
  }
}
```

**Progression role:**
- Unlocks fermentation, brining, and salting recipes **before** the player can make a wood barrel
- Wood barrel remains superior (50L vs 25L) — incentive to progress to coopering
- Pairs with our drying rack (Section 1.1) for a complete stone-age preservation pipeline: fermentation jar for brining/salting → drying rack for finishing

---

### 4.2 Amphora (Trade Vessel) — DONE

> A portable sealed clay vessel for transporting liquids and preserved goods. Designed for trade and sea travel — fits in the **back slot** for personal carry or in **boat accessory slots**.

**Historical context:**
- The amphora was THE container of ancient maritime trade. Millions were produced across the Mediterranean, standardized by region. A single Roman merchant ship could carry 6,000-10,000 amphorae.
- Pointed bottoms allowed stacking in sand or ship holds. Two handles enabled carrying and rope securing.
- Contents were sealed with clay, wax, or resin stoppers. Markings (tituli picti) indicated origin, contents, and date — the ancient equivalent of product labels.
- Different regions had signature shapes: Greek (tall, narrow), Roman (wider, pointed base), Phoenician (torpedo-shaped), Egyptian (rounded).

**Specifications:**
- **Class**: Custom block — sealed liquid container (not full barrel recipe support)
- **Capacity**: 10L liquid OR 6 solid item slots
- **Mode**: Liquid mode (single liquid type, sealed) OR solid mode (dry goods, preserved foods)
- **Seal mechanic**: Craft with beeswax or resin to seal. Sealed amphora has no spoilage. Unsealed amphora spoils at 0.5x rate.
- **Construction**: Clayforming recipe (tall, narrow-necked shape) → fire in kiln
- **Portable**: Wearable in **back slot** (like backpack). Replaces backpack when worn — trade-off between storage variety and liquid transport.
- **Boatable**: `BoatableGenericTypedContainer` behavior — fits in logbarge accessory slots
- **Shape**: Tall narrow-necked jar with two handles. Pointed or flat base variant.
- **Variants**: 9 colors + 2 base shapes (pointed for ship stacking, flat for ground placement)

```json5
// Block definition sketch
{
  code: "amphora",
  class: "BlockAmphora", // custom class — sealed liquid or solid storage
  variantgroups: [
    { code: "color", states: ["blue","fire","black","brown","cream","gray","orange","red","tan"] },
    { code: "base", states: ["pointed","flat"] },
    { code: "sealed", states: ["open","sealed"] }
  ],
  attributes: {
    capacityLitres: 10,
    solidSlots: 6,
    sealMaterial: ["beeswax", "softresin"], // items that can seal it
  },
  behaviors: [
    { name: "Lockable" },
    { name: "BoatableGenericTypedContainer" },
    { name: "GroundStorable" },
    { name: "Unplaceable" } // item form when carried
  ],
  // Wearable properties
  attachableToEntity: true,
  wearableAttachment: { category: "backpack" },
  // Sealed = no spoilage, open = slow spoilage
  spoilSpeedMulByType: {
    "*-sealed": { vegetable: 0, grain: 0, protein: 0, dairy: 0, fruit: 0 },
    "*-open": { vegetable: 0.5, grain: 0.25, protein: 0.5, dairy: 0.5, fruit: 0.5 }
  }
}
```

**Gameplay role:**
- Primary trade container — fill with garum, wine, oil, preserved fish, salt, or dried goods and transport by boat or on foot
- Back-slot wearing creates meaningful choice: backpack (variety of items) vs amphora (bulk liquid/preserved goods)
- Sealed amphora stops spoilage completely — premium preservation but requires beeswax/resin
- Pointed-base variant stacks better on ships (future ship cargo system), flat-base stands on ground
- Pairs with our logbarge accessory system — amphorae in port/starboard slots

---

### 4.3 Waterskin — DONE

> Primitive personal liquid container. Fits in any inventory slot like a jug. Available from stone age using pelts — before pottery is unlocked.

**Historical context:**
- Every seafaring culture used animal skin or bladder containers for portable water. Goatskin was most common in the Mediterranean and Middle East. Pig bladders in Europe. Leather canteens across Asia.
- Waterskins were standard ship provisions — each sailor carried one. Larger skins hung from masts or rigging.
- Degraded over time (hide dries, seams leak) — shorter lifespan than clay or wood containers.

**Specifications:**
- **Type**: Block (like jug) — `BlockLiquidContainerTopOpened` or similar. Fits in **any inventory slot** (not back-slot locked). Same behavior as jug: hold in hand, place on ground, put in a chest, etc.
- **Capacity**: 4L (larger than jug's 3L — hide is flexible and light, but spoils faster and degrades with use)
- **Construction**: Grid recipe — `hide-pelt-small` (2) + `cattailtops` or `papyrustops` (1) for stitching. Same material tier as the hunter backpack.
- **Durability**: Degrades with use (like tools). ~50 uses before breaking. Can be repaired with fat/tallow (re-waterproofing).
- **Properties**: Liquids spoil faster than in clay (1.5x spoil rate) — hide is porous
- **Available**: Stone age — requires hunting (raw hide → oil with fat → 48h cure → pelt) and basic plant fiber. No kiln or pottery needed.
- **Ground storable**: `GroundStorable` with `Quadrants` layout (like jug — 4 per block)
- **Shelvable**: Can be placed on shelves and in display cases

**Hide processing reminder** (base game):
```
raw hide → soak in barrel → scraped hide → oil with fat → oiled hide → 48h cure → pelt
```
The waterskin uses the same pelt material as the hunter backpack — an early stone-age resource that only requires hunting + a barrel soak or lime soak.

```json5
// Block definition sketch — modeled after jug
{
  code: "waterskin",
  class: "BlockLiquidContainerTopOpened",
  maxStackSize: 4,
  attributes: {
    shelvable: true,
    liquidContainerProps: {
      capacityLitres: 4,
      transferSizeLitres: 0.25,
      emptyShapeLoc: "seafarer:shapes/block/waterskin",
      opaqueContentShapeLoc: "shapes/block/basic/nothing",
      liquidContentShapeLoc: "shapes/block/basic/nothing",
      liquidMaxYTranslate: 0.05
    }
  },
  durability: 50,
  behaviors: [
    { name: "GroundStorable", properties: { layout: "Quadrants" } },
    { name: "Unplaceable" },
    { name: "RightClickPickup" }
  ],
  blockmaterial: "Leather",
  liquidSelectable: 1
}
```

```json5
// Grid recipe — same tier as hunter backpack
{
  ingredientPattern: "P_,PP",
  ingredients: {
    "P": { type: "item", code: "hide-pelt-small", quantity: 2 },
    // no stitching material needed — pelts are flexible enough to fold/tie
  },
  width: 2,
  height: 2,
  output: { type: "block", code: "seafarer:waterskin" }
}
// Alternative with cattail stitching (matches hunter backpack recipe style)
{
  ingredientPattern: "PC,PP",
  ingredients: {
    "P": { type: "item", code: "hide-pelt-small", quantity: 3 },
    "C": { type: "item", code: "*", allowedVariants: ["papyrustops", "cattailtops"], quantity: 1 }
  },
  width: 2,
  height: 2,
  output: { type: "block", code: "seafarer:waterskin" }
}
```

**Progression ladder:**
| Container | Age | Capacity | Spoil Rate | Slot | Source |
|-----------|-----|----------|-----------|------|--------|
| **Waterskin** | Stone age | 4L | 1.5x | Any | SaltAndSand |
| **Jug** | Clay age | 3L | 1.0x | Any | Base game |
| **Bucket** | Wood age | 10L | 1.0x | Any | Base game |
| **Amphora** | Clay age | 10L | 0.5x / 0x sealed | Back slot | SaltAndSand |
| **Fermentation jar** | Clay age | 25L | 0.75x | Placed only | SaltAndSand |
| **Barrel** | Bronze age+ | 50L | 1.0x | Placed only | Base game |

---

### 4.4 Salt Extractor (Evaporation Pan) — DONE

> Shallow clay or stone pan for solar salt extraction from seawater. More efficient and historically accurate than barrel evaporation.

**Historical context:**
- **Salt pans** have been used since at least 6000 BC. Phoenicians, Romans, Chinese, Mesoamerican, and Polynesian cultures all independently developed solar evaporation.
- **Process**: Seawater is channeled into shallow pans. Sun and wind evaporate the water over days/weeks. Salt crystals are raked and collected.
- **Scale**: Roman *salinae* were industrial operations with multiple interconnected pans at different evaporation stages. Smaller operations used single clay pans.
- Salt was so valuable it served as currency (Roman *salarium* → "salary"). Control of salt pans meant control of trade.

**Specifications:**
- **Class**: Custom block — `BlockSaltExtractor`
- **Construction**: Clayforming recipe (shallow pan shape) → fire. OR stone slabs arranged in a frame.
- **Capacity**: Holds 5L of saltwater
- **Mechanic**:
  1. Right-click with bucket of saltwater to fill
  2. Evaporation occurs over time based on temperature and rain exposure
  3. When water fully evaporates, salt crystals remain — right-click to harvest
  4. Yields: 5L saltwater → ~0.5 salt items (base game salt)
- **Climate factors**:
  - Hot + dry = fastest (desert coast: ~48h)
  - Temperate = moderate (~96h)
  - Cold/rainy = slowest (~192h) or stalls entirely in rain
  - Must be placed outdoors with sky access (no roof)
- **Placement**: Ground block, shallow (0.125 height). Can place multiple side-by-side for a salt farm aesthetic.
- **Shape**: Flat rectangular clay pan with low raised edges.

```json5
// Block definition sketch
{
  code: "saltpan",
  class: "BlockSaltExtractor",
  attributes: {
    capacityLitres: 5,
    evaporationRatePerHour: {
      base: 0.05, // litres per hour
      temperatureMul: 1.0, // scales with temp
      rainMul: 0.0 // stops in rain
    },
    saltYieldPerLitre: 0.1, // 5L → 0.5 salt
    requiresSkyExposure: true
  },
  sideOpaque: { all: false },
  sideSolid: { all: false },
  collisionbox: { x1: 0, y1: 0, z1: 0, x2: 1, y2: 0.125, z2: 1 }
}
```

**Progression role:**
- Available in stone age — clay pan is simpler than a barrel
- Less efficient per-batch than barrel evaporation but doesn't tie up a barrel
- Encourages coastal settlement (need seawater access + warm climate)
- Multiple pans create a visual salt farm — aesthetic and functional
- Feeds into all our salting/brining/preservation recipes (Section 1.1)

---

### 4.5 Hemp Sack (Cargo Sack)

> A heavy-duty dry goods sack made from our hemp crop. Larger than the base game's linen sack, designed for bulk cargo transport on boats.

**Historical context:**
- Canvas and burlap sacks were the standard for transporting grain, salt, dried fish, spices, and other dry goods on ships. The word "canvas" derives from "cannabis" (hemp).
- Ship holds were packed with sacks stacked floor-to-ceiling. Sacks could be quickly loaded/unloaded by dockworkers.
- Different goods required different sack materials: coarse hemp/jute for grain and salt, finer linen for flour, oiled canvas for goods that needed moisture protection.

**Specifications:**
- **Type**: Item — wearable (back slot) and ground-storable
- **Capacity**: 8 slots
- **Storage flags**: Dry goods only (grain, dried food, salt, seeds, flour — no liquids, no raw meat)
- **Construction**: Grid recipe — hemp cloth (3) + rope (1)
- **Food preservation**: 0.75x spoil rate for grain, 0.85x for other dry goods (breathable hemp)
- **Wearable**: Back slot — replaces backpack. Slower movement when worn (-10% speed, it's heavy cargo)
- **Boatable**: `BoatableGenericTypedContainer` — fits in logbarge accessory slots
- **Ground storable**: Can be placed on ground like linen sack — stackable appearance (pile of sacks)

```json5
// Item definition sketch
{
  code: "hempsack",
  class: "Item",
  maxStackSize: 1,
  attributes: {
    quantitySlots: 8,
    storageFlags: 162, // dry goods only — grain, dried food, seeds
    spoilSpeedMulByFoodCat: {
      grain: 0.75,
      vegetable: 0.85,
      fruit: 0.85
    }
  },
  behaviors: [
    { name: "GroundStorable", properties: { layout: "Stacking", sprintKey: true } },
    { name: "HeldBag" },
    { name: "BoatableGenericTypedContainer" }
  ],
  attachableToEntity: true,
  wearableAttachment: { category: "backpack" }
}
```

**Comparison to existing bags:**

| Bag | Slots | Restrictions | Spoil Reduction | Source |
|-----|-------|-------------|----------------|--------|
| **Linen sack** | 5 | General | None | Base game |
| **Backpack** | 6-8 | General | None | Base game |
| **Hemp sack** | 8 | Dry goods only | 0.75x grain | SaltAndSand |
| **Amphora** | 6 / 10L | Liquid or preserved | 0.5x / 0x sealed | SaltAndSand |

**Design note:** The hemp sack is a **specialized** alternative to the backpack, not a strict upgrade. It holds more dry goods with preservation bonuses, but can't carry tools, weapons, liquids, or raw foods. Sailors packing for a voyage choose: backpack (versatile) vs hemp sack (bulk provisions) vs amphora (liquids/preserved goods).

---

### 4.6 Cargo Net

> Rope net for hanging storage on boats and in camps. Keeps food off the ground (away from pests, Section 5) and provides visual ship rigging.

**Historical context:**
- Ships hung provisions in rope nets from beams and masts to keep them dry, ventilated, and away from bilge water and rats.
- Fishing nets doubled as cargo nets on small vessels. Larger ships had purpose-built rope cradles for barrels and crates.
- Coastal camps used net hammocks between poles for drying fish and storing provisions above animal reach.

**Specifications:**
- **Type**: Block — attachable to walls/ceilings or between two posts
- **Capacity**: 4 slots (small items only — food, bottles, small containers)
- **Construction**: Grid recipe — rope (4) + sticks (2)
- **Placement**: Wall-mounted (like shelf) or ceiling-hung
- **Pest resistance**: Contents immune to pest system (Section 5) — elevated storage
- **Food preservation**: No spoil bonus, but pest immunity is the key benefit
- **Boatable**: Can be placed on boat structures
- **Shape**: Rope net mesh with items visible inside (like a hammock)

```json5
// Block definition sketch
{
  code: "cargonet",
  class: "BlockGenericTypedContainer",
  attributes: {
    quantitySlots: 4,
    storageFlags: 32, // small items only
    pestImmune: true
  },
  behaviors: [
    { name: "Lockable" },
    { name: "Container" },
    { name: "HorizontalAttachable", properties: { handleDrops: true } }
  ]
}
```

---

### 4.7 Containers — Summary & Progression

| Container | Age | Type | Capacity | Slot | Our Mod? |
|-----------|-----|------|----------|------|----------|
| **Waterskin** | Stone | Liquid | 4L | Any (like jug) | Yes |
| **Salt pan** | Stone | Production | 5L input | Placed only | Yes |
| **Fermentation jar** | Clay | Liquid/sealed (barrel recipes) | 25L | Placed only | Yes |
| **Amphora** | Clay | Liquid or solid, sealable | 10L / 6 slots | Back slot | Yes |
| **Hemp sack** | Linen age | Solid (dry goods) | 8 slots | Back slot | Yes |
| **Cargo net** | Rope age | Solid (small items) | 4 slots | Wall/ceiling | Yes |
| Jug | Clay | Liquid | 3L | Any | Base game |
| Bucket | Wood | Liquid | 10L | Any | Base game |
| Storage vessel | Clay | Solid | 12 slots | Placed only | Base game |
| Barrel | Bronze+ | Liquid/sealed | 50L | Placed only | Base game |
| Crate | Wood | Solid | 20-25 slots | Placed only | Base game |
| Trunk | Wood | Solid | 36 slots | Placed only | Base game |
| Bottle | Glass/clay | Liquid | 1L | Any | ACA |
| Keg | Wood | Liquid | Medium | Placed only | HoD |
| Tun | Wood | Liquid | Large (multiblock) | Placed only | HoD |

**What we do NOT add** (already covered by dependencies/optional mods):
- **Glass/clay bottles** → ACulinaryArtillery (required dependency via ExpandedFoods)
- **Kegs** → HydrateOrDieDrate (optional dependency)
- **Tuns** → HydrateOrDieDrate (optional dependency)
- **Cart storage** → CartwrightsCaravan (optional dependency)

**Back-slot trade-offs** (only one at a time):
- Backpack → general-purpose, 6-8 slots, no restrictions
- Hemp sack → bulk dry goods, 8 slots, grain preservation, movement penalty
- Amphora → liquid/preserved trade goods, 10L or 6 slots, sealable

---

## 5. Pests

### 5.1 Rodents

**Feature:** Small pest animals (like smaller rabbits) that eat food left in open or unsealed containers.

**What exists in base game:**
- **Hare** entity exists (`survival/entities/land/hare.json`) — could be a model reference
- **Fox, raccoon** exist as animals
- No pest/vermin system exists
- Food in open containers doesn't attract animals
- Container "sealed" state isn't formally tracked (barrel uses recipe seal times, not a toggle)

**What we need to build:**
- [ ] Rodent entity — small animal, pest behavior AI
- [ ] AI behavior: attracted to food in open/unsealed containers
- [ ] Food consumption mechanic: rodents eat/destroy food items over time
- [ ] Can be trapped or killed
- [ ] Spawning rules: near settlements, food storage areas
- [ ] Shape/model/animations (rat or mouse-sized)
- [ ] This incentivizes proper food storage and sealed containers

**Design consideration:** This is a significant AI/behavior system. May want to phase this in later after core food/storage is solid.

---

## 6. Ships & Boats

### 6.1 Log Barge (Stone Age) — EXISTS

**Status:** Already implemented in SaltAndSand mod.

**Current specs:**
- Large raft (5x0.6x5 hitbox), speedMultiplier 0.8
- 2 controllable seats (front/back, both paddling)
- 9 accessory slots: oar storage, sail mount, 6 expansion slots (port/starboard x fore/mid/aft)
- Accepts: chests, baskets, storage vessels, lanterns, oil lamps, barrels, rope tie posts
- 13 wood type variants
- Requires paddling tool (oar)
- Deconstructible: drops 20 logs, 24 sticks, 6 rope

**Files:**
- `seafarer/entities/nonliving/boat-logbarge.json`
- `seafarer/itemtypes/boats/boat.json`
- `seafarer/itemtypes/wearable/raftsail.json`
- `seafarer/recipes/grid/logbarge.json`

---

### 6.2 Sampan (Copper/Bronze Age)

**Feature:** River boat that functions as a home, shop, and small workshop.

**What exists in base game:**
- **Sailed boat** (`survival/entities/nonliving/boat-sailed.json`): Full sailboat, weight 1900, speedMultiplier 1.2, 3x1.2x3 hitbox
- **Boat construction** (`survival/entities/nonliving/boat-construction.json`): 18-stage construction system with material requirements (planks, support beams, rope, linen, plumb-and-square tool)
- **Sailed boat accessories:** 30+ slots including storage (4 per side, chained), shields (7 per side), ratlines, figurehead, plaques, anchor
- **Construction stages:** Rollers → Keel → Planking (8 stages) → Ribs → Floor → Rudder → Mast → Rigging → Sail → Launch

**What we need to build:**
- [ ] Sampan entity definition — medium river boat
- [ ] Sampan-specific construction stages (simpler than sailed boat, bronze-age appropriate)
- [ ] Home functionality: sleeping area, storage
- [ ] Shop functionality: trade interface (possibly linked to trader system)
- [ ] Workshop functionality: crafting stations usable on board
- [ ] Ship room system integration (see section 7)
- [ ] Shape/model (flat-bottomed river boat with covered cabin)
- [ ] Likely uses the existing boat construction system as a template

---

### 6.3 Catamaran (Copper/Bronze Age)

**Feature:** Fast sailing vessel for travel and fishing.

**What exists in base game:**
- Sailed boat system provides the template for wind/sail mechanics
- Wind patterns: still, light breeze (0.15), medium (0.30), strong (0.60), storm
- Sail furling/unfurling via mast cleat interaction
- Movement currently player-input driven (wind is mostly visual)

**What we need to build:**
- [ ] Catamaran entity definition — dual-hull fast boat
- [ ] Higher speedMultiplier than other boats (target: 1.5+)
- [ ] Fishing mechanic integration (nets? fishing rod mounts?)
- [ ] Limited cargo capacity (speed vs storage tradeoff)
- [ ] Shape/model (twin hull catamaran with sail)
- [ ] Construction recipe appropriate to bronze age

---

### 6.4 Future Ships

More ship types planned for later. All larger ships will use the **sailboat building system** for construction (the 18-stage system from `boat-construction.json`).

---

## 7. Ship Room System

**Feature:** Modular room system for ships. Ships have room areas that can contain pre-built equipment or be customized.

**Example rooms:**
- **Bedroom** — pre-equipped with hammocks or beds
- **Galley** — pre-equipped with oven, butcher table, meat hooks, storage
- **Grow beds** — containers that hold dirt blocks for growing plants
- **Workshop** — crafting stations
- **Storage hold** — bulk storage area

**What exists in base game:**
- **Rideableaccessories behavior** provides the slot/attachment system — items placed in named slots, step-parented to model elements
- **Slot categories** system: `forCategoryCodes` determines what fits where
- **Behind slots** system: `behindSlots` creates dependency chains (must fill slot A before B)
- **No room/interior system** exists — all ship accessories are individual slot-based
- **Room detection** exists for land buildings (used for temperature calculations) but not ships

**What we need to build:**
- [ ] Room definition system — define room types with pre-configured equipment lists
- [ ] Room attachment to ship slots (extending rideableaccessories or new behavior)
- [ ] Pre-built room items (craft a "galley room" item, place it in a ship room slot)
- [ ] OR modular approach: room slots that accept individual items (oven, table, hooks)
- [ ] Shipwright table block — advanced crafting station for room construction and ship planning
- [ ] Grow bed block — container holding dirt for shipboard farming
- [ ] Shape/models for each room type and the shipwright table

**Design decisions needed:**
- Pre-built rooms (simpler, less flexible) vs modular rooms (complex, more player choice)?
- How do rooms interact with walkable boat system (section 9)?
- Does the shipwright table replace the construction system or extend it?

---

## 8. Hydration & Exposure System

### 8.1 Hydration

**Feature:** Players must drink water and liquids regularly. Dehydration reduces stamina and max HP in tiers.

**What exists in base game:**
- **Water portions** exist: `survival/itemtypes/liquid/waterportion.json` — fresh, salt, boiled variants with pollution states (clean, muddy, tainted, poisoned)
- **No hydration/thirst meter** in base game — water is item/commodity only
- **Hunger system** with saturation (1500 max, configurable loss rate) provides a template
- **Player behaviors:** `hunger`, `tiredness`, `bodytemperature` exist as player entity behaviors
- **Trait system** modifies stats: `hungerrate`, `maxhealthExtraPoints`, `walkspeed`, etc.
- **HydrateOrDiedrate mod** already implements a full thirst system with dehydration tiers

**What we need to build:**
- [ ] Hydration player behavior (new server+client behavior)
- [ ] Hydration stat tracking (current hydration, max hydration)
- [ ] Dehydration tier effects: reduced stamina, reduced max HP
- [ ] Drinking mechanic for water and other liquids
- [ ] UI element for hydration display
- [ ] **Compatibility note:** HydrateOrDiedrate already does this — consider making our system optional/configurable, or deferring to their mod and providing integration hooks instead

---

### 8.2 Exposure (Heat/Sun)

**Feature:** Sun/heat exposure damage system similar to the existing freezing mechanic.

**What exists in base game:**
- **Body temperature** behavior exists on player entity (`bodytemperature` in client+server behaviors)
- **Freezing damage** system exists (cold causes damage/debuffs)
- **No heat/sun damage** system exists — only cold is dangerous
- **Weather system** tracks temperature, rain, wind
- **Clothing system** provides warmth modifiers

**What we need to build:**
- [ ] Heat exposure tracking (extending or paralleling bodytemperature behavior)
- [ ] Sun exposure damage at high temperatures
- [ ] Shade/shelter detection (similar to how cold checks for enclosure)
- [ ] Clothing effects: light clothing reduces heat exposure, heavy clothing increases it
- [ ] Hydration tie-in: dehydration accelerates heat damage
- [ ] Visual effects (heat shimmer, screen reddening)
- [ ] **Design note:** This creates the coastal survival loop — need water, need shade, need proper clothing

---

## 9. Walkable Boats

**Feature:** Players can walk on boats as normal surfaces. Toggle between traveling mode and anchored mode.

**Modes:**
- **Traveling mode:** Normal boat behavior, player seated, boat moves
- **Anchored mode:** Boat becomes walkable surface, player can walk around deck

**What exists in base game:**
- **No walkable boat surface** exists — all boat interaction is seat-based
- **Anchor item** exists (`survival/itemtypes/wearable/anchor.json`): Class `ItemAnchor`, but currently decorative/non-functional
- **Ratlines** provide climbable surfaces with mount animation (`climbidle`)
- **Collision boxes** exist via `passivephysicsmultibox` behavior
- **Entity behaviors** available: `repulseagents`, `ellipsoidalrepulseagents` push players away from boats
- `hidewatersurface` behavior hides water rendering inside boat hull

**What we need to build:**
- [ ] Anchor toggle mechanic: interact with anchor to switch modes
- [ ] Anchored state: disable boat movement, enable walkable collision
- [ ] Walking surface: convert boat collision boxes to walkable terrain (this is a significant engine-level challenge)
- [ ] Player-on-moving-entity physics (keeping player position synced with boat)
- [ ] Boarding/disembarking mechanic
- [ ] **Technical challenge:** VS entities don't natively support walkable surfaces — this likely requires custom entity physics or Harmony patches to the movement system

**Design consideration:** This is the most technically challenging feature. May require deep engine integration. Consider phased approach: anchored-only walkability first, then moving-walkability later.

---

## 10. Rift/Treasure Sand

**Feature:** Special sand block (like bone soil) containing buried treasure with themed loot.

**Loot table items:**
- Gold and silver nuggets/items
- Gear and equipment
- Old parchment/scrolls
- Lore items about ancient rift pirates and sailors who feared "the deep ones"
- Maps to POIs and distant islands

**What exists in base game:**
- **Bone soil** (`survival/blocktypes/soil/bony.json`): Pannable block, `pannedBlock: "bonysoil-7"`, lower fertility
- **Panning system** (`survival/blocktypes/wood/pan.json`): Class `BlockPan`, extensive drop tables:
  - Bone soil drops: bones (30%), flax (15%), arrowheads, copper, gears, gems, gold nuggets, lore books, temporal gears (0.001), tuning cylinders (0.0006), jewelry (0.0004-0.0016)
- **Sieve** (`survival/blocktypes/wood/sieve.json`): Material variants (linen → blackbronze), 200 uses
- **Loot vessels** (`survival/blocktypes/clay/lootvessel.json`): Class `BlockLootVessel`, categories: seeds, food, forage, ore, tools, farming, arcticsupplies
- **Treasure hunter trader** NPC exists with dialogue system
- **Buried treasure schematics** exist: `survival/worldgen/schematics/buriedtreasure/reapers-lost-treasure.json`
- **Skeleton with loot** entity: `survival/entities/nonliving/skeletonwithloot.json`

**What we need to build:**
- [ ] Treasure sand block type (similar to bony soil — pannable)
- [ ] Custom drop table: gold, silver, gear, parchment, lore items
- [ ] Lore parchment items with story text about rift pirates and "the deep ones"
- [ ] Treasure map items that point to POIs and distant islands
- [ ] Map mechanic: how do maps lead players to locations? (waypoint? minimap marker?)
- [ ] Worldgen placement: treasure sand spawns on beaches, islands, underwater?
- [ ] POI schematics for treasure locations
- [ ] **Compatibility note:** CraftableCartography adds craftable maps — potential integration for treasure maps

---

## 11. Mod Compatibility

### Required Compatibility

| Mod | ModID | Version | Type | Key Overlap |
|-----|-------|---------|------|-------------|
| A Culinary Artillery | `aculinaryartillery` | 2.0.0-dev.10 | Code | Cooking equipment (cauldrons, frying pans, bottles), simmering recipes. **Adds clay+glass bottles.** |
| Expanded Foods | `expandedfoods` | 2.0.0-dev.7 | Content | New food recipes (pemmican, hardtack, salted meat, sushi, oils). **Depends on ACulinaryArtillery.** |
| Butchering | `butchering` | 1.12.0 | Code | Butcher tables, meat hooks, smoking racks, animal processing. **Adds smoking rack we could reuse/extend.** |
| Cartwright's Caravan | `cartwrightscaravan` | 1.8.0 | Content | Carts, sleds, market stalls. **Trade/transport overlap with our ships.** |
| Carry On | `carryon` | 1.14.0-pre.2 | Code | Makes items carryable. **Need patches for our new blocks.** |

### Optional Compatibility

| Mod | ModID | Version | Type | Key Overlap |
|-----|-------|---------|------|-------------|
| Hydrate or Diedrate | `hydrateordiedrate` | 2.3.7 | Code | Full thirst system, kegs, tuns, water infrastructure. **Major overlap with our hydration system.** |
| Craftable Cartography | `craftablecartography` | 0.2.14 | Code | Compass, sextant, craftable maps. **Could integrate with treasure maps.** |

### Patch System Reference

VS supports **conditional patching** via JSON patch files. This is the primary mechanism for mod compatibility.

#### Patch Format

```json
[
  {
    "op": "add|addmerge|replace|remove",
    "path": "/json/path/to/property",
    "value": { },
    "file": "modid:path/to/target.json",
    "side": "server",
    "dependsOn": [ { "modid": "othermod" } ],
    "enabled": true
  }
]
```

#### Key Properties

| Property | Purpose |
|----------|---------|
| `op` | Operation: `add` (append to array/add property), `addmerge` (merge objects), `replace` (overwrite), `remove` (delete) |
| `path` | JSON pointer to the target property (e.g., `/attributes/enabled`, `/recipes/0/output`) |
| `value` | Data to add/merge/replace (not used with `remove`) |
| `file` | Target file using mod domain prefix (e.g., `game:itemtypes/food/fish.json`, `expandedfoods:recipes/barrel/brandy.json`) |
| `side` | `server`, `client`, or omit for universal |
| `dependsOn` | **Conditional execution** — patch only applies if ALL listed mods are installed |
| `enabled` | Boolean toggle to enable/disable a patch |

#### Conditional Patching with `dependsOn`

Patches with `dependsOn` are **only applied when the specified mod(s) are present**. No `modinfo.json` dependency declaration is needed — the patch silently skips if the mod isn't installed.

```json
[
  {
    "op": "replace",
    "path": "/enabled",
    "value": false,
    "file": "expandedfoods:recipes/barrel/brandy.json",
    "dependsOn": [ { "modid": "expandedfoods" } ]
  }
]
```

Multiple dependencies require ALL mods to be present:
```json
"dependsOn": [ { "modid": "expandedfoods" }, { "modid": "aculinaryartillery" } ]
```

#### Targeting Other Mods' Assets

Use the mod's `modid` as a domain prefix in the `file` property:
- `"file": "game:itemtypes/food/fish.json"` — base game
- `"file": "expandedfoods:itemtypes/food/salted/saltedmeat.json"` — ExpandedFoods
- `"file": "aculinaryartillery:blocktypes/glass/bottle.json"` — ACulinaryArtillery
- `"file": "hydrateordiedrate:itemtypes/liquid/waterportion.json"` — HydrateOrDiedrate

#### Strategies for Handling Conflicts

| Strategy | When to Use | Example |
|----------|------------|---------|
| **Disable theirs** | We have a better/different version | `"op": "remove"` or `"op": "replace"` with `enabled: false` on their recipe |
| **Disable ours** | Their version is fine, avoid duplicates | Conditional patch on our own recipe that disables it when their mod is present |
| **Extend theirs** | Add our ingredients to their recipes | `"op": "addmerge"` to add our items to their allowed inputs |
| **Bridge items** | Make items interchangeable | Patch their recipes to accept our items as valid ingredients |

#### File Organization Convention

```
assets/seafarer/patches/
├── compatibility/
│   ├── expandedfoods/
│   │   ├── disable-duplicate-brandy.json
│   │   └── add-seafarer-ingredients.json
│   ├── aculinaryartillery/
│   │   ├── bottle-integration.json
│   │   └── simmering-recipes.json
│   ├── butchering/
│   │   └── drying-rack-compat.json
│   ├── carryon/
│   │   └── carryable-blocks.json
│   ├── hydrateordiedrate/
│   │   ├── disable-builtin-hydration.json
│   │   └── register-liquids.json
│   ├── cartwrightscaravan/
│   │   └── storage-loading.json
│   └── craftablecartography/
│       └── treasure-map-integration.json
└── [base game patches]
```

The folder structure is purely organizational — only `file` and `dependsOn` properties control targeting.

---

### Compatibility Strategy

**ACulinaryArtillery + ExpandedFoods:**
- These are a unit (ExpandedFoods depends on ACA)
- ACA already adds glass/clay bottles — avoid duplicating, create patches to make their bottles work with our systems
- ExpandedFoods adds pemmican, hardtack, salted meat — coordinate to avoid recipe conflicts
- Use ACA's simmering recipe system for our new stews if present
- Our drying rack should work with their meat items
- **Patch approach:** `dependsOn: aculinaryartillery` — disable our glass bottles, patch theirs to work with our barrel/fermentation recipes. `dependsOn: expandedfoods` — disable duplicate salted meat recipes, bridge their food items as valid ingredients in our cooking recipes.

**Butchering:**
- Their smoking rack could serve as basis for our drying rack, or we make ours compatible
- Meat hooks overlap with galley room equipment
- Ensure our dried/salted meat works with their butchering outputs
- **Patch approach:** `dependsOn: butchering` — patch their smoking rack to accept our salted items, add their butchered meat variants to our drying/salting recipes.

**Cartwright's Caravan:**
- Complementary — they handle land transport, we handle sea transport
- Our storage containers should be caravan-loadable
- Market stall system could integrate with sampan shop functionality
- **Patch approach:** `dependsOn: cartwrightscaravan` — add our storage containers (amphorae, sacks, kegs) to their cart loading categories.

**Carry On:**
- Provide patches so our new blocks (drying rack, salt extractor, amphorae, kegs) are carryable
- Follow their patch format in `assets/carryon/patches/`
- **Patch approach:** `dependsOn: carryon` — add `carryable` behavior to our block types.

**Hydrate or Diedrate:**
- **Key decision:** Build our own hydration system OR make it optional when HoD is installed
- If both present: disable our hydration, use theirs via API hooks
- Their keg/tun could replace our keg — or coexist with different recipes
- Our water-related items (coconut water, etc.) should register with their system
- **Patch approach:** `dependsOn: hydrateordiedrate` — disable our built-in hydration behavior, patch our coconut water/liquids to register thirst values with their system, patch our keg to not conflict with theirs.

**Craftable Cartography:**
- Our treasure maps could use their map system as a base
- Sextant integration for navigation on ships
- Compass integration for ship heading display
- **Patch approach:** `dependsOn: craftablecartography` — patch our treasure map items to use their map rendering system, add our compass/sextant as valid navigation tool alternatives.

---

## 12. Tortuga Village

> **Design Philosophy:** A pirate port settlement that serves as a trade hub and progression gate. Players must find Tortuga to access advanced ship-building, rare items, and the fishing bounty system. Uses the base game's named villager pattern (EntityVillager) -- each NPC is a unique character with custom trade lists and dialogue, not a generic roaming trader.

**What exists in base game:**
- **Named villagers** (EntityVillager): Unique NPCs with per-character trade lists and dialogue trees (e.g., Liga the innkeeper, Gerhardt the hunter)
- **Worldgen structures**: Schematics placed by worldgen with BESpawner blocks to spawn NPCs
- **Dialogue system**: State-machine based conversation with triggers (opentrade, giveitemstack, takefrominventory, revealname, spawnentity), persistent variables, and condition branching
- **Trade system**: TradeProperties JSON defining buy/sell lists with NatFloat price/stock distributions, gear-based currency, weekly stock refresh
- **Climate-specific trader types**: ModSystemClimateSpecificTraderTypes swaps entity variants based on worldgen climate

**What we need to build:**
- Worldgen schematic for the Tortuga village (built in world editor, exported as schematic)
- Worldgen placement rules (coastal biome, minimum distance from spawn, rarity)
- 4 named villager entity definitions
- 4 trade list JSON configs
- 4 dialogue JSON configs
- Spawner block placement in the schematic
- Optional: Craftable Cartography integration for the Cartographer's maps

---

### 12.1 Provisioner

**Role:** General supplies trader for voyages. The go-to for stocking up before a long trip.

**Buys (from player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Alcohol | Rum, chicha, pulque, coconut toddy, grog, switchel | 3-8 per unit |
| Sugar | Raw sugar, molasses, sugar cane | 1-3 per unit |
| Salt | Salt items | 1-2 per unit |

**Sells (to player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Medicine/Healing | Poultices, bandages, healing items | 4-10 |
| Long-lived food | Hardtack, pemmican, dried/salted meats, pickled vegetables | 3-8 |
| Cooking equipment | Clay griddle, cooking pots | 8-15 |
| Containers | Crates, barrels, baskets, storage vessels, amphorae | 5-12 |

**Dialogue:** Friendly barkeep personality. Reveals name after first meeting. Comments on what alcohol the player has brought. Opens trade after brief conversation.

---

### 12.2 Cartographer

**Role:** Exploration enabler and curiosity collector. Sells navigation tools and premium location maps to islands, shipwrecks, and treasure POIs.

**Buys (from player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Lore items | Parchment, scrolls, lore books | 3-8 |
| Clutter items | Decorative/archaeological finds | 1-5 |
| Pelts | Animal pelts/hides | 2-6 |

**Sells (to player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Navigation tools | Compass, sextant, spyglass | 10-25 |
| Blank cartography | Blank maps, mapping supplies | 3-8 |
| Location maps (premium) | Maps to islands, shipwrecks, treasure sand deposits, Rift POIs | 20-50 |

**Dialogue:** Scholarly personality. Interested in lore and stories. Premium maps may require dialogue-based "quests" (bring a lore item or answer a question about exploration) before they appear in the shop.

**Compatibility note:** If Craftable Cartography is installed, location maps could use their map rendering system via dependsOn patches.

---

### 12.3 Shipwright

**Role:** Progression gate for ship-building. You can craft a basic raft on your own, but anything beyond that (sampan, catamaran, future ships) requires schematics and parts purchased from the Shipwright.

**Buys (from player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Rare woods | Aged wood, tropical hardwoods | 5-10 per log |
| Regular wood | Any log type, large quantities | 1-2 per log (low price, volume buyer) |

**Sells (to player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Ship schematics | Sampan schematic, catamaran schematic (required to unlock crafting) | 30-80 |
| Ship parts | Hull reinforcements, mast fittings, keel pieces, copper fittings | 10-25 |
| Nautical items | Ship lanterns, figureheads, anchors, rope ladders | 5-15 |
| Future: Pirate cosmetics | Schematic for pirate-themed clothing/armor crafting | TBD |

**Design note:** Ship schematics are "knowledge items" -- using one teaches the player the crafting recipe permanently. This gates progression without forcing repeated purchases.

**Dialogue:** Gruff, practical personality. Impressed by rare wood offerings. May offer discounts for bulk wood deliveries via dialogue triggers.

---

### 12.4 Treasure Merchant

**Role:** Endgame treasure sink. Buys gold, silver, gems, and treasure finds. Sells rare cosmetics and unique items unavailable anywhere else.

**Buys (from player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Gold items | Gold nuggets, gold ingots | 5-15 |
| Silver items | Silver nuggets, silver ingots | 3-10 |
| Gems | Rough and polished gems | 5-25 depending on quality |
| Treasure finds | Items from Rift/Treasure Sand panning | 3-20 |

**Sells (to player):**

| Item Category | Examples | Price Range (gears) |
|--------------|---------|-------------------|
| Rare cosmetics | Unique clothing, pirate hats, jewelry, decorative items | 20-50 |
| Rare decoratives | Ship trophies, wall-mounted treasure displays, exotic furniture | 15-40 |
| Unique items | Items that cannot be crafted or found elsewhere | 30-80 |

**Dialogue:** Mysterious, collector personality. Appraising and knowledgeable about treasure. Reveals more dialogue options as player sells more treasure (tracked via persistent variables).

---

### Tortuga Worldgen

**Placement rules:**
- Spawns in coastal biomes (beach/ocean edge)
- Minimum distance from world spawn (players should have to explore to find it)
- Rarity: 1 per world region (large maps might have 2-3 total)
- Structure schematic built in VS world editor and exported

**Structure contents:**
- Dock/pier extending into water
- 4 buildings/stalls for each merchant
- Harbormaster's office (for bounty system, section 13)
- Spawner blocks (BESpawner) for each NPC
- Ambient details: barrels, crates, rope, lanterns, anchors

---

## 13. Fishing Bounty System

> **Design Philosophy:** A competitive social mechanic centered around a Harbormaster NPC in Tortuga. Weekly fishing bounties give players rotating goals, a dedicated token currency, a 10-level rank progression, and both all-time and weekly leaderboards. The entire system is server-authoritative with client display via NPC dialogue.

**What exists in base game:**
- **Fishing system**: Rod + bait fishing mechanics, multiple fish species with varying rarity
- **Dialogue system**: Triggers, persistent variables, condition branching -- can drive complex NPC interactions
- **Named villagers**: Custom per-NPC trade lists and dialogue
- **Server save data**: api.WorldManager.SaveGame.StoreData/GetData for persistent mod state
- **Network channels**: api.Network.RegisterChannel for custom server-to-client data sync

**What we need to build:**
- BountyModSystem -- server-side system managing weekly rotation, delivery tracking, scoring, persistence
- Bounty token item type (seafarer:bountytoken)
- Harbormaster NPC (named villager with custom dialogue and trade list)
- Bounty pool JSON config (all possible fish bounties with point values)
- Custom dialogue triggers for bounty operations (view bounties, deliver fish, view leaderboard)
- Network packets for leaderboard sync (server to client)
- Custom GUI for leaderboard display within dialogue
- Rank progression data and title definitions
- Server-side persistence for player scores, ranks, weekly state

---

### 13.1 Harbormaster NPC

**Role:** The sole interface for the bounty system. A named villager in Tortuga who manages bounties, accepts deliveries, sells rewards, and displays the leaderboard.

**Dialogue options:**
1. "What fish do you need this week?" -- Shows active bounties (fish type, quantity, point value)
2. "I've got fish for you." -- Delivery interaction: checks player inventory for bounty fish, accepts delivery, awards tokens + points
3. "Show me the rankings." -- Opens leaderboard GUI (all-time and weekly views)
4. "What rewards do you have?" -- Opens trade GUI (bounty tokens as currency)
5. "What's my rank?" -- Shows player's current rank, title, lifetime points, weekly points

**Personality:** Weathered dockmaster. Competitive, encouraging. Comments on player rank and recent deliveries.

---

### 13.2 Weekly Bounties

**Rotation:** Every 7 game days, the server generates 3-5 new bounties from the bounty pool.

**Bounty structure:**
```
fishCode: "game:fish-catfish-raw"
quantity: 15
pointsPerDelivery: 10
tokensPerDelivery: 5
difficulty: "common"
```

**Difficulty tiers:**

| Tier | Examples | Points | Tokens |
|------|---------|--------|--------|
| Common | Bass, catfish, perch | 5-10 | 3-5 |
| Uncommon | Pike, carp, trout | 10-20 | 5-10 |
| Rare | Sturgeon, salmon, arctic char | 20-40 | 10-20 |

**Weekly generation:** Each week, the server selects 1-2 common, 1-2 uncommon, and 0-1 rare bounties. Fish types don't repeat within a week.

**Delivery:** Player talks to Harbormaster, selects a bounty to deliver against. System checks inventory for the required fish, removes them, and awards points + tokens. A bounty can be delivered multiple times per week (no cap -- grind as much as you want).

---

### 13.3 Points and Ranking

**Points:** Lifetime cumulative total. Never decrease. Determine player rank.

**Rank levels:**

| Level | Title | Points Required |
|-------|-------|----------------|
| 0 | Deckhand | 0 |
| 1 | Hobby Fisher | 50 |
| 2 | Line Caster | 150 |
| 3 | Net Hauler | 350 |
| 4 | Creek Wader | 650 |
| 5 | Deep Liner | 1,100 |
| 6 | Tide Reader | 1,750 |
| 7 | Gill Netter | 2,650 |
| 8 | Salt Veteran | 3,850 |
| 9 | Sea Wolf | 5,500 |
| 10 | Master Angler | 8,000 |

Point thresholds follow an escalating curve -- each rank requires more effort than the last. Values to be tuned during playtesting.

**Title display:** Player title is shown in the Harbormaster dialogue and on the leaderboard. Future: could display as a nametag prefix or chat title.

---

### 13.4 Bounty Tokens and Reward Shop

**Bounty tokens** (seafarer:bountytoken) are a custom item that serves as the dedicated currency for the Harbormaster's reward shop. They are earned from bounty deliveries only -- not tradeable at other merchants.

**Reward shop** (accessed via Harbormaster trade GUI, tokens as currency):

| Category | Examples | Token Cost | Min Rank |
|----------|---------|------------|----------|
| Basic fishing gear | Improved lures, bait bundles | 10-20 | 0 |
| Advanced fishing gear | High-quality rod, rare bait | 30-60 | 3 |
| Cosmetics (basic) | Fisherman's hat, wading boots | 15-30 | 1 |
| Cosmetics (rare) | Captain's coat, tricorn hat | 50-100 | 5 |
| Boat accessories (basic) | Fish trophy mount, net decoration | 20-40 | 2 |
| Boat accessories (rare) | Ornate figurehead, special sail | 80-150 | 7 |
| Master items | Unique items only for rank 10 | 200+ | 10 |

**Rank-gating:** Items in the shop require both sufficient tokens AND minimum rank level. High-rank items are visible but grayed out until the player reaches the required rank.

---

### 13.5 Leaderboard

**Two views:**

**All-time leaderboard** shows: rank position, player name, title, lifetime points (top 10 + your own entry).

**Weekly leaderboard** shows: rank position, player name, title, weekly points, bounties completed this week (top 10 + your own entry).

Player's own entry always visible at bottom if not in top 10.

**Persistence:** Server saves leaderboard state. Weekly leaderboard resets with each bounty rotation. All-time persists forever.

**Implementation:** Custom GuiDialog opened from a dialogue trigger. Server sends leaderboard data to client via network channel on request.

---

## Development Phases (Suggested)

### Phase 1: Food Foundation
- Clay griddle (comal) — cooking surface block with clay/stone variants
- ~~Drying rack~~ **DONE**
- ~~Salting/drying process for meat and fish~~ **DONE**
- Flatbread (cooked on griddle)
- ~~Salt extractor~~ **DONE**
- Clay storage vessel (barrel alternative)

### Phase 2a: Core Crops & Cooking
- Core food crops: ~~chilies~~ **DONE**, coconuts, ~~corn~~ **DONE**, figs, lemons, limes
- Derived products: coconut oil, dried coconut, corn nixtamalization, dried figs/chilies
- ~~Hemp~~ **DONE** (rope and sail fiber — critical for ship building phases)
- Fermentation recipes (barrel)
- New stews, porridge, flatbread wraps, and meals
- Pizza

### Phase 2b: Trade & Specialty Crops
- ~~Ginger~~ **DONE** (anti-seasickness mechanic, cooking ingredient)
- Stimulant crops: ~~coca~~ **DONE**, ~~tea~~ **DONE**, ~~sugar cane~~ **DONE**
- Tropical staples: ~~taro~~ **DONE**, date palm, banana/plantain
- Fiber crops: ~~agave~~ **DONE** (arid biome alternative to hemp)
- Seaweed farming extension
- Sugar processing, rum brewing

### Phase 3: Alcohol, Storage & Trade
- Rum (light + dark) from sugar cane/molasses
- Chicha from corn, pulque from agave, coconut toddy
- Grog and switchel (mixed drinks)
- Preserved fruit in alcohol (rumtopf), preserved lemons/limes
- Patch new fruits into ExpandedFoods drying/candying/syrup systems
- Fermentation jar (stone-age barrel alternative)
- ~~Amphora (portable trade vessel, back slot)~~ **DONE**
- ~~Waterskin (stone-age liquid carry)~~ **DONE**
- ~~Salt extractor (evaporation pan)~~ **DONE**
- Hemp sack (bulk dry goods, back slot)
- Cargo net (hanging storage, pest-immune)

### Phase 4: Ships
- Sampan (with construction system)
- Catamaran
- Ship room system
- Shipwright table

### Phase 5: Survival Systems
- Hydration system (or HoD integration)
- Heat/exposure system
- Pests (rodents)

### Phase 6: Exploration
- Treasure sand
- Lore items and parchments
- Treasure maps
- POI schematics
- Walkable boats (technical R&D)

### Phase 7: Polish & Compatibility
- Mod compatibility patches
- Balance pass on all preservation times, nutrition values
- Handbook entries and documentation
- Localization

---

## 12. Future Considerations

Features and content deferred for possible inclusion in later versions. Research preserved here so it doesn't need to be redone.

### 12.1 Spice Trade Crops

The historical spice trade is deeply connected to maritime culture, but adding 4+ tree/vine crops with specialized harvest mechanics is a large undertaking best saved for after core systems are stable.

#### Black Pepper (*Piper nigrum*)
- **Type**: Climbing vine (requires trellis/support block)
- **Historical context**: The "king of spices" — drove Indian Ocean trade for millennia. Malabar Coast origin. Romans, Arabs, Portuguese, Dutch all fought over pepper trade routes.
- **Climate**: Tropical only (temp 20-30°C, high humidity). `coldDamageBelow: 15`, `heatDamageAbove: 35`
- **Growth**: Perennial vine, 3-4 growth stages on trellis. Produces berry clusters annually after maturity.
- **Harvest mechanic**: Right-click to pick peppercorn clusters (like berry bushes). Vine persists.
- **Processing pipeline**:
  - Fresh peppercorns → dry on rack → **black pepper** (most common)
  - Fresh peppercorns → soak in barrel → remove skin → dry → **white pepper** (milder, longer shelf life)
  - Fresh peppercorns → brine in barrel → **green pepper** (short shelf life, used fresh)
- **Unique mechanic**: Dual product from same harvest based on processing path
- **Derived products**: Ground pepper (quern), pepper sauce, spiced preserves
- **Preservation bonus**: Pepper-spiced foods gain +25% shelf life

#### Cinnamon (*Cinnamomum verum*)
- **Type**: Tree (small, like fruit trees)
- **Historical context**: Sri Lankan origin, Egyptian trade good, medieval luxury. Portuguese colonized Sri Lanka specifically for cinnamon. Inner bark is the spice.
- **Climate**: Tropical (temp 18-32°C). `coldDamageBelow: 12`, `heatDamageAbove: 38`
- **Growth**: Tree sapling → mature tree (like fruit trees). No fruit — harvest is the bark.
- **Harvest mechanic**: **Bark stripping** — use knife on mature tree to get cinnamon bark strips. Tree needs recovery time (seasonal cooldown) before re-harvest. Doesn't kill the tree.
- **Processing pipeline**:
  - Cinnamon bark strips → dry on rack → **cinnamon sticks**
  - Cinnamon sticks → grind in quern → **ground cinnamon**
- **Unique mechanic**: First bark-harvesting crop — novel interaction model
- **Derived products**: Cinnamon oil (press), spiced wine/mead, baked goods flavoring
- **Cooking bonus**: Cinnamon in sweet dishes adds +15% satiety

#### Cloves (*Syzygium aromaticum*)
- **Type**: Tree (small)
- **Historical context**: Moluccas (Spice Islands) origin — so valuable that the Dutch destroyed clove trees on islands they didn't control. Used in Chinese Han dynasty courts (courtiers held cloves in mouth to freshen breath for emperor).
- **Climate**: Tropical island (temp 20-30°C, coastal proximity bonus). `coldDamageBelow: 15`, `heatDamageAbove: 35`
- **Growth**: Tree, slow-growing. Produces flower buds after reaching maturity.
- **Harvest mechanic**: **Flower bud picking** — harvest unopened flower buds (right-click like berries). Must be picked before they open — timing window mechanic.
- **Processing pipeline**:
  - Fresh clove buds → dry on rack → **dried cloves**
  - Dried cloves → grind in quern → **ground cloves**
  - Dried cloves → press → **clove oil** (medicinal, preservative)
- **Unique mechanic**: Harvest timing — buds must be picked in specific growth stage window
- **Derived products**: Clove oil (antiseptic — reduces infection from wounds?), spiced preserves, mulled wine ingredient
- **Preservation bonus**: Clove oil as preservative additive (+30% to stored foods)

#### Nutmeg (*Myristica fragrans*)
- **Type**: Tree (medium)
- **Historical context**: Banda Islands origin. So valuable the Dutch traded Manhattan to the British for the nutmeg-producing island of Run. One of the most concentrated sources of wealth in history.
- **Climate**: Tropical (temp 20-30°C). `coldDamageBelow: 15`, `heatDamageAbove: 35`
- **Growth**: Tree, very slow growing. Dioecious — needs male and female trees for fruit (or simplify for gameplay).
- **Harvest mechanic**: Fruit drops when ripe (like fruit trees). Fruit contains seed (nutmeg) wrapped in red aril (mace).
- **Processing pipeline**:
  - Nutmeg fruit → crack open → **nutmeg seed** + **mace** (two products from one fruit)
  - Nutmeg seed → dry → **dried nutmeg**
  - Dried nutmeg → grate/grind → **ground nutmeg**
  - Mace → dry → **dried mace** (separate, rarer spice)
- **Unique mechanic**: **Dual product harvest** — one fruit yields two distinct spices (nutmeg and mace)
- **Derived products**: Ground nutmeg, dried mace, spiced butter, béchamel-style sauces
- **Medicinal**: Small doses — warming, digestive aid. Large doses — hallucinogenic/toxic (historical sailors used it recreationally on long voyages)

#### Spice Trade Integration Notes
- All 4 spice crops are tropical trees/vines — they create a reason to establish tropical coastal plantations
- Spice processing chains are complex (bark stripping, bud timing, dual products) — each introduces a novel harvesting mechanic
- Combined with the trade ship system, spices become high-value cargo for inter-settlement trade
- Consider a "spice rack" storage block that preserves ground spices and provides cooking bonuses when nearby
- Spice-enhanced recipes could provide the highest-tier nutrition bonuses in the mod
- **Prerequisite**: Core crop system (Phase 2a/2b) should be proven stable before adding these complex tree crops
