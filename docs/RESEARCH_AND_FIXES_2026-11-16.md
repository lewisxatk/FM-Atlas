# FM Atlas — Search and live-reader research (16 November 2026)

## Scope

This note records the source review and external reference checks used for the REF-002 Advanced Search and live Game Pass reader fixes. It is an independent implementation; no third-party source code is copied.

## Game Pass 0.9.2435.0 address cross-check

The FM Atlas FM27GamePass0935Reader uses these RVAs:

| Data | FM Atlas reader |
|---|---:|
| Player pointer array | 0x53B7DE0 |
| Staff pointer array | 0x53B7E28 |
| Club pointer array | 0x53B7F98 |
| Current in-game date | 0x53597C8 |

The FMLibreEditor public Game Pass 0.9.2435.0 profile independently lists the same four locations. Its profile notes that the player, staff and club arrays are begin/end pointer pairs and that the date is validated against contract and injury dates. This supports retaining the current address profile rather than replacing it with guessed offsets.

Reference: https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/gamepass-0.9.2435.0.toml

## Name-layout edge case

The FMLibreEditor FM27 name research documents these Game Pass person-name fields:

- Player: full name +0x2C8, first +0x2D8, last +0x2E0, common +0x2E8.
- Staff: full name +0x140, first +0x150, last +0x158, common +0x160.
- Player-coach person data is shifted by 0xF8; the human manager's person block is shifted by 0x340.

The FM Atlas shift-detection heuristic previously scored full, first and last names but omitted the common-name pointer. It now scores common names for both player and staff layouts too, so a record that has a usable common name is not rejected solely because its other name entries are absent.

Reference: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-names.md

## Search-index corrections

Both directory Quick Search and Advanced Search now resolve a display name in this order:

1. Non-empty entities.display_name.
2. Non-empty projected full_name (or club name) field.
3. Non-empty game_uid as a final searchable fallback.

This covers imported snapshots where the normalized name column is empty but the typed searchable field was projected. Regression tests cover the Mohamed Salah example in both search paths.

## REF-002 control corrections

- Pitch selector reduced to a compact right-hand panel.
- Position choices are actual toggle controls, with checked state synchronized to the search ViewModel. Multiple roles can remain selected; All/Any logic and MIN/MAX familiarity remain independent.
- Enum option text now has an explicit dark item template so contract dropdown entries remain readable in the dark theme.
- Stale copy claiming live-career search was still in progress has been replaced with wording that describes the active local index and live snapshot.

FMRTE's current search documentation describes player, coach and club advanced criteria, numeric ranges, and position filters combined as All or Any, with minimum-position matching. FM Atlas follows that interaction model while retaining its own supported-field and evidence labels.

Reference: https://docs.fmrte.com/scouting/search/

## Validation boundary

The Windows CI build, automated tests, packaging and startup smoke test passed for commit 7003a11775767caf33d1e51edbeef76e385d20e8.

The subsequent advanced-search name-fallback and copy update is a separate commit and must pass CI before its package is treated as validated. The exact user's running FM27 process and save still require an on-machine Load Game test; matching static RVAs alone do not prove that the user's current process passed its runtime fingerprint and layout checks.

The final branch-head build and test result is recorded in the associated GitHub Actions run; a successful earlier commit is not a substitute for validating later source changes.
