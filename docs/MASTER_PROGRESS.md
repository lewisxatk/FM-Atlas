# FM Atlas Master Progress

Last updated: 2026-11-13  
Active branch: `overhaul/monolith-full-platform`  
Baseline commit: `2051bbee042fb8e7203b7855e833e391ccee9ecb`  
Latest source-import commit: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`  
Latest successful test checkpoint: `5ff275d534245880242a7280f95837f6ee3a3b0d`  
Current source head at documentation update: `4fdd17c344a00c64754218a4770e97c33a19f5eb`

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
- This statement was superseded by the later SupportBundleComposer and SavedSearchProfileStore source additions. The exact follow-on application source is now verified by run `43476085012` at commit `6c18e36` with 64/64 tests, 0 warnings and 0 errors. The protected `main` branch remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`. Check the live branch ref before resuming.
- Live memory Apply/Undo remains disabled until exact Game Pass build/field certification and disposable-save read-back/undo acceptance.


## Combined Stage 1 + Stage 2 audit — 2026-11-13

- [x] Created `docs/STAGE1_STAGE2_AUDIT.md` as the authoritative combined report for reliability/Diagnostics and reverse-engineering/source audit.
- [x] Audited the actual Tester & Logs implementation: health checks, report builder, explicit ZIP file allowlist, log collection, SHA-256 manifest, preview, local save dialog and temporary-file write/move flow.
- [x] Found and corrected a privacy-copy mismatch: personal-details opt-in previously claimed to include save filenames and local absolute paths, but the final redaction policy removes those. Opt-in now covers player names and recent edit history only; credentials/tokens, absolute paths and career save filenames are always redacted. Manifest and preview declare the always-redacted categories.
- [x] Updated Help & Tutorial privacy wording to match the Diagnostics page and support README.
- [x] Mapped all 21 navigation destinations to their actual route handlers and implementation scope. The report distinguishes implemented, shared, narrow, informational and safety-blocked routes.
- [x] Audited the 41-file application source tree and 11-file MSTest project tree (10 C# test files plus csproj), WPF shell, SQLite data layer, save bridge, live-memory adapter, field evidence, identity, role fit, search, assistant and graphics resolver.
- [x] Recorded reverse-engineering findings, external reference projects and licence cautions in `docs/STAGE1_STAGE2_AUDIT.md`.
- [x] Windows CI passed on exact integrated Stage 1/2 source commit `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991`: .NET 8 WPF Release build 0 warnings/0 errors; 64/64 MSTest cases passed.
- [ ] Launch Tester & Logs on Windows and inspect default and opt-in ZIPs, entries, report/README, hashes and excluded data.
- [ ] Launch all 21 routes on Windows and record smoke-test results, visual defects and data-dependent states.
- [ ] Confirm exact Game Pass process/build and actual graphics root on the user's Windows installation; cannot be proven by static source review.
- **Stage boundary:** Stage 3–7 remain paused. Do not start the combined product/design implementation until the user reviews the Stage 1/2 report and approves.

## Support bundle composer extraction — 2026-11-13

- [x] Extracted support ZIP composition from the Diagnostics window into `Services/SupportBundleComposer.cs`.
- [x] The composer accepts only the diagnostic report and specifically named current/rotated log files; arbitrary filenames and path traversal are rejected.
- [x] Report and log text are passed through the redaction pipeline; README, preview and manifest state personal-details opt-in and always-redacted categories consistently.
- [x] Manifest records SHA-256 and byte length for every payload file other than the manifest itself; the archive is built from the same exact in-memory package shown in the preview.
- [x] Added five automated tests for allowlisted ZIP entries, archive round-trip, manifest hashes/sizes, redaction and path-traversal rejection.
- [x] Windows Actions run 43476085012 tested application commit 6c18e36fdc706fc49ad74ce15fe1ee73c43ce991: .NET 8 WPF Release build succeeded with 0 warnings / 0 errors; 64/64 tests passed, 0 failed/skipped. This includes the support-bundle composer and saved-search profile store/UI.
- [ ] Manual Windows export inspection remains required; automated composer tests cover archive entries, hashes, redaction and traversal rejection.
- Current tree inventory at the tested source: 41 application source files and 11 test-project files (including the project file).
- Earlier run `43474815591` failed compilation due malformed C# report strings introduced during the privacy-copy edit. The syntax was corrected in `d60206a`; the corrected source is included in the later pending run. Keep the failed run visible in the audit trail rather than treating it as a passing build.


## Stage 1 + Stage 2 final Windows code checkpoint — 2026-11-13 17:49 UTC

- [x] Exact tested application source: `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991`.
- [x] Windows Actions run `43476085012`: .NET 8 WPF Release build succeeded, **0 warnings / 0 errors**; MSTest **64 passed, 0 failed, 0 skipped**.
- [x] Windows Release artifact `14062557163`; test-results artifact `14062892033`.
- [x] The 64 executed cases cover the prior 53 checks plus support-bundle allowlist/ZIP round-trip/manifest hashes/redaction/path-traversal tests, saved-profile persistence and validation, and SQLite isolation of provisional `LIVE:` mirror records from generic local search/count, Attribute Search, Role Lab and Mass Edit.
- [x] Extracted support-bundle composer is the exact code used by Diagnostics preview and local ZIP output. It accepts only named diagnostic/report log payloads, always redacts credentials/tokens, absolute paths and save filenames, and records SHA-256 and byte sizes for payload entries.
- [x] Corrected Help/Settings/Mass Edit copy so it no longer describes live memory Apply/Undo as merely “off by default”: the build-wide certification gate means no in-game writes are available. `LiveWriteCertification.IsCertified` remains false.
- [x] Completed static source audit and 21-route map. Findings are documented in `docs/STAGE1_STAGE2_AUDIT.md`; third-party reverse-engineering notes and licence boundaries are in `research/RESEARCH_REGISTER.md`.
- [ ] Windows runtime acceptance still requires the user to launch the application, smoke-test 21 routes, test themes/DPI, generate and inspect both default and personal-details opt-in support ZIPs, validate a copied FM27 Game Pass save import and verify actual graphics paths.
- [ ] fmsave import provenance remains incomplete: normalized save CA/PA/value/attributes and season-stat fields are not yet joined to field-level parser status/coverage during every query. The audit records this as a critical follow-on requirement.
- Stage 3–7 remain paused pending the user's review and explicit approval. Do not start the combined product/design implementation.
## Completed Stage 1/2 engineering checkpoint — 2026-11-13 17:49 UTC

- [x] Exact tested application source commit: `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991`.
- [x] Windows Actions run `43476085012`: .NET 8 WPF Release build succeeded, **0 warnings / 0 errors**; MSTest **64/64 passed**, 0 failed/skipped.
- [x] SupportBundleComposer and SavedSearchProfileStore are included in the passing build. Test coverage includes strict support ZIP allowlisting, ZIP round-trip, manifest hashes/sizes, privacy redaction, invalid path rejection and saved-search profile persistence/validation.
- [x] Expanded SQLite evidence isolation verifies untrusted LIVE mirror rows do not leak into generic local search/count, Attribute Search, Role Lab and Mass Edit consumers.
- [x] Stage 2 route mapping covers all 21 destinations. Source-level research now includes fmsave 0.12.3 trust/build support, FMLibreEditor's Game Pass 0.9.2435.0 observations and write safeguards, fm-editor's per-build witnessed field model, and FMSuperScout's read-only scanner and explicitly heuristic market-value model.
- [x] Key gaps documented: save-import field provenance is not yet joined to every normalized player/stat field; Transfers can hide live records when any local rows exist; Tactical Lab is informational; Squad Architect is not yet a validated Best XI optimizer; Global Search/Assistant are narrow; live Apply/Undo remains disabled.
- [ ] Remaining user-environment acceptance: launch the WPF app, smoke-test all 21 routes, inspect actual support ZIPs, import a disposable FM27 save copy, check Obsidian/Warm Light and DPI, and identify the installed Game Pass build/graphics root.
- Stage 3–7 remain paused pending the user's review and explicit approval. No broad overhaul implementation has started.

## Latest integrated Search Profiles + Support Bundle checkpoint — 2026-11-13 17:50 UTC

- [x] Added SavedSearchProfile domain models and a local, schema-versioned SavedSearchProfileStore at %LOCALAPPDATA%/FmAtlas/search-profiles.json.
- [x] Stats Search and Attribute Search now provide New / Save / Load / Delete profile controls. Statistics profiles preserve named metric conditions, ALL/ANY, per-90 and minimum minutes; attribute profiles preserve named 1–20 conditions and their text filter.
- [x] Profiles contain criteria only, never player records or save contents. Writes use a temporary file and atomic replacement; the store validates bounds, duplicate names, condition count, profile count and schema. Missing field keys are not silently substituted when loading.
- [x] Added profile-store tests for round-trip, update/create timestamp, duplicate names, delete, invalid numeric/attribute limits and corrupt-file preservation.
- [x] Extracted support ZIP creation into SupportBundleComposer with a strict filename allow-list, path-traversal rejection, redacted report/log inputs, preview text, payload size/SHA-256 manifest, and deterministic ZIP creation.
- [x] Added support-bundle tests for allow-listed entries, manifest hashes/sizes, archive round-trip, redaction and arbitrary-path rejection.
- [x] Exact tested source commit: 6c18e36fdc706fc49ad74ce15fe1ee73c43ce991; Windows Actions run 43476085012: Release build 0 warnings / 0 errors, 64/64 tests passed, 0 skipped.
- [x] Uploaded Windows Release artifact 14062557163 and test-results artifact 14062892033. The TRX independently records 64 passed and 0 failed. The downloaded Release ZIP contains FmAtlas.exe, .NET runtime dependencies, SQLite and save-bridge tools.
- [ ] Windows runtime acceptance remains outstanding: launch both search screens, create/save/load/update/delete profiles, restart the app and confirm persistence; verify missing-field handling, ALL/ANY and per-90 application, and that no player/save data is stored in the profile file.
- [ ] Manually generate default and personal-details-opt-in support ZIPs from the running app; inspect exact entries, preview, README, manifest hashes and always-redacted content.
- [ ] The user has reiterated that the full roadmap should continue as one integrated programme. Internal engineering checkpoints and CI are retained for safety, but this is not a sequence of separate user-facing mini-releases. Continue Stage 3–7 scope on the same protected overhaul branch.
- [ ] Still outstanding from the user's Windows/Game Pass environment: launch all 21 routes, visual/theme/DPI acceptance, real copied-save import, exact Game Pass build/field certification and GraphicsResolver validation against the actual configured folder. Live Apply/Undo remains disabled pending field-level proof and disposable-save rollback acceptance.

## Latest portable Search Profiles checkpoint — 2026-11-13 17:57 UTC

- [x] Exact tested application source commit: e67c0a3b65dcadee485783ca700555bb89fa7076.
- [x] Windows Actions run 43477411616: .NET 8 WPF Release build succeeded with 0 warnings / 0 errors; 67/67 MSTest tests passed, 0 failed, 0 skipped.
- [x] Release artifact 14062868942 and test-results artifact 14063946560 uploaded. The downloaded Windows Release ZIP contains FmAtlas.exe, .NET dependencies, SQLite and the save-bridge tools. The downloaded TRX independently records 67 passed, 0 failed.
- [x] SavedSearchProfileStore now supports Duplicate, ExportJson by all/category, and ImportJson with schema/size/field validation. Imports merge without overwriting existing IDs or same-category names and return explicit skipped-conflict details.
- [x] Stats Search and Attribute Search expose Duplicate profile, Import profiles and Export profiles in addition to New/Save/Load/Delete. Exports are category-specific; imports accept the versioned portable JSON document and refresh the current screen.
- [x] Added regression tests for independent duplicate ID/unique name, category-only export, cross-store import, ID collision safety, name conflict safety, and unsupported schema rejection.
- [ ] Windows runtime acceptance remains outstanding: launch both search screens, verify all profile buttons, export/import between app instances, check skipped conflicts and verify no player/save contents are included in the JSON.
- The branch head at this checkpoint is the tested application commit. Main remains untouched at 2051bbee042fb8e7203b7855e833e391ccee9ecb.
- Continue the complete roadmap as one integrated programme; this feature tranche is not the full product release.

## Latest Attribute Search ALL/ANY verification — 2026-11-13 18:04 UTC

- Exact tested application source commit: `0fbd5fa5162b89798aa55e96669a01b26cd2ddbd`.
- Windows Actions run `43478483715`: .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **68/68 MSTest tests passed**, 0 failed, 0 skipped.
- Release artifact `14064057973`; test-results artifact `14064172732`. Downloaded and inspected the Release ZIP; it contains `FmAtlas.exe`, `FmAtlas.dll`, SQLite and both save-bridge tools. Downloaded TRX independently confirms 68 total, 68 passed, 0 failed.
- Attribute Search now exposes `ALL conditions (AND)` and `ANY condition (OR)`; ALL remains the default. The selected logic is passed to the SQLite candidate query and saved/restored with attribute profiles.
- SQL keeps the free-text player/club/nation/source filter outside the parenthesized criterion group, so it remains ANDed with either ALL or ANY matching. Existing callers preserve ALL behaviour via the default `matchAny=false`.
- Added disposable SQLite integration coverage with four player fixtures: ALL matches only the player meeting every threshold; ANY returns players meeting at least one; free-text filtering still narrows the ANY result. Saved profile round-trip now asserts MatchAny=true persists.
- Branch head is the tested source commit. Protected `main` remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`.
- [ ] Windows runtime acceptance remains outstanding: verify the toggle, saved-profile round-trip and result rows in the running app using an imported copy of an FM27 save; test ALL/ANY on real imported data and ensure missing attributes stay non-matches.
- The full product roadmap remains active; this is another verified integrated tranche, not the final FM Atlas release. Continue the remaining Advanced Search grouping, Player DNA, Scouting Matrix, Moneyball cohorts/percentiles, Tactical Intelligence, Match Lab, development, Save Inspector/editor, Game Pass adapter, Graphics/Skin Studio, mod workbench and release engineering.

## Latest Advanced Search condition-control checkpoint — 2026-11-13 18:09 UTC

- Exact tested application source commit: `eb5d8f2228310459df34713222f6257fc85b171c`.
- Windows Actions run `43479401387`: .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **68/68 tests passed**, 0 failed, 0 skipped.
- Release artifact `14064023817`; test-results artifact `14064766061`. Downloaded and inspected both: TRX counters show 68 executed / 68 passed / 0 failed; release ZIP contains `FmAtlas.exe`, `FmAtlas.dll`, SQLite and both save-bridge tools.
- Stats Search and Attribute Search now provide per-condition Move Up, Move Down and Duplicate actions alongside Remove. Reordering updates both the visible row order and backing condition list, so saved profile serialization and query execution use the same sequence.
- The tested commit adds only `MainWindow.AdvancedSearch.cs`; the current branch head equals the tested SHA. `main` remains unchanged at `2051bbee042fb8e7203b7855e833e391ccee9ecb`.
- [ ] Windows UI acceptance remains pending: visually confirm the extra row controls fit at supported resolutions/DPI, verify order persists after save/load, and duplicate/edit/remove multiple rows on both search screens.
- This is an internal integration checkpoint, not a separate user-facing release. Continue the full product roadmap on the same overhaul branch.

## Integrated Stage 3–7 implementation candidate — 2026-11-13

- The user approved proceeding with the complete Stage 3–7 programme as one integrated development effort. Work continues only on `overhaul/monolith-full-platform`; protected `main` has not been changed.
- Added nested universal AND/OR filtering with typed comparisons, missing-value-safe semantics, match explanations, depth/condition limits, and a local versioned profile store with save/load/duplicate/import/export and conflict-safe merging.
- Integrated new MONOLITH routes for Player DNA, Advanced Search, Scouting Matrix, Moneyball Scout, Tactical Studio, Match Lab, Development, Save Inspector, Graphics & Skins and Modding Workbench. Existing 21 routes remain in place.
- Added transparent cohort mid-rank percentiles with minimum-sample withholding; the UI states that ranking is relative to returned source rows and not competition-strength adjusted.
- Added a read-only graphics config indexer with bounded traversal, safe XML parsing, reparse-point skipping, path-traversal rejection, duplicate-ID and missing-image counts. It does not modify or install graphics.
- Added a modding research catalogue with recorded licence notes and explicit compatibility/verification boundaries. It does not execute third-party tools or copy third-party source.
- Existing database/shortlist/RoleFit/season-stat/diagnostics services are reused. Development UI distinguishes one-snapshot player facts from Atlas-local edit history and does not invent progression curves.
- Live Apply/Undo remains disabled. Exact FM27 Game Pass field certification, actual installed build verification, real save import acceptance and Windows UI visual testing remain outstanding.
- Added automated tests for nested filter logic and persistence, typed numeric/boolean comparison, missing-value semantics, cohort ranking, graphics XML/path safety and research catalogue boundaries.
- Draft validation-only PR #2 targets `overhaul/monolith-full-platform`, not `main`, to run the existing Windows Release build and MSTest workflow. The first run is `43482389999` and was still in progress at this checkpoint. This PR is not intended to be merged.
- No build/test result is claimed for the latest integrated head until the Windows workflow completes. The local container does not have the .NET SDK; Windows Actions is the authoritative compile/test environment.

## Integrated Stage 3–7 continuation — 2026-11-13 18:40 UTC

- [x] Recovered and verified integrated branch checkpoint 4c5881474a5f06ba9ec14702bd5e8169785c593c: Windows .NET 8 restore and WPF Release build passed with 0 warnings / 0 errors; MSTest passed 81/81, 0 failed and 0 skipped. Windows Release and test-result artifacts uploaded. Run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43483705374.
- [x] Fixed the Advanced Search profile import file-size guard and its test-project IO namespace issue before the 81-test green run.
- [x] Continued Moneyball source audit and found the raw-total query was not consistently enforcing the UI minimum-minutes threshold, and the metric catalogue could expose FM View CSV fields without a reliable query fallback.
- [x] Updated seasonal metric search to use indexed facts where available, preserve the older raw-JSON fallback, apply minimum minutes to raw totals, keep season observations separate from match rows, and use the latest FM View CSV export when no usable save observations exist.
- [x] Updated the external metric fallback to enforce minimum minutes for raw totals and per-90 rates; ensured per-90 can fall back to CSV-only imports when no save snapshot exists.
- [x] Updated Moneyball UI to apply its minutes floor to raw totals as well as per-90 and display the per-observation basis.
- [x] Added SQLite integration tests for indexed seasonal totals, minimum-minute filtering, duplicate player-period rows, and FM View CSV-only raw/per-90 fallback.
- [ ] The above Moneyball changes are source-committed but are not yet verified by Windows CI. Latest source head at this note: 8c6985935cc44241d2e8222017184008efbd76a0; current validation run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43484787575 (queued at the time of this update). A previous queued run on commit 40c02ecb was still building; wait for the latest-head run before calling this fix green.
- [ ] Continue the Stage 3–7 feature/data acceptance audit. The integrated pages are initial implementations, not a completed product: Moneyball still lacks position/age/league cohort controls and defensible cross-league calibration; Tactical Studio remains an Atlas role-fit estimate, not SI Tactical Effectiveness; Match Lab is limited to imported aggregate facts; Player DNA comparison and recruitment matrix need runtime acceptance.
- [ ] Windows runtime acceptance remains outstanding: launch all routes, theme/high-DPI checks, profile persistence in the running app, copied FM27 Game Pass save import, exact process/build profile validation, graphics folder scan and UI review.
- [ ] Live Apply/Undo remains blocked by LiveWriteCertification.IsCertified == false. Do not remove the gate without exact build and field-level witnesses plus disposable offline-save rollback acceptance.
- Protected main remains untouched. All work remains on overhaul/monolith-full-platform.

## Moneyball fixture triage — 2026-11-13 18:43 UTC

- Windows run 43484787575 on source 8c698593 built successfully with 0 warnings and 0 errors, then reported 84/85 tests passing.
- The sole failure was in the new SQLite test fixture, not in the Moneyball query: two different players were inserted with the same primary key tuple for FmsaveStatFacts (SnapshotId, TableName, RowIndex, FieldKey).
- Corrected the fixture to use distinct row indexes for each row (commit d863808); the test still covers duplicate player/period observations by using two separate row indexes for the same player.
- Added malformed advanced-filter enum/null-clause validation and regression tests in the same follow-on validation sequence.
- Windows run 43485187445 is checking commit 437fbb5, which includes the corrected fixture and filter validation tests. Latest code head 507cb766 is queued in run 43485344141. Do not call the latest code verified until its Windows run passes.

## Scouting Matrix notes continuation — 2026-11-13 18:45 UTC

- [x] Added editable recruitment notes for a selected shortlist candidate in the Scouting Matrix UI, with a 2,000-character limit and clear/save behavior.
- [x] Verified the database UPSERT updates only Notes on an existing (SourceId, ShortlistName) key; it does not overwrite the player's identity or reset AddedUtc.
- [x] Added a SQLite integration test for note creation, update, clearing and stable player identity.
- [ ] Windows validation for the new note UI and test is queued on latest branch head 491695baf2a14592fd7bb1d6a000db986ee69782: run https://github.com/lewisxatk/FM-Atlas/actions/runs/43485628646.
- [x] Preceding Windows run 43485187445 passed on commit 437fbb5: Release build 0 warnings / 0 errors; 86/86 tests passed. This includes the corrected Moneyball SQLite fixture and malformed Advanced Search filter validation. The newer shortlist-note addition still requires its own current-head CI pass.


## Combined Stage 3–7 integration checkpoint — 2026-11-13 18:44 UTC

- [x] User approved proceeding with the combined Stage 3–7 programme on the existing protected overhaul branch; no separate user-facing mini-releases.
- [x] Exact tested source commit: `437fbb5ad47d11b2bda5c3ea2e5aee20686a2ac3`.
- [x] Windows Actions run `43485187445`: .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **86/86 MSTest tests passed**, 0 failed, 0 skipped.
- [x] Windows Release artifact `14066798539` and test-results artifact `14067566665` uploaded. The Windows package is 14.7 MB; Release and TRX artifacts have 14-day retention.
- [x] Stage 3 shared-shell groundwork includes the central MONOLITH Obsidian/Warm Light token service, Atlas orange semantic accent, legacy palette mapping, sidebar navigation, Ctrl+K global search, route loading/error states and saved theme preference. Full shared-control migration and visual regression review remain pending.
- [x] Stage 4 scouting groundwork includes nested ALL/ANY Advanced Search, typed conditions, validation and match explanations; local criteria-only advanced-search profiles; Player DNA compare; Scouting Matrix shortlist management; Value Finder and the existing Role Lab. Universal Search currently evaluates verified PlayerRecord fields only, over a bounded candidate query; parser field-level coverage is not yet attached to every normalized field.
- [x] Stage 5 tactical groundwork includes Tactical Studio's transparent attribute-weighted Atlas role estimates, Match Lab's retained performance facts, and Development's current-snapshot facts/edit history. These do not claim to reconstruct FM's hidden tactical-effectiveness/buy-in formulas, event timelines or longitudinal development curves that the current sources do not expose.
- [x] Stage 6 data/editing/mod research groundwork includes read-only Save Inspector, Graphics & Skins `config.xml` indexing, Modding Workbench research catalogue, and the existing local database editor. Live memory Apply/Undo remains blocked by `LiveWriteCertification.IsCertified == false`; no Game Pass field offsets or encodings are certified by this checkpoint.
- [x] Moneyball hardening: cohort percentiles use sorted, tie-aware mid-ranks (O(n log n)); sample thresholds withhold percentiles for insufficient cohorts; per-90 conversion is disabled for rates/percentages; season queries exclude match-level rows, apply minimum minutes, deduplicate player/season records, and can fall back to the latest FM View CSV export when no usable save rows exist.
- [x] Moneyball regression coverage includes the no-save-snapshot FM View fallback, 1,005 imported CSV rows, per-90/minutes threshold, and 5,000-observation cohort ranking. Indexed save-stat fixtures now use unique row indexes.
- [x] Advanced filter validation rejects malformed logic/operator enums, null clauses, empty groups, invalid ranges and invalid condition shapes.
- [ ] Remaining Stage 3–7 work is not complete: full shared MONOLITH component/screen migration; richer recruitment board fields and verified player/position suitability; position/league-normalised Moneyball cohorts and side-by-side recruitment comparisons; evidence-backed tactical formation/player-buy-in analysis; dated multi-snapshot development history; broader save-field coverage/provenance; full graphics provider/cache handling; supported mod-management workflows; and final installer/publish/release process.
- [ ] User-PC acceptance remains outstanding: launch the WPF app, inspect all routes/themes/DPI and keyboard flow, verify support ZIP contents, import only a COPY of an FM27 Game Pass save, compare imported fields to the in-game UI, check the actual configured graphics root, and verify that unknown/unsupported Game Pass builds remain read-only.
- [ ] Live Apply/Undo remains prohibited until the exact installed build, each field offset/encoding, identity resolution, preflight, write/read-back and rollback are independently witnessed on a disposable offline career.
- Main remains untouched; all changes and validation are on `overhaul/monolith-full-platform`.


## Combined Stage 3–7 updated Windows checkpoint — 2026-11-13 18:52 UTC

- [x] Exact tested source commit: `c51f80574d9131cb9caa1b25a44541ae370df553` on `overhaul/monolith-full-platform`.
- [x] Windows Actions run `43486407442`: restore successful; .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **87/87 MSTest tests passed**, 0 failed, 0 skipped.
- [x] Windows Release artifact `14066379269` (14,694,174 bytes; SHA-256 `f46c01c18982ec58e79d557cb696ca27b55c61c817207d2753795b14581f36ca`) and test-results artifact `14067926212` (SHA-256 `c08febcbd364e6c6c58865b29cf45879fd87926e7cb61c69a3c4b0a00457131d`) uploaded. GitHub artifact retention expires 27 November 2026.
- [x] The tested tree includes the Scouting Matrix editable notes UI and SQLite create/update/clear regression test; note updates preserve the shortlist AddedUtc timestamp.
- [x] MONOLITH initial WPF resources, orange semantic active-navigation state and dashboard label were aligned with the Obsidian/Atlas identity.
- [x] Moneyball now exports CSV with metric key, provider-specific PlayerSourceId, player/club/period, value, percentile, sample size and evidence basis. Stale result rows and export are invalidated when the selected metric, per-90 toggle or thresholds change.
- [x] Moneyball queries and external export fallback now permit up to 50,000 returned season observations; cohort ranking remains sorted, tie-aware O(n log n), with regression coverage at 50,000 observations. The UI states that the percentile is relative to the returned window, not league-strength adjusted.
- [x] Latest CI verifies source compilation and automated regression tests only. It does not verify runtime WPF visuals, keyboard/DPI behaviour, actual user data mappings, Game Pass installation compatibility, graphics roots or any live memory write.
- [ ] Next validation is user-environment acceptance: launch the exact Windows Release package; smoke-test all routes and both themes at common DPI scales; exercise the Advanced Search and profile import/export screens; edit/clear Scouting Matrix notes; run Moneyball against a copied FM27 save and an FM View export; inspect generated CSV and support ZIPs; validate the user's graphics config root. Do not use the only career save.
- [ ] Position/league-cohort Moneyball, validated position familiarity/15-position suitability pitch, full tactical effectiveness and player buy-in models, multi-snapshot development history, graphics pack management, mod installation/restore, complete shared MONOLITH control migration and final installer/release process remain later implementation gaps. They are not marked complete merely because the initial screens exist.
- [ ] Live Apply/Undo remains blocked by `LiveWriteCertification.IsCertified == false`. Unknown/unverified game builds remain read-only.
- Main remains unchanged at `2051bbee042fb8e7203b7855e833e391ccee9ecb`.


## Combined Stage 3–7 intelligence and save-data checkpoint — 2026-11-13 21:22 UTC

- [x] Exact tested application source: `4fdd17c344a00c64754218a4770e97c33a19f5eb` on `overhaul/monolith-full-platform`.
- [x] Windows Actions run [`43509771993`](https://github.com/lewisxatk/FM-Atlas/actions/runs/43509771993): .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **95/95 MSTest tests passed**, 0 failed, 0 skipped.
- [x] Release artifact `14077394002`, 14,707,768 bytes, SHA-256 `473cc9093e3cc0fbfb508dc2741ee8329c23c1166116642254aceeac17f9dd19`; test-results artifact `14078735267`, SHA-256 `9325e6742509f04d8b96c7f56a6647832c5e30a3b3f971487c3ddaa5cb9ebebf`. Both expire 27 November 2026.
- [x] Player profile now maps the actual 15 fmsave position ratings to a compact pitch layout. The position-key adapter handles the documented `gk, sw, dl, dc, dr, dm, ml, mc, mr, aml, amc, amr, stc, wbl, wbr` keys and aliases. Natural (18+) and Accomplished (15–17) follow fmsave's classifier; lower colour bands are explicitly Atlas-only visual groupings, not Football Manager labels or hidden formulas.
- [x] Moneyball carries the imported primary-position label from save snapshots and FM View CSV rows. Users can rank the full returned cohort or calculate independent percentile distributions within each primary-position group; rows without a usable primary position are excluded from position-specific cohorts. Per-group sample floors still apply, and the UI states that no league-strength adjustment is inferred.
- [x] Development now reads multiple dated snapshots for the same player source ID and displays attribute, CA, PA and age observations with deltas, build and source-file provenance. Parser-verified attributes are distinguished from fmsave-unconfirmed CA/PA/age fields.
- [x] Development identity guard handles fmsave's documented warning that a deleted person's `unique_id` can be assigned to a new person: a changed birth date, or changed name when DOB evidence is missing, starts a new segment and suppresses false deltas. Same-name ID reuse without DOB evidence cannot be ruled out and is disclosed in the UI.
- [x] Squad Architect reads the actual latest-save `squad_planner` table, resolves player UIDs against that same snapshot, preserves unresolved references in order and distinguishes empty slots from missing UID lists. It is gated to FM27 build `27.0.0+2486695` with a registered build, matching fmsave 0.12.3's documented reader boundary. Slots remain numbered; their tactical/category meanings are not guessed.
- [x] Regression coverage includes 15-position key/colour mapping, position cohort separation and sample floors, primary-position propagation through indexed save and FM View data, multi-snapshot trend deltas, recycled unique-ID identity resets, and squad-planner UID/slot validation.
- [ ] Still outstanding: age-band and league-normalised Moneyball cohorts; a verified competition-strength mapping; a formation/role-aware best-XI optimiser connected to validated player-slot eligibility; SI tactical effectiveness/player buy-in reconstruction; full graphics provider/cache/install/restore workflows; wider mod management; full shared MONOLITH control migration; final installer/publish/release process.
- [ ] User-PC acceptance remains outstanding. When the user is ready, test only with a copy of the FM27 Game Pass career; compare player values/positions to the in-game UI, inspect the pitch map and planner slots, test Moneyball group results/CSV and multi-snapshot history, smoke-test every route/theme/DPI, verify graphics roots and review diagnostics/support exports.
- [ ] Do not test against the user's only career save. Live Apply/Undo remains disabled; unknown or unregistered builds remain read-only.
- Main's content tree has been restored to baseline `2051bbee042fb8e7203b7855e833e391ccee9ecb`; see the repository safety correction below for the add/revert history.


## Squad Architect best-XI integration and verified-build data gate — 2026-11-13 21:37 UTC

- [x] Exact tested application source: `5ff275d534245880242a7280f95837f6ee3a3b0d` on `overhaul/monolith-full-platform`.
- [x] Windows Actions run [`43511883115`](https://github.com/lewisxatk/FM-Atlas/actions/runs/43511883115): .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; **103/103 MSTest tests passed**, 0 failed, 0 skipped.
- [x] Release artifact `14079346029`, 14,718,438 bytes, SHA-256 `71de192d2fb6bb3e001920f95d809a9a39cfe5ef5fdf6e23f119543c31ce0ca3`; test-results artifact `14077554786`, SHA-256 `85b4b87eaeaa966f3cf60105b0bd836c7db0b19422ca65aa090161fe5bb176d4`. Both expire 27 November 2026.
- [x] Squad Architect now has four supported formation templates (4-2-3-1, 4-3-3, 4-4-2 and 3-4-2-1) and a global one-player-per-slot assignment. The optimiser combines existing weighted role-fit estimates with source position ratings using the explicit Atlas composite: `roleFit × positionRating / 20`.
- [x] Best-XI candidate queries are restricted to one selected club and the latest save snapshot. They use registered-build position facts only; unknown/unregistered builds yield no position eligibility rather than stale values.
- [x] The result grid shows slot, player, position rating, role-fit estimate and Atlas composite score. Missing eligibility leaves a slot unfilled; a player cannot be assigned twice. Fitness, injuries, suspensions, tactical familiarity, role duties and FM's hidden selection logic are not modelled.
- [x] Role-fit search now supports club filters and configurable result caps; batch position-rating queries provide the same-snapshot eligibility data. Tests cover club isolation, latest registered snapshot, unknown-build rejection, global assignment and duplicate-player prevention.
- [x] Position facts shown in the player profile and Moneyball position cohorts are also gated on registered save builds. This prevents unverified position fields from entering the pitch, cohort grouping or best-XI calculation.
- [ ] Still outstanding: age-band and league-normalised Moneyball cohorts; validated competition-strength mapping; tactical effectiveness/player buy-in reconstruction; match-event timeline integration; full graphics provider/cache/install/restore workflows; wider mod management; full shared MONOLITH control migration; final installer/publish/release process.
- [ ] User-PC acceptance remains deferred until the user is on PC and more implementation is complete. Then test only with a copy of the FM27 Game Pass career; compare player values/positions to the in-game UI, inspect all four formations, best-XI eligibility and planner slots, test Moneyball group results/CSV and multi-snapshot history, smoke-test routes/themes/DPI, verify graphics roots and review diagnostics/support exports.
- [ ] Do not test against the user's only career save. Live Apply/Undo remains disabled; unknown/unregistered builds remain read-only.
- Main remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`.


## Repository safety correction — 2026-11-13 21:39 UTC

- A test-only commit was accidentally sent to the default `main` branch while creating integration-test files. It added three files only: two test files and `docs/MASTER_PROGRESS.md`; no application source was merged.
- All three additions were deleted from `main` in sequential revert commits. Current `main` ref: `05a0928e82cb3b3e99f58a5ae4ebe19479c9bb35`.
- Verified with GitHub compare: `2051bbee042fb8e7203b7855e833e391ccee9ecb...main` returns **zero changed files**. The current `main` content tree is therefore identical to the original baseline, although its commit history contains the add/revert commits.
- The integrated application work remains on `overhaul/monolith-full-platform`; no application code or feature merge is present on `main`.


## Combined development pass — 2026-11-13 — workstreams 1–4

- [x] Added opt-in shared MONOLITH WPF style resources for cards, buttons, text inputs, combo boxes, data grids and section headings.
- [x] Added explicit higher-/lower-is-better Moneyball ranking and exported direction metadata; added a regression test for lower-is-better tied percentiles.
- [x] Added role-profile-based candidate ranking to the Scouting Matrix and shortlist handoff.
- [x] Added a tested recruitment opportunity engine that separates role improvement, market value, asking price, weekly wage and their budget assessments.
- [x] Added phase-specific Tactical Studio filtering and an in-possession/out-of-possession squad comparison panel with two 11-slot templates.
- [x] Added standalone regression tests for recruitment budget semantics and two-phase tactical shape behaviour.
- [ ] Fresh Windows CI build/test verification for this combined commit.
- [ ] Migrate the remaining legacy WPF screens onto keyed MONOLITH styles and complete contrast/DPI/theme visual checks.
- [ ] Add verified age/competition cohort data to the statistics schema before implementing those Moneyball groupings.
- [ ] Add a field-backed asking-price/wage provider before exposing transfer affordability in the live recruitment UI.
- [ ] Validate tactical shape templates and selection results against the user's copied FM27 Game Pass save and in-game tactical screen.
- Live Apply/Undo remains blocked; no original career save is to be modified. main is not targeted by this pass.
