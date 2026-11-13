# FM Atlas 27 — Change Log

## 7.2.0 — Obsidian Intelligence Overhaul (source integration checkpoint)

- Applied the Obsidian + Swiss Editorial dark design direction with charcoal panels, off-white text and electric-lime accent.
- Added multi-condition Statistical Search Lab (up to 60 AND conditions), grouped metric categories, optional per-90 and minimum-minute controls.
- Added indexed player metric facts for season, career-history and match-stat records while preserving the original lossless JSON rows.
- Added multi-attribute 1–20 Player Search with up to 60 conditions and quick role-oriented presets.
- Added a player profile metric path that prefers the indexed fact table and retains a compatibility fallback for older snapshots.
- Added Value & Upside Finder using explainable ability/potential percentile versus market-value percentile comparisons. It explicitly retains the save reader's source value units and does not claim to reproduce SI's valuation formula.
- Added independent exact FM27 Windows Steam/Game Pass build fingerprints and a narrow live writer for CA, PA and 47 visible standard player attributes.
- Added career-load generation rechecks before each live batch write/undo field; hidden attribute offsets remain blocked; read-back and guarded inverse Undo remain required.
- Wired the existing graphics resolver into player and club profiles so the user's local graphics packs are reused rather than copied.
- Corrected help and Tester & Logs copy to distinguish exact module profile matching from field-level write certification.
- Added R7.2.0 implementation status, completion roadmap and Windows/user acceptance checklist.
- Windows .NET build and FM runtime acceptance have not yet been run; see `docs/R7.2.0-IMPLEMENTATION-STATUS.md`.

## 7.1.0 — Aurora Intelligence Upgrade

- Added Role Fit Lab with 23 transparent IP/OOP role profiles, attribute coverage and CSV export.
- Expanded Analytics with derived season per-90 options and a configurable minimum minutes threshold.
- Added Editor Studio OFF-by-default session gate; local mass edit supports Set, Add and percentage Scale.
- Hardened local Undo so stale history cannot overwrite a newer value.
- Added Tester & Logs with environment checks, redacted diagnostics, recent log copy and support ZIP export.
- Added rolling application logs and unhandled-exception capture.
- Added logged build script and refreshed R7 research/master-specification documents.
- Live process writes were disabled in that release; see this 7.2.0 update for the new limited writer.

## 7.0.0 — R7 Aurora Workbench

- Refreshed the FM27 desktop product shell, global search, profile tabs, Analytics, local Mass Edit, diagnostics and database checkpoint workflow.
