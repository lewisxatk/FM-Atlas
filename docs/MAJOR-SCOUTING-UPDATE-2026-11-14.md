# FM Atlas V8 — Major Scouting Update

Branch: rebuild/v8-full-product-rebuild  
Baseline head before this update: 917ebfe837ffb6965ab6a1c0706ad9315d0d859e

## Implemented in this update

- Added reusable scouting profile models supporting 1–40 metric conditions, per-90 requirements, minutes/sample gates, age/value constraints, position/league cohort selection and minimum data coverage.
- Added atomic local profile persistence under the user's LocalApplicationData FmAtlas scouting-profiles folder.
- Added a multi-stat evaluation engine that rejects candidates with missing required values, insufficient minutes/sample size, inadequate data coverage or mismatched cohort.
- Added tie-aware empirical percentile calculations for overall, position, league and combined position/league cohorts.
- Percentile output carries source name, source timestamp, cohort size and missing-data warnings. A percentile is withheld if the target value is missing or the reference population is too small.
- Added a dedicated FM27 position-rating witness report. Existing 15-slot display remains source-backed and explicitly distinguishes imported facts from unverified live-memory offsets.
- Added unit tests for profile persistence, multi-stat gating, coverage, per-90 scaling, tie-aware percentile ranking and missing-value behaviour.

## Important integration boundary

The new profile engine and percentile service are reusable application services. This commit does not claim that a complete new scouting dashboard, UI profile editor, automatic shortlist insertion or all-window Monolith restyling is finished. Those require wiring the services into the existing WPF navigation, shortlist commands and shared visual components, followed by Windows build and UI acceptance. Existing Advanced Search and shortlist workflows remain unchanged.

## Data integrity rules

- No event-level StatsBomb/Wyscout metrics are fabricated from season aggregates.
- A per-90 requirement is calculated only from a supplied numeric metric and a positive minutes total.
- Missing values are not zero.
- Percentiles use only the supplied reference cohort and disclose its size.
- Low coverage and low sample observations are excluded by profile gates.
- Source and update timestamp are retained with each evaluated candidate.
- FM27 live position memory offsets remain unverified until exact-build in-game witnesses are recorded.

## Next integration sequence

1. Bind profile CRUD and multi-condition builder into the Scouting Matrix UI.
2. Add an explicit Add selected matches to shortlist action using existing ScoutDatabase.AddToShortlistAsync and existing shortlist pipeline.
3. Add comparison report export and per-90/percentile columns to Player Comparison.
4. Finish shared Monolith Glass tokens across the main shell, modal windows, DataGrid, charts, empty/error/loading states.
5. Run Windows x64 build, all tests, launch smoke test, and FM27 witness acceptance.
