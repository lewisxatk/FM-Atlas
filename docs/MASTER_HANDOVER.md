# FM Atlas — Master Handover

**Checkpoint:** 14 November 2026  
**Repository:** https://github.com/lewisxatk/FM-Atlas  
**Current integration branch:** `feature/v9-all-roadmap`  
**Active pull request:** [#5 — FM Atlas V10: FM27 Data Hub & Pre-Testing Integration](https://github.com/lewisxatk/FM-Atlas/pull/5)  
**Current delivery stage:** controlled Windows/FM27 acceptance; not release-ready.

## Current source of truth

Continue from `feature/v9-all-roadmap`. Do not recreate the application, restart from `main`, or discard the existing V8/V9 product foundations. The consolidated V10 source tree is:

`application/R7.2.0/FM-Atlas-R7.2.0-Work/`

The working branch contains the WPF/.NET 8 solution, test project, Python save bridge, Monolith interface, player search and scouting work, FM27 save/data explorers, supporting documentation and the V10 Windows workflow. Older commits/branches and the source recovery branch remain historical comparison points; do not overwrite them.

## Verified build baseline

At integration commit `5f8dc334485d71ca39215d427921c5f6ddf1242d`, Windows workflow run #585 completed successfully on a Windows x64 runner:

- .NET 8 solution restore and Release build succeeded.
- Python 3.12.10 installed and verified `fmsave 0.12.4`.
- Python save-bridge syntax and two adapter unit tests passed.
- The complete xUnit suite passed: 106/106.
- Self-contained Windows x64 publish completed and the `FM-Atlas-V10-win-x64` artifact was uploaded.

The follow-up commits add a live-write service gate, three regression tests, diagnostics corrections, updated V10 test documents, a workflow path correction and a dispatcher-warning correction. Windows workflow **#605, attempt 2**, completed successfully: .NET Release build passed, xUnit **109/109**, self-contained win-x64 publish passed and the artifact upload succeeded. The verified application/test commit is `3e236ea9cd4d2b95c299b5ecd53a255b39a92658`; subsequent branch commits changed only root-level handover/roadmap documentation. Current artifact: `FM-Atlas-V10-win-x64`, artifact ID `14126731937`, SHA-256 `3d02aec66ad6a9fe3ccdfe553637095bc78c75619dca36bde5f52623ff86368c`.

A successful CI run verifies compilation, automated tests and packaging. It does not verify a launch on the user's Windows PC, actual FM27 field meanings, Steam/Game Pass attachment, visual acceptance or installer behaviour.

## Product foundations — preserve all of these

- **Monolith Glass:** graphite/charcoal surfaces, warm orange/amber accent, restrained motion, dark/light/Studio appearance options.
- **Search:** Ctrl+K Global Search, FMRTE-style player/staff search, Advanced Search, saved criteria and indexed multi-condition statistic queries.
- **Player intelligence:** profiles, player comparison, attribute radar, position-suitability mapping, similar-player discovery and player-development opportunity screening.
- **Recruitment:** Value Finder/Performance vs Value, Scouting Matrix, saved scouting profiles, shortlist/pipeline and shortlist handoff.
- **Tactical:** Tactical Lab and explainable role-fit screening; scores remain Atlas estimates, not the hidden FM formula or official in-game stars.
- **External scouting analytics:** StatsBomb Open Data event import, unified event-action data, possession/passing analysis, empirical Atlas xT and user-owned Wyscout player-season CSV import. Do not claim Wyscout event-feed parity or commercial model parity.
- **FM27 save intelligence:** read-only fmsave 0.12.4 adapter, offline save import, player season/match/history rows, FM Player Stats Explorer, League Explorer and Team Match Explorer.
- **Live career:** process/build detection and bounded, cancellable, read-only career import. Process fingerprint match is not a certification of every field mapping.
- **Integrity:** source family and raw provenance retained; missing is not zero; missing minutes are not estimated; unresolved names and fixture joins stay visibly unresolved.

All changes must be additive unless a specific regression fix is documented. Do not remove existing Advanced Search, comparisons, position ratings, profiles, shortlist or Monolith work to make a new feature easier.

## V10 first controlled testing boundary

The first round is **read-only** with respect to Football Manager.

- Live-memory Apply and Undo are hard-disabled at the service boundary in V10 pre-testing.
- Automated regression tests are being added to prove Apply/Undo fail before the game process can be opened for writing.
- Tester & Logs must report `Live-memory write gate: DISABLED`.
- Do not test live edits, use a manual switch, or bypass the service gate.
- Local Atlas database edits and live FM memory writes are distinct systems; leave editor functions alone during the first acceptance round.
- Use a copied offline single-player FM27 career, never the only save or an online/shared career.

First test plan: `application/R7.2.0/FM-Atlas-R7.2.0-Work/docs/R7.2.0-USER-TESTING.md`  
Checklist: `application/R7.2.0/FM-Atlas-R7.2.0-Work/docs/R7/BUILD-AND-TEST-CHECKLIST.md`  
Report template: `application/R7.2.0/FM-Atlas-R7.2.0-Work/TEST-REPORT-TEMPLATE.md`

## First acceptance sequence

1. Download the latest successful `FM-Atlas-V10-win-x64` workflow artifact and extract it into a fresh folder.
2. Back up `%LOCALAPPDATA%\FmAtlas` and preserve previous installations.
3. Launch and relaunch the app; inspect Monolith shell, navigation, diagnostics, database preservation and support-pack redaction.
4. Test Ctrl+K, Advanced Search, profiles, comparisons/position ratings, Similar Players, Value Finder, Scouting Matrix/shortlists, Tactical Lab, Player Development and Shortlists.
5. Export/import an Atlas snapshot from a copy of an FM27 offline save using the read-only bridge.
6. Compare representative player data against the actual in-game UI.
7. Compare player-season facts, league tables and team-match reports against FM27 Data Hub/League Hub with the same player, competition, season and match context.
8. Check missing values, source keys, minutes, fixture matching, cancellation and career switching.
9. Inspect DPI/resize behaviour at 100%, 125% and 150% scaling where available.
10. Record the environment and any mismatch in the test report. Do not send the original career save or personal SQLite database.

## Remaining gates after the first test

- Actual Windows app startup/stability and DPI validation.
- Real FM27 save comparisons and witnessed field meanings.
- Steam/Game Pass process profile and save-generation validation.
- Search/analytics performance on a realistic career and cancellation/save-switch checks.
- StatsBomb conversion/edge-case reconciliation against raw public source files.
- Installer first install, upgrade, uninstall and DB preservation/rollback.
- Only after field mappings and rollback are independently witnessed on a disposable save should live editing be reconsidered.

## Repository workflow and safeguards

- Work on `feature/v9-all-roadmap` and PR #5. Do not merge to `main` or base branch until user acceptance gates pass.
- Do not force-push or rewrite previous project history.
- Keep prior R7.x archives, recovery branches, rebuild branches and user data intact.
- Every meaningful development checkpoint needs a commit, changed-file record, exact build/test evidence, known risks and next action.
- Never commit real FM saves, personal databases, passwords, API tokens, licensed facepacks/logos or other private user data.
- Use `docs/NEXT_STEPS.md` for the active next-step plan and `docs/KNOWN_ISSUES.md` for current open verification gaps.

## Historical recovery note

The original 13 November recovery handover and R7.2.0 archive inventory remain useful for source provenance, but their earlier statements that the application source was absent and Windows build verification was pending are superseded by the V10 integration branch and the successful Windows workflow described above. The old R7.2.0 live-writer test sequence is also superseded by the V10 read-only pre-testing gate.
