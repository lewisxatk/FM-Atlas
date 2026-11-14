# FM Atlas V8 — Atlas Liquid

FM Atlas is a Windows desktop application for Football Manager scouting, career intelligence, recruitment, squad analysis, editing research and graphics tools.

> **V8 status (14 November 2026):** The integrated V8 source rebuild is on the isolated branch `rebuild/v8-full-product-rebuild`. Draft pull request [#4 — FM Atlas V8 · Atlas Liquid](https://github.com/lewisxatk/FM-Atlas/pull/4) is open against `fix/r7.2.0-release-build-integration`. A complete hosted Windows .NET 8 Release compile, self-contained x64 publish and artifact upload passed on commit `77ec4364` (Actions run [#157](https://github.com/lewisxatk/FM-Atlas/actions/runs/43536598003)). Later importer-safety, shortlist-count and C# integration-test changes are newer than that artifact and are undergoing a fresh build/test run. V8 remains **not merged or release-ready** pending the latest integrated tests and your PC/runtime acceptance.

## V8 design

The new visual identity is **Atlas Liquid**, with three coordinated appearances:

- **Glass Dark** — recommended default, deep navy surfaces and cool glass controls.
- **Glass Light** — bright translucent controls over a light workspace.
- **Studio** — denser scouting and analytics presentation, sharing the same shell and components.

The shell includes a glass-style top action toolbar, descriptive hover/focus tooltips, grouped collapsible side navigation, active-workspace highlighting, Ctrl+K Global Search and system-aware workspace motion. Data grids and detailed content retain strong readable surfaces rather than blurring every part of the app.

## Current source milestone

The V8 branch preserves the R7.2.0 application and feature foundation while adding:
- Atlas Liquid shell, toolbar, hover/focus help, three appearance modes and collapsible navigation.
- Responsive navigation and improved player workspace activation.
- Universal Advanced Search builder across core player fields, stored attributes/personality and latest-save facts, with ALL/ANY logic, saved profiles, visible-column sets, paging and cancellation; performance-stat searches remain a separate player-period workspace.
- A Windows .NET 8 Release and self-contained x64 build/package workflow.
- Live-memory safety gates: the old full process scan and live Apply handlers are blocked in V8 until bounded scanning and field mappings pass independent validation.
- SQLite writer coordination and a 30-second busy timeout.
- Save import now streams individual tables into a temporary NDJSON bundle and imports each row incrementally in one SQLite transaction; progress, elapsed time and cancellation preserve the last committed snapshot. Manifest row counts are checked for every table before commit, final warnings are persisted, and the adapter refuses to overwrite an existing output directory.
- A Python regression test covers normalisation, table rows, manager-name privacy and preservation of an existing output folder. A new Windows .NET integration-test project covers successful import, truncated manifest counts, malformed NDJSON rollback and retention of the previous snapshot. Its first CI run is in progress.
- SQLite write serialization and a 30-second lock timeout to address concurrent writer contention.

**Do not equate source implementation with verification.** The integrated V8 Release compile/package has passed at commit `77ec4364`; the newer C# integration-test project and later importer/shortlist safeguards are undergoing their own Windows CI run. No real FM career import or Windows runtime/Football Manager stability acceptance has been claimed. Nested Advanced Search logic, multi-sort/player comparison, full MVVM separation, deeper development/tactics/recruitment systems and runtime performance remain open. The durable checklist records the exact state.

## Durable progress and design documents

- [V8 implementation checklist](docs/V8_IMPLEMENTATION_CHECKLIST.md) — ticked after each committed development unit; implementation and test verification tracked separately.
- [V8 full rebuild specification](docs/architecture/FM-ATLAS-V8-FULL-REBUILD.md)
- [V8 implementation status and limitations](docs/architecture/FM-ATLAS-V8-IMPLEMENTATION-STATUS.md)
- [Application setup and test instructions](application/R7.2.0/FM-Atlas-R7.2.0-Work/README.md)
- [Draft integration pull request #4](https://github.com/lewisxatk/FM-Atlas/pull/4)

## Safety and testing

- Use an extracted V8 folder separate from any existing FM Atlas installation. Existing local database and settings are preserved under `%LOCALAPPDATA%\\FmAtlas`; never delete that folder during testing.
- For career data, import a **copy** of an FM save using the local read-only bridge.
- The legacy live-memory scan and live Apply routes are deliberately disabled in this V8 source milestone. Do not try to use them or switch the gates on.
- A hosted Windows Release build and package has passed for the previous integrated commit. The latest importer/test changes must pass their own CI run, followed by a clean PC startup, visual, database, save-import and Football Manager stability acceptance before V8 can be called release-ready.

## Historical source and research

The original R7.2.0 checkpoint, recovery inventory, reverse-engineering notes and older roadmap remain available in the repository history and documentation. Game fields, hidden attributes, role ratings and any inferred metrics must retain explicit evidence and confidence; do not invent mappings or formulas.
