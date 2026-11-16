# FM Atlas — Fresh Rebuild

A clean Windows desktop application for Football Manager 27 editing, save intelligence and player data.

**Current branch:** `rebuild/fresh-foundation-001`  
**Architecture:** .NET 8 WPF, modular Domain / Application / Infrastructure / Desktop / Tests.  
**Design authority:** `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md`.

## Current state

The fresh WPF application now has a functional workspace foundation:
- compact left navigation for Dashboard, Players, Staff, Clubs, Save Manager and Settings (no Search item);
- top Quick Search, modal FMRTE-style Advanced Search, Import CSV and prominent Load Game action;
- closable in-app workspace tabs for Player, Staff, Club and directory views;
- recently opened profile shortcuts on the dashboard;
- player profile sections, including Technical / Goalkeeper attribute mode, CA/PA, attributes, positions, preferred moves and source/evidence labels;
- dedicated Staff and Club profile workspaces with the sections from the FMRTE feature audit;
- automatic lookup of player portraits and club badges from the standard FM27 graphics folder when a valid `config.xml` mapping exists;
- a blocking animated live-load panel with stage/progress information;
- an exact-fingerprint FM27 Game Pass 0.9.2435.0 read-only scanner for Player, Staff and Club snapshots.

**Important current boundary:** the live reader remains externally mapped and has not been validated against a running user's FM27 career. Save Live Changes is still disabled. Several profile sections are structured and searchable but show a clear “not imported” state until their underlying game data is mapped. No current Windows build or in-game runtime test has passed yet.

## Build on Windows

Requires .NET 8 SDK and Windows 10/11 x64.

```powershell
dotnet restore FMAtlas.sln
dotnet build FMAtlas.sln --configuration Release
dotnet test FMAtlas.sln --configuration Release
```

GitHub Actions runs the same restore/build/test sequence on Windows.

## Product and technical references

- `docs/FRESH_REBUILD_MASTER_PLAN.md` — product scope and milestones.
- `docs/FRESH_REBUILD_CHECKLIST.md` — task tracker.
- `docs/CURRENT_CHECKPOINT.md` — current resumable state.
- `docs/SESSION_CONTINUITY.md` — interruption recovery rules.
- `docs/ARCHITECTURE_DECISION.md` — clean application architecture.
- `research/UPLOADED_RESEARCH_INVENTORY.md` — uploaded package hashes and contents.
- `research/RESEARCH_FINDINGS_001.md` — initial technical research evidence.
- `research/FMRTE_FEATURE_AUDIT_001.md` — official FMRTE 27 player/staff/club tab and tool coverage.
- `research/FIELD_COVERAGE_LEDGER.md` — attribute groups, field counts and verification status.
- `docs/UI_REVIEW_001.md` — review of the current shell, Advanced Search, player/staff/club profiles, graphics and outstanding design/build gates.

The historical R7.2.0 source is preserved separately in `recovery/r7.2.0-source-import` and is not the new application's code foundation.


## Importing player data for a first test

The main header includes **Import CSV**. It accepts a UTF-8 player-list CSV with a `Player` or `Name` column and a stable `Unique ID` column. It maps the supported player identity, personal, club, attribute, foot and other values into local typed snapshots. Additional columns are retained and indexed; consistently numeric/date/Yes-No columns receive numeric/date/Boolean search controls, while mixed or ambiguous columns remain text.

Re-importing the exact same file is idempotent. Different export files are kept in separate local provider namespaces so one export cannot silently overwrite another. Imported fields are marked **Game Export / Mapped**, not live-memory verified. No Football Manager process or career file is read or changed by this action.

The FM View Exporter reference documents English comma-separated CSV and German semicolon-separated CSV with decimal commas. Its current README documents FM27 on Steam for Windows; Game Pass compatibility is not claimed. FM Atlas can ingest a compatible CSV from any export route that provides the required headers. See `research/CSV_IMPORT_001.md` for the precise field and safety boundaries.


## Build, test and package locally on Windows

The repository includes a one-command PowerShell script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1
```

It performs .NET restore, Release build, the full solution test suite and a Windows x64 desktop publish. It stops immediately on a failed step and does not create a package unless restore, build and tests have passed. The timestamped output is placed under `artifacts\`. The package is framework-dependent and requires the .NET 8 Windows Desktop Runtime.

Do not use an older artifact after changing source; build and test the exact branch head you intend to try.


## FM27 Game Pass live connection (read-only first pass)

The **Load Game** button now has an exact-build live connection path for FM27 Game Pass package **0.9.2435.0**. It checks the x64 `game_plugin.dll` fingerprint, opens a read-only process handle, reads the live Player/Staff/Club lists, imports typed snapshots into SQLite and makes the records available to Quick Search and Advanced Search. Other Game Pass/Steam builds are rejected by this scanner until a separate profile and verification exist.

During loading, a centered animated progress panel blocks the workspace and owned search/profile windows. It reports the current scan/index stage and returns control after success or failure. The connector monitors FM's save-generation counter and removes the active live snapshot when the process closes, the career is switched, or the live read stops validating.

**Important:** this is a read-only implementation, not a completed live editor. The process accessor has no write operation and **Save Live Changes** remains disabled until field-by-field write/read-back/undo checks pass on a disposable career. The build profile is still externally referenced rather than Atlas-read-verified; the live scanner has not yet passed a Windows build or in-game test. See `research/LIVE_GAME_CONNECTION_001.md` for fields, profile fingerprint, safety boundaries and outstanding verification.
