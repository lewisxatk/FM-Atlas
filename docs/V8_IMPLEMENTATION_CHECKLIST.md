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

- [~] Existing player results are indexed/paged in source; cancellation and full stable sorting still require validation and implementation.
- [x] Add a unified Advanced Search workspace combining core player fields, dynamically discovered PlayerAttributes and latest-save facts in one field/category builder.
- [x] Add a Cancel search action with cancellation propagated through SQLite and last successful result-page retention.
- [~] Add a dynamic category and field library for core fields, contract facts, attributes/personality, positions, traits, state, reputation and raw attributes. Performance metrics remain in the dedicated per-period search workspace; source coverage display and additional categories are still open.
- [x] Universal player search supports up to 60 mixed core/attribute/save-fact conditions with ALL/AND or ANY/OR matching. Specialist performance search remains separate because its criteria are scoped to player-period rows.
- [x] Add local saved universal search profiles with create/replace/load/delete, persisted criteria, search text, AND/OR mode and selected result columns. Reset-to-default is present for the active filter builder; broader filter presets remain open.
- [~] Add configurable visible columns, stable 250-row paging and multi-select bulk Add-to-Shortlist. Bulk insertion is transactional and reports new versus already-present players accurately. Multi-sort, player comparison and shortlist removal/notes actions from the search result grid remain open.
- [ ] Support all 15 FM positions and validated five-level suitability display.
- [ ] Add league, nationality, home-grown, contract and staff filters when available in source data.
- [ ] Ensure missing/undecoded values are never silently treated as zero or as a failed filter match.
- [ ] Add test fixtures for filter semantics, combined logic, paging, sorting and 40k+ player datasets.
- [x] Four baseline Windows .NET integration tests passed on Actions run #168: valid NDJSON commit, manifest-count mismatch rollback, malformed extra-table rollback, and preservation of a previously committed snapshot.
- [~] Two additional Windows .NET integration tests are now committed: cancellation immediately before commit must roll back all new rows, and skipped staff rows must leave reconciliation warnings persisted in `WarningsJson`. The latest Windows run #172 includes both tests; their result is not confirmed yet. WAL contention and broader relationship checks remain outstanding.

## 6. Player profiles and development

- [ ] Complete player profile tabs: Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source/Evidence.
- [ ] Add validated position/role fit, attribute radar and comparison visualisations.
- [~] Add dated CA/PA/value history from source player snapshots with source-file/import evidence; a chart appears only after two distinct in-game dates and does not project or interpolate missing values. Windows verification pending.
- [~] Add longitudinal CA/PA trends only where at least two dated comparable save snapshots exist. Attribute-by-attribute history still requires a stable dated attribute snapshot source.
- [ ] Add player comparison and similar-player discovery with explainable ranking.
- [ ] Ensure no inferred hidden attributes or unsupported development predictions are presented as fact.

## 7. Recruitment, Moneyball and analytics

- [x] Indexed statistic search pushes each metric's numeric threshold into SQLite's indexed candidate predicate, evaluates ALL/ANY with per-condition aggregation, applies per-90/minutes filters before materialisation, groups qualifying player-period rows server-side, and resolves only candidate player identities.
- [~] Add a 40,000-player / 80,000-fact synthetic regression and latency smoke test for indexed ALL/ANY search, including seeding and query thresholds. Real FM27 career verification, actual adapter import throughput and peak-memory benchmarking remain open.
- [x] Centralise the weighted Role Fit calculation in `RoleFitService.CalculateScore`; only finite source-provided 1–20 attributes contribute, and the score returns separate available/expected counts and coverage. It remains explicitly an Atlas estimate, not the game's hidden role formula or star rating.
- [x] Upgrade Role Fit Lab with minimum/maximum age, minimum CA, minimum PA, position-contains and minimum attribute-coverage filters. Candidate predicates are applied in SQLite before candidate limiting; role-coverage thresholds are applied before ranking/result limiting.
- [x] Add cancellation through Role Fit database reading and scoring; a cancelled search keeps the last completed result set.
- [x] Make Role Fit CSV exports traceable with player Source ID and attribute coverage counts, and guard untrusted text cells against spreadsheet-formula injection.
- [x] Add per-attribute weighted evidence to Role Fit scores, including the source attribute value, effective weight, weighted points and share of total; surface evidence in the ranked grid and CSV.
- [x] Merge equivalent normalized attribute keys before database search so aliases such as `Finishing` and `finish_ing` do not throw duplicate-key errors or inflate coverage.
- [x] Verify the new Role Fit calculation, evidence breakdown, duplicate-key handling, database filters, cancellation and export changes in the latest Windows build and automated tests (Windows Actions run #182 passed all workflow stages, including the .NET integration suite and self-contained win-x64 publish).
- [x] Correct Role Fit Lab compile-time closure issues in the cancellation control and age-filter logging scope discovered by Windows CI run #179; a fresh Windows build is required to confirm the repair.
- [ ] Benchmark metric search, import throughput and peak memory using 40k+ player / large-stat fixtures.

- [~] Add side-by-side comparison for up to four selected players and source-backed similar-player discovery based on shared non-hidden 1–20 attribute vectors. Search results and player-profile actions now open both tools; Windows verification is pending.
- [~] Add a five-axis player attribute radar using available Technical, Set Pieces, Mental, Physical and Goalkeeping source ratings. Missing categories are excluded rather than plotted as zero; Windows verification is pending.
- [~] Add locally persisted custom Role Fit templates with editable visible attribute weights, phase/position group, description, and saved age/CA/PA/position/coverage filters. The existing RoleFitProfileStore is the single persistence path; selecting a built-in profile clears any custom saved filters. Replace/load/delete and validation are covered by tests. Windows verification is pending.
- [~] Add a performance-versus-value percentile scatter view using source metric rows, a minimum-minutes floor, source value units, sample count and period freshness. It now also calculates tie-aware within-position and within-league performance percentiles when a cohort has at least three players. League strength is not available in the imported metric index and is not adjusted. Windows verification pending.
- [~] Calculate overall and position/league percentiles across the full eligible metric/value cohort before applying the visible result limit; regression coverage proves alphabetically later high-value-gap players are not excluded. Windows verification pending.
- [ ] Add per-90, percentiles and position/role-specific metrics only when source supports them.
- [ ] Show sample size, minutes, competition strength, freshness and data coverage.
- [~] Similar-player ranking is implemented with explicit similarity score, shared-attribute count and anchor coverage; recruitment pipeline and broader recommendation factors remain open.
- [~] Performance vs Value scatter now filters by metric, per-90 eligibility, minimum minutes, position and league; dots expose hover evidence and filtered rows export to CSV. Zoom/range controls and broader analytics-chart coverage remain open.
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
- [~] All compiler errors have been fixed and the Release build passed on run #168. Removed twelve unused legacy fields, replaced compile-time live-memory `const false` gates with non-constant fail-closed properties, and wrapped deferred profile restoration in an explicit error handler (`c1f5c153`). Run #172 will check whether the known CS0162/CS0169/CS4014 warnings have reduced; any remaining warnings need a fresh review.
- [~] Add unit tests for data mapping, identity joins, filter logic, calculations and validation. New Role Fit scoring and database-filter regression tests are committed; current Windows Actions result is pending.
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
