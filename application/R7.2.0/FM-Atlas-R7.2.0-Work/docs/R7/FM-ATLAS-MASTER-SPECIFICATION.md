# FM Atlas LIVE — Master Technical Specification
**Version:** 1.0  
**Prepared:** 13 November 2026  
**Project:** FMATLAS  
**Status:** Product brief and research-backed implementation plan. Not yet a verified live FM27 build.

---

## 1. Product definition

FM Atlas LIVE is a Windows desktop companion for Football Manager 27. Its purpose is to turn the complete, accessible football world inside the user's active FM27 career into a fast, searchable scouting and performance-analysis platform.

The product direction is Wyscout / StatsBomb-inspired: recruitment searches, player and squad analysis, statistics, per-90 leaderboards, percentiles, comparison reports, development history and tactical intelligence — but all player, club and competition discovery is scoped to the *loaded FM save*. It must follow that save as it moves through future seasons, transfers, retirements and newgen intakes.

FM Atlas is not a real-world football database, and it must not silently substitute external player data for a missing field in the save.

### Product promises
1. One application for discovering, understanding, comparing and recruiting players in an FM career.
2. Search across any accessible league or competition in the loaded save.
3. Clearly distinguish FM-native fields, Atlas-calculated fields, estimated values and missing/unverified values.
4. Work with FM27 Steam and PC Game Pass / Microsoft Store as separate, explicitly fingerprinted storefront profiles.
5. Have minimal impact on FM27 performance.
6. Make every live edit deliberate, auditable and reversible where the game state permits.

### First release scope
The requested product is a single integrated application, not a collection of disconnected mini-tools. The first development candidate should include the shared shell, connection, import pipeline, local database, scouting, player profiles, statistics, comparisons, squad and tactical workspaces, recruitment, historical snapshots, diagnostics and the editor framework. A feature is only marked "working" after its acceptance tests pass on the supported game/store build.

---

## 2. Visual and interaction design

### Agreed direction: Obsidian + Swiss Editorial
- Deep charcoal and graphite application canvas.
- Off-white primary typography and soft-grey supporting text.
- Restrained electric-lime accent for active states, key actions and positive data.
- Rounded, well-spaced buttons, pill filters, compact dropdowns and clean panels.
- Fine borders and subtle depth; avoid heavy glow, excessive glass, noisy gradients and generic Windows controls.
- Editorial hierarchy: strong section titles, aligned numeric columns, careful typographic scale and generous but efficient spacing.
- No Aurora Frost styling.

### Density rule
- **Landing and navigation screens:** balanced, easy to understand, concise summaries and obvious primary actions.
- **Opened workspaces:** information-rich and data-dense, with configurable columns, tabs, charts, percentiles, filters and detailed reports.
- Information density should increase when the user drills into a page, not overwhelm the starting dashboard.

### Global shell
- Slim left navigation with labelled areas and collapsible compact mode.
- Persistent top bar: current game, store, save name/identity, club, in-game date/season, connection state, global search and quick actions.
- Global command/search palette (Ctrl+K).
- Search result actions: open profile, compare, shortlist, open club/competition, add filter.
- Contextual right-side profile panel or full profile page; avoid modal stacks.
- Tables support sort, column picker, pinned columns, saved views, quick filters and CSV export.
- Keyboard navigation and high contrast focus states.
- Optional reduced-motion mode; animations must not run continuously.

### Main navigation
1. Dashboard
2. World Database
3. Scouting Centre
4. Statistics Studio
5. Player Profiles
6. Comparison Studio
7. Squad Centre
8. Tactical Intelligence
9. Development Centre
10. Transfers & Recruitment
11. Club & Competitions
12. Atlas Intelligence
13. Shortlists
14. Diagnostics / Tester
15. Editor (feature-gated; not enabled until the applicable edit profile is verified)

---

## 3. Complete feature inventory

### 3.1 Live Connection & Save Intelligence
- Detect running `fm.exe`; distinguish Steam and Microsoft Store / Game Pass installs.
- Identify `game_plugin.dll`, `GameAssembly.dll` and the actual running module version.
- Select a store-specific, exact-build profile; never apply Steam offsets to Game Pass.
- Identify the loaded career, in-game date, manager and managed club where verified.
- Detect save loads, save switches, game close, game update and stale connection.
- Manual connect, disconnect, reload and refresh controls.
- Controlled refresh; no continuous full-memory scans.
- Show profile/build fingerprint, last successful read and fields supported.
- Read-only safe mode when any signature, profile or validation check fails.
- Save-specific local database and snapshot lineage; do not mix different saves with the same club.
- Reconnect after sleep/minimise; stop unnecessary polling when Atlas is minimised.
- A data coverage report for every entity group (players, staff, clubs, competitions, etc.).

### 3.2 World Database Explorer
Search every entity that can be reliably enumerated in the save:
- Players, staff, managers and player-coaches.
- Clubs and club teams (senior, reserve, youth, women's where available).
- Nations and national teams.
- Leagues, cups and other competitions.
- Stadiums, officials/referees and awards where their entity structures are mapped.
- Free agents, loaned players, transfer-listed players, players with agreed future transfers and newgens.
- Club and player relationships, parent/loan club, current team, current competition.
- Search by name, UID, aliases, club, nationality, age, position, division and other mapped fields.
- Entity profile links and related-entity traversal.
- Completeness labels: complete, partial, only reachable entities, unsupported.

### 3.3 Scouting Centre
**Core filters**
- Position, secondary position and role.
- Age, date of birth, height, weight, preferred foot.
- Current Ability (CA), Potential Ability (PA), reputation, personality and traits where available.
- Technical, mental, physical and hidden attributes where verified.
- Club, division, competition, nation, nationality and eligibility.
- Contract expiry, wage, value, listed status, loan availability, agreed transfer and transfer status.
- Player status, team level, parent club and current club.

**Performance filters**
- Goals, assists, non-penalty goals, shots, shots on target.
- xG, xA, key passes, chances created, progressive passes/carries.
- Dribbles, successful dribbles and success percentage.
- Crosses, pass completion, through balls.
- Tackles, interceptions, blocks, clearances, aerial duels won and pressures if the selected source supplies them.
- Minutes, starts, appearances, average rating.
- Total values and per-90 values; always display the source and denominator.

**Recruitment workflow**
- Filter by any league/competition in the current save.
- Search examples: fast dribbling winger under a value/wage ceiling; high-potential centre-back under 21; replacement for a selected squad player.
- Save searches and presets; copy/share a search definition.
- Select a player and add to shortlist or compare.
- Show matching reasons and failed criteria.
- Distinguish hard criteria from weighted preferences.
- Show affordability and estimated accessibility separately; an estimated interest field must be labelled as an estimate.

### 3.4 Statistics Studio & Leaderboards
- Build top-25 or top-N leaderboards for all verified statistics.
- Example presets: dribbles per 90, successful dribbles, dribble success rate, goals per 90, assists per 90, xA per 90, chances created per 90, key passes per 90, progressive carries per 90, tackles per 90, interceptions per 90, aerial win rate, goalkeeper save percentage.
- Any league/competition; combined multi-league view; selected seasons and scopes where present in FM data.
- Position/role, age, nationality and club filters.
- Minimum minutes/appearances thresholds (e.g. 450 / 900 minutes) to avoid tiny samples leading leaderboards.
- Total and per-90 switch; percentile and league/position cohort ranking.
- Rank within competition, position group, age group and full save.
- Median and comparison-to-cohort.
- Custom leaderboard builder, saved leaderboards, column picker and export.
- Missing metrics never count as zero.
- A statistic only appears if its source field is present and validated. For example, a complete memory player dump does not automatically mean season match stats were captured.

### 3.5 Player Profiles
- Portrait, name, UID, nationality, DOB/age, club, division and current team.
- Current contract, wage, expiry, transfer/loan status and value.
- Position ratings and attribute panel (technical / mental / physical / goalkeeper).
- CA / PA and reputation only where the connector has valid evidence.
- Personality and hidden attributes where supported.
- Season and competition statistics, total/per-90 and percentile context.
- Role suitability; separate FM-native ratings from Atlas-derived scores.
- Career, club, loan and transfer history where data exists.
- Development trend from saved snapshots.
- Injury/availability history where mapped.
- Shortlist, compare, export report and quick recruitment actions.

### 3.6 Comparison Studio
- Compare 2–5 players (UI should comfortably support 2–4).
- Attributes, CA/PA, positions, role ratings, hidden/personality data, performance statistics, per-90, percentiles, age, contracts, value and wages.
- Table deltas, radar or percentile charts and metric-by-metric winner/neutral labels.
- Same league/position/age cohort comparisons.
- Compare a transfer target against a current squad starter.
- Save comparisons and generate report/export.
- Never show a universal winner when the result depends on the user's tactic or role.

### 3.7 Squad Centre
- Senior, reserve, youth and national team squads where available.
- Depth chart by position and role.
- Squad overview: age, value, wages, contracts, injuries, playing time, nationality and homegrown status when supported.
- Best XI and bench planner.
- Position shortages and weak areas.
- Squad balance and role coverage.
- Squad-wide comparison and replacement analysis.
- Player status and loan relationships must be resolved using parent club vs current club carefully.

### 3.8 Tactical Intelligence
- Formation builder with FM27 in-possession and out-of-possession shapes.
- Pitch visualisation with players, positions and suitability heat.
- Best XI / bench selection using role, position, availability and user weighting.
- Role DNA: define required attributes/statistics and search for matches.
- Squad tactical strengths and weaknesses.
- Identify out-of-position players and missing roles.
- Compare formations and role fits.
- Generate role-specific recruitment briefs.
- All home-grown Atlas scores are labelled as derived; do not present them as FM-native ratings.

### 3.9 Development Centre
- Snapshot player state over time: CA/PA, attributes, value, reputation, positions and relevant statistics.
- Attribute progression, CA change and decline.
- Identify stagnation, improvement and first-team readiness.
- Youth intake / newgen discovery by comparing consecutive snapshots.
- Youth prospect rankings and cohort comparison.
- Loan development tracking and club/competition history.
- Season and career timeline.
- Do not infer historical progress before the first snapshot.

### 3.10 Transfers & Recruitment Intelligence
- Target shortlists, watchlists and saved recruitment briefs.
- Contract expiry watchlists, free-agent and loan search.
- Bargain finder and value/wage ranking.
- Agreed transfers and future transfers.
- Incoming/outgoing transfer history.
- Player replacement recommendations.
- Financial affordability: fee, wages, bonuses/clauses only where mapped; assumptions shown.
- Transfer interest/availability as estimates only unless FM exposes authoritative state.
- Alerts on user-selected player changes (contract, club, value, attributes, development).

### 3.11 Club & Competitions
- Club profiles: squads, stadium, finances, budgets, facilities, reputation and club history where verified.
- League tables, schedules, competition results and player ranks where source data includes them.
- Competition format, participant, squad registration and rules where mapped.
- Nation and national team profiles, eligibility and international call-ups where data exists.
- Club vs club and competition vs competition comparison.
- Entity lists must disclose partial enumeration.

### 3.12 Atlas Intelligence
- Career-aware question interface using the local save database.
- Examples: best dribblers in my save; best budget winger for my club; who improved most; compare my starter to targets; weakest squad positions; expiring contracts.
- Cite the local entities/fields and calculations used in each answer.
- Distinguish source facts, calculated analysis and estimates.
- User-controlled local/offline mode; any external AI provider integration is optional and opt-in.
- No sending save data externally by default.

### 3.13 Graphics & image matching
- Player faces, club crests, nation flags and competition logos.
- Match by FM UID and graphics entity type.
- Scan user-selected graphics pack folders for `config.xml`, resolve `from` → `graphics/pictures/...` mappings, and locate PNG/JPG files.
- Cache UID-to-path mappings, validating the cache by config file path, size and modified time.
- Lazy-load/caches images; use placeholders when absent.
- Optional built-in FM images only when the image source is actually accessible and licensing permits.
- Handle multiple graphics packs and duplicate IDs deterministically; disclose the selected source pack.
- Never bundle third-party face/logo packs. Don't silently rewrite the user's graphics config.
- FaceDrop is an optional workflow/reference, not a required dependency. Current FaceDrop release supports FM27 27.0.1 on Steam; Game Pass support isn't documented.

### 3.14 Editor & Edit History (later activation, planned in this integrated architecture)
The editor is a distinct, feature-gated module. Its controls should appear on supported player/staff/club pages only after the applicable entity and field are verified.

**Player edits**
- Attributes, CA/PA, position ratings, personality, traits, foot, height, DOB, nationality, reputation, value, contract details, injury/ban/morale/condition fields where verified.

**Staff edits**
- Staff attributes, roles, preferred jobs, CA/PA and mapped personal details.

**Club edits**
- Finance/budgets, reputation, facilities, identity, stadium and other fields only when field-specific mapping and write behavior are witnessed.

**Other entities**
- Nations, competitions, awards, stadiums and officials only after dedicated entity models and field mappings exist.

**Editing UX**
- Global Editor on/off toggle.
- Per-field edit control on a player/staff/club profile.
- Stage edits before writing; show original value, proposed value and expected scope.
- Apply selected changes; report each field separately.
- Batch/mass edit with preview, selection summary and per-record outcome.
- Persistent edit history: time, save identity, storefront/build, entity type/UID/name, field key, before/after, result, user action and undo status.
- Undo one field or batch only if the current value still equals the value Atlas wrote.
- Re-read and verify every write; never label success before read-back.
- Back up the current save before enabling writes and before first write in a session.
- Require user confirmation that the game is offline/single-player and the active save has a backup.
- Do not bypass protections, DRM or anti-cheat. No unsupported or unverified field writes.
- No FM save file rewriting in the initial editor; live memory changes persist only when user saves in FM.
- Editor disabled after a game patch until the profile is revalidated.

### 3.15 Diagnostics / Tester
- Always-visible small Tester/bug icon in top bar.
- Status panel: game process, storefront, game version, module fingerprint, selected profile, connection age, current save identity (privacy-safe), database counts, scan duration, read errors, missing fields and data provenance.
- Live log console with severity filters and search.
- Copy diagnostics and export a ZIP with logs, build profile summary, schema coverage and anonymised error details.
- Never include save bytes, player private data, account IDs or full save paths unless the user explicitly chooses.
- Clear diagnostics history.
- Provide a safe read-only probe to capture version/profile facts without writing.

---

## 4. Data acquisition strategy

No single current tool proves that it reads every required field. Atlas should merge several sources and record provenance per field.

### 4.1 Memory scanner / live connector
Primary for live world entity data and hidden fields:
- Discover the running FM process and `game_plugin.dll`.
- Read only with least privilege (`PROCESS_VM_READ`, query information); no continuous process patching.
- Discover entity arrays and object relationships from a verified per-build profile.
- Read UID, identity, CA/PA, attributes, positions, personality, contracts, staff, club relationships and other verified records.
- Chunked scanning, capped worker count, overlapping chunk boundaries, adapt to free memory and stop when incomplete/corrupt.
- Use atomic dump/cache writes (`.tmp` → final rename).
- Prefer normal live reads; only retry via snapshot when a defined percentage of memory is unreadable and memory headroom permits.
- Never let a failed/partial scan overwrite the last known-good database.
- Treat every offset as build-specific. A game patch means revalidation.

### 4.2 FM SuperScout import/benchmark
The supplied `FMSuperScout-Setup.exe` hash matches the project's published Windows installer for v1.7.3. The app is read-only and its `dump.json` shape contains metadata, players and staff, including UID, names, club/division, age/DOB, CA/PA, wage, contract expiry, positions, foot, value, listing status, reputation, personality and attribute maps. It is a useful external benchmark and import format.
- Do not depend on its app process to run Atlas permanently.
- Its open-source MIT code may be studied; any code reuse must retain license notices.
- Its dump does not, by itself, represent all season match statistics. Match performance must be imported separately.

### 4.3 FM View Exporter / in-game view CSV
Supplemental for player/club table columns and stats visible in FM:
- Export current player list, squad or scouting table to CSV/HTML.
- Merge by UID; name matching is a fallback and must be marked lower-confidence.
- This preserves what FM exposes in the selected view and its scouting-knowledge masking.
- It can take longer than direct memory scanning and FM limits results in some views. Not a substitute for full-world enumeration.

### 4.4 Direct `.fm` save parsing
Use a read-only save parser as a separate adapter for career/history/competition and saved-state information:
- Read only a settled `.fm` save file; never parse while FM is writing.
- Detect current game build and use a strict parser schema.
- The current FMSave project says its known FM27 layout is `27.0.0+2486695`; other FM27 builds open as unverified, with per-section layout checks and explicit unsupported errors.
- On a known build it can provide player attributes, CA/PA, this-season stats, contracts, staff, clubs, finances, fixtures, league tables, competition rules, suspensions, match histories, training calendars and more.
- Do not assume the known 27.0.0 schema supports 27.0.1/build 26577676; validate each section before use.
- Do not use save parser values to override newer live state without a precedence rule.

### 4.5 Stats merge and provenance
Each field stored in Atlas must carry:
- `source_id` (e.g. `memory_live`, `fm_view_csv`, `fm_save_parser`, `user_import`, `atlas_derived`).
- `source_version` / game build and store.
- `observed_at_utc` and in-game date.
- `confidence` (`verified`, `corroborated`, `estimated`, `unknown`).
- `visibility` / knowledge state where relevant.
- `value` and original raw value if conversion was applied.
- For calculated metrics, formula version and denominator.

Precedence is field-specific. A current in-game memory value should generally supersede an older imported snapshot, but season totals from a CSV view should not be replaced by CA/PA memory fields. Conflicting values are kept as source observations and surfaced in diagnostics.

---

## 5. Current build / platform matrix (research snapshot: 13 Nov 2026)

| Store | Current version fact | Evidence / use |
|---|---|---|
| Steam | FM27 27.0.1, Steam Build ID **26577676**, public update 12 Nov 2026 | Latest researched public Steam build |
| PC Game Pass / Microsoft Store | package version **0.9.2435.0** | Separate Microsoft GDK build; not a Steam build ID |
| FMRTE support listing | FM27.0.1 **2494687** for Steam/Epic and Microsoft Store | FMRTE's own game-version number; do not confuse with Steam Build ID |
| FM27 Live Editor / FMLibre | separate storefront/build profiles | Store-specific identity must be verified at runtime |

**Known Game Pass profile details from FMLibreEditor research:** package `0.9.2435.0`, player array RVA `0x53B7DE0`, staff `0x53B7E28`, clubs `0x53B7F98`, date `0x53597C8`, module image size `0x17969000`, link timestamp `0x6AE8CF77`. The profile claims ten calibration checks passed on a test save dated 3 Jan 2028.

**Known Steam profile details from FMLibreEditor research:** Steam Build ID `26577676`, player array RVA `0x5820810`, staff `0x5820858`, clubs `0x58209C8`, date `0x57CA3B8`, module image size `0x185FA000`, link timestamp `0x6AF38E5C`. Its profile claims checks passed against a test save dated 23 Sep 2027.

These values are research inputs, not a blanket guarantee. Atlas must compare the actual running module fingerprint and run health checks before using a profile. Never hardcode offsets merely because the game is called FM27.

---

## 6. Data model and metric rules

### Canonical entities
- `SaveSession`: internal save ID, manager, managed club, in-game date, store, version profile.
- `Player`: UID, names, DOB, nation(s), current club/team, parent club, positions, foot, CA/PA, rep, contract reference.
- `Staff`: UID, names, DOB, nation, club/team, job/role, staff attributes and contract.
- `Club`, `Team`, `Nation`, `Competition`, `Stadium`, `Award`, `Official`.
- `Contract`, `Transfer`, `Loan`, `Fixture`, `Season`, `PlayerMatchStat`, `PlayerCompetitionStat`, `PlayerSeasonStat`.
- `Snapshot`: entity UID, game date, actual capture time, data hash, supported field set.
- `EditOperation`: entity, field, before/after, store/build/save identity, write/read-back and undo state.

### Attribute mapping
Research indicates common player fields are stored in blocks and some attribute bytes are scaled by five. The scanner must convert raw storage to displayed 1–20 values only after verifying the relevant field map for that build. Do not expose internal offsets as the UI data contract.

Examples from current FM27 field research:
- Common attributes include Crossing, Dribbling, Finishing, First Touch, Passing, Vision, Decisions, Off The Ball, Acceleration, Strength, Stamina, Pace, Jumping Reach, Balance, Agility, Determination, Composure, Concentration and others.
- Hidden fields include Dirtiness, Consistency, Important Matches, Injury Proneness and Versatility.
- Personality fields include Adaptability, Ambition, Loyalty, Pressure, Professionalism, Sportsmanship, Temperament and Controversy.
- Position block includes GK, SW, DL, DC, DR, DM, ML, MC, MR, AML, AMC, AMR, ST, WBL and WBR. Other position forms may be derived / role suitability should be separately verified.

### Per-90 calculations
`per90 = total_stat / minutes_played * 90`
- Only calculate when both values are present and minutes > 0.
- Retain raw total and minutes.
- Do not recalculate if an authoritative precomputed per-90 value is provided unless the UI shows which method it used.
- For rates: `success_pct = successful_events / attempts * 100` when both fields are known.
- Percentile: calculate against a defined cohort (competition + position or role + season); show sample count and cohort name.
- Apply a user-visible minimum minute threshold.
- Do not aggregate competitions unless the competition scope and duplicate semantics are understood.
- A total stat at season scope and the same stat at competition scope may not agree in every game record; preserve source-specific scope.

---

## 7. Architecture

### Recommended modules
1. `Atlas.Desktop` — retain the existing Windows desktop framework after source audit; likely the R7 WPF shell is a starting point, but inspect it before deciding.
2. `Atlas.Core` — domain entities, validation, provenance and feature contracts.
3. `Atlas.Connectors.FM27` — process detection, module discovery, Steam/Game Pass profiles, read-only scanner and health checks.
4. `Atlas.Importers.SuperScout` — FMSuperScout JSON importer.
5. `Atlas.Importers.FMView` — FM View Exporter CSV/HTML importer.
6. `Atlas.Importers.FMSave` — read-only save parser adapter with build gate.
7. `Atlas.Storage` — SQLite repository, migrations, indexes and snapshot storage.
8. `Atlas.Statistics` — per-90, cohort percentiles, leaderboards and derived metrics.
9. `Atlas.Scouting` — filters, saved searches, recruitment rules and weighted fits.
10. `Atlas.Tactics` — formation/role suitability and best XI.
11. `Atlas.Graphics` — config.xml indexing, image lookup and cache.
12. `Atlas.Editor` — isolated, feature-gated write service with safeguards and history.
13. `Atlas.Diagnostics` — logs, profile report and support bundle.

### Storage
- SQLite is the default local source of truth.
- Index by save ID, entity UID, current club, position, age, competition, contract expiry and fields frequently used in filters.
- Use FTS for names and aliases.
- Separate raw observations from normalized current state and historical snapshots.
- Database migrations must be versioned and backed up.
- Keep imported external files and user data local by default.

### Performance controls
- No continuous full process scans.
- Full world scan only on demand, on save load, or when an explicit refresh is triggered.
- Incremental snapshots only when the game date/save identity or tracked data changes.
- Chunked reads, adaptive workers and low-memory fallback.
- All ranking/calculation/search runs against SQLite, not FM memory.
- UI virtualization for large player tables.
- Lazy-load images and charts.
- Suspend refresh while minimised or disconnected.
- Diagnostics record FM CPU/RAM impact during benchmarks; performance targets are validated on the user's PC rather than assumed.

---

## 8. Editor safety model

The editor must be independently disableable. Read-only scouting still works if editing is unavailable.

### Before a write
1. Confirm exact active process, store and module fingerprint.
2. Confirm active save identity and entity UID/object membership.
3. Confirm the specific field is marked verified-writable for this exact profile.
4. Re-read the field and compare with the expected baseline.
5. Preview the old and new values and any dependent state.
6. Create/confirm a save backup.
7. Require a deliberate Apply action.

### During a write
- Write the smallest supported field width.
- Capture native API result.
- Read back immediately and verify equality.
- Report one result per field; partial success is never reported as total success.
- Do not write through an unknown pointer or stale entity reference.

### Undo
- Undo only if current live value equals Atlas's last written value.
- If FM or another editor changed the value since, refuse and explain why.
- Write the old value and verify read-back.
- Persistent edit log must survive Atlas restarts. This is not a guarantee that every game-engine side effect is reversible.
- Include timestamp, player/club UID, field, before, after, process/build, save identity, apply/undo outcome and reason.

### Safety defaults
- Default Editor OFF.
- Default scan/read-only.
- Single-player/offline only.
- No online/shared-save editing.
- No bypassing DRM, anti-cheat or game protections.
- No save-file writes in the first editor implementation.
- On patch/build mismatch, Editor remains disabled until a field-by-field revalidation.

---

## 9. Graphics and FMRTE reference

The uploaded FMRTE archive is **27.0.0 build 2**, an older FM27 build, not the latest FMRTE release. Its archive includes UI examples for the player header and Edit History but not source code.

FMRTE's current public documentation says headers can use:
- The game's built-in images.
- Installed graphics-pack images.
- Initials/fallbacks if no image is available.
- Custom portraits and logos.

FMRTE's documentation describes a setting at Settings → General → Appearance → Header → Show header images / Use the game's built-in images. It does not reveal the proprietary internal implementation.

**Atlas implementation goal:** match the image presented for an entity by resolving the same FM graphics mappings and UID conventions used by the user's installed graphics packs, and use built-in game art only through an accessible/legitimate game asset path. The graphics adapter should resolve `config.xml` records, cache file paths and show the source pack. If there is no face/crest, show a neutral placeholder.

FMRTE's edit history is a useful UX reference: grouped edits by entity/time, old → new values, per-field Undo and batch Undo. Atlas should improve on this with a persistent audit history and stale-value protection.

---

## 10. Research/tool comparison

| Tool / source | Useful research contribution | Limit / Atlas decision |
|---|---|---|
| **FMSuperScout 1.7.3** (user installer hash verified against public release) | Fast read-only live memory scan; players/staff, CA/PA, attributes, personality, roles and transfer context. JSON dump gives a practical import schema. | Excellent performance and data-coverage benchmark. Atlas should own its connector; no reliance on another app to stay running. MIT repository, retain notices if code reused. |
| **FMLibreEditor 0.5.1** | Public open-source live editor for Steam and Game Pass; separate per-store/build profiles; safe reads/writes and diagnostics; Game Pass profile 0.9.2435.0 and Steam profile 26577676. | Best current reference for exact storefront profiles and guarded edit approach. GPL-3.0: use as a separate tool or comply fully with GPL if deriving code; don't copy its implementation into a closed-source proprietary app without resolving licensing. |
| **TalentSeer 27.1.2** | Free live scouting/editor. Search, hidden attributes, comparison, shortlists, snapshots, batch editing, edit history and undo; current update adds interest estimate and more editing. | Strong feature/UX benchmark. Don't assume Game Pass support until independently verified on user's store. |
| **FMRTE 27** | Richest reference for editor feature surface, search, stats, images, built-in game graphics, edit history, undo, mass edit and entity coverage. | User's uploaded build 2 is older. Public latest found is build 11 for 27.0.1; supported game-version number is distinct from Steam Build ID. Proprietary; don't reverse engineer or copy code. |
| **FM Dossier 1.15.4** | Player search, reports, tactics, match/team analytics, recruitment, stats and FM27 workflows. | Public 1.15.4 release is recent; author tests Steam, additional storefront checks are described but Game Pass testing is not fully confirmed. Benchmark functions, not source code. |
| **Genie Scout 27 Beta 4 / build 2705** | Player/staff search, shortlists, best XI, potential development and FM27 season stats (g edition). Release page lists Steam, Epic, Xbox/PC Game Pass and Console. | Important broad compatibility/reference product; use its actual data visibility, not assume all data is accessible. |
| **FM Player Analyzer 2.2.3** | Direct `.fm` read-only parsing; FM View Exporter integration; 47 visible attribute map; 79 FM27 roles and IP/OOP shapes; squad depth and development snapshots; graphics indexing from config.xml. | GPLv3. Save parser honors FM scouting masking and does not expose hidden CA/PA/personality fields; best for visible/save data and analytics architecture. |
| **FM View Exporter** | Exports FM's visible player tables to CSV/HTML with UID and many columns; F9, supports player lists/squad/scouting. | Supplemental; can be slow and subject to game search result caps. Statistics only when visible/exported. GPLv3 plugin. |
| **FMSave / rhiever/fmsave** | Direct save parsing for career/fixture/competition/history and other serialized data. | Read-only and strict per-build schema. 27.0.0 profile does not imply 27.0.1 support. Use only after current-build validation. |
| **Teknik Oda / Toby 1.0.9** | Reads FM's match data, tactical shapes, shot maps, match events and offers in-game assistant. 1.0.7 was tested on build 26577676 with 12/12 checks; 1.0.9 has automatic updates and Game Pass folder fix; Steam tested, Game Pass user reports. | Valuable match-performance research and optional companion inspiration. It doesn't write to save; BepInEx optional in-game feature. Not a world database connector. |
| **FaceDrop 0.3.1** | Drag/drop faces for FM27 players, regens and managers. | Requires Steam + BepInEx 6 per current instructions; not a core Atlas dependency and no documented Game Pass support. |
| **Customize Editor alpha** | Official-editor/skin UI customization and unlocking of editor UI options. | Not a live database reader or memory editor. Treat as UI/modding research only. |
| **SaveMyTime** (user ZIP) | BepInEx Unity IL2CPP training schedule helper; manages `.fmf` schedule files. | Not an Atlas data source; optional training workflow integration could be considered later. |
| **SiRRAHx FM27 Live Editor** | Game Pass tested, Steam/Epic supported but not confirmed by tester in its release note. Player/club/transfer/loan editing, optional in-game button, logs. | Useful Game Pass compatibility and diagnostics reference. Early-access and field/profile validation required. |
| **Jackhamerp24 FM27 Editor** | Player/staff/club/nation editor feature checklist; broad entities and transfers. | Another feature/UX benchmark; validate each current build/store claim independently. |
| **FM27 EditorMod alpha** | Customises official editor UI/options. | Not a replacement for live career editor; don't confuse database setup file editing with live-save memory. |

---

## 11. Build and test roadmap

### Stage A — source audit and project recovery
- Obtain and inspect the actual R7.0.2 source archive (or latest source folder).
- Compare R7.0.0 → R7.0.1 → R7.0.2 and R5.2.1 FM26.
- Inventory what is implemented, stubbed, UI-only or unverified.
- Review build scripts, dependency versions, project architecture and historical compiler errors.
- Recover tested connection code and diagnostics before redesign.
- Retain FM26 functionality where it remains useful, but use versioned FM26/FM27 data adapters.
- Record licenses and code provenance for every reused external library.

### Stage B — application foundation
- Rebuild shell in Obsidian + Swiss Editorial design.
- Shared domain/data contracts.
- SQLite migrations and test fixtures.
- Tester panel / structured log pipeline.
- Import adapters for SuperScout JSON and FM View CSV.

### Stage C — live world extraction
- Store and build detection.
- Steam 26577676 profile.
- Game Pass 0.9.2435.0 profile.
- Entity array census, UID checks and health validation.
- Player/staff/club relationship coverage.
- Attribute, positions, CA/PA, personality, contracts and club relationships.
- Full-scan quality report and no-overwrite on partial scan.
- Performance benchmark on user's PC.

### Stage D — intelligence workspaces
- Database explorer.
- Scouting filters and saved searches.
- Player profiles and image lookup.
- Statistics import and merge.
- Per-90, cohorts, percentiles and leaderboards.
- Comparison Studio.
- Squad Centre and best XI / depth.
- Tactical intelligence.
- Development snapshots and newgen radar.
- Transfer and contract intelligence.
- Club and competitions.
- Atlas Intelligence using the local database.

### Stage E — guarded editor
- Exact-build write profile separated for Steam and Game Pass.
- Field-by-field witness and write test.
- Preview, save backup, apply, read-back, history and guarded undo.
- Player first; then staff and clubs; then other entity types.
- Editor ON/OFF and persistent test diagnostics.

### Stage F — integrated acceptance
- Clean install on Windows 10/11.
- FM27 Steam build 26577676 and Game Pass package 0.9.2435.0 separately.
- Switch saves, switch clubs, advance date, reload game, FM patch mismatch.
- Large save and low-memory performance test.
- Every leaderboard audited against its source and cohort.
- Editor on copied single-player test career only.
- Package portable build and optional installer after real Windows build succeeds.

---

## 12. Acceptance tests

### Connection and data coverage
- Detect Steam and Game Pass separately.
- Match exact fingerprint; reject unknown build in write mode.
- Compare entity counts to independent scanner outputs on same save.
- Validate random sample players/staff/clubs against FM screens.
- Verify UIDs and club/parent-club relationship.
- Report missing or partial entities.
- Partial scan must never replace known-good database.

### Scouting and statistics
- Search player by name and UID.
- Filter by any mapped attribute and hidden field when allowed.
- Query all accessible leagues.
- Combine a performance threshold with age/value/wage/position.
- Verify Dribbles/90 = successful dribbles or FM-defined dribbles divided by minutes as explicitly selected.
- Test minimum minutes.
- Ensure missing stats are not zero.
- Verify percentiles state the cohort and sample size.
- Merge CSV with memory entity by UID and flag unmatched rows.

### Images
- Verify multiple graphics packs, duplicate config mappings and fallback.
- Confirm no config files are rewritten by read-only image indexing.
- Verify UID match for players, clubs and nations.
- Refresh after graphics pack update.

### Editor
- Unsupported build leaves editor disabled.
- Unsupported field cannot be edited.
- Preview matches actual field.
- Read-back determines success.
- Undo refuses stale value.
- Persistent history survives Atlas restart.
- Confirm backup file and test-save confirmation before any write.
- A failed or partial batch reports field-by-field outcomes.

### Diagnostics
- Export a support bundle.
- Check that it does not contain save data or account secrets by default.
- Include module/store/build/profile, counts, timings and error codes.
- Allow user to review bundle contents before sharing.

---

## 13. Known gaps / honest status at handoff

1. The user's R7.0.2 Compiler Fix ZIP was attached directly to the active chat and inspected for this 7.1.0 integration pass. The update is built on that WPF/SQLite source tree rather than the earlier browser Scout Lab prototype.
2. This environment has no .NET SDK / MSBuild installed, so a Windows WPF executable cannot be compiled or live-tested here. The new logged build and publish scripts are provided for the user's Windows PC.
3. The user's third-party research tools were inspected statically; installers were not run or included in this project.
4. Steam and Game Pass build profiles were researched from current public source repositories, but Atlas itself has not yet been validated against the user's actual FM27 installation. Separate tested adapters remain a requirement.
5. Live season statistics are a distinct data domain from hidden attributes / CA/PA. The save bridge retains source tables, but typed field coverage and metric semantics are not proven for every build.
6. The new Role Fit Lab is a transparent estimate and not an official SI formula. Its weights need comparison against in-game role familiarity and player performance.
7. FMRTE proprietary internals were not reverse-engineered. Public docs, user-provided UI material and documented settings informed feature requirements; proprietary code was not copied.

**Next test input:** run the 7.1.0 source on Windows with .NET 8, import a COPY of the user's FM27 Game Pass career and return the build log plus Tester & Logs diagnostics. The source now includes the role-fit, per-90, guarded local editor and support diagnostics workspaces. Live game editing and the wider backlog remain gated until the data and store/build profiles are validated.

---

## 14. Research source links

### Official/current game and platform
- FM27 official platform / launch information: https://www.footballmanager.com/fm27
- SteamDB FM27 depot/build tracking: https://steamdb.info/depot/4530373/
- FMRTE supported versions / build page: https://www.fmrte.com/files/category/54-football-manager-27/

### Main live readers and editors
- FMSuperScout: https://github.com/mavarobli/FMSuperScout
- FMLibreEditor: https://github.com/tozmoz/FMLibreEditor
- SiRRAHx FM27 Live Editor: https://github.com/SiRRAHx/fm27-live-editor
- Jackhamerp24 FM27 Editor: https://github.com/Jackhamerp24/Football-Manager-27-Editor
- barak14 FM Editor research: https://github.com/barak14/fm-editor
- TalentSeer FM27 discussion/download: https://sortitoutsi.net/content/78140/talent-seer-is-here-a-free-scouting-and-editing-tool-for-football-manager-27

### Scouting, stats, save parsers and match analysis
- FM Player Analyzer: https://github.com/ElectroHugin/FM-Player-Analyzer
- FMSave: https://rhiever.github.io/fmsave/
- FM Dossier: https://www.fmdossier.dev/
- FM Genie Scout updates: https://geniescout.com/updates/
- Teknik Oda / Toby 1.0.9: https://sortitoutsi.net/content/78456/teknik-oda-toby-a-coach-who-reads-your-fm27-match-from-fms-own-data-open-beta
- FM View Exporter: https://github.com/ElectroHugin/FM-View-Exporter

### Graphics and official-editor UI
- FaceDrop: https://sortitoutsi.net/content/78390/facedrop-drag-drop-faces-for-fm27-players-regens-managers
- FM27 EditorMod alpha: https://community.sports-interactive.com/forums/topic/613004-wipfm27-editormod-customize-editor-alpha-version/
- FMRTE docs — custom images: https://docs.fmrte.com/graphics-and-files/custom-images/
- FMRTE docs — interface tour: https://docs.fmrte.com/getting-started/interface-tour/
- FMRTE docs — settings: https://docs.fmrte.com/help/settings/
- FMRTE docs — player statistics: https://docs.fmrte.com/ (Player statistics guide)

### Source/license notes
- FMSuperScout repository is MIT-licensed.
- FMLibreEditor and barak14/fm-editor are GPL-3.0 projects.
- FM Player Analyzer and FM View Exporter are GPLv3.
- Third-party installers/binaries are not to be bundled into Atlas. Keep license notices with any permitted code reuse and prefer clean-room implementation of behavior/field mappings where license compatibility is uncertain.

---

## 4. Extended benchmark additions (13 November 2026)

The expanded research register and detailed requirements are maintained in `EXTENDED-RESEARCH-AND-BACKLOG.md`. The following capabilities are now added to the master product scope:

- FM27 **dual-phase roles** (in possession / out of possession), transparent configurable role ratings and a whole-squad optimal XI/B-team solver.
- Starter replacement impact, position gap analysis, recruitment lift simulation, player style-vs-level similarity and tactic comparison.
- Staff job preferences, coaching-area ratings, free-agent staff search, club centre, facilities, wage room, youth academy and three-season succession planning.
- Injury/return dates, retirement plans, transfer-arranged status, full homegrown training links, languages, morale, promises and eligibility where verified.
- Competition-specific squad registration rules and a clear unsupported-league status.
- A statistics schema that covers FM27's more precise per-90 metrics, pass lengths and version-specific defensive-action definitions; minimum sample thresholds and percentiles are mandatory.
- A Match Lab with opponent reports, shot-map/zone analysis, post-match reviews and evidence-backed training suggestions, only where verified data is available.
- A three-source ingestion model: live memory, direct read-only save parser and FM view/CSV/HTML imports. Every field stores provenance and build context.
- A UID-based graphics resolver and diagnostics for face, logo, flag and competition images, including config.xml validation and pack precedence.
- Field-level editor capability registry; Editor OFF by default; staged edits, before/after preview, compare-before-write, read-back verification, guarded Undo, batch preview and persistent history.
- Local report exports, streaming/privacy mode, saved views, profile tabs and a diagnostics drawer with redacted logs.
- Optional BepInEx in-game companion, plugin compatibility checker, pre-game editor helper and training mod compatibility. None is a required dependency for the desktop application.

The full evidence, compatibility cautions, source list, P0/P1/P2 priorities and 30-case test matrix are in `EXTENDED-RESEARCH-AND-BACKLOG.md`.
