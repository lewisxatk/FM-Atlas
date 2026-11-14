# FM Atlas V8 — current handover

**Date:** 2026-11-14  
**Repository:** `lewisxatk/FM-Atlas`  
**Branch:** `rebuild/v8-full-product-rebuild`  
**Application:** `application/R7.2.0/FM-Atlas-R7.2.0-Work/`

## Current objective

Continue the integrated V8 rebuild without replacing existing R7.2.0 functionality. Preserve the Monolith Glass shell, universal Advanced Search, saved search profiles, Players and Staff workspaces, profile tabs, shortlist pipeline, analytics, squad analysis, local editor history, save-import pipeline and all existing source-backed features.

## Work inspected and carried forward

- Live career detection uses the exact FM process/build profile and save-load generation counter.
- The read-only live extractor contains staff and human-manager recognition, including staff CA/PA, age/DOB, nationality, club relationship and role/job mapping.
- The database already contains transactional live staff persistence and the `LiveCareerIdentity` record.
- The Staff directory already merges offline-save and live staff rows, exposes CA/PA and other identity columns, and loads additional staff facts into the selected profile.
- `GetFmsaveStaffFactsAsync` already strips the `LIVE:` prefix before reading `LiveStaffFacts`; this is covered by the existing `StaffSearchProfileAndCareerIdentityUseTheConnectedLiveManager` integration test.
- The live staff query now supports name, club and UID matching; the integration test covers the linked club and UID search paths.
- FM27 preferred-job ratings are now modelled independently using the public 15-group layout: a class-scoped pointer at person+0xD0, 15 byte ratings, 1–20 validation, tie-aware top-group selection and provisional fact persistence. The Staff profile displays the ratings without changing existing directory/search behaviour.
- FM27 staff coaching attributes now use the public FMLibreEditor layout: 19 raw 0–100 fields are converted with the documented `(raw + 2) / 5` display scale, while authority and working-with-youngsters are retained as direct 1–20 ratings. These are persisted as provisional `staff_attributes.*` facts and appear in the existing Staff profile.
- The staff attribute decoder has regression tests for documented offsets, display conversion, direct ratings, out-of-range omission and missing bytes. These tests are committed but have not yet been run by a fresh Windows workflow.
- The same reference mapping now supplies eight staff personality ratings (using the +0x340 human-manager identity shift) and contract facts where readable: weekly wage in GBP, signed date, expiry date and joined-club date. These are stored as provisional profile facts; no unsupported contract field is filled with a default.
- Staff profile facts are grouped by their source keys and rendered by the existing profile view; the new fields do not create a parallel staff directory or replace the established search flow.
- Reference findings and limitations are recorded in `docs/architecture/FM-ATLAS-V8-REFERENCE-TOOL-MAPPING-NOTES.md`; the implementation deliberately keeps group 7 unresolved and recruitment/performance analyst preferences combined.
- Players search is paged at 250 rows, merges local and live source rows before paging, prefers the most complete source row, and restores open profile tabs after navigation. Existing Advanced Search must remain intact.
- The existing Players finder now has direct min/max CA and PA controls for both offline and live sources. It accepts signed PA ranges down to -10, supports ability-only searches without a name query, validates min/max ordering and uses matching filtered count queries. Advanced Search and the saved-profile workflow remain separate and preserved.

## Safety state

**Current source state:** `LiveMemoryImportEnabled` is ON for exact supported FM27 profiles. The monitor detects process/build/save-generation changes and starts the profile-bounded read-only player/staff/club scan when a career is loaded. Each scan has a two-minute hard deadline, progress reporting, toolbar cancellation and cancellation when the monitor detects a session change. The save-load counter is checked before and after extraction; a failed or cancelled scan must retain the previous committed snapshot. Live editing remains OFF. Real FM performance, peak-memory and field-witness acceptance are still required before calling the live connection release-ready.

The current implementation checklist, architecture status and quick-start guidance have been aligned with this bounded-scan state. The exact imported-session guard prevents manager/club shortcuts from treating a cached snapshot from another process as current, even if the save-load counter is reused.

## Commits made in this continuation

- `23a19615` — validate real FM installation via adjacent `fm_Data`, distinguish access denial and select newest supported process.
- `414264fb` — add regression coverage for FM installation-path discovery.
- `76ea9085` — align staff attribute fact keys with FMLibreEditor layout terminology.
- `4f349b74` — test canonical staff field keys and reject invented aliases.
- `39573f9b` — document the second FMSuperScout/FMLibreEditor/fmsave reference pass.
- `ded18c7b` — update the durable V8 implementation checklist for process discovery and staff field naming.
- `3952982e` — add validated 15-group staff preferred-job decoder.
- `98232925` — read preferred-job ratings through the class-scoped FM27 staff identity shift.
- `ae09f063` — persist provisional preferred-job ratings and top groups in live staff facts.
- `d0a93333` — show preferred-job match and role-group ratings in Staff profiles.
- `f0001c03`, `1acaa16b` — add decoder unit tests and SQLite/profile integration coverage.
- `bd5dee45` — document independent FMLibreEditor, FMSuperScout and fmsave research.
- `04d27606`, `781a79ee`, `c8ba6827` — update implementation status, live-career checklist and master checklist.
- `8afe77a4` — intermediate safety correction; superseded by the bounded, cancellable read-only scanner described in the current source state.
- `62a89c5b` — made SQLite live-snapshot publication cancellable and rollback-safe when the FM session changes.
- `242bab86` — rechecked the exact FM process/build/save-generation immediately before and after the database publication.
- `4cf34c5a` — updated implementation status to match the enabled automatic scan.
- `fa226cee` — reconciled the implementation checklist with the actual live-memory safety gate.
- `05f316e5` — live staff search now includes linked club name.
- `2107cc99` — integration test now checks staff club search, job and age.
- `4980a746` — live career detection/import safety gate and help text updated.
- `470dcaf2` — live staff UID search regression added.
- `b503ae78`, `03778f45` — clarified detection status and career monitor behaviour.
- `edd568cc` — manager/club shortcuts now require the exact imported process/profile/save-generation session, not just a matching counter.
- `fdd5067b` — attach the documented FM27 staff coaching-attribute decoder to live staff records.
- `24119295` — add the standalone staff attribute decoder with 19 coaching fields and two direct ratings.
- `e156c329` — persist staff coaching attributes as provisional profile facts.
- `6bf0a216`, `f498d8f9` — add decoder regression tests and correct the invalid-value omission count.
- `b39c3ae9` — decode staff personality with the ordinary/human-manager identity shift.
- `54600473` — add staff personality and contract fields to the live staff reader.
- `011f54e0` — persist staff personality and contract facts in the staff profile store.
- `dcfe6f52`, `38b1b02d` — add personality-shift and expanded staff-fact regression coverage.
- `37e039d3`, `7610d216` — add CA/PA-aware live/offline source counts and align local count text matching with player search.
- `f840de7d` — add CA/PA range controls to the existing Players workspace.
- `741ff78f` — add live/offline CA and signed-PA filter regression coverage.
- The live career probe exposes `MatchesSession`, with a regression covering a new process reusing the same save counter.

## Second reference/reverse-engineering batch

- Inspected the current FMSuperScout Windows scanner and memory reader. Its process discovery checks the executable path and adjacent `fm_Data` directory; its external handle requests read/query rights only; access-denied is a distinct diagnostic; multiple FM processes are resolved deterministically by newest start time. Atlas now applies the install-marker validation, clearer permission status and deterministic newest-supported-process selection in `LiveCareerMonitor`.
- Added `LiveCareerMonitor.IsFootballManagerInstallPath` regression coverage for valid FM install folders, missing `fm_Data`, non-`fm.exe` paths and null input.
- Rechecked FMLibreEditor `profiles/fm27/layout.toml` and `crates/fmlibre-core/src/staff.rs`. The staff layout defines 19 raw coaching fields and two direct ratings, with identity-block shifts applied only at or after `person_block=0x100`; CA/PA, reputation and coaching fields stay unshifted. FM Atlas already follows that offset boundary and caches shift selection by class.
- Corrected the three staff fact keys that diverged from the reference layout: `judging_player_ability` → `judging_ability`, `judging_player_potential` → `judging_potential`, and `judging_staff_ability` → `judging_staff`. Added a regression test that checks canonical keys and rejects the invented aliases.
- Rechecked fmsave's documented FM27 trust model and the Atlas adapter: missing nation display names remain null because fmsave exposes save-local nation IDs; field meanings remain provisional where unconfirmed; managed-club and staff tables are already part of the adapter's streaming table list. No guessed nationality labels were added.
- The live importer remains read-only, background, cancellation-aware, bounded to two minutes and keyed to the FM process/build/save-load generation. FMSuperScout's separate child-process scanner and memory-snapshot retry have not been ported: they require performance measurements and a separate worker protocol rather than an untested architectural change.

## Current user workflow

1. Start a supported FM27 Steam/Game Pass build and load a career.
2. Leave Atlas open; the three-second monitor detects the exact process/build/save-load generation and starts read-only indexing automatically.
3. Watch the player/staff/club progress text. The scan is limited to two minutes and can be cancelled from the toolbar.
4. Use My Manager / My Club and the Players / Staff directories after the scan commits.
5. Use the optional offline save reader only for extra history, match/season statistics, contracts and data not exposed by the live identity scan.
6. Keep live Apply/Undo disabled; all live field values remain provisional pending in-game UI witnesses.

## Verification still required

1. Fresh Windows Release build of the exact current branch head.
2. Full .NET integration suite, including live-career, staff decoder, expanded staff-fact and CA/PA range-filter regressions.
3. Confirm monitor detects load/unload/new-generation changes during the bounded scan and cancels the worker when the session changes.
4. Confirm the Staff directory searches by name, club and UID; select a live staff fixture and verify CA/PA, nation, job, club and all stored live facts.
5. Verify preferred-job ratings and tie/threshold handling against the in-game FM27 Staff Search UI; do not promote these values beyond provisional before that comparison.
6. Verify the 19 staff coaching attributes plus authority/working-with-youngsters against the in-game staff profile; keep them provisional until witnessed.
7. Verify staff personality, weekly wage and contract dates against the in-game screens; keep them provisional until witnessed.
8. Confirm the manager shortcut refuses stale data from a previous FM process even if the save-load counter is identical.
9. Windows PC test of Advanced Search preservation, CA/PA quick filters (including negative PA), Players pagination, profile tabs and navigation restoration.
10. FM27 real-save compatibility, two-minute scan duration, CPU/RAM/peak-memory testing and package acceptance.

Do not mark a verification item complete based only on source inspection or a historical Actions run.
