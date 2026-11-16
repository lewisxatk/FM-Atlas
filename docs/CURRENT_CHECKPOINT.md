# FM Atlas — Current Checkpoint

**Checkpoint:** 001  
**Date:** 2026-11-16 UTC  
**Branch:** `rebuild/fresh-foundation-001`  
**Base:** Design branch commit `0f4b1b9238bd62cf1c9ab3af3f684f7da1c0ff14`  
**Status:** Planning/recovery gate. No new application code started.

## Mission

Fresh FM Atlas desktop application. Legacy application is a source of selectively reusable research/code only; do not continue the old UI or preserve poor architecture merely for continuity.

## Completed and saved

- Approved design references REF-001 through REF-004 are recorded in `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md` on the design branch.
- REF-004 full-screen player profile is specified: left identity rail, central FM attributes in verified FM order, right verified information, compact player sub-tabs and multi-player workspace tabs.
- Fresh rebuild product scope and milestones are in `docs/FRESH_REBUILD_MASTER_PLAN.md`.
- Interruption/recovery procedure is in `docs/SESSION_CONTINUITY.md`.
- Resumable task tracker is in `docs/FRESH_REBUILD_CHECKLIST.md`.

## Historical source and research facts (not yet independently revalidated)

- The repository's prior README says its main branch is a documentation/recovery-control baseline and that full R7.0.2/R7.1.0/R7.2.0 application source had not been imported.
- R7.2.0 is the newest supplied source candidate: 106 files; archived SHA-256 recorded in `recovery/R7.2.0-CHECKPOINT.md`.
- R7.1.0 and R7.0.2 are historical comparison baselines.
- A recovery bundle named `FM-Atlas-GitHub-Recovery-Import-R7.2.0.zip` was previously created; import script is intended to create a recovery branch without modifying main.
- The R7.2.0 package documents a narrow live writer for CA/PA and 47 visible attributes, but compilation, user Game Pass profile match and in-game Apply/read-back/Undo were not verified in the earlier inventory.
- Research notes cover FMLibreEditor, FMSuperScout, fm-editor, fmsave, FM Player Analyzer, FM View Exporter, TalentSeer, Teknik Oda/Toby, FaceDrop, FMRTE and related tools.
- Earlier conversation reports seven uploaded research items: FM Dossier, SaveMyTime, Teknik Oda/Toby, FM Player Analyzer, FMLibreEditor, TalentSeer and FMSuperScout. Their original bytes/installers are not present in this documentation branch. The FMSuperScout installer was previously hash-checked against the published v1.7.3 release; installers were not executed.

## Current milestone

**M0 — Protect and inventory.**

## Current next actions

1. Check whether the user can provide the previously generated R7.2.0 recovery bundle or push its recovery branch to GitHub. Do not claim the application source is available until the tree is verified.
2. Inspect/recover original source archive manifests and hashes.
3. Inventory the previously supplied research packages; request only missing source archives/docs, not already available public repositories.
4. Complete research evidence and licence matrix.
5. Write `docs/ARCHITECTURE_DECISION.md` after the technical audit.
6. Only then create the clean application solution.

## User action needed

Potentially provide:
- `FM-Atlas-GitHub-Recovery-Import-R7.2.0.zip` (or push its generated recovery branch).
- Any source ZIPs/docs for FMLibreEditor or FM Player Analyzer that were not retained.
- The actual installed FM27 Game Pass build/version details and, later, a disposable offline career copy for user-side testing. Do not upload a personal/only career save to GitHub.

## Tests

- No code build or game integration tests run in this checkpoint.
- GitHub documentation writes only.
- No program installers executed.
