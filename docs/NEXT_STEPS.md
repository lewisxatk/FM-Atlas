# Next Steps — FM Atlas

## Current task: recover and baseline the existing application

1. Obtain the original R7.0.2 archive and R7.1.0 package/source from the prior FM Atlas conversation or local PC.
2. Upload both files here (or provide a verified accessible repository/release link).
3. Do not extract over an existing working folder. Keep originals intact.
4. Inventory archive contents, sizes and SHA-256 hashes.
5. Compare R7.0.2 and R7.1.0 at file level; identify source files, project files, assets and generated outputs.
6. Import the authoritative source tree into this repository in a dedicated commit, preserving release archives separately if practical.
7. Inspect the .sln/.csproj files, target frameworks and dependencies.
8. Run restore/build using the correct .NET SDK and record exact output.
9. Create a baseline tag only after source provenance and build state are understood.

## Next feature after baseline

Resume the latest reported task: multi-condition player search / analytics and consistent Obsidian theming, but only after the source diff and data pipeline are understood.

## Required evidence for each change

- changed file list;
- reason for change;
- build command and result;
- relevant tests and result;
- known limitations;
- checkpoint commit SHA.

## Recovery import bundle prepared — 2026-11-13

A local import bundle has been prepared from both uploaded archives. It contains:
- the R7.1.0 package extracted intact under `application/R7.1.0/`;
- the R7.0.2 package extracted intact under `recovery/source-baselines/R7.0.2/`;
- byte-for-byte copies of both original ZIPs under `recovery/packages/`;
- a PowerShell script that clones this repository, creates a new recovery branch, copies only `application/` and `recovery/`, commits and pushes without changing `main`.

The user must run the import script on their Windows PC because the current GitHub connector can manage repository files/commits but cannot transfer a multi-file binary ZIP attachment directly from the chat runtime into a Git commit. After the branch is pushed, inspect it before merging.

The import bundle is delivered in the current conversation as `FM-Atlas-GitHub-Recovery-Import.zip`.
