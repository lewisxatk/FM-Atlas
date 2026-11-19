# FM Atlas — Uploaded Research Package Inventory

**Inventory date:** 2026-11-16  
**Working branch:** `rebuild/fresh-foundation-001`  
**Handling:** Static inspection only. No installer or application executable was run.

## 1. Newly uploaded packages

| Package | Size | SHA-256 | Contents / classification | Use in FM Atlas |
|---|---:|---|---|---|
| `FM-Player-Analyzer-2.2.3(2).zip` | 10,007,467 bytes | `3637b5aa1736d89ece8e752a78e73bf6c1a04b379ab933df4211bfe9cd41dd61` | 419 ZIP entries; 384 extracted files; native C++20 / Qt 6 desktop source, tests, docs and assets. GPL-3.0. | Read-only save parsing, player profile presentation, graphics index, typed database, import pipeline and test design research. No source copied. |
| `TalentSeer-27.1.2-win-x64.zip` | 58,456,433 bytes | `a015be52fcf9891dc7b58e78920fcbf0a8e7afa3084d444dbdcf4e153b3a0fed` | 3 entries: README, compiled `TalentSeer.exe`, third-party notices. No source tree. | Black-box feature benchmark for live data, profiles, CA/PA, hidden attributes, staff, filters and player tabs. Internal algorithms not assumed or copied. |
| `FMRTE 27.0.1-build12.zip` | 65,611,308 bytes | `91b9739ecd9c0cafc115ab39dac45e1b4dc311fea981beb282b66fb44fcb591f` | 495 entries; portable application binaries and resources. Includes 25 `Assets/whatsnew` images. No source code. | User-approved UI/workflow benchmark. Inspect observable features and supplied release imagery; no binary/code extraction or reuse. |
| `genie26g_setup_26.3.2_b1538.zip` | 40,830,666 bytes | `efddc103e0e49fe82c92c67949e25e67c7443b075338498907c385ff63c40085` | One installer executable inside ZIP. No source. | Black-box feature benchmark only; no installer executed. |
| `FMSuperScout-Setup(2).exe` | 28,913,584 bytes | `96ee55a62c05ef7ef4123256a9aaa09410a52bccf979c6c93834fb1ed2738bae` | PE32 Windows GUI installer; static strings identify Inno Setup 6.7.0. | Installer metadata only. The public MIT source repository is the research target. |
| `FMLibreEditor-Setup(2).exe` | 3,492,024 bytes | `5954fc6fce28ac698f03e44c72e7266a37a26ca13bf7257aee9d7f25c6271151` | PE32 Windows GUI installer; static strings identify NSIS 3.13. | Installer metadata only. The public GPL-3.0-or-later source repository is the research target. |
| `FM-Dossier_1.15.4_x64-setup(2).exe` | 11,041,752 bytes | `343e9f6ace7d73815d4da71fa5d97d8c8afed7f74b46ecb73be4e53bb0051b46` | PE32 Windows GUI NSIS 3.11 installer. | Installer metadata only; public/product documentation research separately. |

The executable hashes are inventory identifiers, not a malware verdict or publisher-signature verification.

## 2. FM Player Analyzer source ZIP findings

- The project README describes FM27 save reading as read-only: it reads a career file and does not write to it.
- Desktop implementation is C++20 / Qt 6.8 with CMake 3.28+, a headless-testable `fmcore` library and a separate Qt Widgets application.
- The source tree contains 273 desktop source files, including save readers/importers, SQLite database, graphics index, player profile, squad analysis, comparison and tests.
- The desktop README describes HTML/CSV import, player profile and comparison, development charts, Best XI, squad matrix, tactic explorer, national-team mode, transfer/loan management and role/tactic editors.
- `PROFILE.md` documents per-save settings and database profile storage. `Database.cpp` declares schema version 16.
- Graphics indexing parses Football Manager `config.xml` mappings and resolves player portrait, club logo and nation logo mappings by numeric IDs. The index has a cache and tests for parsing, scanning and cache round-trip.
- `PlayerProfilePage.cpp` is a useful reference for profile information breadth and charts, but its layout is not the approved FM Atlas profile design.
- License is GPL-3.0. Treat source as research-only unless FM Atlas explicitly adopts GPL-compatible distribution and fulfils the corresponding obligations.

## 3. TalentSeer package README findings

The packaged README describes a live FM27 reader/editor for Windows 10/11 that reads the running game after a save is loaded. It advertises player and staff CA/PA, hidden attributes, personality, position ratings, Players/Staff/Shortlist tabs, stacked filters, saved searches and wide player profiles with FM-style attribute columns, positions/roles, contract and potential.

The ZIP contains a compiled executable, not its implementation source. These are package README claims, not independently validated internal behaviour. Do not copy or present TalentSeer's private formula as FM's formula.

## 4. FMRTE portable package findings

The archive contains application binaries/resources rather than source. Its 25 release-image assets include assistant/review, career plans, clone centre, edit history, mass-edit presets, multi-column sorting, loans, transfer rules, squad registration, team selection, tactical understanding, potential attributes, manager job offers/moves, wage contributions, work permits and youth intake.

These filenames are an initial feature-discovery index, not a complete feature inventory. The user's screenshot remains the authoritative advanced-search layout. Additional FMRTE player-profile tabs and editing workflows require a documented feature audit.

## 5. Repositories kept central

| Repository | Licence checked | Research focus | Reuse status |
|---|---|---|---|
| [FMLibreEditor](https://github.com/tozmoz/FMLibreEditor) | GPL-3.0-or-later | FM27 Game Pass/Steam process profiles, memory read/write safeguards, save-generation invalidation, undo and field evidence. | Research only; no code copied. |
| [fm-editor](https://github.com/barak14/fm-editor) | GPL-3.0-or-later | Memory layouts, entity discovery, player/staff/club models and undo. | Research only; no code copied. |
| [FMSuperScout](https://github.com/mavarobli/FMSuperScout) | MIT | Read-only .NET 8 scanner, bulk extraction, player/staff search, snapshots, shortlist and role analysis. | Candidate for selective reuse only after file-level review and notices; currently research only. |
| [fmsave](https://github.com/rhiever/fmsave) | MIT | FM26/FM27 offline save parser, field coverage, typed records, statistics and validation. | Candidate for integration/port only after dependency and packaging review; currently research only. |
| [FM Player Analyzer](https://github.com/ElectroHugin/FM-Player-Analyzer) | GPL-3.0 | Save parsing, import pipeline, SQLite schema, graphics index, player profiles and analytics. | Research only; no code copied. |
| [FM View Exporter](https://github.com/ElectroHugin/FM-View-Exporter) | GPL-3.0 | FM27 column catalogue and export headers in in-game order. | Public schema/reference research; no plugin code copied. |

Closed/commercial packages (FMRTE, Genie Scout, TalentSeer, FM Dossier) are product/black-box references only. Do not decompile, redistribute or copy proprietary code/assets. Observe workflows and use public documentation and user-supplied screenshots.

## 6. Installer-only / source gaps

- FMSuperScout and FMLibreEditor uploads are installers, not source archives; their repositories are available and are being inspected directly.
- FM Dossier is an installer, not a source archive.
- TalentSeer is a compiled application package with a README, not source.
- Genie Scout upload is an installer-only ZIP.
- FMRTE is a portable binary/resource package, not source.
- FM Player Analyzer is the newly uploaded full source ZIP.

## 7. Safety and preservation

- No EXE was launched.
- No installer was run.
- ZIP extraction was limited to the FM Player Analyzer source archive for static review.
- SHA-256 values above identify uploaded files and should be retained with original Library copies.
- No third-party source has been copied into FM Atlas in this research pass.
