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
