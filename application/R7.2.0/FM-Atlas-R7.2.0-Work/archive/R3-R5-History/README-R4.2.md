# FM Atlas R4.2 — Advanced Player Search & Identity Foundation

## What changed
- FMRTE-inspired, sectioned player-search direction using the exposed concepts from the supplied FMRTE 27 package (advanced-search groups, role/ability weighting configuration and separate player/staff/club search models). No FMRTE code or binaries are copied into FM Atlas.
- Live player database migration adds `DateOfBirth`, `RegenStatus`, `RegenEvidence` and `RegenConfidence` without dropping existing player facts or shortlists.
- Read-only FM27 DOB candidate reader: packed day-of-year/year at player `+0x310`, with `+0x408` only when the documented player-coach vtable is detected (otherwise `+0x310`). Values are accepted only if date components are plausible; 1900 is treated as the empty-date sentinel.
- Age filters now apply to live records when DOB exists. The `Age calculated as of` field should be set to the in-game career date (`yyyy-MM-dd`) for correct save-relative ages.
- Regen filter has the requested `Yes / No / Either` behaviour, visible in the results grid and player profile. `Either` includes all records; Yes and No match only explicit classifications.
- All newly imported live records are initially `Unknown / Unverified` for regen. This is deliberate: the supplied FMRTE package exposes `ngRegens.json` facepack nation/ethnicity mapping, not its internal generated-player detection algorithm. FM27 reverse-engineering research lists UID range, missing real face and a record flag as candidate signals, and specifically says regen detection still needs research. No UID-only guess is made.
- Existing R3.4 attribute facts and source confidence labels remain intact.
- No game memory writes. No save-file writes.

## First test
1. Extract into a new folder, preserving the existing installation and `%LOCALAPPDATA%\FmAtlas\scouting.db`.
2. Run `dotnet build .\FmAtlas.sln -c Release`.
3. Launch the app and import live FM27.
4. Check Age values for known players. Set `Age calculated as of` to the in-game date shown in your career.
5. Confirm `Regen: Either` returns the full player list. Yes/No will remain empty until an independently validated regen classification source is integrated.
6. Import the existing R3.4 report to retain attributes.

## Known boundary
The DOB offsets are current FM27 cross-reference candidates and must be checked against multiple in-game witnesses, including a player-coach. Regen detection is not claimed as complete. The Yes/No/Either query semantics and storage are implemented, but classification population remains a separate verified-data task.
