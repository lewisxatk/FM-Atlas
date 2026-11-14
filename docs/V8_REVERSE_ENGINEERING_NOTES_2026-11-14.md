# FM Atlas V8 — reverse-engineering research notes (2026-11-14)

This note records external implementation patterns reviewed during the V8 rebuild. It is a research ledger, not a claim that another project's offsets or source code have been adopted. FM Atlas keeps its own implementation, evidence records and build-specific validation.

## FMSuperScout — fast, read-only career indexing

Repository: https://github.com/mavarobli/FMSuperScout

Files reviewed:
- https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs
- https://github.com/mavarobli/FMSuperScout/blob/main/core/Dumper.cs
- https://github.com/mavarobli/FMSuperScout/blob/main/scanner/Program.cs

Useful architectural patterns:
- Keep a shared scan core separate from its Windows external-process scanner and other hosts.
- Select a version-specific field layout from the loaded game module version; do not silently carry offsets into a new game build.
- Keep the external scanner read-only and independent of a mod/plugin layer in the game folder.
- Emit structured scan diagnostics: phase timings, object counts, class-offset histograms, deduplication counts, manager identity and version status.
- Treat human-manager identity as an explicit relationship to resolve, not a guessed first manager.
- Reuse a long-lived watcher/request signal so the scan can be refreshed without restarting the desktop UI.

FM Atlas application:
- Existing live reader remains read-only, uses exact process/build/session detection, a save-load generation key, bounded cancellation and a transactional snapshot publish.
- Next useful adoption is diagnostic/throughput instrumentation and better staged progress, not copying another project's offsets.
- Keep scan timings, coverage and deduplication visible in the Activity Centre/Diagnostics rather than adding an unbounded memory scan.

## barak14/fm-editor — field evidence and versioned schema

Repository: https://github.com/barak14/fm-editor

Files reviewed:
- https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md
- https://github.com/barak14/fm-editor/blob/main/src/fields/fm27/player.rs
- https://github.com/barak14/fm-editor/blob/main/docs/development.md

Useful patterns:
- Separate field definitions from the version/storefront memory-layout registry.
- Keep a written memory-layout notebook that distinguishes verified fields from inherited or unverified fields.
- Record independent in-game witnesses for each field, including the displayed UI value, player/club identity and exact game build.
- Use fake address spaces and golden UI-captured fixtures for deterministic tests.
- Re-read values after writes and verify Undo against fresh process reads; cached FM profile screens can remain stale until refreshed.
- Group related physical writes under one logical history action, but explicitly test and report partial/refused outcomes.

FM Atlas application:
- Preserve the current fail-closed live-write policy. V8 has no exposed live Apply/Undo actions; the local Editor Studio writes only to the Atlas database.
- Do not import an offset simply because another editor lists it. Require exact build/storefront match, an FM27 UI witness, identity re-resolution, current-byte preflight, read-back and tested rollback before considering any future write capability.
- Continue using source labels and provisional confidence for un-witnessed live fields.

## FMLibreEditor — save-generation safety and graceful read-only fallback

Repository: https://github.com/tozmoz/FMLibreEditor

Files reviewed:
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/INSTALL.md

Useful patterns:
- Explicitly support separate storefront profiles and known game builds.
- When the save changes, stop edits/freezes and require a fresh reload; never carry one save's pending action into another.
- Keep the changes view as an audit trail with a clear Undo state.
- Fall back to read-only when the version/layout is not supported.
- Provide a diagnostics report that avoids exposing player/club names or private file paths.

FM Atlas application:
- Existing live career monitor checks the process, module profile and save-load counter and cancels a scan if the generation changes.
- V8 history keeps old live-memory entries for audit but live Undo is blocked. Local Atlas edits can still be undone in the Atlas database only.
- Diagnostics should report build/profile/coverage and failures without dumping private save contents.

## Current build compatibility caution

The reviewed sources describe different FM27 build identifiers and release states. The FM Atlas settings page currently documents fmsave's registered layout as FM27 `27.0.0+2486695`; the reviewed FMLibreEditor release notes identify later FM27 builds, including Steam `27.0.1` and Game Pass `0.9.2435.0`. These are not interchangeable identifiers. Keep the Atlas reader's exact build gate and provisional data labels until the user's installed build has been checked.

## Next engineering actions

1. Add phase timing, row counts, dedupe and coverage telemetry to the existing bounded scanner.
2. Add golden, source-backed FM27 fixtures for player/staff/club identities and field decoders.
3. Extend diagnostics with build/profile, import validation, database health and last scan/import result.
4. Run current Windows Release build and tests; the current workflow run is queued and must not be reported as passed until completed.
5. Keep UI, Advanced Search, player comparison, shortlists, performance analytics and existing database compatibility intact while extending the data pipeline.
