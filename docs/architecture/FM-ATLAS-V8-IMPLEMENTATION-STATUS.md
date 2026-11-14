# FM Atlas V8 — Integrated implementation status

**Branch:** `rebuild/v8-full-product-rebuild`  
**Product identity:** FM Atlas V8 · Monolith Glass  
**Status:** integrated source milestone with a new automatic live-career connection and staff/manager extraction committed after the last recorded Windows build. Earlier Windows CI runs passed for earlier V8 revisions (#79, #157 and #168), but those runs predate the latest live-monitor, staff importer, database, Monolith Glass and test changes. A fresh Windows Release build/test is required for this revision. The WPF application has not yet been launched on the user's PC and Football Manager acceptance tests have not yet run.

## Implemented in this V8 source pass

| Area | Current source state | Notes |
|---|---|---|
| Application identity | V8.0.0 metadata | Assembly, file and product metadata updated. Historical source-folder names are retained to avoid breaking existing paths. |
| Monolith Glass appearance | Source updated; visual acceptance pending | Graphite/charcoal surfaces, warm amber-orange accents, translucent raised cards, warm glass borders and restrained shadows. Glass Dark, Glass Light and Studio modes persist in `%LOCALAPPDATA%\\FMAtlas\\theme.txt`. |
| Top command toolbar | Source updated | Compact icon controls for Global Search, optional offline save import, Save Atlas, Shortlists, Analytics, automatic live career connection/refresh, My Manager, My Club, theme switching and diagnostics. |
| Hover help | Source implemented | Toolbar buttons and navigation items have explanatory tooltips and delayed hover display. |
| Side navigation | Source implemented | Grouped sections, active item treatment, collapsible icon rail and hover descriptions. |
| Button feedback | Source implemented | Rounded controls include hover, pressed, keyboard-focus and disabled visual states. |
| Workspace motion | Source implemented | 150 ms workspace fade-in is conditional on the Windows system animation setting; no continuous decorative animation. |
| Keyboard search | Source implemented | Ctrl+K opens the existing Global Search window; Ctrl+F remains supported. |
| Universal Advanced Search | Source implemented; Release build passed | One parameterized query combines whitelisted core player fields, PlayerAttributes and latest-save FmsavePlayerFacts with up to 60 typed ALL/AND or ANY/OR conditions. The UI includes dynamic categories/fields, local save/replace/load/delete profiles, visible-column selections, stable 250-row paging and Cancel search propagated through a CancellationToken to SQLite. Multi-select Add-to-Shortlist is transactional and reports newly inserted versus already-present players accurately. Performance remains a separate player-period search; nested logic groups, multi-sort, comparison, shortlist removal/notes and dedicated source-coverage badges remain open. |
| Navigation responsiveness | Improved in source | Navigation buttons stay enabled while workspace construction runs; latest requested destination is queued. |
| Player workspace activation | Improved in source | Previously open profiles are restored after the workspace is displayed rather than awaited during workspace construction. |
| Automatic live-career connection | Source implemented; fresh Windows build and FM27 runtime acceptance pending | A three-second read-only monitor matches an exact Steam/Game Pass module fingerprint and checks the GAME_UI save-load counter. It waits for a loaded career, attaches to that exact process/session, then scans player, staff and club arrays. The counter is checked before and after the scan; a career switch discards the partial read. Reopening Atlas while a career is loaded triggers a fresh snapshot. |
| Human manager and staff context | Source implemented; exact live layout needs user-side confirmation | Staff person-block shift is selected from FM27 name-decoding evidence and cached by class. The human-manager shift is identified separately; CA/PA, age, nation, club and role candidates are stored in LiveStaffFacts. Dashboard status and toolbar shortcuts expose the current manager, club and nation when one manager is uniquely identified. |\n| People search and profiles | Existing Advanced Search retained; live CA/PA integration added | Player records are populated into the existing Players/Advanced Search path, with CA/PA and mapped 1–20 fields. Staff directory includes CA/PA, role, club, age, nation and human-manager status; selecting a record opens its fact profile. Full historical/match statistics and data not exposed by the live reader still require the optional offline save bridge or FM View CSV. |\n| Live-memory edit safety | Blocked by default in V8 UI | CA/PA and visible-attribute live Apply handlers remain gated. Local Atlas edits and read-only live connection are separate. |
| Save-import progress, streaming and cancellation | Source implemented and integration tested | The Python bridge writes per-table NDJSON files incrementally, normalises player/staff/club rows and writes a small manifest (a non-Polars fallback may materialise one table). C# reads one JSON row at a time in the existing SQLite transaction; cancellation/failure rolls back. Every physical table’s row count is reconciled with the manifest before commit; skipped records are distinguished and warnings persist with the import run. The adapter refuses an existing output folder. Python adapter tests and four Windows C# integration tests passed in Actions run #168. Large real-save memory/throughput, cancellation at every table, and WAL/contention tests remain open. |
| SQLite write coordination | Source implemented | Mutating/import/undo/checkpoint operations share a process-wide writer gate and the connection has a 30-second busy timeout with foreign keys enabled. Lock stress tests remain open. |
| NDJSON career-save bundle | Source implemented; integration build pending | The local bridge now emits one manifest plus per-table NDJSON files and normalises player/staff/club rows as a stream. C# reads one line into one JsonDocument at a time and indexes it inside the transaction. The Python adapter syntax check and fake-save regression test passed in Windows Actions run #141. fmsave may still materialise an individual table before iteration; peak memory on a real large save has not yet been measured. |
| Existing R7.2 functionality | Retained in source | Player/staff/club discovery, indexed statistics, multi-attribute and statistical search, Value Finder, Role Fit, squad/tactical workspaces, shortlists, graphics resolution, local edits, logs and diagnostics remain in the source tree. |

## Build verification (historical runs; latest live-sync commits pending a fresh run)

- First hosted Windows Release attempt found seven compile errors: four Advanced Search `RoutedEventHandler` callback signatures and three ambiguous `Path` references caused by the WPF Shapes import. The event handlers and explicit `System.IO.Path` alias were fixed in `80b4160d` and `b35432d8`.
- **Successful run #79:** https://github.com/lewisxatk/FM-Atlas/actions/runs/43534383967 — .NET 8 install, solution restore, Release compilation, self-contained Windows x64 publish and artifact upload all passed for commit `18d32e2d5fbe0f1549da6d9d55391414e0b05bdf`.
- **Successful integrated run #157:** https://github.com/lewisxatk/FM-Atlas/actions/runs/43536598003 — Python adapter regression, .NET 8 restore, Release compilation, self-contained x64 publish and artifact upload all passed for commit `77ec4364ec7504dbf36e55da7693c00440cc8d50`. Artifact ID `14090820119`; extracted product ZIP SHA-256 `2dc76b11ede85de9f28debc61322374292bfbaf88b272b690a12e44a3bff7eaf`.
- **Successful full V8 build and import integration run #168:** https://github.com/lewisxatk/FM-Atlas/actions/runs/43537353738 — Python adapter syntax/regression tests, .NET 8 restore, Release compilation, all four C# NDJSON import tests, self-contained Windows x64 publish and artifact upload passed for commit `e8cbd5da25d4543a684f45095dd877fde3533a3a`. Artifact ID `14090511809`; extracted product ZIP SHA-256 `775bb48f8e61a0080f379075a07732e01eec21e4fe32e746d5e4f57602753c98`. Non-fatal legacy warnings remain (unused old search fields, unreachable live-memory branches, and one unawaited profile-restoration task).
- Later source commits add output-folder protection, truthful shortlist counts, and strict NDJSON count reconciliation (`198ae45d` through `ef1c87b5`). These later code changes still need a fresh build/test run; run #157 predates them.
- **Advanced Search compile run #101:** https://github.com/lewisxatk/FM-Atlas/actions/runs/43535280229 — the newer source revision `37380294103c2609fa376e24c0fa3be9d50932ac` passed .NET 8 restore and Release compilation with the unified search UI and DB query. Its publish step was cancelled by a later workflow run; no artifact is claimed for that revision.
- Artifact: `FM-Atlas-V8-win-x64` (GitHub Actions artifact ID `14088498868`, 68,725,071 bytes including the artifact wrapper). The extracted product ZIP contains `FmAtlas.exe` (x64 PE), `tools/Export-FMAtlas-Fmsave.py`, `Setup-FMAtlas-Save-Bridge.ps1`, and a PDB. Product ZIP SHA-256: `4a16f815150e41855b2b1d5dba884c6878ac5a69f44b8ae44ebea8570b2798e2`.
- This verifies compile and package only. Startup, visual layout, import, runtime database behaviour and FM performance have not been tested on a real Windows PC; V8 is not release-ready yet.

## Safety policy

The V8 application must remain useful without Football Manager running; the local SQLite database and optional offline import remain available.

The live connection is read-only. It uses exact storefront module fingerprints and the GAME_UI save-load generation counter to avoid attaching to the wrong FM process or mixing two careers. The bounded profile-driven arrays are read sequentially. The importer checks the save-load counter before and after scanning and refuses to commit a snapshot if the career changes during the read. The new monitor/import path has not yet been performance-tested on the user's real PC; the first acceptance pass must watch FM responsiveness and check the redacted log.

The module profile match is not field-level certification. Player/staff CA/PA, selected identity values and player attributes remain provisional until compared with the in-game UI. Missing or unsupported fields must remain blank. Full historical/match-stat coverage depends on the optional offline read-only save reader or user-exported CSVs.

Live Apply remains blocked even though the guarded writer code is retained. Re-enable it only after exact Steam/Game Pass field mappings are independently validated against the in-game UI and disposable offline save copies, plus read-back/undo regression tests.

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

1. Run a fresh Windows 10/11 .NET 8 restore, Release build, xUnit suite and self-contained publish for the commits that introduced the live-career monitor, live staff/manager extraction and Monolith Glass theme. Then verify WPF startup on a real Windows PC.
2. Validate Monolith Glass at 100%, 125% and 150% DPI and 1080p/1440p resolutions; check keyboard focus, contrast, clipping, tooltip text and the reduced-motion/system-animation path.
3. Complete the MVVM/service-layer separation; the legacy MainWindow code-behind is still substantial.
4. Continue Advanced Search beyond the new universal cross-category builder: expand verified field coverage, add nested logic groups, multi-sort, multi-select/compare/shortlist actions and dedicated source-coverage badges. Saved profiles, configurable visible columns, cancellation and stable 250-row paging are now implemented in source.
5. Validate the new per-table NDJSON import path on real large saves. The adapter and C# importer now stream table rows one at a time, with a manifest and a single transaction; add manifest-to-import count reconciliation, relationship checks, malformed-row and cancellation rollback tests, lock retry policy, and measured memory/throughput benchmarks.
6. Profile automatic live connection, 40k+ player / 28k+ club / staff scanning, search, profile open, navigation, import and database contention. Confirm a loaded career is detected once, a second save causes exactly one refresh, and no mixed-session rows are committed.
7. Implement the remaining roadmap areas only where source data and validation support them: development history, deeper tactical/match analysis, staff/club/contract coverage and explainable recruitment models.
8. Run regression tests for existing database compatibility, data provenance, identity joins, saved edits, import failures, search and SQLite contention.
9. Only then produce and label a signed/packaged V8 Windows release.

## Build and test

- Python adapter syntax and mocked NDJSON bundle regression test passed on Windows Actions run #141 (commit `cf9559cfe91f278373ff56f012d8294155dcc396`).
- The NDJSON integration and subsequent saved-profile, paging, cancellation and prepared-command revisions are being validated in fresh Windows workflows; no build/package result is claimed for the newest revision until its Release and publish steps complete.
- A previous run #126 failed on a raw-string indentation issue in a then-current `ScoutDatabase.cs` revision; the offending SQL lines were corrected in subsequent source updates. The current run is the verification of the corrected branch state.


The Windows build entry point remains `Build-FM-Atlas-27.bat` in the application folder. It calls `Build-FM-Atlas-27-Logged.bat`, which restores and builds `FmAtlas.sln` in Release configuration and writes `build-logs/latest-build.log`.

Read-only automatic live-career detection is now part of this V8 source milestone and should be tested only on a backed-up career while monitoring FM responsiveness. Keep live Apply/Undo disabled. Begin with the Windows build, app startup, Monolith Glass appearance, automatic career connection, manager/club/nation shortcuts, Players/Advanced Search CA/PA and attribute profiles, Staff profiles, save-switch detection and diagnostics. Use offline read-only import only for extended history, match/season statistics and other data not present in the live scan.

**No successful Windows build or runtime test is asserted by this status document.**
