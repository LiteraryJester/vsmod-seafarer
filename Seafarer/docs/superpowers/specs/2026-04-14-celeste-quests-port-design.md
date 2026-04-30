# Celeste Quest & Dialog — Port to QuestSystem

## Context

Celeste's three quests (Crimson Rose, Rust Hunter, Bear Hunter) are currently
implemented entirely inside `assets/seafarer/config/dialogue/celeste.json`
(860 lines) using inline `setVariables`, entity-scoped flags, and dialog-side
increment triggers. Drake's quests, by contrast, live in proper
ProgressionFramework quest JSON files (`config/quests/drake-*.json`) and use
`questStart` / `questDeliver` triggers. The inconsistency means Celeste's
quests never appear in the player-facing Quest Log, rewards and completion
logic are duplicated in dialog, and NPC-specific friendship/rebuilding
increments are harder to audit.

This design ports Celeste's three quests to the QuestSystem, adds the two
framework features needed to model them cleanly, and re-wires the dialog to
drive quest state through triggers instead of raw variable writes.

## Goals

- Three new quest JSONs under `config/quests/` — one per Celeste quest.
- Dialog drives all quest state transitions via `questStart` and
  `questDeliver` triggers; no inline `setVariables` for quest flags.
- Quest Log GUI surfaces all three quests for players.
- Friendship and rebuilding-tier increments move out of the dialog and into
  quest rewards.
- Gameplay behavior unchanged from the player's perspective (same lines,
  same gates, same rewards) except: the Rust Hunter no longer asks for rot
  drops — it tracks drifter kills server-wide from the moment the quest
  starts.

## Non-goals

- Creating the `seafarer:cutlass-vengeance` item. Current dialog rewards
  `cutlass-blackbronze`; that stays as the Rust Hunter reward. Named
  cutlasses remain a deferred future mechanic.
- Bear Hunter trait (`+1 armor, +2.5% speed`) — still deferred.
- Clay mold recipe for barbed arrowheads — still deferred.
- Reworking Pirate Tales (stays dialog-only, not a QuestSystem quest).
- Reworking the rum-for-treasure-map exchange (stays dialog-only trade).
- Changing friendship storage (`entity.celeste-friendship` stays per-NPC).
- Porting Morgan / Dawnmarie to QuestSystem. Their inline
  `increment-rebuilding-tier` dialog components stay as they are.

## Framework additions

Two additive changes to `vsmod-progression-framework`.

### 1. `Quest.RequiredObjectiveCount` — any-N-of-M completion

Optional int on `Quest`. When set, the quest completes once that many
objectives are complete (rather than all). Null keeps current AND-all
behavior and is fully backward compatible.

```csharp
public int? RequiredObjectiveCount { get; set; }
```

`QuestSystem.AllObjectivesComplete` becomes:

```csharp
int required = quest.RequiredObjectiveCount ?? quest.Objectives.Count;
int done = quest.Objectives.Count(o => IsObjectiveComplete(player, quest.Code, o.Code));
return done >= required;
```

Objectives that remain incomplete when the quest finishes stay pending
forever; `TryDeliver` already returns false if the quest is not `Active`,
so stale turn-ins are naturally rejected.

### 2. Native kill tracking + `kill` objective type

Framework subscribes **once** at server start to
`api.Event.OnEntityDeath`. Every quest loaded at `AssetsFinalize` whose
objectives include type `kill` contributes a pattern string to a shared
registry. On death, the framework takes the dead entity's full code
(domain + path, e.g. `game:drifter-normal`) and checks each registered
pattern via `WildcardUtil.Match`. Matches increment a global counter named
`pf:killcount:<pattern>` through `VariablesModSystem` at `global` scope,
so counters persist with save state.

**Objective JSON shape:**

```json
{
  "code": "kills",
  "type": "kill",
  "pattern": "game:drifter-*",
  "required": 10
}
```

**Behavior:**

- Quest starts → the `kill` objective handler's `OnObjectiveStarted`
  reads the current value of `global.pf:killcount:<pattern>` and stores
  it on the player's objective tree as `baseline` (int).
- Every entity death matching the pattern bumps
  `global.pf:killcount:<pattern>` by 1. All players with any active quest
  using that pattern see their progress reflected automatically.
- `questDeliver` trigger for a `kill`-type objective (dispatched via the
  existing `TryDeliver` path, with type dispatch generalised):
  - `progress = global.pf:killcount:<pattern> - baseline`
  - if `progress >= required`, mark objective completed and apply its
    rewards. Otherwise leave it pending and return false (dialog's
    `check-*-status` path shows the "keep at it" line).

**Pattern syntax:** full entity code with domain. `game:drifter-*` matches
`game:drifter-normal`, `game:drifter-deep`, etc. Uses whatever
pattern-matching helper VS exposes for asset-location globs
(`WildcardUtil.Match` or equivalent on `AssetLocation`); the
implementation picks whichever is idiomatic for matching an
`AssetLocation` against a string pattern.

**Registry:** populated at `AssetsFinalize`. A pattern present in any
loaded quest gets a counter; patterns not referenced by any quest are
never counted (no wasted work on arbitrary kills).

**Implementation impact:**

- `BuiltInObjectiveHandlers` gains a `Kill` class implementing
  `IQuestObjectiveHandler` with `OnObjectiveStarted` (snapshot baseline)
  and a new `TryProgress` code path invoked from a generalised
  `TryDeliver`.
- `QuestObjective` gains a `Pattern` field (string, only read by kill
  handler).
- `QuestSystem`:
  - scans loaded quests for `kill` objectives, builds the pattern
    registry, subscribes to `OnEntityDeath` once
  - refactors `TryDeliver` to dispatch on `objective.Type` through the
    registered handler; current delivery logic moves into
    `BuiltInObjectiveHandlers.Delivery`'s `TryProgress`

## Celeste quest JSONs

Three new files under
`Seafarer/Seafarer/assets/seafarer/config/quests/`.

### `celeste-crimsonrose.json`

Single-item delivery (the sealed chest). Quest-start gives the map via the
**dialog** (`trigger: giveitemstack`) — keeping that dialog-side avoids
needing an on-start framework reward hook.

```json
{
  "code": "celeste-crimsonrose",
  "npc": "celeste",
  "groupLangKey": "quest-group-helping-residents",
  "titleLangKey": "quest-celeste-crimsonrose-title",
  "descriptionLangKey": "quest-celeste-crimsonrose-desc",
  "autoEnable": true,
  "objectives": [
    {
      "code": "chest",
      "type": "delivery",
      "items": [{ "item": "seafarer:sealed-chest", "quantity": 1 }],
      "required": 1,
      "rewards": [
        { "type": "addSellingItem", "itemCode": "game:shovel-copper",
          "itemType": "item", "stacksize": 1,
          "stockAvg": 2, "stockVar": 1, "priceAvg": 5, "priceVar": 1 }
      ]
    }
  ],
  "rewards": [
    { "type": "incrementEntityVariable", "variableName": "celeste-friendship",
      "amount": 1, "thresholdValue": 1,
      "thresholdVariable": "entity.celeste-friendly" },
    { "type": "incrementEntityVariable", "variableName": "rebuilding-tier",
      "variableScope": "global", "amount": 1, "thresholdValue": 4,
      "thresholdVariable": "global.rebuilding-complete" }
  ]
}
```

### `celeste-rusthunter.json`

Kill objective. Pattern matches any drifter variant; the framework
counts server-wide kills regardless of who did the killing.

```json
{
  "code": "celeste-rusthunter",
  "npc": "celeste",
  "groupLangKey": "quest-group-helping-residents",
  "titleLangKey": "quest-celeste-rusthunter-title",
  "descriptionLangKey": "quest-celeste-rusthunter-desc",
  "autoEnable": true,
  "objectives": [
    {
      "code": "kills",
      "type": "kill",
      "pattern": "game:drifter-*",
      "required": 10
    }
  ],
  "rewards": [
    { "type": "giveItem", "itemCode": "seafarer:cutlass-blackbronze",
      "itemType": "item", "stacksize": 1 },
    { "type": "addSellingItem", "itemCode": "seafarer:cutlass-copper",
      "itemType": "item", "stacksize": 1,
      "stockAvg": 2, "stockVar": 1, "priceAvg": 8, "priceVar": 2 },
    { "type": "addSellingItem", "itemCode": "seafarer:cutlass-blackbronze",
      "itemType": "item", "stacksize": 1,
      "stockAvg": 1, "stockVar": 1, "priceAvg": 14, "priceVar": 3 },
    { "type": "incrementEntityVariable", "variableName": "celeste-friendship",
      "amount": 1, "thresholdValue": 1,
      "thresholdVariable": "entity.celeste-friendly" },
    { "type": "incrementEntityVariable", "variableName": "rebuilding-tier",
      "variableScope": "global", "amount": 1, "thresholdValue": 4,
      "thresholdVariable": "global.rebuilding-complete" }
  ]
}
```

### `celeste-bearhunter.json`

Five delivery objectives (one per pelt type), `requiredObjectiveCount: 3`.
Each objective turns in exactly one pelt and awards +50 `bearhunter` XP.
Quest completes after any 3 distinct pelts are delivered.

```json
{
  "code": "celeste-bearhunter",
  "npc": "celeste",
  "groupLangKey": "quest-group-helping-residents",
  "titleLangKey": "quest-celeste-bearhunter-title",
  "descriptionLangKey": "quest-celeste-bearhunter-desc",
  "autoEnable": true,
  "requiredObjectiveCount": 3,
  "objectives": [
    { "code": "polar", "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-polar-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }] },
    { "code": "brown", "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-brown-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }] },
    { "code": "black", "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-black-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }] },
    { "code": "panda", "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-panda-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }] },
    { "code": "sun", "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-sun-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }] }
  ],
  "rewards": [
    { "type": "giveItem", "itemCode": "seafarer:arrow-barbed-copper",
      "itemType": "item", "stacksize": 24 },
    { "type": "addSellingItem", "itemCode": "seafarer:arrow-barbed-copper",
      "itemType": "item", "stacksize": 4,
      "stockAvg": 12, "stockVar": 4, "priceAvg": 2, "priceVar": 0.5 },
    { "type": "addSellingItem", "itemCode": "seafarer:trainingbook-bearhunter",
      "itemType": "item", "stacksize": 1,
      "stockAvg": 1, "stockVar": 0, "priceAvg": 10, "priceVar": 2 },
    { "type": "incrementEntityVariable", "variableName": "celeste-friendship",
      "amount": 1, "thresholdValue": 1,
      "thresholdVariable": "entity.celeste-friendly" },
    { "type": "incrementEntityVariable", "variableName": "rebuilding-tier",
      "variableScope": "global", "amount": 1, "thresholdValue": 4,
      "thresholdVariable": "global.rebuilding-complete" }
  ]
}
```

New lang keys required (title + description for each quest): six strings
total in `lang/en.json` (two per quest).

## Dialog changes (`celeste.json`)

The overall structure stays the same: First Meeting → Main Menu → per-quest
branches. The per-quest branches change the triggers and variables they
read/write.

### Main menu gating

Replace `entity.crimsonrose-complete` / `entity.rusthunter-complete` /
`entity.bearhunter-complete` reads with quest-status reads. Pattern for
each quest:

```json
// Crimson Rose: not yet started → intro + offer
{
  "value": "seafarer:dialogue-celeste-ask-crimsonrose",
  "jumpTo": "crimsonrose-intro",
  "conditions": [
    { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isNotValue": "active" },
    { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isNotValue": "completed" }
  ]
},
// Crimson Rose: active → go to in-progress menu
{
  "value": "seafarer:dialogue-celeste-ask-crimsonrose",
  "jumpTo": "crimsonrose-inprogress",
  "conditions": [
    { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isValue": "active" }
  ]
},
// Crimson Rose: completed → post-quest line
{
  "value": "seafarer:dialogue-celeste-ask-crimsonrose",
  "jumpTo": "crimsonrose-done",
  "conditions": [
    { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isValue": "completed" }
  ]
},
```

Same shape for `rusthunter` and `bearhunter`. The existing "friendship
gate" on rust/bear hunter options (`entity.celeste-friendly = true`) is
preserved as an additional condition on those entries.

Rum-for-map and Pirate Tales gates continue to read
`entity.crimsonrose-complete`. That flag is no longer set directly by
dialog, so we replace those reads with
`player.quest-seafarer-celeste-crimsonrose-status isValue "completed"` —
semantically equivalent.

### Crimson Rose flow

```
crimsonrose-intro (talk)
  → crimsonrose-offer (player: accept / not-yet)
    accept → crimsonrose-start (celeste, talk; trigger: questStart celeste-crimsonrose; trigger: giveitemstack map-crimsonrose) → main
    not-yet → main

crimsonrose-inprogress (player menu)
  - have-chest (if inventory sealed-chest) → crimsonrose-deliver
  - remind-quest → crimsonrose-reminder → main
  - goodbye → goodbye

crimsonrose-deliver (celeste, talk; trigger: questDeliver {code, objective:"chest"}; text: "You actually got my loot back?")
  → check-crimsonrose-status (condition: quest status = completed)
    then → crimsonrose-complete-line (talk: "There. Happy?") → main
    else → crimsonrose-progress-line (talk: unreachable — only one delivery needed) → main

crimsonrose-done (talk, post-quest) → main
```

Removed components: `crimsonrose-reward-shovel`, `crimsonrose-complete`
(setVariables+talk), `crimsonrose-grant-friendship`. The shovel add,
friendship bump, and rebuilding-tier bump are quest rewards now.

### Rust Hunter flow

```
rusthunter-intro (talk)
  → rusthunter-offer (player: accept / not-yet)
    accept → rusthunter-start (celeste, talk; trigger: questStart celeste-rusthunter) → main

rusthunter-inprogress (player menu)
  - check-progress ("How many have I killed?") → rusthunter-check
  - goodbye

rusthunter-check (celeste, talk; trigger: questDeliver {code, objective:"kills"}; text: generic ack)
  → check-rusthunter-status (condition: quest status = completed)
    then → rusthunter-reward-intro (talk: "Quieter nights, finally...") → main
    else → rusthunter-status-line (talk: "That's {progress} of 10...") → main

rusthunter-done (talk, post-quest) → main
```

The `rusthunter-statusreport` line (`That's {entity.rustkills} of 10`) is
replaced with a line parameterised on
`{player.quest-seafarer-celeste-rusthunter-kills-progress}`. Lang key text
updates accordingly.

No `deliver-rot` branch and no `game:rot` inventory checks — kills are
tracked automatically by the framework. The player just asks "how am I
doing" and that triggers a progress check.

### Bear Hunter flow

```
bearhunter-intro (talk)
  → bearhunter-offer (player: accept / not-yet)
    accept → bearhunter-start (celeste, talk; trigger: questStart celeste-bearhunter) → main

bearhunter-inprogress (player menu)
  - deliver-polar (if inventory pelt AND objective-polar-status != completed) → bearhunter-deliver-polar
  - deliver-brown (same)
  - deliver-black (same)
  - deliver-panda (same)
  - deliver-sun   (same)
  - status ("How many do I need?") → bearhunter-statusreport → main
  - goodbye

bearhunter-deliver-<type> (celeste, talk; trigger: questDeliver {code, objective:"<type>"}; text: "A fine pelt.")
  → check-bearhunter-status (condition: quest status = completed)
    then → bearhunter-reward-intro → main
    else → main
```

Per-pelt visibility conditions use
`player.quest-seafarer-celeste-bearhunter-<type>-status isNotValue "completed"`
instead of the old `entity.bearhunter-<type>-delivered` flag. The
`bearhunter-statusreport` text stays; its parameter becomes
`{player.quest-seafarer-celeste-bearhunter-count-progress}` — or the lang
line rewords to "Three distinct pelts required" without a counter if the
framework doesn't expose a cross-objective progress count (cheaper; use
the reword).

Removed components: `bearhunter-increment`, `bearhunter-award-xp`,
`bearhunter-add-arrows`, `bearhunter-add-book`,
`bearhunter-give-arrows` (moved to quest rewards), `bearhunter-complete`
(entity flag set), per-pelt `setVariables` lines.

### Shared cleanups

- Delete the `increment-rebuilding-tier` component at the bottom of
  `celeste.json`. Rebuilding-tier is now incremented via quest rewards.
- Delete inline friendship increment components tied to the three
  ported quests: `crimsonrose-grant-friendship`, the
  `rusthunter-complete` inline increment trigger, and the
  `bearhunter-complete` inline increment trigger. Their work moves to
  quest rewards.
- **Keep** `piratetales-grant-friendship`: Pirate Tales stays dialog-only
  and its friendship increment has no quest-reward equivalent.

## Migration / save-compat note

Existing save games with inline flags set (`entity.crimsonrose-complete =
true`, etc.) will still have those flags after update, but the new dialog
reads quest status instead. Players mid-way through a quest on an old save
will see the quest treated as not started. This is acceptable for a
pre-1.0 mod; no special migration shim.

## Testing

Unit tests aren't typical for JSON content; validation happens via
`validate-assets.py` and in-game smoke tests. Plan:

1. Fresh world, meet Celeste. Main menu offers Crimson Rose option (no
   others yet). Accept → map given, quest appears in Quest Log as
   `active`. Talk to Celeste again with no chest → "How's progress"
   branch only.
2. Obtain chest, deliver → quest completes, shovel appears in her shop,
   friendship flag set, rebuilding-tier bumped. Rust Hunter and Bear
   Hunter options now appear in her menu. Rum-for-map option also
   appears when carrying 100 rum portions.
3. Start Rust Hunter. Go kill 5 drifters. Return and ask "how's progress"
   — status line reports 5 of 10. Kill 5 more. Return, ask again — quest
   completes, Vengeance (blackbronze cutlass) dropped, shop expanded.
4. Start Bear Hunter. Deliver polar pelt → objective completes, 50 XP.
   Deliver brown → 100 XP. Deliver black → 150 XP and quest completes (3
   of 5). Sun and panda pelts no longer offered in menu.
5. Verify friendship and rebuilding-tier increments fire exactly three
   times total across the three quest completions (one per completion).

## Files touched

**Framework:**

- `ProgressionFramework/ProgressionFramework/Quests/Quest.cs` — add
  `RequiredObjectiveCount` on Quest, `Pattern` on QuestObjective.
- `ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs` —
  generalise `TryDeliver` to dispatch on type; update
  `AllObjectivesComplete`; subscribe `OnEntityDeath` at server start;
  build pattern registry at `AssetsFinalize`.
- `ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs`
  or a new `QuestObjectiveHandlers.cs` — add `Kill` handler alongside
  existing `Delivery`. May split into a dedicated file for clarity.

**Seafarer:**

- Create `Seafarer/Seafarer/assets/seafarer/config/quests/celeste-crimsonrose.json`
- Create `Seafarer/Seafarer/assets/seafarer/config/quests/celeste-rusthunter.json`
- Create `Seafarer/Seafarer/assets/seafarer/config/quests/celeste-bearhunter.json`
- Modify `Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json`
  (largest change — restructure three quest branches, delete obsolete
  increment components).
- Modify `Seafarer/Seafarer/assets/seafarer/lang/en.json` — add six quest
  title/description keys; update `dialogue-celeste-rusthunter-statusreport`
  text to reference the new progress variable.
- Modify `Seafarer/Seafarer/quests/celeste.md` — update to reflect the
  QuestSystem-based variable names (so the doc isn't stale).
