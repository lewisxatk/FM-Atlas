# FM Atlas Architecture (Provisional)

This document is a working map, not a verified source-code architecture. Update it after importing and inspecting the actual solution.

## Known / reported technology

- Windows desktop application.
- WPF UI is reported by the previous package audit.
- C# codebase.
- .NET 8 SDK was previously reported installed on the user's Windows PC.
- FM26/FM27 import, database, search, analytics and editing integrations have been described in prior sessions.

## Proposed logical boundaries to verify

1. **Presentation layer** — WPF windows, views, view models, navigation, theme resources.
2. **Application services** — search orchestration, player profile assembly, analytics, editing workflows.
3. **Domain model** — player, club, staff, nation, competition, attributes, positions, role-fit and editing concepts.
4. **Data/import layer** — database readers, schema/version adapters, indexed storage, mapping and validation.
5. **Game integration layer** — process detection, version identification, memory inspection and guarded adapter operations.
6. **Infrastructure** — logging, diagnostics, settings, persistence, undo history, caching and export.

## Boundaries and safety

- Offline/local database editing must be distinguishable from live process memory editing.
- UI controls must not directly perform unvalidated memory writes.
- Import mapping must be version-aware and preserve provenance.
- Analytics/role-fit calculations must document the inputs and avoid presenting estimates as official game values.
- Diagnostics exports must redact sensitive paths, identifiers and user data where appropriate.

## To replace this provisional map

After source import, document:
- solution and project names;
- target frameworks;
- entry point and startup sequence;
- dependency graph;
- database formats and access layer;
- importer pipeline;
- search/indexing pipeline;
- live adapter boundaries;
- settings/logging/diagnostics;
- build and test commands.
