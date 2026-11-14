# FM Atlas V10 — Advanced Football Analytics

## External research and reverse-engineering notes

The implementation follows the architecture and concepts documented by:
- KU Leuven DTAI Sports Analytics Lab, `socceraction`: provider-specific loaders, SPADL conversion, common action schema, pitch-direction normalisation, xT and VAEP as separate valuation frameworks.
- StatsBomb Open Data: competition/season match files plus match-keyed events, lineups and selected 360 files. Source attribution is required when publishing or sharing analysis.
- StatsBomb player/event glossary: event facts have explicit definitions; metric labels should not be inferred from vaguely similar events.

Sources:
- https://socceraction.readthedocs.io/en/latest/
- https://socceraction.readthedocs.io/en/latest/documentation/intro.html
- https://github.com/ML-KULeuven/socceraction
- https://github.com/hudl/open-data
- https://support.hudl.com/s/article/event-data-glossary-player-metrics?language=en_US

We have implemented the architecture pattern, not copied proprietary commercial code. The MIT-licensed `socceraction` project is a useful reference; its documentation requests attribution when its software is used in a product. FM Atlas currently uses its concepts as research guidance and has its own C# implementation. No commercial Wyscout data, credentials or restricted code is bundled.

## V10 implementation

### Unified event actions
- Adds a shared action record for match, competition, season, period, clock, possession, team, player, recipient, event/action type, result, start/end coordinates, shot, goal, xG, provider and source event ID.
- Adds StatsBomb Open Data conversion for supported pass, carry, shot, dribble, pressure, tackle/duel, interception, recovery, clearance, block and foul events.
- Normalises pitch coordinates to a 105 × 68 attacking-direction coordinate system by team and period.
- Preserves recipient IDs/names for directed passing networks.
- Keeps provider-specific adapters explicit; Wyscout player-stat CSV is not misrepresented as Wyscout event feed.

### Empirical Expected Threat
- Fits a 16 × 12 grid from observed shots, goals and successful pass/carry transitions in the selected dataset.
- Calculates scoring probability, move probability and transition probabilities by pitch zone.
- Iteratively calculates zone threat values.
- Values successful passes and carries as end-zone xT minus start-zone xT.
- Excludes failed passes, shots and defensive actions from xT contribution.
- Carries model name, source, match/action sample size and training timestamp.

This is an Atlas empirical xT model, trained on the imported sample. It is not claimed to be numerically equivalent to the published `socceraction` implementation or a commercial model. It must be reconciled on the same match sample before being used for cross-product comparisons. xT is not VAEP.

### Possession and passing analysis
- Summarises provider possession IDs into match/team possession chains.
- Reports actions, passes, carries, shots, goals, xG, xT contribution, final player and outcome.
- Builds directed passer-to-recipient edges, pass counts, completion counts and average pass start/end locations.
- Provides player impact summaries for event totals, xG, xT, pass completion, pressures, tackles, interceptions, progressive passes/carries and final-third entries.
- Adds pitch view with xT zone intensity and observed pass lanes.
- Does not infer player minutes from event counts. Event-derived per-90 values remain unavailable until joined to validated lineup minutes.

### Scouting/recruitment
- Existing V9 player-season analytics, position profiles, percentiles, similar-player search, comparisons/radar, undervalued discovery and Scouting Matrix handoff are preserved.
- Event Lab adds a separate action-derived layer rather than silently overwriting player-season provider facts.

## Metric semantics and limitations

- Progressive passes/carries in the Event Lab currently use a transparent distance-to-goal threshold heuristic (30/15/10 metres based on start location) on the normalised pitch. This is an Atlas heuristic, not a provider-supplied metric.
- Final-third entries are successful/attempted pass or carry endpoints crossing the 80m x-coordinate threshold in the attacking direction.
- Passing networks use explicit StatsBomb pass-recipient fields; they are not tracking-derived influence networks.
- Possession chains use provider possession IDs; they are not tracking-derived possession reconstruction.
- The xT fit is dataset-specific and its reliability depends on sample size, league mix, season mix and event coverage.
- VAEP is not implemented: it requires sequence features, possession-state labels, and trained probabilities of scoring/conceding within a defined horizon.
- No physical tracking, off-ball runs, speed, distance covered, pressure intensity or spatial control is inferred.

## Automated tests added
- xT grid shape and finite fitted values.
- Successful pass/carry valuation and exclusion of failed actions/shots.
- Minimum training-match gate.
- Possession chain summaries and goal outcome.
- Player pass completion and explicit unavailable xT per-90 without minutes.
- Recipient-based passing network aggregation.

## Required validation before release
1. Windows Release build, xUnit and Python adapter suite.
2. Compare action conversion counts and representative events against source JSON and `socceraction` on identical StatsBomb matches.
3. Edge-case fixtures for own goals, penalties, goalkeeper events, extra time, substitutions, missing locations/recipients and event outcomes.
4. Join lineup-derived minutes with substitution/position intervals before any per-90 player rankings.
5. Add match/team/player/period filters and model version/persistence.
6. Validate xT model stability across separate competition/season samples.
7. Implement VAEP only with a separately trained and validated model.
8. Perform Windows UI/DPI and existing-feature regression checks.

## Preservation
Additive changes only. Preserve Advanced Search, saved profiles, player profiles/comparisons/position ratings, Value Finder, Scouting Matrix, role-fit Tactical Lab, Player Development, offline save import, read-only FM27 connector, Monolith Glass and live-write safety gate.


## CI compile corrections (14 November 2026)
The first V10 Windows Release compile reached the C# build and exposed two integration issues: the Event Lab window referenced a WPF type name where the owner theme brush was intended, and the new StatsBomb reader needed the System.IO namespace. Both are corrected in the current branch. The Python adapter suite passed (2 tests) and solution restore passed on the first CI attempt; the next Windows run must confirm the C# fixes and continue through xUnit and self-contained publish.
