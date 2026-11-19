# FM Atlas — Session Continuity and Work Preservation

**Revision:** 003  
**Date:** 2026-11-16  
**Working branch:** `rebuild/fresh-foundation-001`

## Purpose

This project must survive streaming interruptions, conversation length limits, app crashes and handovers to a new chat. GitHub is the durable project record; a conversation is not the only place where decisions or progress may exist.

## Source of truth

1. Product scope: `FRESH_REBUILD_MASTER_PLAN.md`.
2. Visual references: `REFERENCE_LIBRARY.md`.
3. Detailed UI requirements: `DESIGN_SPECIFICATION.md`.
4. Architecture decision: `ARCHITECTURE_DECISION.md`.
5. Uploaded package inventory: `../research/UPLOADED_RESEARCH_INVENTORY.md`.
6. Technical findings: `../research/RESEARCH_FINDINGS_001.md` and `../research/RESEARCH_REGISTER.md`.
7. FMRTE parity inventory: `../research/FMRTE_FEATURE_AUDIT_001.md`.
8. Field counts and confidence: `../research/FIELD_COVERAGE_LEDGER.md`.
9. Current work state: `CURRENT_CHECKPOINT.md`.
10. Task-by-task progress: `FRESH_REBUILD_CHECKLIST.md`.
11. Change history: Git commits and pull requests.

If chat history conflicts with the latest committed specification/checkpoint, stop and reconcile explicitly. Do not guess.

## Mandatory save cadence

- Before substantial work: read the latest checkpoint, checklist and relevant spec.
- After each meaningful research finding: update the research register/evidence note.
- After each completed task or test: update the checklist and checkpoint.
- After each coherent code change: commit to the active feature branch.
- Before changing architecture, field mappings or live-write behaviour: write the decision and evidence first.
- Before ending a work session: update checkpoint with exact completed work, open work, branch and commit SHA.
- Never leave several hours of completed work only in chat text or an uncommitted working tree.

## Atomic work units

Break work into small resumable units:
1. Define one task and acceptance criteria.
2. Inspect exact source files.
3. Make one bounded change.
4. Build/test if the environment supports it.
5. Record command, output and result.
6. Commit the change.
7. Update checklist/checkpoint.
8. Move to the next unit.

Do not begin a large multi-feature edit that cannot be independently saved or verified.

## Interruption recovery procedure

When a stream is interrupted or a new chat starts:
1. Read this document.
2. Read `CURRENT_CHECKPOINT.md`.
3. Read `FRESH_REBUILD_CHECKLIST.md`.
4. Check active branch and latest commit.
5. Inspect uncommitted/untracked changes if working in a local repository.
6. Re-run only the last incomplete task or verification.
7. Do not repeat already committed work.
8. Do not claim work continued while no tool execution occurred.
9. Update checkpoint before continuing substantial work.

## Required checkpoint fields

Every checkpoint must state:
- Date/time (UTC).
- Active branch.
- Latest commit SHA.
- Current milestone and task ID.
- Completed tasks with evidence/commit/test result.
- In-progress task and exact files affected.
- Blockers and user actions needed.
- Tests run and their actual outcomes.
- Next single action.
- Whether any changes remain uncommitted.
- Whether the user must provide files or run commands on Windows.

## Git and backup policy

- Never commit directly to `main` for rebuild work.
- Use small feature branches and commits.
- Preserve original archives and source folders unchanged.
- Never force-push or delete historical recovery branches without explicit approval.
- Tag stable checkpoints.
- Keep a second offline copy of original archives when possible.
- Do not commit career saves, credentials, API keys, personal paths, proprietary licensed assets or generated build output.
- Keep binary research assets in a clearly indexed location only when licensing and repository policy allow it.
- A GitHub commit is a versioned copy, not a substitute for offline backups of original archives.

## Evidence discipline

For every technical result record:
- source/repository/file and exact revision;
- method inspected or test performed;
- target FM version and storefront;
- observed result;
- confidence/status;
- known limitations;
- licence implications.

A source-code claim is not a runtime test. A compile is not an in-game test. A visible UI field is not proof of correct mapping. A successful write call is not proof of persistence.

## User update format

At each substantial checkpoint, report:
- what was actually completed;
- where it was saved (branch and commit);
- what was tested and what was not;
- the next task;
- whether the user needs to do anything.

Never say "still working" unless work is actually being executed in the current session.
