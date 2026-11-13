# FM Atlas Roadmap

Status labels: **Source implemented** = present in the supplied R7.2.0 package but not necessarily compiled; **Verified** = passed the relevant reproducible Windows/game test; **Pending** = not yet completed. R7.2.0 is the latest supplied candidate. The complete source import is still pending.

## Current release gate — R7.2.0 recovery

- [x] Receive and inspect R7.0.2 (83 files), R7.1.0 (95 files) and R7.2.0 (106 files).
- [x] Record archive hashes and compare R7.2.0 against R7.1.0 (19 changed shared files, 11 added, 0 removed).
- [ ] Import all three complete source trees into GitHub on a dedicated recovery branch.
- [ ] Verify imported tree against archive inventory and tag the R7.2.0 baseline.
- [ ] Build R7.2.0 WPF on Windows .NET 8; record full log.
- [ ] Launch and smoke-test the app.
- [ ] Run save-import, search, analytics, CSV and graphics tests on copies.
- [ ] Run guarded live Apply/Undo tests on a disposable offline save only after all preconditions pass.

## Phase 0 — Recovery and source-of-truth

- [x] Obtain R7.0.2, R7.1.0 and R7.2.0 archives.
- [x] Inventory archives and record SHA-256 hashes.
- [x] Compare R7.2.0 against R7.1.0 and identify changed/new files.
- [x] Identify R7.2.0 as latest supplied candidate source.
- [ ] Import source without overwriting originals (see current release gate).
- [ ] Establish a reproducible .NET build and baseline test.
- [ ] Record release/version mapping and create a protected baseline tag.

## Phase 1 — Application foundation

- [ ] Audit WPF solution/project structure and dependencies.
- [ ] Verify .NET target frameworks and supported Windows versions.
- [ ] Establish clean build, warnings and test reporting.
- [ ] Separate application data, cache, logs, imported databases and user settings.
- [ ] Add safe diagnostics export with secrets and personal data redacted.

## Phase 2 — Scouting workspace

- [x] R7.2.0 source: advanced multi-attribute player search, up to 60 AND conditions.
- [ ] Verify the feature in the Windows app with a real imported save.
- [ ] Multi-condition filters with saved AND/OR groups (beyond current AND-only implementation).
- [ ] Position picker covering the game's supported positions.
- [ ] Positional suitability display / pitch visualization.
- [ ] League, nationality, homegrown and player profile details.
- [ ] Saved searches and reusable filter profiles.
- [ ] Performance testing on realistic database sizes.

## Phase 3 — Analytics and Moneyball scouting

- [x] R7.2.0 source: indexed statistic facts and dynamic metric catalogue.
- [x] R7.2.0 source: multi-condition stats search and optional per-90/minutes floor.
- [ ] Verify data correctness and performance against a real FM27 save.
- [ ] Multi-metric analytics refinements and cohort normalisation.
- [ ] Compare, rank and shortlist players.
- [ ] Role-fit calculations with documented inputs and uncertainty.
- [ ] Exportable reports and saved analysis views.

### New feature programme — Wyscout-style Performance & Moneyball Scout

**Goal:** Find undervalued players using actual in-save season performance, per-90 output, positional/league percentiles and FM player attributes. This is an FM-career-data scouting system, not a claim to provide Wyscout's proprietary worldwide real-football database.

- [ ] Audit R7.2.0 source for existing statistic ingestion, metric catalogue, player-season linkage, competition IDs, minutes, transfer value and attribute indexes. Reuse before adding new pipelines.
- [ ] Establish exactly which season statistics can be extracted from FM27 saves and which are unavailable or masked by scouting knowledge.
- [ ] Build a canonical metric registry: raw totals, per-90 metrics, rates/percentages, units, source, season, competition, position and availability.
- [ ] Add statistical screener filters: price/value ceiling, age, position, minutes floor, per-90 output, totals, rates and FM attributes (e.g. Dribbling >= 12).
- [ ] Add percentile engine for position-specific cohorts, with configurable competition, age and minutes cohorts.
- [ ] Display both attribute ratings and observed performance separately (e.g. Dribbling attribute 12 vs successful dribbles per 90 and league percentile).
- [ ] Add multi-league comparison with explicit competition-strength adjustment only where a defensible calibration exists; otherwise show raw league-relative percentiles and mark cross-league estimates.
- [ ] Add value-for-performance ranking using transfer value/asking price where actually available; distinguish estimated value, asking price, wage and fee.
- [ ] Add bargain profiles and saved searches: Cheap Dribblers, Creative Midfielders, High-Output Strikers, Ball-Winning Midfielders, Progressive Defenders and custom user definitions.
- [ ] Add player performance profiles: season-by-season trends, per-90 output, percentile bands, minutes/sample size, role/position context and data freshness.
- [ ] Add shortlist, side-by-side comparisons, CSV/report export and recruitment radar against the user's squad/tactical needs.
- [ ] Validate percentile math, sample thresholds, missing values, duplicate player IDs, loan/transfer season splits and competition changes with test fixtures.
- [ ] Clearly label evidence: **Observed in save**, **Calculated by Atlas**, **Estimated**, or **Unavailable**. Never invent missing statistics or claim a calculated score is FM's internal rating.

**Acceptance criteria:** A user can select an FM27 career, set (for example) transfer value under £1m, age 18–24, winger/attacking-midfield positions, Dribbling attribute >= 12, minimum minutes and a performance percentile threshold, then receive traceable player results showing the actual values, cohort, percentile, sample size and source. The search must not silently treat a high attribute as proof of high match performance.

## Phase 4 — Import and database pipeline

- [ ] Audit FM26/FM27 import paths and version mappings.
- [ ] Validate player, club, nation, competition and staff relationships.
- [ ] Detect duplicate, missing and malformed records.
- [ ] Preserve source provenance and schema/version metadata.
- [ ] Add repeatable import diagnostics and recovery from partial imports.

## Phase 5 — Editing and live adapter

- [x] R7.2.0 source: separate local database editor and narrow live-memory writer.
- [ ] Windows build and disposable-save acceptance of live-memory writer before use.
- [ ] Audit local/offline editing separately from live-memory editing.
- [ ] Verify importer/editor field mappings.
- [ ] Verify persistence and undo/redo.
- [ ] Identify supported game versions and adapter capabilities.
- [ ] Require explicit guarded live-write enablement.
- [ ] Validate writes and rollback against disposable test data.
- [ ] Do not advertise unsupported live writes.

## Phase 6 — Graphics and profile presentation

- [x] R7.2.0 source: FM27 graphics resolver and player/club profile wiring.
- [ ] Verify FM27 graphics integration and profile-graphics resolution against the user's real graphics folders.
- [ ] Define supported graphics folders and lookup rules.
- [ ] Test missing, duplicate and malformed graphics references.
- [ ] Keep licensed/user-provided graphics outside source control unless permitted.

## Phase 7 — UI and themes

- [ ] Audit current Obsidian theme state.
- [ ] Audit Aurora Frost theme and retain as a supported design direction.
- [ ] Apply consistent theme tokens across all views, dialogs and controls.
- [ ] Improve navigation, hierarchy, responsive layout and accessibility.
- [ ] Verify contrast, keyboard navigation and scaling.

## Phase 8 — Value finder and quality of life

- [x] R7.2.0 source: Value & Upside Finder with transparent relative percentile formula and filters.
- [ ] Verify reported value finder and database safeguards with real imported data.
- [ ] Define transparent value/ranking inputs.
- [ ] Add filters, sorting and export.
- [ ] Add user-facing diagnostics and safe settings backup/restore.

## Phase 9 — Tactical Intelligence Studio

- [ ] Audit available FM27 tactical effectiveness and tactical understanding data in save/live sources.
- [ ] Display the four effectiveness areas only where actual source data is accessible: Tactical Understanding, Player Ability, Player Buy-In and Next Match Information.
- [ ] Add tactical complexity analysis using documented inputs.
- [ ] Link player attributes, role suitability and squad composition to tactical recommendations.
- [ ] Distinguish observed FM values from Atlas calculations and estimates.
- [ ] Do not claim to reproduce FM's hidden effectiveness formula without evidence.
- [ ] Keep all tactical editing separate, opt-in and guarded; no injector/BepInEx dependency in the current plan.

## Phase 10 — Recruitment and squad intelligence

- [ ] Compare players and find similar profiles/replacements.
- [ ] Build squad depth, age, contract and wage-risk views.
- [ ] Add budget-aware upgrade planning and recruitment fit.
- [ ] Connect Moneyball findings to the user's tactical and squad requirements.

## Phase 11 — Release engineering

- [ ] Versioned release process and changelog.
- [ ] Reproducible publish/package scripts.
- [ ] Smoke tests on supported Windows environments.
- [ ] Installer/update strategy.
- [ ] Release notes distinguish verified, experimental and unavailable features.

## Reported prior-session milestones (not independently verified)

- [x] Reported: advanced player search, UI theming, statistics and attribute discovery.
- [x] Reported: FM27 graphics, import, player editing, metrics, database and search integrations.
- [x] Reported: guarded live-memory editing scaffolding, importer mappings, persistence and undo.
- [x] Reported: indexed storage, expanded search and profile support.
- [x] Reported: FM Atlas value finder and database safeguards.
- [x] Reported: C# database parsing, UI imports and search logic repairs.

These checked boxes mean **reported in previous work**, not verified by this repository. The authoritative verification checklist remains Phase 0 onward.

## Recovery status update — 2026-11-13

- [x] Received and inspected R7.0.2 archive (83 files; hash recorded).
- [x] Received and inspected R7.1.0 archive (95 files; hash recorded).
- [x] Compared relative paths: 16 shared files differ; R7.1.0 adds 12 files and removes none.
- [ ] Import the R7.1.0 source tree into GitHub while retaining R7.0.2 provenance/comparison.
- [ ] Run Windows .NET 8 WPF build and record exact logs.
- [ ] Execute the R7.1.0 acceptance plan using a COPY of an FM27 save.
- [ ] Resume feature development only after source baseline and build status are known.

See [recovery inventory](../recovery/RECOVERY-INVENTORY.md).

## R7.2.0 recovery update — 2026-11-13

- [x] Received and inspected R7.2.0 source checkpoint (106 files).
- [x] Recorded SHA-256 and compared against R7.1.0.
- [x] Identified 19 changed shared files, 11 added files, no removals.
- [x] Identified new advanced search, Value Finder, build-profile, graphics and live writer source files.
- [ ] Import R7.2.0 source to a dedicated GitHub recovery branch.
- [ ] Preserve R7.1.0 and R7.0.2 as separate historical source baselines.
- [ ] Build R7.2.0 on Windows with .NET 8 WPF.
- [ ] Run user acceptance tests on a copied FM27 career; do not assume live writer works until Apply/read-back/Undo and career-switch safeguards pass.
