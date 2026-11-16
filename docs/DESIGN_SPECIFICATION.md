# FM Atlas — Approved Design Specification

**Revision:** 001  
**Date:** 2026-11-16  
**Status:** Design direction recorded from user instructions and references REF-001 to REF-003. Not yet implemented.

## 1. Product direction

Build a genuinely new FM Atlas desktop application, not a reskin or patch of the previous interface. The approved design combines:

- **Application composition:** REF-001 — the dashboard shown inside the HorizonX share capture.
- **Advanced-search structure:** REF-002 — FMRTE's search customisation dialog.
- **Visual identity:** REF-003 — Watched Earth's black/off-white/orange editorial language.

The result must be recognisably based on these references. Do not replace the chosen layouts with a newly invented dashboard, sidebar or filter-builder.

## 2. Visual system

### Colour and surfaces

- Main application canvas: solid or near-solid black.
- Primary text: warm off-white / ivory.
- Secondary text: muted grey.
- Accent: restrained vivid orange, used for emphasis, selected states, focus and key actions.
- Panels: subtle charcoal variations against the black canvas.
- Dividers and borders: thin, low-contrast grey lines.
- Avoid large bright cards, excessive gradients, blue accents as the dominant colour, glossy effects and decorative shadows.

### Typography

Use a deliberate pairing inspired by REF-003:
- Editorial/display face for large page headings and brand moments.
- Highly legible sans-serif for body copy, controls, labels and dense data.
- Monospaced face for metadata, technical values, timestamps, compact status and selected numeric readouts.

The Watched screenshot contains a distinctive high-contrast editorial heading treatment and monospaced uppercase secondary text. Reproduce the typographic character and hierarchy, not necessarily the exact proprietary font files. Select licensed/local fonts with similar metrics and test readability at normal Windows desktop scaling.

### Spacing, shape and controls

- Compact, deliberate spacing; avoid clutter.
- Buttons should be comfortably clickable but not oversized.
- Use restrained corner radii, consistent across controls.
- Left navigation buttons should visually blend into the main background. Each item gets a subtle rounded hit area; hover/selected states make the item identifiable without a large separate sidebar card.
- Avoid a heavy boxed sidebar or oversized pill buttons.
- Use thin outlines and separators sparingly.
- Ensure text contrast and control hit targets remain accessible.

## 3. Main application composition — REF-001

The embedded Nocturne-style desktop dashboard in REF-001 is the main composition reference.

### Persistent shell

- Compact left navigation rail/sidebar.
- Main content area to the right.
- Top utility/header row with current location/page context and global actions.
- Main content organised into restrained panels/cards.
- Consistent alignment, tight but readable data density and clear hierarchy.
- Footer/status region may show connection/save state where useful.

### Left navigation

The user explicitly says **there must not be a Search button/item in the left navigation**.

Navigation is for product areas, not a duplicate search entry. Proposed destinations to map to the actual application capabilities:
- Dashboard
- Players
- Staff
- Clubs
- Save Manager / database
- Settings

The final labels and grouping must be reconciled with the approved product scope, but no left-side Search item is permitted. Search is global and belongs at the top of the main workspace.

### Dashboard content

Adapt the reference dashboard's restrained card and panel layout to real FM Atlas information:
- current game/save and connection status;
- recently opened or pinned players;
- useful save/database summary;
- recent edits and pending unsaved changes;
- shortcuts to Players, Staff, Clubs and Save Manager;
- diagnostics or compatibility status where genuinely useful.

Do not invent financial metrics or placeholder analytics simply to fill dashboard cards. If data is unavailable, show a clear empty state.

## 4. Top search and game actions

The main workspace header must include:

1. **Quick search bar at the top** for fast lookup.
2. **Advanced Search button at the top**, adjacent to or clearly associated with quick search.
3. A prominent **Load Game** action on the right side of the top header. It should be visually larger/more prominent than ordinary toolbar actions, but not excessively oversized.
4. Save actions styled with the restrained, compact button language of FMRTE.

Search must not be moved into the left navigation.

### Quick search behaviour

- Search players and other supported entities quickly from the top bar.
- Results must come from the currently loaded data source/save.
- Clearly distinguish player, staff and club result types.
- Selecting a player opens that player's profile.
- Provide keyboard focus and sensible keyboard navigation.
- Show a clear empty state and loading/error state.
- Do not show fabricated sample records as if they came from the save.

### Advanced Search behaviour

- Open the FMRTE-style customisation dialog specified in section 5.
- Quick search and Advanced Search are distinct entry points but share the same underlying search/index/query service.
- The Advanced Search button is a real action, not decorative.

## 5. Advanced Search — REF-002 is the controlling reference

The uploaded FMRTE screenshot is the authoritative structure. The goal is to reproduce the dialog's arrangement and information architecture as closely as practical, with FM Atlas typography, black/off-white/orange styling and consistent control sizing.

### Dialog structure

- Centred modal/dialog over the search results workspace.
- Title at the top: a concise search-customisation heading.
- Entity selector tabs near the top: **Player / Coach / Club**.
- Category navigation in a narrow vertical column on the left.
- Filter content arranged in grouped panels to the right.
- Two-column filter-panel arrangement where the available window size permits, following REF-002.
- Position-pitch panel in the upper-right area for player position filters.
- Clear **OK** and **Cancel** actions along the bottom.
- Controls must remain readable and usable; preserve the visual density and structure of the reference rather than expanding every field into a full-width wizard.

### Left category navigation

Use the FMRTE-style vertical category list:
- General
- Contract
- Technical/Gk
- Mental Attributes
- Physical/Hidden
- Preferred Moves
- Relations

Selecting a category displays its relevant grouped filters in the main content area. The active category is visibly marked but not made oversized.

### General category

Follow REF-002's grouped approach and include supported fields such as:
- Personal details: name, age, nationality, other nationality, EU nationality, international caps, international goals, height, weight.
- Division/team lookup where supported.
- Wonderkid/regenerated-player flags where available.
- Reputation: home, current and world.
- Ability: CA, PA, difference and recommended/current ability fields where supported.
- Player positions: pitch selector and positional familiarity/current/potential ratings.
- Training status where available.

The screenshot is a layout/field reference. Exact field availability and meaning must be verified against the selected FM version and loaded data. Do not fabricate fields or silently map unlike values.

### Other categories

- **Contract:** contract dates, wage, value, clauses and other fields only where the data source supports them.
- **Technical/Gk:** all applicable technical and goalkeeper attributes supported by the game/data provider.
- **Mental Attributes:** all applicable mental attributes.
- **Physical/Hidden:** physical attributes plus supported hidden attributes, clearly distinguished.
- **Preferred Moves:** supported player traits/preferred moves.
- **Relations:** supported relationships and related-entity filters.

### Search controls and query semantics

- Numeric fields support minimum/maximum ranges, with valid bounds for each field.
- Boolean fields use compact checkboxes/toggles.
- Enumerated fields use compact dropdowns.
- Entity lookups support searchable selectors where needed.
- Multiple conditions can be combined.
- Search semantics (AND/OR) must be explicit and tested; do not silently change how filters combine.
- Preserve entered filters when switching categories.
- OK applies the filter configuration and runs the search.
- Cancel closes without applying uncommitted changes.
- Validation explains invalid ranges or unsupported fields.

## 6. Save controls and Load Game

- Provide compact Save / Save Changes actions styled consistently with FMRTE's restrained controls.
- The prominent **Load Game** button sits on the far right of the top header.
- Clearly distinguish loading a save/data source from saving edits.
- Show connection state and selected save identity.
- Disable or explain actions that cannot currently be performed.
- Never imply that an edit has been written to the live game when it has only changed an in-memory UI model or local database.

## 7. Player, staff and club functionality

The wider product scope remains:
- real player profiles and player data;
- CA, PA and all supported visible attributes;
- supported hidden attributes and personality fields;
- position familiarity and preferred moves;
- staff profiles and supported staff editing;
- club profiles and supported club editing;
- contract and other supported information;
- live-save connection and editing where technically verified;
- undo/change history and clear unsaved state.

All displayed values must have a traceable source and correct entity/version mapping. Unsupported values must be labelled unavailable rather than fabricated.

## 8. Technical research and reverse engineering

The design does not reduce or replace the existing research requirements. Keep the research register as the technical source of truth and continue investigating:
- FM27 Game Pass process and module identification;
- build/version fingerprints and compatibility profiles;
- player/staff/club entity discovery;
- CA/PA and attribute field mappings;
- hidden attributes and personality;
- read/write boundaries and validation;
- read-back verification and undo;
- save switching and stale pointer invalidation;
- offline save parsing versus live-memory data;
- search indexing and data provenance.

Reference research projects already recorded in [RESEARCH_REGISTER.md](../research/RESEARCH_REGISTER.md) include FMLibreEditor, fm-editor, fmsave, FMSuperScout, gilet, FMM Editor and FMRTE documentation. Re-check licences before reusing code. A documented field or offset is not verified for the user's exact FM27 Game Pass build until tested.

## 9. Design and implementation gates

For each screen/component:
1. Link the controlling reference ID.
2. Record exact layout, hierarchy, dimensions/proportions and states.
3. Record all fields and their data sources.
4. Record each control's behaviour.
5. Approve the specification before implementation.
6. Implement the approved design without unapproved substitutions.
7. Compare a desktop screenshot side-by-side with the reference.
8. Test each interaction against real data.
9. Record deviations and obtain approval for material changes.

A screen is not complete because it looks similar. It must also display real information and its controls must work.

## 10. Explicit exclusions for this initial rebuild

- No Wyscout/StatsBomb-style scouting analytics in the initial simple-editor release.
- No invented dashboard metrics or fake data.
- No left-navigation Search item.
- No oversized controls.
- No replacement of the FMRTE advanced-search layout with a generic filter-builder.
- No reskin-only changes to the old application presented as a fresh rebuild.

## 11. Current implementation status

Everything in this document is a **design/product requirement**, not a statement that the feature has been implemented. The reference set is approved as the current direction; development remains gated until the existing source/recovery baseline and technical architecture have been audited.


## 12. Full-screen player profile — REF-004

**Controlling reference:** `IMG_8153.png` (REF-004), a Profiler Football club-intelligence/player profile screen showing Lamine Yamal.  
**Status:** Approved player-profile layout and interaction direction; not implemented.

### Overall presentation

- A player profile opens as a **full-screen workspace** within the FM Atlas main application, not as a small modal or narrow side drawer.
- The selected player's profile is shown in a dedicated application tab.
- When multiple players are opened, each player gets a separate tab at the top of the workspace. Tabs must make it easy to switch between open players and close them without losing the underlying search results.
- Keep the global application shell and global top actions consistent with the approved main application design. The player tabs belong at the top of the player workspace beneath/within the application header, not as a replacement for global navigation.
- The screenshot is a layout and information-density reference. Its third-party ratings, percentile values, xPerf scores, charts and reliability indicators are not automatically FM game attributes and must not be invented or presented as official FM data.

### Three-column profile composition

#### Left: player identity and core information

A persistent left-hand information column contains:
- Player photograph, if available.
- Player full name.
- Known name / display name where applicable.
- Age and date of birth.
- Nationality and second nationality.
- Club and team.
- League and competition.
- Primary position and other positions.
- Preferred foot.
- Height and weight.
- Player ID and other useful identity metadata where appropriate.
- Additional compact information groups such as contract or market information where supported.

**Graphics locator:** Resolve the player's face from the user's installed Football Manager graphics using player ID and supported config mappings. If a face is not found, show a deliberate neutral placeholder; do not substitute an unrelated player's image. Keep graphics lookup/cache separate from the core player record.

The screenshot's identity rail is visually distinct but compact. It should not consume so much width that the central attributes become cramped.

#### Centre: FM attributes

The central area is the main player-editor workspace.

- Display the full applicable Football Manager attribute set.
- Use the same attribute names, grouping, order and conventions as Football Manager/FMRTE for the selected game version.
- Preserve the familiar technical, mental and physical groupings and goalkeeper attributes where applicable.
- Display the real current values from the selected live save/data source.
- Make supported editable fields directly editable in a consistent manner.
- Show CA and PA clearly in the appropriate ability area.
- Distinguish editable, read-only, unavailable and unverified fields.
- Make unsaved changes obvious.
- Support applying and undoing edits through the validated editing service only.

**Ordering requirement:** The attribute list must be derived from a verified FM27 field/attribute catalogue and match the game's/FMRTE's ordering. Do not invent a new ordering or alphabetise the attributes. Record the canonical ordered list in a versioned schema document and test the rendered sequence against it.

#### Right: supplementary intelligence

The reference includes a right-hand column for compact performance/intelligence panels. FM Atlas may use this area for information that is genuinely available from the selected save, such as:
- Verified season statistics.
- Appearances and minutes.
- Goals, assists and other recorded match statistics.
- Fitness/availability information.
- Position/role suitability when the calculation is documented.
- Historical or trend information where the save contains it.

Third-party-style scores, percentiles, xPerf and reliability measures may only be implemented later if their data source and calculation are explicit. Do not fill this column with invented values. If data is unavailable, use a clear unavailable state or omit the panel.

### Compact player sub-tabs

Each player profile needs its own compact row of small, clearly labelled buttons/tabs across the **top of the player workspace**, following the FMRTE section workflow requested by the user. Do not put these sections in a large left-hand navigation rail: REF-004 and the user's written instruction require top-of-profile tabs. They are separate from the global application navigation and the tabs for other open players.

The official FMRTE 27 player-section audit is recorded in `research/FMRTE_FEATURE_AUDIT_001.md`. Reproduce the supported section coverage, while retaining the user's top-tab placement.

Required player areas, where the relevant record/data exists:

- **Information** — FM attributes, CA/PA, personal details, fitness and morale. This is the main information/attribute surface; do not split the core attributes into an unrelated generic dashboard.
- **Contract** — existing club/national-team contracts where applicable; wage, dates, playing time, bonuses, clauses and other supported fields.
- **Stats** — season overview, season detail and league ranks/comparisons where the loaded save exposes them. Treat as read-only initially. Do not invent xPerf, percentiles, report grades or third-party metrics.
- **Unavailability** — bans and other reasons the player cannot play.
- **Transfer Proposals** — existing offers involving the player, where available.
- **Retire Date** — scheduled retirement, when present.
- **Positions** — all supported position familiarity values, using the FM pitch layout.
- **Preferred Moves** — player traits and supported editing.
- **Relationships** — links to people, clubs and nations, including secondary nationality where available.
- **Languages** — spoken languages and proficiency.
- **Scout Information** — general, positional and role ratings only when the underlying source/calculation is verified and labelled.
- **Staff** — only for a player who is also staff/player-coach; expose the staff-side sections that apply.
- **Clone Center** — later/conditional: copy selected data from the open player to existing target players only if the operation is technically supported and safely validated. It must not claim to create a new player.
- **Personality / Hidden** — expose supported hidden/personality values with field provenance and individual read/write status. These can appear as a sub-section of Information or a dedicated compact tab if needed to preserve readability.
- **History** — career/transfer/development history when present in the loaded career and supported by the data provider.

The compact tab strip should:
- use small, consistent, restrained controls;
- show a clear selected state using the orange accent;
- keep labels readable;
- support horizontal scrolling or a restrained overflow menu on narrower windows;
- retain the current player's tab state when switching between open player records;
- show a subtle unsaved marker on the affected section;
- conditionally hide or mark unavailable sections when the record has no relevant data.

The screenshot's profile header also informs a compact identity/ability summary: player portrait/name/club, CA/PA and Asking Price where supported, plus contract wage/expiry and current/potential rating summaries. The centre remains the main FM attribute editor. The right side is reserved for grounded performance/information panels.

### Editing behaviour

- Each tab shows its own relevant information.
- Fields that are supported for editing can be changed in place or through a clear edit state.
- Read-only fields must look read-only.
- Unsupported fields must not pretend to be editable.
- Changes are staged and clearly marked before application.
- Save/apply controls use the approved compact FMRTE-inspired button style.
- Apply writes only through the guarded live-editing service.
- Read-back verifies the result.
- Undo is available only where a safe prior value and supported rollback exist.
- Switching player tabs must not silently discard pending edits; warn or preserve staged changes.

### Multi-player tab behaviour

- Opening a player from Quick Search or Advanced Search opens that player's profile in a new workspace tab.
- Opening an already-open player should focus the existing tab rather than create duplicates.
- Multiple player tabs can remain open simultaneously.
- Closing a tab with unsaved changes requires a clear save/apply, discard or cancel decision.
- Search results and active filters should remain available when navigating back to the search workspace.

### Acceptance criteria

- [ ] Profile fills the available application workspace.
- [ ] Player identity rail appears on the left.
- [ ] Correct player face is resolved from installed graphics when available.
- [ ] Centre shows the complete supported FM attribute set in verified FM/FMRTE order.
- [ ] CA and PA display correctly.
- [ ] Compact FMRTE-style player sub-tabs appear across the top of the profile, including Information, Contract, Stats, Unavailability, Transfer Proposals, Retire Date, Positions, Preferred Moves, Relationships, Languages, Scout Information, conditional Staff/Clone Center and supported History/Personality sections.
- [ ] Contract, positions, preferred moves/traits, season statistics, history, personality/hidden information and other supported FMRTE-style areas are accessible.
- [ ] Each supported editable field is connected to the correct data mapping and editing service.
- [ ] Right-side intelligence only displays grounded data/calculations.
- [ ] Multiple player profiles can be open in separate tabs.
- [ ] Unsaved changes are protected when switching or closing tabs.
- [ ] Profile layout is visually compared against REF-004 at a desktop resolution.
- [ ] No placeholder or third-party example metrics are misrepresented as Football Manager data.
