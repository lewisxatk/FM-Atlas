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

## Latest continuation point — Role Fit contributions (2026-11-13)

- Exact verified application source commit: `91bd7728c555a0c0e54cfd8d02706467fdafdb89`.
- Windows Actions run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43472380514.
- .NET 8 WPF Release build succeeded with **0 warnings / 0 errors**; MSTest **48/48 passed**, 0 failed/skipped.
- Windows Release artifact: `14061643285`; test-results artifact: `14061143387`.
- RoleFitService now returns one contribution record per available valid role attribute. Each record carries a readable label, source value, Atlas role weight and additive score points. The points sum to the displayed weighted mean.
- ScoutDatabase propagates these records from the latest imported player attribute data. Role Lab displays the selected player's contribution chips beneath the result table; role changes clear stale results; CSV export includes the contribution terms.
- Numeric stability is tested for extreme finite weights. The regression suite verifies contribution sums and readable attribute-name preservation.
- The intermediate compile failure on `2f978f3` was corrected by declaring contribution panel locals before the search-button callback and assigning them later. The final `91bd772` Windows run passed; do not resume from the intermediate source.
- Remaining acceptance: launch the WPF app, visually validate Role Lab layout and selection changes at 100/125/150% DPI, export and inspect CSV using a copied FM27 Game Pass save. Actual FM27 Game Pass field certification remains unverified; live Apply/Undo remains disabled.
- `main` remains preserved. Continue only on `overhaul/monolith-full-platform`.
