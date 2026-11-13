# Next Steps — FM Atlas

## Current verified state (2026-11-13)

The complete R7.2.0 application source is imported on:

`recovery/r7.2.0-source-import`

- GitHub recursive tree is complete (not truncated).
- The application folder contains exactly 106 tracked files, matching the R7.2.0 checkpoint count.
- Required solution, WPF project, source, assets, installer, docs, archive and tools paths are present.
- Temporary import placeholder removed; root `.gitignore` added.
- No merge to `main`; all work remains isolated on the recovery branch.
- Static audit: [R7.2.0 Static Audit](../recovery/R7.2.0-STATIC-AUDIT.md).
- R7.2.0 source archive SHA-256: `34d985a39526698026b1e09855d7f37e9f6a00dfba4863cf6f4d467aafedc2c8`.

**Important:** file count and paths are verified, but a byte-for-byte per-file comparison with the original ZIP has not been performed. Keep all original source ZIPs and the recovery bundle as offline backups. R7.0.2 and R7.1.0 are documented in the recovery inventory but their full application source archives have not been imported as separate source trees in this branch.

## Immediate gate — Windows baseline build

1. Use a fresh extracted copy of `application/R7.2.0/FM-Atlas-R7.2.0-Work`; do not build over an older working installation.
2. Confirm .NET 8 SDK x64 is installed.
3. Run `Build-FM-Atlas-27.bat`.
4. Send the unedited `build-logs/latest-build.log` back for review.
5. If build succeeds, launch the WPF app and test startup, navigation and Tester & Logs.
6. Record Windows version, .NET SDK version and exact FM27 store/build.

No compile, WPF startup or runtime acceptance has yet been verified by this repository audit.

## Gate 2 — database and read-only data

- Back up `%LOCALAPPDATA%\FmAtlas` before any migration test.
- Use a COPY of an offline single-player FM27 career.
- Test save bridge export/import and record player/staff/club/stat counts and warnings.
- Compare representative CA/PA, attributes and statistics against the FM UI.
- Test multi-attribute search, multi-condition statistical search, missing-value handling, minutes thresholds and FM View CSV import.
- Test Value & Upside Finder as a relative screening index, not a real FM valuation formula.
- Test Graphics Resolver against the user's actual Game Pass graphics directory and config.xml files.

## Gate 3 — live editor (blocked until Gates 1–2 pass)

The source contains a narrow live-memory writer, but it is not accepted or certified safe. Exact PE profile matching does not prove each field offset/encoding. Player field mappings and save-load generation pointers require independent validation against the user's exact FM27 build.

- Keep live editor disabled.
- Do not test on the only career save or an online/shared career.
- Do not attempt Apply/Undo until read-only identity/import tests pass and field mappings have independent witnesses.
- Hidden attributes, personality, position, staff, club, contract, transfer and loan writes remain unsupported.

## Development sequence after baseline gates

The agreed visual direction is **MONOLITH**: Obsidian/Graphite surfaces, Atlas Orange accent, restrained animation, rounded controls, high readability, Swiss Sport editorial structure with Touchline football-specific visuals and Pulse data presentation. R7.2.0's current lime-accent Obsidian theme is not the final agreed design.

1. Build shared MONOLITH design tokens and reusable WPF components (buttons, rounded panels, inputs, tables, dialogs, pop-out panels, navigation).
2. Build the FMRTE-style Advanced Search pop-out on the shared components, preserving verified query logic.
3. Build the Scouting Matrix flagship screen (filter builder, player results, selected-player profile).
4. Build Player DNA and Tactical Intelligence screens using only verified data.
5. Apply the design system across the rest of FM Atlas.

### Advanced Search acceptance requirements

- Quick Search plus one-click advanced pop-out.
- Searchable field catalogue grouped by identity, positions, attributes, contracts, club/league, origin/home-grown and verified season statistics.
- Add/remove/duplicate/reorder conditions; grouped AND/OR; include/exclude; type-aware operators and values.
- Active-filter chips, result counts, reset/clear, saved searches and presets.
- No dead filters: every visible criterion must map to an existing field and tested query behaviour.

## Checkpoint rule

Every development session ends with a GitHub commit or an explicitly documented uncommitted state, changed-file list, build/test evidence, known risks and the next exact action. Never label source code complete solely because a feature is described in a roadmap or implementation note.
