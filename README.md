# FM Atlas — Watched Instrument (V8 continuation)

FM Atlas is a Windows desktop application for Football Manager scouting, career intelligence, recruitment, squad analysis, editing research and graphics tools.

> **Watched Instrument UI rebuild (15 November 2026):** Work is isolated on `rebuild/watched-earth-fmrte-ui`, based on the integrated V8 source. Draft PR [#8 — Watched Instrument UI and full-width player profiles](https://github.com/lewisxatk/FM-Atlas/pull/8) defines the new direction and starts the migration. The default visual direction is moving from Monolith Glass to a flat, high-contrast ink interface inspired by Watched.earth, with FMRTE-style record tabs and search workflows. Selected players now open in a full-width profile workspace. This is a work-in-progress branch: a fresh Windows Release build, automated tests and FM27 runtime acceptance are still required.

> **V8 status (14 November 2026):** The integrated source remains on the isolated branch `rebuild/v8-full-product-rebuild`; draft PR [#4 — FM Atlas V8](https://github.com/lewisxatk/FM-Atlas/pull/4) is open against `fix/r7.2.0-release-build-integration`. This update adds read-only career/process/build/session detection, staff/human-manager extraction and live search/profile database integration, plus Monolith Glass. The read-only live entity-array scan runs only for exact supported profiles, with a two-minute deadline, toolbar cancellation and save-generation cancellation. FM performance and field-witness verification remain outstanding. Earlier Windows build/package runs #157 and #168 passed on older commits; they do **not** validate these newest changes. The current live-career regression tests and a fresh Windows Release build/test/publish run must pass before distributing this revision. Real Windows/Football Manager acceptance remains outstanding.

## V8 baseline design (historical)

The V8 base branch's visual identity was **Monolith Glass**: graphite/charcoal surfaces, restrained amber-orange accents, translucent raised navigation and toolbar materials, fine glass borders and minimal motion. Three alternate appearances remain available: **Glass Dark**, **Glass Light** and **Studio**. Monolith Glass is the first-run default; an existing saved theme choice is preserved.

The shell includes a glass-style top action toolbar, descriptive hover/focus tooltips, grouped collapsible side navigation, active-workspace highlighting, Ctrl+K Global Search and system-aware workspace motion. Data grids and detailed profile content retain solid, readable surfaces rather than blurring every cell.

## Current source milestone

The V8 branch preserves the R7.2.0 application and feature foundation while adding:
- Monolith Glass shell, graphite/orange translucent navigation and toolbar, hover/focus help, four appearance modes and collapsible navigation.
- Shared glass styling for workspace inputs, selectors, tabs, progress controls and DataGrids; the Dashboard hero and metric cards adapt to narrower window widths. The shared Card helper preserves its original direct-child contract.
- Toolbar Activity Centre for active live-career scans and offline save imports, with current status, elapsed time, cancellation and a last-task summary.
- Responsive navigation and improved player workspace activation.
- FMRTE/FMSuperScout-style people discovery: fast name/club/nation/UID search, paged Players and Staff directories, direct min/max CA and PA filters (including signed negative PA ranges), and clickable player profiles. The same CA/PA filters apply to live and offline player sources, including filter-only searches and matching result counts.
- Universal Advanced Search builder across core player fields, stored attributes/personality and latest-save facts, with ALL/ANY logic, saved profiles, visible-column sets, paging and cancellation; performance-stat searches remain a separate player-period workspace.
- A Windows .NET 8 Release and self-contained x64 build/package workflow.
- Career/session detection: Atlas identifies supported Windows Steam/Game Pass processes by exact module fingerprint and observes the game's save-load generation counter. The 3-second monitor detects career/load changes and starts a profile-bounded read-only player/staff/club scan for a loaded supported session. Each scan has a two-minute deadline, toolbar cancellation and session-change cancellation. The database commits only a completed snapshot. Editor Studio saves to Atlas locally; live Apply/Undo is hard-blocked, including edit-history live Undo.
- SQLite writer coordination and a 30-second busy timeout.
- Save import now streams individual tables into a temporary NDJSON bundle and imports each row incrementally in one SQLite transaction; progress, elapsed time and cancellation preserve the last committed snapshot. Manifest row counts are checked for every table before commit, final warnings are persisted, and the adapter refuses to overwrite an existing output directory.
- A Python regression test covers normalisation, table rows, manager-name privacy and preservation of an existing output folder. The Windows .NET integration suite covers save-import rollback/preservation and now includes live player CA/PA (including negative PA), attributes/personality/positions, nationality, Advanced Search, Staff identity deduplication and process/build/save-generation checks. A fresh workflow result for the current branch head is still required.
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
- **Automatic live-career use:** start a supported FM27 Windows Steam/Game Pass build, load a career and leave FM Atlas open. The monitor detects the exact game build and save-load generation, then reads the profile-defined player, staff and club arrays without a manual save-file import. Use the top-toolbar Connect/Refresh action for an immediate re-check. A failed automatic scan enters a five-minute retry cooldown; a user-cancelled scan waits for a manual retry.
- The live connector is read-only. Player CA/PA, 52 mapped standard attributes, eight personality ratings, 15 positions, nationality and staff/manager identity links remain provisional until checked against the FM UI. Live Apply and live editing remain disabled.
- Use the offline save bridge on a **copy** of the career when you need historical seasons, match statistics or additional data not exposed by the current live reader.
- A hosted Windows build has passed for earlier source revisions only. The latest changes must pass their own CI run, followed by clean PC startup, Monolith Glass visual review, career detection, offline save import, staff/player profile integration, save-switch detection and Football Manager stability acceptance before V8 can be called release-ready.

## Historical source and research

The original R7.2.0 checkpoint, recovery inventory, reverse-engineering notes and older roadmap remain available in the repository history and documentation. Game fields, hidden attributes, role ratings and any inferred metrics must retain explicit evidence and confidence; do not invent mappings or formulas.
