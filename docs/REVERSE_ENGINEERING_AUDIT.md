# FM Atlas — Reverse-Engineering Audit & Implementation Decisions

**Date:** 2026-11-13  
**Working branch:** overhaul/monolith-full-platform  
**Audited application:** recovered R7.2.0 source under application/R7.2.0/FM-Atlas-R7.2.0-Work/  
**Scope:** static inspection of Atlas source, user-supplied packages, public repositories and official FM27 product information. This is an evidence register, not a claim that every game structure has been reconstructed or runtime acceptance has passed.

## 1. Executive finding

FM Atlas is not a blank application. R7.2.0 already contains a sizeable WPF/SQLite product with player search, save and live identity imports, attribute/statistic search, Value Finder, role scoring, club/staff pages, shortlists, a local editor, a guarded live-memory writer, graphics resolution and diagnostics.

The principal problem is that the application has grown around a very large code-behind shell and a partially integrated feature set. The overhaul must preserve existing data/search paths while turning them into one coherent product with consistent design tokens, typed data provenance, reusable workspaces, explicit feature availability and test evidence.

The strongest reverse-engineering lessons are safety and validation patterns, not copied source code. Atlas will use independently implemented C# services and the existing SQLite model. GPL repositories are research references only unless a deliberate licensing decision is made.

## 2. Atlas source recovery and architecture map

### 2.1 Recovery evidence

- Recovered source lives under application/R7.2.0/FM-Atlas-R7.2.0-Work/.
- The tracked application import has 106 files. Generated build and obj artefacts were not imported.
- Repaired v2 and the later updated.zip were compared locally: 102 common source files were byte-identical; the eight changed source files' Git blob IDs match the corresponding files in the recovery branch.
- The user-supplied Windows Release build log reports 0 errors and 15 warnings using .NET SDK 8.0.427 on Windows 10.0.19045. This proves compilation on that machine, not successful application launch or feature behaviour.
- Source-import commit: 6069c20abb8ab85881da1f5c10860de9b2a8e0f1.

### 2.2 Technology and major files

| Area | Current source | Audit finding |
|---|---|---|
| Application | src/FmAtlas/App.xaml(.cs) | .NET 8 WPF; global exception logging exists |
| Main shell/navigation | MainWindow.xaml.cs | 1,973 lines; most UI is code-behind; root XAML is a host grid |
| SQLite/data access | Data/ScoutDatabase.cs | 2,633 lines; schema, imports, searches and edits concentrated in one class |
| Search workspaces | MainWindow.AdvancedSearch.cs | Attribute and season-stat search; up to 60 AND criteria; per-90/minutes path |
| Analytics/mass edit | MainWindow.R7.cs | Analytics and mass-edit UI |
| Value Finder | MainWindow.ValueFinder.cs | Relative ability/potential percentile vs value percentile; not FM's valuation formula |
| Role Lab | MainWindow.RoleLab.cs and Services/RoleFitService.cs | 23 transparent IP/OOP role profiles; Atlas estimates, not FM internal ratings |
| Live discovery/import | Services/LiveDataImporter.cs | Windows process reader and provisional field surveys/import |
| Build profiles | Services/Fm27BuildProfiles.cs | Storefront/build-keyed PE size and timestamp profiles |
| Live writer | Services/LiveMemoryEditService.cs | Narrow CA/PA and visible-player field writer with preview/preflight/read-back/undo paths |
| Save parser adapter | Services/FmsaveBridgeService.cs and tools/Export-FMAtlas-Fmsave.py | Local Python/fmsave bridge to JSON and SQLite snapshot import |
| Graphics | Services/GraphicsResolver.cs | config.xml resolver for faces/logos/flags; Game Pass root not tested |
| Diagnostics | MainWindow.Diagnostics.cs and Services/AtlasLog.cs | Health checks, rotating logs and support ZIP already exist; accuracy/privacy need hardening |

### 2.3 Existing workspace routes

Dashboard, Players, Recruitment, Clubs, Staff, Squad Architect, Transfers, Tactical Lab, Role Lab, Shortlists, Live Editor, Edit History, Atlas Assistant, Settings, Help & Tutorial, Analytics, Stats Search, Attribute Search, Value Finder, Mass Edit, and Tester & Logs.

A named route is not proof of a complete feature. Some advanced capabilities remain partial or fallback pages. Completion requires a real data source, a working action and acceptance evidence.

### 2.4 Existing database coverage

The SQLite schema separates local player data, live identities/facts, clubs/teams/staff, live import runs, save import runs/snapshots, fmsave fact tables, statistical facts, external stat imports, shortlists and edit history. This separation is useful: source type and import run can remain distinct rather than flattening all information into a misleading single-truth row.

Important table families include Players, PlayerAttributes, EditHistory, Shortlists, LiveImportRuns, LivePlayers, LivePlayerFacts, LiveFieldRegistry, LivePlayerHistory, FmsaveImportRuns, FmsavePlayerSnapshots, FmsavePlayerFacts, FmsaveStatFacts and ExternalStatRows.

**Architecture decision:** preserve the current database and add explicit migrations. Do not replace the database or delete old snapshots to simplify new features.

## 3. External reverse-engineering targets

### 3.1 FMLibreEditor — guarded live-editor architecture

**Repository:** https://github.com/tozmoz/FMLibreEditor  
**License:** GPL-3.0; research only, no code copied.  
**Files reviewed:** development notes, editing, draft validation, field-table caching, restore/session state, change history, saved-filter validation and command palette.

Findings:
- Windows uses a separate process and ReadProcessMemory / WriteProcessMemory; it does not patch game code.
- Game Pass and Steam use separate profiles. A profile absence is not permission to write.
- The project describes dynamic discovery/probe for builds without a known profile while retaining read-only defaults until explicit enablement.
- Before writes it checks save/session identity, re-resolves the person, validates type/range and expected original bytes, and reads the field back.
- Its source describes per-field health checks across samples of up to 2,000 people/staff/clubs. Relationship consistency (staff/person, contract ownership, team membership) can force affected field groups to remain read-only.
- Save loading invalidates resolved pointers; a generation counter detects load transitions.
- Typed edit-draft validation is separated from applying mutations, so invalid values can be rejected before a process write.
- Edit history stores old/new values and supports guarded undo. Saved filters are schema-validated rather than treated as executable expressions.

**Atlas decisions:**
1. Introduce a versioned adapter/profile boundary; unknown builds remain read-only.
2. Add field-level evidence/health status; PE image size/timestamp alone never certifies field semantics.
3. Treat every career load as a new session generation and invalidate cached addresses.
4. Keep preview, apply, read-back and undo distinct.
5. Validate typed edits before memory access and use field-specific ranges/encoders.
6. Store searches as schema-validated data.
7. Do not copy GPL source into Atlas.

### 3.2 FMSuperScout — bulk read-only scanning and import UX

**Repository:** https://github.com/mavarobli/FMSuperScout  
**License:** MIT; architectural reference only, not copied.

Files inspected: README, Windows scanner entry point, Windows process/memory reader, dumper, field registry, fmf research notes, value model and test-harness notes.

Findings:
- The Windows scanner is external and opens the game with least-privilege read/query rights.
- It verifies a real Football Manager executable by checking for adjacent fm_Data, not only the process name.
- It has one-shot and watch modes, a named scan mutex, foreground F9 handling, bounded scan coordination and atomic temporary-file-to-final status writes.
- The scanner documents 32 MiB read chunks, a worker ceiling of eight, memory-pressure awareness and reusable buffers.
- It scans live first and can retry from a copy-on-write process snapshot when the live read failure rate is high. Snapshotting is not the default because it costs memory and can slow the game.
- A status file reports state, player/staff counts, error and progress so the UI can explain a scan instead of remaining stuck at loading.
- The scanner is explicitly read-only.
- Snapshots over time can support development/growth and youth-intake analysis, but only if identity linkage is reliable across imports.
- Published speed figures (about 6 seconds for 60k players) are product claims and were not reproduced by this audit.
- Its value-model notes distinguish true stored values from estimates and show why reputation/age/contract signals do not reveal FM's full hidden valuation formula.
- Its fmf research warns against generating opaque archive files from guessed compression/encryption.

**Atlas decisions:**
1. Improve process identity checks, progress reporting, scan cancellation, single-scan coordination and graceful errors.
2. Keep acquisition read-only; writing remains a separate adapter.
3. Build snapshots only after identity and provenance are reliable.
4. Separate market value, asking price, transfer fee and interest estimates.
5. Do not implement an fmf writer from the archive outline; use documented CSV/HTML interchange.

### 3.3 FMSave — direct save-parser architecture

**Repository:** https://github.com/rhiever/fmsave  
**License:** MIT. Preferred parser-provider and validation reference.

Files reviewed: container parser, bounds-checked primitive scanning, status registry, FM27 layout and public validation API. Current reviewed PyPI release is fmsave 0.12.3 (2026-11-13). It registers FM27 build 27.0.0+2486695; other FM27 builds may borrow an available FM27 layout only with unverified status and warnings. The current API also exposes officials, team match statistics and squad-planner tables. Atlas now pins this adapter version and preserves those three datasets in its raw sidecar.

Findings:
- The container reader treats an FM save as a 26-byte header, named Zstandard frames and a trailer directory.
- A file fingerprint is checked again when frames are read, so a save rewritten during import can be rejected rather than parsed against a stale index.
- Decompression has per-frame and whole-save bounds, limiting memory use from malformed or crafted input.
- Primitive reads are bounds-checked and surface explicit parse errors.
- Layout support is versioned; unregistered or incompatible sections must not silently inherit another build's schema.
- Structural validation and semantic verification differ: known_build means a layout is registered, not that every field meaning is confirmed.
- A field-status registry distinguishes verified from unconfirmed fields.
- Strict mode can block when validation gates fail; permissive mode can retain partial sections with warnings.
- Save-local IDs may be reused; persistent unique IDs are better cross-import match keys.
- The public validation report is designed around structural counts/rates rather than career names or UIDs.

**Atlas decisions:**
1. Keep FMSave read-only and run it through a bounded, cancellable adapter.
2. Store validation report, build ID, source timestamp, warnings and field status with each snapshot.
3. Import partial sections only with explicit coverage and warnings.
4. Never fill missing values with zero.
5. Reject a save that changes during reading; recommend a copy.
6. Keep save-local and global IDs distinct.
7. Treat build registration as format compatibility evidence, not semantic truth.

### 3.4 FM Player Analyzer 2.2.3 — scouting, IP/OOP roles and squad optimisation

**User-supplied archive:** FM-Player-Analyzer-2.2.3(1).zip  
**License:** GPL-3.0 at project level. Its FM27 reader attributes container/player/person layout work to MIT-licensed FMSave. No GPL implementation or asset is copied into Atlas.

Static package inspection found a Qt/C++ desktop application with a separate core layer:
- Fm27Save, Fm27SaveImport, ImportPipeline, ImportSchema, CsvReader and HtmlImporter;
- DwrsEngine, RoleAnalysis, RoleAssignment, TacticExplorer, SquadBuilder and MinCostFlow;
- Development, GapAnalysis, SigningAdvisor, Registration and NationalCallup;
- GraphicsIndex, PlayerStore, ProfileStore, ImportHistory and Freshness;
- pages for profiles/comparison, Best XI, B-team, roles, squad matrix, registration, training/development and national squads.

Findings:
- Save parsing is separate from import transformation and visibility policy.
- FM27 import has a separate knowledge/masking layer. It omits unknown/unscouted data, applies ranges for partially scouted attributes and excludes hidden ability/personality fields from scouting tables.
- Save-local UIDs, database unique IDs and person indexes are carried separately.
- Squad optimisation is a constrained assignment problem; it includes min-cost-flow rather than greedily filling each slot independently.
- In-possession and out-of-possession roles remain separate through analysis and manual assignment.
- Registration requirements are league-specific.
- Player history, freshness and schema migration are first-class concerns.

**Atlas decisions:**
1. Keep parser/provider separate from import and visibility policy.
2. Represent scout knowledge and masked/ranged values explicitly; do not use raw hidden values in a scouting view.
3. Preserve both tactical phases in role data.
4. Implement constrained Best XI assignment with brute-force known-answer tests for small synthetic squads.
5. Model registration as versioned competition rules.
6. Keep imported history and user overrides separate.
7. Do not copy GPL implementation code or assets.

### 3.5 barak14/fm-editor — field-level calibration and undo

**Repository:** https://github.com/barak14/fm-editor  
**License:** GPL-3.0; research only.

Its FM27 layout document explicitly separates fields witnessed on FM27 from offsets inherited from FM26. It records exact-build/platform evidence, labelled examples, field widths, pointer paths and unresolved meanings. Undo entries retain raw old/new bytes and optional policy guards.

**Atlas decision:** maintain a field-level evidence register: field key, build, storefront, record type, type/width, source, witness count, range distribution, cross-field consistency, status and read/write policy. A public offset is a research lead, not permission to edit.


### 3.7 Talent Seer 27.1.2 — scouting workflow and rating methodology

**User-supplied archive:** TalentSeer-27.1.2-win-x64(1).zip  
**Package contents:** README, self-contained Windows executable and third-party notices. No source code or explicit open-source licence was included in the archive. The executable was not run or decompiled; the guide was inspected as product documentation only.

Documented capabilities:
- Player and staff search with saved profiles, role/position filters, multi-select, shortlist, comparisons, collapsible sidebars and virtualised profile workflows.
- 15 player positions and 79 tactical roles; a position/role rating is Talent Seer's own weighted attribute model, not the FM engine's star rating.
- Editable rating profiles: Key attributes weighted ×3, Important ×2, Useful ×1, then adjusted by position familiarity (Natural 1.00, Accomplished 0.95, Competent 0.85, Unconvincing 0.70, Awkward 0.50, Less 0.30). Profiles can be copied and changed and reused consistently across player search, profiles, comparisons and squad selection.
- Potential projection based on a save-trained CA model, a stated average error of about 1.5 CA points, age/position-specific growth assumptions and attribute-CA contribution. This is a product-described estimate, not an independently validated formula and not FM's own forecast.
- A development tracker captures CA, PA and value at each in-game date and keeps the newest 40 snapshots.
- Squad planner supports formation/role slots, best combined XI, manual XI, backups, depth chart and a replacement finder with age, PA, value, wage and contract filters.
- Staff search includes job, coaching/training ratings, FM role preferences and training-area comparisons.
- Club centre exposes club strength, player and staff wage bill, squad value, injuries, youth, contract expiry, training coaches and a multi-year squad planner.
- Bulk edit offers Set/Add/Scale with a full preview and undo; the guide says edits are only applied after explicit confirmation.
- The product documentation says it is free, collects no usage data and optionally checks itch.io daily for updates. This was not independently network-tested.

**Atlas decisions:**
1. Build a 79-role catalogue only after role names, duty, phase and positional eligibility are grounded in a licensed dataset or verified FM27 data source. Do not copy the proprietary rating model.
2. Allow user-owned role-weight profiles, clearly labelled Atlas ratings, with a traceable breakdown of key/important/useful attribute weights and position familiarity multipliers.
3. Implement squad XI assignment as a global constrained optimisation problem, not a greedy per-slot pick. Validate small squad cases against brute-force enumeration.
4. Add a multi-year squad plan, replacement finder and club financial/contract dashboard using fields that are actually present in the save.
5. Add CA/PA/value snapshots and development comparison with freshness and identity provenance.
6. Treat Talent Seer's projection/outlook weights and performance claims as hypotheses, not verified Football Manager mechanics.
7. Do not incorporate the closed executable, unlicensed assets or its internal code.

### 3.8 SaveMyTime — native training workflow plugin

**User-supplied archive:** SaveMyTime(1).zip  
**Package contents:** SaveMyTime.dll and two opaque .fmf training schedule templates. The DLL's managed metadata and strings were inspected without execution; the plugin source and licence were not included.

Static binary evidence:
- The plugin identifies itself as a BepInEx Unity IL2CPP component and refers to the FM27 game date, current manager and managed club.
- It distinguishes actual native training calendar references, manager identity, team database identity, schedule IDs and week start dates.
- Strings show preflight states for game/career changes, native ownership/control, training access, calendar identity, complete editable weeks and stable team identity.
- It describes reading native schedule directories and training library metadata on Unity's main thread, checking schedule entries and seven-day template structure, and reconciling already-dispatched actions rather than blindly resending.
- It contains separate observation/import/apply/revert states and checks for actual readback. Some messages explicitly say schedule contents are unverified or a native readback remains unresolved.
- The bundled .fmf templates are opaque FM resources and were not decoded or redistributed.

**Atlas decisions:**
1. Keep training calendar/schedule inspection as a read-only Match/Training Lab data source until stable team, manager, week, and calendar identities can be independently validated.
2. Model training plans as structured Atlas-local drafts with an explicit preview; do not assume a file or plugin event was successfully applied because it was dispatched.
3. Track dispatch, acknowledgement and actual in-game readback as separate states.
4. Do not introduce the plugin or its opaque .fmf files as Atlas dependencies or redistribute them.
5. Any future native training integration must remain separate from the live memory writer certification gate and require a disposable offline save.

### 3.9 FM Dossier and Teknik Oda — limits of available package inspection

**User-supplied files:** FM-Dossier_1.15.4_x64-setup(1).exe, TeknikOda-Kurulum-1.0.9(1).exe.

The installer binaries were not launched. Static PE/string inspection identified FM Dossier installer metadata and a generic Inno Setup/Delphi installer footprint for Teknik Oda, but did not provide enough reliable evidence about internal application features, data models, memory layouts, FM27 build compatibility or licences.

**Atlas decision:** keep both as feature/workflow research leads only. Do not claim either has been reverse engineered. A lawful source package, public technical documentation, or user-provided screenshots/workflow traces would be needed to deepen functional comparison. No installer, binary or plugin dependency is being added to Atlas.

### 3.6 FM View Exporter and fmf files

FM View Exporter is a GPL-3.0 BepInEx route for exporting visible in-game tables to CSV/HTML. It is useful as an optional import/known-answer fixture, not a replacement for a direct save parser or universal Game Pass connector. FM Player Analyzer's bundled fmf templates are separate-project assets; Atlas must not ship them.

FMSuperScout's archive research found a Zstandard-compressed index and payloads that could not be reproduced with ordinary codecs. The safe choice is readable CSV/HTML import and no guessed archive writer.


### 3.10 Jackhamerp24 FM27 Editor — broad coverage and calibration benchmark

**Repository:** https://github.com/Jackhamerp24/Football-Manager-27-Editor  
**License:** GPL-3.0; no code or offset profiles copied into Atlas.  
**Files reviewed:** README and the public Game Pass offset profile for build 27.0.1+2494687. Profile file reviewed as a research artifact only; no offsets imported into Atlas.

The README documents a much broader live-edit surface than Atlas currently implements:
- Players: attributes, positions, personality, CA/PA, reputation, height, DOB, nationality, work permits, injuries, bans, morale/unhappiness, national call-ups, languages, relationships, career plans, preferred moves, promises and future contracts.
- Transfers: transfer, loans and future transfers with financial terms, instalments, appearance/goal payments, buy-back and sell-on terms, wage contribution and friendlies; swap, release, captain and vice-captain actions.
- Staff: personal/ability attributes, contract, licence, caps/goals, role ratings, preferred jobs, knowledge, formations and tendencies.
- Clubs and world entities: finance, debt, facilities, stadium, cities, nations, competitions, awards, referees and injury types.
- Utilities: mass edit, presets, freezer, export, favorites and hook tools.
- Its README states a 550-value calibration set compared with FMRTE and Football Manager UI, and 71 reader features.

The README claims Windows Steam build 27.0.1+2503222 and Xbox/Game Pass 27.0.1+2494687 were tested. The public profile reviewed includes a separate Game Pass module fingerprint and field maps. These are project-maintainer claims and a third-party profile, not independently reproduced by Atlas. The project targets .NET 10 and GPL-3.0; Atlas remains .NET 8 and must not copy its implementation or profile data without a deliberate compatible licensing decision.

**Atlas decisions:**
1. Add all player/staff/club/nation/competition/stadium/award/referee field families to the entity/feature traceability matrix, not only player attributes.
2. Design the editor UX around multi-entity profiles, tabs, favorites, batch preview, transaction-level history and one-field rollback, but keep writes disabled until Atlas's own evidence passes.
3. Build a field-calibration harness that compares imported/observed values against labelled screenshots or manual known-answer fixtures; don't treat the README's 550-value figure as our validation.
4. Extend coverage tests to women/men records, staff/player shared identity, nations, competitions, cities and stadiums. The README says women’s football is not supported/tested; do not assume all entity classes are covered.
5. Keep build profiles, PE fingerprint and field evidence as separate records; a third-party JSON offset profile is not a certified Atlas mapping.

### 3.11 SiRRAHx FM27 Live Editor — Game Pass-first workflows and research hooks

**Repository:** https://github.com/SiRRAHx/fm27-live-editor  
**License:** No explicit top-level licence was established in the reviewed package metadata. Do not copy its code or redistribute its binaries.  
**Files reviewed:** README, optional BepInEx plugin and research observer implementation.

Findings:
- Its README describes Game Pass/Microsoft Store as tested and Steam/Epic as supported but not yet tester-confirmed in the published README.
- The desktop live editor runs separately; the optional BepInEx add-on is only an in-game launcher button. The README says editing works without the plugin.
- The optional add-on opens the desktop app through a shortcut written under Documents, an interesting workaround for Game Pass sandbox boundaries.
- Its research mode is explicitly off unless a marker file is present. When enabled it observes FM's game-event dispatch through a Harmony postfix, retains a bounded recent event list and writes event traces without altering the original event dispatch.
- The project describes dynamic discovery rather than requiring a fixed hard-coded offset profile for every build; it states that when something cannot be identified confidently, editing remains off and logs explain why.
- The README is explicit that the product is early access and advises backing up saves.

**Atlas decisions:**
1. Add an optional in-game companion/launcher as a separate future module; keep the desktop application independently usable.
2. A Documents-based deep-link/shortcut handoff is a possible Game Pass integration pattern; it does not itself establish that process memory or game files are accessible.
3. Consider an explicit opt-in, read-only event-observation research adapter only after legal, reliability and mod-loader compatibility review. Never install a BepInEx loader silently.
4. Keep research instrumentation off by default, bounded, locally stored and visibly disclosed.
5. Keep unknown-build or unknown-field operation read-only; logs must explain unavailable capabilities without asserting a successful connection.

### 3.12 Integration of the original R7 master specification and extended backlog

The recovered source package contains the user's earlier full R7 master specification, extended research backlog and research/licensing register. These remain preserved in the source tree. The integrated overhaul adds these backlog items to traceability:
- Live connection, world database explorer, all entity classes, profile/compare, leaderboards and custom statistic builder.
- Saved recruitment profiles, hard constraints vs weighted preferences, match explanations, replacement search and affordability vs access estimates.
- Player and staff intelligence, 2–5-way comparison, cohort ranks, age curves, youth intake, loan development and snapshot freshness.
- Senior/reserve/youth/national squad coverage, positional depth, Best XI/bench, role DNA, formation comparison and custom tactics.
- Club finance/headroom/wage bill, stadium/facilities, competition rules, eligibility/national call-up and club/competition comparisons.
- Atlas Assistant local/offline by default, sourced local answers, opt-in external providers and no default career-data transmission.
- Optional FaceDrop/image provider and duplicate-ID/corrupt-file diagnostics; no bundling of third-party images/packs.
- Optional BepInEx companion and mod compatibility checker, without silently installing/deleting loaders or plugins.
- Pre-game editor/database-file discovery and format validation as separate from live-career editing.
- Player similarity split into "level similarity" and "style similarity"; both require explainable inputs.
- Staff finder, training-area comparison, staff succession, national team and registration tools.
- Match Lab timeline, opponent reports, post-match reports, shots/maps only when real coordinates are present and clear sample-size metadata.
- Saved views, column profiles, custom leaderboards and dossier/squad/match exports; local JSON/Markdown AI prompt export only by user action.
- Privacy/streaming mode and support diagnostics with opt-in personal information.

## 4. FM27 Tactical Effectiveness boundary

Official SI material describes four Tactical Effectiveness areas: Tactical Understanding, Player Ability, Player Buy-In and Next Match Information. It also describes Tactical Complexity as the volume of team, player and opposition instructions, with a trade-off between complexity and squad ability.

Official material does not publish the full hidden weighting/formula. Therefore:
- Atlas may organise the four visible factors and show which user-provided or save-derived facts support each.
- Atlas may calculate transparent squad-fit indicators, instruction counts and role-coverage summaries.
- Atlas must not label Atlas output as FM's internal Tactical Effectiveness percentage unless that exact value is sourced from FM.
- Match Lab must distinguish observed match facts from Atlas calculations and missing event-level coverage.

## 5. Live-editor safety assessment

R7.2.0 contains a writer that:
- matches a known Windows module profile by PE size and timestamp;
- resolves a player using UID and expected name;
- stages expected original bytes and target values;
- performs whole-batch preflight;
- rechecks session generation before writes;
- reads values back;
- attempts guarded rollback and supports guarded undo;
- rejects hidden attributes and unsupported fields.

**Critical gap:** profile matching does not prove each field's byte mapping/encoding for the user's current Game Pass binary. The player identity layout, save-load generation pointer and each proposed field offset require independent field-level verification. Partial rollback cannot be guaranteed if the game session changes mid-operation.

**Decision:** local Atlas edits remain available; live memory Apply/Undo must be disabled by a separate build-wide certification gate until copied-save identity checks, field-level witnesses and disposable offline read/write/readback/undo acceptance have passed. There must be no manual escape hatch or inference from an exact profile match.

## 6. Feature implementation map

| Feature | Existing Atlas implementation | Remaining integrated work |
|---|---|---|
| Shell/navigation | Code-built shell; 21 routes | Shared controls, remove fallback/dead routes |
| MONOLITH | R7 palette; partial style helpers | Token resolver and Obsidian/Warm Light foundation added on overhaul; migrate remaining hardcoded surfaces |
| Advanced Search | Attribute/stat searches, up to 60 AND criteria | Field registry, include/exclude, grouped AND/OR, saved profiles, custom columns, explanations |
| Player profiles | Identity, attributes, facts and stats | Player DNA, provenance, freshness, development history and comparisons |
| Value Finder | Relative CA/PA vs value percentile | Explainable cohorts/sample size; separate value/asking price/wage/fee |
| Role Lab | 23 weighted IP/OOP profiles | Broader verified FM27 role data, transparent weights and both phases |
| Recruitment | Recruitment, Squad Architect and Shortlists | Scouting Matrix, squad gaps, succession, replacement players and constrained Best XI |
| Statistics | Dynamic season-stat catalogue, facts, per-90 | Coverage-aware percentiles, sample thresholds, cohort definitions, match metrics |
| Tactical Lab | Basic role/tactical analysis | Tactical Effectiveness panels, instruction complexity, opposition and Match Lab |
| Development | Some profile/stat facts | Snapshot deltas, growth and intake radar with stable identity |
| Save Inspector | FMSave adapter and JSON import | Build-specific validation, per-section coverage and strict/partial import preview |
| Local editor | CA/PA and attributes saved in Atlas DB | Bulk transactions, field registry, preview, undo/redo and persistence checks |
| Live editor | Narrow writer in source | Certification gate locked; read-only first |
| Graphics | config.xml resolver | Game Pass root validation, collision/source precedence, skin library, cache invalidation |
| Diagnostics | Rotating log, Tester & Logs, report and ZIP | Bundle ID/manifest, content preview, rotated logs, runtime facts and accurate status language |
| Mod workbench | Research notes and scripts | Licence/version/path catalogue, safe static inspection; no automatic execution |
| Packaging | Build script and installer source | Reproducible publish, upgrade/rollback, hashes and release acceptance |

## 7. Licence and reuse register

| Project | Licence observed | Atlas policy |
|---|---|---|
| FMSuperScout | MIT | Architecture reference; preserve notices if a dependency is ever chosen |
| FMSave | MIT | Optional external parser/reference; pin version and preserve notices if distributed |
| FMLibreEditor | GPL-3.0 | No source copying absent a deliberate GPL distribution decision |
| barak14/fm-editor | GPL-3.0 | Research only; independent implementation |
| FM Player Analyzer | GPL-3.0 | Research only; independent implementation |
| FM View Exporter | GPL-3.0 | Optional CSV/HTML data source; no source/assets copied |
| Commercial tools | Proprietary or unclear | Workflow benchmarks only; no protected code/assets reused |

## 8. Immediate implementation priorities

1. Preserve the recovered source and database; use explicit schema migrations.
2. Add MONOLITH semantic colours, typography and shared WPF control factories.
3. Harden diagnostics: bundle ID, content preview, privacy scope, rotated logs, runtime facts and no false "live editor ready" status.
4. Add universal field metadata/status and saved-search persistence.
5. Refactor live acquisition into a read-only connector with bounded reads, progress, cancellation, atomic status and stable identity.
6. Add fixture-driven save/CSV import tests before widening statistic coverage.
7. Build Player DNA, Scouting Matrix, Moneyball, Tactical Intelligence and Match Lab from verified data contracts.
8. Add competition registration, club/staff and graphics workspaces.
9. Gate live Apply/Undo behind build-wide certification and field-level evidence.

## 9. Acceptance tests

- Import: known-good fixture counts, corrupt/truncated save, changed-during-read save, unknown build, partially unsupported section, missing values and duplicate IDs.
- Search: one/two/60 criteria, AND/OR truth tables, include/exclude, missing-value semantics, numeric/date boundaries and saved-search roundtrip.
- Statistics: known-answer per-90, minutes threshold, sample exclusion, season/competition separation and missing fields.
- Role/squad: IP/OOP assignments; compare optimiser against brute force for small synthetic squads; deterministic tie-breaking.
- Moneyball: raw vs per-90; cohort and tie handling; separate value/wage/fee; expose sample size.
- Diagnostics: no save/database/exported-player files in ZIP; redaction; bundle ID consistency; rotated logs; hashes and preview.
- Graphics: config variants, relative/absolute paths, traversal attempts, missing image, duplicate UID, cache invalidation.
- Live editor: disabled by default and by certification gate; unsupported builds/fields rejected; changed baseline and stale session rejected; apply/readback/undo on disposable offline save only after witnesses.
- Release: Windows build, launch, navigation smoke, high-DPI, both themes, backup and rollback of existing database.

## 10. Explicit unknowns

- Exact field mapping for the user's current FM27 Game Pass binary.
- Whether the user's installed Game Pass package matches any published profile today.
- FMSave section coverage for the exact local save build.
- Runtime quality of every R7 statistic and role profile against labelled fixtures.
- Actual Game Pass graphics root and installed config variants.
- Runtime behaviour and migration of the recovered R7.2.0 app.
- Whether every visible workspace route is fully implemented beyond its UI builder.

Do not resolve these unknowns by assuming a public tool's advertised support applies to the user's installation.
