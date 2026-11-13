# Recovery Inventory — uploaded R7 packages

**Inventory date:** 2026-11-13  
**Status:** Both uploaded ZIP archives were opened and inspected successfully. Original uploads remain unchanged in the conversation.

## Archive integrity

| Package | ZIP bytes | Entries | Files | Uncompressed bytes | SHA-256 |
|---|---:|---:|---:|---:|---|
| R7.0.2 Compiler Fix | 2,863,633 | 102 | 83 | 3,250,886 | `c49d29c1433681085b84a9ebc4e50373d8b03e69044812fb078b6aca94af1d30` |
| R7.1.0 Integrated Source Test Package | 2,921,046 | 114 | 95 | 3,402,595 | `7a2db7d16f3a2c7e2c8ceb0edfd11ae201c643a315dc7c57bad9b46d125243de` |

## Direct comparison

The archives share 83 relative file paths. 16 shared files differ. R7.1.0 contains 12 additional files and no paths absent from R7.0.2.

### Additional R7.1.0 files

- `BUILD-STATUS-7.1.0.txt`
- `Build-FM-Atlas-27-Logged.bat`
- `Open-FM-Atlas-Logs.bat`
- `TEST-REPORT-TEMPLATE.md`
- `docs/R7.1.0-ACCEPTANCE-AND-TEST-PLAN.md`
- `docs/R7.1.0-INTEGRATION-UPDATE.md`
- `docs/R7/EXTENDED-RESEARCH-AND-BACKLOG.md`
- `docs/R7/FM-ATLAS-MASTER-SPECIFICATION.md`
- `src/FmAtlas/MainWindow.Diagnostics.cs`
- `src/FmAtlas/MainWindow.RoleLab.cs`
- `src/FmAtlas/Services/AtlasLog.cs`
- `src/FmAtlas/Services/RoleFitService.cs`

### Shared files changed in R7.1.0

- `Build-FM-Atlas-27.bat`
- `Build-Installer-FM-Atlas-27.bat`
- `CHANGELOG-FM-ATLAS-27.md`
- `Installer/FM-Atlas-27.iss`
- `Publish-FM-Atlas-27.bat`
- `README.md`
- `START-HERE.txt`
- `VERSION.txt`
- `docs/R7/BUILD-AND-TEST-CHECKLIST.md`
- `docs/R7/IMPLEMENTATION-STATUS.md`
- `docs/R7/QUICK-START.md`
- `src/FmAtlas/App.xaml.cs`
- `src/FmAtlas/Data/ScoutDatabase.cs`
- `src/FmAtlas/FmAtlas.csproj`
- `src/FmAtlas/MainWindow.R7.cs`
- `src/FmAtlas/MainWindow.xaml.cs`

## Findings from R7.1.0 package documentation

- R7.1.0 identifies itself as a .NET 8 WPF / SQLite Windows desktop application.
- It reports Role Fit Lab with 23 transparent In Possession / Out of Possession role profiles.
- It reports season per-90 analytics when source minutes are available.
- Editor Studio is local to the Atlas SQLite database, session-gated and OFF by default.
- Mass Edit supports Set/Add/Scale; Undo checks current values before reverting.
- Tester & Logs, rolling logs and redacted support ZIP export are included.
- The package explicitly says live FM memory writes are not implemented; the live process reader is read-only.
- The package states Windows WPF compilation, Windows startup, real save import and Steam/Game Pass process checks still require testing. Its preparation environment lacked .NET SDK/MSBuild.

## Recovery decision

R7.1.0 is the newest supplied source package and is the candidate working baseline. R7.0.2 is retained as the comparison baseline. This is a version-order decision, not a claim that R7.1.0 has passed a Windows build or runtime acceptance test.

## Next technical action

Import the R7.1.0 source tree without discarding the R7.0.2 comparison. Keep package provenance, hashes and the exact source diff available. Then build on Windows and record the real output before further feature work.
