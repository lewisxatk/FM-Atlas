# FM Atlas V8 — Atlas Liquid

FM Atlas is a Windows desktop scouting, career-intelligence, analytics, squad-planning and editing workbench for Football Manager 27. V8 keeps the R7.2.0 data and workspace foundation while rebuilding the application shell around the Atlas Liquid visual system. The source is still subject to a clean Windows Release build and user-side acceptance testing.

## V8 design direction — Monolith Glass

V8 uses a **Monolith Glass** visual language: graphite/charcoal surfaces, warm amber-orange accents, translucent raised cards, fine warm glass borders, controlled shadows and restrained motion. Three coordinated appearances remain available: **Glass Dark**, **Glass Light** and **Studio**. The first-run default is Glass Dark. The selected appearance is saved under `%LOCALAPPDATA%\\FMAtlas\\theme.txt`.

- A glass-style top action toolbar uses compact icon buttons with explanatory hover tooltips, including direct shortcuts to the human manager and current club.
- The side navigation is grouped by Home, Scouting, Squad & Analysis, Editing, and Data & Tools. It can collapse to an icon rail; hover descriptions remain available.
- Main workspace cards use translucent surfaces, warm glass borders, rounded corners and restrained shadows. Tables remain readable rather than being made fully transparent.
- Ctrl+K opens Global Search. A dedicated Advanced Search hub brings the existing player, attribute/personality and performance-stat builders together. Navigation remains clickable during workspace preparation and queues the most recent destination.
- Previously opened player profiles are restored after the Players workspace is displayed rather than delaying workspace construction.

The shared Monolith Glass tokens are implemented in source. Visual acceptance at Windows display scaling and common resolutions remains outstanding.

## Automatic live-career connection and safety

- Atlas polls for a running FM27 process and waits for an exact supported Steam/Game Pass module fingerprint plus the game's own GAME_UI save-load counter. It does not ask the user to browse to a save file to populate the live player directory.
- When a supported career has finished loading, Atlas attaches to that exact process/session and scans the profiled player, staff and club pointer arrays. The save-load counter is checked before and after the scan; a scan that overlaps a career switch is discarded.
- The staff reader uses the profile's person-block layout, identifies the human-manager record using name-decoding evidence, and imports the manager's club/nation context. The same records appear in the Players, Staff and Clubs workspaces.
- A new save-load generation triggers a fresh snapshot automatically. Reopening Atlas while a career is loaded also triggers a new connection. A toolbar action remains available for manual refresh.
- The live scan is read-only. CA/PA and live attribute values are retained as provisional source-derived fields until compared with the in-game UI; this is not a claim that every memory field is certified.
- Live Apply and Undo through game memory remain blocked. The existing guarded writer code is retained but is not callable through the V8 editor UI.
- Offline career-save import is now optional for additional career history, match/season statistics, contracts, and other tables not exposed by the live identity scan. Select a COPY of the career save; the fmsave reader is read-only.

## Retained R7.2.0 feature foundation

V8 preserves the current player, staff and club records; search workspaces; indexed season/stat facts; multi-attribute player search; statistics search; Value Finder; Role Fit Lab; squad and tactical tools; shortlists; graphics resolver; local editing; diagnostics; and the save bridge. This does not imply that every roadmap feature is complete or has passed testing.

## Included feature foundation

- **Multi-condition Statistical Search Lab**: dynamically discovers numeric fields from retained FM career season statistics, career-history statistics, player match statistics and imported FM View Exporter CSVs. Fields are grouped into Goalkeeping, Attacking, Passing & Creation, Defending, Physical, Possession & Carrying, Discipline, General and Other metrics. Up to 60 AND conditions can be combined against the same player/stat row/period. Per-90 derivation is optional and requires a minimum-minute threshold.
- **Indexed statistics facts**: import retains each source row as lossless JSON and additionally indexes numeric statistic fields by snapshot, player UID, period, minutes, original field path and numeric value. Player profiles use the index first to avoid reparsing every match/season row on each profile open. Older snapshots without the index use a compatibility fallback.
- **Multi-attribute Player Search**: up to 60 simultaneous 1–20 minimum/maximum requirements across the 52-field FM player schema plus eight personality fields, where those fields are actually present in the imported source. Missing values never count as zero. Quick presets include Complete Striker, Ball Winner and Elite Goalkeeper.
- **Live FM career connection**: detects the currently loaded supported FM27 Steam/Game Pass career through its exact module fingerprint and GAME_UI save-load counter; reconnects after a save switch; reads player, staff and club identities; locates the human manager and stores their club/nation context. Live CA/PA and player attribute facts are surfaced with provisional evidence labels.
- **Narrow guarded live writer**: live writes are no longer described as impossible or local-only. The source contains a separate Windows writer for CA, PA and 47 visible standard player attributes. It is OFF until enabled by the user and requires an exact module profile, career-load generation check, UID/name identity check, old-byte preview check, value validation, read-back and guarded inverse Undo. It rechecks the career generation before each field write. Five hidden-attribute offsets are explicitly blocked because the source layout describes them as inherited leads rather than independently witnessed FM27 mappings. Personality, position, staff, club, contract, transfer and other structural edits remain blocked.
- **Graphics Resolver**: scans the user's existing Football Manager 27 graphics folders and `config.xml` files, resolves player/staff/club/competition/nation IDs to local images, constrains paths to the chosen graphics root and caches the results. No facepacks or logos are bundled. Player and club profiles display resolved images where mappings exist.
- **Role Fit Lab**: the existing 23 transparent weighted In Possession / Out of Possession role profiles remain available. They are Atlas estimates, not Sports Interactive's internal role-rating formula.
- **Tester & Logs**: quick environment checks, exact build-profile detection, rolling app logs, copyable diagnostics and a support ZIP that excludes the career save and Atlas SQLite database by default.
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

1. Start FM27, load a career and leave Atlas open. Confirm the connection banner identifies the supported game build and completes the live scan without selecting a save file.
2. Confirm your human-manager name, club and nation, then use the manager/club toolbar shortcuts.
3. Search for a player using Global Search, the Players workspace and Advanced Search. Confirm CA/PA and the available 1–20 attribute profile values against the FM UI. Check that signed negative PA ranges remain visible where supplied.
4. Search the Staff directory, open the human-manager profile and confirm CA/PA, role, club and nation. Check that missing fields stay blank rather than being guessed.
5. Load a second career in FM27 while Atlas remains open. Confirm the save-load counter causes a fresh snapshot and that the old career is not mixed into the new one.
6. Use offline save import only when you need extended history, match/season stats, contracts or data not included in the live identity scan. Select a COPY of the save.
7. Scan the existing graphics root and inspect player/club profiles for face/logo resolution. Test the Glass Dark layout at 100%, 125% and 150% Windows scaling.
8. Live memory editing remains disabled. Do not attempt to enable Apply/Undo until the exact field mappings and rollback workflow are independently validated on a disposable offline career.
9. Send `build-logs/latest-build.log` and the redacted Tester & Logs support ZIP if anything fails.

## What is not complete yet

This is an integrated V8 source milestone, not a claim that the entire FM Atlas vision is finished or that the latest live-career changes have passed Windows compilation or in-game acceptance. The universal player Advanced Search, multi-attribute builder, CA/PA filtering, paged people directory, player comparison and similar-player foundations are retained. Full field coverage for every player/staff/club attribute, every in-game season and match statistic, staff/club/contract/transfer data, full Match Lab event/shot-map data, comprehensive role/tactic solver, training/development/nation registration systems and a fully validated undervaluation model remain unfinished. The user-side build and FM27 validation gates remain open. See:

- `docs/architecture/FM-ATLAS-V8-IMPLEMENTATION-STATUS.md`
- `docs/R7.2.0-IMPLEMENTATION-STATUS.md`
- `docs/R7.2.0-ROADMAP-CHECKLIST.md`
- `docs/R7.2.0-USER-TESTING.md`
- `docs/R7.2.0-RESEARCH-NOTES.md`

## User data safety

The default local database remains `%LOCALAPPDATA%\FmAtlas\scouting.db`. Logs are written under `%LOCALAPPDATA%\FmAtlas\logs\`. The release ZIP does not include the user's database, game installation, graphics packs or career save. Save imports are versioned snapshots; the previous snapshots are not deleted by this update.
