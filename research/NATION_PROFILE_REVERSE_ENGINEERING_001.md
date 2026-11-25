# Nation profile reverse-engineering notes — FM27 Game Pass 27.1.1

Date: 25 November 2026

## Objective

Add a real Nation directory and full-width Nation workspace to FM Atlas so My Career can link the manager's nationality and club nation to a populated Nation record. This is read-only research and implementation planning; it does not authorize memory writes.

## Independent source evidence

### FM Editor 27.1.1 Game Pass profile

Source profile: https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/main/offsets/27.1.1%2B2511875-gamepass.json

The profile matches the user's uploaded `game_plugin.dll`:
- Build string: `27.1.1+2511875`
- Build string RVA: `0x48765B3`
- PE timestamp: `0x6AFF583B`
- Image size: `0x17624000`
- Nation list root RVA: `0x53EAF00`
- Nation list offset: `0x240`
- Nation vtable RVA: `0x4A601A8`
- Nation container vtable RVA: `0x4A6A0C8`
- National-team vtable RVA: `0x4B21B28`

The build string at RVA `0x48765B3` was independently inspected in the user's uploaded DLL and reads exactly `27.1.1+2511875\0`. This corroborates the package identity in addition to the PE timestamp and image size.

### Base Windows nation layout

Source: https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/main/offsets/layouts/windows.json

The 27.1.1 profile overrides the nation list root, list offset and vtables; the base Windows layout supplies the following object fields:

| Field | Offset |
|---|---:|
| Nation ID | `0x0C` |
| Name pointer | `0x18` |
| Short-name pointer | `0x28` |
| Men's national-team pointer | `0x108` |
| Women's national-team pointer | `0x110` |
| Capital | `0xE8` |
| Continent | `0xF0` |
| Stadium | `0x118` |

The Nation repository reads the vector header at `moduleBase + 0x53EAF00`, plus `0x240`, then validates each object's vtable against `moduleBase + 0x4A601A8`. The first implementation in FM Atlas should use this configured vector and strict vtable/name/id checks. Do not copy the external tool's broad fallback heap scans until a separate, tested diagnostic mode exists.

### National-team object layout

The same base layout documents:
- Team object at Nation + `0x108` (men) and Nation + `0x110` (women).
- National-team vtable: `0x4B21B28`.
- Squad vector: team + `0x38`.
- Manager pointer: team + `0x80`.
- Reputation: team + `0xA8`.
- Attendance pointer: team + `0xB0`; average/min/max at attendance + `0x68`, `0x6C`, `0x70`.

The external source documents these values as measured on England and Vietnam. FM Atlas must still validate them against the user's own 27.1.1 career before treating them as verified.

### FMLibreEditor cross-reference

Source profile: https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/gamepass-0.9.2562.0.toml

FMLibreEditor independently documents:
- Men's national-team array root: `0x53F8F50`.
- Women's national-team array root: `0x53F8F68`.
- Tactics list/current index offsets: `0x1018` / `0x1030`.
- Club corporate-facilities offset: `0x8AD`.

These national-team array roots are not the same thing as the Nation directory vector above; they are separate roots and should be modelled separately.

### Apparent Game UI RVA difference resolved

FMLibreEditor uses `game_ui = 0x53F9A60`; FM Editor uses `gameUiRva = 0x53E79C0` and vtable `0x4876858`. FMLibreEditor's profile comments explicitly state that `0x53E79C0` is another static pointer to the same single `GAME_UI` object. These are two aliases, not evidence of a conflicting layout. FM Atlas currently uses the FMLibreEditor canonical pointer and the `0x100` game-load counter offset.

## Nation fields available for a read-only profile

The FM Editor Windows base layout maps these fields on the Nation object or its linked team:
- Economic factor, years to gain nationality, development state, gain-nationality type and dual-nationality restriction.
- FIFA ranking points and position; FIFA/Federation affiliation years; association formation year.
- Youth rating, FA financial power, maximum youth age, FA patience, foreign-manager likelihood, importance and assistant-manager rules.
- Average, minimum and maximum attendance.
- Women's national-team equivalents for ranking, affiliation, youth rating, FA values and attendance.

The source's field ranges are editor-side input limits, not proof of valid read ranges for every save. FM Atlas is read-only and should display raw mapped values with provenance rather than enabling edits.

## FM Atlas implementation plan

1. Add optional Nation layout metadata to `GameBuildProfile` for the 27.1.1 profile; keep unsupported builds null/disabled.
2. Add a read-only Nation vector reader with strict bounds, count limits, vtable, ID and printable-name validation.
3. Add `NationRecord` with provider-scoped identity; add Nation snapshots/indexing without using bare IDs.
4. Link Staff.Nationality and Club.Nation to the Nation record using stable IDs where available; do not join by name alone.
5. Add a full-width closable Nation workspace with Overview, National Team, Women's Team, Attendance, Staff and Squad sections.
6. Read national-team squads with the national-team vtable, not the club-team vtable; validate every player pointer before displaying it.
7. Validate at least 20 nations, high valid-name/ID coverage, plausible national-team pointers and sensible squad sizes before importing the Nation snapshot.
8. Cross-check sample Nation names, FIFA position/ranking, youth rating and national-team membership against the same loaded career in a trusted 27.1.1 tool.

## Licensing and safety

The FM Editor and FMLibreEditor repositories are GPL-3.0; this note records public offsets and architecture only. No source code is copied into FM Atlas. FMSuperScout is MIT-licensed and may be considered for separately attributed reuse if needed. FMRTE is proprietary; use its public feature behaviour as a benchmark, not its code or private implementation.

Nation scanning remains read-only. No Nation field editing, process writes or save-file patching is in scope until the entire read path is independently validated and explicitly designed.
