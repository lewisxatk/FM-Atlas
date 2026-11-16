# FM Atlas — Fresh Rebuild Master Checklist

This checklist is the live task tracker. Update it in the same commit as the checkpoint whenever possible.

Legend: [ ] Not started · [~] In progress · [x] Completed with evidence · [!] Blocked · [-] Deferred

## M0 — Protect and inventory
- [ ] M0.1 Confirm active branch and protect `main`.
- [ ] M0.2 Recover complete R7.2.0 source candidate.
- [ ] M0.3 Preserve R7.1.0 and R7.0.2 archives and compare.
- [ ] M0.4 Verify archive hashes/file manifests.
- [ ] M0.5 Inspect user-supplied research programs and files.
- [ ] M0.6 Record licence and reuse decision per source.
- [ ] M0.7 Audit reusable domain code/data schemas/tests.
- [ ] M0.8 Identify legacy UI/architecture to discard.
- [ ] M0.9 Create clean rebuild checkpoint/tag.

## M1 — Research and blueprint
- [ ] M1.1 FMLibreEditor Game Pass profile and live-edit safety study.
- [ ] M1.2 FMSuperScout scanner/extraction/index study.
- [ ] M1.3 fm-editor build layout/entity/undo study.
- [ ] M1.4 fmsave offline save parser coverage study.
- [ ] M1.5 FM Player Analyzer parsing/graphics study.
- [ ] M1.6 FMRTE feature and workflow inventory.
- [ ] M1.7 FM27 canonical player field and attribute catalogue.
- [ ] M1.8 FM27 attribute display order verified.
- [ ] M1.9 Staff and club field catalogue.
- [ ] M1.10 Game Pass build/process fingerprint verified.
- [ ] M1.11 Read/write coverage matrix established.
- [ ] M1.12 Fresh architecture decision documented.

## M2 — Fresh application foundation
- [ ] M2.1 New clean solution.
- [ ] M2.2 Build pipeline and automated tests.
- [ ] M2.3 Approved application shell and REF-001 layout.
- [ ] M2.4 Watched visual language REF-003.
- [ ] M2.5 Compact left navigation (no Search item).
- [ ] M2.6 Top quick search.
- [ ] M2.7 Top Advanced Search action.
- [ ] M2.8 Prominent right-side Load Game action.
- [ ] M2.9 Compact save actions.
- [ ] M2.10 Workspace/player tabs.
- [ ] M2.11 Settings, logs, diagnostics and local persistence.
- [ ] M2.12 Windows desktop resize/accessibility tests.

## M3 — Game connection and data
- [ ] M3.1 FM process detection.
- [ ] M3.2 Storefront/build identification.
- [ ] M3.3 Career loaded detection.
- [ ] M3.4 Read-only live data acquisition.
- [ ] M3.5 Offline save import.
- [ ] M3.6 Typed local database and migrations.
- [ ] M3.7 Player/staff/club entity mapping.
- [ ] M3.8 Data provenance and unknown-value handling.
- [ ] M3.9 Career switch/reload invalidation.
- [ ] M3.10 Data accuracy comparison against FM.

## M4 — Player workspace
- [ ] M4.1 Full-screen REF-004 profile layout.
- [ ] M4.2 Left identity and information rail.
- [ ] M4.3 Player graphics locator.
- [ ] M4.4 Central attribute display.
- [ ] M4.5 Verified FM attribute names/grouping/order.
- [ ] M4.6 CA and PA display.
- [ ] M4.7 Right-side grounded information panels.
- [ ] M4.8 Compact player tabs.
- [ ] M4.9 Multiple open player tabs.
- [ ] M4.10 Unsaved change protection.

## M5 — Search
- [ ] M5.1 Top Quick Search.
- [ ] M5.2 REF-002 FMRTE-style Advanced Search dialog.
- [ ] M5.3 Player / Coach / Club selectors.
- [ ] M5.4 General filters.
- [ ] M5.5 Contract filters.
- [ ] M5.6 Technical/GK filters.
- [ ] M5.7 Mental filters.
- [ ] M5.8 Physical/Hidden filters.
- [ ] M5.9 Preferred Moves filters.
- [ ] M5.10 Relations filters.
- [ ] M5.11 Position pitch and suitability.
- [ ] M5.12 Min/max, boolean and lookup controls.
- [ ] M5.13 Explicit AND/OR semantics.
- [ ] M5.14 Saved searches and result sorting/columns.
- [ ] M5.15 Search correctness/performance tests.

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
- [ ] M7.3 Staff verified editing.
- [ ] M7.4 Club search/profile.
- [ ] M7.5 Club facilities/finances/stadium information.
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
- [ ] M9 Squad Studio.
- [ ] M10 Tactical Studio and Tactical Effectiveness.
- [ ] M11 Recruitment and Value Finder.
- [ ] M12 Wyscout/StatsBomb-inspired Moneyball/per-90/percentile analytics.

## Current status at creation

- [x] Approved design references REF-001 to REF-004 documented in the design branch.
- [x] Fresh rebuild decision and scope documented in this branch.
- [x] Interruption-safe continuity procedure documented.
- [ ] No new application code has been started.
- [ ] Source archives still require recovery/import and integrity verification.
