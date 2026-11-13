# R6 architecture, research and data limitations

## UX benchmark — workflow patterns, not a visual copy

Official FMRTE documentation describes a quick-search panel, a distinct Advanced Search for player/staff/club filters, and record tabs that keep multiple records open. Its player and club screens expose grouped record sections. FM Atlas R6 adopts these broadly useful interaction patterns while keeping its own Aurora Frost design, names, code and assets; it does not copy FMRTE's proprietary visuals or implementation.

- FMRTE interface tour: https://docs.fmrte.com/getting-started/interface-tour/
- FMRTE Search and Advanced Search: https://docs.fmrte.com/scouting/search/
- FMRTE Quick Search: https://docs.fmrte.com/scouting/quick-search/
- FMRTE player overview: https://docs.fmrte.com/players/overview/
- Genie Scout 27: https://geniescout.com/features/ (feature reference only)
- Open-source alternative inspected: https://github.com/barak14/fm-editor (GPL-3.0; separate implementation and licensing obligations apply)
- FM27 live-layout notes: https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md

## Additional FM27 code research — live data is build-specific

The public `barak14/fm-editor` repository is a valuable technical reference: it documents player, staff and club browsing, attribute and CA/PA fields, contract facts and club finance/facility data. Its repository is GPL-3.0. Its FM27 memory-layout notes state that only the entries explicitly listed as verified have been checked against FM27; other offsets are inherited from FM26. The notes also distinguish a Windows Steam build from Game Pass, which is explicitly described as unverified. Its Windows Steam notes record a specific `game_plugin.dll` fingerprint and array RVAs that differ from the existing Atlas reader's fingerprint/RVAs.

This matters for Atlas: do not transplant an offset simply because it appears in an FM27 table. First match the exact executable/module fingerprint, storefront and build; then validate a representative set of player, staff and club witnesses against the same in-game save. Only then promote a field to verified and expose it as a trusted CA/PA/attribute/finance value. Where a field remains candidate, it belongs in diagnostics with provenance, not in the player rating panel. GPL-3.0 code must not be copied into a differently licensed/distributed Atlas build without an intentional license/compliance decision. A future bridge can either be a properly separated GPL-compatible component or an independently implemented, evidence-backed reader.

The current Atlas `LiveDataImporter` remains read-only and fingerprint-gated. It extracts the already witnessed player/club identity and relationship fields; it does not currently decode the full player attributes, CA/PA, staff directory or rich club facts. R6 deliberately does not replace its fingerprint/RVAs with the newer public repository's Steam-specific values because the user's installed module hash and storefront have not been independently confirmed against that layout.

## WPF performance model

Microsoft distinguishes **UI virtualization** from **data virtualization**. UI virtualization delays generating item containers until they are visible; it does not remove the entire bound data collection from memory. Microsoft also documents that directly adding item container controls to an ItemsControl disables virtualization, and recommends recycling containers for large lists. WPF's standard controls do not supply built-in data virtualization.

- Microsoft Learn — Optimize control performance: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls
- Microsoft Learn — Improve TreeView performance: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-improve-the-performance-of-a-treeview

R6 therefore combines several distinct controls:

1. Search-first empty state: no 40K-player list is constructed when Players first opens.
2. Bounded SQL pages: the primary Players workspace requests up to 250 matches at a time.
3. Data-bound `DataGrid`: row/column virtualization and container recycling are enabled; results are not individually created `Border`/`StackPanel` card trees.
4. Background work: heavier Squad Architect and Transfers retrieval/ranking is moved off the WPF dispatcher. Squad Architect now merges the save/local index with the live identity index rather than letting a partial save snapshot hide live-only identities; filtering, sorting and aggregate calculations are also off the dispatcher.
5. Context-free database continuations: selected data access awaits no longer attempt to resume on the UI dispatcher.
6. Search-first staff and paged club directory reduce unnecessary first-open population.

Note: WPF virtualization reduces visual-container cost; SQLite paging/bounded queries reduces data retrieval and result memory. Both are needed. This is not a formal performance benchmark: machine-specific timings still need measuring on your Windows PC and real save.

## Why 40K identities do not produce 40K profiles

The dashboard reports separate source-level counts. The validated read-only in-memory path in this codebase reads player and club identities, DOB when decoded, and player-to-club relationships. It does not currently provide the full verified player attribute set, all CA/PA values, contracts, condition, staff records or complete club facts. The `GameProcessDetector` memory probe is not the full importer. Therefore a count of 40,292 live player identities is not a count of complete player objects or fully verified attributes.

The screenshot's `Salah` text is also a search term; the 12 results shown there are matching results, not the database total. Remove the query and enter a different term to search the broader identity index.

## CA/PA and attributes

The offline save importer reads fields supplied by the `fmsave` adapter. The current schema imports a player's `current_ability`, `potential_ability`, 1–20 attribute object, and several other fact groups only when those JSON fields exist. The importer itself warns that FM27 field meanings are unconfirmed and that build support is not proof of semantic correctness. The live memory path is more limited; candidate offsets and memory-layout leads must not be exposed as verified ratings until separately witnessed and cross-checked against the game.

In R6, dashes mean missing/unavailable. Attribute labels provide an ordered profile layout, not values generated from offsets. The Local Editor is a local SQLite edit surface only and never writes to the running process or the original career save.

## Club and staff coverage

The club directory now merges records from the latest save snapshot and live club identity source, deduplicating by the name/nation pair for display. A live identity may only contain a UID and name. Detailed finances, debt, stadium, facilities, affiliations, staff and squad links appear only where the imported snapshot or validated relationship table provides them. A missing club fact is a coverage limit, not proof that the game lacks the field.

The live identity importer does not create a staff directory. Staff records and their fact groups are sourced from a career-save snapshot. The dashboard's zero staff count should be treated as a missing import/export capability, not as an empty staff population in the game.

## Data integrity and persistence

- Existing `%LOCALAPPDATA%\FmAtlas\scouting.db` remains the primary store.
- Additive schema migration is retained; `SaveClubUid` is ensured before its index is created.
- Save imports are snapshot-scoped. Older import runs are not automatically deleted.
- Search result limits are bounded; the broad local search method now has a conservative default cap rather than 100,000 rows.
- R6 does not claim validated write access to Football Manager.

## Remaining engineering work before claiming parity

These are deliberately not marked complete: build-specific save parser verification against a real FM27 save; confirmed CA/PA and attribute schema semantics; full live player/staff/club field mapping using the user's exact storefront and module fingerprint; complete staff and club field mapping; a fully validated current save squad relationship model; the five-colour position suitability pitch; mass edit/preset manager; complete transfer and financial analysis; tactical role weighting; true data virtualization for very large indexes; automated SQLite integration tests; Windows UI responsiveness profiling; and full regression testing on Windows 10.

FM Atlas is FMRTE-inspired in workflow, but full feature parity is a larger engineering programme. Keep data provenance visible as the product grows.
