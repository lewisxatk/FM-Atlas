# Watched × FMRTE — design and workflow research for FM Atlas V9

**Research checked:** 15 November 2026  
**Purpose:** Translate the interaction and visual principles of Watched into an original Football Manager desktop product, while adopting the practical record workflow of FMRTE 27. This is a design reference, not a request to copy either product's branding, assets or proprietary implementation.

## 1. Watched — what is actually distinctive

The live site is a JavaScript/WebGL experience, so its text-only browser view can stall while the visual scene loads. The project's public source repository is therefore useful for checking how the product describes its own interface and implementation.

The Watched repository README and landing-image descriptions establish these design characteristics:

- **One clear proposition:** a short, memorable headline communicates the product before the user has to understand its underlying data.
- **Visual-first landing:** a large, interactive globe is the primary visual, with the search action and example places beside/over it. The visual is not a decorative hero image; it is the product's main navigation and explainer.
- **A direct action:** choose a place, search or drop a pin; the system then reveals the result rather than presenting an intimidating form first.
- **Narrative result summary:** the result starts with a sentence-sized summary and then exposes supporting imagery, timeline and individual pass records.
- **Progressive disclosure:** users can move from overview → photo/result → timeline → detailed pass telemetry. Technical details are available, but do not overwhelm the first view.
- **Motion with meaning:** a pass replay reveals the actual image line by line in scan direction; the time-lapse uses real dated imagery. Motion explains the data, rather than being a permanent decorative effect.
- **Precise telemetry:** timestamps, counts, IDs, sensor/altitude detail and coverage labels support the main visual.
- **Strong typographic contrast:** the project's published stack includes Instrument Sans, Martian Mono and Instrument Serif. This creates clear differences between editorial headings, readable interface copy and technical data.

The public source describes a React/Three.js/WebGL experience with local browser computation and Web Workers. FM Atlas is a WPF desktop application, so the appropriate translation is the design language and information hierarchy—not importing Watched's browser stack or adding an unnecessary 3D globe.

Primary source: https://github.com/dev-Armaan/satellites (README, landing and result-image descriptions, package/font references).  
Live product: https://www.watched.earth/ (requires JavaScript and WebGL).

## 2. FMRTE 27 — interaction patterns worth adopting

The current FMRTE 27 interface tour and search documentation describe a mature record-oriented workflow:

- A **toolbar** opens global tools; **main tabs** hold records; a **status bar** identifies the loaded save and game version.
- Quick Search finds a record by name or UID. Advanced Search is a separate tool for Player, Coach/Staff and Club search scopes.
- Each record opens in a main tab. Its sections (such as Information, Contract and Positions) are exposed in a collapsible left rail.
- Tabs can be dragged and offer right-click actions: Close, Close Other Tabs and Close Tabs to the Right.
- Result grids support configurable columns and multi-column sorting. Search criteria and result-column settings are separate concerns.
- Filters include categories, ranges, reusable saved criteria and position-combination logic.
- The interface exposes keyboard shortcuts including Quick Search, Advanced Search, close current tab, close other tabs and close tabs to the right.
- A status bar communicates current save state/version, while edit history and unsaved-change prompts make editing state visible.

FM Atlas adapts these patterns to its own evidence-aware model. It must not present a provisional live-memory value as verified, or enable live writes simply because an edit control exists.

Sources:
- https://docs.fmrte.com/getting-started/interface-tour/
- https://docs.fmrte.com/scouting/search/
- https://docs.fmrte.com/help/settings/
- https://www.fmrte.com/download/fmrte27/

## 3. The FM Atlas translation

### Visual system

- Near-black graphite canvas and clear opaque work surfaces.
- Warm orange as the single signal accent; neutral borders and warm off-white text.
- Large, editorial display typography for workspace/page hierarchy.
- Clean readable body type for explanations and controls.
- Monospaced technical data, UIDs, timestamps, percentages and metric readouts.
- Translucency only on navigation/toolbars/overlays. Dense tables, search forms, profiles and charts remain opaque.
- A visual focal point should be meaningful football data: a pitch, position map, performance comparison, career timeline or player profile—not a decorative 3D object.
- No excessive glow, excessive rounded cards, or tiny essential labels. Keyboard focus, scaling and reduced motion remain part of acceptance.

### Interaction model

1. Quick Search is always available from the command toolbar.
2. Advanced Search is a full workspace with a source-backed field library and saved profiles.
3. Players, Staff and Clubs are searchable directories with configurable, sortable results.
4. A selected person or club opens as a full main record tab; the directory/pool remains available.
5. The record's sections are navigated from a left-hand rail.
6. Right-click and keyboard actions manage record tabs.
7. The status footer keeps career connection, source and active background-job state visible.
8. Editing is separated into local drafts and any future certified live capability. Unsaved changes, blocked fields and edit history must be explicit.

### Data and truth

- Show only game/save fields that the FM27 source actually provides.
- Keep Save, Live, Atlas-calculated, Provisional, Unavailable and Unknown distinct.
- Missing does not mean zero.
- CA/PA and player attributes are not match performance.
- Season/match stats require actual save/CSV source rows and relevant minutes/sample size.
- Role-fit and value scores must disclose their inputs and remain Atlas estimates unless a real game formula is evidenced.
- Unsupported game builds remain read-only.

## 4. Current implementation checkpoint

On `rebuild/v8-full-product-rebuild`:

- [x] V9 shared Monolith design token/component library started.
- [x] Dedicated V9 application shell is wired into startup.
- [x] Global Quick Search field, command actions, grouped side navigation and persistent career/job footer are part of the V9 shell.
- [x] Players and Recruitment are routed to the new full-width workspace.
- [x] Player pool remains available as a permanent tab; selecting a player opens a separate full-workspace record tab.
- [x] Search filters, CA/PA, paging, shortlist and report-import paths are retained.
- [x] Player section navigation now uses a vertical left rail.
- [x] Player record tabs support Close, Close Other Player Tabs and Close Tabs to the Right.
- [x] Pool tab is protected from close actions.
- [ ] Fresh Windows build/test after the latest UI commits.
- [ ] Complete remaining workspace migration to the shared system.
- [ ] Verify visual layout and readability at 100%, 125% and 150% display scaling.
- [ ] Verify search/profile tab state across navigation, closing, restoring and switching appearances.
- [ ] Real FM27/Game Pass field and process acceptance remains separate from UI build verification.

## 5. Next rebuild priorities

1. Complete shell and record-tab interaction acceptance, then make the main Advanced Search workspace use the same full-page frame and filter navigation.
2. Migrate Staff and Clubs to full record tabs and linked profiles.
3. Rebuild Home, Moneyball/Performance, Squad/Tactics, Editor/History and Data Lab using the shared design system.
4. Only after the entire UI is coherent, resume the remaining data-field coverage and advanced editing parity work.
