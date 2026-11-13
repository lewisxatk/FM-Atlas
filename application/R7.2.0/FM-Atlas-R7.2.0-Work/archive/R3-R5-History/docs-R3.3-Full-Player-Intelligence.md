# R3.3 field coverage and validation plan

## Data domains kept separate

1. **Permanent player attributes:** encoded bytes (candidate display transform `(raw + 2) / 5`); out-of-range values are preserved as anomalies, not converted.
2. **Positional suitability:** direct bytes at `player+0x150..0x15E`; these are position ratings, not role ratings.
3. **Ability/reputation:** raw 16-bit values; never passed through the attribute transform.
4. **Contract:** separate pointed sub-record; wage and selected status bytes have FM27 cross-reference evidence. Packed dates remain raw.
5. **Medical/availability:** medical pointer at `player+0xF8`; this release records a bounded raw block and the first two 24-byte vector-header regions only.
6. **Temporary condition:** match fitness, sharpness, fatigue and jadedness are still unmapped.

## Goalkeeper versus outfield

All goalkeeper attributes remain available for every player. The primary-profile flag is selected by comparing the GK suitability byte with the maximum of the 14 outfield suitability bytes, with a GK minimum threshold of 10. This is a UI/profile heuristic, not a complete position or role classifier. Validate mixed-position keepers and outfielders with unusually high GK ratings before treating it as final.

## Medical evidence capture

The FM27 research lead identifies a player medical pointer at `+0xF8`, injury-vector region at medical `+0x00`, injury record stride `0x38`, and absence-vector region at medical `+0x18`, absence record stride `0x20`. It also provides candidate offsets for injury type, dates and recovery-day fields. R3.3 intentionally does not parse these entries because the C# vector container representation/count/capacity and record lifetime semantics have not yet been independently verified against the user's Windows Steam build. The CSV captures raw header evidence for the next reverse-engineering pass without exporting process addresses.

## Next validation gates

- Compare CA/PA, reputation, height, DOB, personality, contract values against in-game screens for all four default players.
- Capture at least one player with a confirmed active injury and one without; verify pointer and vector changes.
- Capture a player with a known suspension and one on holiday; compare absence-vector evidence.
- Capture the same player before and after a match/training session to isolate condition-state candidates.
- Compare a goalkeeper, a natural outfield player, and a dual-position player; verify the classification heuristic and retain all raw suitability values.
- Do not enable editing until each writable field has a verified type, range, class guard, identity guard, readback and undo path.
