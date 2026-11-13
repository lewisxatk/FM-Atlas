# FMRTE 27 reference analysis (user-supplied package)

Inspected files:
- `FMRTE 27-Setup.exe` (metadata/hash only; installer not executed)
- `FMRTE 27.0.0-build2(1).zip` (archive contents and embedded application strings; application not executed)

Observed architecture clues in the compiled application:
- Separate advanced-search view models for players, staff and clubs.
- Player search view models split into General, Contract, Technical/Goalkeeper, Mental, Personality, Physical/Hidden, Preferred Moves and Relations.
- Separate staff coaching, chairman, and staff attribute search models.
- Player advanced search exposes position filters, minimum-position matching, role selection, current/potential positional rating, and trained-at club/nation filters.
- Embedded configuration resources include `Ratings/abilityWeights.json`, `Ratings/rolesAndDuties.json`, `Ratings/teamSelection.json`, and `Ratings/wonderkidGenerator.json`.
- `rolesAndDuties.json` describes role-specific key/preferable/avoid attributes and position penalties. `abilityWeights.json` stores position-specific weighting sets. These are configuration concepts; FM Atlas will implement its own documented model rather than copying FMRTE's proprietary code/data.
- `Facepack/ngRegens.json` is a facepack mapping resource with nation/ethnicity-to-folder rules. It is not evidence of FMRTE's internal regen detection algorithm.
- Static string inspection did not expose a definitive regen boolean or the private memory-reader implementation.

FM Atlas design decisions derived from these observations:
- Keep separate player, staff and club search domains.
- Organise player filters into General, Contract, Attributes, Position/Role, Personality/Hidden and Relationships.
- Use a tri-state generated-player filter: Yes, No, Either.
- Keep an explicit Unknown state; unknown records must not be silently treated as real players.
- Make role ratings configurable and explain their attribute weights and penalties.
- Preserve a strict read-only memory boundary until write support is independently designed and validated.

Limitations: this is static inspection of a compiled third-party package. It does not recover source code, prove its internal memory offsets, or reveal its private regen classification logic.
