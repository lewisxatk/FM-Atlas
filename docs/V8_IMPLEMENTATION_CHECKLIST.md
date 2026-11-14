# FM Atlas V8 — implementation and verification checklist

This file is the durable source of truth for the integrated V8 rebuild. Update it in the same commit as each completed development unit. Never rely on chat streaming as the work log.

## Status rules

- [x] **Implemented / committed** — code or documentation is present on this branch.
- [ ] **Build / test verified** — the relevant build, automated test or manual acceptance evidence has passed.
- [~] **In progress** — partially implemented; do not treat as complete.
- [!] **Blocked / needs evidence** — cannot safely proceed without additional reverse-engineering or test evidence.

Implementation and verification are tracked separately. A feature can be implemented while its build/test box remains unticked.

## Current branch

- Repository: `lewisxatk/FM-Atlas`
- Working branch: `rebuild/v8-full-product-rebuild`
- Product baseline: imported R7.2.0 WPF/.NET 8 source.
- Target: Windows 10/11 desktop, FM27 first; FM26 only where evidence supports it.
- Design: Atlas Liquid — Glass Dark, Glass Light, Studio.
- User testing: deliberately deferred until a coherent integrated milestone exists.

## 0. Preservation, recovery and delivery controls

- [x] V8 branch created separately from the R7.2.0 release integration line.
- [x] Full rebuild specification committed.
- [x] Atlas Liquid visual specification, top toolbar, hover tooltips and side navigation made mandatory.
- [x] This durable checklist created.
- [ ] Record a per-commit V8 work log entry whenever implementation changes are committed.
- [ ] Add a reproducible Windows build and publish script.
- [ ] Produce a single integrated Release package with versioned installer/portable ZIP and checksums.
- [ ] Verify clean install, upgrade path, uninstall and preservation of the user's existing Atlas database/settings.
- [ ] Create a rollback package / previous-version recovery instructions.

## 1. Application shell and Atlas Liquid design system

- [x] Initial Atlas Liquid application resource palette and shared tooltip styling committed.
- [x] Initial glass top toolbar, side navigation, collapsible navigation and contextual hover descriptions committed.
- [x] Initial Glass Dark / Glass Light / Studio theme selector and persisted preference committed.
- [x] Ctrl+K search shortcut wired to existing global search.
- [x] Live-memory scan action disabled in the new shell.
- [ ] Refine all reusable component states: default, hover, pressed, focused, disabled, selected and busy.
- [ ] Add clear accessible labels and keyboard focus traversal to all icon-only toolbar controls.
- [ ] Verify hover/focus tooltips are accurate, readable and stable on every toolbar/navigation action.
- [ ] Complete theme token mapping for all legacy workspaces; remove hard-coded old palette remnants.
- [ ] Apply consistent Atlas Liquid surfaces to dialogs, tabs, filters, grids, charts and empty/loading/error states.
- [ ] Add Reduce Motion setting and honour Windows accessibility/high-contrast preferences.
- [ ] Add lightweight workspace transitions and chart transitions without blocking input.
- [ ] Build and manually review shell at 100%, 125%, 150% Windows display scaling and common laptop/desktop resolutions.
- [ ] Validate Glass Light, Glass Dark and Studio visually and functionally.

## 2. Navigation, responsiveness and app architecture

- [x] Navigation no longer disables all sidebar controls while a workspace is loading; latest destination can be queued.
- [ ] Replace monolithic MainWindow code-behind with testable view models/services (incrementally; retain proven legacy workspaces until replacement).
- [ ] Add cancellable workspace-load/query operations and stale-request cancellation.
- [ ] Ensure navigation never waits for sequential restoration of every previously opened player profile.
- [ ] Add a background job centre with stages, elapsed time, cancellation, completion and failure details.
- [ ] Persist selected workspace, open tabs, filters, grid columns and layout state.
- [ ] Verify UI remains responsive during large queries/imports and failures leave the last stable workspace usable.

## 3. Football Manager stability and live-memory safety

- [x] Full live-memory scan is disabled behind a V8 code safety gate.
- [x] Existing live CA/PA and attribute Apply buttons are blocked behind a separate V8 safety gate.
- [ ] Confirm no startup timer, background service or navigation path invokes the full live scan.
- [ ] Profile memory/CPU usage with FM closed, FM open at menus and FM during a match.
- [ ] Design a bounded, cancellable live discovery process with conservative read budgets, pause/resume and progress.
- [ ] Re-enable live discovery only after stability tests demonstrate no material FM slowdown/crashing.
- [ ] Verify every decoded field against in-game UI and an independent source before promoting mapping confidence.
- [ ] Keep live editing disabled until mapping, identity, read-back, rollback and build fingerprint checks are independently validated.
- [ ] Add a clearly explained capability/status matrix for read-only, provisional and write-supported fields.

## 4. Save import, data provenance and database reliability

- [ ] Confirm fmsave bridge setup and exact FM27 table support/version; retain unsupported-table warnings.
- [ ] Replace single giant JSON snapshot materialisation with bounded streaming/table batches.
- [ ] Add import stages, progress reporting, cancellation and elapsed-time reporting.
- [ ] Import into staging snapshot; validate counts/relationships; publish atomically only after success.
- [ ] Preserve last known-good snapshot on cancellation, parse errors or database lock.
- [ ] Coordinate SQLite writes with a single-writer queue, busy timeout, prepared commands and tested WAL usage.
- [ ] Add retryable lock handling without hiding database failures.
- [ ] Keep live identity, career-save records, validated facts and local edits clearly separated in storage and UI.
- [ ] Show source, snapshot ID, evidence/validation status and coverage on relevant fields and metrics.
- [ ] Test import with large saves, partial tables, malformed rows, duplicate UIDs and interrupted imports.

## 5. Player discovery and advanced search

- [ ] Complete indexed, paged player search with cancellation and stable sorting.
- [ ] Build dedicated advanced-search workspace, not a fixed modal dialog.
- [ ] Add filter library: General, Contract, Technical, Mental, Physical, Hidden, Position, Performance and Source/Coverage.
- [ ] Add arbitrary available-field filters, min/max, include/exclude and AND/OR condition groups.
- [ ] Add saved searches/profiles, saved column sets, presets and reset-to-default.
- [ ] Add configurable columns, multi-sort, paging, multi-select, comparison and shortlist actions.
- [ ] Support all 15 FM positions and validated five-level suitability display.
- [ ] Add league, nationality, home-grown, contract and staff filters when available in source data.
- [ ] Ensure missing/undecoded values are never silently treated as zero or as a failed filter match.
- [ ] Add test fixtures for filter semantics, combined logic, paging, sorting and 40k+ player datasets.

## 6. Player profiles and development

- [ ] Complete player profile tabs: Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source/Evidence.
- [ ] Add validated position/role fit, attribute radar and comparison visualisations.
- [ ] Add CA/PA and attribute history snapshots with source and confidence.
- [ ] Add longitudinal development trends only where dated comparable snapshots exist.
- [ ] Add player comparison and similar-player discovery with explainable ranking.
- [ ] Ensure no inferred hidden attributes or unsupported development predictions are presented as fact.

## 7. Recruitment, Moneyball and analytics

- [ ] Complete recruitment workspace and reusable role templates.
- [ ] Add performance-vs-value scatter plot using comparable real season data.
- [ ] Add per-90, percentiles and position/role-specific metrics only when source supports them.
- [ ] Show sample size, minutes, competition strength, freshness and data coverage.
- [ ] Add similar players, recruitment pipeline and explainable recommendation factors.
- [ ] Build interactive analytics charts with filtering, hover values, zoom/range where useful and export.
- [ ] Validate metrics against source rows and independent calculations.

## 8. Squad, clubs, staff and tactics

- [ ] Complete club directory and squad depth view.
- [ ] Add formation builder, positional coverage, role-fit and age/payroll structure.
- [ ] Add tactical/role analysis with in/out-of-possession distinctions where supported.
- [ ] Add staff search, coaching-role fit and backroom gaps where supported.
- [ ] Add transfer/candidate views and shortlists with provenance-aware actions.
- [ ] Add drag/drop formation interactions with keyboard alternatives and reduced-motion support.

## 9. Editing and history

- [ ] Separate local Atlas draft edits from live FM edits throughout the product.
- [ ] Complete local player/staff/club editing and mass edit preview.
- [ ] Add edit history, changed-field indicators, undo/rollback and audit export.
- [ ] Add presets and validation rules per editable field.
- [ ] Keep every unsupported live write blocked; never weaken existing session/build/identity checks.
- [ ] Verify local edits never change the original career save file.

## 10. Data Lab, diagnostics and graphics

- [ ] Complete save inspector and mapping/evidence browser.
- [ ] Add per-build/storefront field mapping registry with type, decoder, range, witness, confidence and limitation.
- [ ] Add diagnostics for imports, database health, performance, environment and compatibility.
- [ ] Add useful one-click diagnostic bundle and log export.
- [ ] Retain/validate graphics indexing, face/logo lookup, graphics restore and mod tooling.
- [ ] Ensure diagnostics never log private save contents or unnecessary sensitive data.

## 11. Quality gates, performance and testing

- [ ] Restore and compile full solution using .NET 8 on Windows.
- [ ] Resolve all compile errors and actionable warnings; record exact build command and output.
- [ ] Add unit tests for data mapping, identity joins, filter logic, calculations and validation.
- [ ] Add integration tests for import staging, rollback, SQLite contention, cancellation and retry.
- [ ] Add performance benchmarks at 40k+ players, 28k+ clubs and large stats tables.
- [ ] Record startup, navigation, query, profile-open and import timings against R7.2.0 baseline.
- [ ] Verify no FM slowdown/crash in controlled tests before enabling any live scanning.
- [ ] Test backup/restore, offline use, permissions, missing dependencies and corrupted cache.
- [ ] Run full Windows acceptance checklist against a disposable career-save copy.
- [ ] Only declare V8 release-ready after build, automated tests and PC acceptance gates pass.

## 12. Release handover

- [ ] Update README and user setup/test tutorial to match the delivered build.
- [ ] Publish complete integrated build artifact with SHA-256 checksum.
- [ ] Provide one simple download/setup/launch/test procedure.
- [ ] Include known limitations and safe rollback steps.
- [ ] Provide final checklist report with links to commits, CI, build log and acceptance evidence.

## Work log

| Date (UTC) | Unit | Commit | Outcome / evidence |
|---|---|---|---|
| 2026-11-14 | Initial V8 durable checklist | To be recorded after commit | Created checklist; implementation and runtime validation intentionally separate. |
