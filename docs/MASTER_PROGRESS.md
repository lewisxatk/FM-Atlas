# FM Atlas Master Progress

Last updated: 2026-11-13  
Active branch: `overhaul/monolith-full-platform`  
Baseline commit: `2051bbee042fb8e7203b7855e833e391ccee9ecb`  
Latest source-import commit: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`  
Latest successful test checkpoint: `1022078bd4fd79d21f687feb721f6e6e7f4087d7`  
Current source head at documentation update: `e109eabc34aaa5805863031324602d81033efeb1`

## Verified progress

- [x] Repository located and permissions confirmed.
- [x] Existing main history preserved; no changes made to main.
- [x] Dedicated integrated overhaul branch created.
- [x] Existing roadmap, handover, architecture, known issues, recovery inventory and research register inspected.
- [x] Integrated feature scope consolidated in `docs/OVERHAUL_MASTER_PROGRAMME.md`.
- [x] User supplied repaired R7.2.0 v2 build log: Windows .NET 8 WPF Release build succeeded, 0 errors, 15 warnings.
- [x] Build output path confirmed in the log: `src/FmAtlas/bin/Release/net8.0-windows/FmAtlas.exe`.
- [x] Recovered R7.2.0 application tree imported into `application/R7.2.0/FM-Atlas-R7.2.0-Work/` on this branch (source import commit `6069c20`).
- [x] Static recovery audit imported as `recovery/R7.2.0-STATIC-AUDIT.md`.
- [x] The recovery tree contains the 106 tracked source-package files; generated `obj` and build artefacts were not imported.
- [x] Compared repaired v2 ZIP with the recovery import. Correction: eight compile-fix source files differed; the first branch CI build failed with 92 compiler errors. Those eight v2 repairs were applied individually on the overhaul branch, preserving the MONOLITH and safety changes. The repaired branch subsequently passed a Windows .NET 8 Release build and automated tests at commit `5aa82c2`.
- [x] Closed redundant source-import PR #1 after direct import into the overhaul branch; no changes to `main`.

## Newly confirmed product requirement

- [x] User confirmed delivery preference: one integrated development/release candidate, not multiple small user-facing releases.
- [x] Existing Diagnostics / Tester & Logs page, local rolling logs, health checks, copy/export, support ZIP preview, manifest hashes and local-only export inspected in source.
- [x] Fixed over-escaped credential redaction and expanded path redaction to cover complete Windows and UNC paths, including spaces (code checkpoint `6fc4675`).
- [x] Added deterministic support-data redaction regression checks to the Diagnostics health-check list (bearer token, API key, password, user path, spaced drive path, UNC path and standalone save filename).
- [x] Windows .NET 8 Release build and 40/40 expanded MSTest suite passed at commit `1022078` (Actions run `43469077009`); 0 warnings, 0 errors.

## Initial source architecture audit — 2026-11-13

- [x] Confirmed the imported project is a .NET 8 WPF desktop application using Microsoft.Data.Sqlite 8.0.11.
- [x] Confirmed startup initialises AtlasLog, applies MonolithTheme and registers dispatcher, AppDomain and unobserved-task exception logging.
- [x] Mapped the dynamic MainWindow shell, partial UI files, core models, database service and game-adapter services. See docs/SOURCE_ARCHITECTURE_AUDIT.md.
- [x] Confirmed an initial MONOLITH theme service and Obsidian/Warm Light preference already exist; full shared-control and screen migration is incomplete.
- [x] Updated MONOLITH theme token brushes to refresh in place, remap tracked legacy page brushes, and bind main window surfaces to theme tokens.
- [x] Migrated Player Intelligence report XAML surfaces/styles to DynamicResource theme tokens; remaining pages still need migration.
- [ ] Windows theme-switch, high-DPI and accessibility contrast acceptance still required.
- [x] Confirmed exact Game Pass and Steam profile records exist in source, while field-level and user-installation acceptance remain unverified.
- [x] Confirmed Apply and Undo call the global live-write certification gate; IsCertified is hard-coded false, so live memory writes remain blocked.
- [x] Confirmed GraphicsResolver supports a configurable root and config.xml scanning, but has not been tested against the user's actual folders.

## Not yet verified / not complete


- [ ] No application launch evidence yet.
- [ ] No runtime feature acceptance evidence yet.
- [ ] Full source-level audit of every R7.2.0 module remains incomplete; the initial architecture and tool-specific static audit have been recorded.
- [ ] MONOLITH is partially present (central theme service and mode switch); semantic-token coverage, reusable controls and migration of all views remain incomplete.
- [ ] Reverse-engineering register remains desk research/source review, not complete reverse engineering.
- [ ] Exact FM27 Xbox Game Pass build/profile compatibility is unverified.
- [ ] Save parser coverage, import correctness, statistics, percentiles and role-fit require fixtures/known-answer tests.
- [ ] Graphics resolver has not been validated against the user's actual configured folders.
- [ ] Live Apply/read-back/Undo has not been tested on a disposable offline career; live writes must remain guarded/off until then.
- [ ] The complete product roadmap has not been implemented.

## Immediate engineering queue

1. Confirm latest Windows Release build and expanded automated tests; then triage remaining compiler warnings and perform Windows app launch/smoke testing.
2. Finish source-level review and feature-to-file traceability for every imported module.
3. Establish repeatable test fixtures for field provenance, database imports, search, role fit, graphics mapping and recovery.
4. Complete MONOLITH semantic tokens, shared WPF controls and shell migration before feature-screen migration.
5. Expand the universal field registry and Advanced Search while preserving existing query semantics.
6. Implement and validate Player DNA, Scouting Matrix, Moneyball, Tactical Intelligence, Match Lab, squad/recruitment and career-assistant modules.
7. Implement and validate Save Inspector, local editor, Game Pass read-only adapter, graphics/skin and mod research workbench.
8. Add automated tests, fixture coverage, performance/UI tests, installer and release/rollback processes.
9. Produce one integrated release candidate and a complete evidence-based acceptance report.

## Checkpoint discipline

Only mark a feature complete when the source change exists on this branch and its acceptance evidence is linked. A successful compile does not prove runtime functionality. No stage may be described as finished solely because it appears in the roadmap or source comments.

## Latest engineering change log

- 2026-11-13: Recovered-source/v2 comparison corrected: the recovery import had eight stale compile-fix files; all eight were patched on the integrated branch. Windows run `43465739690` succeeded at `5aa82c2` (restore, Release build, 10/10 initial tests and artifact uploads).
- 2026-11-13: Added explicit per-field evidence classification and tests for unverified/candidate/provisional/verified statuses; added provider-safe identity resolver and tests preventing LIVE/FMSAVE fuzzy identity collapse. Current-head Windows run `43466693498` at `fd40df08` is still running at the last check.
- 2026-11-13: FMSave bridge pinned to 0.12.3 and expanded to preserve officials, team match statistics and squad-planner tables in the raw sidecar. Local FM27 Game Pass save compatibility remains unverified.

- 2026-11-13: Corrected credential-redaction patterns, switched to complete drive-letter/UNC path redaction (including paths with spaces), added self-check cases and surfaced the check in Diagnostics. Code commits: `05d2c77`, `f7b9f4c`, `9c90953`, `6fc4675`. Not yet built or run in Windows.

- 2026-11-13: Added the initial Windows MSTest project and CI test execution; committed test code, solution inclusion and workflow updates. Seven sensitive-value pattern cases passed the local pattern-level check; C# tests and WPF build remain unexecuted on Windows.

- 2026-11-13: Improved MONOLITH theme switching so token and legacy brushes update existing controls in place; bound MainWindow and Player Intelligence report surfaces to the theme. Commits: `0929483`, `e9fe226`, `5aa82c2`. Windows UI validation pending.

- 2026-11-13: GitHub Actions Windows run `43465739690` passed at commit `5aa82c2`: .NET restore, WPF Release build, 10/10 MSTest tests, test-results upload and Windows Release artifact upload all succeeded. TRX artifact digest: `6d09bb448e6dafbdab87e49cda69141c0629de9440eec41c8be15d79d343c40b`.
- 2026-11-13: Added Ctrl+K global-search shortcut and MONOLITH canvas background binding in `GlobalSearchWindow` at source commit `830cedc`; independent workflow run was in progress at this checkpoint.

- 2026-11-13: Found and corrected unsafe substring verification checks. Nine SQLite filters now use a normalized exact status allowlist; two UI consumers use `FieldEvidencePolicy`; policy accepts exact canonical statuses only. Added strict allowlist/misleading-prefix tests. Code commits: `8a2af4d`, `8bf70f9`, `c3a721d`, `f32cacd`, `fd40df0`. Latest expanded Windows run: `43466693498` (in progress at handover).

## Latest Windows verification — 2026-11-13 17:03 UTC

- [x] Exact source commit: `ade463712df284034ba475a2f1bc85584e9394d9`.
- [x] GitHub Actions Windows .NET 8 restore and WPF Release build succeeded: 0 errors.
- [x] Automated MSTest suite: 40 executed, 40 passed, 0 failed, 0 skipped.
- [x] Windows Release output and TRX results uploaded. [Actions run 43468496760](https://github.com/lewisxatk/FM-Atlas/actions/runs/43468496760).
- [ ] 12 compiler warnings were triaged: 11 were private MainWindow filter fields never referenced in any of seven partial classes; one was a legacy `AtlasLog.Warn` call. Those have been removed/replaced in commits `886a8a5` and `1022078`. A new CI run is checking the warning cleanup; do not assume zero warnings until it completes.
- Current branch head at this progress update: `1022078bd4fd79d21f687feb721f6e6e7f4087d7`.
- [ ] Windows application launch/UI smoke tests and actual Game Pass integration remain outstanding; CI build success is not runtime acceptance.

## Warning-free verification and SQL integration test — 2026-11-13

- [x] Exact tested source commit: `1022078bd4fd79d21f687feb721f6e6e7f4087d7`.
- [x] Windows .NET 8 Release restore and build passed: **0 warnings, 0 errors**.
- [x] MSTest result: **40 passed, 0 failed, 0 skipped**.
- [x] Run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43469077009.
- [x] Removed 11 genuinely unused private filter declarations after checking every MainWindow partial; replaced obsolete `AtlasLog.Warn` usage. Zero compiler warnings verified at `1022078`.
- [x] Added disposable-path constructor for `ScoutDatabase` and a SQLite integration test covering canonical verified, read-back verified, unverified, provisional and mixed-status CA/PA/attribute rows.
- [x] SQLite evidence-consumer integration test passed on Windows at `e109eab` (run `43470026732`): 41/41 tests, 0 warnings, 0 errors.
- [x] Latest combined Role Fit extraction and SQLite integration source passed Windows CI at `b8bad3a` (run `43470501984`): 47/47 tests, 0 warnings, 0 errors.
- Current code head at this entry: `e109eabc34aaa5805863031324602d81033efeb1`.

## Latest full Windows verification — 2026-11-13 17:14 UTC

- [x] Exact tested commit: `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712`.
- [x] Windows .NET 8 Release restore/build succeeded: **0 warnings, 0 errors**.
- [x] MSTest: **47 executed, 47 passed, 0 failed, 0 skipped**.
- [x] The 47 tests include the SQLite live-fact evidence-consumer integration test and six RoleFit scoring tests in addition to earlier Diagnostics, field-evidence, identity, assignment, metric and search tests.
- [x] Windows Release and TRX artifacts uploaded. Run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43470501984.
- [x] The dedicated SQLite test was independently seen passing at the preceding source commit `e109eab` (run `43470026732`): 41/41, 0 warnings/errors.
- [ ] Application UI launch, route-by-route smoke tests, theme/DPI visual tests, actual support ZIP inspection, Game Pass process integration and save acceptance remain outstanding.
- Current branch head at this entry: `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712`; `main` remains at `2051bbee042fb8e7203b7855e833e391ccee9ecb`, with the overhaul branch 163 commits ahead and 0 behind at verification.

## Role Lab explainability UI checkpoint — 2026-11-13

- [x] Added visible per-role weighted-attribute chips to `MainWindow.RoleLab.cs`; every selected profile now displays its named inputs and exact Atlas weight factors, sorted by weight.
- [x] Added a plain-language method note: only finite FM attribute values in the 1–20 range contribute; the score is a weighted mean of available inputs; at least 50% of requested attributes must be present; missing values are not treated as zero.
- [x] Kept the distinction from FM's proprietary internal role formula explicit. These are Atlas model weights, not claimed SI weights.
- [x] Windows .NET 8 WPF Release build for this UI change passed with **0 warnings, 0 errors**; MSTest passed **47/47** (0 failed/skipped). Run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43471187844.
- [x] Windows Release artifact ID `14061656624` and test-results artifact ID `14061910345` uploaded.
- [ ] WPF runtime visual acceptance remains pending; CI compilation does not prove layout, readability or behaviour in the running app.
- Source change: `4b4ddfcb7299a0fad5dc2ea60658d895a48127df`.


## Latest Role Lab Windows verification — 2026-11-13 17:18 UTC

- [x] Exact application source commit: `4b4ddfcb7299a0fad5dc2ea60658d895a48127df`.
- [x] Windows Actions run `43471187844`: WPF Release build succeeded with 0 warnings/0 errors; 47/47 tests passed.
- [x] Role Lab now exposes the selected profile's weighted inputs and scoring/missing-value method note in the UI source.
- [ ] Launch and visually inspect Role Lab on Windows at supported DPI/resolutions; confirm chips wrap/read clearly and role selection refreshes the inputs.

## Latest Role Fit contribution checkpoint — 2026-11-13 17:27 UTC

- [x] Extended `RoleFitService` to return an auditable contribution for every available, valid role attribute: readable attribute name, source value, Atlas weight and additive score points.
- [x] Contribution points are calculated as terms of the weighted mean and sum back to the displayed score. The calculation scales weights before summing to avoid overflow from extreme finite inputs.
- [x] Propagated contribution records through `ScoutDatabase.SearchRoleFitCandidatesAsync` to the Role Lab candidate rows.
- [x] Added a selected-player contribution panel beneath the result grid; changing role clears stale results, selecting a player refreshes the detail, and CSV export includes all contribution terms.
- [x] Preserved readable attribute labels through normalization and added a regression assertion for `Off The Ball`.
- [x] Windows Release and test checkpoint: exact source commit `91bd7728c555a0c0e54cfd8d02706467fdafdb89`; Actions run `43472380514`; build **0 warnings / 0 errors**, **48/48 tests passed**.
- [x] Artifacts uploaded: Windows Release `14061643285` and test results `14061143387`.
- [ ] WPF visual/runtime acceptance remains pending: inspect the contribution panel at real Windows resolutions/DPI and test selection changes/CSV output with an imported FM27 save copy.
- Note: intermediate commit `2f978f3` failed compilation because local UI variables were captured before assignment; fixed in `0c6546a`, and the final `91bd772` run passed. Do not treat the intermediate failure as the current source state.

## Latest integrated Windows verification — 2026-11-13 17:33 UTC

- [x] Exact tested application source commit: `8cfe5b3f276770179e8cb77db7a02912f2336bdb`.
- [x] Windows Actions run `43473487285` completed successfully: .NET 8 WPF Release build **0 warnings / 0 errors**; MSTest **53/53 passed**, 0 failed, 0 skipped.
- [x] Windows Release artifact `14061763980` and test-results artifact `14062007740` uploaded.
- [x] Coverage includes the Role Fit per-player contribution model and SQLite query integration, 48 prior checks plus expanded Diagnostics redaction cases for quoted JSON/config secrets, Basic authorization, client secrets and query-string tokens.
- [x] Role Fit database integration test seeds an isolated SQLite store and proves that the real candidate query returns readable contribution labels, values, Atlas weights, additive score points, weighted score and 2/3 missing-data coverage.
- [x] Diagnostics redaction pipeline and regression tests cover bearer/basic tokens, quoted secrets, credentials, query tokens, Windows/UNC paths and standalone save filenames.
- [ ] WPF app launch, route-by-route UI acceptance, real imported FM27 save-copy test, theme/DPI visual checks and actual support-ZIP manual inspection remain outstanding.
- The active branch head after this tested commit is `28958fd4bc07c1f54462ec0e829dd22402eed922`; the one commit after the tested source changes only `docs/MASTER_PROGRESS.md`. `main` remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`.
- Live memory Apply/Undo remains disabled until exact Game Pass build/field certification and disposable-save read-back/undo acceptance.
