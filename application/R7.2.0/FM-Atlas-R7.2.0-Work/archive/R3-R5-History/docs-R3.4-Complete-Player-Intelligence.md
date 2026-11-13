# FM Atlas R3.4 — Complete Player Intelligence & Profile Foundation

## Included in this release

- Preserves the existing WPF scouting application, SQLite database, player import, search, club/staff screens and shortlist workflows.
- Promotes the full read-only player discovery workflow to R3.4.
- Adds an integrated intelligence evidence viewer. After a live scan, FM Atlas opens the resulting report in the application; existing R3.4 reports can also be opened later.
- Viewer supports player, category and text filtering, record counts, and a Hidden Stats filter.
- Injury Proneness is explicitly included under Hidden attributes, alongside Consistency, Important Matches, Dirtiness and Versatility. The field is shown with its current confidence/provenance note; it is not claimed independently witnessed on the user's FM27 build.
- Retains both goalkeeper and outfield attributes. The positional suitability values choose the primary profile category; they do not discard the other attribute family.
- Retains CA, PA, reputation, identity probes, contract details, and raw medical evidence in the scan.

## What remains deliberately incomplete

- Most attribute offsets are cross-referenced FM27/previous-version leads, not independently verified on every FM27 build.
- Medical pointer and byte/vector captures are discovery evidence only. They do not yet decode active injury, injury severity, recovery date, suspension or holiday records.
- Match fitness, match sharpness, fatigue and jadedness remain unmapped. They are not converted using the 1–20 attribute formula.
- Role suitability is not yet mapped; the 15 positional suitability bytes are retained separately.
- Packed dates, nationality/birthplace and several player-coach shifted fields are not fully decoded by this release.
- No memory writes or save-file writes are enabled. Editor actions remain disabled until individual field validation, backups, write safeguards and undo are implemented.

## Injury Proneness

`Injury Proneness` is a hidden player attribute in the R3.4 Hidden category, using the current candidate field mapping at player offset `0x18F` and the encoded attribute conversion `(raw + 2) / 5`. The upstream FM27 field reference describes inherited offsets as unverified unless separately witnessed. FM Atlas therefore displays its provenance as provisional and never treats it as an observed injury or medical state.

## Use

1. Start FM27 Steam and load the save.
2. Run FM Atlas and connect to the game.
3. Choose **R3.4 full intelligence scan** and enter up to eight player names.
4. Save the CSV report. FM Atlas automatically opens it in the integrated viewer.
5. Use player/category filters or **Show hidden stats** to inspect Injury Proneness and the other hidden values.
6. Use **Open intelligence report** to reopen a previous scan.

## Safety

The scanner remains read-only and retains the FM27 module fingerprint guard. It requests query/read process rights only. No memory writes, save writes or raw process pointer addresses are exported.
