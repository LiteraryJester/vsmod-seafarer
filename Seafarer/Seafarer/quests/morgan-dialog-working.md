# Morgan — Dialog & Logic Working File

> **How to use:** Edit dialog text and logic here, then translate changes back
> to `assets/seafarer/config/dialogue/morgan.json` and `assets/seafarer/lang/en.json`.
>
> - Lines starting with `>` are Morgan speaking
> - Lines starting with `*` are player choices
> - `[condition]` blocks show the logic gates
> - `{action}` blocks show triggers (give items, start quests, etc.)
> - `=> node-name` shows where the flow jumps next
> - /* */ blocks are content to delete

---

## Character Notes

- **Role:** Provisioner, unofficial mayor of Tortuga
- **Personality:** Optimistic and hopeful. Natural organizer who rallies the town.
- **Story:** Original Tortuga inhabitant. Remembers the good days. Stepped up to lead after the governor was killed in a temporal rift.

---

## Entry Point

```
[condition] Has player met Morgan before? (player.hasmetmorgan)
  NO  => firstmeet
  YES => welcomeback
```

### firstmeet
{set player.hasmetmorgan = true}
> "Well now, a fresh face on the docks! Welcome to Tortuga, voyager. If you've got spirits to sell or need supplies for the voyage ahead, I'm your woman."
(end conversation — no jumpTo, player must re-engage)

### welcomeback
> "Back again? Good to see you. What can I do for you today?"
=> main

---

## Main Menu

Player choices (shown based on conditions):

* "What is your name?" => name
* "What do you do?" => profession
* "Let's trade." => trade (opens trade window)
* "Tell me about this place." => tortuga

### Mine quest options:
* "Can I help with the rebuilding effort?" => mine-intro
  [condition] mine quest NOT active AND NOT completed

* "About the mine?" => mine-inprogress
  [condition] mine quest IS active

### Trade Goods quest options (unlocked after mine completed):
* "What else needs doing?" => tradegoods-intro
  [condition] mine quest IS completed AND tradegoods quest NOT active AND NOT completed

* "About the salt and sugar?" => tradegoods-progress
  [condition] tradegoods quest IS active

### Always available:
* "Goodbye." => goodbye

---

## Simple Responses

### name
{trigger: revealname}
> "Name's Morgan. Any problems or needs come see me. I keep things organized."
=> main

### profession
> "I guess I'm the unofficial town mayor now. Since the governor got eaten by a temporal rift. Doesn't require much work at the moment other than looking after the docks and warehouse."
=> main

### tortuga
> "Tortuga's been a port for traders, seafarers, and pirates for years or at least it was. A temporal typhoon hit the port tearing through the buildings and ripping us out of the old world and into this rust world. We just woke up here, I'm sure your story is much the same."
=> main

### goodbye
> "Fair winds to you. Come back when you need restocking."
(end conversation)

---

## Quest: Reopen the Mine (morgan-mine)

### mine-intro
> "I love the spirit, voyager. Have a talk with the other residents they'll have some work for you too. There was an old miner around here, went a little mad after we ran out of potatoes. If you can convince him to start mining again we'll be able to start smelting ingots."
=> mine-offer

### mine-offer
* "I'll track down the miner for you." => mine-start
* "Maybe later." => main

### mine-start
{trigger: questStart, code: morgan-mine}
> "That's the spirit! Take this letter show it to the old Potato King. He lives under the mountain outside of town. Stubborn as bedrock but maybe he'll listen if he knows it's from me."
{give: 1x seafarer:letter-morgan}
=> main

### mine-inprogress (quest active)
* "I have a signed contract from the Potato King." => mine-deliver-contract
  [condition] has 1x seafarer:signed-contract

* "I lost the letter you gave me." => mine-check-letter
  [condition] does NOT have 1x seafarer:letter-morgan

* "What was I supposed to do again?" => mine-reminder
* "Goodbye." => goodbye

### mine-reminder
> "Find the Potato King and show him my letter and convince him to start mining again. We need him if we're ever going to produce ingots."
=> main

### mine-check-letter
```
[condition] Has letter in inventory?
  YES => mine-reminder
  NO  => mine-replace-letter
```

### mine-replace-letter
> "Losing things at sea is nothing new. Here, take another copy. Don't lose this one."
{give: 1x seafarer:letter-morgan}
=> main

### mine-deliver-contract
{trigger: questDeliver, code: morgan-mine, objective: contract}
> "A signed contract! There are a lot of diagrams about conveyor belts and automation plans in here. But he seems willing to work again. You've made a real difference, voyager."

### mine-complete
> "I'm surprised, didn't really think you'd get this done. The first shipment of ore has already arrived. We can start producing copper ingots, nails, and plates again."
=> main

---

## Quest: In Need of Trade Goods (morgan-tradegoods)

Prereqs: mine quest completed.

### tradegoods-intro
> "Now that the mine is running, we need to build up our trade goods. Salt and sugar are worth their weight in gold out here. Bring me as much as you can find."
=> tradegoods-offer

### tradegoods-offer
* "I'll bring you salt and sugar." => tradegoods-start
* "Maybe later." => main

### tradegoods-start
{trigger: questStart, code: morgan-tradegoods}
> "That's the spirit. Bring me salt and sugar, as much as you can find. With enough of both we can actually start trading properly."
=> main

### tradegoods-progress (quest active — delivery menu)
* "I have salt for you." => deliver-salt
  [condition] salt objective not completed AND has game:salt

* "I brought sugar." => deliver-sugar
  [condition] sugar objective not completed AND has seafarer:sugar

* "How much salt and sugar do you still need?" => tradegoods-status
* "Goodbye." => goodbye

### deliver-salt
{trigger: questDeliver, code: morgan-tradegoods, objective: salt}
> "Salt. Good. Every bit helps."
```
[condition] Salt objective completed?
  YES => salt-complete-line
  NO  => salt-status-line
```

### salt-status-line
> "That's {salt-progress} of 64 salt. I need more before I can start trading it."
=> tradegoods-progress

### salt-complete-line
> "That's all the salt I need! I can finally offer something worth trading."
```
[condition] Quest completed? (both salt + sugar done)
  YES => tradegoods-complete
  NO  => tradegoods-progress
```

### deliver-sugar
{trigger: questDeliver, code: morgan-tradegoods, objective: sugar}
> "Sugar. Sweet. We can do a lot with this."
```
[condition] Sugar objective completed?
  YES => sugar-complete-line
  NO  => sugar-status-line
```

### sugar-status-line
> "That's {sugar-progress} of 64 sugar. Keep it coming."
=> tradegoods-progress

### sugar-complete-line
> "Enough sugar at last. I can find plenty of uses for it."
```
[condition] Quest completed? (both salt + sugar done)
  YES => tradegoods-complete
  NO  => tradegoods-progress
```

### tradegoods-status
> "I need {salt-progress}/64 salt and {sugar-progress}/64 sugar. Bring what you can."
=> tradegoods-progress

### tradegoods-complete
> "That's everything we needed. The trade goods are flowing and Tortuga is looking more like a real port every day. You've made a real difference, voyager."
=> main

---

## Quest Rewards Summary

### Mine (morgan-mine):
- Contract delivery: addSellingItem game:ingot-copper + game:nail-copper + game:metalplate-copper
- Quest completion: rebuilding tier +1

### Trade Goods (morgan-tradegoods):
- Salt objective (64x): addSellingItem game:knife-copper + game:cleaver-copper
- Sugar objective (64x): addSellingItem game:hoe-copper + game:scythe-copper
- Quest completion: rebuilding tier +1
