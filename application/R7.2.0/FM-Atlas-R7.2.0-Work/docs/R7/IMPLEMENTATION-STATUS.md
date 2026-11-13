# FM Atlas 27 — Implementation and Verification Status

## Implemented in the 7.1.0 source update

- Retained the R7 Aurora desktop shell, global search, Players, Staff, Clubs, Recruitment, Squad Architect, Transfers, Tactical Lab, Shortlists, profile tabs, import bridge and existing diagnostics.
- Added **Role Fit Lab** with 23 transparent In Possession / Out of Possession role profiles. The model uses weighted source-provided 1–20 attributes, rejects candidates below 50% attribute coverage and exports ranked CSV results.
- Expanded **Analytics** with derived season per-90 options when the retained season stats/history row provides both a numeric metric and a recognised playing-minutes value. The minimum sample defaults to 450 minutes and is configurable.
- Added **Editor Studio** with a session-only ON/OFF gate, OFF by default on every launch. It supports Atlas-local CA/PA and player attribute changes.
- Expanded **Mass Edit** to Set, Add and percentage Scale. Add/Scale skip missing values, each player edit receives an audit row, and a safety cap is applied.
- Hardened **Undo** with a current-value comparison. Stale undo is refused instead of overwriting a newer value.
- Added **Tester & Logs**: local database/log/Python bridge/process/snapshot checks, redacted diagnostics copy, recent log copy, and support ZIP export.
- Added rolling application logs, unhandled exception capture, version 7.1.0 metadata and updated build/installer version metadata.
- Added a logged Windows build script with `build-logs\latest-build.log` and clipboard-copy prompt.
- Included the updated Master Specification and Extended Research & Backlog in `docs/R7/`.

## Not implemented or not verified — do not assume otherwise

- **Live FM editing is not implemented.** The live process reader remains read-only and only links fields whose current mapping is validated. Editor Studio and Mass Edit change the FM Atlas SQLite database only; they do not modify a career or running game.
- Steam and Game Pass need independently validated process/build fingerprints and separate tested adapters. The current diagnostics report does not claim reliable store identification.
- Save imports depend on the installed `fmsave` reader and supported save/build coverage. The extra save tables are retained losslessly as JSON, but not all fields have typed, indexed, curated UI panels yet.
- Analytics can only discover fields actually retained by the save reader. Per-90 calculations are conditional on minutes being available and must be compared with FM27 Data Hub before use. Match-stat per-90 is not yet implemented as a separate workflow.
- Role Fit Lab is an original Atlas estimate, not SI's formula, tactical role star rating or positional familiarity. Its weights and role list require validation against in-game scouting. It does not yet model player suitability, traits, foot, role duty/instructions or tactical context.
- Full competition registration, staff development, training planner, match timeline/shot map, opponent intelligence, club-finance planner, graphics/config manager, Pre-Game Editor workflow and optional mod adapters remain research/backlog items rather than finished integrated modules.
- User-requested mass live editing, transfer/loan writes, club/staff writes, undo against a live game, save-file writing and game-state freezer features remain disabled.
- No third-party GPL implementation was copied into this source. Research findings are documented separately.

## Data and safety rules

- Use a COPY of a career save for all imports and testing.
- Keep the Editor Studio toggle OFF unless you specifically intend to edit the local Atlas copy.
- Do not enable arbitrary live-memory write offsets. Any later live editor needs store/build-specific fingerprints, a field capability matrix, pre-write identity/current-value checks, read-back verification, guarded undo and a test matrix for both Steam and Game Pass.
- A diagnostics support ZIP excludes the source career save and Atlas SQLite database. The report is redacted by default; review it before sharing.

## Validation status

- Source-level review and static checks were performed during preparation.
- A Windows .NET 8 WPF compile, app startup, real FM27 save import, Steam/Game Pass process test and live-game comparison still need to be run on your PC. The preparation container does not have the .NET SDK/MSBuild, so it cannot produce or truthfully certify a Windows EXE.
- Run `Build-FM-Atlas-27.bat` and send `build-logs\latest-build.log` if anything fails. After launch, open **Tester & Logs**, run quick checks and export the safe diagnostics support ZIP if the app misbehaves.
