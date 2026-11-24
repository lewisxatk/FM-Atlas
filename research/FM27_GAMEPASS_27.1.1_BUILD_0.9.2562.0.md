# FM27 Game Pass 27.1.1 — package 0.9.2562.0

**Observed:** 24 November 2026  
**Storefront:** Xbox / Microsoft Store / PC Game Pass  
**User-provided module:** `game_plugin.dll` copied from the running FM27 installation  
**Claimed game update:** FM27 27.1.1  
**Atlas status:** fingerprint identified; no 27.1.1 scanner or live field mapping is enabled.

## 1. Direct binary evidence

The following values were read from the uploaded PE binary, not inferred from its filename.

| Property | Value |
|---|---:|
| PE architecture | x64 / AMD64 (`0x8664`) |
| PE timestamp | `0x6AFF583B` |
| SizeOfImage | `0x17624000` |
| File size | 391,758,808 bytes |
| FileVersion resource | `27.1.1` |
| ProductVersion resource | `27.1.1+2511875` |
| SHA-256 | `814f0190ea241710e0780bbff6589cc8137e6837c48cc25f868b1b676008242a` |
| Package version | `0.9.2562.0` (observed in the user's loaded WindowsApps module path) |

The package version is taken from the user's loaded path:
`SportsInteractive.FootballManager27_0.9.2562.0_x64__5w3tn6tb6stnm`.

## 2. Independent public research

- Sports Interactive's FM27 27.1.1 announcement is dated 24 November 2026. SteamDB's depot comparison records `game_plugin.dll` as modified in the update. This confirms a game-module change, but does not disclose the new memory layout.
- FMLibreEditor v0.5.4 was released 22 November 2026. Its current repository tree at commit `7ba3d229c6ce178c748e7f5560cdf9aa28a1f70e` (24 November 17:35 UTC) still contains Game Pass profiles only through `0.9.2524.0`; no `0.9.2562.0` profile was present when checked.
- FMRTE's published Windows compatibility list still lists Game Pass 27.1.0 build `2505117`; no 27.1.1 Game Pass profile is listed in the checked page.
- The open-source `fm-editor` repository documents a recorded Game Pass `0.9.2524.0` profile. Its unrecorded-build discovery work is useful research, but a discovered address is not automatically a verified field map.
- A separate public tool mentions save-file support for `27.1.1+2511875`. That is save-format evidence only; it is not evidence that live `game_plugin.dll` offsets are compatible.

Sources:
- https://steamcommunity.com/app/4530370/allnews/
- https://steamdb.info/patchnotes/26797085/
- https://github.com/tozmoz/FMLibreEditor/releases
- https://www.fmrte.com/download/fmrte27
- https://github.com/barak14/fm-editor/blob/main/docs/fm27/memory-layout.md
- https://github.com/d-euh/FM_FaceAutomator

## 3. Exact FM Atlas profile registration

FM Atlas records this exact fingerprint as:

- ID: `fm27-gamepass-0.9.2562.0`
- Evidence: `ExternalReferenceOnly`
- Read-only scanner: disabled
- Live reads: disabled
- Live writes: disabled

This allows the application to identify and explain the new package rather than reporting it as an entirely unknown DLL. It does **not** authorize the 27.1.0 reader to run.

## 4. Why the existing reader is not reused

The existing FM Atlas scanner is pinned to Game Pass package `0.9.2524.0`:

- PE timestamp `0x6AF5C8C6`
- SizeOfImage `0x179AD000`
- Module-relative player/staff/club array roots and the save-generation counter are specific to that profile.

The supplied 27.1.1 module has a different timestamp and image size. Reusing the earlier RVAs or object assumptions without verification could produce incorrect identities and fields. FM Atlas must reject the new package before opening a process-memory read handle.

## 5. Required 27.1.1 validation before enabling a reader

1. Use a disposable/offline career and capture a fresh module scan report.
2. Locate candidate player, staff and club arrays using a documented discovery procedure; do not assume the old RVAs.
3. Validate pointer bounds, object counts, unique IDs and stable identities.
4. Compare multiple known players, staff and clubs against the live game, including names, club links, CA/PA, visible attributes, positions and contract values.
5. Validate the dedicated human-manager record and its current-club relationship.
6. Verify the save-generation counter and ensure a save switch invalidates all old pointers and snapshots.
7. Keep all memory access read-only until the full field map passes repeatable checks.

Only after those checks should a separate 27.1.1 reader be enabled. Live writes require a further independent field-by-field write/read-back/undo validation and remain out of scope here.

## 6. Safety conclusion

The 27.1.1 binary identity is verified from the user's file. The live memory layout is not. This profile is therefore a detection and safe-rejection profile only.


## 7. Additional live-build discovery research

The public repository [Jackhamerp24/Football-Manager-27-Editor](https://github.com/Jackhamerp24/Football-Manager-27-Editor) provides a useful independent discovery workflow:

- Release v1.5.5, published 24 November 2026 at 13:08 UTC, includes `Fm27Editor.Tools.exe` for Windows x64. The release includes the completed Windows/Game Pass `newbuild` discovery path.
- Its `newbuild` command is designed for a running FM27 process with a loaded save. The Windows discovery path reads the module image, finds the in-module FM build string, indexes RTTI, resolves vtables and static roots, checks structural offsets, and emits a profile with `readOnly: true`.
- The README describes discovery as taking roughly 30 seconds. `verify-build` is a separate release gate; it must not be confused with discovery.
- The v1.5.5 published support table still lists Game Pass `27.1.0+2505117`, not 27.1.1. The release predates the 24 November 27.1.1 patch. Therefore the tool's discovery workflow is a promising way to generate candidate mappings, not evidence that the new build is already verified.
- The project is GPL-3.0. FM Atlas has not copied its source code; this is a research reference only.

Official release and source references:
- https://github.com/Jackhamerp24/Football-Manager-27-Editor/releases/tag/v1.5.5
- https://github.com/Jackhamerp24/Football-Manager-27-Editor/releases/download/v1.5.5/Fm27Editor.Tools.exe
- https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/main/CHANGELOG.md
- https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/main/src/Fm27Editor.Core/Offsets/NewBuildDiscoverer.cs
- https://github.com/Jackhamerp24/Football-Manager-27-Editor/blob/main/LICENSE

If this workflow is used to assist the FM Atlas mapping, download only the command-line tool from the v1.5.5 release and run **only** the read-only discovery command on a disposable/offline career:

```powershell
& "$env:USERPROFILE\Desktop\Fm27Editor.Tools.exe" newbuild --out "$env:USERPROFILE\Desktop\fm27-27.1.1-gamepass-profile.json"
```

The game must be running with the disposable save fully loaded. The command should emit a candidate profile and discovery report. Upload both the JSON profile and the console output for review. Do not run `verify-build --writes`, do not enable a release gate, and do not import any generated offsets into the FM Atlas scanner until the candidate profile has been independently checked against the game UI and a second known-good source.
