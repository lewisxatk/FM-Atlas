# FM Atlas — Current Checkpoint

**Checkpoint:** 004  
**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Base head before this checkpoint:** `9eae52a94536abf0b99a96fc5257e0cd195dc69e`  
**Status:** Fresh WPF shell compiles and tests successfully on Windows CI. Artifact-upload workflow is running separately.

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
- A later workflow run with artifact upload is pending: [43787554512](https://github.com/lewisxatk/FM-Atlas/actions/runs/43787554512).

## Current milestone

**M2 — Fresh application foundation.**

## Next actions

1. Confirm the artifact-upload workflow completes and obtain the Windows shell package.
2. Inspect/run the shell on Windows and compare screenshot with REF-001; refine only against approved reference.
3. Add the verified FM27 attribute catalogue to the new Domain project and tests.
4. Build the REF-002 Advanced Search dialog shell and filter model.
5. Build the REF-004 full-screen tabbed player profile shell.
6. Continue field-level Game Pass research and read-only connection probe.

## User action needed

No new research download is required now. The R7.2.0 recovery ZIP is located in the Library; its imported source branch is available. Later, provide the exact FM27 Game Pass package/build details and use a disposable offline career for live integration tests.
