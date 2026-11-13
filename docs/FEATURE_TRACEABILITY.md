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
| Global search | `GlobalSearchWindow.cs`, `MainWindow.xaml.cs` | Separate database-backed Players/Staff/Clubs search; Ctrl+K and Ctrl+F open it, Enter/Escape are supported, results grids virtualize | Source present — partial | Full Ctrl+K command palette with commands/actions, saved searches, keyboard navigation and ranked suggestions; more entity types, ranked suggestions and result explanations |
| Settings/preferences | `MainWindow.xaml.cs`, `Services/MonolithTheme.cs` | Theme selector and local theme preference persistence | Source present — narrow | Central settings schema, validation, migration and user-data location settings |
| Logs and exception capture | `Services/AtlasLog.cs`, `App.xaml.cs` | Local rolling log; dispatcher/AppDomain/task exception handlers | Source present — partial | Structured event fields, correlation IDs, feature/operation tags, log rotation tests, no sensitive-content logging audit |
| Notifications, quick actions, profile menu | — | No integrated implementation identified in the shell audit | Not present — planned | Add accessible notification/status centre, quick actions and account/profile menu |

## 2. MONOLITH design system

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Obsidian / Warm Light palettes | `Services/MonolithTheme.cs`, `MainWindow.xaml.cs`, `PlayerIntelligenceReportWindow.xaml` | Palette tokens and saved mode; token brushes now update in place, legacy page brushes are tracked/remapped, main window uses token surfaces and report XAML uses DynamicResource | Source present — partial | Windows theme-switch test, accessibility contrast, migrate remaining views and XAML styles |
| Legacy colour bridge | `Services/MonolithTheme.cs`, `MainWindow.xaml.cs` | Legacy hex colours are mapped by semantic families and existing mutable brushes are repainted in place when mode changes | Source present — partial | Audit unmapped colours and semantic contrast; add test coverage and migrate to token resources |
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
| Season-stat criteria | `MainWindow.AdvancedSearch.cs`, `Data/ScoutDatabase.cs`, `Services/StatConditionMatcher.cs` | Up to 60 ALL (AND) or ANY (OR) criteria on the same player-period row; missing fields are non-matches, never zero; optional per-90 and minutes floor; shared matcher has unit tests | Source present — partial; Windows acceptance pending | SQLite integration fixtures, nested groups, saved profiles, cross-stat semantics and UI explanation of missing fields |
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
| IP/OOP role profiles | `Services/RoleFitService.cs`, `MainWindow.RoleLab.cs`, `Data/ScoutDatabase.cs`, `tests/FmAtlas.Tests/RoleFitServiceTests.cs` | 23 transparent weighted profiles; shared CalculateEstimate uses only finite 1–20 attributes, excludes missing/invalid values, reports coverage and withholds low-coverage scores; six tests pass in Windows CI | Source present — partially integrated and unit-verified | Windows Role Lab UI acceptance; incorporate position familiarity only when source-backed; show attribute contributions and confidence in UI |
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
| Field registry policy | `Services/FieldEvidencePolicy.cs`, `Data/ScoutDatabase.cs`, `MainWindow.xaml.cs`, `MainWindow.R7.cs`, `tests/FmAtlas.Tests/ScoutDatabaseEvidenceIntegrationTests.cs` | Exact canonical status allowlist; all nine SQL filters and two UI consumers use strict policy; disposable SQLite test proves actual player/CA/PA/attribute/club queries exclude unverified, provisional and mixed statuses; passed Windows CI | Source present — hardened and integration-tested | Audit all remaining consumers and migrate remaining data sources; preserve global write gate; extend multi-run/stale-evidence fixtures |
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
| Windows CI build | `.github/workflows/fm-atlas-windows-build.yml` | Successful Windows .NET 8 restore/build/test and artifact upload at commit `5aa82c2`; latest Ctrl+K/global-search commit `830cedc` has a separate run in progress | Verified — earlier checkpoint only | Confirm latest source checkpoint run; triage warnings and keep runtime UI acceptance separate |
| Support diagnostics | `MainWindow.Diagnostics.cs`, `Services/AtlasLog.cs`, `tests/FmAtlas.Tests/AtlasLogRedactionTests.cs` | Health checks, local logs, bundle preview, SHA-256 manifest, local export; in-app self-check and initial MSTest coverage added | Code-only — unverified after hardening | Windows build/run, execute the unit suite, bundle inspection, log-rotation tests and robust recovery on diagnostic failures |
| Automated test suite | `tests/FmAtlas.Tests/` | Initial Diagnostics suite passed 10/10 at `5aa82c2`; expanded suite now includes field evidence/identity, squad optimiser, metric math and ALL/ANY matcher tests; Windows suite passed at `ade4637` (run `43468496760`): 40/40 | Verified — initial suite only; expanded suite pending | Confirm latest TRX, then add SQLite migration/import, query truth tables, role-fit, graphics and recovery fixtures |
| Release acceptance | Existing test plan and publish scripts | Previous R7.2.0 test plan exists; current branch has no final acceptance report | Not present — planned | Build, launch, data/search/graphics/editor tests, packaging and evidence bundle |

## Navigation inventory observed in source

The current shell creates these routes: Dashboard, Players, Recruitment, Value Finder, Attribute Search, Stats Search, Squad Architect, Clubs, Staff, Transfers, Analytics, Tactical Lab, Role Lab, Shortlists, Live Editor, Mass Edit, Edit History, Atlas Assistant, Settings, Tester & Logs, Help & Tutorial.

This list is a route inventory only. Each route must be opened and acceptance-tested; do not mark a feature complete merely because a navigation item and page builder exist.

## Immediate implementation order

1. Fresh Windows build and run the new MSTest suite on the current branch; run the in-app Diagnostics privacy check.
2. Complete per-module source audit and confirm route-to-requirement links.
3. Expand the initial test project with deterministic fixtures for DB imports, search, metrics, role fit and graphics.
4. Complete MONOLITH shared resources and migrate the main shell before feature screens.
5. Build the canonical field registry and search query engine before expanding scouting UI.
6. Integrate Player DNA, Scouting Matrix, Moneyball, Tactical Intelligence, Match Lab, Career Assistant and missing management workspaces.
7. Finish save/database editing, Game Pass read-only profile, graphics/skin and modding workbench.
8. Complete full acceptance, installer/rollback and one integrated release candidate.

## Evidence checkpoints

- R7.2.0 source import: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`.
- Diagnostics hardening code checkpoint: `6fc46757688be84af497e13c3d27e40877da3f92`.
- Windows CI build/test run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43465739690 (commit `5aa82c2`; TRX 10/10 passed).
- Latest keyboard/global-search source commit: `830cedc7735babdf6aa3fd35d3c82e5029b45395` (Windows run in progress at time of this register update).
- Windows build workflow added: `042c95ea52a063886de4a59f8b49858bc28b0e6d`.
- Current full branch head moves as each documentation/engineering checkpoint is committed; use the active GitHub ref and master progress file for the latest value.


## 13. Integrated overhaul code checkpoints — 13 November 2026

| Area | New source / evidence | Status after latest code changes |
|---|---|---|
| MONOLITH tokens | Services/MonolithTheme.cs, App.xaml.cs, MainWindow.xaml.cs, GlobalSearchWindow.cs | Foundation added: Obsidian/Warm Light preference, central semantic tokens, legacy palette bridge and global search integration. Remaining views still need migration and visual review. |
| Live-write certification | Services/LiveWriteCertification.cs, Services/LiveMemoryEditService.cs, MainWindow.xaml.cs | Blocked safely: Apply and Undo throw unless separate build-wide certification passes; current policy remains false and live-write UI controls are hidden. Read-only/preview remains separate. |
| Field evidence | Data/ScoutDatabase.cs, Services/FieldEvidencePolicy.cs, MainWindow.Diagnostics.cs | Foundation added: registry query, statuses for unverified/candidate/profile-matched provisional/field-verified, conservative normalization and diagnostics counts. Automated tests added; Windows suite passed at `ade4637` (run `43468496760`); SQLite-level consumer fixtures remain outstanding. |
| Provider identity | Services/PlayerIdentityResolver.cs, MainWindow.xaml.cs | Exact SourceId deduplication replaces name+club+age fuzzy collapse in Squad Architect. LIVE and FMSAVE namespaces remain distinct until a verified crosswalk exists. Automated tests added; latest Windows run must be checked. |
| FMSave provider | Services/FmsaveBridgeService.cs, tools/Export-FMAtlas-Fmsave.py, setup script, DB sidecar list | Pinned to fmsave 0.12.3, exact installed version checked, and officials/team match statistics/squad-planner tables are preserved. Exact local career build compatibility is still unknown until tested with a safe local save copy. |
| Diagnostics | Services/AtlasLog.cs, MainWindow.Diagnostics.cs | Existing Diagnostics page hardened: log session ID, secret/path/name redaction, regression tests, evidence summary, local bundle ID, manifest hashes, recent redacted logs, preview, explicit export and no automatic upload. User-data inclusion is opt-in. |
| Source recovery | application/R7.2.0/FM-Atlas-R7.2.0-Work/ | Recovered source imported; eight specific v2 compile repairs reapplied individually after the first import CI failure. Windows CI then passed at commit 5aa82c2. Later-added evidence/identity tests are awaiting their own current-head result. |

## 14. Explicit corrections and current limitations

- The earlier statement that the recovery source tree already matched the repaired v2 ZIP was incorrect. A file-by-file comparison found eight source files that differed; the first CI attempt failed with 92 compiler errors. Those eight compile repairs were applied to the overhaul branch, preserving all newer changes. The subsequent Windows CI run passed.
- The 10/10 test count belongs to the initial redaction-only suite at commit 5aa82c2. Tests for field evidence and provider identity were added afterwards and must not be described as passed until a later Windows run succeeds.
- FMSave's registered FM27 layout is version-specific. Pinning the reader package does not prove that the user's current Game Pass save is fully supported.
- MONOLITH is a partially migrated design foundation, not yet a complete restyle of every screen.
- Reverse engineering so far is static source/package research, source mapping and guarded implementation decisions. It is not a complete reconstruction of FM27's executable or all memory structures.
- The overall product remains incomplete. Player DNA, Scouting Matrix, Moneyball, full Tactical Intelligence, Match Lab, Optimal XI solver, broad multi-entity editing, skin/mod manager and final installer/release still need implementation and acceptance evidence.

## Evidence-policy regression checkpoint — 2026-11-13

- A substring condition (`LIKE '%Verified%'` / `Contains("Verified")`) was unsafe because it also matches `Unverified`.
- All nine occurrences in the live SQLite candidate queries were replaced with an explicit normalized allowlist (`Verified`, `FieldVerified`, `Verified-UserUIWitness-ReadBack` and its FieldVerified equivalent).
- Both UI consumers previously using `Contains("Verified")` now call `FieldEvidencePolicy.CanUseForDecision`.
- `FieldEvidencePolicy.Assess` now accepts only exact canonical verified statuses after normalization; misleading prefixes such as `Verified-Provisional`, `Verified-Needs-UI-Witness` and mixed unverified states fail closed.
- Eight tests from the external same-branch update cover evidence states and provider-specific player identity; two added tests cover strict allowlist behavior and misleading statuses. These tests have not yet passed a Windows run.


## 15. New analytical engines added after the initial traceability pass

| Engine | Source | Implemented behaviour | Acceptance status |
|---|---|---|---|
| Squad selection optimiser | Services/SquadSelectionOptimizer.cs | Maximum-weight one-to-one slot assignment using rectangular Hungarian algorithm; each SourceId can fill at most one slot; missing slot score is ineligible; dummy assignments allow unfilled slots; deterministic tie order | Code added. Tests added for greedy counterexample/global optimum, unfilled slot, no duplicate player, duplicate IDs and invalid scores. Windows CI result pending. Not yet wired into Squad Architect UI or certified against actual FM positional suitability data. |
| Football metric math | Services/FootballMetricMath.cs | Per-90 calculation from supplied totals and minutes, attempt-rate percentage, sample floor, minimum cohort size, tied percentile mid-rank and higher/lower-is-better direction; missing/minutes/cohort statuses distinct | Code added. Tests added for denominators, zero minutes/attempts, ties, lower-is-better, minutes floor, insufficient cohort and bad input. Windows CI result pending. Not yet connected to production Moneyball/Statistics views. |
| Field evidence policy | Services/FieldEvidencePolicy.cs | Status normalization removes spaces/hyphens/underscores/dots; explicit unverified/unknown terms take precedence; evidence statuses remain separate from build-wide write certification | Code added. Regression tests added; current Windows run pending. |
| Provider-safe identity | Services/PlayerIdentityResolver.cs | Exact provider-specific SourceId dedupe only; best-completeness record wins within same provider ID; unresolved IDs do not collapse; cross-provider rows kept distinct | Code added and integrated in Squad Architect. Tests added; current Windows run pending. |

### Explicit integration boundary
These services are domain foundations, not fully delivered screens. Squad Architect still shows source-preserving roster/age/position summaries and does not yet expose the optimizer. Moneyball/Statistics still need their UI workflow, cohort construction, metric registry and verified season/competition/position inputs before the new math service should drive a recommendation.


## 16. Advanced Statistics Search ALL/ANY logic

- Database API: `SearchSeasonStatCriteriaAsync(..., bool matchAny = false)`.
- Shared evaluator: `Services/StatConditionMatcher.cs`.
- UI: `MainWindow.AdvancedSearch.cs` now exposes **Match ALL (AND)** and **Match ANY (OR)**.
- AND requires every requested metric on the same player-period row to exist and satisfy its bounds.
- OR requires at least one available metric on that same row to satisfy its bounds.
- Missing metric fields are non-matches and are never converted to zero. Per-90 still uses that row's actual minutes and respects the sample floor.
- Unit tests: `tests/FmAtlas.Tests/StatConditionMatcherTests.cs` covers AND, OR, missing fields, non-zero-denominator per-90 and invalid bounds.
- Nested condition groups and saved-search persistence are not implemented yet. This is the first useful ALL/ANY layer, not the complete universal query builder.

## Windows acceptance checkpoint — 2026-11-13

- Exact tested source commit: `ade463712df284034ba475a2f1bc85584e9394d9`.
- Windows Actions run `43468496760`: WPF Release build succeeded (0 errors); MSTest 40/40 passed; Release and TRX artifacts uploaded.
- The code-only squad optimizer and football metric math engines have passing unit tests, but they are not yet connected to their intended Squad Architect/Moneyball UI workflows. Do not label those product surfaces complete.
- The ALL/ANY statistics matcher is connected to the statistics search UI/SQLite query. Missing metrics are non-matches (not zero); ALL/ANY and finite-boundary cases have passing tests.
- Warning cleanup after the tested source removed the 11 private fields confirmed unused across the seven MainWindow partials and switched the one obsolete logging call to the current API. Current-head CI is required to verify this cleanup.
- Windows build/test pass is not a substitute for app-launch, visual, Game Pass or save-data acceptance.

- 2026-11-13: Windows run `43469077009` at `1022078` passed 40/40 tests, 0 build warnings and 0 errors. A new disposable SQLite integration test now exercises actual live CA/PA and attribute queries with mixed evidence statuses; a missing `System.IO` import was corrected in `e109eab`, awaiting another Windows run.

## Latest Windows acceptance record

- Source commit `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712`; Windows Actions run `43470501984`: 47/47 tests passed, 0 warnings, 0 errors.
- Includes actual disposable SQLite evidence-consumer integration test and the six RoleFitService calculation tests.
- RoleFit scoring engine is integrated into existing Role Lab database scoring, but no new XI/slot optimizer UI integration is claimed. Squad optimizer remains a separately tested service with explicit eligibility requirements.
- No full WPF UI launch, visual/DPI or in-game Game Pass acceptance has been performed.
