# FM Atlas R4.3 — Unified Career Load & Scouting Workspace

## Release intent
R4.3 consolidates the normal user workflow around one **Load Game** action and keeps technical discovery controls out of the main toolbar. It preserves the R4.2 database and read-only reader architecture.

## User workflow
1. Start Football Manager 27 and load the career.
2. Start FM Atlas and press **Load Game** once.
3. FM Atlas reads the currently validated FM27 player and club structures, validates the known Liverpool/Chelsea and Barcola/Palmer witnesses, writes the snapshot to the local SQLite database, and refreshes player search.
4. Search the imported player population. Blank-name search now requests up to 100,000 live records rather than silently limiting the main grid to 5,000.
5. Select a player to see DOB-derived age and any intelligence facts already imported for that exact FM UID.

## What Load Game currently imports
Verified for the user's witnessed FM27 module build:
- Player UID and decoded display name
- Player date of birth when the candidate packed DOB field validates
- Current team/club relationship when the relationship witness resolves
- Club UID and decoded club name
- Import run and source/provenance metadata

The reader remains read-only and rejects an unknown module fingerprint or failed identity witnesses.

## What is not yet universally available
The current full intelligence reader has been validated against a small witness set, not all ~40,000 players. CA/PA, attributes, contracts, staff, finances, history, performance, positions and generated-player status must not be represented as complete merely because a player identity was imported. Existing imported intelligence facts remain linked by FM UID and displayed in profiles. Unknown values remain blank.

## Regen status
R4.3 retains the Yes / No / Either UI and Unknown-by-default storage. Automatic regen classification is not claimed as complete. The next research work is save-specific identity snapshots, career-date validation, ID allocation evidence and independent generation indicators.

## Build
On Windows with .NET 8 SDK:

```powershell
dotnet build .\FmAtlas.sln -c Release
```

Launch:

```powershell
.\src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe
```

## Safety and compatibility
- Existing `%LOCALAPPDATA%\FmAtlas\scouting.db` is retained; schema changes are additive.
- Game process access is read-only (`QueryInformation | VmRead`).
- No game memory writes, save edits or automatic game-file modifications are performed.
- Build verification must be completed on Windows; this package was structurally checked in a non-Windows environment, not compiled here.
