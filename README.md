# FM Atlas

FM Atlas is a Windows Football Manager intelligence and modding workspace. The integrated programme covers scouting, player intelligence, performance analytics, recruitment, tactical analysis, save/database inspection, safe editing, graphics and skins, mod research and release engineering.

## Current repository status — 13 November 2026

- Private repository: `lewisxatk/FM-Atlas`.
- Active development branch: `overhaul/monolith-full-platform`.
- Main branch is preserved and has not been modified by this overhaul.
- Repaired R7.2.0 application source is present at `application/R7.2.0/FM-Atlas-R7.2.0-Work/`. Source import commit: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`.
- R7.0.2 and R7.1.0 archives have been inventoried and compared. Their complete source trees still need separate immutable GitHub baselines; they are not the current working code.
- The user supplied a Windows 10 / .NET 8.0.427 build log for the repaired R7.2.0 baseline: **0 errors, 15 warnings**. The current overhaul branch has had later source changes and still needs a fresh Windows build and runtime acceptance.
- A Windows .NET 8 Release build workflow has been added at `.github/workflows/fm-atlas-windows-build.yml`. No workflow run is currently reported.
- Current diagnostics privacy hardening, architecture/feature traceability, a Windows MSTest project and the build/test workflow are committed on the overhaul branch. Equivalent regex test cases passed locally; the C# tests, Diagnostics check and current WPF build have not yet been run on Windows.

## Start here

1. [Master handover](docs/MASTER_HANDOVER.md) — authoritative status, decisions and next exact action.
2. [Master progress](docs/MASTER_PROGRESS.md) — verified checkboxes and latest code checkpoint.
3. [Integrated overhaul programme](docs/OVERHAUL_MASTER_PROGRAMME.md) — full product scope and acceptance gates.
4. [Source architecture audit](docs/SOURCE_ARCHITECTURE_AUDIT.md) — files and services actually inspected.
5. [Reverse-engineering audit](docs/REVERSE_ENGINEERING_AUDIT.md) — external research, licensing and technical decisions.
6. [Roadmap](docs/ROADMAP.md) — phased implementation and acceptance checklist.
7. [Next steps](docs/NEXT_STEPS.md) — current build and validation sequence.
8. [Recovery inventory](recovery/RECOVERY-INVENTORY.md) — source archive hashes, file comparisons and import provenance.
9. [R7.2.0 checkpoint](recovery/R7.2.0-CHECKPOINT.md) — latest supplied source-package comparison.

## Design authority

**MONOLITH** is the primary design system: Obsidian/graphite dark mode, Warm Light secondary mode, Atlas orange, Inter typography, Swiss Sport editorial hierarchy, Touchline football visualisation and Pulse statistical presentation. Aurora Frost is not the default direction.

## Engineering and safety rules

- Preserve `main` and all historical source packages. Do not replace or force-push historical baselines.
- Keep source-backed, calculated, estimated, provisional and unavailable values distinct.
- Treat Game Pass, Steam and Epic as separate compatibility targets.
- Do not copy GPL source into Atlas without an explicit licensing decision.
- Unknown game builds remain read-only.
- Live Apply/Undo remains blocked while `LiveWriteCertification.IsCertified` is false. Never enable it without field-level witnesses and disposable offline-save acceptance.
- Never test against the user's only career save.
- A source file, UI route or successful compile is not proof of runtime feature acceptance.
- Keep every change and its evidence on the integrated overhaul branch, with an updated checkpoint after meaningful work.

## Current delivery goal

One integrated FM Atlas release candidate, not a chain of fragmented user-facing releases. Engineering changes will still be built and tested incrementally inside the same programme branch so that the final combined delivery is reliable and recoverable.
