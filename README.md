# FM Atlas V8 — Atlas Liquid

FM Atlas is a Windows desktop application for Football Manager scouting, career intelligence, recruitment, squad analysis, editing research and graphics tools.

> **V8 status (14 November 2026):** The integrated V8 source rebuild is in progress on the isolated branch `rebuild/v8-full-product-rebuild`. Draft pull request [#4 — FM Atlas V8 · Atlas Liquid](https://github.com/lewisxatk/FM-Atlas/pull/4) is open against `fix/r7.2.0-release-build-integration`. It is **not merged or release-ready**. A clean Windows Release build, startup test, full feature regression and Football Manager acceptance tests are still required.

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
- Save import now streams individual tables into a temporary NDJSON bundle and imports each row incrementally in one SQLite transaction; progress, elapsed time and cancellation preserve the last committed snapshot. A Python mock-save streaming test passes on Windows CI; the C# build of this exact integration is in progress.
- SQLite write serialization and a 30-second lock timeout to address concurrent writer contention.

**Do not equate source implementation with verification.** The new NDJSON adapter regression test passes, but the C# Release compile/package for the streaming importer is not yet confirmed and large real-save import behaviour still needs testing. Nested Advanced Search logic, multi-sort/multi-select comparison, full MVVM separation, deeper development/tactics/recruitment systems, runtime performance and Football Manager stability acceptance remain open. The durable checklist records the exact state.

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
- Build and package success must be confirmed by the Windows workflow and a clean Windows PC run. No successful V8 Windows build or runtime test is claimed by this README.

## Historical source and research

The original R7.2.0 checkpoint, recovery inventory, reverse-engineering notes and older roadmap remain available in the repository history and documentation. Game fields, hidden attributes, role ratings and any inferred metrics must retain explicit evidence and confidence; do not invent mappings or formulas.
