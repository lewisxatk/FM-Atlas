# FM Atlas — Current Known Issues and Verification Gaps

**Updated:** 16 November 2026  
**Source of truth:** \`rebuild/fresh-foundation-001\`  
**Draft pull request:** #10 — must remain unmerged until Windows and game-runtime acceptance pass.

## Critical — Windows build is not yet verified

- The latest two Windows Actions jobs on commit \`e5fe8aa5dc1d7920c06cba4e30df243bc29574e0\` failed with \`runner_id=0\`, no runner name and an empty step list.
- No Restore, Build or Test step ran. These workflow failures are runner-allocation failures, not compiler diagnostics.
- This update pins CI to the explicitly supported \`windows-2022\` label and makes CI publish a self-contained test ZIP only after the restore/build/test gate passes.
- No local Windows/.NET SDK/MSBuild environment is available in the authoring session to replace the Windows CI compile or launch test.

## Live FM27 connection — read-only, not yet user-verified

- The current connector is pinned to FM27 Game Pass package 0.9.2435.0 and an exact \`game_plugin.dll\` PE fingerprint.
- The accessor exposes \`ReadProcessMemory\` only. It has no process-memory write function.
- The reader imports Player, Staff and Club records. It is not the complete Football Manager data model: competitions, nations, fixtures, results, history, tactics, training, injuries and transfer activity remain outside this import.
- Field values carry \`Mapped\` evidence from external research until individually compared against the user's running game. Do not promote a field to ReadVerified based only on matching offsets or plausible values.
- The user's actual Game Pass package, live save-generation stability and representative player/staff/club values have not been verified in-game.

## Player, Staff and Club profiles

- The profile workspaces and section navigation exist.
- Data families not supplied by the importer must show a clear Not Imported / Unknown / In Progress state. A visible tab does not mean the field family is populated.
- Player CA/PA and supported attribute values can only be accepted after a comparison with the same player in the running career.
- Club membership joins use source-mapped \`club_uid\`; that relationship still needs an in-game spot-check on the user's build.
- Graphics discovery reads supported FM graphics folders and \`config.xml\` mappings; real local folder/portrait/badge matching needs user-side verification.

## Advanced Search

- Player, Coach and Club entity contexts and typed indexed fields are implemented.
- Numeric/date filters support one-sided lower/upper bounds and inclusive ranges; position matching has separate All/Any logic.
- Search can only query fields actually present in the local index and marked executable. Unsupported/unmapped fields must stay disabled or In Progress, not return fabricated values.
- Newgen status is not populated by the live reader yet. It must remain Unknown unless the source explicitly identifies it; first-seen, youth, PA, name or portrait are not sufficient proof.
- Automated regression tests are present in source but remain unverified until a Windows runner actually executes them.

## Live editing and saving

- Save Live Changes is disabled.
- There is no live process write API and no WriteVerified field.
- A future writer must independently validate exact build, process, active save generation, entity identity, expected old value, writable memory region, value range, read-back and guarded Undo.
- FM career persistence must be confirmed by saving and reloading within Football Manager itself.
- Save Atlas only initialises/verifies local FM Atlas SQLite state; it does not save the FM career.

## UI and release acceptance

- The Watched.Earth-derived black / charcoal / off-white / orange visual direction is in the current shell; screenshot comparison at the user's Windows scaling and approval remain open.
- Directory, Quick Search, Advanced Search and profile interactions need user-side smoke testing with a real career dataset.
- PR #10 must not be merged and no release-ready claim should be made until Windows restore/build/test, a WPF launch, UI acceptance and safe read-only Game Pass verification pass.
