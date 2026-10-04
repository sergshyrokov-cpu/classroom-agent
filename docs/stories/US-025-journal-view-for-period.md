---
id: US-025
epic: EPIC-3
title: Journal view for a period
slug: journal-view-for-period
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v82.
# OD-001 … OD-007 were all resolved by the Owner on 2026-10-04, before
# activation.
---

# User Story

As a **Dean** or an **Admin**

I want to open the journal of one course for a period I choose — every student
of that period against every assignment and material dated in it, each cell
showing the state of that student's work

So that I can see how the teaching process actually went in a course without
opening Classroom course by course, and so that later Stories have one journal
to export and to print through templates.

---

# Business Value

`trebovaniya.md` §4 Epic 3 opens with "Формирование журнала: студент ×
(задание/материал с датой) × клетка, за выбранный период", and §2 gives
"Просмотр и экспорт журнала успеваемости" to both Admin and Dean. §2 also says
why the Dean owns it: "Журнал — его рабочий инструмент".

Everything the journal is built from is already in the installation's database:
courses and rosters with first/last-seen dates (US-014), coursework, materials
and submissions with grades (US-015). Nothing a Dean can see shows any of it
yet — the Dean's only screen today is the home page with the "Synchronize"
button (US-019).

This Story is the root of Epic 3: US-026 … US-030 (kinds of course items,
templates, Excel and Word export, the assembled printed journal) all build on
the journal it produces.

It also discharges an obligation US-015 handed over in writing (US-015 OD-005,
Specification VR-004): a submission whose state the program did not recognise is
shown as its own thing, never as «не сдано» and never as a grade.

---

# Scope

## In scope

- A **journal page** for Admin and Dean: choose a course and a period
  (from-date, to-date), see the journal. How the course is chosen — OD-001.
- **Columns** — every coursework and material of the course whose date falls in
  the period. The date follows the cascade `scheduledTime` → `dueDate` →
  `updateTime` → `creationTime` (BR-052); day boundaries are the school's time
  zone (BR-053, NFR-074). Columns are ordered by that date; each header shows
  the title, the date, and for graded work the maximum points.
- **Rows** — the students of the period (BR-051): course members with role
  `student` who were on the roster for at least part of the period by their
  first-seen / last-seen dates, plus anyone with a submission to a column of the
  period. Students who left before the period or came after it are not shown.
  Teachers are never rows.
- **Cells** computed by BR-056 / BR-057 / BR-058 against the moment the journal
  is built (v57), including:
  - assigned grade (raw points out of the maximum) wins over the state;
  - "turned in, not graded", "returned without a grade", "not turned in" once
    due, "not due yet", "not turned in, no due date";
  - the "late" mark from Google's `late` flag, added to any state;
  - "not assigned" for coursework with no submission for that student
    (assigned to some students only) — never "not turned in";
  - ungraded work: never a grade or "not graded" — "turned in", "returned",
    "not turned in" once due, "not due yet", "not turned in, no due date", with
    the same "late" mark (BR-052, BR-056; OD-002);
  - a material: its own column, marked as a material, with empty cells
    (OD-002);
  - a draft grade shown next to the state, marked "draft", in the full view
    only (BR-057, OD-003);
  - an **unrecognised** submission state shown as its own marked state carrying
    the raw value Google sent (US-015 OD-005);
  - `NEW` counts as not turned in, then by due date, like `CREATED`
    (`trebovaniya.md` v82, OD-004).
- **A "full / short" switch** on the page (OD-003): the full view also shows
  draft grades and the last turn-in date (BR-058); the short view shows only
  assigned grades (BR-057).
- **Defaults and order:** the period defaults to the current calendar month,
  editable (OD-006); rows by full name in the alphabetical order of the UI
  language, students without a name last with a translated label (OD-005);
  columns by date, then by title (OD-007).
- **Read-only mode:** viewing keeps working (§2, BR-026); the page writes
  nothing and calls no Google API.
- **Authorization:** Admin and Dean only (§2); deny by default (SC-4). An
  allowed-role and a forbidden-role test for every endpoint (TC-5).
- **Validation** of the course id and the dates (§8, SC-10): an unknown course,
  a malformed date, or a from-date after the to-date is answered with a message,
  not an error page.
- Ukrainian and English texts for the page, every cell state and every message
  (NFR-073). Data from Google — course name, item titles, student names — is
  never translated.

## Out of scope

- Exporting the journal in any form — US-028 (Excel), US-029 (Word). Viewing is
  not an audited action; exporting is (SC-11), and that row is written by the
  export Stories.
- Report templates, conversion of points to the school's grading scale, the
  short paper layout — US-027, US-030. Conversion is the template's job (§4
  Epic 3): this Story always shows raw points.
- A course list with filters and search — US-020; a course card with its
  roster — US-021. This Story chooses the course from a plain drop-down of all
  synchronized courses by name (OD-001).
- A journal of several courses at once.
- Grade history and rubric grades — never stored (BR-059, Epic 12).
- Any change to synchronization or to what is stored (US-014, US-015).

---

# Acceptance Criteria

## AC-001 The journal of a course for a period

**Given** a signed-in Dean or Admin, a synchronized course, and a period

**When** they open the journal for that course and period

**Then** the columns are exactly the coursework and materials dated in the
period (school time zone, BR-052 date cascade), ordered by date; the rows are
exactly the students of the period (BR-051); each cell shows one state by
BR-056.

## AC-002 Who is in the journal

**Given** students who were on the roster for the whole period, for part of it,
who left before it, who came after it, and an off-roster person with a
submission in the period

**Then** the first, second and last appear; the one who left before and the one
who came after do not; teachers do not appear as rows.

## AC-003 Cell states of graded work

**Given** graded work and submissions in each state BR-056 names

**Then** each cell shows: the assigned grade as raw points out of the maximum
whenever one exists, regardless of state; otherwise "turned in, not graded" for
`TURNED_IN` / `STUDENT_EDITED_AFTER_TURN_IN`, "returned without a grade" for
`RETURNED`, and for `CREATED` / `RECLAIMED_BY_STUDENT` "not turned in" if the due
date has passed at the moment of building, "not due yet" if it has not, "not
turned in, no due date" if there is none; any of them with the "late" mark when
Google set `late`.

## AC-004 Work not assigned to a student

**Given** coursework with no submission row for a student of the period

**Then** that cell shows "not assigned", never "not turned in".

## AC-005 Draft grades

**Given** a submission with a draft grade and no assigned grade

**Then** the full view shows the state with the draft grade next to it, marked
"draft"; the short view shows the state only (OD-003).

## AC-006 An unrecognised state is never hidden

**Given** a submission whose stored state is the "unrecognised" marker

**Then** its cell shows a distinct "unrecognised state" with the raw value
Google sent — never "not turned in" and never a grade.

## AC-007 Ungraded work and materials

**Given** ungraded work (no maximum points) and a material dated in the period

**Then** cells of the ungraded work never show a grade or "not graded" — only
"turned in", "returned", "not turned in" once due, "not due yet" or "not turned
in, no due date", with the "late" mark when set; the material has its own column
marked as a material, with empty cells (BR-052, BR-056, OD-002).

## AC-008 A participant without a name

**Given** a student known only from submissions, whose name and email are absent
(US-015 OD-006)

**Then** the row shows a translated "student without a name" label, after the
named students, and the page does not fail (OD-005).

## AC-009 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** the journal opens as usual, nothing is written and no Google API is
called.

## AC-010 Only Admin and Dean

**Given** an anonymous request

**Then** it is refused; every endpoint has an allowed-role and a forbidden-role
test (TC-5).

## AC-011 Invalid input

**Given** an unknown course id, a malformed date, or a from-date after the
to-date

**Then** the user sees a message naming the problem, nothing else is computed,
and the rejected value is not written to the log (SC-10).

## AC-012 The page is bilingual

**Given** a user whose UI language is Ukrainian or English

**Then** every label and every cell state appears in that language; course
names, item titles and student names appear as Google gave them (NFR-073).

## AC-013 Tests never reach Google

Every test builds its data synthetically in PostgreSQL (TC-2, TC-4); no Google
port is called by the journal.

---

# Open Decisions

All seven were resolved by the Owner on 2026-10-04, before activation (each as
the recommended option). Resolutions are kept next to the question.

## OD-001 How the course is chosen

There is no course list yet (US-020).

Options: (a) the journal page itself has a simple drop-down of all synchronized
courses by name, with no filters or search — US-020 later adds the real list
and links into the journal; (b) do US-020 first and open the
journal only from it; (c) the course is typed as an id.

**Resolution:** (a).

## OD-002 Where the US-025 / US-026 boundary lies

BR-056 defines the cells of graded work, ungraded work and materials in one
rule, and the epic map gives US-026 "distinguish graded work, ungraded work and
materials".

Options: (a) US-025 implements all three kinds — the cell is one computation and
testing it in two Stories duplicates work; US-026 is then closed in the catalog
as covered by US-025; (b) US-025 shows graded work only, and
ungraded-work and material columns appear with US-026.

**Resolution:** (a) — US-026 is removed from the catalog as covered by this Story.

## OD-003 Full and short view on screen

BR-057: the short journal shows only assigned grades; the full journal also
shows draft grades. Layout belongs to templates (US-027).

Options: (a) one switch on the page, "full / short", that only changes whether
draft grades (and the last turn-in date) are shown; (b) the
full view only — the short form arrives with templates.

**Resolution:** (a).

## OD-004 What the `NEW` state means in a cell

US-015 stores six recognised states, including `NEW`, which the Classroom API
documents but BR-056 does not mention (US-015 Specification VR-004). A cell rule
is a business rule and cannot be invented in a Story.

Options: (a) before activation, a new version of `trebovaniya.md` adds `NEW` to
§4 Epic 3 as "not turned in, then by due date", like `CREATED`;
(b) the same, but `NEW` is shown as the "unrecognised state" until checked on a
live domain; (c) leave it open and block the Story.

**Resolution:** (a) — done in `trebovaniya.md` v82 (§4 Epic 3) and BR-056.

## OD-005 A student without a name

A participant created from submissions alone has no name or email (US-015
OD-006).

Options: (a) the row shows a translated label "student without a name" and is
ordered after the named students; (b) the row also shows the
Google user id so two such students can be told apart; (c) such rows are hidden.

**Resolution:** (a).

## OD-006 The default period

Options: (a) the current calendar month, editable; (b) no
default — both dates must be entered; (c) the last 30 days.

**Resolution:** (a).

## OD-007 Row and column order

Options: (a) rows by full name in the alphabetical order of the UI language,
unnamed last; columns by date, then by title; (b) the order
Classroom shows.

**Resolution:** (a).

---

# Notes

- The page is a `GET` with the course id and the dates in the query string — no
  personal data travels in the URL (US-039 INFO-1).
- A course that is archived in Google, and coursework deleted in Google after
  it was imported (US-015 OD-004), are shown as any other — the Specification
  confirms.
- The size of a large course (hundreds of students × a year of items) is a
  performance question for the Specification and the DB design: one query plan,
  no per-cell query.
- Admin and Dean see the same journal; there is no per-course restriction for a
  Dean in v1 (§2).
