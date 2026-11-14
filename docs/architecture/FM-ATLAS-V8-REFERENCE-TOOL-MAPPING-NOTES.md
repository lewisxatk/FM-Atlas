# FM Atlas V8 — reference-tool mapping notes

**Updated:** 2026-11-14  
**Working branch:** `rebuild/v8-full-product-rebuild`

This document records public research findings used to guide independent FM Atlas implementations. It is not a claim that another project's code, binary, or licence has been incorporated. FM Atlas keeps its own C# implementation and its own evidence/status model.

## 1. FMSuperScout — staff preferred-job ratings

Reference: https://github.com/mavarobli/FMSuperScout

Public FM27 field notes in `core/Fields.cs` and `core/Dumper.cs` describe a pointer at person-block offset `+0xD0` to 15 one-byte preference ratings. Values are in the 1–20 range. The maximum group is used as the displayed preferred job only when it reaches 10; tied maximum groups are retained when the tie is small, and broad ties do not get an invented single winner.

The documented groups are:

| Index | FM Atlas key | Meaning / limitation |
|---:|---|---|
| 0 | `manager` | Manager |
| 1 | `assistant_manager` | Assistant manager |
| 2 | `coach` | Coach |
| 3 | `physio` | Physio |
| 4 | `scout` | Scout |
| 5 | `goalkeeping_coach` | Goalkeeping coach |
| 6 | `fitness_coach` | Fitness coach |
| 7 | `job_code_22_unresolved` | Group associated with job code 22; label remains unresolved |
| 8 | `board_ownership` | Board / ownership group; not a single job title |
| 9 | `director_of_football` | Director of football |
| 10 | `head_of_youth_development` | Head of youth development |
| 11 | `loan_manager` | Loan manager |
| 12 | `analyst_recruitment_or_performance` | Recruitment and performance analyst are not distinguished by this preference group |
| 13 | `sports_scientist` | Sports scientist |
| 14 | `technical_director` | Technical director |

FM Atlas implementation:
- `StaffJobPreferenceDecoder` validates exactly 15 bytes and rejects any value above 20.
- All 15 valid ratings are retained as provisional facts, including zeros.
- A top preferred-job label is only produced for a maximum score of at least 10.
- Tied groups are all retained when there are at most three; larger ties produce no single preferred-job label.
- The live importer reads the pointer relative to the same class-scoped staff identity shift already selected by FM Atlas name decoding. Missing or invalid preference data never invalidates the staff identity.
- Ratings are persisted in `LiveStaffFacts` and displayed in the existing Staff profile under a provisional heading.

The reference project describes its preference grouping as derived from a large FM27 staff population and checked against the in-game staff search UI. FM Atlas has not yet performed that user-side witness check on the user's installed build. Treat these fields as provisional until compared with FM27.

## 2. FMLibreEditor — identity layouts and reverse-engineering discipline

Reference: https://github.com/tozmoz/FMLibreEditor

Relevant public research:
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-staff.md
- https://github.com/tozmoz/FMLibreEditor/blob/main/docs/recon/fm27-gamepass-0.9.2435.md

Useful methods and constraints:
- Identify staff person-block shifts by class/vtable and name-decoding evidence, rather than assuming one offset fits every staff object.
- Keep the human-manager identity layout separate from ordinary staff where the build requires it.
- Treat module fingerprints as build selection, not field-level certification.
- Use in-game witnesses and repeatable probes before promoting a candidate field.
- Job-code interpretation and preferred-job grouping are different data: current contract job must not be substituted for preferred job.

FM Atlas retains its own C# implementation. Public GPL-licensed project code is not copied into this repository.

## 3. fmsave — field trust and coverage

Reference: https://github.com/rhiever/fmsave  
Trust guide: https://github.com/rhiever/fmsave/blob/main/docs/guides/trust.md

Adopted data-quality principles:
- Separate field meaning/verification from field presence/coverage and reader integrity checks.
- Preserve `None`/unknown instead of coercing missing values to zero or inventing labels.
- Distinguish save-local UIDs from cross-save unique IDs; use the correct key for the operation.
- A known build or passing structural reader check does not prove every field's meaning.
- Preserve reader warnings and table/field coverage with the import record.
- Keep raw source facts and clearly labelled estimates separate.

FM Atlas already persists the fmsave validation report and table counts with each offline import. Future dashboard work should surface reader status, per-field coverage and warnings without treating advisory warnings as structural failures.

## 4. FMSuperScout — snapshot-based development intelligence

FMSuperScout describes retaining repeated local scan snapshots to calculate CA growth and detect newly appearing players. FM Atlas already stores versioned offline save snapshots and has a player-development history path. The next safe extension is to audit identity stability and compare snapshots only when their game/build/database namespaces are compatible; never label an entire database as a youth intake solely because its IDs changed after a build/database update.

## 5. Evidence and release gate

- The staff preferred-job decoder has pure unit tests for range validation, tied preferences, low scores and broad ties.
- The database has an integration test for persistence and profile retrieval.
- The exact branch head still needs a fresh Windows Release build and test run.
- Live preference values need a controlled FM27 UI comparison before any field is promoted to verified.
- Live editing remains outside this work and stays separately gated.
