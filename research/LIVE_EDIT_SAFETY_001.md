# FM Atlas — Live Read, Edit and Save Safety Design 001

**Date:** 2026-11-16  
**Target under investigation:** FM27 Game Pass package 0.9.2435.0  
**FM Atlas branch:** `rebuild/fresh-foundation-001`  
**Status:** Read-only live connector implemented; stale-snapshot and UI-loading hardening committed; write-preflight policy and tests committed. No live write primitive is enabled.

## 1. Source evidence reviewed

The following public FMLibreEditor files were re-opened for this pass:

- [FM27 Game Pass 0.9.2435.0 recon](https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-gamepass-0.9.2435.md)
- [Game Pass 0.9.2435.0 profile](https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/gamepass-0.9.2435.0.toml)
- [Write-field model and guarded edit logic](https://github.com/tozmoz/FMLibreEditor/blob/main/crates/fmlibre-core/src/edit.rs)
- [Session, save-generation and field-lock logic](https://github.com/tozmoz/FMLibreEditor/blob/main/crates/fmlibre-core/src/session.rs)
- [Windows process memory implementation](https://github.com/tozmoz/FMLibreEditor/blob/main/crates/fmlibre-mem/src/windows/process.rs)

FMLibreEditor is GPL-3.0. These files are used as research evidence only; no source code is copied into FM Atlas.

### 0.9.2435.0 recon facts

The recon note records the package `SportsInteractive.FootballManager27_0.9.2435.0_x64__5w3tn6tb6stnm` and the following `game_plugin.dll` fingerprint:

- PE link timestamp: `0x6AE8CF77`
- image size: `0x17969000`
- player array pair RVA: `0x53B7DE0`
- staff array pair RVA: `0x53B7E28`
- club array pair RVA: `0x53B7F98`
- in-game date RVA: `0x53597C8`
- GAME_UI static RVA: `0x53B4160`

The source reports a same-save comparison across 0.9.2409.0 and 0.9.2435.0: 108 player fields, 37 club fields, and 220 witnesses. It also documents several values that normalised on load and four stadiums whose names differed. This is strong external documentation, not FM Atlas verification against the user's installation.

The recon explicitly notes that static addresses moved between package versions and that some relative distances changed. FM Atlas must therefore match the complete exact fingerprint and must never reuse these RVAs for another package.

## 2. What FM Atlas currently does

The current `WindowsFm27LiveGameConnector`:

1. Finds exactly one `fm.exe` process.
2. Confirms `game_plugin.dll` is loaded and reads its on-disk PE fingerprint.
3. Stops if the fingerprint is not the exact 0.9.2435.0 reference.
4. Opens a read-only process handle (`PROCESS_VM_READ | PROCESS_QUERY_INFORMATION`).
5. Reads the Game UI save-generation count and player/staff/club pointer arrays.
6. Reads typed PlayerRecord, StaffRecord and ClubRecord snapshots.
7. Stores the records and their searchable field projections in a single SQLite batch transaction.
8. Re-reads the save-generation count and player-array state after import.
9. Keeps the live provider only while the same process and save generation remain active.

The current imported entity families are **players, staff and clubs**. This is not yet the whole Football Manager world model. Competitions, nations, fixtures, results, history, tactics, training, injuries, transfer activity and every other data family still require their own field map, provider and acceptance tests.

Imported live values are tagged `LiveGameMemory / Mapped`. This means the implementation uses an externally documented mapping; it does not mean the field has been independently read-verified on the user's machine, and it never grants write permission.

## 3. Stale data and session-change hardening

A live provider is a disposable snapshot, not a cache to keep after a failed reload.

The connector now deletes the active live provider before scanning. If an exception or cancellation happens after a candidate snapshot has been persisted, it attempts provider deletion with a non-cancelled token before releasing the process handle. If cleanup itself fails, it surfaces both the load and cleanup errors rather than silently reporting a clean failure.

The existing session monitor checks the save-generation counter and whether the player array has emptied. A mismatch invalidates the live session and deletes its provider. The main-window view model clears visible live counts and Quick Search results at the start of a load and after a failed/cancelled load.

The monitor is a useful guard, but it is not a substitute for checking the process, generation, target identity and expected old value immediately before every future write.

## 4. Loading overlay and interaction lock

The main window already contains a centred dark loading panel with:

- animated rotating FM spinner;
- progress bar and percentage;
- current stage and explanatory detail;
- hit-test blocking and keyboard focus capture.

The code now also disables every owned top-level workspace while the overlay is visible. This includes the separate Advanced Search and Player Profile windows, which an overlay inside the main window alone would not cover. They are re-enabled when loading ends.

The overlay must remain active until the scan, database transaction and post-import save-generation validation have all completed or failed.

## 5. Write architecture learned from independent source review

The external editor does not treat a field offset as sufficient permission to write. Its documented implementation checks:

- exact build support and health checks;
- whether the active save changed since the session was read;
- field-specific value range and plausible existing value;
- player/staff/club target identity and expected object class;
- related contract/team/club records and pointer relationships;
- expected old value to detect concurrent changes;
- writable memory-region type and protection;
- read-back after the write;
- undo using the original value, with the current value checked again.

Its save-generation marker is sticky: once a different save is observed, writes remain blocked until a fresh recheck/read establishes a new session. The editor also keeps writes away from executable code and arbitrary module image memory.

FM Atlas must independently implement and test its own equivalent policy. This is a design reference, not a licence or proof that the FM Atlas mappings are correct.

## 6. FM Atlas code added in this pass

### Live snapshot lifecycle

`WindowsFm27LiveGameConnector.cs` now:
- clears the live provider before every load attempt;
- clears a candidate provider after cancellation, failed post-import generation checks or other post-persist exceptions;
- uses `CancellationToken.None` for cleanup so a cancelled load cannot cancel its own cleanup;
- reports an aggregate failure if both the load and stale-row cleanup fail;
- marks the disconnect request volatile across UI and worker continuations.

### UI interaction lock

`MainWindow.xaml.cs` now disables all owned windows while the blocking loading overlay is visible, then re-enables them afterwards.

### Live load state

`MainWindowViewModel.cs` now clears old live counts, dashboard import labels and Quick Search results before starting a reload, and also clears them on failure/cancellation. The UI cannot continue displaying a previous career's counts after its live provider has been invalidated.

### Write preflight policy

Added `src/FMAtlas.Application/LiveEditing/LiveEditWritePreflight.cs`. This pure, no-I/O policy requires all of the following before a future writer may attempt a write:

1. a connected live session;
2. unchanged process ID;
3. exact expected build fingerprint;
4. unchanged save-generation count;
5. verified entity identity;
6. field provenance specifically marked `LiveGameMemory / WriteVerified`;
7. exact matching target build on that field's provenance;
8. expected current value still matching memory;
9. valid field range and proposed value;
10. a memory region independently confirmed as a safe writable data region.

Added `tests/FMAtlas.Tests/LiveEditWritePreflightTests.cs` for the positive path and each fail-closed condition.

**Important:** this is a policy gate only. There is no FM Atlas process-memory write call, no editable field has been marked WriteVerified, and the Save Live Changes button remains disabled. The preflight must be wired into a separately tested writer, not treated as a writer itself.

## 7. Required next implementation sequence

1. **Read verification:** create a disposable-save test harness that compares FM Atlas player/staff/club reads against the same live career and records field-by-field match, mismatch and unavailable counts.
2. **Object integrity:** independently validate pointer arrays, object identity, person-block shifts, player-coach variants, contracts and club/team links. Do not silently treat a partial scan as a complete dataset.
3. **Writer primitive:** add a separate Windows write accessor with memory-region checks. Keep the existing read-only accessor read-only.
4. **First writable fields:** select a very small set of scalar fields with independently validated type, range, old-value plausibility, address and read-back. Do not start with pointers, strings, structural lists or bulk editing.
5. **Undo and conflicts:** store old/new values, reject concurrent changes, verify read-back and test rollback on a disposable career.
6. **Expand field catalogue:** only add a field to editable controls after its own tests pass. All other fields remain searchable/read-only.
7. **Expand world coverage:** add other entity families and data sets through independent providers; never claim "everything" while only player/staff/club records are imported.

## 8. Verification status

- Source and tests committed on `rebuild/fresh-foundation-001`.
- Windows Actions runs #563/#564 for recent source heads failed before any job steps were created (`steps=null`, no log URL). They provide no compiler or test output.
- The local tool environment does not have the .NET SDK or Windows, so no successful build, process read or real-game comparison has been performed in this session.
- No live write has been attempted.
