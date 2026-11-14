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
- [x] Add a reproducible Windows build and publish workflow for V8 branch (commit `528dfb02`).
- [ ] Produce a single integrated Release package with versioned installer/portable ZIP and checksums.
- [ ] Verify clean install, upgrade path, uninstall and preservation of the user's existing Atlas database/settings.
- [ ] Create a rollback package / previous-version recovery instructions.

## 1. Application shell and Atlas Liquid design system

- [x] Initial Atlas Liquid application resource palette and shared tooltip styling committed.
- [x] Initial glass top toolbar, side navigation, collapsible navigation and contextual hover descriptions committed.
- [x] Initial Glass Dark / Glass Light / Studio theme selector and persisted preference committed.
- [x] Ctrl+K search shortcut wired to existing global search.
- [x] Live-memory scan action disabled in the new shell.
- [~] Reusable shared button states are implemented for default, hover, pressed, keyboard focus and disabled; selected navigation is styled. Busy-state templates remain outstanding.
- [x] Add automation names/help text and keyboard focus support to icon-only toolbar controls.
- [ ] Verify hover/focus tooltips are accurate, readable and stable on every toolbar/navigation action.
- [~] Initial legacy palette mapping to Atlas Liquid blue/neutral tones is in place; complete token migration and remove hard-coded remnants.
- [ ] Apply consistent Atlas Liquid surfaces to dialogs, tabs, filters, grids, charts and empty/loading/error states.
- [~] Workspace fade-in honours the Windows client-area animation preference; an in-app Reduce Motion setting and full high-contrast pass remain outstanding.
- [~] A 150 ms system-aware workspace fade-in is implemented; chart transitions and broader interaction motion remain outstanding.
- [ ] Build and manually review shell at 100%, 125%, 150% Windows display scaling and common laptop/desktop resolutions.
- [ ] Validate Glass Light, Glass Dark and Studio visually and functionally.

## 2. Navigation, responsiveness and app architecture

- [x] Navigation no longer disables all sidebar controls while a workspace is loading; latest destination can be queued.
- [ ] Replace monolithic MainWindow code-behind with testable view models/services (incrementally; retain proven legacy workspaces until replacement).
- [~] Add cancellable search operations: universal Advanced Search now has a Cancel button, propagates a CancellationToken to the SQLite query, retains the last successful result page when cancelled, and cancels on reset/profile load. Other workspace loads and stale query cancellation remain open.
- [x] Previously opened player profiles are restored after the Players workspace is displayed, not awaited in the navigation construction path.
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
- [x] Remove duplicate full-array/List copies from the C# importer for players, staff and clubs by using lazy JsonElement array enumeration and O(1) array counts (commit `26104ba3`).
- [ ] Replace the remaining full Python table materialisation and single giant JSON snapshot with bounded streaming/table batches.
- [x] Add table-by-table save-reader progress, database indexing progress, toolbar Cancel action and elapsed-time reporting; propagate cancellation through the bridge and SQLite importer.
- [ ] Import into staging snapshot; validate counts/relationships; publish atomically only after success.
- [x] Preserve the last committed snapshot on cancellation, parse errors or database lock: the reader process is stopped and an uncommitted SQLite transaction is rolled back before commit.
- [x] Add a process-wide SQLite writer semaphore around mutating/import/undo/checkpoint operations and a 30-second busy timeout (commit `e63668db`).
- [ ] Stress-test the writer gate under concurrent imports and page reads; verify WAL checkpoint/backup behaviour.
- [ ] Further optimise imports with prepared commands and bounded batch inserts; retain existing indexes and transaction safety.
- [ ] Add retryable lock handling without hiding database failures.
- [ ] Keep live identity, career-save records, validated facts and local edits clearly separated in storage and UI.
- [ ] Show source, snapshot ID, evidence/validation status and coverage on relevant fields and metrics.
- [ ] Test import with large saves, partial tables, malformed rows, duplicate UIDs and interrupted imports.

## 5. Player discovery and advanced search

- [~] Existing player results are indexed/paged in source; cancellation and full stable sorting still require validation and implementation.
- [x] Add a unified Advanced Search workspace combining core player fields, dynamically discovered PlayerAttributes and latest-save facts in one field/category builder.
- [x] Add a Cancel search action with cancellation propagated through SQLite and last successful result-page retention.
- [~] Add a dynamic category and field library for core fields, contract facts, attributes/personality, positions, traits, state, reputation and raw attributes. Performance metrics remain in the dedicated per-period search workspace; source coverage display and additional categories are still open.
- [x] Universal player search supports up to 60 mixed core/attribute/save-fact conditions with ALL/AND or ANY/OR matching. Specialist performance search remains separate because its criteria are scoped to player-period rows.
- [x] Add local saved universal search profiles with create/replace/load/delete, persisted criteria, search text, AND/OR mode and selected result columns. Reset-to-default is present for the active filter builder; broader filter presets remain open.
- [~] Add configurable visible columns and stable 250-row server-side paging with previous/next navigation. Multi-sort, multi-select, comparison and shortlist actions remain open.
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

- [x] Restore and compile full solution using .NET 8 on a hosted Windows runner (GitHub Actions run #79, commit `18d32e2d`).
- [~] All compile errors found in the first run were fixed and the Release build now passes; review remaining non-fatal compiler warnings before declaring warning-clean.
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
- [x] Publish a self-contained Windows x64 V8 checkpoint ZIP with SHA-256 checksum (run #79; `4a16f815150e41855b2b1d5dba884c6878ac5a69f44b8ae44ebea8570b2798e2`).
- [ ] Provide one simple download/setup/launch/test procedure.
- [ ] Include known limitations and safe rollback steps.
- [ ] Provide final checklist report with links to commits, CI, build log and acceptance evidence.

## Work log

| Date (UTC) | Unit | Commit | Outcome / evidence |
|---|---|---|---|
| 2026-11-14 | Atlas Liquid top toolbar, collapsible navigation, theme cycling, tooltips and responsive navigation | `2ccbbcc5`, `1447e6f7`, `5c1090b0` | Initial shell and deferred profile restoration committed; keyboard/tooltips/DPI acceptance not yet verified. |
| 2026-11-14 | Blue accent mapping and system-aware workspace transition | `846b98af`, `2fc2e2e0` | Removed legacy lime accent mapping in favour of the Atlas Liquid blue palette; transition respects Windows client-area animation preference. |
| 2026-11-14 | Dedicated Advanced Search hub | `6ba78cc0`, `1a3c10a4`, `96698d09`, `af414246` | New workspace links to existing player, attribute/personality, statistical and global search tools. It is not yet the universal AND/OR filter builder. |
| 2026-11-14 | Accessible icon controls | `f07c6e86` | Automation names and help text added to toolbar and navigation icon buttons; visual/accessibility acceptance still pending. |
| 2026-11-14 | Windows workflow repair | `8531b0c0` | Consolidated duplicate YAML triggers and configured a Windows x64 self-contained publish ZIP and SHA-256 output. Build result remains unverified. |
| 2026-11-14 | Draft V8 integration pull request | PR [#4](https://github.com/lewisxatk/FM-Atlas/pull/4) | Kept changes isolated from the integration branch pending build and PC acceptance. |
| 2026-11-14 | Temporary career export cleanup | `8b4d1a0f`, `d2fb8f5e`, `8f60dc48`, `d40513fc` | Partial, failed, invalid and completed temporary save JSON exports are removed; the Cancel action is hidden when no import is active. |
| 2026-11-14 | Initial V8 durable checklist | `b76f39d7` | Created this checklist; implementation and runtime validation intentionally separate. |
| 2026-11-14 | Windows x64 build/package automation | `528dfb02` | Workflow now runs on V8 branch pushes, builds Release, publishes self-contained win-x64 output and uploads a ZIP artifact. CI result not yet observed. |
| 2026-11-14 | SQLite writer coordination and busy timeout | `cb44d29`, `e63668db` | Added one process-wide writer semaphore around all identified mutations/imports/undo/checkpoint methods and a 30-second lock wait; the follow-up commit corrected the gate blocks. Concurrency behaviour still requires tests. |
| 2026-11-14 | Save-reader progress stream and table stages | `517a25de`, `755c69d3` | The Python adapter reports each table start, row count and warnings to stderr; the desktop bridge consumes it line-by-line for UI progress. |
| 2026-11-14 | Cancellable career snapshot indexing | `1ed64ed2` | Cancellation is checked during player, staff, club and additional-table indexing; progress is reported and cancellation before commit rolls back the active transaction. |
| 2026-11-14 | Cancellable save-import UI and elapsed time | `005af6a0`, `f0701bb1` | Added a toolbar Cancel action, visible import status, elapsed time and a clear message that the previous committed snapshot is retained. |
| 2026-11-14 | Reduce duplicate JSON array allocations | `26104ba3` | Player, staff and club arrays are now lazily enumerated from JsonDocument rather than being copied into duplicate arrays and Lists. Full envelope memory is still materialised; true bounded streaming remains open. |
| 2026-11-14 | AND/OR logic for specialist search engines | `e9fae94d`, `590d205a` | Attribute/personality and performance-stat search can now match ALL (AND) or ANY (OR) conditions. A single cross-category query builder remained open at that point. |
| 2026-11-14 | Universal Advanced Search models and database query | `7b79c568`, `f6ea031f` | Added field/criterion models, live field catalog discovery and a parameterized query joining core player fields, PlayerAttributes and latest-save FmsavePlayerFacts with AND/OR semantics. |
| 2026-11-14 | Universal Advanced Search interface | `49f29f89`, `aa9e9843`, `a33de936`, `76bb3ce4` | Replaced the navigation-only search hub with a category/field condition builder, typed values, 60-condition limit, ALL/ANY logic, result grid and profile open on double-click. Release compilation passed on Windows Actions run #101 for the initial universal builder; follow-up profile/column/paging changes require a new compile. |
| 2026-11-14 | First Windows Release compile and fixes | `80b4160d`, `b35432d8` | First attempt exposed 7 errors (four event-handler signatures, three WPF/System.IO Path ambiguities). Fixed callback signatures and added an explicit System.IO.Path alias. |
| 2026-11-14 | Successful Windows x64 Release build and package | GitHub Actions run [#79](https://github.com/lewisxatk/FM-Atlas/actions/runs/43534383967), commit `18d32e2d` | .NET 8 setup, solution restore, Release compilation, self-contained win-x64 publish and artifact upload all passed. Product ZIP SHA-256: `4a16f815150e41855b2b1d5dba884c6878ac5a69f44b8ae44ebea8570b2798e2`. This is a compile/package checkpoint only; Windows runtime and FM acceptance remain open. |
