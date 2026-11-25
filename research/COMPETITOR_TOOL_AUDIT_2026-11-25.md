# FM Atlas competitive and reverse-engineering research — 25 November 2026

## Scope and method

This audit compares publicly documented compatibility, release notes, screenshots and open-source build/layout documentation for current Football Manager 27 tools. It does not decompile proprietary FMRTE, Genie Scout, FMST or TalentSeer binaries, and it does not copy their code, assets or private memory maps. Open-source source code is used as a technical cross-check only; licence terms are recorded before any proposed code port.

The primary target is the user's exact Microsoft Store / Game Pass module:
- Package: `0.9.2562.0`
- Product version: `27.1.1+2511875`
- PE timestamp: `0x6AFF583B`
- SizeOfImage: `0x17624000`
- SHA-256: `814f0190ea241710e0780bbff6589cc8137e6837c48cc25f868b1b676008242a`

## Compatibility matrix

| Tool | Publicly documented build support | Useful findings | FM Atlas use |
|---|---|---|---|
| FMRTE 27 | Windows build 15 supports FM 27.1.1 build 2511875, including Microsoft Store | Feature reference for player/staff/club grids, CA/PA difference, contract editing, manager tools and editor workflow | Public feature benchmark only; proprietary code and memory layouts are not copied |
| Genie Scout 27 | 1.0 Beta 8 / Build 2710 dated 24 Nov 2026 supports 27.1.1 on Steam, Epic and Xbox / PC Game Pass | On Loan filter, FM squad statuses, Morale, staff shortlists, localized role names; useful search and shortlist benchmark | Feature checklist; verify which underlying fields FM Atlas can actually import and query |
| FMLibreEditor | Current repository lists Game Pass 0.9.2562.0 | Exact 27.1.1 static roots and staff/player/club field layouts; profile notes 12 checks passed on a separate loaded save | Source for an explicitly attributed, build-specific read-only mapping, pending Atlas validation on Lewis's save |
| FMSuperScout | v1.7.4 (20 Nov 2026) supports FM 27.1 | Open-source external scanner; says 27.1 database memory layout did not change from 27.0 and validates names, ages, CA/PA, per-position attributes, contracts, values, loans and agreed transfers against a running 27.1 save | Independent field-layout cross-check and scan/diagnostics design reference; not proof of 27.1.1 package-specific support |
| FM Editor (Jackhamerp24) | v1.5.6 published 25 Nov; published table still lists 27.1.0 | Separates unknown-build discovery (`newbuild`) from later build verification; useful for a fail-closed discovery pipeline | Research methodology only; do not import its 27.1.0 RVAs as 27.1.1 offsets |
| barak14/fm-editor | Documented Game Pass package 0.9.2524.0 | Independent predecessor layout reference for 27.1.0 | Cross-check legacy profile only; not a 27.1.1 profile |
| FM-Player-Analyzer | FM27 save-file / FM View Exporter import, read-only | Best XI optimisation, positional weak spots, youth/signing analysis and registration tools | Future offline analysis ideas; save parsing is separate from live memory reader |
| FM Scouting Tool 27 (FMST27) | Public FM Scout page describes v3.0.3 | Read-only live scouting from memory; player stats, CA/PA, contracts, club data, shortlist/search workflows | Feature benchmark only; redistribution is restricted to the publisher's own download page |
| FM Dossier | Public listing v1.16.0 says FM27 works, with documented feature limits | Tactical suitability, squad/registration analysis, Moneyball/stat analysis, transfer/loan interest. It explicitly keeps unavailable values as Unknown rather than inventing a figure | Use its source/uncertainty labelling and feature-gap discipline as a benchmark; respect its own data-source limitations |
| Talent Seer | Current public listing is 27.1.4 (22 Nov 2026), but states FM27 for PC tested on Steam; it does not explicitly confirm Game Pass in the listing | Role ratings for 79 tactical roles, CA/PA, hidden/personality data, interest estimates, potential projections, comparison, staff search, search presets and edit history/undo. User's referenced 27.1.2 archive is older than 27.1.4 | Use as a feature benchmark only. Do not assume Game Pass support or copy its proprietary implementation. Prefer a 27.1.4 research build over the older archive if a comparison is required |

## Exact 27.1.1 Game Pass layout evidence

FMLibreEditor's current profile `profiles/fm27/gamepass-0.9.2562.0.toml` identifies Game Pass 0.9.2562.0. The profile comment says it was generated on a loaded save and all 12 profile checks passed. It records that the static roots moved by `0x1FE0` relative to 0.9.2524.0, while the entity-object layout remained the same.

Build-specific module-relative roots:

| Structure | RVA |
|---|---:|
| Player array | `0x53F8EA8` |
| Staff array | `0x53F8EF0` |
| Club array | `0x53F9060` |
| GAME_UI static | `0x53F9A60` |
| Current in-game date | `0x539BD10` |
| GAME_UI saves-loaded field | `0x100` |

FM Atlas stores these roots only on the exact matching profile. It does not apply 27.0 / 27.1.0 static addresses to 27.1.1.

### Manager offset semantics — do not mix these values

Two public tools describe different layers of the manager's memory structure:

- FMLibreEditor `layout.toml` describes the staff object person block as `0x100`, with `person_block_shifts = [0x0, 0x340]`. That is the in-object offset used when locating staff identity fields inside an FM27 staff object.
- FMSuperScout `Fields.cs` records `HUMAN_MANAGER_OFFSET = 0x440` in its class-offset discovery model. This is not the same base/meaning as the FMLibre staff object's person-block shift.

FM Atlas's staff parser probes the in-object person blocks at `0x340` and `0x0`, which agrees with the FMLibre layout description. Do **not** change it to `0x440` without evidence that the reader is using the same base and class model. This distinction matters for the previously missing My Career manager record.

## Feature adoption backlog

The following are product behaviours worth implementing or benchmarking in FM Atlas. They are requirements, not claims that these features already exist.

### Search and data grid
- Entity scope: All / Players / Staff / Clubs.
- Fast search and advanced, typed filters with explicit ALL/ANY logic.
- On-loan state and squad status as searchable fields when present and verified.
- Morale, contract, nationality, position and club fields with source provenance.
- Saved search profiles, visible-column sets, sorting, paging, CSV export and comparison.
- Do not expose a field as searchable just because it is in the UI catalogue; require a real query/storage mapping.

### Player / staff / club records
- Full-width closable record tabs with retained selection and filter state.
- Player CA/PA, current attributes, hidden/personality fields and position familiarity.
- Staff role groups, job preferences, attributes and club links.
- Club details, squads, coaching/scouting/medical staff and competition relationships.
- Human manager profile must be resolved separately from a generic staff directory entry and validated against the game's UI.

### Read-only live data architecture
- Exact module/build profile with per-build static roots.
- Fail closed on unknown package or incomplete layout.
- Guard every pointer and memory range, retain cancellation and save-generation checks.
- Validate record counts, unique IDs, names, club relationships and values against the live game's screens.
- Persist a last-good snapshot; never replace it with partial data.
- Keep provenance as Mapped until FM Atlas independently verifies each field on the user's save.
- Use bounded scans and a diagnostic report with stage, duration, counts, read failures and fingerprint; exclude raw save contents from diagnostics.
- Keep process memory strictly read-only. Live edits remain blocked until separate write/read-back/undo tests exist.

### Offline intelligence
- Consider a separate save-file ingestion path for historical seasons, match statistics and long-term player tracking.
- Keep calculated role ratings, potential projections, estimated market value and derived scores distinct from raw Football Manager values.
- Consider optimal Best XI, squad registration, position-coverage and development tracking after core data coverage is dependable.

## Current FM Atlas implementation status

- The 27.1.1 Game Pass 0.9.2562.0 build-specific read-only profile was added to the fresh-foundation development branch via PR #12. The supplied HUD restyle 002 has now been ported on `rebuild/fm27-27.1.1-ui-hud-port`; the 27.1.1 club-finance corporate-facilities offset is now profile-specific (`0x8AD` for 0.9.2524.0 and 0.9.2562.0, older `0x8A9` retained as the default).
- The HUD/reader integration branch Windows run #1100 passed 126/126 automated tests, Release build with zero warnings/errors, self-contained packaging and WPF startup smoke check.
- No live 27.1.1 career has yet been read by FM Atlas in the user's environment. Field values remain Mapped / ExternalReferenceOnly until checked against a disposable career.
- Save Live Changes remains disabled.
- The supplied HUD restyle 002 is partially ported into the fresh modular WPF application. The full Watched Instrument shell, workspace tabs and full-width player/staff/club record model are still not fully ported; the older implementation remains on WIP PR #8 against the R7.2.0/V8 architecture. Track the broader migration in [issue #13](https://github.com/lewisxatk/FM-Atlas/issues/13).

## Sources

- FMRTE 27 Windows compatibility and changelog: https://www.fmrte.com/download/fmrte27
- Genie Scout 27 updates: https://geniescout.com/updates/
- FMLibreEditor README: https://github.com/tozmoz/FMLibreEditor
- FMLibreEditor 0.9.2562.0 profile: https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/gamepass-0.9.2562.0.toml
- FMLibreEditor shared field layout: https://github.com/tozmoz/FMLibreEditor/blob/main/profiles/fm27/layout.toml
- FMLibreEditor license: https://github.com/tozmoz/FMLibreEditor/blob/main/LICENSE (GPL-3.0-or-later)
- FMSuperScout README: https://github.com/mavarobli/FMSuperScout
- FMSuperScout 1.7.4 release notes: https://github.com/mavarobli/FMSuperScout/blob/main/docs/release-notes-v1.7.4.md
- FMSuperScout field definitions: https://github.com/mavarobli/FMSuperScout/blob/main/core/Fields.cs (MIT)
- Jackhamerp FM27 Editor releases: https://github.com/Jackhamerp24/Football-Manager-27-Editor/releases
- barak14 FM Editor layout: https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md
- FM-Player-Analyzer: https://github.com/ElectroHugin/FM-Player-Analyzer (GPL-3.0)
- Talent Seer public 27.1.4 release and feature notes: https://sortitoutsi.net/content/78140/talent-seer-is-here-a-free-scouting-and-editing-tool-for-football-manager-27
- Talent Seer 27.1.2 author devlog: https://emreyavuz84.itch.io/talent-seer/devlog/1759768/talent-seer-2712-who-would-talk-to-your-club
- FM Dossier public FM27 compatibility discussion: https://sortitoutsi.net/content/77041/fm-dossier
- FMST27 public feature and redistribution page: https://www.fmscout.com/a-fmst-27.html


### 25 November continuation — HUD port and field-layout correction

The user's direct `FM-Atlas-restyled.zip` attachment was inspected. Its 17 November source snapshot is not safe to replace wholesale because it predates the current 27.1.1 Game Pass reader. The HUD resources, main-window backdrop/status indicator, square panels and Advanced Search pitch/group selector were ported selectively to the current modular branch.

The FMLibreEditor 0.9.2562.0 profile and shared layout also document the club-finance corporate-facilities field at `0x8AD`. FM Atlas previously used `0x8A9` unconditionally. The profile now carries this per-build offset and the reader uses the matched value. Team-tactics offsets `0x1018` / `0x1030` are recorded for future tactics support but are not read by the current Atlas reader.

Windows validation for commit `df670351809a47bc34a266ed3ae0287a3e615c6a`: run #1100 passed 126/126 tests, zero warnings/errors, self-contained Windows x64 package and WPF startup smoke test. A user's live 27.1.1 career has not yet been read successfully by this build; the first real-game verification is still outstanding.


### 25 November 13:00 — import pipeline root-cause correction

A follow-up source trace found that the live connector generated package-scoped provider keys, but the streaming validator was pinned to the old 0.9.2524.0 key. This would reject valid 0.9.2562.0 records even after memory reading. The validator now requires an explicit expected provider key, and the connector passes the matched session key. A 27.1.1 regression test was added. Windows run #1129 passed 127/127 tests, zero warnings/errors, packaging and WPF startup smoke. Actual user-career validation remains outstanding.


### Current candidate package — run 1145

The current branch head `476659c814d45af87355d0ae1f443d6a2ec52e13` passed Windows run #1145: 127/127 tests, zero warnings/errors, self-contained x64 package integrity and WPF startup smoke. It includes the 27.1.1 package-scoped provider-key validation fix and the supplied HUD restyle plus compact navigation. The user must still verify a live 27.1.1 career; automated startup is not proof of memory-read compatibility.
