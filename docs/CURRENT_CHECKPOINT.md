# FM Atlas — Current Checkpoint

**Checkpoint:** 002  
**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Previous saved branch head:** `2f8a21b1bd29d12a3a56ddb4224303443da2784a`  
**Status:** Recovery and research inventory completed; architecture selected; fresh application shell is next.

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

The seven newly uploaded packages have been SHA-256 inventoried and statically inspected. No installer or executable was run.

- FM Player Analyzer 2.2.3 source ZIP extracted for static review (GPL-3.0): C++20/Qt 6, headless core, SQLite schema v16, read-only FM27 save import, graphics config index and extensive tests.
- TalentSeer 27.1.2 ZIP contains README + compiled EXE + notices, no source; treated as product benchmark only.
- FMRTE 27.0.1 build 12 ZIP contains portable binaries/resources, no source; 25 release-image assets catalogued as feature leads.
- Genie Scout ZIP contains one installer EXE only.
- FMSuperScout and FMLibreEditor uploads are installers only; their public source repositories were inspected directly.
- FM Dossier upload is an installer only.
- Research inventory and hashes: `research/UPLOADED_RESEARCH_INVENTORY.md`.
- Technical findings: `research/RESEARCH_FINDINGS_001.md`.

## Repository research completed in this session

- FMLibreEditor: separate process architecture, native `game_plugin.dll`, read/write checks, save-generation invalidation, read-only fallback; exact Game Pass evidence is package 0.9.2435.0 and must not be generalised to other builds.
- fm-editor: FM27 layout includes inherited FM26 leads; only explicitly witnessed fields are verified. Its README records a narrow Game Pass package bring-up, not broad feature parity.
- FMSuperScout: MIT, .NET 8 Windows read-only scanner plus local search application; useful extraction/indexing benchmark.
- fmsave: MIT, FM26/FM27 offline parser; 52 player attributes, 8 personality attributes and extensive save tables, with explicit coverage/unknown-value semantics.
- FM View Exporter: GPL-3.0, documents 375 FM27 table columns and 377 Insert Column menu entries; table order is not assumed to equal player-profile attribute order.
- FM Player Analyzer: GPL-3.0, useful save import, profile, graphics indexing and testing research.
- Licensing boundaries are documented; no third-party source copied.

## Architecture decision

- Selected a fresh .NET 8 WPF solution for the Windows 10/Game Pass target.
- This is a clean solution with new Domain, Application, Infrastructure, Desktop and Tests projects; the old WPF UI/code-behind is not reused.
- SQLite is the local Atlas store; live memory, offline save parsing, FM export import and graphics lookup are separate data providers.
- Live writer remains a separate disabled-by-default service, gated by exact build and field-level evidence.
- Full rationale: `docs/ARCHITECTURE_DECISION.md`.

## Design authority

REF-001 to REF-004 are now present in this branch:
- REF-001 main application composition.
- REF-002 FMRTE-style Advanced Search structure.
- REF-003 Watched black/off-white/orange visual language.
- REF-004 full-screen tabbed player profile.

See `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md`.

## Current milestone

**M1 — Research and blueprint; architecture decision complete.**

## Completed and saved

- [x] Located the original recovery ZIP in Library metadata and verified the imported recovery branch/source tree.
- [x] Confirmed 106 imported R7.2.0 application files in GitHub.
- [x] Inventoried all seven newly uploaded packages and recorded hashes.
- [x] Inspected FM Player Analyzer source files relevant to save import, database, profiles and graphics.
- [x] Inspected public FMLibreEditor, fm-editor, FMSuperScout, fmsave and FM View Exporter documentation/source metadata.
- [x] Recorded licence boundaries.
- [x] Selected clean .NET 8 WPF architecture.
- [x] Synced approved design references and specification into this branch.
- [x] Preserved all work in Git commits.

## Next actions

1. Create the clean solution and Windows CI build/test workflow.
2. Implement the first shell only: approved navigation, global header, top Quick Search, Advanced Search action, prominent right-side Load Game and compact save controls.
3. Add no fake players or placeholder metrics; use honest disconnected/empty states.
4. Build through Windows GitHub Actions because this execution environment has no .NET SDK installed.
5. After shell compile, implement the FMRTE-style search dialog and REF-004 player workspace against the approved specs.

## Tests and environment

- Python ZIP integrity/file manifests: completed for uploaded ZIPs.
- SHA-256 inventory: completed for uploaded files.
- Public repository documentation/source reads: completed for initial research pass.
- No installer executed.
- No FM process attached.
- No live memory read/write test.
- No Windows WPF compile or runtime test: `dotnet` is not installed in this container.
- GitHub source tree check: completed.
- New application code: not yet created at this checkpoint.

## User action needed later

When we reach live integration, provide the exact FM27 Game Pass package/build version from the user's PC and use a disposable offline career copy for validation. Do not upload the user's only career save to GitHub.
