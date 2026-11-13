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
- Equivalent regex regression cases passed locally. A Windows .NET 8 MSTest project now covers the cases and is included in the solution/CI workflow; neither the tests nor the current branch build have yet been run on Windows.
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
