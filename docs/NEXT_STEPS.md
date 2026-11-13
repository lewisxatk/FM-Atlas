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

## Backup and import safety — read before running

- Keep the original R7.2.0, R7.1.0 and R7.0.2 ZIP files in their current location. Do not rename, delete or edit them.
- Make a second copy of the recovery bundle to a separate folder or external drive if available.
- Extract the bundle to a short, simple path such as `C:\FMAtlas-Recovery\` (avoid running it from inside the ZIP).
- Confirm the bundle contains `IMPORT-TO-GITHUB.ps1` and the R7.2.0 source archive before running.
- The import script is designed to clone the existing repo and push a new recovery branch; it should not force-push or alter `main`.
- If PowerShell reports an error, stop and copy the full error text. Do not repeatedly rerun it or manually delete the cloned repository until we inspect the result.
- After the push, verify the branch exists on GitHub and that the R7.2.0 source tree is present before any merge.
- Keep the source ZIPs and recovery bundle as offline backups even after GitHub import. GitHub is an additional versioned copy, not the only backup.
- Do not upload FM career saves, personal settings, passwords, API tokens, licensed facepacks/logos or generated build output unless specifically required and safe.

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

## Immediate next development priority — MONOLITH foundation first

The visual foundation is a prerequisite for subsequent feature work, not late-stage polish.

1. Complete the R7.2.0 source import and integrity checks; do not assume the source is in GitHub until the recovery branch is verified.
2. Build and record the clean Windows baseline before refactoring.
3. Audit existing WPF views, resources, styles, navigation and search code.
4. Commit the MONOLITH design specification and approve the Command Centre, Scouting Matrix and Advanced Search mockups.
5. Implement shared design tokens, rounded controls, application shell/navigation, dialogs/pop-outs, tables and reusable data visualisations.
6. Validate the shared foundation at common resolutions and with keyboard navigation.
7. Build the FMRTE-style Advanced Search pop-out on top of shared controls, preserving verified search logic and adding grouped AND/OR, saved profiles, clear active-filter feedback and customisable fields.
8. Only then migrate remaining feature screens using the shared components.

### Search UX requirements

- Quick Search for everyday use and one-click Advanced Search pop-out/drawer for detailed filtering.
- Readable filter rows with field, operator and value controls; add, remove, duplicate and reorder conditions.
- Clear AND/OR groups, include/exclude, searchable field catalogue, inline explanations and type-aware value editors.
- Rounded panels and controls, active-filter chips, result counts, reset/clear, saved searches and presets.
- Grouped field catalogue for identity, positions, attributes, contracts, clubs/leagues, origin/home-grown and verified season statistics.
- Fifteen-position suitability pitch and five suitability levels where supported by actual imported data.
- No dead filters: every visible criterion maps to a verified field and tested query behaviour.

## Feature roadmap after baseline gates

1. Complete saved searches, named presets, AND/OR groups, exclusions and custom result columns.
2. Build the universal field registry and field-level search across player/staff/club/competition/nation data.
3. Build the Wyscout-style Performance & Moneyball Scout: season-stat ingestion, per-90 filters, position/league percentiles, attribute + performance filters, multi-league comparison with honest strength caveats, price/value filters, bargain profiles, trend charts and recruitment shortlists.
4. Calibrate Value & Upside Finder with age, league, position, wage/contract and availability context.
5. Audit FM27 Tactical Effectiveness/Tactical Understanding data, then build Tactical Intelligence Studio only around data we can actually read and validate.
6. Expand staff/club/squad planning, Match Lab and player development only where source data supports them.
7. Complete graphics conflict auditing and asynchronous thumbnail handling.
8. Finish theme/accessibility/resolution QA, packaging, installer and update workflow.

## Checkpoint rule

Every development session must end with a GitHub commit or an explicitly documented uncommitted state, plus changed-file list, build/test evidence, known risks and next exact action. Never call source code complete merely because a response or generated ZIP says it is implemented.
