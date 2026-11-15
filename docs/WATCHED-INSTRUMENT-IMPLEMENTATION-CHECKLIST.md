# FM Atlas — Watched Instrument rebuild tracker

**Branch:** `rebuild/watched-earth-fmrte-ui`  
**Base:** `rebuild/v8-full-product-rebuild`  
**Draft PR:** [#8 — Watched Instrument UI and full-width player profiles](https://github.com/lewisxatk/FM-Atlas/pull/8)  
**Target:** Windows 10/11 desktop; FM27 first, FM26 only where field/source evidence supports it.

## Status key

- [x] Source implemented and committed
- [~] Partially implemented; further work remains
- [ ] Not implemented
- [!] Blocked pending reverse-engineering evidence or real-game validation

A source checkbox is never a build, test, visual-acceptance or game-validation pass. Record evidence separately.

## 1. Watched Instrument visual system

- [x] Research Watched.earth's public design document and source; record the reference and translation into Atlas in `WATCHED-EARTH-FMRTE-FULL-UI-REBUILD.md`.
- [x] Replace Monolith Glass as the first-run default with a dedicated flat ink / bone / sodium-orange theme.
- [x] Add the dedicated shell, header/career readout and left navigation for the Watched theme without deleting the previous theme paths.
- [x] Add flat, square, hairline card surfaces for the shared Card helper in the Watched theme.
- [x] Use locally installed display and monospaced fonts (`Bahnschrift`, `Segoe UI`, `Consolas`) with Windows fallback; no font binaries copied.
- [~] Apply flat square surfaces to the remaining custom profile, club, staff, chart and dialog controls; remove remaining theme-specific glow/gradient effects from the Watched path.
- [ ] Add a dedicated visual token/resource dictionary so colours and typography are not spread through MainWindow code-behind.
- [ ] Build reusable square input, tab, filter, grid, status, chart and tooltip controls.
- [ ] Make labels, current selection and status marks accessible by keyboard and screen reader; test contrast and high-contrast mode.
- [ ] Add a Reduce Motion preference and verify system animation preferences are honoured.
- [ ] Manually inspect every major workspace at 100%, 125%, 150% DPI and laptop/desktop sizes.

## 2. Navigation and workspace model

- [x] Keep the current V8 navigation and existing workspaces available in a dedicated branch.
- [x] Maintain a dedicated Watched shell/sidebar while retaining the existing appearances as optional alternatives.
- [x] Open selected player records in the full available workspace width; hide the finder controls while the record is active and restore them on Back.
- [x] Retain existing per-player closable tabs and record state during this transition.
- [ ] Migrate search workspaces and all entity records into a consistent top-level workspace-tab host.
- [ ] Add closable Players, Staff, Clubs and Advanced Search workspace tabs; restore active tab and open tabs safely.
- [ ] Keep Ctrl+K command palette, recent records and keyboard navigation consistent across entity types.
- [ ] Persist workspace state, active tabs, table columns, column order/width, sorting, filter state and view density.

## 3. Player finder and FMRTE-style search

- [x] Retain indexed/paged player finder and existing CA/PA and position predicates.
- [x] Automatically open a selected player full-width rather than leaving a narrow inspector beside the result list.
- [~] Keep the last finder criteria and result page in memory when entering and leaving a player record.
- [ ] Dedicated filter library with categories, searchable FM field names, supported operators, min/max ranges and include/exclude.
- [ ] ALL/ANY condition groups, reusable presets, saved searches and query explanations.
- [ ] Column chooser based only on fields supported by the current data source.
- [ ] Multi-column sorting, result count, page-size/density settings, export and multi-select actions.
- [ ] Mirror player discovery patterns for staff and clubs using each record's actual supported fields.

## 4. Full player record

- [x] Reuse existing source-backed profile loader and existing player data tabs.
- [x] Use the full workspace width automatically when selecting a player.
- [x] Apply flat Watched styling to the player profile header, identity card, metrics and attribute-value surfaces.
- [x] Link statistical search rows to player records by exact source ID; double-click opens the full-width record tab.
- [x] Add an exact source-ID player lookup that respects the active imported-save snapshot.
- [ ] Full-width record tabs for Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source/Evidence.
- [ ] Attribute catalogue and sections must be generated from supported FM field mappings; unavailable attributes remain unavailable, not zero.
- [ ] Show CA/PA where decoded, with source and validation marker.
- [ ] Display face graphics only where a local graphics mapping resolves; do not fabricate portraits.
- [ ] All player stats shown from actual FM save/live/export records, with competition, season/period, minutes, unit and source.
- [ ] Add visible source coverage and field evidence for provisional/unverified values.
- [ ] Verify every profile field against the FM27 in-game UI or a trusted source row before promoting it to Verified.

## 5. Stats and Moneyball

- [x] Preserve indexed raw FM performance-fact storage and metric discovery from imported records.
- [x] Preserve indexed multi-condition stat search with sample/minutes guards as implemented on V8.
- [~] Existing per-90 and percentile views are labelled as calculated by Atlas and expose sample/freshness constraints in some screens.
- [x] Bind FM View Exporter imports to the exact FMSave snapshot they were matched against; hide archived exports from the active metric catalogue, player profiles and statistical searches after career switches.
- [x] Add an integration test with two careers and snapshot-specific player/metric data to guard against cross-career FM View stat leakage.
- [ ] Make a shared dynamic metric catalogue from all discovered FM source schemas/rows; keep original field name, type, source table, unit, period and version.
- [ ] Expose every supported raw season/career/match metric in player profiles and search columns.
- [ ] Separate raw FM values, Atlas calculations, Atlas estimates and unavailable/unverified fields throughout the UI.
- [ ] Move FM View Exporter player identity and attribute fields into snapshot-scoped provenance records; stop mixing those imported facts into the unversioned Players / PlayerAttributes tables while preserving local edits as a distinct source.
- [ ] For every derived measure, show formula, units, selected cohort, sample size and minimum-minute rules.
- [ ] Never show external football data or unsupported metrics as if they were in-game FM stats.
- [ ] Validate displayed totals, rates and derived values against source records and independent calculations.

## 6. Clubs, staff, squads and tactics

- [~] Existing club and staff directories/profiles remain available and have some source-backed aggregates and facts.
- [ ] Move club and staff profiles to the same full-width closable record-tab framework.
- [ ] Display club, competition, league, facilities, stadium, finances, squad and staff information only where present in FM data.
- [ ] Link player and staff records from club/squad pages.
- [ ] Complete squad planner, formation, positional depth and wage/age views using source-backed facts.
- [ ] Distinguish FM's own role/suitability ratings from Atlas estimates; expose weights and coverage for Atlas estimates.

## 7. Editor and reverse engineering

- [x] Preserve the V8 hard block on live game memory Apply/Undo.
- [x] Preserve local Atlas edit separation from live-game writes.
- [ ] Add a field capability matrix by entity, field, FM build, storefront, source, read status and write status.
- [ ] Research and validate every FM27 field: type, offset/schema, identity association, range, decoder, evidence, confidence and game UI witness.
- [ ] Implement complete local drafts, field search/grouping, mass-edit preview, diff, validation, audit and undo.
- [!] Live writes remain blocked until exact process/build/session identity, field witness, expected-old-value checks, supported bounds, write, read-back and rollback are all proven against a disposable career copy.
- [ ] Do not claim every field can be live-edited until the capability matrix proves that it can.

## 8. Architecture and performance

- [ ] Extract the shell and navigation from the large WPF MainWindow code-behind.
- [ ] Create independent Views, ViewModels, domain models, use-case services and data/game adapters.
- [ ] Move player finder and record rendering into reusable views.
- [ ] Keep WPF dispatcher free of imports, searches and large profile queries; retain cancellation and bounded concurrency.
- [ ] Benchmark 40k+ player and 28k+ club fixtures, large performance-fact tables and simultaneous profile tabs.
- [ ] Benchmark startup, navigation, search, record open and save import before/after.
- [ ] Preserve existing R7.2.0/V8 database, settings, shortlist and snapshot migrations with backup/rollback.

## 9. Validation and release gates

- [x] Create a separate feature branch from the integrated V8 rebuild; do not overwrite `main` or PR #4.
- [x] Open a draft PR against the V8 integration branch.
- [ ] Confirm current Windows .NET 8 Release restore/build on this PR head.
- [ ] Confirm Python adapter syntax and unit tests on this PR head.
- [ ] Confirm complete .NET integration tests on this PR head.
- [ ] Confirm self-contained Windows x64 publish and checksum on this PR head.
- [ ] Launch this build on Windows 10/11 and inspect all workspaces.
- [ ] Check Steam and Game Pass FM27 career detection only on supported, profiled builds.
- [ ] Validate player/staff/club facts and stats against the live FM27 UI and an offline save copy.
- [ ] Check switching and closing careers, stale session cancellation, failed imports and restoration of last committed data.
- [ ] Check local editor changes never mutate the original career save.
- [ ] Create an installable test package with rollback instructions and clear known limitations.

## Work log

| Date (UTC) | Change | Evidence / status |
|---|---|---|
| 2026-11-15 | Watched.earth and FMRTE research translated into a full rebuild specification | Source notes committed; no third-party code copied |
| 2026-11-15 | Watched Instrument shell, sidebar, flat token mapping and type helpers | Source committed; Windows build and visual acceptance pending |
| 2026-11-15 | Player selection opens existing closable record tabs in full-width mode | Source committed; manual UI acceptance pending |
| 2026-11-15 | Player profile header, metrics and attribute value surfaces moved to flat Watched treatment | Source committed; build and visual acceptance pending |
| 2026-11-15 | Stat search results now open player records by exact source ID | Source committed; active-snapshot lookup and full-width navigation implemented; tests pending |
| 2026-11-15 | FM View Exporter imports are now bound to the matching save snapshot; active reads exclude archived exports from other careers | Schema migration + query filters + two-career integration test committed; test execution pending |
| 2026-11-15 | Product metadata updated | Source committed; Windows build pending |

**Current gate:** implementation is in progress. No Windows CI run, clean build, runtime launch or real-game witness has been recorded for this branch. Do not distribute it as release-ready.
