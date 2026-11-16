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
- [~] M2.6 Top Quick Search placeholder added; actual search is intentionally disabled until real data/index exists.
- [~] M2.7 Top Advanced Search action placed; intentionally disabled until query service/dialog exists.
- [~] M2.8 Prominent right-side Load Game button added; currently performs process/module presence check only, not career loading.
- [~] M2.9 Compact Save Atlas / Save Changes controls placed; intentionally disabled until persistence/live writer exist.
- [~] M2.10 Player-profile workspace shell created with an open-player tab row; multi-player tab management is not wired.
- [~] M2.11 Local SQLite schema v2 and typed snapshot persistence implemented; settings/logs/diagnostics integration and Windows verification remain.
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
- [x] M4.1 Full-screen REF-004 profile layout shell created; actual screenshot comparison and live-data integration remain.
- [~] M4.2 Left identity/information rail and portrait placeholder created; read-only graphics locator exists, WPF photo binding remains.
- [~] M4.3 Read-only config.xml graphics locator and synthetic tests implemented; actual user-folder validation, image decoding/cache and profile binding remain.
- [~] M4.4 Central attribute groups are rendered from the Domain catalogue with empty/unavailable values; real player values and editing remain.
- [~] M4.5 Names/grouping/source order documented and coded; personality is separate from hidden attributes; final FM27/FMRTE visual order verification remains.
- [ ] M4.6 CA and PA display.
- [ ] M4.7 Right-side grounded information panels.
- [~] M4.8 Compact top-of-profile FMRTE-style section tabs added (including Personality, Hidden, Staff, Clone Center and Role Attributes); they remain disabled until a real player record is loaded and section navigation is connected.
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
- [ ] M4.9 Multiple open player tabs.
- [ ] M4.10 Unsaved change protection.

## M5 — Search
- [~] M5.1 Top Quick Search UI shell exists and a SQLite Quick Search provider is implemented; UI/provider wiring and profile navigation remain.
- [~] M5.2 REF-002 FMRTE-style Advanced Search dialog shell created; not connected to search execution.
- [~] M5.3 Player / Coach / Club selectors now switch the schema context; filter controls stay disabled until entity/query providers are verified.
- [~] M5.4 General player panel retains the REF-002 layout; other categories are rendered from the schema; query execution is not connected.
- [~] M5.5 Contract panel now renders schema-defined fields; controls/query remain disabled.
- [~] M5.6 Technical/GK panel now renders schema-defined player/coach fields; controls/query remain disabled.
- [~] M5.7 Mental and separate personality panels now render schema-defined fields; controls/query remain disabled.
- [~] M5.8 Physical/Hidden panel now renders schema-defined fields; controls/query remain disabled.
- [~] M5.9 Preferred Moves panel now renders schema-defined fields; controls/query remain disabled.
- [~] M5.10 Relations panel now renders schema-defined fields; controls/query remain disabled.
- [ ] M5.11 Position pitch and suitability.
- [ ] M5.12 Min/max, boolean and lookup controls.
- [ ] M5.13 Explicit AND/OR semantics.
- [ ] M5.14 Saved searches and result sorting/columns.
- [~] M5.15 Quick Search provider tests cover entity filters, literal wildcard handling and limits; Advanced Search executor tests remain.

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
