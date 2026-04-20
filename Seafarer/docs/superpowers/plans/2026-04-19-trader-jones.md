# Jones the Tavern Owner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new quest-less NPC trader, Jones, owner of the Last Resort Bar and Pizzeria — Aussie ex-surfer comic-relief character who sells tavern food/drinks and buys raw ingredients.

**Architecture:** Pure asset addition. New entity JSON (`EntityEvolvingTrader`) with inline `tradeProps`, new simple dialogue JSON modeled on `reva.json`, lang entries, and one-line addition to the creature item's variant list so Jones is spawnable in the creative tab. No C# code changes, no worldgen changes, no quest integration.

**Tech Stack:** Vintage Story JSON5 assets, ProgressionFramework's `EntityEvolvingTrader` class (already registered). Validation via `validate-assets.py` and `dotnet build`.

**Spec:** `docs/superpowers/specs/2026-04-19-trader-jones-design.md`

---

## File Structure

- **Create**: `Seafarer/Seafarer/assets/seafarer/entities/humanoid/trader-jones.json` — entity definition with outfit pool and inline `tradeProps`.
- **Create**: `Seafarer/Seafarer/assets/seafarer/config/dialogue/jones.json` — dialogue tree (no quest branches).
- **Modify**: `Seafarer/Seafarer/assets/seafarer/lang/en.json` — append Jones-specific lang keys and creature name.
- **Modify**: `Seafarer/Seafarer/assets/seafarer/itemtypes/creature/creature.json` — add `"trader-jones"` to the `type` variant states.

---

### Task 1: Create Jones's entity file

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/entities/humanoid/trader-jones.json`

Pattern follows Drake's template (male trader using `trader-female` shape + `disableElements: ["breasts"]` + male outfit config), with outfit selection leaning casual/beach-bum (rope accessories, light shirts, no apron). AI task set is the simpler Potato King variant (no `fleeondamage`). Inline `tradeProps` per the spec.

- [ ] **Step 1: Create the file with full contents**

Write this file exactly:

```json
{
	"code": "trader-jones",
	"class": "EntityEvolvingTrader",
	"tags": ["humanoid", "trader", "human", "habitat-land"],
	"weight": 80,
	"canClimb": true,
	"hitboxSize": { "x": 0.6, "y": 1.8 },
	"deadHitboxSize": { "x": 0.75, "y": 0.5 },
	"behaviorConfigs": {
		"nametag": {
			"showtagonlywhentargeted": true
		}
	},
	"client": {
		"renderer": "Shape",
		"shape": { "base": "game:entity/humanoid/trader-female" },
		"texture": { "base": "game:entity/humanoid/villagerbody/skin/normal*" },
		"behaviors": [
			{ "code": "nametag", "showtagonlywhentargeted": true },
			{ "code": "repulseagents" },
			{ "code": "controlledphysics", "stepHeight": 1.01 },
			{ "code": "interpolateposition" },
			{ "code": "conversable", "dialogue": "seafarer:config/dialogue/jones" }
		],
		"animations": [
			{ "code": "die", "animation": "die", "animationSpeed": 1.75, "weight": 10, "blendMode": "AddAverage" },
			{ "code": "hurt", "animation": "hurt", "animationSpeed": 2, "blendMode": "AddAverage" },
			{ "code": "formal-walk", "animation": "formal-walk", "animationSpeed": 1.5 },
			{ "code": "rowdywelcome", "animation": "rowdywelcome", "blendMode": "AddAverage" },
			{ "code": "lazywelcome", "animation": "lazywelcome", "blendMode": "AddAverage" },
			{ "code": "lazynod", "animation": "lazynod", "blendMode": "AddAverage" }
		]
	},
	"attributes": {
		"disableElements": ["breasts"],
		"frostOverlayThreshold": -10,
		"shouldSwivelFromMotion": false,
		"outfitConfigFileName": "game:traderaccessories-male",
		"voiceSounds": [
			"game:sounds/voice/duduk/",
			"game:sounds/voice/oboe/"
		],
		"outfitversion": 1,
		"partialRandomOutfitsByType": {
			"trader-jones": {
				"bodyskin": [
					{ "Code": "bodyskin-4" },
					{ "Code": "bodyskin-5" },
					{ "Code": "bodyskin-6" },
					{ "Code": "bodyskin-10" }
				],
				"hair-base": [
					{ "Code": "hair-short-trimmed", "Weight": 3.0 },
					{ "Code": "hair-short-sideshave", "Weight": 2.0 },
					{ "Code": "hair-medium-basic", "Weight": 4.0 }
				],
				"beard": [
					{ "Code": "beard-full3", "Weight": 3.0 },
					{ "Code": "beard-full5", "Weight": 2.0 },
					{ "Code": "beard-full7" }
				],
				"mustache": [
					{ "Code": "nothing", "Weight": 10.0 }
				],
				"arm": [
					{ "Code": "nothing", "Weight": 4.0 },
					{ "Code": "arm-tiedrope", "Weight": 4.0 },
					{ "Code": "arm-shoulderpouch", "Weight": 2.0 }
				],
				"face": [
					{ "Code": "nothing", "Weight": 10.0 },
					{ "Code": "face-headband", "Weight": 3.0 }
				],
				"foot": [
					{ "Code": "foot-boots1", "Weight": 3.0 },
					{ "Code": "foot-boots2", "Weight": 3.0 },
					{ "Code": "foot-boots3", "Weight": 2.0 }
				],
				"hand": [
					{ "Code": "nothing", "Weight": 8.0 },
					{ "Code": "hand-glovesleather", "Weight": 2.0 }
				],
				"head": [
					{ "Code": "nothing", "Weight": 6.0 },
					{ "Code": "head-felthat", "Weight": 3.0 },
					{ "Code": "head-skullcap", "Weight": 2.0 }
				],
				"lowerbody": [
					{ "Code": "lowerbody-hose1sailor", "Weight": 5.0 },
					{ "Code": "lowerbody-pants3", "Weight": 2.0 },
					{ "Code": "lowerbody-pantssuspender" }
				],
				"neck": [
					{ "Code": "nothing", "Weight": 10.0 },
					{ "Code": "neck-tradernecklacecopper" }
				],
				"shoulder": [
					{ "Code": "shoulder-shortsleeve-vest2sailor", "Weight": 5.0 },
					{ "Code": "shoulder-sashsimple", "Weight": 3.0 },
					{ "Code": "shoulder-tiedrope", "Weight": 2.0 }
				],
				"upperbody": [
					{ "Code": "upperbody-longsleeve-shirt1worn", "Weight": 2.0 },
					{ "Code": "upperbody-longsleeve-shirt9raggedy", "Weight": 2.0 },
					{ "Code": "upperbody-shortsleeve-shirt17patched", "Weight": 5.0 }
				],
				"upperbodyover": [
					{ "Code": "nothing", "Weight": 10.0 }
				],
				"waist": [
					{ "Code": "waist-tiedrope", "Weight": 6.0 },
					{ "Code": "waist-sashcenterred", "Weight": 3.0 },
					{ "Code": "waist-pouchblue", "Weight": 2.0 }
				]
			}
		},
		"tradeProps": {
			"money": { "avg": 35, "var": 8 },
			"selling": {
				"maxItems": 8,
				"list": [
					{ "code": "seafarer:pansearedfish", "type": "item", "stacksize": 1, "stock": { "avg": 3, "var": 1 }, "price": { "avg": 6, "var": 1.5 } },
					{ "code": "seafarer:searedmeat", "type": "item", "stacksize": 1, "stock": { "avg": 3, "var": 1 }, "price": { "avg": 6, "var": 1.5 } },
					{ "code": "seafarer:flatbread", "type": "item", "stacksize": 1, "stock": { "avg": 4, "var": 1 }, "price": { "avg": 4, "var": 1 } },
					{ "code": "seafarer:tortilla", "type": "item", "stacksize": 1, "stock": { "avg": 4, "var": 1 }, "price": { "avg": 3, "var": 1 } },
					{
						"code": "game:woodbucket", "type": "block", "stacksize": 1,
						"attributes": { "ucontents": [{ "type": "item", "code": "seafarer:spiritportion-rum", "makefull": true }] },
						"stock": { "avg": 2, "var": 1 }, "price": { "avg": 18, "var": 3 }
					},
					{
						"code": "game:woodbucket", "type": "block", "stacksize": 1,
						"attributes": { "ucontents": [{ "type": "item", "code": "seafarer:canejuiceportion", "makefull": true }] },
						"stock": { "avg": 3, "var": 1 }, "price": { "avg": 8, "var": 2 }
					},
					{
						"code": "game:woodbucket", "type": "block", "stacksize": 1,
						"attributes": { "ucontents": [{ "type": "item", "code": "seafarer:coconutmilkportion", "makefull": true }] },
						"stock": { "avg": 2, "var": 1 }, "price": { "avg": 6, "var": 1.5 }
					}
				]
			},
			"buying": {
				"maxItems": 8,
				"list": [
					{ "code": "game:redmeat-raw", "type": "item", "stacksize": 4, "stock": { "avg": 8, "var": 2 }, "price": { "avg": 3, "var": 0.5 } },
					{ "code": "game:fish-raw", "type": "item", "stacksize": 4, "stock": { "avg": 10, "var": 2 }, "price": { "avg": 2, "var": 0.5 } },
					{ "code": "game:grain-spelt", "type": "item", "stacksize": 8, "stock": { "avg": 16, "var": 4 }, "price": { "avg": 1.5, "var": 0.5 } },
					{ "code": "game:grain-rye", "type": "item", "stacksize": 8, "stock": { "avg": 16, "var": 4 }, "price": { "avg": 1.5, "var": 0.5 } },
					{ "code": "seafarer:tomato", "type": "item", "stacksize": 4, "stock": { "avg": 8, "var": 2 }, "price": { "avg": 2, "var": 0.5 } },
					{ "code": "seafarer:chili", "type": "item", "stacksize": 4, "stock": { "avg": 6, "var": 2 }, "price": { "avg": 2.5, "var": 0.5 } },
					{ "code": "seafarer:corn", "type": "item", "stacksize": 4, "stock": { "avg": 8, "var": 2 }, "price": { "avg": 2, "var": 0.5 } },
					{ "code": "game:salt", "type": "item", "stacksize": 4, "stock": { "avg": 4, "var": 1 }, "price": { "avg": 3, "var": 0.5 } }
				]
			}
		}
	},
	"server": {
		"attributes": {
			"pathfinder": {
				"minTurnAnglePerSec": 720,
				"maxTurnAnglePerSec": 1440
			}
		},
		"behaviors": [
			{ "code": "nametag", "showtagonlywhentargeted": true, "selectFromRandomName": ["Jones"] },
			{ "code": "repulseagents" },
			{ "code": "controlledphysics", "stepHeight": 1.01 },
			{ "code": "reviveondeath", "minHours": 24, "maxHours": 72 },
			{ "code": "health", "currenthealth": 25, "maxhealth": 25 },
			{
				"code": "emotionstates",
				"states": [
					{ "code": "aggressiveondamage", "duration": 6, "chance": 0.7, "slot": 0, "priority": 2, "accumType": "noaccum" }
				]
			},
			{
				"code": "taskai",
				"aitasks": [
					{
						"code": "meleeattack", "entityCodes": ["player"], "priority": 2,
						"damage": 6, "mincooldown": 2500, "maxcooldown": 3500,
						"attackDurationMs": 900, "damagePlayerAtMs": 300,
						"animation": "Attack", "animationSpeed": 2,
						"whenInEmotionState": "aggressiveondamage"
					},
					{
						"code": "seekentity", "entityCodes": ["player"], "priority": 1.5,
						"mincooldown": 1000, "maxcooldown": 1500, "seekingRange": 20,
						"movespeed": 0.035, "animation": "Run", "animationSpeed": 1.75,
						"whenInEmotionState": "aggressiveondamage"
					},
					{ "code": "idle", "priority": 1.2, "minduration": 2500, "maxduration": 2500, "mincooldown": 2000, "maxcooldown": 10000, "animation": "laugh" },
					{ "code": "idle", "priority": 1.2, "minduration": 2500, "maxduration": 2500, "mincooldown": 5000, "maxcooldown": 30000, "animation": "idle2" },
					{
						"code": "wander", "priority": 1.0, "movespeed": 0.01,
						"animation": "Walk", "wanderChance": 0.005,
						"maxDistanceToSpawn": 4, "wanderRangeMin": 1, "wanderRangeMax": 3,
						"teleportWhenOutOfRange": true, "teleportInGameHours": 1
					},
					{ "code": "lookaround", "priority": 0.5 }
				]
			},
			{ "code": "conversable", "dialogue": "seafarer:config/dialogue/jones" }
		]
	},
	"sounds": {}
}
```

---

### Task 2: Create Jones's dialogue file

**Files:**
- Create: `Seafarer/Seafarer/assets/seafarer/config/dialogue/jones.json`

Pattern mirrors `reva.json` exactly (no quest branches). The `world` node replaces Reva's `treasures` node with Jones's "is this world real?" ramble.

- [ ] **Step 1: Create the file**

```json
{
    components: [
        {
            code: "testhasmet",
            owner: "jones",
            type: "condition",
            variable: "player.hasmetjones",
            isNotValue: "true",
            thenJumpTo: "firstmeet",
            elseJumpTo: "welcomeback"
        },
        {
            code: "firstmeet",
            owner: "jones",
            type: "talk",
            setVariables: { "player.hasmetjones": "true" },
            text: [
                { value: "dialogue-jones-welcome" }
            ]
        },
        {
            code: "main",
            owner: "player",
            type: "talk",
            text: [
                { value: "dialogue-name", jumpTo: "name" },
                { value: "dialogue-profession", jumpTo: "profession" },
                { value: "dialogue-trade", jumpTo: "trade" },
                { value: "dialogue-jones-world", jumpTo: "world" },
                { value: "dialogue-goodbye", jumpTo: "goodbye" }
            ]
        },
        {
            code: "welcomeback",
            owner: "jones",
            type: "talk",
            text: [
                { value: "dialogue-jones-welcomeback" }
            ],
            jumpTo: "main"
        },
        {
            code: "name",
            owner: "jones",
            type: "talk",
            trigger: "revealname",
            text: [
                { value: "dialogue-jones-name" }
            ],
            jumpTo: "main"
        },
        {
            code: "profession",
            owner: "jones",
            type: "talk",
            text: [
                { value: "dialogue-jones-profession" }
            ],
            jumpTo: "main"
        },
        {
            code: "trade",
            owner: "jones",
            trigger: "opentrade"
        },
        {
            code: "world",
            owner: "jones",
            type: "talk",
            text: [
                { value: "dialogue-jones-world-info" }
            ],
            jumpTo: "main"
        },
        {
            code: "goodbye",
            owner: "jones",
            type: "talk",
            text: [
                { value: "dialogue-jones-goodbye" }
            ]
        }
    ]
}
```

---

### Task 3: Add Jones's lang entries

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json`

Two insertion points:
1. The `item-creature-trader-*` section — add the creature name.
2. The `dialogue-*` section (near the existing `dialogue-reva-*` / `dialogue-potatoking-*` keys) — add Jones's dialogue lines.

Do **not** redefine `dialogue-name`, `dialogue-profession`, `dialogue-trade`, or `dialogue-goodbye` — those come from the base game.

- [ ] **Step 1: Find the existing creature-name entries**

Run: `grep -n "item-creature-trader-" Seafarer/Seafarer/assets/seafarer/lang/en.json`

Expected: a short list of lines like `"item-creature-trader-potatoking": "The Potato King"`.

- [ ] **Step 2: Add Jones's creature-name entry**

Add this line alongside the other `item-creature-trader-*` entries (order doesn't matter, but keep them grouped):

```
	"item-creature-trader-jones": "Jones",
```

- [ ] **Step 3: Find the end of the existing `dialogue-*` block**

Run: `grep -n "dialogue-reva-goodbye\|dialogue-potatoking-goodbye" Seafarer/Seafarer/assets/seafarer/lang/en.json`

Use the result to locate where existing trader dialogue blocks end, and insert Jones's dialogue keys adjacent (typically right after Reva's block).

- [ ] **Step 4: Add Jones's dialogue entries**

Add this block of keys (keep the trailing comma convention used elsewhere in the file):

```
	"dialogue-jones-welcome": "G'day mate! Welcome to the Last Resort Bar and Pizzeria. Pull up a stool, I'll get you sorted. Still can't quite believe this place is real, but the ale's wet and the fish is fresh, so who's complaining?",
	"dialogue-jones-welcomeback": "Oi, you're back! Didn't wipe out between here and there, I hope.",
	"dialogue-jones-name": "Name's Jones, mate. Used to chase the perfect barrel back home, then — crikey, I dunno — one minute I'm paddling out at sunrise, next minute I'm in this rust-cursed world pourin' drinks for strangers. Reckon I must've caught the wrong wave.",
	"dialogue-jones-profession": "I run the Last Resort. Bar, pizzeria, whatever you need. Well — the 'pizzeria' part's a bit aspirational, truth be told. Ovens round here are a bloody shocker, mate. Workin' on it. For now, it's tavern grub and whatever drinks I can rustle up.",
	"dialogue-jones-world": "Is any of this actually real, mate?",
	"dialogue-jones-world-info": "Dunno, grommet. One day I'm watchin' the sun come up over Bondi, next I'm hearin' about temporal storms and rust and towers. Ain't no rust on a surfboard where I'm from, I'll tell ya that. Sometimes I reckon I'm still out there floatin', and this whole show's just a dream I'm havin' while the water carries me somewhere. Either way — the ale's good. That's what matters.",
	"dialogue-jones-goodbye": "Hooroo, mate. Catch a wave for me, yeah?",
```

- [ ] **Step 5: Verify the file parses as valid JSON**

Run: `python3 -c "import json; json.load(open('Seafarer/Seafarer/assets/seafarer/lang/en.json'))"`

Expected: no output, exit code 0. If it errors, fix the trailing comma / brace placement.

---

### Task 4: Register Jones in the creature item

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/itemtypes/creature/creature.json`

- [ ] **Step 1: Open the file and locate the variantgroup states**

The current block is:

```json
variantgroups: [
    { code: "type", states: [
        "trader-celeste", "trader-drake", "trader-morgan",
        "trader-dawnmarie", "trader-potatoking"
    ] }
],
```

- [ ] **Step 2: Append `"trader-jones"` to the states array**

Replace the array contents so it reads:

```json
variantgroups: [
    { code: "type", states: [
        "trader-celeste", "trader-drake", "trader-morgan",
        "trader-dawnmarie", "trader-potatoking", "trader-jones"
    ] }
],
```

No other changes — the file already has `"creativeinventory": { "creatures": ["*"], "seafarer": ["*"] }` which applies to all variants.

---

### Task 5: Validate assets and build

**Files:** (none modified — this is verification only)

- [ ] **Step 1: Run the asset validator**

Run from the repo root: `python3 validate-assets.py`

Expected: Same summary as before the change (same error/warning counts) — no new errors introduced by Jones's assets. The existing unrelated `food.ef_protein` error on `premiumfish` is pre-existing; ignore it.

If a new error appears, read the validator output and fix the referenced file.

- [ ] **Step 2: Build the mod**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`

Expected: `Build succeeded.` with `0 Error(s)`. Warnings about nullable-reference types in unrelated files are pre-existing.

---

### Task 6: Commit

**Files:** all four above (two new files + two modified files).

- [ ] **Step 1: Stage the changes**

```bash
git add Seafarer/Seafarer/assets/seafarer/entities/humanoid/trader-jones.json \
        Seafarer/Seafarer/assets/seafarer/config/dialogue/jones.json \
        Seafarer/Seafarer/assets/seafarer/lang/en.json \
        Seafarer/Seafarer/assets/seafarer/itemtypes/creature/creature.json
```

- [ ] **Step 2: Commit**

```bash
git commit -m "$(cat <<'EOF'
feat(trader): add Jones, owner of the Last Resort Bar and Pizzeria

Quest-less NPC trader — Aussie ex-surfer displaced into the rust
world, sells tavern food and drinks, buys raw ingredients. Dialogue
patterned on Reva's simple no-quest template. Pizza deferred.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 3: Verify the commit**

Run: `git log -1 --stat`

Expected: one commit touching the four files above.

---

### Task 7: Manual in-game test (human)

**Files:** (none)

The validator and build can't verify runtime behavior. Human must confirm these in-game before calling the feature done.

- [ ] **Step 1: Launch VS with the seafarer mod loaded**

Start a creative-mode world with Seafarer and ProgressionFramework enabled.

- [ ] **Step 2: Spawn Jones from the creative inventory**

Open the creative inventory → Seafarer tab (or Creatures tab) → find "Jones" → place him.

Expected: a male humanoid entity appears with a casual outfit. No T-pose, no missing-texture pink, no error spam in the client log.

- [ ] **Step 3: Talk to Jones and walk all menu options**

Right-click Jones. Confirm:
- First-meet welcome line shows the Aussie greeting.
- Menu shows five options: Name / Profession / Trade / "Is any of this actually real, mate?" / Goodbye.
- Each talk option displays its lang string correctly (no `dialogue-jones-*` raw keys visible).
- The Trade option opens the trade UI.

- [ ] **Step 4: Verify the trade UI**

Confirm:
- All eight sell-list items render, including the three buckets (rum / cane juice / coconut milk) showing the correct liquid texture.
- Buying a filled bucket places a bucket with the liquid contents into the player's inventory (creative mode lets you test without currency; or give yourself gears).
- All eight buy-list items accept the expected player items for trade.

- [ ] **Step 5: Verify save/load persistence**

Leave the Jones entity placed, save and exit the world, reload. Confirm Jones is still there with the same outfit and name. Talk to him again: `welcomeback` variant should fire (not `firstmeet`).

- [ ] **Step 6: Fallback decision if buckets don't render**

If the three liquid-in-bucket sell items fail to render or fail to resolve in the trade UI, the spec's documented fallback is to drop them from the sell list. Remove the three bucket entries from `trader-jones.json` under `tradeProps.selling.list` and commit as a follow-up fix (`fix(trader-jones): remove untradeable liquid buckets`).

---

## Self-Review

- **Spec coverage**: Entity creation (T1), dialogue (T2), lang (T3), creature.json update (T4), validation (T5), commit (T6), manual test including bucket fallback (T7). All spec sections covered.
- **Placeholder scan**: No TBDs, TODOs, or "similar to" references. All code blocks are complete and copyable.
- **Type consistency**: Dialogue component `code` values (`testhasmet`, `firstmeet`, `welcomeback`, `main`, `name`, `profession`, `trade`, `world`, `goodbye`) match the `jumpTo` / `thenJumpTo` / `elseJumpTo` targets. Lang keys in the dialogue (`dialogue-jones-welcome`, `dialogue-jones-welcomeback`, `dialogue-jones-name`, `dialogue-jones-profession`, `dialogue-jones-world`, `dialogue-jones-world-info`, `dialogue-jones-goodbye`) match exactly what Task 3 adds to `en.json`. The `conversable` behavior reference (`seafarer:config/dialogue/jones`) matches the dialogue file path in Task 2.
