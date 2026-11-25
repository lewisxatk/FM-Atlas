# FM Atlas — FM27 Game Pass Live Connection 001

**Date:** 2026-11-16  
**Branch:** `rebuild/fresh-foundation-001`  
**Current scanner target:** FM27 Game Pass / Microsoft Store package `0.9.2435.0`  
**Status:** Read-only connector and typed import path committed. Windows build, first real Game Pass session and field-by-field comparison remain outstanding. Live writes are deliberately disabled.

## 1. External reverse-engineering basis

The scanner was developed from publicly documented FM27 layouts and build notes, not from invented offsets:

- FMLibreEditor Game Pass build profile: `profiles/fm27/gamepass-0.9.2435.0.toml`
- FMLibreEditor FM27 edition layout: `profiles/fm27/layout.toml`
- FMLibreEditor relation list handling: `crates/fmlibre-core/src/relations.rs`
- FMSuperScout FM27 person gender flag notes: `core/Fields.cs`

Useful source links:
- https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/gamepass-0.9.2435.0.toml
- https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/layout.toml
- https://github.com/tozmoz/FMLibreEditor/blob/main/crates/fmlibre-core/src/relations.rs
- https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs

These repositories are external references. No source code or binaries have been copied into FM Atlas. The reader contains an independent .NET implementation of selected publicly documented field locations and defensive checks. A documented offset is not the same as a field verified by FM Atlas against the user's installed build.

## 2. Build fingerprint gate

The scanner accepts only the exact x64 module fingerprint:

| Field | Required value |
| --- | --- |
| Storefront | Xbox / Microsoft Store (Game Pass) |
| Package | `0.9.2435.0` |
| PE machine | `0x8664` (x64) |
| PE timestamp | `0x6AE8CF77` |
| SizeOfImage | `0x17969000` |

The reference catalogue also contains Game Pass `0.9.2409.0`, but the current reader does not scan that package. A matching fingerprint on its own does not make a build Atlas-verified. The 0.9.2435.0 profile has an explicit **read-only probe permitted** state; `CanReadLiveData` remains false until FM Atlas has completed its own runtime comparison. `CanWriteLiveData` remains false.

Any unknown, different, x86 or mismatched package is rejected before a process memory read.

## 3. Process access and session identity

- Finds the FM process by executable process name and refuses ambiguity when more than one FM process exists.
- Locates `game_plugin.dll` in the process module list.
- Reads the on-disk PE fingerprint and checks the exact supported profile.
- Opens the process with `PROCESS_VM_READ | PROCESS_QUERY_INFORMATION` only.
- The process accessor contains `ReadProcessMemory`; it has no write method and does not request `PROCESS_VM_WRITE` or `PROCESS_VM_OPERATION`.
- The live provider identity is isolated as `live-fm27-gamepass-0.9.2435.0`. CSV datasets use separate provider namespaces and are not removed when the active live dataset is refreshed.

### Save-generation invalidation

The FM27 Game Pass profile documents the `GAME_UI` object at module RVA `0x53B4160`; its u64 value at `+0x100` counts how many saves the process has loaded since it started.

FM Atlas captures this generation value before scanning, checks it after record extraction, checks it again after the local SQLite import, and rechecks it every two seconds while the live session is active. If the game exits, the player array empties, the save-generation counter changes, or the memory probe fails:

1. The live session is invalidated.
2. The read handle is closed.
3. The live provider rows are deleted from the Atlas database.
4. The UI tells the user to press **Load Game** again.

The app also removes the transient live provider at startup. This prevents an earlier session from being presented as the current career after an app restart.

## 4. Entity arrays and current mapped data

The reader uses the documented module-relative array pairs for:
- Players: RVA `0x53B7DE0`
- Staff: RVA `0x53B7E28`
- Clubs: RVA `0x53B7F98`

It validates pointer ranges, array alignment and an entity-count ceiling before bulk-reading entries. It bulk-reads player and staff objects to reduce individual process-memory calls. A malformed or inaccessible record is skipped where safe; if the resulting player, staff or club set is incomplete, the scan is discarded instead of presenting an incomplete live dataset as a successful load.

### Player records

Current mapped values include:
- UID, name/common name, date of birth, age, nationality, secondary nationality, gender, birthplace city;
- current club, team kind (First Team / U21 / U18 when the documented code matches), competition/league name and primary position;
- height, CA, PA, transfer value, wage and contract expiry;
- visible technical, mental, physical and goalkeeping attributes;
- five hidden attributes, eight personality ratings, left/right foot and 15 positions;
- all 64 documented preferred-move flags;
- international caps/goals and U21 caps/goals;
- home/current/world reputation, condition, match sharpness, jadedness and morale;
- asking price, squad status/number, transfer-list and loan-list flags, contract type, loyalty bonus, contract/playing-time happiness, contract signed/joined dates;
- source-confirmed Newgen flag;
- secondary nationality, trained-in nations, trained-at clubs and work-permit nation relationships where the relationship list validates.

Attribute values are converted from FM's raw 0–100 representation to the displayed 1–20 scale. Missing or invalid fields remain absent; they are not converted to zero.

### Staff records

Current mapped values include:
- UID/name, DOB/age, nationality/gender, club, primary job/role code;
- CA/PA, wage, contract expiry/signed/joined dates and contract type;
- documented coaching, mental, knowledge/medical ratings;
- authority, determination, motivating, people management and working with youngsters where their source fields validate;
- personality ratings and home/current/world reputation.

### Club records

Current mapped values include:
- UID/name/short name, nation, competition, founding year and reputation;
- women's-club flag;
- training/youth facilities and corporate facilities where values validate;
- senior team, stadium name/capacity/seating/used/expansion;
- supporter mix and supporter-trait ratings;
- season-ticket count and social-media follower count;
- balance, transfer budget, wage budget, total wage budget and average ticket price only when the finance record is present and each value passes its range/type check.

Additional fields are projected as typed values into the Indexed Fields search catalogue. They remain evidence-bearing `LiveGameMemory / Mapped` values, not `ReadVerified`.

## 5. Search and local persistence

- Live records for Player, Staff and Club are turned into typed domain snapshots.
- The entire set is saved through `IEntityRecordBatchSnapshotStore.SaveSnapshotsAsync` in one SQLite transaction.
- The same transaction refreshes the normalised `field_values` projection; each field retains source kind, evidence status, source version, target fingerprint and observation time.
- Existing Quick Search and FMRTE-style Advanced Search query the same local index. Additional numeric, Boolean, date and text fields are available through the Indexed Fields category when a consistent typed mapping exists.
- The global Include women preference is applied to both Quick Search and Advanced Search. The player Gender value comes from the documented person gender flag.
- The player profile handoff can load a typed live Player snapshot by its provider-scoped UID.

The live import is a snapshot. It is not a live-updating mirror of every in-game change while the career remains open. Reload the snapshot with **Load Game** to refresh; the monitor currently checks career identity/generation rather than polling every player field.

## 6. Animated loading lock

**Load Game** now starts the asynchronous live connection/import path instead of the old process-presence probe.

The WPF overlay:
- covers the main workspace and owned Advanced Search/profile windows;
- captures mouse hit-testing and keyboard focus for the duration of the load;
- displays a centered compact panel with a rotating dashed ring, live stage/detail, progress bar and percentage;
- reports build-check, entity scanning and database indexing stages;
- closes after success or failure and then resumes Quick Search.

The scan itself runs off the UI thread. SQLite save uses asynchronous I/O. This keeps the window animation responsive while the data is imported and prevents users from triggering search or edit actions against a half-loaded database.

## 7. Important current limitation — live editing and saving

The current connection is **read-only**.

The main UI still has a disabled **Save Live Changes** action. The scanner does not open a write handle. No field is marked `WriteVerified`. The exact external layout is a useful discovery basis, but it is not enough to safely enable editing in FM Atlas.

Before live writes can be enabled, each supported editable field needs:
1. an exact build-specific read/write mapping and allowed value range;
2. identity and active-save generation revalidation immediately before the write;
3. a pre-write read of the current value;
4. a guarded write operation with an explicit field allow-list;
5. immediate read-back comparison and an operation result;
6. a safe inverse/rollback path where the field supports one;
7. confirmation in a disposable career and comparison against the FM UI.

Until those gates pass, FM Atlas will not modify the live process or claim that edits are saved. Search and read-only profiles can be tested first; writing every field is a later acceptance stage.

## 8. Verification status and next work

- Source contracts, reader, connector, SQLite batch importer, save-generation monitor, dashboard binding and blocking animated loader are committed.
- Regression tests cover atomic Player/Staff/Club snapshot import, provider-specific stale-data cleanup, invalid-batch rollback, preferred-move querying, dynamic club field discovery and external-build probe permissions.
- **These tests have not been executed** in this environment. No .NET SDK is installed here, and recent GitHub Actions jobs have failed before allocating a runner or creating step logs.
- No Windows executable or installer has been produced or verified for this change.

Next:
1. Run the .NET 8 Windows restore/build/test script on the current branch head.
2. Correct any compile/XAML/test issues found by the actual runner.
3. Perform the first live Game Pass test with FM27 package 0.9.2435.0 and compare counts/identity/attributes against the in-game UI and independent reference tools.
4. Add missing fields and validate the first read result before marking the build `AtlasReadVerified`.
5. Begin the guarded writer only after the read mapping is verified. Keep **Save Live Changes** disabled until the write and read-back/undo tests pass.

**Safety boundary:** no live process writes and no modification of the user's career file have been performed by FM Atlas.


## 9. Club membership joins now exposed in Club profiles

The Player and Staff readers now expose the club object's numeric UID as an indexed integer field:

- Player: current team pointer -> club pointer -> UID, indexed as `club_uid`.
- Staff: contract pointer -> team pointer -> club pointer -> UID, indexed as `club_uid`.

The Club profile uses the existing Advanced Search index to query exact equality against the Club record's numeric source ID:
- **Squads** queries `SearchEntityKind.Player`.
- **Staff** queries `SearchEntityKind.Coach` (mapped internally to Staff).
- Both are limited to 200 matching entities, show a clear empty/error state and open the corresponding full profile on double-click.
- The query requires a uniquely typed `IntegerRange` `club_uid` field discovered in the current local player/staff index. It does not match on club names.

A regression test inserts Player and Staff records for two different club IDs, then verifies that the Club 77 lookup returns only the Player and Staff records for 77, not the records for 88.

These join fields remain `LiveGameMemory / Mapped`. The SQLite/test verifies batch projection and the exact query behaviour with representative values; only a live comparison against the active FM27 Game Pass career can elevate them to `ReadVerified`.


## 10. 25 November 2026 update — user's FM27 27.1.1 Game Pass package

The original sections above are a historical record of the 0.9.2435.0 investigation. The active user target is now:

| Field | Value |
|---|---|
| Storefront | Microsoft Store / Xbox Game Pass |
| Package | `0.9.2562.0` |
| Product version | `27.1.1+2511875` |
| PE machine | `0x8664` |
| PE timestamp | `0x6AFF583B` |
| SizeOfImage | `0x17624000` |
| SHA-256 | `814f0190ea241710e0780bbff6589cc8137e6837c48cc25f868b1b676008242a` |

The exact profile was added via PR #12. The profile's static roots are the 27.1.1 values recorded in `FM27_GAMEPASS_27.1.1_BUILD_0.9.2562.0.md`. The 27.1.1 reader shares the 0.9.2524.0 object layout with documented exceptions. The corporate-facilities field now uses the build-specific `0x8AD` offset; the old `0x8A9` value is retained only as the default for older reference profiles.

Build 1062, previously given to the user for testing, predates the 27.1.1 profile integration and therefore could not establish 27.1.1 live compatibility. Build 1100 includes the profile and the field-offset correction and passed 126/126 automated tests, Release build, self-contained Windows x64 packaging and WPF startup smoke test. These are software/build checks only; no successful live read from the user's career has yet been witnessed.

The user should test Build 1100 with FM27 fully loaded to a career. If the live load fails, capture `%LOCALAPPDATA%\\FM Atlas\\Logs\\live-connection.log` and the generated diagnostics artifact. The exact failure stage, process fingerprint, record counts and read errors are required to identify the next blocker.

**Safety:** the process reader remains read-only. `CanReadLiveData` and all write capability gates remain fail-closed until a complete user-save comparison is recorded. Do not use a profile match alone to enable live editing.
