# Celeste Quest Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port Celeste's three quests (Crimson Rose, Rust Hunter, Bear Hunter) from inline-dialog-variable implementation to proper ProgressionFramework QuestSystem quests, adding the two framework features needed to model them cleanly (any-N-of-M objective completion and native kill tracking).

**Architecture:** Two additive, backward-compatible framework extensions in `vsmod-progression-framework`: an optional `RequiredObjectiveCount` on `Quest`, and a new `kill`-type objective backed by a pattern-indexed server-side death counter. Content work in `vsmod-seafarer` creates three quest JSON files, rewrites Celeste's three quest dialog branches to fire `questStart`/`questDeliver` triggers instead of setting entity flags, and moves friendship + rebuilding-tier increments from inline dialog components to quest rewards.

**Tech Stack:** C# / .NET 10 (ProgressionFramework), Vintage Story JSON5 assets (Seafarer). Build via `dotnet build`. Asset validation via `python3 validate-assets.py`. VS API's `WildcardUtil.Match` for pattern matching.

**Spec:** `Seafarer/docs/superpowers/specs/2026-04-14-celeste-quests-port-design.md`

---

## File Structure

**Framework (`vsmod-progression-framework`):**
- Modify: `ProgressionFramework/ProgressionFramework/Quests/Quest.cs` — add `RequiredObjectiveCount` on `Quest`, `Pattern` on `QuestObjective`.
- Modify: `ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs` — update `AllObjectivesComplete`, refactor `TryDeliver` to dispatch via handler, subscribe `OnEntityDeath`, maintain pattern registry.
- Modify: `ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs` — move existing delivery logic into `BuiltInObjectiveHandlers.Delivery.TryProgress`; add `BuiltInObjectiveHandlers.Kill`. Rename file later if size justifies.

**Seafarer content:**
- Create: `Seafarer/Seafarer/assets/seafarer/config/quests/celeste-crimsonrose.json`
- Create: `Seafarer/Seafarer/assets/seafarer/config/quests/celeste-rusthunter.json`
- Create: `Seafarer/Seafarer/assets/seafarer/config/quests/celeste-bearhunter.json`
- Modify: `Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json` (largest change: rewrites three quest branches, removes orphan components).
- Modify: `Seafarer/Seafarer/assets/seafarer/lang/en.json` — add 6 quest title/description keys, update one status-report line.
- Modify: `Seafarer/Seafarer/quests/celeste.md` — update variable names so the design-doc stays accurate.

---

## Task 1: Framework — `RequiredObjectiveCount` on `Quest`

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/Quest.cs`
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs` (around line 534, `AllObjectivesComplete`)

- [ ] **Step 1: Add the field on `Quest`**

In `Quest.cs`, find the `Quest` class (begins around line 97). After `Prerequisites` (around line 119), add:

```csharp
    /// <summary>Optional completion threshold. When set, the quest completes
    /// once this many objectives are complete rather than requiring all of
    /// them. Null (default) keeps AND-all behavior. Objectives that remain
    /// incomplete when the quest finishes stay pending and further turn-ins
    /// are rejected because the quest is no longer Active.</summary>
    public int? RequiredObjectiveCount { get; set; }
```

- [ ] **Step 2: Update `AllObjectivesComplete`**

In `QuestSystem.cs` find `AllObjectivesComplete` (around line 534). Current:

```csharp
    private bool AllObjectivesComplete(IServerPlayer player, Quest quest)
    {
        foreach (var obj in quest.Objectives)
        {
            if (!IsObjectiveComplete(player, quest.Code, obj.Code)) return false;
        }
        return true;
    }
```

Replace with:

```csharp
    private bool AllObjectivesComplete(IServerPlayer player, Quest quest)
    {
        int required = quest.RequiredObjectiveCount ?? quest.Objectives.Count;
        int done = 0;
        foreach (var obj in quest.Objectives)
        {
            if (IsObjectiveComplete(player, quest.Code, obj.Code)) done++;
        }
        return done >= required;
    }
```

- [ ] **Step 3: Build**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/ProgressionFramework.csproj
```

Expected: `Build succeeded` 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-progression-framework && \
git add ProgressionFramework/ProgressionFramework/Quests/Quest.cs \
        ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs && \
git commit -m "$(cat <<'EOF'
feat(quests): allow any-N-of-M objective completion via RequiredObjectiveCount

Optional int on Quest. When set, AllObjectivesComplete returns true once
that many objectives are complete rather than all. Null keeps AND-all,
fully backward compatible. Enables quests like "any 3 of 5 variants".

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Framework — dispatch `TryDeliver` through objective handlers

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/Quest.cs` — add `Pattern` on `QuestObjective`.
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs` — add the `Delivery` handler class (implements `IQuestObjectiveHandler`) with current delivery logic.
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs` — refactor `TryDeliver` to dispatch through the handler registry; register built-in `Delivery`.

**Goal:** Move current hardcoded delivery logic out of `TryDeliver` and into a handler so additional objective types (e.g., `Kill`) can coexist with delivery. Behavior for existing quests is unchanged.

- [ ] **Step 1: Add `Pattern` on `QuestObjective`**

In `Quest.cs` find the `QuestObjective` class. After the existing delivery fields (`Items`, `Required`) add:

```csharp
    /// <summary>Pattern used by objective types that filter entity codes
    /// (e.g., <c>kill</c>). Matched against <c>entity.Code.ToString()</c>
    /// via <see cref="Vintagestory.API.Util.WildcardUtil.Match(string, string)"/>.
    /// Example: <c>"game:drifter-*"</c>. Null for objective types that
    /// don't use it.</summary>
    public string? Pattern { get; set; }
```

- [ ] **Step 2: Add the `Delivery` handler (moving existing logic)**

In `QuestRewardHandler.cs` at the bottom of the file (but inside the `namespace ProgressionFramework.Quests;` block), after the `BuiltInRewardHandlers` static class closes, add a new static class `BuiltInObjectiveHandlers`. Current file already has the namespace declaration; append:

```csharp

/// <summary>Built-in quest objective handlers, registered by <see cref="QuestSystem"/>.</summary>
internal static class BuiltInObjectiveHandlers
{
    /// <summary>Standard delivery objective: take a full set of required items
    /// from the player's inventory and advance progress by however many full
    /// sets they could cover, capped at the remaining threshold.</summary>
    public sealed class Delivery : IQuestObjectiveHandler
    {
        public void OnObjectiveStarted(IServerPlayer player, Quest quest, QuestObjective objective) { }

        public bool TryProgress(IServerPlayer player, Quest quest, QuestObjective objective, object eventData)
        {
            var ctx = (QuestProgressContext)eventData;
            int currentProgress = ctx.ObjTree.GetInt("progress");
            int remainingNeeded = objective.Required - currentProgress;
            if (remainingNeeded <= 0) return false;

            int maxTurnIns = int.MaxValue;
            foreach (var req in objective.Items)
            {
                if (req.Quantity <= 0) continue;
                int have = ctx.CountInInventory(player, req.Item);
                int canDo = have / req.Quantity;
                if (canDo < maxTurnIns) maxTurnIns = canDo;
            }

            int turnIns = Math.Min(maxTurnIns, remainingNeeded);
            if (turnIns <= 0)
            {
                ctx.Api.Logger.Notification("[Quest] TryDeliver: {0} missing items for '{1}/{2}'",
                    player.PlayerName, quest.Code, objective.Code);
                return false;
            }

            foreach (var req in objective.Items)
            {
                ctx.TakeFromInventory(player, req.Item, req.Quantity * turnIns);
            }

            int progress = currentProgress + turnIns;
            ctx.ObjTree.SetInt("progress", progress);

            ctx.Api.Logger.Notification("[Quest] TryDeliver: {0} turned in {1}x '{2}/{3}' ({4}/{5})",
                player.PlayerName, turnIns, quest.Code, objective.Code, progress, objective.Required);

            return true;
        }
    }
}

/// <summary>Shared context the handlers receive for `TryProgress`. Lets them
/// read/write the objective's state tree and use inventory helpers without
/// duplicating them per handler.</summary>
internal sealed class QuestProgressContext
{
    public ICoreServerAPI Api { get; init; } = null!;
    public Vintagestory.API.Datastructures.ITreeAttribute ObjTree { get; init; } = null!;
    public Func<IServerPlayer, string, int> CountInInventory { get; init; } = null!;
    public Action<IServerPlayer, string, int> TakeFromInventory { get; init; } = null!;
    public Entity Npc { get; init; } = null!;
}
```

Ensure `using` statements at the top of the file include:
```csharp
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
```
(The existing file already has most of these; add any missing.)

- [ ] **Step 3: Refactor `TryDeliver` to dispatch via handler registry**

In `QuestSystem.cs`, locate `RegisterObjectiveType("delivery", ...)` around line 70 and update the registration to use the new `BuiltInObjectiveHandlers.Delivery`:

```csharp
        RegisterObjectiveType("delivery", new BuiltInObjectiveHandlers.Delivery());
```

Then locate `TryDeliver` (starts around line 425). Replace its body from the objective-type check through to the end-of-method (before the closing `}`) with handler dispatch. Full replacement of the method:

```csharp
    public bool TryDeliver(IServerPlayer player, string questCode, string objectiveCode, Entity npc)
    {
        questCode = ResolveCode(questCode);
        var quest = GetQuest(questCode);
        if (quest == null)
        {
            api?.Logger.Warning("[Quest] TryDeliver: unknown quest '{0}'", questCode);
            return false;
        }
        var status = GetStatus(player, questCode);
        if (status != QuestStatus.Active)
        {
            api?.Logger.Warning("[Quest] TryDeliver: quest '{0}' is {1}, not active", questCode, status);
            return false;
        }

        var objective = quest.Objectives.Find(o => o.Code == objectiveCode);
        if (objective == null)
        {
            api?.Logger.Warning("[Quest] TryDeliver: no objective '{0}' in quest '{1}'", objectiveCode, questCode);
            return false;
        }
        if (IsObjectiveComplete(player, questCode, objectiveCode))
        {
            api?.Logger.Notification("[Quest] TryDeliver: objective '{0}/{1}' already complete", questCode, objectiveCode);
            return false;
        }

        if (!objectiveHandlers.TryGetValue(objective.Type, out var handler))
        {
            api?.Logger.Warning("[Quest] TryDeliver: no handler registered for objective type '{0}'", objective.Type);
            return false;
        }

        var objTree = GetOrAddObjectiveTree(player, quest, objectiveCode);
        var ctx = new QuestProgressContext
        {
            Api = sapi!,
            ObjTree = objTree,
            CountInInventory = CountInInventory,
            TakeFromInventory = TakeFromInventory,
            Npc = npc
        };

        bool advanced = handler.TryProgress(player, quest, objective, ctx);
        if (!advanced) return false;

        int progress = objTree.GetInt("progress");
        ObjectiveProgressed?.Invoke(player, quest, objective);

        if (progress >= objective.Required)
        {
            objTree.SetString("status", "completed");
            ApplyRewards(player, objective.Rewards, quest, npc);
            ObjectiveCompleted?.Invoke(player, quest, objective);
        }

        MarkDirty(player, quest);
        SyncFlat(player, quest);

        if (AllObjectivesComplete(player, quest))
        {
            CompleteQuest(player, quest, npc);
        }

        return true;
    }
```

If `CountInInventory` and `TakeFromInventory` are currently private methods on `QuestSystem`, they stay — the context captures them as delegates. If they don't exist or have different names, find them in `QuestSystem.cs` and use whichever helper names already exist; inline them if needed.

- [ ] **Step 4: Build**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/ProgressionFramework.csproj
```

Expected: `Build succeeded` 0 errors.

- [ ] **Step 5: In-game smoke-test for delivery regression**

Build Seafarer (depends on the framework) and launch a dev world. Start Drake's tradeship quest (talk to Drake → accept). Deliver one birch board. Confirm:
1. The plank is consumed
2. Drake's "thanks" line appears
3. The quest's `progress` variable advances by 1
4. The quest does NOT immediately complete (298 needed)

This proves the delivery logic still works after the refactor. If anything regresses, `git diff` against HEAD~1 and fix before proceeding.

- [ ] **Step 6: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-progression-framework && \
git add ProgressionFramework/ProgressionFramework/Quests/Quest.cs \
        ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs \
        ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs && \
git commit -m "$(cat <<'EOF'
refactor(quests): dispatch TryDeliver through objective handler registry

Moves the current hardcoded delivery logic into BuiltInObjectiveHandlers.Delivery
(implementing IQuestObjectiveHandler.TryProgress). TryDeliver now looks up the
handler by objective.Type and delegates. Adds QuestObjective.Pattern and a
QuestProgressContext passed to handlers. Pure refactor — no behavior change
for the existing 'delivery' type.

Prepares the dispatch path for upcoming 'kill' objective handler.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Framework — `Kill` objective type + native kill tracking

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs` — add `BuiltInObjectiveHandlers.Kill`.
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs` — register `"kill"` type, build pattern registry at `AssetsFinalize`, subscribe `OnEntityDeath` at `StartServerSide`, increment counters on kill.

- [ ] **Step 1: Add the `Kill` handler**

In `QuestRewardHandler.cs`, inside `BuiltInObjectiveHandlers` (where `Delivery` lives), add alongside it:

```csharp
    /// <summary>Tracks kills of entities whose <c>Code.ToString()</c> matches
    /// the objective's <see cref="QuestObjective.Pattern"/>. On start, snapshots
    /// the global counter for the pattern as the baseline. On progress attempt,
    /// computes current - baseline and compares to Required; advances the
    /// progress counter on the objective tree by the delta and returns true
    /// if anything changed.</summary>
    public sealed class Kill : IQuestObjectiveHandler
    {
        public void OnObjectiveStarted(IServerPlayer player, Quest quest, QuestObjective objective)
        {
            if (string.IsNullOrEmpty(objective.Pattern)) return;

            var qs = player.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            int baseline = qs.GetKillCount(objective.Pattern);

            var objTree = qs.GetOrAddObjectiveTreePublic(player, quest, objective.Code);
            objTree.SetInt("baseline", baseline);
            objTree.SetInt("progress", 0);
        }

        public bool TryProgress(IServerPlayer player, Quest quest, QuestObjective objective, object eventData)
        {
            var ctx = (QuestProgressContext)eventData;
            if (string.IsNullOrEmpty(objective.Pattern)) return false;

            var qs = player.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            int baseline = ctx.ObjTree.GetInt("baseline");
            int current = qs.GetKillCount(objective.Pattern);
            int newProgress = Math.Max(0, current - baseline);

            int oldProgress = ctx.ObjTree.GetInt("progress");
            if (newProgress == oldProgress) return false;

            ctx.ObjTree.SetInt("progress", newProgress);
            ctx.Api.Logger.Notification("[Quest] Kill: {0} progress {1}/{2} (pattern {3})",
                player.PlayerName, newProgress, objective.Required, objective.Pattern);
            return true;
        }
    }
```

Note: the handler talks back to `QuestSystem` for `GetKillCount` and an objective-tree getter. Those public helpers are added in the next step.

- [ ] **Step 2: Add kill registry + counter helpers on `QuestSystem`**

In `QuestSystem.cs`, inside the class, add a private registry field and three helpers. Put the field near the other private fields at the top of the class, and the methods near `GetStatus`:

```csharp
    /// <summary>Patterns referenced by any loaded quest's kill objectives.
    /// Populated at AssetsFinalize; OnEntityDeath walks this set.</summary>
    private readonly HashSet<string> killPatterns = new();

    /// <summary>Returns the current global kill counter for a pattern
    /// (server-side, 0 if never incremented).</summary>
    public int GetKillCount(string pattern)
    {
        var varSys = api?.ModLoader.GetModSystem<VariablesModSystem>();
        if (varSys == null) return 0;
        string? value = varSys.GetVariable(EnumActivityVariableScope.Global, $"pf:killcount:{pattern}", null);
        return int.TryParse(value, out int n) ? n : 0;
    }

    /// <summary>Public wrapper so objective handlers can access the per-objective
    /// attribute tree without duplicating lookup code.</summary>
    public Vintagestory.API.Datastructures.ITreeAttribute GetOrAddObjectiveTreePublic(
        IServerPlayer player, Quest quest, string objectiveCode)
        => GetOrAddObjectiveTree(player, quest, objectiveCode);
```

- [ ] **Step 3: Register the `kill` handler type**

In `QuestSystem.cs` find where `Delivery` is registered (from Task 2 step 3). Add:

```csharp
        RegisterObjectiveType("delivery", new BuiltInObjectiveHandlers.Delivery());
        RegisterObjectiveType("kill", new BuiltInObjectiveHandlers.Kill());
```

- [ ] **Step 4: Build the pattern registry at `AssetsFinalize`**

In `QuestSystem.cs`, find `AssetsFinalize` (around line 172). After loaded quests are processed, add a loop that collects kill patterns:

```csharp
    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        // ... existing quest-load code remains unchanged above this line ...

        killPatterns.Clear();
        foreach (var quest in QuestsByCode.Values)
        {
            foreach (var obj in quest.Objectives)
            {
                if (obj.Type.Equals("kill", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(obj.Pattern))
                {
                    killPatterns.Add(obj.Pattern);
                }
            }
        }

        if (killPatterns.Count > 0)
        {
            api.Logger.Notification("[Quest] Registered {0} kill patterns: {1}",
                killPatterns.Count, string.Join(", ", killPatterns));
        }
    }
```

(If `AssetsFinalize` has logic after the quest-loading loop, place this new block at the end of the method. If `QuestsByCode` is the wrong collection name, use whatever dictionary/map stores loaded quests — grep for it nearby.)

- [ ] **Step 5: Subscribe `OnEntityDeath` at `StartServerSide`**

In `QuestSystem.cs`, find `StartServerSide` (around line 91). At the end of the method (before the closing `}`), add:

```csharp
        api.Event.OnEntityDeath += OnEntityDeath;
```

Then add the handler method as a private method on `QuestSystem`:

```csharp
    private void OnEntityDeath(Entity entity, DamageSource? damageSource)
    {
        if (entity == null || killPatterns.Count == 0) return;

        string code = entity.Code?.ToString() ?? "";
        if (string.IsNullOrEmpty(code)) return;

        var varSys = sapi?.ModLoader.GetModSystem<VariablesModSystem>();
        if (varSys == null) return;

        foreach (var pattern in killPatterns)
        {
            if (!Vintagestory.API.Util.WildcardUtil.Match(pattern, code)) continue;

            string varName = $"pf:killcount:{pattern}";
            string? currentStr = varSys.GetVariable(EnumActivityVariableScope.Global, varName, null);
            int current = int.TryParse(currentStr, out int n) ? n : 0;
            int next = current + 1;
            varSys.SetVariable(null, EnumActivityVariableScope.Global, varName, next.ToString());
        }
    }
```

If `VariablesModSystem.SetVariable` requires a non-null entity (check by grepping its signature in `vsapi/`), pass the dead entity in place of `null` on the `SetVariable` call. If the global scope set signature takes only `(scope, name, value)`, adjust accordingly. Inspect the method before copying the code above; the intent is: increment a global-scope variable regardless of any entity.

- [ ] **Step 6: Call `OnObjectiveStarted` for all objectives when a quest starts**

In `QuestSystem.cs`, find where `QuestStatus.Active` is set (grep for the assignment, probably in a `StartQuest` or `questStart` handler). Immediately after setting status to Active and creating objective trees, iterate the objectives and invoke handlers:

```csharp
        foreach (var obj in quest.Objectives)
        {
            if (objectiveHandlers.TryGetValue(obj.Type, out var handler))
            {
                handler.OnObjectiveStarted(player, quest, obj);
            }
        }
```

If the existing code already calls `OnObjectiveStarted` in a loop, confirm it; don't add a duplicate call. If `OnObjectiveStarted` is never called currently, add this loop.

- [ ] **Step 7: Build**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/ProgressionFramework/ProgressionFramework.csproj
```

Expected: `Build succeeded` 0 errors.

- [ ] **Step 8: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-progression-framework && \
git add ProgressionFramework/ProgressionFramework/Quests/Quest.cs \
        ProgressionFramework/ProgressionFramework/Quests/QuestRewardHandler.cs \
        ProgressionFramework/ProgressionFramework/Quests/QuestSystem.cs && \
git commit -m "$(cat <<'EOF'
feat(quests): add 'kill' objective type with native server-wide death tracking

- QuestObjective.Pattern: wildcard for matching Entity.Code.ToString(), e.g.
  "game:drifter-*".
- BuiltInObjectiveHandlers.Kill snapshots a per-pattern global counter at
  quest start (baseline) and computes progress = current - baseline.
- QuestSystem builds a pattern registry at AssetsFinalize and subscribes
  api.Event.OnEntityDeath once at server start; matched deaths increment
  global.pf:killcount:<pattern> via VariablesModSystem.
- All players with any active kill-objective against a pattern see the same
  counter — server-wide, doesn't matter who did the killing.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: Content — lang keys + three quest JSON files

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/lang/en.json`
- Create: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/quests/celeste-crimsonrose.json`
- Create: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/quests/celeste-rusthunter.json`
- Create: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/quests/celeste-bearhunter.json`

- [ ] **Step 1: Add six new quest title/description lang keys**

In `en.json`, alongside existing `quest-drake-*-title` keys, add:

```json
	"quest-celeste-crimsonrose-title": "The Crimson Rose",
	"quest-celeste-crimsonrose-desc": "Celeste's old ship went down with a sealed chest of her loot. Find the wreck site using the map she gave you and dig up the chest.",
	"quest-celeste-rusthunter-title": "Rust Hunter",
	"quest-celeste-rusthunter-desc": "Thin the herd of rust monsters (drifters) around the port. Celeste wants ten dead.",
	"quest-celeste-bearhunter-title": "Bear Hunter",
	"quest-celeste-bearhunter-desc": "Prove your hunting skills to Celeste by bringing her three different kinds of bear pelt — head and all.",
```

Place them near other Celeste-related keys or in alphabetical order, whichever the existing file follows.

- [ ] **Step 2: Create `celeste-crimsonrose.json`**

Write this exact content:

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
        {
          "type": "addSellingItem",
          "itemCode": "game:shovel-copper",
          "itemType": "item",
          "stacksize": 1,
          "stockAvg": 2,
          "stockVar": 1,
          "priceAvg": 5,
          "priceVar": 1
        }
      ]
    }
  ],
  "rewards": [
    {
      "type": "incrementEntityVariable",
      "variableName": "celeste-friendship",
      "amount": 1,
      "thresholdValue": 1,
      "thresholdVariable": "entity.celeste-friendly"
    },
    {
      "type": "incrementEntityVariable",
      "variableName": "rebuilding-tier",
      "variableScope": "global",
      "amount": 1,
      "thresholdValue": 4,
      "thresholdVariable": "global.rebuilding-complete"
    }
  ]
}
```

- [ ] **Step 3: Create `celeste-rusthunter.json`**

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
    {
      "type": "giveItem",
      "itemCode": "seafarer:cutlass-blackbronze",
      "itemType": "item",
      "stacksize": 1
    },
    {
      "type": "addSellingItem",
      "itemCode": "seafarer:cutlass-copper",
      "itemType": "item",
      "stacksize": 1,
      "stockAvg": 2,
      "stockVar": 1,
      "priceAvg": 8,
      "priceVar": 2
    },
    {
      "type": "addSellingItem",
      "itemCode": "seafarer:cutlass-blackbronze",
      "itemType": "item",
      "stacksize": 1,
      "stockAvg": 1,
      "stockVar": 1,
      "priceAvg": 14,
      "priceVar": 3
    },
    {
      "type": "incrementEntityVariable",
      "variableName": "celeste-friendship",
      "amount": 1,
      "thresholdValue": 1,
      "thresholdVariable": "entity.celeste-friendly"
    },
    {
      "type": "incrementEntityVariable",
      "variableName": "rebuilding-tier",
      "variableScope": "global",
      "amount": 1,
      "thresholdValue": 4,
      "thresholdVariable": "global.rebuilding-complete"
    }
  ]
}
```

- [ ] **Step 4: Create `celeste-bearhunter.json`**

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
    {
      "code": "polar",
      "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-polar-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }]
    },
    {
      "code": "brown",
      "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-brown-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }]
    },
    {
      "code": "black",
      "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-black-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }]
    },
    {
      "code": "panda",
      "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-panda-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }]
    },
    {
      "code": "sun",
      "type": "delivery",
      "items": [{ "item": "game:hide-pelt-bear-sun-complete", "quantity": 1 }],
      "required": 1,
      "rewards": [{ "type": "awardTrainingXP", "training": "bearhunter", "xp": 50 }]
    }
  ],
  "rewards": [
    {
      "type": "giveItem",
      "itemCode": "seafarer:arrow-barbed-copper",
      "itemType": "item",
      "stacksize": 24
    },
    {
      "type": "addSellingItem",
      "itemCode": "seafarer:arrow-barbed-copper",
      "itemType": "item",
      "stacksize": 4,
      "stockAvg": 12,
      "stockVar": 4,
      "priceAvg": 2,
      "priceVar": 0.5
    },
    {
      "type": "addSellingItem",
      "itemCode": "seafarer:trainingbook-bearhunter",
      "itemType": "item",
      "stacksize": 1,
      "stockAvg": 1,
      "stockVar": 0,
      "priceAvg": 10,
      "priceVar": 2
    },
    {
      "type": "incrementEntityVariable",
      "variableName": "celeste-friendship",
      "amount": 1,
      "thresholdValue": 1,
      "thresholdVariable": "entity.celeste-friendly"
    },
    {
      "type": "incrementEntityVariable",
      "variableName": "rebuilding-tier",
      "variableScope": "global",
      "amount": 1,
      "thresholdValue": 4,
      "thresholdVariable": "global.rebuilding-complete"
    }
  ]
}
```

- [ ] **Step 5: Run asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: only the known pre-existing `food.ef_protein`/`premiumfish` error.

- [ ] **Step 6: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/lang/en.json \
        Seafarer/Seafarer/assets/seafarer/config/quests/celeste-crimsonrose.json \
        Seafarer/Seafarer/assets/seafarer/config/quests/celeste-rusthunter.json \
        Seafarer/Seafarer/assets/seafarer/config/quests/celeste-bearhunter.json && \
git commit -m "$(cat <<'EOF'
feat(celeste): add QuestSystem quests for Crimson Rose, Rust Hunter, Bear Hunter

Three new quest JSONs + their title/description lang keys. Not yet wired
to the dialog — those changes come in follow-up commits so the dialog
refactor can be reviewed branch-by-branch.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Dialog — rewrite Crimson Rose branch

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json`

- [ ] **Step 1: Update the main-menu entries for Crimson Rose**

In `celeste.json`, find the two existing Crimson Rose menu entries (around lines 38–51, the `ask-crimsonrose` block). Replace them with three status-aware entries:

```json
                // Crimson Rose: not yet started
                {
                    "value": "seafarer:dialogue-celeste-ask-crimsonrose",
                    "jumpTo": "crimsonrose-intro",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isNotValue": "active" },
                        { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isNotValue": "completed" }
                    ]
                },
                // Crimson Rose: active
                {
                    "value": "seafarer:dialogue-celeste-ask-crimsonrose",
                    "jumpTo": "crimsonrose-inprogress",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isValue": "active" }
                    ]
                },
                // Crimson Rose: completed
                {
                    "value": "seafarer:dialogue-celeste-ask-crimsonrose",
                    "jumpTo": "crimsonrose-done",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isValue": "completed" }
                    ]
                },
```

- [ ] **Step 2: Update the rum-for-map and Pirate Tales gates**

Those entries (around lines 54–89) currently check `entity.crimsonrose-complete`. Replace each `{ "variable": "entity.crimsonrose-complete", "isValue": "true" }` condition with:

```json
                        { "variable": "player.quest-seafarer-celeste-crimsonrose-status", "isValue": "completed" }
```

Four total occurrences inside the two rum entries and two pirate-tales entries.

- [ ] **Step 3: Replace the `check-crimsonrose-started` component**

Find `check-crimsonrose-started` (currently checks `player.celeste-crimsonrose-started`). Since `crimsonrose-intro` now only fires when the quest hasn't started, we can delete this component and change `crimsonrose-intro`'s `jumpTo` to go straight to `crimsonrose-offer`.

Replace the block:

```json
        {
            "code": "crimsonrose-intro",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-crimsonrose-intro" }],
            "jumpTo": "check-crimsonrose-started"
        },
        {
            "code": "check-crimsonrose-started",
            "owner": "celeste",
            "type": "condition",
            "variable": "player.celeste-crimsonrose-started",
            "isValue": "true",
            "thenJumpTo": "crimsonrose-inprogress",
            "elseJumpTo": "crimsonrose-offer"
        },
```

with:

```json
        {
            "code": "crimsonrose-intro",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-crimsonrose-intro" }],
            "jumpTo": "crimsonrose-offer"
        },
```

- [ ] **Step 4: Update `crimsonrose-start` to fire `questStart` (keep the map giveaway)**

Find the existing `crimsonrose-start` component and replace it with:

```json
        {
            "code": "crimsonrose-start",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questStart",
            "triggerdata": { "code": "celeste-crimsonrose" },
            "text": [{ "value": "seafarer:dialogue-celeste-crimsonrose-start" }],
            "jumpTo": "crimsonrose-give-map"
        },
        {
            "code": "crimsonrose-give-map",
            "owner": "celeste",
            "trigger": "giveitemstack",
            "triggerdata": {
                "type": "item",
                "code": "seafarer:map-crimsonrose",
                "stacksize": 1
            },
            "jumpTo": "main"
        },
```

Rationale: `questStart` and `giveitemstack` are both triggers on this talk node, but dialog components only fire one trigger each. Split into two nodes — start the quest (talk + trigger), then hand over the map (second trigger-only node), then back to main.

- [ ] **Step 5: Update the delivery chain**

Find `crimsonrose-deliver`. Replace its body + the downstream cascade (currently `crimsonrose-deliver` → `crimsonrose-reward-shovel` → `crimsonrose-complete` → `crimsonrose-grant-friendship` → `increment-rebuilding-tier` → main) with:

```json
        {
            "code": "crimsonrose-deliver",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "celeste-crimsonrose", "objective": "chest" },
            "text": [{ "value": "seafarer:dialogue-celeste-crimsonrose-received" }],
            "jumpTo": "crimsonrose-complete-line"
        },
        {
            "code": "crimsonrose-complete-line",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-crimsonrose-complete" }],
            "jumpTo": "main"
        },
```

Delete the four obsolete components: `crimsonrose-reward-shovel`, `crimsonrose-complete` (the old setVariables+talk), and `crimsonrose-grant-friendship`. (The `crimsonrose-complete-line` above is a renamed replacement for the talk-only part of the old `crimsonrose-complete`.)

The shovel add, friendship increment, and rebuilding-tier increment all happen as quest rewards when `questDeliver` completes the objective. Dialog just shows the final text.

- [ ] **Step 6: Update `crimsonrose-inprogress` to use quest-status instead of the old started flag**

The `crimsonrose-inprogress` component's menu entries currently check inventory only. They don't check `player.celeste-crimsonrose-started` — that gate is in the main-menu path. No changes needed to `crimsonrose-inprogress` itself. Confirm by reading the component:

```bash
grep -A 20 '"code": "crimsonrose-inprogress"' /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json
```

Expected: menu has entries for `have-chest` (inventory condition), `remind-quest`, and `goodbye`. No `player.celeste-crimsonrose-started` reference. If a reference exists, remove that condition — quest-status gating is done at the main-menu level now.

- [ ] **Step 7: Run asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: only the pre-existing `food.ef_protein` error.

- [ ] **Step 8: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json && \
git commit -m "$(cat <<'EOF'
refactor(celeste): wire Crimson Rose through QuestSystem

Dialog fires questStart on accept and questDeliver on chest turn-in;
main-menu gates read player.quest-seafarer-celeste-crimsonrose-status
instead of entity.crimsonrose-complete. Shovel add, friendship bump, and
rebuilding-tier bump move to quest rewards. Rum-for-map and Pirate Tales
unlock gates likewise read the quest status.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: Dialog — rewrite Rust Hunter branch

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json`
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/lang/en.json` — update one status-report line.

- [ ] **Step 1: Update the main-menu entries for Rust Hunter**

Find the two existing Rust Hunter menu entries (around lines 92–106). Replace with three status-aware entries (note: all three retain the `entity.celeste-friendly = true` gate):

```json
                // Rust Hunter: not yet started
                {
                    "value": "seafarer:dialogue-celeste-ask-rusthunter",
                    "jumpTo": "rusthunter-intro",
                    "conditions": [
                        { "variable": "entity.celeste-friendly", "isValue": "true" },
                        { "variable": "player.quest-seafarer-celeste-rusthunter-status", "isNotValue": "active" },
                        { "variable": "player.quest-seafarer-celeste-rusthunter-status", "isNotValue": "completed" }
                    ]
                },
                // Rust Hunter: active
                {
                    "value": "seafarer:dialogue-celeste-ask-rusthunter",
                    "jumpTo": "rusthunter-inprogress",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-rusthunter-status", "isValue": "active" }
                    ]
                },
                // Rust Hunter: completed
                {
                    "value": "seafarer:dialogue-celeste-ask-rusthunter",
                    "jumpTo": "rusthunter-done",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-rusthunter-status", "isValue": "completed" }
                    ]
                },
```

- [ ] **Step 2: Rewrite the quest-flow components**

Find and replace all Rust Hunter quest components. The new chain is:

```json
        // =============================================================
        // Quest: Rust Hunter
        // =============================================================
        {
            "code": "rusthunter-intro",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-rusthunter-intro" }],
            "jumpTo": "rusthunter-offer"
        },
        {
            "code": "rusthunter-offer",
            "owner": "player",
            "type": "talk",
            "text": [
                { "value": "seafarer:dialogue-celeste-accept-rusthunter", "jumpTo": "rusthunter-start" },
                { "value": "seafarer:dialogue-morgan-not-yet", "jumpTo": "main" }
            ]
        },
        {
            "code": "rusthunter-start",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questStart",
            "triggerdata": { "code": "celeste-rusthunter" },
            "text": [{ "value": "seafarer:dialogue-celeste-rusthunter-start" }],
            "jumpTo": "main"
        },
        {
            "code": "rusthunter-inprogress",
            "owner": "player",
            "type": "talk",
            "text": [
                { "value": "seafarer:dialogue-celeste-rusthunter-status", "jumpTo": "rusthunter-check" },
                { "value": "seafarer:dialogue-goodbye", "jumpTo": "goodbye" }
            ]
        },
        {
            "code": "rusthunter-check",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "celeste-rusthunter", "objective": "kills" },
            "text": [{ "value": "seafarer:dialogue-celeste-rusthunter-progress" }],
            "jumpTo": "check-rusthunter-complete"
        },
        {
            "code": "check-rusthunter-complete",
            "owner": "celeste",
            "type": "condition",
            "variable": "player.quest-seafarer-celeste-rusthunter-status",
            "isValue": "completed",
            "thenJumpTo": "rusthunter-reward-line",
            "elseJumpTo": "rusthunter-status-line"
        },
        {
            "code": "rusthunter-status-line",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-rusthunter-statusreport" }],
            "jumpTo": "main"
        },
        {
            "code": "rusthunter-reward-line",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-rusthunter-reward" }],
            "jumpTo": "main"
        },
        {
            "code": "rusthunter-done",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-rusthunter-already-done" }],
            "jumpTo": "main"
        },
```

Delete the old Rust Hunter components: `check-rusthunter-started`, `rusthunter-statusreport` (replaced by `rusthunter-status-line`), `rusthunter-deliver`, `rusthunter-increment`, `check-rusthunter-done`, `rusthunter-progress-line`, `rusthunter-reward-intro`, `rusthunter-give-vengeance`, `rusthunter-add-copper-cutlass`, `rusthunter-add-bronze-cutlass`, `rusthunter-complete`. All of those responsibilities now live in `celeste-rusthunter.json` as quest rewards.

- [ ] **Step 3: Update the rusthunter-statusreport lang line**

In `en.json`, find `dialogue-celeste-rusthunter-statusreport`. Current reads something like `"That's {entity.rustkills} of 10 rust monsters. Keep at it."`. Replace with:

```json
	"dialogue-celeste-rusthunter-statusreport": "That's {player.quest-seafarer-celeste-rusthunter-kills-progress} of 10 rust monsters. Keep at it.",
```

(The framework mirrors each objective's `progress` to `<scope>.quest-<code>-<objective>-progress`; the prefix `player.` here matches the rust hunter quest's player scope.)

- [ ] **Step 4: Run asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: only the pre-existing `food.ef_protein` error.

- [ ] **Step 5: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json \
        Seafarer/Seafarer/assets/seafarer/lang/en.json && \
git commit -m "$(cat <<'EOF'
refactor(celeste): wire Rust Hunter through QuestSystem kill tracking

Dialog fires questStart on accept and questDeliver on progress check.
No more rot delivery — kills are tracked server-wide by the framework's
new native OnEntityDeath counter. Status line parameter switches from
entity.rustkills to the quest-progress mirror. Reward chain (cutlass
grant, shop adds, friendship, rebuilding-tier) moves to quest rewards.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: Dialog — rewrite Bear Hunter branch

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json`

- [ ] **Step 1: Update the main-menu entries for Bear Hunter**

Replace the two existing entries (around lines 109–123):

```json
                // Bear Hunter: not yet started
                {
                    "value": "seafarer:dialogue-celeste-ask-bearhunter",
                    "jumpTo": "bearhunter-intro",
                    "conditions": [
                        { "variable": "entity.celeste-friendly", "isValue": "true" },
                        { "variable": "player.quest-seafarer-celeste-bearhunter-status", "isNotValue": "active" },
                        { "variable": "player.quest-seafarer-celeste-bearhunter-status", "isNotValue": "completed" }
                    ]
                },
                // Bear Hunter: active
                {
                    "value": "seafarer:dialogue-celeste-ask-bearhunter",
                    "jumpTo": "bearhunter-inprogress",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-bearhunter-status", "isValue": "active" }
                    ]
                },
                // Bear Hunter: completed
                {
                    "value": "seafarer:dialogue-celeste-ask-bearhunter",
                    "jumpTo": "bearhunter-done",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-bearhunter-status", "isValue": "completed" }
                    ]
                },
```

- [ ] **Step 2: Rewrite the quest-flow components**

Replace the entire Bear Hunter section (from `bearhunter-intro` to `bearhunter-done` inclusive) with:

```json
        // =============================================================
        // Quest: Bear Hunter
        // =============================================================
        {
            "code": "bearhunter-intro",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-intro" }],
            "jumpTo": "bearhunter-offer"
        },
        {
            "code": "bearhunter-offer",
            "owner": "player",
            "type": "talk",
            "text": [
                { "value": "seafarer:dialogue-celeste-accept-bearhunter", "jumpTo": "bearhunter-start" },
                { "value": "seafarer:dialogue-morgan-not-yet", "jumpTo": "main" }
            ]
        },
        {
            "code": "bearhunter-start",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questStart",
            "triggerdata": { "code": "celeste-bearhunter" },
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-start" }],
            "jumpTo": "main"
        },
        {
            "code": "bearhunter-inprogress",
            "owner": "player",
            "type": "talk",
            "text": [
                {
                    "value": "seafarer:dialogue-celeste-deliver-pelt-polar",
                    "jumpTo": "bearhunter-deliver-polar",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-bearhunter-polar-status", "isNotValue": "completed" },
                        { "variable": "player.inventory", "isValue": "{type: 'item', code: 'game:hide-pelt-bear-polar-complete', stacksize: 1}" }
                    ]
                },
                {
                    "value": "seafarer:dialogue-celeste-deliver-pelt-brown",
                    "jumpTo": "bearhunter-deliver-brown",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-bearhunter-brown-status", "isNotValue": "completed" },
                        { "variable": "player.inventory", "isValue": "{type: 'item', code: 'game:hide-pelt-bear-brown-complete', stacksize: 1}" }
                    ]
                },
                {
                    "value": "seafarer:dialogue-celeste-deliver-pelt-black",
                    "jumpTo": "bearhunter-deliver-black",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-bearhunter-black-status", "isNotValue": "completed" },
                        { "variable": "player.inventory", "isValue": "{type: 'item', code: 'game:hide-pelt-bear-black-complete', stacksize: 1}" }
                    ]
                },
                {
                    "value": "seafarer:dialogue-celeste-deliver-pelt-panda",
                    "jumpTo": "bearhunter-deliver-panda",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-bearhunter-panda-status", "isNotValue": "completed" },
                        { "variable": "player.inventory", "isValue": "{type: 'item', code: 'game:hide-pelt-bear-panda-complete', stacksize: 1}" }
                    ]
                },
                {
                    "value": "seafarer:dialogue-celeste-deliver-pelt-sun",
                    "jumpTo": "bearhunter-deliver-sun",
                    "conditions": [
                        { "variable": "player.quest-seafarer-celeste-bearhunter-sun-status", "isNotValue": "completed" },
                        { "variable": "player.inventory", "isValue": "{type: 'item', code: 'game:hide-pelt-bear-sun-complete', stacksize: 1}" }
                    ]
                },
                { "value": "seafarer:dialogue-celeste-bearhunter-status", "jumpTo": "bearhunter-statusreport" },
                { "value": "seafarer:dialogue-goodbye", "jumpTo": "goodbye" }
            ]
        },
        {
            "code": "bearhunter-statusreport",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-statusreport" }],
            "jumpTo": "main"
        },
        {
            "code": "bearhunter-deliver-polar",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "celeste-bearhunter", "objective": "polar" },
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-pelt-thanks" }],
            "jumpTo": "check-bearhunter-complete"
        },
        {
            "code": "bearhunter-deliver-brown",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "celeste-bearhunter", "objective": "brown" },
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-pelt-thanks" }],
            "jumpTo": "check-bearhunter-complete"
        },
        {
            "code": "bearhunter-deliver-black",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "celeste-bearhunter", "objective": "black" },
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-pelt-thanks" }],
            "jumpTo": "check-bearhunter-complete"
        },
        {
            "code": "bearhunter-deliver-panda",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "celeste-bearhunter", "objective": "panda" },
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-pelt-thanks" }],
            "jumpTo": "check-bearhunter-complete"
        },
        {
            "code": "bearhunter-deliver-sun",
            "owner": "celeste",
            "type": "talk",
            "trigger": "questDeliver",
            "triggerdata": { "code": "celeste-bearhunter", "objective": "sun" },
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-pelt-thanks" }],
            "jumpTo": "check-bearhunter-complete"
        },
        {
            "code": "check-bearhunter-complete",
            "owner": "celeste",
            "type": "condition",
            "variable": "player.quest-seafarer-celeste-bearhunter-status",
            "isValue": "completed",
            "thenJumpTo": "bearhunter-reward-line",
            "elseJumpTo": "main"
        },
        {
            "code": "bearhunter-reward-line",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-reward" }],
            "jumpTo": "main"
        },
        {
            "code": "bearhunter-done",
            "owner": "celeste",
            "type": "talk",
            "text": [{ "value": "seafarer:dialogue-celeste-bearhunter-already-done" }],
            "jumpTo": "main"
        },
```

All previous inline increment / set-flag / reward-chaining components for Bear Hunter are removed (`check-bearhunter-started`, the 5 old `bearhunter-deliver-*` with their per-pelt setVariables, `bearhunter-increment`, `bearhunter-award-xp`, `bearhunter-pelt-thanks` as a separate node, `check-bearhunter-done`, `bearhunter-reward-intro`, `bearhunter-give-arrows`, `bearhunter-add-arrows`, `bearhunter-add-book`, `bearhunter-complete`).

- [ ] **Step 3: Run asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: only the pre-existing `food.ef_protein` error.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json && \
git commit -m "$(cat <<'EOF'
refactor(celeste): wire Bear Hunter through QuestSystem

Five per-pelt delivery objectives under one quest; requiredObjectiveCount=3
in the quest JSON completes it after any three. Dialog fires questDeliver
per pelt with the pelt's objective code; per-type visibility switches from
entity.bearhunter-<type>-delivered to the mirrored quest-objective status.
XP-per-pelt and the final reward bundle (arrows, training book, friendship,
rebuilding-tier) all move to quest rewards.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Dialog — delete orphan rebuilding-tier component

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json`

- [ ] **Step 1: Remove the `increment-rebuilding-tier` component**

The rebuilding-tier increment now fires from each Celeste quest's rewards array (via the framework's `incrementEntityVariable` with `variableScope: global`). The inline dialog component is orphaned.

Find and delete the component (at the bottom of the file, around the last `// === Rebuilding Tier ===` comment). Ensure the preceding component's trailing comma is handled correctly to keep valid JSON.

Before:
```json
        // === Rebuilding Tier ===
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
    ]
}
```

After (component removed, array closes after the preceding component):
```json
    ]
}
```

Check that the component immediately before the deleted block ends cleanly — no dangling comma.

- [ ] **Step 2: Pirate Tales friendship increment stays unchanged**

Confirm `piratetales-grant-friendship` is still present. Pirate Tales is NOT a QuestSystem quest and its friendship bump is the only code path that increments via that component. If it's been accidentally deleted in prior tasks, re-add it (it should still be there — prior tasks didn't touch Pirate Tales).

```bash
grep -c "piratetales-grant-friendship" /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json
```

Expected: 1 match.

- [ ] **Step 3: Run asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: only the pre-existing `food.ef_protein` error.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/config/dialogue/celeste.json && \
git commit -m "$(cat <<'EOF'
chore(celeste): remove orphan inline rebuilding-tier dialog component

All three ported Celeste quests fire the rebuilding-tier increment via
their quest rewards now. The inline dialog component is dead code.
Pirate Tales' inline friendship increment stays because Pirate Tales is
not a QuestSystem quest.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Update `celeste.md` design doc

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/quests/celeste.md`

The design doc is the source of truth. Update it so variable names match what the code now writes and reads.

- [ ] **Step 1: Replace the Quest Variables table**

Find the `## Quest Variables` section. Replace its table with:

```markdown
| Variable | Scope | Purpose |
|----------|-------|---------|
| `player.hasmetceleste` | player | First meeting flag |
| `player.quest-seafarer-celeste-crimsonrose-status` | player | Crimson Rose quest status (active / completed) |
| `player.quest-seafarer-celeste-rusthunter-status`  | player | Rust Hunter quest status |
| `player.quest-seafarer-celeste-rusthunter-kills-progress` | player | Rust Hunter progress counter (kills since quest start) |
| `player.quest-seafarer-celeste-rusthunter-kills-baseline` | player | Server kill counter value when the quest started |
| `player.quest-seafarer-celeste-bearhunter-status`  | player | Bear Hunter quest status |
| `player.quest-seafarer-celeste-bearhunter-<type>-status` | player | Per-pelt objective status (type: polar/brown/black/panda/sun) |
| `entity.celeste-friendship` | entity | Friendship counter on Celeste (0..N) |
| `entity.celeste-friendly`   | entity | Boolean gate: friendship ≥ 1 |
| `entity.celeste-piratetales-told` | entity | Pirate Tales shared (one-shot) |
| `global.pf:killcount:game:drifter-*` | global | Server-wide drifter kill counter (framework-managed) |
| `global.rebuilding-tier` | global | Shared Tortuga rebuilding counter |
| `global.rebuilding-complete` | global | Flag: rebuilding counter reached 4 |
```

- [ ] **Step 2: Update the Rust Hunter section's kill-tracking note**

Replace the existing `> **Kill-tracking note:**` block with:

```markdown
> **Kill tracking:** the `kill` objective type in ProgressionFramework
> subscribes to `OnEntityDeath` server-side and increments
> `global.pf:killcount:game:drifter-*` for every drifter that dies. When
> the quest starts, the current counter value is snapshotted as the
> player's baseline. Progress = current - baseline. All players with an
> active Rust Hunter quest share the same counter, but each has their own
> baseline, so kills only count forward from whenever each player started.
```

- [ ] **Step 3: Update the Bear Hunter per-pelt flag references**

Find the "Per delivery:" section. Replace its second bullet `Increment entity.bearhunter-count` with `Quest-objective progress advances by 1 (per-type objective completes)`. Replace any reference to `entity.bearhunter-<type>-delivered` flags with `player.quest-seafarer-celeste-bearhunter-<type>-status`.

- [ ] **Step 4: Update the Completion sections' note about entity flags**

Find each of the three "Sets:" bullets (`Sets: entity.crimsonrose-complete = true`, rusthunter, bearhunter). Replace with:

```markdown
*Completion:* quest-system marks `player.quest-seafarer-celeste-<code>-status = "completed"` and fires the quest rewards listed below.
```

(Replace `<code>` with the actual quest code in each section.)

- [ ] **Step 5: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/quests/celeste.md && \
git commit -m "$(cat <<'EOF'
docs(celeste): update design doc to reflect QuestSystem variables

Replaces old entity.<quest>-complete and entity.bearhunter-<type>-delivered
references with the framework-mirrored player.quest-* variables. Adds the
new global.pf:killcount variable and rewrites the Rust Hunter kill-tracking
note to describe the framework-native mechanism.

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Final build + validator + in-game smoke checklist

- [ ] **Step 1: Build Seafarer**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 2: Run asset validator**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```

Expected: exit 1 but the only error should be the known pre-existing `food.ef_protein` on `premiumfish`.

- [ ] **Step 3: In-game smoke test**

Launch a dev world with both mods loaded. Run through:

1. Fresh world. Meet Celeste. Main menu: only Crimson Rose + standard options appear (no Rust Hunter, no Bear Hunter, no rum-for-map, no Pirate Tales).
2. Accept Crimson Rose → quest appears in Quest Log as `active`. Map given. Talking to Celeste again: "how's it going" branch only.
3. Get the chest (console spawn a `seafarer:sealed-chest` for speed), return, deliver. Objective completes, quest rewards fire: shovel appears in shop, friendship flag + rebuilding-tier flag set, +1 global counter. Rust Hunter and Bear Hunter options now visible.
4. Start Rust Hunter. Kill 5 drifters (console spawn + kill). Talk to Celeste → status shows "5 of 10". Kill 5 more. Talk → quest completes. Blackbronze cutlass given, shop expanded.
5. Start Bear Hunter. Give yourself 5 different bear pelts. Deliver polar → 50 XP, objective completes, still in progress. Deliver brown → 100 XP. Deliver black → 150 XP AND the quest completes (3 of 5). Panda and sun options no longer shown in the menu.
6. Check Quest Log — all three quests showing `completed`. Friendship counter = 3, `entity.celeste-friendly` = true.
7. Kill another drifter. Confirm the `global.pf:killcount:game:drifter-*` counter increments even though no quest is tracking it (that's fine — the registry keeps the pattern permanent). Start a fresh alt with Rust Hunter — baseline captures the accumulated total; new kills count forward normally.

If any step fails, back out to the nearest passing commit and fix before marking the task complete.

---

## Self-review notes

- Spec coverage: Framework additions → Tasks 1 (RequiredObjectiveCount), 2 (handler dispatch + Pattern field), 3 (kill handler + event subscription). Three quest JSONs → Task 4. Dialog rewrite → Tasks 5 (Crimson Rose), 6 (Rust Hunter), 7 (Bear Hunter). Orphan component cleanup → Task 8. Doc update → Task 9. Build + playtest → Task 10. All spec sections covered.
- No placeholders: every JSON file content is fully written out; every C# snippet is complete; every lang key has concrete text.
- Type consistency: `BuiltInObjectiveHandlers.Delivery` / `BuiltInObjectiveHandlers.Kill` used consistently in Tasks 2–3. `QuestProgressContext` used consistently. `Pattern` field on `QuestObjective` referenced the same way everywhere. Quest codes (`celeste-crimsonrose`, `celeste-rusthunter`, `celeste-bearhunter`) match across Tasks 4–7.
