# FM Atlas V11 — Monolith Glass, Football Intelligence & Live Editor

> **V11 validation status:** the live editor diagnostics and recovery update is committed to `feature/v9-all-roadmap`. Its Windows Release restore/build, complete xUnit suite, Python adapter tests and self-contained x64 publish are running in [latest branch-head workflow](https://github.com/lewisxatk/FM-Atlas/actions/workflows/fm-atlas-v8-windows.yml). Do not distribute this source revision until that exact workflow succeeds.
>
> **Live-write safety:** player/staff/club live editing is explicitly opt-in for the current Atlas session and starts OFF. Apply and Undo re-check the session gate, exact FM27 build, save generation, entity identity, staged bytes and read-back. Only the whitelisted fields are exposed; unsupported structures remain blocked. Use a disposable offline career copy and keep a separate backup.

FM Atlas is a Windows desktop scouting, career-intelligence, analytics, squad-planning and editing workbench for Football Manager 27. V11 extends the retained R7.2.0 data and workspace foundation with a guarded live editor, read-only module diagnostics and privacy-aware support exports. A successful CI build does not replace acceptance against the user's actual FM27 installation and disposable career.

## V11 design direction — Monolith Glass

V10 uses a **Monolith Glass** visual language: graphite/charcoal surfaces, warm amber-orange accents, translucent raised cards, fine warm glass borders, controlled shadows and restrained motion. Three coordinated appearances remain available: **Glass Dark**, **Glass Light** and **Studio**. The first-run default is Monolith Glass; an existing saved appearance is preserved. The selected appearance is saved under `%LOCALAPPDATA%\\FMAtlas\\theme.txt`.

- A glass-style top action toolbar uses compact icon buttons with explanatory hover tooltips, including direct shortcuts to the human manager and current club.
- The toolbar Activity Centre reports the active live scan or offline save import, current stage/status text, elapsed time, cancellation availability and the most recent task summary.
- The side navigation is grouped by Home, Scouting, Squad & Analysis, Editing, and Data & Tools. It can collapse to an icon rail; hover descriptions remain available.
- Main workspace cards use translucent surfaces, warm glass borders, rounded corners and restrained shadows. Tables remain readable rather than being made fully transparent.
- Shared workspace controls use theme-aware glass surfaces for text inputs, selectors, tabs, progress indicators and data-grid headers/rows. Selected and hovered rows use the warm accent without making player data translucent.
- The Dashboard hero uses the shared glass card and metric cards reflow across desktop, medium and narrow workspace widths. The minimum window size is 1000×680; display-scaling acceptance is still pending.
- Ctrl+K opens Global Search. A dedicated Advanced Search hub brings the existing player, attribute/personality and performance-stat builders together. Navigation remains clickable during workspace preparation and queues the most recent destination.
- Previously opened player profiles are restored after the Players workspace is displayed rather than delaying workspace construction.

The shared Monolith Glass tokens are implemented in source. Visual acceptance at Windows display scaling and common resolutions remains outstanding.

## Automatic live-career connection and safety

- Atlas polls for a running FM27 process and waits for an exact supported Steam/Game Pass module fingerprint plus the game's own GAME_UI save-load counter. It does not ask the user to browse to a save file to populate the live player directory.
- When a supported career has finished loading, Atlas attaches to that exact process/session and scans the profiled player, staff and club pointer arrays. The save-load counter is checked before and after the scan; a scan that overlaps a career switch is discarded.
- The staff reader uses the profile's person-block layout, identifies the human-manager record using name-decoding evidence, and imports the manager's club/nation context. The same records appear in the Players, Staff and Clubs workspaces.
- The monitor checks process/build/save-generation changes every three seconds and starts the profile-bounded read-only scan for a supported loaded career. Each scan has a two-minute deadline, progress updates, toolbar cancellation and save-generation cancellation; no partial snapshot is committed.
- The live scan is read-only. It reads player CA/PA, the mapped 52 standard player attributes, eight personality ratings, 15 position ratings, nationality, staff CA/PA and the human-manager context. Player-coach identity fields use the separately detected +0xF8 block. These layout-matched values remain provisional until compared with the in-game UI; this is not a claim that every memory field is certified.
- The V11 profile-level Live Editor can stage and apply explicitly whitelisted player, staff and club scalar fields. Live FM Editing starts OFF and must be explicitly armed for the current Atlas session; the service re-checks the gate on Apply and Undo. Every batch is identity/session/value preflighted, read back and guarded for rollback. The legacy player-only preview remains read-only.
- Offline career-save import is now optional for additional career history, match/season statistics, contracts, and other tables not exposed by the live identity scan. Select a COPY of the career save; the fmsave reader is read-only.

## Retained R7.2.0 feature foundation

V8 preserves the current player, staff and club records; search workspaces; indexed season/stat facts; multi-attribute player search; statistics search; Value Finder; Role Fit Lab; squad and tactical tools; shortlists; graphics resolver; local editing; diagnostics; and the save bridge. This does not imply that every roadmap feature is complete or has passed testing.

## Included feature foundation

- **Multi-condition Statistical Search Lab**: dynamically discovers numeric fields from retained FM career season statistics, career-history statistics, player match statistics and imported FM View Exporter CSVs. Fields are grouped into Goalkeeping, Attacking, Passing & Creation, Defending, Physical, Possession & Carrying, Discipline, General and Other metrics. Up to 60 AND conditions can be combined against the same player/stat row/period. Per-90 derivation is optional and requires a minimum-minute threshold.
- **Indexed statistics facts**: import retains each source row as lossless JSON and additionally indexes numeric statistic fields by snapshot, player UID, period, minutes, original field path and numeric value. Player profiles use the index first to avoid reparsing every match/season row on each profile open. Older snapshots without the index use a compatibility fallback.
- **Multi-attribute Player Search**: up to 60 simultaneous 1–20 minimum/maximum requirements across the 52-field FM player schema plus eight personality fields, where those fields are actually present in the imported source. Missing values never count as zero. Quick presets include Complete Striker, Ball Winner and Elite Goalkeeper.
- **FMRTE/FMSuperScout-style Players finder**: fast name/club/nation/UID lookup, paged results, direct min/max CA and PA filters for both live and offline players (including signed negative PA ranges), filter-only searches and matching result counts. Select a player to open the full available profile with current attributes, personality, positions, evidence and optional historical performance facts.
- **Career/session detection**: automatically identifies supported FM27 Steam/Game Pass processes and GAME_UI save-generation changes, then performs a bounded, cancellable read-only player/staff/club entity-array scan. This is the normal live player-directory connection; offline save import is optional and is reserved for historical/match/season statistics and other data outside the live reader.
- **Live editing**: V11 exposes a guarded, session-only live editor for the current player/staff/club scalar whitelist plus the separately implemented Tactical Effectiveness pathway. It is not complete FMRTE parity; unsupported pointer/list and world-editor structures remain unavailable. Field-by-field acceptance on a disposable FM27 career is still required.
- **Graphics Resolver**: scans the user's existing Football Manager 27 graphics folders and `config.xml` files, resolves player/staff/club/competition/nation IDs to local images, constrains paths to the chosen graphics root and caches the results. No facepacks or logos are bundled. Player and club profiles display resolved images where mappings exist.
- **Role Fit Lab**: the existing 23 transparent weighted In Possession / Out of Possession role profiles remain available. They are Atlas estimates, not Sports Interactive's internal role-rating formula.
- **Tester & Logs**: quick environment checks for the database, log storage, save bridge, FM process, build profile, legacy read-only path, live session gate and field catalogues; a read-only FM module/PE probe; privacy-redacted support reports; rolling stage/apply/undo logs; and a support ZIP that excludes the career save and Atlas SQLite database by default.
- **Preservation**: earlier R7.1.0 implementation documents, historical R3–R6 material, local DB persistence and existing user data paths are retained.

## Research sources used as technical references

- [FMLibreEditor](https://github.com/tozmoz/FMLibreEditor) — public FM27 live-editing research, Steam/Game Pass Windows build profiles, read/write checks and UI workflows. GPL-3.0; Atlas does not copy its implementation.
- [barak14/fm-editor](https://github.com/barak14/fm-editor) — FM27 memory layout investigation and field witnesses. GPL-3.0; its notes distinguish verified mappings from inherited/unverified offsets.
- [FM View Exporter](https://github.com/ElectroHugin/FM-View-Exporter) — in-game player-list CSV/HTML export and FM27 column catalogue. GPL-3.0; CSV import in Atlas is an independent implementation.
- [fmsave](https://github.com/rhiever/fmsave) — read-only FM26/FM27 save reader with structured records, coverage checks, season statistics and match history. MIT.

The licenses above apply to those projects. Their ideas and public game-layout observations inform the architecture; source implementation has not been copied into Atlas. Read `THIRD-PARTY-NOTICES.md` and `docs/R7.2.0-RESEARCH-NOTES.md` before redistributing.

## Build on Windows

1. Extract this ZIP to a new writable folder. Keep previous FM Atlas folders and ZIPs intact.
2. Install the .NET 8 SDK for Windows 10/11 x64.
3. Run `Build-FM-Atlas-27.bat`.
4. Inspect `build-logs/latest-build.log`. The script offers to copy the full log to the clipboard.
5. Only launch `src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe` after a successful build.
6. For a self-contained publish, run `Publish-FM-Atlas-27.bat`. For an Inno Setup installer, run `Build-Installer-FM-Atlas-27.bat` after publishing.

## First test sequence

1. Start FM27 and load a career, then leave Atlas open. Confirm it detects the supported process/build/session and automatically reads the player/staff/club arrays; no save-file import is needed for the live player/staff directory.
2. Confirm your human-manager name, club and nation, then use the manager/club toolbar shortcuts. Check player nationality, CA/PA, personality and position suitability on the Players profile.
3. Search for a player using Global Search, the Players workspace and Advanced Search. Confirm CA/PA, nationality, primary position, the 52 mapped standard attributes, eight personality ratings and 15 position ratings against the FM UI. Check that signed negative PA ranges remain visible, provisional values are clearly labelled and can be filtered without being presented as verified.
4. Search the Staff directory, open the human-manager profile and confirm CA/PA, role, club and nation. Check that missing fields stay blank rather than being guessed.
5. Load a second career in FM27 while Atlas remains open. Confirm the save-load counter causes a fresh live scan and that the old career is not mixed into the new one; if a read is interrupted during the switch, Atlas must discard it and reconnect.
6. Use offline save import only when you need extended history, match/season stats, contracts or data not included in the live identity scan. Select a COPY of the save.
7. Scan the existing graphics root and inspect player/club profiles for face/logo resolution. Test the Monolith Glass layout at 100%, 125% and 150% Windows scaling.
8. Live memory editing remains disabled. Do not attempt to enable Apply/Undo until the exact field mappings and rollback workflow are independently validated on a disposable offline career.
9. Send `build-logs/latest-build.log` and the redacted Tester & Logs support ZIP if anything fails.

## What is not complete yet

This is an V10 pre-testing integration milestone, not a claim that the entire FM Atlas vision is finished or that the latest live-career changes have passed Windows compilation or in-game acceptance. The universal player Advanced Search, multi-attribute builder, CA/PA filtering, paged people directory, player comparison and similar-player foundations are retained. Full field coverage for every player/staff/club attribute, every in-game season and match statistic, staff/club/contract/transfer data, full Match Lab event/shot-map data, comprehensive role/tactic solver, training/development/nation registration systems and a fully validated undervaluation model remain unfinished. The user-side build and FM27 validation gates remain open. See:

- `docs/architecture/FM-ATLAS-V8-IMPLEMENTATION-STATUS.md`
- `docs/R7.2.0-IMPLEMENTATION-STATUS.md`
- `docs/R7.2.0-ROADMAP-CHECKLIST.md`
- `docs/R7.2.0-USER-TESTING.md`
- `docs/R7.2.0-RESEARCH-NOTES.md`

## User data safety

The default local database remains `%LOCALAPPDATA%\FmAtlas\scouting.db`. Logs are written under `%LOCALAPPDATA%\FmAtlas\logs\`. The release ZIP does not include the user's database, game installation, graphics packs or career save. Save imports are versioned snapshots; the previous snapshots are not deleted by this update.
