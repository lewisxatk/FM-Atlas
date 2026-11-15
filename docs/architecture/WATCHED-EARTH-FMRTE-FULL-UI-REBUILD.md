# FM Atlas — Watched × FMRTE full UI rebuild brief

**Working branch:** `rebuild/watched-earth-fmrte-ui`  
**Base:** `rebuild/v8-full-product-rebuild` (R7.2.0 application source + V8 work)  
**Purpose:** replace the legacy workspace composition with a coherent, accessible desktop product. This is not a theme-only pass.

## 1. Reference research and what to borrow

### Watched.earth — visual and interaction reference

The live site requires JavaScript and WebGL, so its interactive scene cannot be fully inspected in a text-only browser. The research review and public project README describe the experience as a dark, high-contrast orbital control surface: an expressive 3D globe/visual focus, a direct search entry point, dense telemetry and status, an event timeline, precise orange reticles, and replayable detail. Its visual language mixes bold condensed display typography with restrained monospaced technical labels and an italic editorial accent. The design is striking because there is one clear focal visual and the supporting data is arranged around it, not because every card is decorated.

Sources:
- https://www.watched.earth/
- https://unique.rzen.dev/reviews/watched-earth/
- https://websitedesignawards.com/site/watched
- https://github.com/dev-Armaan/satellites

Apply the visual principles, not satellite imagery or copied branding:
- deep ink / graphite canvas with soft, deliberate surface separation;
- a warm orange signal colour for selection, focus and active states;
- restrained cool cyan/ice accents for neutral system/data states;
- large, clear condensed section titles; quiet monospaced labels for IDs, values, dates and telemetry; limited italic editorial accents;
- thin technical rules, small status marks, precise charts and data-linked visualisations;
- one strong content focal area, with supporting filters, results and evidence panels;
- subtle transitions only where they explain navigation or data changes.

Watched's documented visual polish should not be copied at the expense of usability. All app controls must have semantic names, keyboard operation, focus states, sensible text scaling and strong contrast. Do not make labels hover-only or encode a status by colour alone.

### FMRTE — interaction and information architecture reference

Use the current FMRTE 27 interaction model as the functional benchmark:
- Quick Search by player/staff/club name or ID.
- Dedicated Advanced Search, with entity type, category navigation, multi-condition filters, min/max bounds, reusable presets and sortable result grids.
- Opening a record creates a full record tab, rather than squeezing the record into a narrow inspector pane.
- Player records expose information, attributes, ability, personal details, fitness/morale, contract and performance-stat sections; staff appears for player-coaches and other sections are conditional on data availability.
- The Stats section is read-only and compares season output against a chosen competition/position cohort; player lists can expose selected stats as columns.
- Mass edit uses preview and an explicit save/apply action; edit history and undo are first-class workflows.

Sources:
- https://docs.fmrte.com/getting-started/interface-tour/
- https://docs.fmrte.com/scouting/search/
- https://docs.fmrte.com/players/overview/
- https://docs.fmrte.com/players/statistics/
- https://docs.fmrte.com/players/ability/
- https://docs.fmrte.com/help/settings/

### Other projects examined

**FMSuperScout** (MIT): study its separated read-only scanner/core and local scouting UI, rapid player pool, stacked filters, CA/PA and attribute filtering, snapshots/development tracking, squad analysis and compare workflow. Do not copy its code or assert its performance figures as Atlas results. Its README and release notes describe field checks against FM27; Atlas must independently witness every field on supported builds.

**FMLibreEditor** (GPL-3.0): use its documented safety patterns as research only—explicit build compatibility, read-only fallback on unknown versions, save-generation invalidation, mass-edit preview and undo. Do not copy GPL source into Atlas without a separate licensing decision.

**barak14/fm-editor** (GPL-3.0): study version-specific layouts, field witnesses, reader/writer abstraction, entity discovery and undo architecture. Unverified offsets remain unverified; do not copy implementation.

**fmsave** (MIT): use as the offline read-only save parser and source of structured career/season/match records where its version coverage supports them. Preserve unknown/missing values and reader warnings.

Sources:
- https://github.com/mavarobli/FMSuperScout
- https://github.com/tozmoz/FMLibreEditor
- https://github.com/barak14/fm-editor
- https://github.com/rhiever/fmsave

## 2. Design system — Watched × FMRTE, made for FM Atlas

The target is an editorial, technical scouting instrument: atmospheric like Watched, dense and immediately usable like FMRTE.

### Visual tokens

- **Canvas:** near-black ink / deep graphite, with subtle depth rather than flat pure black.
- **Surfaces:** raised graphite panels; data tables use opaque, stable surfaces. Glass/translucency is limited to navigation, toolbar, command palette and floating actions.
- **Primary signal:** burnt orange / amber for current selection, active tab, focus and key action.
- **Secondary signal:** cool cyan for neutral live-system and information states; green/red only for grounded positive/negative status.
- **Typography:** use locally installed, Windows-safe font fallbacks. Condensed bold display face for workspace titles; clean sans-serif for body and forms; monospaced face for UIDs, CA/PA, dates, source IDs and numeric telemetry; sparing italic serif accent. Do not bundle or redistribute font files without licence review.
- **Density:** compact enough for thousands of records, but never tiny. Body copy and filter labels must remain readable at normal desktop scaling. Dense tables must support row height, font scaling, column sizing and clear focus/selection.
- **Motion:** subtle, short and purposeful; honour Windows animation preferences and provide Reduce Motion.
- **Accessibility:** labels and tooltips support both pointer and keyboard; visible focus rings; high-contrast mode; no colour-only statuses; no hover-only essential information.

### Shell and navigation

- Persistent left navigation grouped by Home, Scouting, Analysis, Editing and Data & Tools. Collapsible to an icon rail, but labels remain accessible.
- A compact command/action toolbar at the top with Quick Search, Open/Refresh career, active task centre, compare, shortlist, undo/history, settings and context-sensitive actions.
- Ctrl+K opens a keyboard-first command/search palette. It searches all currently supported entity types and navigates to the chosen record.
- Main content is a tabbed workspace. Search workspaces remain open as tabs; each opened player/staff/club record gets its own closable tab. Selecting a record tab gives it the full central workspace width.
- Background imports and scans must not disable navigation. Jobs show stage, elapsed time, progress, cancellation and final outcome.

## 3. Player workspace — required behaviour

### Player finder

- Wide, primary search results workspace, not a persistent split-screen inspector.
- Fast search across name, UID, club and nation.
- Position picker covering the game's position catalogue, with a pitch-style selector where practical.
- Quick filters for age, nationality, club, league, CA and PA; range validation and a clear/reset action.
- Advanced Search opens the complete filter builder: field categories, search-as-you-type field library, supported operators, min/max ranges, ALL/ANY logic, nested groups, include/exclude, saved searches and live result counts.
- Results grid with selectable FM-backed columns. Default columns include player, club, age, position, league, CA, PA, nation, home-grown flags and source/coverage.
- Users can add/remove columns, sort on multiple fields, resize/reorder columns, save layouts, page/virtualise large result sets and multi-select for shortlist/compare/bulk edit.
- Every displayed field states where it came from where that matters: current live session, imported career save, local Atlas value, or calculated analysis.

### Opening a player

Selecting or double-clicking a player opens a full workspace record tab, not a narrow right-hand card. Keep the previous search and result page available as a separate tab. Multiple player tabs can stay open and be switched/closed independently. The application must remember the active tab and, where safe, restore open record tabs on restart.

### Player record tabs

- **Overview:** identity, club, nation, age, position, preferred foot, current status, CA/PA, contract summary, value/wage where present, fitness/morale where present, source and confidence.
- **Attributes:** all decoded FM attributes, grouped using the game's own groups (Technical, Mental, Physical, Goalkeeping, Set Pieces as supported); show missing/unreadable as unavailable, never zero. Personality and hidden attributes are distinct groups and only appear when actually decoded and supported by the current source/build.
- **Positions & Roles:** FM positions and suitability values that are actually present in the source. Atlas-derived role fit must be labelled as an Atlas estimate and expose its attributes/weights/coverage. Never label it as FM's internal role rating.
- **Contract:** dates, wage, clauses, status, squad registration and other contract fields only where present in the loaded data.
- **Performance / Stats:** all season, career and match statistics actually available from the imported FM data. Show competition, season/period, minutes, raw values and units. Per-90, percentile and composite metrics are visibly labelled **Atlas calculated**, with formula, cohort, sample size and minimum minutes visible. Do not invent xG, progressive carries or any other stat that the save/export does not contain.
- **Development:** dated CA/PA and attribute history only when at least two comparable snapshots exist. No fabricated trend line or projection.
- **History:** previous clubs, transfers, loans, international and career timeline only where sourced.
- **Source & Evidence:** source ID, originating snapshot/build, timestamp, field coverage, validation state and warnings.

Tabs are conditional on the data that actually exists. A blank or unavailable section should explain what source is missing rather than showing fabricated placeholders as if they were facts.

## 4. Club and staff records

Use the same full-width record-tab model:
- Club overview, facilities, finances, stadium, competitions, squad, staff and history where available.
- Staff overview, current/preferred roles, attributes, coaching ratings, contracts and history where available.
- Club and staff advanced search should use the same filter-library and grid-column system as Players.
- Squad views should link to player record tabs, staff links to staff record tabs, competition and nation references to their own records where data permits.

## 5. Data truth and statistic contract

FM Atlas must show Football Manager data—not unrelated external football statistics presented as if they exist in the save.

Every field is classified as one of:
1. **FM source value** — read directly from the supported game process, save parser, exported game view or game database. Display the source and compatibility scope.
2. **Atlas calculated** — derived only from source-backed FM values, with the formula, units, cohort and sample count available.
3. **Atlas estimate** — a modelled measure such as role fit; show weights, coverage and uncertainty.
4. **Unavailable / unverified** — not decoded, not present in the source, or not witnessed for this game build. Do not display as zero and do not silently substitute a different metric.

The metric catalogue must be discovered from actual imported schemas and source rows, not a hard-coded list of football metrics. Include raw totals, rates, minutes, period, competition and player context when present. Preserve unknown fields for future compatibility. Where a field changes meaning between FM versions, keep separate version-specific mappings.

## 6. Editor scope and safe write contract

The requirement is broad editability, but implementation is evidence-bound:
- Build an editor capability matrix for each entity type, field, game version and storefront.
- Expose all fields for which the active build has a validated type, offset/schema, decoder, range, identity relationship and verified write/read-back path.
- Clearly separate Local Atlas Draft, offline save editing (only if a validated writer exists), and Live Game Editing. Never label a local-only database change as a live save edit.
- Provide field search, categories, bulk selection, mass-edit preview, validation, changed-field indicators, diff review, apply result, audit history, undo and rollback.
- A live field stays read-only until exact process/build/session identity, field witness, expected-old-value preflight, bounds, write, read-back and rollback have all passed against a disposable offline career.
- Unknown builds and uncertain fields fail closed. Never infer offsets or claim all fields are editable simply because they appear in the UI.
- Preserve existing V8 hard block on live Apply/Undo until the certification gate is independently satisfied.

## 7. Architecture rebuild

The current implementation still concentrates the shell, navigation, page construction and orchestration in a very large WPF `MainWindow.xaml.cs`. That is a confirmed maintainability and change-risk issue. Replace this incrementally with:

- **Shell / navigation:** app shell, workspace host, command palette, global record tabs, navigation state.
- **Design system:** theme tokens, buttons, filters, data grids, tabs, tooltips, charts, status badges and accessible empty/loading/error states.
- **Views:** Home, Players, Advanced Search, Player Record, Club Record, Staff Record, Moneyball, Squad Planner, Tactics, Editor, Data Lab and Diagnostics.
- **ViewModels / application services:** search orchestration, player profile composition, record tab lifetime, column preferences, job orchestration and editing workflows.
- **Domain:** player, staff, club, competition, nation, attribute, contract, statistic and source-evidence types.
- **Data layer:** FM save adapters, live read adapters, indexed SQLite stores, mapping registry and snapshot publisher.
- **Game integration:** version/storefront profiles, process/session detection, read-only scanners and certified writer adapter.
- **Infrastructure:** settings, logs, diagnostics, caching, export, backups and migrations.

Do not attempt a blind all-at-once rewrite of the 262 KB code-behind. Move one workspace at a time behind stable services, keep current feature behaviour, and compare each replacement against regression fixtures.

## 8. Delivery sequence and acceptance gates

1. **Shell and navigation:** typography, tokens, responsive side rail, toolbar, command palette, global workspace tabs, keyboard/focus/accessibility.
2. **Player search:** full-width result grid, complete column chooser, search/filter builder, saved layouts and state restoration.
3. **Full player record tabs:** migrate existing profile data loader; make record tabs full-window and link to/from search.
4. **Club and staff record tabs:** reuse the same record framework.
5. **Stats and Moneyball:** dynamic metric catalogue, period-aware source data, clear raw-vs-derived labels, role/league cohorts and sample guards.
6. **Editing:** complete local draft and field capability matrix; only then certify individual live writes. Preserve live write hard lock until acceptance.
7. **Squad / tactical / recruitment:** linked records, formation, role fit, comparisons and recruitment pipeline using available FM data.
8. **Data Lab and diagnostics:** mapping/evidence browser, source coverage, import jobs, traceable failures.
9. **Release validation:** Windows 10/11 Release build, full automated tests, UI visual/DPI review, real FM27 Steam and Game Pass checks where supported, copied-save acceptance and package checksum.

**Definition of done:** no feature is called complete merely because source code exists. Build, tests, UI acceptance and the relevant real-game/source witness must be recorded separately.

## 9. Immediate code-level issue identified

In the V8 source branch, `PlayerWorkspaceAsync` creates a side-by-side result grid and profile tab panel. Selecting a row only calls `ShowPlayerProfileAsync`; the profile does not automatically take over the content area. The existing `ToggleProfileExpanded(true)` already changes the layout to a full-width profile but leaves the search header visible. The next implementation unit makes selection enter full-width profile mode automatically and hides the search panel until the user returns to the player finder. Search state and open record tabs remain in the existing data structures.

This is the first safe UI behaviour migration while the broader global record-tab architecture is implemented in subsequent units.
