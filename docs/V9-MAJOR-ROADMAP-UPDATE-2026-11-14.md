# FM Atlas V9 — Consolidated Roadmap Update

Date: 14 November 2026  
Branch: `feature/v9-all-roadmap`  
Base: V8 full-product rebuild at `ab4c17f7e5704daa187e5c5e4649848fc23ab93b`

## Purpose

Deliver the next coherent development milestone on top of V8. Preserve the R7.2.0 WPF/.NET 8 application, Advanced Search, player profiles, comparison, similarity, shortlists, saved scouting profiles, database, automatic read-only FM27 connection and Monolith Glass direction.

## Implemented in this source update

### 1. Scouting Matrix → recruitment workflow

- Added an explicit **Add matches to shortlist** action to the existing Scouting Matrix.
- The action confirms the full result count before writing.
- Adds matching player identities to the existing `Scouting Matrix` shortlist through `ScoutDatabase.AddToShortlistAsync`.
- Each shortlist note retains the matched period, minutes where supplied, and the exact matched metric summary.
- Existing multi-condition AND/OR search, per-90 sample gate, saved profile CRUD, FM View CSV import and CSV export are retained.
- Missing saved metric fields continue to remain missing rather than silently selecting another metric.

### 2. Tactical Lab → source-backed role screening

- Replaced the placeholder Tactical Lab page with an interactive role-fit screening workspace.
- Uses the existing `RoleFitService` role definitions and `SearchRoleFitCandidatesAsync` database query.
- Includes role selection, age, CA, PA, position-text and data-coverage filters.
- Includes cancellable searches, retained last completed results, source-evidence/coverage columns, player-profile drill-through and CSV export.
- Every score is labelled as an Atlas estimate, not Football Manager's hidden role formula or official star rating.
- Does not invent tactical instructions, role duties or position familiarity.

### 3. Player Development → opportunity finder

- Added a dedicated Player Development navigation destination.
- Merges the available offline and live player identity sources and deduplicates by name, club and age, preferring the more complete record.
- Filters by age, CA, minimum CA–PA headroom, maximum value and imported position label.
- Ranks candidates by CA–PA headroom, then age and CA.
- Exports source IDs, player identity, CA, PA, headroom, source-unit value and evidence source.
- Excludes missing/invalid CA or PA and negative PA range values from headroom calculations.
- Explicitly describes headroom as a snapshot screening difference, not a prediction of growth.

## Preserved capabilities

- Universal Advanced Search and its existing field/query model.
- Player and staff directories, player profiles and closable profile tabs.
- Player Comparison and its source-backed attribute radar / position-rating comparisons.
- Similar-player workflow.
- Existing Value Finder and Performance vs Value analysis.
- Role Lab and custom role templates.
- Existing shortlists and recruitment pipeline.
- Existing FM27 live-career reader and offline save importer.
- Monolith Glass as the primary application appearance.
- Live memory editing remains disabled.

## Research and reverse-engineering approach

This milestone builds on the research register and scouting-engine notes already in the V8 branch:

- FMSuperScout patterns for whole-career extraction, indexed local search and source-aware screening.
- FM Dossier patterns for shared search/profile context, side-by-side comparisons and durable shortlists.
- FM Scouting Tool patterns for user-defined metrics, cohort screening and saved criteria.
- StatsBomb Open Data event semantics: event-level progressive carries, pressures and similar metrics are not inferred from aggregate FM season totals.

No third-party offsets or proprietary scoring formulas are copied. Atlas estimates remain separately labelled and source evidence is retained.

## Explicit remaining gates — not to be misreported as complete

1. **Historical player development:** the current import model does not provide a validated longitudinal attribute-snapshot history for every player. This update provides CA/PA opportunity screening, not invented growth curves or development forecasts.
2. **Full formation simulator:** tactical save tables are present in some imports, but the current reader does not yet decode every formation, duty, team instruction and tactical effectiveness field into a validated model. Tactical Lab therefore screens role fits without pretending to simulate a tactic.
3. **External event data:** Wyscout/StatsBomb event-level metrics require a connected/imported event dataset and documented metric definitions. They are not fabricated from FM aggregate statistics.
4. **Monolith Glass all-window finish:** the main shell and shared workspace surfaces are in place, but custom modal/dialog, chart, empty/loading/error and high-contrast states still need a systematic pass across every secondary window.
5. **Comparison and position-rating witnesses:** the UI and 15-slot source mapping exist, but exact-build FM27 in-game witness capture remains necessary before promoting provisional live offsets.
6. **Windows acceptance:** V9 now passes the Windows .NET 8 Release build, all 87 xUnit tests, Python adapter tests and self-contained x64 publish. In-game attachment, UI/DPI and save-switch acceptance remain open.
7. **Release packaging:** installer, upgrade, uninstall, database-preservation and rollback acceptance remain open.

## Next step after implementation

The automated V9 build/test/publish gate has passed. Next perform the planned Windows 10/11 runtime review, DPI review, FM27 Steam/Game Pass attachment tests, Advanced Search regression, Scouting Matrix shortlist round-trip, Tactical Lab role-screening checks, Player Development source checks and save-switch/cancellation acceptance.

Do not merge or distribute as release-ready until these gates pass.


## Stage 1 build correction — 14 November 2026

The first V9 Windows Release build restored successfully and passed the Python save-adapter and supported fmsave-reader checks, but compilation stopped in `MainWindow.Intelligence.cs`. The compiler reported CS0165 (captured local variables used before definite assignment) and CS0841 (the Tactical Lab cancellation variable referenced before declaration). The UI event-handler lambdas captured controls and state declared later in the method. The correction moves the Tactical Lab results/cancellation state and dependent controls before the handlers, and moves the Player Development action-button handlers until after the result collections they capture are declared. This is a compile-order correction; it does not change the feature logic. A fresh Windows build is required to verify the correction and discover any subsequent issues.

A follow-up compile pass exposed a second definite-assignment case: the Tactical Lab search/cancel buttons were captured by a local async function. Their variables are now explicitly declared before callback wiring and assigned before the search function is invoked. The final correction was validated by Windows workflow run #444 on commit `d03f0fc56558eb4c852e4c2ab56e5f06e7c84baf`: Release build passed, all 87 xUnit tests passed, Python adapter checks passed, and self-contained win-x64 publish plus artifact upload succeeded. Two non-blocking compiler warnings remain: one unused local function in `ScoutDatabase.cs` and one deferred dispatcher call in `MainWindow.xaml.cs`.


## Stage 2 — scouting analytics engine foundation

The first Stage 2 code slice adds `ScoutingAnalyticsService` and dedicated xUnit coverage.

- Canonical metric facts carry stable keys, labels, units, rate/per-90 semantics, directionality, sample-minute gates and source family.
- Per-90 values are calculated only when the metric is explicitly declared a rate and valid minutes are present; raw totals remain available.
- Cohort percentiles can be scoped by competition, season and position group. Percentiles are withheld when the cohort is below the configured minimum.
- Higher-is-better and lower-is-better metrics are supported; tied values use a mid-rank percentile.
- Coverage counts only finite, source-present metric facts.
- Player-season records retain source name/version and whether facts are event-level. No FM aggregate field is relabelled as a Wyscout/StatsBomb event metric.

### Stage 2 remaining implementation slices

1. Import adapters for StatsBomb Open Data and documented user-supplied event datasets, with schema/version provenance and explicit event-to-metric definitions.
2. Position/role-specific metric catalog (progression, chance creation, finishing, defending, pressing, carrying, aerials, goalkeeping), each with sample gates and source semantics.
3. Player-season/cohort storage and reproducible re-computation when a new import arrives.
4. UI: Wyscout-style filter builder, league/position percentiles, radar and side-by-side comparison, metric explanations and source/coverage indicators.
5. Undervalued-player model combining market value/fee with cohort performance, age, contract context and uncertainty; no opaque single score until inputs are validated.
6. Regression and fixture tests for every importer, metric definition, per-90 conversion, cohort boundary, missing-data case and percentile tie.

The first code slice is an analytics foundation, not a completed Wyscout/StatsBomb feature. Event-level metrics remain unavailable until a real event dataset is imported and mapped.
