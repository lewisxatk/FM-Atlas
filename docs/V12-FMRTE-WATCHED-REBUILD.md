# FM Atlas V12 — FMRTE × Watched rebuild

## Product direction

The application design is **FMRTE × Watched**. This is not a Monolith Glass reskin and does not retain the prior glass UI as the primary experience.

Watched supplies the visual reference: flat graphite surfaces, warm off-white typography, restrained signal orange, hairline borders, compact technical labels and data-first layouts. FMRTE supplies the functional reference: a fast search workspace, multiple entity types, extensive fields, filter conditions and full record workflows.

Official reference material:
- FMRTE search and advanced-search guide: https://docs.fmrte.com/scouting/search/
- FMRTE interface tour: https://docs.fmrte.com/getting-started/interface-tour/
- Watched.earth design reference: https://www.watched.earth/

## Implementation in this branch

Branch: `rebuild/fmrte-watched-search-priority`

Parent: `rebuild/precision-instrument` (preserves the later V12 scouting/editor/reverse-engineering work).

### Shell and design system
- Ported the Watched shell and sidebar into the newer V12 code line.
- Made Watched Instrument the default application appearance.
- Changed the default surface system to solid ink/graphite fills, warm off-white type, orange accents, square corners and thin borders.
- Removed gradient/card shadows from shared cards and applied the flat treatment recursively to workspace controls.
- Updated app title/product metadata to FM Atlas · FMRTE × Watched.
- Retained all existing page routes and the existing FM Atlas database location.

### Unified Advanced Search
The Advanced Search route is now a unified entity hub:
- Player
- Staff
- Club

Each entity has an independent field catalogue and condition builder.

#### Player
- Retains the existing dynamic field discovery for core identity, PlayerAttributes and current FMSave player facts.
- Retains saved profiles, global ALL/ANY logic, sort and paging controls.
- Retains the dedicated position filter and its ALL/ANY + exact/At Least semantics.
- Replaced the 15 numeric position tiles with a pitch layout, with 0–20 rating inputs at each FM position node.
- Text-only search is supported without requiring an additional condition.

#### Staff
- Adds an advanced field catalogue from the latest FMSave staff RawJson plus current LiveStaffFacts.
- Core indexed fields include name, age, club, job, nation, current ability, potential ability and human-manager identity.
- Supports number range, text contains, boolean, ALL/ANY and paged results.
- Double-click opens the existing Staff workspace.

#### Club
- Adds an advanced field catalogue from club RawJson, current LiveClubFacts and club-linked FMSave extra tables where a club relationship is identifiable.
- Core indexed fields include name, nation, linked player count and data source.
- The imported linked-table sources include finances, facilities, stadiums, affiliates, managed clubs, sponsorships and competition rules.
- Supports number range, text contains, boolean, ALL/ANY and paged results.
- Double-click opens the existing Clubs workspace.

### Keyboard
- Ctrl+K and Ctrl+F continue to open global Quick Search.
- Ctrl+Shift+F opens the Player / Staff / Club Advanced Search hub.

## Data integrity and limits

The field menus are source-discovered. They must only offer fields actually present in the latest imported save or current live field registry. The catalogue is not a claim that every possible internal FM27 memory field has been decoded.

The live importer stores validation status and some values remain provisional. Do not present a field as verified solely because it can be read. Live-write safety gates remain unchanged; this UI update does not certify new memory offsets or authorize additional direct writes.

Performance metrics in the separate Stats Search / Scouting Matrix remain their own data workflow. They must not be silently mixed into a player attribute criterion unless a field-specific query and unit/period semantics are implemented.

## Validation required

- Fresh Windows x64 .NET 8 restore/build.
- Full existing xUnit suite.
- Confirm WPF pack URIs and all shell assets resolve.
- Test entity switch Player / Staff / Club.
- Confirm dynamic field menus against a real FM27 save import.
- Test numeric, text, boolean and ALL/ANY filters.
- Test pitch ratings including zero, exact matching, At Least and Any of these.
- Confirm saved player profiles still load with position criteria.
- Confirm paging, sorting, record double-click and navigation.
- Test 100% / 125% / 150% / 200% Windows DPI and smaller laptop display.
- Real FM27 acceptance is required before claiming full field coverage or live-write support.
