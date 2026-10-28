---
artifact_type: open_decisions
story: US-065
version: 2
status: APPROVED
created_at: 2026-10-28T11:05:46Z
updated_at: 2026-10-28T11:09:40Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-065-erasure-exclusion-by-email-in-advance.md
    version: null
  - path: trebovaniya.md
    version: 104
supersedes: null
---

# US-065 Open Decisions

OD-001 and OD-002 come from the Story and were resolved by the Owner before
activation. OD-003 was raised while writing the Specification.

`trebovaniya.md` §7 (v104) has no open item this Story depends on: items 14,
26, 27, 28 and 29 concern submissions after removal from a course,
documentation, re-loading a purged leaver, Meet field names and delegation with
2-step verification.

## OD-001 Which addresses the field accepts

v104 does not say whether the email must be in the school's domain.

- **a) (recommended)** — any domain, like lifting an exclusion today (US-038
  I-13): the two fields behave the same, and a Classroom course may hold a
  participant from another domain.
- b) — only the school's domain: catches a mistyped personal address, but an
  external participant could not be excluded in advance.

**Resolved 2026-10-28 by the Owner: a)** («a, a»). Specification VR-001.

## OD-002 One email or a list at a time

For a domain move the Admin may need to enter several addresses.

- **a) (recommended)** — one email per submission: erasures on request are rare,
  validation and messages stay per address, and each entry is one audit event.
- b) — a list (one address per line): faster for a move, but needs per-line
  results and partial-failure messages.

**Resolved 2026-10-28 by the Owner: a)** («a, a»). Specification FR-001,
VR-001.

## OD-003 The matched person is already stored under another address

**Status: RESOLVED.** Affects Specification FR-008 and AC-017.

The "already loaded" check at entry compares the email stored in the program.
A person loaded earlier under address A, whose address in Google is now B, is
not found when the Admin enters B: the exclusion is stored. At the next
synchronization the roster entry with B matches the exclusion by fingerprint,
while a `ClassroomParticipant` with that Google id — with memberships and
submissions — is already stored. Neither `trebovaniya.md` v104 nor the Story
says what happens then.

- **a) (recommended)** — do not match a Google id that is already stored. The
  sync loads the person as usual (their stored email becomes B) and leaves the
  exclusion without an id. When the Admin enters B again, the page says the
  person is already loaded and leads to the ordinary erasure, which leaves its
  own exclusion with the id. Keeps the US-038 rule that no stored participant
  has an excluded Google id, and never freezes personal data without anyone
  knowing. Known limitation until the Admin acts: the Meet load already skips
  B's connections by fingerprint, while Classroom data keeps loading.
- b) — match and write the id anyway. The sync stops updating the person, but
  their stored data stays until the Admin erases it, and nothing tells the
  Admin. Breaks the US-038 rule above.

Erasing the stored person automatically, or showing the Admin a notice about
the match, would be new behaviour that needs a `trebovaniya.md` change first;
not offered here.

**Resolved 2026-10-28 by the Owner: a)** («а»), after SPECIFICATION and
before SPEC_REVIEW.
