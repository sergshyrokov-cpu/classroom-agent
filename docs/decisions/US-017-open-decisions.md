---
artifact_type: open_decisions
story: US-017
version: 2
status: DRAFT
created_at: 2026-10-03T14:51:28Z
updated_at: 2026-10-03T14:51:28Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-017-retry-backoff-permission-errors.md
    version: null
  - path: trebovaniya.md
    version: 80
supersedes: null
---

# US-017 Open Decisions — Retry, backoff and permission-error handling

Nine decisions (OD-001 … OD-009) were written into the Story by the author and
**resolved by the Owner on 2026-10-03, before activation**, each as the
recommended option (a). They are carried here with their ids and resolutions
unchanged. OD-001 and OD-005 were also written into `trebovaniya.md` v80.

Writing the Specification raised no new Open Decision; its choices are
interpretations I-1 … I-8 in `docs/specifications/US-017-spec.md` §11.

---

## Resolved before activation


### OD-001 How many retries for a transient failure

Options: (a) up to 4 attempts, pauses ~2 s / 8 s / 30 s + jitter, `Retry-After`
honoured; (b) more attempts, pauses up to minutes; (c) configurable by the
Owner.

**Resolution:** (a). v80 adds the 2-minute cap on a requested delay.

### OD-002 What happens when the retries run out

Options: (a) the run stops as failed, committed courses stay, the next run
continues; (b) skip the course and go on.

**Resolution:** (a) — continuing to call Google under `429` burns the quota
every school shares.

### OD-003 A permission failure

Options: (a) stop the run at once, no retries; (b) skip the course and go on.

**Resolution:** (a) — it affects the whole school, not one course.

### OD-004 The schedule after a permission failure

Options: (a) the normal interval, so the run heals by itself; (b) wait for a
successful "Check access".

**Resolution:** (a).

### OD-005 Where the Admin sees the diagnosis

Options: (a) a "Last synchronization" block on the connection page; (b) defer
display to US-024.

**Resolution:** (a). Written into `trebovaniya.md` v80 (Epic 6, §2 matrix).

### OD-006 The wording of the diagnosis

Options: (a) reuse the classification and texts of "Check access" (US-011);
(b) separate texts for synchronization.

**Resolution:** (a).

### OD-007 Network failures

Options: (a) transient, retried like `429`/`5xx`; (b) not retried.

**Resolution:** (a).

### OD-008 A course that answers `404` mid-run, and other errors

Options: (a) skip that course with a `Warning`; any other unclassified failure
stops the run with "unexpected error" instead of the exception type name;
(b) any such failure stops the run.

**Resolution:** (a).

### OD-009 Carried findings

Options: (a) fold US-014 F-1, US-015 F-1 (bounded log values) and US-014 I-5
(blank course name skipped) into this Story; (b) leave them.

**Resolution:** (a).

---

## Raised at TEST_WRITING

### OD-010 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests must call types and members that do not exist
yet, and FR-004 / the entity model left the names of the adapter's failure type
and of the retry seam to "the design" without fixing them. The test-writer may
not modify production behaviour, and no artifact says who creates them — the
same gap US-005, US-007, US-011, US-012, US-015 and US-039 (OD-008) each
resolved for themselves.

Options:

1. **A compile-only skeleton**, as every earlier Story decided — only what the
   tests reference, every behavioural member throwing `NotImplementedException`,
   nothing registered in dependency injection, no existing behaviour changed:
   - `Domain/Enums/SyncDiagnosis` — the eight names of spec FR-006 (a
     declaration, no behaviour);
   - `SyncState.FailRun(DateTimeOffset, int, SyncDiagnosis)` — a new overload
     throwing; the `string` overload stays until IMPLEMENTATION removes it
     (entity model §2);
   - `Application/Models/GoogleReadFailureKind` — `Transient`,
     `Configuration`, `CourseGone`, `Unexpected`;
   - `Application/Ports/GoogleReadFailedException` — `Kind`
     (`GoogleReadFailureKind`) and `Diagnosis` (`SyncDiagnosis?`: the outcome
     for `Configuration`, `GoogleUnavailable` for a final `Transient`,
     `Unexpected` for `Unexpected`, null for `CourseGone`); no Google detail
     (spec FR-004). A plain data-carrying exception, so it is written in full;
   - `Infrastructure/Google/IGoogleRetryJitter` (`double NextFactor()`, in
     [0.8, 1.2]) — the injectable random source of spec FR-003;
   - a second `GoogleClassroomReader` constructor
     `(ISecretStore, GoogleServiceAccountSettings, HttpMessageHandler,
     TimeProvider, IGoogleRetryJitter, ILogger<GoogleClassroomReader>)`
     throwing; the registration keeps the old one until IMPLEMENTATION switches
     it and removes the old;
   - `Application/Models/LastSynchronizationView`,
     `LastSynchronizationStatus` (`NeverRun`, `Running`, `Completed`,
     `Failed`) and `Application/UseCases/GetLastSynchronizationQuery`
     (`Task<LastSynchronizationView> ExecuteAsync(CancellationToken)`, throwing)
     — API design §3, db design §4.

   And the **names the tests fix** for IMPLEMENTATION (the US-039 precedent of
   fixing the switcher markup in a fixture):
   - log events: `SyncGoogleRetry` (Warning, written by the adapter inside the
     run's existing `RunId` scope), `SyncCourseGone` (Warning),
     `SyncCourseNameBlank` (Warning); `SyncRunFailed` stays the run-failure
     event, at `Warning` for `GoogleUnavailable` and `Error` otherwise;
   - translation keys, `uk` and `en`: `LastSync.Title`,
     `LastSync.Status.NeverRun|Running|Completed|Failed`, `LastSync.StartedAt`,
     `LastSync.FinishedAt`, `LastSync.LastSuccess`, `LastSync.LastSuccess.None`,
     `LastSync.Diagnosis.GoogleUnavailable`, `LastSync.Diagnosis.Unexpected`;
     the six configuration diagnoses render the existing
     `AccessCheck.Outcome.<Code>`.
2. Write the tests without compiling them, deferring part of the suite.
3. Implement the production code during TEST_WRITING.

Recommended: option 1. Option 2 breaks TC-1 and the red-phase rule; option 3
writes the tests against finished code, which `AGENTS.md` forbids.

Impact on the Specification: none — a stage mechanism and names the
Specification left to design.

**Resolution:** option 1, decided by the Owner on 2026-10-03.
