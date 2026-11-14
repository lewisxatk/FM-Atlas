# FM27 Data Hub and League Hub reader contract

## Product rule

FM Atlas must not invent, simulate, or label a statistic as an FM27 statistic unless it is present in the FM27 save/game data and its field meaning is supported. Keep external provider metrics in a separately identified source family. Do not fill missing FM data with external values or heuristics.

## Reverse-engineering basis

The read-only adapter uses the public MIT-licensed `fmsave` reader. Its 0.12.4 reference documents the FM27 reader set and explicitly distinguishes decoded values from field meanings that have not yet been confirmed against the game UI.

References:
- https://rhiever.github.io/fmsave/reference/save.html
- https://rhiever.github.io/fmsave/guides/what-a-save-holds.html
- https://rhiever.github.io/fmsave/guides/trusting-a-number.html
- https://github.com/rhiever/fmsave

The reader's documented Data Hub / player performance source is `player_season_stats()`: current-season player stats by competition type and team, including minutes, xG, passes, tackles, and the per-90 / percentage values shown by the game. `player_match_stats()` contains retained individual match histories, but is not a complete career match log. `team_match_stats()` contains the team match-stat report (shots, xG, possession, passes, crosses, tackles, aerials, fouls, cards, corners and distance) for matches retained by the game. Join those records to `fixtures()` using competition, home team, away team and date.

League Hub / competition context comes from `stages()`, `competitions()`, `fixtures()`, `league_tables()`, `competition_rules()` and `transfer_windows()`. `league_tables()` only contains tables once clubs have played; use fixtures to represent the competition before that point. fmsave does not ship league/competition display names because those names are not stored in the save; unresolved names must remain unresolved or be mapped from a separately verified game-database source.

## Adapter changes

The export allow-list now includes `team_match_stats`, `officials` and FM27 `squad_planner`, alongside the existing player season, match and history tables. The adapter preserves each table's original named fields in NDJSON. The C# career importer now explicitly recognises and transactionally archives `team_match_stats`, `officials` and `squad_planner` alongside the player, fixture and competition tables. It verifies each table's row count against the manifest before committing the snapshot. It does not convert a field to a friendly metric label or silently reinterpret a raw value. The team-match report remains a separate team-level source and is deliberately not inserted into player-season metric facts.

## Trust and UI rules

- Preserve raw provider field keys and values.
- Display source, table, period, competition/team identity, and field-validation status.
- Mark FM27 field meanings as unconfirmed unless there is a specific UI witness or upstream fmsave validation evidence.
- Do not use FM26 field meanings as a substitute for an unconfirmed FM27 field.
- Do not infer a missing league/competition name, match, minutes value, player link or historical row.
- Do not treat missing player-match history as zero performance.
- Team match reports are sparse and retained only for some matches; missing rows are not zero.
- Finance and sponsorship values have documented caveats and should not be used as authoritative transfer-budget values without further validation.
- FM Data Hub and League Hub data remain the default source in the FM Stats Explorer. Wyscout CSV and StatsBomb event imports are optional external datasets, visually separated and never merged into FM-native totals.
- Percentiles are calculated only within compatible competition / season / position cohorts and identical metric definitions. Never compare unlike providers as if they shared definitions.

## Next implementation gates

1. Extend the C# importer with dedicated, typed team-match and fixture join indexes; the current update guarantees lossless archival and manifest reconciliation, not a completed relational join.
2. Add real FM27 save fixtures (redacted/synthetic test fixtures only where they preserve the real schema) for Data Hub season stats, team match reports, fixtures, league tables, officials and squad-planner data. A synthetic regression test now covers preservation of team-match stats, fixtures, league tables and officials.
3. Validate a known FM27 build and representative fields against the in-game Data Hub and League Hub. Keep all un-witnessed fields flagged.
4. Make Stats Explorer filters include source, player, club/team, competition, season, match date, position, minutes and arbitrary numeric metric ranges.
5. Add join-quality indicators for player IDs and fixture keys; unresolved joins remain visible and excluded from derived rates.
6. Preserve existing Advanced Search, saved profiles, shortlists, comparisons and read-only save import.


## Current integration checkpoint

The offline C# importer explicitly archives and manifest-validates `team_match_stats`, `officials` and `squad_planner`. The team-level event tables remain separate from player-season metric indexing until a typed team/fixture join is implemented. Regression tests cover the newly added context tables.

## FM Team Match Explorer integration

The C# importer now creates a dedicated `FmsaveTeamMatchFacts` index for numeric fields in `team_match_stats`. Each fact retains the snapshot, source row, competition ID, home-team ID, away-team ID, match date, original field path, numeric value and original report JSON. Identity fields are excluded from metric facts; the complete source row remains in the lossless extra-table archive.

The query API supports metric-key, competition-ID, team-ID and ISO date-range filters. Fixture context is attached only on an exact composite-key match (competition + home team + away team + date), with numeric fixture IDs normalised to text for the join. Unmatched reports remain searchable with `FixtureMatched=false`. The WPF Scouting Analytics page now opens an FM27 Team Match Explorer with those filters, match-level result grid and raw source/fixture audit detail.

This is a team-report explorer, not a player performance feed. It does not infer player involvement, player minutes, competition display names or team names from IDs. Those require validated UID-to-directory mappings and a witnessed FM27 save schema.


## Pre-testing integration — player scouting and league tables

FM Atlas now exposes the existing indexed player-stat search in a dedicated **FM Player Stats Explorer** from Scouting Analytics. It discovers metric fields present in the latest imported snapshot, applies a numeric range and an optional per-90 calculation using recorded minutes, and shows the period, minutes, matched value and exact source table for every returned row. Current-season stats, retained player-match rows, archived history and FM View exports remain visibly distinct. The view does not infer player minutes from appearances or fabricate a zero for an absent statistic.

A new **FM League Explorer** expands the nested `league_tables` rows exported by fmsave into readable standings columns while retaining the original row JSON for inspection. It shows the competition identifier and supplied name separately; a missing competition name remains blank because FM saves do not store those names by default. It displays only live tables found in the save. If no table rows exist, the UI explains that the save may not yet have a played fixture for the competition and directs the user to fixture data rather than manufacturing an all-zero table.

The Team Match Explorer build blocker was corrected by importing the data/model namespaces and declaring its captured WPF controls before the query callback. The existing exact fixture join and unmatched-report retention are unchanged.

### Pre-testing validation gates

- Windows x64 Release restore/build/test/publish must pass on the final branch head.
- Confirm an actual FM27 save against the reader's supported or borrowed-layout warning state; the adapter's structural checks do not establish that every game field has verified semantics.
- Check names and league context against in-game Data Hub / League screens. No external name map is bundled by default.
- Confirm the scouting UI on a real imported career with a large season-stat table; fmsave notes that a full current-season player stats table can contain roughly 400,000 rows.
- Verify live-memory attachment and any editor operation separately. Memory editing remains disabled pending read/write validation.
