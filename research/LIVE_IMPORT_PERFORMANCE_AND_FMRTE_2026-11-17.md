# FM Atlas — Live Import Performance and FMRTE Reference Review

**Date:** 2026-11-17  
**Target:** FM27 Game Pass 0.9.2435.0  
**Status:** Source-level bottleneck identified; batched SQLite importer implemented; Windows performance measurement still required.

## Screenshot finding

The user's first real career test showed an import message for **92,683 live records** while the overall progress display stayed at 96% and the detail line showed only 2%. The 96% was the connector's fixed start-of-index progress; the 2% was the importer's separate sub-progress. It was not a reliable indication that the whole task was 96% complete.

## Source-level bottleneck

The live reader already reads the player, staff and club pointer arrays in bulk and creates typed records before persistence. The expensive stage was the batch snapshot writer:

- It previously projected and JSON-serialised every record into a second, fully materialised in-memory list before starting database writes.
- For every record it then ran separate SQLite operations for the entity row, typed JSON snapshot, deletion of the old field projection, and every individual projected field.
- Player projections intentionally retain CA, PA, visible and hidden attributes, foot ratings, position ratings, personality, preferred moves, season statistics and additional typed searchable fields. Replacing this with a reduced index would have lost information the user needs.

## Implemented import changes

- Process records in batches of 100 rather than retaining a second full list of 92,683 projected/serialised records.
- Preserve a single SQLite transaction across all batches. A failure still rolls the whole batch back.
- Reuse prepared multi-row INSERT commands for entity rows, typed JSON snapshots and field rows.
- Insert 60 field rows per SQL statement (840 bind parameters), remaining below SQLite's historic default 999-variable limit.
- Add an explicit provider-replacement mode. The live provider's old rows are deleted once inside the import transaction, not once for every record's field projection. CSV/other providers remain untouched.
- Report completed records on one 0–100 sub-progress scale; the UI no longer presents a fixed 96% stage with a separate unrelated preparation percentage.

## Public reference: FMRTE

FMRTE's public FM27 documentation confirms that Load Game reads the career currently open in Football Manager, after which players, staff and clubs can be searched. Its public product documentation lists Quick Search and Advanced Search, and its player documentation distinguishes:
- CA (Current Ability), 1–200;
- PA (Potential Ability), the normal development ceiling;
- RCA (Recommended CA), an estimate calculated from attributes and positions using configurable ability weights;
- Football Manager role ratings, which are separate from RCA.

Sources:
- https://docs.fmrte.com/getting-started/load-game/
- https://docs.fmrte.com/players/ability/
- https://www.fmrte.com/

FMRTE is proprietary and its internal import/index implementation is not publicly documented in these sources. FM Atlas should match the documented user-visible behaviour, not claim knowledge of FMRTE internals or copy its code.

## Public source reference: FMLibreEditor

FMLibreEditor is an open-source FM27 editor. Its development notes describe a separate desktop process reading the game's game_plugin.dll heap using ReadProcessMemory; they state that the editor reads the game's entity lists on connection/reload. Its README describes browsing, searching and filtering every player, staff member and club, plus exporting attributes, positions, personality, contracts and club finances.

Sources:
- https://github.com/tozmoz/FMLibreEditor
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md

FM Atlas uses its own C# reader and domain models. This reference is used to corroborate the live-memory approach and expected entity-list behaviour, not to copy FMLibreEditor code.

## Attribute/profile/search coverage retained

The current FM Atlas player projection includes:
- Current Ability and Potential Ability;
- visible attributes, hidden attributes, personality and foot ratings;
- position ratings and preferred moves;
- season statistics;
- extra typed integer, decimal, Boolean, date and text fields discovered by the reader.

The reader converts mapped raw attribute values for the FM 1–20 display scale. Field evidence remains marked **Mapped**, not independently **ReadVerified**, until each field is compared with the user's loaded FM27 career.

RCA is not fabricated: FMRTE's documentation describes it as a separate weighted estimate. FM Atlas needs verified ability-weight data and a tested calculation before showing its own equivalent.

## Verification still required

- Run Windows CI (including the complete test suite) for this importer change.
- Benchmark against a real 92,683-record save and record actual load/index elapsed time, peak memory and persisted entity/field counts.
- Compare a sample of CA, PA, attributes, hidden attributes, positions and staff/club fields against the same in-game records.
- Confirm Quick Search and Advanced Search return the same full live dataset after the import.
