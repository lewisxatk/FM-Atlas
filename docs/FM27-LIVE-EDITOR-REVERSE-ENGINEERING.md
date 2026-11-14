# FM27 Live Editor Reverse-Engineering Findings — 14 November 2026

## Purpose and scope

This note records the FM27-specific research used to continue FM Atlas. It is additive and does not replace the V10 product or its controlled acceptance plan. It distinguishes source facts, external research findings and unverified hypotheses.

**Working branch:** `feature/v9-all-roadmap`  
**Integration PR:** #5 — FM Atlas V10: FM27 Data Hub & Pre-Testing Integration  
**Current boundary:** V10 is a read-only pre-testing build. Live-memory Apply and Undo remain hard-disabled.

## 1. Current FM Atlas source audit

The current V10 source already contains a guarded live-memory writer implementation, but its public gate is deliberately false:

- `LiveMemoryEditService.AllowLiveWritesForThisBuild = false`.
- `LiveWritesEnabled` reports that value.
- `EnsureLiveWritesEnabled()` rejects Apply/Undo before a write handle is opened.
- The pre-testing instructions explicitly prohibit bypassing this gate.

The current attribute schema contains:
- 47 ordinary visible technical, set-piece, mental, physical and goalkeeper attributes.
- Five hidden-field candidates: Consistency, Important Matches, Injury Proneness, Dirtiness and Versatility.
- Eight personality fields.
- A separate position mapping used by the live reader.

**Important:** the schema comments say the five hidden offsets are inherited FM26 leads and remain provisional until FM27 witnesses confirm them. They must not be advertised as verified FM27 fields or made writable merely because a candidate offset exists. The eight personality bytes are also currently described as standard-player-only because player-coach records can have a shifted person block.

The writer's current schema and the live reader's coverage are not proof of complete staff, club, contracts, transfers, promises, tactical fields or all player data. The V10 read-only gate is intentional and must remain in place until acceptance evidence exists.

## 2. FM27 external implementation research

### A. Jackhamerp24 / Football-Manager-27-Editor

Repository: https://github.com/Jackhamerp24/Football-Manager-27-Editor

The project documents a Windows live-memory editor for FM27 Steam and Xbox/Game Pass. Its README reports:
- Exact game-build requirements and separate store-specific offset profiles.
- A process-memory architecture centred on `fm.exe` and `game_plugin.dll`.
- A per-build offset registry, rather than one universal set of addresses.
- A separate read-only memory scanner and offset-finding/calibration tools.
- Field-level preflight, old-value checks, read-back verification and edit history/Undo.
- A broad entity model covering players, staff, clubs and other game objects.
- A calibration save with known values compared against FM and FMRTE.

Its README also explicitly says player attributes can drift back towards a level consistent with CA over in-game days. This matters for FM Atlas: a value changing back later is not necessarily a failed memory write; ability/attribute relationships and subsequent game simulation must be reported separately.

The repository is GPL-3.0. FM Atlas should use it as a research reference and implement its own code. Do not copy source files or port code without a deliberate licence review and attribution/compliance decision.

### B. barak14 / fm-editor

Repository: https://github.com/barak14/fm-editor  
FM27 memory layout notes: https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md

The FM27 notes are unusually useful because they separate witnessed FM27 values from inherited FM26 schema guesses. They describe:
- Build/module fingerprinting and module-relative RVAs.
- Entity-array discovery and object identity checks.
- Store/build-specific profiles.
- Live write tests where values were read back, compared with FM's own UI and undone.
- A cache caveat: FM may continue showing previously cached entity values after a memory write; a day advance refreshed some tested screens.
- Field-level evidence tables, known limitations and explicit unverified states.

A key lesson is that successful memory read-back, FM UI refresh and persistence after a normal FM save/reload are three different acceptance checks. Atlas must not collapse them into one green success state.

The repository's published FM27 memory-layout notes also warn that much of its initial FM27 schema began as an FM26 copy and only explicitly listed fields have been checked against FM27. FM Atlas must apply the same evidence discipline to its own mappings.

### C. FMRTE 27 behaviour reference

Official site: https://www.fmrte.com/  
Documentation: https://docs.fmrte.com/

FMRTE 27 publicly documents live editing of players, clubs and competitions, an edit history with Undo, per-field unsaved indicators, and a confirmation workflow. Its documentation also notes that some changes may only appear after the game advances.

This is a product-behaviour reference, not permission to reproduce proprietary implementation details. The parity target for FM Atlas is the user experience and reliable behaviour, not copied code.

## 3. Required FM Atlas architecture

The long-term editor should be divided into explicit layers:

1. **Build profile** — exact store, executable/module identity, game build and module fingerprint. Unknown or changed builds fail closed.
2. **Entity discovery** — player/staff/club arrays and relationships; stable UID and entity-class verification before any operation.
3. **Field catalogue** — field key, label, entity type, offset/RVA, byte width, encoding, valid range, build profile, source, evidence level and read/write status.
4. **Read model** — current value plus raw bytes, provenance and freshness.
5. **Edit draft** — user changes held locally, with old value and intended value. Drafting never touches FM memory.
6. **Preflight** — re-resolve identity and field address; confirm process/build/session generation; compare current raw bytes with the staged original bytes; validate ranges and encoding.
7. **Apply** — only fields with a witnessed writable mapping may be written. All fields in a batch must pass preflight before the first write.
8. **Read-back** — read exact bytes back and decode them. A mismatch is a failed write, not a warning.
9. **Undo** — restore only if the same process, career generation, entity identity and expected post-edit value still match.
10. **FM confirmation** — separately mark whether the in-game UI refreshed and whether the value persisted after FM's own save and reload.

Never use a guessed offset, a name-only match or an inherited field as a write target. Keep unsupported fields visible as unsupported/research-needed, not as disabled controls that imply support.

## 4. Field coverage plan

### Players

- Preserve the 47 visible attribute fields as the first candidate group, but confirm each against the exact installed FM27 build and in-game witness values.
- CA and PA must be treated as separate fields with explicit encoding/range validation.
- Position suitability/position familiarity must remain distinct from Tactical Effectiveness.
- Hidden attributes remain read-only/research-only until individually witnessed.
- Personality fields require separate player and player-coach identity/layout checks.
- Physical/body fields, nationality, reputation, relationships, preferred moves, languages, injury, bans, happiness, contracts and transfer clauses each need their own entity/field mapping and evidence. Do not infer that a reader mapping is automatically a safe writer mapping.

### Staff

- Staff must be discovered as staff entities, not assumed to be ordinary player objects.
- Validate staff UID, class, person-block layout, club relationship and role.
- Coaching attributes, mental attributes, knowledge, CA/PA, reputation, licence, caps/goals, role ratings and contracts need separate field-level witnesses.
- Training-star displays may be derived from underlying attributes; do not assume the displayed stars are stored as a writable field.

### Clubs

- Club identity and club info/finance object pointers must be independently resolved.
- Facilities, reputation, finances, budgets, status, ownership, affiliations, kits/colours, stadium and club vision each need their own field catalogue and evidence.
- Some game screens may have duplicate values or cached copies; verify all relevant copies and compare against the FM UI before claiming parity.

### Other entities

Competitions, nations, stadiums, cities, awards, referees, injuries, promises, transfers and contract structures are separate research tracks. Their existence in another editor does not establish that Atlas has a valid mapping.

## 5. Tactical Effectiveness — separate reverse-engineering workstream

Sports Interactive's FM27 feature description identifies four core areas:
- Tactical Understanding
- Player Ability
- Player Buy-In
- Next Match Information

It also describes Tactical Complexity as a separate measure influenced by positions and team/player/opposition instructions. Tactical Effectiveness appears across Tactics, Training, Recruitment and Scout Reports and changes as tactics, training, recruitment, player performances and wider events change.

This confirms that Tactical Effectiveness is not a player's positional rating and must not be derived from the existing Atlas position-fit score.

Research tasks:
1. Identify the actual FM27 UI locations and the exact gauges/labels exposed for team and player contexts.
2. Capture controlled before/after observations from a disposable career while changing one factor at a time: training, tactical instructions, squad/player role suitability, player buy-in and next-match opposition information.
3. Establish whether the UI exposes numeric values, categorical bands, component gauges or only rendered bars.
4. Trace save/live-memory candidates only after matching controlled changes to the UI. Treat offsets and formulas as unknown until independently witnessed.
5. Determine which values are player-specific, squad-wide, tactic-specific, match-specific or derived.
6. Preserve time/context: tactic slot, match opponent, training week, squad and player identity.
7. Build a separate evidence-backed reader and display. Do not write Tactical Effectiveness until a genuine writable game field and safe semantics are proven.
8. Keep Tactical Complexity separate from Tactical Effectiveness and from role/position ratings.

Official SI feature reference: https://www.footballmanager.com/fm27/features/fm27-training-and-tactical-effectiveness

The official description confirms the system's components and intended behaviour; it does not disclose the underlying numeric formula or memory layout. No formula should be invented.

## 6. Acceptance matrix before enabling any live write

| Gate | Required evidence |
|---|---|
| Build identity | Exact Steam/Game Pass build profile and module fingerprint |
| Entity identity | UID, entity class, array membership and relationship re-resolution |
| Field mapping | Raw bytes, width, encoding, range and independent FM27 UI witness |
| Read accuracy | Multiple known entities compared with FM27 screens |
| Write accuracy | One-field change on a disposable offline copy; exact memory read-back |
| UI behaviour | FM refresh/caching behaviour documented separately |
| Undo | Original bytes restored only after identity/session/current-value preflight |
| Persistence | FM saves normally, closes/reloads, and value is checked again |
| Regression | Automated fake-memory tests plus real witnessed calibration cases |
| Rollback | Batch abort and partial-write recovery tested; no silent partial success |

The first V10 acceptance round remains read-only. Only after the above evidence is reviewed should a later build selectively enable specific field groups. Do not enable every field at once.

## 7. Immediate continuation order

1. Preserve the V10 read-only gate and existing product foundation.
2. Finish the real Windows/FM27 read-only acceptance round and record exact build/store details.
3. Compare current live player/staff/club reads with FM27 UI witnesses; fix identity and field mapping defects first.
4. Turn the player field catalogue into explicit per-field evidence metadata (verified/read-only/writable/provisional/unsupported).
5. Build a calibration fixture from privacy-safe known values, not a real user career save.
6. Add one narrowly scoped, opt-in write pilot only after independent field witnesses, Undo and save/reload persistence tests.
7. Expand by field group and entity type only when each group passes the same gates.
8. Research Tactical Effectiveness independently using controlled observations and FM27 UI evidence.

## Sources

- Jackhamerp24, Football-Manager-27-Editor: https://github.com/Jackhamerp24/Football-Manager-27-Editor
- barak14, fm-editor: https://github.com/barak14/fm-editor
- barak14, FM27 memory-layout notes: https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md
- FMRTE 27: https://www.fmrte.com/
- FMRTE documentation: https://docs.fmrte.com/
- Sports Interactive, FM27 Training & Tactical Effectiveness: https://www.footballmanager.com/fm27/features/fm27-training-and-tactical-effectiveness
