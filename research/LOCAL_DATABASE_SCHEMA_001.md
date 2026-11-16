# FM Atlas — Local Database Schema 001

**Status:** Initial SQLite migration implemented; repository persistence is not yet implemented.

## Purpose

Create a local store for imported player, staff and club records without conflating values from different sources or treating imported/read values as permission to write to the live game.

## Initial tables

- atlas_schema_migrations — schema version history.
- data_sources — source kind, source version, target build fingerprint, save generation and import timestamp.
- entities — provider-scoped identity for players, staff, clubs, nations and competitions.
- field_values — normalized typed field values with source/evidence/build provenance.
- entity_relationships — typed source-to-target relationships with evidence.
- graphics_mappings — indexed portrait, club logo and nation logo paths.

## Important data integrity rules

- Identity is the tuple (entity_kind, provider_key, source_id). IDs from separate providers are not assumed to be interchangeable.
- Each field stores its own source and evidence status.
- Raw source values and derived Atlas values must not be silently merged.
- Missing values remain absent; no placeholder values are stored as real game data.
- A documented or read-verified value is not write permission.
- Foreign keys preserve entity relationships and prevent orphaned values.
- Graphics paths are an index of user-owned assets; Atlas does not alter the files.

## Migration

Schema version 1 is applied transactionally and recorded both in SQLite PRAGMA user_version and atlas_schema_migrations.

The initial schema is infrastructure only. Player/staff/club repositories, transaction-level imports, typed serialization and indexed queries remain separate tasks.
