---
artifact_type: open_decisions
story: US-038
version: 2
status: APPROVED
created_at: 2026-10-15T05:35:49Z
updated_at: 2026-10-15T05:40:39Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-038-erase-participant.md
    version: null
  - path: trebovaniya.md
    version: 93
supersedes: null
---

# US-038 Open Decisions — Erase all data of one participant on request

The Story carries no Open Decision: 1a, 2a, 2b-a, 3a, 4a and 5a were decided by
the Owner on 2026-10-14 and recorded in `trebovaniya.md` v93. One new Open
Decision was raised while writing the Specification.

`trebovaniya.md` §7 (v93) has no open item this Story depends on: items 10, 14,
26, 27 and 28 are open; none concerns erasure or exclusions.

## OD-001 A participant with no email

`ClassroomParticipant.Email` is optional (US-014 OD-006): Google may omit it,
and a submitter who is not on any roster is stored with neither an email nor a
name. For such a person there is no fingerprint, so:

- their Meet rows cannot be found — Meet rows are tied to a person only by email;
- their exclusion cannot be lifted by entering an email (§4 Epic 10 lifts by
  email only).

A participant with neither name nor email cannot be found by the search at all;
this is accepted under either option.

- a) **(Recommended)** Erase them. The exclusion holds the Google user id and the
  date, with no fingerprint; synchronization skips the Google user id. The
  confirmation page warns that no Meet rows can be tied to this person and that
  the exclusion cannot be lifted by entering an email. Lifting such an exclusion
  is left to a later Story or to the Owner. The school's request is honoured in
  full for everything the program can tie to the person.
- b) Refuse erasure for a participant with no email. The confirmation page says
  that this person must be erased by the Owner's manual procedure. Every
  exclusion then has a fingerprint and can be lifted, but the school cannot
  honour such a request itself.

**Resolution (Owner, 2026-10-15): a)** — such a participant is erased; the
exclusion holds the Google user id and the date with no fingerprint; the
confirmation page warns that no Meet rows can be tied to them and that the
exclusion cannot be lifted by email.

Impact: spec FR-003, FR-006, FR-008, FR-012, AC-019.
