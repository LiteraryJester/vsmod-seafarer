# Drake Dialogue & Quest Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix Drake's broken tricks dialogue flow, move rebuilding-tier tracking to global scope so increments accumulate across all NPCs, and gate the tricks menu option on either `global.rebuilding-complete` or tradeship completion.

**Architecture:** JSON-only data changes in the `seafarer` mod plus one small additive field on `QuestReward` in the `progressionframework` mod. The framework change unlocks a `"variableScope": "global"` option on the `incrementEntityVariable` reward type (defaults remain `"entity"` for backward compat). All gating logic lives in dialogue; no new reward types, no new condition operators.

**Tech Stack:** C# / .NET 10 (ProgressionFramework), Vintage Story JSON5 assets (Seafarer). Build via `dotnet build`. Asset validation via `python3 validate-assets.py`.

**Spec:** `docs/superpowers/specs/2026-04-14-drake-dialog-fixes-design.md`

---

## File Structure

**Framework (`vsmod-progression-framework`):**
- Modify: `ProgressionFramework/ProgressionFramework/Quests/Quest.cs` — add `VariableScope` property on `QuestReward`.
- Modify: `ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs` — read `VariableScope` in `IncrementEntityVariable.Grant`.

**Seafarer (`vsmod-seafarer`):**
- Modify: `Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json` — add `tricks-lesson2` / `tricks-lesson3` components, update blackbronze check variable, replace single tricks menu entry with OR-gated pair.
- Modify: `Seafarer/Seafarer/assets/seafarer/config/dialogue/morgan.json` — change `increment-rebuilding-tier` scope and threshold variable.
- Modify: `Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json` — same.
- Modify: `Seafarer/Seafarer/assets/seafarer/config/dialogue/dawnmarie.json` — same.
- Modify: `Seafarer/Seafarer/assets/seafarer/config/quests/drake-tradeship.json` — add `variableScope`, update `thresholdVariable`.
- Modify: `Seafarer/Seafarer/assets/seafarer/config/quests/drake-tricks.json` — remove `prerequisites`.

---

## Task 1: Framework — add `VariableScope` field on `QuestReward`

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/Quest.cs` (near lines 86–91)
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs` (lines 40–51)

- [ ] **Step 1: Add `VariableScope` property on `QuestReward`**

In `Quest.cs`, locate the `IncrementEntityVariable` field group (currently):

```csharp
    // IncrementEntityVariable
    public string? VariableName { get; set; }
    public int Amount { get; set; } = 1;
    public int ThresholdValue { get; set; }
    public string? ThresholdVariable { get; set; }
}
```

Replace with:

```csharp
    // IncrementEntityVariable
    public string? VariableName { get; set; }
    public int Amount { get; set; } = 1;
    public int ThresholdValue { get; set; }
    public string? ThresholdVariable { get; set; }
    /// <summary>Scope for the target variable. Valid values: "entity" (default),
    /// "player", "global", "group". Defaults to "entity" so existing quests
    /// keep working unchanged.</summary>
    public string? VariableScope { get; set; }
}
```

- [ ] **Step 2: Use the field in the reward handler**

In `QuestRewardHandler.cs`, locate `IncrementEntityVariable.Grant` (lines 40–51):

```csharp
    public sealed class IncrementEntityVariable : IQuestRewardHandler
    {
        public void Grant(QuestReward reward, IServerPlayer player, Entity npc, ICoreAPI api)
        {
            DispatchToTrader(npc, player.Entity, "incrementVariable", new JsonObject(new JObject
            {
                ["scope"] = "entity",
                ["name"] = reward.VariableName,
                ["amount"] = reward.Amount,
                ["thresholdValue"] = reward.ThresholdValue,
                ["thresholdVariable"] = reward.ThresholdVariable
            }));
        }
    }
```

Replace the hardcoded `"entity"` with the field, falling back to `"entity"`:

```csharp
    public sealed class IncrementEntityVariable : IQuestRewardHandler
    {
        public void Grant(QuestReward reward, IServerPlayer player, Entity npc, ICoreAPI api)
        {
            DispatchToTrader(npc, player.Entity, "incrementVariable", new JsonObject(new JObject
            {
                ["scope"] = reward.VariableScope ?? "entity",
                ["name"] = reward.VariableName,
                ["amount"] = reward.Amount,
                ["thresholdValue"] = reward.ThresholdValue,
                ["thresholdVariable"] = reward.ThresholdVariable
            }));
        }
    }
```

- [ ] **Step 3: Build framework to verify compile**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework.csproj
```

Expected: `Build succeeded` with 0 errors. Warnings permitted.

- [ ] **Step 4: Commit framework change**

```bash
cd /mnt/d/Development/vs/vsmod-progression-framework && \
git add ProgressionFramework/ProgressionFramework/Quests/Quest.cs \
        ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs && \
git commit -m "feat(quests): allow variableScope override on incrementEntityVariable reward

Adds optional QuestReward.VariableScope field (default \"entity\", back-compat).
Enables consumer mods to accumulate reward counters at global/player/group
scope rather than per-NPC."
```

---

## Task 2: Add missing `tricks-lesson2` and `tricks-lesson3` components

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json`

- [ ] **Step 1: Insert lesson2 and lesson3 components between `tricks-lesson1` and `tricks-tasks`**

Locate the existing block (around the current `tricks-lesson1` → `tricks-tasks`
sequence):

```json
        {
            "code": "tricks-lesson1",
            "owner": "drake",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-drake-tricks-lesson1" }],
            "jumpTo": "tricks-lesson2"
        },
        {
            "code": "tricks-tasks",
```

Replace with (inserts two new components and updates `tricks-lesson1`'s
`jumpTo` target, which already points at `tricks-lesson2` — confirm it does;
also confirm the new chain reaches `tricks-tasks`):

```json
        {
            "code": "tricks-lesson1",
            "owner": "drake",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-drake-tricks-lesson1" }],
            "jumpTo": "tricks-lesson2"
        },
        {
            "code": "tricks-lesson2",
            "owner": "drake",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-drake-tricks-lesson2" }],
            "jumpTo": "tricks-lesson3"
        },
        {
            "code": "tricks-lesson3",
            "owner": "drake",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-drake-tricks-lesson3" }],
            "jumpTo": "tricks-tasks"
        },
        {
            "code": "tricks-tasks",
```

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors. (Warnings about pre-existing conditions are fine.)

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json && \
git commit -m "fix(drake): wire tricks-lesson2/lesson3 so questStart actually fires

Previously tricks-lesson1 jumped to a non-existent tricks-lesson2 component,
dead-ending the branch and leaving tricks-tasks (which fires questStart for
drake-tricks) unreachable."
```

---

## Task 3: Move morgan's `increment-rebuilding-tier` to global scope

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/morgan.json` (around lines 260–271)

- [ ] **Step 1: Update scope and threshold variable**

Locate:

```json
    {
      "code": "increment-rebuilding-tier",
      "owner": "morgan",
      "trigger": "incrementVariable",
      "triggerdata": {
        "scope": "entity",
        "name": "rebuilding-tier",
        "amount": 1,
        "thresholdValue": 4,
        "thresholdVariable": "entity.rebuilding-complete"
      },
      "jumpTo": "main"
    },
```

Replace with:

```json
    {
      "code": "increment-rebuilding-tier",
      "owner": "morgan",
      "trigger": "incrementVariable",
      "triggerdata": {
        "scope": "global",
        "name": "rebuilding-tier",
        "amount": 1,
        "thresholdValue": 4,
        "thresholdVariable": "global.rebuilding-complete"
      },
      "jumpTo": "main"
    },
```

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors.

---

## Task 4: Move celeste's `increment-rebuilding-tier` to global scope

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json` (around lines 846–858)

- [ ] **Step 1: Update scope and threshold variable**

Locate:

```json
        {
            "code": "increment-rebuilding-tier",
            "owner": "celeste",
            "trigger": "incrementVariable",
            "triggerdata": {
                "scope": "entity",
                "name": "rebuilding-tier",
                "amount": 1,
                "thresholdValue": 4,
                "thresholdVariable": "entity.rebuilding-complete"
            },
            "jumpTo": "main"
        }
```

Replace with:

```json
        {
            "code": "increment-rebuilding-tier",
            "owner": "celeste",
            "trigger": "incrementVariable",
            "triggerdata": {
                "scope": "global",
                "name": "rebuilding-tier",
                "amount": 1,
                "thresholdValue": 4,
                "thresholdVariable": "global.rebuilding-complete"
            },
            "jumpTo": "main"
        }
```

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors.

---

## Task 5: Move dawnmarie's `increment-rebuilding-tier` to global scope

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/dawnmarie.json` (around lines 340–352)

- [ ] **Step 1: Update scope and threshold variable**

Locate:

```json
        {
            "code": "increment-rebuilding-tier",
            "owner": "dawnmarie",
            "trigger": "incrementVariable",
            "triggerdata": {
                "scope": "entity",
                "name": "rebuilding-tier",
                "amount": 1,
                "thresholdValue": 4,
                "thresholdVariable": "entity.rebuilding-complete"
            },
            "jumpTo": "main"
        }
```

Replace with:

```json
        {
            "code": "increment-rebuilding-tier",
            "owner": "dawnmarie",
            "trigger": "incrementVariable",
            "triggerdata": {
                "scope": "global",
                "name": "rebuilding-tier",
                "amount": 1,
                "thresholdValue": 4,
                "thresholdVariable": "global.rebuilding-complete"
            },
            "jumpTo": "main"
        }
```

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors.

---

## Task 6: Move drake-tradeship's reward increment to global scope

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/quests/drake-tradeship.json` (the top-level `rewards` array, lines 131–139)

- [ ] **Step 1: Add `variableScope` and update `thresholdVariable`**

Locate:

```json
  "rewards": [
    {
      "type": "incrementEntityVariable",
      "variableName": "rebuilding-tier",
      "amount": 1,
      "thresholdValue": 4,
      "thresholdVariable": "entity.rebuilding-complete"
    }
  ]
```

Replace with:

```json
  "rewards": [
    {
      "type": "incrementEntityVariable",
      "variableName": "rebuilding-tier",
      "variableScope": "global",
      "amount": 1,
      "thresholdValue": 4,
      "thresholdVariable": "global.rebuilding-complete"
    }
  ]
```

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors.

---

## Task 7: Update drake.json blackbronze check to read `global.rebuilding-complete`

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json` (around lines 54–61)

- [ ] **Step 1: Change the variable reference**

Locate:

```json
        {
            "code": "check-blackbronze-rebuilding",
            "owner": "drake",
            "type": "condition",
            "variable": "entity.rebuilding-complete",
            "isValue": "true",
            "thenJumpTo": "check-blackbronze-tradeship",
            "elseJumpTo": "main"
        },
```

Replace with:

```json
        {
            "code": "check-blackbronze-rebuilding",
            "owner": "drake",
            "type": "condition",
            "variable": "global.rebuilding-complete",
            "isValue": "true",
            "thenJumpTo": "check-blackbronze-tradeship",
            "elseJumpTo": "main"
        },
```

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors.

- [ ] **Step 3: Commit Tasks 3–7 together**

All five changes are one logical shift (per-NPC entity counters → one
shared global counter). Commit together:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/morgan.json \
        Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json \
        Seafarer/Seafarer/assets/seafarer/config/dialogue/dawnmarie.json \
        Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json \
        Seafarer/Seafarer/assets/seafarer/config/quests/drake-tradeship.json && \
git commit -m "refactor(rebuilding): accumulate rebuilding-tier at global scope

Per-NPC entity-scoped counters never accumulated across dialogs; no single
NPC's counter reliably reached the threshold of 4 so entity.rebuilding-complete
never flipped. Move all four increment sites (morgan/celeste/dawnmarie dialogs +
drake-tradeship reward) and drake's blackbronze gate to global scope so the
counter tallies across all NPCs and a single global.rebuilding-complete flag
flips once when any four rebuilding-tier quests are completed.

Requires progressionframework's new QuestReward.VariableScope field."
```

---

## Task 8: Replace tricks menu option with OR-gated pair

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json` (the `main` menu component, the single tricks entry around lines 177–184)

- [ ] **Step 1: Replace the single tricks menu entry with two mutually-exclusive entries**

Locate (inside the `main` component's `text` array):

```json
                // Tricks: unlocked once tradeship is done
                {
                    "value": "seafarer:dialogue-drake-ask-tricks",
                    "jumpTo": "tricks-gate",
                    "conditions": [
                        { "variable": "global.quest-seafarer-drake-tradeship-status", "isValue": "completed" }
                    ]
                }
```

Replace with:

```json
                // Tricks path A: rebuilding hit tier 4 (cross-NPC counter)
                {
                    "value": "seafarer:dialogue-drake-ask-tricks",
                    "jumpTo": "tricks-gate",
                    "conditions": [
                        { "variable": "global.rebuilding-complete", "isValue": "true" }
                    ]
                },
                // Tricks path B: tradeship completed but rebuilding not yet at 4
                {
                    "value": "seafarer:dialogue-drake-ask-tricks",
                    "jumpTo": "tricks-gate",
                    "conditions": [
                        { "variable": "global.rebuilding-complete", "isNotValue": "true" },
                        { "variable": "global.quest-seafarer-drake-tradeship-status", "isValue": "completed" }
                    ]
                }
```

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/drake.json && \
git commit -m "feat(drake): unlock tricks on rebuilding-complete OR tradeship done

Two mutually-exclusive menu entries so the option only appears once even
when both triggers fire."
```

---

## Task 9: Remove hard prerequisite from drake-tricks

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/quests/drake-tricks.json` (line 8)

- [ ] **Step 1: Delete the `prerequisites` line**

Locate:

```json
{
  "code": "drake-tricks",
  "npc": "drake",
  "groupLangKey": "quest-group-helping-residents",
  "titleLangKey": "quest-drake-tricks-title",
  "descriptionLangKey": "quest-drake-tricks-desc",
  "autoEnable": true,
  "prerequisites": ["drake-tradeship"],
  "objectives": [
```

Replace with (drop the `prerequisites` line entirely):

```json
{
  "code": "drake-tricks",
  "npc": "drake",
  "groupLangKey": "quest-group-helping-residents",
  "titleLangKey": "quest-drake-tricks-title",
  "descriptionLangKey": "quest-drake-tricks-desc",
  "autoEnable": true,
  "objectives": [
```

**Why:** The framework AND's all prerequisites (`QuestSystem.IsAvailable`
lines 388–395). With tradeship as a hard prereq, the rebuilding-complete
unlock path would silently fail to start the quest. Dialog now handles
gating; this prereq would block a valid entry path.

- [ ] **Step 2: Run asset validator**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/quests/drake-tricks.json && \
git commit -m "fix(drake-tricks): drop tradeship prerequisite — dialog now gates

Framework prerequisites are AND-only; keeping drake-tradeship as a prereq
would block the rebuilding-complete unlock path. Dialog-side OR gating
already covers both entry points."
```

---

## Task 10: Final build + validator

- [ ] **Step 1: Build Seafarer (pulls the framework reference)**

Run:

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded` with 0 errors. Warnings permitted.

- [ ] **Step 2: Run asset validator one more time**

Run:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: exit code 0, 0 errors.

- [ ] **Step 3: In-game smoke test (manual checklist)**

Launch the game with both mods loaded. On a fresh world:

1. Talk to Drake on first meet — confirm no "tricks" option is in the menu.
2. Start `drake-tradeship` and deliver enough of each material to not yet
   complete it. Talk to Morgan → complete the mine quest; confirm Drake's
   main menu still shows no tricks option.
3. Do 3 more rebuilding-tier-awarding quests (any mix of celeste /
   dawnmarie / morgan / drake-tradeship). On the 4th rebuilding-tier
   increment, `global.rebuilding-complete` should flip. Talk to Drake →
   **tricks** option should now appear (once, not twice).
4. Select tricks → verify the lesson1 → lesson2 → lesson3 → tasks walk
   appears and the `drake-tricks` quest is listed in the Quest Log
   afterward.
5. On a separate save, complete `drake-tradeship` **before** reaching four
   rebuilding-tier bumps. Talk to Drake → tricks option appears via Path B.
6. Continue until `global.rebuilding-complete` flips. Confirm the tricks
   option still appears exactly once (Path A takes over; Path B
   automatically drops because `rebuilding-complete = "true"` fails its
   `isNotValue` clause).
7. Verify the blackbronze milestone still fires once **both**
   `global.rebuilding-complete = true` and tradeship completion are true.

If all seven pass, the change is done.

---

## Self-review notes

- Spec coverage: dialogue flow fix (Task 2), global scope migration (Tasks 1,
  3–7), OR-gated tricks unlock (Task 8), prerequisite removal (Task 9),
  verification (Task 10). All spec sections accounted for.
- No placeholders: every JSON replacement shows exact before/after. Every
  command has expected output.
- Type consistency: the new field is `VariableScope` (C#) / `variableScope`
  (JSON) consistently; `global.rebuilding-complete` and `global.rebuilding-tier`
  used identically everywhere. No `entity.rebuilding-complete` references remain
  after Task 7.
