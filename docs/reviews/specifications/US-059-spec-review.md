---
artifact_type: specification_review
story: US-059
version: 1
status: APPROVED
created_at: 2026-10-27T10:20:00Z
updated_at: 2026-10-27T10:20:00Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-059-sync-marks-courses-gone-and-skips-negative-values.md
    version: null
  - path: trebovaniya.md
    version: 101
  - path: docs/specifications/US-059-spec.md
    version: 1
  - path: docs/specifications/US-059-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-059-open-decisions.md
    version: 1
supersedes: null
---

# US-059 Specification Review

## Summary

**PASS — 0 Critical, 0 Major, 2 Minor.** The Specification is faithful to `trebovaniya.md` v101
(changelog v100/v101, §3 `Course` and `WorkspaceConnection`, §4 Epic 1 "Курс, которого нет в
Classroom", "Пустой список", "Смена технического аккаунта", "Когда повторы не помогли", §5
retention) and to BR-043, BR-047, BR-048, BR-049, BR-072. Every Story AC is represented; the two
derived ACs (AC-017, AC-018) follow from cited rules. The seven interpretations are named in the
digest. Two advisory findings: a millisecond race between the run's connection read and its
`observedAt`, and the digest's silence on the absence of a backfilled account-change instant.

No delegation: the reviewer read every cited passage and the code facts it checked itself.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-059-sync-marks-courses-gone-and-skips-negative-values.md` | — (amended for v101) |
| Specification | `docs/specifications/US-059-spec.md` | 1 (DRAFT) |
| Spec digest | `docs/specifications/US-059-spec-digest.ru.md` | 1 (DRAFT), input spec v1 — current |
| Open Decisions | `docs/decisions/US-059-open-decisions.md` | 1 (none) |
| Requirements | `trebovaniya.md` | 101 |

Inputs are current; nothing stale.

## Fidelity

| Spec rule | Checked against | Result |
|---|---|---|
| FR-001 mark "not in Classroom", list read to its end, data and status untouched, items not marked, `404` only skipped | §4 Epic 1 lines 1063–1071, BR-048 | faithful |
| FR-001.1 archived courses included | BR-047 "archived courses … keep their marks" | faithful |
| FR-001.6, FR-003.2 synchronization records, not mirror edits | §3 `Course` lines 852–855, BR-049 last sentence | faithful |
| FR-002 change instant = saving a different email | §3 `WorkspaceConnection` line 911 ("сохранения другого email, чем был") | faithful; case/space equality is the Story's AC-011 |
| FR-002.4 no new audit event | SC-11 already lists saving/changing `WorkspaceConnection`; BR-049 audits only the Admin's confirmation (Story 3) | faithful |
| FR-004 unmark when back on the list | BR-048, BR-049 ("mark is removed when the course is on the list again") | faithful |
| FR-005 empty list marks nothing, recorded as technical problem | §4 Epic 1 lines 1072–1076, BR-048 | faithful; flag shape and "run completes" are I-5 |
| FR-005.2 skip records kept on empty list | Story scope; US-058 FR-007.3 (read) | faithful |
| FR-006 "not visible" when last sighting before change; seen once by new account → "not in Classroom"; empty list first | §4 Epic 1 lines 1082–1096, BR-049 | faithful |
| FR-006.5 a marked course keeps its mark | Story scope and AC-015 | sourced from the Story |
| FR-007 counts only, no ids/names | SC-10, Story scope | faithful |
| FR-008 negative values skip the course like out-of-range | §4 Epic 1 lines 1033–1040 (v100), BR-043 | faithful |
| Retention: marks and sighting do not extend last activity | §5 lines 1710–1717 (last activity is Google-side + Meet), BR-072 | faithful |

Code facts §1.1 spot-checked: `observedAt` at `RunSynchronizationUseCase.cs:472`, connection read at
`:86`, `BeginAsync` between them — consistent with the Specification.

## Story Coverage

AC-001 … AC-016 each appear in §5 with the same meaning; the restatements (AC-005 adds the
already-marked course from the Story's "Then"; AC-010 fixes the three numbers) narrow, never
widen. Out-of-scope items match the Story's list. Nothing goes beyond the Story.

## Consistency

- **US-058**: FR-005.2 holds back the FR-007.3 removal only for an empty list; skip reason
  `ValueOutOfRange` and event 5135 reused unchanged — consistent.
- **US-018 incremental sync**: an unchanged archived course is listed, not read, and now gets a
  sighting (FR-003.1) — a write the Story acknowledges in Notes.
- **Permission matrix (§2)**: no endpoint, role or cell changes.
- **Read-only (BR-026)**: synchronization does not run and the connection save is already refused —
  no new service write. Correct.
- **Localization (NFR-073)**: no user-visible string. Correct.
- **Audit (SC-11)**: no new event. Correct.

## Open Items

`trebovaniya.md` §7 items 14, 26–29 concern other topics; the Story depends on none. No Open
Decision is needed and none was silently resolved.

## Interpretations

All are named in the Specification §11 and in digest §2:

| Id | Choice | In digest |
|---|---|---|
| I-1 | Course with no sighting counts as seen before the change | yes |
| I-2 | All marks/unmarks/sightings written at the end of the list | yes |
| I-3 | Log counts only new marks and unmarks; no line when all zero | yes |
| I-4 | List of id-less entries is empty | yes |
| I-5 | Empty list does not fail the run | yes |
| I-6 | Negative check in the reader's conversion | yes |
| I-7 | Unmark does not need a read | yes |
| AC-018 | `404` course records a sighting, no mark | yes |

Not named, advisory: the race of m-1 below.

## Digest Fidelity

Every digest statement traces to the Specification; no interpretation is softened or presented as
settled; the language is plain. One omission (m-2).

## Findings

### m-1 (Minor) A change saved between the run's connection read and `observedAt` falls on the unsafe side

- **Where:** Specification FR-006.1, VR-004; code fact §1.1.
- **Checked against:** BR-049 ("last seen on the list **before** the change"); `RunSynchronizationUseCase.cs:86`
  (connection, hence the account the list is read as) and `:472` (`observedAt`), with `BeginAsync`'s
  save between them.
- **What:** the sighting is the run's `observedAt`, taken after the run has fixed its technical
  account. If the Admin saves a new email in that window (milliseconds), the list read as the
  **old** account records sightings later than the change; a course the new account then cannot see
  is marked "not in Classroom" instead of "not visible". A change saved later in the run is on the
  safe side (sightings earlier than the change). The window is tiny and the outcome is the
  pre-v101 behaviour, so it is advisory: DB_DESIGN/IMPLEMENTATION may take the comparison instant
  no later than the connection read, or the Specification may name it as accepted.

### m-2 (Minor) The digest does not say that no account change is recorded before the update

- **Where:** digest §1 bullet "Админ сменил технический аккаунт"; Specification FR-002.3, §9
  Persistence.
- **Checked against:** Specification FR-002.3.
- **What:** a school that changed its technical account before installing this update has no change
  instant, so its lost courses get "not in Classroom", not "not visible". Before the pilot no school
  is affected, so the consequence is theoretical; one sentence in the digest would make it visible.

## Verdict

**PASS.** No Critical or Major finding. m-1 and m-2 are carried as non-blocking findings to
HUMAN_SPEC_APPROVAL.
