# FM Atlas CoachPro R5 — rebuild source package

This is the R5 CoachPro rebuild based on the supplied pale-aqua / teal player-workspace reference. It replaces the old R4.3 main-window screen with a new programmatic WPF shell and separates live-process identity reading from local save-file snapshots.

## Important release status

**This package is a source-level R5 rebuild candidate, not a Windows-verified final release.** It was edited and statically reviewed in a Linux environment that does not have the Windows .NET SDK/WPF toolchain. Do not treat it as a signed or tested Windows installer. The included scripts build it on your Windows PC and surface compile/runtime errors for verification.

## What is implemented in this source

- New CoachPro-style WPF shell with aqua/teal glass-like surfaces, rounded cards, redesigned navigation and player card/profile layout.
- Separate Dashboard, Players, Recruitment, Clubs, Staff, Squad Architect, Transfers, Tactical Lab, Shortlists, Live Editor, Edit History, Atlas Assistant and Settings workspaces.
- Player search with name/club search, age, position, attribute range, left-foot and right-foot filters.
- Full import of the parser's 52 display-scale player attributes and eight personality values when they are present in a parsed snapshot; attribute names remain sourced from the parser.
- Player profile details for positions, foot strength, reputation, raw state, contract and trait fields when supplied.
- Save snapshot import bridge using the external `fmsave` Python package. It is local-only and read-only; the selected save is not modified.
- Save metadata and a per-reader validation report are stored with each imported snapshot.
- Raw player, staff, club and additional table rows are retained locally to support later modules without losing source fields.
- Staff directory and profile facts where the parser exposes them; Club directory and available finance/facility/stadium/affiliate rows.
- Existing SQLite DB path retained: `%LOCALAPPDATA%\FmAtlas\scouting.db`. Schema migrations are additive. Previous snapshot data and user data are not deleted.
- Local shortlist management, CSV export, local-only ability/attribute editing, audit history and undo.
- Existing FM27 live-process reader remains read-only and is still explicitly limited to the fields it has validated.
- Self-contained `dotnet publish` script and Inno Setup installer definition; installed files leave the user's local FM Atlas data folder untouched.

## Data-source boundaries — read before relying on imported values

The external `fmsave` package can expose players, staff, clubs, contracts, 52 attributes, personality, position ratings and many other career tables. However, the package's current FM27 documentation marks FM27 field meanings as unconfirmed. A registered build means a parser layout is known to the package; it does **not** prove that every decoded field has the intended in-game meaning. The import screen stores and surfaces the reader validation report and warns about this limitation.

- Player CA and PA are imported when the parser exposes them, but the parser currently marks the ability fields as unconfirmed. Compare them with the exact in-game profile before relying on them.
- The 52 attributes are exposed on the parser's 1–20 display scale; raw 1–100 values are retained separately as raw facts, not mixed into display ratings.
- Nation IDs in save readers are save-local numeric IDs, not human-readable nation names. The app does not pretend a number is a nationality.
- Contracts, wages, transfer values and finances are shown with source-unit/context caveats. Historical finance rows are not represented as current balances.
- The public staff record currently exposes only a limited set of rated staff attributes (including adaptability); this does not provide all coaching attributes.
- Club facilities coverage is partial; the parser currently documents corporate facilities separately from every in-game training/youth facility line.
- The live process connector still validates player identity/club links only. It is not an FMRTE-equivalent live memory editor.
- The rule-based Atlas Assistant supports only a subset of natural-language filters; it is not yet a full AI tactical/recruitment analyst.
- Tactical Lab currently displays imported tactical/training/selection data and diagnostics; it does not yet compute a fully tested best formation or role-compatibility model.
- Regen/generative status stays Unknown unless supported evidence is imported. No age-only or UID-only classification is made.
- No live game memory writes are implemented. The Local Editor changes the FM Atlas database only.

## Save reader setup

1. Install Python 3.12 or newer for Windows and enable the Python Launcher (`py.exe`).
2. Extract this package to a normal writable folder.
3. Run `Setup-FMAtlas-Save-Bridge.ps1` from PowerShell, or use Settings → Run save bridge setup.
4. The script installs the public `fmsave` package into your Python environment.
5. Open FM Atlas → Import Career Save and select a **copy** of your career save. The reader runs locally and read-only.
6. Check Dashboard and Settings for the build status and reader validation warnings.

Save parsing can take several minutes and produce a large local JSON snapshot in `%LOCALAPPDATA%\FmAtlas\imports`.

## Build on Windows

Install .NET 8 SDK (Windows) first. Then from the extracted root:

```powershell
.\Build-FMAtlas-R5.bat
```

Launch:

```powershell
.\src\FmAtlas\bin\Release\net8.0-windows\FmAtlas.exe
```

## Publish a self-contained app

This bundles the .NET runtime; the player save-reader remains an optional Python sidecar because it uses an external parser package:

```powershell
.\Publish-FMAtlas-R5.bat
```

Output: `publish\win-x64\FmAtlas.exe` plus the local Python adapter and setup helper. Users do not have to extract or manage the sidecar files manually when installed through the installer.

## Build the one-file Windows installer

Install Inno Setup 6 (which provides `ISCC.exe`) and run:

```powershell
.\Build-Installer-R5.bat
```

Output: `installer-output\FM-Atlas-CoachPro-Setup-5.0.0.exe`.

The installer places app files under the current user's Local AppData Programs folder and creates Start Menu / optional desktop shortcuts. Uninstall does not delete `%LOCALAPPDATA%\FmAtlas`.

## Preserved user data

The application database remains `%LOCALAPPDATA%\FmAtlas\scouting.db`. Imported snapshot JSON is stored under `%LOCALAPPDATA%\FmAtlas\imports`. Do not delete these folders when updating FM Atlas.
