# Celeste - Retired Captain

> **Source of truth.** This file mirrors the dialog wired up in
> `assets/seafarer/config/dialogue/celeste.json`. All dialog lines here match
> the lang keys in `assets/seafarer/lang/en.json` -- keep them in sync when
> editing.

## Character

- **Role:** Retired ship captain, cartographer, former pirate
- **Personality:** Cynical and world-weary. Had to maintain a firm hand to keep order at sea. Has seen and done more than most can imagine. Warms up slowly, mostly through shared drinks and shared danger.
- **Story:** As a kid, Celeste spent hours in her father's workshop taking in every map and chart and watching him work. She signed up on the first ship she could find when she was old enough, with her father's trusty monocle and a backpack filled with books and maps. Less than a month out of port she was captured by pirates and forced to work on their ship. Being smart and cunning, she organized a mutiny and took the ship for herself. From there she pursued a newfound love: piracy and adventure. Her bounty grew, her fame grew, her riches grew. The Crimson Rose became an infamous vessel. She traveled the seven seas and visited ports around the world. She was on her way home to reconcile with her dying father when the storm sunk her vessel and dragged her to the rust world.

---

## Shop

**Base shop (`config/tradelists/villager-celeste.json`):**

| Role | Item | Notes |
|------|------|-------|
| Sells | `game:locatormap-treasures` (treasure map) | ~15 gears |
| Sells | `game:locatormap-dungeons` (dungeon map) | ~20 gears |
| Sells | `game:locatormap-treasurehunter` (treasure hunter's map) | ~25 gears |
| Buys  | Lore books, raw hides | see tradelist |

**Expanding shop (added by dialog triggers):**

| Unlock | Trigger | Item |
|--------|---------|------|
| Crimson Rose complete | `addSellingItem` | `game:shovel-copper` |
| Rust Hunter complete  | `addSellingItem` | `seafarer:cutlass-copper`, `seafarer:cutlass-tinbronze` |
| Bear Hunter complete  | `addSellingItem` | `seafarer:arrow-barbed-copper`, `seafarer:trainingbook-bearhunter` (+ one-time gift of 24 × `seafarer:arrow-barbed-copper`) |

**Dialog-based exchange (not a trade-slot):**

| Unlock | Trigger | Exchange |
|--------|---------|----------|
| Crimson Rose complete | dialog `takefrominventory` + `giveitemstack` | 1L (100 portions) of `seafarer:spiritportion-whiterum` or `-darkrum` → 1 × `game:locatormap-treasures` |

---

## General Dialogue

**First meeting** (`dialogue-celeste-welcome`):
> "Another soul washing up on the shoals. I'd offer you a drink but the keg ran dry months ago."

**Welcome back** (`dialogue-celeste-welcomeback`):
> "Back again."

**Name** (`dialogue-celeste-name`):
> "Celeste. Former captain of the Crimson Rose. Former buccaneer, adventurer and explorer."

**Profession** (`dialogue-celeste-profession`):
> "Captain."

**About maps** (`dialogue-celeste-maps-info`):
> "I have a few charts and maps around. Not doing me any good anymore. You want one, make me an offer."

**Goodbye** (`dialogue-celeste-goodbye`):
> "Ya, we'll see if you come back."

---

## Quest: The Crimson Rose

**Prerequisites:** None.
**Completion flag:** `entity.crimsonrose-complete`.

### Intro

*Player* (`dialogue-celeste-ask-crimsonrose`): "I heard you lost a ship -- the Crimson Rose?"

*Celeste* (`dialogue-celeste-crimsonrose-intro`):
> "Why do you lot keep bothering me with this nonsense. Sure, you want to help and I want my loot from the Crimson Rose."

### Accept

*Player* (`dialogue-celeste-accept-crimsonrose`): "I'll find your treasure."

*Celeste* (`dialogue-celeste-crimsonrose-start`):
> "Fine. Here's a map to where she went down. The chest should still be sealed -- it was built to survive worse than a sinking. Dig it up and bring it back to me."

*Gives:* `seafarer:map-crimsonrose` (×1).
*Sets:* `player.celeste-crimsonrose-started = true`.

### In Progress

*Player* (`dialogue-celeste-remind-quest`): "Where did the Crimson Rose go down again?"

*Celeste* (`dialogue-celeste-crimsonrose-reminder`):
> "Follow the map I gave you. The wreck is out there somewhere -- dig around the site and you should find the sealed chest. Don't come back without it."

### Completion

*Player* (`dialogue-celeste-have-chest`, condition: `seafarer:sealed-chest` in inventory): "I recovered the sealed chest from the Crimson Rose."

*Celeste* (`dialogue-celeste-crimsonrose-received`):
> "You actually got my loot back? Fine, fine. I'll donate it to Morgan. Maybe she can find a use for it -- what am I going to do with it now anyway."

*Celeste* (`dialogue-celeste-crimsonrose-complete`):
> "There. Happy? I've told Morgan she can have the lot. And I suppose I can part with a few tools from my collection. You've earned that much."

*Takes:* `seafarer:sealed-chest`.
*Sets:* `entity.crimsonrose-complete = true`.

**Rewards:**

- `addSellingItem` `game:shovel-copper` (stock ~2, price ~5)
- Unlocks the rum-for-treasure-map dialog exchange
- Friendship +1 (sets `entity.celeste-friendly = true`)
- Rebuilding tier +1

### Post-Quest

*Celeste* (`dialogue-celeste-crimsonrose-already-done`):
> "You already brought back my chest. I don't have anything else buried out there, if that's what you're wondering."

---

## Dialog Exchange: Rum for a Treasure Map

**Prerequisites:** Crimson Rose complete (`entity.crimsonrose-complete = true`). Player holds 100 portions (1 L) of `seafarer:spiritportion-whiterum` or `-darkrum`.

> **Liquid detection note:** the dialog condition checks loose `spiritportion` items; rum stored inside an amphora as `ucontents` is not visible to the `player.inventory` condition. Players need the portion item in hand or on a utility belt.

*Player* (`dialogue-celeste-offer-rum-whiterum`): "I'll trade a bottle of white rum for a treasure map."
*Player* (`dialogue-celeste-offer-rum-darkrum`): "I'll trade a bottle of dark rum for a treasure map."

*Celeste* (`dialogue-celeste-rumtrade-thanks`):
> "Aye, a fair trade. Here's a map -- should keep you busy a while."

*Takes:* 100 × `seafarer:spiritportion-whiterum` **or** 100 × `seafarer:spiritportion-darkrum`.
*Gives:* 1 × `game:locatormap-treasures`.

---

## Friendship: Pirate Tales

**Prerequisites:** Crimson Rose complete. Player holds 100 portions of white or dark rum. Single-shot (`entity.celeste-piratetales-told`).

*Player* (`dialogue-celeste-ask-pirate-tales`): "I'd love to hear some of your stories. Rum?"

*(Celeste takes 100 portions of whichever rum is offered.)*

*Celeste* (`dialogue-celeste-piratetales-pour`):
> "Well, if you're offering."

*Celeste* (`dialogue-celeste-piratetales-mate`):
> "Did I ever tell you about my first mate, Robert the Red? He used to wear a little steering wheel on his belt."

*Player* (`dialogue-celeste-piratetales-why`): "Why?"

*Celeste* (`dialogue-celeste-piratetales-wheel`):
> "He said it was driving his nuts. *Laughs loudly.*"

*Player* (`dialogue-celeste-piratetales-terrible`): "That was terrible."

*Celeste* (`dialogue-celeste-piratetales-booty`):
> "You think that's bad? The first time we met, he tried to grab my booty."

*Player* (`dialogue-celeste-piratetales-notexpecting`): "I wasn't expecting that one."

*Celeste* (`dialogue-celeste-piratetales-hooked`):
> "Neither was he. Who'd think a one-handed guy would do that? But what could I do? He had me hooked."

*Player* (`dialogue-celeste-piratetales-stop`): "Please stop."

*Celeste* (`dialogue-celeste-piratetales-arrrated`):
> "I probably should. It gets a little Arrr-rated from here on."

*Player* (`dialogue-celeste-piratetales-plank`): "I want to walk the plank."

*Celeste* (`dialogue-celeste-piratetales-octopi`):
> "Watch out for octopi down there -- they're the most well-armed creatures of the deep."

**Reward:** Friendship +1 (sets `entity.celeste-friendly = true` if not already). Consumes 100 portions of rum.

---

## Quest: Rust Hunter

**Prerequisites:** `entity.celeste-friendly = true` (earned via Crimson Rose, Pirate Tales, or any prior friendship grant).
**Completion flag:** `entity.rusthunter-complete`.

### Intro

*Player* (`dialogue-celeste-ask-rusthunter`): "I hear you're looking to make the port safer."

*Celeste* (`dialogue-celeste-rusthunter-intro`):
> "I am. Those rust monsters attack every night. I do my best, but maybe if we thin their numbers it'll help. Bring me their rotten remains as proof -- ten should do."

> **Kill-tracking note:** the dialog engine has no on-kill trigger, so the quest uses drops as proof-of-kill. `game:rot` is the canonical drifter drop and stands in for the "rust monsters" flavor.

### Accept

*Player* (`dialogue-celeste-accept-rusthunter`): "I'll thin the herd."

*Celeste* (`dialogue-celeste-rusthunter-start`):
> "Good. Don't come back empty-handed."

*Sets:* `player.celeste-rusthunter-started = true`.

### In Progress

*Player* (`dialogue-celeste-deliver-rot`, condition: `game:rot` in inventory): "I've got more rot for you."
*Player* (`dialogue-celeste-rusthunter-status`): "How many do I need to kill?"

*Celeste, status* (`dialogue-celeste-rusthunter-statusreport`):
> "That's {entity.rustkills} of 10 rust monsters. Keep at it."

*Celeste, per delivery* (`dialogue-celeste-rusthunter-progress`):
> "Aye, noted. Keep going."

Each delivery takes 1 × `game:rot` and increments `entity.rustkills`. At 10 deliveries `entity.rusthunter-threshold` flips to `true`.

### Completion

*Celeste* (`dialogue-celeste-rusthunter-reward`):
> "Quieter nights, finally. Here -- I had this tucked away for a rainy day. Consider it earned."

*Gives:* 1 × `seafarer:cutlass-vengeance` (unique black bronze cutlass).

**Rewards:**

- `addSellingItem` `seafarer:cutlass-copper`
- `addSellingItem` `seafarer:cutlass-bronze`
- Friendship +1
- Rebuilding tier +1

### Post-Quest

*Celeste* (`dialogue-celeste-rusthunter-already-done`):
> "You've done your part against the rust. The port sleeps easier now."

---

## Quest: Bear Hunter

**Prerequisites:** `entity.celeste-friendly = true`.
**Completion flag:** `entity.bearhunter-complete`.

### Intro

*Player* (`dialogue-celeste-ask-bearhunter`): "Can you show me some hunting tricks?"

*Celeste* (`dialogue-celeste-bearhunter-intro`):
> "No. Maybe. I don't teach beginners. But show me what you can do -- bring me back 3 different bear pelts, head and all, and I'll show you some tricks."

### Accept

*Player* (`dialogue-celeste-accept-bearhunter`): "Challenge accepted."

*Celeste* (`dialogue-celeste-bearhunter-start`):
> "Good. Three different bears, head still on the pelt. I'll know if you skimp."

*Sets:* `player.celeste-bearhunter-started = true`.

### Objectives

Deliver any three distinct `game:hide-pelt-bear-<type>-complete` items. Each type may only be turned in once (per-type flag).

| Pelt code | Lang key |
|-----------|---------|
| `game:hide-pelt-bear-polar-complete` | `dialogue-celeste-deliver-pelt-polar` |
| `game:hide-pelt-bear-brown-complete` | `dialogue-celeste-deliver-pelt-brown` |
| `game:hide-pelt-bear-black-complete` | `dialogue-celeste-deliver-pelt-black` |
| `game:hide-pelt-bear-panda-complete` | `dialogue-celeste-deliver-pelt-panda` |
| `game:hide-pelt-bear-sun-complete`   | `dialogue-celeste-deliver-pelt-sun`   |

**Per delivery:**

- `takefrominventory` the pelt
- `awardTrainingXP` `bearhunter` +50 XP
- Increment `entity.bearhunter-count`
- Celeste (`dialogue-celeste-bearhunter-pelt-thanks`): "A fine pelt. I'll find a wall for it."

At count 3, `entity.bearhunter-threshold` flips to `true`.

### Completion

*Celeste* (`dialogue-celeste-bearhunter-reward`):
> "These'll look good on my wall. Barely had anything to wear -- at least now I can bare all."

**Rewards:**

- `giveitemstack` 24 × `seafarer:arrow-barbed-copper` (one-time bonus for completing the quest)
- `addSellingItem` `seafarer:arrow-barbed-copper` (stock ~12, price ~2)
- `addSellingItem` `seafarer:trainingbook-bearhunter`
- Friendship +1
- Rebuilding tier +1

### Post-Quest

*Celeste* (`dialogue-celeste-bearhunter-already-done`):
> "You've earned your stripes, hunter. The wall's full enough."

> **Trait note:** the "Bear Hunter" passive (+1 base armor, +2.5% movement speed) is listed as a stretch goal in the original spec and is **not** wired up by this dialog. It would require a custom entity behavior triggered by the training book.

---

## Quest Variables

| Variable | Scope | Purpose |
|----------|-------|---------|
| `player.hasmetceleste` | player | First meeting flag |
| `player.celeste-crimsonrose-started` | player | Crimson Rose quest accepted |
| `player.celeste-rusthunter-started`  | player | Rust Hunter quest accepted |
| `player.celeste-bearhunter-started`  | player | Bear Hunter quest accepted |
| `entity.crimsonrose-complete` | entity | Crimson Rose quest complete |
| `entity.celeste-friendship`   | entity | Friendship counter (0..N) |
| `entity.celeste-friendly`     | entity | Boolean gate: friendship ≥ 1 |
| `entity.celeste-piratetales-told` | entity | Pirate Tales shared (one-shot) |
| `entity.rustkills` | entity | Rust monster kill counter (rot deliveries) |
| `entity.rusthunter-threshold` | entity | Rust Hunter threshold reached (≥ 10) |
| `entity.rusthunter-complete` | entity | Rust Hunter quest complete |
| `entity.bearhunter-polar-delivered` | entity | Polar pelt delivered |
| `entity.bearhunter-brown-delivered` | entity | Brown pelt delivered |
| `entity.bearhunter-black-delivered` | entity | Black pelt delivered |
| `entity.bearhunter-panda-delivered` | entity | Panda pelt delivered |
| `entity.bearhunter-sun-delivered`   | entity | Sun pelt delivered |
| `entity.bearhunter-count` | entity | Total distinct bear pelts delivered |
| `entity.bearhunter-threshold` | entity | Bear Hunter threshold reached (≥ 3) |
| `entity.bearhunter-complete` | entity | Bear Hunter quest complete |
| `entity.rebuilding-tier` | entity | Shared Tortuga rebuilding counter |

## Quest Items

| Item | Code | Status | Role |
|------|------|--------|------|
| Map to the Crimson Rose | `seafarer:map-crimsonrose` | exists | Given to player → leads to sealed chest |
| Sealed Chest | `seafarer:sealed-chest` | exists | Found in shipwreck → delivered to Celeste |
| White Rum portion | `seafarer:spiritportion-whiterum` | exists | Rum-for-map exchange; Pirate Tales cost |
| Dark Rum portion | `seafarer:spiritportion-darkrum` | exists | Rum-for-map exchange; Pirate Tales cost |
| Treasure Map | `game:locatormap-treasures` | exists | Rum-trade output |
| Drifter Rot | `game:rot` | exists | Rust Hunter proof-of-kill |
| Bear pelts (5 variants) | `game:hide-pelt-bear-<type>-complete` | exists | Bear Hunter deliveries |
| Cutlass (all metals) | `seafarer:cutlass-{metal}` | exists | Generic sailor's blade, variants: copper, tinbronze, bismuthbronze, blackbronze, gold, silver, iron, meteoriciron, steel |
| Vengeance (named cutlass) | `seafarer:cutlass-vengeance` | **TODO** — named variant, deferred | Rust Hunter reward (unique) |
| Barbed Arrow (all metals) | `seafarer:arrow-barbed-{metal}` | exists | Bleeds target on hit (1.5 HP over 6s via `EnumDamageOverTimeEffectType.Bleeding`). Variants: copper → steel |
| Barbed Arrowhead | `seafarer:arrowhead-barbed-{metal}` | exists | Smithed 4-per-ingot on an anvil; `requiresTrait: bearhunter`. Grid-assembled into a barbed arrow with stick + feather |

## Crafting Gate: Bear Hunter Training

Barbed arrowhead smithing is gated by the `bearhunter` trait. The trait is granted at `bearhunter` training level 1 (100 XP). XP sources:

- Celeste's Bear Hunter quest: +50 XP per pelt delivery (3 pelts = 150 XP; first two hit the threshold)
- `seafarer:trainingbook-bearhunter`: +100 XP (unlocks trait instantly when the book is read — see `training-xp.json`)

The training is defined under the `hunting` profession in `config/professions.json`. The trait applies to both:

- `recipes/smithing/arrowhead-barbed.json` — direct anvil smithing (4 heads per ingot)
- *(Deferred)* `recipes/clayforming/arrowheadbarbedmold.json` — a clay mold for casting. Not yet wired: would require a new 3D shape for the mold and raw/fired block variants using `class: BlockToolMold`. The smithing path is sufficient for gameplay; the mold path is an obvious expansion if metal conservation matters.
| Barbed Arrow | `seafarer:arrow-barbed` | **TODO** — new item | Bear Hunter reward shop slot |
| Bear Hunter Training Book | `seafarer:trainingbook-bearhunter` | **TODO** — new item | Bear Hunter reward shop slot |

The items marked **TODO** are referenced by the dialog but not yet defined as assets; the dialog wiring still works, but those entries will fail to resolve at runtime until the itemtypes + lang + textures are added.
