# FM27 Game Pass editing and reverse-engineering reference

**Scope:** FM27 Game Pass / Microsoft Store package `0.9.2435.0` (full release observed 10 November 2026).  
**Purpose:** Keep the FM Atlas rebuild tied to observed game data and prevent unsupported live writes.

## Evidence and build identity

The public FMLibreEditor reverse-engineering notes report this build identity for the mapped `game_plugin.dll`:

| Evidence | Observed value |
|---|---:|
| Package version | `0.9.2435.0` |
| Plugin image size | `0x17969000` |
| Link timestamp | `0x6AE8CF77` |
| Players array pair RVA | `0x53B7DE0` |
| Staff array pair RVA | `0x53B7E28` |
| Clubs array pair RVA | `0x53B7F98` |
| Current date RVA | `0x53597C8` |
| Retirement manager RVA | `0x53A9070` |
| Tactics manager RVA | `0x53A9140` |
| Training manager RVA | `0x53A5700` |

These are **Game Pass 0.9.2435.0 RVAs**, not universal FM27 addresses. The preceding Game Pass build 0.9.2409.0 placed the arrays around `0x2CC000` later and the retirement-manager static at a slightly different relative position. The notes explicitly record that the retirement-manager pointer had to be located by finding pointers to the correct object class rather than assuming a fixed distance from the players pair.

The same save was read before and after the 0.9.2435.0 update. The observed object layout did not move, but static locations did. The notes report 10,812 players, 9,359 staff and 19,775 clubs in the inspected save; 220 witness objects were checked. A few source records differed because the game itself changed or normalised data during load. This is evidence for this specific build and save, not a guarantee for later patches.

## Read-only validation required before any write

FMLibreEditor's implementation separates field definitions from health checks. It does not treat finding an address as proof that a field is safe to edit.

Its documented checks cover:

- in-game date consistency against contract dates and current injuries;
- player object plausibility (birth dates, height, abilities, reputation and related fields);
- position ratings;
- attributes;
- personality ratings;
- live values such as condition, match sharpness, jadedness and morale;
- contracts and their back-pointers;
- team membership and club ownership;
- staff records;
- clubs;
- cohesion and tactical familiarity.

The reference implementation samples up to 2,000 objects per relevant check and requires a minimum sample before a check can pass. A failed check keeps the fields it protects from being written. It validates that a target is still the expected object, that pointers lead to the expected class, that the existing value is plausible for the field, that the proposed value fits the field's type/range/choices, and then reads the value back.

**FM Atlas implementation rule:** do not enable a write because a scan succeeded or because a value appears in a UI. Each supported field needs an exact build profile, an object/field mapping, a passing field-specific health check, range validation, an identity re-check immediately before writing, and read-back verification. Structure-changing operations additionally need relationship checks and rollback/recovery behaviour.

## What the reference implementation distinguishes

The reference editor models writable values by entity (player, staff, club), storage kind (signed/unsigned integer, date, flag or pointer), display scale, allowed range, optional blank value, choices, and whether mass editing is allowed. Fields may live on the object itself or on linked records such as contracts, club finance, senior team, stadium, concerns or loan contract.

This matters for the FM Atlas editor:

- Player attributes, position ratings and personality ratings are separate field families and need separate validation.
- Contract wage and expiry are stored on a linked contract record, not assumed to be adjacent to the player object.
- Some one-at-a-time fields (for example squad number and squad status) should not automatically be exposed to mass edit.
- Pointer changes must only accept an address to a record of the same expected class.
- Flags must update only their intended bit and preserve the other bits in the byte.
- Dates must preserve the game's unrelated date flags.
- A field can be readable but still fail the plausibility check and therefore remain non-editable.

## Tactical data: known versus unknown

The FM27 tactics research describes the tactic block and role masks on a tested Steam build and reports a similar block layout on Game Pass 0.9.2409.0. It does **not** establish a complete, validated writable mapping for every tactical-effectiveness value. It also notes that some role masks and the meaning of some bits remain unknown.

Therefore FM Atlas must not invent tactical-effectiveness offsets or write them as if they were confirmed. Tactical roles, position ratings, attributes and any in-game tactical-effectiveness metric must remain distinct concepts. A UI can display a tactical field only when it is sourced from the loaded FM27 career or a verified mapping, and it can be editable only after independent field-level validation on the exact supported build.

## Product and UI consequences for the rebuild

1. Keep live career detection and indexing read-only by default.
2. Show the detected storefront, exact package version and plugin identity in diagnostics.
3. Display a per-field availability state: `Readable`, `Validated for write`, `Unsupported build`, `Failed validation` or `Unknown mapping`.
4. Do not present disabled fields as broken controls; explain the missing evidence and keep the rest of the profile usable.
5. Make edit drafts reviewable before applying; record old value, proposed value, entity identity, source mapping and validation result.
6. Apply writes in small transactions with immediate read-back. If a write or verification fails, stop the batch and report the exact field and record.
7. Never silently fall back to offsets from Steam or an earlier Game Pass package.

## Source material reviewed

- FMLibreEditor, `docs/recon/fm27-gamepass-0.9.2435.md` — Game Pass 0.9.2435.0 binary identity, static movement and before/after save comparison.
- FMLibreEditor, `profiles/fm27/gamepass-0.9.2435.0.toml` — exact build profile and array/date/manager RVAs.
- FMLibreEditor, `crates/fmlibre-core/src/health.rs` — read-only health checks and field gating.
- FMLibreEditor, `crates/fmlibre-core/src/edit.rs` — typed fields, ranges, object/linked-record locations, pointer checks and read-back model.
- FMLibreEditor, `docs/recon/fm27-tactics-and-value.md` — transfer-value and tactic-block research; includes explicit unresolved role/position details.

This document records research findings and FM Atlas acceptance criteria. It does not certify FM Atlas live editing or enable any write path.
