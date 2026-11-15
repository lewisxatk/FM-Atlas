# FM27 statistics coverage and provenance

**Working branch:** `rebuild/v8-full-product-rebuild`  
**Reader:** fmsave 0.12.5  
**Status:** automated fixtures pass; real FM27 Game Pass save verification remains outstanding.

## Player data

- `player_season_stats`: current-season statistics. The reader returns separate rows by competition type and team. Atlas preserves each row using competition kind, club, team slot/team ID and row index rather than merging them into one generic season.
- `player_season_history`: archived season/competition/team rows. Atlas uses the actual season year and competition reference when available.
- `player_match_stats`: individual match rows retained by the save, with date, opponent, competition and opponent team slot in the period identity. This is not a complete career match log; the save only retains the match records it exposes.
- Numeric fields are indexed from the save rows. Missing or null fields are omitted/shown as unavailable, never fabricated as zero.
- Derived per-90 or percentage fields supplied by the reader must remain identifiable as reader-derived from save statistics, not external scouting-site data.

## Club data

- Club directory rows provide team IDs and team slots.
- `league_tables` is filtered to the selected club row before its table facts are shown.
- `fixtures` supplies opponent names, competition and score context.
- `team_match_stats` supplies one team's statistics for one match. Atlas joins it to fixtures using competition ID, home team ID, away team ID and date.
- The match-report view exposes the available game fields, including shots, shots on target, blocked shots, shots outside the box, clear-cut chances, woodwork, xG/npxG, possession, ball distribution, passes, final-third/progressive passes, crosses, tackles, aerials, distance, fouls, offsides, cards and corners.
- Progressive passes may be absent when the save did not retain a detailed report. Missing fields stay unavailable.

## Provenance and limits

- No external website statistics are injected into player or club records.
- fmsave documents `team_match_stats` as team-level match-screen data and describes current-season player stats as separate competition-type/team rows. See the [fmsave API documentation](https://rhiever.github.io/fmsave/).
- The reader's field validation is build-specific. Until the user's installed FM27 build is cross-checked against the in-game screens, fields are marked provisional where appropriate.
- The user's Game Pass save must be tested on a disposable copy before any live-write work is enabled.
- Live editing is a separate capability from reading. Only field mappings with exact build-specific evidence, preflight checks, read-back and rollback certification may be enabled.

## Automated coverage

The integration suite includes fixtures that verify:
- two player current-season rows with different competition types remain distinct;
- club team IDs join to fixture context;
- team match-report values such as shots, xG, possession and progressive passes survive import.

These tests prove the importer and joins against controlled fixtures, not the accuracy of an arbitrary real FM27 career save.
