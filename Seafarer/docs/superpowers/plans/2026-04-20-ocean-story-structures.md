# Ocean Story Structures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `storyStructure: true` flag to `OceanStructureDef` that triggers init-time reservation (via `SaveGameLoaded`), guaranteeing every future chunk sees the reservation and paints its slice. Apply to `wreck-crimsonrose` and `tortuga`.

**Architecture:** Extend `OceanStructureDef` with one bool + `OceanStructureReservation` with one bool. Add `DetermineOceanStoryStructures()` method called from `OnSaveGameLoaded`. For story structures, walk candidates in the spawn band, force-load map regions via `BlockingTestMapRegionExists`, validate, reserve. Per-chunk placement gets a "resolve Y lazily if deferred" step for non-OceanSurface placement modes. Non-story defs keep the existing lazy per-chunk behavior.

**Tech Stack:** C# / .NET 10.0, Vintage Story API (`ICoreServerAPI.WorldManager.BlockingTestMapRegionExists`, `BlockSchematicPartial.PlacePartial`, `protobuf-net` via `SerializerUtil`)

**Spec:** `docs/superpowers/specs/2026-04-20-ocean-story-structures-design.md`

**Project note:** Vintage Story mod, no automated test suite. Verification is `dotnet build`, `python3 validate-assets.py`, and manual in-game verification.

**Build command (from repo root):** `export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj`

---

## File Map

| File | Responsibility | Change |
|---|---|---|
| `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs` | Ocean worldgen — add `StoryStructure` + `OriginYResolved` fields; add `DetermineOceanStoryStructures()` method; hook into `OnSaveGameLoaded`; update per-chunk placement flow with Y-resolution step | Modify |
| `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json` | Add `"storyStructure": true` to `wreck-crimsonrose` and `tortuga` | Modify |

---

### Task 1: Extend data model

Add the new `StoryStructure` field on `OceanStructureDef` and the `OriginYResolved` field on `OceanStructureReservation` (with a fresh ProtoMember tag).

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Add `StoryStructure` to `OceanStructureDef`**

In `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`, find the `OceanStructureDef` class. After the existing `public int MaxSpawnDist = 0;` line, add:

```csharp
        // When true, reservation happens at world init (SaveGameLoaded) rather than
        // lazily per-chunk. Placement is always chunk-iterative via PlacePartial.
        public bool StoryStructure = false;
```

- [ ] **Step 2: Add `OriginYResolved` to `OceanStructureReservation`**

In the same file, find the `OceanStructureReservation` class. After the `[ProtoMember(9)] public bool StructureRecorded;` line, add:

```csharp
        // False until Y has been resolved. For OceanSurface reservations, Y is resolved
        // immediately at reservation time (= seaLevel + OffsetY). For other placement modes,
        // resolution is deferred to the first chunk-gen event for the chunk containing
        // (OriginX, OriginZ) — since terrain height is only available per chunk.
        [ProtoMember(10)] public bool OriginYResolved;
```

- [ ] **Step 3: Build**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -4`
Expected: 0 errors. Warning count may tick up by 1 or 2 (new field nullability-style warnings match existing pattern).

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): add StoryStructure + OriginYResolved fields

StoryStructure flag on OceanStructureDef will trigger init-time
reservation (via SaveGameLoaded) in a follow-up task. OriginYResolved
on OceanStructureReservation tracks whether Y was computed at
reservation time (true for OceanSurface) or still needs per-chunk
terrain lookup (false for Underwater/Coastal/BuriedUnderwater).

No behavior change yet - fields are unused until the next task wires
the init-reservation path.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Add `DetermineOceanStoryStructures` method

The init-time reservation scanner. Called from `OnSaveGameLoaded` after the dicts are deserialized. Walks candidate positions, force-loads map regions, validates, reserves.

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Add the `DetermineOceanStoryStructures` method**

In the `GenOceanStructures` class, add this new private method. Place it immediately after `OnGameWorldSave` (near other lifecycle hooks):

```csharp
        private const int MaxReservationAttempts = 30;

        /// <summary>
        /// Init-time reservation scan for structures flagged StoryStructure = true.
        /// Called from OnSaveGameLoaded after savegame dicts are deserialized.
        /// For each story def without an existing reservation, try up to
        /// MaxReservationAttempts candidate positions in the spawn-distance band.
        /// Map regions are force-loaded as needed.
        /// </summary>
        private void DetermineOceanStoryStructures()
        {
            if (config == null || config.Structures.Length == 0) return;

            var spawnPos = GetSpawnPosSafe();
            if (spawnPos == null)
            {
                Mod.Logger.Notification("Ocean story structures: spawn position not yet determined, will retry on next load.");
                return;
            }

            int seaLevel = sapi.World.SeaLevel;

            foreach (var def in config.Structures)
            {
                if (!def.StoryStructure) continue;
                if (!cachedSchematics.TryGetValue(def.Code, out var variants)) continue;

                // Already reserved (from previous load or a prior save in this session)
                bool alreadyReserved;
                lock (countsLock) { alreadyReserved = reservations.ContainsKey(def.Code); }
                if (alreadyReserved) continue;

                // Respect GlobalMaxCount — if already at cap (e.g., admin-placed), skip
                if (def.GlobalMaxCount > 0)
                {
                    lock (countsLock)
                    {
                        if (globalCounts.TryGetValue(def.Code, out int placed) && placed >= def.GlobalMaxCount) continue;
                    }
                }

                bool placed0 = TryReserveStoryLocation(def, variants, spawnPos, seaLevel);
                if (!placed0)
                {
                    Mod.Logger.Warning("Ocean story structure '{0}': failed to find a valid spot after {1} attempts; will not generate this world.", def.Code, MaxReservationAttempts);
                }
            }
        }

        /// <summary>
        /// Try up to MaxReservationAttempts candidate positions for one story structure.
        /// Returns true if a reservation was written.
        /// </summary>
        private bool TryReserveStoryLocation(OceanStructureDef def, BlockSchematicPartial[][] variants, BlockPos spawnPos, int seaLevel)
        {
            // Deterministic-per-world RNG seeded by code + world seed so repeated loads
            // of the same world tend to converge on the same spot if the first attempts
            // happen to all miss initially (edge case; reservations are normally persisted).
            var localRand = new LCGRandom(sapi.WorldManager.Seed ^ (uint)def.Code.GetHashCode());

            int minDist = Math.Max(def.MinSpawnDist, 0);
            int maxDist = def.MaxSpawnDist > 0 ? def.MaxSpawnDist : 5000;   // reasonable default if unset

            for (int attempt = 0; attempt < MaxReservationAttempts; attempt++)
            {
                // Polar sample in the band
                double angle = localRand.NextDouble() * Math.PI * 2.0;
                double radius = minDist + localRand.NextDouble() * Math.Max(1, maxDist - minDist);
                int candidateX = spawnPos.X + (int)(Math.Cos(angle) * radius);
                int candidateZ = spawnPos.Z + (int)(Math.Sin(angle) * radius);

                // Pick variant + rotation
                int variantIdx = localRand.NextInt(variants.Length);
                var rotations = variants[variantIdx];
                int rotationIdx = def.RandomRotation ? localRand.NextInt(4) : 0;
                var schematic = rotations[rotationIdx];

                // Center-based origin
                int originX = candidateX - schematic.SizeX / 2;
                int originZ = candidateZ - schematic.SizeZ / 2;

                // Force-load the map region containing the candidate center
                int rx = candidateX / regionSize;
                int rz = candidateZ / regionSize;
                if (!sapi.WorldManager.BlockingTestMapRegionExists(rx, rz))
                {
                    // Region failed to load (shouldn't happen under normal circumstances)
                    continue;
                }
                var mapRegion = sapi.World.BlockAccessor.GetMapRegion(rx, rz);
                if (mapRegion == null) continue;

                if (!ValidateOceanCoverage(mapRegion, originX, originZ, schematic.SizeX, schematic.SizeZ, def)) continue;

                // Success — build reservation
                bool isOceanSurface = def.Placement == EnumOceanPlacement.OceanSurface;
                var reservation = new OceanStructureReservation
                {
                    OriginX = originX,
                    OriginY = isOceanSurface ? (seaLevel + def.OffsetY) : 0,
                    OriginZ = originZ,
                    VariantIndex = variantIdx,
                    RotationIndex = rotationIdx,
                    SizeX = schematic.SizeX,
                    SizeY = schematic.SizeY,
                    SizeZ = schematic.SizeZ,
                    StructureRecorded = false,
                    OriginYResolved = isOceanSurface
                };

                lock (countsLock)
                {
                    reservations[def.Code] = reservation;
                    globalCounts[def.Code] = globalCounts.GetValueOrDefault(def.Code) + 1;
                }

                Mod.Logger.Notification("Ocean story structure '{0}' reserved at ({1}, {2}, {3}) [Y {4}resolved]",
                    def.Code, originX, reservation.OriginY, originZ,
                    isOceanSurface ? "" : "un");

                return true;
            }

            return false;
        }
```

- [ ] **Step 2: Hook into `OnSaveGameLoaded`**

Find `OnSaveGameLoaded`. Replace the method body with the extended version (the original keeps loading both dicts; the only addition is the `DetermineOceanStoryStructures()` call at the end):

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

            DetermineOceanStoryStructures();
        }
```

- [ ] **Step 3: Build**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -4`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): DetermineOceanStoryStructures scans candidates at world init

Called from OnSaveGameLoaded after savegame dicts are deserialized.
For each StoryStructure=true def without an existing reservation,
walks up to 30 candidate positions in the spawn-distance band,
force-loads map regions via BlockingTestMapRegionExists, validates
via ValidateOceanCoverage, and writes a reservation + increments
globalCounts.

For OceanSurface placement, Y is resolved immediately (seaLevel +
OffsetY). For other placement modes, OriginYResolved stays false and
resolution is deferred to the first chunk-gen event (added in next
task).

Config integration (wreck-crimsonrose + tortuga getting the flag)
happens in the final config task.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Per-chunk Y resolution + lazy-reservation skip for story defs

Update `HandleOceanSurface` and `PlaceOceanSurfaceSlice` to:
1. Skip lazy-reservation Phase A for story defs (reservation already exists from init)
2. Resolve deferred Y on first chunk-gen touching the origin chunk
3. Proceed with `PlacePartial` once Y is resolved

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Update `HandleOceanSurface` to skip lazy reservation for story defs**

Find `HandleOceanSurface`. Replace it with:

```csharp
        /// <summary>
        /// OceanSurface handler. Two phases per chunk:
        ///   (a) If the def is NOT a story structure and no reservation exists, try to make one lazily.
        ///       (Story structures reserve at init time in DetermineOceanStoryStructures; we do not
        ///        make lazy reservations for them here.)
        ///   (b) If a reservation exists and its cuboid intersects this chunk, place its slice.
        /// </summary>
        private void HandleOceanSurface(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ)
        {
            OceanStructureReservation existing;
            lock (countsLock)
            {
                reservations.TryGetValue(def.Code, out existing);
            }

            // Phase A: create a lazy reservation for non-story defs only
            if (existing == null && !def.StoryStructure)
            {
                existing = TryReserveOceanSurface(def, variants, mapRegion, chunkX, chunkZ);
            }

            if (existing == null) return;
            PlaceOceanSurfaceSlice(request, def, variants, mapRegion, chunkX, chunkZ, existing);
        }
```

- [ ] **Step 2: Add Y-resolution step in `PlaceOceanSurfaceSlice`**

Find `PlaceOceanSurfaceSlice`. Replace the whole method with (the new block is the Y-resolution guard at the top; the rest is the existing logic preserved):

```csharp
        /// <summary>
        /// Paints the slice of a reserved schematic that falls within the current chunk.
        /// Resolves deferred Y on the first chunk-gen that touches the origin chunk.
        /// First-time placement also records the GeneratedStructure for waypoint discovery.
        /// </summary>
        private void PlaceOceanSurfaceSlice(IChunkColumnGenerateRequest request, OceanStructureDef def, BlockSchematicPartial[][] variants,
            IMapRegion mapRegion, int chunkX, int chunkZ, OceanStructureReservation res)
        {
            // Defensive bounds on variant/rotation indices in case save data is from a different config
            if (res.VariantIndex < 0 || res.VariantIndex >= variants.Length) return;
            var rotations = variants[res.VariantIndex];
            if (res.RotationIndex < 0 || res.RotationIndex >= rotations.Length) return;
            var schematic = rotations[res.RotationIndex];
            int seaLevel = sapi.World.SeaLevel;

            // Resolve Y if still deferred (story structures with non-OceanSurface placement).
            // We resolve from the chunk that contains the origin corner (OriginX, OriginZ).
            if (!res.OriginYResolved)
            {
                int originChunkX = res.OriginX / chunksize;
                int originChunkZ = res.OriginZ / chunksize;
                if (chunkX != originChunkX || chunkZ != originChunkZ)
                {
                    // Not our job — wait for the chunk that owns the origin
                    return;
                }

                // We are the origin chunk. Resolve Y from terrain.
                int localX = res.OriginX - originChunkX * chunksize;
                int localZ = res.OriginZ - originChunkZ * chunksize;
                // Guard against edge cases (shouldn't happen but safety first)
                if (localX < 0 || localX >= chunksize || localZ < 0 || localZ >= chunksize) return;

                var mapChunk = request.Chunks[0].MapChunk;
                int terrainHeight = mapChunk.WorldGenTerrainHeightMap[localZ * chunksize + localX];
                int waterDepth = seaLevel - terrainHeight;

                float oceanicity = GetOceanicity(mapRegion, res.OriginX, res.OriginZ);
                float beachStrength = GetBeachStrength(mapRegion, res.OriginX, res.OriginZ);

                if (!IsValidPlacement(def, oceanicity, beachStrength, waterDepth))
                {
                    // Coarse OceanMap validation passed at reservation time but fine-grained terrain
                    // doesn't satisfy the def. Drop the reservation — structure will not generate.
                    Mod.Logger.Warning("Ocean story structure '{0}': per-chunk terrain failed IsValidPlacement (waterDepth={1}); reservation dropped.",
                        def.Code, waterDepth);
                    lock (countsLock)
                    {
                        reservations.Remove(def.Code);
                        if (def.GlobalMaxCount > 0 && globalCounts.TryGetValue(def.Code, out int n) && n > 0)
                        {
                            globalCounts[def.Code] = n - 1;
                        }
                    }
                    return;
                }

                res.OriginY = CalculatePlacementY(def, terrainHeight, schematic);
                res.OriginYResolved = true;
                Mod.Logger.Notification("Ocean story structure '{0}' Y resolved to {1} (terrain={2}, placement={3})",
                    def.Code, res.OriginY, terrainHeight, def.Placement);
            }

            // XZ-footprint intersection with current chunk
            int footprintMinX = res.OriginX;
            int footprintMaxX = res.OriginX + res.SizeX;
            int footprintMinZ = res.OriginZ;
            int footprintMaxZ = res.OriginZ + res.SizeZ;
            int chunkMinX = chunkX * chunksize;
            int chunkMaxX = chunkMinX + chunksize;
            int chunkMinZ = chunkZ * chunksize;
            int chunkMaxZ = chunkMinZ + chunksize;

            if (footprintMaxX <= chunkMinX || footprintMinX >= chunkMaxX) return;
            if (footprintMaxZ <= chunkMinZ || footprintMinZ >= chunkMaxZ) return;

            var startPos = new BlockPos(res.OriginX, res.OriginY, res.OriginZ);
            schematic.PlacePartial(
                request.Chunks, worldgenBlockAccessor, sapi.World,
                chunkX, chunkZ, startPos,
                EnumReplaceMode.ReplaceAll,
                EnumStructurePlacement.Surface,
                replaceMeta: true, resolveImports: true
            );

            // Atomic check-and-set so only the first thread adds the GeneratedStructure record.
            lock (countsLock)
            {
                if (res.StructureRecorded) return;
                res.StructureRecorded = true;
            }
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
        }
```

- [ ] **Step 3: Update `OnChunkColumnGen` dispatch for story-flagged Underwater/Coastal/BuriedUnderwater defs**

Find `OnChunkColumnGen`. The current dispatcher routes `OceanSurface` to `HandleOceanSurface` and everything else to `HandleLegacyPlacement`. Story structures with non-OceanSurface placement need to also go through `HandleOceanSurface` (which now handles deferred Y + per-chunk PlacePartial for any reservation). Replace the dispatcher with:

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

                // Story structures (any placement mode) + OceanSurface (any story flag) use
                // the reservation + per-chunk PlacePartial path.
                if (def.StoryStructure || def.Placement == EnumOceanPlacement.OceanSurface)
                {
                    HandleOceanSurface(request, def, variants, mapRegion, chunkX, chunkZ);
                    continue;
                }

                HandleLegacyPlacement(request, def, variants, mapChunk, mapRegion, chunkX, chunkZ, seaLevel);
            }
        }
```

- [ ] **Step 4: Build**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -4`
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "$(cat <<'EOF'
feat(worldgen): deferred-Y resolution + story-aware chunk dispatch

HandleOceanSurface now skips the lazy Phase-A reservation when the
def is a story structure (reservation was made at init). Dispatcher
routes StoryStructure=true defs with any placement mode through the
OceanSurface path so they get chunk-iterative PlacePartial.

PlaceOceanSurfaceSlice gains a Y-resolution step at the top: if the
reservation's Y is still unresolved (deferred from non-OceanSurface
story structures), compute it from the chunk containing the origin
corner using CalculatePlacementY. If fine-grained terrain fails
IsValidPlacement, the reservation is dropped with a warning.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Flag wreck-crimsonrose and tortuga as story structures

Config-only change.

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`

- [ ] **Step 1: Add `storyStructure: true` to both entries**

Open `Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json`. For the `wreck-crimsonrose` structure and the `tortuga` structure, add `"storyStructure": true`. The `wreck-one` entry should NOT be modified. Resulting file content:

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
      "randomRotation": true,
      "storyStructure": true
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
      "generateGrass": false,
      "randomRotation": true,
      "storyStructure": true
    }
  ]
}
```

(Preserve any fine-grained values in the existing file — only the `"storyStructure": true` line is being added. If the file differs, keep the existing field values and add only the new field.)

Also consider setting `"minSpawnDist"` and `"maxSpawnDist"` on `wreck-crimsonrose` if you want to constrain where it spawns. The story path uses these; without them, it defaults to 0..5000 (the fallback in `TryReserveStoryLocation`). Not required for this task.

- [ ] **Step 2: Build**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer/Seafarer.csproj 2>&1 | tail -4`
Expected: 0 errors.

- [ ] **Step 3: Asset validator**

Run: `cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py 2>&1 | tail -6`
Expected: 1 pre-existing unrelated error (`premiumfish`), 0 new errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git add Seafarer/Seafarer/assets/seafarer/worldgen/oceanstructures.json
git commit -m "$(cat <<'EOF'
feat(worldgen): flag wreck-crimsonrose and tortuga as story structures

Both structures now reserve at world init rather than lazy-per-chunk,
which guarantees every future chunk sees the reservation and paints
its slice. wreck-one stays on the per-chunk path as the reference
for future non-story ocean structures.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Manual in-game verification

Checklist for the user. No automation available.

**Files:** None modified.

- [ ] **Step 1: Create a fresh world**

Any previous world has bad state from prior iterations. Fresh is required.

- [ ] **Step 2: Server log on join**

Expected log lines (in order during world init):
- `Ocean story structure 'wreck-crimsonrose' reserved at (X, Y, Z) [Y unresolved]` (Y is 0 at this point — resolved lazily)
- `Ocean story structure 'tortuga' reserved at (X, Y, Z) [Y resolved]` (Y is seaLevel + offsetY immediately)

If either line is missing, check for: `Ocean story structure '...': failed to find a valid spot after 30 attempts`. That means no coastal spot in the spawn band passed validation — bump `maxSpawnDist` or widen `MaxReservationAttempts`.

If spawn position was not yet determined on first load: `Ocean story structures: spawn position not yet determined, will retry on next load.` — save and reload; it should resolve on the second load.

- [ ] **Step 3: Teleport to each reservation**

For Tortuga: `/tp <tortugaX> 130 <tortugaZ>` using the coords from step 2.
For wreck-crimsonrose: first resolve its Y by walking into the chunk at (originX, originZ). The `Ocean story structure 'wreck-crimsonrose' Y resolved to N` log fires once you hit the origin chunk. Then `/tp <wreckX> <N> <wreckZ>`.

Walk the full footprint of each. Expected: both structures fully present — no gaps, no `Tried to set block outside generating chunks` warnings.

- [ ] **Step 4: Save/reload persistence**

Quit cleanly. Reload. Verify:
- No duplicate placement occurs
- Chunks in the footprint that haven't generated yet paint their slices correctly when first visited post-reload

- [ ] **Step 5: Map items work**

`/giveitem seafarer:map-tortuga 1` and `/giveitem seafarer:map-crimsonrose 1`. Right-click each. Both should add waypoints to the respective reserved locations.

- [ ] **Step 6: Regression — wreck-one still lazy**

`wreck-one` should still spawn via the legacy chance-based path. You may or may not see one — 1.5% per chunk in the Underwater biome means it's rare. Absence is not a regression. But if you DO see one: verify no `Ocean story structure 'wreck-one' reserved` log line appears (it shouldn't — the flag is false).

- [ ] **Step 7: Record success**

If all checks pass:

```bash
cd /mnt/d/Development/vs/vsmod-seafarer
git commit --allow-empty -m "test(worldgen): manually verified ocean story structures

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
"
```

If anything fails, note the specific failure (no reservation, gaps in footprint, Y resolution failure, etc.) before iterating.

---

## Self-review checklist

- **Spec coverage:** `StoryStructure` field (Task 1), `OriginYResolved` field (Task 1), `DetermineOceanStoryStructures` method (Task 2), `OnSaveGameLoaded` hook (Task 2), per-chunk Y resolution (Task 3), story-skip in `HandleOceanSurface` (Task 3), dispatcher routing (Task 3), config integration (Task 4), manual verification (Task 5). All spec sections covered.
- **No placeholders:** Every step has complete code. No "TBD"/"TODO"/"similar to Task N".
- **Type consistency:** `StoryStructure` field name consistent across Task 1 (definition) and Tasks 3–4 (usage). `OriginYResolved` consistent across Task 1 (definition) and Tasks 2–3 (usage). `DetermineOceanStoryStructures` / `TryReserveStoryLocation` / `MaxReservationAttempts` spelled identically. `BlockingTestMapRegionExists` and `GetMapRegion` signatures match VS API. `EnumOceanPlacement.OceanSurface` identifier identical. JSON field `"storyStructure"` (camelCase) matches C# `StoryStructure` (PascalCase) per Newtonsoft default convention.
