# FM Atlas — Next Steps

**Active branch:** `overhaul/monolith-full-platform`  
**Current goal:** produce one integrated FM Atlas release candidate while maintaining small, verified engineering checkpoints inside the same branch.

## Source recovery status

The repaired R7.2.0 application source has been imported into `application/R7.2.0/FM-Atlas-R7.2.0-Work/` (source-import commit `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`). This is the active working source.

The previous instructions to run `IMPORT-TO-GITHUB.ps1` and create another import branch are superseded. Do not repeat the source import or overwrite the existing recovered source.

R7.0.2 and R7.1.0 archives are inventoried, hashed and compared, but their complete source trees have not yet been committed as separate immutable repository baselines.

## Current gate: fresh Windows build and diagnostics acceptance

The user-provided R7.2.0 baseline build log reported Windows 10 / .NET SDK 8.0.427, 0 errors and 15 warnings. In addition, GitHub Actions run `43465739690` passed on Windows at source commit `5aa82c2`: restore, WPF Release build, 10/10 MSTest tests and artifact uploads. The subsequent source changes add Ctrl+K, MONOLITH global-search canvas binding, exact field-evidence filtering and provider-specific identity safeguards. Latest source checkpoint is `fd40df0`; workflow run `43466693498` is testing that expanded branch.

A repeatable workflow is present at `.github/workflows/fm-atlas-windows-build.yml`. It restores and builds the WPF solution on Windows with .NET 8 and uploads Release output. No workflow run is currently reported; a successful run must be recorded before calling the current branch build-verified.

### Build acceptance checklist

- [x] Run the Windows Release workflow for source checkpoint `5aa82c2`; all build and test steps passed (10/10).
- [ ] Confirm the active workflow run for source checkpoint `fd40df0` completes successfully; do not use the earlier 10/10 run as proof of the expanded source.
- [ ] Confirm the expected `FmAtlas.exe` exists in `src/FmAtlas/bin/Release/net8.0-windows/`.
- [ ] Record exact commit SHA, runner Windows version, .NET SDK, build output, warnings and artifact.
- [ ] Triage all warnings; do not hide or suppress warnings without a reason.
- [ ] If CI cannot be enabled or triggered, run the same restore/build commands on Windows and attach the complete log.

The Windows TRX artifact for `5aa82c2` confirms 10/10 Diagnostics tests passed. The expanded suite adds exact evidence-state, mixed/provisional status and provider-ID safety cases; confirm the latest 20-test Windows result. The actual C# `ValidateRedaction()` check still needs to execute inside the Windows Diagnostics page.

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
