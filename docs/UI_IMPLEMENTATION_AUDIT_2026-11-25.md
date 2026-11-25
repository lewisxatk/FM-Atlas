# FM Atlas UI implementation audit — 25 November 2026

**Code branch:** `rebuild/fresh-foundation-001` (27.1.1 reader integration merged via [PR #12](https://github.com/lewisxatk/FM-Atlas/pull/12))  
**Design authority:** `docs/DESIGN_SPECIFICATION.md` and `docs/REFERENCE_LIBRARY.md`  
**Status:** partial source implementation; not visually accepted as the user's supplied redesign.

## What is present in the current WPF source

- A near-black / warm off-white / grey / orange resource palette in `App.xaml`.
- Main application shell in `MainWindow.xaml`, with a 218 px left navigation rail, a 104 px top utility/header region, workspace content and top-level navigation.
- Quick Search, Advanced Search, Save Atlas and Load Game actions in the top header. There is no Search item in the left navigation.
- Dashboard and directory/workspace destinations for Players, Staff, Clubs, My Career, Save Manager and Settings.
- Player profile view with top section tabs, a left identity/photo rail, central attribute workspace and right information panel.
- A dedicated Advanced Search window with Player / Coach / Club selectors, category/filter groups, typed controls and position filters. The 2026-11-16 FMRTE screenshot `IMG_8151.jpeg` is available in the Library; the source uses a related category / filter / pitch arrangement but has not been pixel-compared on Windows.
- Position-map idle/selected states are styled in muted grey and orange.

## Important branch reconciliation finding

The fuller Watched Instrument UI work was not lost; it is isolated on [`rebuild/watched-earth-fmrte-ui`](https://github.com/lewisxatk/FM-Atlas/tree/rebuild/watched-earth-fmrte-ui) under [draft PR #8](https://github.com/lewisxatk/FM-Atlas/pull/8). That branch contains a dynamic WPF shell and full-width player workspace work with the Watched.earth × FMRTE direction. PR #8 is still open and explicitly marks several UI tasks as unfinished. Its base is the older `rebuild/v8-full-product-rebuild` / R7.2.0 architecture, not the fresh modular WPF application used by the current release branch.

The current fresh-foundation branch uses a separate `MainWindow.xaml`, `App.xaml`, views and typed view models. Therefore PR #8 cannot safely be merged wholesale: the UI should be ported deliberately into the current application layer while preserving the current database, search, profiles and guarded live reader. The old branch is a design/behaviour reference, not a drop-in update.

PR #8 itself lists remaining work: full Staff/Club Advanced Search parity, Staff/Club full-width closable record tabs, shared workspace-tab hosting with persistent state and keyboard navigation, and completion of visual tokens, accessible controls and DPI review. These are now explicit migration tasks for this branch.

## What is not complete / not accepted

- The exact main-dashboard and visual-identity reference screenshots REF-001 / REF-003 and the player-profile reference REF-004 are described in the reference documentation, but their binary files were not all available for direct inspection in this audit. The original user-supplied restyle archive is listed in the Library as `FM-Atlas-restyled.zip`, but the Files service did not grant raw-byte materialisation for it in this environment.
- The user has directly reported that the last test ZIP did not show the requested redesign. That report takes precedence over a code-only assumption that the visual implementation is complete.
- There has been no side-by-side screenshot comparison of the running Windows app against the original design source.
- Main navigation still contains deferred/disabled tools, including Squad & Tactics and Recruitment. The presence of a menu entry does not mean the feature exists.
- Several profile/data areas remain incomplete or empty when their data families are not imported or not verified.
- Player/staff/club UI screens are not yet validated with the user's actual 27.1.1 save.

## Acceptance checklist

- [x] Near-black / warm off-white / orange palette resources exist.
- [x] Quick Search and Advanced Search are placed in the global header rather than left navigation.
- [x] Player, Staff and Club directory/profile workspaces exist in source.
- [x] Advanced Search has entity selectors, categories and position filters.
- [x] Player profile has top section tabs and multi-column identity / data / info layout.
- [ ] Recreate the exact REF-001 dashboard composition and spacing.
- [ ] Recreate the REF-003 visual language and typography against the supplied screenshot.
- [ ] Match REF-004 player profile hierarchy and panel density.
- [ ] Run the app on Windows at 100% and 125% scaling and capture screenshots.
- [ ] Compare screenshots side by side with the original UI sources; adjust until accepted by the user.
- [ ] Verify controls and navigation, including every visible action, do something meaningful.
- [ ] Confirm every menu item marked available has a functioning page; otherwise label it planned / disabled.

## Release rule

A successful compile, unit-test run or eight-second WPF startup smoke test does not prove that the visual redesign has been implemented. Do not mark the design complete until the visual comparison and real interaction checklist above pass.


## Supplied HUD restyle archive received and ported (25 November 2026)

The user uploaded `FM-Atlas-restyled.zip` (archive source timestamps: 17 November 2026). The archive contains the actual WPF implementation, including:
- the HUD resource palette, tiled grid/crosshair backdrop, corner-bracket paths and button templates;
- compact text-input styling, dark slim scrollbars and monospaced HUD labels;
- a portrait 15-position football pitch, GK/DEF/MID/ATT/CLEAR group selectors and a live selected-position summary;
- a pulsing live-connection indicator.

The archive's Player, Club and Staff profile views and their view models match the active modular branch, so those existing data-bound profile screens are retained rather than replaced with stale copies.

Port work on `rebuild/fm27-27.1.1-ui-hud-port`:
- [x] Add the supplied HUD palette, grid backdrop, bracket controls, text-box template and slim scrollbar resources.
- [x] Apply the grid backdrop and animated live-status indicator to the main shell.
- [x] Port the portrait pitch and grouped position selectors into Advanced Search.
- [x] Add code-behind synchronisation for selected roles and the selection summary.
- [x] Windows Release build and XAML load verification — run #1100 succeeded; 126/126 tests, zero build warnings/errors, self-contained x64 package and WPF startup smoke test passed.
- [ ] Manual screenshots at 100% and 125% display scaling.
- [ ] Side-by-side comparison against the supplied archive and user reference images.
- [ ] Test all visible buttons, navigation, search, position groups and saved presets.

This is the **HUD restyle 002 port**, not the full Watched Instrument shell/record-tab migration tracked by issue #13. The build is technically verified, but the port is **not visually accepted or release-ready** until the remaining checks pass. The original archive was authored on 17 November and predates the current 27.1.1 reader integration; its older live-reader files must not replace the current build-specific scanner.


### Windows validation evidence — run 1100

- Branch: `rebuild/fm27-27.1.1-ui-hud-port`
- Commit: `df670351809a47bc34a266ed3ae0287a3e615c6a`
- GitHub Actions: https://github.com/lewisxatk/FM-Atlas/actions/runs/45617453400
- Release build: passed, 0 warnings, 0 errors.
- Automated tests: 126/126 passed.
- Self-contained Windows x64 publish and package verification: passed.
- WPF startup smoke test: passed.
- Artifact: `fm-atlas-win-x64-1100`.
- This is not a real FM27 live-career test and not a side-by-side visual acceptance pass.


## Second UI pass — compact navigation and keyboard workflow

The same port branch now also includes:
- A collapsible navigation rail: 218px expanded / 68px compact, with labels hidden and icon tooltips retained in compact mode.
- Keyboard shortcuts: Ctrl+K focuses global quick search; Ctrl+Shift+F opens Advanced Search; Escape closes quick-search results.
- The active workspace title is more prominent; main and profile surfaces use the supplied square-corner treatment.
- The live-status indicator is bound to `LiveSessionActive`, so its colour reflects actual connection state rather than showing green while disconnected.
- The existing closable workspace tabs and full-width Player/Staff/Club profile views are retained. The Player profile already exposes its section tabs in the current modular branch; no duplicate profile implementation was added.

### Windows validation — run 1114

- Commit: `ee25306891830f6382278c46c5f1f9e00c330d6a`
- Run: https://github.com/lewisxatk/FM-Atlas/actions/runs/45618746412
- Release build: passed, 0 warnings, 0 errors.
- Automated tests: 126/126 passed.
- Self-contained Windows x64 package and integrity verification: passed.
- WPF startup smoke test: passed.
- Artifact: `fm-atlas-win-x64-1114`.

Still outstanding: real FM27 27.1.1 career read; screenshot comparison at 100% and 125% DPI; visual acceptance against the supplied reference images; final dashboard polish; and a full Nation profile/data source. The My Career route currently displays manager nationality and current-club nation strings, but there is not yet a full `NationRecord`/Nation workspace.


## Third pass — package-scoped live validation correction

A second 27.1.1 blocker was found by tracing the full import path: the connector created a provider key from the detected package version, but the streaming validator still expected the legacy 0.9.2524.0 key. This meant 0.9.2562.0 records could be rejected after a successful memory read. The connector now passes the session's matched provider key into a validator that requires an explicit expected identity. A regression test covers 0.9.2562.0.

Windows run #1129:
- Commit: `d3125cafacc68f09a8e152c960d6851bb22b8091`
- Run: https://github.com/lewisxatk/FM-Atlas/actions/runs/45619837764
- 127/127 automated tests passed.
- Release build: 0 warnings, 0 errors.
- Self-contained x64 packaging and ZIP integrity: passed.
- WPF startup smoke: passed.
- Artifact: `fm-atlas-win-x64-1129`.

This is still not proof of a successful live career read. The next required acceptance is the user's 27.1.1 career test and the live-connection log if it fails.


## Final test package after cleanup — run 1145

The legacy provider constant was renamed to `LegacyLiveProviderKey` throughout the connector, startup diagnostics and tests, preventing future code from treating 0.9.2524.0 as the current package. The My Career fallback information panel was also brought into the square HUD visual system and its nation wording clarified.

- Commit: `476659c814d45af87355d0ae1f443d6a2ec52e13`
- Run: https://github.com/lewisxatk/FM-Atlas/actions/runs/45620917682
- 127/127 tests passed.
- Release build: 0 warnings, 0 errors.
- Self-contained Windows x64 packaging and ZIP integrity: passed.
- WPF startup smoke test: passed.
- Artifact: `fm-atlas-win-x64-1145`.
- The package is still not a substitute for testing against the user's live career.
