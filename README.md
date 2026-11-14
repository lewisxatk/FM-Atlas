# FM Atlas V8 — Monolith Glass

FM Atlas is a Windows desktop application for Football Manager scouting, career intelligence, recruitment, squad analysis, editing research and graphics tools.

> **V8 status (14 November 2026):** The integrated source remains on the isolated branch `rebuild/v8-full-product-rebuild`; draft PR [#4 — FM Atlas V8](https://github.com/lewisxatk/FM-Atlas/pull/4) is open against `fix/r7.2.0-release-build-integration`. This update adds read-only career/process/build/session detection, staff/human-manager extraction and live search/profile database integration, plus Monolith Glass. The read-only live entity-array scan runs only for exact supported profiles, with a two-minute deadline, toolbar cancellation and save-generation cancellation. FM performance and field-witness verification remain outstanding. Earlier Windows build/package runs #157 and #168 passed on older commits; they do **not** validate these newest changes. The current live-career regression tests and a fresh Windows Release build/test/publish run must pass before distributing this revision. Real Windows/Football Manager acceptance remains outstanding.

## V8 design

The primary visual identity is **Monolith Glass**: graphite/charcoal surfaces, restrained amber-orange accents, translucent raised navigation and toolbar materials, fine glass borders and minimal motion. Three alternate appearances remain available: **Glass Dark**, **Glass Light** and **Studio**. Monolith Glass is the first-run default; an existing saved theme choice is preserved.

The shell includes a glass-style top action toolbar, descriptive hover/focus tooltips, grouped collapsible side navigation, active-workspace highlighting, Ctrl+K Global Search and system-aware workspace motion. Data grids and detailed profile content retain solid, readable surfaces rather than blurring every cell.

## Current source milestone

The V8 branch preserves the R7.2.0 application and feature foundation while adding:
- Atlas Liquid shell, toolbar, hover/focus help, three appearance modes and collapsible navigation.
- Responsive navigation and improved player workspace activation.
- Universal Advanced Search builder across core player fields, stored attributes/personality and latest-save facts, with ALL/ANY logic, saved profiles, visible-column sets, paging and cancellation; performance-stat searches remain a separate player-period workspace.
- A Windows .NET 8 Release and self-contained x64 build/package workflow.
- Career/session detection: Atlas identifies supported Windows Steam/Game Pass processes by exact module fingerprint and observes the game's save-load generation counter. The 3-second monitor detects career/load changes and starts a profile-bounded read-only player/staff/club scan for a loaded supported session. Each scan has a two-minute deadline, toolbar cancellation and session-change cancellation. The database commits only a completed snapshot. Live Apply/editing remains blocked.
- SQLite writer coordination and a 30-second busy timeout.
- Save import now streams individual tables into a temporary NDJSON bundle and imports each row incrementally in one SQLite transaction; progress, elapsed time and cancellation preserve the last committed snapshot. Manifest row counts are checked for every table before commit, final warnings are persisted, and the adapter refuses to overwrite an existing output directory.
- A Python regression test covers normalisation, table rows, manager-name privacy and preservation of an existing output folder. A new Windows .NET integration-test project covers successful import, truncated manifest counts, malformed NDJSON rollback and retention of the previous snapshot. Its first CI run is in progress.
- SQLite write serialization and a 30-second lock timeout to address concurrent writer contention.

**Do not equate source implementation with verification.** Earlier commits passed hosted Windows compile/package and save-import tests; the latest player-coach identity shift, player nationality/personality/position fields, automatic staff/manager detection and Monolith Glass changes are newer than those runs. New regression fixtures exercise the live player/staff database joins and search filters, but they must pass in a fresh Windows workflow. No real FM27 session has been tested by this update, and the user's PC startup, UI, game-performance and save-switch acceptance gates remain open. The universal Advanced Search, player profiles and existing scouting features are preserved; nested filter groups, complete live field coverage, full MVVM separation and additional tactics/recruitment systems remain open.

## Durable progress and design documents

- [V8 implementation checklist](docs/V8_IMPLEMENTATION_CHECKLIST.md) — ticked after each committed development unit; implementation and test verification tracked separately.
- [V8 full rebuild specification](docs/architecture/FM-ATLAS-V8-FULL-REBUILD.md)
- [V8 implementation status and limitations](docs/architecture/FM-ATLAS-V8-IMPLEMENTATION-STATUS.md)
- [Application setup and test instructions](application/R7.2.0/FM-Atlas-R7.2.0-Work/README.md)
- [Draft integration pull request #4](https://github.com/lewisxatk/FM-Atlas/pull/4)

## Safety and testing

- Use an extracted V8 folder separate from any existing FM Atlas installation. Existing local database and settings are preserved under `%LOCALAPPDATA%\\FmAtlas`; never delete that folder during testing.
- **Automatic live-career use:** start a supported FM27 Windows Steam/Game Pass build, load a career and leave FM Atlas open. The monitor detects the exact game build and save-load generation, then reads the profile-defined player, staff and club arrays without a manual save-file import. Use the top-toolbar Connect/Refresh action for an immediate re-check. A 30-second retry follows transient read failures.
- The live connector is read-only. Player/staff CA/PA, mapped attributes/positions and staff identity links remain provisional until checked against the FM UI. Live Apply and live editing remain disabled.
- Use the offline save bridge on a **copy** of the career when you need historical seasons, match statistics or additional data not exposed by the current live reader.
- A hosted Windows build has passed for earlier source revisions only. The latest changes must pass their own CI run, followed by clean PC startup, Monolith Glass visual review, career detection, offline save import, staff/player profile integration, save-switch detection and Football Manager stability acceptance before V8 can be called release-ready.

## Historical source and research

The original R7.2.0 checkpoint, recovery inventory, reverse-engineering notes and older roadmap remain available in the repository history and documentation. Game fields, hidden attributes, role ratings and any inferred metrics must retain explicit evidence and confidence; do not invent mappings or formulas.
