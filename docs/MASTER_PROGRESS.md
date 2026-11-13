# FM Atlas Master Progress

Last updated: 2026-11-13  
Active branch: `overhaul/monolith-full-platform`  
Baseline commit: `2051bbee042fb8e7203b7855e833e391ccee9ecb`  
Latest source-import commit: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`  
Latest source-code checkpoint: `6fc46757688be84af497e13c3d27e40877da3f92`

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
- [x] Compared repaired v2 ZIP with the later `updated.zip`: 102 common source files are byte-identical; the eight changed source files' Git blob hashes match the eight files in the existing recovery branch. The recovery branch therefore already contains the source corresponding to the later repaired build, for those source paths.
- [x] Closed redundant source-import PR #1 after direct import into the overhaul branch; no changes to `main`.

## Newly confirmed product requirement

- [x] User confirmed delivery preference: one integrated development/release candidate, not multiple small user-facing releases.
- [x] Existing Diagnostics / Tester & Logs page, local rolling logs, health checks, copy/export, support ZIP preview, manifest hashes and local-only export inspected in source.
- [x] Fixed over-escaped credential redaction and expanded path redaction to cover complete Windows and UNC paths, including spaces (code checkpoint `6fc4675`).
- [x] Added deterministic support-data redaction regression checks to the Diagnostics health-check list (bearer token, API key, password, user path, spaced drive path, UNC path and standalone save filename).
- [ ] Windows/.NET build and in-app execution of the new redaction check still required; equivalent regex cases passed in a separate local check only.

## Initial source architecture audit — 2026-11-13

- [x] Confirmed the imported project is a .NET 8 WPF desktop application using Microsoft.Data.Sqlite 8.0.11.
- [x] Confirmed startup initialises AtlasLog, applies MonolithTheme and registers dispatcher, AppDomain and unobserved-task exception logging.
- [x] Mapped the dynamic MainWindow shell, partial UI files, core models, database service and game-adapter services. See docs/SOURCE_ARCHITECTURE_AUDIT.md.
- [x] Confirmed an initial MONOLITH theme service and Obsidian/Warm Light preference already exist; full shared-control and screen migration is incomplete.
- [x] Confirmed exact Game Pass and Steam profile records exist in source, while field-level and user-installation acceptance remain unverified.
- [x] Confirmed Apply and Undo call the global live-write certification gate; IsCertified is hard-coded false, so live memory writes remain blocked.
- [x] Confirmed GraphicsResolver supports a configurable root and config.xml scanning, but has not been tested against the user's actual folders.

## Not yet verified / not complete


- [ ] No application launch evidence yet.
- [ ] No runtime feature acceptance evidence yet.
- [ ] No full source-level audit of every R7.2.0 module yet.
- [ ] MONOLITH is partially present (central theme service and mode switch); semantic-token coverage, reusable controls and migration of all views remain incomplete.
- [ ] Reverse-engineering register remains desk research/source review, not complete reverse engineering.
- [ ] Exact FM27 Xbox Game Pass build/profile compatibility is unverified.
- [ ] Save parser coverage, import correctness, statistics, percentiles and role-fit require fixtures/known-answer tests.
- [ ] Graphics resolver has not been validated against the user's actual configured folders.
- [ ] Live Apply/read-back/Undo has not been tested on a disposable offline career; live writes must remain guarded/off until then.
- [ ] The complete product roadmap has not been implemented.

## Immediate engineering queue

1. Run a Windows Release build and smoke test for the recovered source plus Diagnostics redaction hardening; triage the 15 previously reported warnings.
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

- 2026-11-13: Corrected credential-redaction patterns, switched to complete drive-letter/UNC path redaction (including paths with spaces), added self-check cases and surfaced the check in Diagnostics. Code commits: `05d2c77`, `f7b9f4c`, `9c90953`, `6fc4675`. Not yet built or run in Windows.

- 2026-11-13: Added the initial Windows MSTest project and CI test execution; committed test code, solution inclusion and workflow updates. Seven sensitive-value pattern cases passed the local pattern-level check; C# tests and WPF build remain unexecuted on Windows.
