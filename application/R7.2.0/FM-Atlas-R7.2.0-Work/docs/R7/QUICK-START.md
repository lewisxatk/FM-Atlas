# FM Atlas 27 — Quick Start (7.1.0)

## Build and launch

1. Extract the complete ZIP to a new writable folder. Keep your previous R7 folder as a backup.
2. Install the .NET 8 SDK for Windows 10/11 x64.
3. Run `Build-FM-Atlas-27.bat`.
4. The script saves the full restore/build output to `build-logs\latest-build.log` and offers to copy it to the clipboard. Confirm `BUILD SUCCEEDED` and zero errors.
5. Launch `src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe`.

## Import a career

1. Start FM27 and load a career.
2. Use **Reload Game** for the current read-only identity/relationship importer.
3. Use **Import Save** and choose a COPY of your career save for the richer local snapshot.
4. Do not use your only live save for imports. The save bridge is read-only.

## Analytics

1. Open **Analytics** and choose a numeric metric discovered in the latest retained `player_season_stats`, `player_match_stats` or `player_season_history` tables.
2. Entries prefixed **Per 90** calculate a season rate from the raw metric and the first recognised minutes field in the season stats/history row.
3. Set the **Min minutes (per 90)** threshold (450 by default), and optional value filters.
4. The metric and minutes fields must both exist in the imported source. Compare the results with FM27 Data Hub before relying on them; field semantics may differ between saves or reader versions.

## Role Fit Lab

1. Open **Role Lab**.
2. Choose an IP/OOP role profile and select **Find best fits**.
3. Scores are transparent weighted averages from imported 1–20 attributes. Candidates must have at least 50% of the role's requested attributes.
4. Check the coverage column. This is not SI's hidden role formula, positional suitability or in-game star rating. Export the table to CSV for review.

## Local Editor and Mass Edit

1. Open **Editor Studio**. The editor starts OFF every time FM Atlas launches.
2. Enable it only when you intend to edit the FM Atlas local database.
3. Select a player in Players to edit local CA/PA and attributes.
4. For bulk changes, use **Mass Edit**: Set (0–20), Add (-20 to +20), or Scale (-100% to +500%). Preview the matching count before confirming. Add and Scale skip players with no existing value.
5. Every change is recorded in Edit History. Undo checks the current value and refuses stale changes.

**Important:** None of these actions writes to the live FM process or modifies the career save. Live editing is deliberately disabled pending verified, store/build-specific mappings and full write/read-back/undo tests.

## Tester & Logs

1. Open **Tester & Logs** and run quick checks.
2. Use **Copy safe diagnostics to clipboard** for a compact report.
3. Use **Copy recent application log** to copy the runtime log, or **Export support ZIP** for a shareable bundle.
4. The normal report is redacted. The ZIP excludes your career save and Atlas database. Review all content before sharing.

Application logs are stored at `%LOCALAPPDATA%\FmAtlas\logs`. The local Atlas database is `%LOCALAPPDATA%\FmAtlas\scouting.db`.
