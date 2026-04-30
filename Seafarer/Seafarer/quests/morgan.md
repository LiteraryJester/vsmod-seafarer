# Morgan - Provisioner

## Character

- **Role:** Provisioner, unofficial mayor of Tortuga
- **Personality:** Optimistic and hopeful. Despite the pain and struggles she still believes things can get better. A natural organizer who rallies what's left of the town.
- **Story:** One of the original inhabitants of Tortuga when the port was torn from the old world. She remembers the good days of ships traveling the world, bringing wealth and people. Countless sails dotting the harbour; delicacies and pleasures for anyone with enough coin. Now there is nothing left but the dream and scattered survivors. The rust monsters and temporal storms leave little behind. She stepped up to lead the rebuilding after the governor was killed.

---

## Shop

**Sells:**
| Item | Price |
|------|-------|
| Crate | 6 |
| Chest | 6 |
| Bucket | 2 |
| Barrel | 8 |
| Reed basket | 2 |
| Bandages | 2 |

**Buys:**
| Item | Price |
|------|-------|
| Amphora of white rum | 8 |
| Amphora of dark rum | 10 |
| Amphora of olive oil | 6 |
| Amphora of coconut oil | 6 |
| Salt | 1 |
| Sugar | 2 |

---

## General Dialogue

**First meeting:**
> "Well now, a fresh face on the docks! Welcome to Tortuga, voyager. If you've got spirits to sell or need supplies for the voyage ahead, I'm your woman."

**Welcome back:**
> "Back again? Good to see you. What can I do for you today?"

**Name:**
> "Name's Morgan. Any problems or needs come see me. I keep things organized."

**Profession:**
> "I guess I'm the unofficial town mayor now. Since the governor got eaten by a temporal rift. Doesn't require much work at the moment other than looking after the docks and warehouse."

**About Tortuga:**
> "Tortuga's been a port for traders, seafarers, and pirates for years -- or at least it was. A temporal typhoon hit the port, tearing through the buildings and ripping us out of the old world and into this rust world. We just woke up here. I'm sure your story is much the same."

**About the other residents:**
> "Drake's our shipwright -- bit gruff but he knows wood. Dawn Marie tends the plantation -- she's been through a lot. Celeste is a retired captain -- careful with that one, she bites. And if you head out east to the mountain, you might run into the Potato King. Don't ask."

**Goodbye:**
> "Fair winds to you. Come back when you need restocking."

---

## Quest: Reopen the Mine

**Prerequisites:** None (available from first meeting)

### Intro

*Player:* "Can I help with the rebuilding effort?"

> "I love the spirit, voyager. Have a talk with the other residents -- they'll have some work for you too. There was an old miner around here, went a little mad after we ran out of potatoes. If you can convince him to start mining again we'll be able to start smelting ingots."

### Accept

*Player:* "I'll track down the miner for you."

> "That's the spirit! Take this letter -- show it to the old Potato King. He lives under the mountain outside of town. Stubborn as bedrock but maybe he'll listen if he knows it's from me."

*Gives:* Letter from Morgan (`seafarer:letter-morgan`)

### Decline

*Player:* "Maybe later."

> *Morgan nods and returns to her work.*

### In Progress

**Turn in signed contract:**
*Player:* "I have a signed contract from the Potato King."
> See Completion below.

**Request replacement letter (if lost):**
*Player:* "I lost the letter you gave me."
> "Losing things at sea is nothing new. Here, take another copy. Don't lose this one."

*Gives:* Letter from Morgan (replacement)

**Ask for reminder:**
*Player:* "What was I supposed to do again?"
> "Find the Potato King and show him my letter. Convince him to start mining again. We need him if we're ever going to produce ingots."

### Completion

> "A signed contract! There are a lot of diagrams about conveyor belts and automation plans in here. But he seems willing to work again. You've made a real difference, voyager."

> "I'm surprised -- didn't really think you'd get this done. The first shipment of ore has already arrived. We can start producing copper ingots, nails, and plates again."

**Rewards:**
- Copper ingots, nails, and plates added to shop
- Rebuilding score +1

### Post-Quest

> "You've already done so much for us, voyager. The mine is running again thanks to you. Talk to the others -- they could use your help too."

---

## Quest Variables

| Variable | Scope | Purpose |
|----------|-------|---------|
| `player.hasmetmorgan` | player | First meeting flag |
| `player.morgan-mine-started` | player | Mine quest accepted |
| `entity.mine-quest-complete` | entity | Mine quest complete |
| `entity.rebuilding-tier` | entity | Town rebuilding progress |
| `entity.rebuilding-complete` | entity | Town fully rebuilt |

## Quest Items

| Item | Code | Role |
|------|------|------|
| Letter from Morgan | `seafarer:letter-morgan` | Given to player → delivered to Potato King |
| Signed Contract | `seafarer:signed-contract` | Received from Potato King → delivered to Morgan |
