# Tortuga Port Hub Worldgen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Tortuga port hub (201×56×196 schematic) to Seafarer worldgen as a true world-singleton coastal landmark, spawning in shallow ocean 500–3000 blocks from world spawn.

**Architecture:** Extend the existing `GenOceanStructures` ModSystem with three new `OceanStructureDef` fields (`GlobalMaxCount`, `MinSpawnDist`, `MaxSpawnDist`), a server-side persisted count dictionary keyed on structure code, and two new placement gates. No new files — all code changes live in `GenOceanStructures.cs`. Singleton state persists across save/load via `ICoreServerAPI.WorldManager.SaveGame.StoreData/GetData`.

**Tech Stack:** C# / .NET 10.0, Vintage Story API (ICoreServerAPI, SerializerUtil, BlockSchematic, LCGRandom)

**Spec:** `docs/superpowers/specs/2026-04-19-tortuga-worldgen-design.md`

**Project note:** This is a Vintage Story mod. The project has no automated test suite — validation is `dotnet build` for compile-correctness, `python3 validate-assets.py` for asset integrity, and manual in-game verification. Tasks below use build checks in place of unit tests.

---

## File Map

| File | Responsibility | Change Type |
|---|---|---|
| `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` | Ocean worldgen system — extend with singleton tracking and spawn-distance gating | Modify |
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Ocean structure config — add Tortuga entry | Modify |

---

### Task 1: Extend `OceanStructureDef` with new fields

Add three new fields to the config POCO so `oceanstructures.json` can express singleton and spawn-distance constraints.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs:17-29`

- [ ] **Step 1: Add three new fields to `OceanStructureDef`**

In `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`, locate the `OceanStructureDef` class (starts at line 17). Replace the class body with:

```csharp
    public class OceanStructureDef
    {
        public string Code;
        public string[] Schematics;
        public EnumOceanPlacement Placement;
        public float Chance;
        public int MinWaterDepth = 0;
        public int MaxWaterDepth = 255;
        public int OffsetY = 0;
        public int MaxCount = 0;
        public bool SuppressTrees = false;
        public bool RandomRotation = true;

        // World-singleton tracking (0 = unlimited)
        public int GlobalMaxCount = 0;

        // Radial distance from world spawn in blocks (0 = no constraint)
        public int MinSpawnDist = 0;
        public int MaxSpawnDist = 0;
    }
```

- [ ] **Step 2: Build to verify the field addition compiles**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "feat(worldgen): add singleton + spawn-distance fields to OceanStructureDef"
```

---

### Task 2: Add persisted global-count state to `GenOceanStructures`

Add a server-side dictionary keyed on structure code that tracks world-global placement counts, with `SaveGameLoaded` / `GameWorldSave` hooks for persistence.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` — add fields, using directives, lifecycle hooks

- [ ] **Step 1: Add `Vintagestory.API.Util` using directive**

In `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`, locate the using block at lines 1-6. Replace with:

```csharp
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
```

- [ ] **Step 2: Add `globalCounts`, lock, and storage-key fields**

In `GenOceanStructures`, locate the existing private field block (around line 38-48, ending with the `cachedSchematics` field). Immediately after the `cachedSchematics` dictionary declaration, add:

```csharp
        // World-global count of placed structures, keyed on def.Code.
        // Accessed from the chunk-gen worker thread and from save hooks, so guard with countsLock.
        private readonly Dictionary<string, int> globalCounts = new();
        private readonly object countsLock = new();
        private const string CountsDataKey = "seafarer-ocean-structure-counts";
```

- [ ] **Step 3: Register save/load lifecycle hooks in `StartServerSide`**

In `GenOceanStructures.StartServerSide`, locate the call to `api.Event.GetWorldgenBlockAccessor(...)`. Immediately after that block, and before the `var parsers = api.ChatCommands.Parsers;` line, add:

```csharp
            api.Event.SaveGameLoaded += OnSaveGameLoaded;
            api.Event.GameWorldSave += OnGameWorldSave;
```

- [ ] **Step 4: Implement `OnSaveGameLoaded` and `OnGameWorldSave` methods**

Add these two private methods to `GenOceanStructures`. Place them immediately before the `private TextCommandResult OnCmdPlace(...)` method so related server-lifecycle code stays grouped:

```csharp
        private void OnSaveGameLoaded()
        {
            byte[] data = sapi.WorldManager.SaveGame.GetData(CountsDataKey);
            lock (countsLock)
            {
                globalCounts.Clear();
                if (data != null)
                {
                    var loaded = SerializerUtil.Deserialize<Dictionary<string, int>>(data);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded) globalCounts[kv.Key] = kv.Value;
                    }
                }
            }
        }

        private void OnGameWorldSave()
        {
            Dictionary<string, int> snapshot;
            lock (countsLock)
            {
                snapshot = new Dictionary<string, int>(globalCounts);
            }
            sapi.WorldManager.SaveGame.StoreData(CountsDataKey, SerializerUtil.Serialize(snapshot));
        }
```

- [ ] **Step 5: Build to verify compilation**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 6: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "feat(worldgen): persist ocean-structure global counts across save/load"
```

---

### Task 3: Add singleton + spawn-distance gates to `OnChunkColumnGen`

Wire the new fields into placement logic. Two new gates (singleton, spawn-distance); one counter increment after successful placement.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs:147-204` — `OnChunkColumnGen` method

- [ ] **Step 1: Replace the full `OnChunkColumnGen` body with the gated version**

Locate `private void OnChunkColumnGen(IChunkColumnGenerateRequest request)` (starts at line 147). Replace the entire method body with:

```csharp
        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            if (config.Structures.Length == 0) return;

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;
            var mapChunk = chunks[0].MapChunk;
            var mapRegion = mapChunk.MapRegion;
            int seaLevel = sapi.World.SeaLevel;

            var spawnPos = sapi.World.DefaultSpawnPosition.AsBlockPos;

            foreach (var def in config.Structures)
            {
                if (!cachedSchematics.TryGetValue(def.Code, out var variants)) continue;

                rand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
                float roll = (float)rand.NextInt(10000) / 10000f;
                if (roll > def.Chance) continue;

                // Global singleton gate: if a per-world cap is set and we've hit it, skip.
                if (def.GlobalMaxCount > 0)
                {
                    lock (countsLock)
                    {
                        if (globalCounts.TryGetValue(def.Code, out int placed) && placed >= def.GlobalMaxCount)
                            continue;
                    }
                }

                if (def.MaxCount > 0 && CountExistingStructures(mapRegion, def.Code) >= def.MaxCount) continue;

                int localX = rand.NextInt(chunksize);
                int localZ = rand.NextInt(chunksize);
                int posX = chunkX * chunksize + localX;
                int posZ = chunkZ * chunksize + localZ;

                // Spawn-distance gate: radial distance from world spawn.
                if (def.MinSpawnDist > 0 || def.MaxSpawnDist > 0)
                {
                    int dx = posX - spawnPos.X;
                    int dz = posZ - spawnPos.Z;
                    double dist = Math.Sqrt((double)dx * dx + (double)dz * dz);
                    if (def.MinSpawnDist > 0 && dist < def.MinSpawnDist) continue;
                    if (def.MaxSpawnDist > 0 && dist > def.MaxSpawnDist) continue;
                }

                float oceanicity = GetOceanicity(mapRegion, posX, posZ);
                float beachStrength = GetBeachStrength(mapRegion, posX, posZ);

                int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
                int waterDepth = seaLevel - terrainHeight;

                if (!IsValidPlacement(def, oceanicity, beachStrength, waterDepth)) continue;

                var variantRotations = variants[rand.NextInt(variants.Length)];
                int rotationIndex = def.RandomRotation ? rand.NextInt(4) : 0;
                var schematic = variantRotations[rotationIndex];

                int posY = CalculatePlacementY(def, terrainHeight, schematic);
                var startPos = new BlockPos(posX, posY, posZ);

                schematic.Place(worldgenBlockAccessor, sapi.World, startPos, EnumReplaceMode.ReplaceAll, true);

                mapRegion.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = def.Code,
                    Group = "ocean",
                    Location = new Cuboidi(
                        startPos.X, startPos.Y, startPos.Z,
                        startPos.X + schematic.SizeX - 1,
                        startPos.Y + schematic.SizeY - 1,
                        startPos.Z + schematic.SizeZ - 1
                    ),
                    SuppressTreesAndShrubs = def.SuppressTrees,
                    SuppressRivulets = true
                });

                // Increment the world-global counter so singletons don't repeat.
                if (def.GlobalMaxCount > 0)
                {
                    lock (countsLock)
                    {
                        globalCounts.TryGetValue(def.Code, out int placed);
                        globalCounts[def.Code] = placed + 1;
                    }
                }
            }
        }
```

- [ ] **Step 2: Build to verify compilation**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "feat(worldgen): enforce singleton and spawn-distance gates in chunk gen"
```

**Note on admin command:** `OnCmdPlace` (lines 294–332) is intentionally *not* modified. Admin `/ocean place <code>` does not increment `globalCounts` so creative/testing placements don't consume the singleton budget. This matches the spec.

---

### Task 4: Add Tortuga entry to `oceanstructures.json`

Register the new structure with ocean placement, singleton cap, spawn-distance band, and 5% per-chunk chance.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`

- [ ] **Step 1: Add the Tortuga structure entry**

Open `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` and replace its contents with:

```json5
{
  "structures": [
    {
      "code": "wreck-crimsonrose",
      "schematics": ["underwater/wreak-crimson-rose"],
      "placement": "underwater",
      "chance": 0.015,
      "minWaterDepth": 10,
      "maxWaterDepth": 50,
      "offsetY": 0,
      "maxCount": 1,
      "suppressTrees": true,
      "randomRotation": true
    },
    {
      "code": "wreck-crimsonrose",
      "schematics": ["underwater/wreak-one"],
      "placement": "underwater",
      "chance": 0.015,
      "minWaterDepth": 10,
      "maxWaterDepth": 100,
      "offsetY": 0,
      "suppressTrees": true,
      "randomRotation": true
    },
    {
      "code": "tortuga",
      "schematics": ["costal/tortuga"],
      "placement": "underwater",
      "chance": 0.05,
      "minWaterDepth": 3,
      "maxWaterDepth": 15,
      "offsetY": 0,
      "globalMaxCount": 1,
      "minSpawnDist": 500,
      "maxSpawnDist": 3000,
      "suppressTrees": true,
      "randomRotation": true
    }
  ]
}
```

- [ ] **Step 2: Verify the JSON parses and matches `OceanStructuresConfig`**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj`
Expected: Build succeeds. (The build step also copies assets; a malformed JSON won't surface until runtime, but this confirms the tree is intact.)

- [ ] **Step 3: Run the mod asset validator**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py`
Expected: Exit code 0, 0 errors. Warnings acceptable if unrelated to the changed file.

If `validate-assets.py` is not at the repo root, try: `python3 vs_validators/validate-assets.py`. If dependencies are missing, install with `pip install json5 pyyaml`.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json
git commit -m "feat(worldgen): add Tortuga port hub as world-singleton coastal landmark"
```

---

### Task 5: Commit the new schematic

The 18 MB schematic `costal/tortuga.json` is currently untracked. Commit it as part of this feature.

**Files:**
- Commit: `Seafarer/Seafarer/assets/seafarer/worldgen/schematics/costal/tortuga.json`

- [ ] **Step 1: Confirm the file exists and is the expected schematic**

Run: `ls -la /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/assets/seafarer/worldgen/schematics/costal/tortuga.json`
Expected: File exists, size ~18 MB.

- [ ] **Step 2: Commit the schematic**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/worldgen/schematics/costal/tortuga.json
git commit -m "feat(worldgen): add Tortuga port hub schematic (201x56x196)"
```

---

### Task 6: Manual in-game verification

No automated test harness exists; verification is manual. Run Vintage Story with the mod loaded and work through this checklist. Record results in commit messages or a follow-up note.

**Files:** None modified — this is pure verification.

- [ ] **Step 1: Launch the game with the mod loaded and create a fresh world**

Use the Seafarer mod build output. Create a new world and wait for spawn chunks to generate.

- [ ] **Step 2: Confirm Tortuga is registered**

In chat, run: `/ocean list`
Expected: output includes a `tortuga` line with `placement=Underwater, chance=0.05, schematics=1`.

- [ ] **Step 3: Admin-place Tortuga for structure inspection (does NOT consume singleton)**

Stand on a flat area, open chat, run: `/ocean place tortuga`
Expected: "Placed 'tortuga' at {pos} ({N} blocks)" message appears; structure is visible around the player.

- [ ] **Step 4: Verify admin placement did not consume the singleton**

Save and reload the world. Confirm `/ocean list` still reports `tortuga`. Natural gen should still be eligible (the singleton counter only increments on natural chunk-gen placements, not admin commands). This is a code-path assertion; if unsure, re-read `OnChunkColumnGen` — only the natural gen branch increments `globalCounts`.

- [ ] **Step 5: Verify natural singleton placement**

Teleport or sail into a coastal region 500–3000 blocks from spawn. Trigger chunk generation by exploring. Expected: exactly one Tortuga appears somewhere in the band. To verify singleton: continue exploring additional coastline within the band — no second Tortuga should spawn.

Useful commands:
- `/tp ~+500 ~ ~0` etc. to jump around
- `/time set day`
- `/entity countbyclass` and map coordinates to confirm position

- [ ] **Step 6: Verify save/load persistence**

After confirming natural placement, quit the world cleanly (via main menu, not Alt+F4 — `GameWorldSave` must fire). Reload. Continue exploring within the 500–3000 band. Expected: no second Tortuga spawns, confirming the count persisted.

- [ ] **Step 7: Record results**

If all checks pass, commit a short note:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git commit --allow-empty -m "test(worldgen): manually verified Tortuga singleton + persistence"
```

If any check fails, file the failure mode (unexpected spawn count, wrong placement, persistence drift) before iterating.

---

## Self-review checklist

- **Spec coverage:** Config entry (Task 4), `OceanStructureDef` fields (Task 1), `globalCounts` + lock (Task 2), save/load hooks (Task 2), singleton gate (Task 3), spawn-distance gate (Task 3), counter increment (Task 3), admin-command exemption (noted in Task 3, no code change needed), schematic commit (Task 5), manual verification (Task 6). All spec sections covered.
- **No placeholders:** No "TBD", "TODO", or "implement later" in any step. Every code block is complete.
- **Type consistency:** `OceanStructureDef.GlobalMaxCount`, `.MinSpawnDist`, `.MaxSpawnDist` referenced consistently across tasks. `globalCounts` / `countsLock` / `CountsDataKey` spelled identically in Tasks 2 and 3. JSON keys (`globalMaxCount`, `minSpawnDist`, `maxSpawnDist`) are the camelCase form Newtonsoft will map to the PascalCase C# fields — matches the existing config convention (see `chance`, `maxWaterDepth` in the wreck entries).
