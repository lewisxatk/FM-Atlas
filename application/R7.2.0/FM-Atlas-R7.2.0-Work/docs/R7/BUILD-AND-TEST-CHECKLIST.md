# FM Atlas V10 — First Controlled Test Checklist

Use this checklist with `docs/R7.2.0-USER-TESTING.md`. The V10 pre-testing build is read-only against the FM process.

## Package and first launch
- [ ] Latest V10 Windows x64 workflow on `feature/v9-all-roadmap` is green.
- [ ] Downloaded the `FM-Atlas-V10-win-x64` artifact from that successful run.
- [ ] Extracted to a new folder; earlier versions and ZIPs are untouched.
- [ ] Backed up `%LOCALAPPDATA%\FmAtlas` before first launch.
- [ ] `FmAtlas.exe` opens on Windows 10/11 x64 without an unhandled exception.
- [ ] App relaunches cleanly.
- [ ] Monolith Glass Dashboard and navigation render at the tested resolution.
- [ ] Tester & Logs reports the live-memory write gate as DISABLED.

## Preserved search and scouting workspaces
- [ ] Global Search (Ctrl+K) finds a known player.
- [ ] Advanced Search executes five or more simultaneous criteria.
- [ ] Missing attribute/stat values remain unavailable, not zero.
- [ ] Player profile opens and exposes source-backed facts.
- [ ] Player comparison and attribute radar work; missing categories are not plotted as zero.
- [ ] Position ratings/comparison show available slots and preserve unknown/unrated slots.
- [ ] Similar Players returns source-backed matches and coverage.
- [ ] Value Finder shows selected inputs and its relative-score explanation.
- [ ] Scouting Matrix filters and shortlist handoff work.
- [ ] Tactical Lab/Role Fit and Player Development open and return only evidence-supported results.
- [ ] Shortlist records and notes remain visible.

## FM27 copied-save import and data integrity
- [ ] Python 3.12 and fmsave 0.12.4 are detected by Tester & Logs.
- [ ] Save bridge export uses a COPY of an offline single-player FM27 career.
- [ ] Snapshot import completes; player/staff/club and retained-table counts are recorded.
- [ ] Warnings and unknown-build/field-meaning notices are preserved.
- [ ] Representative player identities and values are compared with FM27.
- [ ] Player Stats Explorer discovers metrics actually present in the saved data.
- [ ] Per-90 only appears when real recorded minutes are present.
- [ ] League Explorer matches populated in-game standings rows; empty data is not replaced with synthetic zero rows.
- [ ] Team Match Explorer shows source reports and exact/unmatched fixture status.
- [ ] FM-native facts remain separate from Wyscout CSV and StatsBomb event datasets.

## UI, performance and diagnostics
- [ ] 100%, 125% and 150% Windows scaling checked where available.
- [ ] Window resize does not clip primary controls or data grids.
- [ ] Search, paging, filtering, sorting and profile opening remain responsive.
- [ ] Save-switch/cancellation checks do not mix snapshots.
- [ ] Diagnostics export is redacted by default and excludes the career save/SQLite database.

## Safety — mandatory
- [ ] Live-memory Apply is unavailable/hard-blocked.
- [ ] Live-memory Undo is unavailable/hard-blocked.
- [ ] No memory write was attempted.
- [ ] No test was run against the only save or an online/shared career.
- [ ] No local Atlas editing was performed during the initial read-only test round.

## Do not mark V10 release-ready until
- [ ] A real FM27 save has been checked against in-game Data Hub and League Hub values.
- [ ] Steam and/or Game Pass process attachment has been confirmed for the actual tested build.
- [ ] Monolith styling and DPI are accepted on the user's Windows display.
- [ ] Known mismatches are classified as source limitations, unverified fields, unfinished features or actual defects.
- [ ] Installer, upgrade, uninstall and database rollback receive a separate acceptance round.
