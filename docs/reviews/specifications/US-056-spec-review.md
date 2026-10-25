---
artifact_type: specification_review
story: US-056
version: 3
status: APPROVED
created_at: 2026-10-25T15:10:00Z
updated_at: 2026-10-25T17:00:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-056-membership-starts-at-course-creation.md
    version: null
  - path: trebovaniya.md
    version: 98
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/specifications/US-056-spec-digest.ru.md
    version: 3
  - path: docs/decisions/US-056-open-decisions.md
    version: 1
supersedes: docs/reviews/specifications/US-056-spec-review.md@v2
---

# US-056 Specification Review (v3)

## Summary

Specification v3 closes review v2: M-1 (the unassigned Meet codes candidate test)
is now the fifth member-on-date decision (§1, FR-003.5, FR-006, AC-004 second
paragraph, digest §1), and m-1 (the two operating assumptions behind exact
pre-upgrade recognition) is named in §1, FR-005, I-3, the AC preamble and the
digest. A fresh ripgrep confirms no other reader of the start. The rule stays
faithful to `trebovaniya.md` v98 §3 «Начало участия», and the digest tells the
Owner the truth. One Minor finding. Verdict **PASS**.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-056-membership-starts-at-course-creation.md` | — |
| Requirements | `trebovaniya.md` | 98 |
| Specification | `docs/specifications/US-056-spec.md` | 3 |
| Digest | `docs/specifications/US-056-spec-digest.ru.md` | 3 |
| Open Decisions | `docs/decisions/US-056-open-decisions.md` | 1 |
| Previous review | `docs/reviews/specifications/US-056-spec-review.md` | 2 (superseded by this) |

Inputs are current: the Specification names trebovaniya v98, OD v1 and review v2;
the digest names Specification v3.

## Resolution of review v2

| v2 finding | v3 | Status |
|---|---|---|
| M-1 unassigned-codes candidate test missed | §1 lists `MeetCodesReadSource.GetUnassignedPageAsync` (`:54`) as a reader; FR-003.5 adds it with the order rule kept; FR-006 says the list's order follows the new start while markup and texts do not change; AC-004 second paragraph pins the group placement; digest §1 tells Dean and Admin | Closed |
| m-1 unstated assumptions | §1 and FR-005 name both (runs do not overlap — `SyncRunCoordinator`; clock does not step back); I-3 refers to them; AC preamble requires increasing, distinct run instants; digest §2 states them plainly | Closed |

## Fidelity

Checked against `trebovaniya.md` §3 `CourseMembership` «Начало участия» (v98 text
re-read), Epic 3 «Кто в журнале за период», Epic 4 «Состав курса — на дату
встречи» and «как считается доля», BR-051, PC-8.

- FR-001 a/b/c reproduce §3: roster or submissions in the first-loading run →
  `creationTime`, else that run's day; later arrivals → first seen. FR-001 b's
  instant-vs-day equivalence holds because every consumer compares dates.
- FR-002/FR-005 cover "каждый курс, в том числе созданный после первой
  синхронизации, и участия, сохранённые до v98".
- FR-004 keeps the end of membership and the leaver expiry on last-seen, as §3
  and §5 say.
- Code facts re-checked directly: `MeetCodesReadSource.cs:38-67` computes
  `has_candidate` from `cm.first_seen_at` with the same date rule as
  `RosterOnDate` and orders by it; `rg "FirstSeenAt|first_seen_at" src` outside
  `Migrations` finds only the readers listed in §1 (plus the entity, its
  configuration/index and record types); `SyncRunCoordinator` guards a single
  `_running` flag under a lock.

No invented rule; no contradiction.

## Story Coverage

AC-001…AC-010 carry the Story's data and outcomes. AC-004 is extended to the
unassigned list order (within the Story's "automatic Meet code linking" scope
item). AC-009 adds a derived second course; AC-011 is derived from FR-002 and
marked so. Nothing beyond the Story's scope.

## Consistency

- Permission matrix, read-only mode (BR-026), localization (NFR-073), audit
  (SC-11): untouched; §7 correct.
- US-032 order rule "codes with candidates first" is kept; only the start used by
  the candidate test changes.
- US-053 ↔ FR-002/I-4; US-038 ↔ FR-002/I-5/AC-011; §7 item 27 ↔ FR-001 c;
  mirror principle v83 ↔ I-2 — consistent.

## Open Items

`trebovaniya.md` §7 has no open item this Story depends on. OD-001…OD-005 are
resolved by the Owner and carried with their resolutions.

## Interpretations

1. I-1 `creationTime` later than the first load → still `creationTime` (Spec;
   impossible case, untested — acceptable).
2. I-2 the currently stored `creationTime` is authoritative (Spec + digest).
3. I-3 `course.created_at` as the record of the first load; exact recognition
   under two named conditions (Spec + digest).
4. I-4 first load = first committed load (Spec + digest).
5. I-5 erased and re-admitted person counts from the reload (Spec + digest).
6. FR-001 b run's instant vs run's day — equivalent at date granularity (Spec;
   no effect the Owner could observe).
7. FR-002 purges and erasure do not change "seen in the first load" of others
   (Spec + digest, AC-011).
8. FR-003.5 the unassigned list's candidate test uses the new start (Spec +
   digest §1).

All interpretations that could change the Owner's decision are in the digest.

## Digest Fidelity

Plain Russian; each statement traces to Specification v3. The new bullet on the
unassigned Meet codes list and the two conditions behind exact recognition are
stated without softening. The withdrawn v1 exception remains disclosed. No open
decision is hidden (there are none).

## Findings

### m-1 (Minor) — FR-003.5: "agrees with the scoring of item 4" is broader than meant

**Location:** FR-003.5 last sentence.
**Checked against:** `MeetCodesReadSource.cs:47-56` (`has_candidate` = an
organizer is a course teacher on a meeting's date) and FR-003.4 (scoring also
counts students and applies thresholds).
**What is wrong:** read literally, "its candidate test agrees with the scoring of
item 4 for the same code" could mean the list must reproduce the scorer's whole
result. The intended meaning is narrower: both use the same start (and the same
teacher-on-date rule). Harmless for the outcome; TEST_WRITING and IMPLEMENTATION
should read it as "same start, same teacher-on-date rule", not "same score".

## Delegation

Nothing was delegated. All code facts were read directly by the reviewer.

## Verdict

**PASS** — no Critical or Major findings; one Minor (m-1). Ready for
HUMAN_SPEC_APPROVAL.
