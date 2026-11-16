# FM Atlas — UI and Programme Design Review 001

**Date:** 2026-11-16  
**Branch:** `rebuild/fresh-foundation-001`  
**Controlling source:** `docs/DESIGN_SPECIFICATION.md` revision 001 and `research/FMRTE_FEATURE_AUDIT_001.md`  
**Review status:** Structural UI implementation advanced; Windows build, visual screenshot comparison and live-game acceptance remain outstanding.

## 1. Executive finding

The previous shell was a functional foundation, not the approved final workspace. This pass closes several structural gaps: players, staff and clubs now have directory routes; player/staff/club profiles open in closable in-app tabs; each profile section has a working data/empty-state view; and profile tabs display an active orange state. The dashboard now reflects career data, record counts, genuine actions and recently opened profiles rather than an internal development checklist or a hard-coded pending-edit count.

The interface is closer to the approved HorizonX composition and Watched Earth visual language, but it should not yet be called an exact visual match. A real Windows screenshot comparison against REF-001 and REF-003 is still required.

## 2. Review against the approved references

### REF-001 — main application composition

**Implemented in this pass**
- Compact left navigation for Dashboard, Players, Staff, Clubs, Save Manager and Settings. There is no Search item in the left rail.
- Global Quick Search remains in the top header; Advanced Search is beside it; the primary Load Game action remains at the right.
- The main application workspace now uses closable tabs. Dashboard is the fixed first tab; player, staff, club and directory workspaces open as additional tabs.
- Opening the same entity twice focuses its existing tab rather than opening duplicate profile tabs.
- Dashboard career summary shows connection status and live Player/Staff/Club counts. The write status is explicitly disabled instead of showing a fabricated pending-edit count.
- Dashboard shortcuts route to Players, Staff, Clubs, Advanced Search, player CSV import and Load Game.
- Recently opened profile strip keeps the five most recently opened Player, Staff and Club records and reopens them in their existing profile tabs.

**Still outstanding**
- Pinned profiles and user-defined shortcuts.
- Save Manager and Settings are honest informational workspaces, not full implementations.
- The final screenshot-by-screenshot comparison with the supplied HorizonX reference has not been performed on a running Windows build.
- The main dashboard does not yet have a verified save name, manager identity, club context or date field because those values are not yet projected into the connection status model.

### REF-002 — FMRTE Advanced Search

**Implemented / retained**
- Advanced Search opens as a centred modal dialog rather than a separate persistent editor window.
- Player / Coach / Club selectors and the existing vertical category navigation remain.
- The dynamic field panels use supported typed fields and Indexed Fields discovered from the local database.
- Numeric minimum/maximum, Boolean, date, text and enum field controls retain their typed query semantics.
- AND/OR logic, the player position filter and global Include women preference remain part of the query model.
- OK / Search runs the search; Cancel closes the modal; Escape invokes Cancel.
- Double-clicking a result opens its profile in the main workspace and closes Advanced Search.

**Still outstanding**
- A side-by-side visual comparison of the full dialog against REF-002 at normal Windows scaling.
- Any field not backed by a typed projection and query implementation must stay unavailable; catalogue presence does not imply a live value is available.

### REF-003 — visual identity

The current resource palette uses near-black surfaces, warm off-white primary text, muted grey supporting text and restrained orange for selection, focus and key actions. Georgia/Segoe UI/Consolas provide the editorial / legible / monospaced hierarchy. Controls use thin low-contrast outlines and modest radii.

The profile tabs, workspace tabs and primary actions now use the orange selected state. The palette remains intentionally free of dominant blue accents, bright surfaces and decorative gradients. Exact type scale, spacing and proportions still need screenshot review on Windows.

## 3. Player profile coverage

The player profile is now an in-app tab with an identity rail, central attribute view and supporting right-side information panels. It includes:
- CA and PA with source evidence tooltips;
- all supported visible attribute groups from the FM Atlas catalogue;
- Technical / Goalkeeper attribute view selector;
- Mental, Physical, Hidden, Personality and Feet groups;
- canonical position familiarity ordering;
- preferred moves;
- identity, birth, nationality/second nationality, club/team, competition, position, preferred foot and height/weight fields;
- contract, wage, transfer value and asking-price summary;
- season-statistics, position and data-provenance panels.

Profile sections use the FMRTE audit list:
1. Information
2. Contract
3. Stats
4. Unavailability
5. Transfer Proposals
6. Retire Date
7. Positions
8. Preferred Moves
9. Relationships
10. Languages
11. Scout Information
12. Staff
13. Clone Center
14. Personality / Hidden
15. History

Each section has its own content rows and evidence labels. If the loaded source has not supplied a section, the profile explains that the data was not imported. Staff and Clone Center are visible conditional areas but do not offer unsupported editing or cloning actions.

**Not complete:** per-attribute potential values are not present in the current typed source model; current/PA values should not be presented as a complete current-vs-potential attribute matrix. Season stats, historical records, scout opinions, languages, bans, proposals and retirement dates are not yet populated by the live Game Pass reader. Those tabs are structured and honest, not data-complete.

## 4. Staff profile coverage

Staff results now open a dedicated in-app staff profile, with sections:
- Information
- Contract
- Career Plans
- Promises
- Swap Staff
- Training Rating
- Formations / Tendencies
- Relationships
- Knowledge
- Retire Date
- Languages

The profile presents the typed identity, role, age, gender, nationality, club, CA/PA, coaching attributes, knowledge and any supported indexed values. Empty sections say which source data is absent. Swap Staff remains a read-only explanation until safe write support exists.

## 5. Club profile coverage

Club results now open a dedicated in-app club profile, with sections:
- Information
- Squads
- Youth Intake
- Finances
- Club Vision
- Possible Signings
- Player Clauses
- Transfer Center
- Staff
- Wage Contributions
- Kits & Colours
- Affiliations
- Match Preparation
- Stadium

The currently mapped club record can surface identity, nation, competition, founded year, reputation, facilities, finances, stadium information and other typed search fields. Each section uses source evidence and a clear empty state when the loaded provider has no corresponding record.

**Not complete:** the current live reader does not yet import the complete club-to-player roster, board vision, youth intake, all clauses, kits, affiliations, staff assignments or every stadium/attendance detail. These are profile destinations and data contracts, not fully populated features yet. A full club profile will only be complete when those related entity families are loaded and joined by verified IDs.

## 6. Installed player faces and club badges

The existing read-only `FootballManagerGraphicsLocator` is now connected to profile opening:
- On startup, FM Atlas checks the standard Documents / Sports Interactive / Football Manager 27 / graphics folder.
- It indexes existing `config.xml` mappings without copying, renaming or modifying image files.
- Player portraits resolve by the game UID (including the supported `r-` regen prefix).
- Club badges resolve by the club UID.
- If the folder or mapping is absent, the profile retains a neutral placeholder.

There is no in-app custom graphics-folder picker yet. The standard folder is automatic; non-standard graphics folders require a later Settings workflow.

## 7. Quick Search, directories and profile tabs

- Quick Search still searches only records already imported into the local index.
- Search result type is visible; double-click opens the Player, Staff or Club profile.
- Players, Staff and Clubs each have a dedicated directory workspace with type-scoped search.
- Multiple profile tabs can remain open at once. Tabs are closable and duplicate opens focus the existing tab.
- Recently opened profiles are surfaced on Dashboard.
- The global Include women setting is passed to Quick Search, directory search and Advanced Search. Unknown gender records remain visible rather than being guessed.

## 8. Live editing, loading and save limitations

The Load Game button is wired to the exact-fingerprint FM27 Game Pass read-only connector. Its blocking animated overlay locks the main workspace and owned windows while the player/staff/club scan, SQLite batch transaction and save-generation recheck run.

**Live edit and save are not complete.** The process accessor is read-only, no process write primitive is enabled, no field is marked WriteVerified and Save Live Changes remains disabled. A write policy is present, but it is a preflight gate, not a writer. It requires process/build/save-generation/target identity, old-value, verified field range and writable memory-region checks.

Enabling live editing without a real test against the user's exact FM27 Game Pass build would be unsafe. First runtime read comparison, then individual write/read-back/undo verification in a disposable career are still required.

## 9. Verification

- All seven XAML files checked in this review pass have balanced element structure under a static tag-stack check.
- The repository has automated source tests for live batch import, provider invalidation, profile-independent data projection, Advanced Search, import and write preflight.
- There is no .NET SDK, C# compiler, MSBuild or PowerShell installed in the present execution environment. A Windows build/test could not be run locally.
- Latest GitHub Actions run #737 (ID `43863750491`) failed with a job record containing `steps=null`, no log URL and a 404/BlobNotFound on log download. This is a runner provisioning/log-access failure before Restore/Build/Test, not a successful build and not a diagnosed source compilation failure.
- No executable/package is verified or ready to install from this UI pass.

## 10. Next acceptance gates

1. Run a Windows .NET 8 Release build and full test suite against the current branch head. Fix compiler, WPF binding and test failures from the actual output.
2. Launch on the user's Windows 10 PC and verify FM Atlas starts and the animated load overlay blocks all owned workspaces.
3. Confirm exact installed Game Pass package fingerprint before live connection.
4. Load a disposable career and compare player/staff/club counts, IDs, CA/PA, a broad sample of attributes, gender, positions, club links and staff/club fields against the in-game screens / independent reference.
5. Populate the remaining club relationships and profile sections from verified source fields.
6. Add a guarded write primitive only after the read mapping is independently verified; start with a very small scalar allow-list, then test read-back, undo and conflict handling.
7. Compare desktop screenshots with REF-001 / REF-002 / REF-003 and tune spacing/type sizes to the actual reference.

**Acceptance conclusion:** the workspace structure and core profile navigation are substantially closer to the approved design. The program is not yet “complete” as a live FM editor: runtime read verification, missing entity relationships, full section data, write/undo and successful Windows release testing remain outstanding.


## 11. Club squads and staff relationship follow-up

The current Club profile now has a real indexed relationship path for **Squads** and **Staff**:
- Player live records index a numeric `club_uid` from the player's current team -> club object.
- Staff live records index a numeric `club_uid` from their contract -> team -> club object.
- The Club Squads tab queries Player records using exact `club_uid` equality through the existing typed Advanced Search provider.
- The Club Staff tab queries Staff/Coach records using the same exact numeric club ID.
- Both lists are selectable. Double-click opens the respective player/staff profile in the main workspace.
- The query is bounded to 200 records, preserves provider-scoped identities and does not use fuzzy club-name comparisons.
- A regression test now imports two players and two staff records linked to different clubs, then proves that the correct Player and Staff records are returned for Club ID 77.

These are still externally mapped live relationship fields. The regression verifies the SQLite/index/query behaviour with representative records; it does not independently prove the memory offsets on a running FM27 Game Pass career. Real runtime comparison remains an acceptance gate.
