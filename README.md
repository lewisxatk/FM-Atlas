# FM Atlas — Fresh Rebuild

A clean Windows desktop application for Football Manager 27 editing, save intelligence and player data.

**Current branch:** `rebuild/fresh-foundation-001`  
**Architecture:** .NET 8 WPF, modular Domain / Application / Infrastructure / Desktop / Tests.  
**Design authority:** `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md`.

## Current state

The fresh WPF solution and initial application shell are being established. The first shell includes:
- compact left navigation (no Search item);
- top Quick Search placeholder and Advanced Search action;
- compact Save Atlas / Save Changes controls;
- prominent Load Game action on the right;
- black/off-white/orange design tokens;
- honest disconnected and In Progress states;
- read-only process presence check only.

No player records, attributes, career data or successful edits are fabricated. The first Load Game action only checks whether the FM process and `game_plugin.dll` can be detected; it does not read memory or load a career.

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
