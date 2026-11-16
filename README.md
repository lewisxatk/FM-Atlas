# FM Atlas — Fresh Rebuild

A clean Windows desktop application for Football Manager 27 editing, save intelligence and player data.

**Current branch:** `rebuild/fresh-foundation-001`  
**Architecture:** .NET 8 WPF, modular Domain / Application / Infrastructure / Desktop / Tests.  
**Design authority:** `docs/REFERENCE_LIBRARY.md` and `docs/DESIGN_SPECIFICATION.md`.

## Current state

The fresh WPF solution and initial application shell are being established. The first shell includes:
- compact left navigation (no Search item);
- top Quick Search placeholder and Advanced Search action;
- compact Save Atlas / Save Changes controls;
- prominent Load Game action on the right;
- black/off-white/orange design tokens;
- honest disconnected and In Progress states;
- read-only process presence check only.

No player records, attributes, career data or successful edits are fabricated. The first Load Game action only checks whether the FM process and `game_plugin.dll` can be detected; it does not read memory or load a career.

## Build on Windows

Requires .NET 8 SDK and Windows 10/11 x64.

```powershell
dotnet restore FMAtlas.sln
dotnet build FMAtlas.sln --configuration Release
dotnet test FMAtlas.sln --configuration Release
```

GitHub Actions runs the same restore/build/test sequence on Windows.

## Product and technical references

- `docs/FRESH_REBUILD_MASTER_PLAN.md` — product scope and milestones.
- `docs/FRESH_REBUILD_CHECKLIST.md` — task tracker.
- `docs/CURRENT_CHECKPOINT.md` — current resumable state.
- `docs/SESSION_CONTINUITY.md` — interruption recovery rules.
- `docs/ARCHITECTURE_DECISION.md` — clean application architecture.
- `research/UPLOADED_RESEARCH_INVENTORY.md` — uploaded package hashes and contents.
- `research/RESEARCH_FINDINGS_001.md` — initial technical research evidence.

The historical R7.2.0 source is preserved separately in `recovery/r7.2.0-source-import` and is not the new application's code foundation.
