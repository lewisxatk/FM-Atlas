# FM27 live-load incident and mitigations

**Date opened:** 16 November 2026  
**Branch:** `rebuild/fresh-foundation-001`  
**Current state:** Guarded exact-build, read-only live connection re-enabled for first-user validation. The original cause remains unknown.

## User report

The user observed Football Manager closing after pressing FM Atlas **Load Game**. There was no warning or crash dialog reported. The user later noted the timing may have been coincidental. No Windows Application event log, crash dump or exact incident-time module fingerprint has been supplied. Do not infer causation either way.

## Source inspection

The inspected scanner:
- enumerates the `fm` process and `game_plugin.dll`;
- checks the module PE timestamp, image size and machine against the external Game Pass 0.9.2435.0 reference;
- requests `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` only;
- reads using `ReadProcessMemory`;
- exposes no process terminate, memory write or process kill API.

This establishes the access requested by the code, not that the scan cannot race the game or cause unexpected instability. The exact offsets remain externally sourced until compared with the user's active FM27 career.

## Mitigations now implemented

1. Load Game now requires explicit user confirmation before connecting. Declining the prompt makes no connector call.
2. The exact Game Pass fingerprint gate remains. Unknown or different builds are rejected before process-memory reads.
3. `WindowsProcessMemoryAccessor` checks each requested range using `VirtualQueryEx`; it rejects uncommitted, guarded or non-readable memory, bounds pointer/range arithmetic and read length, and requests no write/termination rights.
4. The connector writes a local diagnostic log to `%LOCALAPPDATA%\FM Atlas\Logs\live-connection.log`, recording stage, process/module fingerprint, read counts, validator result and errors without player names, UIDs or raw career data.
5. Before database import, a layout validator checks all three entity families, provider-scoped unique IDs, names, CA coverage, position ratings, natural-position ratings and player-to-club UID links. Failed checks discard the scan.
6. Save-generation checks before and after scanning/import remain in place. Field evidence stays `Mapped`, not `ReadVerified`, until values are compared with the user's in-game values.
7. Signed negative PA values documented as random-potential range markers are preserved and explained in the player profile.
8. `Save Live Changes` remains disabled. No live writer is enabled.

## Automated verification

Windows Actions run [#933](https://github.com/lewisxatk/FM-Atlas/actions/runs/43909923853), commit `50948ad51fa217e860dc3d482167bf08418877ae`, passed:
- 111 automated tests;
- self-contained x64 publish and ZIP packaging;
- an eight-second WPF process-start smoke test.

That run reported two compiler warnings. Both have been corrected and tests for signed PA range handling have been added. A fresh Windows Actions run is the acceptance gate for the clean package.

CI does not run Football Manager and cannot validate external addresses or game stability. The user's actual Windows 10 / FM27 session remains required.

## First-user acceptance

Use a manually saved disposable/offline career or a copy. Confirm the warning, start the guarded scan, compare a small sample of names, UIDs, CA/PA, attributes, staff and clubs against FM27's UI, and check that the connection diagnostic log shows all validation stages. Stop at the first unexpected closure, hang or mismatch. Preserve the log and last known-good manual save. Do not enable live editing based on read-only test success.
