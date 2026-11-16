# FM Atlas — Graphics Locator Reverse Engineering 001

**Date:** 2026-11-16  
**Target:** Football Manager user graphics folders  
**Reference source:** User-supplied FM Player Analyzer 2.2.3 source ZIP  
**License:** GPL-3.0; source is research-only for FM Atlas unless GPL-3.0 compliance is deliberately adopted.  
**FM Atlas implementation:** Independent .NET implementation; no FM Player Analyzer source code copied.

## Evidence observed in the supplied source archive

The archive contains desktop/src/core/GraphicsIndex.h and GraphicsIndex.cpp, desktop/src/app/GraphicsLibrary.h and GraphicsLibrary.cpp, and desktop/src/tests/test_graphicsindex.cpp.

The documented config mapping convention includes:
- Person portrait: graphics/pictures/person/<id>/portrait.
- Club logo: graphics/pictures/club/<id>/logo.
- Nation logo: graphics/pictures/nation/<id>/logo.

A record maps a relative from file stem/path to a to destination. The config file is located within the graphics pack, and the referenced image is resolved relative to that config folder.

The source explicitly recognises player UIDs in numeric form and r-<numeric-id> form. It indexes config files recursively beneath a selected graphics root. The tested image extensions include PNG and JPG; FM Atlas additionally checks JPEG, WebP, BMP, TIF and TIFF when no extension is included in the mapping.

The reference implementation:
- distinguishes portrait, club logo and nation logo mappings;
- ignores unrelated targets such as person icons;
- handles duplicate mappings deterministically;
- caches the index separately from the user's graphics folders and validates config files before using the cache;
- provides optional enable/disable behaviour;
- performs indexing away from the UI thread.

These are observed design behaviours from the supplied 2.2.3 source and tests, not claims about every graphics pack or every FM build.

## FM Atlas independent implementation

Added:
- src/FMAtlas.Domain/Graphics/GraphicsAssetKind.cs
- src/FMAtlas.Domain/Graphics/GraphicsIndexState.cs
- src/FMAtlas.Domain/Graphics/GraphicsAssetReference.cs
- src/FMAtlas.Domain/Graphics/GraphicsIndexSummary.cs
- src/FMAtlas.Application/Abstractions/IGraphicsLocator.cs
- src/FMAtlas.Infrastructure/Graphics/FootballManagerGraphicsLocator.cs
- tests/FMAtlas.Tests/GraphicsLocatorTests.cs

The FM Atlas indexer:
- recursively scans config.xml files under the selected root;
- parses XML with DTD processing prohibited and no external XML resolver;
- recognises only person portrait, club logo and nation logo targets;
- resolves relative image paths from the config folder;
- rejects absolute paths, wildcard mappings and paths escaping the selected graphics root;
- supports numeric and r- player UIDs;
- uses deterministic sorted-config precedence for duplicate IDs;
- is read-only and does not move, rename or modify any graphics;
- keeps the current index in memory and performs the scan on a worker thread.

## Deliberate differences and limitations

- Persistent on-disk caching is not implemented in this first pass. It will be added only after correctness and cache invalidation tests are designed.
- Image decoding, thumbnail generation, and WPF profile binding are not implemented yet.
- FaceDrop/Newgen mappings and any non-standard graphics config schemes need separate research.
- This is a config indexer, not proof that the indexed file is a valid image. The UI/image loader must handle corrupt or unsupported images and display a neutral placeholder.
- The current indexer has not yet been tested against the user's actual graphics folders.
- No player records have yet been loaded from the user's FM27 career.

## Test plan

Synthetic tests cover:
1. portrait, club logo and nation logo mapping;
2. r- Newgen UID lookup;
3. rejection of icon and unrelated graphics targets;
4. deterministic duplicate mapping precedence;
5. rejection of path traversal outside the graphics root;
6. missing folder state;
7. clear/off state.

Windows CI must pass before marking this component complete. The user's real graphics folder must be tested later on Windows.
