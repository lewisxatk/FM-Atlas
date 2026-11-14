# FM27 Editor Parity Matrix — FMRTE / FMLibreEditor / FM27 Editor

Status: research and implementation map for the active FM Atlas V10 live-write branch `feature/fm27-live-editor-write-parity`.

This is an independently authored implementation plan and field catalogue. It records what the public tools document, what the current Atlas code imports or edits, and what remains unsafe or unimplemented. It does not copy source code from the reference projects.

## Executive position

The target is **FMRTE breadth**, using FMLibreEditor's transparent FM27 layout/recon work and the second FM27 Editor as reverse-engineering references. “Every possible thing” is not a single flat list of numeric offsets. Some values are inline fields; others are pointers, variable-length records, lists, relationships or game-owned state machines. A broad UI may expose source-backed values as Atlas-local overrides, but an in-game write must first identify the correct object, resolve any sub-record, validate the value and linked invariants, confirm the active save generation, re-read the original value, write, read back and roll back safely on failure.

The safest architecture is therefore three explicit states:

1. **Imported source fact** — a value read from a supported FM27 game process or a read-only save snapshot. Source and confidence stay intact.
2. **Atlas local override** — an independently stored, editable value for planning, experimentation and analysis. It never mutates the imported career snapshot or FM process.
3. **Live FM27 change** — the active branch now enables staged live Apply/Undo for its explicitly mapped player, staff and club scalar fields, plus tactical Match Exposure lists. Every field is independently whitelisted and must pass exact-build, entity, sub-record, value, stale-byte, save-generation, read-back and rollback checks. Unsupported structures remain blocked.

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
| Market / contract | Transfer value, asking price; wage, signed/expiry/joined dates, contract type, squad number/status, transfer-listed, loan-listed, loyalty bonus, happiness and playing-time happiness |
| Loans | Loan expiry and option-to-buy fee where the loan-term record is present |
| Other | Remove-all-unhappiness operation; no arbitrary concern-record creation or pointer editing |
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
- Season-ticket count, social-media followers, six supporter-mix percentages and six supporter traits.
- Club balance, transfer budget, wage budget, total wage budget and average ticket price.
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

### Live player, staff and club scalar writer

The active branch `feature/fm27-live-editor-write-parity` uses `LiveEntityMemoryEditService` for the current FM27 process. It is distinct from the older preview-only `LiveMemoryEditService`, whose hard gate remains off and which is no longer the UI's live writer.

- **Player:** CA/PA; all 52 mapped football and hidden attributes; 14 writable position ratings (Sweeper remains intentionally excluded); eight personality values; both feet; 64 preferred-move bits; date of birth; height; condition; sharpness; jadedness; morale; reputation; transfer value and asking price; international caps/goals; player contract wage, signed/expiry/joined dates, type, squad status/number, listing flags, loyalty bonus and contract happiness; loan expiry and option fee; clear-all-unhappiness operation.
- **Staff:** staff-specific CA/PA, reputation, birth date, 19 scaled coaching/knowledge/medical ratings, Authority, Working With Youngsters, eight personality values and contract wage. Staff contract dates are intentionally omitted: FM27 has BASIC_CONTRACT records only 0x28 bytes long for some staff, so a date write at the FULL_CONTRACT offset could overwrite the next packed record unless the contract class is resolved first.
- **Club:** senior-team reputation; season tickets; social-media followers; six supporter-mix percentages; six supporter traits; training/youth/corporate facilities; balance, transfer budget, total wage budget, wage budget, average ticket price; stadium capacity, seating, used capacity and expansion.
- Every field is resolved to the right record (person object, contract, loan, concerns, senior team, club info, finance or stadium). Optional record absence omits only that field. Club finance and stadium record classes are detected and checked before access.
- Apply and Undo are enabled through the profile editor. They re-resolve the entity and active save, compare original bytes, validate cross-field constraints, verify every write by read-back and attempt guarded rollback if a batch fails. The user must save the career inside FM to persist process-memory edits to disk.
- The live editor requires a separate offline test career and an FM27 build in the exact fingerprint registry. It is not a promise that every FM27 patch or storefront is supported.

### Tactical Effectiveness

- The active senior-team tactic is resolved from FM's Tactics Manager rather than guessed from a saved tactic name.
- The editor reads six player Match Exposure areas (Line of Engagement, Pressing Intensity, Passing Style, Chance Creation, Build Up, Pressing Trap) and Position / Role Shapes.
- Match Exposure edits stage and apply to each selected senior-team player's existing familiarity list. The writer verifies list bounds, active tactic identity, player identity, save generation and original list bytes; it blocks any operation that needs an unsupported allocation/growth.
- Training Contribution is derived from the player's FM training-history records, not a directly stored score. The current experimental Max Training Contribution action rewrites validated 12-byte history entries for the current tactic; this is a history operation, not a direct 100% score write, and still requires disposable-save verification.
- FM27's four high-level gauges (Tactical Understanding, Player Ability, Player Buy-In and Next Match Information) and Tactical Complexity are derived systems. Atlas currently edits underlying Match Exposure / shape familiarity, not arbitrary direct percentages for those four gauges.

### Still not implemented as live edits

- Player/staff names, nationality pointers, birthplace, language/knowledge relations, secondary nationality/work-permit ties, player-to-player relationships, injury and ban records, retirement plans, dynamic clauses/bonuses, arbitrary promises, future transfers and transfer-offer workflows.
- Staff department moves, manager appointment/release and contract-class-aware staff date edits.
- Club debts/loans, sponsorships, affiliations, ownership / sugar-daddy status, club vision, kits/colours and arbitrary stadium name/owner/location changes.
- Competition, nation, city, stadium, award, referee/official and injury-type live editors.
- Tactic role/formation/team-instruction editing. Match Exposure is a different data structure from the tactic block.
- Mass-edit plans across arbitrary filters, freeze/hold tools and complete FMRTE preset parity.
- Player season statistics and scouting performance metrics remain read-only data; an FMRTE stats panel does not mean those match statistics are editable game fields.

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

1. Expand field dictionaries and value pickers for enums/flags and replace numeric codes with meaningful choices.
2. Add contract-class-aware staff date editing and richer staff job / department operations.
3. Research and implement dynamic player records: clauses, bonuses, promises, permits, nationality/relations, injuries, bans and retirement.
4. Research club debts, loans, sponsorship, ownership, affiliations and club vision. Debt list growth is explicitly untested in Libre's research.
5. Add competition, nation, city, stadium, award, referee and injury-type live editors.
6. Validate Tactical Effectiveness history operations on a disposable save, then extend the four derived gauge readers and role/tactic editing without conflating the underlying data structures.
7. Add mass-edit plans, freeze tools, full edit history and per-field automated fixtures.

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
