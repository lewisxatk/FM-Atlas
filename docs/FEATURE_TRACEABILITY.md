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
| Player model | `Models/PlayerRecord.cs`, `Data/ScoutDatabase.cs` | Player identity, age/DOB, regen status/confidence, club/nation/league, position/foot, CA/PA, value, home-grown and record-level DataOrigin; raw save snapshot and fmsave validation JSON are retained separately | Source present — partial; critical provenance gap | Attach per-field parser/build/status/coverage/unit/freshness to every decision-facing scalar; do not treat record-level DataOrigin as field verification |
| SQLite store and imports | `Data/ScoutDatabase.cs`, `tests/FmAtlas.Tests/ScoutDatabaseEvidenceIntegrationTests.cs` | Central SQLite schema/import/query layer; raw save facts and indexed metrics; generic save/local queries explicitly exclude `LIVE:` identity mirrors; isolated SQLite tests cover the live evidence boundary | Source present — partial; Windows integration-tested | Full schema audit, field-level provenance joins, migrations, idempotency, rollback and broader fixtures |
| Live field evidence | `Services/FieldEvidencePolicy.cs`, `Data/ScoutDatabase.cs`, `tests/FmAtlas.Tests/ScoutDatabaseEvidenceIntegrationTests.cs` | Exact canonical allowlist for verified states; unverified/provisional/mixed values fail closed. Dedicated live queries filter CA/PA/attributes by field status; generic LIVE mirror rows are excluded from save/local search/count, club roster, Attribute Search, Role Lab and Mass Edit. Disposable SQLite regression passed Windows CI. | Source present — hardened and integration-tested | Audit remaining consumers, add stale/multi-run provenance fixtures and extend field-level source/build/units/freshness across imported save data |
| Universal cross-entity field registry | `Data/ScoutDatabase.cs`, `Services/FieldEvidencePolicy.cs` | Live field registry and exact status policy exist; fmsave import also stores field-status/coverage JSON, but normalized save-player fields and indexed season-stat fields are not joined to per-field evidence at query time | Source present — partial; critical provenance gap | Unify save and live evidence for players/staff/clubs/nations/competitions/contracts/finance/injuries/tactics; expose status/coverage/unit/freshness in all decisions |

## 4. Advanced Scouting Search

| Requirement | Source locations | Current evidence | Status | Remaining work |
|---|---|---|---|---|
| Player search workspace | `MainWindow.xaml.cs`, `Data/ScoutDatabase.cs` | Search results paging, filters, profile panel and database-backed search | Source present — partial | Full verified field coverage, usability and performance tests |
| Attribute criteria | `MainWindow.AdvancedSearch.cs`, `Data/ScoutDatabase.cs`, `Services/SavedSearchProfileStore.cs` | Up to 60 AND criteria; local named profiles save/load/delete criteria only; generic search excludes `LIVE:` provisional mirrors; six profile persistence/validation tests and SQLite evidence-isolation test passed Windows CI | Source present — narrow; code/test verified | AND/OR groups, include/exclude, nested groups, date semantics, match explanations and runtime acceptance |
| Season-stat criteria | `MainWindow.AdvancedSearch.cs`, `Data/ScoutDatabase.cs`, `Services/StatConditionMatcher.cs`, `Services/SavedSearchProfileStore.cs` | Up to 60 ALL (AND) or ANY (OR) criteria on the same player-period row; missing fields are non-matches, never zero; optional per-90/minutes floor; named local profiles; unit tests passed. Imported metric keys are not yet joined to fmsave field-status/reader coverage at query time. | Source present — partial; code/test verified, field-trust gap remains | Join each metric key to fmsave field status and reader coverage; show verified/unconfirmed status; add nested groups and missing-field explanations |
| Position and suitability | `MainWindow.xaml.cs`, `Services/LiveDataImporter.cs` | Position picker and 15-position key set in the live importer | Source present — partial | Validate every FM position and add pitch/suitability visualization with five labelled evidence levels |
| Saved profiles and match explanations | `Models/SavedSearchProfile.cs`, `Services/SavedSearchProfileStore.cs`, `MainWindow.AdvancedSearch.cs` | Atomic local JSON persistence for named Stats/Attribute profiles; up to 200 profiles and 60 conditions; validates IDs, duplicate names, finite bounds, category-specific options and corrupt-file preservation; UI save/load/delete controls and persistence tests | Source present — partial | Match explanations, unavailable-field explanations, nested groups, custom columns and runtime acceptance |

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
| Explainable role estimates | `Services/RoleFitService.cs`, `MainWindow.RoleLab.cs`, `Data/ScoutDatabase.cs`, `tests/FmAtlas.Tests/RoleFitServiceTests.cs`, `tests/FmAtlas.Tests/ScoutDatabaseRoleFitIntegrationTests.cs` | Role Lab exposes named role inputs and exact Atlas weights, valid 1–20 range, missing-value rule and 50% minimum attribute-count coverage. Selected-player detail shows each available input's readable label, value, weight and additive score points; contributions sum to the displayed score and export to CSV. Extreme finite weights are scaled safely. Isolated SQLite integration test proves the database candidate query transports contributions, score and missing-data coverage. Latest Windows run `43473487285` at `8cfe5b3`: 53/53 tests, 0 warnings/errors. | Source present — calculation, database integration and compile/test verified; runtime UI acceptance pending | Visually validate the rendered panel and CSV on Windows with an imported FM27 save copy; clarify count-based coverage (not a probability); position familiarity remains unavailable until source-backed. Scores remain Atlas estimates, not SI hidden-role weights. |
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
| Windows CI build | `.github/workflows/fm-atlas-windows-build.yml` | Exact Stage 1/2 application source commit `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991` passed Windows .NET 8 restore, WPF Release build (0 warnings/errors), 64/64 tests and both Release/TRX artifact uploads (run `43476085012`) | Verified — code/build/test gate passed | Runtime WPF launch, route/DPI smoke testing, copied-save and Game Pass acceptance remain separate |
| Support diagnostics | `MainWindow.Diagnostics.cs`, `Services/AtlasLog.cs`, `Services/SupportBundleComposer.cs`, `tests/FmAtlas.Tests/SupportBundleComposerTests.cs`, `tests/FmAtlas.Tests/AtlasLogRedactionTests.cs` | Health checks, rolling logs, allowlisted support bundle composer, same-package preview/local ZIP output, SHA-256/size manifest, always-redacted credentials/paths/save names and opt-in player names/edit history; privacy and archive regression tests passed Windows CI | Source present — Windows build/unit verified; runtime ZIP inspection pending | Launch Tester & Logs; manually inspect default and opt-in ZIPs, log rotation and diagnostic recovery on Windows |
| Automated test suite | `tests/FmAtlas.Tests/` | Windows Actions run `43476085012` at `6c18e36`: WPF Release build 0 warnings/0 errors; **64/64 passed**. Covers Diagnostics redaction, support ZIP allowlist/round-trip/hash/path traversal, saved-search persistence/validation, strict field evidence/identity, SQLite live-fact mirror isolation, Role Fit calculation and data transport, squad assignment, metric math, ALL/ANY matching and numeric boundaries. | Verified — 64-test Windows code checkpoint; runtime UI acceptance remains open | Add parser/import/migration, field-provenance, graphics resolver, recovery, performance and UI smoke fixtures; inspect latest TRX on every application source change |
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


## Latest Role Fit UI/data acceptance checkpoint

- Exact source commit `91bd7728c555a0c0e54cfd8d02706467fdafdb89`; Windows Actions run https://github.com/lewisxatk/FM-Atlas/actions/runs/43472380514.
- WPF Release build succeeded with 0 warnings and 0 errors; MSTest passed 48/48, 0 failed/skipped. Windows Release artifact `14061643285`; test artifact `14061143387`.
- The Role Fit candidate record now carries contributions from the same latest-snapshot `PlayerAttributes` data used by the database query. The result panel shows each available value, readable attribute label, Atlas weight and additive score points. CSV export includes the same terms.
- This verifies source build and tests only. The app has not yet been launched for visual acceptance on the user's Windows/Game Pass environment. No role-position eligibility is claimed.

## Latest integrated Windows verification — 2026-11-13

- Source commit tested: `8cfe5b3f276770179e8cb77db7a02912f2336bdb`; Actions run https://github.com/lewisxatk/FM-Atlas/actions/runs/43473487285.
- Windows .NET 8 WPF Release build succeeded: 0 warnings, 0 errors. MSTest: 53 passed, 0 failed, 0 skipped.
- Release artifact `14061763980`; test-results artifact `14062007740`.
- The suite includes `ScoutDatabaseRoleFitIntegrationTests`, which uses an isolated SQLite database and verifies that the real Role Lab query returns the weighted score, coverage, readable attribute contribution names, raw values, Atlas weights and additive contribution points.
- It also includes expanded Diagnostics redaction cases for quoted JSON/config values, Basic authorization, client secrets, private keys and query-string access tokens.
- The tested commit is the latest application-code checkpoint. All follow-on commits after `8cfe5b3` are documentation-only checkpoint updates; `main` remains untouched.
- Build/test evidence does not constitute WPF runtime visual acceptance or actual FM27 Game Pass field certification.


## Stage 1/2 verified code checkpoint — 2026-11-13

- Exact tested application source: `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991`; Windows Actions run [43476085012](https://github.com/lewisxatk/FM-Atlas/actions/runs/43476085012).
- .NET 8 WPF Release build: 0 warnings / 0 errors. MSTest: **64/64 passed**, 0 failed/skipped. Windows Release artifact `14062557163`; TRX artifact `14062892033`.
- This checkpoint verifies the code, compile and automated regression suite, not the user's live WPF UI, manual support ZIP review or actual FM27 Game Pass build. Those remain explicit runtime acceptance items.

## Attribute Search ALL/ANY implementation — Windows verified

| Requirement | Source locations | Evidence | Status | Remaining work |
|---|---|---|---|---|
| Attribute Search ALL/AND and ANY/OR | `Data/ScoutDatabase.cs`, `MainWindow.AdvancedSearch.cs`, `Services/SavedSearchProfileStore.cs`, `SavedSearchProfileStoreTests.cs`, `ScoutDatabaseAttributeSearchIntegrationTests.cs` | UI offers both modes; ALL is the default; SQL joins the grouped attribute predicates with AND or OR while text search remains an outer AND; selected mode is saved/restored. A seeded SQLite test checks BOTH / first-only / second-only / neither and text-filter interaction. Windows run `43478483715` at `0fbd5fa`: 68/68 tests, 0 warnings/errors. | Source, build and integration tests verified | Launch on Windows with a copied FM27 save; check visible result correctness, saved-profile persistence and missing-field semantics. Continue to nested groups, includes/excludes, condition reordering and reusable query templates. |

## Advanced Search condition ordering and duplication — Windows verified

| Requirement | Source locations | Evidence | Status | Remaining work |
|---|---|---|---|---|
| Reorder and duplicate conditions | `MainWindow.AdvancedSearch.cs` | Both Stats Search and Attribute Search expose Move Up / Move Down / Duplicate / Remove. The action updates the view container and the underlying ordered filter list together; duplicates copy the current field and valid bounds and respect the 60-condition limit. Windows run `43479401387` at `eb5d8f2`: 68/68 tests, 0 warnings/errors. | Source and Windows compile/test verified; UI behavior still needs manual acceptance | Test row order after profile save/load, duplicate edits, edge rows and all supported DPI sizes; continue toward nested logical groups and include/exclude conditions. |

## Integrated Stage 3–7 implementation candidate — 2026-11-13

| Requirement | Source locations | Implemented behavior | Verification state | Remaining work |
|---|---|---|---|---|
| Nested universal AND/OR search | `Services/AdvancedFilterEvaluator.cs`, `Services/AdvancedFilterProfileStore.cs`, `MainWindow.UniversalSearch.cs` | Nested groups (up to five levels), 200-condition cap, typed numeric/boolean/date comparisons, missing-value-safe behavior, match explanations and portable local criteria-only profiles. | Automated tests added; Windows workflow run `43482389999` initially building. | Await a Windows Release build/test for the current branch head; exercise profile controls with a real imported save on Windows. |
| Player DNA and compare | `MainWindow.Integrated.cs` | Search imported player rows, compare up to three players using locally stored attribute facts, expose data-origin caution. | Source integrated; awaiting build/test. | Confirm which attribute families the FM27 parser actually supplies; verify player facts against in-game UI. |
| Scouting Matrix | `MainWindow.Integrated.cs` | Search candidates, create/select local shortlists, add/remove player records and view saved notes. | Source integrated; awaiting build/test. | Manual data/UI acceptance; do not infer transfer availability or asking prices. |
| Moneyball cohort ranking | `Services/CohortPercentileService.cs`, `MainWindow.Integrated.cs` | Uses the selected imported metric, optional per-90/minute floor, tied mid-rank percentiles and a configurable minimum sample; no unverified league-strength correction. | Service tests added; awaiting build/test. | Validate duplicate/period behavior on actual career exports and add trusted metric coverage labels. |
| Tactical Studio | `Services/RoleFitService.cs`, `MainWindow.Integrated.cs` | Reuses existing weighted role-fit service and exposes score plus attribute coverage. | Existing role service tests; new page awaiting build/test. | Validate imported roles/attributes; score is an Atlas estimate, not SI's hidden tactical model. |
| Match Lab and development | `MainWindow.Integrated.cs` | Shows source-provided performance facts and current player snapshot alongside Atlas-local edit history. | Source integrated; awaiting build/test. | Multi-snapshot identity and chronology are required before real progress charts can be claimed. |
| Save Inspector | `MainWindow.Integrated.cs` | Read-only imported-save metadata, retained extra-table counts, parser validation report and live field registry/evidence. | Source integrated; awaiting build/test. | Inspect parser output from an actual disposable FM27 save copy. |
| Graphics library indexing | `Services/GraphicsLibraryIndexer.cs`, `MainWindow.Integrated.cs` | Bounded read-only config.xml scan; prohibits DTD; skips reparse points; rejects rooted/traversal paths and non-image targets; reports duplicate IDs/missing targets. | Security-oriented tests added; awaiting build/test. | Test against the user's actual Game Pass graphics root; pack installation/editing remains out of scope. |
| Modding research catalogue | `Models/ModResearchEntry.cs`, `Services/ModResearchCatalog.cs`, `MainWindow.Integrated.cs` | Presents source, recorded licence note, relevant Atlas use and explicit limitations for research targets. | Catalogue boundary test added; awaiting build/test. | Re-check licences and exact program/build compatibility before any source reuse or execution. |

The build check is performed by a draft validation-only PR targeting `overhaul/monolith-full-platform`. It is not a PR into `main` and is not intended to be merged. No latest-head build/test pass is claimed until a run for the current code completes. No WPF runtime, user-save or live-memory acceptance is implied.

## Integrated Stage 3–7 implementation evidence update — 2026-11-13

The following source additions are present on overhaul/monolith-full-platform; they remain subject to the status distinctions above.

| Area | Current implementation evidence | Boundary / acceptance still required |
|---|---|---|
| Player DNA | Search local save/player records, compare up to three selected players' stored attributes; player profile renders all 15 source position ratings on a pitch with five transparent Atlas display bands | No complete similar-player model; lower three position bands are Atlas-only groupings; Windows runtime acceptance required |
| Scouting Matrix | Create/select named shortlists, search candidates, add/remove candidates, retain notes and export | No full kanban/radar, role-gap or budget-aware recruitment project workflow |
| Moneyball Scout | Select one imported numeric metric, choose raw/per-90 where supported, apply minutes/sample floors, rank the full cohort or independent primary-position groups, and export provenance-rich CSV | No age-band or league-normalised cohorts; no validated cross-league strength calibration; primary-position grouping excludes unclassified rows |
| Tactical Studio | Rank save-backed players against transparent Atlas role-fit profiles with coverage | Not SI Tactical Effectiveness, tactical complexity or buy-in; no inferred proprietary formula |
| Match Lab | Retrieve stored aggregate performance facts for a selected player | No verified event timeline, shot map or match outcome model |
| Development | Display current facts, dated multi-snapshot attribute/CA/PA/age history, source/build provenance and identity-reset markers | CA/PA/age and birth-date evidence are fmsave-unconfirmed; same-name ID reuse without DOB evidence cannot be ruled out; runtime save acceptance required |
| Save Inspector | Read-only last-import summary, retained table counts and field registry | Real copied-save parser coverage and validation report need acceptance |
| Squad Architect | Four formation templates, global one-player-per-slot best-XI optimiser, transparent role-fit × position-rating composite, club-scoped candidates and read-only squad_planner slots | Atlas estimate only; no fitness/injury/availability/tactical-duty/hidden SI logic; planner slot meanings remain unconfirmed and reader is gated to FM27 build 27.0.0+2486695 |
| Graphics & Skins | Read-only config.xml indexer with bounded traversal, duplicate/missing/path rejection reporting | User's configured Game Pass graphics root, pack precedence, large-pack cache and FaceDrop provider need validation; skin install/restore not implemented |
| Modding Workbench | Research catalogue with licence and compatibility boundaries | Static research only; no unknown third-party EXE/DLL execution or automatic installation |

Superseded: latest code-level Windows verification is source commit `4fdd17c344a00c64754218a4770e97c33a19f5eb`, run `43509771993`, with 95/95 tests and 0 warnings/errors. WPF runtime and user-save acceptance remain outstanding.

## Scouting Matrix notes update — 2026-11-13

- Scouting Matrix now exposes an editable notes field for the selected saved shortlist candidate, capped at 2,000 characters.
- Persistence reuses the shortlist UPSERT, which changes only Notes for an existing source ID/shortlist pair and preserves AddedUtc.
- A disposable SQLite integration test covers create/update/clear and identity preservation. The test passed in Windows run `43486407442` (87/87); visual interaction still requires manual WPF inspection.


## Superseding combined Stage 3–7 code verification — 2026-11-13

- Exact tested source: `c51f80574d9131cb9caa1b25a44541ae370df553`; Windows run [43486407442](https://github.com/lewisxatk/FM-Atlas/actions/runs/43486407442).
- Release build: **0 warnings / 0 errors**. Automated tests: **87 passed, 0 failed, 0 skipped**. Windows Release artifact `14066379269`; TRX artifact `14067926212`.
- Moneyball source corrections verified by the run: season-only cohorts, field-specific indexed-stat coverage, legacy season JSON fallback, latest FM View CSV fallback when no usable save observations exist, minimum-minutes enforcement for raw and per-90 results, per-90 disabled for metrics marked as rates/percentages, up to 50,000 observation return limit, tie-aware O(n log n) cohort percentiles and provenance-rich CSV export.
- Scouting Matrix note create/update/clear persistence verified by isolated SQLite test; AddedUtc is preserved when notes change.
- MONOLITH initial resources and active navigation now use semantic orange tokens; dashboard legacy product label removed.
- This is an automated build/test checkpoint, not WPF manual acceptance. The app still needs to be launched and inspected at supported sizes/DPI; save parsing, source provenance completeness, position suitability, tactical proprietary metrics, Game Pass fingerprint compatibility and configured graphics roots must be validated against a COPY of the user's actual FM27 environment.
- No live-memory Apply/Undo is certified or enabled. Unknown builds remain read-only.


## Latest integrated verification — 2026-11-13 21:22 UTC

- Exact tested source: `4fdd17c344a00c64754218a4770e97c33a19f5eb`; Windows run [43509771993](https://github.com/lewisxatk/FM-Atlas/actions/runs/43509771993).
- .NET 8 WPF Release build: **0 warnings / 0 errors**. MSTest: **95 passed, 0 failed, 0 skipped**.
- Artifacts: Windows Release `14077394002`; TRX `14078735267`.
- Position profile map is built from the 15 fmsave player-position ratings, not the tactical/per-match position mask. Natural (18+) and Accomplished (15–17) are fmsave classifier labels; lower colour tiers are explicitly Atlas-only.
- Moneyball now supports independent primary-position cohorts for save and FM View observations. It does not claim age/league normalisation or league-strength adjustment.
- Development uses dated snapshots, reports per-field evidence class and suppresses deltas across detected unique-ID identity boundaries. fmsave warns that a deleted person's unique_id may be reused; DOB/name checks reduce but cannot eliminate ambiguity.
- Squad Architect displays raw numbered squad-planner slots only for the documented FM27 build gate, resolves UIDs against the same imported snapshot, preserves unresolved references and does not infer slot meanings.
- This is build and automated-test evidence only. No WPF launch, visual acceptance, copied-save import, Game Pass compatibility acceptance or live-memory write has been performed.
- Live Apply/Undo remains disabled. Unknown/unregistered builds remain read-only.


## Latest Squad Architect verification — 2026-11-13 21:37 UTC

- Tested application source: `5ff275d534245880242a7280f95837f6ee3a3b0d`; Windows run [43511883115](https://github.com/lewisxatk/FM-Atlas/actions/runs/43511883115).
- .NET 8 WPF Release build: **0 warnings / 0 errors**. MSTest: **103 passed, 0 failed, 0 skipped**.
- Four formations are available. Slot eligibility requires a role-fit estimate plus a registered-save position rating; the Hungarian/global assignment uses each player at most once.
- Atlas composite formula is documented in UI and service code; it is not represented as FM's internal calculation.
- Latest registered snapshot is required for position eligibility. If the newest import is unregistered, no old position data is silently substituted.
- WPF runtime, copied-save, Game Pass and visual acceptance remain outstanding. Live Apply/Undo remains disabled.
