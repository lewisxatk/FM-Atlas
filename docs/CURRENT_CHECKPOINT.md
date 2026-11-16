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

---

## Checkpoint 015 — FMRTE-style Advanced Search expansion and newgen evidence

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Code head before this checkpoint:** `3f4723a83480cc17a70c64583b6b5b8ec82a8f9f`  
**Status:** Search features and tests committed in source. Windows build/test still unverified because GitHub Actions jobs terminate before runner allocation and provide no steps or logs.

### Work saved

1. **Advanced Search query model**
   - Expanded the domain catalogue for Player, Coach and Club entities.
   - Independent numeric MIN and MAX boundaries; either may be blank.
   - Attribute and personality fields have declared 0–20 search bounds; CA/PA use 0–200; reputation has its own 0–10,000 scale.
   - Position search has individual ratings for the 15 documented position codes and a separate All chosen / Any chosen rule, independent of the overall All / Any criteria.
   - Boolean criteria support Yes, No and Unknown.
   - Text, enum, entity lookup, date range, preferred-move and derived PA-minus-CA criteria have distinct query handling.
   - Unknown or In Progress fields are rejected by validation.

2. **Cross-category field finder**
   - Added a live field finder above the criteria panel.
   - It searches display names and domain keys across all categories for the currently selected entity.
   - Results are grouped by category; field values remain in the search model while the user moves between categories.
   - Selecting a category exits field-finder mode so category navigation remains predictable.

3. **Newgen / original-database classification**
   - Added nullable `IsNewgen` values to player and staff records and indexed query support.
   - Explicit states: generated/newgen (`true`), source-confirmed original database record (`false`), and unknown/unclassified (no evidence).
   - Boolean search exposes all three states; contextual text explains the distinction.
   - No classification is inferred from age, PA, name or face/portrait.
   - The current live importer does not yet populate this field. It will remain Unknown until an authoritative FM field or a correctly baselined, complete-snapshot method is implemented and verified.

4. **Typed record and field projection**
   - Expanded player/staff/club records with typed fields and typed extension dictionaries for integer, decimal, Boolean, date and text values.
   - Projector preserves per-field provenance and normalises keys.
   - SQLite snapshot storage persists typed field values and stores Boolean values as integer 0/1.
   - Newgen, UID, gender, geography, staff role preferences and club facility/finance criteria have projection slots.

5. **Reusable search presets**
   - Added versioned `AdvancedSearchPreset` domain model.
   - Save/Load Preset serialises named criteria to JSON under the user's LocalApplicationData `FM Atlas/Search Presets` directory.
   - Presets include entity, global All/Any logic, position All/Any logic and typed criteria.
   - Loading validates the preset against the current supported catalogue before applying it.
   - Tests added for JSON round-tripping.

6. **Research record**
   - Added `research/ADVANCED_SEARCH_COVERAGE_002.md` with the current field coverage, FMRTE behaviour, scale distinctions, newgen evidence requirements and explicit gaps.

### Tests added but not yet executed

- Catalogue tests for all 15 positions, 0–20 player attribute/foot bounds, staff attributes/role preferences and expanded club criteria.
- Query validation tests for one-sided min/max, out-of-range values, Boolean Unknown and saved preset roundtrip.
- SQLite provider tests for newgen true/false/unknown, max-only attribute filters, position familiarity, club facilities, staff role preferences, PA-minus-CA and independent position matching.
- A provider audit test iterates every field marked READY and executes a representative query against the SQLite provider. This tests the executable SQL path, but cannot establish that a real FM27 importer supplies the value.

### Verification gate and current blocker

- Latest observed workflow for this branch: [43840213199](https://github.com/lewisxatk/FM-Atlas/actions/runs/43840213199), commit `0f031ed838c77f5410ab41fdd9ea6d4936457003`.
- The job reports `conclusion=failure`, `runner_id=0`, `steps=[]`, and `logs_url=null`. Downloading logs returns 404 because no log object was created.
- This is not evidence of a C# or XAML compiler error; it is also not a passing build. No current build artifact is available.
- A final XAML audit caught and corrected a WPF Border that had two direct visual children; the contents are now wrapped in a StackPanel.
- No .NET compiler or SDK is available in this tool environment. Source tests are committed, not executed.

### Current functionality boundaries

- Advanced Search is wired to a read-only SQLite provider and can query already-imported Atlas records for supported fields.
- Quick Search also queries the local imported-record index.
- No live FM27 career/world data has yet been imported by the fresh shell; therefore search cannot yet find the actual save's players until an importer populates the local database.
- Newgen filter semantics and query execution exist, but live newgen status is not yet populated.
- Relations now has a read-only query path through the normalized `entity_relationships` table and a provenance-backed SQLite relationship store. It can match relationship type or related entity name in either direction; the live FM27 importer still needs to populate it.
- Relationship storage uses provider-scoped endpoint identities and persists source/evidence metadata. The provider test covers both endpoint directions and related-name lookup.
- Recommended CA remains In Progress until the actual FMRTE-compatible ability-weight calculation is reverse-engineered and verified.
- Result rows still show basic record identity; a full player-profile handoff is not finished.
- Load Live Save and Save Live Save remain disabled. Detect Game remains a process/module probe, not a career reader.
- No process memory has been read or written; no live career has been modified.

### Next actions

1. Restore a functioning Windows Actions runner and run restore/build/test on this exact source head.
2. Resolve any compile, XAML or test failures exposed by a real runner.
3. Complete the read-only FM27/Game Pass importer and populate the typed fields with field-level provenance.
4. Verify newgen detection against an authoritative FM field or a complete-baseline snapshot delta; keep unknown states explicit.
5. Connect result selection to the full player profile and editor tabs.
6. Expand the original FM Atlas visual system after the search and import foundations are running.
7. Only after a real save has been read and field mappings verified, enable live-save actions. Save Live Save remains a separate, gated write operation.

**Preservation:** Historical recovery source remains untouched. All work in this checkpoint belongs to the fresh rebuild branch.


---

## Checkpoint 016 — Indexed search expansion and read-only player profile handoff

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Starting code head:** `25ff168bb43a0b0855b912ad8d8c4778bf73b03b`  
**Status:** Code and source regression tests committed. Windows restore/build/test remains unverified because Actions jobs terminate before runner steps/logs become available.

### Work recovered and preserved

- Re-fetched the current branch head and the latest committed source before continuing after the interrupted response.
- Preserved all intervening work on the branch, including search presets, typed filters, relationship persistence/search and independent All/Any position grouping.
- Did not alter the historical recovery branch or merge the draft PR.

### Advanced Search coverage and behaviour

- Search remains read-only against the local SQLite index.
- Added/retained the Player, Coach and Club entity contexts, field finder across categories, All/Any matching, separate position All/Any matching, numeric/date MIN/MAX controls, Boolean Any/Yes/No/Unknown, text/enum, trait and position-rating controls.
- Numeric SQL predicates use inclusive `>=` and `<=`. A MIN-only condition (e.g. Dribbling minimum 12) has no upper limit; a MAX-only condition has no lower limit.
- Static catalogue covers the documented player attributes, hidden attributes, separate personality values, left/right foot, positions, CA/PA, reputation, personal/contract fields, staff fields and club facilities/finances. Per-field bounds are validated, including 0–20 ratings and distinct CA/PA/reputation scales.
- The Newgen filter has three explicit states: generated/newgen, source-confirmed original database person, and unknown. Age/PA/name/face are not used as substitutes for a source-confirmed generated-person flag.
- Indexed Data discovers consistently typed fields actually present in local `field_values`, including integer, decimal, text, date and Boolean fields. It excludes a key where stored values use mixed types. This extends search to imported statistics without inventing mappings.
- Search preset JSON includes entity, conditions and matching logic; Clear Filters clears the selected entity's stored criteria and results.
- Relations has a read-only, bidirectional query path over the normalized relationship table. Live FM relationship ingestion is not yet connected.
- Recommended CA remains In Progress until an evidenced formula/field mapping has been independently verified.

### Player profile handoff

- Advanced Search result rows retain the complete provider-scoped entity identity.
- Double-clicking a Player result asks the typed snapshot store for the matching `PlayerRecord`.
- If the snapshot is available, a separate read-only profile window opens; a window for the same provider-scoped identity is activated rather than duplicated.
- If the entity row exists but the typed player snapshot is missing, the app presents an explicit message instead of inventing a profile.
- The profile binds snapshot-backed identity, contract summary, CA/PA with evidence tooltips, attribute groups, position ratings, preferred moves, season statistics and Newgen classification/evidence.
- Coach and Club profile windows are not connected. Profile section-specific screens/tabs, WPF portrait binding and editable player controls remain incomplete.
- Live profile data depends on a future verified importer. The profile does not read live process memory.

### Startup and database

- Removed the conflicting WPF `StartupUri`; the app has one dependency-injected `OnStartup` path.
- Local Atlas database initialisation/migrations are performed before the read-only search providers are used.
- Startup migration errors are reported with a message and non-zero shutdown rather than failing silently.

### Verification and blocker

- Source regression tests now cover one-sided ranges, bounds, Boolean newgen states, position logic, club/staff mappings, relationships, preset roundtrip, discovered indexed season statistics and every field marked READY.
- These tests are committed but **not yet executed** in this session; the local environment has no .NET SDK.
- Latest observed Windows workflow on the starting head: [43842585096](https://github.com/lewisxatk/FM-Atlas/actions/runs/43842585096). It failed within seconds; its job has `steps=null` and no downloadable logs (`BlobNotFound`). This is not a diagnosed C#/XAML error, but it is not a passing build either.
- No current Windows build artifact is verified or available from these runs.

### Explicit remaining work / safety boundary

1. Restore a functioning Windows Actions runner or run the documented .NET 8 restore/build/test commands on the user's Windows machine and resolve any real compiler/test errors.
2. Add a verified FM27 Game Pass read-only save/world-data importer to populate player/staff/club typed records, dynamic field index, relations and authoritative newgen status.
3. Validate every field against FM27 UI and an independent tool for the exact installed package/build; final player attribute display order still needs visual confirmation.
4. Finish player profile section-specific navigation, portrait binding and Coach/Club profiles.
5. Only after real build and field-level verification, implement guarded live writing, read-back, rollback and in-game persistence. Load Live Save and Save Live Save remain disabled.
6. The Wyscout/StatsBomb scouting expansion remains deferred until the basic importer, search and profile are usable.

**Files with the main source changes:** `IIndexedAdvancedSearchFieldProvider.cs`, `AdvancedSearchCatalog.cs`, `AdvancedSearchQuery.cs`, `SqliteAdvancedSearchProvider.cs`, `AdvancedSearchWindowViewModel.cs`, `AdvancedSearchWindow.xaml(.cs)`, `PlayerProfileViewModel.cs`, `PlayerProfileView.xaml`, `MainWindow.xaml.cs`, `App.xaml(.cs)`, and related regression tests.

**Current status:** Changes are committed; no uncommitted changes are known. Do not describe this branch as a verified release until Windows restore/build/test passes.


### Post-checkpoint verification update

- The documentation/checklist state was synced at commit `89f30a4bad73429b81a3310d688851e73a3f6c84`.
- Windows workflow [43843064600](https://github.com/lewisxatk/FM-Atlas/actions/runs/43843064600) on that head completed with failure. Job `133216289077` returned `steps=null`, no log URL, and the job-log download returned 404 (`BlobNotFound`).
- This repeats the earlier runner-start failure pattern; it does not identify a compile error and does not verify a successful build.
- No current Windows artifact was created.


---

## Update — global women-player search visibility and CI runner triage (2026-11-16 10:05 UTC)

**Branch:** `rebuild/fresh-foundation-001`  
**Latest committed head:** `46ffc47057336684ae1c2c32bffbf00ae6002587`  
**Status:** Global women-player visibility has been wired through Quick Search and Advanced Search, with SQLite regression tests committed. Windows CI remains unverified because GitHub-hosted jobs fail before any runner step or log is created.

### Implemented and committed

- Added a global **Include women** checkbox to the main application header.
- Preference defaults OFF and is persisted locally at `%LOCALAPPDATA%\FM Atlas\Settings\include-women-players.txt`.
- Quick Search receives the shared preference and excludes records explicitly identified as female when OFF.
- Advanced Search receives the same live preference through the main view-model callback; the setting therefore applies across all Advanced Search categories and entities without adding a second conflicting toggle.
- Both SQLite query paths exclude explicit female gender text values (`female`, `woman`, `women`, `f`) for Player records when OFF.
- Unknown/missing gender is retained rather than guessed or silently classified.
- The existing typed `PlayerRecord.Gender` value is already projected into the normalised `field_values.gender` index; no new unverified data field was invented.
- Added regression tests for Quick Search and Advanced Search with male, female and unknown-gender records, including the OFF/ON behaviour.

### Advanced Search requirements rechecked

- The field finder searches the documented field catalogue across categories.
- Player attribute catalogue includes 52 unique non-foot standard/hidden attributes, eight separate personality ratings, two separate foot ratings and 15 position ratings.
- Numeric conditions support minimum-only and maximum-only boundaries. Existing integration test `AttributeSearchSupportsMaximumOnlyBoundary` verifies the maximum-only case.
- Newgen/original database classification already uses `is_newgen` with True / False / Unknown semantics and existing integration coverage.
- These are local-index search capabilities. A field still needs an imported, correctly typed value to produce results; no live FM importer has been claimed.
- Wyscout/StatsBomb analytics remain deferred. Unimplemented UI actions remain disabled or labelled In Progress.

### Windows CI failure — not yet a source-code failure

- Workflow: **FM Atlas Windows Build**, run #383, ID `43845648971`, commit `46ffc47057336684ae1c2c32bffbf00ae6002587`.
- Job: `build`; latest rerun job ID `133225118714`.
- Conclusion: failure; zero steps; no runner log URL. The earlier run #177 showed the same pattern.
- A failed-job rerun was requested successfully, but the rerun again produced no steps/logs. There is no compiler or test output to diagnose, so this cannot honestly be reported as a C# or XAML build failure.
- The workflow file still targets `windows-latest` and runs .NET 8 restore, Release build, tests and artifact upload. The current symptom points to job creation/hosted-runner provisioning or an account-level Actions usage/budget restriction, not a demonstrated workflow command failure.
- Next verification: inspect the repository owner's GitHub Actions usage and hard budgets, plus repository Actions permissions; then rerun Windows CI and review actual Restore / Build / Test steps. Do not mark the new code verified until this completes.

### Files changed in this update

- `src/FMAtlas.Application/Abstractions/IEntitySearchProvider.cs`
- `src/FMAtlas.Infrastructure/Persistence/SqliteEntitySearchProvider.cs`
- `src/FMAtlas.Desktop/ViewModels/MainWindowViewModel.cs`
- `src/FMAtlas.Desktop/MainWindow.xaml`
- `src/FMAtlas.Desktop/MainWindow.xaml.cs`
- `src/FMAtlas.Desktop/Views/AdvancedSearchWindow.xaml.cs`
- `src/FMAtlas.Desktop/ViewModels/AdvancedSearchWindowViewModel.cs`
- `src/FMAtlas.Domain/Search/AdvancedSearchQuery.cs`
- `src/FMAtlas.Infrastructure/Persistence/SqliteAdvancedSearchProvider.cs`
- `tests/FMAtlas.Tests/SqliteEntitySearchProviderTests.cs`
- `tests/FMAtlas.Tests/SqliteAdvancedSearchProviderTests.cs`

**Uncommitted work:** None known.  
**Next single action:** Resolve the GitHub Actions runner/budget block and obtain a real Windows restore/build/test result for the current branch head.


---

## Checkpoint 017 — FM27 player CSV import path for first hands-on testing

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Code update:** FM View Exporter-compatible CSV import, additional typed field indexing and desktop **Import CSV** action.  
**Latest code commit before checkpoint sync:** `0a14502e9fab338306c554bfe6cb754d0b349d93`.  
**Documentation/checklist updates:** `172a5fa824f6d10aaaa9aad4fd913819eb95a98b`, `3a2cc82006d3b915a7954b24e1e1fe347f3e9a61`, and current checklist sync `037384ab96e94c2aca3c337deed00157724a9af2`.  
**Status:** Source changes and regression tests are committed. Windows build/test and the actual WPF launch remain unverified.

### What was added

1. `IPlayerExportImporter` application contract and `PlayerExportImportResult` report model.
2. Independent `FMViewExporterCsvImporter` implementation in Infrastructure.
3. Main header **Import CSV** button and file picker, startup registration and progress/result dialog.
4. ViewModel refresh notification so Quick Search notices the imported records.
5. Local SQLite typed snapshot and field projection reuse; no schema migration was needed for the generic typed extra-field dictionaries.
6. Dynamic additional export columns are retained as `export_*` typed fields and exposed to Advanced Search through the existing Indexed Fields discovery.
7. CSV import tests for fixed mappings, attribute groups, duplicate `Nation` headers, typed dynamic fields, Advanced Search execution, semicolon/German decimal-comma format, duplicate UIDs, reimport idempotence and malformed/missing identity input.
8. `research/CSV_IMPORT_001.md` and README first-test instructions.

### Import format and rules

- UTF-8 CSV required.
- Must have player `Player` or `Name` header and stable `Unique ID` header.
- Accepts comma or semicolon delimiter; semicolon mode prefers decimal-comma parsing.
- File limit 64 MB; up to 100,000 player rows and 512 columns.
- Uses Unique ID + SHA-256-derived source-file namespace. Exact same file reimport updates existing snapshots in that namespace. A changed file has a separate namespace, preventing silent overwrite of the earlier snapshot.
- Duplicate UIDs inside one file are skipped and reported.
- Maps explicit identity, personal, club, CA/PA, international, position/foot and attribute values when matching columns exist.
- Maps player attribute fields only when values are valid 0–20 whole numbers. Missing values stay missing.
- CA/PA are mapped only from explicit `CA`/`Current Ability` and `PA`/`Potential Ability` headers. Scout-star `Ability`/`Potential` values are not substituted for CA/PA.
- The two repeated `Nation` headers in the export reference are handled by selecting the final Nation column as the full nationality name; repeated headers are not collapsed into the same dynamic key.
- Extra columns are classified only when all populated values fit one type: Boolean, Integer, Decimal, Date. Mixed/ambiguous columns stay text.
- Each imported field is tagged `GameExport / Mapped`. This does not prove live-memory reads or field verification.
- No FM process memory is read or written and no career file is modified.

### External compatibility caveat

The public FM View Exporter documentation and source notes describe FM27 on Steam for Windows as the current supported target. They do not establish Game Pass/Microsoft Store plugin compatibility. FM Atlas imports a compatible CSV file regardless of the export route, but this importer does not make the exporter itself Game Pass compatible.

### Tests / verification

- Added `tests/FMAtlas.Tests/FMViewExporterCsvImporterTests.cs`.
- Tests are source-committed but not yet executed. The local environment has no .NET SDK.
- Latest Windows Actions run observed after the code update: run #425, ID `43852668416`, on the README/documented branch head. It failed with a job that has `steps=null`, no log URL and no downloadable log object. A failed-job retry was accepted but remained queued with no steps/logs at the latest check. There is no compiler/test output to diagnose, so this is not a known source-code failure and is not a passing build.
- No new Windows artifact was produced by these failed/no-step runs.

### Current test route for user

Once a compatible CSV is available:
1. Start FM Atlas on Windows.
2. Select **Import CSV** in the header.
3. Choose the FM27 player-list CSV.
4. Search for a player in Quick Search or Advanced Search.
5. In Advanced Search, use Indexed Fields to find additional export columns; double-click a player result to open the local read-only Player profile.

The actual output will depend on the columns included in the selected export. No demo or fabricated FM player records are shipped.

### Still outstanding

1. Get a functioning Windows GitHub Actions runner and verify Restore / Build / Test against this exact branch head.
2. Fix any actual compiler/XAML/test failure the runner reveals.
3. Build and inspect a fresh Windows x64 artifact before asking the user to install this update.
4. User tests their own CSV to expose any localized header naming we have not yet mapped.
5. Continue Game Pass-specific read-only process/session identification and live player-data importing as a separate milestone.
6. Keep `Load Live Save` and `Save Live Save` disabled until build-specific read/write, save-generation, read-back and disposable-career verification gates pass.

**Preservation:** The work remains on `rebuild/fresh-foundation-001`. No changes to `main` or the historical R7.2.0 recovery branch.


---

## Checkpoint 018 — CSV importer hardening and local Windows test/package handoff

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Latest source-hardening commit:** `1e74762531c2d74ad7b576c466b0ead754a3e25c`  
**Latest continuity/checklist sync:** `4872b9d7ce0bfa4b88af29ec9d20b59106d67f2b`  
**Status:** CSV import, UI wiring, regression source tests and local Windows build handoff are committed. None of the new C# changes have been compiled/tested in this tool runtime or a verified Windows runner yet.

### Additional hardening after checkpoint 017

1. Changed the importer to create a fresh `FieldProvenance` timestamp for each import execution, rather than using a class-load-time timestamp.
2. Added an end-to-end regression test proving that an explicitly exported `Gender` value is projected to `field_values.gender` and then obeys the global **Include women** setting in Quick Search (OFF excludes explicit female; ON includes her).
3. Added `scripts/Build-Test-Package.ps1` for local Windows validation:
   - requires Windows 10/11 and a .NET 8 SDK;
   - restores the solution;
   - builds the Release solution;
   - runs the full test suite;
   - publishes the Windows x64 desktop app;
   - checks that `FMAtlas.exe` exists;
   - creates a timestamped ZIP only if every prior step passes.
4. Added README instructions for running the script from the repository root:
   `powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1`.
5. The script does not delete previous artifacts. New output is placed in a uniquely timestamped `artifacts` subfolder.

### Source/test files added or changed

- `src/FMAtlas.Application/Abstractions/IPlayerExportImporter.cs`
- `src/FMAtlas.Application/Models/PlayerExportImportResult.cs`
- `src/FMAtlas.Infrastructure/Import/FMViewExporterCsvImporter.cs`
- `src/FMAtlas.Desktop/MainWindow.xaml`
- `src/FMAtlas.Desktop/MainWindow.xaml.cs`
- `src/FMAtlas.Desktop/App.xaml.cs`
- `src/FMAtlas.Desktop/ViewModels/MainWindowViewModel.cs`
- `tests/FMAtlas.Tests/FMViewExporterCsvImporterTests.cs`
- `scripts/Build-Test-Package.ps1`
- `research/CSV_IMPORT_001.md`
- README, master plan, checklist and continuity checkpoint updates.

The CSV importer test file now contains seven test cases. They cover fixed player mappings, attributes/provenance, duplicated Nation headers, dynamic typed columns and Advanced Search, gender toggle integration, German decimal-comma parsing, duplicate IDs/idempotence, missing Unique ID and malformed CSV. (The full count is seven test methods; the first method includes both text and numeric additional-column searches.)

### Verification status

- Local tool container check: `dotnet` is not installed, so no local restore/build/test can run here.
- Last retrievable GitHub Actions run snapshot: **FM Atlas Windows Build**, run #425, ID `43852668416`. The job ended in failure without steps or a log URL. A retry was accepted; that retry also ended with no steps and no downloadable log object.
- Commit-specific status queries after that run returned no status checks, and the Actions run helper only returns pull-request-triggered runs. Therefore there is no confirmed Windows CI result for the later push commits.
- No new test ZIP was created. Do not install or describe this source head as test-ready until the local script or Windows CI passes.

### First test handoff

On Windows, update the checkout to the latest `rebuild/fresh-foundation-001` branch head and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1
```

Only when the script reports that restore, build and tests passed should the timestamped ZIP under `artifacts` be used for the first app test.

When a compatible UTF-8 FM27 player CSV is available:
1. Open FM Atlas.
2. Select **Import CSV**.
3. Choose a CSV containing `Player` or `Name` and `Unique ID`.
4. Find imported players with Quick Search / Advanced Search.
5. Use **Indexed Fields** in Advanced Search to query extra columns.
6. Double-click a Player search result to open its read-only typed snapshot profile.

**Game Pass caveat:** the currently documented FM View Exporter build targets Steam/Windows; Game Pass plugin support is not established. FM Atlas imports a compatible CSV from any exporter, but its importer does not make the exporter Game Pass-compatible. This CSV route is separate from the still-incomplete Game Pass live-save reader.

**Safety boundary unchanged:** no live process-memory read/write and no career-file modification. `Load Live Save` and `Save Live Save` remain disabled. Historical recovery and `main` remain untouched.


### Post-checkpoint Windows Actions verification — current head

- Checkpoint sync commit: `298e158b70705d9bc2756df8c1988b63ae2016ad`.
- Windows workflow run #451, ID `43853394674`, was created for that head and failed with job `build` reporting `steps=null` and no log URL.
- A failed-job retry was accepted. Retry job ID `133251606257` also completed with failure, returned zero steps and its job-log download returned 404/BlobNotFound.
- No Restore / Build / Test command ran, no compiler/test output exists, and no artifact was uploaded. This remains a hosted Actions provisioning/access blocker, not a diagnosed source compile failure and not a pass.
- The added local PowerShell script is therefore the available route to obtain an actionable Windows build/test result. The script itself has not been run in this environment because the .NET SDK and Windows PowerShell are unavailable.


### Checkpoint 019 — import usability and evidence warnings

**Latest source commit:** `233541fed9ab6a6a34c07bc7e5f178b00e96bfa8`.

- The Import CSV result now includes a clear warning when the chosen file has no explicit `Gender` column. It explains that unclassified player records stay visible under the Include women filter.
- The main-header Include women tooltip now describes the unknown-gender behaviour.
- Added test assertions that the missing-gender warning appears only when the CSV lacks gender, and that an explicit gender column feeds the global Quick Search Include women filter.
- Semicolon-delimited CSV decimal parsing now uses the separators present in each value: a comma-only value is parsed as decimal-comma, a dot-only value as decimal-dot, and mixed comma/dot values use the delimiter convention. A regression assertion covers a semicolon CSV containing both `181,5 cm` and `72.5`.
- The dashboard's player tile now reports the number of players imported in the **latest CSV** rather than remaining stuck on “No records imported”. It explicitly labels the metric as the latest import, not a total across all stored datasets.
- Zero-player imports display a “No players imported” status rather than a misleading plural success message.

**Verification:** These source/tests remain unexecuted. The local tool environment has no .NET SDK. The last observed hosted runner run #451 (ID `43853394674`) failed before any steps/logs and its retry failed the same way. Later push commits do not currently have a retrievable status result from the available connector response. No Windows test package exists yet.


---

## Checkpoint 020 — live-session lifecycle, interaction lock and write-safety groundwork

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Checkpoint base before this work:** `2d2287e37848fd71e64b0d62aa7a121ca6e49f48`  
**Source commits from this pass:** `193859ea7ccffdb12cb45f453ad7b0efb3609a66`, `1602bcc712a72a9036d48d5bbc1013eb7f91c52d`, `cee4ac85e9a92a6fbbf186b3832e624e0fbd5a10`, `7c421c4babd913b5e22a4f2764751d7eb12df946`, `eace4973c61fd968089b97c90e58fccc73a8ce8f`, `f216c3a3d32696711366f2815903605a2c0cb7d3`.  
**Status:** Read-only live connector exists for exact Game Pass 0.9.2435.0 reference. This pass hardens lifecycle and loading UX, adds a pure fail-closed write preflight policy and tests, and documents the write architecture. It does not enable live writes.

### Live-session safety changes

1. Every live load now clears the previous live provider before scanning. A failed/cancelled scan cannot leave the previous career appearing to be the current live dataset.
2. If the connector fails after it has persisted a candidate snapshot, it deletes that provider using a non-cancelled cleanup token before closing the process handle. Cleanup failure is surfaced alongside the original load failure.
3. The disconnect-request flag is volatile across the UI and background scan continuations.
4. The main view model clears live counts, live import dashboard labels and Quick Search results before loading and on failed/cancelled loads.
5. The main loading overlay already had a rotating spinner, progress bar, stage/detail text, hit-test blocking and keyboard focus. This pass additionally disables every owned top-level window while loading, including Advanced Search and Player Profile windows, and re-enables them after the overlay closes.

### Write-safety groundwork

1. Added `src/FMAtlas.Application/LiveEditing/LiveEditWritePreflight.cs`.
2. It requires connected session, unchanged process ID, exact build fingerprint, unchanged save-generation marker, verified target identity, per-field `LiveGameMemory / WriteVerified` evidence for the exact build, expected-old-value match, valid range, proposed value in range and safe writable memory region.
3. Added `tests/FMAtlas.Tests/LiveEditWritePreflightTests.cs` covering allowed context and fail-closed conditions.
4. Added `research/LIVE_EDIT_SAFETY_001.md` documenting external write-safety research and FM Atlas implementation boundaries.

### External reverse-engineering research

Re-opened FMLibreEditor 0.9.2435.0 recon, exact build profile, edit-field/write logic, session save-generation logic and Windows memory API. Findings: the external editor checks target identity, field range and plausible old value, pointer class/relationships, save-generation, safe writable region and read-back. It uses a sticky save-changed lock until a fresh recheck. Its GPL-3.0 source remains research-only; no code was copied.

### Current live-read coverage and limits

- The current connector imports Player, Staff and Club records only.
- It is not yet a complete model of every FM world entity/data family.
- The connector uses an externally documented 0.9.2435.0 layout and tags values `Mapped`; this is not FM Atlas field-by-field read verification on the user's machine.
- No field is marked `WriteVerified`; no Windows process write accessor or live write operation is enabled.
- `Save Atlas` saves the local Atlas database. `Save Live Changes` remains disabled and is not equivalent to saving the FM career.

### Verification

- Latest observed branch head during this checkpoint: `eace4973c61fd968089b97c90e58fccc73a8ce8f`.
- Windows Actions runs #563/#564 (IDs `43858378079` / `43858387390`) failed with no job steps (`steps=null`) and no log URL. No compiler or test output was produced.
- Local runtime has no .NET SDK and is not Windows. New C# changes are committed but not compiled/tested here.
- No real FM process read or write test has been performed in this pass.

### Next work

1. Run successful Windows restore/build/tests on exact branch head.
2. Build a disposable-career read-verification harness for player/staff/club fields and object relationships.
3. Add strict scan completeness diagnostics; do not report partial entity reads as a complete career load.
4. Implement a separate safe Windows write primitive and first scalar field only after read evidence is independently verified.
5. Add conflict detection, read-back, rollback and undo tests on a disposable career.
6. Expand data providers to all requested world-data families before claiming full coverage.


### Additional live-session monitor hardening

- Changed `CheckSessionAsync` to contain unexpected session/memory/database errors instead of allowing them to escape the WPF dispatcher timer.
- Invalidation now closes the process handle even if deleting the live provider fails, and the UI receives the cleanup failure rather than a false claim that stale rows were removed.
- All monitor-driven cleanup uses a non-cancelled token.
- Commit: `a6504c38bcb637f8c81b3dc9ee2e694c163e929d`.
- Research note updated: `research/LIVE_EDIT_SAFETY_001.md` (commit `128dc52296775904882801af529245a56e062e20`).


### Live reader source-integrity correction

- A source inspection of the exact branch head found that `FM27GamePass0935Reader.ReadAllRecords` had been truncated in the latest reader edit: the staff loop stopped at an incomplete `if (staff is not null)` and the remaining staff/club loops and return statement were missing.
- Restored the complete method body from the immediately preceding branch commit while preserving the newer `ReadPlayer` object-read length (`0x4C0`) needed for the player-coach shifted trailing fields.
- Re-opened the raw branch file and verified the method now runs from its declaration through the player/staff/club loops and `return records;` before `ReadPlayer` begins.
- Correction commit: `290408df77aa1b7f5d0bfe9239fc812aeadcf637`.
- Windows Actions runs #586/#587 for that exact commit still fail before creating job steps (`steps=null`, no log URL). The source correction is not yet compiler-verified.


### Live-save continuation — strict scan completeness and connector shutdown

**Date:** 2026-11-16 UTC

- [x] Audited the existing FM27 Game Pass 0.9.2435.0 reader and confirmed that it uses an external-reference-only profile and a ReadProcessMemory-only accessor.
- [x] Changed the player, staff and club scan loops from silent best-effort skipping to fail-closed behaviour: every pointer must produce a typed record with a valid identity or the whole scan aborts with entity kind and address.
- [x] Because persistence happens only after ReadAllRecords returns, an incomplete scan is not saved as a successful live snapshot.
- [x] Fixed connector disposal so an in-flight load/check can safely finish its semaphore release after the app requests disconnect.
- [x] Re-opened the independent FMLibreEditor 0.9.2435.0 recon and edit/session implementation. It confirms that a field offset alone is not enough: entity/class checks, pointer relationship checks, current-value plausibility, writable-region checks, optimistic concurrency, read-back and guarded undo are required.
- [ ] Current branch still needs a successful Windows restore/build/test and a real disposable-career comparison.
- [ ] No FM Atlas process-memory write API exists. Save Live Changes remains disabled; Save Atlas is local SQLite only.
- [ ] Only players, staff and clubs are presently live-indexed. Everything requires additional world-data providers and field coverage.

**Latest live-read hardening commits:** `895eda4955cefa522c15f77f2aeda6fef293991b`, `009407fc8d48a4135ee0590700b57aa54cbd1118`, and research update `c90f694702445b1bfc3e43bb3d71e06d07433f64`.

**Next critical task:** build a disposable-save field verification harness for the current read mappings, then implement a separate Windows writer with immediate preflight, read-back and undo. Do not enable writes based only on external offsets.


---

## Checkpoint 020 — FM27 Game Pass live connection, scan, indexing and loading lock

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Current phase:** Live save limitation is the priority.  
**Status:** Exact-build read-only scanner, session invalidation, local typed import and blocking animated loading experience are committed. Windows compile/runtime verification is still outstanding.

### What is now connected

1. **Load Game** invokes `WindowsFm27LiveGameConnector.ConnectAndImportAsync` rather than the earlier presence-only process probe.
2. Process discovery expects one FM process and a loaded `game_plugin.dll`. It checks the x64 PE fingerprint before opening the process.
3. The reader is explicitly restricted to Game Pass 0.9.2435.0:
   - machine `0x8664`;
   - PE timestamp `0x6AE8CF77`;
   - SizeOfImage `0x17969000`.
4. The connector opens a read-only process handle and scans the documented Player, Staff and Club pointer arrays.
5. Player, Staff and Club snapshots are built with typed field values and per-field provenance.
6. The whole live entity set and its search projection is saved in one SQLite transaction.
7. The current active save generation is verified before scanning, after scanning, after database import and every two seconds while connected. A process exit, save switch, empty player array or loss of a valid memory read closes the session and removes the transient live provider.
8. The transient live provider is cleared at app startup and before a fresh load; CSV providers are preserved.

### Live data fields now mapped

**Players:** UID/name/common name, DOB/age, nationality/secondary nationality, gender, city of birth, club/team/competition, position and height; CA/PA, transfer value, wage and contract expiry; 52 standard/hidden attributes, eight personality ratings, two foot ratings, 15 positions, 64 preferred moves; caps/goals and U21 caps/goals; home/current/world reputation, condition, match sharpness, jadedness and morale; asking price, squad status/number, transfer/loan-list flags, contract type, loyalty bonus, happiness and contract dates; Newgen flag; trained-in nation, trained-at club and work-permit nationality relationship search fields.

**Staff:** UID/name, DOB/age, nationality/gender, club and job role; CA/PA, wage, contract expiry/signed/joined dates and contract type; coaching/mental/medical/knowledge values, authority and working-with-youngsters; personality and home/current/world reputation.

**Clubs:** UID/name/short name, nation, competition, founded year, reputation, women's-club flag, training/youth/corporate facilities, stadium name/capacity/seating/used/expansion, supporter mix and traits, season ticket/social follower counts, balance, transfer budget, wage budget, total wage budget and average ticket price when a club's finance record is present and each value validates.

Fields that are absent or fail their validation are left unknown; they are not manufactured as zero. Additional typed values flow into the Indexed Fields category of Advanced Search.

### Loading experience

- A centered compact loading panel displays a rotating dashed ring, stage text, detail, progress bar and percentage.
- The overlay blocks the entire main workspace and owned Advanced Search/profile windows while the scan/index is in progress.
- The UI holds keyboard focus on the overlay; Quick Search popup refresh is deferred until the overlay has closed.
- Memory scanning runs on a background task and SQLite persistence uses asynchronous calls, so the loading animation can keep rendering.

### Critical build / edit safety

- The profile's evidence status remains **ExternalReferenceOnly**. A new `CanAttemptReadOnlyScan` property explicitly allows a scanner only where FM Atlas has code for that exact package. This is not the same as `CanReadLiveData` (still false) and never enables writes.
- `WindowsProcessMemoryAccessor` opens only `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` and exposes `ReadProcessMemory` only.
- **Save Live Changes remains disabled.** No live field is WriteVerified and no live process write has been performed. We are not claiming that editing/saving is finished.
- The current scanner will reject Game Pass 0.9.2409.0, Steam, unknown FM27 patches and any non-matching module fingerprint.

### Tests and verification

New source regression coverage includes:
- atomic Player/Staff/Club batch import into Quick and Advanced Search;
- provider-scoped live data deletion that preserves imported CSV datasets;
- all-or-nothing batch rollback for an invalid entity identity;
- preferred-move HasTrait query from batch-imported live records;
- dynamic club facility, stadium, women's-club and finance-field discovery;
- exact Game Pass profile read-only-probe permission distinct from Atlas-verified read/write permission.

These tests are committed but not executed. This environment does not have the .NET SDK or a Windows runtime. The recent GitHub-hosted Windows jobs have failed before runner allocation and produced no steps or log object. No current Windows build artifact has been produced. Do not call the scanner tested or the current branch release-ready until a real Windows restore/build/test and in-game comparison pass.

### Current remaining priorities

1. Run the full .NET 8 Windows restore/build/test script on the latest branch head and fix actual compiler/XAML/test errors.
2. First live Game Pass test on package 0.9.2435.0; compare counts, names, genders, attributes, CA/PA, contract dates and clubs with the FM UI.
3. Add remaining missing player/staff/club fields after their object structures are confirmed.
4. Mark the build AtlasReadVerified only after that real comparison.
5. Build a separate guarded editing writer only after each field has a tested offset, value bounds, save-generation check, pre-write read, post-write read-back and undo/rollback test. Keep Save Live Changes disabled until then.

Detailed findings: `research/LIVE_GAME_CONNECTION_001.md`.  
Checklist: `docs/FRESH_REBUILD_CHECKLIST.md`.  
Safety boundary: no live process writes and no modification of the user's career file have been performed.


---

## Checkpoint 021 — FMRTE profile workspaces and design review

**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Status:** UI/profile structure and design review committed. The Windows build and runtime verification remain outstanding.

### Main workspace and dashboard

- [x] Replace the previous single-screen-only profile flow with a closable in-app workspace TabControl.
- [x] Keep Dashboard as a fixed first tab; open Player, Staff, Club and directory workspaces in additional tabs.
- [x] Focus an already-open entity tab instead of opening duplicates.
- [x] Add Players, Staff and Clubs directory workspaces with type-scoped search and profile double-click routing.
- [x] Route Quick Search double-click results into the correct typed Player/Staff/Club profile.
- [x] Add a five-item Recently Opened profile strip on Dashboard; reopening an item returns to its existing workspace tab.
- [x] Replace the hard-coded “0 pending edits” metric with a truthful disabled live-write state.
- [x] Replace the internal Development Status panel with current career summary, live entity counts and actual workspace quick actions.
- [x] Keep the left navigation limited to product areas; no Search item was added.

### Advanced Search

- [x] Keep Advanced Search as a modal dialog in accordance with REF-002.
- [x] Use OK / Search and Cancel actions; Escape invokes Cancel.
- [x] Double-clicking a result opens the profile in the main workspace and closes Advanced Search.
- [x] Retain the existing Player/Coach/Club, category, typed-filter and AND/OR query model. Dynamic field availability still depends on typed source data.

### Player profile

- [x] Implement functional FMRTE-audited section switching for Information, Contract, Stats, Unavailability, Transfer Proposals, Retire Date, Positions, Preferred Moves, Relationships, Languages, Scout Information, Staff, Clone Center, Personality/Hidden and History.
- [x] Each section displays real mapped values and evidence labels, or a source-honest empty state.
- [x] Add canonical position ordering.
- [x] Add Technical / Goalkeeper attribute view toggle while retaining shared Mental, Physical, Hidden, Personality and Feet groups.
- [x] Display second nationality, birthplace, asking price and existing identity/contract values where supplied.
- [x] Add active orange state for selected profile tabs.

### Staff and Club profiles

- [x] Add Staff profile workspace and audited sections: Information, Contract, Career Plans, Promises, Swap Staff, Training Rating, Formations/Tendencies, Relationships, Knowledge, Retire Date and Languages.
- [x] Add Club profile workspace and audited sections: Information, Squads, Youth Intake, Finances, Club Vision, Possible Signings, Player Clauses, Transfer Center, Staff, Wage Contributions, Kits & Colours, Affiliations, Match Preparation and Stadium.
- [x] Keep unsupported sections visible with explicit empty states; do not invent club/player/staff data.
- [ ] Populate the full club-to-player roster and all missing club-related entity families. The current live reader maps an initial ClubRecord only.

### Graphics

- [x] Connect the existing read-only FM graphics config indexer to profile creation.
- [x] On startup, index the standard Documents/Sports Interactive/Football Manager 27/graphics folder when it exists.
- [x] Resolve player portraits by game UID (including regens with the supported `r-` prefix) and club badges by numeric club ID.
- [x] Keep neutral placeholders when no matching mapping exists; never modify the graphics folder.
- [ ] Add a Settings option for custom/non-standard graphics folders.

### Verification and remaining gates

- [x] Static tag-stack check across MainWindow, Player/Staff/Club profiles, EntityBrowser, AdvancedSearch and App XAML: all elements balanced after fixing malformed Staff/Club tab button markup.
- [ ] Run a real Windows .NET 8 Release build and full tests.
- [ ] Compare the running desktop UI against REF-001 / REF-002 / REF-003 screenshots at normal Windows scaling.
- [ ] Connect to the user's exact FM27 Game Pass installation and compare imported entities/fields against the in-game UI and an independent source.
- [ ] Populate profile sections only as corresponding field/entity relationships are mapped and verified.
- [ ] Implement a separate process-memory writer, field allow-list, pre-write identity/generation checks, old-value check, read-back, conflict handling and undo on a disposable career.
- [ ] Keep Save Live Changes disabled until the writer and per-field tests have passed.

**Latest observed Windows Actions:** run #737, ID `43863750491`, on commit `afa624850ec5c3f5634c3605fe1fe7eeefee2d7b`, completed as failure. Its job record has `steps=null`, no logs URL and its log download returned 404/BlobNotFound. It failed before Restore/Build/Test ran. This is not evidence of a successful compile or a source compilation failure. The local environment has no .NET SDK, C# compiler, MSBuild or PowerShell.

**Design review:** `docs/UI_REVIEW_001.md`.  
**Live connection safety:** `research/LIVE_GAME_CONNECTION_001.md` and `research/LIVE_EDIT_SAFETY_001.md`.  
**Latest honest state:** profile/workspace structure substantially implemented; many source-specific tabs remain empty; live reader unverified; no writer enabled; no Windows build artifact produced.



### Checkpoint 022 — Club profile linked squads and staff

- [x] Player live records now project a numeric `club_uid` from the player's team -> club object.
- [x] Staff live records now project a numeric `club_uid` from contract -> team -> club object.
- [x] Club Squads section queries the player index by exact `club_uid` and displays matching player names/IDs.
- [x] Club Staff section queries the staff index by exact `club_uid` and displays matching coach/staff names/IDs.
- [x] Double-click from either list opens the corresponding Player or Staff profile in the in-app workspace.
- [x] Added a regression test proving both joins include club 77 records and exclude club 88 records.
- [ ] Verify the source offsets and relationships against a running FM27 Game Pass 0.9.2435.0 career. These are still `LiveGameMemory / Mapped`, not `ReadVerified`.
- [ ] Windows .NET 8 restore/build/test remains blocked by Actions runner failures before steps/logs; no executable artifact is verified.

**Latest code commit for staff/player club links:** see branch head and `research/LIVE_GAME_CONNECTION_001.md`.


## Checkpoint 023 — Club profile typed-section completeness

**Date:** 2026-11-16 UTC
**Branch:** `rebuild/fresh-foundation-001`
**Code commit:** `069c335f3ef56dcf57bf8d42a945b4077abcc994`
**Status:** Club profile presentation coverage expanded; Windows CI still fails before runner steps.

### Work saved
- Expanded the Club profile Youth Intake section to include typed text, integer, decimal, date and Boolean fields where the source key identifies youth/intake data.
- Expanded Finances to display all canonical finance values and every additional typed decimal value not already represented in the canonical finance dictionary. This prevents generic balance/budget fields from being hidden solely because their key does not contain finance/debt/loan/sponsor.
- Expanded Club Vision, Possible Signings, Player Clauses and Transfer Center to display relevant typed text, integer, decimal, date and Boolean fields.
- Expanded Wage Contributions, Kits & Colours, Affiliations, Match Preparation and Stadium to surface their related typed values across the available types.
- All values continue to use existing provenance labels. No fields or values are fabricated and no importer coverage is implied by this presentation change.
- Updated `docs/UI_REVIEW_001.md` with the additional profile coverage audit.

### Verification
- Source change committed as `069c335f3ef56dcf57bf8d42a945b4077abcc994`.
- Windows workflow run #790 / ID `43866210853` failed before creating runner steps. The job record reports `steps=null` and no log URL; no compiler/test result was produced.
- Local environment still has no .NET SDK, MSBuild or Windows WPF runtime. Treat the C# change as committed but not build-verified.

### Remaining priority gates
1. Restore a functioning Windows CI runner and obtain a real .NET 8 Release build/test result.
2. Run the application on Windows and visually compare the dashboard, Advanced Search, Player, Staff and Club profiles against REF-001 to REF-004 at normal scaling.
3. Verify exact FM27 Game Pass 0.9.2435.0 live reads against a disposable career; current external layout mappings remain unverified on the user's installation.
4. Expand actual club-related source imports (youth intake, board vision, clauses, transfers, wage contributions, kits, affiliations and match preparation) only after their entity structures and relationships are confirmed.
5. Populate remaining Player/Staff tabs from real source records; current empty states are intentional where the provider does not yet supply those records.
6. Keep Save Live Changes disabled until a separate writer passes per-field preflight, read-back, conflict and undo tests.

## Checkpoint 024 — Staff and Club profile detail refinement

**Date:** 2026-11-16 UTC
**Branch:** `rebuild/fresh-foundation-001`
**Status:** Additional profile summary fields and section data coverage committed; Windows runtime/build verification remains outstanding.

### Work saved
- Staff profile view model now exposes date of birth, club/team, primary role, unique game ID, wage and contract expiry as presentation properties.
- Staff profile left rail now displays those identity fields and a separate contract summary while retaining age, CA and PA in the header.
- Club profile view model now exposes ownership, chairperson and attendance summary values.
- Club profile left rail now displays ownership, average attendance and chairperson alongside crest, nation/competition and stadium information.
- Club profile section projector now surfaces all relevant typed field kinds for youth intake, finance, club vision, possible signings, clauses, transfer centre, wage contributions, kits/colours, affiliations, match preparation and stadium.
- Removed internal fresh-foundation milestone language from the navigation footer.
- Updated `docs/UI_REVIEW_001.md` to preserve these refinements.

### Safety and verification
- All changes are display/projection improvements over existing typed record values; no memory offsets, values or importer coverage were invented.
- No live process write has been performed. Save Live Changes remains disabled.
- The container has no .NET SDK/MSBuild and cannot compile WPF. The recent Actions job failures have no runner steps/logs, so they cannot be interpreted as compiler results.
- No screenshot comparison against the user's Windows runtime has yet been possible.

### Next
1. Restore a working Windows Actions runner and run Release build plus the complete test suite.
2. Launch on the user's PC and inspect the Dashboard, Advanced Search, Player, Staff and Club tabs at normal scaling.
3. Compare live records with the user's exact FM27 Game Pass build and a disposable career; promote only independently checked fields from Mapped to ReadVerified.
4. Continue implementing source-backed club data families and Player/Staff profile history, stats, languages, clauses, vision, transfers and related tabs as their actual save structures are confirmed.
5. Do not enable live editing until per-field writer, read-back, conflict and undo verification is complete.