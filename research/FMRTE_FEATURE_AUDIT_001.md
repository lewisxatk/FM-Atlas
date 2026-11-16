# FM Atlas — FMRTE 27 Feature Audit 001

**Date:** 2026-11-16  
**Sources:** FMRTE 27 official website and public FMRTE documentation, including the user's uploaded FMRTE 27.0.1 build 12 package.  
**Status:** Feature discovery from public documentation/release notes; not an assertion that FM Atlas implements these features.

## 1. Player profile sections

Official source: [Editing players](https://docs.fmrte.com/players/overview/)

FMRTE 27 documents these player sections:
- **Information:** attributes, ability, personal details, fitness and morale.
- **Staff:** only for a player-coach; staff-side information, Training Rating, Formations/Tendencies and Knowledge.
- **Stats:** read-only season statistics, performance comparisons and competition leaderboards.
- **Unavailability:** bans and other reasons a player cannot play.
- **Transfer Proposals:** existing transfer offers involving the player.
- **Retire Date:** scheduled retirement date.
- **Positions:** familiarity with each position on the pitch.
- **Preferred Moves:** match behaviour traits.
- **Relationships:** links to people, clubs and nations, including second nationalities.
- **Languages:** spoken languages and proficiency.
- **Scout Information:** General Rating, Positional Rating and Role Ratings.
- **Clone Center:** documented in FMRTE FAQ as a player-page tab that copies selected data from the open player to existing target players; it does not create a new player.

The compact player header can expose editable CA/PA and Asking Price, plus summaries for contract wage/expiry and current/potential rating. Sections can be conditional: for example, Contract when a contract exists, Staff for player-coaches, Transfer Proposals when relevant records exist, and Retire Date when scheduled.

## 2. Staff profile sections

Official source: [Editing coaches and staff](https://docs.fmrte.com/staff-and-managers/staff/)

Documented staff sections:
- Information
- Contract (when club or national-team contract exists)
- Career Plans
- Promises
- Swap Staff
- Training Rating
- Formations/Tendencies
- Relationships
- Knowledge
- Retire Date (when scheduled)
- Languages

Staff information includes personal details and staff attributes. Coaching, role, tactical, mental, scouting and chairman-related fields appear where relevant. Training ratings and knowledge have their own workflows.

## 3. Club profile sections

Official source: [Editing clubs](https://docs.fmrte.com/clubs/overview/)

Documented club sections include:
- Information — facilities, attendance, each team's reputation.
- Squads — players in each team, presets and player actions.
- Youth Intake — candidates and their attributes where applicable.
- Finances — balance, transfer/wage budgets, debts, loans and sponsorship.
- Club Vision — club culture items and dates where present.
- Possible Signings — players the club is interested in.
- Player Clauses — clauses in player deals.
- Transfer Center — transfer offers where the club is buying or selling.
- Staff — club staff list and staff actions.
- Wage Contributions — where contributions exist.
- Kits and Colours.
- Affiliations.
- Match Preparation / tactical familiarity.
- Stadium information is covered in a separate Stadiums area.

The actual presence of a tab depends on whether the selected club/save has the corresponding record.

## 4. Search and quick lookup

Official source: [Search and Advanced Search](https://docs.fmrte.com/scouting/search/)

FMRTE documents:
- Quick search by name or UID.
- Advanced Search selectors for Player, Coach and Club.
- Player criteria including age, nationality, continent, region, city of birth, ethnicity, ability, attributes, positions and contract details.
- Coach criteria including nationality/geography, contract details, coaching attributes, scouting attributes and training ratings.
- Club criteria including facilities, finances, debt and stadium details.
- Numeric min/max ranges.
- FMRTE 27 website/release notes also document natural-language search that converts a description into visible removable filters. This is an advanced future option for FM Atlas; initial Quick Search remains ordinary search.

FM Atlas controlling layout remains REF-002. Keep the screenshot's left categories and grouped filter panels; expand the field catalogue inside those categories rather than replacing the dialog with a different workflow.

## 5. Editing, saving and history

Official sources:
- [Interface tour](https://docs.fmrte.com/getting-started/interface-tour/)
- [Edit History and Undo](https://docs.fmrte.com/tools/edit-history/)
- [First edit](https://docs.fmrte.com/getting-started/first-edit/)

Documented workflow:
- Load Game reads the save currently open in Football Manager.
- Save Changes applies edits from the active page to the running game.
- Reload refreshes values for the active record.
- Edit History lists current-session changes and eligible undo actions.
- Pending fields have an accent outline; unsaved tabs have an asterisk; saved fields turn green.
- Closing/reloading a page with pending edits prompts before discarding.
- Undo checks the current game value first; it does not blindly overwrite a value that has changed since the edit.
- History is limited to the current loaded-game session; it does not survive restarting FMRTE and there is no Redo.
- Some actions that add/remove objects or have side effects are recorded but cannot be undone safely.
- Saving in Football Manager is still required to persist live-game edits to the career file.

FM Atlas requirements:
- staged edits, per-page save counts, unsaved-tab marks and saved-state marks;
- refresh/reload with pending-edit protection;
- per-field change history with old/new values;
- undo only if current value still matches the value written by the tracked edit;
- explicit non-undoable status for operations without a safe inverse;
- a separate backup/recovery workflow;
- no claim that writing to live memory has saved the career file.

## 6. Other FMRTE tool families

FMRTE 27 official site and uploaded build 12 release assets identify these feature families:
- Mass Edit for multiple selected players/staff/clubs, squads or search results.
- Preset Manager and reusable presets.
- Freezer Manager for keeping supported values fixed.
- Player comparison.
- Shortlists and favorites.
- Team Selection and formation editing, including role ratings in and out of possession.
- Transfer and loan offers, fees, terms and rules.
- Player promises, work permits and future job.
- Clone Center.
- Club finances, facilities, stadium, kits/colours, affiliations and wage contributions.
- Nation transfer rules and competition rules.
- Squad registration limits.
- Player Stats with season overview, season detail, league ranks and position comparisons; Stats is read-only in FMRTE.
- AI Assistant and scheduled automation are advanced/optional; they require a chosen AI provider/local model and are not part of the initial simple editor.

Official source: [FMRTE 27 product overview](https://www.fmrte.com/) and [FMRTE 27 Windows release notes](https://www.fmrte.com/download/fmrte27).

## 7. FM Atlas priority mapping

### Initial player editor release
- Player Information and full attribute groups.
- Contract.
- Positions.
- Preferred Moves.
- Stats (show only verified saved statistics; read-only at first).
- Unavailability.
- Relationships.
- Languages.
- Retire Date.
- Scout Information only where values/calculations are supported and clearly labelled.
- Staff section only for player-coaches when the data source exposes it.
- Transfer Proposals and Clone Center after their data/operation safety is verified.

### Initial staff and club releases
- Staff profile sections and club profile sections above, with each field's source/read/write coverage.
- Do not expose tabs for records that do not exist; show them conditionally or in a clearly marked unavailable state.

### Later after the simple editor
- Mass Edit and Preset Manager.
- Freezer Manager.
- Team Selection / Squad Studio.
- Transfer/loan management and rules.
- Club/nation/competition/stadium editing expansion.
- Assistant and automation.
- More advanced stats and scouting.

## 8. Important design adaptation

FMRTE places record sections in a collapsible left rail and uses main tabs for records. FM Atlas will **not** copy that exact navigation location because the user has specified compact player-specific buttons/tabs across the top of each full-screen player profile. The section names and functionality are the FMRTE reference; the placement follows the user's REF-004 instruction. Use small, consistent controls and an overflow treatment if the screen becomes too narrow.

The left identity column and central attribute area from REF-004 remain the profile's controlling composition. Do not replace the user's design with a generic FMRTE clone.
