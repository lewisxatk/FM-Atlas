# FM27 live-load safety pause

**Date:** 16 November 2026  
**Branch:** `rebuild/fresh-foundation-001`  
**Status:** Live process scanning disabled in the desktop application pending investigation.

## Reported incident

The user reported that pressing **Load Game** in FM Atlas caused Football Manager to close without a warning. The exact time within the scan, Windows error details, crash dump and game build fingerprint from that session have not been supplied. The root cause is therefore **unknown**.

Treat this as a serious data-safety incident. Do not ask the user to reproduce it on a valuable career.

## What source inspection establishes

The experimental live connector:
- Locates the `fm` process and enumerates its `game_plugin.dll` module.
- Checks a PE fingerprint against a reference profile for FM27 Game Pass 0.9.2435.0.
- Opens the process with `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION`.
- Reads external process memory using `ReadProcessMemory`.
- Holds a process object and read handle while it monitors the save-generation value.

The inspected `WindowsProcessMemoryAccessor` exposes no write method. The inspected connector has no explicit call to `Process.Kill`, `Process.CloseMainWindow` or `TerminateProcess`; its cleanup disposes the managed `Process` object and closes its read handle.

This source inspection does **not** prove that the scanner is harmless. It uses externally mapped addresses and pointer relationships that have not been validated against the user's live career. A package fingerprint match alone is not enough to establish that every memory read is valid, safe or correct. No confirmed explanation for the game closure is available yet.

## Immediate fail-closed changes

- `LiveLoadSafetyGateEnabled` is set to `false` in the desktop view model.
- The command cannot execute while that constant is false.
- `LoadLiveGameAsync` returns before changing UI state or calling the connector if the gate is false.
- Both visible Load Game controls are disabled and labelled **Live Load Paused**.
- The dashboard explains the pause.
- CSV import, local database operations, Quick Search, Advanced Search and local profile views remain usable.
- Save Live Changes remains disabled; no writer is implemented.

The experimental scanner source is retained for controlled investigation, but the UI must not call it while the gate remains false.

## Previous build evidence and its limit

Windows build #903 passed restore, Release compilation, 107 automated tests, self-contained x64 publish and an eight-second WPF process-start smoke test on a Windows Server 2025 runner. Football Manager was not running during that smoke test. It did not exercise live process access and is not evidence that the connector is safe. The new safety-paused source requires a fresh Windows build before distribution.

## Re-enable requirements

Do not change the safety gate to true until all of the following have been addressed:

1. Review Windows Application event logs and any Football Manager/Game Pass crash records available from the affected session.
2. Review the exact installed FM executable and `game_plugin.dll` fingerprint, process architecture, file versions and the reader's module-relative and heap-relative address assumptions.
3. Audit every pointer chain, count, stride and collection traversal against authoritative evidence for the exact installed package. A fingerprint match is not enough.
4. Add bounded reads, conservative scan limits, cancellation and explicit checks for every pointer/region before reading. Avoid assumptions that an in-game array remains stable while it is being traversed.
5. Prove the connector cannot invoke process termination or write APIs, and add diagnostic logging that captures scan stage and last validated object without dumping private career data.
6. Run in a disposable/offline career and compare a small initial sample with the in-game UI. Expand only after the first sample passes without game instability.
7. Keep a manual-save/backup recovery path and stop immediately on any crash, close or mismatch.

The user's own Windows 10/FM27 environment must be part of final acceptance. A hosted CI startup smoke test cannot replace it.

## User safety guidance

Until a safety-paused build is supplied, keep the current FM Atlas build away from the **Load Game** action. If Football Manager has closed, reopen it and check whether the game offers a crash-recovery or auto-save version; compare the save timestamps and preserve the last known-good manual save rather than overwriting it.

No career-save file has been requested or needed for this investigation.
