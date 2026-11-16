# FM Atlas Advanced Search — Coverage and Newgen Evidence (002)

**Date:** 16 November 2026  
**Branch:** `rebuild/fresh-foundation-001`  
**Status:** Source implementation in progress; Windows build and live FM27 import not yet verified.

## 1. Scope and reference behaviour

The Advanced Search implementation is being aligned to the documented FMRTE workflow:

- Search entity: Player, Coach, or Club.
- Criteria are grouped into categories and combine using All / Any.
- Numeric and date criteria have independent minimum and maximum boundaries.
- Player position requirements combine as All chosen or Any chosen.
- Filter Options support saving and loading criteria.
- Club criteria include facilities, finances, debt and stadium details.

Official reference: [FMRTE — Search and Advanced Search](https://docs.fmrte.com/scouting/search/).

The current FM Atlas implementation has independent min/max fields. A one-sided filter such as Dribbling minimum 12 with a blank maximum means 12 or higher. A maximum-only filter means no greater than the supplied value. Supplying both creates an inclusive range.

For position criteria, FM Atlas exposes separate All chosen / Any chosen logic. FMRTE's documented At Least option changes exact position ratings into minimum ratings; in FM Atlas this behaviour is expressed directly by entering a position MIN and leaving its MAX empty.

Saved search criteria are versioned JSON files stored under the user's local `%LOCALAPPDATA%\FM Atlas\Search Presets` directory. Each preset stores the entity, All/Any logic, position logic, and typed field conditions.

## 2. Search catalogue coverage

### Player
- Identity and personal details: name, age, nationality, second nationality, gender, game UID, continent, region, city of birth and ethnicity.
- Development and ability: CA, PA, PA-minus-CA difference, wonderkid and newgen classification.
- Attributes: technical, mental, physical, goalkeeper, hidden, personality and left/right foot ratings.
- Position familiarity: all 15 documented position codes plus the flexible position-familiarity filter.
- Other details: height, weight, international caps and goals, reputation, team, division, training status, preferred foot and contract/value criteria.

### Coach / staff
- Identity and personal details: name, age, nationality, gender, UID, continent, region and city of birth.
- Coaching, goalkeeping, mental, scouting/recruitment, knowledge, medical, data-analysis and personality attributes.
- Training rating, CA/PA, contract details, reputation and preferred role ratings.
- Newgen classification is available as a filter field, shared with Player searches.

### Club
- Facilities: corporate, training, youth facilities, youth coaching, youth recruitment, youth importance, junior coaching, medical and sports science.
- Stadium: stadium name, capacity, average/minimum/maximum attendance and founded year.
- Club information: ownership type, chairperson title, club reputation and team-level reputation fields.
- Finances: balance, transfer budget, remaining transfer budget, wage budget, wage used, maximum wage, transfer revenue percentage, debt, loans, sponsorship, total debt and monthly debt repayments.

## 3. Attribute scales and validation

The FMRTE player-attributes reference documents the standard attribute scale as 1–20, CA/PA as 1–200, and Home/Current/World reputation as values up to 10,000. FM Atlas exposes independent numeric bounds and currently allows the user to enter a lower bound, upper bound, or both. Values outside the declared field bounds are rejected by the query validator.

Reference: [FMRTE — Player attributes](https://docs.fmrte.com/players/attributes/).

The catalogue is generated from `PlayerAttributeCatalog` where possible, avoiding a second manually maintained list for every player attribute. Left and right foot are separate values, and goalkeeper-specific attributes are included separately from outfield technical attributes.

## 4. Newgen classification — evidence and limitations

FMRTE's Quick Search documentation recognises newgen / regen terminology for generated players and staff. The public FMRTE Advanced Search overview lists Player, Coach and Club criteria but does not enumerate Newgen as a standalone Advanced Search field. FM Atlas is therefore adding this as a first-class filter rather than claiming that the FMRTE Advanced Search page lists it explicitly.

References:
- [FMRTE — Quick Search](https://docs.fmrte.com/scouting/quick-search/)
- [FMSuperScout — public implementation notes](https://github.com/mavarobli/FMSuperScout)

FM Atlas stores the value as nullable evidence:
- `true`: the source explicitly identifies the record as generated/newgen.
- `false`: the source explicitly confirms the person is an original database record.
- `null`: there is not enough source evidence to classify the person.

A player is **not** classified as a newgen merely because they are young, have high PA, have a particular face/portrait, or have an unusual name.

FMSuperScout publicly describes retaining successive full-save snapshots and an Intake Radar that reports newgens appearing worldwide since the previous dump. This is a useful research direction for an FM Atlas snapshot-delta detector. It is not yet an FM Atlas implementation and does not establish that any new record observed after a partial or late baseline is definitely a newgen. A proper detector needs a known complete baseline for that save, repeated complete snapshots, stable entity identity, and explicit handling for missing/incomplete scans. An authoritative in-game generated-person flag, when discovered and verified for the target FM27 build, should take precedence.

The current FM Atlas schema and query engine can search the three states, but the current live-save importer does not yet populate `is_newgen`. Until that importer is implemented and verified, results will show Unknown / unclassified rather than guessing.

## 5. What READY means

A field marked **READY** has:
1. a catalog definition for the selected entity;
2. a supported field key or explicit provider mapping;
3. a typed field projection or explicit derived-query implementation; and
4. a parameterised SQLite predicate.

READY does **not** mean:
- the live FM27 process has already supplied that field;
- the field has been read or write-verified for every FM27 distribution/build;
- every imported record contains a value;
- the query runs against the live game process.

The search provider is read-only and searches records already present in the local FM Atlas SQLite index.

A regression test now iterates the catalogue and runs the query builder for each READY field, so the UI cannot silently enable a field for which the SQL provider has no mapping. This test is committed but cannot yet be confirmed as passing because GitHub Actions currently reports a failed job before exposing any runner steps or logs.

## 6. Explicit outstanding items

- Implement and verify the FM27 live-save/world-data importer that populates the typed fields.
- Implement the authoritative newgen flag or a complete-baseline snapshot-delta detector.
- Implement relationship traversal for the Relations category.
- Implement the evidence-backed Recommended CA formula/mapping before enabling that field.
- Validate every field against real FM27 records and the exact target game build.
- Complete Windows restore/build/test and then perform a real-world search test in the user's game.
- Add player-result navigation into the full player profile/editor after the search result identity handoff is wired.

## 7. Safety and data-integrity rules

No memory offsets, write addresses, formulas, drop-down values or newgen rules are invented in this catalogue. A documented field is separate from a mapped field; a mapped field is separate from a read-verified field; and a read-verified field is separate from write verification. Live writes stay disabled until the build profile, field offsets, data representation and save integrity are verified for the user's exact FM build.
