# FM Atlas — Current Handover and Next Steps

**Checkpoint:** 14 November 2026  
**Current integration branch:** `feature/v9-all-roadmap`  
**Pull request:** [#5 — FM Atlas V10: FM27 Data Hub & Pre-Testing Integration](https://github.com/lewisxatk/FM-Atlas/pull/5)  
**Base branch:** `rebuild/v8-full-product-rebuild`

## Verified repository and build state

- The V10 source is on `feature/v9-all-roadmap`; do not continue on `main` or recreate the product from the recovery branch.
- The latest inspected integration checkpoint was commit `5f8dc334485d71ca39215d427921c5f6ddf1242d`.
- Windows workflow run **#585** completed successfully on a Windows x64 runner.
- .NET 8 solution restore and Release build succeeded.
- Python 3.12.10 installed and verified `fmsave 0.12.4`.
- Save-bridge Python syntax validation succeeded; both Python adapter tests passed.
- The complete xUnit suite passed: **106 passed, 0 failed**.
- Self-contained Windows x64 publish completed; `FmAtlas.exe` was present and the ZIP artifact upload succeeded.
- Artifact: `FM-Atlas-V10-win-x64`, run `43618498530`, artifact ID `14123521191`. The artifact expires on 28 November 2026.
- These checks prove compilation, automated tests and packaging. They do **not** prove a WPF launch on the user's PC, an actual FM27 save's field meanings, a store-specific live attachment, visual acceptance or release readiness.

The follow-up commits add an explicit V10 service-level hard gate around live-memory Apply/Undo, regression tests for the gate, corrected test instructions, a workflow path fix and a dispatcher-warning correction. Run the latest workflow after those commits; do not treat the earlier 106-test result as evidence for code added afterwards.

## Product foundations to preserve

This is additive development. Preserve the existing V10 foundations and do not replace them with a reduced demo:

- Monolith Glass application shell and navigation.
- Global Ctrl+K search and FMRTE-style Advanced Search, including saved criteria.
- Player profiles, Player Comparison, attribute radar, position-suitability ratings and similar-player discovery.
- Value Finder / Performance vs Value.
- Scouting Matrix, saved scouting profiles, shortlist/pipeline and shortlist handoff.
- Tactical Lab / role-fit screening and Player Development opportunity screening.
- StatsBomb Open Data event import and the separate user-owned Wyscout player-season CSV import.
- Event Lab's shared action representation, possession/passing analysis and explicitly empirical Atlas xT layer.
- FM27 fmsave adapter, offline save import, player season/match/history tables, FM Player Stats Explorer, League Explorer and Team Match Explorer.
- Read-only live-career detection and import, with save-generation/cancellation safeguards.
- Local database persistence, source provenance, missing-value handling and redacted diagnostics.

Do not claim Wyscout event-feed parity, StatsBomb commercial model parity, invented physical tracking metrics, unverified FM field meanings or an official FM position/role score.

## Immediate next gate — first controlled Windows/FM27 test

Use the latest successful `FM-Atlas-V10-win-x64` artifact from the GitHub Actions workflow on `feature/v9-all-roadmap`.

1. Extract into a new folder; preserve previous versions.
2. Back up `%LOCALAPPDATA%\FmAtlas`.
3. Open the app and verify startup, Monolith, navigation, diagnostics and database preservation.
4. Exercise Global Search, Advanced Search, player profiles, comparisons/position ratings, Similar Players, Value Finder, Scouting Matrix/shortlist, Tactical Lab, Player Development and Shortlists.
5. Export a snapshot from a COPY of an offline FM27 single-player career using the read-only fmsave bridge; import and compare representative values against FM27 screens.
6. Compare FM Player Stats Explorer, League Explorer and Team Match Explorer with the corresponding Data Hub/League Hub pages in the same save.
7. Check missing values, minutes, source tables, competition/team IDs, fixture join status and field-validation warnings.
8. Check DPI/resizing at 100%, 125% and 150% where available.
9. If testing live attachment, keep it read-only and use only a copied offline career. Check the exact Steam/Game Pass profile, game build and manager/club identity.

**Mandatory safety boundary:** live-memory Apply and Undo are hard-disabled in the V10 pre-testing build. Never test live edits in this round. If Tester & Logs does not say `Live-memory write gate: DISABLED`, stop and report it.

Full user instructions: `application/R7.2.0/FM-Atlas-R7.2.0-Work/docs/R7.2.0-USER-TESTING.md`. Checklist: `application/R7.2.0/FM-Atlas-R7.2.0-Work/docs/R7/BUILD-AND-TEST-CHECKLIST.md`. Report template: `application/R7.2.0/FM-Atlas-R7.2.0-Work/TEST-REPORT-TEMPLATE.md`.

## Remaining release gates after read-only smoke test

- Fix and classify actual defects found by the user's real Windows run.
- Verify representative FM27 player-season stats and League Hub rows against in-game screens using exact player, competition and season context.
- Verify save bridge export/import against the exact FM27 build; keep unconfirmed fields labelled as such.
- Validate Steam and Game Pass attachment separately where applicable; a PE/profile fingerprint is not a field witness.
- Test save switching, process restarts, scan cancellation and snapshot isolation on copied careers.
- Finish systematic Monolith layout, scaling, contrast, navigation, empty/loading/error-state and performance review.
- Reconcile StatsBomb event conversion against the public raw source on identical matches; test malformed/incomplete imports, cancellation and large datasets.
- Review installer, first install, upgrade, uninstall, local DB preservation and rollback.
- Only after independent disposable-save field witnesses and rollback sign-off should live editing be reconsidered. It is not part of V10's first testing round.

## Development continuation after the first test

Prioritise real defects and data fidelity over another speculative feature batch. Then:
1. Resolve field mapping gaps and increase save schema fixtures only from real, privacy-safe source samples.
2. Finish xT/action edge cases and model version/sample-quality reporting; VAEP remains a separate future model and requires its own training/validation.
3. Expand Advanced Search grouped AND/OR logic and custom result columns only where the query engine can support them correctly.
4. Finish Monolith consistency across secondary windows, dialogs and charts.
5. Complete installer/update flow and release-grade diagnostics.
6. Re-run the complete Windows build/test/publish gate after every material code change.

## Repository safety and workflow

- Continue on `feature/v9-all-roadmap` and PR #5. Do not merge to `main` or base branch until acceptance gates pass.
- Keep `recovery/r7.2.0-source-import`, `rebuild/v8-full-product-rebuild` and prior release copies intact.
- Every development checkpoint must have a Git commit, changed-file record, exact test evidence, known risks and next action.
- Never add real FM career saves, personal databases, credentials, licensed facepacks/logos or user secrets to Git.
- Keep raw imported values and source provenance; unknown is not zero.
