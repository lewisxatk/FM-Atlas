# FM Atlas V9 — research and design decisions

**Working branch:** `rebuild/v8-full-product-rebuild`  
**Purpose:** record the external product and reverse-engineering research guiding the full UI rebuild and FM27 data/editing rules. This is an independent implementation; it does not copy another product's source code or visual assets.

## 1. Watched.earth — visual direction

Reviewed the live site, its published design system, repository and current design review.

Watched describes its design concept as a precision instrument: a JPL-style mission console art-directed with editorial typography. Its published rules are unusually explicit:

- Flat ink surfaces and hairline borders; no decorative gradients, glassmorphism, drop shadows or rounded cards.
- Warm off-white text on near-black surfaces.
- One rare signal colour (sodium orange) for selected/live/important state, not a rainbow of decorative accents.
- Condensed display typography, readable sans-serif body copy and monospaced data labels/readouts.
- Tabular numerals, exact units and source/status labels.
- Terse, factual copy; unknown values shown as a dash rather than fabricated placeholders.
- Motion is precise and restrained; reduced-motion preferences are honoured.
- Data visualisation is the hero; panels are compact and do not obscure the working area.

**FM Atlas translation:** retain the Watched visual language, but apply it to a desktop football intelligence workstation. Use graphite/ink panels, warm bone text, a restrained orange signal, condensed headings, monospace numeric data, square surfaces and hairline separators. Keep all player attributes, stats, tables and forms opaque and high contrast. Do not copy Watched's globe, orbital motifs, exact wordmark, artwork or site layout.

The Watched design review also calls out an accessibility weakness in its initial DOM. Atlas must not repeat that: controls need visible labels, keyboard focus, automation names, readable contrast and useful scaling.

## 2. FMRTE — interaction reference

Reviewed FMRTE 27's interface-tour and scouting/search guidance. The useful product patterns are:

- A main workspace with multiple persistent record tabs.
- Search and filter controls that remain separate from the selected record.
- Advanced field-based search, numeric ranges, grouped conditions and reusable search criteria.
- Configurable result grids, sortable columns, row selection and contextual actions.
- A full record with section navigation rather than a narrow permanent inspector.
- Keyboard-first operation, clear editing state and visible change history.

**FM Atlas translation:** player/staff/club records open as full workspace tabs; the directory/search tab remains available. Advanced Search is a resizable filter-builder/results split with independent scrolling, field discovery from imported data, AND/OR logic, position familiarity, sorting, paging, saved profiles, comparison and CSV export. FMRTE is a workflow reference, not a source of data or code.

## 3. FMLibreEditor — live-memory and safety research

Reviewed its development guide, health checks, build-specific Game Pass reconnaissance, player/staff/club field layouts and contract notes.

Key engineering lessons:

- Treat each storefront and game build as a distinct layout profile. A patch can move static data even when the underlying object layout appears unchanged.
- A detected pointer or plausible-looking number is not enough to certify a field.
- Before a live write, independently verify the same career/save generation, entity identity, field range, current value shape and read-back result.
- If any check fails, leave the affected fields or structures read-only. A changed save generation must stop writes until data is reloaded and revalidated.
- Validate layout hypotheses against multiple independent witnesses (for example natural-position ratings, role-specific attribute distributions, contract back-references and team/club relationships).
- Keep player, staff, club, contract, list and object-pool operations distinct; structural changes are riskier than scalar field edits.

One published Game Pass reconnaissance file covers build **0.9.2435.0**, observed 10 November 2026. It records that several static addresses moved from 0.9.2409.0 while the sampled object layout remained stable in that test save. This is evidence for build-specific profiles, not permission to reuse those offsets blindly on the user's installation.

**FM Atlas rule:** live editing stays disabled unless the exact installed build and each field/structure mapping pass Atlas's own checks. Local edit drafts, live memory writes and imported save snapshots must remain separate states. No writes are enabled simply because a field appears editable in another tool.

## 4. FMSuperScout — read-only scouting and scanner research

Reviewed its field registry, scanner process and app stylesheet.

Useful patterns:

- Separate the external process scanner from the UI; scanning can be requested once or watched continuously without injecting UI code into the game.
- Keep the scanner read-only and make its process/version detection failures explicit.
- Use version-pinned field registries for CA/PA, player attributes, hidden attributes, positions, contracts, staff, clubs and team relationships.
- Distinguish raw game fields from calculated scouting summaries and estimated values.
- Expose data provenance and version support rather than silently treating stale offsets as current.

Its field definitions include standard player attributes, several hidden attributes and position familiarity. These are useful cross-check targets, but every FM Atlas field still needs independent verification against FM27 save/live data and the in-game UI before it is labelled verified.

## 5. Data truth contract for FM Atlas

1. Only show a statistic as an in-game statistic when its source is an FM27 save field, FM27 live field or validated FM27 game data field.
2. Do not import external football statistics into an in-game stats panel. If Atlas later calculates a metric, label it **Atlas calculated** and disclose its formula, inputs, sample and cohort.
3. CA and PA must be shown as distinct game values, not inferred from attributes or external scouting ratings.
4. Unknown/unavailable is a dash or explicit status, never zero.
5. Every field should carry: entity, field key, source type, game/storefront/build, raw scale, display scale, confidence, read capability, write capability and validation evidence.
6. Player performance statistics, player attributes, staff attributes, club data, tactical effectiveness and Atlas calculations are separate domains.
7. A page must not invent fields merely to fill space. Missing source coverage is visible as unavailable/provisional.

## 6. Full rebuild delivery order

1. Shared Monolith shell, navigation, search, page frame, record tabs, reusable panels, buttons, inputs and data grids.
2. Player directory and full player record, with all supported attributes, CA/PA, positions, contracts, development and in-game performance sections.
3. Advanced Search and saved filters, then staff and club directories/records using the same full-tab model.
4. Stats Search / Moneyball, squad, tactical, transfer, editor, history and Data Lab workspaces migrated to the shared design system.
5. FM27 source registry and field coverage audit, then the complete data and editing parity work.
6. Live-write certification only after build-specific mapping, independent witnesses, save-generation checks, read-back and rollback tests.

## Research sources

- Watched site: https://www.watched.earth/
- Watched source repository (MIT): https://github.com/dev-Armaan/satellites
- Watched design system: https://github.com/dev-Armaan/satellites/blob/main/docs/DESIGN.md
- Watched design review: https://websitedesignawards.com/site/watched
- FMRTE 27 interface tour: https://docs.fmrte.com/getting-started/interface-tour/
- FMRTE scouting/search: https://docs.fmrte.com/scouting/search/
- FMLibreEditor: https://github.com/tozmoz/FMLibreEditor
- FMLibreEditor development and safety guide: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/DEVELOPING.md
- FMLibreEditor FM27 Game Pass reconnaissance: https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-gamepass-0.9.2435.md
- FMLibreEditor staff and contract reconnaissance: https://github.com/tozmoz/FMLibreEditor/tree/main/docs/recon
- FMSuperScout: https://github.com/mavarobli/FMSuperScout
- FMSuperScout versioned field registry: https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs

**Licensing note:** FMLibreEditor and FMSuperScout are inspected as independent research references. Do not copy or port their implementation code into FM Atlas without a separate licence review. Watched is MIT licensed, but Atlas only adopts broad design principles and does not copy its branding or assets.
