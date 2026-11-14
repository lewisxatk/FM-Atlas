# FM Atlas V8 — Integrated implementation status

**Branch:** `rebuild/v8-full-product-rebuild`  
**Product identity:** FM Atlas V8 · Atlas Liquid  
**Status:** integrated source milestone. A hosted Windows .NET 8 Release build and self-contained win-x64 package succeeded on 14 November 2026 (GitHub Actions run #79, commit `18d32e2d`). The WPF application has not yet been launched on a real Windows PC and Football Manager acceptance tests have not yet run.

## Implemented in this V8 source pass

| Area | Current source state | Notes |
|---|---|---|
| Application identity | V8.0.0 metadata | Assembly, file and product metadata updated. Historical source-folder names are retained to avoid breaking existing paths. |
| Atlas Liquid appearance | Source implemented | Glass Dark, Glass Light and Studio modes. The selection persists in `%LOCALAPPDATA%\\FMAtlas\\theme.txt`. |
| Top command toolbar | Source implemented | Compact icon controls for Global Search, offline save import, Save Atlas, Shortlists, Analytics, paused live scan, theme switching and diagnostics. |
| Hover help | Source implemented | Toolbar buttons and navigation items have explanatory tooltips and delayed hover display. |
| Side navigation | Source implemented | Grouped sections, active item treatment, collapsible icon rail and hover descriptions. |
| Button feedback | Source implemented | Rounded controls include hover, pressed, keyboard-focus and disabled visual states. |
| Workspace motion | Source implemented | 150 ms workspace fade-in is conditional on the Windows system animation setting; no continuous decorative animation. |
| Keyboard search | Source implemented | Ctrl+K opens the existing Global Search window; Ctrl+F remains supported. |
| Universal Advanced Search | Source implemented; build verification pending | Replaced the old menu-only hub with a dynamic category/field builder. One parameterized player query combines whitelisted core fields, actually stored PlayerAttributes and latest-save FmsavePlayerFacts, with up to 60 ALL/AND or ANY/OR conditions and typed number/text/boolean controls. Performance remains a separate player-period search. Saved universal profiles, nested logic groups, configurable columns and dedicated source-coverage badges remain open. |
| Navigation responsiveness | Improved in source | Navigation buttons stay enabled while workspace construction runs; latest requested destination is queued. |
| Player workspace activation | Improved in source | Previously open profiles are restored after the workspace is displayed rather than awaited during workspace construction. |
| Live-memory scan safety | Blocked by default in V8 UI | The full legacy scan is behind a compile-time false gate and the toolbar action is disabled. |
| Live-memory edit safety | Blocked by default in V8 UI | CA/PA and visible-attribute live Apply handlers are gated. Local Atlas edits and offline save import are unaffected. |
| Save-import progress and cancellation | Source implemented | Python table-reader stages and row counts stream to the UI; a toolbar Cancel action kills the reader process and the database checks cancellation during row indexing. The elapsed time is shown. Cancellation/failure before commit rolls back the active transaction and retains the previous committed snapshot. The temporary full-save JSON export is deleted after success, cancellation or failure. The C# importer no longer makes duplicate player/staff/club array and List copies; it still parses the complete JSON document in memory. |
| SQLite write coordination | Source implemented | Mutating/import/undo/checkpoint operations share a process-wide writer gate and the connection has a 30-second busy timeout with foreign keys enabled. Lock stress tests remain open. |
| Existing R7.2 functionality | Retained in source | Player/staff/club discovery, indexed statistics, multi-attribute and statistical search, Value Finder, Role Fit, squad/tactical workspaces, shortlists, graphics resolution, local edits, logs and diagnostics remain in the source tree. |

## Build verification

- First hosted Windows Release attempt found seven compile errors: four Advanced Search `RoutedEventHandler` callback signatures and three ambiguous `Path` references caused by the WPF Shapes import. The event handlers and explicit `System.IO.Path` alias were fixed in `80b4160d` and `b35432d8`.
- **Successful run #79:** https://github.com/lewisxatk/FM-Atlas/actions/runs/43534383967 — .NET 8 install, solution restore, Release compilation, self-contained Windows x64 publish and artifact upload all passed for commit `18d32e2d5fbe0f1549da6d9d55391414e0b05bdf`.
- Artifact: `FM-Atlas-V8-win-x64` (GitHub Actions artifact ID `14088498868`, 68,725,071 bytes including the artifact wrapper). The extracted product ZIP contains `FmAtlas.exe` (x64 PE), `tools/Export-FMAtlas-Fmsave.py`, `Setup-FMAtlas-Save-Bridge.ps1`, and a PDB. Product ZIP SHA-256: `4a16f815150e41855b2b1d5dba884c6878ac5a69f44b8ae44ebea8570b2798e2`.
- This verifies compile and package only. Startup, visual layout, import, runtime database behaviour and FM performance have not been tested on a real Windows PC; V8 is not release-ready yet.

## Safety policy

The V8 application must be useful without Football Manager running. Offline, read-only save import remains the primary route for career data.

The full live scan performs a large number of cross-process reads over tens of thousands of player slots. Because the previous diagnostics showed SQLite contention and the user reported Football Manager slowdown/crashes, the V8 application blocks the legacy full scan until a bounded, cancellable, profiled replacement exists. This is a precaution based on observed risk; it is not a claim that one specific memory-read path has been proven to be the sole cause of every crash.

Live Apply remains blocked even though the legacy guarded writer code is retained. Re-enable it only after the exact Steam/Game Pass mappings are independently validated against the in-game UI and disposable offline save copies, plus read-back/undo regression tests.

## Existing feature foundation retained

- Player directory, paged results, player profiles, positions/roles and available attributes.
- Global Search for players, staff and clubs.
- Multi-attribute player search over available source fields.
- Indexed career-season/history/match statistics and multi-condition statistical search, including per-90 with minimum-minute controls.
- Value Finder / upside comparisons based on fields present in the imported data.
- Role Fit Lab and squad/tactical workspaces.
- Club and staff directories, transfer discovery and shortlists.
- Local editor, edit history, mass edit, diagnostics and Tester & Logs.
- Graphics Resolver for locally installed graphics packs.
- Read-only fmsave bridge and FM View Exporter CSV support.

A feature being present in source is not equivalent to verified completeness. Metric provenance, source coverage and unverified game mappings must remain clearly labelled.

## Remaining V8 work before calling the rebuild complete

1. Run and fix a clean Windows 10/11 .NET 8 Release build and WPF startup.
2. Validate Glass Dark, Glass Light and Studio at 100%, 125% and 150% DPI and 1080p/1440p resolutions; check keyboard focus, contrast, clipping, tooltip text and the reduced-motion/system-animation path.
3. Complete the MVVM/service-layer separation; the legacy MainWindow code-behind is still substantial.
4. Replace the search hub and separate builders with the full general-purpose Advanced Search workspace: attribute/personality and performance-stat builders now support ALL/AND or ANY/OR matching, but a single cross-category filter library, nested logic groups, arbitrary core/contract filters, saved profiles and configurable result columns remain open.
5. Finish the save-import redesign: visible progress, cancellation, elapsed time and transaction rollback are implemented, and duplicate C# player/staff/club array copies have been removed. The Python bridge still materialises the table set and emits one large JSON file which the C# importer parses as a full JsonDocument. Replace that with bounded streaming/batches, staging validation, retryable lock handling and measured memory limits.
6. Profile search, profile open, navigation, import and database contention on 40k+ player / 28k+ club fixtures.
7. Implement the remaining roadmap areas only where source data and validation support them: development history, deeper tactical/match analysis, staff/club/contract coverage and explainable recruitment models.
8. Run regression tests for existing database compatibility, data provenance, identity joins, saved edits, import failures, search and SQLite contention.
9. Only then produce and label a signed/packaged V8 Windows release.

## Build and test

The Windows build entry point remains `Build-FM-Atlas-27.bat` in the application folder. It calls `Build-FM-Atlas-27-Logged.bat`, which restores and builds `FmAtlas.sln` in Release configuration and writes `build-logs/latest-build.log`.

Do not test live-memory features in Football Manager. They are blocked in this V8 source milestone. Begin with the offline build, application startup, appearance modes, navigation, Global Search, read-only import of a COPY of a career save, local editing and diagnostics.

**No successful Windows build or runtime test is asserted by this status document.**
