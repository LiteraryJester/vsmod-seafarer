# Training & Profession System Design

## Overview

A config-driven training system where players unlock recipes and earn titles by gaining XP in specific trainings. Trainings are grouped under parent professions. XP is earned through crafting, reading books, or NPC dialogue.

---

## Data Model

```
Profession (e.g. Carpentry)
 ├── Training: Shipwright
 │    ├── Level 1: "Apprentice Shipwright" (100 XP)
 │    │    └── Unlocks: marine-varnish, oiled-canvas, waxed-canvas, canvas-sail
 │    ├── Level 2: "Shipwright" (300 XP)
 │    │    └── Unlocks: outrigger-schematic
 │    └── Level 3: "Master Shipwright" (800 XP)
 │         └── Unlocks: cutter-schematic
 │
 ├── Training: Woodworker
 │    └── Level 1: "Woodworker" (50 XP)
 │         └── Unlocks: seasoned-wood-rack
 │
 Profession (e.g. Cooking)
 ├── Training: Brewer
 │    └── Level 1: "Brewer" (75 XP)
 │         └── Unlocks: rum-distillation, fermented-coconut-water
```

---

## Config Format

### `config/professions.json`

Defines the profession tree. Each training has levels with XP thresholds, optional titles, and trait codes that gate recipes.

```json5
[
    {
        code: "carpentry",
        trainings: [
            {
                code: "shipwright",
                levels: [
                    {
                        level: 1,
                        xp: 100,
                        title: "Apprentice Shipwright",
                        trait: "shipwright"
                    }
                ]
            }
        ]
    },
    {
        code: "cooking",
        trainings: [
            {
                code: "brewer",
                levels: [
                    {
                        level: 1,
                        xp: 100,
                        title: "Brewer",
                        trait: "brewer"
                    }
                ]
            },
             {
                code: "piemaster",
                levels: [
                    {
                        level: 1,
                        xp: 100,
                        title: "Pie master",
                        trait: "piemaster"
                    }
                ]
            }
        ]
    }
]
```

### `config/training-xp.json`

Defines XP sources. Recipes, items, and dialogue triggers that award XP to a specific training.

```json5
[
    // Crafting XP - awarded when recipe output is created
    {
        type: "craft",
        training: "piemaster",
        recipe: "game:pie-*",
        xp: 1
    },

    // Book XP - awarded when a training book is consumed/read
    {
        type: "book",
        training: "shipwright",
        item: "seafarer:trainingbook-shipwright",
        xp: 100
    },

    // Dialogue XP - awarded via dialogue trigger
    {
        type: "dialogue",
        training: "shipwright",
        trigger: "drake-varnish-lesson",
        xp: 50
    },
    {
        type: "dialogue",
        training: "shipwright",
        trigger: "drake-canvas-lesson",
        xp: 50
    }
]
```

---

## How It Integrates With Vintage Story

### Recipe Gating (built-in mechanism)

Recipes use the existing `requiresTrait` field. When a player reaches a training level, its trait code is added to the player's `extraTraits` array on `WatchedAttributes`. The base game's recipe system already checks this.

```json5
// recipes/grid/marine-varnish.json
{
    ingredientPattern: "RP",
    ingredients: {
        "R": { type: "item", code: "game:resin" },
        "P": { type: "item", code: "game:fat" }
    },
    output: { type: "item", code: "seafarer:marine-varnish" },
    requiresTrait: "shipwright"
}
```

No patching needed - the base game `Character.cs` already does:
```csharp
if (charclass.Traits.Contains(recipe.RequiresTrait)) return true;
string[] extraTraits = player.Entity.WatchedAttributes.GetStringArray("extraTraits");
if (extraTraits != null && extraTraits.Contains(recipe.RequiresTrait)) return true;
```

### Data Storage (WatchedAttributes - auto synced)

```
WatchedAttributes
└── "seafarer-training"          (TreeAttribute)
    ├── "shipwright-xp"          (float) = 150.0
    ├── "shipwright-level"       (int)   = 1
    ├── "woodworker-xp"         (float) = 0.0
    ├── "woodworker-level"      (int)   = 0
    └── ...
```

`extraTraits` array (existing VS mechanism):
```
["shipwright"]  // Added when shipwright level 1 reached
```

### XP From Crafting

Hook into `EventMatchesGridRecipe` or the recipe completion event. When a recipe is crafted that matches a training-xp entry, award XP.

```csharp
// In ModSystem.StartServerSide()
api.Event.OnPlayerInteractEntity += CheckDialogueXP;

// Hook recipe completion
harmony.Patch(typeof(GridRecipe).GetMethod("ConsumeInput"),
    postfix: new HarmonyMethod(typeof(TrainingSystem), nameof(OnRecipeCrafted)));
```

### XP From Dialogue

New dialogue trigger type `awardTrainingXP`:
```json5
{
    code: "drake-varnish-complete",
    owner: "drake",
    trigger: "awardTrainingXP",
    triggerdata: {
        training: "shipwright",
        xp: 50
    },
    jumpTo: "next-node"
}
```

This requires registering a custom dialogue trigger in the mod system.

### XP From Books

Training books are items with a `"trainingBook"` attribute. On right-click/consume, they award XP and are consumed.

```json5
// itemtypes/lore/trainingbook-shipwright.json
{
    code: "trainingbook-shipwright",
    class: "ItemBook",
    maxstacksize: 1,
    attributes: {
        readable: true,
        trainingBook: {
            training: "shipwright",
            xp: 50,
            consumeOnRead: true,
            requiredLevel: 0
        }
    }
}
```

---

## C# Implementation

### Data Classes

```csharp
public class TrainingConfig
{
    public bool ShowAllTrainings = false;
    public bool ShowXpNumbers = true;
}

public class Profession
{
    public string Code;
    public string Color;                // Hex color for UI, e.g. "#8B5E3C"
    public List<Training> Trainings;
}

public class Training
{
    public string Code;
    public List<TrainingLevel> Levels;
}

public class TrainingLevel
{
    public int Level;
    public float Xp;
    public string Title;
    public string Trait;
    public string Color;                // Optional override for title color
}

public class TrainingXpSource
{
    public string Type;      // "craft", "book", "dialogue"
    public string Training;
    public string Recipe;    // for craft type (supports wildcards)
    public string Item;      // for book type
    public string Trigger;   // for dialogue type
    public float Xp;
}
```

### Core System

```csharp
public class TrainingSystem : ModSystem
{
    List<Profession> professions;
    List<TrainingXpSource> xpSources;
    Dictionary<string, Training> trainingsByCode;

    public override void StartServerSide(ICoreServerAPI api)
    {
        // Load configs
        professions = api.Assets.Get<List<Profession>>("seafarer:config/professions.json");
        xpSources = api.Assets.Get<List<TrainingXpSource>>("seafarer:config/training-xp.json");

        // Index trainings
        trainingsByCode = professions
            .SelectMany(p => p.Trainings)
            .ToDictionary(t => t.Code);

        // Register dialogue trigger
        api.RegisterDialogueTrigger("awardTrainingXP", OnDialogueAwardXP);

        // Hook crafting events
        api.Event.DidUseBlock += OnCraftComplete;  // or Harmony patch
    }

    public void AwardXP(IServerPlayer player, string trainingCode, float xp)
    {
        var training = trainingsByCode[trainingCode];
        var tree = player.Entity.WatchedAttributes
            .GetOrAddTreeAttribute("seafarer-training");

        float currentXp = tree.GetFloat(trainingCode + "-xp", 0);
        int currentLevel = tree.GetInt(trainingCode + "-level", 0);

        currentXp += xp;
        tree.SetFloat(trainingCode + "-xp", currentXp);

        // Check for level ups
        while (currentLevel < training.Levels.Count)
        {
            var nextLevel = training.Levels[currentLevel];
            if (currentXp < nextLevel.Xp) break;

            currentLevel++;
            tree.SetInt(trainingCode + "-level", currentLevel);

            // Grant trait for recipe unlocks
            AddExtraTrait(player, nextLevel.Trait);

            // Notify player
            var title = Lang.Get("seafarer:training-title-" + nextLevel.Trait);
            api.SendMessage(player, 0,
                Lang.Get("seafarer:training-levelup", title), EnumChatType.Notification);
        }

        player.Entity.WatchedAttributes.MarkPathDirty("seafarer-training");
    }

    private void AddExtraTrait(IServerPlayer player, string trait)
    {
        var existing = player.Entity.WatchedAttributes
            .GetStringArray("extraTraits") ?? Array.Empty<string>();

        if (!existing.Contains(trait))
        {
            player.Entity.WatchedAttributes.SetStringArray("extraTraits",
                existing.Append(trait).ToArray());

            // Re-apply trait attributes so stats update
            // (calls the base game Character system)
        }
    }
}
```

### Dialogue Trigger

```csharp
private void OnDialogueAwardXP(Entity entity, string triggerName, JsonObject data)
{
    if (entity is EntityPlayer eplr)
    {
        var player = api.World.PlayerByUid(eplr.PlayerUID) as IServerPlayer;
        string training = data["training"].AsString();
        float xp = data["xp"].AsFloat();
        AwardXP(player, training, xp);
    }
}
```

---

## Titles

Titles are optional display strings per level. Stored in the lang file and shown in:
- Chat notification on level up
- Player tooltip/nameplate (if desired)
- Handbook/training GUI

```json5
// lang/en.json
"training-title-shipwright": "Apprentice Shipwright",
"training-levelup": "Training complete! You are now a {0}.",
"training-xp-gained": "+{0} XP ({1})"
```

---

## Drake Quest Integration

Drake's "Tricks of the Trade" quest from Tortuga.md maps directly:

| Quest Step | XP Source | Training | XP |
|-----------|-----------|----------|-----|
| Complete varnish lesson | dialogue | shipwright | 50 |
| Complete oiled canvas lesson | dialogue | shipwright | 50 |

**Level 1 (100 XP)** unlocks after completing both lessons (50+50) or reading the apprentice shipwright book.
This gates: marine-varnish, oiled-canvas, waxed-canvas, canvas-sail recipes.


---

## File Structure

```
SaltAndSand/assets/seafarer/
├── config/
│   ├── professions.json          # Profession + training definitions
│   └── training-xp.json          # XP source mappings
├── itemtypes/
│   └── lore/
│       └── trainingbook-shipwright.json
├── recipes/grid/
│   ├── marine-varnish.json       # requiresTrait: "shipwright"
│   ├── oiled-canvas.json         # requiresTrait: "shipwright"
│   └── canvas-sail.json          # requiresTrait: "shipwright"
│   └── oiled-canvas-sail.json          # requiresTrait: "shipwright"
│   └── waxed-canvas-sail.json          # requiresTrait: "shipwright"
└── lang/
    └── en.json                   # Title and notification strings
```

```
SaltAndSand/
├── Systems/
│   └── Training/
│       ├── TrainingSystem.cs           # Main mod system (server + client entry)
│       ├── TrainingData.cs             # Data classes (Profession, Training, TrainingConfig)
│       ├── TrainingBookBehavior.cs     # Book item behavior for XP on read
│       └── GuiDialogTrainingLedger.cs  # Training Ledger GUI dialog
```

---

## Server Config

### `config/training-config.json`

```json5
{
    // Show all trainings in GUI, or only those with XP > 0
    showAllTrainings: false,

    // Show XP numbers or just the bar
    showXpNumbers: true
}
```

Loaded server-side in `StartServerSide()`, synced to clients via a network packet on join. The client reads this to decide what to render.

---

## GUI Design

### Overview

A single dialog toggled with a hotkey (default: `L` for Ledger). Lists professions as collapsible headers with their trainings underneath. Each training shows level, title, and XP progress.

### Layout

```
┌─ Training Ledger ────────────────────────────────────────┐
│                                                           │
│  ▼ Carpentry                                              │
│  ┌──────────────────────────────────────────────────────┐ │
│  │  ★ Shipwright                  Apprentice Shipwright  │ │
│  │  ████████████████████████████████████████░░░░  100%   │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                           │
│  ▼ Cooking                                                │
│  ┌──────────────────────────────────────────────────────┐ │
│  │  ○ Brewer                                             │ │
│  │  ████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  30/100  │ │
│  ├──────────────────────────────────────────────────────┤ │
│  │  ○ Pie Master                                         │ │
│  │  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░   0/100  │ │
│  └──────────────────────────────────────────────────────┘ │
│                                                           │
└──────────────────────────────────────────────────────────┘
```

When `showAllTrainings: false`, professions with zero XP across all trainings are hidden entirely. Trainings within a visible profession that have zero XP are still shown (the profession header being visible means at least one training has progress).

When `showAllTrainings: true`, everything is shown regardless of XP.

### Color System

Colors are defined per profession in the config. The profession header text and the XP bar fill use this color.

```json5
{
    code: "carpentry",
    color: "#8B5E3C",
    trainings: [...]
}
```

Training levels can override with their own color for the title text:

```json5
{
    level: 1,
    title: "Apprentice Shipwright",
    color: "#7ACC7A",
    trait: "shipwright"
}
```

If no level color is set, it inherits from the profession.

**Default palette if no color specified:**
- Profession headers: `#C8B691` (warm parchment)
- Completed levels: `#7ACC7A` (green)
- In-progress levels: `#C8B691` (parchment)
- Locked/no progress: `#808080` (gray)

### Visual States

| State | Icon | Title Color | Bar |
|-------|------|-------------|-----|
| Completed (max level) | ★ gold | Level color or green | Full, profession color |
| In progress | ○ white | Profession color | Partial fill, profession color |
| Not started (visible) | ○ gray | Gray | Empty |

### Hotkey Registration

```csharp
public override void StartClientSide(ICoreClientAPI capi)
{
    capi.Input.RegisterHotKey("trainingledger", "Training Ledger", GlKeys.L,
        HotkeyType.GUIOrOtherControls);

    trainingDialog = new GuiDialogTrainingLedger(capi);
}
```

### C# Dialog Class

```csharp
public class GuiDialogTrainingLedger : GuiDialog
{
    public override string ToggleKeyCombinationCode => "trainingledger";

    public GuiDialogTrainingLedger(ICoreClientAPI capi) : base(capi) { }

    private void ComposeDialog()
    {
        var bgBounds = ElementStdBounds.DialogBackground();
        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        float yOffset = 0;

        var composer = capi.Gui.CreateCompo("trainingledger", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar("Training Ledger", OnTitleBarClose)
            .BeginChildElements(bgBounds);

        foreach (var profession in professions)
        {
            if (!showAll && !HasAnyProgress(profession)) continue;

            // Profession header (clickable to collapse)
            var headerBounds = ElementBounds.Fixed(0, yOffset, 400, 25);
            var profColor = ColorUtil.Hex2Doubles(profession.Color ?? "#C8B691");
            var headerFont = CairoFont.WhiteSmallText().WithColor(profColor);

            composer.AddStaticText("▼ " + Lang.Get("seafarer:profession-" + profession.Code),
                headerFont, headerBounds);
            yOffset += 30;

            // Inset panel for trainings in this profession
            float panelStart = yOffset;
            foreach (var training in profession.Trainings)
            {
                var tree = GetTrainingTree();
                float xp = tree.GetFloat(training.Code + "-xp", 0);
                int level = tree.GetInt(training.Code + "-level", 0);

                if (!showAll && xp <= 0 && level <= 0) continue;

                // Current level info
                var currentLevel = level > 0 ? training.Levels[level - 1] : null;
                var nextLevel = level < training.Levels.Count ? training.Levels[level] : null;
                float maxXp = nextLevel?.Xp ?? currentLevel?.Xp ?? 100;
                bool completed = level >= training.Levels.Count;

                // Icon + training name + title
                string icon = completed ? "★" : "○";
                string title = currentLevel?.Title ?? "";
                string displayName = Lang.Get("seafarer:training-" + training.Code);

                var nameBounds = ElementBounds.Fixed(10, yOffset, 390, 20);
                var nameFont = CairoFont.WhiteSmallText();

                if (completed)
                {
                    string levelColor = currentLevel?.Color ?? "#7ACC7A";
                    composer.AddRichtext(
                        $"<font color=\"#D4AF37\">{icon}</font> " +
                        $"<font>{displayName}</font>" +
                        $"<hfill />" +
                        $"<font color=\"{levelColor}\">{title}</font>",
                        nameFont, nameBounds);
                }
                else
                {
                    composer.AddRichtext(
                        $"<font color=\"#808080\">{icon}</font> {displayName}" +
                        (title != "" ? $"<hfill /><font color=\"{profession.Color ?? "#C8B691"}\">{title}</font>" : ""),
                        nameFont, nameBounds);
                }
                yOffset += 22;

                // XP progress bar
                var barBounds = ElementBounds.Fixed(10, yOffset, 380, 8);
                string barKey = "bar-" + training.Code;
                composer.AddStatbar(barBounds, profColor, barKey);
                yOffset += 18;

                // Store bar ref to set value after compose
                barValues[barKey] = new float[] { xp, maxXp };
            }

            yOffset += 5;
        }

        composer.EndChildElements();
        SingleComposer = composer.Compose();

        // Set bar values after composition
        foreach (var kv in barValues)
        {
            SingleComposer.GetStatbar(kv.Key)
                .SetValues(kv.Value[0], 0, kv.Value[1]);
        }
    }

    private ITreeAttribute GetTrainingTree()
    {
        return capi.World.Player.Entity.WatchedAttributes
            .GetOrAddTreeAttribute("seafarer-training");
    }

    private bool HasAnyProgress(Profession profession)
    {
        var tree = GetTrainingTree();
        foreach (var training in profession.Trainings)
        {
            if (tree.GetFloat(training.Code + "-xp", 0) > 0) return true;
            if (tree.GetInt(training.Code + "-level", 0) > 0) return true;
        }
        return false;
    }
}
```

### Recompose on XP Change

The dialog listens for changes to the `"seafarer-training"` WatchedAttributes path and recomposes when XP changes:

```csharp
public override void OnOwnPlayerDataReceived()
{
    capi.World.Player.Entity.WatchedAttributes
        .RegisterModifiedListener("seafarer-training", OnTrainingChanged);
}

private void OnTrainingChanged()
{
    if (IsOpened()) ComposeDialog();
}
```

---

## Updated Config Format With Colors

### `config/professions.json`

```json5
[
    {
        code: "carpentry",
        color: "#8B5E3C",
        trainings: [
            {
                code: "shipwright",
                levels: [
                    {
                        level: 1,
                        xp: 100,
                        title: "Apprentice Shipwright",
                        trait: "shipwright"
                    }
                ]
            }
        ]
    },
    {
        code: "cooking",
        color: "#C47D2E",
        trainings: [
            {
                code: "brewer",
                levels: [
                    {
                        level: 1,
                        xp: 100,
                        title: "Brewer",
                        trait: "brewer"
                    }
                ]
            },
            {
                code: "piemaster",
                levels: [
                    {
                        level: 1,
                        xp: 100,
                        title: "Pie Master",
                        trait: "piemaster"
                    }
                ]
            }
        ]
    }
]
```

---

## Design Decisions

1. **`extraTraits` for recipe gating** - zero patches needed. The base game already checks this array. We just append trait codes when levels are reached.

2. **`WatchedAttributes` for storage** - auto-syncs client/server, persists with savegame, no separate save files. All under a `"seafarer-training"` subtree.

3. **Cumulative XP (not per-level)** - XP thresholds are total accumulated, not reset per level. Simpler to track and display.

4. **Wildcard recipe matching** - `"recipe": "game:plank-*"` lets you award XP for families of recipes without listing each variant.

5. **Trait codes as level identifiers** - `shipwright` (level 1), `shipwright-2` (level 2), `shipwright-3` (level 3). Each level's trait is additive, so a level 2 player has both `shipwright` and `shipwright-2` traits.

6. **No XP loss on death** - keeps it progression-only. Can be added later if wanted.

7. **Config-driven** - all professions, trainings, XP sources defined in JSON. No recompilation to add new professions or adjust balance.

8. **Server-controlled visibility** - `showAllTrainings` config lets server operators choose whether players see all possible trainings (discovery) or only those they've started (clean UI). Synced to clients on join.

9. **Color system** - profession color for headers and XP bars, optional per-level color override for titles. Defaults to parchment/green/gray if unset. Keeps UI readable without requiring every config entry to specify colors.

10. **Recompose on change** - GUI listens to WatchedAttributes path changes and auto-updates. No polling, no manual refresh needed.

7. **Config-driven** - all professions, trainings, XP sources defined in JSON. No recompilation to add new professions or adjust balance.
