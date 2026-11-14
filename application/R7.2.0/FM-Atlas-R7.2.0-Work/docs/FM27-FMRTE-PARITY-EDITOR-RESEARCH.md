# FM27 FMRTE-parity editor: reverse-engineering notes

Status: research baseline for the FM Atlas V10 editor work. This document distinguishes fields observed in FM27 layouts from fields that still need an in-game witness or a verified write path.

## Reference implementations inspected

- [FMLibreEditor](https://github.com/tozmoz/FMLibreEditor): inspected `crates/fmlibre-core/src/players.rs`, `clubs.rs`, `staff.rs`, `profile.rs`, `own_memory.rs`, `profiles/fm27/layout.toml`, and FM27 recon notes.
- [barak14/fm-editor](https://github.com/barak14/fm-editor): existing FM Atlas player-layout provenance and FM27 field cross-reference.
- [FMSuperScout](https://github.com/mavarobli/FMSuperScout): inspected repository structure and scouting-data architecture.

Do not copy upstream source code. Use the public findings as independent evidence, then maintain Atlas-owned field definitions, validation, and tests.

## Reverse-engineering conclusions

### Player profile

The FM27 player object is substantially broader than CA/PA plus the visible 52 attribute bytes. The inspected FM27 layout/recon material identifies:

- Identity and biography: UID, full/common/first/last name, date of birth, nation, height, city of birth, gender and generated-player flag.
- Ability and attributes: CA, signed PA (including negative random-potential ranges), 52 footballing/hidden attribute fields, left/right foot ratings, eight personality ratings and 15 position ratings.
- Physical/current state: morale, condition, match sharpness and jadedness.
- Reputation and career: home/current/world reputation, international caps/goals and U21 caps/goals.
- Transfer and contract: transfer value, asking price, contract dates, weekly wage, squad status/number, contract kind, listing flags, clauses, bonuses and unhappiness records.
- Traits: player preferred moves/traits.

The field layout is not enough on its own to enable live writes. Each writable field needs a correct FM27 build profile, correct object/class shift, encoding/range validation, a source-backed current value, and a successful in-game read-back witness.

### Staff

FMLibreEditor has a separate staff object and identity-shift model. Staff data includes CA/PA, job/club relationship, contract details, coaching ratings, mental/knowledge/medical ratings, personality and reputation. Player attribute offsets must not be reused for staff: their layout and scaling differ. The existing Atlas staff decoder is a partial import map, not a complete staff editor.

### Clubs and club finances

The inspected FM27 club model exposes club identity, reputation, founded year, nation/league, facilities, supporter mix/traits, stadium and finances. The finance profile includes balance, transfer budget, wage budget and total wage budget. Separate club-info structures hold debt lists.

The debt research documents a list at club-info +0x50 and a 0x1C-byte debt record, with original debt, monthly payment, interest, dates and type. The upstream research explicitly says changing/adding/removing debt records is untested. Therefore Atlas must not advertise debt editing as safe until list-capacity handling, rollback and an in-game witness are completed.

A specific sugar-daddy/owner-status field was not established by the inspected evidence. Do not invent an offset or present it as editable until the field is found and validated.

### Tactical effectiveness and tactics

The FM27 research identifies a tactics block associated with a club's selection, with in-possession and out-of-possession role masks and style. This is not the same thing as a player's tactical-effectiveness rating. Tactical effectiveness must be sourced from the relevant player/club field or game screen, not inferred from the role masks.

The inspected upstream notes show that the tactic block can be copied/rebuilt when a manager leaves the tactics screen. Any writer must resolve the active selection and current tactic block again at apply time; stale pointers are not safe.

## Existing FM Atlas implementation findings

- `PlayerAttributeSchema` currently contains 52 player fields, with inherited/provisional notes for hidden attributes.
- `LiveDataImporter` imports CA/PA, player attributes, personality and positions from the current FM27 read profile.
- `LivePlayerFacts`, `LiveStaffFacts` and `LiveClubFacts` are source-fact stores.
- `LiveMemoryEditService` already contains a staged player-edit design: exact build matching, save-load generation check, UID/name re-resolution, preflight, stale-value checks, read-back verification and rollback. Its hard write gate is intentionally false.
- Its current whitelist only allows CA/PA and visible player attributes. It explicitly rejects hidden attributes and does not support personality, positions, contracts, staff or clubs.
- The current profile Editor Studio saves local Atlas database changes only. Local edits must never be described as changes to the FM career.

## Implementation direction

1. Put an Editor toggle directly on every player/staff/club profile, without removing the existing profile tabs or search workflows.
2. Build an entity-specific field catalogue. Every field should carry: entity type, stable key, display label, data type, allowed range/enum, FM27 source/build, offset or relationship path, validation state, and write support state.
3. Make all source-backed profile values editable in a local Atlas overlay first. Preserve unknown/null as unknown; never create a fake zero.
4. Add staged live editing per entity type. Preview old/new values, require explicit Apply, re-resolve the entity, re-check the game session and original bytes, write as a batch, verify by read-back, and rollback on partial failure.
5. Expand player live editing only after per-field witnesses. Add staff and club writers as separate implementations using their own layouts and identity paths.
6. Treat club finances, owner/sugar-daddy status, debt records, club reputation/facilities, staff fields, tactical instructions and player contracts as separate workstreams. Do not use one generic offset table for them.
7. Keep Edit History, batch undo, field-level evidence and a visible Local vs Live status on every edit surface.

## Current implementation slice

The profile-level editor toggle and local CA/PA + source-backed player attribute editing are now available in the player profile on this branch. This is a first slice, not full FMRTE parity. Live Apply/Undo remains disabled.
