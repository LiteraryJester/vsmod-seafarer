# Drake Dialogue & Quest Fixes

## Context

Drake's dialog (`assets/seafarer/config/dialogue/drake.json`) and companion quests
(`drake-tradeship`, `drake-tricks`, `drake-seasoned`) have three issues:

1. **Broken tricks dialogue flow.** `tricks-lesson1` jumps to `tricks-lesson2`,
   which has lang text but no dialogue component. The `tricks-tasks` component
   (which fires `questStart` for `drake-tricks`) is orphaned — nothing reaches
   it. Result: once the player enters the tricks branch, the quest never
   starts and the conversation dead-ends.
2. **`entity.rebuilding-complete` is unreliable.** Morgan, Celeste, Dawnmarie,
   and the `drake-tradeship` reward each increment a `rebuilding-tier` counter
   scoped to **that NPC's entity**. Each NPC's counter is independent; no
   single NPC's counter reliably reaches 4, so the threshold-triggered
   `entity.rebuilding-complete` flag rarely flips, and when it does it lives
   on an arbitrary NPC entity rather than anywhere a player or other dialog
   can read it.
3. **Tricks unlock gating.** The tricks menu option currently requires
   `global.quest-seafarer-drake-tradeship-status = completed`. Per user intent,
   it should unlock on **either** the tradeship quest being completed **or**
   the rebuilding tier hitting 4 (via any NPC chain).

## Goals

- Fix the tricks dialogue so `questStart` actually fires and the branch
  has no dead-ends.
- Move `rebuilding-tier` accounting to **global scope** so increments
  accumulate across all NPCs and a single `global.rebuilding-complete` flag
  reliably flips when four rebuilding-tier quests are done (regardless of
  which NPC owned them).
- Unlock the tricks branch when either trigger condition is met,
  without the option appearing twice when both are true.

## Non-goals

- Birch → seasoned plank swap in the tradeship quest (deferred; user wants
  birch for now).
- Changes to the blackbronze milestone's own gating logic (still requires
  both rebuilding-complete **and** tradeship-completed; only the variable
  reference is updated to the new global scope).
- New reward types or new dialogue condition operators.
- Changes to the `drake-seasoned` quest content; it continues to unlock via
  tricks completion inside the tricks-menu.

## Design

### 1. Dialogue flow fix (`drake.json`)

Add the two missing components and wire the intro chain end-to-end:

```
tricks-intro
  → tricks-lesson1  (existing)
  → tricks-lesson2  (NEW: text = seafarer:dialogue-drake-tricks-lesson2)
  → tricks-lesson3  (NEW: text = seafarer:dialogue-drake-tricks-lesson3)
  → tricks-tasks    (existing; fires questStart for drake-tricks)
  → tricks-menu     (existing)
```

Both new components are plain `owner: "drake"`, `type: "talk"` nodes. Lang
keys `seafarer:dialogue-drake-tricks-lesson2` and `…-lesson3` already exist
in `lang/en.json`.

### 2. Global rebuilding-tier scope

**Framework change** (`ProgressionFramework`, `Quest.cs` +
`QuestRewardHandler.cs`):

- Add an optional `VariableScope` string property to `QuestReward`
  (defaults to `"entity"` for backward compatibility).
- In `BuiltInRewardHandlers.IncrementEntityVariable.Grant`, read
  `reward.VariableScope ?? "entity"` into the `scope` field of the
  dispatched `incrementVariable` trigger data instead of the current
  hardcoded `"entity"`.

This lets a quest reward specify `"variableScope": "global"` without
affecting existing content.

**Seafarer JSON changes:**

| File | Change |
|---|---|
| `dialogue/morgan.json` `increment-rebuilding-tier` | `scope: "entity"` → `"global"`; `thresholdVariable: "entity.rebuilding-complete"` → `"global.rebuilding-complete"` |
| `dialogue/celeste.json` `increment-rebuilding-tier` | same |
| `dialogue/dawnmarie.json` `increment-rebuilding-tier` | same |
| `quests/drake-tradeship.json` `incrementEntityVariable` reward | add `"variableScope": "global"`; `thresholdVariable: "entity.rebuilding-complete"` → `"global.rebuilding-complete"` |
| `dialogue/drake.json` `check-blackbronze-rebuilding` | `variable: "entity.rebuilding-complete"` → `"global.rebuilding-complete"` |

Once all four rebuilding-tier quests have been completed (any order, any
mix of NPCs), `global.rebuilding-tier` reaches 4 and
`global.rebuilding-complete` is set to `"true"` once.

### 3. Tricks unlock gate (`drake.json`)

Replace the single tricks menu entry with two mutually-exclusive entries
pointing to the same `tricks-gate` target, so the option only appears once
when either condition is met:

```json
// Path A: rebuilding hit tier 4 (accumulated across all NPCs)
{
    "value": "seafarer:dialogue-drake-ask-tricks",
    "jumpTo": "tricks-gate",
    "conditions": [
        { "variable": "global.rebuilding-complete", "isValue": "true" }
    ]
},
// Path B: tradeship completed but rebuilding not yet at 4
{
    "value": "seafarer:dialogue-drake-ask-tricks",
    "jumpTo": "tricks-gate",
    "conditions": [
        { "variable": "global.rebuilding-complete", "isNotValue": "true" },
        { "variable": "global.quest-seafarer-drake-tradeship-status", "isValue": "completed" }
    ]
}
```

Mutual exclusivity comes from Path B requiring `rebuilding-complete !=
"true"` — this is safe because base-game dialog conditions compare raw
strings and an unset variable returns empty/null, which is != "true".

**Quest prereq:** `drake-tricks.json` currently has
`prerequisites: ["drake-tradeship"]`, which is AND-only in the framework
(`QuestSystem.IsAvailable`). On the rebuilding-complete path, this hard
prereq would silently block the quest from starting. Remove the
prerequisite entirely — gating is fully in dialog now.

## Data flow

Once a player does their 4th rebuilding quest with any NPC:

1. NPC dialog component (`increment-rebuilding-tier`) fires
   `incrementVariable` trigger with global scope.
2. `EntityEvolvingTrader.HandleIncrementVariable` bumps
   `global.rebuilding-tier` via `VariablesModSystem` and, because the new
   value ≥ 4 and a threshold variable is set, writes
   `global.rebuilding-complete = "true"`.
3. Next time the player opens Drake's main menu, the Path A tricks
   condition passes, the option appears, and selecting it routes through
   `tricks-gate` → `tricks-intro` → lesson1 → lesson2 → lesson3 →
   tricks-tasks (fires `questStart drake-tricks`) → tricks-menu.

For players on the tradeship path: when the last delivery completes the
tradeship quest, the reward fires `incrementVariable` with global scope
(via the new `variableScope` field), contributing to the same counter.
Simultaneously `tradeship-status` becomes `completed`, and on the next
menu open Path B activates until the tier later reaches 4, at which point
Path A takes over seamlessly.

## Testing

Since these are JSON-only data changes plus a tiny framework field, the
existing asset validator covers structural correctness. Manual in-game
testing after build:

1. Fresh world, first meet with drake — no tricks option in main menu.
2. Talk to morgan/celeste/dawnmarie enough to trigger their
   rebuilding-tier bumps. After the 4th bump from any combination,
   drake's main menu shows the tricks option.
3. Selecting tricks walks through lessons 1/2/3 and starts
   `drake-tricks` (verify in Quest Log).
4. Alternative path: complete drake-tradeship without reaching tier 4.
   Tricks option appears via Path B.
5. Blackbronze milestone still fires correctly after both flags
   (`global.rebuilding-complete = true` AND tradeship completed) are
   observed.

## Files touched

- `ProgressionFramework/ProgressionFramework/Quests/Quest.cs` — add
  `VariableScope` field
- `ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs` —
  read `VariableScope` instead of hardcoding `"entity"`
- `Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json`
- `Seafarer/Seafarer/assets/seafarer/config/dialogue/morgan.json`
- `Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json`
- `Seafarer/Seafarer/assets/seafarer/config/dialogue/dawnmarie.json`
- `Seafarer/Seafarer/assets/seafarer/config/quests/drake-tradeship.json`
- `Seafarer/Seafarer/assets/seafarer/config/quests/drake-tricks.json`
