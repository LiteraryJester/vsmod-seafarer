# Seafloor Fill for OceanSurface Structures Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the gap between Tortuga's underside and the seafloor by raising the floor with muddy gravel up to one block below the structure, in a per-column footprint that matches the schematic's silhouette.

**Architecture:** Add an opt-in JSON property `seafloorFillBlock` on `SeafarerStructure`. At config load, precompute a per-structure column-occupancy mask + lowest local Y. After `PlacePartial` runs for each chunk slice of an opted-in structure, walk that slice's footprint and fill columns the schematic occupies from existing terrain Y up to `bottomY - 1`, then call `UpdateHeightmap`.

**Tech Stack:** C#, .NET 10, Vintage Story 1.21+ modding API. No unit-test infrastructure exists for worldgen code in this repo — verification is via build success and in-game `/wgen seafarer` commands.

**Spec:** `docs/superpowers/specs/2026-05-09-seafloor-fill-design.md`

**Build command:**
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```

---

## Task 1: Add JSON property and internal fields to `SeafarerStructure`

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs` (class `SeafarerStructure`, around lines 29–116)

- [ ] **Step 1: Add the public `SeafloorFillBlock` property and internal cache fields**

In `GenSeafarerStructures.cs`, locate `class SeafarerStructure : WorldGenStructureBase`. Find the existing `internal BlockSchematicPartial schematicData;` block at the bottom of the class (currently around line 113):

```csharp
        internal BlockSchematicPartial schematicData;
        internal int[] replacewithblocklayersBlockids = Array.Empty<int>();
        internal Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps;
    }
```

Add a new `[JsonProperty]` line near the other JSON properties (place it just after the `PostPlaceDecorators` property, around line 108) and three internal fields next to the other `internal` cache fields:

JSON property to add (place near `PostPlaceDecorators`):
```csharp
        [JsonProperty]
        public string SeafloorFillBlock;
```

Internal fields to add (place inside the `internal` block at the bottom of the class):
```csharp
        internal int seafloorFillBlockId;
        internal bool[,] schematicColumnMask;
        internal int schematicLowestY;
```

So the bottom of the class becomes:

```csharp
        internal BlockSchematicPartial schematicData;
        internal int[] replacewithblocklayersBlockids = Array.Empty<int>();
        internal Dictionary<int, Dictionary<int, int>> resolvedRockTypeRemaps;
        internal int seafloorFillBlockId;
        internal bool[,] schematicColumnMask;
        internal int schematicLowestY;
    }
```

- [ ] **Step 2: Build to verify the additions compile**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```
Expected: `Build succeeded` with 0 errors. New fields are unused so far (warnings about unused fields are OK; the next tasks will use them).

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs && \
git commit -m "Add SeafloorFillBlock property and cache fields on SeafarerStructure"
```

---

## Task 2: Implement schematic column-occupancy mask builder

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs` (class `GenSeafarerStructures`, add new private method)

The mask records which `(localX, localZ)` columns of a schematic contain at least one solid (non-air, non-fluid) block, plus the lowest local Y across all solid blocks. Built once per structure at load time.

- [ ] **Step 1: Add the `BuildSchematicColumnMask` method**

In `GenSeafarerStructures.cs`, find `private void DedupeFluidIndices(BlockSchematicPartial schem)` (around line 896). Add the following private method directly above it (so it sits with the other schematic-prep helpers):

```csharp
        /// <summary>
        /// Walks the schematic's packed Indices once to build a per-column
        /// "has any solid block" mask and find the lowest local Y of any solid
        /// block. Solid here means a real block that isn't air and isn't a fluid
        /// (waterfalls inside the schematic shouldn't count as "structure
        /// occupies this column"). The mask drives the seafloor fill silhouette;
        /// the lowest Y sets the fill ceiling.
        /// </summary>
        private void BuildSchematicColumnMask(SeafarerStructure def)
        {
            var schem = def.schematicData;
            if (schem?.Indices == null || schem.BlockIds == null) return;

            int sizeX = schem.SizeX;
            int sizeZ = schem.SizeZ;
            var mask = new bool[sizeX, sizeZ];
            int lowestY = int.MaxValue;

            for (int i = 0; i < schem.Indices.Count; i++)
            {
                uint idx = schem.Indices[i];
                int blockId = schem.BlockIds[i];

                if (!schem.BlockCodes.TryGetValue(blockId, out var blockCode)) continue;
                Block block = api.World.GetBlock(blockCode);
                if (block == null || block.Id == 0) continue;
                if (block.ForFluidsLayer) continue;

                int lx = (int)(idx & 0x3ff);
                int lz = (int)((idx >> 10) & 0x3ff);
                int ly = (int)((idx >> 20) & 0x3ff);

                if (lx < 0 || lx >= sizeX || lz < 0 || lz >= sizeZ) continue;
                mask[lx, lz] = true;
                if (ly < lowestY) lowestY = ly;
            }

            def.schematicColumnMask = mask;
            def.schematicLowestY = lowestY == int.MaxValue ? 0 : lowestY;
        }
```

- [ ] **Step 2: Build to verify the new method compiles**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```
Expected: `Build succeeded` with 0 errors. The method is unused so far — that's expected; Task 3 wires it in.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs && \
git commit -m "Add BuildSchematicColumnMask helper for per-column footprint precompute"
```

---

## Task 3: Resolve `seafloorFillBlock` and build the mask in `LoadConfig`

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs` (method `LoadConfig`, around lines 314–334)

- [ ] **Step 1: Add resolution + mask-build inside the schematic-load loop**

In `LoadConfig`, find the per-def schematic-load loop (currently lines ~314–334):

```csharp
            foreach (var def in scfg.Structures)
            {
                if (def.Schematics == null || def.Schematics.Length == 0) continue;
                try
                {
                    def.schematicData = def.LoadSchematics<BlockSchematicPartial>(api, def.Schematics, null)[0];
                    DedupeFluidIndices(def.schematicData);
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
```

Replace the entire `try` block (everything inside `try { ... }`) with this version that adds fill-block resolution and mask building at the end:

```csharp
                try
                {
                    def.schematicData = def.LoadSchematics<BlockSchematicPartial>(api, def.Schematics, null)[0];
                    DedupeFluidIndices(def.schematicData);
                    def.schematicData.blockLayerConfig = blockLayerConfig;

                    if (scfg.SchematicYOffsets.TryGetValue(
                        "story/" + def.schematicData.FromFile.GetNameWithDomain().Replace(".json", ""),
                        out var off))
                    {
                        def.schematicData.OffsetY = off;
                    }

                    if (!string.IsNullOrEmpty(def.SeafloorFillBlock))
                    {
                        var fillBlock = api.World.GetBlock(new AssetLocation(def.SeafloorFillBlock));
                        if (fillBlock == null)
                        {
                            api.Logger.Error(
                                "Seafarer structure '{0}': seafloorFillBlock '{1}' not found.",
                                def.Code, def.SeafloorFillBlock);
                        }
                        else
                        {
                            def.seafloorFillBlockId = fillBlock.Id;
                            BuildSchematicColumnMask(def);
                        }
                    }
                }
```

(The `catch (Exception e) { ... }` block immediately below stays exactly as-is.)

- [ ] **Step 2: Build to verify wiring**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs && \
git commit -m "Resolve seafloorFillBlock and build column mask at config load"
```

---

## Task 4: Implement `FillSeafloorBelow`

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs` (class `GenSeafarerStructures`, add new private method)

The fill walks the chunk-slice clip of the structure's footprint, replaces the air/water column above the seafloor with the configured fill block where the schematic mask is set, and updates the heightmap once.

- [ ] **Step 1: Add the `FillSeafloorBelow` method**

In `GenSeafarerStructures.cs`, find `private void ClearFootprint(IServerChunk[] chunks, Cuboidi bounds, int chunkX, int chunkZ)` (around line 944). Add the following private method directly below `ClearFootprint`:

```csharp
        /// <summary>
        /// Raises the seafloor under a chunk slice of an opted-in structure.
        /// For each XZ column inside the slice that the schematic actually
        /// occupies (per def.schematicColumnMask), fills from the existing
        /// terrain Y up to one block below the schematic's lowest solid block
        /// using def.seafloorFillBlockId. Clears any fluid in the affected
        /// cells. Caller must guarantee def.seafloorFillBlockId &gt; 0 and
        /// def.schematicColumnMask != null.
        /// </summary>
        private void FillSeafloorBelow(
            SeafarerStructure def,
            SeafarerStructureLocation loc,
            IChunkColumnGenerateRequest request,
            int chunkX,
            int chunkZ)
        {
            var chunks = request.Chunks;
            var mapChunk = chunks[0].MapChunk;
            int mapSizeY = api.WorldManager.MapSizeY;

            int bottomY = loc.Location.Y1 + def.schematicLowestY;
            int fillTopY = bottomY - 1;
            if (fillTopY < 0) return;
            if (fillTopY >= mapSizeY) fillTopY = mapSizeY - 1;

            int chunkMinX = chunkX * chunksize;
            int chunkMinZ = chunkZ * chunksize;
            int clipMinX = Math.Max(loc.Location.X1, chunkMinX);
            int clipMaxX = Math.Min(loc.Location.X2, chunkMinX + chunksize);
            int clipMinZ = Math.Max(loc.Location.Z1, chunkMinZ);
            int clipMaxZ = Math.Min(loc.Location.Z2, chunkMinZ + chunksize);
            if (clipMinX >= clipMaxX || clipMinZ >= clipMaxZ) return;

            int fillId = def.seafloorFillBlockId;
            var mask = def.schematicColumnMask;
            int maskW = mask.GetLength(0);
            int maskD = mask.GetLength(1);

            for (int worldX = clipMinX; worldX < clipMaxX; worldX++)
            {
                int localChunkX = worldX - chunkMinX;
                int localSchemX = worldX - loc.Location.X1;
                if (localSchemX < 0 || localSchemX >= maskW) continue;

                for (int worldZ = clipMinZ; worldZ < clipMaxZ; worldZ++)
                {
                    int localChunkZ = worldZ - chunkMinZ;
                    int localSchemZ = worldZ - loc.Location.Z1;
                    if (localSchemZ < 0 || localSchemZ >= maskD) continue;

                    if (!mask[localSchemX, localSchemZ]) continue;

                    int terrainY = mapChunk.WorldGenTerrainHeightMap[localChunkZ * chunksize + localChunkX];
                    int startY = terrainY + 1;
                    if (startY > fillTopY) continue;

                    for (int y = startY; y <= fillTopY; y++)
                    {
                        int chunkIndex = y / chunksize;
                        if (chunkIndex < 0 || chunkIndex >= chunks.Length) continue;
                        int localY = y % chunksize;
                        var chunk = chunks[chunkIndex];
                        int blockIndex = (localY * chunksize + localChunkZ) * chunksize + localChunkX;
                        chunk.Data.SetBlockUnsafe(blockIndex, fillId);
                        chunk.Data.SetFluid(blockIndex, 0);
                    }
                }
            }

            UpdateHeightmap(request, worldgenBlockAccessor);
        }
```

- [ ] **Step 2: Build to verify the new method compiles**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```
Expected: `Build succeeded` with 0 errors. The method is unused so far — Task 5 wires it in.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs && \
git commit -m "Add FillSeafloorBelow seafloor-raise pass"
```

---

## Task 5: Wire `FillSeafloorBelow` into `PlaceStorySlices`

**Files:**
- Modify: `Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs` (method `PlaceStorySlices`, around lines 595–613)

The fill must run after `PlacePartial` placed at least one block in this slice but before the existing post-placement steps (heightmap update for surface modes, grass gen).

- [ ] **Step 1: Add the call after `PlacePartial` in `PlaceStorySlices`**

Find this block in `PlaceStorySlices` (currently around lines 595–613):

```csharp
                Block rockBlock = ResolveRockBlock(def, loc, request, startPos);
                int blocksPlaced = def.schematicData.PlacePartial(
                    chunks, worldgenBlockAccessor, api.World,
                    chunkX, chunkZ, startPos, EnumReplaceMode.ReplaceAll,
                    ToBaseGamePlacement(def.Placement),
                    GlobalConfig.ReplaceMetaBlocks, GlobalConfig.ReplaceMetaBlocks,
                    def.resolvedRockTypeRemaps, def.replacewithblocklayersBlockids, rockBlock, def.DisableSurfaceTerrainBlending
                );

                if (blocksPlaced <= 0) continue;

                // Post-placement fix-ups that later worldgen passes depend on.
                // Matches base-game GenStoryStructures post-PlacePartial block.
                if (def.Placement is EnumSeafarerPlacement.Surface
                        or EnumSeafarerPlacement.SurfaceRuin
                        or EnumSeafarerPlacement.Coastal
                        or EnumSeafarerPlacement.Underwater)
                {
                    UpdateHeightmap(request, worldgenBlockAccessor);
                }
```

Insert the seafloor-fill call between `if (blocksPlaced <= 0) continue;` and the `// Post-placement fix-ups` comment:

```csharp
                if (blocksPlaced <= 0) continue;

                if (def.seafloorFillBlockId > 0 && def.schematicColumnMask != null)
                {
                    FillSeafloorBelow(def, loc, request, chunkX, chunkZ);
                }

                // Post-placement fix-ups that later worldgen passes depend on.
                // Matches base-game GenStoryStructures post-PlacePartial block.
                if (def.Placement is EnumSeafarerPlacement.Surface
                        or EnumSeafarerPlacement.SurfaceRuin
                        or EnumSeafarerPlacement.Coastal
                        or EnumSeafarerPlacement.Underwater)
                {
                    UpdateHeightmap(request, worldgenBlockAccessor);
                }
```

- [ ] **Step 2: Build to verify wiring**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```
Expected: `Build succeeded` with 0 errors. There should be no unused-field warnings now.

- [ ] **Step 3: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/WorldGen/GenSeafarerStructures.cs && \
git commit -m "Run FillSeafloorBelow after PlacePartial in story slices"
```

---

## Task 6: Opt Tortuga in via JSON config

**Files:**
- Modify: `Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json` (the `tortuga` entry inside `structures`)

- [ ] **Step 1: Add `seafloorFillBlock` to the Tortuga entry**

Find the Tortuga structure entry (the one with `"code": "tortuga"`). Locate the `"yOffset": -12,` line and add the new property immediately after it:

Before:
```json
      "code": "tortuga",
      ...
      "useWorldgenHeight": true,
      "yOffset": -12,
      "disableSurfaceTerrainBlending": false,
```

After:
```json
      "code": "tortuga",
      ...
      "useWorldgenHeight": true,
      "yOffset": -12,
      "seafloorFillBlock": "game:muddygravel",
      "disableSurfaceTerrainBlending": false,
```

- [ ] **Step 2: Run the asset validator**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```
Expected: exit 0, 0 errors. Warnings (if any unrelated to this change) are acceptable.

- [ ] **Step 3: Build to confirm nothing else broke**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && \
  dotnet build /mnt/d/Development/vs/vsmod-seafarer/Seafarer/Seafarer/Seafarer.csproj
```
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git add Seafarer/Seafarer/assets/seafarer/worldgen/seafarerstructures.json && \
git commit -m "Opt Tortuga in to seafloor fill with muddy gravel"
```

---

## Task 7: In-game verification

This is a manual integration test. The repo has no automated worldgen tests; the goal is to exercise the new pass against a real Tortuga generation and confirm the fill behaves as designed.

**Test environment:**
- Vintage Story 1.21+ with the freshly built Seafarer mod loaded.
- A new world with a fresh seed (so Tortuga determination + generation runs from scratch).

- [ ] **Step 1: Launch a new world**

Build is copied to `Seafarer/bin/Debug/Mods/mod/` per `CLAUDE.md`. Either copy that to the user's VS mods folder or launch VS with `--addOrigin` pointing at the build output. Create a new survival world with default settings (or your usual test settings) and let it load to spawn.

- [ ] **Step 2: Teleport to Tortuga**

In the in-game console:
```
/wgen seafarer tp tortuga
```
Expected: "Teleporting to tortuga". You arrive on the Tortuga island.

- [ ] **Step 3: Inspect the underside**

Switch to spectator mode (`/gamemode spectator`) and fly under the island. Confirm:
- No visible open-water gap between Tortuga's lowest blocks and the seafloor anywhere across the footprint.
- Where Tortuga has solid blocks above, the seafloor under those columns is muddy gravel raised up to one block below Tortuga.
- Outside Tortuga's silhouette (where the schematic is empty), the seafloor is unchanged — no muddy gravel plateau extending past the island.
- Heightmap behaves: rain/light reads correctly (no obvious lighting glitches at the new seafloor).

- [ ] **Step 4: Server-log check**

Tail the server log (`%appdata%/VintagestoryData/Logs/server-main.txt` on Windows; equivalent on Linux/Mac) during world generation. Expected: no new errors or warnings related to Seafarer structures. In particular no "set block outside generating chunks" warnings tied to the fill pass.

- [ ] **Step 5: Regression check on other story structures**

Teleport to another story structure that does **not** use seafloor fill, e.g.:
```
/wgen seafarer tp potatoking
```
Confirm it generated normally — no muddy gravel in or around it, no terrain artifacts.

- [ ] **Step 6: Capture findings**

If everything looks right, document the manual run in the commit message of the next change or in a follow-up note. If something looks off (gap remains, plateau extends too far, unwanted muddy gravel elsewhere), file a defect with screenshots; do NOT mark the task complete.

- [ ] **Step 7: (Only if all checks pass) push the branch**

```bash
cd /mnt/d/Development/vs/vsmod-seafarer && \
git push
```

---

## Notes for the implementer

- The `SeafarerStructure` class shadows `WorldGenStructureBase.Placement` with `EnumSeafarerPlacement` — Newtonsoft binds `placement` to the most-derived member. If serialization is acting weird, suspect that first.
- `chunksize` is inherited from `ModStdWorldGen` — don't redeclare it.
- `loc.Location.Y1` is the world Y of the schematic's local Y=0 (set in `ResolveStoryStartY`). Adding `def.schematicLowestY` (the lowest local Y with a solid block) gives the world Y of the structure's lowest solid block.
- `chunk.Data.SetBlockUnsafe` writes the solid layer. Pair every call with `chunk.Data.SetFluid(blockIndex, 0)` so any pre-existing water in that cell is removed.
- This intentionally runs only in `PlaceStorySlices`. If a future scattered roll needs the fill, mirror the same call site in `PlaceScatteredRolls`.
- Noise/jitter on the fill top is deferred (see spec "Out of scope"). Add as a follow-up plan if Tortuga's flat plateau looks too uniform once you eyeball it in-game.
