# FM Atlas — Reference-Led Development Gates

**Revision:** 001  
**Date:** 2026-11-16

This process is mandatory for the new FM Atlas rebuild.

## Gate A — Reference intake

- [ ] Assign a permanent REF ID to each screenshot/source.
- [ ] Preserve the original file and filename.
- [ ] Identify which screen/component it governs.
- [ ] Record exact elements to reproduce.
- [ ] Record what may be adapted and what must remain faithful.
- [ ] Link external source URLs.
- [ ] Resolve conflicting references before coding.

## Gate B — Screen specification

- [ ] Screen layout and hierarchy documented.
- [ ] Sidebar/header/content arrangement documented.
- [ ] Typography, colour, spacing, radii and borders documented.
- [ ] All fields listed with data source and version applicability.
- [ ] All controls have explicit behaviour.
- [ ] Loading, empty, error, selected, disabled and unsaved states documented.
- [ ] Keyboard and accessibility behaviour documented.
- [ ] Acceptance criteria written.

## Gate C — Technical evidence

Every technical claim must be classified:
- **Verified:** reproduced against the user's target game/build or a controlled fixture.
- **Documented:** supported by source code or authoritative documentation, not yet reproduced on the target build.
- **Inferred:** plausible interpretation; must not be presented as fact.
- **Unknown:** evidence is insufficient.
- **Unsupported:** known not to be implemented or not available.

For every field mapping or live-memory operation, record source, evidence, target version/storefront, type/range, validation and test result.

## Gate D — Implementation

- [ ] Implement against the approved screen specification.
- [ ] Do not invent substitute layouts.
- [ ] Do not replace a specified FMRTE-style workflow with a generic alternative.
- [ ] No fake values or decorative controls presented as working functionality.
- [ ] No direct UI-to-memory writes; all writes go through validated application services.
- [ ] Preserve prior working source and use a dedicated branch.

## Gate E — Visual verification

- [ ] Capture the actual Windows desktop screen at a known resolution/scaling.
- [ ] Compare against the controlling reference.
- [ ] Check panel proportions, spacing, alignment, font hierarchy, button sizing and contrast.
- [ ] Record every material deviation.
- [ ] Obtain user approval for material design changes.

## Gate F — Functional verification

- [ ] Test controls with real loaded data.
- [ ] Verify quick search results against the save.
- [ ] Verify each advanced-search category and numeric range.
- [ ] Verify AND/OR behaviour.
- [ ] Verify CA/PA and attribute reads.
- [ ] Verify writes by read-back and in-game confirmation where supported.
- [ ] Verify save/reload persistence.
- [ ] Verify undo/rollback.
- [ ] Verify Game Pass compatibility separately from Steam.

## Release rule

A feature can only be marked complete when its visual specification, technical evidence and functional acceptance tests all pass. A compiled application, attractive screenshot, visible field or clickable button alone is not proof of completion.
