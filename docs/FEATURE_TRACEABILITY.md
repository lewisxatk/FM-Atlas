# FM Atlas — Feature-to-Source Traceability Register

**Revision:** 1 · 13 November 2026  
**Active branch:** `overhaul/monolith-full-platform`  
**Source root:** `application/R7.2.0/FM-Atlas-R7.2.0-Work/src/FmAtlas/`  
**Purpose:** map the integrated product scope to the actual recovered source, evidence state and remaining implementation/acceptance. This is a static source register, not a claim that every visible feature has passed a Windows runtime test.

## Status definitions

- **Source present — partial:** relevant code exists, but scope, consistency or acceptance is incomplete.
- **Source present — narrow:** a limited implementation exists with explicit boundaries; do not imply the wider product scope is delivered.
- **Code-only — unverified:** implementation is present but has not been proven by a fresh current-branch build and runtime tests.
- **Not present — planned:** no corresponding implementation was identified in the inspected R7.2.0 source.
- **Blocked by safety gate:** code path exists but is intentionally disabled until named safety evidence exists.
- **Verified:** reserved for repeatable tests and runtime acceptance linked to the exact commit/build. No whole feature area is labelled verified by this static register.

## 1. Foundation, shell and application reliability

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| WPF desktop entry/startup | `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` | .NET 8 WPF project; App startup, exception hooks and MainWindow Loaded sequence inspected | Source present — partial | Current-branch Windows build, startup smoke test, startup recovery UX |
| Main shell/navigation | `MainWindow.xaml.cs` | Dynamic sidebar, top action bar, career strip and page host; route switch covers 21 destinations | Source present — partial | Refactor 1,980-line code-behind shell; remove dead/fallback routes; keyboard/focus/responsive tests |
| Global search | `GlobalSearchWindow.cs`, `MainWindow.xaml.cs` | Separate global-search window; current shortcut is Ctrl+F | Source present — partial | Ctrl+K command palette, indexed cross-entity search, keyboard navigation and results explanation |
| Settings/preferences | `MainWindow.xaml.cs`, `Services/MonolithTheme.cs` | Theme selector and local theme preference persistence | Source present — narrow | Central settings schema, validation, migration and user-data location settings |
| Logs and exception capture | `Services/AtlasLog.cs`, `App.xaml.cs` | Local rolling log; dispatcher/AppDomain/task exception handlers | Source present — partial | Structured event fields, correlation IDs, feature/operation tags, log rotation tests, no sensitive-content logging audit |
| Notifications, quick actions, profile menu | — | No integrated implementation identified in the shell audit | Not present — planned | Add accessible notification/status centre, quick actions and account/profile menu |

## 2. MONOLITH design system

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Obsidian / Warm Light palettes | `Services/MonolithTheme.cs` | Palette tokens, font fallback and saved theme selection; Settings selector | Source present — partial | Complete semantic resources for all surfaces and states |
| Legacy colour bridge | `Services/MonolithTheme.cs`, `MainWindow.xaml.cs` | Legacy hex colours can be resolved to current theme roles | Source present — narrow | Remove hard-coded values; audit contrast and semantic meaning |
| Shared WPF component library | — | UI helper factories exist in MainWindow for cards, text, inputs and buttons, but there is no cohesive shared component library | Not present — planned | Buttons, inputs, menus, tabs, chips, dialogs, drawers, tables, pitch, charts and focus states |
| Full page migration | Feature partials and `MainWindow.xaml.cs` | Theme-aware helpers are used in places; older palette declarations and per-page styling remain | Source present — partial | Migrate every feature page and test both themes/high-DPI |
| Motion, iconography, responsive shell | `MainWindow.xaml.cs` | Basic glyph navigation and loading pane | Source present — narrow | Consistent icon set, active state, restrained motion, layout scaling and accessibility |

## 3. Canonical data, provenance and field registry

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Player model | `Models/PlayerRecord.cs` | Player identity, age/DOB, regen status/confidence, club/nation/league, position/foot, CA/PA, value, home-grown and origin fields | Source present — partial | Expand typed provenance, game version, visibility, units, confidence and freshness on each field |
| SQLite store and imports | `Data/ScoutDatabase.cs` | Large central SQLite schema/query/import layer; raw save facts and indexed metrics | Source present — partial | Full schema audit, migrations, idempotency, transactions, rollback and fixtures |
| Live field evidence | `Services/FieldEvidencePolicy.cs`, `Data/ScoutDatabase.cs` | Unverified, Candidate, ProfileMatchedProvisional and FieldVerified statuses | Source present — narrow | Enforce status consistently across every feature; independent witnesses; source/build provenance |
| Universal cross-entity field registry | `Data/ScoutDatabase.cs` | Live field registry exists, but not a complete unified metadata registry for every entity and imported scalar | Source present — partial | Complete players/staff/clubs/nations/competitions/contracts/finance/injuries/tactics fields |

## 4. Advanced Scouting Search

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Player search workspace | `MainWindow.xaml.cs`, `Data/ScoutDatabase.cs` | Search results paging, filters, profile panel and database-backed search | Source present — partial | Full verified field coverage, usability and performance tests |
| Attribute criteria | `MainWindow.AdvancedSearch.cs`, `Data/ScoutDatabase.cs` | Up to 60 AND criteria across imported player attribute values | Source present — narrow | AND/OR groups, include/exclude, nested groups, date semantics, save/restore profiles |
| Season-stat criteria | `MainWindow.AdvancedSearch.cs`, `Data/ScoutDatabase.cs` | Dynamic metric catalogue, multiple AND criteria, optional per-90/minutes floor | Source present — narrow | Cross-stat/player-period semantics, missing-data rules, query truth-table tests |
| Position and suitability | `MainWindow.xaml.cs`, `Services/LiveDataImporter.cs` | Position picker and 15-position key set in the live importer | Source present — partial | Validate every FM position and add pitch/suitability visualization with five labelled evidence levels |
| Saved profiles and match explanations | — | Not identified in inspected search partial | Not present — planned | Persist named profiles; explain each match and unavailable field; custom columns and export |

## 5. Player Intelligence / Player DNA

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Player profile | `MainWindow.xaml.cs`, `PlayerIntelligenceReportWindow.xaml.cs`, `Data/ScoutDatabase.cs` | Identity, attribute sections, facts and performance facts surfaced in player views | Source present — partial | Unified DNA profile with provenance/freshness and complete coverage report |
| History and development | `Data/ScoutDatabase.cs` | History/fact queries exist | Source present — narrow | Snapshot deltas, development timeline, reliable stable identity and age curves with known inputs |
| Compare/similar-player finder | — | No complete comparison/replacement workflow identified | Not present — planned | Side-by-side comparison, nearest-neighbour model and transparent feature weighting |
| Personality/current-potential evidence | `Models/PlayerRecord.cs`, database fact stores | Some attributes/facts can be stored; source/visibility is not universally enforced | Source present — partial | Expose only source-backed values; no estimates disguised as observations |

## 6. Performance & Moneyball Scout

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Numeric statistic catalogue | `Data/ScoutDatabase.cs`, `MainWindow.AdvancedSearch.cs` | Dynamic numeric fields across imported save stats and FM View CSV | Source present — partial | Validate table/field mapping and coverage per game build |
| Per-90 calculation and minutes floor | `Data/ScoutDatabase.cs`, `MainWindow.AdvancedSearch.cs`, `MainWindow.R7.cs` | Optional derived per-90 query and sample-minutes threshold | Source present — narrow | Known-answer tests, zero-minute/partial season handling and duplicate joins |
| Value Finder | `MainWindow.ValueFinder.cs`, `Data/ScoutDatabase.cs`, `Models/ValueFinderModels.cs` | Relative CA/PA percentile vs value percentile, filters and CSV export | Source present — narrow | Explain cohort/sample size; distinguish value from asking price, wages and fees |
| Position/league/age percentiles | `Data/ScoutDatabase.cs` | Value percentiles exist; broad defensible cohort normalization not proven | Source present — partial | Cohort definitions, sample thresholds, ties, season and league separation |
| Moneyball ranking/profiles/radar | — | No complete Wyscout-style multi-profile recruitment workspace identified | Not present — planned | Build from verified stats registry and transparent calculations; no fabricated league-strength calibration |

## 7. Scouting Matrix and Recruitment Intelligence

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Recruitment workspace | `MainWindow.xaml.cs` | Recruitment route uses player workspace mode | Source present — narrow | Role/team need and recruitment-project-specific filters |
| Shortlists | `MainWindow.xaml.cs`, `Data/ScoutDatabase.cs` | Create, add, remove, list, notes and CSV export paths | Source present — partial | Priority, tags, status, owner, multiple recruitment projects and reports |
| Squad Architect | `MainWindow.xaml.cs` | Dedicated page route and page builder exist | Source present — narrow | Position gaps, succession, age/home-grown/wage balance, constrained best XI and upgrade planning |
| Recruitment board/radar | — | No complete matrix/kanban/radar workflow identified | Not present — planned | Board views, candidate comparison and position/role gap visualisations |

## 8. Role Lab / Player Fit

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| IP/OOP role profiles | `Services/RoleFitService.cs`, `MainWindow.RoleLab.cs` | 23 transparent weighted profiles; score coverage based on available attributes | Source present — narrow | Validate role inputs, position constraints, missing-field behaviour and weights |
| Explainable role estimates | `Services/RoleFitService.cs` | Source comments explicitly state estimates are not SI hidden formula/stars | Source present — narrow | Display weights, attribute contribution, confidence/coverage and uncertainty in UI |
| Team-specific role requirement fit | — | No complete team-role calibration found | Not present — planned | Add editable tactical-role templates and candidate comparison after data validation |

## 9. Tactical Intelligence Studio

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Tactical Lab route | `MainWindow.xaml.cs` | Tactical Lab page builder is wired into navigation | Source present — narrow | Full acceptance and supported-data inventory |
| Tactical Effectiveness | — | No verified FM27 source for internal effectiveness formula identified | Not present — research gated | Inspect exact FM27 UI/data; show only documented source values; do not invent formula |
| Complexity, buy-in and squad fit | `Services/RoleFitService.cs` | Role profiles provide a limited transparent player fit basis | Source present — narrow | Separate complexity/buy-in measures; show inputs and make all estimates explicit |
| Opposition/instructions analysis | — | Full opposition/tactical outcome workflow not identified | Not present — planned | Build from evidenced data and separate observed vs calculated values |

## 10. Match Lab

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Match-level stats import | `Data/ScoutDatabase.cs`, `Services/FmsaveBridgeService.cs` | Match/stat fact ingestion paths exist in the data layer | Source present — partial | Validate per-match linkage, event vs aggregate semantics and coverage |
| Match analysis workspace | — | No dedicated Match Lab route/page identified | Not present — planned | Build match centre, team/player trends and export only when fixtures prove data coverage |

## 11. Player Development / Career Assistant

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| History facts | `Data/ScoutDatabase.cs`, player profile | Historical fact storage/query exists | Source present — narrow | Time-series snapshots with field provenance and stable identifiers |
| Atlas Assistant | `MainWindow.xaml.cs` | Assistant route and search-oriented helper exist | Source present — narrow | Actual career-aware actions, alerts, reminders and traceable recommendations |
| Development plans and predictions | — | No validated prediction engine identified | Not present — planned | Only add after sufficient real longitudinal data; expose model inputs and uncertainty |

## 12. Save Inspector and Data Import

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Local save bridge | `Services/FmsaveBridgeService.cs`, `tools/Export-FMAtlas-Fmsave.py` | Optional Python Launcher adapter pinned to fmsave 0.12.3; export produces structured JSON | Source present — partial | Exact FM27 save/build coverage, package pinning and recovery tests |
| Save snapshot import | `Data/ScoutDatabase.cs` | JSON/fmsave import, player/staff/club and extra/stat tables, import run tracking | Source present — partial | Section-level report, strict/partial preview, idempotency and unknown-row retention tests |
| FM View CSV provider | `Data/ScoutDatabase.cs`, `MainWindow.AdvancedSearch.cs` | CSV data import feeds statistical search catalogue | Source present — narrow | Robust encoding/column mapping, malformed-file handling and provider provenance |
| FM26/FM27 adapters | `Services/FmsaveBridgeService.cs`, importer | Shared adapter code exists but exact per-game/per-build compatibility not accepted | Code-only — unverified | Test separate exact builds and prevent FM26 container assumptions leaking into FM27 |

## 13. Database and Editor Studio

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Local Atlas editing | `Data/ScoutDatabase.cs`, `MainWindow.xaml.cs` | CA/PA/attribute writes to local SQLite with edit history/undo | Source present — partial | Transaction preview, batch conflict handling, schema migrations and restore tests |
| Mass Edit | `MainWindow.R7.cs` | Local record Set/Add/Scale actions with editor lock and history | Source present — narrow | Test bounds, rounding, batch rollback and large selections |
| Pre-game database editor | — | No general FM pre-game database editor identified in the recovered project | Not present — planned | Research file formats and distinct local DB editor; keep separate from career/live editing |
| Edit audit/undo | `Data/ScoutDatabase.cs` | Undo checks current values before reverting local records | Source present — partial | Round-trip and conflict tests; distinguish local edit history from live writes |

## 14. Live Game Adapter

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Process/build discovery | `Services/GameProcessDetector.cs`, `Services/GameModuleDiagnostics.cs`, `Services/Fm27BuildProfiles.cs` | Separate Game Pass and Steam PE fingerprint records | Source present — narrow | Exact user's installed Game Pass build verification; new-build rejection tests |
| Read-only scan/import | `Services/LiveDataImporter.cs` | Profile-matched player/club identity reader and field survey methods | Code-only — unverified | Independent witnesses, bounded read tests, cancellation and identity/coverage acceptance |
| Field registry policy | `Services/FieldEvidencePolicy.cs`, database | Profile match and field verification are separate statuses | Source present — narrow | Enforce field-level evidence on every consumer and display |
| Apply/read-back/Undo | `Services/LiveMemoryEditService.cs`, `Services/LiveWriteCertification.cs` | Preflight, identity check, career-session invalidation, read-back and undo pathways exist; global certification switch is false | Blocked by safety gate | Keep disabled until exact build, field witnesses and disposable offline-save acceptance pass |

## 15. Graphics and Skin Studio

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Graphics root discovery | `Services/GraphicsResolver.cs` | Documents/OneDrive discovery plus configurable root saved locally | Source present — partial | Validate user's Game Pass path, user override and permission errors |
| Face/logo/flag mappings | `Services/GraphicsResolver.cs` | config.xml reader, ID mapping, image index and path containment | Source present — partial | Duplicate conflict precedence, malformed XML, FaceDrop provider and cache invalidation tests |
| Skin manager | — | No complete skin discovery/install/restore manager identified | Not present — planned | Skin compatibility catalogue, preview, backup/install/restore and cache reload guidance |

## 16. Modding and Research Workbench

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Research register | `research/RESEARCH_REGISTER.md`, `docs/REVERSE_ENGINEERING_AUDIT.md` | External tools/repos, research findings and licensing boundaries documented | Source present — documentation only | Build in-app mod catalogue, version/license/path records and reproducible inspection notes |
| Static package inspection | User-supplied research and archive inventory docs | ZIP/source review is documented; unknown third-party binaries were not executed | Source present — documentation only | Safe static inspection workflows; no automatic execution of unknown EXE/DLL/plugin files |
| Licence/attribution | `docs/REVERSE_ENGINEERING_AUDIT.md`, source notices | GPL research-only boundary and MIT/proprietary distinction recorded | Source present — documentation only | Third-party notice audit before packaging, dependency pinning and distribution checks |

## 17. Value Finder and Squad Tools

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Value & Upside Finder | `MainWindow.ValueFinder.cs`, database models | Relative CA/PA and market-value percentile score, filters, export and caveats | Source present — narrow | Known-answer percentile tests, cohort thresholds, tie handling, data units |
| Squad tools | `MainWindow.xaml.cs` | Squad Architect route and page code | Source present — narrow | Tactical slots, home-grown registration, wage/age balance and upgrade paths |

## 18. Quality, release engineering and support

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Build scripts/installer | Application root build/publish batch files and `Installer/FM-Atlas-27.iss` | Build, publish and installer scripts are present | Source present — partial | Reproducible publish, install/upgrade/rollback and clean-machine validation |
| Windows CI build | `.github/workflows/fm-atlas-windows-build.yml` | Workflow file added for .NET 8 restore, Release build and artifact upload | Code-only — unverified | First successful workflow run and warning triage |
| Support diagnostics | `MainWindow.Diagnostics.cs`, `Services/AtlasLog.cs` | Health checks, local logs, bundle preview, SHA-256 manifest, local export; token/path redaction self-check added | Code-only — unverified after hardening | Windows build/run, bundle inspection, privacy/log-rotation tests and robust recovery on diagnostic failures |
| Automated test suite | — | No dedicated test project/fixture suite identified in the inspected tree | Not present — planned | Add unit/integration/fixture tests and known-answer data without user save leakage |
| Release acceptance | Existing test plan and publish scripts | Previous R7.2.0 test plan exists; current branch has no final acceptance report | Not present — planned | Build, launch, data/search/graphics/editor tests, packaging and evidence bundle |

## Navigation inventory observed in source

The current shell creates these routes: Dashboard, Players, Recruitment, Value Finder, Attribute Search, Stats Search, Squad Architect, Clubs, Staff, Transfers, Analytics, Tactical Lab, Role Lab, Shortlists, Live Editor, Mass Edit, Edit History, Atlas Assistant, Settings, Tester & Logs, Help & Tutorial.

This list is a route inventory only. Each route must be opened and acceptance-tested; do not mark a feature complete merely because a navigation item and page builder exist.

## Immediate implementation order

1. Fresh Windows build of the current branch and in-app Diagnostics privacy check.
2. Complete per-module source audit and confirm route-to-requirement links.
3. Add a test project and deterministic fixtures for privacy, DB imports, search, metrics, role fit and graphics.
4. Complete MONOLITH shared resources and migrate the main shell before feature screens.
5. Build the canonical field registry and search query engine before expanding scouting UI.
6. Integrate Player DNA, Scouting Matrix, Moneyball, Tactical Intelligence, Match Lab, Career Assistant and missing management workspaces.
7. Finish save/database editing, Game Pass read-only profile, graphics/skin and modding workbench.
8. Complete full acceptance, installer/rollback and one integrated release candidate.

## Evidence checkpoints

- R7.2.0 source import: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`.
- Diagnostics hardening code checkpoint: `6fc46757688be84af497e13c3d27e40877da3f92`.
- Windows build workflow added: `042c95ea52a063886de4a59f8b49858bc28b0e6d`.
- Current full branch head moves as each documentation/engineering checkpoint is committed; use the active GitHub ref and master progress file for the latest value.
