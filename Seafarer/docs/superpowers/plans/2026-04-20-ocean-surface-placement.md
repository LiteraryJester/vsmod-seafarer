# Ocean Surface Placement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `OceanSurface` placement mode to `GenOceanStructures` that reserves a validated coastal spot once and paints the schematic across multiple chunk-gen events via `BlockSchematicPartial.PlacePartial`, unblocking large ocean structures like Tortuga.

**Architecture:** Extend `GenOceanStructures.cs` with (a) a new `OceanSurface` enum value, (b) a `OceanStructureReservation` class + persistence, (c) an ocean-coverage validator sampling 9 points, (d) reservation-trigger + per-chunk-placement branches in `OnChunkColumnGen` using `BlockSchematicPartial.PlacePartial` for chunk-scoped writes. Then move Tortuga from `storystructures.json` → `oceanstructures.json` and switch `map-tortuga` back to `ItemOceanLocatorMap`.

**Tech Stack:** C# / .NET 10.0, Vintage Story API (`BlockSchematicPartial`, `IMapRegion.OceanMap`, `SerializerUtil`, `SaveGame.StoreData/GetData`)

**Spec:** `docs/superpowers/specs/2026-04-20-ocean-surface-placement-design.md`

**Project note:** Vintage Story mod. No automated test suite. Validation is `dotnet build` + `python3 validate-assets.py` + manual in-game check.

**Build command (from repo root):** `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`

---

## File Map

| File | Responsibility | Change |
|---|---|---|
| `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` | Ocean worldgen — extend with `OceanSurface` enum, reservation data + persistence, coverage validator, reservation + per-chunk placement logic | Modify |
| `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json` | Remove Tortuga entry + its `schematicYOffsets` entry | Modify |
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Add Tortuga entry with `placement: "oceansurface"` | Modify |
| `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` | Switch class from `ItemLocatorMap` back to `ItemOceanLocatorMap` | Modify |

---

### Task 1: Data-model extensions in `GenOceanStructures.cs`

Add the `OceanSurface` enum value, the `OceanStructureReservation` class, the `reservations` dictionary, save/load plumbing, and switch schematic deserialization from `BlockSchematic` to `BlockSchematicPartial` (required to call `PlacePartial`).

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Add `OceanSurface` to the enum**

In `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`, find the enum block (lines 11–16) and replace with:

```csharp
    public enum EnumOceanPlacement
    {
        Underwater,
        Coastal,
        BuriedUnderwater,
        OceanSurface
    }
```

- [ ] **Step 2: Add the `OceanStructureReservation` class**

Immediately after the enum (before `OceanStructureDef`), add:

```csharp
    public class OceanStructureReservation
    {
        public int OriginX;
        public int OriginY;
        public int OriginZ;
        public int VariantIndex;
        public int RotationIndex;
        public int SizeX;
        public int SizeY;
        public int SizeZ;
        public bool StructureRecorded;
    }
```

- [ ] **Step 3: Change schematic cache to `BlockSchematicPartial[][]` and add `reservations` dict**

Find the cache field declaration (lines 54–56) and the globalCounts block immediately after. Replace that whole block with:

```csharp
        // Key: structure code, Value: array of schematic variants.
        // Each variant is an array of 4 rotations (0, 90, 180, 270).
        // BlockSchematicPartial (not BlockSchematic) so we can call PlacePartial for chunk-scoped writes.
        private Dictionary<string, BlockSchematicPartial[][]> cachedSchematics = new();

        // World-global count of placed structures, keyed on def.Code.
        // Accessed from the chunk-gen worker thread and from save hooks, so guard with countsLock.
        private readonly Dictionary<string, int> globalCounts = new();
        // Persistent reservations for OceanSurface structures. Keyed on def.Code.
        private readonly Dictionary<string, OceanStructureReservation> reservations = new();
        private readonly object countsLock = new();
        private const string CountsDataKey = "seafarer-ocean-structure-counts";
        private const string ReservationsDataKey = "seafarer-ocean-reservations";
```

- [ ] **Step 4: Update `LoadConfig` to deserialize schematics as `BlockSchematicPartial`**

Find the `LoadConfig` method (around lines 106–162). In the foreach-schematicPath loop, locate:

```csharp
                    var baseSchematic = schematicAsset.ToObject<BlockSchematic>();
                    baseSchematic.Init(worldgenBlockAccessor);

                    var rotations = new BlockSchematic[4];
```

Replace with:

```csharp
                    var baseSchematic = schematicAsset.ToObject<BlockSchematicPartial>();
                    baseSchematic.Init(worldgenBlockAccessor);

                    var rotations = new BlockSchematicPartial[4];
```

And in the same loop, find:

```csharp
                        var copy = baseSchematic.ClonePacked();
                        copy.TransformWhilePacked(sapi.World, EnumOrigin.BottomCenter, r * 90);
                        copy.Init(worldgenBlockAccessor);
                        rotations[r] = copy;
```

Replace with (cast required since `ClonePacked` returns base type):

```csharp
                        var copy = (BlockSchematicPartial)baseSchematic.ClonePacked();
                        copy.TransformWhilePacked(sapi.World, EnumOrigin.BottomCenter, r * 90);
                        copy.Init(worldgenBlockAccessor);
                        rotations[r] = copy;
```

Also find and update the cached-variants list. Locate:

```csharp
                var variants = new List<BlockSchematic[]>();
```

Replace with:

```csharp
                var variants = new List<BlockSchematicPartial[]>();
```

- [ ] **Step 5: Extend `OnSaveGameLoaded` to load reservations**

Find `OnSaveGameLoaded` (around lines 361–376). Replace the whole method with:

```csharp
        private void OnSaveGameLoaded()
        {
            byte[] countsData = sapi.WorldManager.SaveGame.GetData(CountsDataKey);
            byte[] resData = sapi.WorldManager.SaveGame.GetData(ReservationsDataKey);
            lock (countsLock)
            {
                globalCounts.Clear();
                if (countsData != null)
                {
                    var loaded = SerializerUtil.Deserialize<Dictionary<string, int>>(countsData);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded) globalCounts[kv.Key] = kv.Value;
                    }
                }

                reservations.Clear();
                if (resData != null)
                {
                    var loaded = SerializerUtil.Deserialize<Dictionary<string, OceanStructureReservation>>(resData);
                    if (loaded != null)
                    {
                        foreach (var kv in loaded) reservations[kv.Key] = kv.Value;
                    }
                }
            }
        }
```

- [ ] **Step 6: Extend `OnGameWorldSave` to persist reservations**

Find `OnGameWorldSave` (immediately after `OnSaveGameLoaded`). Replace the whole method with:

```csharp
        private void OnGameWorldSave()
        {
            Dictionary<string, int> countsSnapshot;
            Dictionary<string, OceanStructureReservation> resSnapshot;
            lock (countsLock)
            {
                countsSnapshot = new Dictionary<string, int>(globalCounts);
                resSnapshot = new Dictionary<string, OceanStructureReservation>(reservations);
            }
            sapi.WorldManager.SaveGame.StoreData(CountsDataKey, SerializerUtil.Serialize(countsSnapshot));
            sapi.WorldManager.SaveGame.StoreData(ReservationsDataKey, SerializerUtil.Serialize(resSnapshot));
        }
```

- [ ] **Step 7: Update the admin `/ocean place` command for the new schematic type**

Find `OnCmdPlace` (near bottom of file, around lines 388–426). The line `var schematic = variantRotations[sapi.World.Rand.Next(4)];` currently typed as `BlockSchematic` — it'll auto-pick up the new type from the changed `cachedSchematics` dict. No code change needed in that method; verify the build succeeds, which confirms the type change is compatible.

- [ ] **Step 8: Build to verify the data-model changes compile**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds, 0 errors. (`Vintagestory.ServerMods` namespace contains `BlockSchematicPartial`; if the build fails with "type not found," add `using Vintagestory.ServerMods;` at the top of the file.)

- [ ] **Step 9: Commit**

```bash
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): extend GenOceanStructures data model for OceanSurface

Adds OceanSurface enum value, OceanStructureReservation class, the
reservations dictionary with save/load plumbing, and switches schematic
cache to BlockSchematicPartial so per-chunk PlacePartial calls are
available in the next task.

No behavior change yet - reservations dict is unused until the
placement logic is added.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Add ocean-coverage validator

Add `ValidateOceanCoverage` method that samples 9 points and applies the moderate rule (center + corners required, ≥ 7/9 total) plus center water-depth check.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Add the `ValidateOceanCoverage` method**

Immediately after the existing `GetBeachStrength` method, insert:

```csharp
        /// <summary>
        /// Validates that a candidate footprint has enough open water for OceanSurface placement.
        /// Moderate rule: center + 4 corners must be ocean; at least 7 of 9 samples ocean overall;
        /// center water depth within def.MinWaterDepth..MaxWaterDepth (if set).
        /// </summary>
        private bool ValidateOceanCoverage(IMapRegion mapRegion, int originX, int originZ, int sizeX, int sizeZ, OceanStructureDef def)
        {
            int cx = originX + sizeX / 2;
            int cz = originZ + sizeZ / 2;

            int[,] samples = new int[9, 2]
            {
                { cx, cz },                              // 0: center
                { originX, originZ },                    // 1-4: corners
                { originX + sizeX, originZ },
                { originX, originZ + sizeZ },
                { originX + sizeX, originZ + sizeZ },
                { cx, originZ },                         // 5-8: edge midpoints
                { cx, originZ + sizeZ },
                { originX, cz },
                { originX + sizeX, cz }
            };

            int oceanSamples = 0;
            bool centerOcean = false;
            bool cornersAllOcean = true;

            for (int i = 0; i < 9; i++)
            {
                float oceanicity = GetOceanicity(mapRegion, samples[i, 0], samples[i, 1]);
                bool isOcean = oceanicity > 0;
                if (isOcean) oceanSamples++;

                if (i == 0) centerOcean = isOcean;
                else if (i <= 4 && !isOcean) cornersAllOcean = false;
            }

            if (!centerOcean || !cornersAllOcean) return false;
            if (oceanSamples < 7) return false;

            // Center water-depth check uses the current map chunk's heightmap, which may not be
            // available if the center is in a neighbor chunk. Accept as valid in that case;
            // OceanMap sampling already gave us coarse confidence.
            int seaLevel = sapi.World.SeaLevel;
            var centerMapChunk = sapi.WorldManager.GetMapChunk(cx / chunksize, cz / chunksize);
            if (centerMapChunk == null) return true;

            int terrainHeight = centerMapChunk.WorldGenTerrainHeightMap[(cz % chunksize) * chunksize + (cx % chunksize)];
            int waterDepth = seaLevel - terrainHeight;
            if (def.MinWaterDepth > 0 && waterDepth < def.MinWaterDepth) return false;
            if (def.MaxWaterDepth > 0 && def.MaxWaterDepth < 255 && waterDepth > def.MaxWaterDepth) return false;

            return true;
        }
```

- [ ] **Step 2: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): ValidateOceanCoverage samples 9 points + water depth

Used by the OceanSurface reservation flow (added in next commit) to
reject candidate locations whose footprint overlaps too much land or
has wrong water depth. Moderate rule: center + corners required;
>= 7 of 9 samples must be ocean.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Wire reservation + per-chunk placement into `OnChunkColumnGen`

Split the chunk-gen handler into (a) existing per-chunk placement for legacy modes, (b) reservation logic for `OceanSurface` + per-chunk `PlacePartial` for existing reservations.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Replace `OnChunkColumnGen` with the version that dispatches by placement mode**

Find the entire `OnChunkColumnGen` method (currently lines 182–271) and replace with:

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

            foreach (var def in config.Structures)
            {
                if (!cachedSchematics.TryGetValue(def.Code, out var variants)) continue;

                if (def.Placement == EnumOceanPlacement.OceanSurface)
                {
                    HandleOceanSurface(request, def, variants, mapRegion, chunkX, chunkZ);
                    continue;
                }

                HandleLegacyPlacement(request, def, variants, mapChunk, mapRegion, chunkX, chunkZ, seaLevel);
            }
        }

        /// <summary>
        /// Legacy per-chunk placement for Underwater / Coastal / BuriedUnderwater modes.
        /// Unchanged behavior from pre-OceanSurface era.
        /// </summary>
        private void HandleLegacyPlacement(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapChunk mapChunk, IMapRegion mapRegion, int chunkX, int chunkZ, int seaLevel)
        {
            rand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
            float roll = (float)rand.NextInt(10000) / 10000f;
            if (roll > def.Chance) return;

            if (def.GlobalMaxCount > 0)
            {
                lock (countsLock)
                {
                    if (globalCounts.TryGetValue(def.Code, out int placed) && placed >= def.GlobalMaxCount) return;
                }
            }

            if (def.MaxCount > 0 && CountExistingStructures(mapRegion, def.Code) >= def.MaxCount) return;

            int localX = rand.NextInt(chunksize);
            int localZ = rand.NextInt(chunksize);
            int posX = chunkX * chunksize + localX;
            int posZ = chunkZ * chunksize + localZ;

            if (def.MinSpawnDist > 0 || def.MaxSpawnDist > 0)
            {
                var spawnPos = GetSpawnPosSafe();
                if (spawnPos == null) return;
                int dx = posX - spawnPos.X;
                int dz = posZ - spawnPos.Z;
                double dist = Math.Sqrt((double)dx * dx + (double)dz * dz);
                if (def.MinSpawnDist > 0 && dist < def.MinSpawnDist) return;
                if (def.MaxSpawnDist > 0 && dist > def.MaxSpawnDist) return;
            }

            float oceanicity = GetOceanicity(mapRegion, posX, posZ);
            float beachStrength = GetBeachStrength(mapRegion, posX, posZ);

            int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
            int waterDepth = seaLevel - terrainHeight;

            if (!IsValidPlacement(def, oceanicity, beachStrength, waterDepth)) return;

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

            if (def.GlobalMaxCount > 0)
            {
                lock (countsLock)
                {
                    globalCounts.TryGetValue(def.Code, out int placed);
                    globalCounts[def.Code] = placed + 1;
                }
            }
        }

        /// <summary>
        /// OceanSurface handler. Runs in two phases per chunk:
        ///   (a) If no reservation exists and this chunk passes validation, create one.
        ///   (b) If a reservation exists and its cuboid intersects this chunk, PlacePartial its slice.
        /// </summary>
        private void HandleOceanSurface(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            OceanStructureReservation existing;
            lock (countsLock)
            {
                reservations.TryGetValue(def.Code, out existing);
            }

            // Phase A: try to create a reservation if none exists
            if (existing == null)
            {
                existing = TryReserveOceanSurface(def, variants, mapRegion, chunkX, chunkZ);
                // existing is non-null only if reservation was successful; fall through to Phase B
            }

            // Phase B: place whatever slice of the reserved structure falls in this chunk
            if (existing == null) return;
            PlaceOceanSurfaceSlice(request, def, variants, mapRegion, chunkX, chunkZ, existing);
        }

        /// <summary>
        /// Attempts to pick and reserve a valid OceanSurface location using the current chunk as candidate.
        /// Returns the reservation on success, null otherwise.
        /// </summary>
        private OceanStructureReservation TryReserveOceanSurface(OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            // Singleton gate
            if (def.GlobalMaxCount > 0)
            {
                lock (countsLock)
                {
                    if (globalCounts.TryGetValue(def.Code, out int placed) && placed >= def.GlobalMaxCount) return null;
                }
            }

            rand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
            float roll = (float)rand.NextInt(10000) / 10000f;
            if (roll > def.Chance) return null;

            int localX = rand.NextInt(chunksize);
            int localZ = rand.NextInt(chunksize);
            int candidateX = chunkX * chunksize + localX;
            int candidateZ = chunkZ * chunksize + localZ;

            if (def.MinSpawnDist > 0 || def.MaxSpawnDist > 0)
            {
                var spawnPos = GetSpawnPosSafe();
                if (spawnPos == null) return null;
                int dx = candidateX - spawnPos.X;
                int dz = candidateZ - spawnPos.Z;
                double dist = Math.Sqrt((double)dx * dx + (double)dz * dz);
                if (def.MinSpawnDist > 0 && dist < def.MinSpawnDist) return null;
                if (def.MaxSpawnDist > 0 && dist > def.MaxSpawnDist) return null;
            }

            int variantIdx = rand.NextInt(variants.Length);
            var variantRotations = variants[variantIdx];
            int rotationIndex = def.RandomRotation ? rand.NextInt(4) : 0;
            var schematic = variantRotations[rotationIndex];

            // Center-based origin: structure centered on candidate position
            int originX = candidateX - schematic.SizeX / 2;
            int originZ = candidateZ - schematic.SizeZ / 2;

            if (!ValidateOceanCoverage(mapRegion, originX, originZ, schematic.SizeX, schematic.SizeZ, def)) return null;

            var reservation = new OceanStructureReservation
            {
                OriginX = originX,
                OriginY = sapi.World.SeaLevel + def.OffsetY,
                OriginZ = originZ,
                VariantIndex = variantIdx,
                RotationIndex = rotationIndex,
                SizeX = schematic.SizeX,
                SizeY = schematic.SizeY,
                SizeZ = schematic.SizeZ,
                StructureRecorded = false
            };

            lock (countsLock)
            {
                // Re-check singleton after validation (another thread may have beaten us)
                if (def.GlobalMaxCount > 0 && globalCounts.TryGetValue(def.Code, out int placed2) && placed2 >= def.GlobalMaxCount) return null;
                reservations[def.Code] = reservation;
                globalCounts[def.Code] = globalCounts.GetValueOrDefault(def.Code) + 1;
            }

            Mod.Logger.Notification("Ocean structure '{0}' reserved at ({1}, {2}, {3})", def.Code, originX, reservation.OriginY, originZ);
            return reservation;
        }

        /// <summary>
        /// Paints the slice of a reserved schematic that falls within the current chunk.
        /// First-time placement also records the GeneratedStructure for waypoint discovery.
        /// </summary>
        private void PlaceOceanSurfaceSlice(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ, OceanStructureReservation res)
        {
            int footprintMinX = res.OriginX;
            int footprintMaxX = res.OriginX + res.SizeX;
            int footprintMinZ = res.OriginZ;
            int footprintMaxZ = res.OriginZ + res.SizeZ;
            int chunkMinX = chunkX * chunksize;
            int chunkMaxX = chunkMinX + chunksize;
            int chunkMinZ = chunkZ * chunksize;
            int chunkMaxZ = chunkMinZ + chunksize;

            // Footprint-vs-chunk intersection in XZ
            if (footprintMaxX <= chunkMinX || footprintMinX >= chunkMaxX) return;
            if (footprintMaxZ <= chunkMinZ || footprintMinZ >= chunkMaxZ) return;

            // Safety: defensive bounds on variant/rotation indices in case save data is from a different config
            if (res.VariantIndex < 0 || res.VariantIndex >= variants.Length) return;
            var rotations = variants[res.VariantIndex];
            if (res.RotationIndex < 0 || res.RotationIndex >= rotations.Length) return;
            var schematic = rotations[res.RotationIndex];

            var startPos = new BlockPos(res.OriginX, res.OriginY, res.OriginZ);
            schematic.PlacePartial(
                request.Chunks, worldgenBlockAccessor, sapi.World,
                chunkX, chunkZ, startPos,
                EnumReplaceMode.ReplaceAll,
                EnumStructurePlacement.Surface,
                replaceMeta: true, resolveImports: true
            );

            if (!res.StructureRecorded)
            {
                mapRegion.AddGeneratedStructure(new GeneratedStructure()
                {
                    Code = def.Code,
                    Group = "ocean",
                    Location = new Cuboidi(
                        res.OriginX, res.OriginY, res.OriginZ,
                        res.OriginX + res.SizeX - 1,
                        res.OriginY + res.SizeY - 1,
                        res.OriginZ + res.SizeZ - 1
                    ),
                    SuppressTreesAndShrubs = def.SuppressTrees,
                    SuppressRivulets = true
                });
                lock (countsLock) { res.StructureRecorded = true; }
            }
        }
```

- [ ] **Step 2: Add `using Vintagestory.ServerMods;` if the build complains about `BlockSchematicPartial` / `EnumStructurePlacement`**

Top of file, after existing `using` directives. If the build from step 3 below passes, skip this step.

- [ ] **Step 3: Build to verify**

Run: `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -6`
Expected: Build succeeds, 0 errors.

If errors mention `BlockSchematicPartial` or `EnumStructurePlacement` not found, add `using Vintagestory.ServerMods;` to the top of the file and re-run the build.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): OceanSurface reservation + per-chunk PlacePartial flow

OceanSurface structures now reserve a validated location on first
eligible chunk-gen, persist the reservation in savegame data, and
paint the schematic slice-by-slice as adjoining chunks generate.
Mirrors base-game GenStoryStructures' chunk-iterative pattern but
with ocean-coverage validation instead of landform matching.

Legacy Underwater/Coastal/BuriedUnderwater placement paths are
preserved unchanged in HandleLegacyPlacement.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Remove Tortuga from story structures

Unregister Tortuga from the story-structure system since it's moving back to ocean placement.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json`

- [ ] **Step 1: Delete the Tortuga entry and its schematicYOffsets line**

Open `Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json`. Make two edits:

1. In `schematicYOffsets`, remove the line `"story/seafarer:tortuga": -27,` (or whatever value is currently there). Keep `"story/seafarer:potato-king-house": 0`.

2. In the `structures` array, delete the entire Tortuga structure block (the one with `"code": "tortuga"`). Keep only the `potatoking` structure.

The resulting file should look like:

```json
{
  "schematicYOffsets": {
    "story/seafarer:potato-king-house": 0
  },
  "rocktypeRemapGroups": {},
  "structures": [
    {
      "code": "potatoking",
      "group": "storystructure",
      "name": "Potato King's House",
      "schematics": ["surface/potato-king-house"],
      "placement": "surface",
      "UseWorldgenHeight": true,
      "DisableSurfaceTerrainBlending": true,
      "dependsOnStructure": "spawn",
      "minSpawnDistX": -2500,
      "maxSpawnDistX": 2500,
      "minSpawnDistZ": -2500,
      "maxSpawnDistZ": 2500,
      "requireLandform": "veryflat",
      "landformRadius": 80,
      "generateGrass": true,
      "skipGenerationCategories": {
        "structures": 80,
        "trees": 50,
        "shrubs": 50,
        "hotsprings": 100,
        "patches": 30
      }
    }
  ]
}
```

(Preserve any user edits to the Potato King entry not shown in this plan — only the Tortuga-related lines are being removed.)

- [ ] **Step 2: Build + validate**

Run:
```
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -4 && python3 validate-assets.py 2>&1 | tail -6
```
Expected: build succeeds; validator baseline (1 pre-existing unrelated error, 0 new).

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/worldgen/storystructures.json
git commit -m "$(cat <<'EOF'
refactor(worldgen): remove Tortuga from story structures

Tortuga is moving to the Seafarer OceanSurface placement path.
Remove both the structure entry and its schematicYOffsets hint.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Add Tortuga to ocean structures with `oceansurface` placement

Register Tortuga in the ocean-structure config so the new `OceanSurface` code path handles it.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`

- [ ] **Step 1: Add the Tortuga entry as a third element in the `structures` array**

Open `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`. Add after the second entry (`wreck-one`). The final file content should be:

```json5
{
  "structures": [
    {
      "code": "wreck-crimsonrose",
      "schematics": ["underwater/wreck-crimson-rose"],
      "placement": "underwater",
      "chance": 0.015,
      "minWaterDepth": 10,
      "maxWaterDepth": 50,
      "offsetY": 0,
      "maxCount": 1,
      "suppressTrees": true,
      "generateGrass": false,
      "randomRotation": true
    },
    {
      "code": "wreck-one",
      "schematics": ["underwater/wreck-one"],
      "placement": "underwater",
      "chance": 0.015,
      "minWaterDepth": 10,
      "maxWaterDepth": 50,
      "offsetY": 0,
      "suppressTrees": true,
      "generateGrass": false,
      "randomRotation": true
    },
    {
      "code": "tortuga",
      "schematics": ["costal/tortuga"],
      "placement": "oceansurface",
      "chance": 1.0,
      "minWaterDepth": 5,
      "maxWaterDepth": 40,
      "offsetY": -27,
      "globalMaxCount": 1,
      "minSpawnDist": 500,
      "maxSpawnDist": 3000,
      "suppressTrees": true,
      "randomRotation": true
    }
  ]
}
```

(Preserve any user edits to the wreck entries that aren't shown here — only the new `tortuga` element is being added.)

- [ ] **Step 2: Build + validate**

Run:
```
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -4 && python3 validate-assets.py 2>&1 | tail -6
```
Expected: build succeeds; validator baseline (0 new errors).

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json
git commit -m "$(cat <<'EOF'
feat(worldgen): register Tortuga with oceansurface placement

chance 1.0 means every chunk in the spawn band is considered for
reservation; ValidateOceanCoverage does the real filtering. The
globalMaxCount: 1 singleton ensures only one Tortuga per world.
offsetY: -27 embeds the sand foundation 27 blocks below sea level
while buildings emerge above.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Switch `map-tortuga` back to `ItemOceanLocatorMap`

The map item needs the Seafarer class again, since Tortuga is no longer in the base-game story-structures dictionary.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json`

- [ ] **Step 1: Rewrite the map item definition**

Replace `Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json` with:

```json
{
    "code": "map-tortuga",
    "class": "ItemOceanLocatorMap",
    "maxstacksize": 1,
    "attributes": {
        "displaycaseable": true,
        "shelvable": true,
        "readable": true,
        "editable": false,
        "maxPageCount": 1,
        "searchRange": 10000,
        "locatorPropsbyType": {
            "*": {
                "schematiccode": "tortuga",
                "waypointtext": "location-tortuga",
                "waypointicon": "x",
                "waypointcolor": [0.95, 0.75, 0.2, 1],
                "randomX": 15,
                "randomZ": 15
            }
        }
    },
    "shape": { "base": "game:item/clutter/fishing/bottlemessage" },
    "creativeinventory": { "general": ["*"], "items": ["*"], "seafarer": ["*"] }
}
```

Differences vs the current state:
- `"class": "ItemLocatorMap"` → `"class": "ItemOceanLocatorMap"`
- `"locatorProps": {...}` → `"locatorPropsbyType": { "*": {...} }`
- Re-introduce `"searchRange": 10000` so the bottle finds Tortuga from anywhere in the spawn-distance band

- [ ] **Step 2: Build + validate**

Run:
```
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -4 && python3 validate-assets.py 2>&1 | tail -6
```
Expected: build succeeds; validator baseline.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/Seafarer/assets/seafarer/itemtypes/lore/map-tortuga.json
git commit -m "$(cat <<'EOF'
feat(item): switch map-tortuga back to ItemOceanLocatorMap

Tortuga now lives in GenOceanStructures (via OceanSurface placement)
and registers its location as a GeneratedStructure on the mapRegion.
ItemOceanLocatorMap's FindFreshStructureLocation path is the right
resolver for that; base-game ItemLocatorMap (which reads the story
structure dictionary) would no longer find it.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Manual in-game verification

Checklist for the user. No automation.

**Files:** None modified.

- [ ] **Step 1: Create a fresh world**

Fresh world required — any existing world has state from prior placement attempts.

- [ ] **Step 2: Watch the server log for the reservation notification**

As the world generates spawn chunks + surrounding area, look for:

```
[Notification] [seafarer] Ocean structure 'tortuga' reserved at (X, Y, Z)
```

The reservation fires on the first chunk within the spawn band that passes `ValidateOceanCoverage`. Record the coordinates.

Expected: one such line. If no reservation appears after exploring a reasonable band, the coverage filter may be too strict for the generated terrain — widen `maxSpawnDist` or relax the moderate rule.

- [ ] **Step 3: Teleport to the reserved origin**

Run: `/tp <X> 130 <Z>` with the coordinates from step 2.

Expected: you're near Tortuga. Parts of it may not be visible yet if chunks outside your view haven't generated.

- [ ] **Step 4: Explore the full footprint**

Walk a circle of ~200 blocks around the origin (Tortuga is 178×171). As new chunks generate under you, you should see the schematic paint in piece by piece. Absence of `Tried to set block outside generating chunks` warnings in the log confirms the per-chunk `PlacePartial` path is working.

Expected: full schematic visible with no gaps or truncation at chunk boundaries.

- [ ] **Step 5: Map activation**

Give yourself a `map-tortuga`: `/giveitem seafarer:map-tortuga 1`. Teleport back to spawn: `/tp 512000 130 512000` (or wherever spawn is). Right-click the map.

Expected: `Approximate location of Tortuga added to your world map` — the base-game `strucLocSys.FindFreshStructureLocation` within `searchRange: 10000` finds the `GeneratedStructure` record placed by `PlaceOceanSurfaceSlice`.

- [ ] **Step 6: Save/load persistence**

Quit world cleanly (via main menu). Reload. Walk into a region of Tortuga's footprint that you haven't visited yet.

Expected: the slice in that region paints correctly as the chunks generate (reservation persisted via savegame data). Log should not produce any warnings.

- [ ] **Step 7: Regression — wrecks still place**

Explore broad ocean areas. Eventually you should spot one of the crimson-rose or wreck-one wrecks (1.5% per chunk, `Underwater` placement mode). Confirms the legacy code path still works.

- [ ] **Step 8: Regression — Potato King still places**

Run `/tpstoryloc potatoking`. Expected: teleport to the placed Potato King's House.

- [ ] **Step 9: Regression — map-tortuga still drops from fishing and panning**

In creative, spawn saltwater/reef fish and kill for ~30-40 drops, or pan sand/gravel repeatedly. Expected: occasional `map-tortuga` drops.

- [ ] **Step 10: Record success**

If all checks pass:

```bash
git commit --allow-empty -m "$(cat <<'EOF'
test(worldgen): manually verified OceanSurface placement + regressions

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

If anything fails, note the failure mode (no reservation notification, gaps in footprint, map no-location, regression in wrecks/potato-king/drops) before iterating.

---

## Self-review checklist

- **Spec coverage**: enum addition (Task 1), reservation class + persistence (Task 1), schematic type change (Task 1), validator (Task 2), reservation flow (Task 3), per-chunk placement flow (Task 3), Y computation (Task 3), config integration (Tasks 4 + 5), map class swap (Task 6), regression + verification (Task 7). All spec sections covered.
- **No placeholders**: Every code block is complete. No "TBD"/"similar to Task N"/etc.
- **Type consistency**: `BlockSchematicPartial` used consistently in Tasks 1 and 3 (cache type + method signatures). `OceanStructureReservation` field names (`OriginX`, `OriginY`, `OriginZ`, `VariantIndex`, `RotationIndex`, `SizeX`, `SizeY`, `SizeZ`, `StructureRecorded`) identical across Task 1 class definition and Task 3 usage. `reservations` / `countsLock` / `ReservationsDataKey` identifiers identical across Tasks 1 and 3. `"tortuga"` spelled identically in Task 5 (`code`), Task 6 (`schematiccode`), and the existing lang entries. Task 2's validator method signature matches Task 3's call site (`ValidateOceanCoverage(mapRegion, originX, originZ, sizeX, sizeZ, def)`).
