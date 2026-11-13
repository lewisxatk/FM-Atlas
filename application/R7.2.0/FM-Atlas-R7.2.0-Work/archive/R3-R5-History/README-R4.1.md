# FM Atlas R4.1 — Player Intelligence Bridge

## Purpose

R4.1 carries the existing R3.4 full-player-intelligence scanner into the main R3.7 scouting application and connects its decoded fields to live player profiles. It is an integration milestone, not a UI redesign.

## What is included

- R3.7.2 visible player-search controls and fixes.
- R3.8 candidate FM27 module fingerprint and player/club array locations:
  - module SHA-256: `252FF8FB5B7A4A5A5464DB6BA632E309EEF9B6E65FFA992AE20575A189E66C6A`
  - player array pair: `0x57057D0 / 0x57057D8`
  - club array pair: `0x5705988 / 0x5705990`
- Existing R3.4 full intelligence scanner (attributes, positions, CA/PA, reputation, contract evidence and medical raw probes).
- New **Import report to profiles** action:
  - accepts an R3.4 full-intelligence CSV;
  - matches rows strictly by FM player UID against already imported live players;
  - checks that the report module fingerprint matches the latest completed live import;
  - stores decoded facts and the original confidence/provenance label in `LivePlayerFacts`;
  - does not create identities or guess values.
- Selected live-player profile now shows imported technical, mental, physical, goalkeeper, hidden, positional and ability facts.
- Existing SQLite database schema is extended in place. Existing shortlist and player records are retained.

## Important data limitations

The R3.4 scan is the real extraction pipeline already developed in the previous milestone. It is not a fully verified universal attribute map. Many attribute offsets remain cross-reference candidates and appear with their confidence label. CA/PA, reputation and some contract values have separate evidence labels. Medical records are raw discovery evidence only. Match fitness, sharpness and fatigue remain unmapped.

R4.1 does not enable writing to FM27 memory or the save file. The local editor remains a local-database feature only.

## Install/build

1. Keep your existing FM Atlas folder and `%LOCALAPPDATA%\FmAtlas\scouting.db` untouched as a fallback.
2. Extract this ZIP to a **new folder**.
3. Open PowerShell in the extracted `FmAtlas-R3.7-Unified-CoachPro` folder.
4. Run:

```powershell
dotnet build .\FmAtlas.sln -c Release
```

5. Launch:

```powershell
.\src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe
```

The project targets Windows WPF and .NET 8. Build verification must be run on your Windows machine; this development environment does not have the .NET SDK.

## First real test — one connected workflow

1. Start FM27 and load the same career/save you want to scout.
2. In FM Atlas choose **Import live FM27**. The reader stops if its module fingerprint or Liverpool/Chelsea and Barcola/Palmer relationship witnesses fail.
3. Choose **Scan player intelligence** and enter the four existing witnesses: Alisson, Bradley Barcola, Cole Palmer and Leon Goretzka. Save the CSV.
4. Choose **Import report to profiles** and select that freshly generated CSV.
5. Select one of those players. The profile should now show the imported attributes grouped by category and retain the field confidence labels.

The report import intentionally rejects a CSV captured from a different module build. If the live import is from one FM build and the report from another, scan again on the current build.

## What to send back

Send the build output and one screenshot of a selected player profile after importing the report. That is enough for the first validation round; no separate diagnostic export is requested.
