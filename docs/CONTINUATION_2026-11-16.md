# FM Atlas — Continuation checkpoint (16 November 2026)

## Source of truth
- Repository: \`lewisxatk/FM-Atlas\`
- Active branch: \`rebuild/fresh-foundation-001\`
- Base checkpoint for this follow-up: \`e5fe8aa5dc1d7920c06cba4e30df243bc29574e0\`
- Draft PR: #10; do not merge until Windows build, tests and runtime acceptance pass.
- \`main\` remains untouched by rebuild work.

## Current implementation
The branch contains the clean .NET 8 WPF solution, typed Domain/Application/Infrastructure layers, SQLite snapshot and field index, Dashboard, Players/Staff/Clubs directories, player/staff/club profile workspaces, Quick Search, schema-driven FMRTE-style Advanced Search, CSV import, graphics-folder discovery, and the exact-fingerprint read-only FM27 Game Pass 0.9.2435.0 connector.

The connector reads Player, Staff and Club snapshots into a disposable SQLite live provider and monitors the active career's save-generation marker. The Windows process accessor exposes reads only. The live writer does not exist; Save Live Changes remains disabled. Save Atlas is local Atlas database initialisation/verification, not a Football Manager career save.

The current profiles intentionally show Not Imported / Unknown / In Progress for data families not supplied by the importer. Newgen provenance is not populated and must remain Unknown. The full Football Manager world model is not yet imported.

## Latest CI evidence before this update
- Runs 43871064257 and 43871053277 both ended before any job steps.
- Their jobs report \`runner_id=0\`, empty runner names and empty \`steps=[]\`.
- No Restore, Build or Test ran. Do not describe those runs as compiler failures or successful verification.
- The source-authoring environment does not have Windows/.NET SDK/MSBuild, so a Windows build and WPF launch are still outstanding.

## Changes in this follow-up
- Pin Windows CI to the supported Windows Server 2022 hosted runner instead of following \`windows-latest\`.
- Expand CI to run restore, Release build, automated tests with TRX output, self-contained Windows x64 publish and ZIP packaging.
- Always upload available environment/test diagnostics; upload the runnable ZIP only after the full build/test/publish job succeeds.
- Update the local PowerShell packaging script to write per-step logs, TRX test results, a SHA-256 package hash and a human-readable pass report. The Windows ZIP is self-contained and does not require a separate .NET Desktop Runtime.
- Add \`docs/TEST_HANDOFF_2026-11-16.md\` with safe first-launch and read-only FM27 acceptance steps.
- Replace stale pre-rebuild issue notes with current branch-specific verification gaps.

## Test and acceptance rules
1. Build/package the exact branch head. Never use an older artifact after a source update.
2. Test the app on Windows 10/11 x64. Run Restore, Release Build and all tests before creating a package.
3. Run FM27 Game Pass package 0.9.2435.0 with a disposable career copy only. A different package fingerprint must be rejected rather than guessed.
4. Compare representative Player, Staff and Club values with the same in-game records. Report mismatches; do not adjust offsets from a single plausible sample.
5. Exercise Quick Search and Advanced Search minimum-only, maximum-only, bounded-range, position and All/Any cases.
6. Capture screenshots at the actual Windows display scale; no visual-completion claim until compared with REF-001 to REF-004.
7. Do not implement or enable writes until the read mapping is independently verified and all preflight/read-back/Undo gates pass.

## FMRTE workflow and search rules
- Player, Coach and Club entity scopes; field group/category navigation; saved search criteria; independent minimum and maximum conditions.
- Minimum-only means value >= minimum; maximum-only means value <= maximum; bounded numeric ranges are inclusive.
- All selected positions means AND; Any of these means OR. Position familiarity is a minimum rating when the user supplies a minimum and leaves the maximum blank.
- Unknown values stay unknown. Generated/newgen is source-identified, not guessed from age, PA, portrait, name or first-seen timestamp.
- Loading the game means reading the currently running career. Football Manager itself persists a live career when the user saves it inside the game.
- Undo in a future writer must check the expected current value before restoring an old value.

## Visual authority
Apply the approved Watched.Earth-inspired black/charcoal/off-white/orange visual identity while retaining REF-001 app composition, REF-002 FMRTE Advanced Search information architecture and REF-004 player-profile arrangement. Avoid inventing substitute layouts or decorative metrics. Check contrast, keyboard navigation and 100% / common Windows scaling.

## Safety boundary
- Existing memory accessor is read-only; no write primitive exists.
- Save Live Changes remains disabled.
- Unknown/unregistered game builds remain read-only.
- Save Atlas refers only to local Atlas SQLite state.
- Never test destructive operations on the user's only career save.
- Do not merge PR #10 or mark the app release-ready before Windows and Game Pass acceptance.
