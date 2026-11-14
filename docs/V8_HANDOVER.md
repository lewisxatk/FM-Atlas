# FM Atlas V8 — current handover

**Date:** 2026-11-14  
**Repository:** `lewisxatk/FM-Atlas`  
**Branch:** `rebuild/v8-full-product-rebuild`  
**Application:** `application/R7.2.0/FM-Atlas-R7.2.0-Work/`

## Full integrated continuation batch — 2026-11-14

- Commit `e53f26e4`: added the toolbar Activity Centre for the existing live-career scan and offline save-import jobs. It shows active task name, live status, elapsed time, current cancellation availability and the previous task summary. It reuses the current cancellation tokens; it does not start duplicate import jobs.
- Commit `e53f26e4`: standardised shared DataGrid row/header/cell styling for all workspaces, including hover and selected states.
- Commit `e53f26e4`: replaced the Dashboard hero's legacy CoachPro label and flat surface with the Atlas Monolith Glass hero card, and added adaptive 4/2/1 metric-card columns.
- Commit `d2d73758`: removed the clickable live Apply actions from Editor Studio. The editor in this V8 build is local Atlas database-only.
- Commit `30a0110b`: fixed the shared Card helper to preserve its direct Child contract. Existing pages populate `Card(...).Child`; wrapping the child in an internal Grid would break those established callers. Also placed edit-history live Undo behind the same hard V8 live-write gate. Local undo remains available.
- Commit `98df46a4`: applied theme-aware surfaces to TextBox, ComboBox, TabControl and ProgressBar controls after workspace creation, with a catch-and-log fallback if styling cannot be applied.
- Commit `15efd8ed`: lowered the minimum WPF window bounds to 1000×680 so the dashboard can use its adaptive layout on smaller displays.
- Commit `57e74111`: switched the Activity Centre toolbar icon to a Segoe MDL2 glyph and made theme application failures non-fatal to workspace navigation.
- Commit `6f00241d`: updated the durable checklist with implementation and remaining verification states.

**Verification boundary:** these are source commits, not a claim of successful Windows compilation, visual acceptance or real-game compatibility. The next gate is a fresh Windows Release build, all xUnit/Python adapter tests and self-contained x64 publish against the final branch head. Then manually inspect all four appearance modes, DataGrid/ComboBox/TabControl states and Dashboard layout at 100%, 125% and 150% DPI. Real FM27 process/session and player/staff field witnesses remain separate acceptance requirements. Live memory Apply/Undo remains blocked.

## Latest integrated continuation — 2026-11-14

- Commit `91ea60ad`: closing the main WPF window now stops the live-career monitor and requests cooperative cancellation of any active read-only live scan or offline save import. The existing importer/database cancellation path is responsible for rolling back unpublished work and retaining the last committed snapshot.
- Commit `82916f68`: added separate implementation and Windows verification checklist entries. The code change is implemented, but close-during-scan/import behaviour still requires a fresh Windows workflow and runtime acceptance; no test pass is claimed.
- Preservation remains mandatory: Advanced Search, saved profiles, player/staff directories, profiles, shortlist, analytics, save import and the four appearance modes are not replaced by this continuation.

## Current objective

Continue the integrated V8 rebuild without replacing existing R7.2.0 functionality. Preserve the Monolith Glass shell, universal Advanced Search, saved search profiles, Players and Staff workspaces, profile tabs, shortlist pipeline, analytics, squad analysis, local editor history, save-import pipeline and all existing source-backed features.

## Work inspected and carried forward

- Live career detection uses the exact FM process/build profile and save-load generation counter.
- The read-only live extractor contains staff and human-manager recognition, including staff CA/PA, age/DOB, nationality, club relationship and role/job mapping.
- The database already contains transactional live staff persistence and the `LiveCareerIdentity` record.
- The Staff directory already merges offline-save and live staff rows, exposes CA/PA and other identity columns, and loads additional staff facts into the selected profile.
- The selected Staff profile now groups live coaching attributes, personality and contract facts into dedicated sections, while retaining preferred-job ratings and generic imported facts. Wage is explicitly displayed as GBP/week; all live facts remain provisional.
- `GetFmsaveStaffFactsAsync` already strips the `LIVE:` prefix before reading `LiveStaffFacts`; this is covered by the existing `StaffSearchProfileAndCareerIdentityUseTheConnectedLiveManager` integration test.
- The live staff query now supports name, club and UID matching; the integration test covers the linked club and UID search paths.
- FM27 preferred-job ratings are now modelled independently using the public 15-group layout: a class-scoped pointer at person+0xD0, 15 byte ratings, 1–20 validation, tie-aware top-group selection and provisional fact persistence. The Staff profile displays the ratings without changing existing directory/search behaviour.
- FM27 staff coaching attributes now use the public FMLibreEditor layout: 19 raw 0–100 fields are converted with the documented `(raw + 2) / 5` display scale, while authority and working-with-youngsters are retained as direct 1–20 ratings. These are persisted as provisional `staff_attributes.*` facts and appear in the existing Staff profile.
- The staff attribute decoder has regression tests for documented offsets, display conversion, direct ratings, out-of-range omission and missing bytes. These tests are committed but have not yet been run by a fresh Windows workflow.
- The same reference mapping now supplies eight staff personality ratings (using the +0x340 human-manager identity shift) and contract facts where readable: weekly wage in GBP, signed date, expiry date and joined-club date. These are stored as provisional profile facts; no unsupported contract field is filled with a default.
- Staff profile facts are grouped by their source keys and rendered by the existing profile view; the new fields do not create a parallel staff directory or replace the established search flow.
- Reference findings and limitations are recorded in `docs/architecture/FM-ATLAS-V8-REFERENCE-TOOL-MAPPING-NOTES.md`; the implementation deliberately keeps group 7 unresolved and recruitment/performance analyst preferences combined.
- Players search is paged at 250 rows, merges local and live source rows before paging, prefers the most complete source row, and restores open profile tabs after navigation. Existing Advanced Search must remain intact.
- The existing Players finder now has direct min/max CA and PA controls for both offline and live sources. It accepts signed PA ranges down to -10, supports name-free position/CA/PA searches, validates min/max ordering and uses matching filtered count queries for text, position and ability bounds. Advanced Search and the saved-profile workflow remain separate and preserved.

## Safety state

**Current source state:** `LiveMemoryImportEnabled` is ON for exact supported FM27 profiles. The monitor detects process/build/save-generation changes and starts the profile-bounded read-only player/staff/club scan when a career is loaded. Each scan has a two-minute hard deadline, progress reporting, toolbar cancellation and cancellation when the monitor detects a session change. The save-load counter is checked before and after extraction; a failed or cancelled scan must retain the previous committed snapshot. Live editing remains OFF. Real FM performance, peak-memory and field-witness acceptance are still required before calling the live connection release-ready.

The current implementation checklist, architecture status and quick-start guidance have been aligned with this bounded-scan state. The exact imported-session guard prevents manager/club shortcuts from treating a cached snapshot from another process as current, even if the save-load counter is reused.

## Commits made in this continuation

- `ae669c2d` — upgrade save-bridge setup to fmsave 0.12.4.
- `01af910e` — update the adapter's fmsave compatibility documentation.
- `3f05e462` — require fmsave 0.12.4 or newer before enabling save import.
- `8982d262` — install and verify fmsave 0.12.4 in Windows CI.
- `e448faff` — explain missing/outdated save-reader versions in the UI.
- `f5e74be5` — document the upstream fmsave compatibility upgrade.
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
- `9c7d1312`, `afedac0e` — make source counts honour the active position and CA/PA filters.
- `4b25d4b3` — pass the active position into the filtered source counts.
- `58f9adac` — extend CA/PA search regression coverage to position filters.
- `d2949e9a`, `17f0b800` — allow position-only searches and show the active position/CA/PA filter summary.
- `8e864b67`, `46b95d21` — organise staff coaching, personality and contract facts into dedicated profile sections.
- `019fd8dc` — update the in-app quick-start guidance for CA/PA and position-only people search.
- `b7f1bdfe` — reconcile the master implementation checklist with the expanded staff profile coverage.
- The live career probe exposes `MatchesSession`, with a regression covering a new process reusing the same save counter.

## Second reference/reverse-engineering batch

- Inspected the current FMSuperScout Windows scanner and memory reader. Its process discovery checks the executable path and adjacent `fm_Data` directory; its external handle requests read/query rights only; access-denied is a distinct diagnostic; multiple FM processes are resolved deterministically by newest start time. Atlas now applies the install-marker validation, clearer permission status and deterministic newest-supported-process selection in `LiveCareerMonitor`.
- Added `LiveCareerMonitor.IsFootballManagerInstallPath` regression coverage for valid FM install folders, missing `fm_Data`, non-`fm.exe` paths and null input.
- Rechecked FMLibreEditor `profiles/fm27/layout.toml` and `crates/fmlibre-core/src/staff.rs`. The staff layout defines 19 raw coaching fields and two direct ratings, with identity-block shifts applied only at or after `person_block=0x100`; CA/PA, reputation and coaching fields stay unshifted. FM Atlas already follows that offset boundary and caches shift selection by class.
- Corrected the three staff fact keys that diverged from the reference layout: `judging_player_ability` → `judging_ability`, `judging_player_potential` → `judging_potential`, and `judging_staff_ability` → `judging_staff`. Added a regression test that checks canonical keys and rejects the invented aliases.
- Rechecked fmsave's documented FM27 trust model and the Atlas adapter: missing nation display names remain null because fmsave exposes save-local nation IDs; field meanings remain provisional where unconfirmed; managed-club and staff tables are already part of the adapter's streaming table list. No guessed nationality labels were added.
- The live importer remains read-only, background, cancellation-aware, bounded to two minutes and keyed to the FM process/build/save-load generation. FMSuperScout's separate child-process scanner and memory-snapshot retry have not been ported: they require performance measurements and a separate worker protocol rather than an untested architectural change.

## Third reference pass — fmsave compatibility

- The old setup script installed `fmsave==0.9.3`, dated before the FM27 27.0.1 full-release build. Upstream now publishes `0.12.4` (13 November 2026), with additional FM27 reader fixes including the 27.0.1 training/mentoring block trailer handling.
- Updated `Setup-FMAtlas-Save-Bridge.ps1` to install 0.12.4; `FmsaveBridgeService.IsAvailableAsync` now requires 0.12.4 or newer; the Windows workflow installs and checks 0.12.4 before adapter tests.
- The adapter remains streaming and preserves fmsave's coverage/validation warnings. It does not infer nationality names from save-local nation IDs, and it does not treat a structurally readable unregistered build as verified.
- Exact-save acceptance still requires running the import against a COPY of the user's current save on Windows. The Python adapter unit tests use synthetic records; they are not a substitute for a real career fixture.

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


## Monolith Glass surface refinement — 2026-11-14

Continued the V8 visual foundation without changing the data/search/import paths:

- Commit `e8b59302`: replaced the shared `Card` helper's flat translucent fill with a layered glass surface: vertical material gradient, subtle inner edge, fine top reflection and restrained elevation. All content remains crisp; no blur is applied to tables or text.
- Commit `bba2b816`: refined the sidebar rail and top action toolbar with coordinated translucent gradients, light-catching edges and restrained shadows; toolbar icon buttons now use a warm layered material.
- Commit `96eff9e0` (checklist update): records the implementation and keeps the Windows build, complete test suite and DPI/theme visual review explicitly pending.

**Verification boundary:** source changes are committed to `rebuild/v8-full-product-rebuild`. They have not yet been compiled or visually reviewed on Windows. Do not mark the glass system release-verified until the current branch head passes the Windows workflow and 100%/125%/150% DPI checks. Dialogs, tabs, filters, grids, charts, empty/loading/error states and accessibility/high-contrast coverage remain on the design-system backlog.


## Native Windows backdrop research — 2026-11-14

The V8 glass treatment remains deliberately cross-version and does not depend on undocumented Windows composition APIs.

- Microsoft documents `DWMWA_SYSTEMBACKDROP_TYPE` from Windows 11 build 22621; `DWMSBT_MAINWINDOW` maps to the long-lived-window Mica material. See https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type and https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute.
- Microsoft explicitly says `SetWindowCompositionAttribute` is not recommended and advises using `DwmSetWindowAttribute` instead: https://learn.microsoft.com/en-us/windows/win32/dwm/setwindowcompositionattribute.
- Decision: retain the layered WPF glass material as the reliable Windows 10/11 baseline. Consider an optional, guarded Windows 11 Mica backdrop adapter only after the current WPF build and visual tests pass; always preserve the existing solid/translucent fallback for Windows 10, disabled transparency and unsupported systems. Do not add an undocumented Acrylic dependency or compromise window-drag/resize responsiveness for blur.

This keeps the app's Windows 10 target intact while leaving a documented route to native Windows 11 material support.


## Advanced Search Studio and verification correction — 2026-11-14

- Commit `f48dfa71`: Advanced Search condition controls now wrap to the next line at narrow workspace widths instead of overflowing as one horizontal strip. Each condition has move-up/move-down controls; the visible order and captured query order move together. Existing field discovery, 60-condition limit, ALL/ANY logic, saved profile schema, server-side sorting, paging, cancellation, compare, similar-player, CSV and shortlist actions are retained.
- Commit `57ba205b`: corrected a sparse-fixture assertion in `StaffAttributeDecoderUsesDocumentedFm27OffsetsAndDisplayScale`. The fixture seeds five valid ratings, one invalid raw value and leaves other offsets zero; the decoder intentionally omits zero/missing and out-of-range values. Expected decoded count is five. Decoder behaviour was not weakened.
- Commit `8813b9d0`: recorded the FMRTE 27, Genie Scout 27 and FMLibreEditor research in the reverse-engineering ledger. Patterns are adopted as product guidance, not copied proprietary formulas.
- Commit `c7cce367`: corrected local-variable initialization for the new condition reorder handlers before CI.

The previous Windows run `43554026041` built Release successfully and passed the Python adapter tests, but the .NET suite reported 59/60 because the sparse decoder test expected 20 results instead of the five seeded valid facts. The x64 publish and artifact upload were skipped. The corrected branch head requires a fresh complete Windows run; no new build/test/publish pass is claimed yet.

### Glass status

The Monolith Glass source system now includes the shared glass-card material, themed toolbar/sidebar, shared DataGrid row/header/hover/selection styles, theme-aware TextBox/ComboBox/TabControl/progress styling, and the adaptive dashboard hero. This is implementation status, not visual acceptance. Dialog-specific polish, high-contrast accessibility, dropdown popup/scrollbar consistency, chart and empty/loading/error states, and 100%/125%/150% Windows DPI review remain open. Windows 10 fallback remains the baseline; native Windows 11 Mica is deferred until the cross-version build and visual pass are clean.


## Position scouting pitch and latest Windows verification — 14 November 2026

- Windows workflow run `43554520053` (commit `6a65a8a2`) completed successfully: Python adapter checks, .NET 8 restore, Release build, complete xUnit suite, self-contained Windows x64 publish and artifact upload all passed.
- Artifact: `FM-Atlas-V8-win-x64`, 68,885,686 bytes, SHA-256 `3adbd4232d0e1e8910df43ab83acb81f1fbde9880cdb3e60fb121f8efbf746c4`, expires 28 November 2026. This artifact corresponds to commit `6a65a8a2`; later source changes require a fresh build before distribution.
- Commit `e1a9fa3d`: replaced the flat 5×3 position-rating tiles in player profiles with a pitch-shaped, responsive 15-position map. The visual arrangement is presentation-only; all ratings and five display bands still come from `PositionSuitabilityService`. Missing/zero/out-of-range values remain unrated. Position tiles can be selected to inspect their source rating and band. Provisional live values remain explicitly labelled.
- Commit `fb2e8a42`: added position-suitability regression tests for canonical position order, all five band boundaries, missing/invalid values, legacy ST alias handling and later-source precedence.
- Fresh Windows verification is required for the new pitch and tests. Do not distribute the previous artifact as though it contains these later changes.

### Permanent reverse-engineering reference rule

Every meaningful reverse-engineering or competitor-research batch must update `docs/V8_REVERSE_ENGINEERING_NOTES_2026-11-14.md` with date, source/repository links, files/pages reviewed, observed behaviour, confidence/limitations, Atlas implementation decision and future follow-up. Also update the implementation checklist and this handover when implementation status changes.


## Verified Windows build — 14 November 2026

The latest branch-head workflow is now green; this supersedes the earlier pending-build statements above.

- Branch: rebuild/v8-full-product-rebuild
- Commit: 21430f4fa4ca5b831022e67c70438a5e5cc9dff0
- Workflow run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43555015459
- Result: all 12 workflow steps passed, including Python adapter validation, .NET 8 restore, Release build, career-import integration tests, self-contained Windows x64 publish and artifact upload.
- Artifact: FM-Atlas-V8-win-x64, 68,887,451 bytes; SHA-256 5813abb152cc4f82147c20fb0325fd309c1d34fddc4082161a791be2c6496541; expires 28 November 2026.
- This artifact includes the pitch-map changes and the Canvas type-shadowing fix.

Still not release acceptance: real Windows visual/DPI review, real FM27 career field validation, live scan performance/peak memory and full user-PC acceptance remain open. Live editing remains disabled.

## Next implementation batch

The next source task is an independent Advanced Search position group (ALL / ANY and minimum familiarity where supported by the stored schema). Keep this separate from the existing global ALL / ANY mode. First trace the actual fact keys and SQL/query contract; do not guess the storage shape. Saved profiles must remain backward compatible, and existing criteria, columns, paging, sorting, cancellation, compare, similar-player, export and shortlist actions must remain intact.

## Reverse-engineering safety rule — FMLibreEditor

The FMLibreEditor development notes document a fail-closed write pattern: verify the same save/session, confirm the object still belongs to the intended person, validate the new value, confirm the current value matches the expected field layout, then read back the write. Any failed precondition means no write. A save change invalidates pending edits and requires reload; unsupported layouts fall back to read-only.

FM Atlas adopts this as a future safety checklist only. It does not enable live writes. Before any future live Apply/Undo work, require all five checks, exact build/profile support, save-generation invalidation, tested rollback and an independent in-game witness. Current live editing remains OFF.


## Monolith Glass finishing and Advanced Search position group — 14 November 2026

### Implemented in source

- Shared Monolith Glass cards now use a deeper graphite material gradient, warm amber-orange edge lighting, a restrained inner highlight and controlled shadow depth. The direct-child Card contract is unchanged; other theme branches are retained.
- Advanced Search has a dedicated searchable field-library input. It filters the current category's discovered fields without inventing unavailable fields.
- CA and PA remain first-class numeric criteria: CA integer 0–200; PA integer -10–200 to retain signed random-potential values. Player attributes remain whole-number 1–20.
- Added a dedicated 15-position familiarity group. Every position accepts 0–20; zero means no requirement. ALL selected is the default, “Any of these” changes only the position group to ANY, and “At Least” changes exact values to minimums.
- Position group is separate from the existing general criteria AND/OR switch and is combined with the general result. It supports position-only searches.
- Search profiles gained an optional PositionFilter property. Older saved JSON profiles without it continue to load; position filters are included in new profile save/load and export.
- Existing criteria, 60-condition cap, field discovery, result columns, sorting, paging, cancellation, compare, similar-player discovery, CSV and shortlist actions are retained.
- Added position filter model/profile regression tests. SQL searches numeric values from fmsave's `positions` group and the existing live position facts. Natural/accomplished position classifications are not used as numeric ratings.

### Research basis

FMRTE's current search documentation specifies the exact position semantics: ALL nonzero position values by default; “Any of these” for OR within positions; “At Least” converts exact values into minimums; zero means no requirement. fmsave 0.12.3 documents the 15 named numeric position ratings separately from its natural/accomplished classification lists. The research and links are recorded in `docs/V8_REVERSE_ENGINEERING_NOTES_2026-11-14.md`.

### Verification boundary

This batch is source-implemented and regression tests are added, but it is not yet Windows build-verified. Wait for the fresh workflow after the latest commits. Real FM27 SQL-result validation and Windows DPI/visual acceptance remain open. Live-memory ratings remain provisional until checked against the in-game Positions page.


## Advanced Search position verification follow-up — 14 November 2026

- Saved-profile loading now clears the transient field-library text filter before restoring criteria. This avoids an edge case where a saved field hidden by the active field search could otherwise resolve to the first visible field.
- Added an SQLite integration test using real table contracts: FMSAVE snapshot position facts and live LivePlayerFacts are both seeded. The test asserts exact ALL, exact ANY and At Least ALL result sets across offline and live players.
- This follow-up is later than workflow #43590420150 and its artifact. That green run verifies the feature implementation and model/profile tests, but the newly added SQL integration test and field-search restore safeguard need their own fresh Windows workflow before they are considered verified.


## Final Windows verification — Monolith Glass and Advanced Search positions — 14 November 2026

- Workflow run: https://github.com/lewisxatk/FM-Atlas/actions/runs/43590756414
- Branch: rebuild/v8-full-product-rebuild
- Verified head: 9ec72cbf34af3b51984c7fe23ad2c54573ec36fc
- All 12 workflow steps passed: Python adapter validation, supported fmsave verification, streaming bundle tests, .NET 8 restore, Release build, complete xUnit suite, self-contained Windows x64 publish and artifact upload.
- The suite includes the SQLite position-search integration test for exact ALL, exact ANY and At Least ALL, spanning FMSAVE snapshot facts and live position facts. Saved-profile backwards compatibility and JSON round-trip tests also passed.
- Artifact: FM-Atlas-V8-win-x64, 68,894,168 bytes; SHA-256 e76e763e26b35dd73ad4ee9ab1e1b95ffb34b7c090269e406658a2ad7f627f93; expires 28 November 2026.
- Artifact is the current verified build for this update. Download from the Actions run's Artifacts section.
- Still open: manual Windows visual/DPI review, real FM27 in-game field comparison, actual career scan performance and full user-PC acceptance. Live-memory field confidence remains provisional until witnessed in game. Live editing remains disabled.
