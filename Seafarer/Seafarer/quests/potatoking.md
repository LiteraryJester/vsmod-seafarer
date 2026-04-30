# Potato King - Mad Miner

## Character

- **Role:** Miner and machinist, gone slightly mad
- **Personality:** Eccentric, dramatic, obsessed with potatoes. Speaks in grandiose terms about himself and his "kingdom" under the mountain. Genuinely skilled at mining and ore processing -- when motivated. Currently retired in a sulk.
- **Story:** Once ran the mine and smeltery for Tortuga, producing all the copper and bronze the port needed. After the temporal storms destroyed the island's potato supply, he lost his motivation and retreated under the mountain. Refuses to work until his beloved potato is found. Covers his isolation by pretending to an imaginary court of subjects (mine rats, mostly).

---

## Shop

**Sells:** *(none initially -- he's retired and bitter)*

**Buys:**
| Item | Price |
|------|-------|
| Potato | 3 |

---

## General Dialogue

**First meeting:**
> "Eh? Who's there? The Potato King holds no audiences at this hour! Not until the last potato is found! Go, go -- unless you bring tubers, there's nothing for you here."

**Welcome back:**
> "You again? Unless you've got something important, leave me to my work. Or rather, my lack of work."

**Name:**
> "They call me the Potato King. I used to run the mine and the smeltery."

**Profession:**
> "I was the finest ore processor in all of Tortuga! Copper, bronze, iron -- whatever the port needed, I produced. But what's the point of mining when there's not a single potato left to eat?"

**About the mine:**
> "Stopped the works. Let it rust. Let the rails seize. Without proper fuel in a miner's belly, there's no point. And there's no proper fuel without potatoes."

**About potatoes:**
> "The humble tuber! Boiled, mashed, fried, baked, chipped -- no greater food has ever been or will be. The temporal storms scattered every last seed across the ruins. Without them, the kingdom starves."

**Goodbye:**
> "Go. The King has much to brood upon."

---

## Quest: The Last Potato (sub-quest for Morgan's Reopen Mine)

**Prerequisites:** Player has Letter from Morgan in inventory.

### Letter Delivery

*Player:* "I have a letter from Morgan."

> "A letter from Morgan, eh? Let me see that... 'Dear old friend, please resume mining operations, the port needs you, blah blah blah.' She always was an optimist."

*Takes:* Letter from Morgan (`seafarer:letter-morgan`)

### Potato Quest Start

> "Fine. I'll make you a deal. There's one potato left -- ONE! -- somewhere in a ruined restaurant. The temporal storms scattered everything. Find me that potato and I'll sign whatever contract Morgan wants. Here's a map to where I last saw it."

*Gives:* Potato King's Map (`seafarer:map-potato`)

### Reminder

*Player:* "About that potato..."

> "The map I gave you shows the ruins. Find the last potato! It's out there somewhere. Without it, no deal."

### Potato Delivered

*Player:* "I found the last potato!"

> "Is that... is that really it? THE potato? Oh glorious day! Finally I can start planting potatoes again. Fish and chips, jacket potatoes, and crisps -- oh my!"

*Takes:* Potato (`game:vegetable-potato`)

### Contract Given

> "A deal is a deal. Here's the signed contract for Morgan. I have most of the machine working so it shouldn't take long to start turning out copper ore. Bronze will take a bit longer."

*Gives:* Signed Contract (`seafarer:signed-contract`)

### Post-Quest

> "Machines are humming. Tubers are sprouting. The kingdom prospers once more!"

---

## Quest Variables

| Variable | Scope | Purpose |
|----------|-------|---------|
| `player.hasmetpotatoking` | player | First meeting flag |
| `entity.letter-received` | entity | Morgan's letter delivered |
| `entity.contract-given` | entity | Signed contract issued |

## Quest Items

| Item | Code | Role |
|------|------|------|
| Letter from Morgan | `seafarer:letter-morgan` | Taken from player |
| Potato King's Map | `seafarer:map-potato` | Given to player → leads to potato |
| Potato | `game:vegetable-potato` | Taken from player |
| Signed Contract | `seafarer:signed-contract` | Given to player → delivered to Morgan |
