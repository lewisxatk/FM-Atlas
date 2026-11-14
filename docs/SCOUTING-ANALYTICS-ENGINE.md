# FM Atlas — Scouting Analytics Engine

## Purpose

A Wyscout / StatsBomb-inspired recruitment workspace built into FM Atlas. The implementation uses provider-specific import adapters and a shared player-season analytics model so that source provenance and metric semantics are retained.

## Research and design references

- StatsBomb Open Data: https://github.com/hudl/open-data
  - Dataset hierarchy: competitions and seasons, match files, event files and lineups.
  - Attribution requirement: analyses published or shared from this dataset must credit StatsBomb and use its logo in accordance with the dataset terms.
- Wyscout Data Glossary: https://dataglossary.wyscout.com/
  - Metric definitions are provider-specific. For example, Wyscout progressive passes use pitch-advancement thresholds that vary according to the start/end halves; a long pass is not automatically a progressive pass.
- socceraction: https://github.com/ML-KULeuven/socceraction
  - Architectural reference for provider adapters and a common action representation, with SPADL and action-value frameworks such as xT and VAEP. FM Atlas does not copy its Python dependency into the WPF application; the architecture is used as a reference.

## Import paths

### StatsBomb Open Data

Choose the local `competitions.json` file from an extracted StatsBomb Open Data folder. FM Atlas expects the standard sibling folders:

- `matches/{competition_id}/{season_id}.json`
- `events/{match_id}.json`
- `lineups/{match_id}.json`

The importer aggregates player event counts by player, team, competition and season; derives minutes from line-up position intervals; and retains provider provenance. It reads one match's events at a time and reports progress every 25 matches.

Implemented event facts include passes attempted/completed, shot assists, goal assists where supplied, long passes (30m+), shots, shots on target, goals, shot xG, carries, dribbles attempted/completed, pressures, duels, aerial duels/wins, tackles where explicitly tagged, interceptions, recoveries, clearances, blocks, goalkeeper actions/saves/claims/punches/sweeper actions, fouls, miscontrols, dispossessions, offsides and cards.

Derived rates include pass completion %, dribble success % and aerial duel win %. Counting metrics can be normalized per 90 using lineup-derived minutes and a minimum-sample gate.

### Wyscout CSV

Choose a user-owned Wyscout player-statistics CSV export. The adapter expects player name and minutes played; player ID, team, competition, season, position, age and market value are optional. Numeric metric columns are retained as source-keyed metrics. Existing per-90 fields are not normalized a second time. Currency suffixes and common decimal/thousands formats are handled.

This is a CSV import adapter, not a Wyscout API client, event JSON importer, scraper or subscription bypass. Use data that the user is entitled to export.

### FM career data

Existing FM Scouting Matrix and Advanced Search remain available as their own source family. The new external provider records do not overwrite FM save records or alter the live-game read-only boundary.

## Analytics

- Competition, season and position-group cohorts.
- Per-90 conversion from raw event counts using actual imported minutes.
- Minimum sample minutes applied to both calculated and provider-pre-normalized per-90 metrics.
- Percentiles withheld when fewer than five players qualify for a ranking (radar requires at least three).
- Higher-is-better and lower-is-better metrics.
- Ties use mid-rank percentiles.
- Position-specific preferred metric profiles: goalkeeper, centre-back, full-back/wing-back, defensive midfielder, central midfielder, attacking midfielder, winger and striker.
- Similar-player discovery normalizes metric scales to a shared 0–100 cohort range and reports metric coverage.
- Player comparison and radar are restricted to the same provider, competition, season and position cohort.
- Undervalued-player discovery requires performance, age, market value and minutes. The transparent Atlas score weights performance percentile 65%, age 20% and relative cohort value 15%. It is a discovery heuristic, not a transfer fee prediction or vendor valuation.

## Important limitations — do not overclaim

- The initial StatsBomb adapter currently aggregates the event types listed above. It does not yet calculate provider-equivalent progressive passes/runs, final-third entries, PPDA, xT, VAEP, possession chains or tracking-derived physical metrics.
- Wyscout CSV columns preserve the source's definitions and units; the adapter does not infer missing event detail from season aggregates.
- StatsBomb Open Data is a selected public sample, not a complete global player database. Cohort sizes and season coverage depend on the files the user imports.
- Position groups are inferred from supplied lineup position labels; they are not a verified tactical role assignment.
- Goalkeeper action metrics are only as complete as the event types and goalkeeper tags supplied in the dataset.
- Age and market value are not present in every event dataset. The undervalued finder correctly returns no candidates when required fields are missing.
- The first radar compares percentile profiles, not raw values across different providers.
- xT/VAEP require validated action conversion, possession segmentation and a model/grid calibration. They are deliberately not estimated from generic counts.
- Commercial Wyscout/StatsBomb data and logos are not bundled with FM Atlas.

## Local persistence

Imported records are saved under:

`%LOCALAPPDATA%/FmAtlas/ScoutingDatasets/player-seasons.json`

Re-importing a provider replaces the previous cached rows for that provider and preserves the other provider's rows. The cache is local to the PC.

## Test coverage

Automated tests cover StatsBomb folder import and line-up minutes, event metric aggregation, Wyscout CSV parsing, per-90 normalization, minimum sample gates, cohort percentiles, lower-is-better metrics, missing/small cohorts, coverage, similar-player scaling and undervalued discovery.

## Next acceptance gates

1. Run Windows .NET 8 Release build and all xUnit tests.
2. Import a real public StatsBomb Open Data folder and compare several match/player totals against the raw source files.
3. Import a user-owned Wyscout CSV and verify headers, units, age/value and sample minutes.
4. Check the radar against manual percentile calculations for a small cohort.
5. Test large imports, cancellation, malformed JSON/CSV, missing files and recovery from interrupted local cache writes.
6. Confirm Advanced Search, FM Scouting Matrix, Player Comparison, Value Finder, Tactical Lab, Player Development and read-only FM27 connection remain unchanged.
7. Only after event-model validation, consider xT/VAEP, progressive actions and richer position/role modelling.
