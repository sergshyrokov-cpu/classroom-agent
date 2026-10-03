---
artifact_type: open_decisions
story: US-037
version: 3
status: DRAFT
created_at: 2026-10-03T16:51:10Z
updated_at: 2026-10-03T17:03:34Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-037-retention-purge.md
    version: null
  - path: trebovaniya.md
    version: 80
supersedes: null
---

# US-037 Open Decisions — Retention purge

Five decisions (OD-001 … OD-005) were written into the Story by the author and
**resolved by the Owner on 2026-10-03, before activation**, each as the
recommended option (a). They are carried here with their ids and resolutions
unchanged.

Writing the Specification raised one new decision, **OD-006**, resolved by the
Owner on 2026-10-03 as option (a).

---

## Resolved before activation

### OD-001 When in the day the purge runs

Options: (a) once shortly after start-up, then every 24 hours; (b) at a fixed
night hour.

**Resolution:** (a) — simple, and the school's time zone setting does not
exist yet.

### OD-002 Purge and synchronization at the same time

Options: (a) never overlap: the purge waits for a running synchronization;
(b) independent.

**Resolution:** (a) — synchronization must not update a course while the
purge deletes it.

### OD-003 A failure on one course

Options: (a) roll that course back, log it, go on with the rest; the next run
retries; (b) stop the whole run.

**Resolution:** (a).

### OD-004 A "last purge" screen for the Admin

Options: (a) none — the audit event and the log suffice; (b) a block on the
connection page, as US-017.

**Resolution:** (a) — a screen would need a new cell in the §2 permission
matrix.

### OD-005 The last Admin account

Options: (a) delete it like any other; the next Google sign-in recreates it
while the email is in `AllowedAdmin`; (b) never delete the last Admin.

**Resolution:** (a) — §5 v45 deletes accounts "независимо от отключения", and
`CompleteGoogleSignInUseCase` already creates the account on a first sign-in.

---

## Raised by the Specification

### OD-006 A stored course with no Google date at all

`trebovaniya.md` §5 v36 defines last activity only from Google dates (course
update, coursework creation/update, submission update, Meet). All of them are
nullable in the stored model, and synchronization deliberately **imports** an
unknown course that has no date (US-015 OD-001). For such a course the rule
yields no last activity, and the purge could never delete it — the data would be
kept forever, which §5 forbids.

Options:

- **(a) Recommended:** fall back to `Course.CreatedAt` — when this installation
  first imported the course. Its N years count from that moment, as a student
  already off the roster at the first synchronization counts from that run (§5
  v56 known limitation). Any later Google date still takes precedence.
- (b) Never delete such a course (no evidence of age). Contradicts §5: data with
  no expiry.
- (c) Raise it as a project-level question in `trebovaniya.md` §7 and leave such
  courses untouched until a new version decides.

**Impact:** FR-002 and AC-015 of the Specification follow (a). With (b), FR-002
drops the fallback and AC-015 asserts the course is kept; with (c), the same,
plus a new item in `trebovaniya.md` §7. In practice Google returns
`updateTime` for every course, so the case is rare either way.

**Resolution:** (a) — resolved by the Owner on 2026-10-03 at
`HUMAN_SPEC_APPROVAL` review, together with interpretations I-1 … I-7 of the
Specification.

---

## Raised at TEST_WRITING

### OD-007 Compile-only skeleton created at TEST_WRITING

Raised by TEST_WRITING. The tests call types and members that do not exist yet.
The test-writer may not change production behaviour, and no artifact says who
creates them. This is the same gap every earlier Story resolved the same way
(US-015 OD-012, US-017 OD-010, US-039 OD-008).

Options:

1. **A compile-only skeleton** — only what the tests reference. Every behavioural
   member throws `NotImplementedException`; nothing is registered in dependency
   injection, and there is no EF configuration and no migration. No existing
   behaviour changes:
   - `Domain/Enums/AuditAction.RetentionPurgeRun` — an enum member;
   - `Domain/Rules/RetentionPurgeCounts` — a `readonly record struct` of the five
     counts with `Zero`; plain data, written in full;
   - `Domain/Rules/RetentionRule` — `Cutoff`, `IsExpired`, `LatestActivity`,
     throwing;
   - `AuditEvent` — five `int?` properties (data) and the factory
     `RetentionPurgeRun(RetentionPurgeCounts, DateTimeOffset)`, throwing;
   - `Application/Ports/IRetentionPurgeStore` — the interface of entity model §3;
   - `Application/Models/CourseActivityDates`, `RetentionPurgeOutcome`,
     `RetentionPurgeFailure`, `RetentionPurgeStep` — data, written in full;
   - `Application/UseCases/RunRetentionPurgeUseCase` — a constructor and
     `ExecuteAsync`, throwing;
   - `Infrastructure/Persistence/RetentionPurgeStore` — every member throwing;
   - `Web/BackgroundServices/RetentionPurgeBackgroundService` — throwing,
     **not** registered;
   - `SyncRunCoordinator.TryStartPurge()` / `PurgeCompleted()`, throwing.

   **One signature adjustment of the entity model, recorded here:**
   `RetentionPurgeOutcome` carries, besides the counts, the list of failed units
   (`RetentionPurgeFailure`: the step, the course's internal id or null, and the
   exception **type** name). The Application layer has no logger in this
   project, so the background service writes the `Error` lines of spec FR-012
   from this outcome. That is why the outcome must carry the course id; entity
   model §4 said "no ids". It still carries no Google id and no data.

   **The names the tests fix** for IMPLEMENTATION:
   - log events `RetentionPurgeStarted` (Information), `RetentionPurgeCompleted`
     (Information), `RetentionPurgeUnitFailed` (Error),
     `RetentionPurgeWaitingForSync` (Information).

   **One test-infrastructure change**, not production code: the purge runs at
   every host start and writes an audit row, so `InstallationTestHost.AuditRowsAsync`
   starts excluding `retention_purge_run` rows; the purge tests read them
   through their own helper. Without this, the ~200 existing assertions about
   "the audit rows of my action" would count a row that has nothing to do with
   them. `AuditRowsAsJsonAsync` (the "no personal data" check) keeps every row.
2. Write the tests without compiling them, deferring part of the suite.
3. Implement the production code during TEST_WRITING.

Recommended: option 1. Option 2 breaks TC-1 and the red-phase rule; option 3
writes the tests against finished code, which `AGENTS.md` forbids.

Impact on the Specification: none. This is a stage mechanism, plus names the
Specification left to design.

**Resolution:** option 1, decided by the Owner on 2026-10-03.
