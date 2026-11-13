# FM Atlas CoachPro R5 — implementation and verification review

## Rebuild overview

R4.3 was a single-window scouting workspace with a live identity probe. R5 replaces the main window with a multi-workspace WPF shell and adds a save-file snapshot adapter. The former SQLite database remains the primary local store; the new `Fmsave*` tables are additive and snapshot-scoped.

## Implemented in source

| Area | Implementation | Status |
|---|---|---|
| Visual shell | CoachPro aqua/teal surfaces, sidebar navigation, rounded cards, responsive grid columns | Implemented; requires Windows visual review |
| Players | Search, profile, 52 display attributes + personality when supplied, positions, state, contract facts | Implemented in source; FM27 field meanings not confirmed |
| Recruitment | Age/position/attribute/foot filters and shortlist workflow | Partial; no transfer-budget or similar-player engine |
| Staff | Save snapshot staff list and raw scalar profile facts | Partial; source exposes limited rated staff attributes |
| Clubs | Club directory and linked player count plus related raw facts | Partial; some save-local IDs and finance records need interpretation |
| Squad Architect | Club squad listing and positional/age overview | Partial; no fully validated recommendation model |
| Transfers | Transfer/value/CA/PA display where available | Partial; currency and field semantics require verification |
| Tactical Lab | Tactics, training, selections, mentoring and set-piece table counts/raw records | Partial; no tested tactical scoring engine |
| Editor | FM Atlas local database CA/PA and attribute edits, audit and undo | Local-only; never writes to the running game/save |
| Atlas Assistant | Rule-based parser for age, basic position and attribute search | Prototype; not generative AI |
| Live reader | Existing read-only process identity importer | Limited to validated identity/relationship data |
| Save reader | Offline local `fmsave` adapter and validation report import | Implemented; requires Windows Python 3.12+ and a supported save build |
| Persistence | Existing database path retained; additive schema migrations | Static source review only; migration requires Windows runtime test |
| Installer | Inno Setup script + self-contained publish and build scripts | Definition supplied; installer not compiled in this environment |

## Specific parser caveats

- `fmsave` documents player `uid` as save-local and `unique_id` as the game database ID that is more stable across new careers from the same DB. The adapter uses a composite namespace (`game`, `db_version`, `unique_id`) for Atlas identity and keeps save-local `uid` for relationships/snapshot rows.
- `fmsave` marks its FM27 field meanings unconfirmed. Even if the exact build is listed as registered, this does not certify semantic accuracy.
- Current ability, potential ability and reputation values are retained, but documented as unconfirmed by the parser.
- Display attributes are 1–20 and raw attributes are 1–100. Only the display scale is placed in the standard attribute table.
- Nations reader rows contain save-local `nation_id` and game `database_id`, not country names. FM Atlas does not display IDs as nationality names.
- Current staff coverage does not include every FM staff attribute. Club facility coverage is partial.

## Not implemented / not claimed

- Full FMRTE live editor parity or any memory write path.
- Fully verified FM27 offsets for CA/PA, every attribute, contract or club field.
- Confirmed import of all staff coaching/mental attributes.
- A complete, validated finances engine, budget-aware recruitment ranking or financial forecasts.
- Fully computed tactic/role suitability, squad optimiser or best-XI engine.
- Full AI-powered conversational assistant.
- Cross-build guarantees for any FM27 save build not explicitly registered and tested.

## Verification performed in this environment

- Python bridge source compiles with `python -m py_compile`.
- WPF XAML and project XML parse as well-formed XML.
- C# delimiters and referenced page wiring receive static checks.
- ZIP archive integrity is checked after packaging.

## Verification not possible here

This environment does not have the Windows .NET 8 SDK/MSBuild/WPF targeting pack, Python `fmsave`, or a Football Manager 27 career save. The R5 WPF project therefore has **not** been compiled or run in Windows, and the save parser has not been exercised on your career. Those checks are required before calling this a production release.
