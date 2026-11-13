# FM Atlas Master Progress

Last updated: 2026-11-13  
Active branch: `overhaul/monolith-full-platform`  
Baseline commit: `2051bbee042fb8e7203b7855e833e391ccee9ecb`  
Latest source-import commit: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`

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
- [x] Built-in Diagnostics / Send Logs requirement added to the master programme: structured logs, one-click health checks, privacy-redacted support ZIP, bundle preview/confirmation, copyable error details, local-first export and log rotation.
- [ ] Diagnostics feature is specified but not implemented in the application yet.

## Not yet verified / not complete


- [ ] No application launch evidence yet.
- [ ] No runtime feature acceptance evidence yet.
- [ ] No full source-level audit of every R7.2.0 module yet.
- [ ] MONOLITH design system is specified but not implemented/migrated in the application.
- [ ] Reverse-engineering register remains desk research/source review, not complete reverse engineering.
- [ ] Exact FM27 Xbox Game Pass build/profile compatibility is unverified.
- [ ] Save parser coverage, import correctness, statistics, percentiles and role-fit require fixtures/known-answer tests.
- [ ] Graphics resolver has not been validated against the user's actual configured folders.
- [ ] Live Apply/read-back/Undo has not been tested on a disposable offline career; live writes must remain guarded/off until then.
- [ ] The complete product roadmap has not been implemented.

## Immediate engineering queue

1. Import repaired R7.2.0 v2 source with provenance and immutable historical baselines.
2. Inventory solution files, assets, scripts, dependencies, data schema, UI construction, search, analytics, import and live adapter.
3. Create architecture map and feature-to-file traceability.
4. Add MONOLITH semantic tokens/shared WPF controls and migrate shell before feature views.
5. Implement universal field registry and Advanced Search enhancements while preserving existing query behaviour.
6. Implement/validate Player DNA, Scouting Matrix, Moneyball, Tactical Intelligence, Match Lab, squad/recruitment and career assistant modules.
7. Implement/validate Save Inspector, local editor, Game Pass read-only adapter, graphics/skin and mod research workbench.
8. Add automated tests, fixtures, performance tests, UI smoke tests, installer and release process.
9. Produce one integrated release candidate and a complete acceptance report.

## Checkpoint discipline

Only mark a feature complete when the source change exists on this branch and its acceptance evidence is linked. A successful compile does not prove runtime functionality. No stage may be described as finished solely because it appears in the roadmap or source comments.
