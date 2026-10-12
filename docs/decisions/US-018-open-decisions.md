---
artifact_type: open_decisions
story: US-018
version: 2
status: DRAFT
created_at: 2026-10-12T06:04:04Z
updated_at: 2026-10-12T06:30:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-018-incremental-sync.md
    version: null
  - path: trebovaniya.md
    version: 90
supersedes: 1
---

# US-018 Open Decisions — Incremental synchronization

**Three, all resolved.** OD-001 and OD-002 by the Owner on 2026-10-12, before
activation; OD-003 raised by TEST_WRITING and resolved by the Owner the same day. Writing
the Specification raised none; `trebovaniya.md` §7 has no open item on
incremental synchronization. Choices the wording leaves open are
interpretations I-1 … I-6 in `docs/specifications/US-018-spec.md` §11.

## OD-001 When the weekly full run happens

Options: (a) a run is full when no full run has succeeded yet, or the last
successful full run is at least 7 days old — no weekday setting, survives
downtime; (b) the first run after midnight (school time zone,
`Installation:TimeZone`) on a fixed weekday; (c) as (b) with the weekday set by
the Admin.

**Resolved**: (a).
Impact: FR-005, VR-002, I-5, I-6.

## OD-002 A full run that fails

Options: (a) the full-run time is stored only when the whole run succeeds, so
the next run is again full; (b) the full-run time is stored at the start of a
full run, so a failed one is not repeated until next week.

**Resolved**: (a).
Impact: FR-006, I-3.

## OD-003 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests reference declarations that do not exist
yet: `IncrementalSyncRule` (entity model §1), `Course.CompleteReadUpdateTime`
and `Course.RecordCompleteRead()` (§2), `SyncState.LastFullRunAt` and the
`fullRun` parameter of `CompleteRun` (§3),
`ICourseRepository.GetCompleteReadUpdateTimeAsync` (§4), and the skipped-as-
unchanged count the outcome carries for the host (§5 step 4), named
`SynchronizationRunOutcome.CoursesSkippedUnchanged`. The test-writer may not
change production code; US-017 … US-045 resolved the same gap the same way.

Options: (a) a compile-only skeleton — declarations only; new members throw
`NotImplementedException` (new properties get-only and throwing, so EF does not
map them before the migration); `CompleteRun` gains the parameter and ignores
it; no EF mapping, no migration, no existing behaviour changed; IMPLEMENTATION
owns and completes it; (b) no skeleton — tests that do not compile until
IMPLEMENTATION.

**Resolved**: (a) — Owner, 2026-10-12.
Impact: test generation report §2.
