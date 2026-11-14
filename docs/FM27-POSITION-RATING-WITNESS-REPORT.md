# FM27 Position-Rating Witness Report

**Purpose:** distinguish implemented position-rating display from offsets actually witnessed against the user's exact FM27 build.

## Current evidence state

- FM Atlas canonical position schema contains 15 source slots: GK, SW, DL, DC, DR, WBL, WBR, DM, ML, MC, MR, AML, AMC, AMR and STC.
- Imported positions facts are mapped into these slots and displayed on the player comparison report.
- Ratings are accepted only as finite values from 1–20. Missing values remain unrated; they are never replaced with zero or inferred from the primary position.
- Display bands (Low 1–4, Limited 5–9, Competent 10–14, Strong 15–17, Natural 18–20) are presentation labels, not additional game data.
- Legacy ST is accepted as an alias for canonical STC.

## Witness matrix

| Evidence path | Status | Interpretation |
|---|---|---|
| Offline fmsave positions facts | Source-backed when present | Use imported value as-is |
| Live FM27 memory position offsets | Not promoted to verified | Requires exact build witness |
| Primary position string | Not a numeric rating | Never use as a substitute |
| Missing position fact | Missing | Display as — / Unrated |

## Required in-game witness capture

For each canonical slot, record: game build/storefront, player UID, in-game displayed rating, raw imported value, source path, timestamp, and independent repeat observation. Include natural, competent and unrated examples where available. Do not change the memory schema or promote a live offset based on a single matching player.

## Acceptance

- [ ] Compare at least 10 players against the FM27 player profile on the user's installed build.
- [ ] Include goalkeeper, defender, midfielder and forward examples.
- [ ] Confirm canonical STC mapping and legacy ST alias behaviour.
- [ ] Confirm a missing value stays missing.
- [ ] Record any disagreement with raw source evidence before changing the decoder.
- [ ] Only then consider promoting a live-memory field mapping to verified.
