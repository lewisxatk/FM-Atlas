# FM Atlas Roadmap

Status labels: **Verified** = evidence inspected in this repository or reproducible test; **Reported** = described in prior session/screenshots but not independently verified here; **Pending** = not yet confirmed or completed.

## Phase 0 — Recovery and source-of-truth

- [ ] Obtain R7.0.2 archive and R7.1.0 package/source.
- [ ] Inventory archives and record SHA-256 hashes.
- [ ] Compare package/source versions and generate a file-level change report.
- [ ] Identify the latest authoritative source tree.
- [ ] Import source without overwriting originals.
- [ ] Establish a reproducible .NET build and baseline test.
- [ ] Record release/version mapping and create a protected baseline tag.

## Phase 1 — Application foundation

- [ ] Audit WPF solution/project structure and dependencies.
- [ ] Verify .NET target frameworks and supported Windows versions.
- [ ] Establish clean build, warnings and test reporting.
- [ ] Separate application data, cache, logs, imported databases and user settings.
- [ ] Add safe diagnostics export with secrets and personal data redacted.

## Phase 2 — Scouting workspace

- [ ] Verify reported advanced player search implementation.
- [ ] Multi-condition filters (AND/OR groups where appropriate).
- [ ] Position picker covering the game's supported positions.
- [ ] Positional suitability display / pitch visualization.
- [ ] League, nationality, homegrown and player profile details.
- [ ] Saved searches and reusable filter profiles.
- [ ] Performance testing on realistic database sizes.

## Phase 3 — Attributes and analytics

- [ ] Audit attribute discovery and indexed attribute storage.
- [ ] Verify the reported 500-leaf discovery cap and remove only if confirmed.
- [ ] Multi-metric analytics filters.
- [ ] Compare, rank and shortlist players.
- [ ] Role-fit calculations with documented inputs and uncertainty.
- [ ] Exportable reports and saved analysis views.

## Phase 4 — Import and database pipeline

- [ ] Audit FM26/FM27 import paths and version mappings.
- [ ] Validate player, club, nation, competition and staff relationships.
- [ ] Detect duplicate, missing and malformed records.
- [ ] Preserve source provenance and schema/version metadata.
- [ ] Add repeatable import diagnostics and recovery from partial imports.

## Phase 5 — Editing and live adapter

- [ ] Audit local/offline editing separately from live-memory editing.
- [ ] Verify importer/editor field mappings.
- [ ] Verify persistence and undo/redo.
- [ ] Identify supported game versions and adapter capabilities.
- [ ] Require explicit guarded live-write enablement.
- [ ] Validate writes and rollback against disposable test data.
- [ ] Do not advertise unsupported live writes.

## Phase 6 — Graphics and profile presentation

- [ ] Verify FM27 graphics integration and profile-graphics resolution.
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

- [ ] Verify reported value finder and database safeguards.
- [ ] Define transparent value/ranking inputs.
- [ ] Add filters, sorting and export.
- [ ] Add user-facing diagnostics and safe settings backup/restore.

## Phase 9 — Release engineering

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
