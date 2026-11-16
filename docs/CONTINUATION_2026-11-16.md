# FM Atlas — Continuation checkpoint (16 November 2026, 12:55 UTC)

## Source of truth
- Repository: `lewisxatk/FM-Atlas`
- Active branch: `rebuild/fresh-foundation-001`
- Source checkpoint before this note: `a36c56f2c027c0454f4cb8a83b69fe3c4b376204d`
- Draft PR: #10; do not merge until Windows build, tests and runtime acceptance pass.
- `main` remains untouched by rebuild work.

## Verified repository state
The branch contains the fresh .NET 8 WPF solution, typed domain/application/infrastructure layers, SQLite index, dashboard, directories, profile workspaces, Advanced Search, read-only FM27 Game Pass connector and diagnostics. Player, Staff and Club profile sections are present, but several sections intentionally show empty states because the live importer does not yet supply their related entity families.

The Club profile now joins imported Player and Staff records by numeric `club_uid`. This join is source-mapped and regression-tested with fixtures, but the memory offsets have not been verified against the user's running Game Pass career.

## Latest Windows CI result
- Run 43868285042, attempt 1: failed before runner allocation.
- Retried job 133311554992, attempt 2: failed in five seconds with `runner_id=0`, empty `steps=[]`, and no runner name.
- Neither attempt ran Restore, Build or Test. This is not a compiler diagnosis and is not a successful build.
- Current tool environment has no Windows/.NET SDK/MSBuild, so no local WPF compile or launch can replace this check.

Do not alter workflow configuration based only on these runner-allocation failures. Obtain a real Windows runner or run the documented build script on the user's Windows PC.

## FMRTE 27 research rules confirmed
Official documentation:
- https://docs.fmrte.com/scouting/search/
- https://docs.fmrte.com/scouting/quick-search/
- https://docs.fmrte.com/getting-started/load-game/
- https://docs.fmrte.com/players/overview/
- https://docs.fmrte.com/clubs/overview/
- https://docs.fmrte.com/help/faq/

Rules to preserve:
1. Advanced Search has Player, Coach and Club entity types, grouped criteria, independent minimum/maximum ranges and saved criteria.
2. Position criteria default to All selected positions; Any of these changes the logic to OR. FMRTE's At Least mode changes exact position ratings to minimum ratings. FM Atlas expresses minimum familiarity with a minimum and blank maximum.
3. Numeric fields must allow minimum-only, maximum-only and inclusive bounded ranges. For 1–20 attributes, do not silently substitute exact matching for a minimum.
4. Quick Search is distinct from Advanced Search. FMRTE supports name/ID lookup, men's/women's scopes, type-aware results and explicit generated/newgen terms.
5. Generated/newgen means a source-identified generated record. It is not synonymous with first appearing in a later scan, being young, high PA, unusual name or portrait.
6. A first-seen-since-last-scan detector requires a known complete baseline, repeated complete snapshots, stable identity and explicit incomplete-scan handling. Until then, unknown newgen status remains Unknown.
7. Loading means reading the currently running career. A live-game edit is not persisted to the career file until Football Manager itself saves the career.
8. Undo must check the current value before restoring the previous value; never blindly overwrite concurrent game changes.

Current code audit:
- `AdvancedSearchQuery` and its validator support one-sided numeric/date boundaries.
- Search catalog generates player attribute fields from `PlayerAttributeCatalog` and supports typed indexed fields.
- Separate All/Any position logic exists.
- `is_newgen` exists as a filter, but the live importer does not yet populate it. Unknown must remain visible as Unknown.
- Regression tests include one-sided ranges, position logic and unknown newgen status; these are source tests only until a Windows runner executes them.

## Watched.Earth / FM Atlas visual direction
Apply the user's REF-003 visual language: near-black canvas, charcoal surfaces, restrained vivid orange, editorial display typography, monospaced technical metadata, subtle outlines and dense data visualisations. Preserve REF-001 main application composition, REF-002 FMRTE Advanced Search information architecture and REF-004 full-screen player profile composition. Do not replace approved layouts with a generic dashboard/filter builder. Keep readable accessible contrast, keyboard navigation and explicit control labels.

## Remaining work — ordered
1. Restore successful Windows .NET 8 Release restore/build/test for the exact branch head; fix only errors produced by real logs.
2. Launch the WPF app and compare Dashboard, directories, Advanced Search, Player, Staff and Club profiles against REF-001 to REF-004 at normal Windows scaling.
3. Verify the exact FM27 Game Pass package fingerprint and live reads against a disposable offline career. Compare entity counts, identity, attributes, CA/PA, positions, staff and club relationships with in-game screens and independent evidence.
4. Promote fields from Mapped to ReadVerified only after field-by-field comparison. Expand importer coverage to the missing club, player and staff record families.
5. Implement authoritative newgen provenance or a complete-baseline snapshot-delta detector; never label a newly observed player a newgen without evidence.
6. Only after successful read verification, implement a separate allow-listed writer. Require process/build/save-generation/entity identity, expected-old-value preflight, safe writable-region validation, immediate read-back, conflict handling and guarded Undo.
7. Test first scalar edits only on a disposable offline career. Keep Save Live Changes disabled until each field is WriteVerified.
8. Complete custom graphics-folder selection, remaining profile data, visual/DPI/accessibility acceptance and release packaging.

## Safety
- Existing process-memory accessor is read-only.
- No process-memory write API is implemented.
- Save Live Changes remains disabled.
- Unknown/unregistered game builds remain read-only.
- Save Atlas refers to local FM Atlas database state; it does not save edits into the FM career.
- Never test destructive operations on the user's only career save.
- Do not merge PR #10 or mark the app release-ready before actual Windows and Game Pass acceptance.
