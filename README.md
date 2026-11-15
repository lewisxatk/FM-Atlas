# FM Atlas — Full Interface Rebuild (V9 development)

FM Atlas is a Windows desktop Football Manager scouting, career intelligence, analytics, squad-planning and editing workspace.

> **Current development line:** `rebuild/v8-full-product-rebuild` (preserves the V8/R7.2.0 application and all earlier work). Draft integration PR [#4](https://github.com/lewisxatk/FM-Atlas/pull/4) remains open. The project is now prioritising the complete Watched-inspired × FMRTE-style interface rebuild before the remaining data/editor expansion.

## Product direction

**Watched atmosphere × FMRTE workflow.** Graphite/near-black surfaces, precise orange signal accents, strong editorial typography, telemetry-style labels and a clean information hierarchy. Quick Search and Advanced Search are separate entry points; records open in full-width, persistent main tabs. Dense tables, charts and profile content remain opaque and readable.

This is a full application/interaction rebuild, not a palette-only reskin. See [V9 full UI rebuild specification](docs/V9-FULL-UI-REBUILD.md).

## What is preserved

The new presentation layer is being built over the existing R7.2.0/V8 foundation. Existing save import, read-only FM27 connection, player/staff/club records, advanced search, CA/PA filters, indexed statistics, role-fit estimates, Value Finder, shortlists, graphics lookup and diagnostics are retained. New UI work must not delete or silently replace these paths.

## Latest implementation checkpoint — 15 November 2026

- The player/recruitment workspaces now use a full-width player-pool tab and full-size player record tabs. The old narrow permanent profile inspector is removed from this route.
- The record profile reuses the existing Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source/Evidence sections.
- Player pool search, position, CA/PA filters, status, paging and loaded results are retained when navigating away and back.
- The persistent pool tab cannot be closed with Ctrl+W; profile-load failures are reported in the pool status line.
- A shared Monolith design library provides semantic dark/light tokens, display/body/telemetry typography, reusable panels, action buttons and theme-aware inputs.
- The new app shell source includes grouped navigation, persistent Quick Search (Enter/Ctrl+K), contextual page title, full workspace surface and a compact live-career/import footer.
- The new application shell and its latest refinements are **not yet confirmed by the latest Windows build**. The last fully successful Windows workflow was run #746 at commit `8d9e27f91cbf4d8238d7a66182a862b94e86651b`, which verified the full-width player tabs and initial Monolith design system, including .NET Release build, career-import integration tests and self-contained x64 publish. The workflow run and artifact details are recorded below.

### Verified Windows checkpoint

- [Windows Actions run #746](https://github.com/lewisxatk/FM-Atlas/actions/runs/43772395059) — succeeded for commit `8d9e27f91cbf4d8238d7a66182a862b94e86651b`.
- Artifact: `FM-Atlas-V8-win-x64` (68,920,750 bytes).
- SHA-256: `cb971407cf3e82aa5d0ddb107d60c0db6baf58652c6947a9d94ccd9e819021df`.
- Artifact expiry: 29 November 2026.

**Important:** this successful run predates the newest application-shell changes. Do not describe the latest shell as compiled/tested until a new workflow run for the latest branch head succeeds. No full visual acceptance or real FM27/Game Pass runtime acceptance has been completed.

## Data integrity and editing safety

- Display only statistics and fields grounded in FM save/game/database sources.
- Keep Save, Live, Atlas-calculated, Provisional, Unavailable and Unknown evidence distinct.
- Missing values are not zero; never invent hidden attributes or proprietary FM formulas.
- CA/PA and player attributes remain distinct from observed season/match performance.
- Live Apply/Undo remains disabled until exact build/storefront field mappings, UI witnesses, preflight, read-back and rollback are independently certified. Test only against a disposable offline career copy.

## Start here

- [V9 Full UI Rebuild](docs/V9-FULL-UI-REBUILD.md) — current product direction, scope and delivery sequence.
- [V8 implementation checklist](docs/V8_IMPLEMENTATION_CHECKLIST.md) — source implementation and verification tracking.
- [V8 full rebuild specification](docs/architecture/FM-ATLAS-V8-FULL-REBUILD.md) — previous interaction and data architecture.
- [V8 reverse-engineering notes](docs/V8_REVERSE_ENGINEERING_NOTES_2026-11-14.md) — FMRTE, Genie Scout, FMSuperScout, FMLibreEditor and fm-editor research.
- [V8 implementation status](docs/architecture/FM-ATLAS-V8-IMPLEMENTATION-STATUS.md).
- [Application setup and test instructions](application/R7.2.0/FM-Atlas-R7.2.0-Work/README.md).
- [Draft integration pull request #4](https://github.com/lewisxatk/FM-Atlas/pull/4).

## Research and licensing

The project uses FMRTE and Genie Scout as behavioural references and FMSuperScout, FMLibreEditor, fm-editor, fmsave and FM View Exporter as research references. Their proprietary formulas and GPL source are not copied into Atlas. Build-specific field evidence and licensing must be checked before adopting implementation details.
