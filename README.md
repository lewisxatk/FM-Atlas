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

**Live Game loading is currently safety-paused.** The Load Game controls are disabled and the view model blocks the connector call. Do not try to re-enable or bypass this gate. The pause follows a user report that Football Manager closed unexpectedly when Load Game was pressed. CSV import, local database storage and searches over imported data remain available. See `docs/LIVE_LOAD_SAFETY_PAUSE_2026-11-16.md`.

The application is a substantial functional workspace foundation, not yet a complete FM editor. Some profile sections remain empty until their source data is imported. Search only returns records and fields present in the local index and supported by the typed query provider.

## Current verification

Windows GitHub Actions run [#903](https://github.com/lewisxatk/FM-Atlas/actions/runs/43903568849), commit `274188ea3f3a1ba3722be5bbce7aca597e8d7cb3`, completed successfully on a Windows Server 2025 hosted runner:
- Restore and Release build passed.
- **107 automated tests passed; 0 failed.**
- Self-contained Windows x64 publish passed.
- `FMAtlas.exe` was present and the package was created and uploaded.
- The WPF startup smoke test passed: the process remained running for eight seconds.

The smoke test did not have Football Manager running and does **not** test live process access, live memory reads, Windows 10 on the user's PC, or safety against the reported FM closure. The next safety-paused build must be tested before distributing it.

## Build and test on Windows

Requires Windows 10/11 x64. A self-contained package does not require a separate .NET Desktop Runtime. To build from source, install the .NET 8 SDK and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1
```

The script performs restore, Release build, the full solution test suite and a self-contained Windows x64 publish. It stops at the first failed step, writes separate logs under `artifacts\logs`, stores TRX test results under `artifacts\test-results`, and creates a timestamped ZIP plus test report only after every step passes.

GitHub Actions runs on `windows-latest`. It restores, builds, runs tests, publishes the self-contained x64 app and performs a basic process-start smoke test. That smoke test is not a live FM integration test. Workflow artifacts are retained for 14 days.

## Current safe user testing

1. Keep the Live Load button disabled; do not attempt to connect FM27 from this build.
2. The top-bar Save Atlas action only initializes/verifies the local FM Atlas database. It does not modify a Football Manager career.
3. CSV import remains available. Imported values are marked as Game Export / Mapped, not live-memory verified.
4. Quick Search, Advanced Search and the profile workspaces can be exercised against imported CSV datasets.
5. Do not use a valuable career to test any future live-connection build. The live-load safety gate will remain until the closure report is investigated and a controlled validation passes.

## Product and technical references

- `docs/FRESH_REBUILD_MASTER_PLAN.md` — product scope and milestones.
- `docs/FRESH_REBUILD_CHECKLIST.md` — task tracker.
- `docs/CURRENT_CHECKPOINT.md` — resumable project state.
- `docs/SESSION_CONTINUITY.md` — interruption recovery rules.
- `docs/ARCHITECTURE_DECISION.md` — application architecture.
- `docs/LIVE_LOAD_SAFETY_PAUSE_2026-11-16.md` — incident report and re-enable gates.
- `research/UPLOADED_RESEARCH_INVENTORY.md` — uploaded package hashes and contents.
- `research/RESEARCH_FINDINGS_001.md` — technical research evidence.
- `research/FMRTE_FEATURE_AUDIT_001.md` — FMRTE 27 player/staff/club tab and tool coverage.
- `research/FIELD_COVERAGE_LEDGER.md` — field counts and verification status.
- `docs/UI_REVIEW_001.md` — current UI structure and outstanding acceptance gates.

Historical R7.2.0 source is preserved separately in `recovery/r7.2.0-source-import` and is a research/reference pool, not the new application's code foundation.

## Live Game Pass connection — not currently available

The repository retains the experimental FM27 Game Pass 0.9.2435.0 read-only scanner, but the desktop app's connection path is safety-locked. The external memory layout has not been validated against the user's running career. The source includes pointer traversal and external reference offsets; a matching executable fingerprint alone is not sufficient proof that those reads are safe or correct.

No live memory scanning should be initiated from the application while the safety gate is in place. The gate must not be removed until a controlled reproduction/diagnostic review, field-by-field read verification and a disposable-career test have passed. Unknown builds must continue to fail closed.

## Save buttons and editing boundary

- **Save Atlas** initializes and verifies the local FM Atlas SQLite database. It does not save or modify the Football Manager career.
- **Live Load** is temporarily disabled during the safety investigation.
- **Save Live Changes** remains disabled. No verified process-memory writer is enabled.
- To save the Football Manager career, use Football Manager's own save command. FM Atlas will not claim that a live edit is persisted until a writer passes per-field mapping, value-bound, target identity, save-generation, read-back, conflict, undo/rollback and save/reload verification.
