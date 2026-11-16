# FM Atlas — Reverse Engineering Findings 002

**Date:** 2026-11-16  
**Working branch:** `rebuild/fresh-foundation-001`  
**Scope:** FM27 Game Pass live-data architecture, offline-save trust, and search acceptance gaps.  
**Handling:** Source inspection only. No third-party code, binaries, proprietary assets, or unverified offsets copied into FM Atlas.

## 1. FMLibreEditor: separate build profiles from edition layouts

Inspected:
- https://github.com/tozmoz/FMLibreEditor/blob/ae48fcf30ce4cb1fca1425a6c405df685cad758d/crates/fmlibre-core/src/profile.rs
- https://github.com/tozmoz/FMLibreEditor/tree/main/profiles/fm27

The source models two different kinds of information:

- A **build profile** identifies a particular executable/plugin build and contains build-specific static locations/RVAs.
- An **edition layout** describes offsets inside game objects for an edition. The source explicitly documents that object layouts can be shared across storefront builds of the same game version, while RVAs are build-specific.
- The layout deserializer rejects unknown fields, which is a useful fail-closed configuration pattern.

**FM Atlas decision:** keep executable fingerprinting, static pointer discovery, edition object layout, and per-field read/write evidence as separate concepts. A matching module timestamp/image size is only a candidate build identification. It must not automatically enable memory reads or writes.

Do not copy any FMLibreEditor memory offsets. GPL-3.0 source remains research-only unless FM Atlas deliberately adopts compatible licensing obligations.

## 2. Save-generation invalidation is a first-class safety requirement

The inspected FMLibreEditor `GameLayout` documents a game-owned object that counts how many saves the game has loaded since process start. The count changes when a career/save is loaded, but not merely because the editor reads its lists.

**FM Atlas implementation requirement:**
1. Capture process identity, module fingerprint, and save-generation value when a read session starts.
2. Associate every pointer/cache snapshot with that generation.
3. Re-check generation before using cached entity pointers and before staging/applying any edit.
4. If the generation changes, invalidate all pointer-derived state and require a fresh read.
5. Treat process restart, module replacement, career reload, and unknown generation as separate invalidation reasons.

This is a research-derived design lead, not an assertion that FM Atlas currently has a verified Game Pass generation reader.

## 3. FMSuperScout: keep scanning separate from the desktop UI

The FMSuperScout repository is MIT-licensed and describes a separate read-only scanner/application architecture. Its field catalogue distinguishes player, staff, club, contract, attribute, position, hidden, and personality data.

Reference:
- https://github.com/mavarobli/FMSuperScout

**FM Atlas decision:** retain the provider boundary already selected in the fresh architecture. A future Game Pass reader should return typed, provenance-bearing snapshots to the application layer. WPF controls should not own process handles, raw pointer arithmetic, or direct memory access.

Any MIT code reuse requires file-level review, dependency review, and preservation of required copyright/license notices. No code has been copied in this pass.

## 4. fmsave: field trust, coverage, and reader validation are different

Inspected:
- https://github.com/rhiever/fmsave/blob/main/docs/guides/trust.md
- https://github.com/rhiever/fmsave/blob/main/src/fmsave/models/players.py

The trust guide makes three important distinctions:

- **Verified/unconfirmed** describes whether a field's meaning was checked against the game screen.
- **Coverage** describes how often a field is present; missing values can have several causes and are not equivalent to zero.
- **Reader checks** assess structure, joins, and expected ranges. Some are advisory; passing strict checks still does not prove every decoded value is correct.

The player model also distinguishes displayed attributes (1–20) from raw attributes (1–100), and warns that some identifiers/relationships remain unconfirmed.

**FM Atlas decision:** preserve these as separate metadata dimensions. Never convert missing data to zero, never treat a populated field as automatically verified, and never expose raw storage values as the displayed 1–20 rating without an explicit conversion and test.

The fmsave project is MIT-licensed; any reuse still requires dependency/license review and retained notices. No code has been copied in this pass.

## 5. Search implementation audit: confirmed behaviour and remaining acceptance work

Inspected fresh-branch source:
- `MainWindow.xaml`
- `MainWindowViewModel.cs`
- `AdvancedSearchWindow.xaml.cs`
- `AdvancedSearchWindowViewModel.cs`
- `AdvancedSearchQuery.cs`
- `SqliteEntitySearchProvider.cs`
- `SqliteAdvancedSearchProvider.cs`

Confirmed in source:
- The Include women preference is a global main-window setting persisted under the user's LocalAppData FM Atlas settings folder.
- Quick Search passes the preference into the SQLite provider.
- Advanced Search receives a live preference callback and places the current value on each executed query.
- Both SQLite search providers exclude explicit female aliases when the preference is off; non-player entity types are not filtered.
- Unknown or missing gender is retained rather than silently classified as female.
- Advanced Search has cross-category field-finder mode, min/max controls, independent position AND/OR logic, saved JSON presets, and a query validator.
- SQL user values are parameterised; LIKE wildcard characters are escaped for literal text search.
- Dynamic indexed fields are discovered from typed SQLite field projections. Ambiguous mixed value types are omitted.

Remaining acceptance gaps:
1. Add Quick Search tests for female aliases (`female`, `woman`, `women`, `f`), unknown gender, and Staff/Club records.
2. Test the setting persistence across application restarts and preference changes while Advanced Search remains open.
3. Verify gender source normalisation for real FM27 Game Pass imports; do not assume every provider uses the same stored text.
4. Build a field-by-field matrix: documented, mapped, indexed, executable, read-verified, and not-yet-implemented. A visible field catalogue entry is not proof of a working query mapping.
5. Test every generated attribute key and its min-only/max-only/range behaviour against stored fixture records.
6. Verify presets preserve criteria and logic while the global women-player preference remains a separate application setting.

## 6. CI evidence and current limitation

The latest observed workflow for commit `46ffc47057336684ae1c2c32bffbf00ae6002587` is run 383 (ID `43845648971`). GitHub reports the `build` job failed, but the job has no step summary and the job-log endpoint returns 404/BlobNotFound. This does not identify a compiler/test failure and does not establish a billing/quota cause.

The workflow definition uses `windows-latest`, .NET 8 setup, restore, Release build, test, and artifact upload. A previous successful Windows run is recorded in `docs/CURRENT_CHECKPOINT.md`; it does not verify commits made after that run.

**Next CI actions:** inspect repository Actions settings/billing and runner availability; trigger a fresh workflow_dispatch/push run once the runner issue is understood; retain logs and artifact checks. Do not mark the current source as build-verified until the latest commit passes.

## 7. Next engineering sequence

1. Resolve and reproduce the Windows runner failure.
2. Add the Quick Search women-filter tests and persistence tests.
3. Complete Advanced Search field mapping/fixture coverage and clearly label fields still In Progress.
4. Continue Game Pass build identification and read-only connection research, with exact build evidence.
5. Implement read-only import only after the process/session safety gates and a disposable-career test plan are ready.
6. Keep live writes disabled until each field has independent read verification, a guarded write test, read-back, and in-game confirmation.
