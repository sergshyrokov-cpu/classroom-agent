---
artifact_type: specification_review
story: US-060
version: 1
status: APPROVED
created_at: 2026-10-27T13:03:23Z
updated_at: 2026-10-27T13:03:23Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-060-marks-in-course-list-journal-report-and-export.md
    version: null
  - path: trebovaniya.md
    version: 102
  - path: docs/specifications/US-060-spec.md
    version: 1
  - path: docs/specifications/US-060-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-060-open-decisions.md
    version: 1
supersedes: null
---

# US-060 Specification Review

## Summary

**Verdict: PASS** — 0 Critical, 0 Major, 3 Minor.

The Specification follows `trebovaniya.md` v102 (Epic 1 "пропущенный курс", Epic 2 v100–v102,
Epic 3 v99–v102) and BR-045, BR-046, BR-048, BR-049, BR-057, BR-067, BR-068, BR-069 closely. It covers
every Story AC, adds two derived ACs from a resolved OD and the Story Notes, and names its
interpretations. OD-003 is open, and the Specification and digest both say so. The Owner resolves
it at `HUMAN_SPEC_APPROVAL`; it does not stop the review.

No work was delegated. The reviewer read the cited requirement passages (`trebovaniya.md` lines
1150–1176, 1270–1298), the business rules, `CourseMembership.cs`,
`RunSynchronizationUseCase.cs` lines 990–1050, `CourseListSource.cs` lines 45–60 and
`ReportBuilder.cs` line 259 directly.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-060-marks-in-course-list-journal-report-and-export.md` | — |
| Specification | `docs/specifications/US-060-spec.md` | 1 (DRAFT) |
| Digest | `docs/specifications/US-060-spec-digest.ru.md` | 1 (DRAFT, input spec v1) |
| Open Decisions | `docs/decisions/US-060-open-decisions.md` | 1 |

Inputs are current: the spec cites `trebovaniya.md` v102 and OD v1, and the digest cites spec v1.

## Fidelity

- FR-001 matches Epic 2 v100/v102 and BR-048/BR-049: the mark sits next to the status, the last
  stored status is shown and no filter is added.
- FR-002 matches Epic 2 v101 and BR-069. The "teachers on roster" fact is confirmed at
  `CourseListSource.cs:53`.
- FR-003 matches Epic 3 v100/v102, Epic 1 "пропущенный курс" and BR-046/BR-049. The no-date case
  follows OD-002 a). The precedence rule is I-2.
- FR-004 matches Epic 3 v99 and BR-067: a highlight and a label, and the scale value stays 100 %
  (v98). Drafts appear only where they are shown (BR-057). The `maximum > 0` condition is m-1.
- FR-005 matches BR-045 and BR-067: the column and its grades stay, the header carries the mark,
  and the mark also sits next to the topic on «Теми занять».
- FR-006 matches Epic 3 v101 and BR-068. The fact that a submission-only student is stored with
  first sighting = last sighting and off the roster is confirmed at
  `RunSynchronizationUseCase.cs:1032-1033`. `CourseMembership.NotOnRoster` never moves
  `LastSeenAt` and `SeenAgain` sets `OnRoster = true`, so OD-003 a) cannot produce a wrong date.
  The claim in the OD is correct.
- FR-007 and FR-008 match BR-067/BR-068 ("the value stays, a colour and a note") and OD-001 a).
- §7 matches BR-026, SC-7, SC-10, SC-11 and SC-13. It adds no write, no audit event and no
  destination.
- I found no rule without a source.

## Story Coverage

AC-001…AC-010 are all present, each with the same meaning as in the Story. Spec AC-003 adds the
short journal's draft behaviour (BR-057). Spec AC-005 states that the blank form's grading sheet
gets no item mark, which is consistent with OD-001 a) ("only if the form has a place"). AC-011
(OD-002 a) and AC-012 (Story Notes, I-2) are marked as derived. The out-of-scope list matches the
Story's.

## Consistency

- Permission matrix: no change. Admin and Dean see the same (BR-002).
- Read-only mode: viewing and export already work without a guard (BR-026). There is no new
  service write.
- Localization: every text goes to both resx files (NFR-073). Names and template text are not
  translated.
- Audit: unchanged (SC-11).
- US-058/US-059: the stored fields are used as they were designed. Synchronization is not
  touched.
- `ReportBuilder.cs:259` applies the scale only when `max > 0`, which agrees with spec §1.1.

## Open Items

`trebovaniya.md` §7 holds no item this Story depends on, and the OD file says so with reasons.
OD-001 and OD-002 were resolved a) by the Owner. **OD-003 is open.** It blocks FR-006 and AC-008
until the Owner chooses. The Specification and the digest show it as open with both options and a
recommendation.

## Interpretations

The Specification names I-1…I-6, and all six appear in digest section 2. The digest also covers
FR-003.3 (an archived course is not "skipped"). Two more choices are not named; see m-1 and m-2:

1. I-1: the journal's header line goes above the table.
2. I-2: header precedence, with one line for "not visible" and "skipped".
3. I-3: placement in the program export.
4. I-4: notes in the blank form for the header and item marks.
5. I-5: the item mark has no date.
6. I-6: Ukrainian wording.
7. *(not named)* "Above maximum" requires maximum > 0 (FR-004.1, VR-001) — m-1.
8. *(not named)* The Ukrainian «вибув» is the masculine form — m-2.

## Digest Fidelity

Every digest statement traces to the Specification. OD-003 is presented as open, with the
recommendation and its cost stated honestly. OD-001 and OD-002 are correctly reported as resolved.
The Russian is plain. One consequence is left out (m-3).

## Findings

**m-1 (Minor) — the `maximum > 0` condition is not named as a choice.**
Location: spec FR-004.1, VR-001; digest §2 does not mention it.
Source: BR-067 and Epic 3 v99 say only "больше максимума". Under the literal reading, any positive
grade on an item with maximum 0 would be above the maximum.
The spec excludes maximum 0, which agrees with how the report's scale treats max 0
(`ReportBuilder.cs:259`). In practice the case is almost unreachable, which is why this is Minor
and not Major. Suggested fix: add a line to I-list and the digest, or confirm the reading at the
gate.

**m-2 (Minor) — «вибув» is gendered in Ukrainian.**
Location: spec FR-009 table ("left, with date / no date"); digest §1 and §2 (I-6).
Source: Epic 3 v101 and BR-068 use the Russian «выбыл».
In Ukrainian the past tense agrees with gender, so a girl would be labelled «вибув». The system
stores no gender, so a gender-neutral wording would avoid this, e.g. «вибув(-ла)» or
«не в списку курсу з {0}». The label is user-visible text in a school document, so the Owner should
choose it at the gate.

**m-3 (Minor) — the digest leaves out that the card and the journal disagree on a submission-only
student.**
Location: spec §10 says this; digest §4 says only "Раздел «Вибули з курсу» в карточке курса
остаётся как есть".
Source: BR-068 (no date when the system does not know when the student left).
After this Story, the course card will show a submission-only student with the date of their
first sighting, while the journal and report will show «вибув» with no date. The Owner reads only
the digest and does not learn about this visible inconsistency. It is out of scope by the Story's
choice. Suggested fix: one line in digest §4, so the Owner can decide whether the card needs a
follow-up.

## Verdict

**PASS.** There are no Critical or Major findings, and m-1…m-3 are advisory. At
`HUMAN_SPEC_APPROVAL` the Owner decides OD-003 and may act on the three Minor findings.
