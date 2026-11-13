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
- The repaired v2 source package has not yet been imported into this GitHub branch. The repository's existing source-of-truth history and recovery archives remain important and must be preserved.

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
