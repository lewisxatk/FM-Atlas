# FM Atlas V10 — Advanced Football Analytics Engine

## Research basis and architecture decisions

This milestone follows the public architecture documented by the KU Leuven DTAI Sports Analytics Lab's `socceraction` toolkit:
- Provider-specific StatsBomb/Wyscout/Opta loaders.
- Conversion from provider event schemas to the shared SPADL action representation.
- Common action fields and pitch orientation before valuation.
- xT and VAEP as distinct action-valuation approaches.

References:
- https://socceraction.readthedocs.io/en/latest/
- https://socceraction.readthedocs.io/en/latest/documentation/intro.html
- https://github.com/ML-KULeuven/socceraction
- https://github.com/hudl/open-data

StatsBomb Open Data uses `competitions.json`, competition/season match JSON, and match-keyed `events/` and `lineups/` JSON; selected matches also contain `three-sixty/`. Public dataset use requires source attribution when publishing or sharing analysis. FM Atlas does not bundle the dataset.
Wyscout event data is not the same as a Wyscout player-season CSV export. FM Atlas only imports user-owned authorised CSVs and does not scrape, bypass access controls or claim to support a commercial Wyscout event feed.

## Implemented in this milestone

### Unified event action layer
- Adds a common action record with match, competition, season, period, clock, possession, team, player, recipient, event/action type, result, start/end coordinates, shot/xG/goal facts, provider and original event ID.
- Imports supported StatsBomb Open Data events into the shared action model.
- Normalises coordinates to a 120 × 80 attacking-direction pitch by team and period.
- Preserves provider event identifiers and event ordering.
- Keeps the StatsBomb adapter explicit rather than pretending that provider schemas are interchangeable.

### Empirical Expected Threat
- Fits a 16 × 12 grid from the imported event sample.
- Estimates zone scoring probability, move probability and observed successful-move transitions.
- Iteratively solves the zone threat values from that dataset.
- Values successful passes and carries by end-zone threat minus start-zone threat.
- Does not assign xT to shots, failed passes or defensive actions.
- Stores model name, training source, match/action sample counts and timestamp.

This is a transparent, dataset-specific empirical xT model. It is not a claim of parity with a commercial model or with the socceraction implementation until cross-validation and numerical reconciliation have been completed. Do not compare xT values from separately trained datasets without calibration.

### Possession and network analysis
- Groups actions into team/match possession chains.
- Summarises passes, carries, shots, goals, xG, xT added, final action and possession outcome.
- Builds directed passing edges from StatsBomb pass recipient fields, including pass count, completion count and average start/end locations.
- Adds a player-impact summary including passes, carries, shots, goals, xG, xT contribution, pressures, tackles, interceptions, progressive passes/carries and final-third entries.
- Does not infer minutes played from event count; xT per 90 remains unavailable unless a validated minutes source is joined.

### User interface
- Adds Event Lab entry from the scouting workspace.
- Shows player impact, possession chains, passing network and a capped first-5,000 action audit view.
- Runs import and model fitting in cancellable background work.
- Displays the training sample and explicit limitations alongside the results.

## Definitions and limitations

- Progressive pass/carry in this first pass uses a documented distance-to-goal threshold heuristic on normalised pitch coordinates (30/15/10 metres by starting distance). It is not represented as an exact StatsBomb/Wyscout provider field.
- Final-third entry is a pass/carry crossing the 80m x-coordinate.
- Passing network is event-recipient based; it is not a tracking-derived spatial influence network.
- Possession chains use provider possession IDs and are not a reconstructed tracking-based possession model.
- xT is trained from observed shots and successful pass/carry transitions; sample size and dataset composition materially affect it.
- VAEP is not implemented by this code slice. It requires action-sequence feature engineering, possession-state labels and trained scoring/conceding probability models. Do not label xT as VAEP.
- No tracking-derived speed, distance covered, off-ball movement or pressure intensity is invented.

## Automated tests added

- Empirical xT model shape and finite outputs.
- Successful pass/carry valuation and exclusion of failed actions/shots.
- Minimum training-match gate.
- Possession chain summaries and goal outcome.
- Player pass completion and unavailable per-90 values without validated minutes.
- Recipient-based directed passing network aggregation.

## Remaining validation gates

1. Run Windows Release build and all xUnit/Python tests.
2. Reconcile action counts and representative match event IDs against the source JSON.
3. Compare action conversion and xT outputs against `socceraction` on the same StatsBomb matches; record tolerances and any deliberate mapping differences.
4. Add event-type edge-case fixtures (own goals, penalties, goalkeeper events, advantage, substitutions, extra time, missing recipients, missing locations).
5. Join lineup-derived player minutes and validate substitutions/position intervals before adding event-derived per-90 values.
6. Add calibrated competition/season model cohorts and avoid pooling unrelated competitions by default.
7. Add visual pitch plots for xT actions and passing networks, with filters for match, team, player and period.
8. Only then consider VAEP, xT model export/versioning and multi-provider Wyscout event adapter.

## Preservation
This work is additive. Advanced Search, existing player comparisons/position ratings, Value Finder, Scouting Matrix, role-fit Tactical Lab, Player Development, offline save import, read-only FM27 connector and the live-write safety gate remain in place.
