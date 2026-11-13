# FM Atlas

FM Atlas is a Windows desktop application for Football Manager scouting, player analysis, editing, analytics, database inspection and modding research.

> **Repository status (2026-11-13):** The private repository contains the project-control documentation, archive inventories and R7.2.0 recovery checkpoint notes. The complete application source trees from R7.0.2, R7.1.0 and R7.2.0 have **not yet been imported**. No Windows build or in-game acceptance test has been verified from this repository.

## Current source candidate

**R7.2.0 is the newest supplied candidate** (106 files; SHA-256 recorded in `recovery/RECOVERY-INVENTORY.md`). R7.1.0 (95 files) and R7.0.2 (83 files) are historical comparison baselines. Keep all three intact. Do not continue feature work until the R7.2.0 source is imported and its baseline build is tested.

## Current priority

1. Import all three source checkpoints on a dedicated recovery branch without changing `main`.
2. Inspect the imported solution, project files, version metadata and file provenance.
3. Build the R7.2.0 WPF app on Windows with .NET 8 and capture the full log.
4. Fix compile/startup defects before feature development.
5. Run the R7.2.0 data/search/graphics acceptance checks.
6. Test narrow live Apply/Undo only on a disposable offline career after exact process-profile and field-baseline checks.
7. Resume the remaining roadmap in `docs/ROADMAP.md`.

## Start here

- [Master handover](docs/MASTER_HANDOVER.md)
- [Roadmap](docs/ROADMAP.md)
- [Next steps](docs/NEXT_STEPS.md)
- [Recovery inventory](recovery/RECOVERY-INVENTORY.md)
- [R7.2.0 checkpoint notes](recovery/R7.2.0-CHECKPOINT.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Changelog](docs/CHANGELOG.md)
- [Known issues](docs/KNOWN_ISSUES.md)
- [Research register](research/RESEARCH_REGISTER.md)

## Evidence and safety rules

- Never overwrite or delete an existing project/archive until it has been inventoried and backed up.
- Keep original release archives immutable; use versioned folders and dedicated branches.
- Distinguish **source present**, **compiled**, **launched**, **tested with imported save**, and **verified in-game**.
- Treat R7.2.0 live writing as implemented in source but unverified until exact build matching, identity checks, byte preflight, Apply/read-back, career-switch blocking and Undo pass on a disposable offline save.
- Hidden attributes and unsupported structural edits remain blocked.
- Do not invent game structures, offsets, formulas, hidden attributes or data mappings. Record evidence and uncertainty.
- Keep secrets, personal data, career saves and licensed game assets out of the repository unless explicitly necessary and safe.

## Working baseline

The current GitHub main branch is a documentation/recovery-control baseline, not yet the application source baseline. The R7.2.0 import branch is the next milestone.
