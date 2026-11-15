# FM Atlas Scouting Engine — Reverse-Engineering Notes (2026-11-14)

## Reference products and patterns reviewed

### FMSuperScout
Repository: https://github.com/mavarobli/FMSuperScout

Observed patterns from its public project description and release notes:
- Whole-career player/staff extraction followed by fast local search.
- Attribute filters, saved presets, player comparisons, role ratings, squad analysis and shortlist export.
- Explicit user control over hidden attributes and player-data presentation.
- Source-aware value estimates are distinguished from directly read values.

FM Atlas adaptation:
- Preserve the existing career importer, local SQLite index and Advanced Search.
- Keep reusable recruitment criteria separate from save facts.
- Use atomic local profile persistence and bounded conditions.
- Keep unverified live memory fields labelled provisional; never promote a candidate offset based only on a plausible-looking number.

### FM Dossier
References:
- https://www.fmdossier.dev/features/players
- https://www.fmdossier.dev/changelog

Observed patterns:
- Search and dossiers share the same filtered player context.
- Comparison is side-by-side and includes season statistics and radar overlays.
- Shortlists are a durable recruitment workflow, not just a temporary selection.
- Role suitability is explicitly identified as the product's calculation rather than Football Manager's own rating.
- Scout knowledge and exact values are separated; hidden or missing knowledge should not leak into reports.

FM Atlas adaptation:
- Keep the source 1–20 FM position familiarity distinct from Atlas role-fit calculations.
- Route selected profile matches into the existing shortlist database, preserving evidence in notes.
- Do not use profile match score as an FM rating or player quality score.

### FM Scouting Tool 26
Reference: https://www.fmscout.com/a-fmst-26.html

Observed patterns:
- User-defined metrics, custom player pools, position benchmarks, percentile filters and grouped conditions.
- Shortlists and saved metric definitions are part of the scouting workflow.

FM Atlas adaptation:
- Added a reusable profile engine with multi-metric conditions, position/league cohorts, minimum minutes/sample and coverage gates.
- Added tie-aware empirical percentiles with cohort sizes and explicit warnings for missing target values or small cohorts.

### StatsBomb Open Data
Reference: https://github.com/statsbomb/open-data

The event schema includes individual events such as passes, carries, shots and pressures, plus contextual fields. These are not equivalent to aggregate player-season statistics. An event-level metric such as progressive carries or pressures per 90 requires suitable event records and definitions, not a guess from goals, assists or total passes.

FM Atlas boundary:
- FM career season statistics can support per-90 and cohort percentile calculations when the metric and minutes are present.
- StatsBomb/Wyscout event metrics must remain unavailable unless an event-data provider/import is connected and the exact definition is documented.
- Do not label ordinary FM aggregate statistics as StatsBomb data.

## Implemented code in this milestone

- ScoutingProfileModels.cs — multi-condition profile and provenance contracts.
- ScoutingProfileStore.cs — local atomic JSON profile persistence.
- ScoutingProfileEngine.cs — strict profile matching, per-90 calculations, minutes/sample/coverage gates.
- PlayerStatComparisonService.cs — empirical percentiles for overall, position, league and combined cohorts.
- ScoutingShortlistBridge.cs — adds selected matches through the existing ScoutDatabase shortlist method and stores source, timestamp, coverage, sample, minutes and missing fields in the shortlist note.
- ScoutingProfileEngineTests.cs — tests for profile persistence, metric gates, per-90 and tie-aware percentile behaviour.
- FM27-POSITION-RATING-WITNESS-REPORT.md — exact-build witness checklist for the 15 imported position slots.

## Remaining integration work

The new core services are not yet a finished WPF Scouting Matrix UI. Remaining work includes profile CRUD controls, results table, cohort selector, player report integration, explicit bulk shortlist action, and shared Monolith Glass treatment across the main shell, windows, dialogs and chart states. Run the Windows build and tests before treating this milestone as accepted.
