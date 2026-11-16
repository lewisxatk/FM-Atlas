# FM Atlas — Current Checkpoint

**Checkpoint:** 003  
**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Previous saved branch head:** `e088c9f43bf91c08ef1d161a662d683ecc10e030`  
**Status:** First clean WPF foundation and initial dashboard shell committed. Windows build result pending.

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

## Repository research completed in this session

- FMLibreEditor: separate process architecture, native `game_plugin.dll`, read/write checks, save-generation invalidation, read-only fallback; exact Game Pass evidence is package 0.9.2435.0 and must not be generalised to other builds.
- fm-editor: FM27 layout includes inherited FM26 leads; only explicitly witnessed fields are verified. Its README records a narrow Game Pass package bring-up, not broad feature parity.
- FMSuperScout: MIT, .NET 8 Windows read-only scanner plus local search application; useful extraction/indexing benchmark.
- fmsave: MIT, FM26/FM27 offline parser; 52 player attributes, 8 personality attributes and extensive save tables, with explicit coverage/unknown-value semantics.
- FM View Exporter: GPL-3.0, documents 375 FM27 table columns and 377 Insert Column menu entries; table order is not assumed to equal player-profile attribute order.
- FM Player Analyzer: GPL-3.0, useful save import, profile, graphics indexing and testing research.
- Licensing boundaries are documented; no third-party source copied.
- Official FMRTE 27 player, staff, club, search and edit-history documentation was researched; detailed tab/feature audit committed as `research/FMRTE_FEATURE_AUDIT_001.md`.
- Player tabs now follow the actual FMRTE 27 section inventory while retaining the user's required top-of-profile tab placement.

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
- A Windows GitHub Actions restore/build/test workflow is committed.
- Initial unit tests cover connection-result state objects.

## Current milestone

**M2 — Fresh application foundation.**

## Tests and environment

- Uploaded ZIP manifest and SHA-256 inventory: completed.
- Public repository source/documentation reads: initial pass completed.
- No installer executed.
- No FM process attached.
- No live memory read/write test.
- No local WPF compile: `dotnet` is not installed in this container.
- GitHub Actions workflow file committed; run/status not yet verified.
- No visual screenshot comparison yet.
- All work committed to GitHub; no uncommitted changes in the GitHub branch.

## Next actions

1. Run/verify the Windows GitHub Actions build; fix any XAML/C# compile errors.
2. If Actions cannot run automatically, user builds on Windows with the README commands and supplies the unedited build log.
3. Compare the actual desktop screenshot with REF-001 and refine only against the approved reference.
4. Build the REF-002 Advanced Search dialog structure.
5. Build the REF-004 player profile workspace/tab layout.
6. Continue field-level research and read-only Game Pass probe.

## User action needed

No new download is required now. The R7.2.0 recovery ZIP is located in the Library; the imported source branch is available. Later, provide the exact FM27 Game Pass package/build details and use a disposable offline career for live integration tests.
