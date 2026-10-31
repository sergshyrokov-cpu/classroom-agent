---
artifact_type: specification_review
story: US-061
version: 1
status: APPROVED
created_at: 2026-10-31T14:19:53Z
updated_at: 2026-10-31T14:19:53Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/specifications/US-061-spec.md
    version: 1
  - path: docs/specifications/US-061-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-061-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-061 Specification Review

## Summary

**PASS.** The Specification is faithful to `trebovaniya.md` v104 (Epic 4 v102 items "курс с
пометкой — не кандидат" and "Курс с пометкой" in the reports) and to BR-062 / BR-065. Every Story
AC is covered; the three derived ACs trace to resolved Open Decisions or a named interpretation.
No Critical or Major finding; two Minor findings.

Nothing was delegated: the reviewer read the Story, Specification, digest, Open Decisions,
`trebovaniya.md` lines 28–40, 1144–1152, 1222–1228, 1340–1360, 1395–1468, BR-062 / BR-065, US-060
OD-002, the `DataMarks.*` keys in `SharedResource.uk.resx`, and the header rows of
`MeetReportsWorkbookMapper` / `MeetMeetingWorkbookMapper`.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| story | docs/stories/US-061-meet-reports-and-linking-for-marked-courses.md | — |
| specification | docs/specifications/US-061-spec.md | 1 |
| spec_digest | docs/specifications/US-061-spec-digest.ru.md | 1 (input: spec v1 — current) |
| open_decisions | docs/decisions/US-061-open-decisions.md | 1 |
| requirements | trebovaniya.md | 104 |

## Fidelity

- **FR-001 / VR-002** — BR-065 (v102) and Epic 4 line 1408: a marked course is "not offered by the
  automatic link nor by the candidate list". Removing it from the candidate set before `Decide` is
  the reading that makes the stated purpose ("иначе код курса, созданного заново … сам никогда не
  привязался бы") achievable; shares are per-course and independent of the candidate set, so "same
  numerator and denominator" is correct. Thresholds unchanged. ✔
- **FR-001.4 / FR-004** — "уже сделанные привязки к нему остаются" and "an automatic link is never
  revised by the system" (v41). ✔
- **FR-002** — "не предлагают … список кандидатов". ✔
- **FR-003** — manual pick allowed (v102); mark on the choice page from OD-002 a). ✔
- **FR-005** — BR-062 / Epic 4 line 1464: mark with its date and «состав курса — на <дату
  последнего успешного чтения>», on screen and in the export. The date of the mark is `AbsentSince`,
  as for the course-list mark (line 1150 «не виден … с датой»). No-date wording follows OD-001 a)
  and reads like US-060 OD-002 a) («Дані не оновлювалися» without a date). ✔
- **FR-005.5** — BR-062 names only the two marks; a skip alone is not a mark (VR-001). Disclosed as
  I-1. ✔
- **FR-006** — NFR-073; existing keys `DataMarks.Course.NotInClassroomSince`,
  `DataMarks.Course.NotVisibleSince`, `DataMarks.Header.NotInClassroomSince` exist with the quoted
  Ukrainian values. ✔
- **§7** — read-only (BR-026: viewing and export work; linking blocked), no new audit (SC-11), no
  outbound (SC-13), no Google call (SC-7). ✔
- **§1.1 code facts** spot-checked: header rows of both workbooks (Course | name, table at row
  index 5 / 8), `GetMeetCodesQuery.Candidates` reused by `GetMeetCodeCourseChoiceQuery`,
  `DataMarkRules` exists. ✔

No invented rule, no contradiction.

## Story Coverage

AC-001…AC-008 of the Story are all present with unchanged meaning. Derived: AC-009 (OD-001 a),
AC-010 (OD-002 a), AC-011 (I-1). Out-of-scope matches the Story and adds only I-2 exclusions. ✔

## Consistency

- US-060: wording reused (I-4); export placement mirrors the journal export (I-5); undated wording
  aligned with US-060 OD-002 a). ✔
- Permission matrix §2: Admin and Dean as today (BR-002); no new cell. ✔
- Read-only mode, localization, audit list: unchanged and correctly stated. ✔
- BR-083 ("not a course" codes are not scored) — untouched; FR-001.4 keeps it. ✔

## Open Items

`trebovaniya.md` §7 has no open item this Story depends on (item 28 concerns `call_ended` field
names, which this Story does not read). OD-001 and OD-002 are resolved a) by the Owner 2026-10-27.
No new Open Decision is needed. ✔

## Interpretations

| # | Choice | In digest §2 |
|---|---|---|
| I-1 | A course only skipped by sync gets no Meet line | yes |
| I-2 | Mark shown only on reports and the course-choice page | yes |
| I-3 | Mark is current state; cleared mark → candidate again, line gone | yes |
| I-4 | Reused wording; new capitalised "not visible" header text | yes |
| I-5 | Export line in the third cell of the Course header row | **no — stated in §1 as fact** (m-1) |
| I-6 | Marked courses removed before the decision; gap measured to next unmarked | yes |
| — | Mark and roster joined into one line by «; » (FR-005.1); the Story lists them as two texts | no (m-2) |
| — | The current course on the choice page also shows its mark (FR-003.2) | yes, implicitly (§1 "рядом с его названием") |

## Digest Fidelity

Every digest statement traces to the Specification; the open-questions section is accurate; the
language is plain Russian. The example line in §1 shows the exact composed text. Two choices are
told as facts instead of being offered for confirmation (m-1, m-2) — both low-stakes layout choices
following US-060 precedent, visible to the Owner in §1, hence Minor rather than Major.

## Findings

- **m-1 (Minor)** — digest §1 "В выгрузке строка стоит в третьей ячейке строки «Курс» шапки" vs
  spec I-5: an interpretation is told as a fact and is missing from digest §2. Checked against the
  spec-verifier rule "every interpretation is named in digest section 2". Low stakes (same placement
  as US-060's journal export, nothing moves in the printed tables).
- **m-2 (Minor)** — spec FR-005.1 joins the mark and the roster into one line with «; »; the Story
  AC-001/AC-002 and BR-062 name two texts without saying one line or two. Not listed as an
  interpretation; the digest shows the joined line in its example, so the Owner sees it, but not as
  a choice.

## Verdict

**PASS** — 0 Critical, 0 Major, 2 Minor (m-1, m-2). Ready for `HUMAN_SPEC_APPROVAL`.
