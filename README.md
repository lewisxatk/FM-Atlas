# FM Atlas

FM Atlas is a Windows Football Manager intelligence and modding workspace. The integrated programme covers scouting, player intelligence, performance analytics, recruitment, tactical analysis, save/database inspection, safe editing, graphics and skins, mod research and release engineering.

## Current repository status — 13 November 2026

- Private repository: `lewisxatk/FM-Atlas`.
- Active development branch: `overhaul/monolith-full-platform`.
- Main branch is preserved and has not been modified by this overhaul.
- Repaired R7.2.0 application source is present at `application/R7.2.0/FM-Atlas-R7.2.0-Work/`. Source import commit: `6069c20abb8ab85881da1f5c10860de9b2a8e0f1`.
- R7.0.2 and R7.1.0 archives have been inventoried and compared. Their complete source trees still need separate immutable GitHub baselines; they are not the current working code.
- Latest verified Windows CI run `43486407442` at source commit `c51f80574d9131cb9caa1b25a44541ae370df553`: .NET 8 WPF Release build **0 warnings / 0 errors** and **87/87 MSTest tests passed**. Release artifact `14066379269`; test-results artifact `14067926212`. It includes the integrated Stage 3–7 code, Scouting Matrix note persistence, Moneyball FM View fallback/50,000-row cohort path and provenance-rich CSV export. WPF launch, visual/DPI, copied-save and actual Game Pass runtime acceptance remain outstanding.
- A Windows .NET 8 Release build/test workflow is active at `.github/workflows/fm-atlas-windows-build.yml`; see the master progress/handover for the latest exact tested commit.
- Diagnostics privacy hardening, source/feature traceability, Windows MSTest tests, ALL/ANY statistics logic, field-evidence safeguards, provider identity handling, theme updates and CI build automation are committed. See `docs/MASTER_PROGRESS.md` for exact build and test evidence; app/game runtime acceptance remains outstanding.

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
