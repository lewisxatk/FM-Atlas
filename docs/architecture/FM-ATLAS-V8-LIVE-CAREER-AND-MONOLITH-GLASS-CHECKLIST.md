# FM Atlas V8 — Live Career, People Search and Monolith Glass

> **Current safety override (2026-11-14):** Full live entity-array import is OFF by default. The monitor detects process/build/session generation only. The automatic-import items in earlier notes describe implemented extractor/transaction capabilities, not permission to run a full scan. Offline read-only save import remains the current data-population route until bounded cancellation, FM performance and field-witness checks pass.

**Working branch:** `rebuild/v8-full-product-rebuild`  
**Status:** source implementation committed; fresh Windows build and real FM27 acceptance still required.  
**Principle:** load FM27, load a career, leave Atlas open. Atlas should recognise the active career without asking the user to choose a save file.

## 1. Career/session detection — entity-array import currently paused

### Implemented in source

- [x] Poll for the running Football Manager process after Atlas startup.
- [x] Identify only supported FM27 Steam/Game Pass module fingerprints already held in the build-profile registry.
- [x] Read the game's `GAME_UI` save-load generation counter.
- [x] Wait until the counter and profiled player/staff/club array counts indicate that a career is loaded.
- [x] Attach to the exact process ID and profile discovered by the monitor, rather than taking the first process named `fm`.
- [x] Include process ID, storefront profile and save-load counter in the session key.
- [x] Re-check the same save-load counter before and after the scan.
- [x] Discard the snapshot if the career changes while Atlas is reading it.
- [x] Detect and report a changed career generation without reading the entity arrays. Full automatic entity-array rescan remains OFF until the safety gates below pass.
- [x] Detect a supported FM process/build when Atlas opens while a career is already loaded; do not automatically import it.
- [x] Keep a toolbar action that explains the paused import gate; it does not initiate a memory scan.
- [x] Keep the original local Atlas database and offline save-import path intact.

### Still needs real-PC verification

- [ ] Confirm the Steam and Game Pass profiles match the exact FM27 build installed on the user's PC.
- [ ] Confirm the monitor sees the career only after FM has finished loading it.
- [ ] Confirm detection-only monitoring does not materially affect FM CPU/RAM or responsiveness.
- [x] Keep full entity-array scans disabled by default and avoid repeated import attempts on each monitor tick.
- [ ] Load a second save without closing Atlas and confirm the detector reports the new generation without importing a snapshot.
- [ ] After a bounded scanner is implemented and enabled for controlled testing, confirm a save switch during a scan discards that scan.
- [ ] Measure detection-only CPU/memory use on a real career; separately benchmark any future bounded import.

## 2. Human manager, club and nation

### Implemented in source

- [x] Scan the profile-matched FM27 staff pointer array as part of the live snapshot.
- [x] Use FM staff class/vtable identity to cache the staff person-block shift.
- [x] Select the staff person-block shift by name-decoding evidence, following the public FMLibreEditor discovery method.
- [x] Identify the human-manager object as the staff record using the shifted identity block.
- [x] Read source-backed staff identity fields: name, DOB/age where available, nation, employer club and contract job code.
- [x] Read staff CA and signed PA candidates.
- [x] Store live staff records and field facts in the local database.
- [x] Store a unique human-manager record with the live save-load counter, manager name, club and nation.
- [x] Add dashboard manager/club/nation context when exactly one manager is identified.
- [x] Add toolbar shortcuts to open the manager's Staff profile and current Club profile.

### Still needs real-PC verification

- [ ] After the import gate is safely re-enabled, confirm the detected manager name matches the manager shown inside FM27.
- [ ] Confirm manager club and nation match the in-game profile.
- [ ] Compare manager/staff CA and PA with the in-game UI; values remain provisional until witnessed.
- [ ] Confirm the shortcut opens the correct person/club profile after each save change.
- [ ] Confirm the live manager is not confused with an ordinary staff member or a stale prior-save manager.

## 3. FMRTE-style people search and profiles

### Implemented in source

- [x] Keep the existing Global Search and Ctrl+K command entry point.
- [x] Keep the existing Players workspace with paged, virtualised results.
- [x] Keep the universal Advanced Search hub and existing multi-condition player filters.
- [x] Keep the multi-attribute/personality builder and its ALL/AND and ANY/OR behaviour.
- [x] Expose CA and PA in player search result columns and the existing database filter model.
- [x] Store live player CA/PA in the same player table used by the existing search/profile workspace.
- [x] Preserve negative PA values used by random-potential ranges rather than dropping them.
- [x] Store the live 1–20 player attribute candidates in PlayerAttributes so the existing player profile and attribute search can use them.
- [x] Keep source provenance and provisional labels for live field values.
- [x] Extend Staff search results with CA, PA, job, age, nation, club and human-manager status.
- [x] Open the selected Staff record into its fact profile; namespace live staff IDs with `LIVE:` so they cannot collide with offline save IDs.
- [x] Add database integration coverage for live manager context, staff search, staff fact profiles and signed PA.

### Known boundary

- The live identity scan does not provide every historical or match-performance statistic. Those facts are still supplied by the optional read-only fmsave save import or FM View Exporter CSVs.
- The current live staff extractor does not claim complete personality, all staff attributes, wage/contract, training or staff history coverage.
- Any field not actually supplied by the source remains blank; a module fingerprint is not proof that every field offset is verified.
- The existing player Advanced Search and Attribute Search are retained; this pass does not claim that every entity type now has one universal, identical filter builder.

### Still needs real-PC verification

- [ ] Search by name, club, nation and UID against the current live career.
- [ ] Confirm CA/PA values and negative PA values display correctly.
- [ ] Confirm all available live player attributes appear in the correct profile groups.
- [ ] Confirm a player selected from the live list opens a profile with no missing stored attributes.
- [ ] Confirm saved searches, filters and profiles still work after a career reload.
- [ ] Import the optional offline save only when extended history/statistics are required; verify that the offline and live records do not duplicate or overwrite one another.

## 4. Monolith Glass interface

### Implemented in source

- [x] Rename the V8 window/product-facing title to Monolith Glass.
- [x] Use graphite/charcoal backgrounds and warm amber-orange accent tokens.
- [x] Keep Glass Dark, Glass Light and Studio appearance choices.
- [x] Apply translucent glass surfaces, warm borders and restrained drop shadows to shared cards.
- [x] Update toolbar hover/focus illumination to warm amber.
- [x] Retain grouped/collapsible navigation and compact icon toolbar.
- [x] Retain Ctrl+K global search and the current Advanced Search entry point.
- [x] Retain the manager and club quick-action shortcuts.
- [x] Keep motion restrained and conditional on the Windows system animation setting.

### Still needs real-PC verification

- [ ] Inspect the complete shell and every workspace at 100%, 125% and 150% Windows scaling.
- [ ] Inspect 1080p and 1440p layouts for clipping and awkward column widths.
- [ ] Check glass contrast over all cards, dialogs, dropdowns, DataGrids and profile panels.
- [ ] Confirm keyboard focus, navigation, tooltips and reduced-motion behaviour.
- [ ] Confirm the application still reads clearly in Glass Light and Studio.

## 5. Reverse-engineering references

The implementation uses the public projects as behavioural and technical references, not as code to copy.

- **FMRTE** — its documented workflow is to load a career in Football Manager first, then attach the editor to that loaded career and confirm the current manager/club. Atlas follows that same user-facing workflow.
- **FMSuperScout** — its public project describes a dedicated scanner feeding a local scouting/search application. Atlas retains the local database/search approach and avoids rendering the full player pool as one giant UI list.
- **FMLibreEditor** — its public FM27 research documents the GAME_UI load counter, exact storefront profiles, staff person-block shift detection and read-only validation principles. Atlas uses these ideas to identify the active career and manager, while keeping its own C# implementation and fail-closed checks.
- **fmsave** — remains the read-only route for larger structured save tables, career history and performance facts that are not included in the live identity scan.

Reference code and profile files are subject to their own licences. Atlas does not vendor their implementation. Public field-layout observations are treated as provisional evidence and must still be checked against the user's in-game UI.

## 6. Release / user test gate

- [ ] Fresh Windows .NET 8 restore and Release build for this exact branch head.
- [ ] All xUnit and Python adapter tests pass on that exact commit.
- [ ] Self-contained Windows x64 artifact is produced.
- [ ] Atlas launches and initializes without changing/deleting the user's existing database.
- [ ] Detection-only monitor correctly identifies supported Steam or Game Pass process/build/session; do not require live entity-array import for this gate.
- [ ] Manager/club/nation are correct.
- [ ] Player and staff search, CA/PA and profile facts are correct.
- [ ] A second career load triggers a new clean snapshot.
- [ ] Football Manager remains responsive during scans.
- [ ] Monolith Glass passes DPI/resolution checks.
- [ ] Live editing remains disabled until separately certified.

**Do not call this a finished release until the latest Windows build and real FM27 acceptance gates pass.**
