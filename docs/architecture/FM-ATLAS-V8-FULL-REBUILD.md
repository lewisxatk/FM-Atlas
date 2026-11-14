# FM Atlas v8 — Full Product Rebuild

Status: approved development direction; implementation work required.
Base: r7.2.0 release-integration branch. Target: Windows desktop, FM27 first; preserve FM26 where evidence supports it.

## Non-negotiable direction

This is a full product and interaction rebuild, not a theme refresh. Do not reuse the current sidebar/header/player-workspace composition as the target. Do not use Aurora Frost. Establish a new visual identity, information architecture, navigation model, reusable component system and performance architecture.

Target capabilities:
- FMRTE-style record discovery, advanced filtering, tabs, quick search and record navigation.
- Genie Scout-style player pool, positional/role ratings, saved searches and potential analysis.
- Moneyball-style performance-versus-value discovery using actual season/match statistics, with sample-size and source coverage shown.
- FM Atlas differentiators: provenance-aware reverse-engineered data, account/career context, explainable recruitment, squad planning and safe editing.

## Evidence from the current r7.2.0 build

### Data provenance is currently easy to misunderstand

The supplied 2026-11-13 diagnostics report shows:
- Live identity rows: 40,292 players and 28,497 clubs.
- Latest save snapshot: 0 players, 0 clubs, 0 staff.
- Local player attribute facts: 0; live facts: 345.
- FM27 PID 2924 was detected; game_plugin.dll was readable; read-only probe passed; exact gamepass-0.9.2435.0 module profile matched.
- The live extractor found 40,286 player slots with attribute coverage and 28,497 clubs. A subsequent live import attempt failed with SQLite Error 5: database is locked.
- The save bridge log later records Starting read-only FM career save export/import at 23:47:23 UTC, but the supplied diagnostics contains no completion or failure entry after that point. The save import is unconfirmed, not successful.

These are different datasets and must never be added together or represented as equivalent complete profiles:
1. Game-world live identities — identity rows discovered in running-game memory; not necessarily full profiles, attributes, contracts or validated relationships.
2. Career snapshot records — records read from the selected save file, scoped to that save and snapshot ID.
3. Validated facts — field-level facts with explicit evidence/validation state.
4. Local Atlas data and edits — separate local records and changes.

Every row and metric must carry a visible source label and coverage state. Do not silently merge/deduplicate sources using name + club + age as durable identity. Use source-native UIDs and cross-source identifiers only where documented and validated.

### Current UI/navigation problems confirmed in source

- MainWindow.xaml.cs is approximately 163 KB and contains the application shell, navigation, data orchestration and many workspace builders in one code-behind class.
- Navigation swaps in a static Preparing data and layout pane, awaits page construction, and disables all sidebar buttons while navigation is busy. Users get no useful progress, cancellation or responsive navigation.
- PlayerWorkspaceAsync builds the page and then awaits every previously opened player profile sequentially before returning. Profile loading itself fetches multiple data groups. This is a plausible source of long workspace activation and needs profiling/fixing.
- The Players page combines a basic name search, a short hard-coded position list, position ribbon, results grid and profile panel.
- Advanced Search launches GlobalSearchWindow, not a dedicated FMRTE-style filter-builder workspace.
- GlobalSearchWindow uses fixed text boxes for a limited set of fields (age, CA/PA, nation, league, feet, one selected attribute, preferred foot and home-grown). It requires a two-character text query, loads up to 350 player results plus live results and staff/clubs in one flow. It is not a general multi-condition query builder with category navigation, arbitrary attributes, saved profiles or a comprehensive column model.
- The save adapter calls a large list of fmsave table readers sequentially, materializes them with to_dicts()/list(), converts records to plain Python objects, then emits a large JSON snapshot. The database importer parses that JSON and writes records/facts in a single SQLite transaction with per-player and per-fact statements. This needs profiling and a streaming/batched pipeline with progress, cancellation, resumability and atomic snapshot publication.
- FmsaveBridgeService has a 30-minute timeout, but the UI has no stage/progress updates and its log only records start/completion/failure. The report shows the start but no terminal event.

## Product research translated into requirements

### FMRTE 27 patterns worth adopting (not copying visually)
- Quick Search by name or ID, accent-insensitive matching, previews/history and keyboard-first navigation.
- Search types for Players, Staff and Clubs.
- Advanced Search with categories on the left, logically grouped filters, min/max ranges, search results in tabs and reusable presets.
- Record tabs, compact entity headers, closable tabs, right-click actions and persistent grid column order/width/sort.
- Player season stats with league/position comparisons and percentile presentation.
- Edit history, changed-field indicators, undo and explicit unsaved-change prompts.

### Genie Scout patterns worth adopting
- One fast player pool with type-ahead narrowing and a filter ribbon.
- Current and potential rating/ability columns and positional/role ratings.
- Search by all available attributes, hidden attributes only when actually decoded, personality, position ratings, contracts, wage and value.
- Saved searches and shortlists.
- Similar-player discovery and projected development only where a transparent, evidence-backed model can be built.

### Moneyball / FMDataLab patterns worth adopting
- Separate ability/role potential from observed performance: attributes describe capability; season and match statistics describe what a player has actually done.
- Role/position-aware statistical comparisons.
- Performance-versus-market-value discovery, percentile/scatter views and explainable metric weighting.
- Sample size, competition strength, minutes played, data freshness and missing coverage must be visible. Do not rank players from sparse or incomparable samples as if certainty were high.

Research references:
- FMRTE 27: https://www.fmrte.com/
- FMRTE Search and Advanced Search: https://docs.fmrte.com/scouting/search/
- FMRTE interface tour: https://docs.fmrte.com/getting-started/interface-tour/
- Genie Scout features: https://geniescout.com/features/
- FMDataLab Moneyball: https://www.fmdatalab.com/
- fmsave source and FM27 reader coverage: https://github.com/rhiever/fmsave and https://rhiever.github.io/fmsave/

## New information architecture

1. Home — career status, data coverage, import jobs, watchlist, recruitment insights and actionable alerts.
2. Players — full player pool, type-ahead search, table/compact/card views, configurable columns, multi-select and contextual actions.
3. Advanced Search — dedicated full workspace with entity type, searchable filter library, category rail, condition builder, AND/OR groups, ranges, include/exclude, saved searches and live result count.
4. Player Profile — tabbed record with Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source/Evidence.
5. Squads & Clubs — club directory, squad depth, positional coverage, age/payroll structure and squad planner.
6. Recruitment / Moneyball — role templates, performance-versus-value, per-90 and percentile metrics, similar players, shortlist pipelines and explainable recommendations.
7. Staff — staff search, role fit and backroom gaps.
8. Tactics & Roles — tactic-aware role needs, player fit and squad-level comparisons.
9. Editor — separate local draft vs live-game edit modes, field support matrix, preview, audit, undo/rollback and explicit safety gates.
10. Data Lab — import manager, build/layout registry, mapping/evidence browser, validation reports and diagnostics.

Quick Search is a command/search overlay, not a permanently occupying global search text box. Navigation should be compact, collapsible and context-aware; users must be able to switch workspace while background jobs continue.

## New visual identity

Create an original visual system from first principles. Do not use Aurora Frost or recolour the existing UI. Avoid generic admin-dashboard cards and the current lime-on-charcoal style. Use a confident editorial/scouting-workstation aesthetic: clear hierarchy, strong typography, high information density without clutter, restrained surfaces, high-quality player imagery/logos where installed, consistent iconography, deliberate spacing, fast keyboard interactions and carefully designed empty/loading/error states. Produce separate design system and screen prototypes before porting the full feature set.

## Performance and architecture requirements

- Separate UI, application/use-case, domain, data access and game-integration layers.
- Move away from the monolithic MainWindow code-behind to MVVM or equivalent testable view models and services.
- Background jobs must never block the WPF dispatcher. Use cancellation tokens, progress events, bounded concurrency and clear job state.
- Navigation must be instant; show a skeleton only for the target pane, keep navigation usable, and cancel stale page queries.
- Use SQLite WAL deliberately, busy timeout, a single-writer queue or coordinated write service, prepared commands, batch inserts, indexes and bounded read connections. Do not hide lock errors or weaken transaction safety.
- Imports: stage output, validate it, stream/batch into a new snapshot, commit/publish only after successful validation. Preserve previous snapshots. Record per-table counts, elapsed time, warnings, errors and coverage.
- Avoid eagerly materializing every table into one large JSON object. Evaluate table-specific JSONL/streaming output or direct local IPC. The save reader remains read-only.
- Search must use indexed columns and attribute fact tables, support paging and cancellation, and avoid querying all entity types for every search unless requested.
- Persist grid columns, sort, filters, tabs and workspace state.
- Add benchmark fixtures for 40k+ players, 28k+ clubs, attribute fact rows and large season-stat tables. Track startup, navigation, query, profile-open and import timings.

## Reverse-engineering and mapping work

- Preserve exact module fingerprint matching and read-only memory scanning.
- Keep candidate offsets, inherited offsets, field names and UI-witnessed fields separate.
- Build a mapping registry per game build/storefront with field type, source offset/schema, decoder, expected range, evidence, witness status, confidence and limitations.
- Do not promote an offset merely because a value looks plausible. Cross-check against the in-game UI and independent save-reader values where comparable.
- Use save-native UIDs for within-save joins; use persistent unique IDs for cross-save identity only as documented by fmsave. Do not infer durable identity from name/club/age.
- Explore current fmsave readers and release notes for every supported table, but treat FM27 field meanings as unconfirmed where the reader says so.
- Keep live memory data, save parser data and editor write support as separate capability matrices.

## Definition of done for the next integrated development stage

1. A full new application shell and navigation, not a theme swap.
2. Player pool with real indexed search, configurable grid and responsive paging.
3. Dedicated Advanced Search with arbitrary available-field conditions, saved filters and accurate coverage indicators.
4. Player profiles that clearly separate save data, live identities, provisional facts and verified facts.
5. Import pipeline with visible stage progress, cancellation, bounded memory, safe transaction publication, retryable failure and detailed diagnostics.
6. Background jobs never freeze navigation or make the application appear hung.
7. A performance benchmark report against the current build.
8. Unit/integration tests for mapping, identity joins, import counts, search conditions, cancellation and SQLite contention.
9. A Windows Release build and real PC test pass before calling it complete.

## Immediate investigation items

- Diagnose the current import process: the log says it started but has no completion/failure record. The player workspace loading screen is not evidence that the import succeeded.
- Profile PlayerWorkspaceAsync and the sequential open-profile restoration path.
- Inspect fmsave 0.12.x reader coverage and the save validation JSON before trusting the current save import.
- Explain the 40,292/28,497 live identity counts separately from the zero save snapshot counts everywhere.
- Do not ask the user to repeatedly rebuild or test until a coherent integrated milestone is ready.