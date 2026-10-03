---
artifact_type: open_decisions
story: US-017
version: 1
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
