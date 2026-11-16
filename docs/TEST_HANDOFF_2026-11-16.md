# FM Atlas — First Windows and FM27 test handoff

**Target branch:** `rebuild/fresh-foundation-001`  
**Target build:** Windows 10/11 x64, .NET 8 WPF  
**Live scanner target:** FM27 Game Pass 0.9.2435.0 only  
**Mode:** read-only; no live edit or career-save writes

## 1. Obtain the test build

Use the newest successful **FM Atlas Windows Build** workflow run for the exact branch head.

- Download the artifact named `fm-atlas-win-x64-<run number>`.
- Extract the ZIP fully to a normal folder, for example `C:\FMAtlas\TestBuild`.
- Run `FMAtlas.exe` from the extracted folder.
- Do not use an artifact from an earlier commit.

If the hosted workflow has not produced a green run, the easiest local route is to double-click `Run-FMAtlas-TestBuild.cmd` in the repository root. It builds, tests, packages and launches the newly built app only after success. Or build on the Windows PC from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Test-Package.ps1
```

The script requires the .NET 8 SDK. On success it creates a self-contained ZIP and a text report under `artifacts`. Step-by-step logs are in `artifacts\logs`; automated test results are in `artifacts\test-results`. A failed step stops packaging and leaves its log behind.

## 2. Protect the career first

- Back up the career and create a separate disposable test copy using Football Manager's own Save As workflow.
- Load only that test copy for FM Atlas checks.
- Do not use the only copy of an important career.
- Keep FM Atlas in read-only mode. **Save Live Changes is disabled and must stay disabled.**
- **Save Atlas** only creates/verifies the local FM Atlas database; it does not save changes into Football Manager.

The connector is currently pinned to the Game Pass package 0.9.2435.0. If the PC has a different Game Pass or Steam build, FM Atlas should reject it. Do not bypass that guard or substitute offsets.

## 3. First launch and basic UI

1. Launch FM Atlas before opening the live connection.
2. Confirm the dashboard opens without an error or blank window.
3. Check the sidebar: Dashboard, Players, Staff, Clubs, Save Manager and Settings; Search must remain in the top header, not the sidebar.
4. Check the top header: Quick Search, Advanced Search, Import CSV, Save Atlas, disabled Save Live Changes and Load Game.
5. Resize the window and note whether any panels, controls or profile content clip. The current main window minimum is 1180 × 740 logical pixels.
6. Record the Windows version, screen resolution and display scaling percentage.

## 4. Read-only live career test

1. Start FM27 Game Pass and load the disposable career completely to a stable in-game screen.
2. Ensure there is only one running Football Manager process.
3. Return to FM Atlas and press **Load Game** once.
4. Let the blocking progress panel finish. Do not open/close the game or change saves during the scan.
5. Record the connection result and displayed Player, Staff and Club counts.
6. If loading fails, copy the complete error text and capture a screenshot. Do not repeatedly retry if it reports a read or cleanup failure.

A successful scan means only that the current source mapping completed and the snapshot was indexed. It does **not** prove every field is accurate.

## 5. Quick Search and directories

Test one known example of each:
- A player by full name and a partial name.
- A member of staff by full name.
- A club by full name.

Confirm the correct record type and identity are displayed. Open each result and check that the profile shows the source/evidence labels and does not fill unavailable fields with fabricated values.

Check the Players, Staff and Clubs directory pages. Report missing/duplicate names, zero counts, wrong clubs or incorrect record types.

## 6. Advanced Search acceptance cases

Use a known player or a small set of players whose values you can see in Football Manager. Try these separately:

| Test | Example input | Expected meaning |
|---|---|---|
| Minimum only | Dribbling min 15; max blank | Dribbling >= 15 |
| Maximum only | Pace min blank; max 10 | Pace <= 10 |
| Bounded range | Strength 12–16 | 12 <= Strength <= 16 |
| Combined AND | Dribbling >= 15 and Pace >= 15 | Both conditions must match |
| Position familiarity | DC min 15; max blank | Centre-back familiarity >= 15 |
| All / Any positions | Choose two positions, change All to Any | AND / OR selection changes results appropriately |
| Clear filters | Set several criteria, then Clear | All criteria reset |

Try the Player, Coach and Club entity scopes and change filter categories. Report any field that is missing, enabled despite no data, has the wrong control, or returns inconsistent results. Some fields are intentionally marked In Progress until their importer exists.

## 7. Profile and visual checks

Open one known player, staff member and club.

- Player: compare name, age, club, CA, PA, position, visible attributes, goalkeeper mode where relevant, hidden/personality/foot values and preferred moves when populated.
- Staff: compare name, role/job, club and the available coaching attributes.
- Club: compare name, nation, competition, stadium, capacity, reputation and the linked player/staff lists when populated.
- Portraits/badges: check whether the correct local graphics resolve. A missing image is useful feedback; do not manually change the game graphics folder for this test.

Capture screenshots of the dashboard, Advanced Search, one player profile, one staff profile and one club profile. Compare at the actual Windows scaling with the approved REF-001 to REF-004 designs.

## 8. Send back this report

Copy and fill this section. Screenshots and error text are more useful than sending the career file.

- FM Atlas commit / artifact run:
- Windows edition, version and build:
- Display resolution / scaling:
- FM27 storefront:
- FM27 package version (if known):
- Live Load result:
- Player / Staff / Club counts:
- Quick Search result:
- Advanced Search cases passed / failed:
- Player CA/PA/attribute comparison:
- Staff comparison:
- Club and membership comparison:
- Graphics result:
- UI clipping / layout issues:
- Exact error text:

Attach the generated `FMAtlas-Test-Report-*.txt` and the per-step logs if the local build script was used. Do not send a career save, account credentials, or licensed facepack/logo files.

## What is deliberately not testable yet

- Live editing and Save Live Changes.
- Saving FM career data from FM Atlas.
- Newgen classification from the live scanner.
- Full competitions, nations, fixtures, results, tactics, training, injury and transfer history data.
- Profile fields whose data family is not yet imported or read-verified.
- Unregistered FM builds and Steam builds.

These remain locked or marked In Progress until source coverage, field accuracy and safety checks are evidenced.
