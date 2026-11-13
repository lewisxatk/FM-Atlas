# R4.3 — Unified Career Load & Scouting Workspace

- Replaced the crowded technical toolbar with a focused scouting toolbar and one primary **Load Game** action.
- Removed the extra confirmation prompt from the primary Load Game flow; the operation remains read-only and retains module fingerprint and witness safety checks.
- Renamed the live import action and progress text to match the user-facing Load Game workflow.
- Increased the empty-search live-player result cap from 5,000 to 100,000 so the normal player grid can browse the full imported population (subject to actual records and UI performance).
- Updated assembly, file and package version to 4.3.0.
- Preserved diagnostic/import code paths and existing SQLite database migrations.
- Documented exact coverage boundaries: the current live reader imports verified identities, DOB candidates and club links; detailed attribute coverage is not yet universal.
