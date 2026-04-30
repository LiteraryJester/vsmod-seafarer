# GenSeafarerStructures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `GenOceanStructures.cs` with a single class `GenSeafarerStructures` that duplicates base-game `GenStoryStructures` behavior (init-time determination, persistence, per-chunk `PlacePartial`, land claims) and adds Coastal/Underwater/OceanSurface placement with ocean-aware validation, opt-in footprint clearing, and hooks for future decorators.

**Architecture:** New Seafarer-owned mod system replacing the old one. Types live alongside the mod system in one file (matching base-game convention). Config migrates to `seafarerstructures.json`; old `storystructures.json` and `oceanstructures.json` are deleted so base-game story-gen stops picking up our entries. Placement fans out through a `ResolveY` + `ValidateOceanPlacement` pair that handles five placement modes. Story and scattered structures are the same type gated by a `storyStructure` flag. Extension point is an `OnStructurePlaced` event.

**Tech Stack:** C# / .NET 10.0, Vintage Story API (`ModStdWorldGen`, `BlockSchematicPartial.PlacePartial`, `IMapRegion.OceanMap`/`BeachMap`, `LCGRandom`, protobuf-net via `SerializerUtil`).

**Spec:** `docs/superpowers/specs/2026-04-20-gen-seafarer-structures-design.md`

**Project note:** Vintage Story mod, no automated test suite. Verification is `dotnet build`, `python3 validate-assets.py`, and manual in-game verification.

**Build command (from repo root):**
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj
```

**Strategy:** Build the new class in parallel to the old one with an empty config. The old `GenOceanStructures` stays running through Tasks 1–8 and continues to serve `oceanstructures.json` entries. Task 9 is a single-commit cutover that migrates all config entries, deletes the old class, and deletes the old JSON files. This keeps the mod functional at every checkpoint and contains the risky migration to one reviewable diff.

---

## File Map

| File | Responsibility | Change |
|---|---|---|
| `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs` | New mod system — types, enum, config loading, story determination, per-chunk placement, commands, hooks | Create |
| `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json` | Merged ocean-aware structure config | Create |
| `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` | Old base-game-picked-up story entries | **Delete** (Task 9) |
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Old scattered ocean structure config | **Delete** (Task 9) |
| `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` | Old mod system being replaced | **Delete** (Task 9) |

---

### Task 1: Types, enum, and empty config asset

Create the types and enum that everything else depends on. Ship an empty config asset so `LoadConfig` has something to read without erroring.

**Files:**
- Create: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`
- Create: `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json`

- [ ] **Step 1: Create the new file with namespace and using directives**

Write `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

#nullable disable

namespace Seafarer.WorldGen
{
    public enum EnumSeafarerPlacement
    {
        Surface,
        SurfaceRuin,
        Coastal,
        Underwater,
        OceanSurface
    }

    public class SeafarerStructure : WorldGenStructureBase
    {
        public string Group;
        public string Name;
        public EnumSeafarerPlacement Placement = EnumSeafarerPlacement.Surface;
        public bool StoryStructure = false;
        public float Chance = 0f;
        public bool ClearFootprint = false;

        public bool RequireOcean = false;
        public bool RequireCoast = false;
        public int MinWaterDepth = 0;
        public int MaxWaterDepth = 255;

        public string DependsOnStructure;
        public int MinSpawnDistX;
        public int MaxSpawnDistX;
        public int MinSpawnDistZ;
        public int MaxSpawnDistZ;

        public string RequireLandform;
        public int LandformRadius;
        public int GenerationRadius;
        public int? ForceRain;
        public int? ForceTemperature;

        public Dictionary<string, int> SkipGenerationCategories;
        public Dictionary<int, int> SkipGenerationFlags;

        public bool UseWorldgenHeight;
        public bool DisableSurfaceTerrainBlending;
        public bool GenerateGrass;
        public bool SuppressTrees;
        public bool RandomRotation = true;

        public bool BuildProtected;
        public int ProtectionLevel = 10;
        public string BuildProtectionName;
        public string BuildProtectionDesc;
        public bool AllowUseEveryone;
        public bool AllowTraverseEveryone;
        public bool ExcludeSchematicSizeProtect;
        public int ExtraLandClaimX;
        public int ExtraLandClaimZ;
        public Cuboidi[] CustomLandClaims;

        public string[] PostPlaceDecorators = Array.Empty<string>();

        internal BlockSchematicPartial schematicData;
    }

    public class SeafarerStructuresConfig
    {
        public Dictionary<string, int> SchematicYOffsets = new();
        public Dictionary<string, Dictionary<AssetLocation, AssetLocation>> RocktypeRemapGroups = new();
        public SeafarerStructure[] Structures = Array.Empty<SeafarerStructure>();
    }

    [ProtoContract]
    public class SeafarerStructureLocation
    {
        [ProtoMember(1)] public string Code;
        [ProtoMember(2)] public BlockPos CenterPos;
        [ProtoMember(3)] public Cuboidi Location;
        [ProtoMember(4)] public int LandformRadius;
        [ProtoMember(5)] public int GenerationRadius;
        [ProtoMember(6)] public int DirX;
        [ProtoMember(7)] public Dictionary<int, int> SkipGenerationFlags;
        [ProtoMember(8)] public bool DidGenerate;
        [ProtoMember(9)] public int WorldgenHeight = -1;
        [ProtoMember(10)] public string RockBlockCode;
        [ProtoMember(11)] public bool OceanValidated;

        public int MaxSkipGenerationRadiusSq;
    }

    public class SeafarerStructurePlacedEvent
    {
        public SeafarerStructure Def;
        public string Code;
        public int ChunkX;
        public int ChunkZ;
        public Cuboidi Bounds;
        public int BlocksPlacedInSlice;
    }
}
```

- [ ] **Step 2: Create the empty config asset**

Write `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json`:

```json
{
  "schematicYOffsets": {},
  "rocktypeRemapGroups": {},
  "structures": []
}
```

- [ ] **Step 3: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: Build succeeded, 0 errors. Warnings may exist but new code should compile cleanly. The `#nullable disable` directive matches existing files.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json
git commit -m "$(cat <<'EOF'
feat(worldgen): scaffold SeafarerStructure types and empty config

New types live in WorldGen/GenSeafarerStructures.cs alongside the
eventual mod system (matches base-game convention). EnumSeafarerPlacement
introduces Coastal/Underwater/OceanSurface on top of base-game
Surface/SurfaceRuin. SeafarerStructureLocation is ProtoContract-ready
for savegame persistence. Empty seafarerstructures.json gives LoadConfig
a well-formed asset to read once the mod system lands.

No behavior yet — the mod system class is added in the next task.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Mod system scaffolding and asset loading

Add the `GenSeafarerStructures : ModStdWorldGen` class, register event handlers, implement `LoadConfig` to read the (empty) asset, and wire savegame load/save stubs. Leaves placement empty — just proves the pipeline loads without errors.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`

- [ ] **Step 1: Append the mod system class to the file**

At the end of `GenSeafarerStructures.cs` (inside the `Seafarer.WorldGen` namespace, after the types), add:

```csharp
    [ProtoContract]
    public class SeafarerGenFailed
    {
        [ProtoMember(1)]
        public List<string> MissingStructures;
    }

    public class GenSeafarerStructures : ModStdWorldGen
    {
        public SeafarerStructuresConfig scfg;

        protected ICoreServerAPI api;
        protected LCGRandom strucRand;
        protected LCGRandom grassRand;
        protected IWorldGenBlockAccessor worldgenBlockAccessor;
        protected BlockLayerConfig blockLayerConfig;
        protected bool FailedToGenerateLocation;
        protected IServerNetworkChannel serverChannel;

        protected readonly OrderedDictionary<string, SeafarerStructureLocation> storyLocations = new();
        protected readonly List<string> attemptedCodes = new();
        protected bool LocationsDirty;

        public IReadOnlyDictionary<string, SeafarerStructureLocation> Locations => storyLocations;
        public event Action<SeafarerStructurePlacedEvent> OnStructurePlaced;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
        public override double ExecuteOrder() => 0.21;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.api = api;
            base.StartServerSide(api);

            api.Event.SaveGameLoaded += Event_SaveGameLoaded;
            api.Event.GameWorldSave += Event_GameWorldSave;

            if (TerraGenConfig.DoDecorationPass)
            {
                api.Event.InitWorldGenerator(InitWorldGen, "standard");
                api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.Vegetation, "standard");
                api.Event.GetWorldgenBlockAccessor(OnWorldGenBlockAccessor);
            }

            serverChannel = api.Network.RegisterChannel("SeafarerGenFailed");
            serverChannel.RegisterMessageType<SeafarerGenFailed>();
        }

        private void OnWorldGenBlockAccessor(IChunkProviderThread chunkProvider)
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        }

        public void InitWorldGen()
        {
            strucRand = new LCGRandom(api.WorldManager.Seed ^ 2389173L);
            grassRand = new LCGRandom(api.WorldManager.Seed);
            blockLayerConfig = BlockLayerConfig.GetInstance(api);

            LoadConfig();
        }

        private void LoadConfig()
        {
            var assets = api.Assets.GetMany<SeafarerStructuresConfig>(api.Logger, "worldgen/seafarerstructures.json");

            scfg = new SeafarerStructuresConfig();
            var structures = new List<SeafarerStructure>();

            foreach (var (_, conf) in assets)
            {
                foreach (var remap in conf.RocktypeRemapGroups)
                {
                    scfg.RocktypeRemapGroups.TryAdd(remap.Key, remap.Value);
                }
                foreach (var offset in conf.SchematicYOffsets)
                {
                    scfg.SchematicYOffsets.TryAdd(offset.Key, offset.Value);
                }
                if (conf.Structures != null) structures.AddRange(conf.Structures);
            }

            scfg.Structures = structures.ToArray();

            foreach (var def in scfg.Structures)
            {
                if (def.Schematics == null || def.Schematics.Length == 0) continue;
                try
                {
                    def.schematicData = def.LoadSchematics<BlockSchematicPartial>(api, def.Schematics, null)[0];
                    def.schematicData.blockLayerConfig = blockLayerConfig;

                    if (scfg.SchematicYOffsets.TryGetValue(
                        "story/" + def.schematicData.FromFile.GetNameWithDomain().Replace(".json", ""),
                        out var off))
                    {
                        def.schematicData.OffsetY = off;
                    }
                }
                catch (Exception e)
                {
                    api.Logger.Error("Seafarer structure '{0}': schematic load failed: {1}", def.Code, e.Message);
                }
            }

            api.Logger.Notification("Seafarer structures: loaded {0} definitions.", scfg.Structures.Length);
        }

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            // Story + scattered placement wired up in later tasks.
        }

        private void Event_SaveGameLoaded()
        {
            var locData = api.WorldManager.SaveGame.GetData<OrderedDictionary<string, SeafarerStructureLocation>>("seafarer-structure-locations");
            if (locData != null)
            {
                foreach (var kv in locData) storyLocations[kv.Key] = kv.Value;
            }

            var attempted = api.WorldManager.SaveGame.GetData<List<string>>("attemptedToGenerateSeafarerLocation");
            if (attempted != null) attemptedCodes.AddRange(attempted);
        }

        private void Event_GameWorldSave()
        {
            if (LocationsDirty)
            {
                api.WorldManager.SaveGame.StoreData("seafarer-structure-locations", SerializerUtil.Serialize(storyLocations));
                LocationsDirty = false;
            }
            api.WorldManager.SaveGame.StoreData("attemptedToGenerateSeafarerLocation", attemptedCodes);
        }

        protected void TriggerOnStructurePlaced(SeafarerStructure def, int chunkX, int chunkZ, Cuboidi bounds, int blocks)
        {
            OnStructurePlaced?.Invoke(new SeafarerStructurePlacedEvent
            {
                Def = def,
                Code = def.Code,
                ChunkX = chunkX,
                ChunkZ = chunkZ,
                Bounds = bounds,
                BlocksPlacedInSlice = blocks
            });
        }
    }
```

- [ ] **Step 2: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors. `ExecuteOrder` collides with the inherited virtual signature — confirm override keyword is correct (the template uses `public override double ExecuteOrder() => 0.21;`).

- [ ] **Step 3: Launch the game once and verify no errors**

Run the game long enough to reach the main menu, or start a new world if you have one ready. In server log expect one line:
```
[Seafarer] Seafarer structures: loaded 0 definitions.
```
No exceptions. Close the game.

If you don't want to launch yet, skip this step — later tasks will catch any load-time issue.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): add GenSeafarerStructures mod system scaffolding

Registers on the Vegetation pass, loads seafarerstructures.json via
api.Assets.GetMany, stores story locations + attempted list to savegame.
Placement handlers are empty stubs — this task just proves the pipeline
loads without errors. Old GenOceanStructures.cs is untouched and still
serves oceanstructures.json until Task 9 cuts over.

ExecuteOrder = 0.21 slots us just after base-game GenStoryStructures
(0.2) so story locations determined by both systems are cleanly ordered.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Story-structure determination

Port base-game `DetermineStoryStructures` → `DetermineSeafarerStoryStructures`. Computes X/Z for each `storyStructure: true` entry at init, persists the result, and reports missing/failed entries. No per-chunk placement yet — the next task handles that.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`

- [ ] **Step 1: Add `SkipGenerationCategories` hash resolution**

Find `LoadConfig` inside `GenSeafarerStructures`. After the `scfg.Structures = structures.ToArray();` line but before the schematic-load loop, add:

```csharp
            // Resolve skip-generation-categories to SHA-hashed flags so base-game worldgen
            // can match against our radii.
            foreach (var def in scfg.Structures)
            {
                if (def.SkipGenerationCategories != null)
                {
                    def.SkipGenerationFlags = new Dictionary<int, int>();
                    foreach (var category in def.SkipGenerationCategories)
                    {
                        int key = BitConverter.ToInt32(
                            System.Security.Cryptography.SHA256.HashData(
                                System.Text.Encoding.UTF8.GetBytes(category.Key.ToLowerInvariant())));
                        def.SkipGenerationFlags.Add(key, category.Value);
                    }
                }
            }
```

Also add `using System.Security.Cryptography;` and `using System.Text;` at the top of the file if not already present (already imported transitively in most cases; add if the build complains).

- [ ] **Step 2: Wire determination into `InitWorldGen`**

Append to `InitWorldGen` (after `LoadConfig();`):

```csharp
            DetermineSeafarerStoryStructures();
            strucRand.SetWorldSeed(api.WorldManager.Seed ^ 2389173L);
```

- [ ] **Step 3: Add `DetermineSeafarerStoryStructures` method**

Inside `GenSeafarerStructures`, add:

```csharp
        private const int MaxDeterminationAttempts = 30;

        protected void DetermineSeafarerStoryStructures()
        {
            BlockPos spawnPos;
            var df = api.WorldManager.SaveGame.DefaultSpawn;
            if (df != null)
            {
                spawnPos = new BlockPos(df.x, df.y ?? 0, df.z, 0);
            }
            else
            {
                spawnPos = api.World.BlockAccessor.MapSize.AsBlockPos / 2;
            }

            int i = 0;
            foreach (var def in scfg.Structures)
            {
                if (!def.StoryStructure) { i++; continue; }

                // Already determined (persisted from prior load)
                if (storyLocations.TryGetValue(def.Code, out var existing))
                {
                    // refresh runtime-tunable fields in case the asset was edited
                    existing.LandformRadius = def.LandformRadius;
                    existing.GenerationRadius = def.GenerationRadius;
                    existing.SkipGenerationFlags = def.SkipGenerationFlags;
                    if (existing.SkipGenerationFlags != null && existing.SkipGenerationFlags.Count > 0)
                    {
                        int max = existing.SkipGenerationFlags.Max(f => f.Value);
                        existing.MaxSkipGenerationRadiusSq = max * max;
                    }
                    i++;
                    continue;
                }

                if (attemptedCodes.Contains(def.Code)) { i++; continue; }

                strucRand.SetWorldSeed(api.WorldManager.Seed ^ (uint)(def.Code.GetHashCode() + i));
                TryDetermineLocation(def, spawnPos);
                attemptedCodes.Add(def.Code);
                i++;
            }

            LocationsDirty = true;
        }

        private void TryDetermineLocation(SeafarerStructure def, BlockPos spawnPos)
        {
            BlockPos basePos = spawnPos;
            if (!string.IsNullOrEmpty(def.DependsOnStructure) && def.DependsOnStructure != "spawn")
            {
                if (storyLocations.TryGetValue(def.DependsOnStructure, out var dep))
                {
                    basePos = dep.CenterPos.Copy();
                }
                else
                {
                    FailedToGenerateLocation = true;
                    api.Logger.Error(
                        "Seafarer structure '{0}' depends on '{1}', which was not found. Use /wgen seafarer setpos to place it manually.",
                        def.Code, def.DependsOnStructure);
                    return;
                }
            }

            if (def.schematicData == null)
            {
                api.Logger.Error("Seafarer structure '{0}': no schematic data, skipping determination.", def.Code);
                FailedToGenerateLocation = true;
                return;
            }

            int dirX = strucRand.NextFloat() > 0.5 ? -1 : 1;
            int dirZ = strucRand.NextFloat() > 0.5 ? -1 : 1;

            for (int attempt = 0; attempt < MaxDeterminationAttempts; attempt++)
            {
                int distanceX = def.MinSpawnDistX + strucRand.NextInt(Math.Max(1, def.MaxSpawnDistX + 1 - def.MinSpawnDistX));
                int distanceZ = def.MinSpawnDistZ + strucRand.NextInt(Math.Max(1, def.MaxSpawnDistZ + 1 - def.MinSpawnDistZ));

                int px = basePos.X + distanceX * dirX;
                int pz = basePos.Z + distanceZ * dirZ;
                int py = def.Placement == EnumSeafarerPlacement.Surface || def.Placement == EnumSeafarerPlacement.OceanSurface
                    ? api.World.SeaLevel + def.schematicData.OffsetY
                    : 1;

                var pos = new BlockPos(px, py, pz, 0);
                int schemX = def.schematicData.SizeX;
                int schemZ = def.schematicData.SizeZ;
                int minX = pos.X - schemX / 2;
                int minZ = pos.Z - schemZ / 2;
                var loc = new Cuboidi(minX, pos.Y, minZ, minX + schemX, pos.Y + def.schematicData.SizeY, minZ + schemZ);

                // NOTE: Ocean validation is deferred to per-chunk Y-resolution (see Task 5).
                // Candidate accepted on first attempt; if fine-grained validation fails later,
                // the reservation is dropped and the structure is reported via listmissing.
                var entry = new SeafarerStructureLocation
                {
                    Code = def.Code,
                    CenterPos = pos,
                    Location = loc,
                    LandformRadius = def.LandformRadius,
                    GenerationRadius = def.GenerationRadius,
                    DirX = dirX,
                    SkipGenerationFlags = def.SkipGenerationFlags
                };
                if (entry.SkipGenerationFlags != null && entry.SkipGenerationFlags.Count > 0)
                {
                    int max = entry.SkipGenerationFlags.Max(f => f.Value);
                    entry.MaxSkipGenerationRadiusSq = max * max;
                }

                storyLocations[def.Code] = entry;
                api.Logger.Notification("Seafarer story structure '{0}' determined at ({1}, {2}, {3})", def.Code, pos.X, pos.Y, pos.Z);
                return;
            }

            FailedToGenerateLocation = true;
            api.Logger.Warning("Seafarer story structure '{0}': no valid candidate found after {1} attempts.", def.Code, MaxDeterminationAttempts);
        }

        public List<string> GetMissingStructures()
        {
            var missing = new List<string>();
            foreach (var code in attemptedCodes)
            {
                if (!storyLocations.ContainsKey(code)) missing.Add(code);
            }
            return missing;
        }
```

- [ ] **Step 4: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): determine Seafarer story-structure locations at init

Ports base-game DetermineStoryStructures pattern: spawn-dist box
sampling with per-code seeded RNG, dependsOnStructure chaining,
persisted attempted-list for /wgen regen idempotency. Ocean
validation is deferred to chunk-gen (next task) to avoid the
map-regions-not-loaded-yet problem on fresh worlds.

With config still empty, the loop is a no-op. Next task adds the
per-chunk placement path that reads storyLocations.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Per-chunk story placement with Y resolution for all modes

Port base-game per-chunk story placement into `OnChunkColumnGen`, adapted for `EnumSeafarerPlacement`. Handles Y resolution for each mode (Surface, SurfaceRuin, Coastal, Underwater, OceanSurface), calls `PlacePartial`, emits `GeneratedStructure`, emits land claims, fires `OnStructurePlaced`.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`

- [ ] **Step 1: Replace the empty `OnChunkColumnGen` with the story path**

Find `OnChunkColumnGen` in `GenSeafarerStructures`. Replace its body with:

```csharp
        private readonly Cuboidi tmpCuboid = new();

        private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
        {
            if (scfg == null || scfg.Structures.Length == 0) return;

            var chunks = request.Chunks;
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;
            var mapRegion = chunks[0].MapChunk.MapRegion;

            tmpCuboid.Set(
                chunkX * chunksize, 0, chunkZ * chunksize,
                chunkX * chunksize + chunksize, chunks.Length * chunksize, chunkZ * chunksize + chunksize);

            worldgenBlockAccessor.BeginColumn();

            PlaceStorySlices(request, mapRegion, chunkX, chunkZ);
            // Scattered roll wired up in Task 7.
        }

        private void PlaceStorySlices(IChunkColumnGenerateRequest request, IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            var chunks = request.Chunks;

            foreach (var (code, loc) in storyLocations)
            {
                if (!loc.Location.Intersects(tmpCuboid)) continue;

                var def = scfg.Structures.FirstOrDefault(s => s.Code == code);
                if (def == null || def.schematicData == null) continue;

                if (!loc.DidGenerate)
                {
                    loc.DidGenerate = true;
                    LocationsDirty = true;
                }

                var startPos = new BlockPos(loc.Location.X1, loc.Location.Y1, loc.Location.Z1, 0);

                if (!ResolveStoryStartY(def, loc, request, ref startPos)) continue;

                int blocksPlaced = def.schematicData.PlacePartial(
                    chunks, worldgenBlockAccessor, api.World,
                    chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll,
                    ToBaseGamePlacement(def.Placement),
                    GlobalConfig.ReplaceMetaBlocks, GlobalConfig.ReplaceMetaBlocks,
                    null, Array.Empty<int>(), null, def.DisableSurfaceTerrainBlending
                );

                if (blocksPlaced <= 0) continue;

                EmitRegionRecord(mapRegion, def, loc.Location);
                EmitLandClaims(def, loc.Location);

                TriggerOnStructurePlaced(def, chunkX, chunkZ, loc.Location, blocksPlaced);
            }
        }

        /// <summary>
        /// Resolves Y for a story-structure chunk slice. Returns false if Y is not yet
        /// resolvable (e.g. the origin chunk hasn't generated yet).
        /// </summary>
        private bool ResolveStoryStartY(SeafarerStructure def, SeafarerStructureLocation loc, IChunkColumnGenerateRequest request, ref BlockPos startPos)
        {
            int chunkX = request.ChunkX;
            int chunkZ = request.ChunkZ;

            bool needsTerrainY = def.Placement is EnumSeafarerPlacement.SurfaceRuin
                                 or EnumSeafarerPlacement.Coastal
                                 or EnumSeafarerPlacement.Underwater
                                 || def.UseWorldgenHeight;

            if (!needsTerrainY)
            {
                // Surface / OceanSurface: sea level anchored.
                int y = api.World.SeaLevel + def.schematicData.OffsetY;
                loc.Location.Y1 = y;
                loc.Location.Y2 = y + def.schematicData.SizeY;
                startPos.Y = y;
                LocationsDirty = true;
                return true;
            }

            if (loc.WorldgenHeight >= 0)
            {
                startPos.Y = ResolveYFromTerrain(def, loc.WorldgenHeight);
                return true;
            }

            // Need terrain height at origin. Only the origin chunk owns it.
            int originChunkX = loc.Location.X1 / chunksize;
            int originChunkZ = loc.Location.Z1 / chunksize;
            if (chunkX != originChunkX || chunkZ != originChunkZ) return false;

            int localX = loc.Location.X1 - originChunkX * chunksize;
            int localZ = loc.Location.Z1 - originChunkZ * chunksize;
            if (localX < 0 || localX >= chunksize || localZ < 0 || localZ >= chunksize) return false;

            int terrainHeight = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
            loc.WorldgenHeight = terrainHeight;
            int resolvedY = ResolveYFromTerrain(def, terrainHeight);
            loc.Location.Y1 = resolvedY;
            loc.Location.Y2 = resolvedY + def.schematicData.SizeY;
            startPos.Y = resolvedY;
            LocationsDirty = true;
            return true;
        }

        private int ResolveYFromTerrain(SeafarerStructure def, int terrainHeight)
        {
            return def.Placement switch
            {
                EnumSeafarerPlacement.SurfaceRuin => terrainHeight - def.schematicData.SizeY + def.schematicData.OffsetY,
                EnumSeafarerPlacement.Coastal     => terrainHeight + def.schematicData.OffsetY,
                EnumSeafarerPlacement.Underwater  => terrainHeight + def.schematicData.OffsetY,
                EnumSeafarerPlacement.OceanSurface => api.World.SeaLevel + def.schematicData.OffsetY,
                _ /* Surface */                   => api.World.SeaLevel + def.schematicData.OffsetY,
            };
        }

        private static EnumStructurePlacement ToBaseGamePlacement(EnumSeafarerPlacement p)
        {
            return p switch
            {
                EnumSeafarerPlacement.Underwater    => EnumStructurePlacement.Underwater,
                EnumSeafarerPlacement.SurfaceRuin   => EnumStructurePlacement.SurfaceRuin,
                EnumSeafarerPlacement.Coastal       => EnumStructurePlacement.Surface,
                EnumSeafarerPlacement.OceanSurface  => EnumStructurePlacement.Surface,
                _                                   => EnumStructurePlacement.Surface,
            };
        }

        private void EmitRegionRecord(IMapRegion mapRegion, SeafarerStructure def, Cuboidi bounds)
        {
            var existing = mapRegion.GeneratedStructures.FirstOrDefault(g => g.Code == def.Code);
            if (existing != null) return;

            mapRegion.AddGeneratedStructure(new GeneratedStructure
            {
                Code = def.Code,
                Group = "seafarerstructure",
                Location = bounds.Clone(),
                SuppressTreesAndShrubs = def.SuppressTrees,
                SuppressRivulets = true
            });
        }

        private void EmitLandClaims(SeafarerStructure def, Cuboidi bounds)
        {
            if (!def.BuildProtected) return;

            if (!def.ExcludeSchematicSizeProtect)
            {
                var claims = api.World.Claims.Get(bounds.Center.AsBlockPos);
                if (claims == null || claims.Length == 0)
                {
                    api.World.Claims.Add(new LandClaim
                    {
                        Areas = new List<Cuboidi> { bounds.Clone() },
                        Description = def.BuildProtectionDesc,
                        ProtectionLevel = def.ProtectionLevel,
                        LastKnownOwnerName = def.BuildProtectionName,
                        AllowUseEveryone = def.AllowUseEveryone,
                        AllowTraverseEveryone = def.AllowTraverseEveryone
                    });
                }
            }

            if (def.ExtraLandClaimX > 0 && def.ExtraLandClaimZ > 0)
            {
                var center = bounds.Center;
                var extra = new Cuboidi(
                    center.X - def.ExtraLandClaimX, 0, center.Z - def.ExtraLandClaimZ,
                    center.X + def.ExtraLandClaimX, api.WorldManager.MapSizeY, center.Z + def.ExtraLandClaimZ);
                var claims = api.World.Claims.Get(extra.Center.AsBlockPos);
                if (claims == null || claims.Length == 0)
                {
                    api.World.Claims.Add(new LandClaim
                    {
                        Areas = new List<Cuboidi> { extra },
                        Description = def.BuildProtectionDesc,
                        ProtectionLevel = def.ProtectionLevel,
                        LastKnownOwnerName = def.BuildProtectionName,
                        AllowUseEveryone = def.AllowUseEveryone,
                        AllowTraverseEveryone = def.AllowTraverseEveryone
                    });
                }
            }

            if (def.CustomLandClaims != null)
            {
                foreach (var shape in def.CustomLandClaims)
                {
                    var c = shape.Clone();
                    c.X1 += bounds.X1; c.X2 += bounds.X1;
                    c.Y1 += bounds.Y1; c.Y2 += bounds.Y1;
                    c.Z1 += bounds.Z1; c.Z2 += bounds.Z1;

                    var claims = api.World.Claims.Get(c.Center.AsBlockPos);
                    if (claims != null && claims.Length > 0) continue;

                    api.World.Claims.Add(new LandClaim
                    {
                        Areas = new List<Cuboidi> { c },
                        Description = def.BuildProtectionDesc,
                        ProtectionLevel = def.ProtectionLevel,
                        LastKnownOwnerName = def.BuildProtectionName,
                        AllowUseEveryone = def.AllowUseEveryone,
                        AllowTraverseEveryone = def.AllowTraverseEveryone
                    });
                }
            }
        }
```

- [ ] **Step 2: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors. Warnings about `chunksize` usage should not appear — it's inherited from `ModStdWorldGen`.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): per-chunk story placement with mode-aware Y resolution

PlaceStorySlices iterates story locations intersecting the current
chunk, resolves Y from terrain for Coastal/Underwater/SurfaceRuin
(waits for the origin chunk to generate) or from sea level for
Surface/OceanSurface, calls PlacePartial, and emits GeneratedStructure
and land-claim records. Fires OnStructurePlaced for future decorators.

ToBaseGamePlacement collapses our five modes to the three PlacePartial
understands for block-layer replacement purposes. Our own Y math still
controls vertical placement.

Behavior is still a no-op end-to-end because the config is empty —
Task 9 migrates the existing entries.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Ocean validation (deferred from determination to chunk Y-resolve)

Add OceanMap/BeachMap sampling and wire it into `ResolveStoryStartY`. If the candidate origin fails `requireOcean` / `requireCoast` / depth bounds, drop the story location and report it.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`

- [ ] **Step 1: Add OceanMap/BeachMap sampling helpers**

Inside `GenSeafarerStructures`, add:

```csharp
        private int RegionChunkSize => api.WorldManager.RegionSize / chunksize;

        private float GetOceanicity(IMapRegion mapRegion, int posX, int posZ)
        {
            if (mapRegion?.OceanMap == null || mapRegion.OceanMap.Data.Length == 0) return 0f;
            return SampleRegionMap(mapRegion.OceanMap, posX, posZ);
        }

        private float GetBeachStrength(IMapRegion mapRegion, int posX, int posZ)
        {
            if (mapRegion?.BeachMap == null || mapRegion.BeachMap.Data.Length == 0) return 0f;
            return SampleRegionMap(mapRegion.BeachMap, posX, posZ);
        }

        private float SampleRegionMap(IntDataMap2D map, int posX, int posZ)
        {
            int rlX = (posX / chunksize) % RegionChunkSize;
            int rlZ = (posZ / chunksize) % RegionChunkSize;
            if (rlX < 0) rlX += RegionChunkSize;
            if (rlZ < 0) rlZ += RegionChunkSize;

            float fac = (float)map.InnerSize / RegionChunkSize;
            int ul = map.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac));
            int ur = map.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac));
            int bl = map.GetUnpaddedInt((int)(rlX * fac), (int)(rlZ * fac + fac));
            int br = map.GetUnpaddedInt((int)(rlX * fac + fac), (int)(rlZ * fac + fac));
            return GameMath.BiLerp(ul, ur, bl, br,
                (float)(((posX % chunksize) + chunksize) % chunksize) / chunksize,
                (float)(((posZ % chunksize) + chunksize) % chunksize) / chunksize);
        }

        /// <summary>
        /// 9-sample footprint check against OceanMap. Returns true if the candidate
        /// origin satisfies the def's ocean/coast/depth requirements.
        /// </summary>
        private bool ValidateOceanPlacement(SeafarerStructure def, IMapRegion mapRegion, int originX, int originZ, int sizeX, int sizeZ, int terrainHeight)
        {
            bool wantsOceanCheck = def.RequireOcean
                                   || def.RequireCoast
                                   || def.Placement is EnumSeafarerPlacement.Underwater or EnumSeafarerPlacement.OceanSurface
                                   || def.MinWaterDepth > 0
                                   || def.MaxWaterDepth < 255;

            if (!wantsOceanCheck) return true;

            int cx = originX + sizeX / 2;
            int cz = originZ + sizeZ / 2;
            int seaLevel = api.World.SeaLevel;
            int waterDepth = seaLevel - terrainHeight;

            if (def.MinWaterDepth > 0 && waterDepth < def.MinWaterDepth) return false;
            if (def.MaxWaterDepth < 255 && waterDepth > def.MaxWaterDepth) return false;

            float centerOcean = GetOceanicity(mapRegion, cx, cz);
            bool centerIsOcean = centerOcean > 0;

            if (def.RequireCoast)
            {
                float beach = GetBeachStrength(mapRegion, cx, cz);
                bool coast = beach > 0 || (centerIsOcean && waterDepth <= def.MaxWaterDepth);
                if (!coast) return false;
            }

            if (def.RequireOcean || def.Placement is EnumSeafarerPlacement.Underwater or EnumSeafarerPlacement.OceanSurface)
            {
                if (!centerIsOcean) return false;

                int oceanSamples = 0;
                int[,] offsets = new int[8, 2]
                {
                    { originX, originZ },
                    { originX + sizeX, originZ },
                    { originX, originZ + sizeZ },
                    { originX + sizeX, originZ + sizeZ },
                    { cx, originZ },
                    { cx, originZ + sizeZ },
                    { originX, cz },
                    { originX + sizeX, cz },
                };
                for (int i = 0; i < 8; i++)
                {
                    if (GetOceanicity(mapRegion, offsets[i, 0], offsets[i, 1]) > 0) oceanSamples++;
                }
                // center + at least 6 of 8 edge samples must be ocean
                if (oceanSamples < 6) return false;
            }

            return true;
        }
```

- [ ] **Step 2: Wire validation into `ResolveStoryStartY`**

In `ResolveStoryStartY`, find the block after `loc.WorldgenHeight = terrainHeight;` (where we compute `resolvedY`). Replace:

```csharp
            int terrainHeight = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
            loc.WorldgenHeight = terrainHeight;
            int resolvedY = ResolveYFromTerrain(def, terrainHeight);
            loc.Location.Y1 = resolvedY;
            loc.Location.Y2 = resolvedY + def.schematicData.SizeY;
            startPos.Y = resolvedY;
            LocationsDirty = true;
            return true;
```

with:

```csharp
            int terrainHeight = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
            var mapRegion = request.Chunks[0].MapChunk.MapRegion;

            if (!loc.OceanValidated)
            {
                bool ok = ValidateOceanPlacement(def, mapRegion, loc.Location.X1, loc.Location.Z1,
                    def.schematicData.SizeX, def.schematicData.SizeZ, terrainHeight);
                if (!ok)
                {
                    api.Logger.Warning(
                        "Seafarer story structure '{0}': per-chunk ocean validation failed (waterDepth={1}); dropping reservation.",
                        def.Code, api.World.SeaLevel - terrainHeight);
                    storyLocations.Remove(def.Code);
                    FailedToGenerateLocation = true;
                    LocationsDirty = true;
                    return false;
                }
                loc.OceanValidated = true;
            }

            loc.WorldgenHeight = terrainHeight;
            int resolvedY = ResolveYFromTerrain(def, terrainHeight);
            loc.Location.Y1 = resolvedY;
            loc.Location.Y2 = resolvedY + def.schematicData.SizeY;
            startPos.Y = resolvedY;
            LocationsDirty = true;
            return true;
```

Also update the non-terrain branch in the same method — the `if (!needsTerrainY)` block — to run ocean validation for `OceanSurface` (which sits at sea level but still needs ocean water below it). Replace the existing `if (!needsTerrainY) { ... }` block with:

```csharp
            if (!needsTerrainY)
            {
                if (def.Placement == EnumSeafarerPlacement.OceanSurface && !loc.OceanValidated)
                {
                    int originChunkX0 = loc.Location.X1 / chunksize;
                    int originChunkZ0 = loc.Location.Z1 / chunksize;
                    if (chunkX != originChunkX0 || chunkZ != originChunkZ0) return false;

                    int localX0 = loc.Location.X1 - originChunkX0 * chunksize;
                    int localZ0 = loc.Location.Z1 - originChunkZ0 * chunksize;
                    if (localX0 < 0 || localX0 >= chunksize || localZ0 < 0 || localZ0 >= chunksize) return false;

                    var mapRegion = request.Chunks[0].MapChunk.MapRegion;
                    int th = request.Chunks[0].MapChunk.WorldGenTerrainHeightMap[localZ0 * chunksize + localX0];
                    if (!ValidateOceanPlacement(def, mapRegion, loc.Location.X1, loc.Location.Z1,
                            def.schematicData.SizeX, def.schematicData.SizeZ, th))
                    {
                        api.Logger.Warning(
                            "Seafarer story structure '{0}': OceanSurface ocean check failed; dropping reservation.", def.Code);
                        storyLocations.Remove(def.Code);
                        FailedToGenerateLocation = true;
                        LocationsDirty = true;
                        return false;
                    }
                    loc.OceanValidated = true;
                }

                int y = api.World.SeaLevel + def.schematicData.OffsetY;
                loc.Location.Y1 = y;
                loc.Location.Y2 = y + def.schematicData.SizeY;
                startPos.Y = y;
                LocationsDirty = true;
                return true;
            }
```

- [ ] **Step 3: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): ocean validation at story-structure chunk resolve

Samples OceanMap in a 9-point grid across the footprint (center + 4
corners + 4 edge midpoints). RequireOcean gates on center-plus-≥6
samples ocean; RequireCoast accepts beach OR shallow-ocean at center;
depth band enforced from terrain height vs. sea level.

Validation runs the first time a story structure's origin chunk
generates, using the WorldGenTerrainHeightMap that's now available.
Validation failure drops the reservation and reports via listmissing —
admin can relocate with /wgen seafarer setpos (command in Task 8).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Footprint clearing (Tortuga fix)

Add `ClearFootprint` and invoke it before `PlacePartial` when `def.ClearFootprint` is true. Solid-layer air-fill the structure's XYZ bounding box clipped to the current chunk.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`

- [ ] **Step 1: Add `ClearFootprint` helper**

Inside `GenSeafarerStructures`, add:

```csharp
        private int cachedAirBlockId = -1;
        private int AirBlockId
        {
            get
            {
                if (cachedAirBlockId == -1)
                {
                    var air = api.World.GetBlock(new AssetLocation("air"));
                    cachedAirBlockId = air?.Id ?? 0;
                }
                return cachedAirBlockId;
            }
        }

        /// <summary>
        /// Fills the structure's XYZ bounding box (clipped to the current chunk)
        /// with air on the solid layer. Runs before PlacePartial so the schematic's
        /// blocks overwrite the cleared region and interior negative space stays open.
        /// </summary>
        private void ClearFootprint(IServerChunk[] chunks, Cuboidi bounds, int chunkX, int chunkZ)
        {
            int chunkMinX = chunkX * chunksize;
            int chunkMinZ = chunkZ * chunksize;
            int clipMinX = Math.Max(bounds.X1, chunkMinX);
            int clipMaxX = Math.Min(bounds.X2, chunkMinX + chunksize);
            int clipMinZ = Math.Max(bounds.Z1, chunkMinZ);
            int clipMaxZ = Math.Min(bounds.Z2, chunkMinZ + chunksize);
            if (clipMinX >= clipMaxX || clipMinZ >= clipMaxZ) return;

            int mapSizeY = api.WorldManager.MapSizeY;
            int clipMinY = Math.Max(bounds.Y1, 0);
            int clipMaxY = Math.Min(bounds.Y2, mapSizeY);
            if (clipMinY >= clipMaxY) return;

            int air = AirBlockId;

            for (int y = clipMinY; y < clipMaxY; y++)
            {
                int chunkIndex = y / chunksize;
                if (chunkIndex < 0 || chunkIndex >= chunks.Length) continue;
                int localY = y % chunksize;
                var chunk = chunks[chunkIndex];

                for (int x = clipMinX; x < clipMaxX; x++)
                {
                    int localX = x - chunkMinX;
                    for (int z = clipMinZ; z < clipMaxZ; z++)
                    {
                        int localZ = z - chunkMinZ;
                        int blockIndex = (localY * chunksize + localZ) * chunksize + localX;
                        chunk.Data.SetBlockIdUnsafe(blockIndex, air);
                    }
                }
            }
        }
```

- [ ] **Step 2: Wire `ClearFootprint` into `PlaceStorySlices`**

In `PlaceStorySlices`, find the block:

```csharp
                int blocksPlaced = def.schematicData.PlacePartial(
                    chunks, worldgenBlockAccessor, api.World,
                    chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll,
                    ToBaseGamePlacement(def.Placement),
                    GlobalConfig.ReplaceMetaBlocks, GlobalConfig.ReplaceMetaBlocks,
                    null, Array.Empty<int>(), null, def.DisableSurfaceTerrainBlending
                );
```

Insert the footprint clear immediately before this call:

```csharp
                if (def.ClearFootprint)
                {
                    ClearFootprint(chunks, loc.Location, chunkX, chunkZ);
                }

                int blocksPlaced = def.schematicData.PlacePartial(
                    chunks, worldgenBlockAccessor, api.World,
                    chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll,
                    ToBaseGamePlacement(def.Placement),
                    GlobalConfig.ReplaceMetaBlocks, GlobalConfig.ReplaceMetaBlocks,
                    null, Array.Empty<int>(), null, def.DisableSurfaceTerrainBlending
                );
```

- [ ] **Step 3: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors. If `chunk.Data.SetBlockIdUnsafe` is not the correct method name on `IServerChunk`, consult VS API `IServerChunk` / `IChunkBlocks` and swap to the correct accessor — candidates are `chunk.Blocks[index] = air;` or `chunk.SetBlockUnsafe(index, air);`. Fix in place, rebuild.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): opt-in footprint clearing for coastal structures

When def.ClearFootprint is true, the structure's XYZ bounding box
(clipped to current chunk) is air-filled on the solid layer before
PlacePartial runs. The schematic's blocks then overwrite cleared
cells, and interior empty space stays open — fixes Tortuga merging
with island terrain without needing author-stamped air meta-blocks.

Off by default. Enable on structures that sit on landforms with
large interior negative space.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Scattered structure roll path

Add the chance-per-chunk path for non-story entries. Same `PlacePartial` + region/claim emission as the story path, but without persistence.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`

- [ ] **Step 1: Add `PlaceScatteredRolls`**

Inside `GenSeafarerStructures`, add:

```csharp
        private void PlaceScatteredRolls(IChunkColumnGenerateRequest request, IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            var chunks = request.Chunks;
            var mapChunk = chunks[0].MapChunk;

            foreach (var def in scfg.Structures)
            {
                if (def.StoryStructure) continue;
                if (def.schematicData == null) continue;
                if (def.Chance <= 0f) continue;

                strucRand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
                float roll = strucRand.NextFloat();
                if (roll > def.Chance) continue;

                int localX = strucRand.NextInt(chunksize);
                int localZ = strucRand.NextInt(chunksize);
                int posX = chunkX * chunksize + localX;
                int posZ = chunkZ * chunksize + localZ;

                int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
                if (!ValidateOceanPlacement(def, mapRegion, posX - def.schematicData.SizeX / 2, posZ - def.schematicData.SizeZ / 2,
                        def.schematicData.SizeX, def.schematicData.SizeZ, terrainHeight))
                {
                    continue;
                }

                int originX = posX - def.schematicData.SizeX / 2;
                int originZ = posZ - def.schematicData.SizeZ / 2;
                int y = ResolveYFromTerrain(def, terrainHeight);
                var startPos = new BlockPos(originX, y, originZ, 0);
                var bounds = new Cuboidi(
                    originX, y, originZ,
                    originX + def.schematicData.SizeX, y + def.schematicData.SizeY, originZ + def.schematicData.SizeZ);

                if (def.ClearFootprint)
                {
                    ClearFootprint(chunks, bounds, chunkX, chunkZ);
                }

                int blocksPlaced = def.schematicData.PlacePartial(
                    chunks, worldgenBlockAccessor, api.World,
                    chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll,
                    ToBaseGamePlacement(def.Placement),
                    GlobalConfig.ReplaceMetaBlocks, GlobalConfig.ReplaceMetaBlocks,
                    null, Array.Empty<int>(), null, def.DisableSurfaceTerrainBlending
                );

                if (blocksPlaced <= 0) continue;

                EmitRegionRecord(mapRegion, def, bounds);
                EmitLandClaims(def, bounds);
                TriggerOnStructurePlaced(def, chunkX, chunkZ, bounds, blocksPlaced);
            }
        }
```

- [ ] **Step 2: Call it from `OnChunkColumnGen`**

In `OnChunkColumnGen`, replace:

```csharp
            PlaceStorySlices(request, mapRegion, chunkX, chunkZ);
            // Scattered roll wired up in Task 7.
```

with:

```csharp
            PlaceStorySlices(request, mapRegion, chunkX, chunkZ);
            PlaceScatteredRolls(request, mapRegion, chunkX, chunkZ);
```

- [ ] **Step 3: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): scattered-structure chance-per-chunk placement

Mirrors the story path minus persistence: seed RNG from (chunkX, chunkZ,
code), roll chance, pick random local XZ, run ocean validation,
ResolveYFromTerrain, optional ClearFootprint, PlacePartial, emit
records, fire OnStructurePlaced. Same def type — only storyStructure
flag and persistence differ.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: Commands (`/wgen seafarer` subtree)

Add `tp`, `setpos`, `listmissing`, `rmsc`, `place`, `list` subcommands mirroring the base-game `/wgen story` pattern, plus the root aliases.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs`

- [ ] **Step 1: Extend `StartServerSide` with command registration**

At the end of `StartServerSide` (after the `serverChannel.RegisterMessageType<SeafarerGenFailed>();` line), add:

```csharp
            var parsers = api.ChatCommands.Parsers;
            api.ChatCommands.GetOrCreate("wgen")
                .BeginSubCommand("seafarer")
                    .BeginSubCommand("tp")
                        .WithRootAlias("tpseafarerloc")
                        .WithDescription("Teleport to a Seafarer story structure")
                        .RequiresPrivilege(Privilege.controlserver)
                        .RequiresPlayer()
                        .WithArgs(parsers.Word("code"))
                        .HandleWith(OnCmdTp)
                    .EndSubCommand()
                    .BeginSubCommand("setpos")
                        .WithRootAlias("setseafarerstrucpos")
                        .WithDescription("Set the location of a Seafarer story structure")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("code"), parsers.WorldPosition("position"), parsers.OptionalBool("confirm"))
                        .HandleWith(OnCmdSetPos)
                    .EndSubCommand()
                    .BeginSubCommand("listmissing")
                        .WithDescription("List Seafarer story structures that failed to determine or validate")
                        .RequiresPrivilege(Privilege.controlserver)
                        .HandleWith(OnCmdListMissing)
                    .EndSubCommand()
                    .BeginSubCommand("rmsc")
                        .WithDescription("Reset a Seafarer story structure's generation state so /wgen regen will re-place it")
                        .RequiresPrivilege(Privilege.controlserver)
                        .WithArgs(parsers.Word("code"))
                        .HandleWith(OnCmdRmsc)
                    .EndSubCommand()
                    .BeginSubCommand("place")
                        .WithDescription("Force-place a Seafarer structure at your position (debug)")
                        .RequiresPrivilege(Privilege.controlserver)
                        .RequiresPlayer()
                        .WithArgs(parsers.Word("code"))
                        .HandleWith(OnCmdPlace)
                    .EndSubCommand()
                    .BeginSubCommand("list")
                        .WithDescription("List all registered Seafarer structure codes and status")
                        .HandleWith(OnCmdList)
                    .EndSubCommand()
                .EndSubCommand();
```

- [ ] **Step 2: Add the command handlers**

Inside `GenSeafarerStructures`, add:

```csharp
        private TextCommandResult OnCmdTp(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            if (!storyLocations.TryGetValue(code, out var loc))
                return TextCommandResult.Error("No such Seafarer story structure: " + code);
            var pos = loc.CenterPos.Copy();
            pos.Y = (loc.Location.Y1 + loc.Location.Y2) / 2;
            args.Caller.Entity.TeleportTo(pos);
            return TextCommandResult.Success("Teleporting to " + code);
        }

        private TextCommandResult OnCmdSetPos(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            var def = scfg.Structures.FirstOrDefault(s => s.Code == code);
            if (def == null) return TextCommandResult.Error("No such Seafarer structure: " + code);
            if (def.schematicData == null) return TextCommandResult.Error("Structure has no schematic loaded: " + code);

            bool confirm = args[2] is bool b && b;
            var pos = ((Vec3d)args[1]).AsBlockPos;

            if (!confirm)
            {
                var chunkRange = (int)Math.Ceiling(def.LandformRadius / (float)chunksize) + 3;
                return TextCommandResult.Success(
                    $"Will move '{code}' to {pos}. Add 'true' to confirm. After, regenerate chunks in the area (e.g. /wgen delr {chunkRange}).");
            }

            int schemX = def.schematicData.SizeX;
            int schemZ = def.schematicData.SizeZ;
            int minX = pos.X - schemX / 2;
            int minZ = pos.Z - schemZ / 2;
            var cub = new Cuboidi(minX, pos.Y, minZ, minX + schemX, pos.Y + def.schematicData.SizeY, minZ + schemZ);

            var entry = new SeafarerStructureLocation
            {
                Code = code,
                CenterPos = pos,
                Location = cub,
                LandformRadius = def.LandformRadius,
                GenerationRadius = def.GenerationRadius,
                SkipGenerationFlags = def.SkipGenerationFlags,
                OceanValidated = true
            };
            if (entry.SkipGenerationFlags != null && entry.SkipGenerationFlags.Count > 0)
            {
                int max = entry.SkipGenerationFlags.Max(f => f.Value);
                entry.MaxSkipGenerationRadiusSq = max * max;
            }
            storyLocations[code] = entry;
            LocationsDirty = true;

            return TextCommandResult.Success($"Moved '{code}' to {pos}. Regenerate chunks in this area to materialize it.");
        }

        private TextCommandResult OnCmdListMissing(TextCommandCallingArgs args)
        {
            var missing = GetMissingStructures();
            if (missing.Count == 0) return TextCommandResult.Success("No missing Seafarer structures.");
            if (args.Caller.Player is IServerPlayer p)
            {
                serverChannel.SendPacket(new SeafarerGenFailed { MissingStructures = missing }, p);
            }
            return TextCommandResult.Success("Missing: " + string.Join(", ", missing));
        }

        private TextCommandResult OnCmdRmsc(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            if (!storyLocations.TryGetValue(code, out var loc))
                return TextCommandResult.Error("No such Seafarer story structure: " + code);
            loc.DidGenerate = false;
            loc.WorldgenHeight = -1;
            loc.OceanValidated = false;
            loc.RockBlockCode = null;
            LocationsDirty = true;
            return TextCommandResult.Success($"Reset generation state for '{code}'.");
        }

        private TextCommandResult OnCmdPlace(TextCommandCallingArgs args)
        {
            var code = (string)args[0];
            var def = scfg.Structures.FirstOrDefault(s => s.Code == code);
            if (def == null || def.schematicData == null)
                return TextCommandResult.Error("Unknown or unloaded structure: " + code);

            var player = args.Caller.Player as IServerPlayer;
            var pos = player.CurrentBlockSelection?.Position ?? player.Entity.Pos.AsBlockPos;

            int placed = def.schematicData.Place(api.World.BlockAccessor, api.World, pos, EnumReplaceMode.ReplaceAll, true);
            api.World.BlockAccessor.Commit();
            return TextCommandResult.Success($"Placed '{code}' at {pos} ({placed} blocks).");
        }

        private TextCommandResult OnCmdList(TextCommandCallingArgs args)
        {
            if (scfg == null || scfg.Structures.Length == 0)
                return TextCommandResult.Success("No Seafarer structures configured.");
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Seafarer structures:");
            foreach (var def in scfg.Structures)
            {
                bool loaded = def.schematicData != null;
                string status = def.StoryStructure
                    ? (storyLocations.ContainsKey(def.Code) ? "determined" : (attemptedCodes.Contains(def.Code) ? "FAILED" : "pending"))
                    : $"scattered (chance={def.Chance})";
                sb.AppendLine($"  {def.Code} — {def.Placement}, {status}{(loaded ? "" : " [NO SCHEMATIC]")}");
            }
            return TextCommandResult.Success(sb.ToString());
        }
```

- [ ] **Step 3: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): /wgen seafarer command subtree

Commands: tp, setpos, listmissing, rmsc, place, list. Mirrors base-game
/wgen story semantics and adds a debug /wgen seafarer place for
in-place testing without waiting for chunk-gen. Root aliases
/tpseafarerloc and /setseafarerstrucpos match the base-game
convenience pattern.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Config migration and cutover

Migrate all entries from `storystructures.json` and `oceanstructures.json` into the new `seafarerstructures.json`. Delete the old JSON files and `GenOceanStructures.cs`. Run asset validation. In-game verification of all four structures (Tortuga, Potato-King, Crimson Rose, wreck-one).

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json`
- Delete: `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json`
- Delete: `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`
- Delete: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Write the migrated config**

Overwrite `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json` with:

```json
{
  "schematicYOffsets": {
    "story/seafarer:potato-king-house": 0,
    "story/seafarer:tortuga": -27,
    "story/seafarer:wreck-crimson-rose": 0
  },
  "rocktypeRemapGroups": {},
  "structures": [
    {
      "code": "potatoking",
      "group": "seafarerstructure",
      "name": "Potato King's House",
      "schematics": ["surface/potato-king-house"],
      "placement": "Surface",
      "storyStructure": true,
      "useWorldgenHeight": true,
      "disableSurfaceTerrainBlending": true,
      "dependsOnStructure": "spawn",
      "minSpawnDistX": -2500, "maxSpawnDistX": 2500,
      "minSpawnDistZ": -2500, "maxSpawnDistZ": 2500,
      "requireLandform": "veryflat",
      "landformRadius": 80,
      "generateGrass": true,
      "buildProtected": true,
      "protectionLevel": 10,
      "buildProtectionName": "custommessage-potatoking",
      "buildProtectionDesc": "Potato King's House",
      "allowUseEveryone": false,
      "allowTraverseEveryone": true,
      "skipGenerationCategories": {
        "structures": 80, "trees": 50, "shrubs": 50,
        "hotsprings": 100, "patches": 30
      }
    },
    {
      "code": "tortuga",
      "group": "seafarerstructure",
      "name": "Tortuga",
      "schematics": ["costal/tortuga"],
      "placement": "Coastal",
      "storyStructure": true,
      "clearFootprint": true,
      "excludeSchematicSizeProtect": true,
      "disableSurfaceTerrainBlending": true,
      "dependsOnStructure": "spawn",
      "minSpawnDistX": -3000, "maxSpawnDistX": 3000,
      "minSpawnDistZ": -3000, "maxSpawnDistZ": 3000,
      "requireCoast": true,
      "requireLandform": "shallowislands",
      "landformRadius": 10,
      "generateGrass": true,
      "buildProtected": true,
      "protectionLevel": 10,
      "buildProtectionName": "custommessage-tortuga",
      "buildProtectionDesc": "Tortuga — neutral port",
      "allowUseEveryone": true,
      "allowTraverseEveryone": true,
      "skipGenerationCategories": {
        "structures": 300, "trees": 200, "shrubs": 150,
        "hotsprings": 250, "patches": 100, "pond": 200, "rivulets": 100
      }
    },
    {
      "code": "wreck-crimsonrose",
      "group": "seafarerstructure",
      "name": "Wreck of the Crimson Rose",
      "schematics": ["underwater/wreck-crimson-rose"],
      "placement": "Underwater",
      "storyStructure": true,
      "useWorldgenHeight": true,
      "dependsOnStructure": "spawn",
      "minSpawnDistX": -3000, "maxSpawnDistX": 3000,
      "minSpawnDistZ": -3000, "maxSpawnDistZ": 3000,
      "requireOcean": true,
      "minWaterDepth": 10,
      "maxWaterDepth": 80,
      "landformRadius": 0,
      "skipGenerationCategories": {
        "structures": 80, "trees": 60, "shrubs": 60,
        "hotsprings": 80, "patches": 40
      }
    },
    {
      "code": "wreck-one",
      "group": "seafarerstructure",
      "schematics": ["underwater/wreck-one"],
      "placement": "Underwater",
      "storyStructure": false,
      "chance": 0.015,
      "requireOcean": true,
      "minWaterDepth": 10,
      "maxWaterDepth": 50,
      "suppressTrees": true,
      "randomRotation": true
    }
  ]
}
```

- [ ] **Step 2: Delete the old JSON files and old mod system**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer
rm Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json
rm Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json
rm Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
```

- [ ] **Step 3: Build**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -8
```
Expected: 0 errors. If any file still references types from the old `GenOceanStructures.cs` (e.g. `OceanStructureDef`, `GenOceanStructures`), fix the reference — either delete it if unused or migrate it to the new `SeafarerStructure` type.

- [ ] **Step 4: Run asset validation**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -20
```
Expected: exit 0, 0 errors. Warnings acceptable if pre-existing.

- [ ] **Step 5: Manual in-game verification**

Launch the game and create a **new** world (so fresh determination runs). Record the seed. Log in with `controlserver` privileges.

Verification checklist — check each and record pass/fail:

1. **Load-time log entries.** Server log shows:
   - `Seafarer structures: loaded 4 definitions.`
   - Four `Seafarer story structure '…' determined at (…)` lines for potatoking / tortuga / wreck-crimsonrose (unless the spawn-box happened to fail every attempt; wreck-one is scattered so won't log here).
2. **`/wgen seafarer list`** — prints all four codes with expected placement modes and statuses.
3. **Tortuga placement.** `/wgen seafarer tp tortuga`. Confirm:
   - Structure is on or adjacent to an island shore.
   - Interior rooms are not filled with dirt/stone (clearFootprint fix).
   - No landclaim issues — `/landclaim mine` near the center shows the tortuga claim.
4. **Potato King placement.** `/wgen seafarer tp potatoking`. Confirm the house sits flush on flat ground, no buried rooms.
5. **Crimson Rose placement.** `/wgen seafarer tp wreck-crimsonrose`. Confirm:
   - Wreck is on the seabed, not at y=1.
   - Water is present above it (wasn't placed on dry land).
6. **Scattered wrecks.** Fly over ocean for a few minutes (`/gamemode creative`, `/time speed 0.5` if needed). At least one `wreck-one` should appear. `/wgen seafarer list` is enough to confirm the def is loaded.
7. **/wgen seafarer listmissing** — should report none missing on a successful run.

If any step fails, capture the log and iterate. Do not commit until all seven pass.

- [ ] **Step 6: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add -A Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): migrate to GenSeafarerStructures, remove old system

Migrates tortuga / potato-king / wreck-crimsonrose / wreck-one into
seafarerstructures.json. Tortuga uses Coastal + clearFootprint
(fixes island-merge). Crimson Rose uses Underwater (Y now resolves
to seabed, not y=1). Wreck-one keeps chance-per-chunk via
storyStructure: false.

Deletes:
- GenOceanStructures.cs (replaced by GenSeafarerStructures)
- storystructures.json (stops base-game GenStoryStructures from
  hijacking our ocean entries — the root cause of the breakage)
- oceanstructures.json (merged into seafarerstructures.json)

Verified in a fresh world: all four structures generate at the right
Y, Tortuga interiors stay clear, wreck is fully underwater, and
/wgen seafarer list/listmissing/tp behave as expected.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-review notes

- **Spec coverage:** All ten spec build-sequence items map to tasks — 1→T1, 2→T2, 3→T3, 4–5→T4, 6→T5, 7→T6, 8→T7, 9→T8, 10→T9 step 2, 11→T9 step 5. Hooks (OnStructurePlaced) land in T2 (declaration) and T4 (invocation). Map-readiness (GeneratedStructure emission + public Locations dict) lands in T2+T4.
- **Type consistency:** `SeafarerStructureLocation.OceanValidated` (`[ProtoMember(11)]`) is introduced in T1 and consumed in T5 (per-chunk validation gate) and T8 (`setpos` sets it true). `OnStructurePlaced` event declared in T2 and fired in T4/T7. `EnumSeafarerPlacement` introduced in T1, consumed in T3/T4/T5/T7. `storyLocations` dict is the canonical name throughout.
- **Placeholders:** None. Every step has the exact code and commands. The one caveat is T6 Step 3's "if `chunk.Data.SetBlockIdUnsafe` is not the correct method name" — this is a known VS-API lookup that the executing agent resolves by swapping to the correct accessor (`chunk.Blocks[index] = air;` or `chunk.SetBlockUnsafe`). Noted explicitly so the agent doesn't stall.
