# FM Atlas — Fresh Rebuild

A Windows desktop application for Football Manager scouting, save intelligence and (in a later, separately verified stage) editing.

**Current branch:** `rebuild/fresh-foundation-001`  
**Architecture:** .NET 8 WPF, modular Domain / Application / Infrastructure / Desktop / Tests.  
**Design authority:** `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md`.

## Current implementation

The fresh WPF workspace currently includes:
- Dashboard plus Players, Staff, Clubs, Save Manager and Settings navigation.
- Global Quick Search and a modal, typed Advanced Search dialog.
- Player, Staff and Club directories with closable in-app profile tabs.
- Player, Staff and Club profile sections based on the FMRTE feature audit, with explicit source-backed values or honest empty states.
- Player attribute catalogue, including Technical, Goalkeeping, Mental, Physical, Hidden, Personality and Feet groups.
- Club Squads and Staff lists joined from imported numeric club IDs, with navigation into linked profiles.
- Recently opened profile shortcuts and automatic player portrait / club badge lookup from standard FM27 graphics config mappings.
- CSV player import into local, provider-scoped SQLite storage. Re-importing the same source is idempotent; additional typed columns are retained and searchable where supported.
- A blocking live-load progress panel and an exact-fingerprint FM27 Game Pass 0.9.2435.0 read-only scanner for Player, Staff and Club snapshots.

The application is a substantial functional workspace foundation, not yet a complete FM editor. Some profile sections remain empty until their source data is imported. Search only returns records and fields present in the local index and supported by the typed query provider.

## Current verification

Windows GitHub Actions run [#896](https://github.com/lewisxatk/FM-Atlas/actions/runs/43901795505), commit `73fb9eafd8ba522806f53bdf9423005580536642`, completed successfully on a Windows hosted runner:
- Restore and Release build passed.
- **107 automated tests passed; 0 failed.**
- Self-contained Windows x64 publish passed.
- `FMAtlas.exe` was present and the package was created and uploaded.
- Published ZIP SHA-256: `4ACC8318667178722461368FC82809E80399502A74A0E430B055D11817A4268A`.

This confirms compilation, tests and packaging on Windows Server 2025. It does **not** yet prove the app opens correctly on the user's Windows 10 desktop, visually matches the design references, or correctly reads the user's running FM27 career. The next workflow update adds an automated process-start smoke check; its result must be checked before treating that additional gate as passed.

## Build and test on Windows

Requires Windows 10/11 x64. The checked-in application package is self-contained and does not require a separate .NET Desktop Runtime. To build from source, install the .NET 8 SDK and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1
```

The script performs restore, Release build, the full solution test suite and a self-contained Windows x64 publish. It stops at the first failed step, writes separate logs under `artifacts\logs`, stores TRX test results under `artifacts\test-results`, and creates a timestamped ZIP plus test report only after every step passes.

GitHub Actions now runs on `windows-latest` (not a Linux cross-build). It restores, builds, runs tests, publishes the self-contained x64 app, checks startup on the Windows runner and uploads the ZIP plus diagnostics. Workflow artifacts are retained for 14 days.

## First user test

1. Download and extract the latest `FM-Atlas Windows x64` ZIP from the successful Windows build's Actions artifacts.
2. Run `FMAtlas.exe` from the extracted folder. Keep all files in the same folder.
3. Check that the main window opens, Quick Search popup can be opened and dismissed, and Advanced Search opens and closes without a crash.
4. Open Players, Staff and Clubs directories. Inspect the profile workspaces and note which sections show source-backed data versus “not imported”.
5. Use CSV import for a compatible player export if available. Imported/exported values are labelled with their source; they are not treated as live-memory verified.
6. Follow `docs/TEST_HANDOFF_2026-11-16.md` for the safe live-connection test sequence. Do not use a valuable career for first-pass validation.

## Product and technical references

- `docs/FRESH_REBUILD_MASTER_PLAN.md` — product scope and milestones.
- `docs/FRESH_REBUILD_CHECKLIST.md` — task tracker.
- `docs/CURRENT_CHECKPOINT.md` — resumable project state.
- `docs/SESSION_CONTINUITY.md` — interruption recovery rules.
- `docs/ARCHITECTURE_DECISION.md` — application architecture.
- `research/UPLOADED_RESEARCH_INVENTORY.md` — uploaded package hashes and contents.
- `research/RESEARCH_FINDINGS_001.md` — technical research evidence.
- `research/FMRTE_FEATURE_AUDIT_001.md` — FMRTE 27 player/staff/club tab and tool coverage.
- `research/FIELD_COVERAGE_LEDGER.md` — field counts and verification status.
- `docs/UI_REVIEW_001.md` — current UI structure and outstanding acceptance gates.

Historical R7.2.0 source is preserved separately in `recovery/r7.2.0-source-import` and is a research/reference pool, not the new application's code foundation.

## FM27 Game Pass live connection — read-only first

The Load Game path is restricted to the exact documented FM27 Game Pass package **0.9.2435.0**. It fingerprints the game module and opens a read-only process handle. The scanner projects Player, Staff and Club snapshots and the app imports those records into its local index. It rejects unknown fingerprints rather than guessing at memory layouts.

**Important:** the external memory layout is still awaiting comparison with a running career on the user's installation. The scanner has not yet been accepted as accurate for the user's live save. Player/Staff/Club counts, IDs, CA/PA, attributes, club links and other fields must be checked against the in-game UI and an independent reference before their status is upgraded to ReadVerified.

The connector monitors the save-generation marker and invalidates the active live snapshot if FM closes, the career is switched or the live read stops validating. A successful scanner call is not proof that every imported field is mapped correctly.

## Save buttons and editing boundary

- **Save Atlas** initialises and verifies the local FM Atlas SQLite database. It does not save or modify the Football Manager career.
- **Save Live Changes** is intentionally disabled. No verified process-memory writer is enabled.
- The Windows process accessor is read-only. Do not enable editing until each field has a verified mapping, value bounds, target identity and save-generation preflight, a pre-write old-value check, post-write read-back, conflict handling and tested undo/rollback on a disposable offline career.
- To save the Football Manager career, use Football Manager's own save command. FM Atlas will not claim that a live edit is persisted until a writer passes those checks and the game save/reload test.
