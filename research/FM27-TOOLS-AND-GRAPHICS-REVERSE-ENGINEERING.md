# FM27 Tools, Memory Reading and Graphics Research

**Research date:** 13 November 2026  
**Purpose:** Record the user-supplied tools, public references, static inspection of uploads and practical implications for FM Atlas.  
**Scope:** Uploaded installers were not executed or installed. No live FM process or user save was inspected. No third-party code has been copied into FM Atlas.

## Executive conclusion

The strongest route for Atlas's fast scouting data is a dedicated read-only acquisition layer: identify the exact game build/storefront, locate the loaded player/staff/club entity arrays, bulk-read records using verified per-build field maps, then normalise and index those records locally. FMSuperScout and FMLibreEditor expose this architecture in source. FM Player Analyzer and fmsave provide a separate offline save-file route. A graphics indexer can map stable FM IDs to portraits/logos using pack config.xml files without changing user files.

We should learn from their architecture and safeguards, not blindly transplant offsets or binaries. The user's target is FM27 Xbox/Game Pass on Windows; Steam compatibility is not proof of Game Pass compatibility.

## 1. User-provided tools and pages

### TalentSeer 27.1.2
- Page: https://sortitoutsi.net/content/78140/talent-seer-is-here-a-free-scouting-and-editing-tool-for-football-manager-27
- Upload: `TalentSeer-27.1.2-win-x64(1).zip`
- ZIP contents: README, `TalentSeer.exe` and third-party notices; no project source.
- Its README describes reading loaded FM27 player/staff data including CA/PA, hidden attributes and personality, plus filters and ratings for 79 roles. It also describes editing values and projecting attributes towards PA.
- Research value: benchmark search, attribute coverage, role-fit UX and profiles. These are product claims, not independently reproduced timings/field validation. Treat potential attribute projection as the tool's model unless confirmed as a true game field.
- Limitation: supplied package is a closed binary, so its internal read method is not established.

### Teknik Oda / Toby 1.0.9
- Page: https://sortitoutsi.net/content/78456/teknik-oda-toby-a-coach-who-reads-your-fm27-match-from-fms-own-data-open-beta
- Upload: `TeknikOda-Kurulum-1.0.9(1).exe`
- Public description/release notes describe an in-game BepInEx plugin plus companion app for match/tactical data. Version notes say it was tested end-to-end on FM build 26577676 on 12 November 2026, 12 of 12 checks.
- Research value: match events, tactical data, plugin lifecycle, build verification and BepInEx conflict handling. This is a different acquisition surface from a player database scanner.
- The uploaded file was statically identified as an Inno Setup installer; it was not executed or unpacked.

### FaceDrop
- Page: https://sortitoutsi.net/content/78390/facedrop-drag-drop-faces-for-fm27-players-regens-managers
- The page says it uses BepInEx 6 on FM27 Steam for Windows and stores images under `Documents\Sports Interactive\Football Manager 27\facedrop`.
- It supports drag/drop and clipboard face assignment. Real-player assignments are shared across saves; regen/manager assignments are stored per career. The creator says save and game files stay untouched.
- Research value: treat FaceDrop as a separate face-image provider, not automatically as a normal config.xml graphics pack.

### Customize Editor alpha skin/mod
- Page: https://community.sports-interactive.com/forums/topic/613004-wipfm27-editormod-customize-editor-alpha-version/
- The author describes a base skin for the FM27 Pre-Game Editor that can change appearance, expose hidden options and change selected limits without editing .fmf files directly.
- Instructions place extracted files in the Editor skins folder and remove `%LOCALAPPDATA%\Sports Interactive\Editor 27\Caches\editor.skc` before relaunching.
- It is explicitly alpha; Editor updates may require a refreshed mod. Research value: editor skin structure, cache invalidation and customisation. It is not presented as a live-save editor.

### Scout Reporter
- Page: https://fmscouting.sebastianfoder.com/
- Browser-local CSV analysis. It imports FM view exports; Wonderkid mode expects a Hidden Attributes view containing CA, PA and Professionalism.
- Offers attribute weights, Best XI within budget, squad analysis and staff scoring. Its page says CSV stays in the browser.
- Research value: safe CSV provider, import workflow and transparent scoring options. It does not claim to read the live process itself.

### Supplied GitHub repositories

| Project | URL | Main research value | Licence caution |
|---|---|---|---|
| FMSuperScout | https://github.com/mavarobli/FMSuperScout | Fast read-only live scan, memory layouts, snapshot model and development history | MIT; verify current licence and retain notices if reused |
| FMLibreEditor | https://github.com/tozmoz/FMLibreEditor | FM27 live editor, Game Pass profiles, safe writes and recon docs | GPL-3.0; no code transplant without licence decision |
| FM Player Analyzer | https://github.com/ElectroHugin/FM-Player-Analyzer | FM27 save parser, scouting import and graphics config.xml index | GPL-3.0; reference only unless Atlas adopts compatible licensing |

Additional useful references:
- fmsave: https://github.com/rhiever/fmsave — read-only save parser; MIT (verify current LICENSE).
- FM View Exporter: https://github.com/ElectroHugin/FM-View-Exporter — view export workflow; verify current FM27 compatibility.

### FM View Exporter
- Repository: https://github.com/ElectroHugin/FM-View-Exporter
- GPLv3 BepInEx plugin for FM27 Windows/Steam. Exports the currently displayed squad, player database or shortlist view to CSV and HTML with F9.
- It scrolls through the list because FM only fills rows currently on screen; a squad can finish in seconds, but large player searches can take minutes and FM caps a search view at 10,000 results.
- It exports exactly the visible FM view: hidden/unscouted values remain hidden. This is a UI-level export, not a hidden-stat memory reader.
- Research value: a user-friendly import fallback, repeatable CSV fixtures and a route to compare Atlas's parser against FM's displayed columns. GPLv3; do not copy code without licence decision.

- FM Dossier: https://www.fmdossier.dev/how-it-works — proprietary live-memory scouting reference.
- FMRTE docs: https://docs.fmrte.com/ — feature/UX reference, not source-code evidence.
- FMRTE 27 product page: https://www.fmrte.com/ — current feature benchmark includes live player/club/competition editing, quick search, player stats, transfer terms and undo. Its docs say scouting/search is free while saving edits requires a licence. This reveals advertised features, not its internal implementation.


## 2. How fast live-memory tools find hidden attributes

Public source from FMSuperScout and FMLibreEditor documents a common pattern:

1. Find the running FM process and open it with only the permissions required.
2. Identify the native game module (notably `game_plugin.dll`) and exact build/storefront profile.
3. Locate the loaded player, staff and club entity arrays using known build metadata or a cautious discovery scan.
4. Enumerate only committed/readable private memory regions; skip no-access, guarded and irrelevant regions.
5. Find object instances using vtable/class metadata and expected object shapes.
6. Decode records with per-build field offsets: names, UIDs, attributes, CA/PA, contracts, values, positions and club/team relationships.
7. Bulk-read in bounded chunks, limit worker count and cache module images/metadata to avoid an OS read for every pointer.
8. Convert raw memory into normalised local records and build a local search index. The UI should not query FM memory for every keystroke.

Hidden stats can be read because the scanner reads the loaded game database records rather than the scouting UI. CA/PA and hidden attributes may therefore exist in the underlying objects even when FM does not display them. Fog-of-war is a separate policy: a tool can choose to mask values for realism even if the file/parser can decode them.

### FMSuperScout source observations
- `core/MemScan.Win.cs` uses Windows `OpenProcess`, `VirtualQueryEx` and `ReadProcessMemory`. The external process handle requests read/query access, not write access.
- It scans committed `MEM_PRIVATE` readable/writable or copy-on-write regions, skipping guard/no-access pages.
- It caches mapped images of `game_plugin.dll` and `GameAssembly.dll` for fast vtable/meta resolution, and reuses large buffers to reduce repeated allocations and memory pressure.
- It uses bounded 32 MiB chunks, up to eight workers, and an optional Windows PSS virtual-address clone for a consistent retry if live reading fails. Snapshotting is not the default because it has a memory/commit cost.
- `core/MemScan.cs` wraps safe typed reads and string decoding; it avoids direct raw dereferencing of arbitrary game addresses.
- `core/Fields.cs` contains version-specific memory layouts. `docs/repin-guide.md` describes revalidating class offsets, CA/PA, names, contracts and attribute ranges after a game patch.
- README scan timings (for example ~6 seconds for 60k+ people) are project claims; Atlas has not reproduced them.
- Source: https://github.com/mavarobli/FMSuperScout

### FMLibreEditor source observations
- `docs/DEVELOPING.md` describes FM27's UI as Unity/IL2CPP while native game database/simulation structures live in `game_plugin.dll`.
- It separates read-only process handles from read/write handles and detects Microsoft Store/Game Pass packaging.
- It has build profiles and defaults to read-only when the build/layout is not checked.
- Before every write it checks: current save identity; object/person identity; valid new value; expected current value; read-back result.
- It detects a save load/reload and suspends writes until data is refreshed, preventing stale pointers crossing careers.
- The recon docs include specific Game Pass builds, but those are build-specific historic observations. They do not prove the profile matches the user's current install.
- Source: https://github.com/tozmoz/FMLibreEditor

## 3. Offline save parsing is another data provider

The uploaded FM Player Analyzer 2.2.3 ZIP contains source for its FM27 parser and importer:
- The parser recognises an `fmf.` save container with a trailer directory and concatenated Zstandard frames.
- It reads the trailer directory to discover sections such as `game_db` and `fog_of_war`, then decompresses the relevant frame/section instead of treating the whole file as one simple compressed stream.
- The importer applies scouting-knowledge rules and deliberately suppresses selected hidden values in its standard import/UI path. Parsing a value and choosing to expose it are separate decisions.
- A live-save watcher waits until file size and modification time stabilise before importing, avoiding reads while FM is still writing the save.
- Atlas opportunity: a separate read-only Save Import provider, with source/build/timestamp and field-confidence attached to each imported record.
- Do not assume FM26 container details apply to all FM27 builds. Validate on disposable copies and fixtures.
- Source: https://github.com/ElectroHugin/FM-Player-Analyzer

## 4. Graphics, faces, logos and skins

### Conventional graphics packs

Graphics packs commonly carry a `config.xml` in each pack folder. Example mappings:

```xml
<record from="face_123" to="graphics/pictures/person/123/portrait"/>
<record from="123_club" to="graphics/pictures/club/123/logo"/>
```

A graphics indexer for Atlas should:
- Find the actual FM user-data root, then inspect its `graphics` folder. Do not scan only the game installation folder.
- Recursively find each `config.xml` below the graphics root.
- Parse mappings for person portraits, club logos and nation logos; extend to kits/backgrounds only after confirming their target formats.
- Resolve the `from` filename stem against files in the pack folder (PNG/JPG and any confirmed formats).
- Index by stable FM UID/entity ID; retain original mapping text, pack name and folder path.
- Detect duplicate mappings, missing files, unsupported targets and malformed config files.
- Cache a compact index using root path and each config file's relative path, size and modified time. Rebuild only when a relevant config changes.
- Preview the resolved image in player/staff/club profiles; provide Open Folder and Rescan actions. Never rename, move or rewrite user files.

FM Player Analyzer implements this pattern in `GraphicsIndex.cpp/.h`: recursive config discovery, mappings for person/club/nation images, sorted ID index, file-extension resolution and a cache invalidated by config size/mtime. Its header comments estimate 560,000 portrait entries at roughly 4.5 MB of index data. That is a source comment, not an Atlas benchmark.

### FaceDrop and regen identities
FaceDrop's `facedrop` folder is a separate workflow. Conventional facepacks use config mappings and IDs; some regen/newgen packs use raw `r-<UID>` strings. Preserve raw mapping strings as well as any parsed numeric UID so unresolved IDs are diagnosable.

### Skin discovery
- Extracted skins typically have a skin config file such as `skin_config.xml` plus their other resources.
- Some skins are supplied as `.fmf` files placed directly in the `skins` folder.
- Editor skins/mods can live under a separate Editor user-data tree, not the main FM27 user-data folder.
- The Customize Editor alpha uses `%LOCALAPPDATA%\Sports Interactive\Editor 27\Caches\editor.skc` as a cache reset step. Atlas must not delete it automatically; only expose the location and a user-confirmed open-folder action.

### Official user-data paths and Game Pass
SEGA says the Sports Interactive folder can be changed inside FM via Preferences → Detailed → Your World → Change Folder. Its support page also says the Steam/Epic `--user_data_location` launch option is not available for Microsoft Store/Xbox FM. So:
- Do not hard-code a Steam path for the user's Game Pass edition.
- Read the configured location when possible; otherwise discover likely Documents/OneDrive paths and ask the user to confirm.
- Keep user-data path separate from protected game installation paths (for example WindowsApps).
- Persist the verified user-data root as an Atlas setting and revalidate it after FM version changes.

Official guidance: https://support.sega.com/hc/en-us/articles/29578432758801-How-to-change-the-Football-Manager-s-Sports-Interactive-folder-location-in-my-computer

## 5. Static inspection of uploaded files

All uploads below were identified/inspected statically. No installer or DLL was executed.

| Uploaded file | Static result | Research value / limitation |
|---|---|---|
| `FM-Dossier_1.15.4_x64-setup(1).exe` | PE32 NSIS installer; SHA-256 `343e9f6ace7d73815d4da71fa5d97d8c8afed7f74b46ecb73be4e53bb0051b46` | Proprietary live-memory reference; not installed/unpacked |
| `SaveMyTime(1).zip` | Contains `SaveMyTime.dll` (1,344,000 bytes) and two small `.fmf` schedule templates; DLL is a .NET/Mono PE assembly; ZIP SHA-256 `2b8eabeb431c813d370617129434968e849d673380b732cb6a0b8ce132d09be0` | Static strings suggest extensive training schedule discovery/verification and UI context handling; exact behaviour not verified |
| `TeknikOda-Kurulum-1.0.9(1).exe` | PE/Inno Setup 6.7; SHA-256 `37e717db165f875bc5d8a89c03c3bf4bc20513320785df47700ccbbe949294fc` | Not executed; public page used for feature research |
| `FM-Player-Analyzer-2.2.3(1).zip` | 419-entry source archive; SHA-256 `3637b5aa1736d89ece8e752a78e73bf6c1a04b379ab933df4211bfe9cd41dd61` | Contains FM27 save parsing and graphics index source; GPL-3.0 |
| `FMLibreEditor-Setup(1).exe` | PE32 NSIS installer; SHA-256 `5954fc6fce28ac698f03e44c72e7266a37a26ca13bf7257aee9d7f25c6271151` | Not executed; public source/docs are better evidence for internals |
| `TalentSeer-27.1.2-win-x64(1).zip` | README + `TalentSeer.exe` + third-party notices; SHA-256 `a015be52fcf9891dc7b58e78920fcbf0a8e7afa3084d444dbdcf4e153b3a0fed` | Closed binary in supplied package; README/product claims only |
| `FMSuperScout-Setup(1).exe` | PE/Inno Setup 6.7; SHA-256 `96ee55a62c05ef7ef4123256a9aaa09410a52bccf979c6c93834fb1ed2738bae`; **matches the GitHub v1.7.3 published SHA-256** | Confirms the supplied installer is the published v1.7.3 asset; not executed; public MIT source inspected instead |

## 6. FM Atlas implementation recommendations

### P0 — protect and verify R7.2.0
- Import the R7.2.0 source checkpoint to its isolated GitHub recovery branch first.
- Inventory existing memory reader, build profiles, LiveMemoryEditService, tests and read/write guards.
- No replacement of existing code until a file-by-file gap analysis exists.

### P1 — read-only FM27 acquisition core
- Detect process, executable path, package/storefront, game_plugin.dll version/hash and exact build.
- Use profile-specific field maps and record provenance.
- Discover entity arrays from a verified profile or diagnostic fallback; refuse to label data Verified when layout checks fail.
- Bulk-read into normalised DTOs and a fast local index; keep UI work out of the scan loop.
- Unknown builds remain read-only by default.

### P1 — graphics library
- User-data folder discovery/selection.
- Graphics pack scanner with config.xml parser and UID index.
- Skin folder discovery and metadata view; list .fmf skin files but do not unpack or mutate them.
- FaceDrop special-provider detection.
- Profile previews, source pack path, duplicate/missing-image report, rescan and open-folder actions.
- Never rename, move or rewrite user assets.

### P2 — offline save inspector/import
- Separate parser adapter from live memory.
- Work on copies only; validate bounds, frame counts, decompression limits and incomplete writes.
- Keep fog-of-war policy separate from source data and distinguish Unknown from zero.

### P2 — Match Lab data provider
- Study Teknik Oda's in-game match-data provider and plugin lifecycle separately from player DB extraction.
- Start with documented match summaries/user exports; deeper integration follows only after storefront/build compatibility is confirmed.

## 7. Verification gates

- The user's exact Game Pass FM27 build and game_plugin.dll version are not yet known.
- Another tool's Game Pass support does not validate Atlas's memory layout.
- No uploaded EXE/DLL was launched; installer contents/behaviour are not established by static PE type alone.
- SaveMyTime behaviour needs source or safe isolated runtime inspection.
- TalentSeer's hidden-stat and role scoring claims need controlled comparison against known FM values.
- FM Player Analyzer parser needs tests against FM27 sample copies.
- Graphics indexing needs real sample packs to test path patterns, case sensitivity, duplicate IDs, missing files and FaceDrop coexistence.

## Public sources
- https://sortitoutsi.net/content/78140/talent-seer-is-here-a-free-scouting-and-editing-tool-for-football-manager-27
- https://sortitoutsi.net/content/78456/teknik-oda-toby-a-coach-who-reads-your-fm27-match-from-fms-own-data-open-beta
- https://sortitoutsi.net/content/78390/facedrop-drag-drop-faces-for-fm27-players-regens-managers
- https://community.sports-interactive.com/forums/topic/613004-wipfm27-editormod-customize-editor-alpha-version/
- https://github.com/mavarobli/FMSuperScout
- https://github.com/tozmoz/FMLibreEditor
- https://github.com/ElectroHugin/FM-Player-Analyzer
- https://github.com/rhiever/fmsave
- https://www.fmdossier.dev/how-it-works
- https://fmscouting.sebastianfoder.com/
- https://support.sega.com/hc/en-us/articles/29578432758801-How-to-change-the-Football-Manager-s-Sports-Interactive-folder-location-in-my-computer
