# FM Atlas — FM27 Player Export Import 001

**Date:** 2026-11-16  
**Working branch:** `rebuild/fresh-foundation-001`  
**Implementation:** `FMViewExporterCsvImporter`  
**Status:** Source and regression tests committed; Windows build/test still pending.

## 1. Source contract inspected

The public FM View Exporter documentation describes:
- F9 export of the currently displayed player list to CSV and HTML;
- `Unique ID` as the stable player identifier in an export;
- `Player` as the standard player header, with `Name` used in the Player Database view;
- headers matching Football Manager's displayed column names;
- English CSV using commas and German-style CSV using semicolons and decimal commas;
- a published FM27 column reference (`docs/columns/fm27.csv`) covering 375 table columns and an Insert Column menu catalogue.

Useful references:
- https://github.com/ElectroHugin/FM-View-Exporter/blob/main/README.md
- https://github.com/ElectroHugin/FM-View-Exporter/blob/main/docs/VIEW-ANALYZER-FM27.md
- https://github.com/ElectroHugin/FM-View-Exporter/blob/main/docs/columns/fm27.csv

The Analyzer view reference lists 47 visible player attributes across Goalkeeping, Mental, Physical and Technical groups. It also documents that `Personality` is not included in the Player Database and Shortlist views, so it must remain unknown when absent rather than being inferred.

**Storefront caveat:** the exporter's current README documents Football Manager 27 on Steam for Windows as supported. It does not claim Game Pass/Microsoft Store compatibility. FM Atlas's importer accepts compatible CSV files, but this does not establish that the exporter plugin will run on the user's Game Pass installation.

## 2. Import workflow added to FM Atlas

The main header now has an **Import CSV** action. It opens a file picker and imports a UTF-8 CSV into the local SQLite database.

Required columns:
- `Player` or `Name`;
- `Unique ID` (or a recognised UID alias).

Known fields mapped into typed PlayerRecord properties include:
- identity, age, date of birth, nationality, second nationality and gender when an explicit gender column is supplied;
- club, team, division/league, position and preferred foot;
- height, weight, international caps/goals;
- CA/PA only when explicitly supplied under matching CA/Current Ability and PA/Potential Ability headers;
- standard attributes, hidden attributes, personality ratings and foot ratings when the corresponding numeric columns are present.

The importer does not convert star-based `Ability` / `Potential` scout opinions into CA/PA. Those remain separate additional fields. It does not infer Newgen status, gender, hidden attributes, role ratings or statistics that are not present in the file.

## 3. Additional exported columns

Columns not covered by the fixed mapping are retained as provider-scoped `export_*` fields and indexed for Advanced Search.

Type classification is conservative and is computed across all populated values in a column:
- all Yes/No or True/False values → Boolean;
- all whole numbers → Integer;
- all consistent decimal values → Decimal;
- all recognised date values → Date;
- mixed, range-like, currency-like, or otherwise ambiguous values → Text.

Ambiguous values are not silently converted. Numeric fields become searchable with MIN/MAX; Boolean and Date fields use their corresponding Advanced Search controls; text fields support Contains/Equals.

The importer handles duplicate headers by creating distinct indexed field keys where necessary. It also handles the repeated `Nation` header documented by the exporter: the final `Nation` column is selected for the full nationality name, and other duplicates are kept separate only when they are not part of the fixed mapping.

## 4. Identity, idempotence, and data safety

- The importer requires a player name and a non-empty Unique ID. It never invents an identity from a row number.
- The exact source file is SHA-256 hashed; the hash-derived provider namespace isolates different export files.
- Re-importing the exact same file updates the same provider-scoped snapshots rather than duplicating them.
- Duplicate UIDs inside a single CSV are skipped and reported.
- Each imported field is tagged `DataSourceKind.GameExport` and `FieldEvidenceStatus.Mapped`. This is not a live-memory verification.
- Unrecognised columns are still searchable, but their values are not presented as verified FM Atlas domain fields.
- Import is read-only with respect to Football Manager. It writes only to the local FM Atlas database.
- File constraints: UTF-8, maximum 64 MB, 100,000 data rows, 512 columns.
- The CSV is processed row by row into typed snapshots. If an import is interrupted after some rows, importing the same file again is safe and idempotent for rows already saved.

No code from FM View Exporter was copied into FM Atlas. Its GPLv3 project is used as a documented file-format reference only; the FM Atlas importer is an independent .NET implementation.

## 5. Regression coverage added

Source tests cover:
1. comma-delimited CSV, quoted commas in player names, base player fields and known attribute mappings;
2. duplicate `Nation` headers and selecting the full nationality value;
3. dynamic decimal and text column indexing and execution through Advanced Search;
4. inferred Boolean, Date and Integer columns;
5. semicolon-delimited German decimal-comma parsing;
6. duplicate Unique IDs and idempotent repeat imports;
7. rejecting files without a stable Unique ID;
8. rejecting malformed quoted CSV.

These tests are committed but have not yet executed on a Windows runner in this session.

## 6. What this enables for first testing

When a compatible FM27 player-list CSV is available, the user can:
1. open FM Atlas;
2. choose **Import CSV**;
3. select the exported player-list CSV;
4. search imported players in Quick Search or Advanced Search;
5. double-click a player from Advanced Search to open the read-only typed player profile;
6. use the Indexed Fields category to search additional columns.

This is an offline/export-backed test route. **Load Live Save** and **Save Live Save** remain separate and disabled until the Game Pass process reader and field-level safety verification are complete.


## 7. Follow-up hardening

- Provenance timestamps are now created per import run, rather than once when the importer class is first loaded.
- Added an integration regression test proving that an explicit `Gender` column in an import flows into the existing global Include women Quick Search filter. The test checks the default OFF behaviour and ON behaviour.
