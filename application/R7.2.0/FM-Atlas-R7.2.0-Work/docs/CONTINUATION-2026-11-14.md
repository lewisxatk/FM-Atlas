# FM Atlas continuation — 14 November 2026

## Branch and preservation

- Working branch: overhaul/monolith-full-platform.
- This branch is intentionally kept separate from main; it is substantially ahead and has diverged from it.
- The separate rebuild/v8-full-product-rebuild line and its open V8 pull request are not merged or rewritten by this continuation.
- Do not force-push, reset, or merge these lines as a substitute for a deliberate integration review.

## Changes in this continuation

1. ScoutDatabase now sets an explicit 60-second SQLite command timeout in its connection string. The app runs independent database operations on worker threads and some workspaces issue multiple reads concurrently; the longer timeout gives transient write contention time to clear instead of failing at the provider default timeout.
2. Added ScoutDatabaseConcurrencyIntegrationTests, which holds a real SQLite write transaction, starts a second Atlas write, verifies it waits, releases the first transaction, and verifies both shortlist records persist.
3. Existing large-dataset coverage remains in ScoutDatabaseAttributeSearchIntegrationTests.SearchPlayerAttributeCriteriaAsync_HandlesFortyThousandPlayerDataset: 40,286 synthetic players, three indexed attributes per player, and a three-condition query expected to return only the seeded target.

## Reverse-engineering review

- FMLibreEditor v0.5.1 release notes (11 Nov 2026) confirm separate known FM27 Steam 27.0.1 and Game Pass 0.9.2435.0 builds, save-change detection that suspends writes until explicit reload, and privacy-conscious diagnostics. Atlas already keeps storefront-specific profiles, save-generation checks and redacted support bundles; these remain mandatory safeguards, not optional polish.
- FMLibreEditor's development notes describe game_plugin.dll process access, PE-header profile identification, per-build entity-array locations and a fail-closed/read-only approach when checks do not agree. Atlas must continue to treat a module profile match as distinct from field-level verification.
- FMSuperScout's public project describes fast full-save extraction, instant local filtering, saved presets, three-player comparison, squad needs and role ratings. These are useful workflow/performance benchmarks; they do not establish Atlas data correctness or license permission to copy code.
- fmsave remains the read-only save-data path; FM View Exporter remains a supplementary source for exported in-game columns. Missing fields must remain missing, and source units/coverage must be retained.

## Automated validation

The branch contains .github/workflows/fm-atlas-windows-build.yml, which runs on pushes to this branch. It restores and builds the .NET 8 WPF solution on Windows, runs the MSTest suite, and uploads the test results and Release output when successful.

A GitHub commit is not proof that this workflow passed. Record the actual Actions run result and artifact before describing the build as validated.

## Required PC acceptance before release

- Launch the built executable on Windows 10/11 and confirm no startup exception.
- Import a COPY of the FM27 career save; preserve the original save and existing Atlas database.
- Confirm player/staff/club counts, import warnings and build-coverage report.
- Run Advanced Search, multi-attribute search, statistical search, player comparison, similar-player and Performance vs Value against the imported data.
- Exercise shortlist/recruitment projects and confirm budgets distinguish asking price from market value and weekly wage.
- Confirm Players workspace loading/progress completes on the real save; collect redacted diagnostics if it stalls.
- Validate faces/logos using the user's installed graphics root.
- Keep live memory editing disabled unless the exact storefront profile and field-level evidence checks pass. Any Apply/Undo trial must use a disposable offline copy and only the currently whitelisted fields.
- Test common Windows scaling/resolutions and confirm no clipped controls or inaccessible navigation.

## Status

Source changes are committed. Windows CI and user-machine runtime acceptance are pending until their results are observed. This document intentionally does not mark untested release gates complete.
