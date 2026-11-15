# FM Atlas — Product Recovery Research & Rebuild Specification

**Status:** Research baseline; no application feature code changed in this checkpoint.  
**Branch:** `rebuild/precision-instrument`  
**Baseline:** `12e6a9a9e81c7adae3b072967ee3c2e078ce15ca`  
**Purpose:** Define a genuine product and architecture rebuild, not a theme refresh or feature expansion.

## 1. Executive decision

FM Atlas should become a **precision football intelligence workstation**: visually distinctive, fast, calm, data-dense where useful, and easy to understand. The Watched.earth reference contributes art direction and interaction principles only. FM Atlas must not reproduce its globe, page composition, satellite metaphors, copy, or exact component layouts.

The old application shell is not the target. The current UI is constructed largely in a very large WPF code-behind file; the XAML window itself is only a root grid. The recovery must establish a new information architecture, component system, view-model boundaries, data contracts and performance model before migrating functionality.

**No new feature scope is authorised by this document.** Existing requested capabilities are being reorganised and rebuilt against explicit acceptance tests.

## 2. Watched.earth research

### 2.1 Source material inspected

- Live reference: https://www.watched.earth/
- Public source repository: https://github.com/dev-Armaan/satellites
- Design system: `docs/DESIGN.md`
- Architecture and module contracts: `docs/ARCHITECTURE.md`
- README, screenshots and project structure.

The website's source is available under MIT. Its design document is unusually explicit and useful as a design-research source. We should take inspiration from principles, not copy its branding or product expression.

### 2.2 Visual principles worth translating

- Near-black ink surfaces with warm off-white text.
- A single, rare signal colour (Watched uses sodium orange) reserved for primary action, live state, selection and meaningful alerts.
- A restrained secondary cool tone only where a distinct data category genuinely needs it.
- Condensed display typography paired with monospaced labels and numeric readouts.
- Tabular numerals for values; consistent units and explicit data labels.
- Thin hairline separators, section rules and strong contrast instead of decorative containers.
- Generous negative space around important analysis, balanced with compact, legible data tables.
- Flat surfaces; no decorative gradients, glassmorphism, drop shadows, neon glow or stacks of rounded cards.
- No fake metrics, filler copy, generic spinners or ungrounded status claims.
- Motion should explain a state change or data relationship; it must be brief, precise and respect reduced-motion settings.
- Responsive composition and keyboard-first operation are treated as product behaviour, not afterthoughts.
- Uncertainty and missing data are stated plainly. Unknown values are shown as unavailable, never fabricated.

### 2.3 FM Atlas translation — a distinct identity

Working art direction: **ATLAS / FIELD INSTRUMENT**.

- Ink background, graphite work surfaces, warm bone typography and one burnt-orange signal accent.
- A distinctive condensed editorial display face, highly readable sans body face and monospaced data face. Fonts must be correctly licensed and bundled with notices.
- Square or near-square geometry, hairline borders and restrained separators; no glass panels.
- A compact instrument-style application frame with a persistent career/build/data status rail.
- Football-native visual language: pitch geometry, positional maps, role diagrams, attribute bands, performance plots and squad structures. No satellite/globe imagery or space-themed decoration.
- One primary workspace at a time, with contextual inspectors/drawers only when they add value.
- Dense data grids must remain readable and configurable; do not turn every record into a card.
- Accent colour must communicate meaning, not merely decorate the interface.
- Every metric must carry its unit, source and coverage state where those affect interpretation.

The aim is not to make FM Atlas look like Watched. It is to apply the same level of visual discipline, clarity and authored identity to football intelligence.

## 3. Existing FM Atlas audit — findings from source

### 3.1 Presentation architecture

Inspected the current branch's:
- `src/FmAtlas/MainWindow.xaml`
- `src/FmAtlas/MainWindow.xaml.cs`
- `src/FmAtlas/GlobalSearchWindow.cs`
- `src/FmAtlas/FmAtlas.csproj`

Findings:
- `MainWindow.xaml` contains an almost empty root grid; most of the actual shell and pages are constructed imperatively in `MainWindow.xaml.cs`.
- `MainWindow.xaml.cs` is approximately 331,000 characters and contains shell construction, navigation, search orchestration, dashboard and many workspace builders, event handling and styling helpers.
- The current implementation visibly uses gradients, rounded card borders, drop shadows, a large sidebar/header composition and repeated decorative card treatments. This is precisely the kind of accumulated presentation code that must not be preserved as the target design.
- The app has a mix of local data editing and live-memory editing. These must remain distinct concepts and must never be presented as interchangeable.
- The existing global search is a separate modal window with a fixed set of fields. It is not a full FMRTE-style searchable filter library and condition-builder workspace.

### 3.2 Data and live connection

Inspected:
- `Services/LiveCareerMonitor.cs`
- `Services/LiveDataImporter.cs`
- `Services/Fm27BuildProfiles.cs`
- `Services/LiveMemoryEditService.cs`
- `Services/LiveEntityMemoryEditService.cs`
- `Data/ScoutDatabase.cs`

Findings:
- The reader is Windows process-memory based and uses exact module fingerprints / per-build profiles. This is a necessary foundation, not proof that every decoded field is correct.
- The live importer contains candidate reads for player name, UID, nationality, team/club relationship, CA, PA, visible attributes, personality, position values and other scalar values.
- The importer itself labels several mappings provisional and uses plausibility checks. A field existing in the record model or being range-valid does not make it verified.
- The current importer performs per-entity loops and many individual process-memory reads. The implementation needs profiling and a measured read plan; do not assume the reader is either fast or slow without measuring it.
- The database separates live identity data, save snapshots and local data. The UI must expose those distinctions instead of combining counts into misleading totals.
- The prior architecture/recovery notes report a historical SQLite database-lock failure during live import. This is a known failure to reproduce and fix, not a proven explanation for every missing field.
- The current code contains hard safety gates around live-memory writes. These remain closed until field-level evidence, rollback and save/reload tests are completed.
- The current live scan's successful process/profile match must not be presented as proof that the manager, club, CA/PA, attributes or relationships were read correctly.

### 3.3 Search and data usability

The current global search uses a fixed set of text boxes and dropdowns (for example age, CA/PA, nationality, league, foot, one selected attribute and position). This is a useful prototype, but it is not the requested search product.

The rebuilt search must be a first-class workspace:
- Entity scopes: players, staff, clubs; other entity types only when supported by imported data.
- Search by name and stable source ID.
- Searchable filter catalogue grouped by meaningful categories.
- Any supported player attribute, hidden attribute or personality field, with min/max operators and visible source/validation state.
- Position and role filters using actual supported FM27 position identifiers.
- Range, exact, contains, any-of and none-of operators where semantically appropriate.
- AND/OR groups, include/exclude and saved searches.
- Configurable result columns, sorting, multi-select and contextual actions.
- Results must be database-backed and virtualised; do not construct a visual control for every record.
- Empty results and missing values must be explained, not silently hidden.

## 4. External product and implementation research

### 4.1 FMRTE 27 — product behaviour reference

Sources:
- https://www.fmrte.com/
- https://docs.fmrte.com/
- https://docs.fmrte.com/scouting/search/
- https://docs.fmrte.com/getting-started/interface-tour/
- https://docs.fmrte.com/players/ability/

Patterns to learn from:
- Quick Search and Advanced Search are separate but connected workflows.
- Search supports players, staff and clubs, with grouped criteria and range filters.
- Record tabs, persistent result grids, configurable columns and keyboard shortcuts.
- Player profile sections separate attributes, ability, personal details, contracts and season statistics.
- Role ratings are distinct from CA/PA and have their own calculation semantics.
- Edit drafts, explicit save/apply, changed-field indicators, edit history and undo.
- Mass edit supports a preview before committing.
- Season statistics are presented separately from attributes and ability; FMRTE documents its season stats as read-only.

Do not copy FMRTE's proprietary implementation or visual design. Use its public behaviour as a parity checklist.

### 4.2 FMLibreEditor — live editing and safety reference

Sources:
- https://github.com/tozmoz/FMLibreEditor
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md

Patterns to learn from:
- Separate process-memory access, FM domain logic, build profiles, probes and UI.
- Exact build fingerprints and storefront-specific profiles.
- Calibration/probe tooling and field-level checks.
- Unknown or changed builds fall back to read-only.
- Every write must re-resolve the entity and validate process, build, career generation, identity and expected current value.
- Apply and undo are guarded operations, not direct UI-bound writes.
- Search, filtering, player/staff/club browsers, profile editing and mass-edit previews are separate components/workflows.

FMLibreEditor is GPL-3.0. FM Atlas must not copy or port its code without a deliberate licence/compliance review. Use documented behaviour and independently implement compatible concepts.

### 4.3 FMSuperScout — high-throughput read/scouting reference

Sources:
- https://github.com/mavarobli/FMSuperScout
- https://github.com/mavarobli/FMSuperScout/blob/main/core/

Patterns to investigate:
- Small scanner process separated from the browsing application.
- Bounded, fast memory dump/read pipeline.
- A normalized local snapshot that makes subsequent search instant.
- Real CA/PA and hidden fields where actually read from memory.
- Filter stacking, saved presets, staff search, role ratings, comparison, squad analysis and longitudinal snapshots.
- Clear distinction between direct reads and estimates.

The public project says it is read-only; it is not a live editor. Its scanner and model tests are relevant to data ingestion and performance, not a complete replacement for the editor architecture.


### 4.3.1 Scanner implementation findings

Inspected the FMSuperScout source files `core/Dumper.cs`, `core/MemScan.Win.cs` and `core/Fields.cs`, rather than relying only on its README.

Implementation patterns worth testing against FM Atlas:
- Reads are organised around memory-region scans and reusable large buffers, not a fresh small byte-array allocation for every individual field.
- The scanner uses thread-local result collections during parallel work, then merges them after scanning.
- Buffer pooling is explicit; the source comments explain that allocating new large-object-heap buffers per scan caused heap growth.
- It records phase timings, progress, scan state and errors in diagnostics.
- It can use a process snapshot as a fallback for a consistent point-in-time read, but documents the extra commit-memory cost and does not make snapshotting the default path.
- Memory availability is measured and used to adapt worker/cache decisions.

This is a research pattern, not an instruction to copy its implementation. FM Atlas must benchmark its current per-entity read strategy against a chunked/batched strategy and choose based on measured peak memory, allocations, scan time and accuracy. Snapshotting must be opt-in/fallback only if the extra memory is acceptable.

The FM Atlas importer currently allocates per-entity read buffers and dictionaries while traversing tens of thousands of player records. This is a concrete allocation hotspot to profile, but it is not proof by itself of the user's reported memory consumption.

### 4.2.1 FMLibreEditor implementation findings

Inspected its `crates/fmlibre-core/src/index.rs`, `locate.rs`, `edit.rs` and `profile.rs` in addition to its development notes.

Important implementation lessons:
- The build profile selects module-relative addresses, while the object layout separately describes field offsets. These are different layers and should remain separate.
- A profile match is followed by structural checks; the editor can search for key structures when a profile is missing or no longer fits.
- Memory probing is read-only and works over bounded chunks of writable, non-executable module data.
- Field encodings are explicit (signed/unsigned widths, dates, flags, pointers and scaled attributes).
- Pointer writes are checked against the expected object class; field writes revalidate the object and current bytes and read the result back.
- Layouts are strict data contracts, with unknown fields rejected during deserialization.

This supports a typed, versioned field catalogue and probe tool in FM Atlas, rather than scattering offsets and decoding assumptions through UI or importer code.

### 4.4 Football Manager 27 — authoritative product data

Official source:
- https://www.footballmanager.com/fm27/features/fm27-training-and-tactical-effectiveness

The official description defines Tactical Effectiveness through four connected areas:
1. Player Ability
2. Next Match Information
3. Player Buy-In
4. Tactical Understanding

Tactical Complexity is a separate measure influenced by the volume of team, player and opposition instructions. Tactical Effectiveness is surfaced across Tactics, Training, Recruitment and Scout Reports.

Therefore:
- Tactical Effectiveness must not be substituted with Atlas role-fit or position suitability.
- Tactical Complexity must remain a separate concept.
- The official description does not publish the underlying numeric formula or memory layout. No formula or offset may be invented.
- The rebuild should display actual in-game values only when read and validated. Otherwise show the observed component, evidence state or unavailable status.

### 4.5 Wyscout / StatsBomb-style scouting — honest scope

FM career data can provide attributes, contracts, values, season statistics and match statistics when those are actually present and correctly linked in the save. Public StatsBomb Open Data can provide event examples for supported competitions. User-supplied Wyscout exports can provide additional data where the user has a valid export.

These sources are not interchangeable:
- FM season totals are not StatsBomb event data.
- StatsBomb Open Data is not a universal current global player database.
- Wyscout CSV import is not a Wyscout live API or a licence to distribute Wyscout data.
- Derived per-90 metrics require minutes and a documented denominator.
- Percentiles require a defined comparison cohort.
- Cross-league ratings require a defensible calibration; otherwise show league-relative results.

Every metric must be marked as one of: **Observed in FM save**, **Imported external event data**, **Calculated by Atlas**, **Estimated**, or **Unavailable**.

## 5. Target information architecture

This is the proposed product structure. It is a redesign of navigation and workflows, not an instruction to add new features.

1. **Home** — selected career, connection state, import health, data coverage and actionable overview.
2. **Players** — fast player pool, filter ribbon, configurable grid and persistent record tabs.
3. **Search Studio** — full filter builder for players, staff and clubs, saved searches and result previews.
4. **Player Profile** — Overview, Attributes, Ability, Positions & Roles, Contract, Performance, Development, History and Evidence.
5. **Recruitment** — saved profiles, performance/value screening, similar-player discovery, shortlists and recruitment pipeline.
6. **Squads** — club/squad directory, depth, positional coverage and squad structure.
7. **Staff** — staff directory, attributes, roles, job preferences and suitability where supported.
8. **Tactics & Roles** — player-role fit, tactical needs and verified Tactical Effectiveness data kept separate.
9. **Editor** — explicit local draft and live-game modes, field catalogue, preview, apply, history and undo.
10. **Data Lab** — imports, source provenance, build profiles, field evidence, diagnostics and validation.

Global search should be a keyboard-first command/search overlay (Ctrl+K), not a replacement for the full Search Studio.

## 6. Target architecture

The UI and data layers must be separable and testable. Avoid another giant code-behind class.

Proposed boundaries:
- **Presentation:** reusable WPF controls, design tokens, views and navigation shell.
- **Application:** view models, commands, query orchestration, import jobs and state transitions.
- **Domain:** player/staff/club records, metric definitions, field evidence and editing rules.
- **Infrastructure:** SQLite repositories, process scanner, save parser adapters, file/graphics resolvers and telemetry.
- **FM27 adapter:** exact build profiles, memory map catalogue, entity resolution, read plans and guarded write adapter.
- **Analytics:** metric normalization, per-90 calculations, cohort/percentile engine, role fit and performance/value ranking.

Key contracts:
- Every record carries a stable source ID and source/snapshot identity.
- Every field has source, freshness, coverage and validation/evidence metadata.
- Missing is not zero; estimated is not observed.
- Import work is cancellable, progress-reporting, bounded and atomic at snapshot publication.
- UI navigation never blocks on a full import or large query.
- Queries are paged/virtualized and indexed; no unbounded in-memory result collections.
- Database writes are serialized deliberately, use short transactions and expose lock/timeout telemetry.
- Live reads and writes are separate interfaces with separate process permissions.
- Live writes remain disabled until each field group passes the full acceptance matrix.

## 7. Performance and reliability acceptance gates

Before any feature is called complete:
- Measure cold start, idle memory, post-import memory, peak scan memory and query latency on a representative FM27 career.
- Profile allocations and retained objects; do not guess at the memory leak/source.
- Scan must be cancellable and must discard incomplete snapshots.
- Database import must be atomic and resumable or safely restartable.
- Search results must return in bounded pages with responsive typing and no UI freeze.
- Navigation must remain responsive while background jobs run.
- Repeated connect/disconnect and career switching must not accumulate timers, handlers, entity collections or stale records.
- App restart must preserve the correct source/career state without stale manager/club details.
- Unknown build, invalid mapping, mismatched identity or uncertain data must fail closed and display the reason.

Performance numbers will be set after baseline measurement on the actual Windows target hardware; do not invent a target before measurement.

## 8. Live editor acceptance gates

No live write is enabled by the visual rebuild.

For every writable field:
1. Exact build/storefront fingerprint.
2. Entity class and stable UID resolution.
3. Field offset, size, encoding, range and evidence recorded.
4. Known in-game value compared with the FM27 UI.
5. One-field write on a disposable offline career copy.
6. Exact memory read-back.
7. FM UI refresh behaviour documented separately.
8. Undo restores the original bytes after revalidation.
9. Normal FM save/close/reload confirms persistence.
10. Automated regression and failure/partial-write recovery tests pass.

A candidate offset, plausible value or successful memory read is not enough. Tactical Effectiveness remains read-only/research-only until its actual semantics and writable storage are independently proven.

## 9. Delivery order

1. Complete the source and runtime audit; record exact defects and baseline metrics.
2. Freeze the legacy UI as a reference only; preserve data adapters and proven domain logic where justified.
3. Establish the new architecture, design tokens and navigation shell.
4. Rebuild the Players/Search Studio experience around real data and fast queries.
5. Rebuild player profiles, role/position analysis and recruitment workflows.
6. Rebuild data import and Data Lab with explicit provenance, coverage and validation.
7. Rebuild the editor as a safe draft/apply/undo workflow; keep live writes gated.
8. Rebuild analytics and Tactical Effectiveness views from validated FM27 data.
9. Profile memory and responsiveness; fix the measured causes.
10. Run full automated and Windows runtime acceptance before publishing a test build.

## 10. Research references

- Watched website: https://www.watched.earth/
- Watched source: https://github.com/dev-Armaan/satellites
- FMRTE: https://www.fmrte.com/
- FMRTE search documentation: https://docs.fmrte.com/scouting/search/
- FMRTE interface tour: https://docs.fmrte.com/getting-started/interface-tour/
- FMRTE ability documentation: https://docs.fmrte.com/players/ability/
- FMLibreEditor: https://github.com/tozmoz/FMLibreEditor
- FMLibreEditor development notes: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- FMSuperScout: https://github.com/mavarobli/FMSuperScout
- FM27 Tactical Effectiveness: https://www.footballmanager.com/fm27/features/fm27-training-and-tactical-effectiveness

## 11. Explicit non-goals

- Do not clone Watched.earth.
- Do not simply recolour or round the existing FM Atlas pages.
- Do not preserve the current sidebar/header/card composition as the target shell.
- Do not add features to compensate for broken foundations.
- Do not fabricate player values, statistics, role ratings or tactical formulas.
- Do not copy GPL or proprietary source code into FM Atlas without a licence review.
- Do not enable unverified live writes.
