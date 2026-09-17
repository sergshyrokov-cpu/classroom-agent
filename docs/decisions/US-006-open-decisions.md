---
artifact_type: open_decisions
story: US-006
version: 2
status: APPROVED
created_at: 2026-09-17T15:31:06Z
updated_at: 2026-09-17T15:37:50Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-006-control-plane-push.md
    version: null
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-006 Open Decisions

Story-level Open Decisions for US-006 (Control Plane push on status change). Every
item is resolved only by a human, at `HUMAN_SPEC_APPROVAL`; the resolution is
written next to the item and nothing is deleted.

The Story's own "Open Decisions" section records none: its questions were decided in
`trebovaniya.md` v76. `trebovaniya.md` section 7 has no open item this Story depends
on (items 10 and 14 concern Google; item 26 documentation; item 27 was closed in
v75). No NuGet package or skeleton decision is needed: all projects exist and no
package is added (Specification I-13).

Status summary:

| Id | Subject | Status | Affects |
|---|---|---|---|
| OD-001 | A swallowed push may leave a stale status for up to 6 hours | RESOLVED (2026-09-17) | FR-010, FR-009, AC-011 |

---

## Carried from the Story

None.

## Raised by the Specification

### OD-001 — A push swallowed by the limit or a running check

**Status: RESOLVED.**

**Gap.** `trebovaniya.md` §9 v76 and Story AC-011 say: a push that arrives while a
check is running, or less than a minute after the previous push-triggered check, is
answered `202` and **does nothing**. Two ordinary situations then leave the school on
the wrong status until its next scheduled check — up to **6 hours**:

1. **Quick change back.** The Owner suspends a school by mistake and resumes it 20
   seconds later. The first push starts a check, which receives `suspended`; the
   second push arrives within the minute and is ignored. The school stays in
   read-only mode for up to 6 hours although it is active in the Control Plane.
2. **Check in flight.** A scheduled check has already asked the Control Plane and
   is waiting for its answer when the Owner suspends the school. The push arrives
   while that check runs and is ignored; the check records the old status
   `active`. The suspension reaches the school up to 6 hours late — exactly what the
   push was meant to prevent.

**Impact.** FR-010 and FR-009 step 3 as written implement the decided rule and carry
this risk. Choosing option 2 changes a rule fixed in `trebovaniya.md` v76, so it
needs a new requirements version (v77) and an update of Story AC-011 before the
Specification can be approved.

**Options.**

1. Keep the rule as decided: a swallowed push does nothing; accept that a quick
   change back or a push during a running check may take up to 6 hours. The load
   bound is the strictest.
2. *(Recommended)* **Remember one pending check.** A push that may not start a check
   now is still answered `202`, and the installation remembers — once, not per push —
   that a check is owed. It runs that check as soon as it is allowed: right after
   the running check completes, or when the minute since the previous push-triggered
   check has passed. Any number of swallowed pushes still produce at most one extra
   check, so the "not more than once a minute, never two at once" bound holds and a
   flood of forged pushes cannot load the Control Plane more than option 1 does. The
   last status change always reaches the school within about a minute.
3. Drop the one-minute limit and keep only "never two at once": a push during a
   running check still does nothing (case 2 remains), but case 1 is fixed. A flood
   of pushes could then cause back-to-back checks.

**Resolution:** *Resolved 2026-09-17 by the human (the Owner): option 2. Recorded in
`trebovaniya.md` v77 (§8, §9); Story AC-011 updated; Specification v2 FR-009, FR-010.*

## Interpretations

Behaviour not literally fixed by the Story or `trebovaniya.md` is stated in the
Specification as interpretations I-1 … I-14 (section 11 there). They are reviewed at
`HUMAN_SPEC_APPROVAL`; any interpretation the human rejects becomes an Open Decision
in a new version of this document.

*Accepted 2026-09-17 by the human (the Owner) in the conversation: I-1 … I-14.*
