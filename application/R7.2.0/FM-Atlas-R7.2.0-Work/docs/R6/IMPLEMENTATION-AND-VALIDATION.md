# R6 implementation and validation report

## Implemented in this source package

- Renamed application metadata/version to 6.0.0 and updated the main window title.
- Restyled the application toward Aurora Frost: ice-blue surfaces, stronger navy text, blue accent, Segoe UI typography and WPF layout rounding.
- Replaced the bulk player card display path with a search-first DataGrid flow capped at 250 matches per page. The grid is data-bound and uses row/column virtualization, recycling and logical scrolling.
- Kept the pop-out Global Search / Advanced Search window with debounced as-you-type search, filters for supported player/staff/club criteria, bounded results and double-click record open.
- Kept per-player closable profile tabs, Ctrl+W close shortcut, cross-workspace session retention, and full-profile expansion.
- Rendered player attributes in category order and added a conspicuous missing-data banner rather than leaving a confusing wall of dashes.
- Corrected profile rendering to write into the selected tab's local StackPanel, avoiding cross-tab writes when concurrent profile selection completes asynchronously.
- Made the Staff page search-first and added an explicit explanation when the current data sources contain no staff rows.
- Improved club directory source merging, pagination and display of imported facts / linked player rows. Club squads are fetched only after a club is selected, not automatically for the first row.
- Moved heavy Squad Architect and Transfers retrieval/ranking to background work. Squad Architect now merges partial save/local player rows with the live identity index, and performs filter/sort/aggregate work on a worker thread.
- Added robust date-of-birth parsing for live records and SQLite indexes for common name, club, attribute and player-fact lookups.
- Applied `ConfigureAwait(false)` across common data-reader await paths in the SQLite service to avoid unnecessary UI-context continuation.
- Kept Atlas Assistant, local database editor, shortlists, import bridge and edit history.
- Added R6 build, publish and optional installer scripts; root entry points now route to R6. Older release notes are grouped under archive.
- Added a full beginner tutorial and a technical/data-provenance research note.

## Source review findings addressed

1. R5's player result code could create many individual UI controls for a large result set; R6 uses a data-bound DataGrid with a bounded page.
2. Search results were requested with a 100,000-row default in a broad database method; the default was reduced, and primary player workspace uses 250-row pages.
3. WPF virtualization alone would not have been sufficient: Microsoft states that data virtualization is separate and not built in. R6 applies SQL result limits as well.
4. Player profile rendering used a shared `_profilePanel` reference after asynchronous database reads; this could target the wrong tab if the selection changed. Rendering now uses the captured profile panel for that tab.
5. Staff was previously populated on opening the workspace; the new first-open state waits for a query.
6. Squad Architect and Transfers could retrieve and sort thousands of rows on navigation; their heavy retrieval/ranking is now run via background tasks.
7. One malformed live DOB could throw during a search; the mapping now uses `TryParse`.
8. `SaveClubUid` is ensured before its SQLite index is created in the migration path.
9. Club and staff search refreshes now catch and display errors instead of allowing a timer-triggered exception to escape into the WPF dispatcher.
10. Cross-source player merges now prefer the richest available record when the same name/club/age appears in both save/local and live indexes, rather than always letting the first local row hide richer live facts.
11. Player-result merge, deduplication and ordering now run on a worker thread too; deep paging no longer performs a large collection sort on the WPF dispatcher.

## What this report does not certify

- This source package was not compiled by Windows .NET 8/WPF in the preparation environment; the actual R6 binary is not verified here.
- It has not been run against your FM27 installation or your personal career save.
- No timing benchmark or memory profile has been captured on your PC.
- The live reader still has identity-level coverage and is not a verified full player attribute/CA/PA import.
- `fmsave` field meanings for FM27 remain provisional as noted by the adapter.
- Clubs and staff remain limited to what the imported records expose; absence of a field is not filled by inference.
- It is not full FMRTE or Genie Scout parity, and live writing/mass editing is not enabled.

## Required Windows acceptance test

1. Build using `Build-FMAtlas-R6.bat` and confirm 0 errors.
2. Launch and navigate Dashboard, Players, Recruitment, Clubs, Staff, Squad Architect, Transfers, Shortlists, Live Editor, Edit History, Atlas Assistant, Settings and Help.
3. Open Players without typing: no bulk player list should load and the UI should remain responsive.
4. Search multiple terms; page forward/back; open at least five profiles; switch tabs; close one with × and another with Ctrl+W.
5. Open Global Search with Ctrl+F; test Players, Staff, Clubs and Advanced filters; double-click a result.
6. Search a club; inspect facts and squad; double-click a squad player to open a player profile.
7. Run Load Game and confirm the count and source coverage labels; verify that CA/PA remain blank where the live source has no verified ability facts.
8. Import a COPY of an FM27 save if supported; compare a handful of CA/PA and attributes with the in-game screens and inspect the parser validation report.
9. Open Atlas Assistant, create/remove a shortlist record, and verify local edit history.
10. Close the app, reopen it, and verify the existing database and previous snapshot records remain.

Report any first exception and the full build output before relying on the release.
