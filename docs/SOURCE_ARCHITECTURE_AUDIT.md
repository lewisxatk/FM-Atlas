# FM Atlas — Source Architecture Audit

Audit date: 2026-11-13  
Branch: overhaul/monolith-full-platform  
Recovered source root: application/R7.2.0/FM-Atlas-R7.2.0-Work/  
Purpose: record what is actually present in the imported application before broad implementation. This is an initial architecture pass, not a complete review of every method or runtime capability.

## 1. Project and build shape

- Windows desktop application built with WPF on .NET 8 (net8.0-windows).
- Project file: src/FmAtlas/FmAtlas.csproj.
- SQLite dependency: Microsoft.Data.Sqlite 8.0.11.
- Project enables nullable reference types and implicit usings.
- Root solution: FmAtlas.sln.
- Build scripts, installer configuration, graphics assets, historical archives, docs and a Python save-export bridge are included in the recovered application tree.
- User-provided Windows Release build evidence dated 2026-11-13 reports 0 errors and 15 warnings. That is evidence of a build on the user's PC, not a build performed during this audit.

## 2. Startup and application shell

- App.xaml.cs initialises AtlasLog and applies MonolithTheme before the main window is constructed.
- App.xaml.cs registers logging for WPF dispatcher exceptions, AppDomain unhandled exceptions and unobserved task exceptions.
- MainWindow.xaml is a minimal WPF Window containing the RootGrid host; most UI is composed dynamically in C#.
- MainWindow.xaml.cs is the largest UI file (approximately 1,980 source lines in the inspected revision). It builds the sidebar, header, career status strip and page host, and contains most navigation and shared visual helper methods.
- Current shell actions include global search, reload game, Save Atlas, Save Game guidance, Import Save and Diagnostics.
- Existing global-search window is accessible through Ctrl+F and Ctrl+K. Ctrl+K currently opens the existing Players/Staff/Clubs search window; the richer command palette with actions and suggestions remains planned.
- Navigation is a dynamically built list of text-glyph buttons. A polished icon system, command palette, notifications centre and full responsive workspace system remain outstanding.
- Navigation includes Dashboard, Players, Recruitment, Value Finder, Attribute Search, Stats Search, Squad Architect, Clubs, Staff, Transfers, Analytics, Tactical Lab, Role Lab, Shortlists, Live Editor, Mass Edit, Edit History, Atlas Assistant, Settings, Tester & Logs and Help & Tutorial. Presence in navigation does not prove each page is complete.

## 3. MONOLITH theme status

- Services/MonolithTheme.cs provides central colour roles, an Inter/Segoe UI font fallback, Obsidian and Warm Light modes, local theme preference persistence and a legacy palette resolver. The overhaul branch now keeps token brush instances stable and updates tracked legacy brushes in place when the theme changes.
- App.xaml.cs calls MonolithTheme.Apply at startup.
- SettingsPageAsync provides an Obsidian/Warm Light selector.
- MainWindow.xaml.cs routes many legacy hex colours through MonolithTheme.ResolveLegacy.
- App.xaml and MainWindow.xaml still declare the earlier R7 palette and fixed dark window defaults. Many page implementations continue to create one-off controls and hard-coded colours.
- Current status: theme service and initial mode switch exist; a complete semantic resource dictionary, typography/spacing/radius tokens, reusable button/input/table/dialog components and migration of every page are not complete.
- Authoritative design remains MONOLITH: Obsidian/graphite, Warm Light secondary mode, Atlas orange, Inter and restrained motion. Aurora Frost is not the primary direction.

## 4. Main code areas

### UI partials
- MainWindow.AdvancedSearch.cs: attribute and statistical filter pages; the inspected implementation is narrower than the planned universal AND/OR query builder.
- MainWindow.Diagnostics.cs: Tester & Logs page, system checks, report generation, support ZIP preview and export.
- MainWindow.R7.cs: additional R7 pages and UI flows.
- MainWindow.RoleLab.cs: role-fit interface.
- MainWindow.ValueFinder.cs: value and upside finder UI.
- GlobalSearchWindow.cs: separate global-search window.
- PlayerIntelligenceReportWindow.xaml and its code-behind: player intelligence report surface.

### Data and models
- Data/ScoutDatabase.cs: the main SQLite persistence, import, search and metric-query layer (approximately 2,665 lines in the inspected revision).
- Models/PlayerRecord.cs: player model.
- Models/AdvancedSearchModels.cs: statistical and attribute search models.
- Models/ValueFinderModels.cs: Value Finder model records.
- The database exposes records for live field evidence, save imports, performance facts, history and workspace metrics. Schema and migration coverage still need a full audit and fixture testing.

### Services
- AtlasLog.cs: local rolling logs, severity, UTC timestamp and session ID; maximum active log size 2 MB with rotated backups.
- FieldEvidencePolicy.cs: maps statuses into Unverified, Candidate, ProfileMatchedProvisional and FieldVerified. A profile match is not treated as field verification.
- Fm27BuildProfiles.cs: contains separate Game Pass and Steam module fingerprint/profile records. A matching profile is not proof that every field mapping is correct or that the user's installed build is supported.
- LiveMemoryEditService.cs: read-only staging plus guarded ApplyBatch/UndoBatch pathways, player identity re-resolution, stale-save generation checks, original-byte preflight, read-back and undo checks.
- LiveWriteCertification.cs: global certification switch currently returns IsCertified = false. ApplyBatch and UndoBatch call EnsureWritesEnabled, so live memory writes remain blocked.
- GraphicsResolver.cs: configurable graphics root, Documents/OneDrive default discovery, config.xml scanning, image indexing and path containment checks. It has not been tested against the user's actual Game Pass graphics folder.
- FmsaveBridgeService.cs: optional local Python Launcher adapter; expected fmsave package version is 0.12.3.
- PlayerAttributeSchema.cs: field-key/attribute layout definitions with provenance comments; source comments are not independent field witnesses.
- RoleFitService.cs: 23 transparent coaching-style role profiles weighted from available 1–20 attributes. It explicitly does not claim to reproduce the game's hidden role formula or star ratings.
- GameProcessDetector.cs and GameModuleDiagnostics.cs: process/module discovery and diagnostics.
- LiveDataImporter.cs: live snapshot acquisition and payload mapping.
- Other data and analytics behaviour remains subject to per-module traceability and tests.

## 5. Diagnostics and privacy audit

Existing implementation found:
- Tester & Logs page with quick local checks.
- Local rolling logs and log-folder access.
- Copy diagnostics and copy recent log actions.
- Support ZIP generation with a bundle ID, manifest and SHA-256 hashes for included files.
- Preview window listing archive contents and diagnostic report, with explicit confirmation before a Save File dialog.
- Local export only; no automatic transmission.
- ZIP excludes career saves, SQLite database, FM installation files and arbitrary user files by design. Personal details are an explicit opt-in in the Diagnostics UI.

Defect found and corrected:
- Two credential-redaction expressions were over-escaped, so common bearer-token and key/password patterns could fail to match.
- The original absolute-path expression stopped at spaces, leaving the remainder of some Windows paths visible.
- Current AtlasLog redaction corrects those patterns, replaces full drive-letter paths (including spaces), replaces UNC paths and retains standalone save-filename redaction.
- ValidateRedaction supplies deterministic examples for bearer token, API key, password, user profile path, spaced drive path, UNC path and standalone save filename.
- MainWindow.Diagnostics.cs runs this check as part of quick diagnostics.

Verification boundary:
- Equivalent pattern-level test cases passed locally.
- The actual C# self-check has not yet been run in the Windows app.
- No .NET build was performed during this audit. Build and runtime acceptance must be recorded separately after execution on Windows.

Remaining Diagnostics work:
- Current log format is tab-delimited text, not the planned complete structured schema with correlation IDs, feature/module and operation fields.
- Bundle preview is present, but broader privacy tests and export-size/rotation tests are still required.
- Add automated tests around redaction, log rotation, manifest integrity, cancellation, temp-file cleanup and export failure handling.

## 6. Search, scouting and role intelligence status

- Player search and filter pages exist in the shell and database.
- Advanced attribute/stat search partials exist, but full universal field registry coverage, grouped AND/OR conditions, persisted profiles, match explanations and all-field traceability are not proven complete.
- RoleFitService contains 23 profiles with transparent weights and available-attribute coverage fields.
- Do not describe those results as the game's internal role ratings.
- Per-90 metrics, percentiles, cohort sizes, league adjustment and Moneyball ranking require known-answer fixtures before being accepted.

## 7. Safety and compatibility boundaries

- FM27 Game Pass and Steam fingerprints are represented separately in source.
- Exact match of size and timestamp is a profile-selection step only; it does not certify individual fields.
- Live Apply and Undo are disabled globally by LiveWriteCertification.IsCertified = false.
- Unknown builds must remain read-only. No field should be promoted to verified without independent evidence and a reproducible witness.
- Save import, statistics, graphics and local editor require tests against disposable copies and fixture data.
- Never test live writes against the user's only career save.

## 8. Next engineering sequence

1. Build the recovered project on Windows and run the Diagnostics redaction check; record all warnings and failures.
2. Finish a per-module source audit and requirement-to-file traceability matrix.
3. Add automated fixture tests for redaction, database schema/imports, search query correctness, role-fit calculations and graphics config resolution.
4. Complete shared MONOLITH resources and reusable WPF controls; migrate the shell first, then each page.
5. Implement the universal field registry and advanced query engine before adding more search UI.
6. Integrate remaining scouting, analytics, tactical, career, save inspection, editor and graphics modules behind clear evidence states.
7. Keep live write support blocked until exact build, field-level witnesses, disposable-save Apply/read-back/Undo and rollback acceptance are documented.

## 9. Evidence ledger

- Repository: private lewisxatk/FM-Atlas.
- Programme branch: overhaul/monolith-full-platform.
- Source import commit: 6069c20abb8ab85881da1f5c10860de9b2a8e0f1.
- Diagnostics privacy hardening code checkpoint: 6fc46757688be84af497e13c3d27e40877da3f92.
- This audit is static source inspection. It is not a runtime acceptance report.

## Subsequent additions after the initial static audit — 2026-11-13

- Added `tests/FmAtlas.Tests/`, a Windows .NET 8 MSTest project referencing the application project.
- Added deterministic tests for the Diagnostics redaction self-check, bearer/API-key/password/path/save-name redaction, line-break cleanup, output-size cap and ordinary text preservation.
- Added the test project to `FmAtlas.sln` and extended the Windows CI workflow to execute `dotnet test` and upload TRX results.
- NuGet references are pinned to Microsoft.NET.Test.Sdk 18.11.0 and MSTest 4.5.2. The NuGet Gallery lists these as compatible with .NET 8; package versions are deliberately pinned rather than floating. See the official package pages: https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.11.0 and https://www.nuget.org/packages/MSTest/4.5.2.
- A separate local pattern-level check passed all seven sensitive-value cases. The C# tests and WPF solution have not yet run on Windows.

- 2026-11-13 follow-up: PlayerIntelligenceReportWindow.xaml now uses DynamicResource bindings for MONOLITH canvas/surface/text/accent/border tokens. Theme switching updates existing token and legacy brushes without rebuilding the current page. This code has not yet been exercised in a Windows UI run.

## Field evidence policy follow-up — 2026-11-13

- A further review found nine SQLite filters using `ValidationStatus LIKE '%Verified%'`, plus two UI filters using `Contains("Verified")`. These expressions could classify the word `Unverified` as verified.
- The SQL filters now use a closed set of exact canonical statuses after normalization. The player detail and position-suitability UI now use `FieldEvidencePolicy.CanUseForDecision` instead of substring tests.
- `FieldEvidencePolicy.Assess` now accepts only exact statuses `Verified`, `FieldVerified`, `Verified-UserUIWitness-ReadBack` and `FieldVerified-UserUIWitness-ReadBack` (separator/case normalized). Other verified-looking prefixes fail closed.
- Added tests for exact allowlist recognition, provisional/mixed statuses, cross-provider IDs and exact-source-ID deduplication; current test suite now contains 20 expected test cases including the original Diagnostics tests.
- Current Windows run for the expanded suite: https://github.com/lewisxatk/FM-Atlas/actions/runs/43466693498. At the time of this audit update it was in progress; no result is claimed here.

## Latest Windows validation — 2026-11-13

- Windows GitHub Actions run `43468496760`, exact source commit `ade463712df284034ba475a2f1bc85584e9394d9`, completed .NET 8 restore and WPF Release build successfully: 0 errors.
- MSTest result: 40 passed, 0 failed, 0 skipped. The suite covers Diagnostics redaction, evidence policy, provider identity, squad selection assignment, metric math, ALL/ANY statistics and numeric-boundary validation.
- Compiler warnings: 12. Source review confirmed eleven private MainWindow filter fields appeared only in their declarations across all seven MainWindow partial source files; these declarations were removed. One use of obsolete `AtlasLog.Warn` was switched to `AtlasLog.Warning`. Follow-up Windows run is pending for these changes.
- Latest source commits for warning cleanup: `886a8a5` and `1022078`; current branch head at audit update is `1022078bd4fd79d21f687feb721f6e6e7f4087d7`.
- This audit remains static plus CI build/unit-test evidence. No application launch, WPF UI automation, Game Pass process attachment or save-file runtime acceptance is claimed.

## Warning-free build and SQL integration extension — 2026-11-13

- Actions run `43469077009` at exact source commit `1022078bd4fd79d21f687feb721f6e6e7f4087d7`: Windows .NET 8 WPF Release build succeeded with 0 warnings and 0 errors; the test suite passed 40/40.
- Eleven private MainWindow filter fields were confirmed unused across all seven partial classes and removed; the only obsolete `AtlasLog.Warn` invocation was replaced with `AtlasLog.Warning`.
- `ScoutDatabase` now has a path-based constructor to support disposable fixture databases without changing the default LocalAppData database location. The optional pooling argument permits test fixtures to close/delete their SQLite files deterministically.
- `ScoutDatabaseEvidenceIntegrationTests` seeds a disposable SQLite database and checks that actual live player query, CA/PA filters and attribute filters exclude unverified/provisional/mixed statuses. The missing `System.IO` import was corrected in `e109eab`; current Windows execution is pending.

## Latest data and role-scoring test evidence — 2026-11-13

- Windows Actions run `43470501984` for exact commit `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712` passed .NET 8 restore/build with 0 warnings and 0 errors; MSTest 47/47 passed.
- `ScoutDatabaseEvidenceIntegrationTests` exercises a real disposable SQLite database and actual live search consumers, including CA/PA and attribute filters, using canonical verified/read-back states and unverified/provisional/mixed statuses. Run `43470026732` at `e109eab` passed 41/41; latest combined run passed with 47 tests.
- `ScoutDatabase(string databasePath, bool pooling = true)` preserves the original default LocalAppData constructor path while allowing isolated database fixtures and deterministic pool disabling.
- `RoleFitService.CalculateEstimate` centralizes the transparent weighted calculation and is used by `SearchRoleFitCandidatesAsync`; six tests cover weighting, coverage, normalized names, missing/out-of-range/nonfinite values and malformed profile weights.
- RoleFit remains an Atlas estimate, not FM's internal role rating. User interface acceptance, position familiarity, tactical context and attribute contribution explainability remain outstanding.
- The separate maximum-weight SquadSelectionOptimizer has unit tests but is not integrated into Squad Architect because validated player/slot eligibility mapping remains incomplete.


## Longitudinal data, position ratings and squad planner integration — 2026-11-13

- `PositionSuitabilityService` maps the fmsave `Positions` record's 15 named ratings into the profile pitch. It normalises dotted/legacy field keys and retains missing ratings as unavailable. Natural (18+) and Accomplished (15–17) follow fmsave's documented classifier; the lower three colour bands are Atlas-only display groupings.
- `CohortPercentileService.CalculateByGroup` calculates independent tie-aware percentile distributions by imported primary-position label. Unknown labels are excluded from position-specific cohorts. `ScoutDatabase.SearchSeasonMetricAsync` and per-90 queries retain the player primary-position label from save snapshots; FM View raw rows are parsed for common position headings.
- `GetPlayerDevelopmentHistoryAsync` reads dated raw player snapshots by PlayerSourceId and calculates deltas only between consecutive observations. It distinguishes parser-verified attributes from fmsave-unconfirmed CA/PA/age fields. A reused unique_id with a changed DOB, or changed name when DOB is missing, begins a new identity segment; this is a guard, not proof of identity.
- `GetLatestSquadPlannerSlotsAsync` reads only the latest snapshot's `squad_planner` extra-table rows, resolves UIDs against that same snapshot, retains unresolved/null entries in order and validates season offsets 0–2 and slot numbers 1–27. It requires registered FM27 build `27.0.0+2486695` because fmsave 0.12.3 documents that exact reader boundary. Slot semantics are not inferred.
- Windows run `43509771993` at exact commit `4fdd17c344a00c64754218a4770e97c33a19f5eb`: Release build 0 warnings / 0 errors; 95/95 tests passed.
- Automated tests do not substitute for actual FM27 Game Pass runtime, save-import, UI/DPI, or graphics-root acceptance.


## Formation-based Squad Architect optimiser and position evidence gates — 2026-11-13

- `SquadArchitectService` defines four formation templates and converts role-fit results plus position-rating evidence into slot-score edges. Composite is explicitly `roleFit × positionRating / 20`; `SquadSelectionOptimizer` solves the global assignment and prevents one candidate filling multiple slots.
- `ScoutDatabase.SearchRoleFitCandidatesAsync` supports a club filter and caller-defined result cap. `GetPlayerPositionRatingsForClubAsync` loads only the latest imported snapshot and requires `KnownBuild=1`; it does not fall back to an older verified snapshot when the latest import is unregistered.
- `GetPlayerPositionRatingsAsync` and `GetFmsavePlayerFactsAsync(..., "positions")` likewise require the latest registered snapshot. Moneyball save-derived primary-position grouping only reads the position label when that snapshot is registered.
- UI shows the score components and states exclusions: no FM hidden score, fitness, injuries, suspensions, tactical familiarity, duties, player traits or availability.
- Windows run `43511883115` at source `5ff275d534245880242a7280f95837f6ee3a3b0d`: 0 warnings / 0 errors; 103/103 tests passed.
