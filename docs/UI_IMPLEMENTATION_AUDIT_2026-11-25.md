# FM Atlas UI implementation audit — 25 November 2026

**Code branch:** `rebuild/fm27-0.9.2562-readonly-profile`  
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
