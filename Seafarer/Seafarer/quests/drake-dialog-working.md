# Drake — Dialog & Logic Working File

> **How to use:** Edit dialog text and logic here, then translate changes back
> to `assets/seafarer/config/dialogue/drake.json` and `assets/seafarer/lang/en.json`.
>
> - Lines starting with `>` are Drake speaking
> - Lines starting with `*` are player choices
> - `[condition]` blocks show the logic gates
> - `{action}` blocks show triggers (give items, start quests, etc.)
> - `=> node-name` shows where the flow jumps next
> - /* */ blocks are content to delete

---

## Entry Point

```
[condition] Has player met Drake before? (player.hasmetdrake)
  NO  => firstmeet
  YES => welcomeback
```

### firstmeet
{set player.hasmetdrake = true}
> "Hm. Another one looking for a ship, I'd wager. Well, you've come to the right place. I'm the only shipwright in Tortuga, and if you want anything bigger than a log raft, you'll need what I sell."
=> check-blackbronze-milestone

### welcomeback
> "Back again. Need more supplies for that vessel of yours?"
=> check-blackbronze-milestone

---

## Black Bronze Milestone (one-shot unlock)

Requires: rebuilding complete AND tradeship quest completed.

```
[condition] Already unlocked? (entity.drake-blackbronze-unlocked = true)
  YES => main
  NO  => check-blackbronze-rebuilding

[condition] Rebuilding complete? (global.rebuilding-complete = true)
  YES => check-blackbronze-tradeship
  NO  => main

[condition] Tradeship completed? (global.quest-seafarer-drake-tradeship-status = completed)
  YES => blackbronze-announce
  NO  => main
```

### blackbronze-announce
{set entity.drake-blackbronze-unlocked = true}
> "Tortuga's on her feet again, and the forge has been humming. We have some new good for sale now."
{addSellingItem: game:chisel-blackbronze, stock 1+/-1, price 15+/-3}
{addSellingItem: game:hammer-blackbronze, stock 1+/-1, price 15+/-3}
{addSellingItem: game:saw-blackbronze, stock 1+/-1, price 15+/-3}
{addSellingItem: game:shears-blackbronze, stock 1+/-1, price 15+/-3}
{addSellingItem: game:pickaxe-blackbronze, stock 1+/-1, price 18+/-3}
{addSellingItem: game:prospectingpick-blackbronze, stock 1+/-1, price 18+/-3}
=> main

---

## Main Menu

Player choices (shown based on conditions):

* "What is your name?" => name
* "What do you do?" => profession
* "Let's trade." => trade (opens trade window)

### Trade Ship quest options:
* "Can I help build a new trade ship?" => tradeship-intro
  [condition] quest NOT active AND NOT completed

* "How it the ship going?" => tradeship-progress
  [condition] quest IS active

/*
* "Can I help build a new trade ship?" => tradeship-done
  [condition] quest IS completed
*/

### Tricks of the Trade (unlocked after rebuilding complete OR tradeship completed):
* "Can you teach me to build ships?" => tricks-gate
  [condition] global.rebuilding-complete = true

* "Can you teach me to build ships?" => tricks-gate
  [condition] rebuilding NOT complete AND tradeship IS completed

---

## Simple Responses

### name
{trigger: revealname}
> "Drake. I build ships. Or rather, I sell what you need to build them yourself. Schematics, fittings, rope, planks, the works."
=> main

### profession
> "I'm a shipwright. Any fool can lash logs together for a raft, but a real ship takes proper materials and know-how. Bring me wood, especially rare timber, and I'll trade you what you need."
=> main

---

## Quest: New Trade Ship (drake-tradeship)

### tradeship-intro
> "Well voyager, if you want to help us get trade back up and running what we really need is a new sailboat. I can build a magnificent one if you can get the materials together. I've got some old tools kicking around somewhere, while you're getting the materials I'll dig them up."
=> tradeship-offer

### tradeship-offer
* "I'll gather the materials." => tradeship-start
* "Maybe later." => main

### tradeship-start
{trigger: questStart, code: drake-tradeship}
> "Right then. I need birch boards for the hull, rope for the rigging, and linen for the sails. Bring them to me as you find them, I'll keep a tally."
=> main

### tradeship-progress (quest active — delivery menu)
* "I have birch boards for the hull." => deliver-boards
  [condition] boards not completed AND has game:plank-birch
* "I brought rope for the rigging." => deliver-rope
  [condition] rope not completed AND has game:rope
* "Here's linen for the sails." => deliver-linen
  [condition] linen not completed AND has game:linen-normal-down
* "How's the ship coming along?" => tradeship-statusreport

### deliver-boards
{trigger: questDeliver, code: drake-tradeship, objective: boards}
> "Good timber. I'll add these to the pile."
```
[condition] Boards objective completed?
  YES => boards-complete-line
  NO  => boards-status-line
```

### boards-status-line
> "That's {boards-progress} of 298 birch boards. Keep them coming."
=> tradeship-progress

### boards-complete-line
> "That's all the boards we need! The hull is taking shape. Here, found these old tools while clearing out the workshop."
=> tradeship-progress

### deliver-rope
{trigger: questDeliver, code: drake-tradeship, objective: rope}
> "Solid rope. This'll hold."
```
[condition] Rope objective completed?
  YES => rope-complete-line
  NO  => rope-status-line
```

### rope-status-line
> "That's {rope-progress} of 27 coils of rope. Need more for the rigging."
=> tradeship-progress

### rope-complete-line
> "Rigging is sorted! Dug up a saw and shears for your trouble."
=> tradeship-progress

### deliver-linen
{trigger: questDeliver, code: drake-tradeship, objective: linen}
> "Good quality cloth. Should make fine sails."
```
[condition] Linen objective completed?
  YES => linen-complete-line
  NO  => linen-status-line
```

### linen-status-line
> "That's {linen-progress} of 21 linen squares. The sails need more."
=> tradeship-progress

### linen-complete-line
> "The sails are done! Found a pickaxe and prospecting pick in the back of the shop, they're yours."
=> tradeship-progress

### tradeship-statusreport
> "Hull needs {boards-progress}/298 boards. Rigging needs {rope-progress}/27 rope. Sails need {linen-progress}/21 linen. Bring what you can."
=> tradeship-progress

### tradeship-done (post-quest)
> "The ship's built and ready to sail. You did good work, voyager."
=> main

---

## Quest: Tricks of the Trade (drake-tricks)

### tricks-gate
```
[condition] Quest active?
  YES => tricks-menu
  NO  => tricks-gate-completed

[condition] Quest completed?
  YES => tricks-menu
  NO  => tricks-intro
```

### tricks-intro
> "Hmm, I guess I have the time now that we aren't struggling."

### tricks-lesson1
> "I can teach you some tricks to better wood using seasoned wood, varnish, and oiled canvas sails."

### tricks-lesson2
> "Treating the hull with resin or shellac is essential if you don't want more leaks than a cheesecloth."

### tricks-lesson3
> "Lastly, oiling and waxing your sail will make it last longer and go faster."

### tricks-tasks
{trigger: questStart, code: drake-tricks}
> "Bring me 18 resin and 6 rendered fat and I'll teach you how to make marine varnish."
> "Then we'll need 20 linen and 5 oil to make the oiled canvas."
=> tricks-menu

### tricks-menu (delivery menu)
* "Here are the varnish ingredients." => deliver-varnish
  [condition] varnish not completed AND has 18x game:resin AND has 6x game:fat

* "Can you show me how to make the oiled canvas now?" => deliver-canvas
  [condition] canvas not completed AND has 20x game:linen-normal-down AND has 6x game:fat

* "How does this look?" => deliver-sail
  [condition] canvas IS completed AND sail-review NOT completed AND has 1x seafarer:oiled-canvas-sail

* "I've got the knack of ship building, now can you teach me how to build a decent boat?" => seasoned-intro
  [condition] tricks IS completed AND seasoned NOT active AND NOT completed

* "Here is the seasoned wood you asked for." => deliver-seasoned
  [condition] seasoned IS active AND has 160x game:plank-seasoned

### deliver-varnish
{trigger: questDeliver, code: drake-tricks, objective: varnish}
> "Great, I've got an old crusty pot we can use. We'll just cook it up until the resin melts. If you don't have fat, oil works just as well."
> "Just be sure to stir it until it's thick and melted, then you can apply it directly to wood or the hull of your ship."
> "Your ship will be a bit faster but mainly it'll be watertight and won't sink like a stone."
=> tricks-menu

### deliver-canvas
{trigger: questDeliver, code: drake-tricks, objective: canvas}
> "Sure, it's pretty simple. You take the canvas, shove it in a barrel of oil in a ratio of 4 linen to 1 oil, and seal it overnight."
> "You'll get some nice waterproof canvas. If you rub wax onto it you can make it even stronger."
> "Here, take that oiled canvas and sew in 4 meters of rope. Bring me back that sail to show me you know what you're doing."
=> tricks-menu

### deliver-sail
{trigger: questDeliver, code: drake-tricks, objective: sail-review}
> "Not bad. Your stitching could use work, and you'll want to reinforce it here and here to prevent tearing, but you've got the knack for it."
=> tricks-menu

---

## Quest: Seasoned Wood (drake-seasoned)

### seasoned-intro
{trigger: questStart, code: drake-seasoned}
> "Sure, bring me 160 planks of seasoned wood and I'll teach you to make an outrigger, a fast lightweight fishing boat."
> "Seasoning wood takes a long time, but it's well worth it if you want a boat to last. Stack up your lumber outside in the wind, cover it to keep the rain off, and in a month or two you'll have seasoned wood."
=> tricks-menu

### deliver-seasoned
{trigger: questDeliver, code: drake-seasoned, objective: seasoned}
> "You really are a hard worker. Here, take this schematic for an outrigger. I'll sell you a new one if you need it."
=> main
