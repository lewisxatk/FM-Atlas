# FM Atlas Reverse-Engineering Research — Initial Findings

**Research date:** 2026-11-13  
**Status:** Desk research; no external source code has been copied into FM Atlas. No live game process or user save was accessed.  
**Rule:** Treat each game build and storefront as a separate compatibility target until verified.

## Executive findings

There are now several useful open-source FM26/FM27 projects that expose real implementation details rather than merely describing commercial tools. The strongest research leads are:

1. **fmsave** — read-only FM26/FM27 save-file parser. Best lead for an offline save-analysis/import pipeline.
2. **fm-editor** — Rust live-memory editor for FM24/26/27. Best lead for entity discovery, version layouts, field mapping, process attachment and undo architecture.
3. **FMLibreEditor** — FM27 live editor explicitly targeting Windows Game Pass and Steam. Particularly relevant to Atlas's storefront compatibility and safe-write requirements.
4. **FMSuperScout** — high-performance scouting reader with a separated scanner/core/UI architecture, snapshots, filters and development tracking.
5. **gilet** — published FM26 save-format investigation with concrete container/decompression details. Useful for understanding the file format, but must be validated against our own sample builds before implementation.
6. **FMM Editor** — pre-game database editor; useful for database-file editing concepts, not a substitute for live-save editing.

These are research references, not dependencies. We must check licences and compatibility before reusing any code. In particular, GPL projects may impose obligations if code is copied or linked; the safer default is independent implementation from observed behaviour and documented interfaces.

## Repository/source register

### 1. fmsave — offline save parser

- Repository: https://github.com/rhiever/fmsave
- Evidence: public source repository and README.
- Claimed coverage: FM26 and FM27; supports players, contracts, staff, clubs, finances, facilities, competitions, fixtures, match statistics, injuries, tactics and other save tables.
- Design worth studying:
  - Read-only operation; no process-memory access and no save modification.
  - Typed immutable records and table/query API.
  - CSV, JSON, JSONL and DataFrame export.
  - Field-level coverage/status and validation reports; unknown values remain unknown rather than guessed.
  - Version-specific layouts and explicit warnings for unverified builds.
- Atlas opportunity: build an offline Save Inspector/import pipeline and use exported structured data as a test fixture for scouting, squad and analytics features.
- Limitation: it documents data stored in the save; it does not provide all names rendered from the installed game database, and some links/fields remain unavailable.
- Confidence: high that the repository documents these features; exact support for the user's installed FM27 build remains unverified.
- Licence: repository states MIT; verify the current LICENSE file before reuse.

### 2. fm-editor — live process editor

- Repository: https://github.com/barak14/fm-editor
- Evidence: source repository, README and Cargo manifest.
- Claimed coverage: FM24, FM26 and FM27; Windows and Linux/Proton.
- Design worth studying:
  - Version-specific memory layouts and build identification.
  - Separate storefront detection from game-version detection.
  - Entity search for players, staff and clubs.
  - Read/write abstraction per platform.
  - Undo stack for writes.
  - Read-only debug/MCP inspection tools for probing fields and pointer chains.
  - Explicit notes that a successful attach is not proof that a layout is verified.
- Important caution: its README says FM27 support is still in progress and some builds are uncalibrated. Do not treat all FM27 fields or storefronts as equally verified.
- Atlas opportunity: compare the memory adapter, schema/version profile and undo design against Atlas's R7.2.0 LiveMemoryEditService.
- Licence: GPL-3.0-or-later. Do not copy code into Atlas without an explicit licence-compatibility decision.
- Confidence: high for repository structure and stated design; exact per-build field coverage requires inspecting its version-specific docs and tests.

### 3. FMLibreEditor — FM27/Game Pass live editor

- Repository: https://github.com/tozmoz/FMLibreEditor
- Evidence: public source repository and DEVELOPING/INSTALL documentation.
- Claimed target: FM27 Windows Game Pass/Microsoft Store and Steam; macOS Steam.
- Design worth studying:
  - Explicit Game Pass and Steam build profiles.
  - Separate process reader and core library.
  - Read-only default on unrecognised/new game builds.
  - Before-write checks for save identity, object identity, value range, expected current value and read-back verification.
  - Undo and preview-oriented bulk edits.
  - Detects when a save is loaded/reloaded and suspends writes until data is refreshed.
  - Build-profile discovery and fallback scanning for selected structures.
- Particularly relevant: the docs describe the game UI save-load counter and why stale pointers can target a different career after a save switch.
- Atlas opportunity: use these documented safety invariants as an acceptance checklist for our own live editor. Compare Game Pass detection carefully against the user's Xbox/Game Pass install.
- Licence: repository states GPL-3.0-or-later. Do not copy code without a licence decision.
- Confidence: high for the documented architecture; the exact tested package/build combinations must be read from the repository's current profiles and test notes.

### 4. FMSuperScout — high-performance scouting/data extraction

- Repository: https://github.com/mavarobli/FMSuperScout
- Evidence: public source repository and README.
- Claimed coverage: FM26 and FM27, Windows and macOS.
- Design worth studying:
  - Split between scanner/core and local scouting application.
  - Fast bulk extraction from the running game's loaded database.
  - Search and filters over visible and hidden attributes, CA/PA, value and wages.
  - Snapshot history and development tracking.
  - Role ratings, squad analysis, shortlist and exports.
  - Large-save performance claims and tests.
- Atlas opportunity: benchmark data-loading, indexing, search responsiveness, snapshot model and development history. Compare its role-rating and value models to Atlas's Value & Upside Finder.
- Limitation: its README says the scanner is read-only; it is not a live editor.
- Licence: repository states MIT; verify current LICENSE before reuse.
- Confidence: high for declared architecture/features; performance numbers are project claims, not independently reproduced here.

### 5. gilet — FM26 save container investigation

- Repository: https://github.com/fifteen15-labs/gilet
- Evidence: public source documentation, including SAVE_FORMAT.md.
- Documented observation: an FM26 Career.fm sample is described as having an `fmf.` container header followed by concatenated Zstandard frames. The document explains why treating the entire tail as one ordinary Zstandard stream can fail and describes frame-by-frame decoding.
- Atlas opportunity: study the container parser and validation methodology for an offline save-inspection tool.
- Caution: this is an FM26 investigation. Do not assume the same container/version details apply unchanged to FM27 or every patch. Obtain test saves and verify header, frame boundaries, decompressed sections and checksums independently.
- Licence: not assessed in this pass; inspect before any reuse.
- Confidence: medium-high as a documented investigation; not yet reproduced by Atlas.

### 6. FMM Editor — pre-game database editor

- Repository: https://github.com/nyongrand/fmm-editor
- Evidence: public repository README.
- Focus: editing extracted Football Manager database files, with people, clubs, competitions and related models/parsers.
- Atlas opportunity: study database-file discovery, parsing, model relationships and pre-game editor workflows.
- Limitation: requires database files and is not a live-save memory editor. Do not conflate pre-game database edits with changes to an active career.
- Confidence: medium; inspect the current repository and supported FM database formats before relying on specific parsers.

### 7. Commercial reference: FMRTE 27

- Product/docs: https://www.fmrte.com/ and https://docs.fmrte.com/
- Evidence: vendor feature and getting-started documentation.
- Publicly described capabilities include searching and editing the running save, player/club/competition editing, quick search, player stats, transfer terms and undo.
- Atlas opportunity: use as a feature/UX benchmark only. Do not assume internal implementation details from product pages.
- Confidence: high for advertised features, not for undocumented internals.

## Cross-project architecture lessons

1. **Separate data acquisition from the UI.** Keep process readers, save parsers, domain models, search/indexing and WPF presentation independently testable.
2. **Treat source types as separate providers.** Offline save parser, live-memory reader, pre-game database reader and static game data have different coverage and freshness. Do not merge them into one undifferentiated data source.
3. **Version every schema.** Store exact game build, storefront, module fingerprint and schema/profile identity with each capture.
4. **Make uncertainty visible.** A field should be Verified, Observed, Inferred, Unsupported or Unknown. Never silently substitute FM26 offsets/meanings into FM27.
5. **Read-only first.** Build reliable scanning/export/validation before enabling writes.
6. **Guard every write.** Require a known build, current save identity, object identity, range validation, expected old value, successful read-back and a reversible journal.
7. **Handle save reloads.** Invalidate cached entity pointers/data when the loaded career changes; suspend writes until a fresh scan.
8. **Keep bulk operations previewable.** Show affected entities and before/after values, then commit as one undoable transaction where possible.
9. **Use fixtures and repeatable tests.** Preserve anonymised or synthetic records and test parsing, joins, filters, version selection and write guards without a live game.
10. **Keep Atlas's UI distinctive.** Learn from these tools' capabilities and safety, but retain the Aurora Frost WPF design and Atlas-specific workflow.

## FM Atlas-specific investigation priorities

| Priority | Investigation | Why it matters | Gate |
|---|---|---|---|
| P0 | Inspect R7.2.0 LiveMemoryEditService against the above safety invariants | Protect existing implementation | Source import required |
| P0 | Confirm FM27 Steam vs Game Pass process/module identification | User has Game Pass; wrong profile risks invalid reads/writes | Exact installed build details and safe read-only test |
| P0 | Compare FM27 build profiles and field maps across projects | Avoid stale offsets and false support claims | Source/docs inspection plus matching build |
| P1 | Evaluate fmsave for offline save import | Gives Atlas robust scouting/analytics data without memory writes | Test copy of a save and parser validation |
| P1 | Evaluate FMSuperScout data model and indexing strategy | Faster search and development tracking | Source review and benchmarks |
| P1 | Study gilet container parsing | Enables diagnostics and offline save tools | Independent FM26/FM27 sample validation |
| P2 | Study FMM Editor database relationships | Improves pre-game database/graphics workflows | Confirm FM database format/version |
| P2 | Benchmark FMRTE UX workflows | Helps feature parity and usability | Manual product walkthrough |

## What has NOT been done

- No external repository has been cloned into or merged with FM Atlas.
- No external source code has been copied into the project.
- No FM executable, DLL, process memory or user save has been inspected in this research pass.
- No FM27 Game Pass offsets or field layouts have been independently verified.
- No third-party program has been installed or executed.
- No live write or save modification has been attempted.
- No build or performance claim has been independently reproduced.

## Next research actions

1. Once the R7.2.0 source is in GitHub, compare its memory reader, build profiles, search engine and live writer against these references.
2. Read the detailed per-version memory-layout and development docs in fm-editor and FMLibreEditor.
3. Inspect fmsave's parser modules and field-coverage registry; decide whether to integrate as a separate tool/library or independently implement an adapter.
4. Inspect FMSuperScout's scanner/core/UI boundary and benchmark strategy.
5. Build a compatibility matrix for FM27 Steam and Game Pass using exact build numbers and module fingerprints.
6. Only after read-only verification, design a disposable-save acceptance test for write/verify/undo.

## Sources

- https://github.com/rhiever/fmsave
- https://github.com/barak14/fm-editor
- https://github.com/tozmoz/FMLibreEditor
- https://github.com/mavarobli/FMSuperScout
- https://github.com/fifteen15-labs/gilet
- https://github.com/nyongrand/fmm-editor
- https://www.fmrte.com/
- https://docs.fmrte.com/


## 8. FM27 tools, memory reading and graphics integration (13 November 2026)

A detailed follow-on review of user-supplied links and uploaded files is recorded in [FM27-TOOLS-AND-GRAPHICS-REVERSE-ENGINEERING.md](FM27-TOOLS-AND-GRAPHICS-REVERSE-ENGINEERING.md).

Key additions:
- FMSuperScout source documents a read-only Windows scanner using OpenProcess, VirtualQueryEx and ReadProcessMemory, versioned field layouts, cached module images, bounded parallel reads and optional PSS snapshot retry. Its scan speed claims remain unverified by Atlas.
- FMLibreEditor source documents FM27 Game Pass/Steam profiles and conservative live-write checks: save identity, object identity, value range, expected old value, read-back, and write suspension across save reloads. GPL-3.0; no code copied.
- FM Player Analyzer 2.2.3 source ZIP was inspected. It contains an FM27 save parser (container/trailer/Zstandard sections), scouting-knowledge-aware import, save watcher and GraphicsIndex implementation. GPL-3.0; no code copied.
- The GraphicsIndex design recursively parses pack config.xml files and maps person/club/nation IDs to image filenames, with a compact cache invalidated by config file size/mtime. Atlas should implement an independent read-only graphics library and preserve the user's pack files.
- FaceDrop uses a separate Documents/.../facedrop location; standard graphics packs and FaceDrop should be modelled as different providers.
- SEGA's official guidance confirms the Sports Interactive user-data folder is configurable in FM and that the Steam/Epic --user_data_location override is unavailable for Microsoft Store/Xbox FM. Game Pass must not inherit a hard-coded Steam path.
- TalentSeer 27.1.2 upload contains a README and closed executable but no source. Its hidden-stat, role-rating and editing claims remain product claims pending independent tests.
- Teknik Oda, FaceDrop, Scout Reporter and Customize Editor pages were reviewed. Scout Reporter is CSV/browser-local; Teknik Oda is a BepInEx match-analysis route; Customize Editor is an alpha Pre-Game Editor skin/mod; FaceDrop is a separate face provider.
- FM Dossier documentation confirms an external, read-only live-memory reader with local processing; its source is proprietary.
- SaveMyTime ZIP contains a .NET/Mono DLL and two .fmf schedule templates. Static strings suggest substantial training-schedule discovery/verification logic, but behaviour is not established.
- All supplied EXE files were statically identified only and were not executed or installed. No user save or live game process was inspected.
- Exact FM27 Game Pass build/profile compatibility remains unverified for Atlas. The R7.2.0 source import remains a prerequisite for comparing Atlas's existing reader and LiveMemoryEditService against these projects.


## 9. Additional FM27 data export research (13 November 2026)

- **FM View Exporter**: https://github.com/ElectroHugin/FM-View-Exporter — open-source GPLv3 BepInEx plugin for FM27 Windows/Steam. It exports currently visible player lists to CSV/HTML via F9, including all rows by scrolling the view. This is a UI-level export, not a hidden-memory reader; unscouted values stay hidden. Large searches take longer and FM's visible result list is capped. Useful as a safe import/test-fixture route, but it is not a replacement for a full database scanner.
- **FMRTE 27**: https://www.fmrte.com/ and https://docs.fmrte.com/ — current commercial feature benchmark for live editing, search, player stats, transfer terms, mass changes and undo. Public docs establish advertised features only; internal implementation remains proprietary.
- **FMSuperScout installer verification**: the uploaded `FMSuperScout-Setup(1).exe` SHA-256 exactly matches the GitHub v1.7.3 release asset checksum. It was not executed.


## 10. Game Pass compatibility qualification (13 November 2026)

- **FMLibreEditor** explicitly targets Windows Xbox/Game Pass and Steam; retain it as the primary external reference for Game Pass-safe live-memory access, while still validating Atlas against the user's exact current FM build.
- **FMSuperScout** troubleshooting documentation says Steam is fully tested; Epic/Xbox Game Pass should work because the scanner is storefront-independent, but was not tested end-to-end by its author. Do not call it verified Game Pass support.
- **FM View Exporter** documents Steam for Windows; it should not be presented as a known Game Pass-compatible plugin.
- **FM Dossier** documentation says FM27 has been tested on Steam; other official store editions are checked on each read but have not been tested by the developer.
- These qualifications matter because Atlas's intended target is Xbox/Game Pass. A working Steam offset/profile is not sufficient evidence for Game Pass.

## Source-level reverse-engineering follow-up — 2026-11-13

The initial register above was checked against current public source files, not just project README claims. This is still desk research: no third-party code was copied into Atlas, no third-party executable was run, and no user's game process/save was accessed.

### fmsave: trust contract and build support

Current public source inspected:
- `src/fmsave/_layouts/fm27_27_0_0.py`
- `docs/guides/trust.md`
- `README.md`

Important correction to any broad “FM27 supported” claim: the current README documents the normal FM27 reader for build `27.0.0+2486695`. Other FM27 builds are unverified and may borrow an FM27 layout only when section schemas match; the validation report warns about the borrowed layout. A registered build means a layout exists, not that every field meaning has been checked against the game.

The trust guide makes three distinct evidence dimensions explicit:
1. **Field meaning:** verified against the in-game UI or unconfirmed.
2. **Coverage:** how often the field is present (a missing value may mean omitted data, failed join or unsupported decoding).
3. **Reader integrity:** structural and consistency checks passed or warned.

Unknown values remain `None`, not fabricated zeros. Reader checks distinguish blocking integrity failures from advisory population/coverage warnings. The parser deliberately preserves stored money units and historical finance periods rather than pretending they are current display-currency totals.

Atlas implication: store field-level status, presence/coverage and reader validation separately. Do not promote every field just because the overall save parser reports a recognised layout. The current Atlas `PlayerRecord` has a record-level `DataOrigin`, while field evidence is stored separately; a future canonical field registry should preserve source, build, visibility, unit, confidence, freshness and validation per field.

### fm-editor: versioned memory schemas and evidence

Current public source inspected:
- `README.md`
- `docs/fm27/memory-layout.md`
- `src/fields/fm27/player.rs`
- Repository tree under `src/fields/fm24`, `src/fields/fm26`, `src/fields/fm27`

The source separates field definitions by game version and records witness comments next to individual fields. The FM27 layout document explicitly says that its initial schema inherited from FM26 and that only the fields listed as verified were checked on FM27; inherited offsets remain unverified. It records the tested build, save context, human-readable witnesses, offsets, widths, pointer chains and the exact UI label observed.

The project also separates game version from storefront detection. Its README notes that one FM27 Game Pass package has been brought up for a specific Store package and that unmapped Game Pass addresses stay disabled; an attach is not proof that every field layout is valid. The editor has a per-field whitelist and an undo stack, and it explicitly warns that writes affect the running game's memory.

Atlas implication: the current build-profile registry is a useful first gate, but a module size/timestamp match must never become blanket field certification. Maintain per-version field maps and independent UI witnesses. Keep unsupported build/storefront combinations read-only. A field needs evidence about its value and meaning, not only a plausible address.

### FMLibreEditor: modular UI and strict write evidence

Current public source inspected:
- `README.md`
- `CONTRIBUTING.md`
- `app/src/CommandPalette.tsx`
- Repository tree under `app/src` and `app/src-tauri`

FMLibreEditor uses a Rust core with a React/TypeScript UI and a Tauri bridge. The UI is split into distinct player/staff/club browsers, profiles, comparisons, filters, scouting, saved filters, command palette, mass-edit and diagnostics components. Its Ctrl+K palette searches both entities and commands, keeps keyboard focus, supports arrows/Enter/Escape, debounces requests and offers result limits.

Its contribution guide states that new writable fields need two independent confirmations and a documented method for finding the offset. It also describes testing across storefronts and reporting the exact FM version. The README labels the application beta and calls for offline single-player backups. This is a useful safety and product-structure reference, but the project is GPL-3.0-or-later.

Atlas implication: the biggest design lesson is not to copy the React/Tauri stack. It is to break the large MainWindow into independently testable feature components/services, add a real command palette, and enforce independent evidence before any new writable field is exposed.

### FMSuperScout: scanner/core/UI split and read-only acquisition

Current public source inspected:
- `README.md`
- `scanner/Program.cs`
- `core/MemScan.Win.cs`
- Repository tree under `scanner`, `core`, `plugin`, `app`, `mac`, `test`

The repository separates an external scanner, shared scan core, optional in-process plugin and local application. The Windows process reader requests only `PROCESS_VM_READ` and `PROCESS_QUERY_INFORMATION`; it does not request VM write rights. It can use a Process Snapshotting API copy-on-write clone as a fallback when a consistent read is needed, but explicitly notes the memory and performance cost. It reuses large image buffers and limits parallel readers to reduce large-object-heap pressure and game-process memory overhead.

The app keeps scan snapshots and advertises development deltas, intake radar and saved filters. Those are project features/claims; Atlas has not independently benchmarked their performance or validated their calculations.

Atlas implication: retain the read-only-first architecture, avoid asking for write privileges in discovery paths, and measure scan memory/latency against large saves. Snapshot consistency should be an explicit, opt-in fallback with a resource-cost warning rather than a default that slows FM.

### Cross-project conclusions

1. **Version and storefront are separate axes.** A supported FM27 version does not automatically prove Game Pass/Steam module addresses are interchangeable.
2. **Build registration is not field verification.** Track each field's witness and status separately.
3. **Missing is not zero.** Keep null/unknown distinct from a real zero, and show coverage alongside results.
4. **Reader integrity is not semantic correctness.** Structural checks, field meaning and presence coverage are different.
5. **Read-only scanning should use least privilege.** Do not request write access until an explicitly certified edit operation.
6. **Undo is a first-class safety feature.** It needs original bytes/current-value preflight, session identity, read-back and recovery evidence.
7. **Scouting tools derive value from history.** Snapshots unlock growth, development and intake comparisons, but only when player identity is stable within a save and cross-save identity links are evidenced.
8. **Modular architecture pays off.** Scanner, data model, query engine, UI components and platform adapters should be independently testable.
9. **Licensing is a hard boundary.** FMLibreEditor and fm-editor are GPL-3.0-or-later; use as architectural research only unless a deliberate licensing decision is made. Do not transplant source code into a differently licensed/distributed Atlas release.

These findings have been added to the Stage 1/2 audit as recommendations. They do not mean Atlas has implemented the referenced products' feature sets.

### Additional safety and implementation details verified in FMLibreEditor

The current `docs/DEVELOPING.md` was inspected directly. It documents a useful pre-write contract: before every write, check (1) the same save is still loaded, (2) the object still belongs to the same person, (3) the target value is valid for the field, (4) the current value is plausible for that field/layout, and (5) read-back equals the written value. Any failed check aborts the write.

Its save-change guard is particularly relevant: it reads the game's save-load counter and suspends all writes/freezes after a save load or return to the main menu until the data is reloaded. The docs explain why UID and address checks alone are insufficient: another career can contain the same UID at a reused address. It also documents per-build profiles and a read-only fallback when profile locations fail validation. The in-game date discovery requires two independent witnesses rather than accepting any plausible date-like number.

This validates several of Atlas's existing safety concepts (session generation, identity re-resolution, current-value preflight, read-back and closed write certification) but also highlights that a Game Pass module profile should not be treated as universal. FMLibreEditor documents a tested Store package 0.9.2435.0 and separately records build-specific static locations. Atlas's corresponding profile entry is a useful research cross-check, not independent certification of the user's own installation.

### Additional scanner implementation detail verified in FMSuperScout

The Windows scanner source `scanner/Program.cs` selects a real FM process by checking for the adjacent `fm_Data` directory, handles access-denied/elevated-process cases distinctly from “game not running”, and opens the process with read-only rights. The core's `MemScan.Win.cs` uses `ReadProcessMemory` and `VirtualQueryEx`; it optionally captures a copy-on-write process snapshot for a consistent retry, while explicitly accounting for commit-memory cost and slower writes to FM memory during the snapshot. It reuses large image buffers to reduce LOH growth and caps parallel workers.

Atlas opportunity: keep process discovery diagnostics explicit (not-running vs access-denied vs unsupported build), use least privilege for scanning, and test large-save memory/latency. The snapshot technique is a research option, not something to adopt without profiling and separate acceptance.

### fmsave version pin cross-check

The public fmsave `pyproject.toml` and `CHANGELOG.md` were checked on 13 November 2026. The current package is **0.12.3**, released 12 November 2026, under MIT. Atlas's `FmsaveBridgeService` expects version **0.12.3**, so its pinned bridge version currently matches the public parser release.

The 0.12.3 change log says an available FM27 layout may be tried when a section schema is unfamiliar, with a warning when another schema is borrowed. This is a compatibility attempt, not verified support. The 0.12.1 change log also documents a specific training/mentoring layout adjustment for FM27 build `27.0.1+2494687`. The current FM27 layout registry still identifies `27.0.0+2486695` as its registered build; other builds remain unverified and must be checked via the parser's validation report.

Atlas implication: keep the exact 0.12.3 version pin, expose parser validation warnings and per-reader coverage to the user, and record the actual save build/schema in the imported snapshot. Never translate “parser opened the save” or “known build” into “all imported fields verified.”

### FMSuperScout valuation-model review

The public `docs/value-model.md` was inspected. It distinguishes three different concepts that many tools blur together:

- **Observed FM transfer value:** used directly when the scanner reads a concrete value from memory.
- **Estimated market value:** a calibrated reputation/age/contract-length model for records without an observed value. The project documents a model revision after a CA-driven model materially overshot mid-range players; it also states that the model underestimates strong, low-fame players and gives teenage estimates a wider uncertainty band (about ±55%).
- **Estimated asking/transfer fee and interest:** separate buyer/seller heuristics. Asking price depends on listing status, contract, buyer club reputation and a wage-rank squad-status proxy; interest is explicitly heuristic because ambition/loyalty/playing time are not available in the dump.

Atlas implication: do not make the Transfers route or Value Finder present an estimated fee/value as a real FM value. If valuation modelling is added later, use observed values first, a separately named estimate second, explain its inputs/cohort and sample size, show an uncertainty band, and state unavailable inputs. Do not use CA alone as a value proxy or infer exact asking price/interest from missing data.
