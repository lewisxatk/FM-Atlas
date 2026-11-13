# FM Atlas LIVE — Extended Research & Feature Backlog
**Revision:** 1.1  
**Research date:** 13 November 2026  
**Purpose:** Additional benchmark research beyond the initial R0.1 specification. This document is part of the FMATLAS project record and should be read alongside `FM-ATLAS-LIVE-MASTER-SPECIFICATION.md`.

> Research rule: a feature appearing in another product is a design lead, not proof that FM exposes it reliably on every storefront. Every Atlas feature must have a source, a confidence label, a per-build compatibility check and an acceptance test. Do not copy proprietary code or bundle third-party packs/tools without checking their licences.

---

## 0.7.1 implementation checkpoint (13 November 2026)

The following items were added to the native R7.0.2 source for this 7.1.0 integration build:

| Requirement | 7.1.0 state | Notes |
|---|---|---|
| Local rolling log and runtime exception capture | Implemented in source | `%LOCALAPPDATA%\FmAtlas\logs`; redacted user paths |
| Tester / support diagnostics | Implemented in source | Copy report, copy recent log, export ZIP; default excludes save and database |
| Derived season per-90 analytics | Implemented in source, requires data validation | Requires numeric stat and recognised minutes field; 450-minute default floor |
| Analytics CSV export | Implemented in source | Exports currently displayed search results |
| Transparent IP/OOP role-fit scoring | Initial implementation | 23 profiles, weighted 1–20 values, 50% coverage minimum, CSV; not the SI formula |
| Local editor safety gate | Implemented in source | OFF by default on each launch; local-only changes |
| Local mass edit Set / Add / Scale | Implemented in source | Add/Scale skip missing values; clamped to 0–20; individual audit rows |
| Guarded local Undo | Implemented in source | Refuses to reverse a history item if current value no longer matches |
| Atomic local CA/PA save | Implemented in source | Both ability values and history entries commit in one SQLite transaction |
| Steam/Game Pass memory profiles and live writes | Not implemented / not verified | Must remain read-only until independent build-specific tests pass |
| Full stats coverage, match lab, registration, staff development and graphics manager | Backlog | Requires further typed data mappings and source-specific tests |

This is a code integration checkpoint, not a statement that the Windows application has compiled or passed in-game acceptance tests. Use `docs/R7/IMPLEMENTATION-STATUS.md` for current verified boundaries and `docs/R7.1.0-ACCEPTANCE-AND-TEST-PLAN.md` for the test sequence.

## 1. Newly confirmed / expanded benchmark set

| Tool / source | What it adds to the research | Platform / confidence notes | Atlas decision |
|---|---|---|---|
| **FM Player Analyzer 2.2.3** (user supplied source ZIP) | Role-fit model; dual in-possession/out-of-possession tactics; optimal Best XI + B-team; gap analysis; replacement impact; tactic comparison; talent scoring; development tracking; player similarity; squad registration; staff and national-team tools; user-defined role/tactic definitions. | Its 2.2.3 changelog and source were inspected locally. Direct FM27 save reading is read-only; export import uses FM View Exporter. | Highest-value analysis benchmark. Build Atlas-native role score, two-phase XI solver, replacement and registration analysis. Keep score formulas editable and explainable. |
| **Talent Seer 27.1.2** (user supplied installer and README) | 79 tactical roles (40 IP / 39 OOP), potential projections, development outlook, CA recommendation, staff job preference, coaching-area scores, injury information, retirement plans, homegrown links, languages, morale, contracts/clauses, similar players, club centre and three-season squad planner, bulk editing and undo. | Windows; README says tested Steam. Do not assume Game Pass until tested. Uploaded README reviewed. | Add all verified features to backlog; use estimates only when labelled. Make all editor actions staged, reversible and testable. |
| **FMSuperScout 1.7.3** | Fast full-save read of CA/PA, hidden and personality attributes, position ratings, value, contracts, staff, snapshots, growth and intake radar. | Windows scanner is read-only; FM26/27. Project says ~6 seconds for 60k and ~17 seconds for 635k-person large save. Benchmark claim, not Atlas performance target. | Benchmark the scanner approach and parallel/chunked reads. Atlas connector should remain independent; snapshots/intake radar are required. |
| **FMLibreEditor** | Separate Windows Steam and Game Pass FM27 build profiles; player/staff/club editor; guarded writes; read-back; per-field health checks; change history and Undo; graphics-folder support. | Windows Game Pass and Steam advertised; new/unverified builds fall back to read-only. GPL-3.0. | Primary technical safety reference for the connector and edit validation. Do not copy GPL code unless Atlas licensing is deliberately made compatible. |
| **barak14/fm-editor** | Deep entity and field discovery: players, staff, clubs, nations, competitions, awards, officials, stadiums, cities and injury types; future transfers; promises; loan terms; kit/finance/rule fields; profile checks and Undo. | GPL-3.0. Its own documentation distinguishes verified FM27 mappings from inherited/unverified FM26 offsets. Has separate Game Pass/Steam profiles in current source. | Use as a research map and validation checklist; every field still needs independent witnesses. |
| **Jackhamerp24 FM27 Editor** | Broad editor coverage, transfer/loan terms, future transfers, promises, national call-ups, tactical familiarity, squad presets, freezer, mass edit, export/import, favourites and many entity types. | Project claims Steam + Game Pass. Its README explicitly says women's football is not supported/tested. | Add entity-browser, transfer, mass-edit and preset requirements. Add gender/entity coverage tests before claiming completeness. |
| **Touchline Forge 27 0.2.1** | Five editor modules (players, staff, clubs, competitions, nations); staged/revert edits; tabs for multiple open profiles; lazy detail loading; faster filtered searches; custom attribute colours. | Windows, tested Steam and Xbox/PC Game Pass, updated for 27.0.1+2494687. Early test software. | Add multi-profile tabs, preserved search state, lazy profile detail and attribute colour preferences. |
| **SiRRAHx FM27 Live Editor 0.3.14** | Game Pass-first live editor; transfer and loan operations; optional in-game button; logging; dynamic locating instead of relying solely on fixed offsets. | Game Pass tested; Steam/Epic supported but not yet tester-confirmed in the cited project page. BepInEx is optional for the button. | Add optional in-game companion only after desktop connector is stable. Use structured log/diagnostic handoff. |
| **FMRTE 27** | Broad player/staff/club/competition/nation/stadium tools; season statistics; search in plain language; reusable presets; face/logo and facepack controls; AI assistant; edit history and undo. | Current product documentation distinguishes free scouting from licensed saving. Storefront support must be checked against the current release. User's supplied archive is build 2 and is a UI/reference snapshot, not proof of current build compatibility. | Benchmark the edit history, profile presentation, stats pages and graphic resolution. Do not copy proprietary internals. |
| **Genie Scout 27 Beta 4 / Build 2705** | General, positional and role ratings; potential-attribute projection; progress rate and probability of development; similar-player search; staff/club search; shortlists; player history; GS Stats; in-game scout and face/kit support. | 10 Nov 2026 release notes list FM27 27.0.1 and Steam, Epic, Xbox/PC Game Pass and Console support. Some features are g-edition-only. | Add role/peak comparison and similar-player workflows. Keep projected values explicitly labelled as Atlas estimates. |
| **FM Dossier** | Three knowledge/decision lenses: Scout (fog-of-war), Full (ground truth) and Moneyball (value vs production); player dossiers; role-fit radar; squad depth; nation eligibility pools; age curve; AI-ready exports. | Current public page says FM27 works, tested on Steam; Epic/MS Store/Game Pass are checked on each read but not yet tested. Supplied installer filename is 1.15.4; website's displayed older version should not be treated as the same build. | Add optional Scout/Full knowledge view and Moneyball value lens. Preserve scouted ranges and provenance. |
| **Teknik Oda — Toby 1.0.9** | Pre-match opponent analysis; live match shot-map/zones/player-number analysis; post-match review; player-profile questions; offer advice; training schedule suggestions; staff search; squad/tactical assistant; report cards tracking 15-minute outcomes. | Open beta, read-only. Author says Steam tested; Game Pass plugin/reader has user-reported success, not full author verification. | Add Match Lab in stages: start with saved match/shot data and post-match reports; only add live commentary after data source and sample-size checks. |
| **FM View Exporter** | F9 CSV/HTML extraction of the visible FM table, exact game headers, UID and 375-column reference; can scroll through long result lists. | BepInEx plugin; source page says Windows Steam. FM only fills roughly first 255 view columns; searches capped around 10,000 results; very large exports can take minutes. Game Pass support not established. | Optional import adapter, not a required data source. Add schema/version detection, F9 import and warning on missing/empty columns. |
| **FMSave** | Read-only parser/browser for save files; players, attributes, CA/PA, contracts, current-season stats, fixtures, tables, competition rules, suspensions, per-match history, training calendars and mentoring groups. | FM27 layout is verified for 27.0.0+2486695; other builds may be unverified and section readers should refuse changed schemas. The current 27.0.1 build 2494687 therefore needs a fresh parser validation before we rely on its season/match/stat sections. | Use as the disk-save data-source reference. Build a versioned parser layer and fail closed on unsupported sections. |
| **FMPT Chief Analyst / FMPortugal tooling** | Another current editor/scouting direction; benchmark its search and editing workflows once a reproducible current build is available. | Public FMPortugal coverage identifies it as a Windows live scout/editor; no independent build-level field coverage has been established in this pass. | Add to future hands-on comparison, not a dependency. |
| **XML Configurator 2.2.0** | Validates graphics-pack files and `config.xml` entries, including faces, logos, kits, flags, trophies, balls, backgrounds and stadiums; reports bad filenames and supports undo for batch renames. | Windows desktop utility; offline. | Add Atlas Graphics Diagnostics: missing files, invalid paths, duplicate UID mappings, config parse errors and source-pack conflicts. Do not auto-rewrite configs without a backup and explicit action. |
| **FaceDrop 0.3.1** | Drag/drop or clipboard workflow for applying portraits to players, regens and managers; stores user images in a dedicated folder. | 0.3.1 says FM27 27.0.1; Steam/Windows, BepInEx 6. | Treat as a graphics workflow reference; Atlas should resolve and preview faces, not require FaceDrop to be installed. |
| **Official FM27 Pre-Game Editor** | Pre-save database editing for players, clubs, competition rules, stadiums, awards, finances, kits and custom DB structures. | SEGA support article updated 11 Nov says available on Steam, Epic and Microsoft Store / PC Game Pass. Separate from live in-game editing. | Add a separate pre-save database utility workspace/import checker. Never conflate editor data files with edits to an existing career. |
| **Official FM27 In-Game Editor** | Official live-save editing feature, distinct from the Pre-Game Editor. | Released 10 Nov 2026 according to current FM27 guides; availability/permissions may be fixed at save creation. | Add a help/compatibility panel to identify whether official editing is enabled; do not rely on it for Atlas scouting data. |
| **SaveMyTime (user supplied ZIP)** | Package contains `SaveMyTime.dll` plus two `.fmf` training schedule files. DLL strings reference native training schedule metadata and schedule loading. | Binary plugin only; no source or README was included, so exact behaviour and supported builds are not yet verified. | Keep as optional training-mod compatibility test. Do not make it a required connector or assume its DLL exposes a public API. |

---


### Known current FM27 storefront fingerprints (13 November 2026)

- **Steam:** FM27 27.0.1, Steam Build ID **26577676**.
- **PC Game Pass / Microsoft Store:** package version **0.9.2435.0**.
- These are not interchangeable identifiers. The app must also capture the actual `game_plugin` module fingerprint and profile revision. A package version alone is not sufficient to authorise memory reads or writes.
- FMLibreEditor publishes separate profiles for the Steam build and Game Pass package. Its Game Pass 0.9.2435.0 profile comments state that all ten health checks passed on its test save and that the object layout was unchanged across the 10 November update. Treat this as an external witness, then reproduce our own tests.
- The official FM27 Pre-Game Editor is documented by SEGA as available from Steam, Epic and Microsoft Store; the Microsoft Store install also appears in the PC Game Pass library. Keep this separate from the paid/official In-Game Editor and from third-party live memory editors.

## 2. Feature requirements to add to Atlas

### P0 — Core data and connection (must come before broad claims)
1. **Storefront/build matrix:** separate Steam, PC Game Pass/Microsoft Store and (later) Epic profiles. Record executable, `game_plugin` version/hash, package/build ID, module size and profile revision. No shared offsets by assumption.
2. **Connection health checks:** confirm loaded save (not menu), entity arrays, player/staff/club identities, contract ownership, in-game date and cross-entity relationships. A failed check disables the affected field group and all writes.
3. **Read-only-first safety:** attach/query uses minimal read rights; no editor writes before explicit editor enable. New/unknown build = read-only mode.
4. **Multi-source import pipeline:** live memory, direct save parser, FM View Exporter CSV/HTML, manual CSV/JSON and future match exports. Track source and timestamp per field, not merely per player row.
5. **UID-based identity and merge:** primary UID; fallback identity for unassigned newgens uses name + DOB + nationality + club/context, with ambiguity warning. Never silently merge recycled UIDs; store DOB/nation as continuity witnesses.
6. **Save profiles and isolation:** separate SQLite store/profile per career; in-game date and game build on every snapshot; detect save rewind and avoid mixing old/new timeline.
7. **Partial coverage:** report entity counts, fields missing, unsupported schema sections and any skipped records. Do not imply every world entity was read if arrays/relationships are incomplete.

### P1 — Scouting intelligence and tactical analysis
1. **Role model for both phases:** FM27 roles are separate in-possession and out-of-possession concepts. Store role definitions per game version and separate key/preferred attribute weights for each phase.
2. **Configurable role ratings:** show formula, source attributes, familiarity/position penalty and result. Users may create profiles/weights by tactic or save. Label Atlas scores as derived, not native FM stars.
3. **Optimal XI and B-team solver:** solve the whole assignment, not greedy per-slot picks. FM27 should jointly account for IP and OOP roles; support pinned players, central/side preferences, role weighting, availability and user-controlled playing-time priorities.
4. **Replacement impact:** for each starter/slot, show the next-best replacement and how much the XI loses; allow injury/absence scenarios and depth charts for senior, B-team and youth.
5. **Tactic explorer:** rank formations/tactics by coverage first, then squad quality per line/slot; permit imported FM tactics and user-created Atlas tactics.
6. **Recruitment lift / gap analysis:** simulate adding a target to the squad, recompute optimal XI and quantify improvement by position/role. Show which positional shortages are genuine and which are caused by players being assigned away from their best role.
7. **Similar player search:** separate style similarity (attribute shape) from level similarity (relative quality). Provide age, value, wage, role and potential filters, and explain why each candidate matches.
8. **Talent/development outlook:** PA headroom, age runway, personality and injury factors can feed an *estimated* outlook; expose each component and never call it FM's own probability unless it is read natively.
9. **Staff finder:** searchable staff and unemployed staff; current job vs preferred jobs; staff role suitability; coaching-area score and expected-star mapping; budget/wage filters; compare staff.
10. **Club Centre:** club strength, finances/budget headroom, weekly wage bill, facilities, best staff per coaching area, youth academy/intake, squad contract horizon and position succession plan for the next three seasons.
11. **National-team centre:** eligible player pools by age group, primary/secondary nationality, declaration/retirement/eligibility state and call-up recommendations where verified.
12. **Squad registration assistant:** versioned rule packs by competition/season. Check list size, home-grown/club-trained/nation-trained, U21/U18, non-EU and special wage/loan rules. Clearly say when a league's rules are not modelled.

### P1 — Statistics and Match Lab
1. **FM27 stats schema:** include current native Data Hub/per-90 metrics such as xA/90, dribbles/90, shots-on-target/90, open-play key passes/90, chances-created/90, xG/90, non-penalty xG/90 and key tackles/90 where FM provides them.
2. **Pass breakdown:** short/medium/long completed passes and success rates where data is available.
3. **Defensive action definition:** version the calculation; FM27's out-of-possession defensive actions includes fouls, so do not reuse an old formula without build/version label.
4. **Per-90 and rate safeguards:** per-90 = event total / minutes × 90; percentage metrics use their own denominator; zero-minute/zero-attempt rows are N/A, not zero-percent.
5. **Sample thresholds:** adjustable minimum minutes/appearances (default examples 450/900) before ranking; show the threshold and sample size beside each ranking.
6. **Cohorts and percentiles:** rank within competition, position/role, age band and whole save. Do not compare goalkeeper and outfield metrics on one universal percentile.
7. **Match timeline:** store date, competition, opponent, venue, result, starts/subs/minutes, rating and available match events from saved-game data or exports.
8. **Shot maps / zones:** research the underlying match data and build a match report viewer only after field validation; do not infer shot coordinates from a summary statistic.
9. **Opponent reports:** last-five match form, likely XI, shape, injury/suspension and strengths/weaknesses if source data is present; include source and freshness date.
10. **Post-match report:** what worked, where chances/shots came from, standout players and tactical next steps. All narrative conclusions must point to evidence and note small samples.
11. **Training planner:** calendar-aware training suggestions around fixture congestion, player condition/fatigue, expected load and tactical focus. Suggestions only; no automated schedule writes until supported and tested.
12. **Training history:** capture snapshots and calendar/mentoring data from supported save schemas; distinguish actual sessions from suggested plans.

### P1 — Editor, transactions and history
1. **Global Editor switch:** OFF by default. Turning it ON requires save backup reminder and supported store/build status.
2. **Field-level capability matrix:** every entity field has Read status, Write status, evidence reference, version/store scope, last test and Undo status. Grey out fields not verified for the active store/build.
3. **Stage → preview → apply:** display before/after values and the number of records affected. No writing while editing text fields.
4. **Pre-write identity checks:** PID/session epoch, save identity, module/profile fingerprint, object/UID and relationship checks; confirm the live value still equals the value Atlas read.
5. **Read-back verification:** re-read each write; display each field outcome individually. Partial failure must not appear as full success.
6. **Guarded Undo:** undo only if the current game value still matches Atlas's last written value. If FM or another tool changed it afterward, refuse and explain.
7. **Batch/mass edit:** select a filtered subset; preview all; Set / Add / Scale% where meaningful; cap batches; produce a rollback record per field/entity.
8. **Entity relationship edits:** transfers, loans, future transfers, swaps, staff moves, contracts and club/team list operations require stronger multi-pointer checks and explicit scope. Build after basic scalar edits.
9. **Freeze/freezer feature:** optional advanced mode only; prominent status and exact targets, pause/disconnect/close behaviour, persistence per save and safety tests. Not part of default editor mode.
10. **Edit history:** session and persistent history with timestamp, game date, store/build, save ID, entity UID, field, old/new, action, apply result, verification result and Undo result.
11. **No implicit edit:** opening a profile never arms the editor. A profile's Editor tab or individual Edit button must be explicit.
12. **Save backup integration:** offer a save copy/backup before first write; never overwrite backups; show exact backup location and whether it succeeded.

### P1 — Graphics and user experience
1. **UID graphics resolver:** parse graphics-pack `config.xml`; resolve `graphics/pictures/person/<UID>/portrait`, club crests, nation flags and competition badges, including common `r-<UID>` and normal/higher-resolution variants where configured.
2. **Pack precedence:** show all matches and which pack wins; allow a user-selected preferred pack; support cache invalidation if `config.xml` or source images change.
3. **Missing/corrupt graphics audit:** report unresolved IDs, bad XML, nonexistent `from` paths, duplicate UID targets and corrupt/unreadable image files.
4. **Image fallback:** use a bundled neutral placeholder when no valid graphic exists; never show the wrong player's face because of a recycled UID.
5. **Profile tabs:** multiple open player/staff/club profiles; keep scouting filters and scroll position intact when returning to results.
6. **Fast search:** load rows first; fetch expensive full profiles only when opened; keep a responsive UI with very large saves.
7. **Saved views and columns:** named filter presets, column layouts, views by job/position/competition and export. Global search and Ctrl+K.
8. **Local report export:** player dossiers, squad reports, match reports and clean JSON/Markdown for optional AI prompts. External AI is opt-in; never silently upload save data.
9. **Diagnostics button:** copy/export logs with game build, store, module fingerprint, Atlas version, connection steps, profile health checks and error stack. Redact Windows username, save path, account identifiers and player names by default; user may opt into a richer log.
10. **Streaming/privacy mode:** hide manager name, club finances or selected personal labels in screenshots/exports.

### P2 — optional companion / mod ecosystem
1. **In-game Atlas companion:** small top-bar/profile actions via optional BepInEx plugin. Desktop app must work without it.
2. **Mod compatibility checker:** detect BepInEx installation and installed plugins; show potential conflicts, version warnings and how to launch vanilla. Never install, remove or update third-party mods silently.
3. **Theme/skin compatibility research:** FM Scout skin/panel modifications can change on-screen layouts; Atlas must read data sources, not depend on screen coordinates or a particular skin.
4. **Match engine experiments:** treat `simatch.fmf` edits as a separate sandbox project; record original file hash, game version, mod hash, test save and controlled match results. Never auto-install a match-engine mod into the user's live game.
5. **Pre-Game Editor companion:** validate `.fmf` editor-data files and provide new-save instructions; this is separate from live save editing.
6. **Training schedules:** optional integration with user-supplied schedule files/mods (including SaveMyTime) only after plugin format, permissions and build compatibility have been understood.

---

## 3. Recommended architecture changes

- **Connector layer:** `SteamConnector`, `GamePassConnector`, future `EpicConnector`; each selects a versioned profile by exact binary fingerprints and runs the same logical validation suite.
- **Readers:** `MemoryReader`, `SaveFileReader`, `ViewExportReader`, `MatchDataReader`. Each returns typed records with field-level provenance (`source`, `build`, `timestamp`, `confidence`, `observed/derived/estimated`).
- **Normalised domain model:** player, staff, club, team, nation, competition, stadium, award, referee, fixture, match, appearance, season-stat, contract, injury, transfer, training event and graphics reference.
- **Local storage:** SQLite; save-specific database/profile, source snapshots and import lineage; indexes on UID, name, club, competition, date, role and common filter fields.
- **Analysis engine:** versioned role definitions/weights; two-phase squad solver; replacement/gap analysis; similarity engine; transfer/value analysis; registration rules; percentiles and statistical rates.
- **Editor engine:** capability/evidence registry, transactional staging, compare-before-write, read-back and guarded Undo. Keep it isolated from the reader so a failure in one never corrupts the other.
- **Presentation:** desktop client; optional local API shared by desktop and in-game companion; logs and diagnostics accessible from an explicit Tester drawer.

### Licence / reuse caution
- FMSuperScout declares MIT.
- FM Player Analyzer and FM-View-Exporter declare GPL-3.0.
- FMLibreEditor and barak14/fm-editor declare GPL-3.0.
- Reading their public code to learn formats and test methods is not the same as copying their implementation. Any direct code reuse needs a licence decision and attribution/source obligations review. Prefer independent implementation unless the project deliberately chooses compatible licensing.

---

## 4. Updated test matrix (must be completed incrementally)

| Test ID | Test | Acceptance condition | Steam | Game Pass |
|---|---|---|---|---|
| CON-001 | Detect correct process/store | Atlas identifies actual storefront and exact build; no guess | Pending | Pending |
| CON-002 | Main menu vs loaded career | No player scan until a real save is loaded | Pending | Pending |
| CON-003 | Reconnect after save switch | Old career data is cleared or isolated; correct new save loaded | Pending | Pending |
| CON-004 | Unknown game update | Read-only mode; field groups show unsupported status | Pending | Pending |
| DATA-001 | Player identity | UID, name, DOB and nation cross-check against FM UI | Pending | Pending |
| DATA-002 | Newgen identity | UID-less prospect survives UID assignment without duplicate | Pending | Pending |
| DATA-003 | Loan and parent club | Current club and owning club are distinguished correctly | Pending | Pending |
| DATA-004 | Player-coach/staff overlap | Hybrid player/staff is not double-counted as two people | Pending | Pending |
| DATA-005 | Women's data | Separate coverage and validation; no false labels or unverified writes | Pending | Pending |
| STATS-001 | Export schema | CSV headers map by version; unknown/missing columns are reported | Pending | Pending |
| STATS-002 | Per-90 | Formula and minutes denominator verified against a manually checked FM player | Pending | Pending |
| STATS-003 | Small samples | Threshold prevents tiny-minute players dominating rankings | Pending | Pending |
| TACT-001 | IP/OOP role model | Both phase role sets correctly loaded and rated | Pending | Pending |
| TACT-002 | Optimal XI | Small test squads match brute-force optimum; large squads complete within target | Pending | Pending |
| TACT-003 | Replacement impact | Removing a starter recomputes the whole XI and shows exact loss | Pending | Pending |
| GRAPH-001 | Face/crest UID | Correct image shown for known UID and neutral fallback for missing UID | Pending | Pending |
| GRAPH-002 | Duplicate mapping | Source pack precedence is shown; no random/incorrect portrait | Pending | Pending |
| EDIT-001 | Editor disabled by default | No write path reachable while Editor toggle is OFF | Pending | Pending |
| EDIT-002 | Compare-before-write | A changed live field blocks the stale write | Pending | Pending |
| EDIT-003 | Read-back | UI shows verified only when the written value reads back exactly | Pending | Pending |
| EDIT-004 | Guarded Undo | Undo refuses to overwrite later FM changes | Pending | Pending |
| EDIT-005 | Mass-edit preview | Preview exactly matches selected records and calculated values | Pending | Pending |
| LOG-001 | Diagnostic export | Logs contain build/error evidence and redact personal paths by default | Pending | Pending |

Run on a separate test career first. Any write test must use a disposable copy, paused game state where appropriate, and a verified backup. Do not test experimental editor operations in the main career.

---

## 5. Immediate build order after the R7 source audit

1. **R0.2 Source consolidation:** inspect R7.0.2 and the R5.2.1 FM26 foundation; document what is reused, removed or rewritten. Keep the existing user-facing FM Atlas codebase as the baseline, not the browser-only Scout Lab.
2. **R0.3 Connection parity:** implement process/store detection and build profiles; test Steam and Game Pass separately. Read-only only.
3. **R0.4 Data importer:** normalised records, SQLite, source provenance, diagnostics and F9/manual import fallback.
4. **R0.5 Scouting + graphics:** player/staff/club search, UID graphics, profiles, filters, shortlists, exports.
5. **R0.6 Analysis:** role scoring, dual-phase tactics, Best XI, B-team, replacement and gap analysis.
6. **R0.7 Statistics + Match Lab:** match and season stats, leaderboards, per-90, percentiles, comparisons and report cards.
7. **R0.8 Development + club/nation:** snapshots, intake radar, future squad plans, staff/coaching and registration.
8. **R0.9 Editor alpha:** explicit switch, a small verified field set, staged preview, read-back, history and Undo on both stores.
9. **R1.0 Integrated candidate:** combine all modules in one application, but only mark individual capabilities working after their own acceptance tests pass.

This sequence does not remove the user's request for one integrated program. It controls development and validation order so the first combined build is not a large untestable collection of half-connected features.

---

## 6. Source register

- FMSuperScout: https://github.com/mavarobli/FMSuperScout
- FMLibreEditor: https://github.com/tozmoz/FMLibreEditor
- fm-editor: https://github.com/barak14/fm-editor
- Jackhamerp24 FM27 Editor: https://github.com/Jackhamerp24/Football-Manager-27-Editor
- FM Player Analyzer: https://github.com/ElectroHugin/FM-Player-Analyzer
- FM View Exporter: https://github.com/ElectroHugin/FM-View-Exporter
- FMSave: https://github.com/rhiever/fmsave and https://rhiever.github.io/fmsave/app/
- FMRTE documentation: https://docs.fmrte.com/
- Genie Scout features / updates: https://geniescout.com/features/ and https://geniescout.com/updates/
- Talent Seer: https://sortitoutsi.net/content/78140/talent-seer-is-here-a-free-scouting-and-editing-tool-for-football-manager-27
- Teknik Oda: https://sortitoutsi.net/content/78456/teknik-oda-toby-a-coach-who-reads-your-fm27-match-from-fms-own-data-open-beta
- FaceDrop: https://sortitoutsi.net/content/78390/facedrop-drag-drop-faces-for-fm27-players-regens-managers
- Touchline Forge 27: https://cocolichegames.itch.io/touchline-forge-27
- SiRRAHx Live Editor: https://github.com/SiRRAHx/fm27-live-editor
- FM Dossier: https://www.fmdossier.dev/
- XML Configurator: https://fmtigrotto.com/tools/xml-configurator
- SEGA Pre-Game Editor help: https://support.sega.com/hc/en-us/articles/52324155452945-How-To
- Official FM27 data improvements: https://www.footballmanager.com/fm27/features/fm27-2d-match-engine-game-editor-and-deeper-world-upgrades
- Official FM27 training changes: https://www.footballmanager.com/fm27/features/fm27-training-and-tactical-effectiveness
- FM27 skin catalogue: https://www.fmscout.com/c-fm27-skins.html
- FM27 mod overview: https://fmtweak.com/fm27-mods/
- FM27 tactic testing reference: https://www.fmtacticlab.com/en
