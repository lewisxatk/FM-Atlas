# FM Atlas — Field Coverage Ledger 001

**Date:** 2026-11-16  
**Purpose:** Track field counts, groupings, source evidence and confidence. This ledger is not a write whitelist.

## 1. Player attribute catalogue

Sources:
- [FMLibreEditor player attribute groups](https://github.com/tozmoz/FMLibreEditor/blob/main/app/src/playerAttributes.ts)
- [FMLibreEditor player reader](https://github.com/tozmoz/FMLibreEditor/blob/main/crates/fmlibre-core/src/players.rs)
- [fmsave player model](https://github.com/rhiever/fmsave/blob/main/src/fmsave/models/players.py)
- [FMSuperScout memory field catalogue](https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs)
- [FMRTE 27 player attributes](https://docs.fmrte.com/players/attributes/)

The current public reference catalogues agree on **52 unique non-foot player attributes**, with left/right foot stored separately. The group arrays overlap on First Touch and Passing between Technical and Goalkeeping; do not sum group lengths as unique field counts.

### Display groups and source ordering

| Group | Count in source list | Notes |
|---|---:|---|
| Technical | 14 | Corners, Crossing, Dribbling, Finishing, First Touch, Free Kicks, Heading, Long Shots, Long Throws, Marking, Passing, Penalty Taking, Tackling, Technique |
| Mental | 14 | Aggression, Anticipation, Bravery, Composure, Concentration, Decisions, Determination, Flair, Leadership, Off The Ball, Positioning, Teamwork, Vision, Work Rate |
| Physical | 8 | Acceleration, Agility, Balance, Jumping Reach, Natural Fitness, Pace, Stamina, Strength |
| Goalkeeping | 13 | Aerial Reach, Command of Area, Communication, Eccentricity, First Touch, Handling, Kicking, One on Ones, Passing, Punching, Reflexes, Rushing Out, Throwing |
| Hidden | 5 | Consistency, Dirtiness, Important Matches, Injury Proneness, Versatility |
| Feet | 2 | Left Foot, Right Foot; separate from the 52 non-foot attributes |

The FMLibreEditor UI source comments that a player with goalkeeper rating at GK >= 15 is treated as a goalkeeper and its profile shows Goalkeeping instead of Technical. This is a useful implementation lead, not yet a FM Atlas acceptance result.

### Important distinctions

- fmsave models 52 non-foot attributes and stores raw and displayed values separately.
- Its documentation says some raw values can be edited above the normal 1–20 display scale; therefore do not assume raw memory bytes equal displayed ratings.
- FMSuperScout documents a 55-byte attribute block stored at PLAO_ATTRS with values multiplied by five. This byte count is a storage-layout observation, not proof of a 55-field UI catalogue or a safe write map.
- FMLibreEditor player reader describes its attribute vector in the order of its ATTRIBUTE_GROUPS.
- The FM View Exporter 375-column table catalogue is not the same thing as the player profile's attribute grouping/order.

**Display-order status:** Documented reference order exists; exact FM27 profile order still requires a visual comparison against the user's FM27 profile and FMRTE. Do not mark the final order as verified until that comparison is recorded.

## 2. Personality and hidden values

fmsave documents eight personality values:
- Adaptability
- Ambition
- Loyalty
- Pressure
- Professionalism
- Sportsmanship
- Temperament
- Controversy

The five hidden attributes listed above are a separate group from those eight personality values. Keep them separate in the domain schema and UI. Do not collapse all hidden/personality values into one field list.

## 3. Positions

The documented player position rating array has 15 entries:
1. Goalkeeper
2. Sweeper
3. Left Back
4. Centre Back
5. Right Back
6. Defensive Midfielder
7. Left Midfielder
8. Central Midfielder
9. Right Midfielder
10. Attacking Midfielder Left
11. Attacking Midfielder Centre
12. Attacking Midfielder Right
13. Striker Centre
14. Left Wing Back
15. Right Wing Back

This rating array is not the same as match/tactic position masks. Use distinct types and schemas.

## 4. Staff fields — initial coverage

FMLibreEditor source documents these staff attribute groups:
- Coaching: Attacking, Defending, Fitness, Goalkeeping, Possession, Set Pieces, Tactical, Technical, Working With Youngsters.
- Mental: Authority, Determination, Motivating, People Management.
- Knowledge: Data Analysis, Judging Ability, Judging Potential, Judging Staff, Negotiating, Tactical Knowledge.
- Medical: Physiotherapy, Sports Science.

FMRTE documentation also describes role/tactical/scouting/chairman-related staff attributes, training ratings, formations/tendencies, country knowledge, career plans and contracts. The complete FM27 staff catalogue is not yet verified.

## 5. Club fields — initial coverage

FMLibreEditor club summary source documents:
- UID, full/short name, women's club flag;
- senior-team reputation;
- founded year;
- nation and nation UID;
- current league, league UID and league nation.

FMRTE documents broader sections such as facilities, attendance, squads, youth intake, finances, debt, loans, sponsorship, club vision, possible signings, player clauses, transfer centre, staff, wage contributions, kits/colours, affiliations and match preparation. These remain to be mapped and individually verified.

## 6. Live memory confidence

The FMLibreEditor Game Pass profile for package 0.9.2435.0 records a specific plugin image size/link timestamp and array pointers for players, staff and clubs. Its comments state that the object layout matched the earlier Game Pass 0.9.2409.0 profile, while static pointer locations differed.

This is a version-specific public research profile. FM Atlas must still:
- identify the user's exact installed package and module fingerprint;
- compare its field layout against independent witnesses;
- validate entity identity and save-generation changes;
- test every field's read mapping separately;
- keep write capability disabled until its field-specific write test, read-back and in-game persistence pass.

## 7. Coverage status vocabulary

Use these states per field:
- **Documented:** a source describes the field/meaning.
- **Mapped:** a proposed FM Atlas mapping exists for a specified build.
- **Read verified:** read against the exact target build and compared with FM UI/independent tool.
- **Write verified:** safely written on a disposable career, read back and confirmed in-game.
- **Read-only:** deliberately not editable.
- **Unknown:** insufficient evidence.
- **Unsupported:** not available for this version/provider.

A documented or mapped field is not automatically safe to write.
