# Ocean Structure Worldgen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a config-driven worldgen system that places structures in underwater, coastal, and buried-underwater locations, starting with the Crimson Rose shipwreck.

**Architecture:** A standalone `GenOceanStructures` ModSystem hooks into VS's `ChunkColumnGeneration` at the `TerrainFeatures` pass. It reads structure definitions from `oceanstructures.json`, detects ocean/coast via `IMapRegion.OceanMap`/`BeachMap`, places schematics with `BlockSchematic.Place()`, and registers them via `AddGeneratedStructure()` for locator map discovery.

**Tech Stack:** C# / .NET 10.0, Vintage Story API (IWorldGenBlockAccessor, BlockSchematic, IMapRegion, LCGRandom)

**Spec:** `docs/superpowers/specs/2026-04-16-ocean-worldgen-design.md`

---

### Task 1: Config Data Model

Define the C# classes that deserialize `oceanstructures.json`.

**Files:**
- Create: `Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Create the config data model classes**

Create `Seafarer/WorldGen/GenOceanStructures.cs` with the config POCOs and the empty ModSystem shell:

```csharp
using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Seafarer.WorldGen
{
    public enum EnumOceanPlacement
    {
        Underwater,
        Coastal,
        BuriedUnderwater
    }

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
    }

    public class OceanStructuresConfig
    {
        public OceanStructureDef[] Structures = Array.Empty<OceanStructureDef>();
    }

    public class GenOceanStructures : ModSystem
    {
        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
        public override double ExecuteOrder() => 0.31;
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "feat(worldgen): add ocean structure config data model"
```

---

### Task 2: Config Loading and Schematic Caching

Load `oceanstructures.json`, resolve schematic assets, and cache 4 rotations per schematic.

**Files:**
- Modify: `Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Add schematic loading and caching fields**

Add these fields and the `StartServerSide` + `InitWorldGen` methods to `GenOceanStructures`:

```csharp
public class GenOceanStructures : ModSystem
{
    private ICoreServerAPI sapi;
    private OceanStructuresConfig config;
    private IWorldGenBlockAccessor worldgenBlockAccessor;
    private LCGRandom rand;
    private int chunksize;
    private int regionSize;
    private int regionChunkSize;

    // Key: structure code, Value: array of schematic variants.
    // Each variant is an array of 4 rotations (0, 90, 180, 270).
    private Dictionary<string, BlockSchematic[][]> cachedSchematics = new();

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;
    public override double ExecuteOrder() => 0.31;

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;

        api.Event.InitWorldGenerator(InitWorldGen, "standard");
        api.Event.ChunkColumnGeneration(OnChunkColumnGen, EnumWorldGenPass.TerrainFeatures, "standard");
        api.Event.GetWorldgenBlockAccessor(chunkProvider =>
        {
            worldgenBlockAccessor = chunkProvider.GetBlockAccessor(false);
        });
    }

    private void InitWorldGen()
    {
        chunksize = GlobalConstants.ChunkSize;
        regionSize = sapi.WorldManager.RegionSize;
        regionChunkSize = regionSize / chunksize;
        rand = new LCGRandom(sapi.WorldManager.Seed ^ 8415718);

        LoadConfig();
    }

    private void LoadConfig()
    {
        cachedSchematics.Clear();

        var asset = sapi.Assets.TryGet(new AssetLocation("seafarer", "worldgen/oceanstructures.json"));
        if (asset == null)
        {
            Mod.Logger.Notification("No oceanstructures.json found, ocean structure generation disabled.");
            config = new OceanStructuresConfig();
            return;
        }

        config = asset.ToObject<OceanStructuresConfig>();

        foreach (var def in config.Structures)
        {
            var variants = new List<BlockSchematic[]>();

            foreach (var schematicPath in def.Schematics)
            {
                var schematicAsset = sapi.Assets.TryGet(
                    new AssetLocation("seafarer", "worldgen/schematics/" + schematicPath + ".json")
                );
                if (schematicAsset == null)
                {
                    Mod.Logger.Warning("Ocean structure '{0}': schematic not found at '{1}', skipping.", def.Code, schematicPath);
                    continue;
                }

                var baseSchematic = schematicAsset.ToObject<BlockSchematic>();
                baseSchematic.Init(worldgenBlockAccessor);

                var rotations = new BlockSchematic[4];
                for (int r = 0; r < 4; r++)
                {
                    var copy = baseSchematic.ClonePacked();
                    copy.TransformWhilePacked(sapi.World, EnumOrigin.BottomCenter, r * 90);
                    copy.Init(worldgenBlockAccessor);
                    rotations[r] = copy;
                }

                variants.Add(rotations);
            }

            if (variants.Count > 0)
            {
                cachedSchematics[def.Code] = variants.ToArray();
            }
            else
            {
                Mod.Logger.Warning("Ocean structure '{0}': no valid schematics found, will not generate.", def.Code);
            }
        }

        Mod.Logger.Notification("Loaded {0} ocean structure definitions with {1} total schematic variants.",
            config.Structures.Length, cachedSchematics.Count);
    }

    private void OnChunkColumnGen(IChunkColumnGenerateRequest request)
    {
        // Placeholder — implemented in Task 3
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "feat(worldgen): add config loading and schematic caching"
```

---

### Task 3: Ocean/Coast Detection and Placement Logic

Implement `OnChunkColumnGen` — the core placement loop that detects ocean/coastal positions and places schematics.

**Files:**
- Modify: `Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Add the ocean detection helper**

Add this method to `GenOceanStructures`:

```csharp
private float GetOceanicity(IMapRegion mapRegion, int posX, int posZ)
{
    if (mapRegion.OceanMap == null || mapRegion.OceanMap.Data.Length == 0) return 0;

    var rlX = (posX / chunksize) % regionChunkSize;
    var rlZ = (posZ / chunksize) % regionChunkSize;
    var oFac = (float)mapRegion.OceanMap.InnerSize / regionChunkSize;

    var oceanUpLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac));
    var oceanUpRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac));
    var oceanBotLeft = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac), (int)(rlZ * oFac + oFac));
    var oceanBotRight = mapRegion.OceanMap.GetUnpaddedInt((int)(rlX * oFac + oFac), (int)(rlZ * oFac + oFac));

    return GameMath.BiLerp(
        oceanUpLeft, oceanUpRight, oceanBotLeft, oceanBotRight,
        (float)(posX % chunksize) / chunksize,
        (float)(posZ % chunksize) / chunksize
    );
}
```

- [ ] **Step 2: Add the beach detection helper**

Add this method to `GenOceanStructures`:

```csharp
private float GetBeachStrength(IMapRegion mapRegion, int posX, int posZ)
{
    if (mapRegion.BeachMap == null || mapRegion.BeachMap.Data.Length == 0) return 0;

    var rlX = (posX / chunksize) % regionChunkSize;
    var rlZ = (posZ / chunksize) % regionChunkSize;
    var bFac = (float)mapRegion.BeachMap.InnerSize / regionChunkSize;

    var beachUpLeft = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac), (int)(rlZ * bFac));
    var beachUpRight = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac + bFac), (int)(rlZ * bFac));
    var beachBotLeft = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac), (int)(rlZ * bFac + bFac));
    var beachBotRight = mapRegion.BeachMap.GetUnpaddedInt((int)(rlX * bFac + bFac), (int)(rlZ * bFac + bFac));

    return GameMath.BiLerp(
        beachUpLeft, beachUpRight, beachBotLeft, beachBotRight,
        (float)(posX % chunksize) / chunksize,
        (float)(posZ % chunksize) / chunksize
    );
}
```

- [ ] **Step 3: Add the placement validation method**

Add this method to `GenOceanStructures`:

```csharp
private bool IsValidPlacement(OceanStructureDef def, float oceanicity, float beachStrength, int waterDepth)
{
    switch (def.Placement)
    {
        case EnumOceanPlacement.Underwater:
        case EnumOceanPlacement.BuriedUnderwater:
            return oceanicity > 0 &&
                   waterDepth >= def.MinWaterDepth &&
                   waterDepth <= def.MaxWaterDepth;

        case EnumOceanPlacement.Coastal:
            bool isBeach = beachStrength > 0;
            bool isShallowOcean = oceanicity > 0 &&
                                  waterDepth >= 0 &&
                                  waterDepth <= def.MaxWaterDepth;
            return isBeach || isShallowOcean;

        default:
            return false;
    }
}
```

- [ ] **Step 4: Add the Y position calculation method**

Add this method to `GenOceanStructures`:

```csharp
private int CalculatePlacementY(OceanStructureDef def, int terrainHeight, BlockSchematic schematic)
{
    switch (def.Placement)
    {
        case EnumOceanPlacement.Underwater:
        case EnumOceanPlacement.Coastal:
            return terrainHeight + def.OffsetY;

        case EnumOceanPlacement.BuriedUnderwater:
            return terrainHeight - schematic.SizeY + def.OffsetY;

        default:
            return terrainHeight + def.OffsetY;
    }
}
```

- [ ] **Step 5: Add the global count check method**

Add this method to `GenOceanStructures`:

```csharp
private int CountExistingStructures(IMapRegion mapRegion, string code)
{
    int count = 0;
    foreach (var gs in mapRegion.GeneratedStructures)
    {
        if (gs.Code == code) count++;
    }
    return count;
}
```

- [ ] **Step 6: Implement OnChunkColumnGen**

Replace the placeholder `OnChunkColumnGen` with:

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

        rand.InitPositionSeed(chunkX + def.Code.GetHashCode(), chunkZ);
        float roll = (float)rand.NextInt(10000) / 10000f;
        if (roll > def.Chance) continue;

        if (def.MaxCount > 0 && CountExistingStructures(mapRegion, def.Code) >= def.MaxCount) continue;

        int localX = rand.NextInt(chunksize);
        int localZ = rand.NextInt(chunksize);
        int posX = chunkX * chunksize + localX;
        int posZ = chunkZ * chunksize + localZ;

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
    }
}
```

- [ ] **Step 7: Verify it compiles**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj
```
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "feat(worldgen): implement ocean/coast detection and structure placement"
```

---

### Task 4: Debug Command

Add `/ocean place <code>` and `/ocean list` server commands for testing.

**Files:**
- Modify: `Seafarer/WorldGen/GenOceanStructures.cs`

- [ ] **Step 1: Add debug command registration**

Add to the end of `StartServerSide`, after the event registrations:

```csharp
var parsers = api.ChatCommands.Parsers;
api.ChatCommands.GetOrCreate("ocean")
    .RequiresPrivilege(Privilege.controlserver)
    .BeginSubCommand("place")
        .WithDescription("Force-place an ocean structure at your position")
        .RequiresPlayer()
        .WithArgs(parsers.Word("code"))
        .HandleWith(OnCmdPlace)
    .EndSubCommand()
    .BeginSubCommand("list")
        .WithDescription("List all registered ocean structure codes")
        .HandleWith(OnCmdList)
    .EndSubCommand();
```

- [ ] **Step 2: Implement OnCmdPlace**

Add this method to `GenOceanStructures`:

```csharp
private TextCommandResult OnCmdPlace(TextCommandCallingArgs args)
{
    var code = (string)args[0];
    var player = args.Caller.Player as IServerPlayer;

    if (!cachedSchematics.TryGetValue(code, out var variants))
    {
        return TextCommandResult.Error("Unknown ocean structure code: " + code);
    }

    var pos = args.Caller.Player.CurrentBlockSelection?.Position
              ?? player.Entity.Pos.AsBlockPos;

    var variantRotations = variants[sapi.World.Rand.Next(variants.Length)];
    var schematic = variantRotations[sapi.World.Rand.Next(4)];

    int placed = schematic.Place(sapi.World.BlockAccessor, sapi.World, pos, EnumReplaceMode.ReplaceAll, true);
    sapi.World.BlockAccessor.Commit();

    return TextCommandResult.Success(string.Format("Placed '{0}' at {1} ({2} blocks)", code, pos, placed));
}
```

- [ ] **Step 3: Implement OnCmdList**

Add this method to `GenOceanStructures`:

```csharp
private TextCommandResult OnCmdList(TextCommandCallingArgs args)
{
    if (config.Structures.Length == 0)
    {
        return TextCommandResult.Success("No ocean structures configured.");
    }

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("Ocean structures:");
    foreach (var def in config.Structures)
    {
        bool hasSchematics = cachedSchematics.ContainsKey(def.Code);
        sb.AppendLine(string.Format("  {0} — {1}, chance={2}, schematics={3}{4}",
            def.Code, def.Placement, def.Chance,
            def.Schematics.Length,
            hasSchematics ? "" : " [NO VALID SCHEMATICS]"));
    }
    return TextCommandResult.Success(sb.ToString());
}
```

- [ ] **Step 4: Add required using for Privilege**

Ensure these usings are at the top of the file:

```csharp
using Vintagestory.API.Config;
```

`Privilege` lives in `Vintagestory.API.Common` which is already imported. `GlobalConstants` lives in `Vintagestory.API.Config`.

- [ ] **Step 5: Verify it compiles**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add Seafarer/WorldGen/GenOceanStructures.cs
git commit -m "feat(worldgen): add /ocean place and /ocean list debug commands"
```

---

### Task 5: Config JSON and Map Item Update

Create the `oceanstructures.json` config with the Crimson Rose entry, and update the map item to point to the new structure code.

**Files:**
- Create: `Seafarer/assets/seafarer/worldgen/oceanstructures.json`
- Modify: `Seafarer/assets/seafarer/itemtypes/lore/map-crimsonrose.json`

- [ ] **Step 1: Create oceanstructures.json**

Create `Seafarer/assets/seafarer/worldgen/oceanstructures.json`:

```json5
{
    structures: [
        {
            "code": "wreck-crimsonrose",
            "schematics": ["underwater/wreak-crimson-rose"],
            "placement": "underwater",
            "chance": 0.015,
            "minWaterDepth": 4,
            "maxWaterDepth": 50,
            "offsetY": 0,
            "maxCount": 5,
            "suppressTrees": true,
            "randomRotation": true
        }
    ]
}
```

- [ ] **Step 2: Update map-crimsonrose.json**

In `Seafarer/assets/seafarer/itemtypes/lore/map-crimsonrose.json`, change the `locatorPropsbyType` section. Replace:

```json
"schematiccode": "buriedtreasurechest",
```

with:

```json
"schematiccode": "wreck-crimsonrose",
```

And add the randomX/randomZ fields so the section reads:

```json5
"locatorPropsbyType": {
    "*": {
        "schematiccode": "wreck-crimsonrose",
        "waypointtext": "location-crimsonrose",
        "waypointicon": "x",
        "waypointcolor": [0.8, 0.1, 0.1, 1],
        "randomX": 15,
        "randomZ": 15
    }
}
```

- [ ] **Step 3: Run asset validation**

Run:
```bash
cd /mnt/d/Development/vs/vsmod-seafarer && python3 validate-assets.py
```
Expected: Exit 0 with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Seafarer/assets/seafarer/worldgen/oceanstructures.json Seafarer/assets/seafarer/itemtypes/lore/map-crimsonrose.json
git commit -m "feat(worldgen): add Crimson Rose wreck config and update locator map"
```

---

### Task 6: In-Game Verification

Test the complete system in Vintage Story.

**Files:**
- None (testing only)

- [ ] **Step 1: Build the mod**

Run:
```bash
export VINTAGE_STORY="/mnt/c/Users/alexa/AppData/Roaming/Vintagestory" && dotnet build Seafarer/Seafarer.csproj
```
Expected: Build succeeded.

- [ ] **Step 2: Launch Vintage Story and create a test world**

Create a new creative world. Check the server log for:
- `Loaded 1 ocean structure definitions with 1 total schematic variants.`
- No warnings about missing schematics.

- [ ] **Step 3: Test /ocean list**

In the chat, run:
```
/ocean list
```
Expected output:
```
Ocean structures:
  wreck-crimsonrose — Underwater, chance=0.015, schematics=1
```

- [ ] **Step 4: Test /ocean place**

Stand in a flat area or in water and run:
```
/ocean place wreck-crimsonrose
```
Expected: The Crimson Rose wreck schematic appears at the targeted block position. Verify it has aged planks, a chest, saltwater blocks, and aquatic clutter.

- [ ] **Step 5: Test with rotations**

Run `/ocean place wreck-crimsonrose` several times. Verify the wreck appears in different orientations (the random rotation should produce varied results).

- [ ] **Step 6: Test the locator map**

Give yourself the map:
```
/giveitem seafarer:map-crimsonrose 1
```
Use the map item (right-click). Expected: A waypoint appears on the world map pointing to a generated wreck location, OR "No location found" if no wrecks have been worldgen'd nearby (expected in a freshly created world with limited exploration).

- [ ] **Step 7: Verify worldgen placement**

Teleport to ocean areas and explore. Use `/wgen pos` or similar to check if wrecks generate on the ocean floor in the expected depth range. Alternatively, generate a large world and search for structures.

Note: Worldgen-placed structures only appear in newly generated chunks. Pre-existing chunks won't have wrecks.
