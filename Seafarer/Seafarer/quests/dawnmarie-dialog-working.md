# Dawn Marie — Dialog & Logic Working File

> **How to use:** Edit dialog text and logic here, then translate changes back
> to `assets/seafarer/config/dialogue/dawnmarie.json` and `assets/seafarer/lang/en.json`.
>
> - Lines starting with `>` are Dawn Marie speaking
> - Lines starting with `*` are player choices
> - `[condition]` blocks show the logic gates
> - `{action}` blocks show triggers (give items, start quests, etc.)
> - `=> node-name` shows where the flow jumps next
> - /* */ blocks are content to delete

---

## Character Notes

- **Role:** Plantation owner and gardener
- **Personality:** Depressed. Wants to believe and dream, but is still grieving. Speaks softly, avoids eye contact.
- **Story:** A free-spirited artist who stopped in Tortuga to paint before sailing to England. Trapped when the temporal typhoon hit. Her dreams of painting for the royal court are fading as she struggles to grow what little she can.

---

## Entry Point

```
[condition] Has player met Dawn Marie before? (player.hasmetdawnmarie)
  NO  => firstmeet
  YES => welcomeback
```

### firstmeet
{set player.hasmetdawnmarie = true}
> "Oh... a visitor. I didn't think anyone new came here anymore. I suppose you're looking to trade? I don't have much, but what I have is honest work."
(end conversation — no jumpTo)

### welcomeback
> "You came back. I wasn't sure you would."
=> main

---

## Main Menu

Player choices (shown based on conditions):

* "What is your name?" => name
* "What do you do?" => profession
* "Let's trade." => trade (opens trade window)

### Orchard quest options:
* "Need any help with the orchard?" => orchard-intro
  [condition] orchard quest NOT active AND NOT completed

* "About those fruit trees." => orchard-progress
  [condition] orchard quest IS active

### Plantation quest options:
* "Need any help with the plantation?" => plantation-intro
  [condition] plantation quest NOT active AND NOT completed

* "About the seeds." => plantation-progress
  [condition] plantation quest IS active

### Always available:
* "Goodbye." => goodbye

---

## Simple Responses

### name
{trigger: revealname}
> "Dawn Marie. I used to paint, portraits, landscapes, anything beautiful. Now I grow what I can and try not to think about what I've lost."
=> main

### profession
> "I tend the plantation at the edge of town. Flowers, sugar cane, a few olive trees. I sell seeds and cuttings, and I'll buy any paintings you find. They remind me of better days."
=> main

### goodbye
> "Take care out there. The world is harder than it used to be."
(end conversation)

---

## Quest: Rebuild the Orchard (dawnmarie-orchard)

Any 6 unique fruit tree cuttings completes the quest.

### orchard-intro
> "There were so many beautiful fruit trees here at one time. I used to spend hours painting under them. Bring me fruit tree cuttings and I can start replanting."
=> orchard-offer

### orchard-offer
* "I'll find some cuttings." => orchard-start
* "Maybe later." => main

### orchard-start
{trigger: questStart, code: dawnmarie-orchard}
> "Anything you can find — apples, cherries, peaches, pears, oranges, mangoes, breadfruit, lychee, pomegranate. Any six different kinds will be enough to get us started."
=> main

### orchard-progress (quest active — delivery menu)
* "I have a pink apple cutting." => deliver-orchard-pinkapple
  [condition] pinkapple not delivered AND has game:fruittreecutting-pinkapple
* "I have a red apple cutting." => deliver-orchard-redapple
  [condition] redapple not delivered AND has game:fruittreecutting-redapple
* "I have a yellow apple cutting." => deliver-orchard-yellowapple
  [condition] yellowapple not delivered AND has game:fruittreecutting-yellowapple
* "I have a cherry cutting." => deliver-orchard-cherry
  [condition] cherry not delivered AND has game:fruittreecutting-cherry
* "I have a peach cutting." => deliver-orchard-peach
  [condition] peach not delivered AND has game:fruittreecutting-peach
* "I have a pear cutting." => deliver-orchard-pear
  [condition] pear not delivered AND has game:fruittreecutting-pear
* "I have an orange cutting." => deliver-orchard-orange
  [condition] orange not delivered AND has game:fruittreecutting-orange
* "I have a mango cutting." => deliver-orchard-mango
  [condition] mango not delivered AND has game:fruittreecutting-mango
* "I have a breadfruit cutting." => deliver-orchard-breadfruit
  [condition] breadfruit not delivered AND has game:fruittreecutting-breadfruit
* "I have a lychee cutting." => deliver-orchard-lychee
  [condition] lychee not delivered AND has game:fruittreecutting-lychee
* "I have a pomegranate cutting." => deliver-orchard-pomegranate
  [condition] pomegranate not delivered AND has game:fruittreecutting-pomegranate
* "How's the orchard coming along?" => orchard-status
* "Goodbye." => goodbye

### deliver-orchard-{type}
{trigger: questDeliver, code: dawnmarie-orchard, objective: {type}}
> "A {type} tree! Those were my favorite in spring. Thank you, I'll plant it right away."
{+20 gardening xp each, and cutting is added to stock}
```
[condition] Quest completed? (6 deliveries)
  YES => orchard-complete
  NO  => orchard-progress
```

### orchard-status
> "Bring me cuttings from any six different fruit trees. Anything helps."
=> orchard-progress

### orchard-complete
> "The orchard is alive again. I can hardly believe it. Paint it someday, voyager, it deserves to be remembered."
=> main

---

## Quest: Rebuild the Plantation (dawnmarie-plantation)

Any 6 unique crop seed deliveries (10 seeds each) completes the quest.

### plantation-intro
> "More food variety would help everyone out. Bring me 10 seeds of any crop and I can start growing them."
=> plantation-offer

### plantation-offer
* "I'll find some seeds." => plantation-start
* "Maybe later." => main

### plantation-start
{trigger: questStart, code: dawnmarie-plantation}
> "Rice, soybeans, amaranth, cassava, peanuts, pineapple, sunflower, rye, parsnip, turnip, spelt, onion, flax, carrot — any six different kinds, ten seeds each."
=> main

### plantation-progress (quest active — delivery menu)
* "I have rice seeds." => deliver-plantation-rice
  [condition] rice not delivered AND has 10x game:seeds-rice
* "I have soybean seeds." => deliver-plantation-soybean
  [condition] soybean not delivered AND has 10x game:seeds-soybean
* "I have amaranth seeds." => deliver-plantation-amaranth
  [condition] amaranth not delivered AND has 10x game:seeds-amaranth
* "I have cassava seeds." => deliver-plantation-cassava
  [condition] cassava not delivered AND has 10x game:seeds-cassava
* "I have peanut seeds." => deliver-plantation-peanut
  [condition] peanut not delivered AND has 10x game:seeds-peanut
* "I have pineapple seeds." => deliver-plantation-pineapple
  [condition] pineapple not delivered AND has 10x game:seeds-pineapple
* "I have sunflower seeds." => deliver-plantation-sunflower
  [condition] sunflower not delivered AND has 10x game:seeds-sunflower
* "I have rye seeds." => deliver-plantation-rye
  [condition] rye not delivered AND has 10x game:seeds-rye
* "I have parsnip seeds." => deliver-plantation-parsnip
  [condition] parsnip not delivered AND has 10x game:seeds-parsnip
* "I have turnip seeds." => deliver-plantation-turnip
  [condition] turnip not delivered AND has 10x game:seeds-turnip
* "I have spelt seeds." => deliver-plantation-spelt
  [condition] spelt not delivered AND has 10x game:seeds-spelt
* "I have onion seeds." => deliver-plantation-onion
  [condition] onion not delivered AND has 10x game:seeds-onion
* "I have flax seeds." => deliver-plantation-flax
  [condition] flax not delivered AND has 10x game:seeds-flax
* "I have carrot seeds." => deliver-plantation-carrot
  [condition] carrot not delivered AND has 10x game:seeds-carrot
* "How's the plantation coming along?" => plantation-status
* "Goodbye." => goodbye

### deliver-plantation-{type}
{trigger: questDeliver, code: dawnmarie-plantation, objective: {type}}
> "Good, good. I'll get these in the ground before the season changes."
{+10 gardening xp each, and seeds added to stock}
```
[condition] Quest completed? (6 deliveries)
  YES => plantation-complete
  NO  => plantation-progress
```

### plantation-status
> "Bring me ten seeds of any six different crops. We need variety to feed everyone."
=> plantation-progress

### plantation-complete
> "The fields will feed us again. Proper food, not just dried fish and hardtack."
=> main

---

## Quest Rewards Summary

### Orchard (dawnmarie-orchard):
- Per fruit tree delivery: +20 Gardening XP, adds that cutting to sell list
- Quest completion (6 deliveries): rebuilding tier +1, adds Grow Pot to shop

### Plantation (dawnmarie-plantation):
- Per seed delivery: +10 Gardening XP, adds that seed to sell list
- Quest completion (6 deliveries): rebuilding tier +1, adds Gardening Book to shop
