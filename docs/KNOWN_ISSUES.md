# FM Atlas V10 — Known Issues and Verification Gaps

**Last reviewed:** 14 November 2026  
**Integration branch:** `feature/v9-all-roadmap`  
**Tracking PR:** [#5 — V10 FM27 Data Hub & Pre-Testing Integration](https://github.com/lewisxatk/FM-Atlas/pull/5)

This document separates verified automated results from work that still requires a real Windows/FM27 environment. A passing build is not the same as user acceptance.

## Verified automated baseline

At the inspected initial V10 integration checkpoint, Windows workflow run #585 completed successfully:

- .NET 8 solution restore and Release build: passed.
- Python 3.12.10 and `fmsave 0.12.4`: installed and version-checked.
- Save-bridge syntax and Python adapter tests: passed (2 tests).
- xUnit suite: 106 passed, 0 failed.
- Self-contained Windows x64 publish and artifact upload: passed.

The follow-up safety-gate and documentation commits require a fresh workflow run before they can be treated as part of the verified build. The earlier CI result must not be retroactively presented as testing those later changes.

## Critical — live-write safety

- The source contains a guarded low-level FM memory writer, but the exact field encodings and offsets have not been independently witnessed against the user's exact FM27 build.
- V10 pre-testing must be read-only. Apply and Undo are hard-disabled at the service boundary; automated tests verify they reject calls before opening the game process.
- Tester & Logs must report `Live-memory write gate: DISABLED`.
- Process/build fingerprint detection is not a certification of all field mappings.
- Do not attempt Apply, Undo or manual memory writes in the first controlled test round.
- Hidden fields, personality, position, staff, club, contracts, transfers and loans remain outside the supported writer scope.

## Requires user-side Windows/FM27 acceptance

- The application still needs to be launched on the user's actual Windows 10/11 PC. CI compilation and publish do not prove startup, DPI, GPU/rendering or navigation behaviour on that machine.
- Monolith Glass needs visual review at common resolutions and Windows scaling (100%, 125%, 150%), including secondary windows, dialogs, tables, charts and empty/loading/error states.
- FM27 live process attachment needs separate read-only checks for the exact Steam/Game Pass edition, executable/module fingerprint, game build, loaded career generation and manager/club identity.
- The fmsave import needs validation against a copy of an actual FM27 career. Structural parsing and manifest counts do not prove every field's in-game meaning.
- Representative player stats must be compared with the matching FM27 Data Hub player, competition and season.
- League tables must be compared with the same in-game League Hub/competition state. Empty tables are not evidence of a zero-point table.
- Team-match reports may be sparse. Fixture joins are only considered matched on the full competition/home team/away team/date key; unmatched reports remain visible.
- Save switch, cancellation and re-import behaviour need testing against copied careers.
- Installer, upgrade, uninstall and database rollback/preservation still require a separate acceptance round.

## Scouting analytics limitations

- StatsBomb Open Data coverage depends on the selected public competition/season/match files; it is not a complete global player database.
- Wyscout support is for user-owned player-season CSV exports. It is not a Wyscout API client or event-feed implementation.
- StatsBomb event actions and FM save season statistics remain separate source families. Do not merge them into one total or silently substitute one for the other.
- Atlas xT is an empirical model trained on imported event samples and is not claimed to be identical to socceraction, StatsBomb or a commercial model.
- VAEP, full provider-equivalent progressive-action definitions, PPDA and tracking-based physical metrics must not be shown as available unless their own event definitions, inputs and validation are implemented.
- Event-derived per-90 metrics require validated lineup/substitution minutes. Do not infer minutes from appearances or event counts.
- Cross-league performance comparisons require defensible cohort calibration; otherwise show league-relative results with the caveat visible.
- Undervalued scoring is an Atlas discovery heuristic, not a fee prediction or third-party valuation.

## Data integrity rules

- Missing fields remain unavailable; they are never silently converted to zero.
- Missing player minutes are not estimated.
- Competition/team display names are not inferred from IDs without a verified mapping.
- FM-native facts, external season metrics and event-level actions retain distinct provenance.
- Provisional or inherited field offsets stay labelled unverified until confirmed against a real FM27 in-game witness.
- A source count, parser success or UI display alone does not prove semantic correctness.

## Follow-up checks after CI

- [ ] Latest workflow passes with the V10 live-write hard gate and its new regression tests.
- [ ] The V10 diagnostics status explicitly reports the hard-disabled gate.
- [ ] No stale user-test instructions ask for live Apply/Undo.
- [ ] The WPF dispatcher warning is resolved without changing deferred profile restoration behaviour.
- [ ] The correct workflow file path is present in push/PR path filters.
- [ ] User-side first launch and read-only FM27 test report is received.
