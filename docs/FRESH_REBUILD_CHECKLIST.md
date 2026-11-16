# FM Atlas — Fresh Rebuild Master Checklist

This checklist is the live task tracker. Update it in the same commit as the checkpoint whenever possible.

Legend: [ ] Not started · [~] In progress · [x] Completed with evidence · [!] Blocked · [-] Deferred

## M0 — Protect and inventory
- [x] M0.1 Confirm active branch and keep main untouched.
- [x] M0.2 Locate the R7.2.0 recovery ZIP in Library metadata and verify the corresponding imported GitHub recovery branch contains 106 application files. Raw ZIP materialization was refused by the file service; GitHub source tree is the recovered working reference.
- [x] M0.3 Preserve R7.1.0 and R7.0.2 historical records and comparison documentation in the recovery branch.
- [x] M0.4 Record uploaded-package hashes and inspect ZIP manifests.
- [x] M0.5 Inspect user-supplied research packages and relevant public repositories.
- [x] M0.6 Record licence and reuse decision per source.
- [x] M0.7 Complete first-pass audit of reusable research, data schemas, graphics index and tests.
- [x] M0.8 Decide legacy UI/architecture to discard; clean .NET 8 WPF architecture approved.
- [x] M0.9 Create and commit a separate fresh-rebuild checkpoint branch.

## M1 — Research and blueprint
- [~] M1.1 FMLibreEditor Game Pass profile and live-edit safety study (initial study done; continue field-by-field).
- [~] M1.2 FMSuperScout scanner/extraction/index study (initial study done; inspect scanner implementation/tests further).
- [~] M1.3 fm-editor build layout/entity/undo study (initial study done; field evidence review ongoing).
- [~] M1.4 fmsave offline save parser coverage study (initial study done; schema/coverage validation still needed).
- [x] M1.5 FM Player Analyzer parsing/graphics/profile architecture inspected.
- [~] M1.6 FMRTE feature and workflow inventory (official feature audit 001 committed; full UI/feature audit still required).
- [x] M1.7 FM27 canonical player attribute catalogue implemented in Domain (52 unique standard/hidden non-foot fields, 8 separate personality fields and 2 foot ratings; source order documented; final visual order still provisional).
- [ ] M1.8 FM27 attribute display order verified against FM27 player profile and FMRTE.
- [ ] M1.9 Staff and club field catalogue complete.
- [ ] M1.10 User's Game Pass build/process fingerprint verified on Windows.
- [ ] M1.11 Read/write coverage matrix established field-by-field.
- [x] M1.12 Fresh architecture decision documented.

## M2 — Fresh application foundation
- [x] M2.1 New clean solution and project boundaries created (Domain, Application, Infrastructure, Desktop, Tests).
- [x] M2.2 Windows GitHub Actions restore/build/test passed on Windows Server 2025 with .NET SDK 8.0.428; build 0 warnings/0 errors; 3 unit tests passed. See run 43787424965.
- [x] M2.2a Windows x64 shell artifact uploaded and verified (run 43787554512; artifact 14191091352; SHA-256 recorded in CURRENT_CHECKPOINT.md).
- [~] M2.3 Initial application shell created against REF-001 composition; visual comparison not yet performed.
- [~] M2.4 Watched visual tokens (black/off-white/orange) added; font/spacing polish and screenshot comparison remain.
- [x] M2.5 Compact left navigation has no Search item.
- [~] M2.6 Top Quick Search is wired to the read-only SQLite entity index; it searches imported Atlas records only, not live FM memory.
- [~] M2.7 Advanced Search opens a separate non-modal FMRTE-inspired workspace and executes supported filters against the read-only SQLite provider.
- [~] M2.8 Detect Game performs a process/module presence probe only; Load Live Save remains disabled because no career importer is connected.
- [~] M2.9 Save Atlas initialises/verifies the local SQLite database; Save Live Save remains disabled pending field/build/write verification.
- [~] M2.10 Double-clicking a player search result opens its typed local snapshot in a separate read-only profile window; independent player tabs and editable sections remain unfinished.
- [~] M2.11 Local SQLite schema v2, migrations, typed snapshots, normalised field_values and relationship persistence are implemented in source; final Windows verification and settings/logs integration remain.
- [ ] M2.12 Windows desktop resize/accessibility tests.

## M3 — Game connection and data
- [ ] M3.1 FM process detection.
- [ ] M3.2 Storefront/build identification.
- [ ] M3.3 Career loaded detection.
- [ ] M3.4 Read-only live data acquisition.
- [ ] M3.5 Offline save import.
- [~] M3.6 SQLite schema v2, migration runner, typed snapshots and normalised field_values projection implemented; real save import and source-provider integration remain.
- [~] M3.7 Typed PlayerRecord/StaffRecord/ClubRecord models, provider-scoped identities and field projector implemented; real provider mappings/import still pending.
- [~] M3.8 Per-field source/evidence/build provenance model is preserved in snapshots and indexed field_values; populated from real providers still pending.
- [ ] M3.9 Career switch/reload invalidation.
- [ ] M3.10 Data accuracy comparison against FM.

## M4 — Player workspace
- [~] M4.1 REF-004 profile opens from player search results and binds a read-only typed local snapshot; full section navigation, graphics binding and FM27 live data remain.
- [~] M4.2 Identity rail binds imported player details; portrait placeholder remains until graphics locator is connected to WPF image loading/cache.
- [~] M4.3 Read-only config.xml graphics locator and synthetic tests implemented; actual user-folder validation, image decoding/cache and profile binding remain.
- [~] M4.4 Central attribute groups bind imported visible/hidden/personality/foot values with per-value evidence; profile remains read-only and live importer is outstanding.
- [~] M4.5 Names/grouping/source order documented and coded; personality is separate from hidden attributes; final FM27/FMRTE visual order verification remains.
- [~] M4.6 CA and PA display from typed PlayerRecord snapshots with evidence shown on hover; live FM27 reading is not verified.
- [~] M4.7 Right-side panels display snapshot-backed ability, newgen status/evidence, season statistics, positions and field-evidence summary.
- [~] M4.8 Compact section tabs are present and can update the selected-section state; individual section-specific views/content are not yet fully switched or implemented.
- [ ] M4.8a Information and complete attribute view.
- [ ] M4.8b Contract.
- [ ] M4.8c Stats (read-only, real save data).
- [ ] M4.8d Unavailability.
- [ ] M4.8e Transfer Proposals.
- [ ] M4.8f Retire Date.
- [ ] M4.8g Positions.
- [ ] M4.8h Preferred Moves.
- [ ] M4.8i Relationships and Languages.
- [ ] M4.8j Scout Information (verified only).
- [ ] M4.8k Conditional Staff and Clone Center.
- [ ] M4.8l History and Personality/Hidden where supported.
- [~] M4.9 Multiple player profiles can be open as separate windows; duplicate windows for the same provider-scoped player identity are focused rather than recreated. In-window multi-player tab strip is not implemented.
- [ ] M4.10 Unsaved change protection.

## M5 — Search
- [~] M5.1 Quick Search is wired to SQLite and displays imported local entity results; double-click player result opens its read-only typed snapshot profile.
- [~] M5.2 Advanced Search is connected to the read-only SQLite query provider and runs validated Player/Coach/Club searches for fields marked READY or discovered in Indexed Data.
- [~] M5.3 Player / Coach / Club selectors change the field catalogue and query entity; controls remain disabled individually where a field mapping is still In Progress.
- [~] M5.4 General category is populated from the typed field catalogue; supported fields execute against local imported values.
- [~] M5.5 Contract category includes supported numeric/date/text fields; unsupported fields remain labelled In Progress.
- [~] M5.6 Technical/GK category uses the player attribute catalogue plus staff-specific coaching/GK fields; only tested mappings are enabled.
- [~] M5.7 Mental and personality categories include the separate eight-field personality catalogue; supported indexed values can be queried.
- [~] M5.8 Physical/Hidden category includes physical and hidden fields with declared 0–20 bounds; supported indexed values can be queried.
- [~] M5.9 Preferred Moves supports Has/Lacks trait query semantics where the indexed relationship data exists.
- [~] M5.10 Relations executes read-only bidirectional relationship queries by relation kind or related entity name; FM importer population remains outstanding.
- [~] M5.11 Individual position-rating filters and separate All chosen / Any chosen logic are implemented; pitch visualization and five-colour suitability presentation remain outstanding.
- [~] M5.12 Typed controls support independent numeric/date MIN/MAX, Boolean Yes/No/Unknown, text/enum lookup, traits and named position ratings. Blank MIN/MAX means no bound.
- [x] M5.13 Global All/Any logic and independent position All/Any grouping are implemented with regression coverage in source.
- [~] M5.14 Versioned JSON presets and Clear Filters are implemented; result sorting, custom result columns and exclusions/groups remain.
- [~] M5.15 Regression tests cover the query provider, ranges, Boolean newgen states, position logic, relationships, discovered indexed stats and every READY mapping; tests are committed but not executed on Windows yet.

## M6 — Live editing
- [ ] M6.1 Separate guarded write adapter.
- [ ] M6.2 Build profile match required.
- [ ] M6.3 Entity and save-generation checks.
- [ ] M6.4 Address/type/range/expected-value preflight.
- [ ] M6.5 Staged edit workflow.
- [ ] M6.6 CA/PA write and read-back on disposable save.
- [ ] M6.7 Visible attribute writes and read-back.
- [ ] M6.8 Undo/rollback verification.
- [ ] M6.9 In-game persistence after continue/reload.
- [ ] M6.10 Hidden/personality/positions writes assessed individually.
- [ ] M6.11 Staff and club writes assessed individually.
- [ ] M6.12 Other categories assessed individually.

## M7 — Staff and clubs
- [ ] M7.1 Staff search/profile.
- [ ] M7.2 Staff attribute and contract information.
- [ ] M7.2a Career Plans, Promises and Swap Staff.
- [ ] M7.2b Training Rating, Formations/Tendencies and Knowledge.
- [ ] M7.2c Relationships, Retire Date and Languages.
- [ ] M7.3 Staff verified editing.
- [ ] M7.4 Club search/profile.
- [ ] M7.5 Club facilities/finances/stadium information.
- [ ] M7.5a Squads, Youth Intake and Club Vision.
- [ ] M7.5b Possible Signings, Player Clauses and Transfer Center.
- [ ] M7.5c Staff, Wage Contributions, Kits/Colours and Affiliations.
- [ ] M7.6 Club verified editing.

## M8 — Release
- [ ] M8.1 Full regression suite.
- [ ] M8.2 Clean Windows build.
- [ ] M8.3 Game Pass acceptance tests.
- [ ] M8.4 Backup/restore tests.
- [ ] M8.5 Installer/package.
- [ ] M8.6 Release notes and known limitations.
- [ ] M8.7 User acceptance sign-off.

## Deferred after basic editor
- [-] M9 Squad Studio.
- [-] M10 Tactical Studio and Tactical Effectiveness.
- [-] M11 Recruitment and Value Finder.
- [-] M12 Wyscout/StatsBomb-inspired Moneyball/per-90/percentile analytics.

## Current status
- [x] Approved design references REF-001 to REF-010 documented.
- [x] Fresh rebuild product scope and milestones documented.
- [x] Interruption-safe continuity procedure documented.
- [x] Recovery branch/source inventory checked.
- [x] Uploaded research package inventory and first research findings committed.
- [x] Architecture decision documented.
- [x] FMRTE 27 feature audit and field coverage ledger committed.
- [x] Clean WPF solution and initial shell source committed.
- [x] FM27 player attribute catalogue and domain tests committed; Windows CI run 43787875022 passed.
- [~] REF-002 advanced-search dialog and typed filter model committed; category/entity navigation now switches schema-driven panels; actual query provider and Windows CI verification remain.
- [~] REF-004 player profile workspace shell committed; typed player record and graphics locator groundwork added, but profile data/photo binding remains.
- [x] Windows CI build and three unit tests passed.
- [x] Windows shell build artifact uploaded and inspected.
- [~] Typed provenance-aware player/staff/club record models committed; provider/import integration pending.
- [~] Read-only graphics config indexer and synthetic tests committed; Windows CI and real folder verification pending.
- [~] SQLite schema v1 migration and tests committed; repository persistence pending.
- [~] Advanced Search category/entity switching and schema-generated panels committed; fields/query still disabled pending verified provider.
- [~] User-side launch and visual comparison still pending.


## Update — checkpoint 010 (2026-11-16)

- [~] M2.11 Local SQLite schema v1 and migration runner added; Windows CI and repository integration still pending.
- [~] M3.7 Typed PlayerRecord, StaffRecord, ClubRecord and provider-scoped EntityIdentity added; no provider/importer connected.
- [~] M3.8 Per-field source/evidence provenance model added; values not yet imported.
- [~] M4.3 Read-only graphics locator implemented with synthetic tests; real FM graphics folder, CI, image decoding and WPF integration pending.
- [~] M5.2 Advanced Search dialog now switches between schema-driven categories.
- [~] M5.3 Player / Coach / Club selection now changes the schema context; filter fields remain disabled.
- [~] M5.5–M5.10 All documented category panels now render schema-driven fields; no executable query provider.
- [ ] M3.6a Player/staff/club repositories and transaction-based persistence.
- [ ] M4.3a Persistent graphics index cache and profile photo binding.
- [ ] M5.11 Position pitch interaction and positional familiarity ranges.
- [ ] M5.12 Actual min/max, boolean, lookup and date input binding.
- [ ] M5.13 Execute validated AND/OR filters against a verified data provider.

### Verification state

- New tests added for provenance, graphics index and SQLite schema migration.
- They remain unverified until Windows CI completes because the current environment has no .NET SDK.
- Latest code head: `d762f24e5e50e45240c476fe6b8c63b7d8dfe0d9`.
- Latest Windows workflow: [43829628775](https://github.com/lewisxatk/FM-Atlas/actions/runs/43829628775), queued at checkpoint time.
- Do not mark this code as compiled/tested until the runner actually executes.


## Update — checkpoint 011 (2026-11-16)

- [~] M3.6a Versioned typed JSON snapshots for PlayerRecord, StaffRecord and ClubRecord committed; normalised searchable field projection remains.
- [~] M3.6b SQLite schema v2 migration and explicit v1-to-v2 upgrade path committed; Windows runner verification pending.
- [~] M3.7a IEntityRecord contract ensures a snapshot uses identity from the record itself.
- [~] M4.3 Graphics locator source and synthetic tests committed; actual Windows graphics directory validation remains.
- [~] M5.2–M5.10 Dynamic category/entity panels are wired to the field catalogue; actual search execution is not implemented.
- [ ] M3.11 Project typed source records into indexed field_values within the import transaction.
- [ ] M3.12 Implement indexed player/staff/club repository queries.
- [ ] M4.3b Bind player image and data to the REF-004 profile.
- [ ] M5.16 Connect Quick Search and Advanced Search to real indexed repositories.

**Latest code before this documentation update:** `aae269ec2cb6dc96a645183684ec8d8d3ffb3fc9`.  
**Latest successful Windows build remains:** `fbe36400`. New code is not build-verified while Actions jobs fail before runner allocation.


## Update — checkpoint 012 (2026-11-16)

- [~] M3.11 EntityRecordProjector maps Player/Staff/Club records into normalised field_values, preserving field provenance in the same transaction as snapshots.
- [~] M3.12 SqliteEntitySearchProvider supports read-only Quick Search over provider-scoped entity records; repository query coverage and UI wiring remain.
- [~] M3.13 Player hidden attributes, personality and foot ratings are persisted as separate field groups.
- [~] M5.1 Top Quick Search provider is implemented; top-bar UI connection remains.
- [~] M5.15 Quick Search tests added; Windows CI runner allocation prevents .NET verification.
- [ ] M5.16 Wire Quick Search results to player/staff/club profile workspaces.
- [ ] M5.17 Implement validated Advanced Search SQL execution for a supported field subset.
- [ ] M5.18 Add Advanced Search result correctness and query-combination tests.

**Latest code before this documentation update:** `e63dc6ee62063c2046ab5f7f28d13c905b9d1d81`.  
**Latest code is not build-verified:** GitHub Actions jobs terminate before runner allocation; the local environment has no .NET SDK.


## Update — checkpoint 013 (2026-11-16)

- [~] M2.11 Local SQLite database initialisation and **Save Atlas** command wired into the desktop header; local path is under LocalApplicationData.
- [~] M3.2 Process detection remains separate from career loading and is labelled **Detect Game**.
- [~] M3.3 **Load Live Save** action is present but disabled/In Progress until a verified career identity and read-only importer are implemented.
- [~] M6.1–M6.9 **Save Live Save** action is present but disabled/In Progress until build-specific mappings, guarded writes, read-back and rollback are proven.
- [ ] M3.4 Read-only live career acquisition and save identity provider.
- [ ] M3.5 Offline save import and record mapping.
- [ ] M4.6 CA/PA values connected to a real PlayerRecord.
- [ ] M4.7 Full attribute/profile data binding.
- [ ] M4.3b Bind graphics locator to the player portrait slot.
- [ ] M5.1 Wire Quick Search input/results to IEntitySearchProvider.

The desktop buttons do not imply that live save loading or writing is already functional. Only local Atlas database initialisation is currently wired.


## Update — checkpoint 014 (2026-11-16)

- [x] Corrected the stale schema idempotence test to assert the current schema version (v2).
- [ ] Full .NET restore/build/xUnit run remains blocked by GitHub Actions jobs failing before runner allocation; no compiler logs are available.


## Update — advanced search and read-only player profile (2026-11-16)

- [~] M5.16 Dynamic Indexed Data category inspects typed field keys already present in `field_values`; consistently typed integer, decimal, text, date and Boolean keys are exposed. Unknown/ambiguous keys are not guessed.
- [~] M5.17 Search result identity handoff opens a read-only typed PlayerRecord snapshot in a separate profile window; Coach and Club profile windows are still In Progress.
- [~] M5.18 Profile binds CA/PA, player attributes, hidden/personality/foot ratings, position ratings, preferred moves, season statistics and newgen evidence where values exist in the local snapshot.
- [~] M5.19 Startup now uses one dependency-injected OnStartup path and initialises/migrates the local Atlas database before opening read-only search providers.
- [!] M8.2 Windows restore/build/test verification blocked: latest GitHub Actions jobs fail before exposing runner steps/logs. Do not mark tests as passing.


## Update — global women-player visibility (2026-11-16)

- [~] M5.20 Global **Include women** preference added to the main header, defaults OFF, persists locally, and is shared by Quick Search and Advanced Search.
- [~] M5.21 SQLite Quick Search and Advanced Search exclude records explicitly identified as female while the preference is OFF; unknown/missing gender remains visible rather than guessed.
- [~] M5.22 Regression tests added for both search paths and OFF/ON behaviour; tests are committed but not executed because GitHub-hosted Windows jobs fail before runner steps/logs.
- [~] M5.23 Rechecked one-sided numeric search: minimum-only and maximum-only conditions are supported; maximum-only has an existing SQLite integration test. Newgen/original search remains Yes / No / Unknown through `is_newgen`.
- [!] M8.2 Current Windows restore/build/test verification blocked by GitHub Actions job startup failure (zero steps, no runner logs); this is not evidence of a source compile failure.
- [-] Wyscout/StatsBomb analytics remain deferred until the simple editor works.

Latest code/test commit: `46ffc47057336684ae1c2c32bffbf00ae6002587`.  
Checkpoint documentation commit: `4fdce8dc8dbbe42c76532d67bec67d6ceba837d4`.  
Current task: resolve hosted-runner/account Actions usage restriction and run the full Windows CI suite.


## Update — FM27 player CSV import path for first hands-on testing (2026-11-16)

- [~] M3.5a Add independent FM View Exporter CSV importer for player-list exports. This is an offline export import path, not an offline `.fm` save parser and not a live-memory reader.
- [~] M3.5b Add **Import CSV** action to the main WPF header and register the importer with the app composition/startup.
- [~] M3.5c Map stable Unique ID, player name, selected identity/personal/club fields, CA/PA only when explicitly exported, 52-attribute catalogue fields, hidden/personality/foot ratings, and additional dynamic columns.
- [~] M3.5d Infer extra columns as integer/decimal/Boolean/date only when every populated value fits the type; otherwise retain them as text. Mixed/range/currency values are not coerced.
- [~] M3.5e Add idempotent file-hash provider namespaces, duplicate UID handling, UTF-8/size/row/column guards, comma and semicolon CSV parsing, and field-level Game Export / Mapped provenance.
- [~] M3.5f Add importer tests for base player mappings, attribute groups, duplicate Nation headers, typed extra columns, numeric/date/Boolean search, German decimal-comma CSV, duplicate IDs, repeat import, missing UID and malformed quoting.
- [~] M5.24 Imported player snapshots can be found by Quick Search and Advanced Search and opened in the existing read-only typed player profile.
- [ ] M8.2 Run full Windows .NET restore/build/xUnit suite for the current importer and UI changes. Source tests are committed but not executed.
- [ ] M8.3 Produce and inspect a fresh Windows x64 artifact after the current source head passes.
- [ ] M8.4 User performs first end-to-end test with their own FM27 player CSV and reports any header mismatch.

**Compatibility note:** FM View Exporter's public current documentation lists FM27 Steam on Windows as its supported target; it does not establish Game Pass plugin compatibility. FM Atlas can ingest a compatible CSV from another route if it supplies the required headers. This does not replace or claim completion of the Game Pass live-save reader.
**Safety boundary:** CSV import reads the selected export and writes only the local Atlas database. It does not read or write FM process memory or modify an FM career file. Imported fields are Game Export / Mapped, not live-memory verified.
**Current code head:** `3a2cc82006d3b915a7954b24e1e1fe347f3e9a61`. Windows Actions run #425 / `43852668416` failed before runner steps/logs; failed-job retry `133249765674` is still queued at the latest check. Do not mark compile/tests as passing.


### Follow-up — build handoff and imported gender verification

- [~] M5.25 Added an import integration test proving explicit CSV `Gender` data feeds the global Include women Quick Search filter.
- [~] M3.5g Field provenance timestamp is generated once per import execution and shared by that import's field values.
- [~] M8.5 Added `scripts/Build-Test-Package.ps1` so the user can run restore, Release build, the full solution tests and Windows x64 publish on their own Windows 10/11 PC. The script stops on any failure and creates no ZIP unless all checks pass.
- [ ] M8.6 Run the local PowerShell build script against the latest branch head and attach the resulting test log / ZIP after passing.
- [!] Hosted Windows CI still has no actionable steps/logs. Latest observed failed run #425 is still the only retrievable run snapshot; its retry ended with zero steps and no log object. The current source is not yet build-verified.

**Latest source hardening commits:** `ba27e090390f429a92f8e34d810114a57031d900`, `82853b019ad393bb0d1920266738064aeafbc41a`, `3109bcc2f3f94cec6a00821d8ae3264718997ef6`, `1e74762531c2d74ad7b576c466b0ead754a3e25c`.


### Latest CI recheck — 2026-11-16

- [!] Latest retrievable Windows Actions run #451 (`43853394674`) on checkpoint sync commit `298e158b70705d9bc2754b24e1f1e1fe347f3e9a61` failed before any job steps were created.
- [!] Retry job `133251606257` also failed with `steps=[]`, no log URL, and 404/BlobNotFound on log download.
- [ ] Current branch head must still pass Windows restore/build/test before an application ZIP is considered test-ready.
- [~] Local fallback script `scripts/Build-Test-Package.ps1` committed to run the exact checks and package only on a clean pass.


## Live save priority update — 2026-11-16

- [~] L1 Exact Game Pass 0.9.2435.0 read-only connector: externally mapped, still awaiting user-machine field verification.
- [~] L2 Player/staff/club batch import into SQLite and Quick/Advanced Search.
- [x] L3 Clear the live provider before every load attempt so stale career data is not left searchable after a failed reload.
- [x] L4 Delete candidate live snapshots after post-import failure/cancellation using a non-cancelled cleanup token.
- [x] L5 Clear visible live counts and Quick Search results while loading and after failed/cancelled loads.
- [x] L6 Lock every owned search/profile window while the animated live-load overlay is visible.
- [x] L7 Add pure fail-closed write preflight for process/build/save/identity/field evidence/old-value/range/memory-region checks.
- [x] L8 Add regression tests for all write-preflight rejection conditions.
- [x] L9 Document the external write-safety research and no-copy GPL boundary in `research/LIVE_EDIT_SAFETY_001.md`.
- [ ] L10 Get a successful Windows build and full test run for the latest branch head.
- [ ] L11 Verify current live reader against a disposable FM27 Game Pass career field-by-field.
- [ ] L12 Add strict scan completeness diagnostics and reject partial career snapshots.
- [ ] L13 Implement a separate safe memory writer only after read verification.
- [ ] L14 Implement first scalar edits with preflight, read-back, conflict handling and undo on a disposable career.
- [ ] L15 Expand beyond player/staff/club to all required FM data families.

**Non-negotiable:** no field is editable until its own write evidence is `WriteVerified` for the exact active build. The current connector remains read-only. The overlay blocks both the main window and owned workspaces while the read/index/validation transaction runs.
