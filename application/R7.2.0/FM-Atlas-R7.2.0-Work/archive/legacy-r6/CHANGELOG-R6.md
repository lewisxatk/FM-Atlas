# FM Atlas CoachPro R6 — Aurora Workbench

## R6.0.0 source upgrade
- Search-first, bounded, virtualized player workspace.
- Global search pop-out and advanced filter controls retained and refined.
- Closable player profile tabs and full-width profile mode.
- Ordered player attribute profile layout and explicit attribute-coverage messaging.
- Combined live/save club directory with paged access and squad facts where available.
- Staff search-first behaviour and no-data messaging.
- Background loading for heavier Squad Architect and Transfers work.
- Safer async tab rendering and defensive DOB parsing.
- Aurora Frost typography, colour contrast and spacing refinements.
- R6 build/publish/installer scripts, tutorial and implementation report.

This is a source upgrade pending Windows build and real FM27 save validation. See docs/R6/IMPLEMENTATION-AND-VALIDATION.md.


### R6.0.0 source-hardening pass

- Moved the remaining player/club/staff database searches and profile fact reads onto worker tasks so Microsoft.Data.Sqlite synchronous I/O does not occupy the WPF dispatcher.
- Added graceful error states to club and staff search refreshes, including debounced searches.
- Global Search now has a 420 ms as-you-type debounce and reruns the latest query after an older in-flight request completes.
- Club workspace no longer auto-opens the first club and immediately loads its squad; select a club to load its facts and linked players.
- Expanded advanced player filters with league and left/right foot strength ranges.
- Squad Architect now combines save/local rows with the live identity index, so a 1,400-player save snapshot no longer hides the 40K live identity records. Filtering/sorting/age/CA/PA/position summaries run off the UI dispatcher.
- Inspected the public FM27 open-source `fm-editor` project; documented GPL-3.0 and per-build fingerprint/storefront limitations rather than copying unverified offsets into Atlas.

- Cross-source player deduplication now prefers the richest row so sparse save records do not mask richer live facts.
