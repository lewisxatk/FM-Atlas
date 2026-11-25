# FM27 Game Pass 27.1.0 support profile

**Target update:** FM27 27.1.0, Microsoft Store / Xbox PC  
**Game Pass package version:** 0.9.2524.0  
**Profile date:** 17 November 2026  
**FM Atlas status:** exact fingerprint and external read-only layout mapped; Atlas live field values still require a user's save-level verification. Editing is disabled.

## Evidence and independent corroboration

1. FMLibreEditor added `profiles/fm27/gamepass-0.9.2524.0.toml` for the 17 November update. Its notes state that the profile was generated with `fmlibre-probe profile` against a save dated 18 January 2028 and that all 12 checks passed. It explicitly states that the object layout is unchanged except for two fields/objects: corporate facilities within club finance moved from 0x8A9 to 0x8AD, and the team tactics list/current index moved eight bytes forward.
2. The independent Football Manager 27 Editor repository contains `offsets/27.1.0+2505117-gamepass.json`, which records the same Game Pass fingerprint, Game UI address, loads offset and player-list root.
3. FMRTE's Microsoft Store compatibility list identifies FM27 27.1.0 build 2505117 as supported.

Sources:
- https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/gamepass-0.9.2524.0.toml
- https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/main/offsets/27.1.0%2B2505117-gamepass.json
- https://www.fmrte.com/download/fmrte27
- https://footballmanager.sega.com/asia-en/info/details/000290.html

## Exact PE fingerprint

| Field | Value |
|---|---:|
| Architecture | x64 (0x8664) |
| PE timestamp | 0x6AF5C8C6 |
| SizeOfImage | 0x179AD000 |
| Game Pass package | 0.9.2524.0 |

The new fingerprint is different from the previous 0.9.2435.0 package. FM Atlas must reject the older fingerprint in this reader rather than reusing the new RVAs against it.

## Module-relative roots (RVAs)

| Root | 0.9.2524.0 value |
|---|---:|
| Player array pair | 0x53FAE88 |
| Staff array pair | 0x53FAED0 |
| Club array pair | 0x53FB040 |
| GAME_UI pointer | 0x53FBA40 |
| In-game current date | 0x539DD10 |
| GAME_UI save-load counter | 0x100 |

The player/staff/club array pairs each contain the begin/end pointers used by the existing reader. The GAME_UI address is a static pointer in `game_plugin.dll`; its object contains the loaded-save counter at +0x100.

## Object field layout compatibility

The FMLibreEditor profile states that the general object layout remains the same as 0.9.2435.0 apart from:
- Club finance corporate facilities: offset 0x8AD instead of 0x8A9.
- Team tactics list and selected index: offsets 0x1018 and 0x1030 instead of 0x1010 and 0x1028.

The current FM Atlas live scanner reads player, staff and base club fields. It does not read club-finance corporate facilities or the tactics object. Those two changes therefore remain outside this initial reader update and must not be silently treated as supported.

The shared player field offsets (CA, PA, 1–20 attributes, positions, personality, contract) and staff person/attribute layout are retained by the independent profile source. FM Atlas preserves its existing player/staff/club parser and changes only the per-build module roots and fingerprint for this target.

## Safety / verification boundary

- The scanner opens Football Manager with read-only process rights only.
- Exact fingerprint matching is mandatory before reading entity arrays.
- The live provider key is versioned to 0.9.2524.0 so old live snapshots cannot be mistaken for current ones.
- Records are validated and streamed into a transactional local index; incomplete imports roll back.
- The mapping evidence remains `Mapped` / `ExternalReferenceOnly`; it is not an FM Atlas independent field-by-field verification.
- No process-memory writes, Save Live Changes or editing are enabled.
- Before promoting this profile to `AtlasReadVerified`, test against the user's 27.1.0 Game Pass career and compare a sample of players, staff and clubs, including CA, PA, visible/hidden attributes, positions, age/date, club joins and the manager record.

## Next validation

1. Run the Windows build/test pipeline for this change.
2. Confirm the user's `game_plugin.dll` timestamp and image size match 0x6AF5C8C6 / 0x179AD000.
3. Connect on a disposable/offline save first.
4. Verify imported player/staff/club counts and compare representative values with the same records in FM27.
5. Capture elapsed time and peak memory from `%LOCALAPPDATA%\FM Atlas\Logs\live-connection.log`.

