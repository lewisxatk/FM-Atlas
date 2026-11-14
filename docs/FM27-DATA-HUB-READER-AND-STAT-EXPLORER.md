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

The export allow-list now includes `team_match_stats`, `officials` and FM27 `squad_planner`, alongside the existing player season, match and history tables. The adapter preserves each table's original named fields in NDJSON. It does not convert a field to a friendly metric label or silently reinterpret a raw value.

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

1. Verify that the C# bundle importer recognises the added NDJSON table names and indexes team-match / league context rather than merely archiving it as an unknown extra table.
2. Add real FM27 save fixtures (redacted/synthetic test fixtures only where they preserve the real schema) for Data Hub season stats, team match reports, fixtures and league tables.
3. Validate a known FM27 build and representative fields against the in-game Data Hub and League Hub. Keep all un-witnessed fields flagged.
4. Make Stats Explorer filters include source, player, club/team, competition, season, match date, position, minutes and arbitrary numeric metric ranges.
5. Add join-quality indicators for player IDs and fixture keys; unresolved joins remain visible and excluded from derived rates.
6. Preserve existing Advanced Search, saved profiles, shortlists, comparisons and read-only save import.
