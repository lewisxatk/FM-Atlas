# Next Steps — FM Atlas

## Current gate: get all source history safely into GitHub

**Current verified repository state:** GitHub `main` contains documentation, roadmap, handover and recovery inventory. It does not yet contain the complete application source trees. R7.2.0 is the latest supplied source candidate.

### User action required once

1. Download and extract the latest recovery bundle: `FM-Atlas-GitHub-Recovery-Import-R7.2.0.zip`.
2. On Windows, install Git for Windows if needed and sign in to GitHub.
3. Run `IMPORT-TO-GITHUB.ps1` from the extracted bundle in PowerShell.
4. The script clones this repo, creates a branch named `recovery/r7.2.0-source-import-<timestamp>`, copies the full R7.2.0 app source and preserves R7.1.0/R7.0.2 plus all original ZIPs under `recovery/`, then commits and pushes.
5. Send the printed branch URL back here. Inspect the tree and compare counts before merging anything into `main`.

The script does not force-push and does not alter `main`. Keep the original ZIPs untouched.

## After source import — developer sequence

### Gate 1: source integrity
- [ ] Confirm R7.2.0 source tree, assets, docs, scripts, solution and project files are all present.
- [ ] Confirm R7.1.0 and R7.0.2 comparison baselines are retained separately.
- [ ] Compare imported file counts and hashes against the archive inventory.
- [ ] Identify generated binaries, user data, secrets or licensed assets that should not be committed.
- [ ] Tag the imported R7.2.0 source baseline.

### Gate 2: reproducible Windows build
- [ ] Check .NET 8 SDK, Windows Desktop targeting pack and restore dependencies.
- [ ] Run the supplied logged build script.
- [ ] Save the complete `build-logs/latest-build.log` as a build artifact or attach it to the checkpoint.
- [ ] Fix all compile errors; review warnings.
- [ ] Launch the WPF app and verify startup, navigation and diagnostics.
- [ ] Record exact Windows version, .NET SDK, FM store and game build.

### Gate 3: data and core functionality
- [ ] Back up `%LOCALAPPDATA%\FmAtlas` before testing migrations.
- [ ] Import a COPY of an FM27 save through the save bridge.
- [ ] Validate player/staff/club counts, source coverage and representative CA/PA/attributes.
- [ ] Test multi-attribute search with 5+ conditions and missing-value handling.
- [ ] Test multi-condition statistics search, per-90 calculations and minutes floor.
- [ ] Import an FM View Exporter CSV and verify UID mapping and duplicate headers.
- [ ] Test Value & Upside Finder output and confirm its relative-score explanation.
- [ ] Test graphics resolver against the user's actual graphics folder.

### Gate 4: guarded live editor acceptance
Only after Gates 1–3 pass:
- [ ] Confirm exact Game Pass/Steam process profile and module fingerprint.
- [ ] Compare player identity and CA/PA/visible attribute baselines with FM UI and an independent editor.
- [ ] Use a disposable offline single-player career copy.
- [ ] Test one small CA/PA edit and one visible attribute edit.
- [ ] Verify Apply read-back, FM Continue behaviour, Undo and career-switch refusal.
- [ ] Stop and disable writes if any identity, offset, value or rollback check is uncertain.
- [ ] Keep hidden-attribute, personality, position, staff, club, contract, transfer and loan writes blocked.

## Feature roadmap after baseline gates

1. Complete saved searches, named presets, AND/OR groups, exclusions and custom result columns.
2. Build the universal field registry and field-level search across player/staff/club/competition/nation data.
3. Improve analytics with competition/position/age cohorts, weighted per-90 aggregation and confidence/provenance.
4. Calibrate Value & Upside Finder with age, league, position, wage/contract and availability context.
5. Expand staff/club/squad planning, tactic explorer, Match Lab and player development only where source data supports them.
6. Complete graphics conflict auditing and asynchronous thumbnail handling.
7. Finish theme/accessibility/resolution QA, packaging, installer and update workflow.

## Checkpoint rule

Every development session must end with a GitHub commit or an explicitly documented uncommitted state, plus changed-file list, build/test evidence, known risks and next exact action. Never call source code complete merely because a response or generated ZIP says it is implemented.
