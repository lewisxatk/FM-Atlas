# FM Atlas 27 — Windows Build and Acceptance Checklist (7.1.0)

## Build and first launch
- [ ] Windows 10/11 x64 with .NET 8 SDK installed (`dotnet --list-sdks` shows 8.x)
- [ ] Extracted complete ZIP to a writable folder
- [ ] `Build-FM-Atlas-27.bat` completes successfully with zero errors
- [ ] `build-logs\latest-build.log` exists and contains the complete build output
- [ ] `FmAtlas.exe` starts and shows the Aurora Workbench shell
- [ ] App starts after closing/reopening; no unhandled exception is added to `%LOCALAPPDATA%\FmAtlas\logs\fm-atlas.log`

## Existing workspaces
- [ ] Dashboard opens
- [ ] Global Search, Players, Staff and Clubs return results
- [ ] Player detail and position suitability displays available source data without inventing missing fields
- [ ] Recruitment, Squad Architect, Transfers, Tactical Lab and Shortlists open and respond
- [ ] Import a COPY of an FM27 career save; inspect import summary and warning counts

## Analytics and role fit
- [ ] Analytics discovers numeric fields from the latest import
- [ ] Raw metric search returns player records with matching UIDs
- [ ] Per-90 options are shown only for applicable numeric metrics
- [ ] Per-90 search excludes records below the selected minutes threshold
- [ ] Compare at least five per-90 values with FM27 Data Hub for the same season/player and record differences
- [ ] Missing/unrecognised minutes fields do not produce a per-90 result
- [ ] Role Lab returns scores only when at least 50% of requested attributes are available
- [ ] Role Lab shows coverage and exports a valid CSV
- [ ] Compare a sample of role-fit results with in-game positional familiarity and your own tactical expectations

## Local editor safety
- [ ] Editor Studio starts OFF after fresh launch
- [ ] Mass Edit is locked while editor is OFF
- [ ] Enable editor and change one local attribute; verify Edit History row appears
- [ ] Set operation accepts 0–20 and rejects out-of-range values
- [ ] Add modifies existing local values, skips missing values and clamps to 0–20
- [ ] Scale multiplies existing values by (1 + percentage/100), skips missing values and clamps to 0–20
- [ ] Set/Add/Scale mass-edit changes each create individual audit entries
- [ ] Undo reverses latest edit successfully
- [ ] Undo refuses stale edit if the value has changed since the history row
- [ ] Close/reopen app; editor returns to OFF
- [ ] Confirm no FM career save or live-game value has been changed

## Tester & Logs
- [ ] Quick checks show database/log/Python bridge/process/snapshot status
- [ ] Store profile appears as not reliably distinguished (expected until adapter validation)
- [ ] Copy safe diagnostics works and omits save filename/player list by default
- [ ] Copy recent application log works
- [ ] Support ZIP opens and contains report/log/readme but not career save or SQLite DB
- [ ] Optional personal-detail checkbox is only enabled knowingly; inspect before sending

## Critical limitations
- [ ] Do not treat the live identity count as full player-profile coverage
- [ ] Do not attempt live memory writes or assume Game Pass and Steam layouts are interchangeable
- [ ] Do not treat the Role Fit score as the SI formula or in-game star rating
- [ ] Do not assume every xG/xA/defensive/pass metric is present in the save reader
- [ ] Keep the original career save unchanged; test with a copy

## When reporting a problem
Send the following where relevant:
1. `build-logs\latest-build.log` for a compile/publish failure.
2. A redacted diagnostics text or Tester & Logs support ZIP for runtime problems.
3. The page name, action performed, expected result and actual result.
4. A screenshot of the error or UI problem (without private details you do not wish to share).
