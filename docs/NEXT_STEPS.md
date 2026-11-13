# FM Atlas — Next Steps

**Active branch:** `overhaul/monolith-full-platform`  
**Current goal:** produce one integrated FM Atlas release candidate while maintaining small, verified engineering checkpoints inside the same branch.

## Source recovery status

The repaired R7.2.0 application source has been imported into `application/R7.2.0/FM-Atlas-R7.2.0-Work/` (source-import commit `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`). This is the active working source.

The previous instructions to run `IMPORT-TO-GITHUB.ps1` and create another import branch are superseded. Do not repeat the source import or overwrite the existing recovered source.

R7.0.2 and R7.1.0 archives are inventoried, hashed and compared, but their complete source trees have not yet been committed as separate immutable repository baselines.

## Current gate: fresh Windows build and diagnostics acceptance

The user-provided R7.2.0 baseline build log reported Windows 10 / .NET SDK 8.0.427, 0 errors and 15 warnings. The latest completed expanded Windows run `43468496760` passed at commit `ade4637`: .NET 8 restore, WPF Release build, 40/40 MSTest tests, Release artifact and TRX uploads; 0 errors, 12 compiler warnings. Commits `886a8a5` and `1022078` then removed 11 unused private filter fields and replaced the one obsolete AtlasLog.Warn call. CI for those warning-cleanup commits is running/queued; confirm their exact result before claiming the warnings are gone.

A repeatable workflow is present at `.github/workflows/fm-atlas-windows-build.yml`. It restores and builds the WPF solution on Windows with .NET 8 and uploads Release output. The workflow has successful Windows runs; see the exact tested commit and 40/40 result above. Each newer source commit still requires a fresh run.

### Build acceptance checklist

- [x] Run the Windows Release workflow for source checkpoint `ade4637`; WPF Release build and 40/40 tests passed.
- [ ] Confirm the queued/current workflow for warning-cleanup source checkpoint `1022078` completes successfully.
- [ ] Confirm the expected `FmAtlas.exe` exists in `src/FmAtlas/bin/Release/net8.0-windows/`.
- [ ] Record exact commit SHA, runner Windows version, .NET SDK, build output, warnings and artifact.
- [ ] Triage all warnings; do not hide or suppress warnings without a reason.
- [ ] If CI cannot be enabled or triggered, run the same restore/build commands on Windows and attach the complete log.

The Windows run for `ade4637` confirms 40/40 tests passed, covering Diagnostics, evidence policy, provider identity, squad optimiser, metric math, ALL/ANY matching and finite boundaries. The latest warning-cleanup commit still needs its own run. The actual C# `ValidateRedaction()` check still needs to execute inside the Windows Diagnostics page.

### Runtime smoke checklist

- [ ] Launch the app and confirm startup/database initialisation.
- [ ] Visit every navigation route and record failures, blank pages, fallback pages and dead controls.
- [ ] Run Diagnostics; confirm the privacy regression check passes.
- [ ] Export a support ZIP, inspect every entry, verify its manifest hashes and confirm no save/database/personal files were included by default.
- [ ] Test Obsidian and Warm Light, 100/125/150% DPI and 1080p/1440p layouts.
- [ ] Record screenshots, logs, environment and exact commit as acceptance evidence.

## Engineering sequence after the build gate

1. Finish source-to-feature traceability for every R7.2.0 module and record which routes are genuinely usable.
2. Add fixture-backed tests for diagnostics redaction/log rotation, imports, missing values, search truth tables, per-90 calculations, percentiles and role-fit.
3. Complete MONOLITH semantic tokens and reusable WPF controls; migrate the shell before migrating feature pages.
4. Build the canonical field registry with provenance, version, units, visibility, confidence and verified-use status.
5. Expand Advanced Search to full field-aware conditions, grouped AND/OR, saved profiles, explanations and export.
6. Complete Player DNA, Scouting Matrix, Moneyball Scout, recruitment, squad planning, Tactical Intelligence, Match Lab and Career Assistant.
7. Complete offline Save Inspector, local Database/Editor Studio, Game Pass read-only adapter, Graphics/Skin Studio and Modding Research Workbench.
8. Add database migration/rollback, installer/update, performance, accessibility, regression and Windows release acceptance.
9. Create one integrated release candidate with verified/experimental/unsupported feature labels and a full acceptance report.

## User-data and live-edit safety

- Keep the original local database and career saves backed up.
- Use copies for save imports and acceptance.
- Do not upload personal saves or licensed graphics.
- Live Apply/Undo remains disabled by the global certification gate. It must stay disabled until exact Game Pass build detection, field-level independent witnesses, disposable offline Apply/read-back/Undo and career-switch rollback tests all pass.

## Stop conditions

Stop and inspect the evidence before proceeding if a build changes the database unexpectedly, a save import silently drops unknown fields, an unknown build is matched, a memory pointer becomes stale, a support export contains personal data or a UI action has no real supported operation.

### Field evidence regression gate

- [x] Remove every `LIKE '%Verified%'` and `Contains("Verified")` consumer that could match `Unverified`.
- [x] Require exact normalized canonical statuses in `FieldEvidencePolicy`.
- [x] Add strict status allowlist and misleading-prefix tests.
- [ ] Confirm latest Windows run and inspect TRX; then add SQLite-level regression fixtures proving unverified/provisional rows do not enter live CA/PA/attribute result sets.

## Current next engineering gate

1. Confirm Windows CI for current source head `1022078` and record the warning count. The immediately prior test checkpoint `ade4637` passed 40/40 with 12 warnings; 11 dead fields and one obsolete logging call have since been cleaned.
2. Add query-level SQLite fixtures proving unverified/provisional live facts cannot influence CA/PA/attribute results (unit policy tests alone do not prove SQL semantics).
3. Finish source-to-feature route acceptance and safe diagnostics/support-ZIP inspection on Windows.
4. Expand canonical field provenance and exact-build profile coverage; keep unknown builds and live Apply/Undo read-only/blocked.
5. Continue MONOLITH token/shared-control migration and connect tested metric/squad engines to real UI only when required inputs are valid and provenance is visible.

## Latest build and new test status

- Windows Actions run `43469077009` at `1022078`: build succeeded with **0 warnings / 0 errors**; **40/40 tests passed**.
- New SQL integration fixture `ScoutDatabaseEvidenceIntegrationTests` checks real SQLite CA/PA and attribute query outputs for verified versus unverified/provisional/mixed statuses. First run failed to compile due to a missing `System.IO` import; fixed in `e109eab`.
- [ ] Confirm the latest run for `e109eab` and subsequent pooling fixture changes; aim for 41/41 or more tests, zero build warnings.
- [ ] If the integration test fails at runtime, inspect SQLite schema/fixture errors and correct them; do not remove the consumer test to restore a green build.

## Latest tested checkpoint

- Windows run `43470501984`, commit `b8bad3a`: Release build passed with 0 warnings/0 errors; **47/47 tests passed**.
- This includes disposable SQLite integration proof that unverified/provisional live facts do not enter CA/PA and attribute search, plus six RoleFit estimate tests.
- The next gate is no longer basic source compilation. It is application and feature acceptance: launch the WPF app on Windows, inspect all 21 routes, test theme switching/high-DPI, inspect support ZIP contents, validate Role Lab with a representative fixture, and verify no regression in user database migration/import.
- Do not wire SquadSelectionOptimizer into Squad Architect until exact slot eligibility and position familiarity are available from validated sources. Do not enable live memory Apply/Undo.

## Active source checkpoint after 47-test baseline — 2026-11-13

- Last fully verified application source: `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712` — Windows .NET 8 WPF Release build succeeded, 0 warnings/0 errors, 47/47 MSTest tests passed. CI: https://github.com/lewisxatk/FM-Atlas/actions/runs/43470501984.
- New source commit `4b4ddfcb7299a0fad5dc2ea60658d895a48127df` adds the selected role's complete weighted-attribute list and a method/missing-data note to Role Lab. Windows CI: https://github.com/lewisxatk/FM-Atlas/actions/runs/43471187844. It completed successfully: 0 warnings/0 errors, 47/47 tests passed; Release artifact `14061656624`, TRX artifact `14061910345`.
- Next product work: validate the rendered Role Lab contribution panel and CSV export on Windows using a copied FM27 save; then continue the remaining integrated product roadmap. Keep coverage clearly identified as an available-attribute count, not a probability.
- Do not add guessed position eligibility to a Best XI optimiser. The optimizer must receive a verified candidate-to-slot score matrix, with source IDs kept distinct unless a cross-provider identity link is evidenced.
- Continue the broader MONOLITH shared-control migration, full Advanced Search profiles/condition groups, player DNA, Moneyball cohorts, Recruitment Matrix, Tactical Intelligence, Match Lab, Save Inspector, editor, Game Pass read-only adapter, graphics/skin tools, mod workbench, tests and packaging.
- Still required from the user's Windows/Game Pass environment: launch and route smoke tests, Obsidian/Warm Light and DPI checks, support-ZIP manual inspection, actual save-import validation on a copy, and exact FM27 Game Pass build/runtime checks. Live Apply/Undo stays disabled.

## Latest verified source checkpoint — 2026-11-13 17:33 UTC

- Exact tested application source commit: `8cfe5b3f276770179e8cb77db7a02912f2336bdb`.
- Windows Actions run `43473487285`: .NET 8 WPF Release build **0 warnings / 0 errors**; **53/53 tests passed**, 0 failed/skipped. Link: https://github.com/lewisxatk/FM-Atlas/actions/runs/43473487285.
- Windows Release artifact `14061763980`; TRX artifact `14062007740`.
- The Role Fit contribution model is now covered from the UI data path through the database query. A disposable SQLite test proves the candidate row includes readable attribute labels, raw values, Atlas weights, additive score points, overall score and 2/3 coverage when one of three fields is absent.
- Diagnostics redaction now includes quoted JSON secrets, Basic auth, client secrets, query-string tokens, personal Windows/UNC paths and standalone save filenames in both the test suite and in-app self-check.
- All follow-on commits after tested source `8cfe5b3` are documentation-only checkpoint updates; the application source is unchanged from the 53-test run. Main remains preserved at `2051bbee042fb8e7203b7855e833e391ccee9ecb`; fetch the live branch ref before resuming.
- Next required user-environment acceptance: launch the WPF app, route-by-route smoke check, test Role Lab selection and CSV on an imported copy of an FM27 Game Pass save, theme/DPI screenshots, inspect support ZIP entries and validate GraphicsResolver against actual folders.
- Continue the broader programme: Advanced Search full profiles and nested groups, Player DNA, Scouting Matrix/recruitment, Moneyball cohort percentiles, Tactical Intelligence, Match Lab, development assistant, Save Inspector, local editor, Game Pass read-only adapter, Graphics/Skin Studio, mod workbench, installer and release/rollback.
- Preserve read-only behaviour for unsupported builds. Live Apply/Undo remains disabled until field-level certification and disposable-save read-back/rollback acceptance passes.


## Agreed execution order — 2026-11-13

The user requested Stage 1 and Stage 2 to be completed together first, then receive a findings report. After that review, the user will decide whether to combine the remaining stages into one integrated implementation and test it. Do not begin Stage 3–7 before that approval.

- Combined findings and route map: `docs/STAGE1_STAGE2_AUDIT.md`.
- Stage 1: finish exact-commit CI for the Diagnostics privacy-copy/manifest correction; manually generate and inspect both support ZIP variants on Windows; verify manifest hashes and excluded files.
- Stage 2: finish static source/feature traceability for all 21 routes; run Windows route smoke tests and capture actual Game Pass build, save import and graphics folder acceptance evidence.
- Once both stages pass, return the report with confirmed findings, incomplete features, risk boundaries and proposed unified Stage 3–7 scope. Wait for the user's approval before beginning it.
