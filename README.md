# FM Atlas

FM Atlas is a Windows desktop application for Football Manager scouting, player analysis, editing, analytics, database inspection and modding research.

> **Repository status (2026-11-13):** The complete R7.2.0 application source has been imported on the isolated branch `recovery/r7.2.0-source-import`. GitHub's recursive tree confirms 106 tracked files in the application source directory and all required solution/project/source paths. The branch has not been merged into `main`. The Windows .NET 8 build, WPF startup, save import and in-game acceptance tests have **not** yet been run.

## Current source candidate

**R7.2.0 is the current working source candidate** (106 files; SHA-256 recorded in `recovery/RECOVERY-INVENTORY.md`). R7.1.0 (95 files) and R7.0.2 (83 files) remain documented historical comparison baselines; keep their original ZIP archives immutable offline. Their full application source trees have not been separately imported into this branch.

## Current priority

1. Build the imported R7.2.0 solution on Windows with .NET 8 and capture the full log.
2. Fix compile/startup defects before feature development.
3. Run R7.2.0 data/search/graphics acceptance checks on copies of user data and career saves.
4. Keep live Apply/Undo disabled until exact process-profile and field mappings are independently validated on a disposable offline career.
5. Resume the agreed MONOLITH design-system and FMRTE-style Advanced Search development sequence.

## Start here

- [Master handover](docs/MASTER_HANDOVER.md)
- [Roadmap](docs/ROADMAP.md)
- [Next steps](docs/NEXT_STEPS.md)
- [Recovery inventory](recovery/RECOVERY-INVENTORY.md)
- [R7.2.0 checkpoint notes](recovery/R7.2.0-CHECKPOINT.md)
- [R7.2.0 static audit](recovery/R7.2.0-STATIC-AUDIT.md)
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
- Keep secrets, personal settings, career saves and licensed third-party assets out of Git.
