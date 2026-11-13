# FM Atlas — Master Handover

**Checkpoint:** 2026-11-13  
**Repository:** https://github.com/lewisxatk/FM-Atlas  
**Active programme branch:** `overhaul/monolith-full-platform`  
**Main branch:** preserved; do not use for unreviewed overhaul work.

## Mission

Build FM Atlas into a premium Windows Football Manager intelligence and modding suite covering scouting, recruitment, analytics, tactical intelligence, player development, squad planning, save/database inspection, safe editing, graphics/skin management and mod research.

## Authoritative design decision

**MONOLITH is the primary design system.** Obsidian/graphite dark theme, warm-light secondary theme, Atlas orange accents, Inter typography, Swiss Sport editorial hierarchy, Touchline football visualisations and Pulse statistical presentation. Use rounded, approachable controls and restrained motion. Aurora Frost is superseded as the primary direction and must not be reintroduced as the default.

## Current source/build facts

- The user ran the repaired R7.2.0 v2 package on Windows 10 with .NET SDK 8.0.427.
- Uploaded build log dated 2026-11-13 15:56:50 reports **Build succeeded, 0 errors, 15 warnings**.
- Output reported: `src/FmAtlas/bin/Release/net8.0-windows/FmAtlas.exe`.
- This verifies compilation on the user's PC only. Application startup, runtime features, imported save correctness, graphics resolution and live editing are not yet verified.
- The repaired R7.2.0 application source has been imported into `application/R7.2.0/FM-Atlas-R7.2.0-Work/` on the active overhaul branch. Source import commit: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`.
- `main` remains untouched. The active branch advances as work is committed; use `docs/MASTER_PROGRESS.md` and the GitHub branch ref for the latest checkpoint.
- A current source-level architecture audit is recorded in `docs/SOURCE_ARCHITECTURE_AUDIT.md`. Feature-to-source coverage is tracked in `docs/FEATURE_TRACEABILITY.md`.

## Current implementation checkpoint

- The application is a .NET 8 WPF project using Microsoft.Data.Sqlite 8.0.11.
- App startup initialises local rolling logs, applies the MONOLITH theme preference, and records unhandled dispatcher/AppDomain/task exceptions.
- A MONOLITH theme service supports Obsidian and Warm Light. The latest branch changes now update token brushes and tracked legacy brushes in place, bind the main window to theme tokens and migrate the Player Intelligence report XAML to DynamicResource tokens; older screens still contain hard-coded values and shared-control migration is incomplete.
- Diagnostics already has health checks, copy/export actions, a support ZIP preview/confirmation screen, a SHA-256 manifest, and local-only export. The existing support archive excludes career saves, the SQLite database, FM installation files and arbitrary files by default.
- A source audit found over-escaped credential patterns and incomplete handling of Windows paths containing spaces. These are now corrected in AtlasLog.cs. A Diagnostics self-check covers bearer tokens, API keys, passwords, user paths, spaced drive paths, UNC paths and standalone save filenames.
- GitHub Actions Windows run `43468496760` at commit `ade4637` completed .NET 8 restore, WPF Release build and expanded automated suite: 40 tests executed, 40 passed, 0 failed, 0 skipped; build reported 0 errors and 12 warnings. Release and TRX artifacts were uploaded. This verifies compilation and these automated unit tests at that exact checkpoint, not application UI or game integration.
- Live Apply and Undo remain blocked by LiveWriteCertification.IsCertified = false. Do not enable them without independent field witnesses and disposable offline save acceptance.
- Game Pass profile records and a configurable graphics resolver exist in source; neither the user's exact installed build nor their real graphics folders have been validated.

## Reverse-engineering status

The research register records FMLibreEditor, fm-editor, FMSuperScout, fmsave, gilet, FM Player Analyzer, FM View Exporter, FMM Editor, FMRTE, FM Dossier, TalentSeer, Teknik Oda, FaceDrop, Customize Editor, Scout Reporter and SaveMyTime.

These are not all fully reverse-engineered. Current evidence includes desk research, documentation review and static inspection of some uploaded packages. No external executable was run, no user save or live process was inspected, and the user's exact FM27 Game Pass build/profile has not been verified. GPL projects remain research-only unless a deliberate licence decision is made. See `research/RESEARCH_REGISTER.md` and `docs/OVERHAUL_MASTER_PROGRAMME.md`.

## Programme scope

The authoritative integrated feature scope and acceptance gates are in [OVERHAUL_MASTER_PROGRAMME.md](OVERHAUL_MASTER_PROGRAMME.md). It includes:
- MONOLITH shared WPF design system and application shell;
- universal field/data registry and FMRTE-style Advanced Search;
- Player DNA, Scouting Matrix, role fit and recruitment intelligence;
- Wyscout-style Performance & Moneyball Scout with traceable per-90 metrics and percentiles;
- Tactical Intelligence Studio and Match Lab;
- player development and career assistant;
- offline Save Inspector/import pipeline and database/editor studio;
- guarded Game Pass live adapter;
- graphics and skin studio, modding/research workbench;
- quality, accessibility, tests, packaging and release engineering.

## Safety and evidence

- Treat Game Pass, Steam and Epic as separate targets. Game Pass is the user's intended target.
- Unknown builds remain read-only.
- A module fingerprint alone does not verify every memory field.
- Live writes require save identity, entity identity, range/type validation, expected-old-value preflight, read-back, career-switch invalidation and guarded Undo.
- Keep hidden/structural fields blocked until independently verified.
- Never test on the user's only career save; use disposable offline copies.
- Keep licensed game assets, third-party binaries, personal saves and secrets out of source control unless specifically safe and permitted.
- Distinguish researched, source-inspected, implemented, compiled, launched, fixture-tested, copied-save-tested and verified-in-game.
- Do not mark roadmap items complete without linked source changes and acceptance evidence.

## Checkpoint references

- [Integrated overhaul programme](OVERHAUL_MASTER_PROGRAMME.md)
- [Master progress](MASTER_PROGRESS.md)
- [Roadmap](ROADMAP.md)
- [Architecture](ARCHITECTURE.md)
- [Research register](../research/RESEARCH_REGISTER.md)
- [Recovery inventory](../recovery/RECOVERY-INVENTORY.md)
- [R7.2.0 source checkpoint](../recovery/R7.2.0-CHECKPOINT.md)

## Test and CI checkpoint

- Added `tests/FmAtlas.Tests/` with MSTest 4.5.2 and Microsoft.NET.Test.Sdk 18.11.0; target is net8.0-windows and the test host enables WPF framework dependencies.
- Added tests for the built-in redaction regression suite, bearer/API key/password removal, drive/UNC path removal, save-filename removal, newline normalization, truncation and normal-text preservation.
- Added test project to the solution. Windows build workflow now runs the suite and uploads TRX results.
- The new tests are source additions only at this point. Do not claim pass until an actual Windows workflow or local run is recorded.
- Current main branch remains preserved. Recheck the branch head in GitHub before each handover because later checkpoints continue to advance it.

## MONOLITH theme follow-up

- Existing resource token brushes are now updated in place during theme changes instead of being replaced.
- Legacy brushes created through `MonolithTheme.ResolveLegacy` are tracked through weak references and remapped on theme changes; dead references are pruned.
- MainWindow background/foreground use central Canvas/TextPrimary resources.
- PlayerIntelligenceReportWindow uses DynamicResource for theme-aware surfaces, buttons, text boxes, data grids and report accent/muted text.
- This avoids destroying a page or losing its local filter state just to switch themes. It remains a source-level implementation until Windows runtime/theme/DPI acceptance is recorded.

## Windows build/test evidence

- Successful Actions run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43465739690
- Tested commit: `5aa82c27117b4badf63e19d88681b12eb0c4db30`.
- Windows job completed all steps: checkout, .NET 8 install, environment capture, restore, Release build, automated tests, TRX upload, executable metadata and Release artifact upload.
- TRX counters: total 10, executed 10, passed 10, failed 0, error 0.
- Test artifact SHA-256: `6d09bb448e6dafbdab87e49cda69141c0629de9440eec41c8be15d79d343c40b`.
- Release artifact size: 14,591,556 bytes.
- This is the first automated Windows build/test acceptance for this source branch. Later source commits need their own workflow result. App launch, route coverage, actual support ZIP content, theme switching at different DPI and Game Pass/FM runtime integration remain untested.
- Latest source change after that successful run adds Ctrl+K and global-search canvas token at `830cedc`; its separate Actions run was still in progress at the time of this update.

## Field-evidence hardening follow-up

A later source review found that the original substring checks `LIKE '%Verified%'` and `Contains("Verified")` could match `Unverified`. This was corrected in the active branch:

- All nine occurrences in `ScoutDatabase.cs` now use a normalized exact allowlist of canonical verified statuses.
- Both known UI consumers now call `FieldEvidencePolicy.CanUseForDecision`.
- `FieldEvidencePolicy.Assess` accepts only exact normalized verified statuses; provisional/ambiguous/mixed values fail closed.
- New MSTest cases cover allowed canonical statuses, misleading prefixes, cross-provider ID separation and exact-ID duplicate resolution.
- Latest source commit: `fd40df08c8f8194881be56a1e5198dfcfa1ca1d2`. The expanded Windows workflow run is `43466693498` and was still running during this handover update.
- Do not promote these changes to runtime-verified until the latest Actions run completes and the UI/data flows are tested with a representative, disposable dataset.

## Latest verified source checkpoint — 2026-11-13

- Windows Actions run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43468496760
- Exact tested commit: `ade463712df284034ba475a2f1bc85584e9394d9`.
- .NET 8 restore: passed. WPF Release build: succeeded, 0 errors. MSTest: 40 passed / 40 total. Release artifact and TRX uploaded.
- The expanded tests include Diagnostics redaction, strict evidence classification, provider identity isolation/deduplication, squad assignment optimizer, football metric calculations, ALL/ANY statistics matching and non-finite boundary rejection.
- Build warnings at that tested commit: 12 (11 unused MainWindow private filter declarations and one obsolete AtlasLog.Warn call). A source-only warning cleanup removed the 11 unused declarations after confirming they were referenced nowhere in seven MainWindow partial classes, and changed the legacy call to AtlasLog.Warning. Commits: `886a8a5`, `1022078`.
- CI run for the first cleanup commit `886a8a5` is in progress; a queued run for current head `1022078` is pending. No zero-warning claim until a completed run is inspected.
- Application launch, UI screen coverage, actual support ZIP inspection, theme/DPI acceptance and Game Pass runtime compatibility remain untested.

## Warning-free Windows checkpoint and SQLite consumer test

- Windows Actions run `43469077009`, exact tested commit `1022078bd4fd79d21f687feb721f6e6e7f4087d7`: WPF Release build succeeded with **0 warnings / 0 errors**; all **40/40** unit tests passed. Link: https://github.com/lewisxatk/FM-Atlas/actions/runs/43469077009.
- The warning count was reduced from 12 to zero without suppression: eleven unused private fields were removed after searching every MainWindow partial; one call was changed from obsolete `AtlasLog.Warn` to `AtlasLog.Warning`.
- New SQLite integration test `tests/FmAtlas.Tests/ScoutDatabaseEvidenceIntegrationTests.cs` seeds a disposable database with canonical verified, read-back verified, unverified, provisional and mixed-status facts, then checks CA/PA values, filters and attribute results. It uses a dedicated test database path and disables SQLite pooling for safe cleanup.
- The first test build exposed missing `System.IO` imports; corrected in commit `e109eab`. The test has not yet completed a Windows run.

## Latest Windows checkpoint — 2026-11-13

- Actions run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43470501984.
- Exact tested commit: `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712`.
- Windows .NET 8 Release build: succeeded with **0 warnings and 0 errors**.
- Automated suite: **47/47 passed**, 0 failed, 0 skipped. Release and TRX artifacts uploaded.
- Tests now include a disposable SQLite database integration test. It seeds trusted and untrusted live fact statuses and verifies the actual SearchLivePlayers, CA/PA filter, attribute filter and club query consumers exclude unverified/provisional/mixed status facts while retaining player identities.
- RoleFitService.CalculateEstimate is now a shared, unit-tested weighted estimate used by the database Role Lab search. Six tests cover weighted mean, coverage, normalized keys, missing/invalid values and invalid profiles.
- Prior dedicated SQLite checkpoint: Actions run `43470026732`, exact commit `e109eab`, 41/41 passed, 0 warnings/errors.
- Main remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`; integrated overhaul is 163 commits ahead, 0 behind at this checkpoint.
- These checks establish build/unit/integration-test evidence only. WPF runtime, visual/DPI, all route smoke tests, support ZIP manual inspection, actual Game Pass and save integration remain to be performed.

## Current continuation point — Role Lab transparency

- Latest fully verified application checkpoint: `b8bad3a10d0bb51222a6ca6042ed9e4f462e5712`, Windows Actions run `43470501984`: WPF Release build 0 warnings/0 errors; 47/47 MSTest passed; Release and TRX artifacts uploaded.
- Follow-on source change `4b4ddfcb7299a0fad5dc2ea60658d895a48127df` updates `MainWindow.RoleLab.cs` so selecting a role shows every weighted attribute as a labelled chip, along with the model's input range, missing-value rule and minimum-coverage method note.
- The new UI change passed Windows Actions run https://github.com/lewisxatk/FM-Atlas/actions/runs/43471187844: WPF Release build 0 warnings/0 errors, 47/47 tests passed. Release artifact `14061656624` and TRX artifact `14061910345` were uploaded.
- It is compile/test verified only. Its rendered layout, role-selection refresh and readability at real Windows DPI/resolutions still require manual runtime acceptance.
- The Role Lab scores remain Atlas weighted estimates, not Sports Interactive's internal role formula or star rating. Per-player contributions are now shown; position familiarity remains unavailable until source-backed.
- Do not integrate the squad optimizer into a Best XI screen until player-to-slot eligibility is backed by validated position suitability data and duplicate LIVE/FMSAVE identities are handled with a verified crosswalk.
- All changes remain on `overhaul/monolith-full-platform`; `main` is preserved. Continue from the current branch head and exact file history, not from this text alone.

## Latest continuation point — Integrated Role Fit and Diagnostics checkpoint (2026-11-13)

- Exact tested application source commit: `8cfe5b3f276770179e8cb77db7a02912f2336bdb`.
- Windows Actions run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43473487285.
- .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; MSTest **53/53 passed**, 0 failed/skipped.
- Windows Release artifact: `14061763980`; test-results artifact: `14062007740`.
- The Role Fit work now has: visible role-weight chips/method note; per-player selected contribution detail; readable attribute names, source values, Atlas weights and additive score points; contribution-aware CSV export; overflow-safe weighted mean; unit tests and a real SQLite candidate-query integration test using an isolated temp DB.
- Diagnostics redaction now tests quoted JSON secrets with spaces, Bearer and Basic authorization, API/client/private secrets, query-string tokens, Windows/UNC paths and save filenames. The in-app self-check shares the regression cases.
- All follow-on commits after tested application source `8cfe5b3` are documentation-only checkpoint updates; no application source has changed since the 53-test run. `main` is untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`. Always fetch the live branch ref before resuming.
- Remaining acceptance: launch the WPF app on Windows, inspect all navigation routes, visually test Role Lab and themes at 100/125/150% DPI, exercise Role Lab and CSV against a copied FM27 Game Pass save, manually inspect default and opt-in support ZIPs, and validate GraphicsResolver against actual configured folders.
- The full integrated programme is not complete. Advanced Search profiles/condition groups, Player DNA, Moneyball/per-90/percentiles, recruitment, Tactical Intelligence, Match Lab, Save Inspector/editor, Game Pass read-only certification, graphics/skin/mod workbench and release packaging remain on the master roadmap.
- Live memory Apply/Undo remains disabled until exact Game Pass build/field certification, disposable-save Apply/read-back/Undo, identity checks and rollback evidence pass. Never test on the user's only career save.
- Continue only on `overhaul/monolith-full-platform`, not `main`. Use the current branch files and this checkpoint, not the interrupted chat transcript.


## Combined Stage 1 + Stage 2 handover — 2026-11-13

- Authoritative report: `docs/STAGE1_STAGE2_AUDIT.md`.
- Stage 1 source audit found a mismatch between the Diagnostics personal-details checkbox/README and the final redaction policy. The code and Help copy now say that opting in includes player names and recent edit history only; credentials/tokens, absolute paths and career save filenames remain always redacted.
- The support ZIP implementation uses a fixed in-memory file allowlist, a manifest with SHA-256 hashes for listed report/README/log entries, a preview window and a local Save File dialog. The actual Windows-generated ZIP has not yet been manually inspected.
- Stage 2 maps all 21 navigation destinations. Key findings: Players and Recruitment share a workspace; Transfers is only a ranking by imported value; Tactical Lab is an informational data-coverage surface; Atlas Assistant is rule-based; Role Lab uses transparent Atlas weights, not FM's hidden role formula; Squad Architect is a roster/coverage view and does not yet use the optimizer as a validated Best XI engine.
- Architecture findings: dynamic WPF code-behind shell; central SQLite data/import/query class; 38 app source files and 9 test-project files; fmsave Python bridge; separate Game Pass/Steam memory profiles; global live-write certification remains false; graphics resolver reads existing config.xml packs but actual user Game Pass paths remain unverified.
- Last completed Windows verification before the privacy wording/manifest correction: run `43473487285`, application source `8cfe5b3`, 53/53 tests, 0 warnings/errors. This does **not** verify later source edits.
- Current branch may have newer commits and CI runs; always fetch the live branch ref and latest workflow status before resuming. A workflow was in progress during this handover; do not infer success from a pending/cancelled run.
- Remaining Stage 1 acceptance: pass CI on the exact privacy-copy/manifest commit, run the app, create default and opt-in ZIPs, independently inspect entries/hashes and verify exclusions.
- Remaining Stage 2 acceptance: Windows launch and smoke-test all 21 routes; validate real Game Pass process/build, imported save copy and graphics root. These require the user's Windows environment.
- Stage 3–7 are explicitly on hold until the user reviews the findings and approves the next combined implementation.

## Latest Stage 1 implementation handover — SupportBundleComposer

- The previous inline ZIP assembly was extracted into `Services/SupportBundleComposer.cs`. `MainWindow.Diagnostics.cs` now uses the same composed package for preview and ZIP output.
- The composer only accepts a report and named application log text. Its filename allowlist rejects arbitrary entries and path traversal; report/log text is redacted; the manifest hashes and sizes the payload entries; archive bytes are round-trip tested.
- Five new tests live in `tests/FmAtlas.Tests/SupportBundleComposerTests.cs`. They cover exact entry allowlisting, ZIP round-trip, manifest integrity, privacy redaction and invalid filename rejection.
- Latest test run at the time of this handover: Actions run `43475232418`, exact source commit `a3efdb57115475c6d7693a26f85928910664fdee`, still in progress. The last completed baseline before these changes remains `43473487285` with 53/53 tests and zero warnings/errors.
- A preceding CI run `43474815591` failed at compile because the initial privacy-copy edit malformed two report string literals. The lines were repaired in `d60206a`; the later `a3efdb5` run includes the repaired code. Do not describe the failed intermediate commit as the active source state.
- Current source inventory: 41 application source files and 11 test-project files including the csproj.
- Stage 2 route audit is documented in `docs/STAGE1_STAGE2_AUDIT.md`; Stats Search and Attribute Search also now contain a local saved-profile UI backed by `SavedSearchProfileStore`, with persistence tests. Saved profiles persist criteria only, not player/save data.
- Stage 3–7 remain paused. Wait for current CI, then Windows app/ZIP/route acceptance and the user's review.
## Latest verified Stage 1/2 source handover — 2026-11-13 17:49 UTC

- Exact tested application source: `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991`.
- Windows Actions run `43476085012` passed: .NET 8 WPF Release build 0 warnings/0 errors; MSTest 64/64 passed. Link: https://github.com/lewisxatk/FM-Atlas/actions/runs/43476085012.
- This passing run includes the corrected Diagnostics privacy policy, SupportBundleComposer and its ZIP/manifest/redaction tests, SavedSearchProfileStore and its persistence tests, and expanded SQLite evidence-isolation integration.
- The failed intermediate runs are historical and documented: `43475232418` had missing IO imports and an outdated search test call; `43475788525` had further test compile issues. The imports, MSTest assertion API and test call were corrected before the passing `43476085012` run.
- The source-level Stage 2 audit maps all 21 routes and records a critical missing per-field provenance join between fmsave validation and normalized save/stat fields. It also identifies the Transfers local/live fallback asymmetry.
- Detailed findings: `docs/STAGE1_STAGE2_AUDIT.md`; external source research: `research/RESEARCH_REGISTER.md`; updated feature matrix: `docs/FEATURE_TRACEABILITY.md`.
- Still requires the user's Windows environment: actual WPF launch and route smoke tests, real support ZIP inspection, disposable FM27 save import, visual/DPI checks and exact Game Pass build/graphics-root identification.
- Stage 3–7 remain paused. Do not start the combined overhaul until the user reviews the findings and approves.

## Final Stage 1 + Stage 2 code/CI handover — 2026-11-13 17:49 UTC

- Authoritative findings and route map: `docs/STAGE1_STAGE2_AUDIT.md`.
- Exact tested source commit: `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991`.
- Windows Actions run `43476085012`: .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; MSTest **64/64 passed**, 0 skipped. [Run 43476085012](https://github.com/lewisxatk/FM-Atlas/actions/runs/43476085012).
- Artifacts: Windows Release `14062557163`; test results `14062892033`.
- Current source inventory at the tested tree: **41 application source files** and **11 files in the MSTest project** (10 C# test files plus the csproj).
- Diagnostics now uses `SupportBundleComposer` for the same allowlisted in-memory package shown in preview and written locally. Five tests verify exact entries, ZIP round-trip, manifest hashes/sizes, redaction and path-traversal rejection. Absolute paths, career-save filenames and credentials/tokens remain always redacted; personal names and recent edit history require opt-in.
- Saved named profiles are implemented for Stats Search and Attribute Search. They persist criteria/options only in a versioned local JSON file, validate up to 200 profiles and 60 conditions each, use atomic replacement, and preserve corrupt originals. Six tests cover validation/persistence. Nested Boolean groups and match explanations remain missing.
- The SQLite live-evidence leak is closed in the generic save/local decision/edit consumers: LIVE mirrors are excluded from ordinary search/count, club-roster, Attribute Search, Role Lab and Mass Edit. The live identity route remains separate; verified live facts are still governed by the canonical field-evidence allowlist. The new isolated SQLite regression passed.
- Stage 2 static review maps all 21 routes and records the actual narrow/shared/informational/blocked behaviour. Key gaps: Recruitment reuses Players; Transfers is not a real transfer-list/asking-price engine and has a save/live source asymmetry; Tactical Lab is a data-coverage view, not a decoded tactic-analysis engine; Atlas Assistant is rule-based; Role Lab is an Atlas weighted estimate, not SI's hidden role rating; Squad Architect is not a validated Best XI optimizer.
- Reverse-engineering findings and legal boundaries are in `docs/STAGE1_STAGE2_AUDIT.md` and `research/RESEARCH_REGISTER.md`. Third-party binaries were not executed and no GPL code or proprietary offsets were copied.
- **Remaining user-side acceptance:** launch the WPF app; smoke-test all 21 routes; test themes at 100/125/150% DPI; create and inspect default and opt-in ZIPs from the actual UI; validate import against a copied FM27 Game Pass save; identify the user's exact installed build; validate the configured graphics root. CI build/tests cannot establish these runtime behaviours.
- **Critical follow-on data requirement:** fmsave's reader/field status and coverage are exported, but normalized player and season-stat values are not consistently joined to individual field provenance at query time. Until that is addressed, imported values must retain parser warnings and must not be described as individually verified.
- Main remains preserved. Live memory Apply/Undo remains disabled by `LiveWriteCertification.IsCertified = false`.
- The user explicitly requested Stage 3–7 remain on hold until reviewing Stage 1/2 findings and approving the combined implementation. Do not begin those stages without that approval.

## Superseding continuation instruction — 2026-11-13

The user has now explicitly instructed us to continue the complete FM Atlas overhaul and ensure the full agreed scope is included, as one integrated development programme. This supersedes the older Stage 3–7 hold language above. Internal stages remain useful for traceability and risk controls, but they are not separate user-facing releases and do not require another approval checkpoint before continuing.

Current verified application checkpoint remains commit 6c18e36fdc706fc49ad74ce15fe1ee73c43ce991, Windows run 43476085012: WPF Release 0 warnings / 0 errors, 64/64 tests passed. The application source has not changed since that tested commit; subsequent worktree commits are docs/research only. Verify the live branch ref before resuming.

## Latest continuation point — portable Search Profiles — 2026-11-13 17:57 UTC

- [x] Exact tested application source commit: e67c0a3b65dcadee485783ca700555bb89fa7076.
- [x] Windows Actions run 43477411616: .NET 8 WPF Release build succeeded with 0 warnings / 0 errors; 67/67 MSTest tests passed, 0 failed, 0 skipped.
- [x] Release artifact 14062868942 and test-results artifact 14063946560 uploaded. The downloaded Windows Release ZIP contains FmAtlas.exe, .NET dependencies, SQLite and the save-bridge tools. The downloaded TRX independently records 67 passed, 0 failed.
- [x] SavedSearchProfileStore now supports Duplicate, ExportJson by all/category, and ImportJson with schema/size/field validation. Imports merge without overwriting existing IDs or same-category names and return explicit skipped-conflict details.
- [x] Stats Search and Attribute Search expose Duplicate profile, Import profiles and Export profiles in addition to New/Save/Load/Delete. Exports are category-specific; imports accept the versioned portable JSON document and refresh the current screen.
- [x] Added regression tests for independent duplicate ID/unique name, category-only export, cross-store import, ID collision safety, name conflict safety, and unsupported schema rejection.
- [ ] Windows runtime acceptance remains outstanding: launch both search screens, verify all profile buttons, export/import between app instances, check skipped conflicts and verify no player/save contents are included in the JSON.
- The branch head at this checkpoint is the tested application commit. Fetch the live ref before resuming. Main remains untouched at 2051bbee042fb8e7203b7855e833e391ccee9ecb.
- Continue the complete roadmap as one integrated programme; this feature tranche is not the full product release.

## Superseding tested source checkpoint — Attribute Search ALL/ANY (2026-11-13)

- Exact current tested application source: `0fbd5fa5162b89798aa55e96669a01b26cd2ddbd`.
- Windows Actions run `43478483715`: WPF Release build 0 warnings / 0 errors; 68/68 passed, 0 failed, 0 skipped.
- Release artifact `14064057973`; test-results artifact `14064172732`. The downloaded release contains FmAtlas.exe, FmAtlas.dll, SQLite and save-bridge scripts; the downloaded TRX confirms 68 passed.
- Attribute Search supports ALL/AND (default) and ANY/OR, persists that choice in Attribute Search profiles, keeps free-text filtering ANDed outside the OR group, and is covered by a real disposable SQLite integration test.
- This checkpoint supersedes earlier references in this handover to 67/67 or 64/64 as the latest tested source. Main remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`.
- Remaining user-environment checks: open the WPF app, test actual search results with a copied FM27 Game Pass save, verify saved profile persistence, and complete visual acceptance. Continue the whole integrated roadmap rather than treating this tranche as final.

## Superseding tested source checkpoint — Advanced Search condition controls (2026-11-13)

- Exact tested application source: `eb5d8f2228310459df34713222f6257fc85b171c`.
- Windows Actions run `43479401387`: WPF Release build 0 warnings / 0 errors; 68/68 tests passed, 0 failed, 0 skipped.
- Release artifact `14064023817`; test-results artifact `14064766061`. Both downloaded and inspected; TRX confirms 68 pass, Release ZIP contains FmAtlas.exe, FmAtlas.dll, SQLite and save-bridge tools.
- Stats Search and Attribute Search now expose Move Up, Move Down and Duplicate actions on each condition row; the underlying ordered list changes with the visible rows.
- This commit is the current branch head; main remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`.
- Remaining: run the app on Windows, validate row layout, save/load row order, duplicate and edge-row behavior, then implement nested AND/OR groups and include/exclude semantics.
- Continue the wider programme as one integrated product; do not treat this checkpoint as the final release.


## Latest combined Stage 3–7 handover — 2026-11-13 18:52 UTC

- Active branch: `overhaul/monolith-full-platform`; exact tested head `c51f80574d9131cb9caa1b25a44541ae370df553`.
- Latest Windows CI: [run 43486407442](https://github.com/lewisxatk/FM-Atlas/actions/runs/43486407442). .NET 8 WPF Release: **0 warnings / 0 errors**; MSTest **87/87 passed**, 0 failed/skipped.
- Release artifact: `14066379269`; test-results artifact: `14067926212`. Download from the Actions run page; artifacts expire 27 November 2026.
- The latest tested package includes the MONOLITH palette/navigation updates, nested universal Advanced Search and local profiles, Player DNA, shortlist matrix with editable candidate notes, Moneyball season/CSV fallback and 50,000-row cohort support, transparent Tactical Studio, Match Lab, Development, Save Inspector, Graphics indexing and Modding Workbench.
- Do not describe this as the fully completed roadmap or a user-accepted release. Remaining implementation gaps and exact runtime acceptance list are in `docs/MASTER_PROGRESS.md` and `docs/FEATURE_TRACEABILITY.md`.
- Next action is manual acceptance on the user's Windows machine using a disposable copy of the FM27 Game Pass career, then report any visible issues/logs for the integrated fix pass. Do not use the original only-save.
- `main` remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`; no feature branch merge has occurred. Live Apply/Undo remains disabled.


## Latest combined development handover — 2026-11-13 21:22 UTC

- Active branch: `overhaul/monolith-full-platform`; exact tested source `4fdd17c344a00c64754218a4770e97c33a19f5eb`.
- Latest Windows CI: [run 43509771993](https://github.com/lewisxatk/FM-Atlas/actions/runs/43509771993). .NET 8 WPF Release: **0 warnings / 0 errors**; MSTest **95/95 passed**, 0 failed/skipped.
- Release artifact `14077394002`; test-results artifact `14078735267`; both expire 27 November 2026.
- New work: 15-position pitch and source-key adapter; primary-position-normalised Moneyball cohorts and CSV output; multi-snapshot Development with identity-reuse guard and evidence labels; supported-build-gated squad planner slot viewer in Squad Architect.
- fmsave 0.12.3 currently documents the squad planner reader for FM27 build `27.0.0+2486695` with one human manager. Slot meanings are unconfirmed; Atlas displays stored slot numbers only.
- fmsave documents that a deleted person's unique_id may be assigned to a new person. Development history therefore resets on changed DOB or changed name when DOB is missing; same-name reuse without DOB remains ambiguous.
- Next work remains the broader programme: age/league Moneyball cohorts where source mapping permits, formation/role-aware XI selection with validated eligibility, deeper tactical intelligence, graphics and mod-management workflows, shared MONOLITH control migration and release engineering.
- User-PC acceptance is deliberately deferred until the user is at the PC and more work is complete. Use a disposable copy of the FM27 Game Pass career; never use the only save.
- `main` remains untouched at `2051bbee042fb8e7203b7855e833e391ccee9ecb`; no merge has occurred. Live Apply/Undo remains disabled.


## Squad Architect best-XI handover — 2026-11-13 21:37 UTC

- Exact tested source: `5ff275d534245880242a7280f95837f6ee3a3b0d`; Windows run [43511883115](https://github.com/lewisxatk/FM-Atlas/actions/runs/43511883115).
- Release build: **0 warnings / 0 errors**; **103/103 MSTest passed**.
- Artifacts: Windows Release `14079346029`; test results `14077554786`.
- Squad Architect supports 4-2-3-1, 4-3-3, 4-4-2 and 3-4-2-1. It queries role-fit candidates by selected club and latest snapshot, obtains position ratings from the same latest registered snapshot, and runs a global one-player-per-slot optimiser.
- Composite formula: `role-fit estimate × source position rating / 20`. This is an Atlas heuristic, not SI's hidden score. It does not include fitness, injury, suspension, availability, tactical familiarity, duty or player-trait effects.
- Position data is gated on registered builds for the profile pitch, Moneyball position cohorts and best-XI selection. If the latest import is unregistered, the app does not silently fall back to stale previous-snapshot position ratings.
- Latest code still needs user-side runtime acceptance. Wait until the user is on PC; test a disposable copy of FM27 Game Pass career, never the only save.
- `main` remains untouched; Live Apply/Undo remains disabled.
