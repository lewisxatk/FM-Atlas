# FM Atlas — Record Snapshot Persistence 001

**Status:** Implemented on schema v2; Windows CI verification pending.

## What it stores

The local SQLite database now has a version 2 migration that adds entity_record_snapshots. The table stores a typed JSON snapshot keyed by:
- entity kind;
- provider key;
- provider source ID;
- CLR record type.

Each snapshot also records its snapshot schema version and saved timestamp. The corresponding entities row is created/updated in the same SQLite transaction.

## Why keep a JSON snapshot layer?

It preserves the typed source record, including per-field SourcedValue provenance, when the exact field projection is still being developed. It is not a substitute for the normalised field_values table or a searchable index. The future importer will save a lossless snapshot and create/update a queryable projection in one import transaction.

## Safety and integrity

- Provider-scoped IDs do not overwrite each other.
- Player, staff and club records use the same generic snapshot store.
- Source/evidence/build metadata remain attached to values.
- Unknown/absent fields remain absent.
- Deleting a typed snapshot does not delete the entity.
- The store requires the Atlas database to have been initialised first.
- This is local data persistence only. It never writes to the live FM process or career save.

## Tests added

- Player snapshot round-trip, including nested attribute values and provenance.
- Provider-scoped ID isolation.
- Typed StaffRecord and ClubRecord snapshots.
- Snapshot deletion.
- Invalid identity rejection.

Windows CI is required before this code is marked build/test verified.


## Identity contract refinement

PlayerRecord, StaffRecord and ClubRecord now implement IEntityRecord. SaveSnapshotAsync accepts the typed record and reads its EntityIdentity directly rather than accepting a second independent identity parameter. This removes a caller-side mismatch risk. Query/load/delete still require the requested provider-scoped identity and record type.

The store preserves a non-null display name when a later partial snapshot does not provide one. The entity and snapshot writes are transactional. No normalised searchable field projection is produced yet.


## Searchable field projection

The snapshot store now projects supported scalar fields to field_values inside the same transaction as the typed JSON record snapshot. This ensures the stored snapshot and searchable projection cannot be committed separately.

Snapshot replacement deletes the previous field projection before inserting the fields from the new typed record. Snapshot deletion removes the field projection when the entity has no remaining snapshots, but preserves the entity row.

A new read-only SqliteEntitySearchProvider queries indexed entity display names. It supports Player/Staff/Club filtering, literal substring matching with escaped LIKE metacharacters and result limits. The WPF Quick Search bar is not connected yet. Advanced Search execution remains a separate development task.
