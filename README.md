# FM Atlas — Fresh Rebuild

A Windows desktop application for Football Manager scouting, live-save intelligence and (in a later, separately verified stage) editing.

**Current development branch:** `rebuild/fm27-0.9.2562-readonly-profile`  
**Architecture:** .NET 8 WPF, modular Domain / Application / Infrastructure / Desktop / Tests.  
**Design authority:** `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md`.

## Current implementation

- Dashboard plus Players, Staff, Clubs, My Career, Save Manager and Settings navigation.
- Global Quick Search and a modal, typed Advanced Search dialog.
- Player, Staff and Club directories with closable in-app profile tabs.
- Player attribute catalogue, visible/hidden/personality/foot ratings and CA/PA fields.
- Club Squads and Staff lists joined from imported numeric club IDs.
- CSV player import into local, provider-scoped SQLite storage.
- Build-specific, read-only FM27 Game Pass live scanner profiles for packages 0.9.2524.0 (27.1.0) and 0.9.2562.0 (27.1.1).
- Memory reads check that the requested range is committed and readable using `VirtualQueryEx` before `ReadProcessMemory`; unreadable/guarded ranges stop the scan.
- A pre-import layout validator checks Player/Staff/Club identities, unique UIDs, names, CA coverage, position ratings, natural-position ratings and player-to-club UID joins.
- A local diagnostic log records process/module fingerprint, scan stage, read counts, validation outcomes and errors without recording player names, UIDs or raw career data.

The app is a functional workspace foundation, not yet a complete FM editor. Some profile sections remain empty until their source data is imported. Search only returns records and fields present in the local index and supported by the typed query provider.

## Live connection — FM27 27.1.1 first-user validation

**The 27.1.1 Game Pass profile now has a guarded, read-only scanner path.** This is the change added on `rebuild/fm27-0.9.2562-readonly-profile`. It removes the previous hard-coded 0.9.2524-only rejection, but it does not mark the new fields as Atlas-verified.

Exact 27.1.1 module identity from the user's uploaded DLL:

| Property | Value |
|---|---:|
| Package | `0.9.2562.0` |
| ProductVersion | `27.1.1+2511875` |
| PE timestamp | `0x6AFF583B` |
| SizeOfImage | `0x17624000` |
| Architecture | x64 |

Build-specific module-relative roots used for the guarded read-only probe (from the published FMLibreEditor profile for Game Pass 0.9.2562.0):

| Structure | RVA |
|---|---:|
| Players array | `0x53F8EA8` |
| Staff array | `0x53F8EF0` |
| Clubs array | `0x53F9060` |
| GAME_UI static | `0x53F9A60` |
| In-game date | `0x539BD10` |
| GAME_UI saves-loaded field | `0x100` |

The external profile describes the entity-object layout as unchanged from 0.9.2524.0. FM Atlas therefore reuses its existing read-only entity parser but supplies these build-specific roots from the matched profile. It does not use the 27.1.0 root addresses on 27.1.1.

**Evidence boundary:** the current 27.1.1 profile remains `ExternalReferenceOnly`. FMLibreEditor lists Game Pass 0.9.2562.0 among versions checked by its project; its profile notes 12 checks passed on a separate loaded save. This is useful external evidence, not proof that FM Atlas has validated every field in the user's career. The connector continues to label imported fields `Mapped`, not `ReadVerified`. The scanner is read-only and does not request process-memory write permissions.

The first live test should use a manually saved disposable/offline career. Check that player, staff and club data appears and compare sample identities, club links, CA/PA, visible attributes and in-game date against FM27. Stop if counts or identities look wrong and send the diagnostic log.

The connector writes diagnostics to `%LOCALAPPDATA%\FM Atlas\Logs\live-connection.log`. The log records scan stages, module fingerprint, read counts and validation/error messages; it does not write player names, UIDs or raw career data.

## Live editing and saving boundary

- The live connector is **read-only**. It does not request `PROCESS_VM_WRITE` or `PROCESS_VM_OPERATION`; it has no memory-write or process-termination API.
- **Save Live Changes** remains disabled. No field has `WriteVerified` status.
- **Save Atlas** initializes/verifies the local SQLite database. It does not save or modify the Football Manager career.
- Do not treat a successful snapshot or matching fingerprint as proof that all fields are accurate.
- Live editing must wait until each field has a build-specific mapping, identity/save-generation checks, a pre-write value check, read-back verification and tested rollback on a disposable career.

## Windows build verification

Build 1068 (27.1.1 read-only profile integration):
- Windows Actions run [#45614077051](https://github.com/lewisxatk/FM-Atlas/actions/runs/45614077051).
- **126 automated tests passed; 0 failed.**
- Restore, Release build, test execution and self-contained Windows x64 packaging all completed successfully.
- WPF process-start smoke test passed: `FMAtlas.exe` remained running for eight seconds on the hosted Windows runner.
- ZIP SHA-256: `9E8ADCE304B93A922E6306B4E7FDA06EA858569C8A2E3EE88450F393E23045AA`.
- This verifies compilation, the automated test suite, packaging and basic startup. It does not validate the user's actual live FM27 27.1.1 career.

## UI status — not yet accepted as complete

The WPF source contains parts of the approved black / warm off-white / orange visual system, a compact left navigation, a global top search/action header, player-profile panels and a typed Advanced Search dialog. The approved design is **not** accepted as fully implemented: there has been no side-by-side Windows screenshot comparison against all supplied reference screens, several navigation destinations are still placeholders/deferred, and the user reported that the last package did not show the supplied redesign.

See `docs/UI_IMPLEMENTATION_AUDIT_2026-11-25.md` for the verified source audit and the remaining visual acceptance work. Do not describe a successful build or eight-second startup as proof that the redesigned UI has been delivered.

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
- `research/FM27_GAMEPASS_27.1.1_BUILD_0.9.2562.0.md` — user's binary fingerprint and external build profile mapping.
- `research/FMRTE_FEATURE_AUDIT_001.md` — player/staff/club profile and search coverage.
- `research/FIELD_COVERAGE_LEDGER.md` — field status and evidence.
- `docs/UI_IMPLEMENTATION_AUDIT_2026-11-25.md` — current visual implementation and acceptance gaps.

Historical R7.2.0 source remains in `recovery/r7.2.0-source-import` as research/reference material; it is not the fresh application's code foundation.
