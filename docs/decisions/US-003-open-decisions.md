---
artifact_type: open_decisions
story: US-003
version: 1
status: APPROVED
created_at: 2026-09-17T09:15:08Z
updated_at: 2026-09-17T09:29:38Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-003-manage-allowed-admins.md
    version: null
  - path: trebovaniya.md
    version: 70
supersedes: null
---

# US-003 Open Decisions

Story-level Open Decisions for US-003 (Manage AllowedAdmin entries). Every item
is resolved only by a human, at `HUMAN_SPEC_APPROVAL`; the resolution is written
next to the item and nothing is deleted.

**There are no Open Decisions.**

The Story's own "Open Decisions" section records none: its questions (email in
the installation's domain, format and letter case, uniqueness, re-adding a revoked
email, a limit on entries, managing entries of a suspended installation, confirming
a revocation, fewer than two Admins) were decided in `trebovaniya.md` v70 (§3, §4,
§9). `trebovaniya.md` section 7 has no open item this Story depends on (items 10
and 14 concern Google access and synchronization).

Status summary:

| Id | Subject | Status | Affects |
|---|---|---|---|
| — | none | — | — |

---

## Carried from the Story

None.

## Raised by the Specification

None. Behaviour not literally fixed by the Story or `trebovaniya.md` is stated in
the Specification as interpretations I-1 … I-10 (section 11 there), derived from
conventions and from the choices the human confirmed for US-002. They are reviewed
at `HUMAN_SPEC_APPROVAL`; any interpretation the human rejects becomes an Open
Decision in a new version of this document.
