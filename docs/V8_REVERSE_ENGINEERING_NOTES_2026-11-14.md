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
