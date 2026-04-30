# Jones the Tavern Owner — Design

## Summary
Add a new NPC trader, **Jones**, owner of the "Last Resort Bar and Pizzeria." Jones is a male, Aussie ex-surfer displaced into the rust world — comic-relief flavor, no quests, no plot hooks. He sells prepared tavern food and drinks and buys raw ingredients.

## Concept
- **Name**: Jones
- **Gender**: Male (uses `game:entity/humanoid/trader-male` shape and male outfit pool)
- **Business**: Last Resort Bar and Pizzeria
- **Personality**: Aussie ex-surfer, gently confused about whether the rust world is real. Surfer slang (swell, barrel, wipeout) + Aussie slang (mate, g'day, crikey, bloody, fair dinkum). Dialogue is comic relief.
- **Quests**: None. No quest-gated dialogue branches, no quest items, no training integration.
- **Pizza**: Acknowledged in dialogue as "aspirational — ovens here are a shocker." No pizza items added in this change; deferred to a future update.

## Files to create or modify
1. **`Seafarer/Seafarer/assets/seafarer/entities/humanoid/trader-jones.json`** — new entity file. Class `EntityEvolvingTrader`, shape `game:entity/humanoid/trader-male`, `outfitConfigFileName: game:traderaccessories-male`, inline `tradeProps`. Outfit pool drawn from Drake/Morgan/Potato King male templates with beach-bum leaning (rope accessories, casual shirts, sandals if available).
2. **`Seafarer/Seafarer/assets/seafarer/config/dialogue/jones.json`** — dialogue tree modeled on `reva.json`. Components listed below under *Dialogue structure*.
3. **`Seafarer/Seafarer/assets/seafarer/lang/en.json`** — add lang keys (enumerated below).
4. **`Seafarer/Seafarer/assets/seafarer/itemtypes/creature/creature.json`** — append `"trader-jones"` to the `type` variant states so the trader is spawnable from the creative inventory.

No external tradelist file. All current trader entities use inline `tradeProps`; the dangling `villager-*.json` tradelist files are not referenced by any entity JSON and appear unused. Match the dominant pattern.

## Trade inventory (inline `tradeProps`)
- **Money**: `{ avg: 35, var: 8 }`.
- **Selling** — `maxItems: 8`:
  - `seafarer:pansearedfish` — `stock { avg: 3, var: 1 }`, `price { avg: 6, var: 1.5 }`
  - `seafarer:searedmeat` — `stock { avg: 3, var: 1 }`, `price { avg: 6, var: 1.5 }`
  - `seafarer:flatbread` — `stock { avg: 4, var: 1 }`, `price { avg: 4, var: 1 }`
  - `seafarer:tortilla` — `stock { avg: 4, var: 1 }`, `price { avg: 3, var: 1 }`
  - `seafarer:spiritportion-rum` — sold as a full `game:woodbucket` via `creativeinventoryStacks`-style `attributes.ucontents`. `stock { avg: 2, var: 1 }`, `price { avg: 18, var: 3 }`
  - `seafarer:canejuiceportion` — full `game:woodbucket`. `stock { avg: 3, var: 1 }`, `price { avg: 8, var: 2 }`
  - `seafarer:coconutmilkportion` — full `game:woodbucket`. `stock { avg: 2, var: 1 }`, `price { avg: 6, var: 1.5 }`
- **Buying** — `maxItems: 8`:
  - `game:redmeat-raw` — `stock { avg: 8, var: 2 }`, `price { avg: 3, var: 0.5 }`
  - `game:fish-raw` — `stock { avg: 10, var: 2 }`, `price { avg: 2, var: 0.5 }`
  - `game:grain-spelt` — `stock { avg: 16, var: 4 }`, `price { avg: 1.5, var: 0.5 }`
  - `game:grain-rye` — `stock { avg: 16, var: 4 }`, `price { avg: 1.5, var: 0.5 }`
  - `seafarer:tomato` — `stock { avg: 8, var: 2 }`, `price { avg: 2, var: 0.5 }`
  - `seafarer:chili` — `stock { avg: 6, var: 2 }`, `price { avg: 2.5, var: 0.5 }`
  - `seafarer:corn` — `stock { avg: 8, var: 2 }`, `price { avg: 2, var: 0.5 }`
  - `game:salt` — `stock { avg: 4, var: 1 }`, `price { avg: 3, var: 0.5 }`

Prices calibrated roughly against existing traders (Celeste sells maps 15–25, Potato King buys potatoes at 3). Jones's sell prices cover ingredient cost + small margin; buy prices are slightly above raw-stack street value to justify selling to him.

## Dialogue structure (mirrors `reva.json`)
Components, in order:
1. `testhasmet` — condition on `player.hasmetjones`; jumps to `firstmeet` if not met, else `welcomeback`.
2. `firstmeet` — Jones talks, sets `player.hasmetjones = true`. Lang: `dialogue-jones-welcome`.
3. `welcomeback` — Jones talks. Lang: `dialogue-jones-welcomeback`. `jumpTo: main`.
4. `main` — player menu with options:
   - `dialogue-name` → `name`
   - `dialogue-profession` → `profession`
   - `dialogue-trade` → `trade`
   - `dialogue-jones-world` → `world`
   - `dialogue-goodbye` → `goodbye`
5. `name` — Jones introduces himself, `trigger: revealname`. Lang: `dialogue-jones-name`. `jumpTo: main`.
6. `profession` — Jones explains the Last Resort. Lang: `dialogue-jones-profession`. `jumpTo: main`.
7. `trade` — `trigger: opentrade`.
8. `world` — Jones rambles about the rust world / whether this is real. Lang: `dialogue-jones-world-info`. `jumpTo: main`.
9. `goodbye` — closing line. Lang: `dialogue-jones-goodbye`.

## Lang entries (to add under existing trader-dialogue section)
- `item-creature-trader-jones`: `"Jones"`
- `dialogue-jones-welcome`: First-meet welcome. Aussie surfer flavor. ~2 sentences.
- `dialogue-jones-welcomeback`: Brief casual return greeting.
- `dialogue-jones-name`: Introduces himself as Jones, mentions surfing, admits he's not sure how he got here.
- `dialogue-jones-profession`: Explains Last Resort Bar and Pizzeria, notes pizza is "aspirational — ovens here are a shocker, still workin' on it."
- `dialogue-jones-world`: Player menu option — "Is any of this actually real, mate?" or similar.
- `dialogue-jones-world-info`: Jones's response, confused ramble about rust, temporal storms, last wave he caught.
- `dialogue-jones-goodbye`: Casual Aussie farewell.

## Creature.json change
The current `creativeinventory` variants list is:
```json
"trader-celeste", "trader-drake", "trader-morgan", "trader-dawnmarie", "trader-potatoking"
```
Append `"trader-jones"`. No other change required — the creature item already has `"creativeinventory": { "creatures": ["*"], "seafarer": ["*"] }`.

## Out of scope
- Worldgen spawn / port placement — Jones is creative-only until worldgen is wired (same as Reva today).
- Pizza items and recipes — deferred to a future update.
- Quests, training, evolving-trader stage progression beyond the default.
- External `villager-jones.json` tradelist file — not used by the current entity pattern.
- Reva's missing entity — out of scope; she remains in her current state.

## Validation plan
After implementation:
1. Run `python3 validate-assets.py` — expect 0 new errors.
2. Build: `dotnet build Seafarer/Seafarer.csproj` — expect clean build.
3. In-game creative test (manual): spawn Jones from the seafarer creative tab, verify outfit loads, dialogue flows through all five menu options, trade UI opens with the expected items, and Jones persists across save/load.
4. **Liquid-in-bucket trade verification**: Confirm the three bucket-filled drinks (rum, cane juice, coconut milk) render with the correct liquid texture in the trade UI and resolve to filled buckets in the player's inventory on purchase. `TradeItem` inherits from `JsonItemStack` so `attributes.ucontents` should resolve, but no base-game trader uses this pattern. If the buckets don't resolve correctly in-game, the fallback is to drop the drinks from the sell list and keep Jones tavern-food-only until a follow-up change.

## Shared dialogue lang keys (already available)
`dialogue-name`, `dialogue-profession`, `dialogue-trade`, `dialogue-goodbye` are provided by the base game lang file — do not redefine them in `seafarer:lang/en.json`. Only Jones-specific keys (prefixed `dialogue-jones-*`) need to be added.
