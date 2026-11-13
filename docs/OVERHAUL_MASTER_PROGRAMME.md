# FM Atlas — Integrated Overhaul Programme

**Programme branch:** `overhaul/monolith-full-platform`  
**Baseline:** R7.2.0 repaired source v2; Windows Release build reported successful on 2026-11-13 (0 errors, 15 warnings).  
**Purpose:** One integrated product programme covering source recovery, reverse-engineering evidence, MONOLITH design system, scouting, analytics, tactical intelligence, editing, graphics/modding, testing and release engineering.

## Operating rules

1. Preserve R7.0.2, R7.1.0 and R7.2.0 as immutable historical baselines. Work only on this programme branch until reviewed.
2. The user's current target is FM27 on Xbox Game Pass PC. Steam, Epic and Game Pass are separate compatibility targets; never infer Game Pass support from Steam.
3. Distinguish: researched, source-inspected, implemented, compiled, launched, tested with fixture, tested with copied save, verified in-game. Never promote a status without evidence.
4. No invented offsets, structures, formulas, player data, hidden attributes, competition-strength adjustments or game capabilities.
5. No third-party source copying without licence review. GPL repositories are research references unless a deliberate licence decision is recorded.
6. Read-only acquisition and offline analysis precede live writes. Live writes remain disabled unless exact build, process, save and entity identity checks pass.
7. Never test against the user's only career save. Use disposable offline copies. Preserve originals and maintain a rollback journal.
8. Every visible control must map to a real supported field/action. Mark unsupported or unavailable fields rather than shipping dead controls.
9. Keep a feature traceability matrix linking each requirement to implementation files, tests and evidence.
10. Each checkpoint records commit, changed files, build output, test output, remaining risks and next exact action.

## Product definition

FM Atlas is a premium Windows Football Manager intelligence and modding suite, not merely a player search screen. It combines scouting, recruitment, analytics, tactical intelligence, player development, squad planning, database/save inspection, safe editing, graphics/skin management and mod research in one coherent application.

### MONOLITH visual identity — authoritative

- Primary theme: Obsidian / graphite dark.
- Secondary theme: Warm light.
- Accent: Atlas orange.
- Palette: Obsidian `#101216`, Graphite `#1B1F25`, Elevated `#252A32`, Border `#343A43`, Atlas Orange `#F28A36`, Soft White `#F1F2F3`, Muted Grey `#9299A3`; light theme Warm Mist `#F2F2EF`, White `#FFFFFF`, Ink `#242830`, Deep Orange `#E87826`.
- Typography: Inter / Inter Display with tabular numerals.
- Design influences: Swiss Sport editorial hierarchy, Touchline football-specific visualisations, Pulse statistical presentation.
- Rounded, approachable geometry; restrained transitions; no constant animation or flashing.
- Aurora Frost is not the primary direction and must not override MONOLITH.
- Design tokens and reusable components precede feature-screen migration.


### Built-in Diagnostics & Support Centre
- Persistent structured application logs with severity, timestamp, correlation ID, feature/module, operation and exception details.
- User-facing Diagnostics page reachable from the main shell and from startup/recovery paths; must remain accessible when a feature module fails.
- One-click Run Diagnostics covering app configuration, required files, data-provider health, import status, cache/database integrity, permissions, dependency/runtime information and feature availability.
- Export Support Bundle to a single ZIP containing redacted logs, diagnostic results, app/build version, OS/.NET details, relevant configuration with secrets removed, and a manifest describing included files.
- Preview bundle contents and explicitly confirm export; never include career-save contents, personal documents, credentials, tokens, or arbitrary user files by default.
- Provide Copy Error Details and a human-readable diagnostic summary, plus a stable support-bundle ID to match reports to logs.
- Log rotation, size limits, retention settings, crash/restart recovery, and safeguards against logging player personal data or sensitive memory contents.
- Local-first export: no automatic upload or transmission. The user chooses where to save and how to share the ZIP.
- Diagnostics must report unavailable/unsupported checks honestly and must not claim a repair occurred unless it was performed and verified.

## Reverse-engineering workstream

### Research targets and current evidence

| Target | What to inspect | Current status / boundary |
|---|---|---|
| FMLibreEditor | Game Pass/Steam profiles, process attach, field layouts, stale-pointer prevention, preview/apply/undo | Desk/source research recorded; GPL-3.0; no code copied; exact user's build unverified |
| barak14/fm-editor | Versioned layouts, entity discovery, reader/writer abstraction, undo and debug inspection | Research lead; GPL-3.0; FM27 layouts may be uncalibrated |
| FMSuperScout | Scanner/core/UI separation, bulk read strategy, indexing, snapshots, role/value models | Research lead; MIT stated; performance claims not reproduced |
| rhiever/fmsave | Offline save parsing, typed tables, coverage and validation | Research lead; MIT stated; exact installed build coverage to test |
| gilet | FM26 container, Zstandard frame handling and validation methodology | FM26 evidence only; FM27 parity not assumed |
| FM Player Analyzer | Save parser, scouting knowledge, graphics index and data mapping | Uploaded source statically inspected; GPL-3.0; independent implementation only unless licensing decision |
| FM View Exporter | Visible-list CSV/HTML export and fixture ingestion | GPL-3.0 BepInEx/Steam route; not assumed Game Pass compatible |
| FMM Editor | Pre-game database parsing, entity relationships and editing workflows | Research lead; separate from active-career editing |
| FMRTE 27 | Search, editing, stats, transfer terms, mass change, undo and workflow UX | Commercial feature benchmark only; internals proprietary |
| FM Dossier | Read-only live memory and local processing | Product docs reviewed; source proprietary; Steam tested per docs, other stores not assumed |
| TalentSeer | Scouting, hidden-stat and role-rating claims | Uploaded package/README inspected; executable not run; claims unverified |
| Teknik Oda | Match analysis and BepInEx integration | Product/page research only; do not assume compatibility |
| FaceDrop | Separate face/image provider and storage paths | Model as separate provider, not standard graphics pack |
| Customize Editor | Pre-game editor skin/mod workflow | Alpha/product research; validate supported game version |
| Scout Reporter | CSV/browser-local reporting workflow | UX/data-export reference |
| SaveMyTime | Schedule template/DLL behaviour | Static inspection only; no execution; behaviour unverified |
| Official SI/SEGA documentation | User-data path configuration and Microsoft Store/Game Pass exceptions | Prefer official path rules; never hard-code Steam paths for Game Pass |

### Required reverse-engineering deliverables

- Per-project source tree map, licence, build method, dependencies, entry points, data structures, parser/reader strategy, version handling, tests and limitations.
- FM26/FM27 version and storefront compatibility matrix with exact game build, module fingerprint, evidence and confidence.
- Independent FM save-container validation fixtures; never assume FM26 container details equal FM27.
- Save-data field dictionary: field name, type, source, version, visibility/scouting condition, units, provenance and confidence.
- Memory field map: module, build fingerprint, structure/offset, type, range, identity key, evidence source, test status and supported read/write state.
- Graphics path/provider map including configurable SI user-data folder, standard graphics packs, config.xml indexing, duplicate conflicts, FaceDrop and cache invalidation.
- Feature/UX comparison against FMRTE, FMSuperScout, FM Dossier and other tools without copying protected code/assets.
- Licence and attribution register for every external project and asset.
- Explicit list of unanswered questions and blocked pathways. Research is not complete while these are unresolved.

## Full product feature inventory

### A. Foundation and application shell
- Reproducible .NET 8 WPF build, startup diagnostics, structured logs and redacted support bundle.
- Separation of presentation, application services, domain models, data providers, game adapters and infrastructure.
- Central settings, database/cache/log/user-data locations, backup/restore and migration.
- Main navigation, sidebar, top bar, command palette (Ctrl+K), global search, notifications/status, quick actions and profile/settings.
- Resizable workspace and layouts; keyboard-first navigation, high-DPI, contrast and text scaling.
- Shared loading, empty, error, progress and unsupported-data states.

### B. MONOLITH shared design system
- Semantic tokens for colours, typography, spacing, radii, borders, elevation, focus, status and motion.
- Buttons, icon buttons, text/numeric inputs, combo boxes, searchable dropdowns, tabs, chips, badges, tooltips, menus, dialogs, pop-outs/drawers, cards and tables.
- Shared chart, percentile bar, radar, attribute grid, player pitch and tactical pitch visualisations.
- Dark and warm-light themes driven by the same tokens.
- Screenshot/visual regression checkpoints and accessibility/resolution checks.
- Migrate every feature screen to shared components; no one-off style islands.

### C. Universal data and search engine
- Canonical field registry across players, staff, clubs, nations, competitions, contracts, attributes, positions, season stats and development.
- Data provenance, game version, capture timestamp, source type and confidence per field.
- Universal search with quick and advanced modes, field/category selector, typed operators and type-aware editors.
- Multiple conditions, add/remove/duplicate/reorder, include/exclude, grouped AND/OR and nested groups where supported.
- Save, rename, duplicate, delete, import/export and reuse search profiles.
- Active-filter chips, match explanations, result counts, custom columns, sorting and export.
- Missing/unknown semantics, date handling, numeric boundaries and large-dataset performance tests.
- Every criterion must be backed by a verified field and executable query.

### D. Advanced Scouting Search
- FMRTE-inspired interaction model implemented independently.
- Left filter builder, centre result table, right player summary/profile.
- All verified FM positions; 15-position pitch/suitability visualisation and five labelled suitability levels where data supports.
- Physical, mental, technical and hidden attributes only where genuinely available and authorised by source.
- Age, date, nationality, league, club, home-grown, origin, contract, value, wages and availability.
- Newgen/regen origin values All/Newgen/Real/Unknown; Unknown must never be silently treated as Newgen; age relative to in-game date.
- Saved searches, presets, reusable profiles and custom result columns.
- Explain why each player matched and which fields are unavailable.

### E. Player Intelligence / Player DNA
- Full player profile: identity, age, position/suitability, attributes, foot, personality only when sourced, club/league/nationality, contract and home-grown status.
- Attribute radar, role suitability, strengths/weaknesses, development history and season trends.
- Current ability/potential only when source supports it; estimates visibly marked.
- Player comparison, similar-player search and replacement suggestions.
- Profile graphics and history with data freshness and provenance.

### F. Performance & Moneyball Scout (Wyscout-style)
- Audit save data for season, competition, minutes, player-season linkage and statistic coverage.
- Canonical metric registry with raw totals, per-90, rates/percentages, units, season, competition, position, source and sample size.
- Filters combining transfer value/asking price, age, position, minutes, per-90 performance, totals, rates and FM attributes.
- Position/league/age/minutes cohort percentiles and sample-size thresholds.
- Separate FM attributes from observed match performance.
- Cross-league comparisons show raw league-relative percentiles unless defensible strength calibration exists.
- Value-for-performance ranking; distinguish transfer value, asking price, wage and actual fee.
- Profiles: Cheap Dribblers, Creative Midfielders, High-Output Strikers, Ball-Winning Midfielders, Progressive Defenders and user-defined.
- Season trends, shortlist, side-by-side comparison, CSV/report export and recruitment radar.
- Validate transfers, loans, duplicate IDs, season splits, competition changes and missing data.
- Label Observed in save / Calculated by Atlas / Estimated / Unavailable.

### G. Scouting Matrix and Recruitment Intelligence
- Recruitment board with shortlist, priority, status, notes, tags and owner.
- Compare candidates against squad needs and tactical roles.
- Similar-player/replacement finder, squad depth, age profile, contract expiry, wage risk and home-grown balance.
- Budget-aware transfer and wage planning; fee vs value vs asking price kept distinct.
- Recruitment radar, position gaps, succession planning and saved recruitment projects.
- Exportable recruitment reports and shareable CSV/PDF outputs.

### H. Role Lab and Player Fit
- Preserve and validate existing 23 transparent IP/OOP role profiles.
- Role suitability with visible weights, inputs, position restrictions and uncertainty.
- Team-specific role requirements and compare candidates.
- Never claim estimates are the game's internal role score unless verified.

### I. Tactical Intelligence Studio
- Inspect FM27 Tactical Effectiveness sources before implementation.
- Four areas only where accessible: Tactical Understanding, Player Ability, Player Buy-In, Next Match Information.
- Tactical complexity and squad suitability based on documented inputs.
- Link roles, attributes, squad composition and player buy-in to tactical choices.
- Separate observed FM values from Atlas calculations/estimates.
- Do not invent hidden effectiveness formulas.
- Editing is separate, explicit and guarded; no injector/BepInEx dependency by default.

### J. Match Lab
- Match and performance analysis where save data supports it.
- Team/player match statistics, trends, opposition reports, tactical outcomes and export.
- Separate event-level data from aggregate stats; show coverage gaps.

### K. Player Development and Career Assistant
- Snapshot history, development tracking, age curves only when defensible, training/progress trends and attribute changes.
- Contract, squad registration, pathway, loan and playing-time planning where data is available.
- Career briefings, reminders and actionable alerts based on real save changes.
- No invented predictions; expose model inputs and confidence.

### L. Save Inspector and Data Import
- Read-only offline save parser/provider, version-specific container handling and typed records.
- FM26/FM27 import path and schema adapters.
- Player/staff/club/nation/competition relationships, IDs, duplicates, malformed records and missing mappings.
- Scouting-knowledge-aware values and masked/unscouted fields.
- Repeatable import, partial recovery, diagnostics and provenance.
- FM View Exporter CSV/HTML import as an optional fixture/provider, not a substitute for full save import.
- Compare imported fields against in-game UI and independent tools.

### M. Database and Editor Studio
- Keep pre-game database editing, Atlas-local database editing and live-career editing as distinct modes.
- Local editor: preview, Set/Add/Scale, bulk selection, validation, transaction, save and guarded Undo/Redo.
- FM database file discovery/parsing, entity relationships, versioning and backups.
- Explicit edit mode, changed-field indicators and deliberate Save action.
- Audit fields and persistence; test migrations and rollback.
- Never conflate database edits with changes to the active career.

### N. Live Game Adapter
- Exact storefront/build detection; Game Pass target first, Steam separate.
- Read-only default for unknown builds.
- Process/module fingerprint plus field-level profile validation; module fingerprint alone is insufficient.
- Save identity, entity identity, range, type, expected old value and current byte preflight.
- Save-load generation detection; invalidate stale pointers and suspend writes after reload/career switch.
- Preview, explicit opt-in, write journal, read-back verification and guarded Undo/rollback.
- Only supported fields enabled; hidden attributes and structural edits blocked until independently verified.
- Disposable offline save acceptance only. No user's only save, online/shared save or unsupported profile.

### O. Graphics and Skin Studio
- Configurable Sports Interactive user-data location; Game Pass must not inherit Steam override assumptions.
- Standard graphics pack providers, config.xml parser/index, person/club/nation ID mapping, duplicate/conflict detection.
- Separate FaceDrop provider/path.
- Async thumbnail cache, invalidation by config metadata, missing/broken image diagnostics.
- Skin discovery, preview, install/restore workflow and compatibility labels where lawful and supported.
- Never include copyrighted graphics packs or game assets in repository/releases without permission.

### P. Modding and Research Workbench
- Catalogue mods/tools/repos, versions, licences, supported game versions and installation paths.
- Safe static inspection of archives/binaries; no automatic execution of unknown EXEs/DLLs.
- FM file-format research, database/graphics/skin structure notes and reproducible fixtures.
- Plugin/injector approaches remain opt-in research; do not introduce them as dependencies without threat/licence/compatibility review.

### Q. Value Finder and Account/Squad Tools
- Preserve current Value & Upside Finder but verify formula, source fields and database safeguards.
- Transparent ranking, filters, sorting, export and explanation.
- Squad planner, budget-aware upgrade paths, wage/contract and depth views.

### R. Release Engineering and Quality
- Unit, integration, parser, query truth-table, migration, UI and performance tests.
- Test fixtures and anonymised/synthetic data; no personal saves committed.
- Build warnings triage, reproducible publish, packaging, installer, update and rollback.
- Smoke tests on supported Windows environments and explicit Game Pass/Steam matrix.
- Release notes identify verified, experimental, unsupported and unavailable capabilities.

## Implementation / acceptance gates

The user requested a single integrated delivery. Work should be coordinated as one programme and integrated branch, but acceptance remains evidence-based. A single untested mega-change is not a reliable delivery. Build/test continuously inside the same branch; do not defer all verification to the end.

### Mandatory final acceptance
- Source tree imported and hash/provenance recorded.
- MONOLITH tokens/components used by every migrated screen.
- No dead controls; feature-to-field traceability complete.
- Search truth tables and saved-search round-trip pass.
- Import/DB migrations pass against copies and fixtures.
- Performance/percentile calculations pass known-answer tests.
- Graphics resolver tested against real configured folders.
- App launches and smoke tests pass on Windows 10.
- FM27 Game Pass read-only profile confirmed for exact build.
- Live Apply/read-back/Undo only after safe disposable-save preconditions; otherwise clearly disabled and documented.
- All warnings triaged; all known gaps recorded.
- Build artifacts, logs, screenshots and release package archived.

## Current evidence snapshot — 2026-11-13

- GitHub repo: private `lewisxatk/FM-Atlas`; default branch `main`.
- Programme branch created from commit `2051bbee042fb8e7203b7855e833e391ccee9ecb`: `overhaul/monolith-full-platform`.
- User's repaired R7.2.0 v2 Windows build log reports **Build succeeded, 0 errors, 15 warnings**. Executable path: `src/FmAtlas/bin/Release/net8.0-windows/FmAtlas.exe`.
- This proves compilation on the user's PC only. App launch, feature behaviour, data import and live editing remain unverified.
- Repaired R7.2.0 source is imported under `application/R7.2.0/FM-Atlas-R7.2.0-Work/` (source-import commit `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`). Historical baselines remain separate and preserved.
- Initial source architecture audit is recorded in `docs/SOURCE_ARCHITECTURE_AUDIT.md`. It confirms the existing WPF/SQLite architecture, partial MONOLITH integration, Diagnostics foundation, live-write gate and graphics resolver boundaries.
- Diagnostics hardening checkpoint `6fc46757688be84af497e13c3d27e40877da3f92` fixes credential-token and full Windows/UNC path redaction and adds in-app regression checks. Equivalent regex tests passed locally; the Windows build and runtime check remain pending.
- Prior reverse-engineering register is desk research. It explicitly records that no third-party source was copied, no external executable was run, no live game process/save was inspected, and no user's exact Game Pass build/profile was verified.

## Change log

- 2026-11-13: Created integrated overhaul branch and consolidated product, research, design, feature and acceptance scope. No claim that the listed features have been implemented by this document.
- 2026-11-13: Reconciled the source-import status with the live branch, recorded the first application architecture audit and corrected diagnostics privacy redaction. Runtime acceptance remains pending.

- 2026-11-13: Added initial Windows .NET 8 MSTest project and CI test execution for Diagnostics privacy behaviour; Windows execution remains pending.
- 2026-11-13: Improved MONOLITH runtime theme switching with in-place token/legacy brush updates; bound MainWindow and Player Intelligence report XAML to theme resources. Full page migration and Windows visual acceptance remain pending.

## Current verified engineering checkpoint — 2026-11-13

The following supersedes older build-status statements above where they conflict:

- Repaired R7.2.0 source is imported at `application/R7.2.0/FM-Atlas-R7.2.0-Work/`; source-import commit `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`.
- Windows Actions run `43470501984` tested application source commit `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712`: .NET 8 WPF Release build succeeded with **0 warnings, 0 errors**; **47/47 tests passed**. Release and test-result artifacts were uploaded.
- Coverage includes Diagnostics redaction, field-evidence and identity policy, a real disposable SQLite test of live CA/PA/attribute query consumers, role-fit calculation, squad-assignment optimizer, metric math and ALL/ANY search matching.
- Follow-on Role Lab source change `4b4ddfcb7299a0fad5dc2ea60658d895a48127df` exposes selected role input attributes and weights plus a method/missing-data explanation. Its own Windows workflow `43471187844` completed successfully: WPF Release build 0 warnings/0 errors and 47/47 tests passed; visual/runtime acceptance remains open.
- These are compile and automated-test results. They do not establish that the WPF application has launched, every page works, themes are visually accepted, support ZIPs have been manually inspected, or FM27 Game Pass/save integration is accepted.
- Exact Game Pass build/field compatibility remains unverified. Live memory Apply/Undo remains disabled by the global certification gate.

## Latest verified Role Fit contribution implementation — 2026-11-13

- Latest exact code commit: `91bd7728c555a0c0e54cfd8d02706467fdafdb89`.
- Windows Actions run `43472380514`: .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **48/48 MSTest tests passed**. Release artifact `14061643285`; test-results artifact `14061143387`.
- RoleFitService now returns explainable per-attribute additive score contributions. It retains human-readable profile labels, excludes missing/invalid values, requires at least 50% count coverage and scales weights to avoid overflow.
- The database carries contributions to the Role Lab candidate result; the UI displays per-player contributions on selection and exports them to CSV. The contribution sum is tested against the displayed weighted estimate.
- This improves the Role Lab implementation, but does not complete the role/scouting workstream. Position familiarity, role suitability evidence, player-level UI visual acceptance and exact FM27 Game Pass runtime verification remain open.
- The intermediate compile failure on `2f978f3` was corrected in `0c6546a`; the final tested commit is `91bd772`.
- WPF runtime, route-by-route UI acceptance, actual save-copy import acceptance, graphics folder validation, and Game Pass build/field certification remain outstanding. Live memory Apply/Undo remains disabled.

## Latest integrated Windows acceptance checkpoint — 2026-11-13 17:33 UTC

- Exact tested source commit: `8cfe5b3f276770179e8cb77db7a02912f2336bdb`.
- Actions run `43473487285`: .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **53/53 tests passed**, 0 failed/skipped. Release artifact `14061763980`; test-results artifact `14062007740`.
- The latest Role Fit implementation now exposes each selected candidate's available input values, readable labels, Atlas role weights and additive contributions; CSV export carries the breakdown. Unit tests prove contributions sum to the weighted estimate and guard extreme finite weights.
- SQLite integration test `ScoutDatabaseRoleFitIntegrationTests` seeds a disposable test DB and exercises the real Role Lab query; it verifies score, count coverage, named inputs, weights and additive contributions from stored player attributes. The FM save importer writes these attributes to PlayerAttributes, which the query consumes; FMSAVE candidates remain scoped to the latest snapshot.
- Diagnostics redaction now covers quoted JSON/config secrets, Basic authorization, client/private secrets, query-token values, Windows and UNC paths, and save filenames. Both unit regressions and the in-app Diagnostics self-check exercise these formats.
- All commits after tested application source `8cfe5b3` are documentation-only checkpoint updates; application source remains unchanged from the 53-test run. The protected `main` branch remains at `2051bbee042fb8e7203b7855e833e391ccee9ecb`; fetch the live branch ref before resuming.
- Still not verified: WPF startup/route acceptance, visual theme and DPI acceptance, the user's exact FM27 Game Pass build/profile, actual save-copy import behaviour and a manual inspection of exported support ZIPs. The whole overhaul is not complete.
- The next integrated engineering gates remain Advanced Search, player intelligence, Moneyball cohorts/percentiles, recruitment workflows, Tactical Intelligence, Match Lab, development, Save Inspector/local editor, Game Pass read-only adapter, graphics/skin/mod workbench and release packaging. Live Apply/Undo remains disabled until field-level certification and safe read-back/rollback evidence.

## Latest verified Search Profiles + Support Bundle checkpoint — 2026-11-13 17:50 UTC

- Exact tested application source commit: 6c18e36fdc706fc49ad74ce15fe1ee73c43ce991.
- Windows Actions run 43476085012: .NET 8 WPF Release build 0 warnings / 0 errors; 64/64 tests passed, 0 failed/skipped.
- Release artifact 14062557163; test-results artifact 14062892033.
- Stats Search and Attribute Search now provide local New/Save/Load/Delete profile controls with schema/bounds/duplicate validation, atomic persistence and stale-field protection. Statistics profiles retain ALL/ANY, per-90 and minimum minutes; attribute profiles retain the optional text filter and 1–20 conditions. Profiles persist criteria only, not player or career-save content.
- The SupportBundleComposer now enforces a strict entry allow-list, redacts report/log content, rejects arbitrary paths/path traversal, previews the exact package and creates a SHA-256/size manifest. Tests cover archive round-trip, allowed entries, hash correctness and redaction.
- The tested application source is still the latest application code; subsequent branch changes are documentation/research only. Main remains untouched at 2051bbee042fb8e7203b7855e833e391ccee9ecb.
- User direction: continue the entire roadmap as one integrated programme. Internal implementation stages are verification gates, not separate user-facing releases; earlier hold language elsewhere in the docs is superseded.
- Still outstanding: launch and visually test both search screens, restart/persistence and real results; manually inspect both support ZIP privacy modes; launch all 21 routes; verify theme/DPI; test a copied FM27 Game Pass save, exact build/field certification and actual graphics root. Live memory Apply/Undo stays disabled until field-level certification and disposable-save rollback pass.

## Latest verified integrated engineering checkpoint — portable Search Profiles — 2026-11-13 17:57 UTC

- [x] Exact tested application source commit: e67c0a3b65dcadee485783ca700555bb89fa7076.
- [x] Windows Actions run 43477411616: .NET 8 WPF Release build succeeded with 0 warnings / 0 errors; 67/67 MSTest tests passed, 0 failed, 0 skipped.
- [x] Release artifact 14062868942 and test-results artifact 14063946560 uploaded. The downloaded Windows Release ZIP contains FmAtlas.exe, .NET dependencies, SQLite and the save-bridge tools. The downloaded TRX independently records 67 passed, 0 failed.
- [x] SavedSearchProfileStore now supports Duplicate, ExportJson by all/category, and ImportJson with schema/size/field validation. Imports merge without overwriting existing IDs or same-category names and return explicit skipped-conflict details.
- [x] Stats Search and Attribute Search expose Duplicate profile, Import profiles and Export profiles in addition to New/Save/Load/Delete. Exports are category-specific; imports accept the versioned portable JSON document and refresh the current screen.
- [x] Added regression tests for independent duplicate ID/unique name, category-only export, cross-store import, ID collision safety, name conflict safety, and unsupported schema rejection.
- [ ] Windows runtime acceptance remains outstanding: launch both search screens, verify all profile buttons, export/import between app instances, check skipped conflicts and verify no player/save contents are included in the JSON.
- The branch head at this checkpoint is the tested application commit. Main remains untouched at 2051bbee042fb8e7203b7855e833e391ccee9ecb.
- Continue the complete roadmap as one integrated programme; this feature tranche is not the full product release.
