# FM Atlas 27 — Obsidian Intelligence 7.2.0

FM Atlas is a Windows desktop scouting, career-intelligence, analytics and live-editing workbench for Football Manager 27. This 7.2.0 source checkpoint extends the previous R7.1.0 integration without deleting its history or replacing the existing local database design.

## Design direction

**Obsidian + Swiss Editorial**: charcoal surfaces (`#0D1117`, `#171F28`, `#202A35`), off-white typography, quiet grey secondary text, fine borders and electric-lime accent (`#C6F36B`). The existing desktop navigation and workspaces have been brought onto this palette. This is a significant theme pass, but final visual acceptance still requires running the WPF app at the user's Windows display scaling and resolution.

## Included in 7.2.0 source

- **Multi-condition Statistical Search Lab**: dynamically discovers numeric fields from retained FM career season statistics, career-history statistics, player match statistics and imported FM View Exporter CSVs. Fields are grouped into Goalkeeping, Attacking, Passing & Creation, Defending, Physical, Possession & Carrying, Discipline, General and Other metrics. Up to 60 AND conditions can be combined against the same player/stat row/period. Per-90 derivation is optional and requires a minimum-minute threshold.
- **Indexed statistics facts**: import retains each source row as lossless JSON and additionally indexes numeric statistic fields by snapshot, player UID, period, minutes, original field path and numeric value. Player profiles use the index first to avoid reparsing every match/season row on each profile open. Older snapshots without the index use a compatibility fallback.
- **Multi-attribute Player Search**: up to 60 simultaneous 1–20 minimum/maximum requirements across the 52-field FM player schema plus eight personality fields, where those fields are actually present in the imported source. Missing values never count as zero. Quick presets include Complete Striker, Ball Winner and Elite Goalkeeper.
- **Live FM identity/attribute reader**: matches exact Windows FM27 module fingerprints and separate Steam/Game Pass build profiles; reads player/club identities, CA/PA, positions and candidate attribute values. Live-imported values retain explicit provisional provenance.
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

1. Open **Tester & Logs** and run the checks.
2. Import a COPY of the FM27 career save. Review fmsave build coverage and reader warnings.
3. Compare a selection of CA/PA, attributes, personality and season statistics against the in-game UI. Import an FM View Exporter CSV to supplement the save reader.
4. Scan the existing graphics root and inspect player/club profiles for face/logo resolution.
5. Only after the exact Steam/Game Pass build profile matches and the source data checks pass, test one CA/PA edit and one visible attribute edit on an offline copied single-player career. Confirm in-game, press Continue, and test guarded Undo. Never use the only copy of a career.
6. Send `build-logs/latest-build.log` and the redacted Tester & Logs support ZIP if anything fails.

## What is not complete yet

This is an implementation checkpoint, not a claim that the entire FM Atlas vision is finished or that Windows compilation/gameplay testing has passed. The user-side build and FM27 validation gates remain open. Broad FMRTE-style arbitrary field search across all entities, complete staff/club/contract/transfer live editing, full Match Lab event/shot-map data, a comprehensive role/tactic solver, training/development/nation registration systems and a fully validated undervaluation model remain on the roadmap. See:

- `docs/R7.2.0-IMPLEMENTATION-STATUS.md`
- `docs/R7.2.0-ROADMAP-CHECKLIST.md`
- `docs/R7.2.0-USER-TESTING.md`
- `docs/R7.2.0-RESEARCH-NOTES.md`

## User data safety

The default local database remains `%LOCALAPPDATA%\FmAtlas\scouting.db`. Logs are written under `%LOCALAPPDATA%\FmAtlas\logs\`. The release ZIP does not include the user's database, game installation, graphics packs or career save. Save imports are versioned snapshots; the previous snapshots are not deleted by this update.
