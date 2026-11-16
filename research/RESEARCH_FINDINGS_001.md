# FM Atlas — Research Findings 001

**Date:** 2026-11-16  
**Status:** Static source/documentation findings. No FM27 process was attached and no live memory write was performed.

## 1. FMLibreEditor — live editing architecture and Game Pass evidence

Sources inspected:
- [README](https://github.com/tozmoz/FMLibreEditor/blob/main/README.md)
- [Developing guide](https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md)
- [Game Pass 0.9.2435.0 research](https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-gamepass-0.9.2435.md)
- [fmlibre-mem crate](https://github.com/tozmoz/FMLibreEditor/tree/main/crates/fmlibre-mem)
- [fmlibre-probe crate](https://github.com/tozmoz/FMLibreEditor/tree/main/crates/fmlibre-probe)

Documented architecture:
- FM27 is a Unity/IL2CPP interface; the game database/simulation resides in native `game_plugin.dll`.
- FMLibreEditor runs as a separate process and its Windows implementation uses process memory APIs.
- Its guide describes reads and writes into the game heap, not code patching.
- It describes a read-only probe and separate core/memory crates.
- Before each write it checks save identity/generation, object identity, valid new value, expected current value/layout, and read-back.
- It detects save-load generation changes and blocks writes until data is reloaded. This prevents stale pointers from silently targeting another career.
- It falls back to read-only for unrecognised or unverified builds.

Game Pass evidence:
- The inspected recon note describes package `SportsInteractive.FootballManager27_0.9.2435.0_x64__5w3tn6tb6stnm`.
- It records a particular Game Pass module image size and link timestamp, and field/static addresses for that specific build.
- It reports comparison of a defined set of entities/witnesses across Game Pass builds 0.9.2409.0 and 0.9.2435.0.
- This is build-specific evidence, not a universal FM27 layout. Do not apply those addresses to another package version or to Steam.

FM Atlas implications:
1. Detect exact storefront and exact module build before enabling any read/write feature.
2. Use a dedicated read-only probe to discover and validate identity structures first.
3. Store per-build field layouts separately from domain models.
4. Capture a monotonically changing save-load generation and invalidate all cached pointers on change.
5. Keep write access disabled when the build, identity, field mapping or current value is uncertain.
6. Use the five preconditions as acceptance tests, not as a guarantee that an unverified mapping is correct.

Licence: GPL-3.0-or-later. Research only; no implementation copied.

## 2. fm-editor — memory layout and version confidence

Sources inspected:
- [README](https://github.com/barak14/fm-editor/blob/main/README.md)
- [FM27 memory layout](https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md)

Key documented cautions:
- FM27 memory layout began as a copy of an FM26 schema.
- The project says only fields explicitly listed as verified have been checked against FM27; other offsets are inherited leads.
- The notes distinguish module fingerprint, storefront detection and field-level evidence. A successful attach or recognised module hash does not establish every field's meaning.
- The README describes a specific Game Pass Store package bring-up where players, staff, clubs, date and manager read correctly and one edit/Undo was checked in-game. That is one build/test context, not proof of broad parity.
- The repository's supported-version table marks FM27 as in progress and Game Pass support as tied to a specific Store package. Unmapped features remain disabled.

FM Atlas implications:
- Field-level verification must be independent of module identification.
- Keep a per-field evidence record: source, exact build, witness, type/width, range, confidence, read/write status and regression test.
- Distinguish Steam and Game Pass profiles even if some object structures are shared.
- Do not infer support for staff/club/contract editing from successful player reads.

Licence: GPL-3.0-or-later. Research only; no implementation copied.

## 3. FMSuperScout — high-throughput read-only scanning

Sources inspected:
- [README](https://github.com/mavarobli/FMSuperScout/blob/main/README.md)
- [Scanner project](https://github.com/mavarobli/FMSuperScout/blob/main/scanner/FMSuperScout.Scanner.csproj)
- [License](https://github.com/mavarobli/FMSuperScout/blob/main/LICENSE)

Documented architecture:
- Two parts: a running-game reader/scanner and a local search/viewer application.
- Windows scanner project targets `net8.0-windows`, is self-contained for publishing, and describes `ReadProcessMemory` only.
- README claims rapid extraction of large player/staff populations, including CA/PA, hidden personality fields and transfer values.
- README explicitly says the scanner is read-only and never writes to the game.
- The application includes stacked filters, staff role searches, snapshots, growth tracking, shortlists, comparisons, role ratings and squad analysis.

FM Atlas implications:
- Benchmark scanning and indexing separately from UI rendering.
- Keep read-only acquisition independent of the future write adapter.
- Measure scan time, record counts, missing-field coverage and index time on a representative save.
- Snapshot history can support later development trends, but is deferred until the basic editor works.

Licence: MIT. Candidate for selective reuse only after source-level review and preserving copyright/permission notices. No code copied in this pass.

## 4. fmsave — offline save parsing and trust model

Sources inspected:
- [README](https://github.com/rhiever/fmsave/blob/main/README.md)
- [What a save holds](https://github.com/rhiever/fmsave/blob/main/docs/guides/what-a-save-holds.md)
- [License](https://github.com/rhiever/fmsave/blob/main/LICENSE)

Documented coverage:
- FM26/FM27 read-only save parser.
- Player records include 52 attributes, eight personality attributes, CA/PA, value, reputation, condition, traits and contract data.
- Also documents staff, contracts, suspensions, clubs, finances, sponsorships, facilities, stadiums, fixtures, competition rules, season statistics, player match statistics, history, training and mentoring.
- FM27 registered layout covers build `27.0.0+2486695`; other builds may be marked unverified depending on reader layout.
- It reports field-level coverage and uses `None` for missing/unreadable values rather than guessing.
- Save-local `uid` and cross-career `unique_id` are different identifiers.
- Money values are retained in the unit stored in the save; they are not automatically converted to displayed currency.
- Some data is absent or incomplete by design (for example some match histories and staff/club fields).

FM Atlas implications:
- Preserve missing/unknown values rather than replacing with zero.
- Store both save-local and cross-career identifiers distinctly.
- Keep raw stored units and display currency metadata separate.
- Use reader coverage as an input to field provenance and import diagnostics.
- Use disposable save copies for integration tests.

Licence: MIT. Candidate for integration/port after technical/dependency review; no code copied in this pass.

## 5. FM Player Analyzer — profile, import and graphics research

Sources inspected: uploaded `FM-Player-Analyzer-2.2.3(2).zip`, including root README, desktop README, `PROFILE.md`, `GraphicsIndex.cpp`, `GraphicsLibrary.cpp`, `PlayerProfilePage.cpp`, `Fm27SaveImport.cpp`, database and test files.

Findings:
- The desktop core is separate from the Qt Widgets UI and is described as headless-testable.
- SQLite stores player data, imported profile settings and documents; database schema is versioned.
- Graphics indexing parses `config.xml` records and resolves player portrait, club logo and nation logo mappings by numeric IDs.
- The index has a cache and tests for parsing, scanning and cache round-trip.
- The player profile implementation has a broad information surface and performance/statistical widgets; this is a functionality reference, not the approved FM Atlas layout.
- The FM27 save reader/import path has dedicated unit tests and optional real-save end-to-end tests.
- The project README describes FM27 career reading as read-only.

FM Atlas implications:
- Adopt a separate core/domain layer with UI-independent tests.
- Make graphics lookup an independent index/cache service using IDs from the save.
- Keep save parser and live memory providers separate.
- Use schema migrations and per-save profiles for local Atlas data.
- Use the FM27 attribute and graphics import tests as acceptance-test references.

Licence: GPL-3.0. Research only; no source copied.

## 6. FM View Exporter — canonical table columns

Sources inspected:
- [README](https://github.com/ElectroHugin/FM-View-Exporter/blob/main/README.md)
- [FM27 column catalogue](https://github.com/ElectroHugin/FM-View-Exporter/blob/main/docs/SPALTEN-FM27.md)
- [Machine-readable FM27 column catalogue](https://github.com/ElectroHugin/FM-View-Exporter/blob/main/docs/columns/fm27.csv)

Findings:
- Documents 375 FM27 player table columns and 377 Insert Column menu entries.
- Headers are kept in the order Football Manager shows them, including duplicate names where the game has them.
- Some columns are icons/derived labels and may export blank.
- The source warns the game only populates approximately the first 255 columns in a view.
- This is a strong reference for field naming, table order and visible export semantics, but it is not the full player-profile attribute order or proof of hidden-field semantics.

FM Atlas implications:
- Use the catalogue to build the visible table-column schema and import mappings.
- Preserve duplicate columns by stable internal IDs rather than header name alone.
- Do not assume table column order is the same as the full player-profile attribute-group order; verify the latter against FM27/FMRTE profile screens separately.

Licence: GPL-3.0. Public schema research only; no plugin code copied.

## 7. Commercial/reference packages

- **FMRTE 27.0.1 build 12:** uploaded portable package contains executable and UI/release assets, not source. The 25 `whatsnew` images indicate feature areas such as edit history, mass-edit presets, clone centre, loans, transfers, squad registration, tactical understanding and potential attributes. Use the user's screenshots and observable product behaviour as UX/feature evidence; no binary code is reused.
- **TalentSeer 27.1.2:** uploaded package contains README + executable + notices, not source. Its README describes live reading/editing, player/staff search, CA/PA, hidden fields, positions, potential projection and saved filters. Treat claims as product documentation, not independently verified internals.
- **Genie Scout 26:** uploaded ZIP contains one installer executable; no source.
- **FM Dossier 1.15.4:** uploaded installer only; no source.
- These tools can inform feature coverage and user workflows, but do not decompile, redistribute or copy proprietary code/assets.

## 8. Confidence and unresolved questions

| Topic | Current status | Next evidence required |
|---|---|---|
| Game Pass process detection | Documented for specific packages | Identify user's actual installed FM27 package and module metadata. |
| FM27 object layouts | Partial, build-specific evidence | Independent field-by-field witness and tests. |
| Read-only live scan | Documented in public projects | Compare records against user's FM27 save. |
| Live writes | Public projects demonstrate selected operations | Independent implementation, disposable save, field-level preflight and in-game read-back. |
| All visible attributes | 52 fields documented by fmsave | Verify exact profile display order and applicability by position. |
| Hidden attributes | Multiple projects expose subsets | Verify meaning, storage and build-specific mapping separately. |
| Staff/club editing | Some public tools advertise support | Map each field, storefront and build independently. |
| Graphics | config.xml approach documented and tested in FMPA | Validate the user's actual graphics folder/config pack. |
| Player profile sub-tabs | Feature benchmark from FMRTE/TalentSeer | Complete FMRTE player feature audit and match approved REF-004 layout. |


## 9. Follow-up source inspection — FMLibreEditor Game Pass build and FMSuperScout field layout

Additional exact source files inspected after the first research pass:

- [FMLibreEditor FM27 Game Pass 0.9.2435.0 recon](https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-gamepass-0.9.2435.md)
- [FMLibreEditor player attribute groups](https://github.com/tozmoz/FMLibreEditor/blob/main/app/src/playerAttributes.ts)
- [FMLibreEditor probe entry point](https://github.com/tozmoz/FMLibreEditor/blob/main/crates/fmlibre-probe/src/main.rs)
- [FMSuperScout Fields.cs](https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs)
- [FMSuperScout external scanner](https://github.com/mavarobli/FMSuperScout/blob/main/scanner/Program.cs)

### FMLibreEditor Game Pass 0.9.2435.0

The recon note is explicitly for package:
`SportsInteractive.FootballManager27_0.9.2435.0_x64__5w3tn6tb6stnm`.

It records:
- `game_plugin.dll` image size `0x17969000` and link timestamp `0x6AE8CF77` for 0.9.2435.0.
- Earlier 0.9.2409.0 had image size `0x175DA000` and timestamp `0x6AE563A1`.
- Several static pointers moved by approximately `0x2CC000`, and not all relative distances stayed identical.
- The authors report that player/staff/club object layouts and a set of 220 witnesses did not move between those two specific Game Pass builds.
- The same save was read after loading on both builds; the note records detailed comparison counts and a small number of changed/normalised values.

FM Atlas consequence: a module fingerprint is not enough to hardcode old static addresses. Build identification and pointer discovery/calibration must be separate. The user's installed package must be identified before any profile is considered applicable. This evidence does not prove that the user's current installation is 0.9.2435.0.

### FMSuperScout memory layout source

The inspected `Fields.cs` documents:
- a version-specific `Fields` profile, with FM27 27.0 derived from FM26 and a specific manager-offset change;
- CA/PA and reputation values as 16-bit fields in its documented layout;
- 15 player position bytes;
- a 55-byte raw player-attribute block, with the listed attribute values stored multiplied by five;
- separate hidden-attribute offsets and separate left/right foot offsets;
- eight personality bytes in the person block;
- separate player and staff attribute blocks;
- club/team/contract relationships and selected transfer-deal structures.

The source comments state which observations were checked against the author's running FM27 27.0 build. This is valuable independent research, but it is not an FM Atlas mapping and is not permission to write those offsets into the user's game. The 55-byte storage block is not a UI attribute count and not the FM profile display order.

The FMSuperScout external scanner explicitly describes itself as read-only and uses a separate process to inspect the game. It also distinguishes access-denied/elevated-process conditions from the game not running. FM Atlas should preserve that diagnostic distinction in its own connection UX.

### Attribute catalogue interpretation

FMLibreEditor's `playerAttributes.ts` explicitly describes its arrays as the order FM lists them, by group. This is the current best source for the initial profile display catalogue:
- Technical: 14
- Mental: 14
- Physical: 8
- Goalkeeping: 13
- Hidden: 5
- Feet: 2

First Touch and Passing occur in both Technical and Goalkeeping. Across all profile groups this produces 52 unique non-foot attributes. The final visual order remains provisional until checked against the user's FM27 profile and FMRTE.

### TalentSeer uploaded user guide

The uploaded `TalentSeer-27.1.2-win-x64.zip` contains a README, compiled executable and third-party notices, not source code. Its README claims:
- live FM27 reading and editing;
- player and staff tabs, shortlists, stacked filters and saved searches;
- a wide player profile with FM-style attribute columns, positions/roles, contract and potential;
- attribute and personality rules;
- clubs/Club Centre, squad view, Best XI, comparison, transfer/contract and injury information;
- a potential projection/outlook formula explicitly described by the product as Talent Seer's estimate, not Football Manager's own development.

These are claims in the vendor/user guide, not independently tested behaviour or knowledge of its private implementation. They are useful feature-discovery references only. FM Atlas must not present TalentSeer's estimates as official FM values or copy its proprietary formulas.

## 10. Fresh domain catalogue implementation

FM Atlas now contains `src/FMAtlas.Domain/Players/PlayerAttributeCatalog.cs`:
- stable string keys and display labels;
- group membership and within-group order;
- separate outfield and goalkeeper profile group sets;
- shared First Touch and Passing definitions;
- five hidden fields kept separate;
- left/right foot kept separate from the 52 unique non-foot fields;
- evidence status defaulted to `Documented`;
- no memory offsets or write permissions embedded in the domain catalogue.

Tests were added in `tests/FMAtlas.Tests/PlayerAttributeCatalogTests.cs` for unique field count, group overlap, outfield/goalkeeper group selection, hidden/feet separation, technical order and key lookup.

This catalogue is a documented schema foundation only. It does not prove live reads, field-level memory mapping, write support or final visual ordering.
