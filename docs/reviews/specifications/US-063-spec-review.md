---
artifact_type: specification_review
story: US-063
version: 1
status: APPROVED
created_at: 2026-10-28T17:00:12Z
updated_at: 2026-10-28T17:00:12Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-063-generated-and-data-as-of-in-journal-and-export.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/specifications/US-063-spec.md
    version: 1
  - path: docs/specifications/US-063-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-063-open-decisions.md
    version: 1
supersedes: null
verdict: PASS
---

# US-063 Specification Review

## Summary

PASS. The Specification renders `trebovaniya.md` §4 Epic 3 «Когда сформирован и на какой момент
данные» (v103) and BR-084 faithfully, covers all seven Story criteria, applies the three resolved
Open Decisions and names its seven interpretations in the digest. Two Minor findings, no blocking
one. Nothing was delegated: the reviewer read the cited requirement text and the code facts itself.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | docs/stories/US-063-generated-and-data-as-of-in-journal-and-export.md | — |
| Requirements | trebovaniya.md | 104 |
| Specification | docs/specifications/US-063-spec.md | 1 |
| Digest | docs/specifications/US-063-spec-digest.ru.md | 1 (inputs: spec v1 — current) |
| Open Decisions | docs/decisions/US-063-open-decisions.md | 1 |

## Fidelity

Checked against `trebovaniya.md` lines 1363–1377 and BR-084 (`business-rules.md` 487–499):

- "Generated" = the instant the cells are built for, the same "due" instant (v57 / BR-056) — FR-001.1.
- "Data from Google as of" = the course's last successful read — FR-002.1, VR-002 (`LastCompleteReadAt`,
  OD-002 a).
- School time zone and user's language — FR-006.1/3 (NFR-074, NFR-073).
- On screen in the journal and report header — FR-003.
- Excel export (journal, report by template, blanks from it) in a note on the course-name cell, not a
  printed cell — FR-004, FR-005. The code has one journal export (`ExportJournalCommand`) with two
  layouts (`ExportLayout.Program`, `GroupLessonsBlank`); both are covered.
- No "grade changed" mark (mirror) — §10.

Code facts in §1.1 spot-checked and accurate: `GetJournalQuery.cs:31`, `ReportBuilder.cs:80`,
`GetReportQuery.cs:35`, `ExportJournalCommand.cs:145` clock reads; `GroupLessonsBlankMapper` course
cell with US-060 note (lines 82–98, grading copies only); `ReportWorkbookMapper.HeaderBlock` called for
both sheets (101, 140); `IReportTexts` backed by `SharedResource` (`LocalizedReportTexts`); screen
date-time format `ShortDatePattern + " HH:mm"`.

No invented rule, no contradiction.

## Story Coverage

AC-001…AC-007 present with unchanged meaning; AC-004 is correctly narrowed to the blank form (the
program layout's course-name cell carries no note today — US-060's line is a separate printed cell).
Derived AC-008 (OD-003 a, I-4) and AC-009 (OD-002 a) stay in scope. Out-of-scope list matches the Story
plus the v104 known limitation.

## Consistency

- Permission matrix: no change; Admin and Dean see the same (BR-002).
- Read-only mode (BR-026): viewing and export keep working; the only write stays the permitted audit
  row (I-6).
- Localization (NFR-073): labels in resx; course and template text untranslated.
- Audit (SC-11): no new event. SC-13: no new destination.
- US-060: its header line and note are kept and placed first (I-5); OD-001 a mirrors US-060 OD-002.

## Open Items

No `trebovaniya.md` §7 item is touched. OD-001…OD-003 resolved a) by the Owner 2026-10-27 and applied.

## Interpretations

1. I-1 export "generated" = build's B, not the audit time.
2. I-2 date + HH:mm, no seconds.
3. I-3 journal lines above the table (no course heading exists).
4. I-4 program layout: note on both sheets.
5. I-5 existing note first.
6. I-6 read-only "nothing written" = no new write; audit row stays.
7. I-7 Ukrainian wording of the Russian labels.

All seven are in digest §2. No unnamed interpretation found.

## Digest Fidelity

Every digest statement traces to the Specification; the Open Decisions are reported as resolved, the
interpretations as needing confirmation; plain Russian. See m-2 for one omission of consequence.

## Findings

- **m-1 (Minor)** — spec FR-006.3 says "the requirements word the labels in Russian («Сформирован»,
  «Данные из Google на», «невідомо»)". `trebovaniya.md` has no "unknown" case; «невідомо» comes from
  the Story's OD-001 a). Wrong attribution only; the behaviour is sourced correctly in FR-002.3.
- **m-2 (Minor)** — for a course with US-060's notice «Дані не оновлювалися з <дата>» the Dean will see
  two lines carrying the same date (that notice and «Дані з Google на: <дата, время>»), on screen and in
  the blank's note. Both are required (US-060, BR-084), so this is not a defect, but neither the
  Specification nor the digest says so; the Owner may want to judge the look before approval.

## Verdict

PASS — no Critical or Major findings; m-1 and m-2 are advisory.
