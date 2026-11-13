# FM Atlas — Known Issues and Verification Gaps

This list replaces the earlier recovery-era issue list. It distinguishes code present in the recovered R7.2.0 tree from features accepted at runtime.

## Critical verification gates

- **Current overhaul branch not freshly built:** the user supplied a successful R7.2.0 baseline build log (0 errors, 15 warnings), but later branch changes need a new Windows .NET 8 build. A Windows GitHub Actions workflow has been added, but no run is currently reported.
- **Application launch/runtime not evidenced:** navigation, database startup, themes and each feature route still need Windows smoke tests.
- **Diagnostics privacy hardening not yet run in the app:** `AtlasLog` token/password and full Windows/UNC path redaction have been corrected in source; `ValidateRedaction()` appears in quick Diagnostics. Pattern-level tests passed locally, but the .NET self-check needs Windows execution.
- **Historical baseline trees incomplete:** R7.0.2 and R7.1.0 ZIPs are inventoried and compared, but only R7.2.0 is currently imported as the active app tree. Commit/tag older complete source baselines separately before claiming all versions are preserved as Git trees.

## Architecture and UI

- MainWindow is a large code-behind class; application, presentation, data and adapter responsibilities are not yet separated cleanly.
- Many navigation routes are present, but route presence does not prove each page is complete. Every route needs acceptance and dead-control review.
- MONOLITH theme service supports Obsidian and Warm Light, but semantic token coverage, reusable controls, typography/spacing/radius tokens and migration of all screens remain incomplete.
- Current shell uses Ctrl+F global search, not the planned Ctrl+K command palette. Notifications, responsive workspace layouts and polished icon navigation remain pending.
- Root App.xaml/MainWindow.xaml still contain legacy R7 palette defaults; ensure theme resources become authoritative without regressions.

## Search, data and analytics

- Advanced Search source has attribute/stat filtering with up to 60 AND conditions, but full universal field registry, grouped AND/OR logic, saved search profiles, nested groups, explanations and cross-entity search remain incomplete.
- Player position/suitability visuals, full profile coverage, Player DNA and comparison workflows need implementation/acceptance.
- Stats/Analytics use imported metrics and optional per-90 calculations. Known-answer tests for totals, per-90, minutes floors, season joins, missing values and duplicate identities remain outstanding.
- Value Finder is a relative cohort ranking of CA/PA versus value percentile, not FM's hidden valuation formula. Fee, asking price, market value and wage must remain separate concepts.
- RoleFitService contains 23 weighted coaching-style profiles; it is not the game's internal role suitability formula. Validate weights, coverage and position restrictions against fixtures.
- Cross-league normalization and competition-strength adjustments are not defensible until the source and calibration data are proven; show raw league-relative results meanwhile.

## Save import and database

- Fmsave bridge requires exact version/layout validation against representative FM27 saves and known-answer table counts.
- Preserve raw rows and missing/unknown values; never turn missing values into zero.
- Full entity/field metadata registry across player, staff, club, competition, nation, finance, contract, injury and tactical entities remains pending.
- Schema migration, import transaction recovery, idempotency, duplicate identities and rollback need fixture testing.

## Live game adapter

- Build profile matches are based on PE size/timestamp records. A profile match is not field-level certification and does not prove the user's exact Game Pass binary.
- FieldEvidencePolicy distinguishes unverified/candidate/profile-matched provisional/field-verified records.
- `LiveWriteCertification.IsCertified` currently returns false. Apply and Undo are blocked. Do not change this until exact build, per-field witnesses and disposable offline-save acceptance exist.
- Read-only acquisition, session invalidation, identity matching and UI witness collection require independent verification.

## Graphics, skins and modding

- GraphicsResolver supports configured folders and config.xml parsing, but user's actual Game Pass graphics root, duplicate precedence, cache invalidation and FaceDrop separation need acceptance.
- Skin discovery/install/restore workflow and FM27 compatibility lab remain future work.
- Unknown executables, DLLs and plugin packages must be inspected statically only; never run them automatically.
- GPL-licensed repositories remain research-only unless the user makes a deliberate licensing/distribution decision.

## Packaging and release

- Installer and publish scripts exist in source; reproducible publish, upgrade/migration, rollback, code-signing decision, hashes and clean-machine install remain unverified.
- Accessibility, high-DPI, performance and regression coverage are not yet complete.
## Stage 1/2 verification update — 2026-11-13

The following supersedes older build/privacy notes above where they conflict:

- Last completed green application verification before the new Diagnostics composer/profile additions: Windows run 43473487285, exact source 8cfe5b3, 53/53 tests, 0 warnings and 0 errors.
- The support ZIP composer has been extracted into Services/SupportBundleComposer.cs; five tests cover allowlisted entries, ZIP round-trip, manifest hashes/sizes, redaction and path-traversal rejection.
- Saved Stats/Attribute Search profiles now use SavedSearchProfileStore and local atomic JSON persistence; six tests cover round-trip, update, duplicate names, deletion, validation and corrupt-file preservation.
- The first CI run for those additions failed because the new source files lacked explicit System.IO imports. Those imports were added. The manifest serializer was also set to camelCase to match its test/schema contract.
- Windows Actions run 43475788525, exact code commit 6cfdee5, is the current verification run for the corrected composer/profile source. It was pending at the time of this note; do not claim success until it completes.
- A preceding compile failure also occurred from malformed C# string literals during an initial privacy-copy edit; those lines were corrected before the current run. Both failed intermediate runs remain part of the audit history.
- Diagnostics opt-in now includes player names and recent edit history only. Credentials/tokens, absolute paths and career save filenames remain always redacted. Actual ZIP generation and manual inspection on the user's Windows PC remain outstanding.
- All 21 routes have been statically mapped in docs/STAGE1_STAGE2_AUDIT.md; Windows runtime route smoke tests, visual/DPI acceptance, actual Game Pass build verification and graphics-root validation remain outstanding.
## Latest Stage 1/2 Windows verification — 2026-11-13 17:49 UTC

- Corrected source commit `6c18e36fdc706fc49ad74ce15fe1ee73c43ce991` passed Windows Actions run `43476085012`: Release build 0 warnings/0 errors; 64/64 tests passed.
- The earlier pending status for run `43475788525` is superseded. That run failed on missing System.IO imports and test API/signature issues; these were fixed before `43476085012`.
- Automated tests cover support bundle archive allowlisting, hash/size manifest, redaction, path-traversal rejection, saved search profile persistence and SQLite live-evidence isolation.
- Manual WPF route smoke tests, actual support ZIP inspection, save-copy import acceptance, theme/DPI visuals, Game Pass build confirmation and graphics-root validation remain open.
## Integrated Stage 3–7 and Moneyball continuation — 2026-11-13

- The 81-test Windows pass at commit 4c588147 verifies compilation and the automated test suite only; it does not establish WPF runtime acceptance or actual Football Manager/Game Pass compatibility.
- Moneyball is an initial single-metric cohort screen. It does not yet provide defensible position/age/league segmentation, multi-metric profiles, cross-league strength adjustment, or a recruitment-grade value model. Percentiles describe the rows actually returned, not SI ratings.
- Raw seasonal totals and per-90 queries have received further fixes for minutes floors, duplicate player-period observations and FM View CSV fallback. The added SQLite tests are awaiting Windows CI on current branch head 8c698593; do not mark them verified until that run passes.
- The metric catalogue may contain fields from different providers. The UI must continue to disclose provider/period/sample basis and must not merge incompatible rows or treat absent minutes as a qualified sample.
- Player DNA, Scouting Matrix, Tactical Studio, Match Lab and Development are early integrated views. Their presence in navigation is not evidence of complete workflows or real-save correctness.
- Tactical Studio's score is an Atlas weighted attribute estimate. It is not Football Manager's internal role score or Tactical Effectiveness calculation.
- Match Lab currently exposes stored aggregate performance facts. It must not display invented event timelines, shot locations or match ratings.
- Game Pass build/profile recognition remains unconfirmed for the user's installed executable. A module fingerprint is not field-level certification.
- Live memory Apply/Undo remains deliberately disabled. Do not weaken the write certification gate.
- Manual WPF startup, all-route smoke tests, theme/high-DPI/accessibility checks, imported copied-save acceptance, graphics pack validation, clean-machine installer tests and release rollback remain open.

## Moneyball validation correction — 2026-11-13

Windows run 43484787575 built with zero warnings/errors but had 84/85 tests pass. The only failure was a test-fixture primary-key collision caused by reusing the same FmsaveStatFacts row index for two different players. The fixture row indexes were corrected in commit d863808; this was not evidence of a failing production query. The corrected test and follow-on malformed-filter tests are being re-run in Windows CI. Keep the result pending until the current-head run is green.
