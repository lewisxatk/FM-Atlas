# FM Atlas — Fresh Rebuild Master Plan

**Revision:** 003  
**Date:** 2026-11-16  
**Working branch:** `rebuild/fresh-foundation-001`  
**Design authority:** [Reference Library](./REFERENCE_LIBRARY.md) and [Design Specification](./DESIGN_SPECIFICATION.md)  
**Architecture:** [Architecture Decision](./ARCHITECTURE_DECISION.md)  
**Research inventory:** [Uploaded Research Inventory](../research/UPLOADED_RESEARCH_INVENTORY.md)  
**Research findings:** [Research Findings 001](../research/RESEARCH_FINDINGS_001.md)  
**Continuity authority:** [Session Continuity](./SESSION_CONTINUITY.md)

## 1. Product decision

Build a genuinely fresh FM Atlas desktop application from a clean .NET 8 WPF solution and modular architecture. The previous application is a research/reference source only. Do not spend time preserving weak UI, tangled architecture or features that cannot be verified.

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

## 6. Fresh architecture decision

The selected foundation is .NET 8 WPF with Domain, Application, Infrastructure, Desktop and Tests projects. The old WPF interface/code-behind is discarded. See `ARCHITECTURE_DECISION.md` for dependency direction, data-provider boundaries, SQLite persistence, write-safety and proof-of-concept acceptance.

## 7. Development sequence

1. Protect/recover source and complete research inventory.
2. Build the new WPF solution and Windows CI.
3. Implement approved application shell and core navigation.
4. Implement Quick Search and FMRTE-style Advanced Search.
5. Implement read-only live connection and data import.
6. Implement full-screen player profiles and data tabs.
7. Implement guarded editing after field-level verification.
8. Expand staff/club data and editing.
9. Release and test.
10. Only then resume deferred tactical/scouting analytics.

## 8. Current implementation status

The fresh solution has been created in `FMAtlas.sln` with Domain, Application, Infrastructure, Desktop and Tests projects. The initial WPF shell has the compact no-Search sidebar, top Quick Search placeholder, Advanced Search action, compact Save Atlas/Save Changes controls and prominent Load Game button. Load Game currently performs process/module presence detection only; it does not read memory or import a career. All unimplemented controls are disabled and labelled In Progress through tooltips.

The Windows GitHub Actions build/test workflow is committed. The current execution environment has no .NET SDK, so no local compile has been claimed. Windows CI and a user-side build/screenshot review remain required. See `CURRENT_CHECKPOINT.md` and `FRESH_REBUILD_CHECKLIST.md`.
