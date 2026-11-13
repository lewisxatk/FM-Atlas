# FM27 field registry provenance

The 47 player attribute offsets in `PlayerAttributeSchema.cs` are sourced from the public `barak14/fm-editor` FM27 player field table. Their decode formula is `(raw + 2) / 5` as documented by that project's encoding notes. The upstream documentation explicitly distinguishes independently witnessed FM27 mappings from inherited FM26 leads. FM Atlas therefore exports these as provisional on the user's exact build until screenshot comparisons pass.

Steam-only fingerprint gate retained from the user's validated build. No write APIs are present in the new attribute snapshot path.
