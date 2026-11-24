# FM Atlas — Fresh Rebuild

A Windows desktop application for Football Manager scouting, live-save intelligence and (in a later, separately verified stage) editing.

**Current branch:** `rebuild/fresh-foundation-001`  
**Architecture:** .NET 8 WPF, modular Domain / Application / Infrastructure / Desktop / Tests.  
**Design authority:** `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md`.

## Current implementation

- Dashboard plus Players, Staff, Clubs, Save Manager and Settings navigation.
- Global Quick Search and a modal, typed Advanced Search dialog.
- Player, Staff and Club directories with closable in-app profile tabs.
- Player attribute catalogue, visible/hidden/personality/foot ratings and CA/PA fields.
- Club Squads and Staff lists joined from imported numeric club IDs.
- CSV player import into local, provider-scoped SQLite storage.
- An exact-fingerprint FM27 Game Pass 0.9.2524.0 read-only live scanner, with explicit user confirmation before attaching to the FM process.
- Memory reads check that the requested range is committed and readable using `VirtualQueryEx` before `ReadProcessMemory`; unreadable/guarded ranges stop the scan.
- A pre-import layout validator checks Player/Staff/Club identities, unique UIDs, names, CA coverage, position ratings, natural-position ratings and player-to-club UID joins.
- A local diagnostic log records process/module fingerprint, scan stage, read counts, validation outcomes and errors without recording player names, UIDs or raw career data.

The app is a functional workspace foundation, not yet a complete FM editor. Some profile sections remain empty until their source data is imported. Search only returns records and fields present in the local index and supported by the typed query provider.

## Live connection — first-user validation

**Load Game is enabled for the guarded, read-only scanner.** The user must first save the career in FM27, wait for it to finish loading, then accept the explicit connection warning in FM Atlas.

**FM27 27.1.1 detection:** FM Atlas now recognises the user's Game Pass package 0.9.2562.0 by its supplied DLL fingerprint (PE timestamp `0x6AFF583B`, image size `0x17624000`). This is identification only. The 27.1.1 profile deliberately has no scanner enabled until its module roots and entity layouts are mapped and validated. Load Game must reject this build safely for now; do not bypass the guard or reuse 27.1.0 addresses.

The scanner:
1. Locates the running FM process and `game_plugin.dll`.
2. Checks the exact Game Pass 0.9.2524.0 x64 PE timestamp and image-size reference before opening a memory handle.
3. Requests `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` only.
4. Checks each memory range with `VirtualQueryEx` before reading it.
5. Captures and rechecks the game's save-generation counter.
6. Reads Player, Staff and Club lists and validates the assembled snapshot before storing it in the local Atlas database.
7. Discards a failed/partial snapshot rather than indexing partial results.

The Game Pass 0.9.2524.0 reference profile and field layout are drawn from external reverse-engineering documentation. A fingerprint match and automated sanity checks are not a substitute for comparing the imported values against the user's actual FM27 career. Until that comparison is complete, the values remain `Mapped`, not `ReadVerified`.

The connector writes diagnostics to `%LOCALAPPDATA%\FM Atlas\Logs\live-connection.log`. The log records scan stages, module fingerprint, read counts and validation/error messages; it does not write player names, UIDs or raw career data.

Use a manually saved disposable/offline career or a copy for first-pass validation. If FM closes, hangs or produces mismatched values, stop the test and preserve both the diagnostic log and the last known-good manual save. The earlier FM closure is unconfirmed and may have been coincidental; no root cause has been established.

## Live editing and saving boundary

- The live connector is **read-only**. It does not request `PROCESS_VM_WRITE` or `PROCESS_VM_OPERATION`; it has no memory-write or process-termination API.
- **Save Live Changes** remains disabled. No field has `WriteVerified` status.
- **Save Atlas** initializes/verifies the local SQLite database. It does not save or modify the Football Manager career.
- Do not treat a successful snapshot or matching fingerprint as proof that all fields are accurate.
- Live editing must wait until each field has a build-specific mapping, identity/save-generation checks, a pre-write value check, read-back verification and tested rollback on a disposable career.

## Windows build verification

Latest verified baseline before the 27.1.1 integration:
- Windows Actions run [#1055](https://github.com/lewisxatk/FM-Atlas/actions/runs/44575813957), commit `197c89e6cde92599c81c214f7297daab60581975`.
- **125 automated tests passed; 0 failed.**
- Release build completed with **0 warnings and 0 errors**.
- Self-contained Windows x64 publishing and ZIP packaging passed (about 73.3 MB).
- WPF process-start smoke test passed: `FMAtlas.exe` remained running for eight seconds on the hosted Windows runner.
- This baseline still targets the previous 27.1.0 Game Pass scanner; the new 27.1.1 integration is being built and tested separately.

The hosted WPF smoke test does not run Football Manager. It cannot validate the user's actual live career or prove every external memory offset on their machine.

## Build from source

Requires the .NET 8 SDK. On Windows:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1
```

GitHub Actions builds on `windows-latest`, runs the full test suite, publishes a self-contained x64 desktop package and performs a basic WPF process-start smoke check. Workflow artifacts are retained for 14 days.

## References and project notes

- `docs/FRESH_REBUILD_MASTER_PLAN.md` — product scope and milestones.
- `docs/FRESH_REBUILD_CHECKLIST.md` — task tracker.
- `docs/CURRENT_CHECKPOINT.md` — resumable project state.
- `docs/SESSION_CONTINUITY.md` — interruption recovery rules.
- `docs/LIVE_LOAD_SAFETY_PAUSE_2026-11-16.md` — original incident and mitigations.
- `research/LIVE_GAME_CONNECTION_001.md` — build profile and field map research.
- `research/FM27_GAMEPASS_27.1.1_BUILD_0.9.2562.0.md` — fingerprint from the user's supplied 27.1.1 DLL and the evidence boundary for this package.
- `research/FMRTE_FEATURE_AUDIT_001.md` — player/staff/club profile and search coverage.
- `research/FIELD_COVERAGE_LEDGER.md` — field status and evidence.

Historical R7.2.0 source remains in `recovery/r7.2.0-source-import` as research/reference material; it is not the fresh application's code foundation.
