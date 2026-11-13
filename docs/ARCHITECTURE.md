# FM Atlas — Observed Source Architecture

**Status:** Static source audit of the recovered R7.2.0 application. This replaces the earlier provisional architecture sketch. It does not claim a complete runtime architecture or feature acceptance; see [Source Architecture Audit](SOURCE_ARCHITECTURE_AUDIT.md).

## Verified project shape

- Windows desktop application using C# and WPF.
- Solution: `application/R7.2.0/FM-Atlas-R7.2.0-Work/FmAtlas.sln`.
- Project: `src/FmAtlas/FmAtlas.csproj`, target `net8.0-windows`, `UseWPF=true`.
- Database dependency: Microsoft.Data.Sqlite 8.0.11.
- Nullable reference types and implicit usings are enabled.
- Root XAML is a simple host window; most UI is composed dynamically in code-behind.
- Main persistence and import/search logic is concentrated in `Data/ScoutDatabase.cs` (about 2,665 lines in the inspected source revision).

## Observed startup sequence

1. `App.xaml.cs` constructs `AtlasLog` and applies the saved `MonolithTheme`.
2. Global logging handlers are registered for WPF dispatcher exceptions, AppDomain unhandled exceptions and unobserved task exceptions.
3. WPF loads `MainWindow.xaml`; the MainWindow constructor initialises logging, calls `InitializeComponent`, applies the preferred font and registers keyboard handlers.
4. On Window Loaded, the database is initialised, the dynamic shell is built, connection/career status is refreshed and Dashboard navigation is requested.
5. MainWindow catches startup/database errors, logs them and displays a user-facing error. A more resilient recovery/startup diagnostics path remains future work.

## Source map

| Boundary | Current files | Observed responsibility / limitations |
|---|---|---|
| WPF host/startup | `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` | Startup hooks, global logging, dynamic shell, navigation and page composition. MainWindow is a large code-behind class, not an MVVM-separated shell. |
| UI feature partials | `MainWindow.AdvancedSearch.cs`, `MainWindow.Diagnostics.cs`, `MainWindow.R7.cs`, `MainWindow.RoleLab.cs`, `MainWindow.ValueFinder.cs` | Search, Tester & Logs, analytics/mass edit, Role Lab and Value Finder surfaces. Partial class structure; not a clean presentation/application boundary yet. |
| Secondary windows | `GlobalSearchWindow.cs`, `PlayerIntelligenceReportWindow.xaml(.cs)` | Global search and player intelligence report presentation. |
| Domain/view models | `Models/PlayerRecord.cs`, `Models/AdvancedSearchModels.cs`, `Models/ValueFinderModels.cs` | Player, shortlist, statistic and Value Finder records. Registry/provenance metadata remains distributed across persistence/services. |
| Persistence/data | `Data/ScoutDatabase.cs` | SQLite schema, migrations, imports, search, metrics, player facts, shortlists and edit history. This file is a refactoring/testability hotspot. |
| Logging/diagnostics | `Services/AtlasLog.cs`, `MainWindow.Diagnostics.cs` | Local rolling logs, health checks, support report and ZIP export. Current format is tab-delimited and some correlation/context fields remain absent. |
| Theme | `Services/MonolithTheme.cs` | Obsidian/Warm Light palettes, Inter fallback, persistent mode and legacy palette resolver. Full shared controls and screen migration are incomplete. |
| Field evidence | `Services/FieldEvidencePolicy.cs`, `Data/ScoutDatabase.cs` | Unverified/Candidate/ProfileMatchedProvisional/FieldVerified states. A module profile match is not field verification. |
| Game build/process | `Services/Fm27BuildProfiles.cs`, `Services/GameProcessDetector.cs`, `Services/GameModuleDiagnostics.cs` | Process and module identification, separate Game Pass/Steam build profile records and diagnostics. User-specific exact build acceptance is not established. |
| Read-only live scan | `Services/LiveDataImporter.cs` | Live process identity/player/club read path, profile matching, field survey and snapshot payload. This requires independent field witnesses and exact-build testing. |
| Live edit | `Services/LiveMemoryEditService.cs`, `Services/LiveWriteCertification.cs`, `Services/PlayerAttributeSchema.cs` | Staged narrow field writes, identity/session checks, preflight/read-back/undo code. Global certification currently returns false; Apply/Undo stay blocked. |
| Offline save import | `Services/FmsaveBridgeService.cs`, `tools/Export-FMAtlas-Fmsave.py`, `Data/ScoutDatabase.cs` | Optional local Python/fmsave adapter and JSON snapshot import. Exact save-version coverage and full section-level validation remain unproven. |
| Graphics | `Services/GraphicsResolver.cs` | Configurable root, config.xml mapping and image indexing for faces/logos/flags. User's actual Game Pass folder and collision behaviour need validation. |
| Role intelligence | `Services/RoleFitService.cs`, `MainWindow.RoleLab.cs` | 23 transparent weighted role profiles; not the game's hidden role formula or official star rating. |

## Navigation routes observed

The shell builds sidebar routes for Dashboard, Players, Recruitment, Value Finder, Attribute Search, Stats Search, Squad Architect, Clubs, Staff, Transfers, Analytics, Tactical Lab, Role Lab, Shortlists, Live Editor, Mass Edit, Edit History, Atlas Assistant, Settings, Tester & Logs and Help & Tutorial.

A route being visible in navigation does not prove completeness. Search, Analytics, Mass Edit, Live Editor and other routes need per-screen acceptance. The current shell has a Ctrl+F global search path; the planned Ctrl+K command palette is not implemented.

## Runtime and evidence boundaries

- The user-provided Windows build report for repaired R7.2.0 says 0 errors and 15 warnings. It predates the later overhaul-branch privacy changes.
- A repeatable Windows .NET 8 workflow has been added, but no run is currently reported.
- No application launch or UI smoke test is evidenced by this audit.
- FM27 Game Pass and Steam profiles are separate code records. Neither proves the user's exact installed build is field-certified.
- `LiveWriteCertification.IsCertified` is hard-coded false. Apply and Undo must remain blocked until independent field evidence and disposable offline-save acceptance exists.
- Save parser coverage, analytics correctness, graphics folder discovery and all visible workspace behaviour remain subject to fixtures and runtime tests.

## Next architectural work

1. Establish fresh Windows build/run evidence and a warning triage baseline.
2. Add focused tests and fixture projects around redaction, import, search, percentiles, role fit and graphics resolution.
3. Extract shared MONOLITH semantic resources and WPF control factories before page migration.
4. Separate UI composition from query/import/domain services so screen redesign does not silently alter data correctness.
5. Create a canonical field registry with source, version, units, visibility, confidence and verified-use status.
6. Keep the live memory adapter behind read-only/field-level certification boundaries.
