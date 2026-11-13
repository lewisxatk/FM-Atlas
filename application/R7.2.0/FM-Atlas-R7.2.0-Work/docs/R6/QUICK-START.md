# FM Atlas CoachPro R6 — Quick-start tutorial

## 1. Build and launch

1. Extract the complete R6 ZIP to a normal folder, for example `Documents\FM Atlas R6`. Do not run it from inside the ZIP preview.
2. Install the **.NET 8 SDK for Windows** if it is not already installed.
3. Double-click `Build-FMAtlas-R6.bat`.
4. Wait for `Build succeeded` and `0 Error(s)`. A warning is not the same as a failed build; copy any warning/error text if unsure.
5. Launch `src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe`.

For a portable self-contained release, run `Publish-FMAtlas-R6.bat`. It creates `publish\win-x64\FmAtlas.exe`. The optional installer script requires Inno Setup 6 and is launched with `Build-Installer-R6.bat`.

## 2. Load your career safely

**Live identity import:** open Football Manager 27 with your career loaded, then choose **Load Game**. This path is read-only and currently gathers the identity and relationship data the validated live reader knows how to decode. It does not mean CA/PA, the full attribute array, staff or all club facts have been imported.

**Fuller save snapshot:** choose **Import Career Save** and select a COPY of your career save. If the save bridge is missing, run `Setup-FMAtlas-Save-Bridge.ps1` from the extracted R6 folder, then restart FM Atlas. The parser works locally/offline. Review its warning report because FM27 field semantics are still build-dependent.

## 3. Find players

- Open **Players**. The result table starts empty deliberately; it no longer tries to render the entire player database alphabetically.
- Type at least two characters into the search box and press Enter or **Search**.
- Choose a position filter or one of the position chips. Results are loaded in pages of 250.
- Double-click a result or select it to open its player profile tab.
- Use **← Previous** / **Next →** to move through result pages.
- Use **Expand full profile** to give the profile the full content width.

## 4. Use global and advanced search

- Press **Ctrl+F** or choose **Global Search** to open the search pop-out. Results begin narrowing as you type (420 ms pause); press Enter or Search to run immediately.
- Press **Ctrl+Shift+F** or select **Advanced Search** to open the same search workspace with the full filter set visible.
- Search can target players, staff, clubs or all supported types. Filters include age, position, CA/PA, nationality, league, attribute range, left/right foot strength, preferred foot and home-grown status when the underlying imported records contain those values.
- Unknown values do not pass a minimum/maximum filter. For example, a CA filter will not silently treat an unavailable CA as zero.
- Double-click a result or press **Open selected record**.

## 5. Player tabs and profile layout

Every player opens in their own tab. Select the tab to switch between players and press the **×** on that tab to close it. **Ctrl+W** closes the selected profile. Tabs are retained when you navigate to other workspaces and return to Players during the current app session.

The full profile is grouped in familiar Football Manager order: Technical, Set Pieces, Mental, Physical, Goalkeeping and Hidden. Other sections show position suitability, personal details, contracts, condition/state, traits and source provenance when imported.

A dash means **not provided by the data source**. It is not a zero rating. CA/PA and attributes appear only if an imported save/report or validated field contains them.

## 6. Clubs and staff

Open **Clubs** to search the combined club directory. Select a club to inspect its source, nation, linked player count, imported facts and squad list. The club profile does not auto-load on first opening; select a row to fetch its squad. Double-click a player in the squad to open their profile.

Finance, debt, facilities, stadium, affiliations and detailed contract fields can only be shown if the save export actually supplied them and the reader retained them. A live club identity alone does not contain those facts.

Open **Staff** and type at least two characters. Staff identity, role and club fields are displayed where available. The current live identity reader does not populate staff; if no staff results are returned, import a supported save snapshot and check its export counts.

## 7. Atlas Assistant, shortlists and editing

- **Atlas Assistant** remains a local rule-based query helper. It can translate supported phrases into database filters; it is not a cloud AI assistant.
- Add players to local shortlists from their player profile, then review/export them under **Shortlists**.
- **Live Editor** edits FM Atlas's own local database and edit history. It does not write to the running Football Manager process or the original save.
- Use **Edit History** to inspect and undo eligible local changes.

## 8. Data folder and backups

Your database is stored at `%LOCALAPPDATA%\FmAtlas\scouting.db`. Open the folder from **Help & Tutorial** or paste `%LOCALAPPDATA%\FmAtlas` into File Explorer. Close FM Atlas before copying the database and its `-wal`/`-shm` companion files. Keep a separate backup before importing a major save or making many local edits.

## 9. Troubleshooting

- **Build fails:** ensure the .NET 8 SDK is installed; rerun the build script and copy the first compiler error plus the final summary.
- **Dashboard says 40K players but CA/PA is blank:** that count is live identity rows, not fully populated profiles. Import a save snapshot or supported intelligence CSV for broader fields.
- **Only a few search results appear:** check the search term. A search for `Salah` should return Salah matches, not all players. Remove the text to search a different name.
- **No club facts/squad:** verify that a local save snapshot exists and that the parser exported club rows and player-to-club links.
- **No staff:** verify the importer export contains staff rows. The live identity reader does not currently provide a staff list.
- **Values appear suspicious:** check the importer validation report and compare important values with the in-game UI. Do not use provisional memory offsets as field truth.
