# FM Atlas V8 — reverse-engineering research notes (2026-11-14)

This note records external implementation patterns reviewed during the V8 rebuild. It is a research ledger, not a claim that another project's offsets or source code have been adopted. FM Atlas keeps its own implementation, evidence records and build-specific validation.

## FMSuperScout — fast, read-only career indexing

Repository: https://github.com/mavarobli/FMSuperScout

Files reviewed:
- https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs
- https://github.com/mavarobli/FMSuperScout/blob/main/core/Dumper.cs
- https://github.com/mavarobli/FMSuperScout/blob/main/scanner/Program.cs

Useful architectural patterns:
- Keep a shared scan core separate from its Windows external-process scanner and other hosts.
- Select a version-specific field layout from the loaded game module version; do not silently carry offsets into a new game build.
- Keep the external scanner read-only and independent of a mod/plugin layer in the game folder.
- Emit structured scan diagnostics: phase timings, object counts, class-offset histograms, deduplication counts, manager identity and version status.
- Treat human-manager identity as an explicit relationship to resolve, not a guessed first manager.
- Reuse a long-lived watcher/request signal so the scan can be refreshed without restarting the desktop UI.

FM Atlas application:
- Existing live reader remains read-only, uses exact process/build/session detection, a save-load generation key, bounded cancellation and a transactional snapshot publish.
- Next useful adoption is diagnostic/throughput instrumentation and better staged progress, not copying another project's offsets.
- Keep scan timings, coverage and deduplication visible in the Activity Centre/Diagnostics rather than adding an unbounded memory scan.

## barak14/fm-editor — field evidence and versioned schema

Repository: https://github.com/barak14/fm-editor

Files reviewed:
- https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md
- https://github.com/barak14/fm-editor/blob/main/src/fields/fm27/player.rs
- https://github.com/barak14/fm-editor/blob/main/docs/development.md

Useful patterns:
- Separate field definitions from the version/storefront memory-layout registry.
- Keep a written memory-layout notebook that distinguishes verified fields from inherited or unverified fields.
- Record independent in-game witnesses for each field, including the displayed UI value, player/club identity and exact game build.
- Use fake address spaces and golden UI-captured fixtures for deterministic tests.
- Re-read values after writes and verify Undo against fresh process reads; cached FM profile screens can remain stale until refreshed.
- Group related physical writes under one logical history action, but explicitly test and report partial/refused outcomes.

FM Atlas application:
- Preserve the current fail-closed live-write policy. V8 has no exposed live Apply/Undo actions; the local Editor Studio writes only to the Atlas database.
- Do not import an offset simply because another editor lists it. Require exact build/storefront match, an FM27 UI witness, identity re-resolution, current-byte preflight, read-back and tested rollback before considering any future write capability.
- Continue using source labels and provisional confidence for un-witnessed live fields.

## FMLibreEditor — save-generation safety and graceful read-only fallback

Repository: https://github.com/tozmoz/FMLibreEditor

Files reviewed:
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/INSTALL.md

Useful patterns:
- Explicitly support separate storefront profiles and known game builds.
- When the save changes, stop edits/freezes and require a fresh reload; never carry one save's pending action into another.
- Keep the changes view as an audit trail with a clear Undo state.
- Fall back to read-only when the version/layout is not supported.
- Provide a diagnostics report that avoids exposing player/club names or private file paths.

FM Atlas application:
- Existing live career monitor checks the process, module profile and save-load counter and cancels a scan if the generation changes.
- V8 history keeps old live-memory entries for audit but live Undo is blocked. Local Atlas edits can still be undone in the Atlas database only.
- Diagnostics should report build/profile/coverage and failures without dumping private save contents.

## Current build compatibility caution

The reviewed sources describe different FM27 build identifiers and release states. The FM Atlas settings page currently documents fmsave's registered layout as FM27 `27.0.0+2486695`; the reviewed FMLibreEditor release notes identify later FM27 builds, including Steam `27.0.1` and Game Pass `0.9.2435.0`. These are not interchangeable identifiers. Keep the Atlas reader's exact build gate and provisional data labels until the user's installed build has been checked.

## Next engineering actions

1. Add phase timing, row counts, dedupe and coverage telemetry to the existing bounded scanner.
2. Add golden, source-backed FM27 fixtures for player/staff/club identities and field decoders.
3. Extend diagnostics with build/profile, import validation, database health and last scan/import result.
4. Run current Windows Release build and tests; the current workflow run is queued and must not be reported as passed until completed.
5. Keep UI, Advanced Search, player comparison, shortlists, performance analytics and existing database compatibility intact while extending the data pipeline.


## FMRTE 27 and Genie Scout 27 — search and scouting interaction patterns (14 November 2026)

Reviewed the current FMRTE 27 search documentation and Genie Scout 27 feature reference.

- Keep Quick Search as a fast name/ID entry point; keep Advanced Search as the explicit multi-criteria workspace.
- FMRTE separates player, coach and club search scopes, offers min/max ranges, reusable saved criteria and independent result-column configuration. Its position filters support both all-selected and any-selected matching, and zero means no criterion.
- Genie Scout exposes all standard and hidden attributes, mental traits, position familiarity, positional and role ratings, searchable saved criteria, compare and similar-player flows. Its public feature descriptions distinguish current and projected potential data.
- FM Atlas already preserves Quick Search/global search separately from its player Advanced Search, uses discovered source-backed fields, stores profiles and columns/sorts, and keeps compare/similar/export/shortlist actions. The present batch improves the filter-row ergonomics without replacing that model.
- Do not copy proprietary formulas or infer hidden values. FM Atlas role scores remain explicitly labelled estimates until independently validated against FM's own displayed ratings or a documented formula.

Reference links:
- FMRTE 27 search guide: https://docs.fmrte.com/scouting/search/
- FMRTE 27 product/release notes: https://www.fmrte.com/
- Genie Scout 27 feature reference: https://geniescout.com/features/

## FMLibreEditor — current FM27 source and safety patterns (14 November 2026)

Reviewed the public `tozmoz/FMLibreEditor` repository and its development notes.

- The project is an open-source, separate-process FM27 reader/editor supporting Windows Steam and Game Pass as well as macOS Steam.
- Its documented architecture reads the native `game_plugin.dll` process memory rather than the Unity UI process; its README describes a read-only fallback for unsupported game versions and refusing writes until the game version has been checked.
- Its public FM27 implementation provides a useful independent source for field-layout research, position-map presentation, search and save-generation safety. FM Atlas continues to keep its own profile fingerprints, range checks, source-confidence labels and cancellation/session guards.
- Cross-checking the current staff decoder regression fixture found a test-only count error: the fixture seeds five valid nonzero ratings, one invalid rating (101) and leaves the rest zero. The decoder correctly omits missing/invalid bytes, so the expected dictionary count is five. Do not weaken the decoder's omission rules to satisfy a fixture count.

Reference links:
- Repository: https://github.com/tozmoz/FMLibreEditor
- Development architecture notes: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- Repository README and current implementation status: https://github.com/tozmoz/FMLibreEditor

## Search Studio interaction update

- Filter rows now wrap at narrow widths rather than forcing the entire category/field/value/action set onto one horizontal line.
- Each condition can move up or down; visual order and criteria-capture order are updated together. Remove remains available.
- This is a UI-only enhancement to the existing criteria/profile model. It does not alter query semantics, saved profile schema, field discovery, paging, sorting or the 60-condition limit.


## FMRTE 27 search semantics, Genie Scout ratings and FMLibreEditor pitch/release patterns — 14 November 2026

### Sources reviewed

- FMRTE Search and Advanced Search documentation: https://docs.fmrte.com/scouting/search/
- FMRTE player overview / positions and role ratings: https://docs.fmrte.com/players/overview/
- FMRTE 27 build notes: https://www.fmrte.com/download/fmrte27/
- Genie Scout 27 feature reference: https://geniescout.com/features/
- FMLibreEditor repository: https://github.com/tozmoz/FMLibreEditor
- FMLibreEditor development architecture: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- FMLibreEditor release notes: https://github.com/tozmoz/FMLibreEditor/releases

### Observed patterns and confidence

- **FMRTE — high confidence for documented UI behaviour:** Quick Search is a fast name/ID lookup, while Advanced Search is a separate multi-criteria workspace. Its player/coach/club search scopes expose different field groups. Position criteria support ALL-selected and ANY-selected matching; a zero rating contributes no position requirement. Result grids support multi-column sorting and configurable columns.
- **FMRTE — role/position presentation:** the player profile exposes familiarity by pitch position and role ratings at a selected position. Build notes describe game-formula role ratings and potential ratings; these are proprietary/game-derived semantics and must not be approximated without validated weights.
- **Genie Scout — high confidence for published feature set, not its proprietary formula:** exposes general, positional and role ratings, current/peak views, hidden attributes, mental traits, saved searches, and role/duty filters in its paid edition. Treat this as a feature benchmark only.
- **FMLibreEditor — high confidence for project architecture and published release behaviour:** documents a pitch-position map as part of its interface direction, separate-process native module reading, storefront-specific build support, save-change invalidation, and read-only fallback when a build/profile is not supported. Release notes also show that build support evolves independently for Steam and Game Pass.

### Atlas decisions

- Implemented a pitch-shaped profile view for all 15 canonical position slots. It is a display of existing source values, not a position recommender.
- Kept the existing 1–20 display bands: 1–4 Low, 5–9 Limited, 10–14 Competent, 15–17 Strong, 18–20 Natural. These are Atlas presentation bands, not claims about FMRTE/Genie Scout's exact colour thresholds.
- The pitch map is responsive through a Viewbox, and each position tile is selectable to show its source value and band.
- Zero, missing, non-finite and out-of-range values remain Unrated. Legacy ST maps to canonical STC. Later explicit source values continue to take precedence.
- Added regression tests covering all canonical slots, band boundaries, invalid/missing values, alias mapping and precedence.
- Do not copy Genie Scout/FMRTE role formulas. A role-rating engine needs an independently evidenced FM27 weight schema, exact position familiarity semantics and golden comparisons with in-game/FMRTE output before it can be labelled anything other than an estimate.

### Future implementation queue

1. Add position AND/ANY grouping to Advanced Search with explicit minimum-rating semantics; preserve current saved profiles through a backward-compatible schema migration.
2. Add a role/duty matrix only after the FM27 role-weight schema is independently sourced and versioned.
3. Add current-versus-potential positional/role views only when the source exposes both values.
4. Add pitch UI golden screenshots and DPI acceptance tests; do not treat the previous 5×3 grid as equivalent to the tactical pitch.
5. Re-check FMLibreEditor release notes and storefront build coverage before changing the FM27 live reader's exact build registry.

### Research record maintenance

This file is the permanent reverse-engineering reference for future work. Add a dated entry for every new research batch, including links, reviewed files/pages, observed behaviour, confidence/limitations, implementation decisions and explicit future follow-ups. Update the V8 checklist and handover alongside any implementation.


## FMLibreEditor write-safety checklist and branch-head verification — 14 November 2026

### Research source

- Repository: https://github.com/tozmoz/FMLibreEditor
- Development notes: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md

### Observed pattern

The documented writer performs a sequence of preflight checks before changing memory: (1) confirm the save/session is still the same, (2) confirm the object still belongs to the intended person, (3) validate the proposed value, (4) confirm the current value matches the expected field layout, and (5) read the value back after writing. Failed checks are fail-closed. A save change suspends edits/freezes until reload; unsupported builds use read-only fallback.

Confidence: high for the project's published safety design; this is not proof that its offsets or writer are suitable for FM Atlas.

### FM Atlas decision

- Keep live Apply/Undo disabled.
- Retain the five preflight/read-back checks as mandatory future acceptance criteria, alongside exact build/profile matching, process identity, save-generation invalidation and tested rollback.
- Never carry pending edits across a save switch; require a fresh reload.
- Do not port the writer or its offsets as a shortcut. Atlas must validate its own field layout and identity model.

### Latest Windows verification

- Workflow: https://github.com/lewisxatk/FM-Atlas/actions/runs/43555015459
- Branch: rebuild/v8-full-product-rebuild
- Commit: 21430f4fa4ca5b831022e67c70438a5e5cc9dff0
- All 12 steps passed: Python adapter validation, .NET 8 restore, Release build, career-import integration tests, self-contained Windows x64 publish and artifact upload.
- Artifact: FM-Atlas-V8-win-x64, 68,887,451 bytes; SHA-256 5813abb152cc4f82147c20fb0325fd309c1d34fddc4082161a791be2c6496541; expiry 28 November 2026.
- This is the first verified artifact containing the 15-position pitch map and the canvas type-shadowing fix. It does not constitute real FM27 acceptance or DPI visual testing.

### Next follow-up

Trace the actual Advanced Search position-fact schema and query path before adding independent ALL/ANY position-group semantics and minimum familiarity. Preserve the current global condition logic and old saved-profile compatibility. Add regression coverage for empty/zero, ALL, ANY, threshold, legacy aliases, old/new profile round-trip, cancellation and paging. If the schema does not retain numeric per-position familiarity, stop and document the gap rather than synthesising values.


## FMRTE position criteria and fmsave numeric position schema — 14 November 2026

### Sources reviewed

- FMRTE 27 Advanced Search: https://docs.fmrte.com/scouting/search/
- FMRTE 27 position familiarity: https://docs.fmrte.com/players/positions-and-traits/
- FMRTE 27 attribute scales: https://docs.fmrte.com/players/attributes/
- fmsave 0.12.3 player record reference: https://rhiever.github.io/fmsave/reference/records.html
- fmsave Positions model source: https://rhiever.github.io/fmsave/_modules/fmsave/models/players.html
- FM Atlas adapter: `application/R7.2.0/FM-Atlas-R7.2.0-Work/tools/Export-FMAtlas-Fmsave.py`
- FM Atlas snapshot ingestion: `src/FmAtlas/Data/ScoutDatabase.cs`, especially the `FmsavePlayerFacts` ingestion loop and the `positions` JSON group.

### Evidence and confidence

- **FMRTE documented search semantics — high confidence:** all nonzero position criteria must match by default; “Any of these” changes the position group to any selected position; “At Least” changes exact position values into minimum values; zero contributes no position requirement. This is separate from the rest of the search criteria.
- **fmsave numeric source schema — high confidence for the published reader model:** the `Positions` value object contains 15 numeric 1–20 ratings, with named fields `gk, sw, dl, dc, dr, dm, ml, mc, mr, aml, amc, amr, stc, wbl, wbr`. `natural_positions` and `accomplished_positions` are classification lists (18+ and 15–17), not substitutes for the numeric ratings.
- **FM27 field meaning — still unconfirmed by fmsave:** the reader documents structural coverage, but FM27 meaning still requires in-game comparison. FM Atlas keeps the existing provisional-source warning for live-memory fields.

### FM Atlas implementation decision

- Added a dedicated 15-slot position-familiarity group to Advanced Search.
- Each position accepts an integer rating from 0 to 20. Zero is a no-op. Exact matching is the default; “At Least” uses greater-than-or-equal.
- “Any of these” controls only the position group. With it off, every entered position must match. The existing general ALL/AND or ANY/OR condition mode is untouched; the position group is ANDed with the overall general-condition result.
- The SQL uses the numeric `positions` facts from the latest fmsave snapshot and the existing `LivePlayerFacts` position keys for live records. It does not infer familiarity from natural/accomplished classification lists.
- Legacy `ST` is normalized to canonical `STC`; the 15 canonical position codes are retained.
- Position-only searches are supported. Position settings are stored as an optional profile property, so existing JSON profiles without that property remain compatible.
- Added a searchable field-library text filter; the field list remains discovered from the imported data. Attributes stay 1–20, CA 0–200, and PA -10–200 (including signed random-potential values).
- Kept all existing general filters, 60-condition cap, saved columns/sorts, global logic, cancellation, paging, comparison, similar-player, CSV and shortlist actions.

### Visual direction carried forward

Monolith Glass remains the primary interface: graphite/obsidian surfaces, warm off-white typography, amber-orange accent, layered translucent cards with a fine warm edge, restrained shadow depth, compact rounded controls, dense but readable information hierarchy, and minimal animation. No neon glow, flashing, glass blur over text, or heavy 3D. The shared card material has been refined and the Advanced Search condition rows/position tiles use the same dark graphite surface language.

### Verification still required

The source and regression tests are committed, but a fresh Windows workflow must pass before calling this batch build-verified. Then inspect the Advanced Search workspace and shared Monolith Glass surfaces at 100%, 125% and 150% DPI. Confirm the exact/At Least, ALL/ANY and zero no-op behaviours against a real imported FM27 career; keep live-memory values labelled provisional until witnessed in-game.


### Position-filter integration hardening — 14 November 2026

- Added a database-level regression using the actual FMSAVE snapshot and live-fact tables. It exercises exact ALL, exact ANY and At Least ALL against both offline and live position-rating rows.
- The profile loader now clears the temporary field-library search string before reconstructing saved conditions, preventing a filtered dropdown from remapping a saved field to an unrelated first result.
- The prior successful run 43590420150 passed before these final test/safeguard changes. Its artifact must not be represented as containing this follow-up. A fresh Windows run is required.


### Final implementation verification — 14 November 2026

- Workflow run 43590756414: https://github.com/lewisxatk/FM-Atlas/actions/runs/43590756414
- Verified head 9ec72cbf34af3b51984c7fe23ad2c54573ec36fc.
- All 12 steps passed, including Release build, full xUnit suite, SQLite integration for exact ALL/ANY/At Least position semantics across offline and live data, self-contained win-x64 publish and artifact upload.
- Artifact FM-Atlas-V8-win-x64: 68,894,168 bytes; SHA-256 e76e763e26b35dd73ad4ee9ab1e1b95ffb34b7c090269e406658a2ad7f627f93; expires 28 November 2026.
- Manual Windows DPI/visual review and real FM27 in-game field comparison remain outstanding. The test establishes SQL semantics against seeded data; it does not certify every live-memory field meaning.


## Monolith Glass finish, FM position-rating comparison and scouting-engine research — 14 November 2026

### Repositories and product references reviewed

- FMSuperScout repository: https://github.com/mavarobli/FMSuperScout
- FMSuperScout scanner field map: https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs
- FMSuperScout app and virtualised player table: https://github.com/mavarobli/FMSuperScout/blob/main/app/app.js
- FMSuperScout UI tokens and controls: https://github.com/mavarobli/FMSuperScout/blob/main/app/style.css
- FMSuperScout value model and its calibration limitations: https://github.com/mavarobli/FMSuperScout/blob/main/docs/value-model.md
- FMLibreEditor project overview: https://github.com/tozmoz/FMLibreEditor
- FMLibreEditor development architecture and safety checks: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- FMRTE Advanced Search reference: https://docs.fmrte.com/scouting/search/
- Genie Scout feature reference: https://geniescout.com/features/

### Findings and confidence

- **FMSuperScout scanner architecture — high confidence from source:** field layouts are versioned by FM build; 15 position values, CA/PA, attributes and source value fields are distinct mapped fields. This supports keeping FM Atlas's source/build evidence separate from the presentation layer.
- **FMSuperScout large-dataset UX — high confidence from source:** the app uses a virtualised player table for large player counts, persisted filters/columns/shortlists, multi-player compare and profile views. Atlas already has virtualised grids and shortlist/compare workflows; keep those rather than introducing a second player browser.
- **FMSuperScout value estimation — high confidence for the documented model, low transferability to Atlas:** its own notes record a CA-driven model being replaced after material mid-range overestimation; the revised estimate uses reputation, age, contract length and a youth correction. It explicitly distinguishes read FM values from estimated values, and estimated asking price from transfer value. Atlas must not copy coefficients or present estimated values as FM's actual transfer value.
- **FMLibreEditor architecture — high confidence from published development notes:** the editor has player lists/profiles, filtering, position maps, export and save-generation invalidation. Its write path requires session identity, object identity, value validation, current-layout validation and read-back. Atlas reuses the product pattern of clear read-only state and evidence labels, not its writer or memory offsets.
- **FMRTE/Genie Scout — feature benchmark:** position and role-aware filtering, hidden attributes, configurable result views, compare and scouting profiles are valuable interaction references. Their proprietary role formulas are not imported.
- **StatsBomb-style analytics boundary:** FM save exports are not StatsBomb event feeds. FM Atlas can provide a similar analytical workflow (per-90 rates, cohorts, percentiles, scatter plots and value screening) only for fields actually present in the imported career. It must not fabricate xG, progressive carries, pressures or event-level measures when the save has not supplied them.

### Implementation in this batch

- Extended Performance vs Value candidate search with an optional numeric FM position-familiarity cohort filter. It reads the selected position's numeric value from the latest FMSAVE `positions` facts and requires the configured minimum (1–20).
- Added the selected FM position rating to the result grid, chart tooltip and CSV export so the analyst can see the actual rating used for cohort inclusion.
- Position/league cohort percentiles remain separately reported. The global performance and value percentiles are recalculated within the eligible population after minutes, sample-size, value and selected FM position-rating filters. Position/league visible filters are post-calculation display filters.
- Added a position-cohort SQLite integration test with players sharing the same textual position family but different numeric familiarity; only the player meeting the selected position threshold should enter the cohort.
- Applied Monolith Glass graphite/amber surfaces and matching input/button colours to the standalone Performance vs Value window.

### Limitations and validation

- The new position-rating cohort filter currently applies to the offline FMSAVE statistical dataset, because the season/match performance facts are sourced from imported save tables. It does not pretend that provisional live-memory position values have corresponding season-stat rows.
- A position threshold is a scouting cohort constraint, not a role suitability score. No role formula is inferred.
- Percentile values are descriptive ranks within the current eligible import, not cross-save or real-world league benchmarks. Small position/league cohorts can have no percentile (minimum cohort size remains three).
- The FM27 in-game Positions page still needs to be checked against the same imported player before live-memory position fields can be promoted beyond provisional.
- Windows build, UI visual/DPI review and the new SQLite test are pending the workflow triggered by these commits.

### Next steps

1. Extend the same source-aware Monolith Glass treatment to remaining custom dialogs, chart/empty/error states and high-contrast/focus states.
2. Add a dedicated position-rating witness report to capture in-game FM27 values beside FMSAVE and live-reader values for the same player, then resolve discrepancies with evidence.
3. Expand scouting profiles with reusable metric bundles, player comparison views and a clearly labelled value-gap shortlist.
4. Add only those performance fields actually present in the save/CSV. Keep event-level metrics and third-party proprietary formulas out of the catalogue unless an external source explicitly provides them.


### Scouting percentile comparison refinement — 14 November 2026

- Added a chart comparison selector for the overall eligible pool, the player's position cohort, or the player's league cohort. Position/league modes use the already calculated group percentile fields; rows without a sufficiently large cohort are omitted from that chart view rather than assigned a fabricated percentile.
- Added the selected FM position familiarity to the candidate model, grid, chart tooltip and CSV. When no FM position filter is active, the value remains explicitly unavailable.
- Added an integration assertion that an unrestricted scouting query returns the full three-player test pool and does not invent a selected position rating.
- This makes the chart closer to a cohort-first scouting workflow: choose a real numeric FM position-familiarity floor, then view overall, same-position or same-league performance relative to source-data peers.


### First Windows CI attempt and SQLite cleanup correction — 14 November 2026

- Windows restore and Release compilation passed.
- 83 of 84 xUnit tests passed. The new position-cohort query assertions completed; the test then failed while deleting its temporary SQLite file because Microsoft.Data.Sqlite retained a pooled connection on Windows.
- Root cause was test cleanup, not the scouting SQL: the exception occurred in the `finally` block at `File.Delete(databasePath)`.
- Corrected the test cleanup to call `SqliteConnection.ClearAllPools()` before deleting the temporary database and WAL/SHM files.
- A fresh Windows workflow is required to verify this correction; the failed run is not a green build and no artifact was produced.


### Final Windows verification — integrated Monolith Glass and scouting cohort milestone — 14 November 2026

- Workflow: https://github.com/lewisxatk/FM-Atlas/actions/runs/43593364372
- Verified source commit: `b45ca350ce4c05a07ab07434e0d3cc39c408f2fd`
- Result: success. All 12 workflow steps passed: adapter validation, supported fmsave verification, streaming bundle tests, .NET restore, Release build, full xUnit suite, self-contained Windows x64 publish and artifact upload.
- Full xUnit suite: 84 passed, 0 failed.
- The new position-cohort integration test passed with isolated unpooled SQLite connections. It verifies the selected numeric FM position threshold excludes players below the threshold, returns the source rating, and leaves the rating absent when no position code is selected.
- Artifact: `FM-Atlas-V8-win-x64`, ID `14112889881`, size 68,896,831 bytes; SHA-256 `39846fd69868c36fa557d1c859bba63f90ef32b863354e44ff293da55e2e019c`; expires 28 November 2026.
- The previous 83/84 failure was resolved by using isolated unpooled SQLite connections in the new test; no global pool clearing remains.
- Manual Windows DPI/visual acceptance, real FM27 in-game position witness comparison and actual user-PC acceptance remain open. The build passing does not prove the FM27 source values match the in-game player profile.


## Position-rating comparison and Wyscout/StatsBomb scouting architecture — continuation 14 November 2026

### Fresh research reviewed

- FMSuperScout current FM27/26 README and architecture: https://github.com/mavarobli/FMSuperScout
- FM Dossier current product workflows and limitations: https://fmdossier.dev/
- FM Dossier data-reading and read-only model: https://www.fmdossier.dev/how-it-works
- FMLibreEditor v0.5.1 release notes: https://github.com/tozmoz/FMLibreEditor/releases
- StatsBomb Open Data repository and data structure: https://github.com/hudl/open-data
- Wyscout data model and advanced player-stat fields: https://github.com/withqwerty/football-docs/blob/main/docs/wyscout/data-model.md
- StatsBomb-based multi-match analytics example: https://github.com/AlexandreMorel/Football-Analytics
- Football analytics metric definitions and reference formulas: https://github.com/withqwerty/football-docs

### Findings that affect the product design

- FMSuperScout documents a full loaded-career extraction of 60,000+ players/staff in seconds, with real CA/PA, hidden attributes, position familiarity, role ratings, instant local filtering and up to three-player comparison. Its documentation is explicit that estimated market values and asking prices are estimates, not exact FM values.
- FM Dossier provides a useful product pattern for separate Full, Scout and Moneyball lenses, every-role suitability, side-by-side comparison, recruitment suggestions and season-stat workflows. Its published FM27 caveats show why each feature needs a per-build support/coverage state rather than a blanket compatibility claim.
- FMLibreEditor v0.5.1 added a scouting workflow with fit, estimated fee/wage, likely-to-join guidance, squad depth and release suggestions. Its save-change behaviour explicitly invalidates stale state until reload.
- Wyscout's advanced-stat model includes a large set of pre-aggregated player metrics: xG-related fields, key/progressive passes, progressive runs, final-third passes, smart/through passes, crosses, dribbles, defensive/aerial duels, interceptions, recoveries, counterpressing recoveries, goalkeeper actions and accuracy splits.
- StatsBomb Open Data is a different data shape: match event JSON with events, lineups and selected 360 frames. Event-level measures such as shot xG, pressure, carries, dribbles and pass locations require those event feeds, not merely a normal FM save player table.
- Multi-match scouting should aggregate player-match or player-season rows, normalize counting metrics per 90 only when valid minutes exist, preserve sample sizes, and compare players within defensible position/league cohorts. A similarity score is a normalized metric-vector distance, not a claim of identical role or tactical function.

### FM Atlas implementation in this continuation

- Player Comparison now adds a dedicated Position suitability section for all 15 canonical FM position slots. Each compared player shows the imported numeric rating as n/20; missing values remain an em dash.
- The comparison uses the same canonical mapping and 1–20 source-backed ratings as the existing Position Suitability and Advanced Search work. It does not infer a position rating from a player's primary position, natural/accomplished classification or role estimate.
- Existing player attributes, radar, profile, CA/PA, value, shortlist and Advanced Search paths remain intact.
- Change committed to rebuild/v8-full-product-rebuild as 58eef84f4ddadae702b12ffbbe05975ead3b6bca.

### Wyscout/StatsBomb-style engine: implementation boundary

FM Atlas should deliver the scouting workflow and metric analysis experience, while remaining honest about its data source:

1. FM-native career layer: current ability/potential, attributes, position familiarity, role fit, contract, wages, value, squad context and whatever player-season/player-match statistics the imported save actually contains.
2. Performance layer: source-aware metric catalogue, per-90 calculations, minimum minutes/sample controls, position and league cohorts, percentiles, trend snapshots, player comparison, similarity and value-gap screening.
3. External event-data layer (future adapter): only when the user supplies or connects a lawful Wyscout/StatsBomb-compatible dataset. Keep provider, competition, season, match, event coverage and licence attribution with every imported record.
4. Unavailable-data handling: do not display xG, xA, progressive carries, pressures, PPDA, shot maps or event-derived xT unless the relevant source fields/events are present. Missing event coverage must stay missing, not become zero.
5. Model governance: document each metric's definition/version, numerator, denominator, time basis, sample floor, direction (higher/lower better), source, and whether it is source-provided or Atlas-derived. Similarity and value models must be labelled as Atlas estimates and export their method/coverage.

The current Performance vs Value window already supports discovered save metrics, per-90, minimum minutes and sample size, overall/position/league percentiles, numeric FM position-rating cohort filtering, and CSV export. This continuation extends the side-by-side player comparison with source-backed position ratings. It does not claim that the complete Wyscout/StatsBomb event-data engine is finished; the external provider adapter, metric catalogue governance, season trend UI and event-data ingestion remain future work.

### Verification and next gates

- A fresh Windows workflow must validate commit 58eef84f4ddadae702b12ffbbe05975ead3b6bca; the previous 84/84 result predates this comparison change.
- Test comparison rows for 2–4 players with partial position coverage, alias normalization and no fabricated values.
- Check comparison window layout at 100%, 125% and 150% DPI.
- Continue the Wyscout/StatsBomb adapter as an opt-in import path, separate from the FM live-memory reader. Do not scrape authenticated provider services or copy proprietary models.
