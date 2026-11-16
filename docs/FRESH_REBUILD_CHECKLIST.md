# FM Atlas — Fresh Rebuild Master Checklist

This checklist is the live task tracker. Update it in the same commit as the checkpoint whenever possible.

Legend: [ ] Not started · [~] In progress · [x] Completed with evidence · [!] Blocked · [-] Deferred

## M0 — Protect and inventory
- [x] M0.1 Confirm active branch and keep main untouched.
- [x] M0.2 Locate the R7.2.0 recovery ZIP in Library metadata and verify the corresponding imported GitHub recovery branch contains 106 application files. Raw ZIP materialization was refused by the file service; GitHub source tree is the recovered working reference.
- [x] M0.3 Preserve R7.1.0 and R7.0.2 historical records and comparison documentation in the recovery branch.
- [x] M0.4 Record uploaded-package hashes and inspect ZIP manifests.
- [x] M0.5 Inspect user-supplied research packages and relevant public repositories.
- [x] M0.6 Record licence and reuse decision per source.
- [x] M0.7 Complete first-pass audit of reusable research, data schemas, graphics index and tests.
- [x] M0.8 Decide legacy UI/architecture to discard; clean .NET 8 WPF architecture approved.
- [x] M0.9 Create and commit a separate fresh-rebuild checkpoint branch.

## M1 — Research and blueprint
- [~] M1.1 FMLibreEditor Game Pass profile and live-edit safety study (initial study done; continue field-by-field).
- [~] M1.2 FMSuperScout scanner/extraction/index study (initial study done; inspect scanner implementation/tests further).
- [~] M1.3 fm-editor build layout/entity/undo study (initial study done; field evidence review ongoing).
- [~] M1.4 fmsave offline save parser coverage study (initial study done; schema/coverage validation still needed).
- [x] M1.5 FM Player Analyzer parsing/graphics/profile architecture inspected.
- [~] M1.6 FMRTE feature and workflow inventory (official feature audit 001 committed; full UI/feature audit still required).
- [~] M1.7 FM27 canonical player field and attribute catalogue (52 unique non-foot attributes, 8 personality values, 15 position ratings documented; verify exact profile applicability/order).
- [ ] M1.8 FM27 attribute display order verified against FM27 player profile and FMRTE.
- [ ] M1.9 Staff and club field catalogue complete.
- [ ] M1.10 User's Game Pass build/process fingerprint verified on Windows.
- [ ] M1.11 Read/write coverage matrix established field-by-field.
- [x] M1.12 Fresh architecture decision documented.

## M2 — Fresh application foundation
- [x] M2.1 New clean solution and project boundaries created (Domain, Application, Infrastructure, Desktop, Tests).
- [x] M2.2 Windows GitHub Actions restore/build/test passed on Windows Server 2025 with .NET SDK 8.0.428; build 0 warnings/0 errors; 3 unit tests passed. See run 43787424965. Artifact upload workflow is pending separately.
- [~] M2.3 Initial application shell created against REF-001 composition; visual comparison not yet performed.
- [~] M2.4 Watched visual tokens (black/off-white/orange) added; font/spacing polish and screenshot comparison remain.
- [x] M2.5 Compact left navigation has no Search item.
- [~] M2.6 Top Quick Search placeholder added; actual search is intentionally disabled until real data/index exists.
- [~] M2.7 Top Advanced Search action placed; intentionally disabled until query service/dialog exists.
- [~] M2.8 Prominent right-side Load Game button added; currently performs process/module presence check only, not career loading.
- [~] M2.9 Compact Save Atlas / Save Changes controls placed; intentionally disabled until persistence/live writer exist.
- [ ] M2.10 Workspace/player tabs.
- [ ] M2.11 Settings, logs, diagnostics and local persistence.
- [ ] M2.12 Windows desktop resize/accessibility tests.

## M3 — Game connection and data
- [ ] M3.1 FM process detection.
- [ ] M3.2 Storefront/build identification.
- [ ] M3.3 Career loaded detection.
- [ ] M3.4 Read-only live data acquisition.
- [ ] M3.5 Offline save import.
- [ ] M3.6 Typed local database and migrations.
- [ ] M3.7 Player/staff/club entity mapping.
- [ ] M3.8 Data provenance and unknown-value handling.
- [ ] M3.9 Career switch/reload invalidation.
- [ ] M3.10 Data accuracy comparison against FM.

## M4 — Player workspace
- [ ] M4.1 Full-screen REF-004 profile layout.
- [ ] M4.2 Left identity and information rail.
- [ ] M4.3 Player graphics locator.
- [ ] M4.4 Central attribute display.
- [ ] M4.5 Verified FM attribute names/grouping/order.
- [ ] M4.6 CA and PA display.
- [ ] M4.7 Right-side grounded information panels.
- [ ] M4.8 Compact top-of-profile FMRTE-style tabs.
- [ ] M4.8a Information and complete attribute view.
- [ ] M4.8b Contract.
- [ ] M4.8c Stats (read-only, real save data).
- [ ] M4.8d Unavailability.
- [ ] M4.8e Transfer Proposals.
- [ ] M4.8f Retire Date.
- [ ] M4.8g Positions.
- [ ] M4.8h Preferred Moves.
- [ ] M4.8i Relationships and Languages.
- [ ] M4.8j Scout Information (verified only).
- [ ] M4.8k Conditional Staff and Clone Center.
- [ ] M4.8l History and Personality/Hidden where supported.
- [ ] M4.9 Multiple open player tabs.
- [ ] M4.10 Unsaved change protection.

## M5 — Search
- [ ] M5.1 Top Quick Search.
- [ ] M5.2 REF-002 FMRTE-style Advanced Search dialog.
- [ ] M5.3 Player / Coach / Club selectors.
- [ ] M5.4 General filters.
- [ ] M5.5 Contract filters.
- [ ] M5.6 Technical/GK filters.
- [ ] M5.7 Mental filters.
- [ ] M5.8 Physical/Hidden filters.
- [ ] M5.9 Preferred Moves filters.
- [ ] M5.10 Relations filters.
- [ ] M5.11 Position pitch and suitability.
- [ ] M5.12 Min/max, boolean and lookup controls.
- [ ] M5.13 Explicit AND/OR semantics.
- [ ] M5.14 Saved searches and result sorting/columns.
- [ ] M5.15 Search correctness/performance tests.

## M6 — Live editing
- [ ] M6.1 Separate guarded write adapter.
- [ ] M6.2 Build profile match required.
- [ ] M6.3 Entity and save-generation checks.
- [ ] M6.4 Address/type/range/expected-value preflight.
- [ ] M6.5 Staged edit workflow.
- [ ] M6.6 CA/PA write and read-back on disposable save.
- [ ] M6.7 Visible attribute writes and read-back.
- [ ] M6.8 Undo/rollback verification.
- [ ] M6.9 In-game persistence after continue/reload.
- [ ] M6.10 Hidden/personality/positions writes assessed individually.
- [ ] M6.11 Staff and club writes assessed individually.
- [ ] M6.12 Other categories assessed individually.

## M7 — Staff and clubs
- [ ] M7.1 Staff search/profile.
- [ ] M7.2 Staff attribute and contract information.
- [ ] M7.2a Career Plans, Promises and Swap Staff.
- [ ] M7.2b Training Rating, Formations/Tendencies and Knowledge.
- [ ] M7.2c Relationships, Retire Date and Languages.
- [ ] M7.3 Staff verified editing.
- [ ] M7.4 Club search/profile.
- [ ] M7.5 Club facilities/finances/stadium information.
- [ ] M7.5a Squads, Youth Intake and Club Vision.
- [ ] M7.5b Possible Signings, Player Clauses and Transfer Center.
- [ ] M7.5c Staff, Wage Contributions, Kits/Colours and Affiliations.
- [ ] M7.6 Club verified editing.

## M8 — Release
- [ ] M8.1 Full regression suite.
- [ ] M8.2 Clean Windows build.
- [ ] M8.3 Game Pass acceptance tests.
- [ ] M8.4 Backup/restore tests.
- [ ] M8.5 Installer/package.
- [ ] M8.6 Release notes and known limitations.
- [ ] M8.7 User acceptance sign-off.

## Deferred after basic editor
- [-] M9 Squad Studio.
- [-] M10 Tactical Studio and Tactical Effectiveness.
- [-] M11 Recruitment and Value Finder.
- [-] M12 Wyscout/StatsBomb-inspired Moneyball/per-90/percentile analytics.

## Current status
- [x] Approved design references REF-001 to REF-004 documented.
- [x] Fresh rebuild product scope and milestones documented.
- [x] Interruption-safe continuity procedure documented.
- [x] Recovery branch/source inventory checked.
- [x] Uploaded research package inventory and first research findings committed.
- [x] Architecture decision documented.
- [x] FMRTE 27 feature audit and field coverage ledger committed.
- [x] Clean WPF solution and initial shell source committed.
- [x] Windows CI build and three unit tests passed.
- [~] Windows build artifact upload workflow running; desktop runtime and visual comparison still pending.
