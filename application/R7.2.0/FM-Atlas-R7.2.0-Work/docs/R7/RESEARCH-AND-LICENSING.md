# FM27 Technical Research and Licensing Notes

## Research reference

The public `barak14/fm-editor` repository describes itself as a GPL-3.0-or-later real-time editor for FM24, FM26 and FM27. It documents player, staff and club search, player attributes, positions, CA/PA, reputation, contracts, club finances/facilities, comparison and undo.

Repository: https://github.com/barak14/fm-editor
FM27 layout notes: https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md
FM27 player schema: https://github.com/barak14/fm-editor/blob/main/src/fields/fm27/player.rs
FM27 club schema: https://github.com/barak14/fm-editor/blob/main/src/fields/fm27/club.rs

The published FM27 notes include these useful leads:

- Steam build 27.0.0+2486695 was investigated on Windows; its documented module SHA-256 is `bc940e472418fc5c214208d7cf30fecf600f689b6f9d71c11ba19a2df0fe057f`.
- Its documented player/staff/club arrays are at `game_plugin.dll` RVAs 0x57037D0, 0x5703818 and 0x5703988 respectively.
- Player CA/PA are documented at +0x264/+0x266 as little-endian u16 values.
- Fifteen positional-suitability ratings are documented as direct u8 values at +0x150..+0x15E, with a 1–20 scale and multiple witnesses.
- The notes explicitly warn that other inherited FM26 offsets may remain unverified on FM27; Game Pass is not validated by the Steam schema.
- Club reputation, balance, transfer budget, facilities and stadium data have specific sub-record paths; ownership/sugar-daddy semantics are not mapped merely because an adjacent numeric field exists.

These facts are research leads, not a blanket certification that every offset is safe for every FM27 patch or storefront. The R6 live importer has its own older fingerprint and identity-only extraction path. This R7 source package does not replace it with a full memory reader.

## Licence

The upstream editor is GPL-3.0-or-later. If FM Atlas copies, adapts, links tightly with or distributes a derivative of that code, GPL obligations may apply to the combined work. Do not strip the licence, hide attribution, or attempt to bypass it. This release does not bundle upstream source code; it uses the public documentation as a research reference. Seek legal advice before distributing a combined derivative or commercial product.

## Football Manager trademark

Football Manager and related marks belong to Sports Interactive / SEGA. FM Atlas is an independent third-party project and is not affiliated with or endorsed by them.
