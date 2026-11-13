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
- Indexed storage, expanded search and profile support.
- FM Atlas value finder with database safeguards.
- C# database parsing, UI imports and search logic inspection/repairs.
- R7.1.0 analytics / role-fit / editor-local / logging package pass (reported).

## 2026-11-13 — Recovery archives inspected

- Inspected both user-uploaded archives: R7.0.2 (83 files) and R7.1.0 (95 files).
- Recorded SHA-256 hashes and compared all relative paths.
- Found 16 changed shared files and 12 files added in R7.1.0; no files were removed from the later package.
- Confirmed R7.1.0 package notes say live FM memory writes are not implemented and Windows build/runtime testing remains outstanding.
- Added `recovery/RECOVERY-INVENTORY.md`.
- Full source-tree import remains pending; no claim is made that the ZIP contents are already in GitHub.

## 2026-11-13 — R7.2.0 checkpoint received

- Inspected the newly supplied R7.2.0 ZIP (106 files, 3,610,671 uncompressed bytes).
- SHA-256 recorded: `34d985a39526698026b1e09855d7f37e9f6a00dfba4863cf6f4d467aafedc2c8`.
- Compared with R7.1.0: 19 shared files changed, 11 added, none removed.
- Recorded the R7.2.0 implementation status, roadmap and user-test gates.
- Corrected the live-editing status: R7.1.0 had no live writer; R7.2.0 contains a narrow guarded writer in source, but Windows compile and live acceptance are still unverified.
- R7.2.0 is the newest supplied candidate; full source import remains pending.
