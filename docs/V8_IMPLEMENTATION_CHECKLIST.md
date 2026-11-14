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
- Design: Monolith Glass primary appearance — graphite/charcoal surfaces, warm orange accents and translucent glass controls; Glass Dark, Glass Light and Studio remain available.
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

## 1. Application shell and Monolith Glass design system

- [x] Initial Atlas Liquid application resource palette and shared tooltip styling committed.
- [x] Initial glass top toolbar, side navigation, collapsible navigation and contextual hover descriptions committed.
- [x] Monolith Glass is the first-run default, with graphite/charcoal surfaces, restrained orange accents and persisted theme preference; Glass Dark, Glass Light and Studio remain selectable.
- [x] Top toolbar Connect / Refresh action connects to the current supported FM27 career; automatic entity-array indexing runs after a valid save-load generation is detected.
- [x] Ctrl+K search shortcut wired to existing global search.
- [~] Reusable shared button states are implemented for default, hover, pressed, keyboard focus and disabled; selected navigation is styled. Busy-state templates remain outstanding.
- [x] Add automation names/help text and keyboard focus support to icon-only toolbar controls.
- [ ] Verify hover/focus tooltips are accurate, readable and stable on every toolbar/navigation action.
- [~] Monolith Glass token mapping and principal navigation/glass surfaces are implemented; remaining hard-coded colours, dialog coverage and high-contrast refinements remain open.
- [ ] Apply consistent Monolith Glass surfaces to dialogs, tabs, filters, grids, charts and empty/loading/error states.
- [~] Workspace fade-in honours the Windows client-area animation preference; an in-app Reduce Motion setting and full high-contrast pass remain outstanding.
- [~] A 150 ms system-aware workspace fade-in is implemented; chart transitions and broader interaction motion remain outstanding.
- [ ] Build and manually review shell at 100%, 125%, 150% Windows display scaling and common laptop/desktop resolutions.
- [ ] Validate Monolith Glass, Glass Light, Glass Dark and Studio visually and functionally at 100%, 125% and 150% DPI.

## 2. Navigation, responsiveness and app architecture

- [x] Navigation no longer disables all sidebar controls while a workspace is loading; latest destination can be queued.
- [ ] Replace monolithic MainWindow code-behind with testable view models/services (incrementally; retain proven legacy workspaces until replacement).
- [~] Add cancellable search operations: universal Advanced Search now has a Cancel button, propagates a CancellationToken to the SQLite query, retains the last successful result page when cancelled, and cancels on reset/profile load. Other workspace loads and stale query cancellation remain open.
- [x] Previously opened player profiles are restored after the Players workspace is displayed, not awaited in the navigation construction path.
- [ ] Add a background job centre with stages, elapsed time, cancellation, completion and failure details.
- [ ] Persist selected workspace, open tabs, filters, grid columns and layout state.
- [ ] Verify UI remains responsive during large queries/imports and failures leave the last stable workspace usable.

## 3. Football Manager stability and live-memory safety

**Safety gate (2026-11-14):** `LiveMemoryImportEnabled` is ON for exact supported FM27 profiles. The read-only entity-array scan has a two-minute hard deadline, toolbar cancellation and save-generation cancellation. Live editing remains OFF; real FM performance and field-witness acceptance are still required.

- [x] Add exact PE-fingerprint process detection for supported Windows FM27 Steam and Game Pass builds; unknown builds and duplicate supported processes fail closed.
- [x] Add read-only GAME_UI save-load generation detection. A career connection is keyed by process, build profile and the in-game load counter, not by a remembered save-file path.
- [x] Start automatic live-career detection on application load; the monitor probes every three seconds and starts the profile-bounded read-only index when a supported career generation is loaded.
- [x] Replace the legacy broad process-memory scan with profile-bounded player/staff/club array reads. The automatic monitor invokes the bounded extractor for a supported loaded career; it does not scan unknown builds or duplicate supported FM processes.
- [x] Check the save-load generation before and after extraction; discard the partial snapshot if FM changes career while Atlas is reading.
- [x] Keep bounded extraction cancellation, generation checks and retry handling in the automatic importer. Each scan has a two-minute deadline and toolbar Cancel; the monitor cancels it when the process/build/save-generation changes.
- [x] Implement transactional persistence for automatic live player/staff/club snapshots and uniquely identified manager club/nation context. The database receives the scan cancellation token and checks it before commit; cancellation rolls back the new snapshot and retains the previous one.
- [x] Surface live players and staff in the searchable Players/Staff directories. Player CA/PA, 52 mapped attributes, eight personality ratings, 15 position ratings and nationality/club links are read from the current identity block (including the documented player-coach +0xF8 shift) and remain explicitly provisional until an in-game UI witness is recorded.
- [x] Fix live player search to resolve ability.current_ability and ability.potential_ability, including profile-matched provisional values and negative PA ranges, for display and CA/PA filtering. Include live nationality, primary position and CA/PA/personality/attribute/position filters in the existing player/Advanced Search data path.
- [x] Display all available provisional live position ratings and the eight personality ratings on player profiles with explicit source-confidence warnings; show live nation and primary position in the player directory.
- [x] Bind My Manager / My Club shortcuts to the currently detected save-load generation; do not display a previous career's manager as current when FM closes or changes career.
- [x] Keep live CA/PA/attribute Apply and live editing disabled. The automatic connector is read-only.
- [ ] Run the newly added live-career database/search regression tests against the exact current branch head in the Windows workflow.
- [ ] Compare player/staff CA/PA, all mapped player attributes, eight personality ratings, position ratings, player-coach identity fields, manager identity, nation and club against the current FM27 UI on supported Steam and Game Pass builds.
- [ ] Profile memory/CPU and FM responsiveness with FM closed, at menus and during a match; verify save switch, save close, game restart and duplicate-process behaviour.
- [ ] Add a user-visible capability/status matrix for read-only, provisional and write-supported fields; maintain write gates until mapping, identity, read-back and rollback are independently validated.

## 4. Save import, data provenance and database reliability

- [ ] Confirm fmsave bridge setup and exact FM27 table support/version; retain unsupported-table warnings.
- [x] Remove duplicate full-array/List copies from the C# importer for players, staff and clubs by using lazy JsonElement array enumeration and O(1) array counts (commit `26104ba3`).
- [x] Replace the single giant JSON snapshot with a temporary per-table NDJSON bundle. The adapter streams Polars rows via `iter_rows(named=True)` and normalises player/staff/club rows one at a time; it does not retain the complete set of tables in memory. A compatibility fallback for non-Polars reader wrappers still uses `to_dicts()` for that one table.
- [x] Refuse to write into an existing adapter output folder; the adapter never recursively deletes a caller-supplied path. Added a regression test that verifies a pre-existing sentinel file survives.
- [x] Add table-by-table save-reader progress, database indexing progress, toolbar Cancel action and elapsed-time reporting; propagate cancellation through the bridge and SQLite importer.
- [x] The C# importer parses the small manifest and reads NDJSON one row at a time inside one SQLite transaction; new import/snapshot rows are published only on commit, and cancellation/failure rolls back. Player, staff, club and every additional table’s physical NDJSON row count is checked against the manifest before commit; skipped valid-identity rows are warned and reconciliation warnings are stored with the snapshot.
- [x] Preserve the last committed snapshot on cancellation, parse errors or database lock: the reader process is stopped and an uncommitted SQLite transaction is rolled back before commit.
- [x] Add a process-wide SQLite writer semaphore around mutating/import/undo/checkpoint operations and a 30-second busy timeout (commit `e63668db`).
- [ ] Stress-test the writer gate under concurrent imports and page reads; verify WAL checkpoint/backup behaviour.
- [~] Reuse prepared player, staff, club, raw player, attribute, player-fact, stat-fact and sidecar inserts during bundle indexing; a large-save throughput benchmark and full contention test remain open.
- [ ] Add retryable lock handling without hiding database failures.
- [ ] Keep live identity, career-save records, validated facts and local edits clearly separated in storage and UI.
- [ ] Show source, snapshot ID, evidence/validation status and coverage on relevant fields and metrics.
- [x] Python adapter regression suite passed on Windows Actions run #168: NDJSON bundle schema, normalized player/staff/club/stat rows, manager-name privacy and preservation of an existing output directory are covered. Large real-save import, duplicate UID and peak-memory checks remain open.

## 5. Player discovery and advanced search

- [x] Main Players directory retains fast name/club/nation/UID lookup, position filtering, paged/virtualized results and player-profile opening; live player CA/PA values are now surfaced by database search, including explicit provisional field status.
- [~] Existing player results are indexed/paged in source; cancellation and full stable sorting still require validation and implementation.
- [x] Add a unified Advanced Search workspace combining core player fields, dynamically discovered PlayerAttributes and latest-save facts in one field/category builder.
- [x] Add a Cancel search action with cancellation propagated through SQLite and last successful result-page retention.
- [~] Add a dynamic category and field library for core fields, contract facts, attributes/personality, positions, traits, state, reputation and raw attributes. Performance metrics remain in the dedicated per-period search workspace; source coverage display and additional categories are still open.
- [x] Universal player search supports up to 60 mixed core/attribute/save-fact conditions with ALL/AND or ANY/OR matching. Specialist performance search remains separate because its criteria are scoped to player-period rows.
- [x] Add local saved universal search profiles with create/replace/load/delete, persisted criteria, search text, AND/OR mode and selected result columns. Reset-to-default is present for the active filter builder; broader filter presets remain open.
- [~] Add configurable visible columns, stable 250-row paging and multi-select bulk Add-to-Shortlist. Bulk insertion is transactional and reports new versus already-present players accurately. Two-key server-side sorting now works before paging and is saved/restored with Advanced Search profiles; Reset filters restores name-ascending sort as well as default columns. Shortlist removal/notes actions remain open.
- [x] Verify the 15-position suitability service, canonical STC and shared Players search-position catalogue in Windows Actions run #210; Release build, all integration tests, self-contained x64 publish and artifact upload passed.
- [~] Advanced Search now uses the same 15-position catalogue in a dedicated editable position picker, retaining the legacy ST alias and arbitrary source-specific text entry; database regression coverage includes Sweeper and STC. Awaiting Windows verification in the next combined run.
- [~] Main Players workspace now shows league and Home-grown at club/nation as Yes/No/Unknown, retains the legacy ST alias beside canonical STC, and includes home-grown data when choosing the richest save/live row. Windows verification pending.
- [x] Add source-backed Home-grown at club / Home-grown in nation Boolean criteria to Advanced Search, with Yes/No/Unknown result values and regression coverage proving missing NULL data matches neither Yes nor No.
- [~] League, nationality and imported contract facts are available in the universal Advanced Search field library and covered by combined ALL/AND regression tests. Staff search remains a separate FMRTE-style Staff directory; live staff CA/PA, role, club, age, nation and human-manager status are shown with provisional source labels.
- [x] Keep missing/undecoded values distinct from zero: numeric/attribute/fact predicates require a stored value, and nullable home-grown values match neither Yes nor No; result display retains Unknown where applicable.
- [~] Add regression coverage for universal Advanced Search core fields, imported attributes, save facts, ALL/ANY logic, stable paging and the 60-condition guard. Broader mixed-field 40k+ dataset and full multi-sort validation remain outstanding.
- [x] Four baseline Windows .NET integration tests passed on Actions run #168: valid NDJSON commit, manifest-count mismatch rollback, malformed extra-table rollback, and preservation of a previously committed snapshot.
- [x] Cancellation-before-commit rollback and persisted skipped-staff-warning integration tests passed in Windows Actions run #208; subsequent run #210 also passed the expanded integration suite. WAL contention and broader relationship checks remain outstanding.

## 6. Player profiles and development

- [~] Add source-backed player profile tabs for Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source/Evidence. Live standard attributes and eight personality ratings flow through the existing attribute store; provisional live positions appear in the position map and source-fact list with a confidence warning. Windows build and visual acceptance pending.
- [~] Add a source-backed five-axis profile radar and a multi-player category-average overlay in Player Comparison. The comparison radar only includes categories with valid imported 1–20 values for every player; Hidden and Personality groups are excluded and missing categories are not scored as zero. Top-six per-player Role Fit estimates include built-in and locally saved custom templates with the same coverage-aware scoring and an explicit custom label. Windows build and visual acceptance pending.
- [~] Add dated CA/PA/value history from source player snapshots with source-file/import evidence; a chart appears only after two distinct in-game dates and does not project or interpolate missing values. Windows verification pending.
- [~] Add longitudinal CA/PA trends only where at least two dated comparable save snapshots exist. Attribute-by-attribute history still requires a stable dated attribute snapshot source.
- [~] Add player comparison and similar-player discovery with explainable ranking. Implemented for up to four players with source-backed shared-attribute scores, coverage evidence and a shared-category comparison radar; Windows verification pending.
- [ ] Ensure no inferred hidden attributes or unsupported development predictions are presented as fact.

## 7. Recruitment, Moneyball and analytics

- [x] Indexed statistic search pushes each metric's numeric threshold into SQLite's indexed candidate predicate, evaluates ALL/ANY with per-condition aggregation, applies per-90/minutes filters before materialisation, groups qualifying player-period rows server-side, and resolves only candidate player identities.
- [x] Add and pass a 40,000-player / 80,000-fact synthetic regression and latency smoke test for indexed ALL/ANY search, including seeding and query thresholds (Windows Actions run #220 passed the full integration suite). Real FM27 career verification, actual adapter import throughput and peak-memory benchmarking remain open.
- [x] Centralise the weighted Role Fit calculation in `RoleFitService.CalculateScore`; only finite source-provided 1–20 attributes contribute, and the score returns separate available/expected counts and coverage. It remains explicitly an Atlas estimate, not the game's hidden role formula or star rating.
- [x] Upgrade Role Fit Lab with minimum/maximum age, minimum CA, minimum PA, position-contains and minimum attribute-coverage filters. Candidate predicates are applied in SQLite before candidate limiting; role-coverage thresholds are applied before ranking/result limiting.
- [x] Add cancellation through Role Fit database reading and scoring; a cancelled search keeps the last completed result set.
- [x] Make Role Fit CSV exports traceable with player Source ID and attribute coverage counts, and guard untrusted text cells against spreadsheet-formula injection.
- [x] Add per-attribute weighted evidence to Role Fit scores, including the source attribute value, effective weight, weighted points and share of total; surface evidence in the ranked grid and CSV.
- [x] Merge equivalent normalized attribute keys before database search so aliases such as `Finishing` and `finish_ing` do not throw duplicate-key errors or inflate coverage.
- [x] Verify the new Role Fit calculation, evidence breakdown, duplicate-key handling, database filters, cancellation and export changes in the latest Windows build and automated tests (Windows Actions run #182 passed all workflow stages, including the .NET integration suite and self-contained win-x64 publish).
- [x] Correct Role Fit Lab compile-time closure issues in the cancellation control and age-filter logging scope discovered by Windows CI run #179; a fresh Windows build is required to confirm the repair.
- [ ] Benchmark metric search, import throughput and peak memory using 40k+ player / large-stat fixtures.

- [~] Add side-by-side comparison for up to four selected players and source-backed similar-player discovery based on shared non-hidden 1–20 attribute vectors. Search results and player-profile actions open both tools; Windows Actions run #220 passed the build and integration suite, with the latest provenance-rich export changes in run #221. Manual visual acceptance remains pending.
- [~] Add a five-axis player attribute radar using available Technical, Set Pieces, Mental, Physical and Goalkeeping source ratings. Missing categories are excluded rather than plotted as zero; automated tests/build passed in Windows Actions run #220. Manual visual acceptance remains pending.
- [~] Add locally persisted custom Role Fit templates with editable visible attribute weights, phase/position group, description, and saved age/CA/PA/position/coverage filters. The existing RoleFitProfileStore is the single persistence path; selecting a built-in profile clears any custom saved filters. Replace/load/delete and validation are covered by tests. Windows Actions run #220 passed; manual visual acceptance remains pending.
- [~] Add a performance-versus-value percentile scatter view using source metric rows, a minimum-minutes and minimum-sample floor, source value units, sample count and period freshness. It calculates tie-aware within-position and within-league performance percentiles when a cohort has at least three players. League strength is not available in the imported metric index and is not adjusted. Windows Actions run #220 passed the pre-existing scatter workflow; the new sample-floor extension is awaiting run #222.
- [~] Calculate overall and position/league percentiles across the full eligible metric/value cohort before applying the visible result limit; regression coverage proves alphabetically later high-value-gap players are not excluded. The new sample-count floor is applied before cohort percentiles; Windows verification is pending run #222.
- [ ] Add per-90, percentiles and position/role-specific metrics only when source supports them.
- [~] Show sample size, minutes, latest period and data coverage in the Performance vs Value grid, hover evidence and CSV. Added a minimum sample-count floor applied in SQLite before percentile cohorts are calculated. Competition strength is not exposed by the imported metric index and is not inferred; independent metric validation and freshness against a real career remain open.
- [~] Similar-player ranking is implemented with explicit similarity score, shared-attribute count and anchor coverage; selected candidates can be added in bulk to any named shortlist, with duplicate/source-ID handling verified by regression tests. The shortlist workflow tracks stage, priority, source-unit target fee/wage, next action, review date and notes, with stage/priority filtering and due-review summary. Provenance-rich similar-player CSV export now includes source IDs, home-grown status and evidence; Windows verification for the latest export is running in Actions run #221.
- [~] Similar Player Discovery adds age, league, nation, source-unit value and home-grown status to its candidate table and exports a provenance-rich CSV with reference/candidate source IDs, similarity score, shared-attribute count, coverage and evidence; Player Comparison includes home-grown club/nation. Windows verification pending.
- [~] Performance vs Value scatter now filters by metric, per-90 eligibility, minimum minutes, position and league; dots expose hover evidence and filtered rows export to CSV. Zoom/range controls and broader analytics-chart coverage remain open.
- [ ] Validate metrics against source rows and independent calculations.

## 8. Squad, clubs, staff and tactics

- [~] Club profile includes linked squad size and source coverage, age bands, primary-position counts, average age/CA/PA, total player value, wage records/mean/total, home-grown Yes/No/Unknown, and staff only on exact save club-name matches. Currency/pay period and missing fields are not inferred. Windows verification pending.
- [ ] Add formation builder, positional coverage, role-fit and age/payroll structure.
- [ ] Add tactical/role analysis with in/out-of-possession distinctions where supported.
- [~] Staff directory search and selectable profiles are implemented for imported save/live staff; live CA/PA, nation, club, age, role and human-manager indicators are present. Full coaching-attribute coverage, coaching-role fit and backroom gap analysis remain open.
- [~] Add a local shortlist recruitment pipeline with Identified, Monitoring, Scouting, Contact, Offer, Signed and Rejected stages; candidate notes are editable and retained locally. Existing databases add the Stage column in-place with an Identified default. Windows verification pending.
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

- [~] Centralise safe CSV text quoting and spreadsheet-formula neutralisation across Value Finder, Role Fit and Performance vs Value exports; regression tests cover formula prefixes, leading whitespace, quote escaping and numeric-cell opt-out. Windows verification pending.
- [~] Make Advanced Search profile storage testable against an isolated folder and validate saved sort keys (maximum two, whitelisted fields, no duplicates); round-trip and invalid-sort tests are committed. Windows verification pending.

- [x] Restore and compile full solution using .NET 8 on a hosted Windows runner (GitHub Actions run #79, commit `18d32e2d`).
- [~] All compiler errors have been fixed and the Release build passed on run #168. Removed twelve unused legacy fields, replaced compile-time live-memory `const false` gates with non-constant fail-closed properties, and wrapped deferred profile restoration in an explicit error handler (`c1f5c153`). Run #172 will check whether the known CS0162/CS0169/CS4014 warnings have reduced; any remaining warnings need a fresh review.
- [~] Add unit tests for data mapping, identity joins, filter logic, calculations and validation. Added live-career regression tests for provisional player CA/PA/negative PA, standard attributes, eight personality ratings, position/nation filters, Advanced Search, staff search/profile facts, live/offline staff deduplication, manager nation/club, save-load counter changes and full process/build/session identity matching; latest Windows Actions result is pending.
- [ ] Add integration tests for import staging, rollback, SQLite contention, cancellation and retry.
- [ ] Add performance benchmarks at 40k+ players, 28k+ clubs and large stats tables.
- [ ] Record startup, navigation, query, profile-open and import timings against R7.2.0 baseline.
- [ ] Verify no FM slowdown/crash during the enabled bounded scan on the user's real PC; use the toolbar Cancel action and confirm the two-minute ceiling before treating it as release-ready.
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

| 2026-11-14 | Club profile squad, contract, home-grown and staff intelligence | Pending CI | Added source-backed club squad aggregates, wage coverage/total/mean in source units, home-grown Yes/No/Unknown, exact club-name save staff records, and regression tests for missing fields and units. Live staff links remain undisplayed until validated. Windows verification pending. |

| 2026-11-14 | Advanced Search canonical position picker | Pending CI | Replaced free-text-only input for the core Position field with a searchable editable picker populated from the shared 15-position catalogue plus the legacy ST alias. Saved profile load/save continues to use the same TextValue criterion contract. Added database regression coverage for SW and STC filters. Windows verification pending. |
| 2026-11-14 | Performance vs Value minimum sample floor and 40k metric-search verification | Pending CI | The existing 40,000-player / 80,000-fact indexed ALL/ANY fixture passed the full Windows integration suite in run #220. Added a minimum sample-count input to Performance vs Value, enforced in the SQLite aggregate before percentiles are calculated, plus a regression proving one-row candidates are excluded while two-row candidates remain. Real-save validation, import throughput and peak-memory profiling remain open. |
| 2026-11-14 | CSV export helper repair and custom Role Fit profiles in player profiles | Pending CI | Rebuilt CsvExportSafety in a valid compile-safe structure, preserving formula-safe cell quoting and source-backed player-row export. Player profile top-six Role Fit now includes saved custom templates alongside built-ins, with regression coverage for persisted weights, score, coverage and evidence. Windows verification pending. |

| 2026-11-14 | Advanced Search full-result CSV export and filter coverage | Pending CI | Added cancellable, streaming CSV export of up to 50,000 Advanced Search matches in batches of 500, written to a temporary file and published only on completion. Exports source IDs and source-backed profile data with shared formula-safe CSV quoting. Regression now combines league, nationality and contract-fact filters. Run #214 exposed duplicate sort declarations and #215 exposed the missing Microsoft.Win32 import for SaveFileDialog; both compile issues have been addressed. Extracted Advanced Search row/header formatting into a testable shared CSV helper and added coverage for commas, formula injection, unknown values and source IDs. Fresh Windows verification pending. |

| Date (UTC) | Unit | Commit | Outcome / evidence |
|---|---|---|---|
| 2026-11-14 | Automatic live FM27 career connection | `576add49`, `db2dc265`, `b0af2a0f` | Added an exact-profile, read-only process monitor using the game's GAME_UI save-load counter; bounded player/staff/club array extraction; human-manager/staff identity, CA/PA, nation/club/role capture; and transactional persistence. The manager's source remains provisional until a real-career UI comparison. |
| 2026-11-14 | Live player search/profile and career identity integration | `9fe85f22`, `dfb52634`, `9e964fc0` | Corrected field-key/status handling so provisional live CA/PA and negative PA ranges appear in player search and CA/PA/attribute filters. Added the current save-load counter to workspace metrics and displayed provisional live position ratings in player profiles with a confidence warning. |
| 2026-11-14 | Monolith Glass and automatic reconnect safeguards | `9d302ca8`, `8439bc3a` | Added Monolith Glass as the first-run theme while preserving Glass Dark/Light/Studio; clears old career status when FM closes; binds My Manager/My Club shortcuts to the active session generation. Transient live-scan failures now retry after a five-minute cooldown. |
| 2026-11-14 | Live-career import/search regression fixtures | `ecfbc20e` | Added isolated SQLite tests for provisional live player CA/PA (including negative PA), CA/PA/attribute filters, live staff search/profile facts, manager nation/club and changing save-load generations. Windows CI and runtime results are pending. |
| 2026-11-14 | Player profile field coverage and player-coach layout | `661a978c` | Added class-scoped player identity-block detection using decoded name witnesses; reads the correct player-coach-shifted UID, DOB, nation and eight personality ratings while keeping position/standard attribute offsets separate. |
| 2026-11-14 | Live player search and nationality/position integration | `2e266675`, `7d28a048`, `20bda5af` | Persisted player nation and primary natural position, added position-rating filtering to the live search path, and passed the existing directory position selection through to live queries. |
| 2026-11-14 | People search confidence and expanded regression coverage | `5ab8b84b`, `577f6512`, `8989c74b` | Made live relationship and CA/PA/attribute/personality/position confidence visible, and added test assertions for player nation, personality, position filters and the retained Advanced Search path. Windows Actions and FM UI witness checks remain pending. |
| 2026-11-14 | Live/offline staff deduplication and session identity regression | `51f03e16`, `b997bcab`, `0d64848b` | Staff directory merges a person present in both live and offline records by name/club/role and prefers current live facts. Tests verify process ID, exact build profile and save-load counter are all part of the live session identity; reusing a reset counter after a game restart must not reuse the previous manager. |
| 2026-11-14 | Application handover and source confidence completion | `5fe84293`, `775d33d5`, `40da6014` | Corrected the app and root README to describe the automatically running bounded live entity-array scan rather than manual offline import, and extended the checklist for player-coach identity, personality, nationality and live position search. |
| 2026-11-14 | Advanced Search CSV dialog namespace repair | `9185f34d` | Run #215 compiled the duplicate-sort repair but exposed CS0246 for SaveFileDialog; imported Microsoft.Win32 in the Advanced Search code-behind. Fresh Windows verification pending. |
| 2026-11-14 | Similar-player hidden/personality exclusion regression | Pending CI | Extended the discovery integration fixture with Hidden and Personality rows for anchor and candidates; assertions prove similarity uses only source-backed footballing attributes and does not inflate the reference vector. Windows verification pending. |
| 2026-11-14 | Advanced Search CSV row contract and safety tests | Pending CI | Moved the stable 16-column header and per-player CSV formatting into CsvExportSafety so export mapping can be regression-tested independently. Added assertions for source ID, formula injection, embedded commas, null/Unknown values and source units. Windows verification pending. |
| 2026-11-14 | Advanced Search sort-control definite-assignment repair | Pending CI | Release run #212 exposed that search-button callbacks capture the sort-option array and four sort pickers before their declarations. Predeclared the typed sort state at workspace scope, then assigned the controls before rendering the sort toolbar; the two-level SQL whitelist, paging and saved-profile behaviour remain unchanged. Windows verification pending. |
| 2026-11-14 | Player search result enrichment, sort repair and safe CSV exports | Pending CI | Fixed the Advanced Search sort-control closure compile errors and null-safe field resolution; Reset now clears custom sorting. Main Players shows league and home-grown Yes/No/Unknown, retains legacy ST beside canonical STC, and deduplication prefers rows with home-grown facts. Centralised safe CSV quoting; added isolated profile sort round-trip/validation and formula-injection tests. Windows verification pending. |
| 2026-11-14 | Squad Architect suitability and contract coverage | Pending CI | Added a batched latest-snapshot query for explicit positions ratings and raw contract wage facts. Squad Architect now displays all 15 position suitability slots with rated-player count, competent+ count and average source rating, plus wage-record coverage/total/mean in source units and home-grown counts with Unknown distinct. Added SQLite integration coverage for source position aliases, invalid/missing values and imported wage facts. Windows verification pending. |
| 2026-11-14 | Recruitment comparison context and similar-player export | Pending CI | Added Home-grown at club/nation rows to player comparison; enriched Similar Player Discovery with age, league, nation, source-unit transfer value and home-grown Yes/No/Unknown fields; added provenance-rich CSV export with reference/candidate source IDs, similarity, shared attribute counts, coverage and evidence using shared spreadsheet-safe quoting. Added export regression coverage. Windows verification pending. |
| 2026-11-14 | Multi-player shared-category comparison radar | Pending CI | Added a separate comparison-radar service and overlay while preserving the full side-by-side table. Categories are averaged from actual 1–20 source ratings and plotted only when every selected player has data for that category; Hidden and Personality are excluded. Added tests for category means, shared coverage and missing-category handling. Windows verification pending. |
| 2026-11-14 | Advanced Search multi-sort and position filter parity | Pending CI | Added two-key SQL-whitelisted server-side sorting before pagination, persisted primary/secondary sort fields and directions in local profiles, and wired the Players dropdown to the shared canonical 15-position catalogue including Sweeper. Added a database regression for sort tie-breaks across pages and the SW position filter. Windows verification pending. |
| 2026-11-14 | Fifteen-slot position suitability mapping | Pending CI | Added a dedicated tested service for the 15 source position slots and five 1–20 display bands; canonical STC is used with ST/position aliases accepted. Zero, missing, non-finite, out-of-range and unknown fields are unrated. The profile map consumes imported position facts and Verified live facts only. Windows verification pending. |
| 2026-11-14 | Advanced Search home-grown filters and result fields | Pending CI | Added explicit Home-grown at club / nation Boolean result columns and Yes/No/Unknown display fields; expanded the Advanced Search fixture to prove true, false and unreported NULL states are distinguished for both filters. Advanced Search builder, saved profiles, ALL/ANY conditions, paging and shortlist actions are preserved. Windows verification pending. |
| 2026-11-14 | Performance vs Value nullable cohort-test repair | Pending CI | The Windows build confirmed application compilation after the IO/definite-assignment fixes and then failed in two xUnit assertions because position/league percentiles are nullable for insufficient cohort data. Updated the fixture to assert non-null before checking numeric precision. Fresh Windows verification pending. |
| 2026-11-14 | Role Fit template store / local-function compile repair | Pending CI | Added the missing System.IO namespace for atomic JSON template file operations; predeclared the Role Lab template-delete and description controls before captured local functions to satisfy C# definite-assignment rules. Run #206 exposed these errors; the new commit requires a fresh Windows Release/test/publish run. |
| 2026-11-14 | Per-player Role Fit summary in profile | Pending CI | The Positions & Roles tab now uses the same RoleFitService as Role Lab to show the six best supported role estimates, coverage and leading attribute evidence. The scoring normalizer accepts the category-prefixed source keys emitted by the player profile; added a regression test for Technical/Physical display-key mapping. Also removed a duplicate bulk-shortlist test introduced by parallel branch updates. Windows verification pending. |
| 2026-11-14 | Tabbed player profile workspace | Pending CI | Split the existing profile data into Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source & Evidence tabs. Preserved existing profile loading, attribute radar, role/position facts, source-only dated development snapshots, live-validation warnings and source provenance. Windows build and visual acceptance pending. |
| 2026-11-14 | Advanced Search cross-source regression coverage | Pending CI | Added an integration fixture combining core player fields, imported 1–20 attributes and latest-snapshot contract facts; verifies ALL/ANY semantics, missing-value exclusion, deterministic paging and rejection beyond 60 conditions. The existing Advanced Search workspace, saved profiles and result actions remain unchanged. Windows verification pending. |
| 2026-11-14 | Similar-player recruitment shortlist action | Pending CI | Similar Player Discovery can add selected candidates to a named shortlist with source-ID de-duplication and a clear new/already-present summary; added integration coverage for duplicate IDs, invalid IDs, repeat-add counts and note preservation. Windows verification pending. |
| 2026-11-14 | Local shortlist recruitment pipeline | Pending CI | Added in-place migration for Stage, Priority, target fee/wage, next action and review date; legacy shortlist rows remain intact with Identified/Normal defaults. The workspace supports stage/priority filters, due-review/high-priority/target-fee summary, editable metadata and formula-safe CSV export. Similar Player Discovery can add up to 50 selected candidates to a named shortlist with source-ID de-duplication and repeat-add note preservation. Tests cover migration, round-trip, validation and bulk-add behaviour. Windows verification pending. |
| 2026-11-14 | Full-cohort Performance vs Value ranking | `af5f82d0` | Removed the alphabetical pre-percentile SQL cap; overall, position and league cohort percentiles and coverage now use every eligible player in the imported snapshot, with the requested result limit applied only after ranking. Added a regression fixture where the strongest value-gap player is alphabetically outside the first two records. Windows verification pending. |
| 2026-11-14 | Performance vs Value cohort filters | Pending CI | Added position and league selectors plus a clear-filters action. Chart and CSV use the visible filtered rows while all overall and cohort percentiles remain anchored to the full eligible metric/value pool. Windows verification pending. |
| 2026-11-14 | Role Fit scoring, recruitment filters and cancellation | `7d8cef6b`, `3e4acf28` | Extracted a reusable coverage-aware weighted score; added pre-limit age/CA/PA/position filters, adjustable minimum attribute coverage, cancellation, and regression tests for score math and database filters. Windows verification pending. |
| 2026-11-14 | Traceable and safer Role Fit export | `2a6bfc80` | CSV now includes stable player Source IDs and available/expected attribute counts; untrusted text cells are guarded against spreadsheet formula injection. Windows verification pending. |
| 2026-11-14 | Role Fit explainability and normalized-key hardening | `57b08fba` | Each ranked player now carries per-attribute weighted evidence, shown in the results grid and CSV; database search combines normalized aliases before building SQL parameters; added regression coverage for weighted breakdowns and duplicate normalized keys. Windows verification pending. |
| 2026-11-14 | Custom Role Fit templates | Pending CI | Consolidated onto the existing per-PC RoleFitProfileStore. Custom role weights, phase, position group, description and recruitment filters (age, CA, PA, position, coverage, result limit) are saved together; selecting a template restores its filters. Existing atomic save/load/delete tests now also verify filter round-trip and replacement. Windows verification pending. |
| 2026-11-14 | Player profile radar and discovery integration | Pending CI | Added a source-only five-axis attribute radar and wired Similar Player Discovery plus comparison of up to four open profiles into the player hero actions. Missing categories/ratings are excluded. Windows verification pending. |
| 2026-11-14 | Position and league performance percentiles | `92207aa0` | Performance vs Value reports within-position and within-league percentiles using actual imported group labels; groups with fewer than three players show no percentile. Export includes cohort percentiles and stored league. Expanded the regression fixture to verify tied-cohort behavior and minimum sample-size rules. Windows verification pending. | Performance vs Value now reports within-position and within-league percentiles using the actual imported labels; groups with fewer than three players show no percentile. Export includes both cohort percentiles and the stored league. Windows verification pending. |
| 2026-11-14 | 40,000-player indexed-search stress fixture | Pending CI | Expanded synthetic database coverage from 5,000 to 40,000 save players and 80,000 numeric stat facts. ALL/ANY searches assert exact results, sample membership, seeding and query-time guardrails. This tests indexed search at scale, not the full save-import adapter or peak-memory profile. Windows verification pending. |
| 2026-11-14 | Dated player development history | Pending CI | Added a snapshot-history query for CA, PA and value, a per-player dated trend chart, source/import evidence, and a two-distinct-date rule; added a regression that imports three snapshots across two game dates and checks same-date latest-snapshot ordering. Windows verification pending. |
| 2026-11-14 | Performance Value logging namespace compile repair | `b0ecb617` | Imported the service namespace required by the new Performance vs Value window to resolve the CI compile error from run #185. Run #186 exposed a namespace-shadowed Canvas call and the template Save event ordering; both are fixed in the follow-up commit. Windows verification pending. |
| 2026-11-14 | Role Fit Windows compile repair | `4d10811d` | Fixed the cancellation button's definite-assignment issue and retained age-filter values outside validation scope for completion logging; corrected regression expectations for normalized duplicate attributes. Verified by successful Windows Actions run #182. |
| 2026-11-14 | Performance vs Value percentile analysis | `bdbbc9a0` | Added metric-to-value percentile comparison using the best available source table (season stats, then season history, then match stats), minutes-weighted per-90 for eligible counting metrics, career-wide percentiles, source-value units, sample count, latest period, coverage and an interactive scatter view. CI pending. |
| 2026-11-14 | Player comparison, similar-player discovery and scale verification | `73c4092e` | Added side-by-side comparison for up to four players, attribute-vector similarity from source-backed non-hidden 1–20 footballing attributes, explained coverage and a synthetic 5,000-player/10,000-fact ALL/ANY metric-search fixture. Windows build exposed one Advanced Search button definite-assignment issue; fix is being committed. |
| 2026-11-14 | Indexed metric-search pushdown | `6f6193aa` | Added SQLite-side ALL/ANY evaluation using aggregated per-condition predicates, per-90/minutes filtering, candidate-row grouping, a composite fact-search index, lazy player identity resolution and an import/search regression fixture. Windows build, integration tests and package passed in run #176; real-career correctness and large-table benchmarks remain outstanding. |
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
| 2026-11-14 | Reduce duplicate JSON array allocations | `26104ba3` | First removed secondary array/List copies; superseded by the later per-table NDJSON stream. |
| 2026-11-14 | Per-table NDJSON adapter | `c8ec8fc2`, `a28ae44a` | Python now streams each table to its own NDJSON file, then normalises player/staff/club records one at a time; fixed normalizer return handling in the follow-up commit. |
| 2026-11-14 | Incremental NDJSON database import | `7f7f8c1e` | C# accepts the new manifest/bundle schema and yields one parsed JSON row at a time into the existing SQLite transaction; legacy JSON import remains supported. |
| 2026-11-14 | Bridge/UI bundle integration | `a00c17f7`, `c31a0072` | The Python bridge now returns an output directory, and the UI imports it and recursively cleans up the temporary bundle. |
| 2026-11-14 | NDJSON adapter regression test and CI validation | `c18b3322`, `cf9559cf` | Added Python syntax validation and a mocked fmsave adapter regression test; Python test step passed on Windows run #141. .NET build/publish for the same revision is still running. |
| 2026-11-14 | AND/OR logic for specialist search engines | `e9fae94d`, `590d205a` | Attribute/personality and performance-stat search can now match ALL (AND) or ANY (OR) conditions. A single cross-category query builder remained open at that point. |
| 2026-11-14 | Universal Advanced Search models and database query | `7b79c568`, `f6ea031f` | Added field/criterion models, live field catalog discovery and a parameterized query joining core player fields, PlayerAttributes and latest-save FmsavePlayerFacts with AND/OR semantics. |
| 2026-11-14 | Universal Advanced Search interface | `49f29f89`, `aa9e9843`, `a33de936`, `76bb3ce4` | Replaced the navigation-only search hub with a category/field condition builder, typed values, 60-condition limit, ALL/ANY logic, result grid and profile open on double-click. Release compilation passed on Windows Actions run #101 for the initial universal builder; follow-up profile/column/paging changes require a new compile. |
| 2026-11-14 | Local saved search profiles and configurable result columns | `c060e4bf`, `e281a339`, `faecd076`, `33c57756` | Added atomic per-PC profile storage and create/replace/load/delete UI; profiles preserve criteria, search text, ALL/ANY logic and selected result columns. Added visible/hidden result-column controls and retained a required Player column. Windows compile for this final profile UI is pending run #146. |
| 2026-11-14 | Reuse prepared SQLite commands during save indexing | `2ed5eb72` | Player upsert, raw player, player facts, stale-attribute cleanup, attribute, staff, club and extra-table inserts now reuse prepared commands within the import transaction. Large-save throughput and transaction tests still required. |
| 2026-11-14 | Career bundle count reconciliation | `fd8fe59b` | Before committing a new snapshot, compare manifest and indexed player/staff/club/additional-table counts; add explicit warnings for skipped/mismatched rows and abort if no player row can be indexed. C# build and malformed-bundle tests remain pending. |
| 2026-11-14 | Persist save-import reconciliation warnings | `779c7f42` | Manifest/indexing warnings are written back to the final import-run record in the same transaction instead of being lost after the initial insert. |
| 2026-11-14 | Protect adapter output directories | `198ae45d`, `996302b6` | The Python bridge refuses an existing output folder and the regression test verifies a sentinel file survives; passed in Actions run #168. |
| 2026-11-14 | Accurate bulk shortlist counts | `c1aeaa96`, `377b55e7` | The transactional insert ignores existing player/shortlist pairs and returns only newly added records; the UI reports new versus already-present counts. |
| 2026-11-14 | Strict NDJSON row-count integrity | `ef1c87b5` | The C# importer verifies every physical table stream against its manifest count before commit, distinguishes raw rows read from valid records indexed, and rolls back missing/truncated bundles; compiled and integration tested in run #168. |
| 2026-11-14 | Isolated C# import integration test project | `8856a3f4`, `c32b3ad6`, `6c848f83`, `84a4da28`, `e8cbd5da` | Added an injectable per-test SQLite path, a Windows xUnit test project in the solution and CI `dotnet test` stage. Four tests cover commit, manifest mismatch rollback, malformed NDJSON rollback and preservation of the prior committed snapshot. CI execution pending. |
| 2026-11-14 | Cancellation/warning integration tests and compiler warning cleanup | `955f2c9e`, `c1f5c153` | Added tests for cancellation immediately before commit and for persisted row-skipping warnings. Removed 12 unused legacy fields, changed the live-memory gates to non-constant fail-closed properties, and added a guarded async profile-restoration wrapper. Windows run #172 is checking the six-test suite and warning changes. |
| 2026-11-14 | Successful integrated V8 build and package | GitHub Actions run [#157](https://github.com/lewisxatk/FM-Atlas/actions/runs/43536598003), artifact `14090820119` | Python adapter regression tests, .NET 8 restore, Release build, self-contained win-x64 publish and ZIP upload all passed for commit `77ec4364`. Product ZIP SHA-256: `2dc76b11ede85de9f28debc61322374292bfbaf88b272b690a12e44a3bff7eaf`. Later folder-safety, shortlist-count and strict row-count code commits need a fresh compile before their own validation can be ticked.
| 2026-11-14 | Successful integrated V8 build and import tests | GitHub Actions run [#168](https://github.com/lewisxatk/FM-Atlas/actions/runs/43537353738), artifact `14090511809`, commit `e8cbd5da` | Python adapter syntax/regression tests, .NET 8 restore, Release build, all 4 C# import integration tests, self-contained win-x64 publish and ZIP upload all passed. Product ZIP SHA-256: `775bb48f8e61a0080f379075a07732e01eec21e4fe32e746d5e4f57602753c98`. This validates source/build/import fixtures, not the app’s real Windows startup or a large real FM save import.
| 2026-11-14 | First Windows Release compile and fixes | `80b4160d`, `b35432d8` | First attempt exposed 7 errors (four event-handler signatures, three WPF/System.IO Path ambiguities). Fixed callback signatures and added an explicit System.IO.Path alias. |
| 2026-11-14 | Successful Windows x64 Release build and package | GitHub Actions run [#79](https://github.com/lewisxatk/FM-Atlas/actions/runs/43534383967), commit `18d32e2d` | .NET 8 setup, solution restore, Release compilation, self-contained win-x64 publish and artifact upload all passed. Product ZIP SHA-256: `4a16f815150e41855b2b1d5dba884c6878ac5a69f44b8ae44ebea8570b2798e2`. This is a compile/package checkpoint only; Windows runtime and FM acceptance remain open. |


## 11. Live career identity, staff and manager integration (2026-11-14)

The exact-profile read-only live scan is enabled automatically for supported FM27 builds. Each scan is hard-limited to two minutes, reports progress, is cancellable from the toolbar, is monitored against process/build/save-generation and passes the same cancellation token into the SQLite publication so a cancelled session is not committed.

- [x] Add the profile-matched read-only staff extractor, including the distinct human-manager identity-block layout and class-scoped layout selection.
- [x] Capture staff UID/name, DOB/age, CA/PA, nation UID/name, linked club UID/name and job code/title when the source exposes them.
- [x] Reject ambiguous human-manager identity: only exactly one detected manager is published into `LiveCareerIdentity`.
- [x] Persist live staff identities and field-level facts in `LiveStaff`, `LiveStaffFacts` and `LiveCareerIdentity` within the same live snapshot transaction.
- [x] Include the live save-generation counter in career identity and reject a scan if the generation changes before completion.
- [x] Expose live staff in the existing Staff directory and profile-facts panel; `LIVE:` IDs are normalised before querying live staff facts.
- [x] Keep manager/club toolbar shortcuts tied to the current probe generation rather than stale prior-session identity.
- [x] Require the manager/club shortcuts to match the exact process ID, build profile and save-generation session that produced the committed live snapshot; a matching counter alone is insufficient.
- [x] Add SQLite integration coverage for staff search, manager identity, CA/PA, age, nation, club, provisional facts and replacement on a new session.
- [x] Add active-snapshot replacement coverage: old LIVE player/fact rows are replaced atomically while offline/local records and their attributes remain intact.
- [x] Add a cancelled-before-process-open regression to ensure a cancelled live scan exits without attaching to FM.
- [x] Extend the live staff search contract so name, club and numeric UID queries all work against live rows; add regressions for club and UID search.
- [x] Keep live editing disabled.
- [x] Bound live entity-array scanning to a two-minute deadline and check cancellation at entity batches; keep the monitor checking the session while the worker runs.
- [ ] Run Windows Release build and the full .NET integration suite after the latest staff-search, bounded-scan and exact-session commits.
- [ ] Run controlled FM27 PC acceptance: load career, verify the bounded scan and staff/manager identity, switch career during a scan, cancel manually, close/reopen FM and confirm no mixed-session snapshot is committed.
- [ ] Profile FM CPU/RAM, scan duration and responsiveness at menu and during a match; confirm the two-minute deadline is sufficient for a real large career.
- [ ] Validate every staff field and the human-manager object layout against the in-game UI and an independent source; keep all un-witnessed values provisional.
- [ ] Keep the two-minute cap, cancellation and exact-session guards in place. Do not increase scan scope or enable live editing without independent field and rollback evidence.

## 12. Latest handover

- Current working branch: `rebuild/v8-full-product-rebuild`.
- Current release source remains the R7.2.0-based V8 WPF/.NET 8 application under `application/R7.2.0/FM-Atlas-R7.2.0-Work/`.
- Advanced Search, Players search, paged results, profile tabs, deferred profile restoration and staff/club workspaces are retained. Do not replace these with a simplified new search or rebuild.
- The Staff live-facts lookup already handles the `LIVE:` source-ID prefix in this branch. Do not reintroduce a numeric-ID-only lookup.
- The read-only live-memory import is ON for exact supported profiles, with a two-minute deadline, toolbar cancellation and session-change cancellation. Offline save import remains available for extended history and match statistics. Live editing remains OFF.
- No Windows build/test result is claimed for the latest commits until a fresh Actions run reports success. Historical green runs do not validate the latest edits.
- User PC/runtime acceptance, actual FM27 save compatibility, full import throughput/peak-memory, and packaged release acceptance remain outstanding.
- Canonical handover companion: `docs/V8_HANDOVER.md`.
