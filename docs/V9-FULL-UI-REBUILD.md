# FM Atlas — V9 Full Interface Rebuild

**Working line:** `rebuild/v8-full-product-rebuild` (continues the preserved V8 implementation and PR #4)
**Priority:** The complete application UI and interaction rebuild comes before the remaining feature expansion.
**Platform:** Windows 10/11 desktop · FM27 first · FM26 only where source evidence supports it.

## Mission

Rebuild FM Atlas as a coherent football-data workstation inspired by the visual atmosphere of Watched and the practical record workflow of FMRTE. This is not a palette-only reskin. Replace the legacy page composition, navigation behaviour and player-inspector interaction with a shared application shell, searchable record tabs, purpose-built workspaces and consistent reusable components.

Preserve the R7.2.0/V8 data, search, save-import, live-read and analytics foundations. Build the new UI over those services; do not delete or rewrite working data paths merely to make the interface look new.

## Visual direction — Watched atmosphere × FMRTE workflow

### Watched-inspired visual language

- Deep graphite / near-black canvas, with restrained warm signal-orange highlights.
- Precise telemetry micro-labels, timestamps, IDs, data-source indicators and aligned numeric readouts.
- A strong editorial hierarchy: condensed display headings, clean readable UI text, and monospaced numeric/technical values.
- High information density with generous grouping and deliberate empty space; avoid generic piles of rounded dashboard cards.
- Purposeful visualisations that reveal structure and relationships rather than decorative animation.
- Subtle glass/translucent treatment only for navigation, toolbars, tabs and floating actions. Keep tables, forms, charts and player records opaque and readable.

### FMRTE-inspired workflow

Use the current FMRTE 27 interaction patterns as functional references, not copied artwork:

- Quick Search by name or UID, with previews/history and keyboard-first use.
- Advanced Search with entity types, grouped categories, ranges, condition logic and reusable presets.
- Persistent main record tabs for players, staff, clubs and other supported entities.
- A full record page with its own clear section navigation, rather than a narrow profile inspector.
- Configurable result columns, sorting, selection and contextual row actions.
- Clear unsaved/edit state, source labels and edit history. Atlas must keep its own live-write safety gates.

## Readability and accessibility rules

- Normal body text must remain comfortably readable at common desktop sizes; avoid tiny 8–9 px labels for essential information.
- Important values use high contrast; muted text is reserved for non-critical supporting details.
- Keep numeric columns aligned and use tabular/monospaced figures.
- Tooltips supplement visible labels; they never replace accessible names.
- All key actions support keyboard focus and shortcuts.
- Support 100%, 125% and 150% display scaling, minimum laptop resolutions and resizable side rails.
- Honour Windows high-contrast and Reduce Motion preferences. No constant decorative motion.
- Do not use blur behind data grids or dense record content.

## Data truth rules in every screen

- Only show fields extracted from FM27 save data, current game memory, or the installed FM database where a validated source exists.
- Mark each value as **Save**, **Live**, **Atlas calculated**, **Provisional**, **Unavailable** or **Unknown** as appropriate.
- Do not invent FM statistics, hidden attributes, CA/PA, role ratings, positions, contractual values or competition data.
- Keep player attributes/CA/PA separate from observed season and match statistics.
- Calculations such as role-fit, percentiles or value scores must expose their inputs, cohort, sample size and limitations.
- A missing value is not zero.

## New information architecture

1. **Home / Career Overview** — career identity, connection status, data coverage, active jobs and recent work.
2. **Players** — full-width player pool, type-ahead search, position/CA/PA quick filters, configurable grid, selection and multi-select.
3. **Advanced Search** — full workspace with Player / Staff / Club entity types, searchable field library, category navigation, AND/OR groups, ranges, include/exclude, saved profiles and visible result count.
4. **Record tabs** — every selected player/staff/club opens as a full main tab, with close/reopen behaviour and preserved search tab.
5. **Player Profile** — identity header and section navigation for Overview, Attributes, Positions & Roles, Contract, Performance, Development, History and Source/Evidence. Sections display only supported values.
6. **Staff and Clubs** — full searchable directories and tabbed records with linked people, teams and career context.
7. **Moneyball / Performance** — actual save statistics, per-90 where minutes are present, cohort percentiles, comparison and value context.
8. **Squad / Tactics** — squad depth, position/role suitability, formation and tactical context, clearly separating game values from Atlas estimates.
9. **Editor / History** — local drafts and live editing remain distinct; unsupported or uncertified live writes stay blocked.
10. **Data Lab** — import, mapping evidence, build profiles, source coverage, validation, diagnostics and logs.

## Shared design system and shell

Build a reusable component system before broad page migration:

- App shell, compact grouped navigation and active workspace state.
- Global command/search overlay and top action toolbar.
- Persistent workspace/record tab strip with context menus.
- Page header, breadcrumbs, status bar and contextual actions.
- Standard panel, section header, field row, metric tile and evidence badge.
- Search field, range input, multi-select, condition builder and saved-search selector.
- Data grid with column picker, stable sort, paging, virtualisation, multi-select and row actions.
- Tabs, side rail, popover, modal, tooltip, empty, loading, error and confirmation states.
- Accessible focus, hover, pressed, disabled and busy states.

## Delivery sequence

### Stage A — full UI foundation (current priority)

- [x] Preserve the V8 source and continue on its dedicated branch.
- [x] Introduce the full-width player-pool / record-tab composition: the pool remains a tab and a selected player opens in a sibling full-workspace tab.
- [x] Verify the full-width player pool/profile-tab composition and initial shared design system with Windows Release build, career-import integration tests and self-contained x64 publish (Actions run #746, commit `8d9e27f91cbf4d8238d7a66182a862b94e86651b`).
- [x] Implement the new shared application shell source: grouped navigation, persistent Quick Search, contextual workspace header, full-height page host and compact career/import status footer. Latest shell refinements still require a fresh Windows build and visual acceptance.
- [~] Full-width player pool and player record tabs are in source; common page frames, fully reusable record tabs for Staff/Clubs, column controls and the remaining record section rail still need migration.
- [ ] Complete the Player Profile full-page sections and responsive header.
- [ ] Rebuild Advanced Search as a first-class workspace using the existing query/index services.
- [ ] Migrate Staff and Club directories/profiles to the same record-tab model.
- [ ] Migrate Home, Moneyball, Squad, Tactics, Editor, History and Data Lab to the common component system.
- [ ] Visually accept all primary screens at 100%, 125% and 150% DPI, dark/light modes and common laptop/desktop resolutions.

### Stage B — data coverage and reverse engineering

- [ ] Audit every FM27 field visible/editable in the live game and save files against an explicit field registry.
- [ ] Cross-check CA/PA, all standard attributes, personality, positions, staff/club fields and career context against game UI witnesses and independent save-parser data where comparable.
- [ ] Audit player, club, staff, competition and season/match statistic ingestion. Map every displayed statistic to an in-game or save-reader field.
- [ ] Research FMRTE, FMSuperScout, FMLibreEditor, fm-editor, fmsave and FM View Exporter workflows and layouts; do not copy GPL code into Atlas without a licensing decision.
- [ ] Separate verified, provisional, estimated, unavailable and unsupported fields by exact game build/storefront.
- [ ] Test search, profiles, performance and all linked pages against large and real FM27 career datasets.

### Stage C — editing and parity

- [ ] Build a complete local edit model with field validation, preview, change highlighting and history.
- [ ] Extend live editing only for fields with exact build-specific mapping evidence, witness comparisons, preflight, read-back and rollback certification.
- [ ] Keep all other fields blocked and state why in the UI.
- [ ] Test Apply/Undo only on a disposable offline career copy.

## Implementation checkpoint — 15 November 2026

Branch: `rebuild/v8-full-product-rebuild`

- Added `MainWindow.PlayerWorkspace.Rebuild.cs` and routed Players/Recruitment to it.
- The player pool occupies the full workspace; selecting a player opens a sibling full-size record tab. The pool remains open and protected from Ctrl+W.
- Search text, position, CA/PA criteria, result count/status and paging are retained across workspace navigation.
- Existing database search, source/confidence label, pagination, shortlist/report actions and profile data assembly are reused.
- Added `MonolithDesignSystem.cs` with semantic dark/light tokens, Bahnschrift display text, Segoe UI body text, Consolas telemetry text, theme-aware panels/buttons/inputs and editorial accents.
- Added `MainWindow.Shell.Rebuild.cs`: persistent Quick Search, contextual page header, grouped navigation host, full-height workspace and compact career/import status footer. Players/Recruitment remain on the shared existing navigation/service paths.
- Added explicit pool-tab/error handling so closing a player profile does not remove the persistent pool and a profile-load failure does not add a duplicate welcome tab.
- Successful Windows verification for the earlier full-width player screen/design-system checkpoint: Actions run #746, commit `8d9e27f91cbf4d8238d7a66182a862b94e86651b`. The .NET Release build, career-import integration tests, self-contained x64 publish and artifact upload passed.
- Subsequent shell, search-state and light-theme refinements are committed after that verified checkpoint. They require a fresh Windows run and visual review before being marked verified.
- No real Windows display-scale or FM27/Game Pass acceptance is claimed at this point.

## Research references

- Watched: https://www.watched.earth/
- Watched design review: https://websitedesignawards.com/site/watched
- FMRTE 27 interface tour: https://docs.fmrte.com/getting-started/interface-tour/
- FMRTE 27 search guide: https://docs.fmrte.com/scouting/search/
- FMRTE 27 feature overview: https://www.fmrte.com/
- FMSuperScout: https://github.com/mavarobli/FMSuperScout
- FMLibreEditor: https://github.com/tozmoz/FMLibreEditor
- fm-editor: https://github.com/barak14/fm-editor
- fmsave: https://github.com/rhiever/fmsave

