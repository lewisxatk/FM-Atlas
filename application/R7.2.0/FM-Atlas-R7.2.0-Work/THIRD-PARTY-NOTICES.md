# Third-party notices — FM Atlas 27

FM Atlas R5 invokes the independently installed `fmsave` Python package as an optional local save reader. The package is not vendored or redistributed in this source package; the setup script installs it from its public Python package distribution. `fmsave` is published under the MIT License. Retain the package's own copyright and licence notices with any redistribution of the package itself.

FM Atlas does not incorporate or redistribute code from the GPL-3.0 `barak14/fm-editor` project. That project was inspected as a research reference only. Its licensing terms and corresponding source obligations would need to be followed before any code from it is incorporated or distributed. See `docs/R7/RESEARCH-AND-LICENSING.md` for the build/fingerprint validation note.

FM Atlas remains a separate WPF application. Football Manager and related marks belong to their respective owners.

## Additional R7.2.0 research-only references

- `tozmoz/FMLibreEditor` is GPL-3.0-or-later. Its repository was inspected for FM27 Steam/Game Pass build profile evidence, guarded memory access practices, session-generation checks and UI workflows. No source code from it is included in FM Atlas.
- `ElectroHugin/FM-View-Exporter` is GPL-3.0. Its public CSV column catalogue and export behaviour were inspected to design Atlas's independent CSV importer. No plugin code is included in FM Atlas.
- `barak14/fm-editor` is GPL-3.0-or-later. Its public FM27 memory-layout notes were inspected as research, including their own warnings that some field offsets are inherited from FM26 and not independently verified on FM27. No source code is included in FM Atlas.
- `rhiever/fmsave` is MIT. FM Atlas invokes the locally installed package through its own export bridge; it does not bundle the package source in this ZIP. The package's own licence notice should be retained if distributing it separately.

Repository URLs, versions/build IDs inspected and the difference between verified, inherited and provisional field evidence are recorded in `docs/R7.2.0-RESEARCH-NOTES.md`.
