# Changelog

All entries are checkpoint notes. Earlier work is reconstructed from the previous conversation and screenshots; it is not a substitute for original Git history.

## 2026-11-13 — Repository baseline

- Created private GitHub repository `lewisxatk/FM-Atlas`.
- Confirmed repository was empty before initial documentation.
- Added project README, master handover, roadmap, provisional architecture, known issues, next steps and research register.
- Captured previous-session R7.0.2/R7.1.0 package findings and reported implementation milestones.
- No application source code imported and no build/test performed in this repository.

## Historical milestones reported in prior ChatGPT work (dates/source commits to be recovered)

- Advanced player search, statistics and attribute discovery.
- FM27 graphics, live import, player editing, metrics, database and search integrations.
- Guarded live-memory editing scaffolding, importer mappings, persistence, undo and profile graphics resolution.
- Indexed storage, expanded search, profile support and R7.1.0 analytics / role-fit / editor-local / logging package pass (reported).
- FM Atlas value finder with database safeguards.
- C# database parsing, UI imports and search logic inspection/repairs.

## 2026-11-13 — Recovery archives inspected

- Inspected user-uploaded R7.0.2 (83 files) and R7.1.0 (95 files) archives.
- Recorded SHA-256 hashes and compared all relative paths.
- Found 16 changed shared files and 12 files added in R7.1.0; no files were removed.
- Confirmed R7.1.0 package notes say live FM memory writes are not implemented and Windows build/runtime testing remains outstanding.
- Added `recovery/RECOVERY-INVENTORY.md`.
- Full source-tree import remains pending; no claim is made that ZIP contents are already in GitHub.

## 2026-11-13 — R7.2.0 checkpoint received

- Inspected the R7.2.0 ZIP (106 files, 3,610,671 uncompressed bytes).
- SHA-256: `34d985a39526698026b1e09855d7f37e9f6a00dfba4863cf6f4d467aafedc2c8`.
- Compared with R7.1.0: 19 shared files changed, 11 added, none removed.
- R7.2.0 is the newest supplied candidate. Its guarded live writer is source implementation only and remains unverified on Windows and in-game.
- Full source import remains pending.

## 2026-11-13 — Research and proposed evolution added

- Added Wyscout-style Performance & Moneyball Scout to the roadmap: season stats, per-90 metrics, position/league percentiles, attribute + performance filters, value ceiling, multi-league comparisons, bargain profiles and recruitment shortlists.
- Added explicit data provenance and confidence labels; FM save data availability and cross-league calibration must be verified before claims are made.
- Added Tactical Intelligence Studio research gate for FM27 Tactical Effectiveness and Tactical Understanding, with no promise to reproduce hidden FM formulas.
- Confirmed no BepInEx/injector dependency in the current roadmap; standalone app focus retained.
- Added backup/import safety steps to Next Steps.
