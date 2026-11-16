# FM Atlas — Current Checkpoint

**Checkpoint:** 005  
**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Base head before this checkpoint:** `8d7e21327408a0a8f16a3aecb49d25dbe53e6e79`  
**Status:** Fresh WPF shell compiles, tests and Windows artifact upload successfully. First shell package is available for user review.

## Mission

Build a genuinely fresh FM Atlas desktop application. Legacy source is a research/reference pool only. Do not continue the old UI or preserve poor architecture merely for continuity.

## Recovered source — verified repository state

- Located the previously generated `FM-Atlas-GitHub-Recovery-Import-R7.2.0.zip` in the user's FMATLAS Library folder: `/FMATLAS/FM-Atlas-GitHub-Recovery-Import-R7.2.0.zip`, 17,481,541 bytes.
- The current tool could identify the Library record but refused raw-byte materialization, so the ZIP itself was not extracted again in this session.
- The corresponding GitHub recovery branch is present: `recovery/r7.2.0-source-import`.
- Its current branch commit was inspected: `2599dcc027fe2913d9a5f179ff697b0dbf2b5762`.
- Its recursive Git tree is not truncated and contains 119 blobs total, including exactly 106 files under `application/R7.2.0/FM-Atlas-R7.2.0-Work/`, with 31 source files under `src/`.
- Required WPF project, solution, app views, services, data models, build scripts, assets, docs and research notes are present.
- The source package targets `net8.0-windows`, uses WPF and Microsoft.Data.Sqlite 8.0.11.
- The R7.2.0 static audit explicitly says no Windows build, WPF launch, real save import, Game Pass process match or live Apply/Undo test was run. Its narrow writer is not accepted as verified.
- The recovery branch remains historical source only; the fresh rebuild branch is separate. No legacy code has been copied into the new solution.

## Uploaded research inventory

The seven newly uploaded packages have been SHA-256 inventoried and statically inspected. No installer or application executable was run.

- FM Player Analyzer 2.2.3 source ZIP extracted for static review (GPL-3.0): C++20/Qt 6, headless core, SQLite schema v16, read-only FM27 save import, graphics config index and extensive tests.
- TalentSeer 27.1.2 ZIP contains README + compiled EXE + notices, no source; treated as product benchmark only.
- FMRTE 27.0.1 build 12 ZIP contains portable binaries/resources, no source; 25 release-image assets catalogued as feature leads.
- Genie Scout ZIP contains one installer EXE only.
- FMSuperScout and FMLibreEditor uploads are installers only; their public source repositories were inspected directly.
- FM Dossier upload is an installer only.
- Research inventory and hashes: `research/UPLOADED_RESEARCH_INVENTORY.md`.
- Technical findings: `research/RESEARCH_FINDINGS_001.md`.
- FMRTE 27 player/staff/club/search/edit-history feature audit: `research/FMRTE_FEATURE_AUDIT_001.md`.
- Field counts, attribute groups and confidence states: `research/FIELD_COVERAGE_LEDGER.md`.

## Repository research completed

- FMLibreEditor: separate process architecture, native `game_plugin.dll`, write preconditions, save-generation invalidation, read-only fallback; exact Game Pass evidence is package 0.9.2435.0 and must not be generalised to other builds.
- fm-editor: FM27 layout includes inherited FM26 leads; only explicitly witnessed fields are verified. Its README records a narrow Game Pass package bring-up, not broad feature parity.
- FMSuperScout: MIT, .NET 8 Windows read-only scanner plus local search application; useful extraction/indexing benchmark.
- fmsave: MIT, FM26/FM27 offline parser; 52 unique non-foot attributes, 8 personality fields and extensive save tables, with explicit coverage/unknown-value semantics.
- FMLibreEditor player UI catalogue lists Technical 14, Mental 14, Physical 8, Goalkeeping 13, Hidden 5 and Feet 2; Technical/Goalkeeping overlap First Touch and Passing. This yields 52 unique non-foot attributes plus two foot ratings. Exact FM27 profile order still needs visual confirmation.
- FMSuperScout documents a 55-byte raw attribute block scaled by five; this is not a 55-field UI catalogue or a write map.
- FM View Exporter: GPL-3.0, documents 375 FM27 table columns and 377 Insert Column menu entries; table order is not assumed to equal player-profile attribute order.
- FM Player Analyzer: GPL-3.0, useful save import, profile, graphics indexing and testing research.
- FMRTE official documentation audited for player, staff and club tabs, advanced search, edit history and other tool families.
- Licensing boundaries are documented; no third-party source copied.

## Architecture decision

- Selected a fresh .NET 8 WPF solution for the Windows 10/Game Pass target.
- New projects: Domain, Application, Infrastructure, Desktop and Tests.
- The old WPF UI/code-behind is not reused.
- SQLite is referenced by Infrastructure for the local Atlas store; schema/repositories are not yet implemented.
- Live memory, offline save parsing, FM export import and graphics lookup will be separate providers.
- Live writer remains a separate disabled-by-default service, gated by exact build and field-level evidence.
- Full rationale: `docs/ARCHITECTURE_DECISION.md`.

## Initial application shell committed

- `FMAtlas.sln` and `Directory.Build.props`.
- Domain / Application / Infrastructure / WPF Desktop / Tests project files.
- WPF black/off-white/orange theme resources.
- Compact left navigation with no Search item.
- Top Quick Search placeholder and Advanced Search action.
- Compact Save Atlas / Save Changes controls (disabled until implemented).
- Prominent Load Game action on the right.
- Honest dashboard state: no career loaded, no player records imported, no pending edits.
- Load Game currently performs a read-only process/module presence check only. It does not read memory, identify the build, load a career or edit the game.
- Windows GitHub Actions restore/build/test workflow committed, with Windows build artifact upload step added.
- All unimplemented controls are disabled or clearly identified as In Progress.

## Windows CI evidence

- Run [43787424965](https://github.com/lewisxatk/FM-Atlas/actions/runs/43787424965) on commit `5a4dcb6b2075c8d60cc6e675d7a9557b21558bd5` completed successfully.
- Restore passed.
- All five projects compiled on Windows Server 2025 with .NET SDK 8.0.428.
- Build: 0 warnings, 0 errors.
- Tests: 3 passed, 0 failed.
- This is a compile/unit-test result only. It does not prove the WPF app has been launched, visually compared, connected to FM or read/written game data.
- Workflow run [43787554512](https://github.com/lewisxatk/FM-Atlas/actions/runs/43787554512) completed successfully, including artifact upload.
- Artifact: `fm-atlas-fresh-shell-win-x64.zip`, 13,154,594 bytes, SHA-256 `587c9e02ed906ca47dad868a9fef003b5f290378a68fe3028040d5054629bb22`, GitHub artifact ID `14191091352`, expires 2026-11-30.
- The ZIP was downloaded and inspected: 35 files including `FMAtlas.exe`, application DLLs, runtime config and SQLite dependencies. It is a framework-dependent shell preview, not the finished editor.

## Current milestone

**M2 — Fresh application foundation.**

## Next actions

1. User inspects/runs the shell package on Windows and compares screenshot with REF-001; refine only against approved reference.
2. Add the researched FM27 attribute catalogue to the new Domain project and tests, clearly marking profile order provisional until visually confirmed.
3. Build the REF-002 Advanced Search dialog shell and filter model.
4. Build the REF-004 full-screen tabbed player profile shell.
5. Continue field-level Game Pass research and read-only connection probe.

## User action needed

No new research download is required now. The R7.2.0 recovery ZIP is located in the Library; its imported source branch is available. Later, provide the exact FM27 Game Pass package/build details and use a disposable offline career for live integration tests.


---

## Checkpoint 006 — continuation after stream interruption

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Checkpoint base head:** `eecf272dad88daad192252a063e1ce72e1d3bcd5`  
**Status:** Domain attribute catalogue and first REF-004 player-profile workspace shell committed. Latest Windows CI runs were still in progress when this checkpoint was written.

### Work continued and saved

1. Re-opened the existing master plan, checklist, continuity procedure, research inventory, findings, field ledger, FMRTE audit, architecture decision and current checkpoint before continuing.
2. Re-inspected the exact FMLibreEditor FM27 Game Pass 0.9.2435.0 recon, player attribute group source and probe entry point.
3. Re-inspected FMSuperScout `Fields.cs` and its external scanner implementation.
4. Re-opened the uploaded FM Player Analyzer source and TalentSeer user guide. No EXE or installer was executed.
5. Added `src/FMAtlas.Domain/Players/PlayerAttributeCatalog.cs` with 52 unique non-foot attributes, separate feet, shared Technical/Goalkeeping First Touch and Passing definitions, source order and explicit documented evidence status.
6. Added `tests/FMAtlas.Tests/PlayerAttributeCatalogTests.cs` covering counts, group overlap, outfield/goalkeeper group selection, hidden/feet separation, technical order and lookup.
7. Added the REF-004 player-profile view model and WPF view shell:
   - left identity/information rail and neutral portrait placeholder;
   - central FM attribute groups rendered from the Domain catalogue;
   - right-side empty/verified-data panels;
   - open-player tab row;
   - compact FMRTE-style player section tabs at the top;
   - no fabricated player, CA/PA, statistics or editable values.
8. Added profile-specific WPF styles to `App.xaml`.
9. Updated the research findings with the additional source-level evidence and the distinction between documented layout and user-build verification.

### CI and build evidence

- Attribute catalogue/test commit triggered Windows workflow run [43787875022](https://github.com/lewisxatk/FM-Atlas/actions/runs/43787875022); all steps completed successfully, including restore, build, tests and artifact upload.
- The profile view/model/style commits triggered Windows workflow runs including [43788055915](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788055915), [43788051366](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788051366), [43788027235](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788027235), [43788023540](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788023540), [43788020996](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788020996) and [43788017254](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788017254). They were pending/in progress at the time of this checkpoint; do not mark the profile view as build-verified until the latest run is checked.
- Local container does not have the .NET SDK. Windows GitHub Actions is the compile/test authority for this branch.

### Important limits

- Player profile view is a shell/template, not yet wired to a live player provider or workspace tab manager.
- Attribute values are deliberately empty. No CA/PA or field values are being fabricated.
- Profile section buttons are present but must remain unavailable until actual player data and per-section providers are connected.
- No player memory reads or writes have been performed.
- The FMLibreEditor Game Pass profile applies only to the documented 0.9.2435.0 package. The user's installed package has not been fingerprinted.
- The FMRTE/FMLibreEditor/FMSuperScout proprietary/source-code reuse boundaries remain unchanged. No third-party code was copied.

### Current milestone

M4 — Player workspace shell started, after M1 catalogue foundation.

### Next actions

1. Check latest Windows CI run(s) and fix any XAML/C# compile or test failures before adding more UI.
2. Add the REF-002 Advanced Search schema and WPF dialog shell, keeping all controls disabled until a real search/index provider exists.
3. Implement typed player profile data contracts and connect the profile shell to actual selected-player records once a read provider is available.
4. Continue Game Pass exact-package identification and read-only probe research; do not enable writes.
5. Update this checkpoint after the next coherent change.

### Uncommitted work

None known. All code and documentation changes above were committed through GitHub. Verify latest branch head before continuing.


---

## Checkpoint 007 — Advanced Search continuation

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Head:** `596c3200b3907c2780d4b83bc6d33ecbcffea923`  
**Status:** REF-002 Advanced Search visual shell and documented search schema committed. Windows CI for the newest commits is pending.

### Work saved

1. Added `src/FMAtlas.Domain/Search/AdvancedSearchCatalog.cs`:
   - seven approved FMRTE-style categories in the required order;
   - Player / Coach / Club entity types;
   - typed field kinds (text, ranges, boolean, date, lookup, enum, position, trait);
   - source-documented general, contract, coach and club search fields;
   - attribute search fields generated from the canonical player attribute catalogue;
   - explicit distinction between documented field existence and executable query support;
   - all fields remain `InProgress`, so the UI cannot imply they are already searchable.
2. Added `tests/FMAtlas.Tests/AdvancedSearchCatalogTests.cs` for category order, evidence/execution status, entity-specific fields, attribute coverage and player-only field restrictions.
3. Added `src/FMAtlas.Desktop/Views/AdvancedSearchWindow.xaml` and code-behind:
   - REF-002 style dialog composition;
   - Player / Coach / Club selectors;
   - left-side category list;
   - grouped General filter panels;
   - position pitch placeholder;
   - OK / Cancel actions;
   - explicit In Progress messaging;
   - filter controls and actions are disabled because no verified query/index provider is connected yet.
4. Previous checkpoint's REF-004 player-profile shell remains committed, with section buttons disabled until a real player is selected.

### CI status

- Attribute catalogue run [43787875022](https://github.com/lewisxatk/FM-Atlas/actions/runs/43787875022) passed.
- Profile view/style run [43788055915](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788055915) passed.
- Search dialog and domain-schema runs [43788205408](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788205408), [43788210267](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788210267), [43788255439](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788255439) and [43788258789](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788258789) were queued/in progress at checkpoint time. Check the newest run before claiming the search shell builds.

### Current limitations

- The Advanced Search dialog is a visual/structural shell; controls are intentionally disabled.
- Search field definitions are documented, not read-verified or query-executable.
- General filter layout is shown. Other categories still need their full grouped UI panels.
- Quick Search remains a disabled placeholder.
- No player records, statistics or search results are fabricated.
- No live game memory has been read or written.
- No original recovery ZIP bytes were needed for this step; the historical source branch remains available and untouched.

### Next exact actions

1. Check Windows CI on the latest head and correct any XAML/C# errors.
2. Expand Advanced Search panels for Contract, Technical/GK, Mental, Physical/Hidden, Preferred Moves and Relations from the field schema.
3. Add a SearchFilter query model and tests for min/max validation and AND/OR semantics before connecting a data provider.
4. Connect the full-screen player profile to a typed player record; add the graphics lookup provider as a separate service.
5. Continue exact Game Pass build identification and read-only probe research. No writes until the target build and field-level mappings are verified.

### Uncommitted changes

None known. All changes above were committed through GitHub. Re-read the branch head and checkpoint before the next work unit.


---

## Checkpoint 008 — recovery, FMRTE package audit and read-only build identification

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Head at checkpoint:** `d9bb7ee5d7d8a2204521f1b237ba43e9e3d3b472`  
**Status:** Research and code changes saved. Latest code changes after the last successful Windows build have not yet been compiled because GitHub Actions jobs are failing before any runner/steps start.

### Recovery ZIP

- Located the exact original archive in the user's Library: `/FMATLAS/FM-Atlas-GitHub-Recovery-Import-R7.2.0.zip` (17,481,541 bytes).
- Attempted raw-byte materialization; the file provider refused the raw-byte path, so the ZIP could not be extracted in the current runtime.
- Confirmed the recovered source is already available in GitHub branch `recovery/r7.2.0-source-import`, under `application/R7.2.0/FM-Atlas-R7.2.0-Work`.
- Read the stored 13 November R7.2.0 Windows build log: 92 compile errors and 15 warnings. Major categories include missing System.IO symbols, unassigned locals, missing importer fingerprint/RVA constants, missing AtlasLog.Warn and an IntPtr-to-ulong conversion. This reinforces the decision to build fresh and use old code only as selective research.

### Uploaded packages inspected

- FMRTE 27.0.1 build 12 ZIP: package metadata, release notes and selected bundled UI screenshots inspected without executing it.
- FM Player Analyzer 2.2.3 ZIP: source tree, read-only FM27 save-reader/graphics architecture and third-party notices inspected.
- TalentSeer 27.1.2 ZIP: README and third-party notices inspected; binary not executed/decompiled.
- FMSuperScout, FMLibreEditor and FM-Dossier installers: file metadata/static strings inspected; installers not executed.
- Genie Scout 26 ZIP: contains only the installer executable; not executed.

### Licensing decisions

- FMLibreEditor and FM Player Analyzer are GPLv3: research/reference only unless FM Atlas intentionally adopts GPLv3 obligations.
- FMSuperScout and fmsave are MIT: potential code reuse with attribution/notices and dependency review.
- FMRTE is commercial/proprietary; do not copy code or assets.
- TalentSeer is supplied as a binary and guide; use the guide as feature discovery only.

### New FMRTE reference material

- REF-005: Potential Attributes panel.
- REF-006: Edit History.
- REF-007: Tactical Understanding and national tactical understanding.
- REF-008: Assistant Review.
- REF-009: Mass Edit Presets.
- REF-010: Career Plans.

The feature audit now records the controls and behaviour visible in those build 12 assets and the package's exact 27.0.1 build 12 release notes.

### Additional code committed

1. Player attribute catalogue now has:
   - 52 unique standard/hidden non-foot attributes;
   - 8 separate personality ratings in the documented FM storage order;
   - 2 foot ratings kept separate;
   - outfield and goalkeeper profile groups;
   - shared First Touch and Passing;
   - source-evidence labels.
2. Advanced Search now has:
   - seven category definitions;
   - Player/Coach/Club entity support;
   - typed filter field definitions;
   - personality fields in Mental Attributes;
   - typed conditions and structural validation;
   - explicit All/Any logic;
   - all search fields still In Progress and not executable.
3. Player profile shell section list now includes Personality, Hidden Attributes, Staff, Clone Center and Role Attributes; actions remain disabled until a real player record and providers exist.
4. Read-only PE fingerprint reader added; it reads only on-disk module metadata, not process memory.
5. Reference-only build registry added for FMLibreEditor Game Pass 0.9.2435.0 and 0.9.2409.0 fingerprints. Both profiles are explicitly read-disabled and write-disabled.
6. Main window diagnostics report the external reference match without enabling live data access.

### CI evidence and limitation

- Last known successful Windows build/test run: [43788503042](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788503042), commit `fbe36400b4894c28acd50afe22e651fe692e9796`.
- Subsequent code commits for personality and build-profile support triggered runs that ended in failure with `runner_id=0` and empty `steps`; the jobs did not start, so these are **not compiler/test failures**.
- A rerun was attempted; it again failed before a runner or step was allocated.
- Local container has no .NET SDK and cannot build WPF. Do not claim the newest code is build-verified until a Windows runner completes restore/build/test.

### Current implementation limits

- No live process memory has been read.
- No player, staff or club records have been imported from the user's running game.
- No writes have been performed.
- The 0.9.2435.0 and 0.9.2409.0 profiles are external reference fingerprints only; user's installed package remains unidentified.
- Player profile and Advanced Search are structural shells. Their controls are intentionally disabled until data/query providers exist.
- The R7.2.0 ZIP bytes were not materialized, but the recovered source branch and historical build log are available.

### Next exact actions

1. Resolve/await Windows Actions runner availability and compile the latest branch. Fix any genuine compiler/test failures once logs exist.
2. Add the remaining Advanced Search category panels from the schema (Contract, Technical/GK, Mental, Physical/Hidden, Preferred Moves, Relations).
3. Add typed PlayerRecord/StaffRecord/ClubRecord models and source provenance.
4. Implement the Game Pass read-only process probe and compare the actual user module fingerprint to the reference registry.
5. Implement graphics locator indexing `config.xml` and resolving portraits by player UID.
6. Connect player profile tabs to a real read provider; do not enable editing yet.
7. Continue only after updating this checkpoint with exact commit/run evidence.

### Uncommitted changes

None known. All listed changes are committed. Re-read this checkpoint and branch head before further work.


---

## Checkpoint 009 — verified artifact and CI runner state

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Current head:** `cc7a0240ee41b04efc9fcdc572940bfbbb20be31`

### Last verified Windows build artifact

The last successful Windows build is from commit `fbe36400b4894c28acd50afe22e651fe692e9796`:
- Workflow run: [43788503042](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788503042)
- Result: success (restore, build, tests and artifact upload).
- Artifact: `fm-atlas-fresh-shell-win-x64`
- Artifact ID: `14191452135`
- Size: 13,190,770 bytes
- Expires: 2026-11-30.

This artifact includes the fresh WPF shell, REF-004 profile view shell, REF-002 advanced-search shell, search domain model and static PE fingerprint reader. It predates the later eight-personality-field and reference-only build-profile registry additions, so it is a fallback verified build, not the latest source state.

### Latest CI runner issue

The code commits after `fbe36400` triggered GitHub Actions runs that ended within seconds with:
- job `runner_id=0`;
- empty `steps`;
- no build/test log available.

A failed-job rerun was attempted and produced the same pre-runner failure. This is not evidence of a C# or XAML compile failure; the job never reached checkout/restore/build. Do not mark the latest code as verified until a Windows runner actually executes the workflow.

### Current safe continuation point

- All code and docs are committed; no uncommitted changes are known.
- Last fully verified build is commit `fbe36400`.
- Latest source head is `cc7a0240`.
- Latest unverified additions: separate Personality group and search filters, reference-only GameBuildProfileCatalog, MainWindow reference-match diagnostic, FMRTE REF-005–REF-010 documentation.
- Next step is to get a Windows Actions runner to execute the latest source, then fix any actual compile/test errors if they appear.


---

## Checkpoint 010 — typed data, graphics index, Advanced Search panels and SQLite schema

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Head:** `d762f24e5e50e45240c476fe6b8c63b7d8dfe0d9`  
**Status:** New source and schema work committed. Latest Windows Actions run was queued when this checkpoint was recorded; compile/test result is not yet available.

### Work saved since checkpoint 009

1. **Typed, provenance-aware domain records** (commit `f3094878`):
   - Data source kinds and field provenance.
   - Player, staff and club record types with sourced nullable fields.
   - Provider-scoped entity identity.
   - Attribute/personality/position/statistic and finance/facility dictionaries.
   - Write-verification metadata requires live-memory origin, write-verified status and a target build fingerprint; it is not a write command.

2. **Independent read-only graphics locator** (commit `f3094878`):
   - Recursively indexes config.xml mappings below a user-selected graphics root.
   - Resolves person portraits, club logos and nation logos.
   - Accepts numeric and r- Newgen player IDs.
   - Ignores non-profile assets such as person icons.
   - Resolves relative paths and rejects absolute paths, wildcard paths and traversal outside the graphics root.
   - Deterministic duplicate precedence.
   - No image files are moved, renamed or modified.
   - Synthetic tests added for asset mapping, Newgen UIDs, duplicate mappings, path traversal, missing folders and clearing the index.
   - Research evidence and limitations recorded in `research/GRAPHICS_REVERSE_ENGINEERING_001.md`.
   - Not yet tested against the user's actual graphics folders. Image decoding, WPF binding and persistent cache are not implemented.

3. **Schema-driven Advanced Search category navigation** (commit `8f67e909`):
   - Player/Coach/Club selectors now switch the entity context.
   - All seven FMRTE categories switch the visible field panels.
   - General player panel retains the dedicated REF-002 two-column layout.
   - Other categories are rendered from the Domain field catalogue.
   - Field controls remain disabled and clearly state query handlers are In Progress; there is no real search provider yet.
   - Cancel works. OK remains disabled until a verified query executor exists.
   - This is a structural/data-bound UI shell, not executable Advanced Search.

4. **Local SQLite schema migration 1** (commit `d762f24e`):
   - Added IAtlasDatabase abstraction and AtlasDatabase initializer.
   - Transactional, idempotent schema version 1.
   - Tables for migrations, data sources, provider-scoped entities, field values with source/evidence/build provenance, entity relationships and graphics mappings.
   - Foreign keys and indexes for entity name, field keys, numeric values and relationship targets.
   - Tests added for initialisation, schema tables, idempotence and foreign-key enforcement.
   - Typed record repositories, import transactions and indexed query execution are not implemented yet.

### CI / verification

- Latest run for head `d762f24e`: [43829628775](https://github.com/lewisxatk/FM-Atlas/actions/runs/43829628775). It was queued at checkpoint time.
- Recent workflow runs for commits `f3094878` and `8f67e909` failed with a build job that had `steps=null`, no logs and no runner allocation. These failures did not reach checkout/restore/build; they are not compiler diagnostics.
- Local execution environment has no .NET SDK, so no local WPF/SQLite compile or test has been claimed.
- Last verified Windows build remains commit `fbe36400`, run [43788503042](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788503042). It predates the new code above and is not the latest package.

### Current limitations

- No FM27 process memory has been read.
- No career save has been imported into the new application.
- No player/staff/club record has been supplied by a provider.
- No live writes have been attempted or enabled.
- Graphics index has only synthetic tests pending Windows CI and a real user folder check.
- SQLite schema exists, but persistence repositories and migration/restore UI do not.
- Advanced Search panels switch and render definitions, but filtering does not execute.
- Player profile remains a shell; it is not yet connected to the record models or graphics locator.

### Next exact actions

1. Check Windows Actions run 43829628775; if no runner was allocated, retry once and record the exact job state. Do not label code build-verified without a completed runner.
2. Fix actual compile/test failures if logs are produced.
3. Add repositories and transaction-based persistence for PlayerRecord, StaffRecord and ClubRecord, preserving field provenance.
4. Add graphics index cache and connect the locator to the left player identity rail through a real player record.
5. Continue offline save/import research and build a read-only record provider before any live-memory read is enabled.
6. Keep live write service disabled until exact Game Pass build and field mappings are verified on a disposable career.

### Uncommitted changes

None known. All code and research above are committed. Re-read the branch head and this checkpoint before the next work unit.


---

## Checkpoint 011 — versioned typed record snapshot persistence

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Head before this checkpoint update:** `aae269ec2cb6dc96a645183684ec8d8d3ffb3fc9`  
**Status:** Code committed. Windows Actions continues to fail before allocating a runner; no .NET build or xUnit result is available for these latest commits.

### Work saved

1. **SQLite schema upgraded from version 1 to version 2**:
   - Added `entity_record_snapshots`.
   - Existing version 1 databases have an explicit migration path to version 2.
   - Migration history and PRAGMA user_version are updated transactionally.
   - Existing schema versions newer than the application are rejected rather than downgraded.

2. **Typed entity record contract**:
   - Added `IEntityRecord`, implemented by PlayerRecord, StaffRecord and ClubRecord.
   - Snapshot store obtains its key from the record's own EntityIdentity; callers cannot accidentally supply a mismatched entity ID separately.

3. **SQLite typed snapshot store**:
   - Added `IEntityRecordSnapshotStore` and `SqliteEntityRecordSnapshotStore`.
   - Serialises the typed record as versioned JSON.
   - Preserves sourced values, dictionaries and per-field provenance.
   - Uses entity kind + provider key + source ID + record type as the key.
   - Saves entity row and snapshot in one transaction.
   - Upserts display name without erasing an existing name when a later snapshot supplies null.
   - Supports typed load and snapshot deletion without deleting the underlying entity.

4. **Tests added/updated**:
   - Schema version 2 and table creation.
   - Idempotent migration history.
   - Upgrade from an existing version 1 schema.
   - Foreign key setting.
   - Player snapshot round-trip and provenance preservation.
   - Game Pass/Steam provider ID collision isolation.
   - Staff and club snapshot round-trips.
   - Snapshot deletion and invalid identity rejection.

### Verification

- The schema DDL and foreign-key relationships were smoke-checked independently with the local SQLite engine. This is an SQL-level check, not a .NET compile or test run.
- Latest relevant Windows Actions runs continue to fail with job `steps=null` and no runner/log allocation. This does not establish a C# build failure, but it also means the new C# code remains unverified.
- Last verified Windows build remains the earlier fallback artifact from commit `fbe36400` / run [43788503042](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788503042). It predates these changes.

### Remaining limitations

- The snapshot store is lossless persistence only; it does not yet populate the normalised `field_values` projection.
- There is no Player/Staff/Club query repository or executable Advanced Search provider yet.
- No user career has been imported and no live process memory has been read.
- Graphics indexing has not been tested against the user's actual graphics directory.
- No live writes are implemented or enabled.

### Next exact actions

1. Resolve the GitHub Actions runner allocation issue and run restore/build/tests on Windows; fix only errors produced by a real compiler/test run.
2. Add typed record-to-field projection/import in the same database transaction as snapshot persistence.
3. Add indexed player/staff/club repositories for Quick Search and Advanced Search.
4. Connect player profile tabs to loaded PlayerRecord values and connect the graphics locator to the portrait slot.
5. Continue read-only Game Pass fingerprint and career identity research. Keep write operations disabled.

### Preservation

- All source and documentation changes are committed; no uncommitted changes are known.
- Re-read this checkpoint, the master checklist and branch head after any interruption.


---

## Checkpoint 012 — record projection and Quick Search provider

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Head before this documentation update:** `e63dc6ee62063c2046ab5f7f28d13c905b9d1d81`  
**Status:** Code committed. Latest Windows Actions push and pull-request runs again failed before runner allocation; no compile/test logs are available.

### Work saved since checkpoint 011

1. **Typed field projection**
   - Added EntityFieldProjection and EntityRecordProjection.
   - Added IEntityRecordProjector and EntityRecordProjector.
   - Projects PlayerRecord, StaffRecord and ClubRecord into normalised field values.
   - Player visible attributes, hidden attributes, personality, foot ratings, position ratings, preferred moves and season statistics are kept in distinct domain collections.
   - Canonical field keys are normalised to trimmed lowercase identifiers.
   - Missing values are omitted; values are not fabricated.
   - Wrong entity-kind/record combinations are rejected.

2. **Atomic snapshot + searchable projection**
   - SqliteEntityRecordSnapshotStore now writes the typed JSON snapshot and replaces field_values inside the same SQLite transaction.
   - Every indexed field stores value kind, source kind, evidence status, source version, target build fingerprint and observation timestamp.
   - Snapshot deletion now removes the associated field projection when no snapshots remain, while leaving the entity identity row intact.
   - Tests added for snapshot round-trip, provenance projection, hidden/personality/foot groups, stale projection cleanup and record type handling.

3. **Quick Search provider**
   - Added EntitySearchResult and IEntitySearchProvider.
   - Added SqliteEntitySearchProvider over the local entities index.
   - Searches Player, Staff/Coach and Club records.
   - Supports entity-kind filtering, case-insensitive substring search and a bounded result limit (1–200).
   - Escapes LIKE wildcard characters so search text is treated literally.
   - Search provider is a read-only service; the WPF top-bar UI is not wired to it yet.
   - Tests added for multiple entity types, entity filters, wildcard escaping, empty query and invalid limits.

### Verification state

- SQLite schema, foreign-key shape and LIKE escaping were smoke-checked independently with Python's SQLite engine. This does not compile or test the C# application.
- GitHub Actions run [43830612763](https://github.com/lewisxatk/FM-Atlas/actions/runs/43830612763) and later run [43830474897](https://github.com/lewisxatk/FM-Atlas/actions/runs/43830474897) failed with a build job showing `steps=null` and `logs_url=null`. The runner did not reach checkout/restore/build.
- Latest code therefore remains unverified by .NET/WPF compiler and xUnit.
- Last verified Windows artifact remains from commit `fbe36400`, run [43788503042](https://github.com/lewisxatk/FM-Atlas/actions/runs/43788503042). It predates all changes in checkpoints 010–012.

### Current functionality boundaries

- Snapshot persistence and field projection now exist.
- Quick Search has a local query provider but the top-bar UI is not connected.
- Advanced Search UI is schema-driven, but no executable Advanced Search query provider exists yet; controls remain disabled.
- No real FM career data has been imported.
- No actual installed graphics folder has been indexed.
- No FM process memory has been read.
- No live writes have been attempted or enabled.
- Player profile still needs record, image and tab navigation binding.

### Next exact actions

1. Restore Windows runner allocation and run the full current solution/tests. Do not mark code verified until the job gets real steps and logs.
2. Connect Quick Search UI to IEntitySearchProvider with entity type labels and profile navigation.
3. Implement the Advanced Search SQLite query executor with a tested supported-field subset; keep all unsupported fields In Progress.
4. Connect PlayerRecord loading to REF-004 profile tabs and graphics locator.
5. Add read-only career/save import provider and verify records against the actual FM27 Game Pass build.


---

## Checkpoint 013 — local Atlas save and live-save action placeholders

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Code commit:** `8b614737ce59ee9b1d834f0c478d32217942b81f`

### Work saved

- The desktop app now constructs the local SQLite database under the current user's LocalApplicationData `FM Atlas/atlas.db` folder.
- Added a working **Save Atlas** command. It creates/initialises the local database, verifies the schema version and reports the resulting local path.
- Separated process detection from live-career loading in the header: the active process check is labelled **Detect Game**.
- Added visible **Load Live Save** and **Save Live Save** actions, intentionally disabled and labelled In Progress until career identity, read mapping and guarded write verification exist.
- The existing local database save is distinct from saving the user's FM career. Save Atlas does not modify the live save.

### Verification

- Desktop composition references the existing Infrastructure project, which contains AtlasDatabase.
- The UI and view-model changes are committed.
- Latest Windows Actions run for the UI commit: [43830820075](https://github.com/lewisxatk/FM-Atlas/actions/runs/43830820075). It was queued at the time of this checkpoint; no successful build/test result is claimed.
- Latest successful Windows build remains the earlier fallback artifact from `fbe36400`; it predates the fresh-foundation code.

### Next actions

1. Verify the queued Windows build and fix any real compile/XAML/test errors.
2. Implement the read-only FM27 Game Pass career/save identity provider.
3. Enable Load Live Save only after a real save can be identified and imported without modifying it.
4. Connect the imported PlayerRecord to the profile, including CA, PA, all visible attributes, hidden/personality groups and portrait lookup.
5. Implement Save Live Save only after the write adapter has build-specific field mappings, preflight checks, read-back and rollback tests.
6. Wire Quick Search to the new SQLite provider; keep Advanced Search controls In Progress until validated query execution exists.

### Explicit safety boundary

No live career file has been changed. No process memory has been read or written. The only new save operation here is the local Atlas database initialisation.


---

## Checkpoint 014 — schema test correction and CI retry

**Date:** 2026-11-16 UTC

- Reviewed the schema v2 migration tests and found one stale assertion expecting schema version 1 after idempotent initialisation.
- Corrected it to assert `AtlasDatabase.SchemaVersion` (currently version 2). The migration-history assertion already expects exactly two migrations.
- Latest test-fix commit: `550a3f9cd52ba1666a1e022ced5446e163e8ca85`.
- Latest Windows Actions run: [43830954622](https://github.com/lewisxatk/FM-Atlas/actions/runs/43830954622). It again failed before runner allocation with `steps=null` and `logs_url=null`; no compiler or test output was produced.
- A retry of run [43830873991](https://github.com/lewisxatk/FM-Atlas/actions/runs/43830873991) also failed before runner allocation. This is an Actions execution issue, not a diagnosed source error.
- Local machine/tool environment has no .NET SDK, so the correction is committed but not yet compiled or executed.

**Next:** regain a functioning Windows runner, then execute restore/build/test on the full latest branch. Do not package the unverified code as a tested build.
