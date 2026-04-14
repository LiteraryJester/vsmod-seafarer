# Seafarer Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Seafarer from `vsmod-salt-and-sand` to a fresh `vsmod-seafarer` repo, rename `SaltAndSand` → `Seafarer` throughout, reorganize C# files into VSSurvivalMod-style type folders, dedupe the `itemtypes/resource/` vs `resources/` typo, and update cross-repo references — with zero behavior changes.

**Architecture:** Copy source tree from old repo into new repo's existing `Seafarer/Seafarer/` project folder (overwriting the template stub), rename csproj + ModSystem, `sed`-rebase the namespace in every `.cs` file, `git mv` files into type folders (`Block/`, `BlockEntity/`, `CollectibleBehavior/`, `Entity/`, `EntityBehavior/`, `Item/`, `Recipe/`, `Config/`), merge `itemtypes/resources/seeds.json` into `itemtypes/resource/`, update CLAUDE.md files and forwarding notes, and verify with `dotnet build` + Python asset validators + manual in-game smoke test.

**Tech Stack:** Vintage Story modding API, .NET 10.0, Harmony, Newtonsoft.Json, `dotnet build`, Python 3 validators.

**Two repos involved:**
- Source: `/mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/` (to be retired, kept as archive)
- Target: `/mnt/d/Development/vs/vsmod-seafarer/` on branch `0.5.0` (currently has spec commit + LICENSE + scaffolding)

**Third repo referenced (no changes needed to its code):**
- `/mnt/d/Development/vs/vsmod-progression-framework/` — only its `CLAUDE.md` gets a tiny update (Task 13)

**Spec:** `Seafarer/docs/superpowers/specs/2026-04-14-seafarer-migration-design.md`

**Prerequisite env:**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
```

Set this once at the top of your session.

---

## Task 1: Wipe stale template and copy source tree

**Files:**
- Delete: `Seafarer/Seafarer/*` inside target repo (template stub — overwritten in Step 2)
- Create: `Seafarer/Seafarer/*` populated from salt-and-sand

- [ ] **Step 1: Inspect current target state**

```bash
ls /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/
```

Expect the stub to contain roughly: `Properties/`, `Seafarer.csproj`, `SeafarerModSystem.cs`, `assets/`, `modinfo.json` (and stale `bin/`, `obj/`). None of these are worth preserving — they're template defaults.

- [ ] **Step 2: Wipe the stub project folder**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
rm -rf Properties Seafarer.csproj SeafarerModSystem.cs assets modinfo.json bin obj
ls    # should be empty
```

- [ ] **Step 3: Copy salt-and-sand project tree in**

Copy everything except build artifacts:

```bash
cd /mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/SaltAndSand
rsync -av --exclude='bin/' --exclude='obj/' --exclude='.vs/' ./ /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/
```

If `rsync` isn't available, use `cp -r` with post-cleanup:

```bash
cp -r /mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/SaltAndSand/. /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/
rm -rf /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/bin \
       /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/obj
```

Verify:

```bash
ls /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/
# Expect: Properties/, SaltAndSand.csproj, SaltAndSandModSystem.cs, assets/,
# docs/ (if docs was inside SaltAndSand/), modinfo.json, quests/, and ~35 .cs files at root
```

- [ ] **Step 4: Commit the raw copy**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer
git commit -m "Copy Seafarer source tree from vsmod-salt-and-sand (pre-rename)"
```

---

## Task 2: Copy sibling artifacts (validators, root docs)

**Files:**
- Create: `vsmod-seafarer/validate-assets.py`
- Create: `vsmod-seafarer/validate-food.py`
- Create: `vsmod-seafarer/validate-dialogue.py`
- Create: `vsmod-seafarer/vs_validators/` (directory)
- Create: `vsmod-seafarer/requirements-dev.txt`
- Create: `vsmod-seafarer/Seafarer/CLAUDE.md` (from salt-and-sand's)
- Create: `vsmod-seafarer/Seafarer/docs/` (from salt-and-sand's, already partially copied in Task 1 if docs was nested under SaltAndSand/)

- [ ] **Step 1: Copy Python validators to repo root**

```bash
cd /mnt/d/Development/vs/vsmod-salt-and-sand
cp validate-assets.py validate-food.py validate-dialogue.py requirements-dev.txt /mnt/d/Development/vs/vsmod-seafarer/
cp -r vs_validators /mnt/d/Development/vs/vsmod-seafarer/
rm -rf /mnt/d/Development/vs/vsmod-seafarer/vs_validators/__pycache__ 2>/dev/null || true
```

- [ ] **Step 2: Copy solution-level CLAUDE.md and docs**

If `/mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/docs/` exists after Task 1, move it up one level to solution root:

```bash
if [ -d /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/docs ]; then
    mv /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/docs /mnt/d/Development/vs/vsmod-seafarer/Seafarer/docs-from-project
fi
```

Copy the solution-level CLAUDE.md from salt-and-sand:

```bash
cp /mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/CLAUDE.md \
   /mnt/d/Development/vs/vsmod-seafarer/Seafarer/CLAUDE.md
```

If `/mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/FEATURES.md` exists, copy it too:

```bash
[ -f /mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/FEATURES.md ] && \
    cp /mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/FEATURES.md \
       /mnt/d/Development/vs/vsmod-seafarer/Seafarer/FEATURES.md
```

- [ ] **Step 3: Merge docs if both exist**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer
if [ -d docs-from-project ] && [ ! -d docs ]; then
    mv docs-from-project docs
fi
# docs/ already contains the migration spec/plan from earlier work;
# preserve those by using cp -nr (no-clobber recursive) if there's a collision:
if [ -d docs-from-project ]; then
    cp -nr docs-from-project/. docs/
    rm -rf docs-from-project
fi
```

- [ ] **Step 4: Commit sibling artifacts**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add validate-assets.py validate-food.py validate-dialogue.py \
        requirements-dev.txt vs_validators Seafarer/CLAUDE.md
# FEATURES.md only if present
[ -f Seafarer/FEATURES.md ] && git add Seafarer/FEATURES.md
# docs additions
git add Seafarer/docs
git commit -m "Copy validators, solution-level CLAUDE.md, and docs from salt-and-sand"
```

---

## Task 3: Rename project files (csproj + ModSystem)

**Files:**
- Rename: `Seafarer/Seafarer/SaltAndSand.csproj` → `Seafarer/Seafarer/Seafarer.csproj`
- Rename: `Seafarer/Seafarer/SaltAndSandModSystem.cs` → `Seafarer/Seafarer/SeafarerModSystem.cs`

- [ ] **Step 1: Rename via git mv**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv SaltAndSand.csproj Seafarer.csproj
git mv SaltAndSandModSystem.cs SeafarerModSystem.cs
```

- [ ] **Step 2: Commit the rename**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add -A
git commit -m "Rename project: SaltAndSand.csproj + ModSystem → Seafarer"
```

At this point the code won't compile — class name `SaltAndSandModSystem` inside `SeafarerModSystem.cs` and `namespace SaltAndSand` in every file are still stale. Task 4 fixes it.

---

## Task 4: Rebase namespace SaltAndSand → Seafarer

**Files:**
- Modify: every `.cs` file under `Seafarer/Seafarer/` that contains `SaltAndSand`.

- [ ] **Step 1: Run namespace sed on all C# files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
# The \b word boundary is important so "SaltAndSand" doesn't match substrings.
# This rewrites: namespace SaltAndSand; namespace SaltAndSand.X; using SaltAndSand.X;
#                SaltAndSand.Y.Z references; class SaltAndSandModSystem; and the
#                "SaltAndSandConfig.json" const value stays unless we exclude it.
#
# The config filename constant ("SaltAndSandConfig.json") is modid-neutral and
# user-facing — DO NOT rewrite it. So we exclude that string first via a
# targeted temporary marker, then restore.
find . -name '*.cs' -print0 | xargs -0 sed -i 's|"SaltAndSandConfig\.json"|@@KEEPCONFIGNAME@@|g'
find . -name '*.cs' -print0 | xargs -0 sed -i 's|\bSaltAndSand\b|Seafarer|g'
find . -name '*.cs' -print0 | xargs -0 sed -i 's|@@KEEPCONFIGNAME@@|"SaltAndSandConfig.json"|g'
```

Note: the `"SaltAndSandConfig.json"` exclusion is deliberate — renaming that string would invalidate any existing on-disk config files users have saved. Keep it pinned.

- [ ] **Step 2: Verify no unintended SaltAndSand references remain**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
grep -rn "SaltAndSand" .
```

Expected hits:
- `"SaltAndSandConfig.json"` — the preserved config file constant (should appear in `SeafarerModSystem.cs`)
- Nothing else.

If there are other hits, investigate and rewrite them with `sed` or manually.

- [ ] **Step 3: Build**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer
dotnet build Seafarer/Seafarer.csproj
```

Expected: ProjectReference error — the csproj still points at the framework csproj with an absolute path identical to salt-and-sand's, which still works. What matters is that namespace rebase compiles. If you see errors like `namespace 'SaltAndSand' not found`, your sed missed a file.

Expected result: 0 errors. (ProjectReference to framework is inherited verbatim from salt-and-sand's csproj; that's still correct.)

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add -A
git commit -m "Rebase namespace SaltAndSand → Seafarer across all C# files"
```

---

## Task 5: Reorganize C# files into type folders

**Files:**
- Move 35+ `.cs` files from `Seafarer/Seafarer/*.cs` into type subfolders.

- [ ] **Step 1: Create the type folders**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
mkdir -p Block BlockEntity CollectibleBehavior Entity EntityBehavior Item Recipe Config
```

- [ ] **Step 2: Move Block/* files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv BlockAmphora.cs Block/
git mv BlockAmphoraStorage.cs Block/
git mv BlockBurrito.cs Block/
git mv BlockDryingFrame.cs Block/
git mv BlockGriddle.cs Block/
git mv BlockGriddleHearth.cs Block/
git mv BlockGriddleHearthBase.cs Block/
git mv BlockOpenCoconut.cs Block/
git mv BlockPrepTable.cs Block/
git mv BlockSaltPan.cs Block/
```

- [ ] **Step 3: Move BlockEntity/* files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv BlockEntityAmphoraStorage.cs BlockEntity/
git mv BlockEntityBurrito.cs BlockEntity/
git mv BlockEntityDryingFrame.cs BlockEntity/
git mv BlockEntityGriddleHearth.cs BlockEntity/
git mv BlockEntityOpenCoconut.cs BlockEntity/
git mv BlockEntityPrepTable.cs BlockEntity/
git mv BlockEntityPrepTableSlot.cs BlockEntity/
git mv BlockEntitySaltPan.cs BlockEntity/
```

- [ ] **Step 4: Move CollectibleBehavior/* files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv BehaviorClamShuck.cs CollectibleBehavior/
git mv BehaviorCoconutCrack.cs CollectibleBehavior/
git mv BehaviorPlaceBurrito.cs CollectibleBehavior/
git mv BehaviorShellCrush.cs CollectibleBehavior/
```

- [ ] **Step 5: Move Entity/ and EntityBehavior/ files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv EntityProjectileBarbed.cs Entity/
git mv EntityBehaviorExposure.cs EntityBehavior/
git mv ExposureCondition.cs EntityBehavior/
git mv ExposureConfig.cs EntityBehavior/
```

- [ ] **Step 6: Move Item/ file**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv ItemMudRake.cs Item/
```

- [ ] **Step 7: Move Recipe/ files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv GriddleRecipe.cs Recipe/
git mv GriddleRecipeRegistry.cs Recipe/
git mv PrepTableRecipe.cs Recipe/
git mv PrepTableRecipeRegistry.cs Recipe/
```

- [ ] **Step 8: Move Config/ files**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
git mv DryingFrameConfig.cs Config/
git mv GriddleConfig.cs Config/
git mv SaltPanConfig.cs Config/
git mv MudRakeConfig.cs Config/
```

- [ ] **Step 9: Verify root is clean**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
ls *.cs
```

Expected: exactly one file — `SeafarerModSystem.cs`.

```bash
ls
```

Expected: `Block/`, `BlockEntity/`, `CollectibleBehavior/`, `Config/`, `Entity/`, `EntityBehavior/`, `Item/`, `Properties/`, `Recipe/`, `SeafarerModSystem.cs`, `Seafarer.csproj`, `assets/`, `modinfo.json`, `quests/`.

- [ ] **Step 10: Build**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer
dotnet build Seafarer/Seafarer.csproj
```

Expected: 0 errors. SDK-style csproj globs pick up `.cs` files recursively, so folder moves don't require csproj edits.

- [ ] **Step 11: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add -A
git commit -m "Reorganize C# files into type folders (Block, BlockEntity, Item, etc.)"
```

---

## Task 6: Dedupe itemtypes/resource vs resources

**Files:**
- Move: `assets/seafarer/itemtypes/resources/seeds.json` → `assets/seafarer/itemtypes/resource/seeds.json`
- Modify: any files referencing `seafarer:resources/` or `itemtypes/resources/`.

- [ ] **Step 1: Move seeds.json**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/itemtypes
git mv resources/seeds.json resource/seeds.json
```

- [ ] **Step 2: Remove the now-empty resources/ directory**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/itemtypes
rmdir resources 2>/dev/null || echo "resources/ still contains files — inspect before deleting"
```

If `rmdir` fails, there are still files in `resources/`. List and evaluate — there shouldn't be any other than what Task 1 copied.

- [ ] **Step 3: Find and fix references**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
grep -rn "seafarer:resources/\|itemtypes/resources/\|resources/seeds" . | grep -v '\.md:'
```

Non-markdown hits must be rewritten. Typical locations:
- Recipe JSONs referring to `seafarer:seeds-*` by resources path
- Handbook entries
- Patch JSONs

For each hit file, update the path. Example: `"seafarer:resources/seeds-corn"` → `"seafarer:resource/seeds-corn"`. If a hit uses `resources/seeds` as the exact folder component, replace with `resource/seeds`.

To automate across the asset tree:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer
find assets -type f \( -name '*.json' -o -name '*.json5' \) -print0 | \
    xargs -0 sed -i 's|seafarer:resources/|seafarer:resource/|g; s|itemtypes/resources/|itemtypes/resource/|g'
```

Run the grep again — should now be 0 non-markdown hits.

- [ ] **Step 4: Verify seeds.json content unchanged**

```bash
diff /mnt/d/Development/vs/vsmod-salt-and-sand/SaltAndSand/SaltAndSand/assets/seafarer/itemtypes/resources/seeds.json \
     /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/itemtypes/resource/seeds.json
```

Expected: empty output (files identical).

- [ ] **Step 5: Build**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer
dotnet build Seafarer/Seafarer.csproj
```

Expected: 0 errors (pure asset change; build output is unchanged).

- [ ] **Step 6: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add -A
git commit -m "Merge itemtypes/resources/ into itemtypes/resource/ (dedupe typo)"
```

---

## Task 7: Run asset validators

**Files:**
- No edits. This is a validation-only step.

- [ ] **Step 1: Install validator deps**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
pip install -r requirements-dev.txt
```

Expected: `json5` and `pyyaml` installed (or already present).

- [ ] **Step 2: Adjust validators for the new repo layout**

The validators were designed for `vsmod-salt-and-sand/SaltAndSand/SaltAndSand/assets/…`; the new layout is `vsmod-seafarer/Seafarer/Seafarer/assets/…`. Inspect each validator for hardcoded paths:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
grep -n "SaltAndSand\|vsmod-salt-and-sand" validate-assets.py validate-food.py validate-dialogue.py vs_validators/*.py
```

For every hit, replace `SaltAndSand` with `Seafarer` in the path literal. If a validator uses a discovery function (searches for `modinfo.json` up from CWD), no edit is needed.

Do NOT rewrite `vs_validators` itself if the package name is internal and generic — only its path literals.

- [ ] **Step 3: Run validate-assets.py**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
python3 validate-assets.py
```

Expected: exit 0, 0 errors. Warnings are acceptable if they match the ones from the old repo.

If errors appear that reference paths like `resources/` → fix those references and re-run (that would indicate Task 6 step 3 missed a hit).

- [ ] **Step 4: Run validate-food.py**

```bash
python3 validate-food.py
```

Expected: exit 0, 0 errors.

- [ ] **Step 5: Run validate-dialogue.py**

```bash
python3 validate-dialogue.py
```

Expected: exit 0 or the same errors/warnings as before migration (dialogue content didn't change).

- [ ] **Step 6: Commit any validator path fixes**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add -A
git commit -m "Point validators at new Seafarer/ project path" || echo "No validator path changes needed"
```

The `|| echo …` catches the case where no edits were required.

---

## Task 8: Update modinfo.json (sanity check)

**Files:**
- Verify/modify: `Seafarer/Seafarer/modinfo.json`

The modinfo was copied verbatim from salt-and-sand in Task 1 — it already has `modid=seafarer`, `progressionframework 1.0.0` dep, correct description. This task just verifies that.

- [ ] **Step 1: Inspect**

```bash
cat /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/modinfo.json
```

Expected content:

```json
{
    "$schema": "https://moddbcdn.vintagestory.at/schema/modinfo.latest.json",
    "type": "Code",
    "modid": "seafarer",
    "name": "Seafarer",
    "description": "Get out of the mines and onto the ocean. A mod about exploration on the seas, with new seafaring vessels, tropical crops, food preservation, and port locations to discover.",
    "authors": [
        "TechnoGoth",
        "LiteraryJester"
    ],
    "version": "0.5.0",
    "dependencies": {
        "game": "1.22.0-rc.7",
        "progressionframework": "1.0.0"
    }
}
```

If any field differs, correct it to match the above.

- [ ] **Step 2: If changes were needed, commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/modinfo.json
git commit -m "Verify modinfo matches migrated content" || echo "modinfo already correct"
```

---

## Task 9: Update Seafarer CLAUDE.md

**Files:**
- Modify: `Seafarer/CLAUDE.md` (copied from salt-and-sand, needs rename-awareness)

- [ ] **Step 1: Inspect current content**

```bash
head -50 /mnt/d/Development/vs/vsmod-seafarer/Seafarer/CLAUDE.md
```

You'll see the salt-and-sand CLAUDE.md. It mentions `SaltAndSand/`, the old project structure, and (after earlier updates) the ProgressionFramework dependency.

- [ ] **Step 2: Rewrite the title line, project structure, and paths**

Open the file and change:
- First line (title): `# Salt and Sand - Vintage Story Mod` → `# Seafarer - Vintage Story Mod`
- Project overview paragraph: replace "A Vintage Story mod (modid: `seafarer`) that started as a log barge/raft mod and is expanding in scope." with:
  ```
  A Vintage Story mod (modid: `seafarer`) focused on ocean exploration — seafaring vessels, tropical crops, food preservation, salt pans, and port trader networks. Migrated from `vsmod-salt-and-sand` to this dedicated repo in April 2026. Hard-depends on ProgressionFramework for training, quests, and evolving traders.
  ```
- Project structure ASCII diagram — replace the `SaltAndSand/` tree with:
  ```
  Seafarer/                               # solution root
  ├── Seafarer/                           # C# project
  │   ├── SeafarerModSystem.cs            # entry point
  │   ├── modinfo.json                    # modid: "seafarer"
  │   ├── Block/                          # block classes (DryingFrame, SaltPan, Griddle, etc.)
  │   ├── BlockEntity/                    # block entities for above
  │   ├── CollectibleBehavior/            # ClamShuck, CoconutCrack, PlaceBurrito, ShellCrush
  │   ├── Entity/                         # EntityProjectileBarbed
  │   ├── EntityBehavior/                 # Exposure + helpers
  │   ├── Item/                           # ItemMudRake
  │   ├── Recipe/                         # Griddle + PrepTable recipe types + registries
  │   ├── Config/                         # DryingFrame, Griddle, SaltPan, MudRake configs
  │   ├── assets/seafarer/                # mod-namespaced assets (blocktypes, itemtypes, …)
  │   ├── assets/game/patches/            # JSON patches to base game data
  │   └── Seafarer.csproj
  ├── ZZCakeBuild/                        # Cake build system
  └── Seafarer.sln
  ```
- Build section: `dotnet build SaltAndSand/SaltAndSand.csproj` → `dotnet build Seafarer/Seafarer.csproj`
- Output path: `SaltAndSand/bin/Debug/Mods/mod/` → `Seafarer/bin/Debug/Mods/mod/`
- Any other mentions of "Salt and Sand" or "SaltAndSand" in descriptive prose → replace with "Seafarer"
- Keep the ProgressionFramework integration section as-is (paths are already repo-relative or use framework's absolute path).
- Add a top-level NOTE near the Project Overview:
  ```
  > **Successor to `vsmod-salt-and-sand`** — that repo remains on disk as a historical archive. All new work happens here.
  ```

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/CLAUDE.md
git commit -m "Update CLAUDE.md for Seafarer migration (rename, restructure, successor note)"
```

---

## Task 10: Update ProgressionFramework CLAUDE.md consumer reference

**Files:**
- Modify: `/mnt/d/Development/vs/vsmod-progression-framework/ProgressionFramework/CLAUDE.md`

The framework's CLAUDE.md currently mentions `vsmod-salt-and-sand` as the example consumer. Update to point at `vsmod-seafarer`.

- [ ] **Step 1: Locate and update the consumer section**

```bash
cd /mnt/d/Development/vs/vsmod-progression-framework
grep -n "vsmod-salt-and-sand\|Salt and Sand\|salt-and-sand" ProgressionFramework/CLAUDE.md
```

For each hit, update to the new location. The "Consumers" section specifically should read:

```markdown
## Consumers

- **Seafarer** (`vsmod-seafarer`, modid `seafarer`) — hard dependency in its modinfo. Its content JSONs (`assets/seafarer/config/quests/`, `config/training/`, `config/tradelists/`, `config/dialogue/`) are discovered automatically. Repo path: `D:\Development\vs\vsmod-seafarer\` (WSL: `/mnt/d/Development/vs/vsmod-seafarer/`).
- Archived: `vsmod-salt-and-sand` — Seafarer's previous home, kept on disk as a historical reference only.
```

- [ ] **Step 2: Commit in framework repo**

```bash
cd /mnt/d/Development/vs/vsmod-progression-framework
git add ProgressionFramework/CLAUDE.md
git commit -m "Update consumer reference: vsmod-salt-and-sand → vsmod-seafarer"
```

Framework stays on its current branch (`1.0.0`).

---

## Task 11: Add forwarding note to salt-and-sand

**Files:**
- Create: `vsmod-salt-and-sand/MIGRATED.md` (new file)
- Or modify: `vsmod-salt-and-sand/SaltAndSand/CLAUDE.md` top-of-file

- [ ] **Step 1: Create MIGRATED.md at repo root**

Write `/mnt/d/Development/vs/vsmod-salt-and-sand/MIGRATED.md`:

```markdown
# Repo archived

Seafarer development has moved to **`vsmod-seafarer`** (`D:\Development\vs\vsmod-seafarer\`).

This repository is preserved as a historical reference only. All new work — code, commits, issues, releases — happens in the new repo.

## Why the move
The project was renamed from "Salt and Sand" to "Seafarer" to match its modid, and the codebase was restructured to follow VSSurvivalMod's type-folder organization. Migrating into a fresh repo let us restart with a clean tree and clean git history.

## If you need something from here
- Prior commit history is intact on this repo's branches.
- The last working state before migration is tagged on the `refactor` branch.
- All assets, configs, and C# source are carried forward verbatim (with namespace + path adjustments) in the new repo.

Migration date: 2026-04-14.
```

- [ ] **Step 2: Commit in the old repo**

```bash
cd /mnt/d/Development/vs/vsmod-salt-and-sand
git add MIGRATED.md
git commit -m "Archive: project moved to vsmod-seafarer"
```

The old repo stays on branch `refactor`. No further commits expected.

---

## Task 12: Final build + in-game smoke test

**Files:**
- No edits — this is the end-to-end validation.

- [ ] **Step 1: Build framework (confirm still clean)**

```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory"
cd /mnt/d/Development/vs/vsmod-progression-framework
dotnet build ProgressionFramework/ProgressionFramework.csproj 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 2: Build Seafarer**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer/Seafarer
dotnet build Seafarer/Seafarer.csproj 2>&1 | tail -5
```

Expected: 0 errors, 0 warnings.

- [ ] **Step 3: Confirm output DLL + modinfo are produced**

```bash
ls /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/bin/Debug/Mods/mod/
```

Expected: `Seafarer.dll`, `Seafarer.deps.json`, `Seafarer.pdb`, `modinfo.json`.

- [ ] **Step 4: Launch VS with framework + new Seafarer mod**

Prompt to user:

> "Please launch Vintage Story with both mod folders added via `--addOrigin` or via Mod Manager:
>
> 1. `D:\Development\vs\vsmod-progression-framework\ProgressionFramework\ProgressionFramework\`
> 2. `D:\Development\vs\vsmod-seafarer\Seafarer\Seafarer\`
>
> **Do NOT load the old salt-and-sand mod** at the same time — having both Seafarer instances loaded would cause duplicate-asset errors.
>
> Load (or create) a test world."

- [ ] **Step 5: Verify smoke matrix**

Same matrix as the earlier extraction smoke test:

1. Mods load without errors.
2. Evolving trader spawns (`/entity spawn drake` or via worldgen); trade dialog opens.
3. A quest (e.g., `drake-tradeship`) can be accepted via dialogue.
4. Craft a recipe that grants XP (e.g., a pie); XP increments.
5. Ledger hotkey `L` — same behavior as post-extraction (no regression; not required to work if it wasn't working before).
6. Disable ProgressionFramework → Seafarer refuses to load with missing-dependency message.

Any failure → stop, investigate, file follow-up.

- [ ] **Step 6: Tag the migration as complete**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git tag -a migration-complete -m "Seafarer migrated from vsmod-salt-and-sand; builds and loads cleanly"
```

---

## Spec coverage self-review

| Spec section | Covered by |
|---|---|
| Decisions (rename, modid, history, namespace) | Task 1 (copy), Task 3 (rename), Task 4 (namespace), modid preserved by Task 1/8 |
| Repo layout | Tasks 1–5 land this exactly |
| C# file organization | Task 5 (one step per folder) |
| Rename manifest | Task 3 (file renames), Task 4 (symbol rewrites) |
| Asset cleanup (resource/resources) | Task 6 |
| External dependencies (framework) | Task 1 carries ProjectReference verbatim; Task 12 Step 1 verifies |
| Migration sequence (13 steps in spec) | Tasks 1–12 |
| Validation | Task 7 (validators), Task 12 (build + smoke) |
| Old repo disposition | Task 11 (forwarding note) |

No gaps.

## Placeholder scan

Scanned for "TBD", "implement later", "appropriate error handling", "similar to Task N" — none present.

One conditional command pattern (`|| echo "No validator path changes needed"`) appears twice; those are explicit, not placeholders — they handle the branching case cleanly.

## Execution notes

- Tasks 1–8 all touch only the new `vsmod-seafarer` repo. Sequential.
- Task 9 touches only the new repo's `CLAUDE.md`.
- Task 10 touches only the framework repo.
- Task 11 touches only the archived salt-and-sand repo.
- Task 12 spans repos and needs the user at the keyboard.

If execution happens across sessions, Task 8 (build passes) is a clean checkpoint.
