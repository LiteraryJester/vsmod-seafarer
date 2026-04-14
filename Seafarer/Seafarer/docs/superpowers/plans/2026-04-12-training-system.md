# Training & Profession System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a config-driven training/profession system where players earn XP through crafting, books, and NPC dialogue to unlock recipes and earn titles.

**Architecture:** JSON configs define professions/trainings/XP sources. Server-side `TrainingSystem` ModSystem handles XP awards and level-ups by appending trait codes to the player's `extraTraits` WatchedAttribute (which the base game already checks for recipe gating). A client-side GUI dialog shows progress. The existing `EntityEvolvingTrader.Dialog_DialogTriggers` handles the `awardTrainingXP` dialogue trigger. A Harmony postfix on `GridRecipe.ConsumeInput` awards crafting XP.

**Tech Stack:** C# (.NET 10), Vintage Story API, Harmony, JSON5 configs

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `SaltAndSand/Training/TrainingData.cs` | Create | Data classes: `Profession`, `Training`, `TrainingLevel`, `TrainingXpSource`, `TrainingConfig` |
| `SaltAndSand/Training/TrainingSystem.cs` | Create | ModSystem: loads configs, awards XP, manages level-ups, syncs config to clients |
| `SaltAndSand/Training/GuiDialogTrainingLedger.cs` | Create | Client GUI dialog with profession/training tree and XP bars |
| `SaltAndSand/EntityEvolvingTrader.cs` | Modify | Add `awardTrainingXP` case to `Dialog_DialogTriggers` |
| `assets/seafarer/config/professions.json` | Create | Profession/training/level definitions |
| `assets/seafarer/config/training-xp.json` | Create | XP source mappings (craft, book, dialogue) |
| `assets/seafarer/config/training-config.json` | Create | Server settings (showAllTrainings, showXpNumbers) |
| `assets/seafarer/lang/en.json` | Modify | Add profession/training/notification lang keys |

---

### Task 1: Data Classes

**Files:**
- Create: `SaltAndSand/Training/TrainingData.cs`

- [ ] **Step 1: Create the data classes file**

```csharp
namespace SaltAndSand.Training;

public class TrainingConfig
{
    public bool ShowAllTrainings { get; set; } = false;
    public bool ShowXpNumbers { get; set; } = true;
}

public class Profession
{
    public string Code { get; set; } = "";
    public string Color { get; set; } = "#C8B691";
    public List<Training> Trainings { get; set; } = new();
}

public class Training
{
    public string Code { get; set; } = "";
    public List<TrainingLevel> Levels { get; set; } = new();
}

public class TrainingLevel
{
    public int Level { get; set; }
    public float Xp { get; set; }
    public string Title { get; set; } = "";
    public string Trait { get; set; } = "";
    public string? Color { get; set; }
}

public class TrainingXpSource
{
    public string Type { get; set; } = "";
    public string Training { get; set; } = "";
    public string? Recipe { get; set; }
    public string? Item { get; set; }
    public string? Trigger { get; set; }
    public float Xp { get; set; }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build SaltAndSand/SaltAndSand.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SaltAndSand/Training/TrainingData.cs
git commit -m "feat: add training system data classes"
```

---

### Task 2: JSON Config Files

**Files:**
- Create: `assets/seafarer/config/professions.json`
- Create: `assets/seafarer/config/training-xp.json`
- Create: `assets/seafarer/config/training-config.json`

- [ ] **Step 1: Create professions.json**

Create `SaltAndSand/assets/seafarer/config/professions.json`:

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

- [ ] **Step 2: Create training-xp.json**

Create `SaltAndSand/assets/seafarer/config/training-xp.json`:

```json5
[
    {
        type: "craft",
        training: "piemaster",
        recipe: "game:pie-*",
        xp: 1
    },
    {
        type: "book",
        training: "shipwright",
        item: "seafarer:trainingbook-shipwright",
        xp: 100
    },
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

- [ ] **Step 3: Create training-config.json**

Create `SaltAndSand/assets/seafarer/config/training-config.json`:

```json5
{
    showAllTrainings: false,
    showXpNumbers: true
}
```

- [ ] **Step 4: Run asset validation**

Run: `python3 validate-assets.py 2>&1 | grep -E "(ERROR|Passed|Warnings|Errors)"`
Expected: No new errors

- [ ] **Step 5: Commit**

```bash
git add SaltAndSand/assets/seafarer/config/professions.json SaltAndSand/assets/seafarer/config/training-xp.json SaltAndSand/assets/seafarer/config/training-config.json
git commit -m "feat: add training system config files"
```

---

### Task 3: TrainingSystem ModSystem (Server Side)

**Files:**
- Create: `SaltAndSand/Training/TrainingSystem.cs`

- [ ] **Step 1: Create the core TrainingSystem**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace SaltAndSand.Training;

public class TrainingSystem : ModSystem
{
    public const string TrainingTreeKey = "seafarer-training";

    public List<Profession> Professions { get; private set; } = new();
    public List<TrainingXpSource> XpSources { get; private set; } = new();
    public TrainingConfig Config { get; private set; } = new();

    public Dictionary<string, Training> TrainingsByCode { get; private set; } = new();
    public Dictionary<string, Profession> ProfessionByTrainingCode { get; private set; } = new();

    private ICoreServerAPI? sapi;

    public override bool ShouldLoad(EnumAppSide side) => true;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        LoadConfigs(api);
    }

    private void LoadConfigs(ICoreAPI api)
    {
        // Load professions
        var professionsAsset = api.Assets.TryGet(
            new AssetLocation("seafarer:config/professions.json"));
        if (professionsAsset != null)
        {
            try
            {
                var token = JToken.Parse(professionsAsset.ToText());
                Professions = token.ToObject<List<Profession>>() ?? new();
            }
            catch (Exception e)
            {
                api.Logger.Error("[Training] Failed to load professions.json: {0}", e.Message);
            }
        }

        // Load XP sources
        var xpAsset = api.Assets.TryGet(
            new AssetLocation("seafarer:config/training-xp.json"));
        if (xpAsset != null)
        {
            try
            {
                var token = JToken.Parse(xpAsset.ToText());
                XpSources = token.ToObject<List<TrainingXpSource>>() ?? new();
            }
            catch (Exception e)
            {
                api.Logger.Error("[Training] Failed to load training-xp.json: {0}", e.Message);
            }
        }

        // Load config
        var configAsset = api.Assets.TryGet(
            new AssetLocation("seafarer:config/training-config.json"));
        if (configAsset != null)
        {
            try
            {
                var token = JToken.Parse(configAsset.ToText());
                Config = token.ToObject<TrainingConfig>() ?? new();
            }
            catch (Exception e)
            {
                api.Logger.Error("[Training] Failed to load training-config.json: {0}", e.Message);
            }
        }

        // Build indexes
        TrainingsByCode.Clear();
        ProfessionByTrainingCode.Clear();
        foreach (var prof in Professions)
        {
            foreach (var training in prof.Trainings)
            {
                TrainingsByCode[training.Code] = training;
                ProfessionByTrainingCode[training.Code] = prof;
            }
        }

        api.Logger.Notification("[Training] Loaded {0} professions, {1} trainings, {2} XP sources",
            Professions.Count, TrainingsByCode.Count, XpSources.Count);
    }

    /// <summary>
    /// Award XP to a player for a specific training. Handles level-ups and trait grants.
    /// Call from server side only.
    /// </summary>
    public void AwardXP(IServerPlayer player, string trainingCode, float xp)
    {
        if (!TrainingsByCode.TryGetValue(trainingCode, out var training))
        {
            sapi?.Logger.Warning("[Training] Unknown training code: {0}", trainingCode);
            return;
        }

        var tree = player.Entity.WatchedAttributes
            .GetOrAddTreeAttribute(TrainingTreeKey);

        float currentXp = tree.GetFloat(trainingCode + "-xp", 0);
        int currentLevel = tree.GetInt(trainingCode + "-level", 0);

        currentXp += xp;
        tree.SetFloat(trainingCode + "-xp", currentXp);

        // Check for level-ups
        while (currentLevel < training.Levels.Count)
        {
            var nextLevel = training.Levels[currentLevel];
            if (currentXp < nextLevel.Xp) break;

            currentLevel++;
            tree.SetInt(trainingCode + "-level", currentLevel);

            // Grant trait for recipe unlocks
            AddExtraTrait(player, nextLevel.Trait);

            // Notify player
            string title = Lang.Get("seafarer:training-title-" + nextLevel.Trait,
                nextLevel.Title);
            sapi?.SendMessage(player, 0,
                Lang.Get("seafarer:training-levelup", title),
                EnumChatType.Notification);

            sapi?.Logger.Notification("[Training] {0} reached {1} level {2} ({3})",
                player.PlayerName, trainingCode, currentLevel, nextLevel.Title);
        }

        player.Entity.WatchedAttributes.MarkPathDirty(TrainingTreeKey);
    }

    private void AddExtraTrait(IServerPlayer player, string trait)
    {
        var existing = player.Entity.WatchedAttributes
            .GetStringArray("extraTraits") ?? Array.Empty<string>();

        if (existing.Contains(trait)) return;

        var updated = new string[existing.Length + 1];
        existing.CopyTo(updated, 0);
        updated[existing.Length] = trait;
        player.Entity.WatchedAttributes.SetStringArray("extraTraits", updated);
        player.Entity.WatchedAttributes.MarkPathDirty("extraTraits");
    }

    /// <summary>
    /// Find XP sources matching a crafted recipe output code.
    /// Supports wildcard patterns like "game:pie-*".
    /// </summary>
    public IEnumerable<TrainingXpSource> GetCraftXpSources(string outputCode)
    {
        foreach (var source in XpSources)
        {
            if (source.Type != "craft" || source.Recipe == null) continue;

            if (source.Recipe.Contains('*'))
            {
                string pattern = "^" + Regex.Escape(source.Recipe).Replace("\\*", ".*") + "$";
                if (Regex.IsMatch(outputCode, pattern, RegexOptions.IgnoreCase))
                    yield return source;
            }
            else if (source.Recipe.Equals(outputCode, StringComparison.OrdinalIgnoreCase))
            {
                yield return source;
            }
        }
    }

    /// <summary>
    /// Find XP sources matching a dialogue trigger name.
    /// </summary>
    public TrainingXpSource? GetDialogueXpSource(string triggerName)
    {
        return XpSources.FirstOrDefault(s =>
            s.Type == "dialogue" && s.Trigger == triggerName);
    }

    /// <summary>
    /// Find XP sources matching a book item code.
    /// </summary>
    public TrainingXpSource? GetBookXpSource(string itemCode)
    {
        return XpSources.FirstOrDefault(s =>
            s.Type == "book" && s.Item == itemCode);
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build SaltAndSand/SaltAndSand.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SaltAndSand/Training/TrainingSystem.cs
git commit -m "feat: add TrainingSystem ModSystem with XP awards and level-ups"
```

---

### Task 4: Dialogue Trigger Integration

**Files:**
- Modify: `SaltAndSand/EntityEvolvingTrader.cs` (line 55-73, the switch block)

- [ ] **Step 1: Add awardTrainingXP case to EntityEvolvingTrader**

Add these lines inside the `switch (value)` block in `Dialog_DialogTriggers`, after the `decrementVariable` case:

```csharp
            case "awardTrainingXP":
                return HandleAwardTrainingXP(triggeringEntity, data);
```

Then add this method to the class:

```csharp
    private int HandleAwardTrainingXP(EntityAgent triggeringEntity, JsonObject data)
    {
        if (World.Side != EnumAppSide.Server) return -1;
        if (triggeringEntity is not EntityPlayer eplr) return -1;

        var player = Api.World.PlayerByUid(eplr.PlayerUID) as IServerPlayer;
        if (player == null) return -1;

        string? training = data["training"]?.AsString();
        float xp = data["xp"]?.AsFloat(0) ?? 0;
        if (string.IsNullOrEmpty(training) || xp <= 0) return -1;

        var system = Api.ModLoader.GetModSystem<Training.TrainingSystem>();
        system?.AwardXP(player, training, xp);

        return -1;
    }
```

- [ ] **Step 2: Verify it compiles**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build SaltAndSand/SaltAndSand.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SaltAndSand/EntityEvolvingTrader.cs
git commit -m "feat: add awardTrainingXP dialogue trigger to EntityEvolvingTrader"
```

---

### Task 5: Crafting XP via Harmony Patch

**Files:**
- Create: `SaltAndSand/Training/CraftingXpPatch.cs`

- [ ] **Step 1: Create the Harmony patch**

This postfix runs after `GridRecipe.ConsumeInput` succeeds (returns true), awarding XP to the player.

```csharp
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace SaltAndSand.Training;

[HarmonyPatch(typeof(GridRecipe), nameof(GridRecipe.ConsumeInput))]
public static class CraftingXpPatch
{
    [HarmonyPostfix]
    public static void Postfix(GridRecipe __instance, IPlayer byPlayer, bool __result)
    {
        // Only award XP on successful crafting, server-side only
        if (!__result) return;
        if (byPlayer?.Entity?.Api?.Side != EnumAppSide.Server) return;

        var outputCode = __instance.Output?.Code?.ToString();
        if (string.IsNullOrEmpty(outputCode)) return;

        var system = byPlayer.Entity.Api.ModLoader.GetModSystem<TrainingSystem>();
        if (system == null) return;

        var serverPlayer = byPlayer as IServerPlayer;
        if (serverPlayer == null) return;

        foreach (var source in system.GetCraftXpSources(outputCode))
        {
            system.AwardXP(serverPlayer, source.Training, source.Xp);
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build SaltAndSand/SaltAndSand.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SaltAndSand/Training/CraftingXpPatch.cs
git commit -m "feat: add Harmony patch for crafting XP awards"
```

---

### Task 6: Training Ledger GUI

**Files:**
- Create: `SaltAndSand/Training/GuiDialogTrainingLedger.cs`

- [ ] **Step 1: Create the GUI dialog**

```csharp
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace SaltAndSand.Training;

public class GuiDialogTrainingLedger : GuiDialog
{
    private readonly TrainingSystem system;
    private readonly Dictionary<string, float[]> barValues = new();

    public override string ToggleKeyCombinationCode => "seafarertrainingledger";
    public override bool PrefersUngrabbedMouse => false;

    public GuiDialogTrainingLedger(ICoreClientAPI capi, TrainingSystem system) : base(capi)
    {
        this.system = system;
    }

    public override void OnOwnPlayerDataReceived()
    {
        base.OnOwnPlayerDataReceived();
        capi.World.Player.Entity.WatchedAttributes
            .RegisterModifiedListener(TrainingSystem.TrainingTreeKey, OnTrainingChanged);
    }

    private void OnTrainingChanged()
    {
        if (IsOpened()) ComposeDialog();
    }

    public override bool TryOpen(bool withFocus = true)
    {
        ComposeDialog();
        return base.TryOpen(withFocus);
    }

    private void ComposeDialog()
    {
        barValues.Clear();

        double width = 420;
        double rowH = 22;
        double barH = 10;
        double sectionGap = 12;

        bool showAll = system.Config.ShowAllTrainings;
        bool showNumbers = system.Config.ShowXpNumbers;

        // Calculate total height
        double contentHeight = 0;
        foreach (var prof in system.Professions)
        {
            if (!showAll && !HasAnyProgress(prof)) continue;
            contentHeight += 28; // header
            foreach (var training in prof.Trainings)
            {
                if (!showAll && GetTrainingXp(training.Code) <= 0
                    && GetTrainingLevel(training.Code) <= 0) continue;
                contentHeight += rowH + barH + 8;
            }
            contentHeight += sectionGap;
        }

        if (contentHeight < 50) contentHeight = 50;

        var bgBounds = ElementBounds.Fill
            .WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle)
            .WithFixedAlignmentOffset(0, 0);

        var innerBounds = ElementBounds.Fixed(0, 0, width, contentHeight);

        var composer = capi.Gui.CreateCompo("seafarertrainingledger", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("seafarer:training-ledger-title"), () => TryClose())
            .BeginChildElements(bgBounds);

        double y = 0;

        foreach (var prof in system.Professions)
        {
            if (!showAll && !HasAnyProgress(prof)) continue;

            // Profession header
            var profColor = HexToDoubles(prof.Color);
            var headerFont = CairoFont.WhiteSmallishText().WithColor(profColor);
            var headerBounds = ElementBounds.Fixed(0, y, width, 24);
            string profName = Lang.Get("seafarer:profession-" + prof.Code);
            composer.AddStaticText(profName, headerFont, headerBounds);
            y += 28;

            foreach (var training in prof.Trainings)
            {
                float xp = GetTrainingXp(training.Code);
                int level = GetTrainingLevel(training.Code);

                if (!showAll && xp <= 0 && level <= 0) continue;

                var currentLevel = level > 0 ? training.Levels[level - 1] : null;
                var nextLevel = level < training.Levels.Count ? training.Levels[level] : null;
                bool completed = level >= training.Levels.Count;
                float maxXp = nextLevel?.Xp ?? currentLevel?.Xp ?? 100;

                // Training name line
                string icon = completed ? "\u2605" : "\u25CB"; // star or circle
                string trainingName = Lang.Get("seafarer:training-" + training.Code);
                string title = currentLevel?.Title ?? "";
                string iconColor = completed ? "#D4AF37" : "#808080";
                string titleColor = currentLevel?.Color ?? (completed ? "#7ACC7A" : prof.Color);

                string richText = $"<font color=\"{iconColor}\">{icon}</font>  {trainingName}";
                if (!string.IsNullOrEmpty(title))
                {
                    richText += $"  <font color=\"{titleColor}\">- {title}</font>";
                }

                var nameBounds = ElementBounds.Fixed(12, y, width - 24, rowH);
                var nameFont = CairoFont.WhiteSmallText();
                composer.AddRichtext(richText, nameFont, nameBounds);
                y += rowH;

                // XP bar
                var barBounds = ElementBounds.Fixed(12, y, width - 24, barH);
                string barKey = "bar-" + training.Code;
                composer.AddStatbar(barBounds, profColor, barKey);

                // XP text overlay
                if (showNumbers)
                {
                    string xpText = completed
                        ? Lang.Get("seafarer:training-complete")
                        : $"{(int)xp} / {(int)maxXp}";
                    var xpFont = CairoFont.WhiteDetailText().WithFontSize(10);
                    var xpBounds = ElementBounds.Fixed(12, y - 1, width - 24, barH + 2);
                    composer.AddStaticText(xpText, xpFont, EnumTextOrientation.Center, xpBounds);
                }

                barValues[barKey] = new float[] { xp, 0, maxXp };
                y += barH + 8;
            }

            y += sectionGap;
        }

        composer.EndChildElements();
        SingleComposer = composer.Compose();

        // Set bar values after composition
        foreach (var kv in barValues)
        {
            var bar = SingleComposer.GetStatbar(kv.Key);
            if (bar != null)
            {
                bar.SetValues(kv.Value[0], kv.Value[1], kv.Value[2]);
            }
        }
    }

    private float GetTrainingXp(string code)
    {
        var tree = capi.World.Player.Entity.WatchedAttributes
            .GetTreeAttribute(TrainingSystem.TrainingTreeKey);
        return tree?.GetFloat(code + "-xp", 0) ?? 0;
    }

    private int GetTrainingLevel(string code)
    {
        var tree = capi.World.Player.Entity.WatchedAttributes
            .GetTreeAttribute(TrainingSystem.TrainingTreeKey);
        return tree?.GetInt(code + "-level", 0) ?? 0;
    }

    private bool HasAnyProgress(Profession prof)
    {
        foreach (var training in prof.Trainings)
        {
            if (GetTrainingXp(training.Code) > 0) return true;
            if (GetTrainingLevel(training.Code) > 0) return true;
        }
        return false;
    }

    private static double[] HexToDoubles(string hex)
    {
        if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7)
            return new double[] { 0.78, 0.73, 0.57, 1.0 }; // default parchment

        int r = Convert.ToInt32(hex.Substring(1, 2), 16);
        int g = Convert.ToInt32(hex.Substring(3, 2), 16);
        int b = Convert.ToInt32(hex.Substring(5, 2), 16);
        return new double[] { r / 255.0, g / 255.0, b / 255.0, 1.0 };
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build SaltAndSand/SaltAndSand.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SaltAndSand/Training/GuiDialogTrainingLedger.cs
git commit -m "feat: add Training Ledger GUI dialog"
```

---

### Task 7: Client-Side Registration

**Files:**
- Modify: `SaltAndSand/SaltAndSandModSystem.cs`

- [ ] **Step 1: Register hotkey and dialog in StartClientSide**

Add at the top of the file:

```csharp
using SaltAndSand.Training;
```

Add a field to the class:

```csharp
        private GuiDialogTrainingLedger? trainingDialog;
```

Add the following lines inside `StartClientSide`, after the existing code:

```csharp
            api.Input.RegisterHotKey("seafarertrainingledger",
                Lang.Get("seafarer:training-ledger-title"), GlKeys.L,
                HotkeyType.GUIOrOtherControls);

            var trainingSystem = api.ModLoader.GetModSystem<TrainingSystem>();
            trainingDialog = new GuiDialogTrainingLedger(api, trainingSystem);
```

- [ ] **Step 2: Verify it compiles**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build SaltAndSand/SaltAndSand.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SaltAndSand/SaltAndSandModSystem.cs
git commit -m "feat: register Training Ledger hotkey and dialog"
```

---

### Task 8: Lang Entries

**Files:**
- Modify: `assets/seafarer/lang/en.json`

- [ ] **Step 1: Add training system lang entries**

Add these entries to `en.json` (after the existing training-related dialogue entries):

```json
"training-ledger-title": "Training Ledger",
"training-levelup": "Training complete! You are now: {0}",
"training-xp-gained": "+{0} XP ({1})",
"training-complete": "Complete",

"profession-carpentry": "Carpentry",
"profession-cooking": "Cooking",

"training-shipwright": "Shipwright",
"training-brewer": "Brewer",
"training-piemaster": "Pie Master",

"training-title-shipwright": "Apprentice Shipwright",
"training-title-brewer": "Brewer",
"training-title-piemaster": "Pie Master",
```

- [ ] **Step 2: Run asset validation**

Run: `python3 validate-assets.py 2>&1 | grep -E "(ERROR|Passed|Warnings|Errors)"`
Expected: No new errors

- [ ] **Step 3: Commit**

```bash
git add SaltAndSand/assets/seafarer/lang/en.json
git commit -m "feat: add training system lang entries"
```

---

### Task 9: Integration Smoke Test

- [ ] **Step 1: Full build**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build SaltAndSand/SaltAndSand.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 2: Run asset validation**

Run: `python3 validate-assets.py 2>&1 | grep -E "(ERROR|Passed|Warnings|Errors)"`
Expected: No new errors from training system files

- [ ] **Step 3: Verify file structure**

Run: `find SaltAndSand/Training -name "*.cs" | sort && find SaltAndSand/assets/seafarer/config -name "training*" -o -name "profession*" | sort`

Expected output:
```
SaltAndSand/Training/CraftingXpPatch.cs
SaltAndSand/Training/GuiDialogTrainingLedger.cs
SaltAndSand/Training/TrainingData.cs
SaltAndSand/Training/TrainingSystem.cs
SaltAndSand/assets/seafarer/config/professions.json
SaltAndSand/assets/seafarer/config/training-config.json
SaltAndSand/assets/seafarer/config/training-xp.json
```
