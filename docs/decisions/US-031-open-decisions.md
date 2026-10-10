---
artifact_type: open_decisions
story: US-031
version: 2
status: DRAFT
created_at: 2026-10-10T05:35:31Z
updated_at: 2026-10-10T06:02:59Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-031-meet-events-pull.md
    version: null
  - path: trebovaniya.md
    version: 87
supersedes: null
---

# US-031 Open Decisions — Pull Meet events and keep history beyond 180 days

Eight decisions (OD-001 … OD-008) were written into the Story and **resolved by
the Owner on 2026-10-06, before activation**, each as option (a). They are carried
here with their ids and resolutions unchanged; OD-004 … OD-006 are also in
`trebovaniya.md` v87.

Writing the Specification raised two more: OD-009 (an item still open in
`trebovaniya.md` §7) and OD-010. Both were resolved by the Owner at `HUMAN_SPEC_APPROVAL` on 2026-10-10.

## OD-001 Scope — RESOLVED (a)

Pull, storage and per-meeting expiry only; linking is US-032, reports US-033.

## OD-002 First pull — RESOLVED (a)

Everything Google keeps: 180 days back. → Spec FR-002.

## OD-003 Later pulls — RESOLVED (a)

Re-read from 3 days before the end of the last successful Meet pull. → FR-002,
FR-007.

## OD-004 Which meetings — RESOLVED (a)

Only meetings organized by a domain account (`trebovaniya.md` v87). → FR-005.

## OD-005 Domain account — RESOLVED (a)

Exact match of the connection domain, no subdomains (`trebovaniya.md` v87).
→ FR-006.

## OD-006 Order and failures — RESOLVED (a)

Classroom first, then Meet; a Meet permission failure stops the run like any
permission failure. → FR-001, FR-010.

## OD-007 Admin view — RESOLVED (a)

One line "Meet meetings loaded up to" in the "Last synchronization" block.
→ FR-011.

## OD-008 Audit — RESOLVED (a)

No separate pull event; the purge's audit row gains meeting and participation
counts. → FR-013.

## OD-009 The `call_ended` fields are unverified — RESOLVED (a)

`trebovaniya.md` §7 item 28 (v87, "Проверить при внедрении"): the field names
`meeting_code`, `organizer_email`, `identifier`, `identifier_type`, `is_external`,
`endpoint_id`, `duration_seconds`, `conference_id` come from Google's
documentation and were never checked against a live response. The Story depends
on them (AGENTS.md: a Story depending on an open §7 item has an Open Decision).

Impact: Spec FR-003/FR-004 (mapping), I-3 (one event per leaving connection,
time = leave time). Design and tests are unaffected — fixtures are synthetic
(TC-4). If the live names differ, only the adapter's single mapping place changes.

This item is project-level and is closed by a new version of `trebovaniya.md`
after the Owner's check on a live domain; the Story cannot resolve it.

Options:

- **(a) Proceed** with the documented names, mapping in one place, and the Owner
  verifies at the first deployment as item 28 says. *(Recommended — the check
  needs a live domain, and every day without the pull loses history.)*
- (b) Hold the Story until the Owner has run a check on the live domain.

Resolution: **(a)**, by the Owner on 2026-10-10 at `HUMAN_SPEC_APPROVAL`. The
Story proceeds; `trebovaniya.md` §7 item 28 stays open until the live check.

## OD-010 Time zone of the "Meet meetings loaded up to" line — RESOLVED (a)

The Story says the line is shown "in the installation's time zone" (the school
time zone setting of US-025 FR-010). The rest of the same block (US-017 FR-007)
shows its times in **UTC** today, with a "UTC" suffix.

Impact: Spec FR-011, AC-009.

Options:

- **(a)** The Meet line in the school's time zone, as the Story says; the other
  lines of the block stay in UTC. *(Recommended — follows the Story without
  touching what US-017 shipped; the line is labelled clearly.)*
- (b) Move the whole block to the school's time zone (changes US-017 behaviour
  and its tests — wider than this Story).
- (c) Show the Meet line in UTC like the rest of the block (contradicts the
  Story's wording).

Resolution: **(a)**, by the Owner on 2026-10-10 at `HUMAN_SPEC_APPROVAL`.

## OD-011 Compile-only skeleton created at TEST_WRITING — RESOLVED (a)

Raised by TEST_WRITING. The tests reference declarations that do not exist yet —
`MeetSession`, `MeetParticipation`, `MeetConnection`, `SchoolDomainAccount`,
`SyncStep`, the new `SyncState` members (`MeetLoadedUpTo`, `FailedStep`, the
three-argument `CompleteRun`, the four-argument `FailRun`), the two
`RetentionPurgeCounts` counts and the two `AuditEvent` purge properties, the
ports `IMeetReportsReader` and `IMeetSessionRepository` with their models, the
two `IRetentionPurgeStore` methods, the `LastSynchronizationView` additions, the
Meet counts of `SynchronizationRunOutcome`, `GoogleMeetReportsReader` and
`MeetSessionRepository`. The test-writer may not change production code — the
gap US-017 (OD-010), US-025, US-027, US-028 (OD-005), US-037 (OD-007), US-039 and
US-042 (OD-001) resolved the same way.

Options: (a) a compile-only skeleton — only the declarations the tests
reference; new members throw `NotImplementedException`; no EF mapping, no
migration; the port and the repository registered in DI only because the
synchronization use case cannot be constructed without them (the host tests
substitute the port); IMPLEMENTATION owns and completes it; (b) no skeleton —
tests that do not compile until IMPLEMENTATION.

**Resolution:** (a) — Owner, 2026-10-10.
