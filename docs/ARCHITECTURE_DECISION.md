# FM Atlas — Architecture Decision 001

**Date:** 2026-11-16  
**Status:** Decision for the fresh foundation; subject to a Windows build proof-of-concept.  
**Decision:** New .NET 8 WPF desktop solution with strict modular boundaries. Do not reuse the old WPF UI/code-behind.

## 1. Decision context

FM Atlas is a Windows desktop application for Football Manager 27, with Windows 10 and Xbox/Game Pass as the primary target. The user already has a Windows/.NET 8 development environment. The hardest integration surface is Windows process detection and version-specific process-memory access.

The new program must reproduce the approved screenshots and full feature scope while keeping data acquisition, search, persistence and live writes independently testable.

## 2. Framework choice

**Choose .NET 8 WPF for the desktop shell.**

Reasons:
- Direct fit for the Windows 10 desktop target.
- Native access to Windows process APIs through isolated P/Invoke adapters.
- Uses the known .NET 8 toolchain already used by the project and the user's environment.
- Supports fully custom templates/styles, vector icons, rounded controls, custom typography and the approved black/off-white/orange visual system.
- Avoids adding an Electron/browser runtime or a second application language solely for the UI.
- Allows a single signed/published Windows desktop product and straightforward local diagnostics.

Alternatives considered:
- **Tauri/React/Rust (FMLibreEditor):** technically strong and worth studying for memory-access safety, but adds a multi-language UI/backend build and packaging stack. Do not adopt solely to copy its architecture.
- **C++/Qt (FM Player Analyzer):** excellent separation between headless core and UI and strong native performance, but a separate C++/Qt toolchain is unnecessary for this Windows-only product. Use its testability and database design lessons, not its framework.
- **Avalonia/WinUI/Electron:** no demonstrated user requirement that outweighs the known WPF/.NET Windows integration path. Revisit only if the proof-of-concept identifies a concrete limitation.

The existing R7.2.0 project also targets `net8.0-windows` and WPF, but this decision does **not** authorise carrying over its old window, navigation, controls or code-behind.

## 3. New solution boundaries

Proposed solution:

```text
FMAtlas.sln
src/
  FMAtlas.Domain/          # typed entities, field definitions, provenance, invariants
  FMAtlas.Application/     # use cases, ports, search/query contracts, validation
  FMAtlas.Infrastructure/  # SQLite, import providers, graphics index, process adapters
  FMAtlas.Desktop/         # WPF views, view-models, styles, navigation and dialogs
tests/
  FMAtlas.Tests/           # unit/integration tests with fixtures; no game required
docs/
  ...
research/
  ...
```

Dependency direction:
- Domain has no dependency on WPF, SQLite or process-memory APIs.
- Application depends on Domain and defines interfaces/ports.
- Infrastructure implements Application ports and references Domain.
- Desktop composes Application and Infrastructure; UI controls never call process-memory APIs directly.
- Tests exercise Domain/Application and fake provider implementations without Football Manager running.

## 4. Data acquisition boundaries

Use distinct providers with explicit provenance:
1. **Live memory reader** — Windows read-only process adapter; exact Game Pass/Steam build profiles; no writes in the first proof-of-concept.
2. **Offline save provider** — a separate parser/bridge for supported career copies; report field-level coverage and unverified layouts.
3. **FM export provider** — import CSV/HTML from FM View Exporter or standard exports; preserve raw columns and duplicate headers.
4. **Graphics provider** — parse the user's own graphics `config.xml` and resolve player/club/nation images by stable IDs.
5. **Static catalogue provider** — game-version-specific field labels, attribute order and position/role definitions with explicit source/version.

Never merge these into a single source that hides where a value came from or when it was captured.

## 5. Persistence

- Local SQLite database via `Microsoft.Data.Sqlite`.
- Versioned migrations and transactional writes.
- Separate local Atlas records/settings from any live Football Manager mutation.
- Store game version, storefront, module fingerprint, save identity and capture timestamp with each snapshot.
- Preserve raw source values, source units, missing values and provenance.
- Use save-local UIDs and cross-career unique IDs as distinct fields.
- No FM career save, personal data or memory dump is committed to GitHub.

## 6. Search and player profile

- Quick Search and Advanced Search share an application-level query service.
- Advanced Search is a FMRTE-style dialog based on REF-002, not a new generic filter builder.
- Player profiles are full-workspace tabs based on REF-004.
- Attribute catalogue/order is versioned and tested; FM View Exporter table-column order is not assumed to equal player-profile attribute order.
- Player profile sub-tabs include Overview, Attributes, Contract, Positions, Preferred Moves/Traits, Season Stats, History, Personality/Hidden, Relations and all other verified FMRTE player sections.
- No fabricated sample players or analytics are shown in production states.

## 7. Live editing safety architecture

The writer is a separate, disabled-by-default application service and adapter. It is not part of the UI layer.

Before any write:
- exact process and storefront;
- exact supported module build/profile;
- current career identity and save-load generation;
- stable entity identity and re-resolution;
- field-level evidence and mapping status;
- expected current value/bytes;
- type and range validation;
- staged preview and explicit apply;
- read-back verification;
- reversible journal and guarded undo where proven.

Unknown build, changed generation, unresolved identity, unverified field or failed precondition means read-only. No write testing until read-only process detection and data checks pass on the user's machine. All write tests use a disposable offline career copy.

## 8. Licensing/reuse rule

- MIT projects (FMSuperScout, fmsave): selective code reuse may be considered after exact file-level review, compatibility testing and preservation of copyright/permission notices.
- GPL-3.0 projects (FMLibreEditor, fm-editor, FM Player Analyzer, FM View Exporter): research and independent implementation by default. Any direct code reuse requires an explicit decision to comply with the relevant copyleft distribution obligations.
- Commercial/closed binaries (FMRTE, TalentSeer, Genie Scout, FM Dossier): observable workflow and documentation references only; no code/assets copied or decompiled.

No third-party code is copied into the new solution by this decision.

## 9. Proof-of-concept and next implementation step

The first implementation milestone is a clean WPF shell and build pipeline, with:
- a compact left navigation without a Search item;
- top Quick Search and Advanced Search action;
- prominent Load Game action on the right;
- compact save actions;
- main workspace tabs;
- the black/off-white/orange Watched-inspired tokens and readable type hierarchy;
- no sample data: disconnected/empty states are honest.

Add Windows GitHub Actions build/test validation. The local container does not currently have the .NET SDK, so Windows CI and the user's Windows build will provide the actual compile result. Do not mark the shell complete until a Windows build and screenshot comparison pass.
