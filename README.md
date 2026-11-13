# FM Atlas

FM Atlas is a Windows desktop application for Football Manager scouting, player analysis, editing, analytics, database inspection and modding research.

> **Repository status (2026-11-13):** The repository has been created and the initial project-control documentation is being established. The application source code and R7.0.2/R7.1.0 packages have **not yet been imported** into this repository. No build or runtime verification is claimed by this repository checkpoint.

## Project priorities

1. Preserve existing working application code and release packages.
2. Recover the exact source state behind the latest R7.1.0 work.
3. Maintain a verified, versioned roadmap and changelog.
4. Improve scouting, multi-condition player search, analytics and editing workflows.
5. Research Football Manager data formats and modding tools without treating assumptions as facts.
6. Build and test each meaningful change before calling it complete.

## Start here

- [Master handover](docs/MASTER_HANDOVER.md)
- [Roadmap](docs/ROADMAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Changelog](docs/CHANGELOG.md)
- [Known issues](docs/KNOWN_ISSUES.md)
- [Next steps](docs/NEXT_STEPS.md)
- [Research register](research/RESEARCH_REGISTER.md)

## Development safety rules

- Never replace or delete an existing project/archive until it has been inventoried and backed up.
- Keep original release archives immutable; work from extracted copies or dedicated branches.
- Record source commit, package version, build result and test evidence for each checkpoint.
- Distinguish **implemented**, **present in a package**, **compiled**, **runtime-tested** and **verified in-game**.
- Do not claim live game writes work until an actual supported write path has been tested against a safe copy.
- Do not invent game structures, offsets, formulas, hidden attributes or data mappings. Record evidence and uncertainty.
- Keep secrets, personal data, game saves and licensed game assets out of the repository unless explicitly necessary and safe.

## Working baseline

The first baseline is a documentation-only checkpoint based on the previous conversation and the screenshots supplied on 2026-11-13. The actual source baseline is pending import and audit.
