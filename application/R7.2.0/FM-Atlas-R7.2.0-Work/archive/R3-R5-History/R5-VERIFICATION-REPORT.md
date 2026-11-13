# FM Atlas CoachPro R5 — verification report

## Completed checks in the package-preparation environment

- Parsed every WPF XAML file and the project file as well-formed XML.
- Python-compiled `tools/Export-FMAtlas-Fmsave.py`.
- Ran the save adapter against a mocked `fmsave` package and verified player, staff and club records, CA/PA, display attributes, raw attributes and non-fabricated nationality handling.
- Executed the initial and save-snapshot SQLite schema scripts in SQLite and checked additive columns used by R5.
- Exercised representative CA/PA, position and 1–20 attribute filter SQL.
- Checked every page registered in navigation has a page method and every `_database` call in the main window has a corresponding database method.
- Checked source delimiter counts and package file inventory.

## Checks not completed

- No .NET 8 SDK, MSBuild or WPF targeting pack is installed in this environment; the project has not been compiled.
- No actual Football Manager 27 career save was available; `fmsave` has not been run against the user's save.
- No live FM27 process was available to validate the current memory reader.
- The Inno Setup compiler is not installed here; the Windows installer definition has not been compiled.

## Release gate on Windows

1. Run `Build-FMAtlas-R5.bat` and resolve any compiler errors before running the executable.
2. Install the optional save bridge and import a **copy** of the current career save.
3. Inspect the fmsave validation report and compare CA/PA, attributes, contracts and manager identity against the in-game UI for a representative sample.
4. Verify the existing `%LOCALAPPDATA%\\FmAtlas\\scouting.db` remains intact.
5. Only then build `FM-Atlas-CoachPro-Setup-5.0.0.exe` using `Build-Installer-R5.bat` and Inno Setup 6.

R5 is a substantive source rebuild, but it is not yet a verified production release or a full FMRTE / Genie Scout replacement. The live editor is local-database-only; live process writes remain disabled.
