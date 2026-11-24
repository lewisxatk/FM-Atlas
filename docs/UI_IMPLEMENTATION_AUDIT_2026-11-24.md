# FM Atlas UI implementation audit — 24 November 2026

**Code baseline:** `rebuild/fresh-foundation-001` at `197c89e6cde92599c81c214f7297daab60581975`  
**Design direction:** black/graphite surfaces, warm off-white text, restrained orange accent; editorial / readable / monospaced typography. The approved references are recorded in `docs/DESIGN_SPECIFICATION.md`.

## Implemented in the WPF source

- Compact left navigation for Dashboard, Players, Staff, Clubs, My Career, Save Manager and Settings. Search is not a left-navigation item.
- Global Quick Search and Advanced Search in the top header, with Load Game as the prominent primary action.
- Player / Coach / Club Advanced Search entity selectors, FMRTE-style categories, typed filter controls, MIN/MAX ranges, AND/OR logic, searchable field catalogue and saved presets.
- Player profile workspace with top-level FMRTE-style section tabs, left identity rail, central attribute area, right-side information panels, CA/PA and position-familiarity pitch.
- Dedicated Staff and Club profiles with closable workspace tabs; Club Squads and Staff are joined through numeric club IDs.
- Installed graphics lookup for player faces and club badges, with neutral placeholders where a matching image is unavailable.
- Source/evidence labels and honest empty states; unsupported data is not filled with invented metrics.
- Live connector and Save Live Changes remain read-only / disabled.

## UI corrections in this integration

- Removed the duplicate My Career navigation entry.
- Reduced the Advanced Search interactive position map to a compact right-hand panel (224 px panel; 126 px pitch viewport).
- Normal position buttons use muted grey; selected positions use the orange accent. The code-behind already maintains this selected-state behaviour.
- Kept the position map beside the filter fields, not above the filter workspace.

## Still not accepted as complete

- No Windows screenshot comparison against the supplied visual references has been performed for this commit.
- The manager / club / nation page still depends on a dedicated human-manager marker and verified manager-to-club links being present in the imported snapshot.
- Save Manager and Settings remain limited workspaces; Squad & Tactics and Recruitment are still deferred.
- Some profile sections (season statistics, historical records, proposals, languages, retirements and some club/staff relationships) remain empty until their data families are imported and verified.
- Advanced Search only executes fields with real typed storage/query mappings. A visible field catalogue entry is not proof that a field is populated by the live reader.
- No live write path is enabled.

## Acceptance checklist

- [x] The approved black / off-white / orange visual language is represented in the WPF resources and screens.
- [x] Global Quick Search and Advanced Search remain in the top header.
- [x] Search is absent from the left navigation.
- [x] Player, Staff and Club directories and profile workspaces exist.
- [x] Player profile sub-tabs are placed across the top of the profile.
- [x] Position selection is on the right of the Advanced Search filters and has grey idle / orange selected states.
- [x] Duplicate My Career navigation entry removed.
- [ ] Side-by-side screenshot review on Windows at normal desktop scaling.
- [ ] Confirm My Career manager, club and nation data against the user's live save.
- [ ] Confirm all desired Advanced Search fields against the live index.
- [ ] Confirm actual FM27 27.1.1 live player/staff/club reads on a disposable career.
- [ ] Verify field-level write/read-back/undo before any editing is enabled.
