# Changelog

All entries are checkpoint notes. Earlier work is reconstructed from the previous conversation and screenshots; it is not a substitute for original Git history.

## 2026-11-13 — Repository baseline

- Created private GitHub repository `lewisxatk/FM-Atlas`.
- Confirmed repository was empty before initial documentation.
- Added project README, master handover, roadmap, provisional architecture, known issues, next steps and research register.
- Captured previous-session R7.0.2/R7.1.0 package findings and reported implementation milestones.
- At creation time no application source code had been imported; that status was later superseded by the R7.2.0 recovery import documented below.

## Historical milestones reported in prior ChatGPT work (dates/source commits to be recovered)

- Advanced player search, statistics and attribute discovery.
- FM27 graphics, live import, player editing, metrics, database and search integrations.
- Guarded live-memory editing scaffolding, importer mappings, persistence, undo and profile graphics resolution.
- Indexed storage, expanded search, profile support and R7.1.0 analytics / role-fit / editor-local / logging package pass (reported).
- FM Atlas value finder with database safeguards.
- C# database parsing, UI imports and search logic inspection/repairs.

## 2026-11-13 — Recovery archives inspected

- Inspected user-uploaded R7.0.2 (83 files) and R7.1.0 (95 files) archives.
- Recorded SHA-256 hashes and compared all relative paths.
- Found 16 changed shared files and 12 files added in R7.1.0; no files were removed.
- Confirmed R7.1.0 package notes say live FM memory writes are not implemented and Windows build/runtime testing remains outstanding.
- Added `recovery/RECOVERY-INVENTORY.md`.
- At this checkpoint only R7.0.2 and R7.1.0 archive inventory had been recorded; their full source trees were not yet committed to GitHub.

## 2026-11-13 — R7.2.0 checkpoint received

- Inspected the R7.2.0 ZIP (106 files, 3,610,671 uncompressed bytes).
- SHA-256: `34d985a39526698026b1e09855d7f37e9f6a00dfba4863cf6f4d467aafedc2c8`.
- Compared with R7.1.0: 19 shared files changed, 11 added, none removed.
- R7.2.0 is the newest supplied candidate. Its guarded live writer is source implementation only and remains unverified on Windows and in-game.
- At this checkpoint R7.2.0 had been received but not yet imported; import was completed later on the integrated overhaul branch.

## 2026-11-13 — Research and proposed evolution added

- Added Wyscout-style Performance & Moneyball Scout to the roadmap: season stats, per-90 metrics, position/league percentiles, attribute + performance filters, value ceiling, multi-league comparisons, bargain profiles and recruitment shortlists.
- Added explicit data provenance and confidence labels; FM save data availability and cross-league calibration must be verified before claims are made.
- Added Tactical Intelligence Studio research gate for FM27 Tactical Effectiveness and Tactical Understanding, with no promise to reproduce hidden FM formulas.
- Confirmed no BepInEx/injector dependency in the current roadmap; standalone app focus retained.
- Added backup/import safety steps to Next Steps.

## 2026-11-13 — Integrated overhaul branch and source recovery

- Created/continued `overhaul/monolith-full-platform` while leaving `main` untouched.
- Imported repaired R7.2.0 app source under `application/R7.2.0/FM-Atlas-R7.2.0-Work/` (source-import commit `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`).
- Reconciled stale source-import status in the master handover, master programme, roadmap, architecture, recovery inventory and root README.
- Added `docs/SOURCE_ARCHITECTURE_AUDIT.md` with observed project/startup/UI/data/service boundaries and explicit runtime unknowns.
- Corrected credential redaction and complete Windows/UNC path redaction in `AtlasLog`; added a Diagnostics privacy self-check and documented pending Windows execution.
- Added `.github/workflows/fm-atlas-windows-build.yml` for reproducible Windows .NET 8 restore/build and Release artifact upload. A workflow run has not yet been reported.
- Current application build/runtime acceptance remains pending after these branch changes.

- Added an initial Windows .NET 8 MSTest project for Diagnostics redaction and supporting input-sanitisation behaviour; included it in the solution and wired it into Windows CI. First Windows execution remains pending.
- Added `docs/FEATURE_TRACEABILITY.md` covering source locations, current coverage and implementation/acceptance gaps across the integrated programme.

- Improved MONOLITH theme switching: central token brushes update in place, tracked legacy brushes are re-mapped without rebuilding pages, MainWindow surfaces use theme tokens, and Player Intelligence report XAML uses DynamicResource bindings. Windows visual acceptance pending.

- Corrected a field-evidence data-integrity defect: substring matching for `Verified` could include `Unverified`. All nine live-fact SQL conditions now use an explicit normalized allowlist; two UI consumers use the shared evidence policy; strict canonical-status tests added.
- Added tests for provider-specific identity separation, exact-ID completeness deduplication and fail-closed unknown/provisional evidence statuses.

## 2026-11-13 — Windows validation and reliability follow-up

- Windows run `43468496760` at `ade4637`: .NET 8 restore and WPF Release build passed; 0 errors; MSTest 40/40 passed; Release and TRX artifacts uploaded. Compiler reported 12 warnings.
- Added ALL/ANY match mode to Advanced Statistics Search and shared `StatConditionMatcher`: missing metric values remain missing/non-matches; per-90 uses player-period minutes; NaN/infinite bounds are rejected.
- Added tested squad one-to-one assignment optimizer and FootballMetricMath calculation helpers. These remain separate services until integrated with validated data/UI flows.
- Corrected fail-closed evidence classification and consumers; preserved the global live-write kill switch.
- Removed 11 unused private MainWindow filter declarations after confirming no references in any seven MainWindow partial files; replaced an obsolete `AtlasLog.Warn` call with `AtlasLog.Warning`. New Windows run required to verify post-cleanup warning count.

- Windows Actions run `43469077009` passed at commit `1022078`: .NET 8 WPF Release build with 0 warnings/0 errors; 40/40 MSTest tests passed.
- Added test-isolated `ScoutDatabase(string databasePath, bool pooling = true)` constructor and SQLite integration coverage for live field evidence query consumers; missing test-file IO import was corrected, Windows execution pending.

## 2026-11-13 — SQL evidence integration and Role Fit scoring

- Windows run `43470026732` at `e109eab`: Release build 0 warnings/0 errors; 41/41 tests passed, including the new actual SQLite evidence-consumer integration test.
- Extracted `RoleFitService.CalculateEstimate` as a single transparent weighted scoring implementation and changed database Role Lab search to use it.
- Added six RoleFit tests for weighted means, coverage, normalized attribute keys, missing values, out-of-range/nonfinite values and profile validation.
- Windows run `43470501984` at `b8bad3a`: Release build 0 warnings/0 errors; **47/47 tests passed**; Release and TRX artifacts uploaded.
- Main branch preserved; 163 commits ahead/0 behind at this checkpoint.


## 2026-11-13 — Integrated development pass, workstreams 1–4

- Added opt-in MONOLITH WPF control styles bound to the existing Obsidian/Warm Light dynamic tokens.
- Added a Moneyball metric direction selector so lower-is-better metrics can be ranked correctly; direction is visible and exported.
- Extended Scouting Matrix with role-profile candidate ranking and add-to-shortlist flow.
- Added an independent recruitment opportunity engine with incumbent-score lift and separate asking-price/weekly-wage budget checks; missing financial fields stay unavailable.
- Added independent in-possession/out-of-possession squad analysis with 11-slot starter templates and phase-compatible role validation.
- Added eight regression tests across cohort direction, recruitment budgeting and dual-phase tactics.
- The new combined branch checkpoint still requires Windows Actions build/test verification and user-PC acceptance. No runtime or copied-save validation is claimed by this changelog entry.
