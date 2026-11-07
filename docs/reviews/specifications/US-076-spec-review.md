---
artifact_type: specification_review
story: US-076
version: 2
status: APPROVED
created_at: 2026-11-07T06:41:35Z
updated_at: 2026-11-07T06:54:49Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-076-page-help.md
    version: null
  - path: trebovaniya.md
    version: 112
  - path: docs/specifications/US-076-spec.md
    version: 2
  - path: docs/specifications/US-076-spec-digest.ru.md
    version: 2
  - path: docs/decisions/US-076-open-decisions.md
    version: 1
supersedes: 1
---

# US-076 Specification Review (v2)

## Summary

Second review, after the loop-back of review v1 (CHANGES_REQUIRED, F-1 Major). Specification v2 and
digest v2 fix F-1, m-1, m-2 and m-3 of v1. No Critical or Major finding remains. One Minor finding
(capitalised mark labels at sentence start). Verdict **PASS**.

Nothing was delegated: the reviewer read the Story, Specification v2, digest v2, Open Decisions v1,
`trebovaniya.md` section 5 «Справка на странице» (lines 2112–2135), BR-068 in
`docs/product/business-rules.md`, both `SharedResource` resx files and the views
`Journal/Index.cshtml`, `MeetReports/Index.cshtml`, `MeetReports/Meeting.cshtml` directly.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-076-page-help.md` | — |
| Requirements | `trebovaniya.md` | 112 |
| Specification | `docs/specifications/US-076-spec.md` | 2 |
| Digest | `docs/specifications/US-076-spec-digest.ru.md` | 2 |
| Open Decisions | `docs/decisions/US-076-open-decisions.md` | 1 |

Inputs are current: the Specification names Story and `trebovaniya.md` v112; the digest names
Specification v2.

## Fidelity

FR-001…FR-009 restate section 5 «Справка на странице» (Где / Что в ней / Как) and NFR-079; FR-010 restates
Story Scope item 5. The mechanism of FR-004 (view-data page id read by the layout, `<details>`/`<summary>`)
follows the Story Notes hint and needs no script or CSP change (SC-15). No invented rule found.

Help texts of section 4.1, re-checked line by line:

- **Journal line 2 (v1 F-1).** Now names what the journal shows: «вибув(-ла)» beside a student
  (`DataMarks.Left.On`, `Journal/Index.cshtml:118`; BR-068 "left <last-seen date>"), «немає в Classroom»
  under one coursework title (`DataMarks.Item.NotInClassroom`; BR-045), and the course line above the table
  «Курсу немає в Classroom з …» / «Дані не оновлювалися з …» (`DataMarks.Header.NotInClassroomSince`,
  `DataMarks.Header.NotUpdatedSince`; BR-048, BR-049). Correct. **F-1 fixed.**
- **Meet codes line 2 (v1 m-1).** «здебільшого збігаються» / "mostly match" describes BR-065 without
  numbers and without over-claiming. **Fixed.**
- **Meet reports line 3, meeting line 3 (v1 m-2).** The list names the column «Не в списку курсу»
  (`MeetReports.Meetings.NotOnCourseList`, `Index.cshtml:133`); the meeting page names the per-person mark
  «не в списку курсу» (`MeetReports.Mark.NotOnCourseList`, `Meeting.cshtml:88`); «Інші учасники» is limited
  to accounts the program does not know from Classroom (BR-064; row at `Meeting.cshtml:93`, column at
  `Index.cshtml:134`). **Fixed.**
- Other lines unchanged from v1 and still faithful (BR-060, BR-061, BR-065, BR-083, BR-026, Epic 3 v84).

UI labels quoted in section 4.1 were compared with both resx files: «Непризначені»/"Unassigned",
«Прив'язати до іншого курсу»/"Link to another course", «Не в списку курсу»/"Not on the course list",
«не в списку курсу»/"not on the course list", «Інші учасники»/"Other participants", «немає в
Classroom»/"not in Classroom", «Курсу немає в Classroom з …»/"Course not in Classroom since …", «Дані не
оновлювалися з …»/"Data not updated since …", «Звіт»/"Report", «Копіювати»/"Copy", «Експорт в
Excel»/"Export to Excel", «Академічний журнал»/"Academic journal", «Стан даних»/"Data health", «Шаблони
звітів»/"Report templates", «Зустрічі Meet»/"Meet meetings", «не курс»/"not a course" — all match,
except the capitalisation noted in m-1.

## Story Coverage

AC-001…AC-007 are each kept and refined (section 5); Scope item 5 is FR-010 with no AC, listed as a
supporting change. Out-of-scope list matches the Story. Nothing beyond scope.

## Consistency

Permission matrix (section 2): all five policies admit Admin and Dean, no new cell. Read-only mode
(BR-026): the help writes nothing. Localization (NFR-073): every string from `SharedResource` resx.
NFR-078: multi-sentence lines keep their periods, as section 5 says. Audit (SC-11): no action added.
NFR-075: the search hint stays unchanged; its position relative to the opened help is I-2.

## Open Items

Section 7 item 26 is wider and correctly left open; v111/v112 decided this Story's case. OD-001 resolved
by the Owner and carried. No open item is silently resolved.

## Interpretations

I-1 (journal marks as shown, not as section 5 words them), I-2 (help opens above the search hint), I-3
(the journal page has no export), I-4 («строка» = short paragraph), I-5 (no links), I-6 (two Meet texts),
I-7 (export named on the template list). No further unnamed interpretation found. The digest also names
the numberless wording of automatic linking and the «Не в списку курсу»/«Інші учасники» distinction.

## Digest Fidelity

Digest v2 traces to Specification v2; section 2 lists I-1…I-7 (v1 m-3 **fixed**), section 3 states there
are no open decisions and retells spec m-1/m-2 with recommendations. Plain Russian, nothing softened.

## Findings

### m-1 (Minor) — the «вибув(-ла)» mark capitalised in journal line 2

Specification 4.1 journal line 2 begins «"Вибув(-ла)" з датою…» / ""Left" with a date…", while the UI
label is lowercase «вибув(-ла)» / "left" (`DataMarks.Left.On`); line 3 already uses lowercase. Section 4.1
requires quoted labels to equal the current values. Sentence-start capitalisation does not mislead a Dean,
so this is advisory: either keep it as a sentence start or reword so the label stays lowercase
(e.g. «Позначка «вибув(-ла)» з датою…»). Can be settled at API design when the keys are fixed.

The Specification's own findings (spec m-1: section 5 wording and «четырёх страницах»; spec m-2:
data-health page hint not covered by tests) remain accurate and stay with their stated homes.

## Verdict

**PASS** — no Critical or Major finding. Ready for `HUMAN_SPEC_APPROVAL`.
