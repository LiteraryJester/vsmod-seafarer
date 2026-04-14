# Dawn Marie - Plantation Owner

## Character

- **Role:** Plantation owner and gardener
- **Personality:** Depressed. Wants to believe and dream, but is still grieving everything she lost. Speaks softly, avoids eye contact.
- **Story:** A free-spirited artist who stopped in the port for a week to capture its beauty on canvas before sailing onwards to England. When the port was torn into the rust world, she was trapped and left with nothing. Her hope and optimism have faded. Her dreams of painting for the royal court are now fleeting as she struggles to grow what little she can on this harsh island.

---

## Shop

**Sells:**
| Item | Price |
|------|-------|
| Butterfly net (copper) | 6 |
| Sugar cane seeds | 2 |
| Olive tree cutting | 8 |

**Buys:**
| Item | Price |
|------|-------|
| Paintings | 8 |

**Expanding shop (after quests):**
- Salt quest: copper knife, copper cleaver
- Sugar quest: copper hoe, copper scythe
- Orchard quest: each delivered fruit tree cutting added to sell list
- Plantation quest: each delivered seed added to sell list
- All orchard trees rebuilt: Grow pot added to sell list
- All plantation crops rebuilt: Gardening book added to sell list

---

## General Dialogue

**First meeting:**
> "Oh... a visitor. I didn't think anyone new came here anymore. I suppose you're looking to trade? I don't have much, but what I have is honest work."

**Welcome back:**
> "You came back. I wasn't sure you would."

**Name:**
> "Dawn Marie. I used to paint, portraits, landscapes, anything beautiful. Now I grow what I can and try not to think about what I've lost."

**Profession:**
> "I tend the plantation at the edge of town. Flowers, sugar cane, a few olive trees. I sell seeds and cuttings, and I'll buy any paintings you find. They remind me of better days."

**About Tortuga:**
> "I was only supposed to be here for a week. One more painting, then onwards to England. The storm changed that. Sometimes I still wake up expecting to hear my mother's voice calling me for breakfast."

**About her art:**
> "I haven't touched a brush in months. What's the point? Nobody's left to appreciate it. Though... if you find any old paintings out there, bring them to me. I'll pay well. They remind me there was once beauty in the world."

**Goodbye:**
> "Take care out there. The world is harder than it used to be."

---

## Quest: In Need of Trade Goods

**Prerequisites:** None

### Intro

*Player:* "Is there anything I can do to help?"

> "Morgan is full of dreams despite everything. I remember when I used to believe in hope. I don't have much in the way of skills. But everyone would be willing to trade for salt and sugar if I had enough."

### Accept

*Player:* "I'll bring you salt and sugar."

> "You'd do that? I... thank you. Bring me salt and sugar, as much as you can find. With enough of both I can actually start trading properly."

### Decline

*Player:* "Maybe later."

> *She nods silently and turns back to her flowers.*

### Objectives (parallel - complete in any order)

#### Salt (64 needed)

*Player:* "I have salt for you."
> "Salt. Good. Every bit helps."

*Progress:* "That's {entity.salt} of 64 salt. I need more before I can start trading it."

*Complete:* "That's all the salt I need! I can finally offer something worth trading. Here, take these, I found them while clearing the old storehouse."

**Rewards:** Copper knife and copper cleaver added to shop.

#### Sugar (64 needed)

*Player:* "I brought sugar."
> "Sugar. Sweet of you."

*Progress:* "That's {entity.sugar} of 64 sugar. Keep it coming."

*Complete:* "Enough sugar at last. I can find plenty of uses for it. Found some old tools, they're yours."

**Rewards:** Copper hoe and copper scythe added to shop.

### Status Check

*Player:* "How much salt and sugar do you still need?"
> "I need {entity.salt}/64 salt and {entity.sugar}/64 sugar. Bring what you can."

### Completion (both objectives done)

> "You actually did it. Why? I... I don't understand why you'd help someone like me. But thank you. Maybe Morgan is right, maybe things can get better."

**Rewards:** Rebuilding score +1.

### Post-Quest

> "You've already done more than I could have asked for. The trade goods are flowing now. Thank you."

---

## Quest: Rebuild the Orchard

**Prerequisites:** Rebuilding level > 3 OR both Salt and Sugar quests completed

### Intro

*Player:* "Need any help with the orchard?"

> "There were so many beautiful fruit trees here at one time. I used to spend hours painting under them. Bring me fruit tree cuttings and I can start replanting."

### Objectives (any 6 deliveries completes quest)

Each fruit tree cutting can be delivered once. Accepted varieties:

- Pink Apple
- Red Apple
- Yellow Apple
- Cherry
- Peach
- Pear
- Orange
- Mango
- Breadfruit
- Lychee
- Pomegranate

**Per delivery:**
- +20 Gardening XP
- Adds that cutting to Dawn Marie's sell list

**After 6 cuttings delivered:**
- Orchard marked as rebuilt
- Rebuilding score +1
- Adds Grow Pot to shop inventory

### Delivery

*Player:* "I have a {fruit} cutting for you."
> "A {fruit} tree! Those were my favorite in spring. Thank you, I'll plant it right away."

### Completion

> "The orchard is alive again. I can hardly believe it. Paint it someday, voyager, it deserves to be remembered."

---

## Quest: Rebuild the Plantation

**Prerequisites:** Rebuilding level > 3

### Intro

*Player:* "Need any help with the plantation?"

> "More food variety would help everyone out. Bring me 10 seeds of any crop and I can start growing them."

### Objectives (any 6 deliveries completes quest)

Accept 10 seeds of any of these crops:

- Rice
- Soybean
- Amaranth
- Cassava
- Peanut
- Pineapple
- Sunflower
- Rye
- Parsnip
- Turnip
- Spelt
- Onion
- Flax
- Carrot

**Per delivery:**
- +10 Gardening XP
- Adds that seed to Dawn Marie's sell list

**After 6 crops delivered:**
- Plantation marked as rebuilt
- Rebuilding score +1
- Adds Gardening Book to shop inventory

### Delivery

*Player:* "I have 10 {crop} seeds."
> "Good, good. I'll get these in the ground before the season changes."

### Completion

> "The fields will feed us again. Proper food, not just dried fish and hardtack."

---

## Quest Variables

| Variable | Scope | Purpose |
|----------|-------|---------|
| `player.hasmetdawnmarie` | player | First meeting flag |
| `player.dawnmarie-tradegoods-started` | player | Trade goods quest accepted |
| `entity.salt` / `entity.salt-complete` | entity | Salt delivery counter / flag |
| `entity.sugar` / `entity.sugar-complete` | entity | Sugar delivery counter / flag |
| `entity.tradegoods-complete` | entity | Trade goods quest complete |
| `entity.orchard-*-delivered` | entity | Per-cutting delivery flags |
| `entity.orchard-count` | entity | Total orchard deliveries |
| `entity.orchard-complete` | entity | Orchard fully rebuilt |
| `entity.plantation-*-delivered` | entity | Per-seed delivery flags |
| `entity.plantation-count` | entity | Total plantation deliveries |
| `entity.plantation-complete` | entity | Plantation fully rebuilt |
