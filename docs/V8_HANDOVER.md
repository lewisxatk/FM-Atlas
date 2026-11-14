# FM Atlas V8 — current handover

**Date:** 2026-11-14  
**Repository:** `lewisxatk/FM-Atlas`  
**Branch:** `rebuild/v8-full-product-rebuild`  
**Application:** `application/R7.2.0/FM-Atlas-R7.2.0-Work/`

## Current objective

Continue the integrated V8 rebuild without replacing existing R7.2.0 functionality. Preserve the Atlas Liquid/Monolith shell, universal Advanced Search, saved search profiles, Players and Staff workspaces, profile tabs, shortlist pipeline, analytics, squad analysis, local editor history, save-import pipeline and all existing source-backed features.

## Work inspected and carried forward

- Live career detection uses the exact FM process/build profile and save-load generation counter.
- The read-only live extractor contains staff and human-manager recognition, including staff CA/PA, age/DOB, nationality, club relationship and role/job mapping.
- The database already contains transactional live staff persistence and the `LiveCareerIdentity` record.
- The Staff directory already merges offline-save and live staff rows, exposes CA/PA and other identity columns, and loads additional staff facts into the selected profile.
- `GetFmsaveStaffFactsAsync` already strips the `LIVE:` prefix before reading `LiveStaffFacts`; this is covered by the existing `StaffSearchProfileAndCareerIdentityUseTheConnectedLiveManager` integration test.
- The live staff query now supports name, club and UID matching; the integration test covers the linked club and UID search paths.
- Players search is paged at 250 rows, merges local and live source rows before paging, prefers the most complete source row, and restores open profile tabs after navigation. Existing Advanced Search must remain intact.

## Safety state

**Verified source correction in this continuation:** `LiveMemoryImportEnabled` was found set to `true` even though this handover and the V8 live-career checklist explicitly said the gate was OFF. That contradiction would have allowed the 3-second monitor to start full entity-array scans automatically. It has now been corrected to `false` in `MainWindow.xaml.cs` (commit `8afe77a4`). The monitor remains detection-only: process/build/session generation checks are allowed; large player/staff/club array scans are not started automatically or by the toolbar action. Live editing remains OFF. Offline read-only save import is the supported way to populate career data until controlled FM performance and field-witness checks pass.

The implementation checklist has also been reconciled with this safety state (commit `fa226cee`). The extractor and transactional persistence code remain in the branch for controlled fixtures and future acceptance; their presence is not evidence that automatic career indexing is currently enabled.

## Commits made in this continuation

- `8afe77a4` — corrected the live-memory gate to OFF and clarified detection-only UI wording.
- `fa226cee` — reconciled the implementation checklist with the actual live-memory safety gate.
- `05f316e5` — live staff search now includes linked club name.
- `2107cc99` — integration test now checks staff club search, job and age.
- `4980a746` — live career detection/import safety gate and help text updated.
- `470dcaf2` — live staff UID search regression added.
- `b503ae78`, `03778f45` — clarified detection status and career monitor behaviour.
- `edd568cc` — manager/club shortcuts now require the exact imported process/profile/save-generation session, not just a matching counter.
- The live career probe exposes `MatchesSession`, with a regression covering a new process reusing the same save counter.

## Verification still required

1. Fresh Windows Release build of the exact post-correction branch head (the correction triggers the V8 Windows workflow).
2. Full .NET integration suite, including the live staff fixture tests and Advanced Search regressions.
3. Confirm monitor detects load/unload/new-generation changes during the bounded scan and cancels the worker when the session changes.
4. Confirm the Staff directory searches by name, club and UID; select a live staff fixture and verify CA/PA, nation, job, club and all stored live facts.
5. Confirm the manager shortcut refuses stale data from a previous FM process even if the save-load counter is identical.
6. Windows PC test of Advanced Search preservation, Players pagination, profile tabs and navigation restoration.
7. FM27 real-save compatibility, two-minute scan duration, CPU/RAM/peak-memory testing and package acceptance.

Do not mark a verification item complete based only on source inspection or a historical Actions run.
