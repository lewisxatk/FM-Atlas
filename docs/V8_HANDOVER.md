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
- Players search is paged at 250 rows, merges local and live source rows before paging, prefers the most complete source row, and restores open profile tabs after navigation. Existing Advanced Search must remain intact.

## Safety state

**Current source state:** `LiveMemoryImportEnabled` is ON for exact supported FM27 profiles. The monitor detects process/build/save-generation changes and starts the profile-bounded read-only player/staff/club scan when a career is loaded. Each scan has a two-minute hard deadline, progress reporting, toolbar cancellation and cancellation when the monitor detects a session change. The save-load counter is checked before and after extraction; a failed or cancelled scan must retain the previous committed snapshot. Live editing remains OFF. Real FM performance, peak-memory and field-witness acceptance are still required before calling the live connection release-ready.

The current implementation checklist, architecture status and quick-start guidance have been aligned with this bounded-scan state. The exact imported-session guard prevents manager/club shortcuts from treating a cached snapshot from another process as current, even if the save-load counter is reused.

## Commits made in this continuation

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
- The live career probe exposes `MatchesSession`, with a regression covering a new process reusing the same save counter.

## Current user workflow

1. Start a supported FM27 Steam/Game Pass build and load a career.
2. Leave Atlas open; the three-second monitor detects the exact process/build/save-load generation and starts read-only indexing automatically.
3. Watch the player/staff/club progress text. The scan is limited to two minutes and can be cancelled from the toolbar.
4. Use My Manager / My Club and the Players / Staff directories after the scan commits.
5. Use the optional offline save reader only for extra history, match/season statistics, contracts and data not exposed by the live identity scan.
6. Keep live Apply/Undo disabled; all live field values remain provisional pending in-game UI witnesses.

## Verification still required

1. Fresh Windows Release build of the exact post-correction branch head (the correction triggers the V8 Windows workflow).
2. Full .NET integration suite, including the live staff fixture tests and Advanced Search regressions.
3. Confirm monitor detects load/unload/new-generation changes during the bounded scan and cancels the worker when the session changes.
4. Confirm the Staff directory searches by name, club and UID; select a live staff fixture and verify CA/PA, nation, job, club and all stored live facts.
5. Confirm the manager shortcut refuses stale data from a previous FM process even if the save-load counter is identical.
6. Windows PC test of Advanced Search preservation, Players pagination, profile tabs and navigation restoration.
7. FM27 real-save compatibility, two-minute scan duration, CPU/RAM/peak-memory testing and package acceptance.

Do not mark a verification item complete based only on source inspection or a historical Actions run.
