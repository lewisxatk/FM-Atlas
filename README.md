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

## Build and test on Windows

Requires Windows 10/11 x64 and the .NET 8 SDK.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1
```

The script performs restore, Release build, the full solution test suite and a self-contained Windows x64 publish. It stops at the first failed step, writes a separate log for each .NET step under `artifacts\logs`, stores TRX test results under `artifacts\test-results`, and creates a timestamped ZIP plus a text test report only after every step passes. The ZIP includes the runtime and does not require a separate .NET Desktop Runtime installation.

GitHub Actions cross-builds the Windows WPF target on a Linux hosted runner using `EnableWindowsTargeting=true`, runs the automated tests, and publishes a self-contained Windows x64 ZIP. This avoids the current Windows hosted-runner allocation failure. Cross-compilation and unit tests do not replace launching the WPF app on Windows or validating the live FM27 connection. Artifacts and diagnostics are retained for 14 days.

## First user test

Follow `docs/TEST_HANDOFF_2026-11-16.md`. It explains how to obtain the Windows test package, safely prepare a disposable FM27 career, exercise Load Game, Quick Search, Advanced Search and the profile workspaces, and report results without sharing a career-save file.

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

The main header includes **Import CSV**. It accepts a UTF-8 player-list CSV with a `Player` or `Name` column and a stable `Unique ID` column. It maps supported player identity, personal, club, attribute, foot and other values into local typed snapshots. Additional columns are retained and indexed; consistently numeric/date/Yes-No columns receive numeric/date/Boolean search controls, while mixed or ambiguous columns remain text.

Re-importing the exact same file is idempotent. Different export files are kept in separate local provider namespaces so one export cannot silently overwrite another. Imported fields are marked **Game Export / Mapped**, not live-memory verified. No Football Manager process or career file is read or changed by this action.

The FM View Exporter reference documents English comma-separated CSV and German semicolon-separated CSV with decimal commas. Its current README documents FM27 on Steam for Windows; Game Pass compatibility is not claimed. FM Atlas can ingest a compatible CSV from any export route that provides the required headers. See `research/CSV_IMPORT_001.md` for the precise field and safety boundaries.

## FM27 Game Pass live connection (read-only first pass)

The **Load Game** button has an exact-build live connection path for FM27 Game Pass package **0.9.2435.0**. It checks the x64 `game_plugin.dll` fingerprint, opens a read-only process handle, reads the live Player/Staff/Club lists, imports typed snapshots into SQLite and makes the records available to Quick Search and Advanced Search. Other Game Pass/Steam builds are rejected by this scanner until a separate profile and verification exist.

During loading, a centered animated progress panel blocks the workspace and owned search/profile windows. It reports the current scan/index stage and returns control after success or failure. The connector monitors FM's save-generation counter and removes the active live snapshot when the process closes, the career is switched, or the live read stops validating.

**Important:** this is a read-only implementation, not a completed live editor. The process accessor has no write operation and **Save Live Changes** remains disabled until field-by-field write/read-back/undo checks pass on a disposable career. The build profile is still externally referenced rather than Atlas-read-verified; the live scanner has not yet passed a Windows build or in-game test. See `research/LIVE_GAME_CONNECTION_001.md` for fields, profile fingerprint, safety boundaries and outstanding verification.

## Save buttons — what they do

- **Save Atlas** initialises and verifies the local FM Atlas SQLite database. It does not save or modify the Football Manager career.
- **Save Live Changes** is intentionally disabled. No live process write primitive is present.
- To save a Football Manager career, use Football Manager's own save command. FM Atlas will not claim that a live edit is persisted to the career until write verification and an in-game save/reload test exist.
