# R5 CoachPro rebuild

- Replaced the R4.3 main window with a new multi-workspace CoachPro shell based on the supplied aqua/teal player workspace reference.
- Added Dashboard coverage and snapshot status, Player and Recruitment workspaces, position/age/CA/PA/attribute/foot filters, player cards and full imported attribute profiles.
- Added a local read-only `fmsave` save snapshot bridge and import validation record, retaining save-local raw tables alongside normalized player, staff and club directories.
- Added a manager card source from the human-manager record when present in the parsed staff table.
- Added Clubs, Staff, Squad Architect, Transfers, Tactical Lab, Shortlists, Local Editor, Edit History and rule-based Atlas Assistant workspaces.
- Added CA/PA search and category position filters, and retained raw 1–100 attributes separately from displayed 1–20 ratings.
- Preserved the existing SQLite database path and prior R4.x data through additive schema changes.
- Added self-contained win-x64 publish and Inno Setup installer definitions.

## Known boundaries

- FM27 save parser field meanings are currently unconfirmed; values require comparison against the live game.
- The live-memory importer remains read-only and identity/link focused. No memory writes are enabled.
- Staff attributes, club facilities and finance data are partial where the external parser does not expose complete fields.
- Tactical suitability, budget-optimised recommendations and generative assistant reasoning remain future engineering work.
- No Windows build or actual FM27 save import was run in the preparation environment; see `docs-R5/SOURCE-REVIEW-R5.md`.
