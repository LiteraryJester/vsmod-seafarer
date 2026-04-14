# Port of Tortuga

A small desolate port. Aged rotting hulls of vessels half submerged in the water. Signs point to a once thriving port but the broken and decaying buildings point to some disaster that was never recovered from.

---

## NPCs

- **Morgan** - Provisioner, leading town reconstruction
- **Dawn Marie** - Plantation owner, runs a small flower stand
- **Celeste** - Retired explorer and adventurer
- **Drake** - Shipwright
- **Potato King** - Mad miner/machinist

---

## NPC Details

### Morgan

- **Role:** Provisioner
- **Story:** One of the original inhabitants of the port when it was torn from the old world into this one. She remembers the good old days of ships traveling the whole world bringing wealth and people to Tortuga. Of countless sails dotting the harbour and delicacies and pleasures to indulge for anyone with enough coin. Now there is nothing left but the dream and scattered survivors. The rust monsters and temporal storms leaving little remaining.
- **Personality:** Optimistic and hopeful. Despite all the pain and struggles she still hopes things can get better.

**Sells:**

- Crate
- Chest
- Bucket
- Barrel
- Reed basket
- Bandages

**Buys:**

- Amphora of 10L white rum
- Amphora of 10L dark rum
- Sugar
- Salt
- Amphora of 10L olive oil
- Amphora of 10L coconut oil

---

### Dawn Marie

- **Role:** Plantation owner and gardener
- **Story:** A free spirited artist who stopped in the port for a week to capture its beauty on canvas before sailing onwards to England. When the port was torn into the rust world she was trapped and left with nothing. Her hope and optimism have faded. Her dreams of painting for the royal court now fleeting as she struggles to grow what little she can on this harsh island.
- **Personality:** Depressed. Wants to believe and dream but is still dealing with everything she lost.

**Sells:**

- Butterfly net
- Sugar cane seeds
- Corn cane seeds
- Olive tree cutting
- Pomigrant tree cutting

**Buys:**

- Paintings

---

### Celeste

- **Role:** Retired explorer and adventurer
- **Story:** As a kid Celeste used to spend hours in her father's workshop taking in every map and chart and watching him work. She signed up on the first ship she could find when she was old enough with her father's trusty monocle and a backpack filled with books and maps. Less than a month out of port and she was captured by pirates and forced to work on the boat. But being smart and cunning she organized a mutiny and took the ship for herself. From there she pursued a newfound love: piracy and adventure. As her bounty grew so did her fame and riches. The Crimson Rose became an infamous vessel. She traveled the seven seas and visited ports around the world. She was on her way home to reconcile with her dying father when the storm sunk her vessel and dragged her to the rust world.
- **Personality:** Cynical, world-weary adventurer. She had to maintain a firm hand to keep order. Has seen and done more than most can imagine.

**Sells:**

- Treasure maps

**Buys:**

- Lore books

---

### Drake

- **Role:** Shipwright
- **Story:** Drake worked as a carpenter from an early age. Lured by the promise of easy coin he signed up for the crew of a pirate ship and never looked back. He ended up spending 3 years in prison before getting a job on the docks maintaining the vessels coming into Tortuga. Has a collection of adventure novels and blueprints for vessels.

**Sells:**

- 

**Buys:**

- Linen
- Seasoned boards
- Varnished boards

---

## Quests

### Town State

- **Initial state:** Destitute
- **Rebuilding score:** 0

---

### Rebuilding Tortuga

#### Quest: Restablish Trade

> The docks are in shambles, ruins of their once former glory, and the rotting hulk of a vessel sits half submerged. Drake stands at the edge of the dock coiling a pile of rope.
>
> Crates sit empty in the warehouse, exposed to the elements.
> A plantation at the edge of town stands untended, its fields fallow.

---

#### Drake - New Trade Ship

> *"Well voyager, if you want to help us get trade back up and running what we really need is a new sailboat. I can build a magnificent one if you can get the materials together. I've got some old tools kicking around somewhere - while you're getting the materials I'll dig them up."*

**Objectives:**

- Deliver 298 birch boards → copper chisel and hammer added to shop
- Deliver 27 coils of rope → copper saw and shears added to shop
- Deliver 21 linen squares → copper pickaxe and prospecting pick added to shop

**On completion:** Ship is marked as built. Rebuilding score +1.

> *"I'm surprised. Didn't really think you'd get this done but you've made a huge difference."*

---

#### Morgan - Reopen Mine

> *"I love the spirit, voyager. Have a talk with the other residents, they'll have some work for you. There was an old miner machinist around here - went a little mad after we ran out of potatoes. Can you track him down? With his help we can start producing ingots again."*

**Gives:** Letter from Morgan (can request a new one if lost)

**Objectives:**

- Bring the letter to the Potato King
- Potato King wants you to track down the last potato from a nearby dungeon (gives map to its location)
- Returning the potato rewards a signed contract
- Return the signed contract to Morgan

**On completion:** Copper ingots, nails, and plates added to Morgan's shop. Rebuilding score +1.

> *"There are a lot of diagrams about conveyer belts and automation plans in here. But he seems happy and is producing again."*

---

#### Dawn Marie - In Need of Trade Goods

> *"Morgan is full of dreams despite everything. I remember when I used to believe in hope. I don't have much in the way of skills. But everyone would be willing to trade for salt and sugar if I had enough."*

**Objectives:**

- Deliver 64 salt → copper knife and cleaver added to store
- Deliver 64 sugar → copper hoe and scythe added to store

**On completion:** Rebuilding score +1.

> *"You actually did it. Why?"*

---

#### Celeste - The Crimson Rose

> *"Why do you lot keep bothering me with this nonsense. Sure, you want to help and I want my loot from the Crimson Rose."*

**Gives:** Map to the Crimson Rose

**Objectives:**

- Dig up a sealed chest from the wreck of the Crimson Rose
- Return the chest to Celeste

**On completion:** Rebuilding score +1. Copper shovel added to store. Can now trade an amphora of rum for treasure maps.

> *"You actually got my loot back? Fine, fine. I'll donate it to Morgan. Maybe she can find a use for it - what am I going to do with it now anyway."*

---

### Town Advancement

When rebuilding score reaches **4**, the town advances from **Destitute** to **Struggling**.

At the new state:

- All stores add **black bronze** variants of the tools they sell
- Available currency increases
- Prices increase
