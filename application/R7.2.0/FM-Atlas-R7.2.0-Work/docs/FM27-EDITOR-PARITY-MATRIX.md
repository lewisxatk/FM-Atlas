# FM27 Editor Parity Matrix — FMRTE / FMLibreEditor / FM27 Editor

Status: research and implementation map for the FM Atlas V10 pre-testing branch `feature/fm27-fmrte-parity-editor`.

This is an independently authored implementation plan and field catalogue. It records what the public tools document, what the current Atlas code imports or edits, and what remains unsafe or unimplemented. It does not copy source code from the reference projects.

## Executive position

The target is **FMRTE breadth**, using FMLibreEditor's transparent FM27 layout/recon work and the second FM27 Editor as reverse-engineering references. “Every possible thing” is not a single flat list of numeric offsets. Some values are inline fields; others are pointers, variable-length records, lists, relationships or game-owned state machines. A broad UI may expose source-backed values as Atlas-local overrides, but an in-game write must first identify the correct object, resolve any sub-record, validate the value and linked invariants, confirm the active save generation, re-read the original value, write, read back and roll back safely on failure.

The safest architecture is therefore three explicit states:

1. **Imported source fact** — a value read from a supported FM27 game process or a read-only save snapshot. Source and confidence stay intact.
2. **Atlas local override** — an independently stored, editable value for planning, experimentation and analysis. It never mutates the imported career snapshot or FM process.
3. **Live FM27 change** — a field-specific staged change. It is unavailable until the exact build, field layout, entity identity, UI witness, range, write/read-back and rollback checks have passed. The global Apply/Undo hard gate remains OFF in this branch.

## Reference projects and research

### FMLibreEditor

Repository: https://github.com/tozmoz/FMLibreEditor

Relevant sources inspected:
- `crates/fmlibre-core/src/edit.rs` — player, staff and club writable-field catalogue and validation.
- `crates/fmlibre-core/src/players.rs`, `staff.rs`, `clubs.rs` — entity discovery and detail readers.
- `profiles/fm27/layout.toml` — FM27 object/record offsets and field types.
- `docs/DEVELOPING.md` — build matching, save-load generation checks, record validation, UI witnesses, read-back and failure policy.
- `docs/recon/fm27-contracts.md`, `fm27-staff.md`, `fm27-tactics-and-value.md`, `fm27-club-debts.md`, `fm27-loans.md`, `fm27-transfers.md`, `fm27-youth-intake.md` — field-specific reverse-engineering evidence and explicit gaps.

The repo is GPL-3.0-or-later. Atlas uses its public research as a technical reference and independently authored implementation; do not copy its source implementation.

### FMRTE 27

Official product and documentation: https://www.fmrte.com/

FMRTE's scope is substantially wider than an attribute editor: player/staff details, attributes, positions, preferred moves, relationships, contracts, transfers, promises and permits; club finance, facilities, stadiums, affiliations and kits; competition rules, standings, prizes and nation settings; presets, mass editing, search, history and undo. FMRTE's current player-season statistics view is a separate read-only surface, not proof that every performance metric is editable.

### FM27 Editor (Jackhamerp24)

Repository: https://github.com/Jackhamerp24/Football-Manager-27-Editor

The README lists player, staff, club, nation, stadium, city, competition, award, referee and injury-type screens; transfer and future-transfer terms; promises, permits, relationships, knowledge, languages, formations/tendencies; mass editing, presets, freeze tools, history/undo and CSV/JSON export. It describes a build-calibration process against FM UI and FMRTE values. Its GPL code and offset manifests are a reverse-engineering reference only; Atlas does not copy the project implementation.

## Field coverage from the FM27 Libre layout

### Player object

The inspected Libre field catalogue includes these writable primitive or bounded fields:

| Group | Fields documented by the Libre editor |
|---|---|
| Core ability | Current Ability, Potential Ability |
| Physical / identity | Height, date of birth, nation pointer, city-of-birth pointer |
| Market / reputation | Asking price; home, current and world reputation |
| International record | Senior caps and goals; U21 caps and goals |
| Current state | Condition, match sharpness, jadedness, morale |
| Football attributes | All layout-mapped attribute groups (technical, mental, physical, goalkeeper, hidden) |
| Positions | Position ratings; Sweeper is intentionally not offered because the game no longer uses it and the stored rating is always 1 |
| Personality | Adaptability, ambition, loyalty, pressure, professionalism, sportsmanship, temperament, controversy |
| Traits | 64 individual trait bits in the player trait mask |
| Contract | Wage, expiry, squad number, squad status, transfer-listed, loan-listed, loyalty bonus, happiness, playing-time happiness |
| Loans | Loan expiry and option-to-buy fee where the loan-term record is present |
| Unhappiness | The editor's supported operation is “remove all” (clear the list); the record is not rebuilt or expanded |

The layout also identifies pointers/lists for contracts, injuries/absences, relationships, clauses, competition bonuses and other structures. A pointer being documented is not by itself a safe write recipe. Variable-size lists need capacity management and the target object's correct allocator/ownership rules.

### Staff object

The Libre field catalogue includes:
- Staff CA and PA.
- Home, current and world reputation.
- Nationality pointer.
- Coaching / staff ratings: Goalkeeping, Judging Ability, Judging Potential, People Management, Motivating, Physiotherapy, Tactical Knowledge, Attacking, Defending, Fitness, Possession, Technical, Tactical, Sports Science, Negotiating, Judging Staff, Set Pieces, Data Analysis and Determination.
- Direct ratings for Authority and Working With Youngsters.
- The eight personality values.
- Contract wage and expiry.

The staff identity/person block has a different layout from a normal player. The human manager's identity block is shifted by +0x340 in the documented case; coaching attributes, reputation and CA/PA remain at their base offsets. Atlas must not treat staff as players or apply player offsets to a staff object.

### Club, finance and stadium

The inspected Libre field catalogue includes:
- Senior-team reputation.
- Training, youth facilities, youth coaching and youth recruitment.
- Corporate facilities.
- Club balance, transfer budget, wage budget and total wage budget.
- Stadium capacity, seating, used capacity and expansion capacity.

The FM27 layout also identifies separate club, club-info, club-finance, senior-team and stadium records. Finance values are not necessarily present for every club: clubs outside the playable leagues may use a smaller finance class. Stadium seating cannot exceed capacity. Those are structural invariants, not just display rules.

### Documented unknowns and volatile structures

- **Club debt records:** Libre's research describes the debt list but explicitly says changing/adding/removing debt records is untested. Do not present debt-record writes as live supported.
- **Sugar daddy / rich benefactor ownership:** a visible FMRTE feature is not enough to infer a safe field; Atlas still needs a verified FM27 owner/benefactor mapping.
- **Tactics:** the tactical block includes team style and role masks, but it is not the same thing as a per-player “tactical effectiveness” score. The block can be rebuilt when the tactics view changes, so selection/object re-resolution and UI witnesses are essential.
- **Dynamic lists:** promises, clauses, languages, relations, future transfers, loans, staff departments, debts and other list entries need capacity/ownership-safe code. Do not append by guessing.
- **Build specificity:** an offset from one storefront/build is not portable to every FM27 build. Profile mismatch must return to read-only mode.

## FMRTE breadth target matrix

The following is the product-scope target, not a claim that all features are already implemented.

| Area | Breadth to target |
|---|---|
| Players | Personal/biographical data, nationality/languages, CA/PA, all attributes including verified hidden fields, foot, positions, personality, traits, reputation, condition/morale/sharpness, career history/statistics, contracts/clauses, promises, permits, injuries/bans, preferred moves, relations, transfer/loan/future-transfer actions |
| Staff | Biographical details, nationality/languages/knowledge, CA/PA, coaching/mental/medical/scouting ratings, role ratings, personality, reputation, contract, licence, career plan, relationships, formations/tendencies, promises, job/club moves |
| Clubs | Information, reputation, ownership/board, benefactor status, finances/balance/budgets/wages, ticket prices, debts/loans/sponsorship, facilities, stadium/ground, attendance, club vision, affiliations, relationships, staff/squads, kits and colours |
| Competitions | Reputation, stages, points/standings, prize money, substitution and registration rules, squad rules and competition settings |
| Nations | Reputation, agreements, transfer/work-permit rules, national squads/staff, language/colour/kit fields, rivalries and relationships |
| Stadiums and cities | Stadium name/owner/location/capacity/seating/expansion; city name/nation/region/language/weather where the source data exists |
| Other directories | Awards, referees/officials, injury types and other FM database entities that the current build can resolve |
| Tooling | Advanced search, presets, batch/mass editing, value freeze where technically safe, import/export, field-level history and undo |

## What this Atlas branch implements

### Completed in this branch

- Player profile editor remains intact, including CA/PA and source-backed player attributes.
- The editor now has a generic `LocalEntityOverrides` store keyed by entity type, source ID, group and field. This keeps original value, override value, source label and timestamp.
- Generic local field editors are added to player, staff and club profiles. They expose all scalar facts the selected source actually imported, not invented fields.
- Generic field saves write to Atlas only and create audit-history entries.
- The local undo path understands these entity override records and restores the previous override or removes the first override.
- A dedicated **World Editor** page lists every table present in the current `FmsaveExtraTables` snapshot, including contracts, suspensions, finances, sponsorships, facilities, stadiums, affiliations, stages, competitions, fixtures, competition rules, transfer windows, nations, officials, injuries, training, mentoring, tactics, set pieces and selections.
- World Editor lets the user search records, inspect all flattened scalar fields (including nested object and array elements) and save local overrides against the exact snapshot/table/row identity. The raw imported JSON and source snapshot are preserved. Unsupported structured operations are not silently treated as scalar memory edits.
- Live FM27 player import now retains additional scalar facts: height, condition, match sharpness, jadedness, morale, reputation, asking price, senior/U21 caps and goals, left/right foot, in addition to CA/PA, attributes, personality, positions, nationality and birth date.
- The live player staging catalogue now has range/type-aware previews for the mapped player fields, with current-vs-target checking. It includes supported attributes, position ratings (excluding Sweeper), personality, feet, CA/PA, height, condition, match sharpness, jadedness, morale, reputation, asking price and international statistics.
- A read-only live preview panel is wired into Editor Studio for a selected live player. It re-resolves the process/build/player and stages only changed fields. It does not write memory.

### Explicitly not live-editable yet

- The global hard gate in `LiveMemoryEditService` remains `false`. Apply and Undo are deliberately unavailable.
- Hidden attributes remain blocked from live preview until their FM27 mapping is independently witnessed.
- Staff, club, competition, nation, city and stadium live writers are not yet implemented in Atlas.
- Player contracts, loans, transfer clauses, relationships, promises, work permits, injuries/bans and variable-length collections are not yet live-writable.
- Club debts, sugar-daddy ownership, kits, club vision, tactical blocks and player tactical-effectiveness values are not enabled as live writes without the required evidence.
- Player season/performance data stays read-only/scouting intelligence. A stat surfaced by FMRTE is not automatically an editable game field.

## Acceptance gate before any live Apply is enabled

For every entity type and field group:
1. Match an exact FM27 executable/module fingerprint and supported storefront/build profile.
2. Resolve the entity uniquely by stable identity (UID plus name or other independent witness); resolve its true sub-record (contract/finance/stadium etc.).
3. Confirm the active career/save-load generation before staging and immediately before each write.
4. Validate raw field type, allowed input range, current-value plausibility, and linked invariants (e.g. capacity >= seating; finance class exists).
5. Compare sampled values against FM's own UI and an independent editor such as FMRTE; record golden fixtures for the exact build.
6. Stage all changes, show old → new values, and re-read every original value before the first write.
7. Apply only after explicit user confirmation; read every target back, journal the edit, and roll back partial batches only while the same save generation and object identity remain valid.
8. Repeat on a disposable copy of a career save, including undo, save/reload persistence and UI confirmation.
9. Any mismatch or unknown build disables the affected field group and leaves all other read-only information available.

## Immediate remaining build areas

1. Add directory-specific labels, filters and linked navigation for competitions, nations, stadiums, cities, awards and officials on top of World Editor.
2. Add field dictionaries and value pickers for enums/flags instead of expecting users to edit numeric codes or internal IDs manually.
3. Implement field-specific live readers and staged writers for staff and club first, then contract/finance/stadium sub-records. Keep arrays and pointer changes out until allocator/capacity handling is proven.
4. Add structured editing workflows for transfers, loans, relationships, promises, permits, affiliations, debts and tactical blocks.
5. Add mass-edit plans with filters, before/after review, per-entity preflight and atomic undo groups.
6. Add exact-build calibration fixtures and automated test cases for every supported field. Do not switch the live-write hard gate on until the fixtures pass on a disposable save.

## Source links

- FMLibreEditor: https://github.com/tozmoz/FMLibreEditor
- Libre FM27 layout: https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/layout.toml
- Libre edit-field catalogue: https://github.com/tozmoz/FMLibreEditor/blob/main/crates/fmlibre-core/src/edit.rs
- Libre development / write-safety design: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- Libre FM27 staff research: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-staff.md
- Libre FM27 tactics research: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-tactics-and-value.md
- Libre FM27 debt research: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-club-debts.md
- FMRTE: https://www.fmrte.com/
- FMRTE documentation: https://www.fmrte.com/docs/
- FM27 Editor: https://github.com/Jackhamerp24/Football-Manager-27-Editor
