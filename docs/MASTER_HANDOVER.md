# FM Atlas — Master Handover

**Checkpoint date:** 2026-11-13  
**Project:** FM Atlas Windows desktop application  
**Current repository:** https://github.com/lewisxatk/FM-Atlas  
**Checkpoint type:** Documentation baseline; source import pending

## 1. Mission

Build FM Atlas into a polished Football Manager scouting, analytics, player editing and modding desktop suite. The established visual direction is **Aurora Frost** (minimal white/blue, clear Apple-like layout); a separate **Obsidian** dark theme overhaul has also been discussed and is evidenced in the latest session screenshots. Theme work must be coherent across the whole application, not just the logo or a few accent colours.

## 2. What the latest conversation reports

The following items are **reported as implemented or inspected in the previous ChatGPT session**, based on screenshots and visible conversation text. They have not yet been independently checked against source code in this repository.

- FM27 graphics, live import, player editing, metrics, database and search integrations.
- Guarded live-memory editing, importer mappings, UI updates, persistence, undo and profile-graphics resolution.
- Player-search, memory-editing, metrics and diagnostics code inspected and patched.
- Indexed storage, live-editing safeguards, expanded search, profile support and documentation updates.
- FM Atlas value finder implemented/documented with database safeguards.
- C# database parsing, UI imports and search logic inspected/repaired.
- Advanced player search, UI theming, statistics and attribute discovery described as implemented and verified in that earlier session.
- Multi-condition search workspaces and a consistent Obsidian theme were the next development focus.

These are historical session claims, **not this repository's verified feature status**.

## 3. Critical package/source findings from the latest session

The previous session reported:
- An uploaded **R7.0.2 archive** was intact.
- A previously generated **R7.1.0 package** was also present.
- R7.1.0 reportedly included an analytics / role-fit / editor-local / logging pass.
- The R7.1.0 package reportedly stated that live writes were not implemented.
- WPF compilation reportedly had not been run for that package.
- The next task was to trace exact source diffs and the data pipeline, extending existing work rather than replacing it.

The actual archives and source files are not attached to this repository yet. These findings must be rechecked against the files when supplied.

## 4. Search and analytics audit

The previous session described a source audit finding:
- Statistics were discovered as flat numeric leaf names, capped at 500.
- The Analytics screen applied one metric at a time.
- Player attributes were stored in a separate indexed table, potentially providing a better basis for multi-attribute filtering.
- Planned work: extend both paths into proper multi-condition search workspaces.

Do not assume the cap or schema details remain true until the source and database are imported and inspected.

## 5. Product/design direction already established

- Windows 10 desktop application; .NET 8 SDK was previously installed on the user's PC.
- WPF application architecture is reported in the latest package audit.
- Primary scouting layout: filters on the left, central player results, selected-player profile/details on the right.
- Top navigation with clear actions for loading/saving a game and searching.
- Player position picker should cover FM positions and show positional suitability clearly (including pitch visualization and colour-coded suitability).
- Search should cover attributes including mental, hidden and physical attributes where supported by the data source.
- Player profile should expose league, nationality, homegrown information and useful player details.
- Editing mode should make editable attributes explicit and provide a deliberate save action.
- Themes discussed: Aurora Frost and Obsidian. Do not silently discard either; establish the current intended default after inspecting the latest source/package.

## 6. Safety and data integrity requirements

Live-memory editing is high risk. Keep it guarded and opt-in. Before any write:
- identify the exact target process and supported game version;
- validate process/module identity and expected memory region;
- validate address, type, range and current value;
- preserve original values for undo/rollback;
- avoid writes when mappings or version checks are uncertain;
- log the operation and its result without leaking sensitive data;
- never modify the user's only save or database copy during testing.

The presence of a memory adapter or UI does not prove that live writes are implemented or safe.

## 7. Repository state

At this checkpoint:
- GitHub repository exists and is private.
- Default branch is `main`.
- Repository was confirmed empty before the documentation baseline was created.
- No application source code, release archives, database, game save, build output or installer has been imported.
- No build or test has been run from this repository.

## 8. Immediate recovery sequence

1. Obtain the exact R7.0.2 archive and R7.1.0 package/source tree from the previous work.
2. Preserve original files unchanged; record file names, sizes and hashes.
3. Extract copies into an isolated audit workspace.
4. Compare R7.0.2 with R7.1.0 and identify source files changed, added or removed.
5. Determine which package corresponds to the latest source changes.
6. Import the verified source tree to this repository in a dedicated baseline commit.
7. Restore/build with the documented .NET SDK and record the actual output.
8. Only then continue feature implementation.

## 9. Checkpoint update format

For each development checkpoint record:
- date and branch;
- starting commit;
- files changed;
- features implemented;
- build/test commands and exact results;
- known failures and risks;
- package/archive produced (if any);
- next safe action.

Never mark work complete solely because code was drafted or a package was generated.
