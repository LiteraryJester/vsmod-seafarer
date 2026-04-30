# Mod Pages — Seafarer & Progression Framework

**Date:** 2026-04-29
**Author:** alexa (with Claude)
**Status:** Approved for implementation

## Problem

Seafarer's existing public mod page (`vsmod-salt-and-sand/modpage.html`, last updated for v0.2.4) is now stale. Since it was written, the mod has shipped:

- The Outrigger boat (advanced shipbuilding, schematic + trait gates)
- Tortuga — the port hub previously billed as "What's Next" — with 5 NPCs (Morgan, Drake, Dawn Marie, Celeste, Potato King), 4 main rebuilding quests, secondary follow-up quests, and an evolving-trader system
- A training/professions system (Shipwright, Bear Hunter, Buccaneer, Gardener, Brewer, Pie Master) with an L-key Training Ledger
- The marine-varnish / oiled-canvas / waxed-canvas / seasoned-plank / varnished-plank crafting chain, partly trait-gated

In parallel, the underlying training/quest/evolving-trader systems were extracted into a new standalone framework mod, **Progression Framework** (`vsmod-progression-framework`, modid `progressionframework`, v1.0.0), which Seafarer now hard-depends on. That framework needs its own public mod page.

## Goal

Produce two public mod-hub pages, written as self-contained `<div>`-wrapped HTML pasteable into the Vintage Story mod hub editor:

1. **Seafarer** — a full rewrite of the existing modpage that promotes Tortuga and the Outrigger to headline features and trims the long server-config tables to a single condensed reference.
2. **Progression Framework** — a brand-new page that pitches the mod to two audiences (players who pick it up because Seafarer requires it, and modders who want to integrate). Includes compact, copyable JSON examples for adding quests, adding training, and gating a recipe by a crafting trait.

Both pages target the same mod-hub editor and use the visual style of the popular reference mods we surveyed (BetterRuins, FoodShelves, OverhaulLib, BloodTrail, PrimitiveSurvival).

## Non-goals

- No images embedded in HTML — the mod hub handles screenshots separately.
- No changelog table — the hub auto-generates that from version uploads.
- No "What's Next" / roadmap section on either page (deferred until a roadmap exists).
- No new in-game features. This is documentation work only.
- No update to the legacy `vsmod-salt-and-sand/modpage.html` — that repo is archived.

## Visual style

Both pages mirror the structural conventions of the existing Seafarer modpage and the popular reference pages:

- Outer wrapper: `<div style="max-width: 820px; margin: 0 auto; font-family: sans-serif; line-height: 1.6;">…</div>`
- Centered hero block: `<h2>` tagline + paragraph pitch + version banner
- `<hr style="border: none; border-top: 1px solid #555; margin: 1.5em 0;">` between major sections
- Top-level sections use **emoji-prefixed `<h3>` headings** (BetterRuins style) — adds visual scan-ability without overwhelming
- Sub-sections use plain `<h4>`
- Bullet lists for feature enumerations; bold for feature names; em-dash for inline definitions ("**Feature** — description")
- Server-config tables wrapped in a `<div class="spoiler">` collapsible block (consistent with old page)
- Inline `<code>` for asset paths, item codes, and JSON keys
- `<pre><code>` blocks (no syntax highlighting) for the modder JSON examples on the Progression Framework page

## Seafarer page — section outline

Output: `D:\Development\vs\vsmod-seafarer\modpage.html`

1. **Hero block** — center-aligned `<h2>` tagline ("Get out of the mines. The ocean is calling."), one-paragraph pitch, then a version banner showing current 1.22 release and the 1.21 backport (current versions filled in at write time from `Seafarer/Seafarer/modinfo.json`).
2. **⚓ Seafaring** — Log Barge, **Outrigger** (NEW — schematic-built in two stages or trait-gated for shipwrights), Waterskin, Sails (canvas / oiled canvas / waxed canvas variants).
3. **🏝 Tortuga** *(headline new section)* — short setup paragraph (port torn from old world by temporal typhoon), the five NPCs each as a one-line bullet (role + a sentence of personality), the rebuilding loop (4 main quests → score 0→4 → state advances Destitute → Struggling), and a sentence on evolving traders ("NPC stock and dialogue change as you complete work for them"). Mention the discoverable nature of the port.
4. **🥩 Food Preservation** — Salt Pan, Prep Table, Drying Frame, the salting → drying → soaking chain (kept from old page; tightened wording).
5. **🐚 Shellfish & Foraging** — Live Clams, Shell Crushing, Mud Raking (kept from old page).
6. **🌶 Tropical Crops** — Corn (nixtamalization, masa, tortillas, corn beer/whiskey), Coconut Palms, Sugar Cane, Chilies & Tomatoes (kept).
7. **🔥 Cooking — The Griddle** — Griddle tiers (clay → copper → bronze → iron → steel), griddle recipes, Burritos (kept).
8. **🍶 Fermentation & Storage** — Onggi Jar, Garum/Soy/Hot Sauce/Miso, Kimchi, fermented vegetables/meat, Amphora (Liquid + Storage). Merges old "Fermentation & Condiments" and "Storage & Containers" into one section.
9. **❄️ Environment** — Exposure System (heatstroke/frostbite, three tiers).
10. **📜 Training & Quests** — short bridge section: "Seafarer ships training/quest content for the **Progression Framework** (required dependency). Five professions — Shipwright, Bear Hunter, Buccaneer, Gardener, Brewer, Pie Master. Press L in-game to open the Training Ledger and quest log." Hyperlink to the Progression Framework mod-hub page.
11. **🔧 Compatibility** — same shape as old page: paragraph saying everything is net new content; bullet list of tested-and-supported mods (ConfigLib, Hydrate or Diedrate, Expanded Foods); add a separate **Required dependency** callout for Progression Framework above the list.
12. **⚙️ Server Configuration (collapsible spoiler)** — single condensed table replacing the old four-table layout. Columns: **Subsystem | Setting | Default | What it controls**. Rows merge Exposure, Drying Frame, Salt Pan, Griddle, Mud Rake; defaults only, no prose intros. Aim for ~20–25 rows total instead of the old ~35 rows split across four tables.

## Progression Framework page — section outline

Output: `D:\Development\vs\vsmod-progression-framework\modpage.html`

1. **Hero block** — `<h2>` tagline ("Quests, training, and evolving traders for Vintage Story modders."), one-paragraph pitch describing the three subsystems, version banner.
2. **✨ For Players** — short. The page must make clear this is a framework, not a content mod. Bullet list:
   - Press **L** in-game to open the Training Ledger (training tab + quest log tab).
   - Quests, professions, and trader stock changes come from whichever consumer mod you have installed.
   - Currently shipped by: **Seafarer** (link). Other mods may follow.
   - This mod adds nothing on its own — install a consumer mod to actually use it.
3. **🧩 For Modders — Overview** — one paragraph: "Drop JSON into your mod's `assets/<yourmod>/config/{quests,training,tradelists,dialogue}/` and the framework discovers it at `AssetsFinalize`. Register custom objective/reward/XP-source handlers from C# via `RegisterObjectiveType`, `RegisterRewardType`, `RegisterXpSourceKind`." Plus a bullet list of asset-discovery paths and the protected identifiers (entity class `EntityEvolvingTrader`, item class `ItemTrainingBook`, hotkey `progressionframeworkledger`, network channel `progressionframework:questsync`).
4. **📋 Adding a Quest** — 1-paragraph intro explaining the JSON shape (`code`, `npc`, `scope`, objectives with type/required/rewards). Then a ~12-line minimal example showing a single `delivery` objective with one `addSellingItem` reward. Compact, fictional but valid.
5. **🎓 Adding Training** — 1-paragraph intro explaining `professions.json` (profession → trainings → levels → trait grant) and `xp.json` (XP source kinds: `craft`, `dialogue`, `book`). Then a ~10-line `professions.json` snippet (one profession, one training, one level) and a ~6-line `xp.json` snippet (one `craft` source).
6. **🔒 Gating a Recipe with a Crafting Trait** — 1-paragraph intro: "Add `requiresTrait: \"<trait-code>\"` to a grid recipe variant. The trait is granted at the level the training defines (see Adding Training). A common pattern is to ship two recipe variants — one always-available with extra ingredients, one trait-gated with a simpler recipe." Then a ~10-line two-variant grid recipe example matching this pattern.
7. **🛠 Built-in Handlers** — three short lists:
   - **Quest objective types:** `delivery`, `kill`
   - **Quest reward types:** `addSellingItem`, `giveItem`, `awardTrainingXP`, `incrementEntityVariable`
   - **XP source kinds:** `craft`, `dialogue`, `book`
8. **🔧 Compatibility & Requirements** — required Vintage Story version (1.22.0-rc.7+), no known incompatibilities, link to source/issues.

## Examples on the Progression Framework page

All three examples are **compact** (per Q1 = A): minimal-shape, fictional but syntactically valid, ~5–15 lines each. They are not pulled verbatim from Seafarer files. Modders who want a fuller reference can link out to the Seafarer GitHub repo.

Each example uses a placeholder mod domain (`mymod:`) and placeholder NPC / training / trait codes (`farmer`, `gardening`, `green-thumb`, etc.) so it reads as a generic template rather than a copy of Seafarer's content.

## Open content TBDs at write-time

These are filled in when actually generating the HTML, not pre-decided in this spec:

- Exact current 1.22 and 1.21 version strings (read from each mod's `modinfo.json`).
- Whether to keep the existing 1.21 backport caveat paragraph from the old modpage.
- Final phrasing of NPC personality blurbs (one sentence each; condensed from `quests/Tortuga.md`).

## Risks / things to verify post-write

- The single condensed config table on the Seafarer page must still show every setting that was tunable in the old four-table layout. After write, cross-check against old `modpage.html` line-for-line.
- The trait-gated recipe example must syntactically match Vintage Story's grid-recipe schema (`requiresTrait` is a recipe-level field, not an ingredient-level field). Verify against the real `outrigger-rollers.json`.
- The asset-discovery path list and built-in handler lists on the Progression Framework page must match the framework's `CLAUDE.md` content-discovery table and registered handler kinds.

## Out of scope

- Updating archived `vsmod-salt-and-sand/modpage.html`.
- Embedding screenshots or banner images.
- Generating banner / hero / mini PNGs.
- Translating either page (English only).
- Pushing the pages to mods.vintagestory.at — that's a manual paste step the user does after review.

## Implementation transition

Once this spec is approved, generate the implementation plan via the `superpowers:writing-plans` skill.
