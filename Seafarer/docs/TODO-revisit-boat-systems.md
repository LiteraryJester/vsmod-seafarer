# TODO: Revisit boat health & traits systems

**Status:** Disabled 2026-04-29. C# code remains, JSON wiring removed.

Two related features were built and disabled:

1. **Boat health system** — HP, combat/collision/storm damage, repair via glue/varnish, wreckage drop on death.
   Spec: `docs/superpowers/specs/2026-04-28-boat-raft-health-design.md`
   Plan: `docs/superpowers/plans/2026-04-28-boat-raft-health.md`

2. **Boat traits system** — sail traits (canvas/oiled/waxed) and material traits (seasoned/varnished hull) with rarity color coding.
   Spec: `docs/superpowers/specs/2026-04-28-boat-traits-design.md`
   Plan: `docs/superpowers/plans/2026-04-28-boat-traits.md`

## Why disabled

The right-click apply path for sail traits collides with vanilla boat interactions (mount, rideable accessories). Tried gating on Ctrl+right-click — interaction still didn't reach our behavior. Needs investigation into whether Ctrl is reserved by something upstream, or whether `EntityBoat` short-circuits collectible-behavior lookups before they fire. Until that's solved, the whole feature stack is dormant.

## What's still in place (live code, dormant configuration)

- C#: `EntityBehaviorShipMechanics`, `BehaviorBoatRepair`, `BehaviorBoatSail`, `BoatTrait` + registry. All registered in `SeafarerModSystem.Start`. Inert because no boat declares the behaviors.
- Asset: `assets/seafarer/config/boat-traits.json` (still loaded, just unused).
- Lang: trait names, rarity names, color templates, repair fail message — all in `en.json`.

## What was removed

- `boat-outrigger.json` and `boat-logbarge.json`: the `health` and `shipmechanics` behavior entries plus the `extendShipMechanicsByType` / `extendShipMechanics` config blocks.
- Seven JSON patches in `assets/seafarer/patches/`:
  - `vanilla-boat-raft-shipmechanics.json`
  - `vanilla-boat-sailed-shipmechanics.json`
  - `glueportion-boatrepair.json`
  - `varnishportion-marine-boatrepair.json`
  - `canvas-sail-boatsail.json`
  - `oiled-canvas-sail-boatsail.json`
  - `waxed-canvas-sail-boatsail.json`

## Re-enabling

The disable commit is one revert away from a fully-working state (modulo the Ctrl interaction issue that still needs solving). When ready:

1. `git revert <disable-commit-sha>` to restore boat JSONs and patches.
2. Solve the right-click conflict — options to investigate:
   - Add interaction help via `GetHeldInteractionHelp` so the prompt shows in-world.
   - Try a different gate (Shift instead of Ctrl).
   - Investigate `EntityBoat.OnInteract` — possibly Harmony-patch it to call our behavior path before its own handling.
   - Move sail apply to a different surface entirely (e.g., a workbench block or a quest-driven recipe).
3. Re-run the playtest checklists from both plans.
