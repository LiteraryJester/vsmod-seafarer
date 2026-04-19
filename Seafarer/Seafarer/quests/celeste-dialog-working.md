# Celeste — Dialog & Logic Working File

> **How to use:** Edit dialog text and logic here, then translate changes back
> to `assets/seafarer/config/dialogue/celeste.json` and `assets/seafarer/lang/en.json`.
>
> - Lines starting with `>` are Celeste speaking
> - Lines starting with `*` are player choices
> - `[condition]` blocks show the logic gates
> - `{action}` blocks show triggers (give items, start quests, etc.)
> - `=> node-name` shows where the flow jumps next
> - /* */ blocks are content to delete

---

## Entry Point

```
[condition] Has player met Celeste before? (player.hasmetceleste)
  NO  => firstmeet
  YES => welcomeback
```

### firstmeet
{set player.hasmetceleste = true}
> "Another soul washing up on the shoals. I'd offer you a drink but the keg ran dry months ago."
=> check-blackbronze-milestone

### welcomeback
> "Back again."
=> check-blackbronze-milestone

---

## Black Bronze Milestone (one-shot unlock)

```
[condition] Already unlocked? (entity.celeste-blackbronze-unlocked = true)
  YES => main
  NO  => check-blackbronze-rebuilding

[condition] Rebuilding complete? (global.rebuilding-complete = true)
  YES => blackbronze-announce
  NO  => main
```

### blackbronze-announce
{set entity.celeste-blackbronze-unlocked = true}
> "Now that the port's on its feet, I can part with a few finer blades and tools. Take a look next time you're trading."
{addSellingItem: seafarer:cutlass-blackbronze, stock 1+/-1, price 14+/-3}
{addSellingItem: game:shovel-blackbronze, stock 1+/-1, price 15+/-3}
=> main

---

## Main Menu

Player choices (shown based on conditions):

* "What is your name?" => name
* "What do you do?" => profession
* "Let's trade." => trade (opens trade window)
* "Do you have any special maps?" => maps

### Crimson Rose quest options:
* "I heard you lost a ship, the Crimson Rose?" => crimsonrose-intro
  [condition] quest NOT active AND NOT completed

* "About the Crimson Rose?" => crimsonrose-inprogress
  [condition] quest IS active

/* 
* "I heard you lost a ship, the Crimson Rose?" => crimsonrose-done
  [condition] quest IS completed 
*/

### Rum-for-map trade (after Crimson Rose completed):
> "If you've got rum to spare I'll glad you swap a decent drink for a treasure map"

* "I'll trade a bottle of white rum for a treasure map." => rumtrade-whiterum
  [condition] Crimson Rose completed AND has 100x spiritportion-whiterum

* "I'll trade a bottle of dark rum for a treasure map." => rumtrade-darkrum
  [condition] Crimson Rose completed AND has 100x spiritportion-darkrum

### Pirate Tales (after Crimson Rose completed, one-shot):
* "I'd love to hear some of your stories. Rum?" => piratetales-take-whiterum
  [condition] Crimson Rose completed AND tales NOT told AND has 100x white rum

* "I'd love to hear some of your stories. Rum?" => piratetales-take-darkrum
  [condition] Crimson Rose completed AND tales NOT told AND has 100x dark rum

### Rust Hunter quest options:
* "I hear you're looking to make the port safer." => rusthunter-intro
  [condition] celeste-friendly = true AND quest NOT active AND NOT completed

* "I've got an update on the drifter problem." => rusthunter-inprogress
  [condition] quest IS active

/*
* "I hear you're looking to make the port safer." => rusthunter-done
  [condition] quest IS completed
*/

### Bear Hunter quest options:
* "Can you show me some hunting tricks?" => bearhunter-intro
  [condition] celeste-friendly = true AND quest NOT active AND NOT completed

* "About those bear pelts." => bearhunter-inprogress
  [condition] quest IS active

/*
* "Can you show me some hunting tricks?" => bearhunter-done
  [condition] quest IS completed
/*

### Always available:
* "Goodbye." => goodbye

---

## Simple Responses

### name
{trigger: revealname}
> "Celeste. Former captain of the Crimson Rose. Former buccaneer, adventurer and explorer."
=> main

### profession
> "These days I just try and keep the port safe from rust monsters and try and figure out how to make booze from salt water"
=> main

### maps
> "I have a few charts and maps around. Not doing me any good anymore. You want one, I'll trade you from a decent drink."
=> main

### goodbye
> "Ya, we'll see if you come back."
(end conversation)

---

## Quest: The Crimson Rose

### crimsonrose-intro
> "Why do you lot keep bothering me with this nonsense. Sure, you want to help and I want my loot from the Crimson Rose."
=> crimsonrose-offer

### crimsonrose-offer
* "I'll find your treasure." => crimsonrose-start
* "Maybe later." => main

### crimsonrose-start
{trigger: questStart, code: celeste-crimsonrose}
> "Fine. Here's a map to where she went down. The chest should still be sealed, it was built to survive worse than a sinking. Bring it back here."
{give: 1x seafarer:map-crimsonrose}
=> main

### crimsonrose-inprogress (quest active)
* "I have your booty from the Crimson Rose." => crimsonrose-deliver
  [condition] has 1x seafarer:sealed-chest
* "Where did the Crimson Rose go down again?" => crimsonrose-reminder
* "Goodbye." => goodbye

### crimsonrose-reminder
```
[condition] Has map in inventory? (seafarer:map-crimsonrose)
  YES => crimsonrose-reminder-hasmap
  NO  => crimsonrose-reminder-givemap
```

### crimsonrose-reminder-hasmap
> "Follow the map I gave you. The wreck is out there somewhere, find the chest or don't."
=> main

### crimsonrose-reminder-givemap
> "You lost the map? Unbelievable. Here, I have another copy. Try not to lose this one."
{give: 1x seafarer:map-crimsonrose}
=> main

### crimsonrose-deliver
{trigger: questDeliver, code: celeste-crimsonrose, objective: chest}
> "You actually got my loot back? I wasn't expecting that I guess I'll give it to Morgan she'lld find a use for it, what am I going to do with it now anyway."
=> main

/*
### crimsonrose-complete-line
> "There. Happy? I've told Morgan she can have the lot. And I suppose I can part with a few tools from my collection. You've earned that much."
=> main

### crimsonrose-done (post-quest)
> "You already brought back my chest. I don't have anything else buried out there, if that's what you're wondering."
=> main
{addSellingItem: game:shovel-copper}
*/

---

## Friendship: Pirate Tales

### piratetales-take-whiterum / piratetales-take-darkrum
{take: 100x spiritportion-whiterum OR spiritportion-darkrum}
=> piratetales-line1

### piratetales-line1
> "Well, if you're offering."

### piratetales-line2
> "Did I ever tell you about my first mate, Robert the Red? He used to wear a little steering wheel on his belt."

* "Why?" =>

### piratetales-line3
> "He said it was driving his nuts. *Laughs loudly.*"

* "That was terrible." =>

### piratetales-line4
> "You think that's bad? The first time we met, he tried to grab my booty."

* "I wasn't expecting that one." =>

### piratetales-line5
> "Neither was he. Who'd think a one-handed guy would do that? But what could I do? He had me hooked."

* "Please stop." =>

### piratetales-line6
> "I probably should. It gets a little Arrr-rated from here on."

* "I want to walk the plank." =>

### piratetales-line7
{set entity.celeste-piratetales-told = true}
> "Watch out for octopi down there, they're the most well-armed creatures of the deep."
{increment entity.celeste-friendship by 1, threshold 1 => entity.celeste-friendly}
=> main

---

## Rum-for-Map Trade

### rumtrade-whiterum
{take: 100x seafarer:spiritportion-whiterum}
=> rumtrade-give-map

### rumtrade-darkrum
{take: 100x seafarer:spiritportion-darkrum}
=> rumtrade-give-map

### rumtrade-give-map
> "Aye, a fair trade. Here's a map, should keep you busy a while."
{give: 1x game:locatormap-treasures}
=> main

---

## Quest: Rust Hunter

### rusthunter-intro
> "I am. Those rust monsters attack every night. I do my best, but maybe if we thin their numbers it'll help, call it ten to start"
=> rusthunter-offer

### rusthunter-offer
* "I'll thin the herd." => rusthunter-start
* "Maybe later." => main

### rusthunter-start
{trigger: questStart, code: celeste-rusthunter}
> "Good. good."
=> main

### rusthunter-inprogress (quest active)
* "How many do I need to kill?" => rusthunter-check
* "Goodbye." => goodbye

### rusthunter-check
{trigger: questDeliver, code: celeste-rusthunter, objective: kills}
> "Aye, noted. Keep going."

```
[condition] Quest completed? (quest status = completed)
  YES => rusthunter-reward-line
  NO  => rusthunter-status-line
```

### rusthunter-status-line
> "That's {kills-progress} of 10 rust monsters. Keep at it."
=> main

### rusthunter-reward-line
> "Quieter nights, finally. Here, I had this tucked away for a rainy day. Consider it earned."
=> main

### rusthunter-done (post-quest)
> "You've done your part against the rust. The port sleeps easier now."
=> main
{addSellingItem: seafarer:cutlass-copper}
{addSellingItem: seafarer:bucanneer-training-book}
{give: 1x seafarer:blackbronze-cutlass}

---

## Quest: Bear Hunter

### bearhunter-intro
> "No. Maybe. I don't teach beginners. But show me what you can do, bring me back 3 different bear pelts, head and all, and I'll show you some tricks."
=> bearhunter-offer

### bearhunter-offer
* "Challenge accepted." => bearhunter-start
* "Maybe later." => main

### bearhunter-start
{trigger: questStart, code: celeste-bearhunter}
> "Good. Three different bears, head still on the pelt. I'll know if you skimp."
=> main

### bearhunter-inprogress (quest active)
* "A polar bear pelt, head and all." => bearhunter-deliver-polar
  [condition] polar not delivered AND has polar pelt
* "A brown bear pelt, head and all." => bearhunter-deliver-brown
  [condition] brown not delivered AND has brown pelt
* "A black bear pelt, head and all." => bearhunter-deliver-black
  [condition] black not delivered AND has black pelt
* "A panda bear pelt, head and all." => bearhunter-deliver-panda
  [condition] panda not delivered AND has panda pelt
* "A sun bear pelt, head and all." => bearhunter-deliver-sun
  [condition] sun not delivered AND has sun pelt
* "How many pelts do you need?" => bearhunter-statusreport
* "Goodbye." => goodbye

### bearhunter-statusreport
> "Bring me pelts from three different kinds, head and all."
=> main

### bearhunter-deliver-{type} (polar/brown/black/panda/sun)
{trigger: questDeliver, code: celeste-bearhunter, objective: {type}}
> "A fine pelt. I'll find a wall for it."

```
[condition] Quest completed? (quest status = completed)
  YES => bearhunter-reward-line
  NO  => main
```

### bearhunter-reward-line
> "These'll look good on my wall. Barely had anything to wear, at least now I can bare all."
=> main

### bearhunter-done (post-quest)
> "You've earned your stripes, hunter. The wall's full enough."
=> main
{addSellingItem: seafarer:barbedarrow-copper}
{addSellingItem: seafarer:bearhunter-training-book}
{give: 24x seafarer:barbedarrow-copper}