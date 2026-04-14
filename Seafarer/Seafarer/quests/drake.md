# Drake - Shipwright

> **Quest system.** Drake's three quests are defined in
> `assets/seafarer/config/quests/drake-*.json` and driven by
> `Quests/QuestSystem.cs`. The dialog file fires `questStart` and
> `questDeliver` triggers; the system handles item take-in, progress,
> per-objective rewards, and completion rewards. Per-player state lives on
> `player.WatchedAttributes["seafarer:questlog"]`, mirrored to flat keys like
> `player.quest-drake-tradeship-status` for dialog conditions.

## Character

- **Role:** Shipwright, Tortuga's only dockworker
- **Personality:** Practical, no-nonsense craftsman. Respects hard work over words. Gruff exterior but fair.
- **Story:** Drake worked as a carpenter from an early age. Lured by the promise of easy coin, he signed up for the crew of a pirate ship and never looked back. He ended up spending 3 years in prison before getting a job on the docks maintaining the vessels coming into Tortuga. Has a collection of adventure novels and blueprints for vessels stashed in the back of his workshop.

---

## Shop

**Sells:**
| Item | Price |
|------|-------|
| Rope | 2 |

**Buys:**
| Item | Price |
|------|-------|
| Linen | 2 |
| Seasoned wood (plank-seasoned) | 3 |

**Expanding shop (after quests):**
- Boards turned in: copper chisel, copper hammer
- Rope turned in: copper saw, copper shears
- Linen turned in: copper pickaxe, copper prospecting pick
- Both Tricks lessons complete: Apprentice Shipwright's training book
- Seasoned wood quest complete: outrigger schematic

---

## General Dialogue

**First meeting:**
> "Hm. Another one looking for a ship, I'd wager. Well, you've come to the right place. I'm the only shipwright in Tortuga, and if you want anything bigger than a log raft, you'll need what I sell."

**Welcome back:**
> "Back again. Need more supplies for that vessel of yours?"

**Name:**
> "Drake. I build ships. Or rather, I sell what you need to build them yourself. Schematics, fittings, rope, planks -- the works."

**Profession:**
> "I'm a shipwright. Any fool can lash logs together for a raft, but a real ship takes proper materials and know-how. Bring me wood -- especially rare timber -- and I'll trade you what you need."

**About ships:**
> "You probably think lashing logs together is all it takes to build a boat. But trust me, you want it to last, you need seasoned wood, resin, and caulk."

**Goodbye:**
> "Bring wood next time. I always need more."

---

## Quest: New Trade Ship

**Prerequisites:** None

### Intro

*Player:* "Can I help build a new trade ship?"

> "Well voyager, if you want to help us get trade back up and running what we really need is a new sailboat. I can build a magnificent one if you can get the materials together. I've got some old tools kicking around somewhere -- while you're getting the materials I'll dig them up."

### Accept

*Player:* "I'll gather the materials."

> "Right then. I need birch boards for the hull, rope for the rigging, and linen for the sails. Bring them to me as you find them -- I'll keep a tally."

### Decline

*Player:* "Maybe later."

> *Drake grunts and returns to coiling his rope.*

### Objectives (parallel - complete in any order)

#### Birch Boards (298 needed)

*Player:* "I have birch boards for the hull."
> "Good timber. I'll add these to the pile."

*Progress:* "That's {entity.boards}/298 birch boards. Keep them coming."

*Complete:* "That's all the boards we need! The hull is taking shape. Here -- found these old tools while clearing out the workshop."

**Rewards:** Copper chisel and copper hammer added to shop.

#### Rope (27 needed)

*Player:* "I brought rope for the rigging."
> "Solid rope. This'll hold."

*Progress:* "That's {entity.rope}/27 coils of rope. Need more for the rigging."

*Complete:* "Rigging is sorted! Dug up a saw and shears for your trouble."

**Rewards:** Copper saw and copper shears added to shop.

#### Linen (21 needed)

*Player:* "Here's linen for the sails."
> "Good quality cloth. Should make fine sails."

*Progress:* "That's {entity.linen}/21 linen squares. The sails need more."

*Complete:* "The sails are done! Found a pickaxe and prospecting pick in the back of the shop -- they're yours."

**Rewards:** Copper pickaxe and copper prospecting pick added to shop.

### Status Check

*Player:* "How's the ship coming along?"
> "Hull needs {entity.boards}/298 boards. Rigging needs {entity.rope}/27 rope. Sails need {entity.linen}/21 linen. Bring what you can."

### Completion (all 3 objectives done)

> "I'm surprised, didn't really think you'd get this done -- but you've made a huge difference. She's a beauty. Trade will be flowing again before you know it."

**Rewards:** Rebuilding score +1.

### Post-Quest

> "The ship's built and ready to sail. You did good work, voyager."

---

## Quest: Tricks of the Trade

**Prerequisites:** Trade Ship built OR rebuilding score > 3

### Intro

*Player:* "Can you teach me to build ships?"

> "Hmm, I guess I have the time now that we aren't struggling."
>
> "Three most important things you probably don't know: air-seasoning your wood will make it lighter and stronger."
>
> "Treating the hull with resin or shellac is essential if you don't want more leaks than a cheesecloth."
>
> "Lastly, oiling and waxing your sail will make it last longer and go faster."
>
> "Let's get started. Bring me 18 resin and 6 rendered fat, that's all you need, and I'll teach you how to make marine varnish."
>
> "Oiled canvas is what we'll be making the sail out of. Bring me 20 linen and 1L oil."

### Lesson 1: Marine Varnish

**Ingredients required:** 18 resin + 6 rendered fat (both taken from inventory)

*Player:* "Here are the varnish ingredients."

> "Great, I've got an old crusty pot we can use. We'll just cook it up until the resin melts. If you don't have fat, oil works just as well."
>
> "Just be sure to stir it until it's thick and melted, then you can apply it directly to wood or the hull of your ship."
>
> "Your ship will be a bit faster but mainly it'll be watertight and won't sink like a stone."

**Rewards:**
- Shipwright XP +50
- Bucket of 2L marine varnish
- If both Tricks lessons complete: Apprentice Shipwright's training book added to shop

### Lesson 2: Oiled Canvas

**Ingredients required:** 20 linen + 1L oil (taken from inventory)

*Player:* "Can you show me how to make the oiled canvas now?"

> "Sure, it's pretty simple. You take the canvas, shove it in a barrel of oil in a ratio of 4 linen to 1L oil, and seal it overnight."
>
> "You'll get some nice waterproof canvas. If you rub wax onto it, you can make it even stronger."
>
> "Here, take that oiled canvas and sew in 4 meters of rope. Bring me back that sail to show me you know what you're doing."

**Rewards:** 20 oiled canvas.

### Lesson 3: Show Oiled Sail

**Prerequisites:** Oiled Canvas lesson complete. Player has crafted 1 oiled-canvas-sail.

*Player:* "How does this look?"

> "Not bad. Your stitching could use work, and you'll want to reinforce it here and here to prevent tearing, but you've got the knack for it."

**Rewards:**
- Shipwright XP +50 (reaches level 1 = Apprentice Shipwright)
- If both Tricks lessons complete: Apprentice Shipwright's training book added to shop

### Seasoned Wood Quest

**Prerequisites:** Shipwright level ≥ 1 (both previous lessons complete = 100 XP).

*Player:* "I've got the knack of shipbuilding, now can you teach me how to build a decent boat?"

> "Sure. Bring me 160 planks of seasoned wood and I'll teach you to make an outrigger -- a fast, lightweight fishing boat."
>
> "Seasoning wood takes a long time, but it's well worth it if you want a boat to last. Stack up your lumber outside in the wind, cover it to keep the rain off, and in a month or two you'll have seasoned wood."

### Return Seasoned Wood

**Ingredient required:** 160 seasoned planks.

*Player:* "Here's the seasoned wood you asked for."

> "You really are a hard worker. Here, take this schematic for an outrigger. I'll sell you a new one if you need it."

**Rewards:**
- Outrigger schematic (given directly)
- Outrigger schematic added to shop

---

## Quest Variables

| Variable | Scope | Purpose |
|----------|-------|---------|
| `player.hasmetdrake` | player | First meeting flag |
| `player.drake-tradeship-started` | player | Trade ship quest accepted |
| `entity.boards` / `entity.boards-complete` | entity | Boards counter / flag |
| `entity.rope` / `entity.rope-complete` | entity | Rope counter / flag |
| `entity.linen` / `entity.linen-complete` | entity | Linen counter / flag |
| `entity.tradeship-complete` | entity | Trade ship built |
| `player.drake-tricks-started` | player | Tricks quest started |
| `entity.varnish-complete` | entity | Varnish lesson complete |
| `entity.canvas-complete` | entity | Canvas lesson complete |
| `entity.sail-reviewed` | entity | Sail review complete |
| `entity.training-book-added` | entity | Training book unlocked |
| `entity.seasoned-started` | entity | Seasoned wood quest started |
| `entity.seasoned-complete` | entity | Seasoned wood delivered |

## Quest Items

| Item | Code | Role |
|------|------|------|
| Marine varnish | `seafarer:varnishportion-marine` | Reward from varnish lesson (in woodbucket) |
| Oiled canvas | `seafarer:oiled-canvas` | Reward from canvas lesson (20x) |
| Oiled canvas sail | `seafarer:oiled-canvas-sail` | Player crafts → returns for review |
| Outrigger schematic | `seafarer:schematic-outrigger` | Reward from seasoned wood quest |
| Apprentice Shipwright book | `seafarer:trainingbook-shipwright` | Added to shop after both lessons |
