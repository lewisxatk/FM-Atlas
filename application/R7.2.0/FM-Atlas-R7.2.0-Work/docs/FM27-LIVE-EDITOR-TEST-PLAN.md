# FM Atlas — FM27 Live Editor Test Plan

Build acceptance for branch `feature/fm27-live-editor-activation`.

## Session opt-in and editor controls

- Atlas Local Editor and Live FM Editing are separate switches. Local overrides only change the Atlas database.
- Live FM Editing starts OFF on every launch and is enabled from **Editor Studio → Enable Live FM Editing** for the current session only.
- The live writer also checks this state inside the service before Apply and Undo; hiding or showing a UI panel is not the only write gate.
- Closing Atlas disarms live editing. Re-enable it explicitly after restarting the application.
- Live player/staff/club panels show field groups, searchable labels/keys and the permitted numeric range. Stage only submits values that differ from the current value.

## Build that this plan refers to

- Workflow: https://github.com/lewisxatk/FM-Atlas/actions/runs/43637701548
- Code commit: `8a1e87cf6928943aaaa7d5231c92b86992030a09`
- Artifact: `FM-Atlas-V10-win-x64`
- Test result: Windows Release build succeeded; xUnit regression suite passed (164 tests); self-contained Windows x64 publish and artifact upload succeeded.

## Before the first live test

1. Make a separate copy of the FM27 career and load that copy offline. Do not use an online/network career or your only save.
2. Close any other Football Manager process. Run FM Atlas and FM at the same Windows privilege level.
3. Confirm the FM27 executable build is one of the registered profiles. The first supported target is Steam build 26577676 (12 Nov 2026); Steam 26385295 and Game Pass 0.9.2435.0 are also registered.
4. In FM, record the original values of the test player, staff member, club and active tactic. Keep the in-game windows available for comparison.
5. In Atlas, open the live career and select an entity with an exact UID/name match. Open Editor Studio and explicitly enable **Live FM Editing**. The profile-local Editor switch is for Atlas-local overrides and does not arm live writes.

## Test protocol for every supported field

For each field or field group:
1. Change one value only at first.
2. Choose **Stage**. Confirm the old → new preview is correct and that the game value has not changed.
3. Choose **Apply** and confirm the warning. Confirm Atlas reports read-back verification.
4. Check the same value in the FM user interface.
5. Choose **Undo** before saving. Confirm FM returns to the original value.
6. Repeat with a new target and save the disposable career inside FM. Close and reload the career; verify the saved value persisted.

Do not bulk-test many fields until single-field Apply/Undo has passed for that record type. For bit flags sharing a byte (for example preferred-move bits or contract listing flags), test one flag at a time in the first pass.

## Player profile coverage

- All 52 player attributes, including the five hidden attributes.
- Position ratings (all current editable positions; Sweeper is not offered).
- Personality ratings, left and right foot.
- Current Ability, Potential Ability, height, date of birth, asking price.
- Condition, match sharpness, jadedness, morale and home/current/world reputation.
- Senior and U21 international caps and goals.
- Preferred-move / trait bits.
- Contract wage, signed date, expiry date, joined-club date, squad number, squad status, transfer-listed and loan-listed flags, loyalty bonus, happiness and playing-time happiness.
- Remove All Unhappiness (clears the validated concern count; Undo should restore it).
- Loan expiry and optional future fee (only for players whose live loan record resolves and passes its owner/team checks).

## Staff profile coverage

- Current Ability, Potential Ability and home/current/world reputation.
- Coaching, scouting, tactical, medical/fitness and other staff ratings exposed by the FM27 field catalogue.
- Authority and Working With Youngsters.
- Personality and date of birth.
- Staff contract wage where the supported contract record resolves.

Staff uses a separate FM object layout. Do not treat a staff value as a player field.

## Club profile coverage

- Senior-team reputation.
- Training, youth facilities, youth coaching, youth recruitment and corporate facilities.
- Balance, transfer budget, wage budget and total wage budget.
- Average ticket price.
- Season tickets, founding year and social-media count.
- Supporter mix and supporter traits.
- Stadium capacity, seating, used capacity and expansion capacity.

Some clubs do not have the full finance or stadium record. Atlas should omit fields where the record class cannot be validated. Stadium constraints must reject seating or used capacity greater than total capacity.

## Tactical Effectiveness coverage

- Six Match Exposure areas: Line of Engagement, Pressing Intensity, Passing Style, Chance Creation, Build Up and Pressing Trap.
- Position / Role Shapes.
- Training Contribution calculated from the FM training-history entries.
- **Max Training Contribution** rewrites history entries for the tactic currently selected in FM. Test with a copy, check the preview, Apply, read back, Undo, save/reload, and then re-check.

The active tactic, senior team, player identity, each list header and original list contents are re-resolved before Apply and Undo. Tactical list growth uses the measured FM27 Windows allocation classes inside a private 64 KB pool; requests beyond the measured 4 KB block size are refused before any write.

## Expected blocks

The editor must block, not guess, when:
- the FM executable build is not registered or the process cannot be uniquely identified;
- the career generation changes after staging;
- an entity UID/name or contract/club/finance/stadium back-reference does not match;
- the current value differs from the preview;
- a field is outside its valid range or enum choices;
- a required record is absent or belongs to an unrecognized object class;
- a tactical history list exceeds the measured allocator size classes;
- a field/list has changed since Apply, making Undo unsafe.

## Not claimed as complete live parity

The live writer in this build covers the scalar player, staff and club field catalogue above plus Tactical Effectiveness history operations. It is not a complete clone of every FMRTE screen. The following still need their own field discovery, record ownership rules, editor UI and regression coverage before being enabled live:

- changing nationalities/languages, relationships, preferred relationships, promises and work permits;
- transfer and future-transfer records, release clauses, competition bonuses and other dynamic contract lists;
- injuries, bans, suspensions and player availability records;
- club debt records, owner/benefactor status, sponsorship contracts, affiliations and club vision;
- competition rules, nation settings, cities, awards, referees and other world entities;
- all team-tactic instruction bits and non-player tactical settings;
- string/pointer replacement and variable-length relationship/contract collections.

Performance statistics and calculated outputs must not be presented as editable source fields unless FM itself stores them as writable fields. The editor changes the running game process; saving inside Football Manager is what persists a change to the career file on disk.
