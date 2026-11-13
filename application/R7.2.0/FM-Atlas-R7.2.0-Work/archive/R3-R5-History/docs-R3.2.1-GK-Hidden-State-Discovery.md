# FM Atlas R3.2.1 — goalkeeper, outfield and hidden-state discovery

## Goalkeeper vs outfield attributes

FM27 stores the common technical, set-piece, mental and physical attribute bytes and the goalkeeper-specific attribute bytes in the same player record. Goalkeeping fields are not a separate structure. The player-position suitability block begins at `player + 0x150`:

- `+0x150`: GK suitability
- `+0x151..+0x15E`: outfield suitability values (SW, DL, DC, DR, DM, ML, MC, MR, AML, AMC, AMR, ST, WBL, WBR)

R3.2.1 labels a profile as Goalkeeper when GK suitability is at least 10 and is greater than or equal to the maximum outfield suitability; otherwise it labels it Outfield. This is a pragmatic classification heuristic, not a claim that FM internally stores a binary goalkeeper/outfield class. The exported `ProfileApplicability` column marks goalkeeper attributes as `Primary` or `Secondary/NotPrimary`; shared attributes remain `Shared`.

Do not discard secondary values. They can be useful when a player retrains, has a secondary position, or is compared across roles. The UI should display the relevant attribute group first and allow the user to expand the other group.

## Hidden attributes

R3.2.1 adds the inherited FM26 lead offsets for Consistency (`0x18B`), Important Matches (`0x18E`), Injury Proneness (`0x18F`), Dirtiness (`0x188`) and Versatility (`0x190`). They are exported as `Hidden` and remain provisional until checked against independent FM27 witnesses. The normal encoded attribute conversion `(raw + 2) / 5` is used only when raw is 0..98 and yields 1..20.

## Condition and medical state — separate data domain

These are not 1–20 player attributes and must never use the attribute decoder:

- Match fitness / condition
- Match sharpness
- Fatigue / jadedness
- Active injuries and injury type
- Injury start date and estimated full/light training days
- Suspensions and other absences

The upstream FM27 Steam research identifies a medical pointer at `player + 0xF8`, an injuries vector at medical-record `+0x00`, an injury record size of `0x38`, injury type pointer at record `+0x08`, type name at pointed object `+0x20`, injury start at record `+0x20`, full-days at `+0x28`, and light-days at `+0x2A`. R3.2.1 exports only whether the medical pointer is non-null. It does not dereference or decode the vector yet. Fitness, sharpness and fatigue remain explicitly `UNMAPPED`.

This is deliberate: before a future editor can modify these fields, we need bounded pointer validation, multiple live witnesses, value ranges, UI cross-checks, write acknowledgement, rollback and undo tests. R3.2.1 remains strictly read-only and requests only query/read process rights.

## Penalty Taking anomaly

The FM27 cross-reference still lists Penalty Taking at `+0x167`. The user's earlier Palmer sample returned raw `100`, outside the normal encoded range. R3.2.1 does not silently move the offset or reinterpret 100: it marks that row `SentinelOrWrongMapping-Investigate`. The offset remains a provisional lead until a second player and in-game value establish whether this is a sentinel, class-specific exception or incorrect inherited mapping.

## Evidence status

- Barcola and Palmer visible attributes previously matched their screenshots for the compared outfield attributes.
- Alisson's snapshot provides a useful goalkeeper witness, but it is not independently screenshot-verified in this release.
- Goretzka and the hidden attributes remain provisional without direct screenshot comparisons.
- Upstream FM27 notes explicitly state most attribute offsets are inherited from FM26; do not present the entire schema as independently confirmed for FM27.

## Future editor architecture

Keep three distinct data domains in the player model:

1. **Core attributes** — shared, outfield and goalkeeper attributes; encoded 1–20.
2. **Hidden attributes** — consistency, important matches, injury proneness, dirtiness, versatility and future discoveries.
3. **Live state / medical** — fitness, sharpness, fatigue, injuries, recovery, bans and absences; separate types, units and validation rules.

A future editing release should enable each field only after its mapping, range, update timing and undo behaviour are independently verified. No background writers or silent edits.
