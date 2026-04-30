# Rebuilding Tortuga - Quest Tree

## Overview

The Port of Tortuga was torn from the old world by a temporal typhoon. Five survivors
remain, each maintaining their trade as best they can. Four main quests each award +1 to
the town's rebuilding score. When the score reaches 4, the town advances from **Destitute**
to **Struggling**.

```
                              REBUILDING TORTUGA
                           (rebuilding-tier: 0 -> 4)
                                      |
         +---------------+---------------+---------------+
         |               |               |               |
    [Drake: +1]    [Morgan: +1]    [Dawn Marie: +1]    [Celeste: +1]
    Trade Ship     Reopen Mine     Trade Goods          Crimson Rose
         |               |               |               |
         v               v               v               v
    +advanced      +mine running   +more food/         +treasure
     shipbuilding                    trade goods        recovered
     quests
```

After the main quests complete and the town reaches a stable state, secondary
character-development quests open up - specialised training, follow-up trust-building
tasks, and combat content.

---

## NPC Summary

| NPC | Role | Dialogue | Main Quest | Secondary Content |
|-----|------|----------|-----------|-------------------|
| **Morgan** | Provisioner, unofficial mayor | `dialogue/morgan.json` | Reopen the Mine | (acts as hub for other NPC intros) |
| **Drake** | Shipwright | `dialogue/drake.json` | New Trade Ship | Tricks of the Trade (Shipwright training) |
| **Dawn Marie** | Plantation owner | `dialogue/dawnmarie.json` | In Need of Trade Goods | Rebuild the Orchard; Rebuild the Plantation |
| **Celeste** | Retired captain | `dialogue/celeste.json` | The Crimson Rose | Rust Hunter; Bear Hunter; Friendship tales |
| **Potato King** | Mad miner (sub-NPC) | `dialogue/potatoking.json` | The Last Potato (Morgan sub-quest) | — |

---

## Main Quest Flows

### Drake - New Trade Ship

```
[Talk to Drake] --> "Can I help build a new trade ship?"
      |
      v
[Accept Quest] -- player.drake-tradeship-started = true
      |
      v
  +---+---+---+  (parallel objectives, any order)
  |       |       |
  v       v       v
BOARDS  ROPE   LINEN
298x    27x    21x
  |       |       |
  v       v       v
+chisel +saw   +pickaxe
+hammer +shears +prospecting pick
  |       |       |
  +---+---+---+
      |
      v
[All 3 done?] --> entity.tradeship-complete = true
      |               rebuilding-tier +1
      v
  SHIP BUILT
```

**Variables:**
- `player.drake-tradeship-started` - quest accepted
- `entity.boards` / `entity.boards-complete` - birch board counter (threshold: 298)
- `entity.rope` / `entity.rope-complete` - rope counter (threshold: 27)
- `entity.linen` / `entity.linen-complete` - linen counter (threshold: 21)
- `entity.tradeship-complete` - all materials delivered

**Item Codes:**
- `game:plank-birch` (boards)
- `game:rope` (rope)
- `game:linen-normal-down` (linen)

**Shop Rewards:**
- Boards done: `game:chisel-copper`, `game:hammer-copper`
- Rope done: `game:saw-copper`, `game:shears-copper`
- Linen done: `game:pickaxe-copper`, `game:prospectingpick-copper`

---

### Morgan - Reopen Mine (with Potato King sub-quest)

```
[Talk to Morgan] --> "Can I help with the rebuilding effort?"
      |
      v
[Accept Quest] -- player.morgan-mine-started = true
      |                gives: seafarer:letter-morgan
      v
[Bring letter to Potato King]
      |
      v
[PK reads letter] -- entity.letter-received = true
      |                takes: seafarer:letter-morgan
      |                gives: seafarer:map-potato
      v
[Find potato in dungeon]
      |
      v
[Return potato to PK] -- entity.contract-given = true
      |                    takes: game:vegetable-potato
      |                    gives: seafarer:signed-contract
      v
[Return contract to Morgan] -- entity.mine-quest-complete = true
      |                         takes: seafarer:signed-contract
      |                         rebuilding-tier +1
      v
  MINE REOPENED
  +ingots +nails +plates in Morgan's shop
```

**Variables:**
- `player.morgan-mine-started` - quest accepted
- `entity.letter-received` (Potato King) - letter delivered
- `entity.contract-given` (Potato King) - potato returned
- `entity.mine-quest-complete` (Morgan) - contract returned

**Item Codes:**
- `seafarer:letter-morgan` - quest letter (replaceable if lost)
- `seafarer:map-potato` - locator map to dungeon
- `game:vegetable-potato` - the last potato
- `seafarer:signed-contract` - proof of agreement

**Shop Rewards:**
- `game:ingot-copper` (4x, stock 8)
- `game:nail-copper` (8x, stock 16)
- `game:metalplate-copper` (2x, stock 4)

---

### Dawn Marie - In Need of Trade Goods

```
[Talk to Dawn Marie] --> "Is there anything I can do to help?"
      |
      v
[Accept Quest] -- player.dawnmarie-tradegoods-started = true
      |
      v
  +---+---+  (parallel objectives, any order)
  |       |
  v       v
SALT   SUGAR
 64x    64x
  |       |
  v       v
+knife  +hoe
+cleaver +scythe
  |       |
  +---+---+
      |
      v
[Both done?] --> entity.tradegoods-complete = true
      |               rebuilding-tier +1
      v
  TRADE GOODS FLOWING
```

**Variables:**
- `player.dawnmarie-tradegoods-started` - quest accepted
- `entity.salt` / `entity.salt-complete` - salt counter (threshold: 64)
- `entity.sugar` / `entity.sugar-complete` - sugar counter (threshold: 64)
- `entity.tradegoods-complete` - both delivered

**Item Codes:**
- `game:salt` (salt)
- `seafarer:sugar` (sugar)

**Shop Rewards:**
- Salt done: `game:knife-copper`, `game:cleaver-copper`
- Sugar done: `game:hoe-copper`, `game:scythe-copper`

---

### Celeste - The Crimson Rose

```
[Talk to Celeste] --> "I heard you lost a ship -- the Crimson Rose?"
      |
      v
[Accept Quest] -- player.celeste-crimsonrose-started = true
      |                gives: seafarer:map-crimsonrose
      v
[Follow map, dig up sealed chest]
      |
      v
[Return chest to Celeste] -- entity.crimsonrose-complete = true
      |                        takes: seafarer:sealed-chest
      |                        rebuilding-tier +1
      |                        celeste-friendship +1
      v
  TREASURE RECOVERED
  +copper shovel in Celeste's shop
  +can trade rum for treasure maps
```

**Variables:**
- `player.celeste-crimsonrose-started` - quest accepted
- `entity.crimsonrose-complete` - chest returned
- `entity.celeste-friendship` - friendship counter

**Item Codes:**
- `seafarer:map-crimsonrose` - locator map to wreck
- `seafarer:sealed-chest` - recovered treasure

**Shop Rewards:**
- `game:shovel-copper`
- Rum → Treasure map trade unlocked

---

## Secondary / Follow-Up Quest Flows

### Drake - Tricks of the Trade

**Prerequisites:** Trade Ship built OR rebuilding score > 3

```
[Talk to Drake] --> "Can you teach me to build ships?"
      |
      v
[check-tricks-started] -- skips intro if already started
      |
      v (first time only)
[Lessons 1-3] -- seasoning, varnish, oiling
      |           sets player.drake-tricks-started = true
      v
  TRICKS MENU (player picks what to work on)
      |
  +---+---+---+---+
  |       |       |       |
  v       v       v       v
VARNISH CANVAS  SAIL   SEASONED WOOD
```

**Varnish Lesson:**
- Deliver: 18 resin + 6 fat
- Drake teaches varnish-making
- Receive: bucket of 2L marine varnish
- Reward: Shipwright XP +50; flag `entity.varnish-complete`

**Oiled Canvas Lesson:**
- Deliver: 20 linen + 1L oil (taken from inventory)
- Drake teaches canvas oiling
- Receive: 20 oiled canvas
- Flag: `entity.canvas-complete`

**Sail Review:**
- Craft an oiled-canvas-sail from the given canvas + rope
- Bring sail back to Drake for review
- Reward: Shipwright XP +50 (reaches level 1)
- Flag: `entity.sail-reviewed`

**Training Book Unlock:**
- If both Varnish + Sail lessons complete → Apprentice Shipwright book added to shop
- Flag: `entity.training-book-added`

**Seasoned Wood Quest:**
- Prerequisites: Shipwright level ≥ 1
- Deliver: 160 seasoned planks
- Reward: Outrigger schematic (given) + added to shop
- Flag: `entity.seasoned-complete`

**Total XP: 100 → Shipwright Level 1 ("Apprentice Shipwright")**

**Recipes Unlocked by Shipwright trait:**
- Marine varnish (cook pot): 18 resin + 1L oil / 6 rendered fat
- Oiled canvas (barrel, 24h): 4 linen + 1L oil
- Waxed canvas (grid): oiled canvas + beeswax
- Oiled canvas sail (grid): 20 oiled canvas + 4 rope
- Waxed canvas sail (grid): 20 waxed canvas + 4 rope / oiled sail + beeswax
- Varnished planks (barrel, 24h): 4 seasoned planks + 1L marine varnish

**Related Game Mechanics:**
- Base game planks Dry-transition into `game:plank-seasoned` after 168 hours
- Seasoned planks + varnish → `game:plank-varnished`
- Both variants registered as proper game-domain planks (accepted by all 52 base game plank recipes)

---

### Dawn Marie - Rebuild the Orchard

**Prerequisites:** Rebuilding level > 3 OR Trade Goods complete

Deliver fruit tree cuttings (one-time per variety, any 6 completes quest):
Pink Apple, Red Apple, Yellow Apple, Cherry, Peach, Pear, Orange, Mango,
Breadfruit, Lychee, Pomegranate.

**Per delivery:** +20 Gardening XP, adds that cutting to shop list.

**Completion (after 6):**
- Rebuilding score +1
- Grow Pot added to shop

---

### Dawn Marie - Rebuild the Plantation

**Prerequisites:** Rebuilding level > 3

Deliver 10 seeds of any crop (one-time per crop, any 6 completes quest):
Rice, Soybean, Amaranth, Cassava, Peanut, Pineapple, Sunflower, Rye,
Parsnip, Turnip, Spelt, Onion, Flax, Carrot.

**Per delivery:** +10 Gardening XP, adds that seed to shop list.

**Completion (after 6):**
- Rebuilding score +1
- Gardening Book added to shop

---

### Celeste - Friendship: Pirate Tales

**Prerequisites:** Crimson Rose quest complete. Player has ≥1L of rum in an amphora.

*Player:* "I'd love to hear some of your stories. Rum?"

Celeste tells pirate jokes (Robert the Red, octopi, the plank). Consumes the rum.

**Reward:** Friendship +1

---

### Celeste - Rust Hunter

**Prerequisites:** Friendship > 0

Kill 10 rust monsters.

**Rewards:**
- Black bronze cutlass "Vengeance" (unique, named)
- Copper and bronze cutlasses added to shop
- Rebuilding score +1

---

### Celeste - Bear Hunter

**Prerequisites:** Friendship > 0

Bring 3 different bear pelts (Polar, Brown, Black, Panda, Sun).

**Per delivery:** +50 Bear Hunter XP

**Rewards:**
- Barbed arrows added to shop
- Bear Hunter training book added to shop
- Bear Hunter trait: +1 base armor, +2.5% movement speed

---

## Town Advancement

| Score | State | Effect |
|-------|-------|--------|
| 0 | Destitute | Starting state. Basic shops only. |
| 4 | Struggling | All shops add black bronze tool variants. Currency and prices increase. |

**Threshold variable:** `entity.rebuilding-complete` (set when `rebuilding-tier` reaches 4)

---

## Dialogue File Map

| NPC | Dialogue JSON | Trader JSON |
|-----|--------------|-------------|
| Morgan | `config/dialogue/morgan.json` | `entities/humanoid/trader-morgan.json` |
| Drake | `config/dialogue/drake.json` | `entities/humanoid/trader-drake.json` |
| Dawn Marie | `config/dialogue/dawnmarie.json` | `entities/humanoid/trader-dawnmarie.json` |
| Celeste | `config/dialogue/celeste.json` | `entities/humanoid/trader-celeste.json` |
| Potato King | `config/dialogue/potatoking.json` | `entities/humanoid/trader-potatoking.json` |
| Reva | `config/dialogue/reva.json` | (existing base) |

---

## Quest Items

| Item | Code | Role |
|------|------|------|
| Letter from Morgan | `seafarer:letter-morgan` | Morgan → Potato King |
| Signed Contract | `seafarer:signed-contract` | Potato King → Morgan |
| Map to Crimson Rose | `seafarer:map-crimsonrose` | Celeste → shipwreck |
| Potato King's Map | `seafarer:map-potato` | Potato King → ruin |
| Sealed Chest | `seafarer:sealed-chest` | shipwreck → Celeste |
| Training Book (Shipwright) | `seafarer:trainingbook-shipwright` | Drake's shop after both Tricks lessons |
| Outrigger Schematic | `seafarer:schematic-outrigger` | Drake's seasoned-wood reward |

## Crafting Items

| Item | File | Source |
|------|------|--------|
| Marine Varnish | `itemtypes/liquid/varnishportion-marine.json` | Cook pot: 18 resin + 1L oil / 6 rendered fat |
| Oiled Canvas | `itemtypes/resource/oiled-canvas.json` | Barrel (24h): 4 linen + 1L oil |
| Waxed Canvas | `itemtypes/resource/waxed-canvas.json` | Grid: oiled canvas + beeswax |
| Canvas Sail | `itemtypes/resource/canvas-sail.json` | Grid: 20 linen-normal-down + 4 rope (no training required) |
| Oiled Canvas Sail | `itemtypes/resource/oiled-canvas-sail.json` | Grid: 20 oiled canvas + 4 rope |
| Waxed Canvas Sail | `itemtypes/resource/waxed-canvas-sail.json` | Grid: 20 waxed canvas + 4 rope, or oiled sail + beeswax |
| Seasoned Plank | `game:plank-seasoned` (via patch) | Dry transition from base game planks (168h) |
| Varnished Plank | `game:plank-varnished` (via patch) | Barrel (24h): 4 seasoned planks + 1L marine varnish |

## Recipes and Patches

| Recipe | File | Type | Requires |
|--------|------|------|----------|
| Marine Varnish | `recipes/cooking/marinevarnish.json` | Cook pot | — |
| Oiled Canvas | `recipes/barrel/oiled-canvas.json` | Barrel (24h seal) | — |
| Varnished Plank | `recipes/barrel/varnished-plank.json` | Barrel (24h seal) | — |
| Waxed Canvas | `recipes/grid/waxed-canvas.json` | Grid (shapeless) | — |
| Canvas Sails | `recipes/grid/canvas-sail.json` | Grid (shapeless) | — |
| Plank Seasoning | `patches/plank-seasoning.json` | Dry transition (168h) | — |
| Plank Variants | `patches/plank-variants.json` | Variant expansion | — (adds seasoned/varnished to game:plank and 27 other wood-typed blocks) |

## Training System

Quests award XP to professions via the Training System (`Systems/Training/`):

| Profession | Training | Unlocks at Level 1 |
|-----------|----------|---------------------|
| Carpentry | Shipwright (100 XP) | Apprentice Shipwright trait, recipe gates |
| Cooking | Brewer (100 XP) | (not yet wired) |
| Cooking | Pie Master (100 XP) | (not yet wired) |
| Hunting | Bear Hunter (150 XP, via Celeste) | +1 armor, +2.5% movespeed |
| Gardening | (via Dawn Marie) | Planned |

Training GUI: press **L** to open the Training Ledger.
