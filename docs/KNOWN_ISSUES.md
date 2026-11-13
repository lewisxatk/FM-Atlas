# Known Issues and Verification Gaps

## Critical — source recovery

- **Application source not yet in this repository.** R7.0.2 archive and R7.1.0 package/source need to be supplied and inventoried.
- **No reproducible build baseline.** The previous session reported WPF compilation had not been run for R7.1.0.
- **Live writes are unverified / reportedly unavailable in R7.1.0.** Do not describe the feature as operational until the adapter is inspected and tested safely.

## Search and analytics

Previous session source audit reported:
- numeric statistics discovered as flat leaf names;
- discovery capped at 500;
- Analytics screen applied one metric at a time;
- player attributes stored in a separate indexed table.

These findings need source/schema verification. The intended improvement is multi-condition search and analytics without breaking the existing indexed attribute path.

## UI / theme

- Previous work discussed both Aurora Frost and Obsidian.
- The latest screenshots describe making Obsidian consistent across the application.
- The current default theme and completion status need confirmation from the actual source/package.

## Import / database

- FM26/FM27 mappings and schema compatibility require repeatable validation.
- Parsing and import fixes are reported, but source-level regression tests are not yet available in this repository.

## Evidence discipline

Do not close an issue based only on a screenshot of a progress message. Require source inspection, build/test evidence and (where applicable) runtime verification.

## Recovery gaps discovered from the actual packages

- R7.1.0 contains more project history/specification than was captured in the earlier conversation-only handover.
- The full R7.1.0 Master Specification and Extended Research & Backlog must be retained in the GitHub source of truth.
- The two ZIPs are inventoried, but their complete source trees are not yet present in GitHub.
- R7.1.0's own build-status file says the preparation environment lacked .NET SDK/MSBuild; a successful Windows compile is not evidenced.
- Live FM memory writes are explicitly out of scope / not implemented in this package. Do not present local SQLite editing as live game editing.
