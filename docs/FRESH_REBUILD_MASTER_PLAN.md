# FM Atlas — Fresh Rebuild Master Plan

**Revision:** 001  
**Date:** 2026-11-16  
**Working branch:** `rebuild/fresh-foundation-001`  
**Design authority:** [Reference Library](./REFERENCE_LIBRARY.md) and [Design Specification](./DESIGN_SPECIFICATION.md)  
**Continuity authority:** [Session Continuity](./SESSION_CONTINUITY.md)

## 1. Product decision

Build a genuinely fresh FM Atlas desktop application from a clean solution and architecture. The previous application is a research/reference source only. Do not spend time preserving weak UI, tangled architecture or features that cannot be verified.

Retain only useful, independently audited material:
- verified domain knowledge and field catalogues;
- useful parsers, importers, algorithms and tests where technically sound and licence-compatible;
- graphics mapping research;
- validated version/build fingerprints;
- safe editing concepts and evidence;
- useful user requirements and product behaviour.

Do not blindly port legacy code. Every reused component must have provenance, licence review, tests and a reason to retain it.

## 2. Product principles

1. The user's approved screenshots and written specifications govern the design.
2. A fresh application is the goal; a reskin or incremental patch is not an acceptable substitute.
3. The first release is a simple, reliable editor, not the full scouting analytics suite.
4. Real game/save data only. Never fabricate players, attributes, statistics, scores or successful writes.
5. Read, local data persistence and live-game writes are distinct operations.
6. No UI control may directly perform unvalidated process-memory writes.
7. Game Pass is the primary compatibility target; Steam is a separate profile.
8. Every field has provenance, version applicability and read/write status.
9. Every feature is complete only after visual and functional acceptance tests pass.
10. Preserve work continuously and make interrupted sessions resumable from GitHub.

## 3. Design authority

Current approved references:
- REF-001: main application composition — compact navigation, clean dark workspace and restrained panels.
- REF-002: FMRTE advanced-search dialog — authoritative structure and information architecture.
- REF-003: Watched Earth visual language — black, off-white, restrained orange, editorial display typography, monospaced metadata and subtle outlines.
- REF-004: full-screen player profile — left identity rail, central FM attributes, right verified information, compact player-specific tabs.

See `REFERENCE_LIBRARY.md` and `DESIGN_SPECIFICATION.md` for exact requirements. Do not invent alternative layouts.

## 4. Target product scope

### 4.1 Fresh desktop foundation
- Clean solution and project boundaries.
- Application shell, global header, compact left navigation and top search.
- Prominent Load Game action on the right.
- Compact Save / Save Changes actions.
- Player workspace tabs and full-screen player profile tabs.
- Logging, diagnostics, local settings, persistence, error states and packaging.
- Keyboard navigation, resizing and Windows desktop accessibility.

### 4.2 Game connection and save management
- Detect supported FM process and exact build/storefront.
- Detect loaded career and connection state.
- Load live game data.
- Identify save identity and save-load generation.
- Detect career switches/reloads and invalidate stale pointers.
- Refresh data safely.
- Keep Atlas local database save separate from writes into FM.
- Import offline save copies where supported.
- Backup, restore and clear diagnostics.

### 4.3 Players
- Player identity, name, UID, age, DOB, nationality, club, team, league and competition.
- Position familiarity, preferred foot, height, weight, international information and reputation.
- Contract, value, wage, squad status, availability and injury fields where supported.
- CA, PA and all applicable standard attributes.
- Goalkeeper attributes where applicable.
- Hidden attributes, personality, preferred moves/traits and relations where supported.
- Player face resolution from installed graphics/config mappings and FaceDrop research where compatible.
- Full-screen profile based on REF-004.
- Multiple open player tabs without losing search context.
- Correct FM/FMRTE attribute names, grouping and canonical order for the exact supported version.

### 4.4 Player profile tabs
At minimum:
- Overview
- Attributes
- Contract
- Positions
- Preferred Moves / Traits
- Season Stats
- History
- Personality / Hidden
- Relations
- Other verified FMRTE-style player sections found during feature audit.

Tabs are compact controls at the top of the profile. The profile uses a left identity/information rail, central attribute/editor workspace and right column for grounded information. Unsupported or unavailable data is not fabricated.

### 4.5 Staff
- Staff search and profiles.
- Managers, coaches, scouts, medical and other supported staff types.
- Applicable attributes, CA/PA where meaningful, contract, nationality, reputation and supported fields.
- Advanced search and guarded editing only where mappings are verified.

### 4.6 Clubs
- Club search and profiles.
- Identity, nation, competition, reputation, finances, facilities, stadium, colours, badge and relations where supported.
- Advanced search and guarded editing only where mappings are verified.

### 4.7 Quick and advanced search
- Quick search bar at the top of the workspace, never a Search item in left navigation.
- Advanced Search button at the top.
- REF-002 FMRTE-style dialog with Player / Coach / Club selectors.
- Categories: General, Contract, Technical/Gk, Mental Attributes, Physical/Hidden, Preferred Moves, Relations.
- Grouped filter panels, two-column layout where appropriate, position pitch, compact numeric ranges, boolean controls and entity lookups.
- Multiple criteria, explicit AND/OR semantics, validation, saved filters, sorting, result columns and preserved filter state.
- Every filter must map to a real field and pass tests.

### 4.8 Live editing engine
- Separate read and write adapters.
- Exact process/storefront/build identification.
- Version-specific field catalogue and memory layouts.
- Entity identity checks and save-generation checks.
- Address, type, range and expected-current-value validation.
- Preflight and staged changes.
- Safe apply through application services.
- Read-back verification.
- Undo/rollback where supported and verified.
- Audit history and clear unsaved/pending states.
- Refuse writes on unknown builds, stale identities, uncertain mappings or failed preconditions.
- Never test against the user's only or online/shared career.

### 4.9 Database and provenance
- Typed player, staff, club, nation, competition, contract, statistic and relationship records.
- Version-aware import and migrations.
- Indexed search and filters.
- Duplicate and missing-record handling.
- Provenance for every field: live memory, save parser, FM export, graphics config, calculated, estimated or unavailable.
- Separate raw source values from derived Atlas calculations.
- Preserve unknown fields as unknown.

### 4.10 Graphics
- Locate configured FM user-data and graphics folders.
- Parse supported `config.xml` mappings.
- Index player faces, club badges, flags and other supported graphics.
- Match by stable IDs.
- Detect missing/duplicate assets.
- Cache and refresh indexes.
- Research FaceDrop as a distinct provider.
- Never move or modify user assets without permission.

### 4.11 Later product areas — deferred
These remain in the long-term product roadmap, but are not allowed to delay the simple editor:
- Squad Studio and squad depth.
- Tactical Studio and Tactical Effectiveness research.
- Role suitability and documented calculations.
- Recruitment and Value Finder.
- Player comparison and shortlists.
- Wyscout/StatsBomb-inspired performance/Moneyball analytics, per-90 statistics, percentiles and recruitment intelligence.

No scouting analytics build-out until the basic editor and live editing acceptance gates pass, except retaining/reusing already verified data infrastructure.

## 5. Technical research requirements

Investigate and compare:
- FMLibreEditor: Game Pass/Steam build profiles, live editing safety, save-generation checks and undo.
- FMSuperScout: read-only scanner, entity discovery, bulk extraction, indexing, snapshots and search.
- fm-editor: version layouts, memory adapters, field mapping and undo architecture.
- fmsave: offline FM26/FM27 save parsing and field coverage.
- FM Player Analyzer: save parsing, graphics config mapping and player identity.
- FM View Exporter: CSV/HTML export as an independent test/import route.
- FMRTE: observable product features, search, player profile tabs, editing workflow and undo behaviour.
- TalentSeer: feature coverage and profile/search benchmark; closed binary only, no assumption about internal implementation.
- Teknik Oda/Toby: match/tactical data and plugin lifecycle; separate from player-memory scanner.
- FaceDrop: face assignment behaviour and data location.
- FM27 Editor Mod: pre-game editor skin/customisation research, not a live editor.
- gilet: save container research.

Check current licences before reusing any code. GPL projects are research-only unless an explicit compatible licensing decision is made. MIT projects still require licence and notice review. Never copy binaries or offsets without provenance and verification.

## 6. Fresh architecture decision gate

Do not select the final UI framework or blindly inherit the legacy architecture based only on historical notes. First:
1. Confirm the user's target Windows environment and installed SDK.
2. Inspect the supplied R7.2.0 candidate only to extract useful domain logic and dependencies.
3. Build a tiny technical proof of concept for desktop UI, process detection, offline import and testability.
4. Compare maintainability, accessibility, packaging and Game Pass integration.
5. Record the decision and rationale in `docs/ARCHITECTURE_DECISION.md`.
6. Create a clean new solution with no legacy UI copied across.

The previously reported stack was C# / WPF / .NET 8, but this is a candidate to verify, not an unquestioned constraint.

## 7. Milestones and completion gates

### M0 — Protect and inventory
- [ ] Verify current GitHub branches and preserve the main branch.
- [ ] Recover the complete R7.2.0 candidate and R7.1.0/R7.0.2 comparison archives.
- [ ] Verify archive hashes and source inventories.
- [ ] Inspect all user-supplied research assets and record provenance/licences.
- [ ] Identify reusable code versus reference-only code.
- [ ] Save a clean project checkpoint.

### M1 — Research and technical blueprint
- [ ] Inspect relevant source repositories and bundled documentation.
- [ ] Compare live-memory approaches and Game Pass profiles.
- [ ] Audit offline save parser coverage.
- [ ] Establish canonical player/staff/club field catalogue.
- [ ] Establish FM27 visible attribute names/order from verified sources.
- [ ] Define provenance and read/write status.
- [ ] Complete fresh architecture decision.
- [ ] Approve technical blueprint.

### M2 — New application foundation
- [ ] Create new clean solution and project layout.
- [ ] Implement approved REF-001/REF-003 application shell.
- [ ] Implement global quick search and Advanced Search entry.
- [ ] Implement prominent Load Game and compact save actions.
- [ ] Implement navigation, workspace tabs, settings, diagnostics and logging.
- [ ] Build reproducibly and package a first shell for visual review.

### M3 — Data acquisition and Save Manager
- [ ] Implement supported process/build detection.
- [ ] Implement read-only live data acquisition.
- [ ] Implement offline save import where verified.
- [ ] Implement typed database, indexing and provenance.
- [ ] Implement career switch/reload handling.
- [ ] Validate representative player/staff/club data against FM.

### M4 — Player workspace
- [ ] Implement REF-004 full-screen player profile.
- [ ] Implement multi-player tabs.
- [ ] Implement graphics locator.
- [ ] Implement complete versioned attribute catalogue and ordering.
- [ ] Implement profile sub-tabs and supported data.
- [ ] Verify CA/PA and all applicable attributes.

### M5 — Search
- [ ] Implement Quick Search.
- [ ] Implement REF-002 FMRTE-style Advanced Search layout.
- [ ] Implement every verified category and field.
- [ ] Implement explicit AND/OR query semantics.
- [ ] Implement saved filters, sorting and result columns.
- [ ] Verify filters against actual imported save records.

### M6 — Live editing
- [ ] Complete build-specific Game Pass profile.
- [ ] Implement guarded write adapter.
- [ ] Verify player identity and save-generation checks.
- [ ] Implement staged changes, preflight, apply and read-back.
- [ ] Implement safe undo/rollback.
- [ ] Test visible attributes and CA/PA on disposable offline copy.
- [ ] Expand field coverage only after each field is independently verified.
- [ ] Verify in-game persistence after continue/reload.

### M7 — Staff and club editors
- [ ] Staff profiles, search and verified editing.
- [ ] Club profiles, search and verified editing.
- [ ] Contracts, relations and other supported categories.

### M8 — Stabilisation and release
- [ ] Regression and data accuracy tests.
- [ ] Windows build and startup tests.
- [ ] Game Pass compatibility tests.
- [ ] Backup/recovery tests.
- [ ] Installer and release notes.
- [ ] User acceptance test and release sign-off.

### M9+ — Deferred expansion
- [ ] Squad Studio.
- [ ] Tactical Studio and Tactical Effectiveness.
- [ ] Recruitment/Value Finder.
- [ ] Advanced Moneyball/per-90/percentile analytics.

## 8. Release acceptance

The initial usable release is not complete until:
- the new application is demonstrably a fresh solution;
- approved shell and player/search references are implemented;
- the user's FM27 Game Pass build is detected correctly;
- a real career can be loaded/read;
- CA/PA and the complete supported player attribute set display accurately;
- advanced search works against real records;
- supported player changes apply, read back and persist in the game;
- undo/rollback and career-switch protections pass;
- no fake data or inert controls are presented as complete;
- the build, logs, tests and release artifact are preserved.

## 9. Status vocabulary

Use only:
- **Not started**
- **Researching**
- **Documented**
- **Implemented — untested**
- **Built**
- **Tested**
- **Verified in FM27 Game Pass**
- **Blocked**
- **Deferred**

Never mark a task complete based solely on a code file, screenshot, compile or previous conversation claim.
