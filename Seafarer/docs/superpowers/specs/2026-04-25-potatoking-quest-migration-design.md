# Potato King quest migration — design

## Goal

Finish the Potato King leg of the Morgan-mine quest chain by migrating it to the
ProgressionFramework quest system, and re-target the in-quest map to send the
player to the nearest abandoned-farm structure to recover a potato seed.

## Today's state

- `morgan-mine` is already migrated to the new quest system. Its single
  objective delivers `seafarer:signed-contract` to Morgan and increments
  `rebuilding-tier`.
- The Potato King leg in between is still on the old flag-based system:
  `assets/seafarer/config/dialogue/potatoking.json` uses
  `entity.letter-received` and `entity.contract-given` flags to advance, with
  no entry in `assets/seafarer/config/quests/`.
- The map item `seafarer:map-potato` (`assets/seafarer/itemtypes/lore/map-potato.json`)
  is wired to `ItemOceanLocatorMap` but its `schematiccode` is `"potatoking"`
  (the King's own house — wrong target).
- The current fetch item is `game:vegetable-potato`. Should be a potato seed
  (`seafarer:seeds-potato`, an existing seafarer item).
- The `abandoned-farm` schematic at
  `assets/seafarer/worldgen/schematics/surface/abandoned-farm.json` already
  contains `seafarer:seeds-potato` blocks — verified during exploration.

## Approach

Standalone `potatoking-lastpotato` quest under "Rebuilding Tortuga", gated by
the player carrying Morgan's letter. Quest-start is implicit: handing the
letter to the Potato King fires `questStart` and `giveitemstack` for the map.
Quest-deliver is the seed handed back to the King; the contract is given via
the dialog's `giveitemstack` trigger (mirrors how Morgan gives the letter and
Celeste gives the map). Increments to `rebuilding-tier` stay on `morgan-mine`
so the score doesn't double-count.

The map's `schematiccode` flips from `"potatoking"` to `"abandoned-farm"`.
`ItemOceanLocatorMap.FindFreshStructureLocation` then resolves the nearest
abandoned-farm at use-time and consumes it. `searchRange` drops from 10000 to
2000 (matches the C# default; abandoned-farms are scattered densely enough that
this is sufficient). `randomX`/`randomZ` drop from 15 to 8 so the waypoint
lands close enough to the small ruin to be findable.

## Components to add or modify

### New file: `assets/seafarer/config/quests/potatoking-lastpotato.json`

```jsonc
{
  "code": "potatoking-lastpotato",
  "npc": "potatoking",
  "scope": "server",
  "groupLangKey": "quest-group-rebuilding-tortuga",
  "titleLangKey": "quest-potatoking-lastpotato-title",
  "descriptionLangKey": "quest-potatoking-lastpotato-desc",
  "autoEnable": true,
  "objectives": [
    {
      "code": "seed",
      "type": "delivery",
      "items": [{ "item": "seafarer:seeds-potato", "quantity": 1 }],
      "required": 1
    }
  ],
  "rewards": []
}
```

Rationale:
- `npc: "potatoking"` so the quest log groups under the right NPC.
- `scope: "server"` to match `morgan-mine` and the rest of the
  Rebuilding-Tortuga group; the town only needs the Last Potato found once.
  Status is therefore read as `global.quest-seafarer-potatoking-lastpotato-status`
  in the dialog conditions.
- No `incrementEntityVariable` reward; `rebuilding-tier` already moves on
  `morgan-mine` completion downstream.
- Contract handoff isn't a quest reward; it happens in dialog so the message
  flow ("a deal is a deal, here's the contract") stays narrative-shaped.

### Rewritten file: `assets/seafarer/config/dialogue/potatoking.json`

Top-level structure:

1. **First meeting / welcome-back**: unchanged — sets `player.hasmetpotatoking`,
   jumps to `main`.
2. **`main` (player-owned menu)** with branches:
   - `dialogue-name` → `name` (always available).
   - **Letter present and quest not active/completed** → `lastpotato-letter-received`.
     Conditions: status `isNotValue: "active"`, status `isNotValue: "completed"`,
     and `player.inventory` contains `seafarer:letter-morgan`.
   - **Quest active and player has seed** → `lastpotato-deliver`.
   - **Quest active (otherwise)** → `lastpotato-reminder`.
3. **`lastpotato-letter-received`**: takes letter (`takefrominventory`), speaks
   the letter-response line, jumps to `lastpotato-start`.
4. **`lastpotato-start`**: `questStart` for `potatoking-lastpotato`, speaks the
   quest-start line, jumps to `lastpotato-give-map`.
5. **`lastpotato-give-map`**: `giveitemstack` for `seafarer:map-potato`, jumps
   back to `main`.
6. **`lastpotato-reminder`**: condition node — if player has the map, jump to
   the with-map reminder line; otherwise jump to the lost-map branch.
7. **`lastpotato-reminder-hasmap`**: speaks the reminder line, back to `main`.
8. **`lastpotato-reminder-givemap`**: speaks the lost-map line, falls through
   to `lastpotato-give-map` (replacement copy).
9. **`lastpotato-deliver`**: `questDeliver` for `objective: "seed"`, speaks the
   seed-received line, jumps to `lastpotato-give-contract`.
10. **`lastpotato-give-contract`**: speaks the contract line and
    `giveitemstack` for `seafarer:signed-contract`, back to `main`.

Removed entries from the old version: `letter-received`, `potato-quest-start`,
`potato-reminder`, `potato-deliver`, `give-contract`. Removed flags:
`entity.letter-received`, `entity.contract-given` — replaced by reading
`global.quest-seafarer-potatoking-lastpotato-status`.

### Modified file: `assets/seafarer/itemtypes/lore/map-potato.json`

Three changes inside `attributes.locatorPropsbyType["*"]`:
- `schematiccode`: `"potatoking"` → `"abandoned-farm"`.
- `randomX`: `15` → `8`.
- `randomZ`: `15` → `8`.

And one change in `attributes`:
- `searchRange`: `10000` → `2000`.

`waypointtext`, `waypointicon`, `waypointcolor`, the lang key
`location-potato`, the texture, and all transforms stay as-is.

### Modified file: `assets/seafarer/lang/en.json`

Add (in the `quest-*` block near the existing quest titles/descs):
- `quest-potatoking-lastpotato-title` — "The Last Potato".
- `quest-potatoking-lastpotato-desc` — "The Potato King won't sign Morgan's
  contract until someone finds him a potato seed. He's marked an abandoned
  farm on the map — go dig around in the ruins."

Add (in the dialogue block near the existing potato-king lines):
- `dialogue-potatoking-quest-start` (replaces `dialogue-potatoking-potato-quest`)
  — "Fine. I'll make you a deal. There's one seed potato left, ONE!, in an
  abandoned farm out in the ruins. The temporal storms scattered everything.
  Find me that seed and I'll sign whatever contract Morgan wants. Here's a map."
- `dialogue-potatoking-have-seed` (replaces `dialogue-potatoking-have-potato`)
  — "I found the last potato seed!"
- `dialogue-potatoking-quest-reminder` (replaces
  `dialogue-potatoking-potato-reminder`) — "The map I gave you points to an
  abandoned farm. Find that potato seed! Without it, no deal."
- `dialogue-potatoking-lostmap` — "You lost the map?! Bah. Here, another copy.
  Try to keep this one in one piece."
- `dialogue-potatoking-seed-received` (replaces
  `dialogue-potatoking-potato-received`) — "Is that... is that really it? A
  seed potato! Oh glorious day! I'll plant it, propagate it, the kingdom will
  eat again!"

Update existing keys:
- `itemdesc-map-potato` — "A hastily drawn map to an abandoned farm where the
  last potato seed can be found."
- `dialogue-potatoking-give-contract` — "A deal is a deal. Here's the signed
  contract for Morgan. Tell her the mine will be operational again. And tell
  her I expect a steady supply of potatoes once my crop's grown!"

Remove (no longer referenced):
- `dialogue-potatoking-potato-quest`
- `dialogue-potatoking-potato-reminder`
- `dialogue-potatoking-have-potato`
- `dialogue-potatoking-potato-received`

Keep (still referenced by the dialogue):
- `dialogue-potatoking-welcome`
- `dialogue-potatoking-welcomeback`
- `dialogue-potatoking-name`
- `dialogue-potatoking-show-letter`
- `dialogue-potatoking-letter-response`
- `dialogue-potatoking-remind`
- `dialogue-potatoking-goodbye` (unused now; remove if validator complains).

## Out of scope

- The `potatoking` *story structure* in `worldgen/seafarerstructures.json`
  (Potato King's House) is unchanged — it's still where the NPC lives.
- No changes to the abandoned-farm schematic or its placement rules.
- No changes to the `morgan-mine` quest config — that's the right level of
  decoupling. The contract item still flows: Potato King → player → Morgan,
  and the second half (player → Morgan) is already wired through `morgan-mine`.

## Validation

- After changes, run `python3 validate-assets.py` from the seafarer mod root.
  Errors must be fixed before commit; warnings noted in the commit message.
- In-game smoke test:
  1. Talk to Morgan, accept `morgan-mine`, get the letter.
  2. Walk to Potato King, present the letter — quest should appear in the
     ledger as Active.
  3. Use the map — should add a waypoint pointing at an abandoned-farm.
  4. Visit that abandoned-farm and pick up `seafarer:seeds-potato`.
  5. Return to Potato King — quest should complete, contract handed over.
  6. Return to Morgan with the contract — `morgan-mine` should complete and
     `rebuilding-tier` should tick once.
- Lost-map case: drop the map, talk to Potato King again — should re-issue.
