---
id: US-027
epic: EPIC-3
title: Report templates and the on-screen report
slug: report-templates
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v84.
# Scope decided with the Owner on 2026-10-04 in discovery (questions 1–12);
# OD-001 … OD-003 resolved by the Owner on 2026-10-04, before activation.
---

# User Story

As a **Dean** or an **Admin**

I want a "Reports and templates" section where I keep the school's report
templates — a built-in "Academic journal" and templates of our own — and where I
can see the report a template produces for a course and a period

So that the electronic journal can be turned into the paper journal a school
keeps for a group and a semester, with grades in the school's own scale,
without anyone editing layouts in code.

---

# Business Value

`trebovaniya.md` v84 (§3 `ReportTemplate`, §4 Epic 3, §4 "Направление
развития") makes report templates a section of the program of its own. §2 gives
"Использование шаблонов отчётов" and "Создание и редактирование шаблонов
отчётов" to both Admin and Dean: "Журнал — его рабочий инструмент".

US-025 shows the journal exactly as Classroom holds it — raw points, every
state. A school needs the same data in the shape of its paper academic journal:
grades in the school's scale, one sheet of grades and one of lesson topics.
Nearly every school needs that journal, so it ships with the program as a
built-in template.

The template reads **journal fields**, not the Google mirror's tables. Today the
mirror fills them; after Epics 14–17 the integration of the two data streams
will, and no template has to change (v84, v83 "two data layers").

US-028 (Excel), US-029 (Word) and US-030 (assembled printed journal) all build
files from the report this Story produces.

---

# Scope

## In scope

- **A new UI section "Reports and templates"** (v84), separate from "Google
  Workspace", where the journal of US-025 stays.
- **Templates as sets of settings** (§3 v84), stored by the installation as
  school-own data (second data layer, v83):
  - view: full or short (BR-057);
  - what each cell state of BR-056 is shown as;
  - whether materials are hidden;
  - the grading scale (below);
  - the number of teaching hours per lesson, default 2.
  An Excel layout file is **not** part of this Story — US-028.
- **The built-in template "Academic journal"** (v84), present in every
  installation: short view; two parts —
  - **"Grading" (Оцінювання)**: students × lessons, one column per coursework
    item, the lesson date in the header;
  - **"Lesson topics" (Теми занять)**: one row per coursework item, materials
    excluded, ungraded work included; lesson date = the item's publication date
    (OD-001: `scheduledTime` if set, otherwise `creationTime`); an item belongs to
    the report's period by that same date; topic = the item's title; hours = the template setting; teacher
    names = the course's teachers, comma-separated; "independent work" and
    "teacher's signature" left empty.
  It cannot be changed or deleted; it can be **copied**, and the copy is an
  ordinary template. A later program version may update the built-in template;
  copies are not touched.
- **Templates created in the school**: create (from scratch or as a copy),
  change, delete. Shared by the whole school — any Admin or Dean sees, changes
  and deletes any of them; each records its author. Not bound to courses.
- **The grading scale**: a table of ranges "percent of the item's maximum →
  school grade", set once in the template. One preset — the even 12-point scale
  of OD-002, editable after choosing; any other scale is entered as a range
  table by hand; and the option **"no conversion"** — raw points out of the maximum, as in
  Classroom. How a teacher set up grading in Classroom does not matter: the
  mirror always holds raw points and the maximum (§4 Epic 3).
- **The on-screen report**: choose a template, a course and a period; see the
  report the template produces. Viewing is not exporting.
- **Journal fields**: the report is built from a set of named journal fields
  (student, lesson, date, topic, grade, cell state, teachers…) that today's
  journal computation fills. The Specification names the fields; templates
  never reach the Google mirror's tables directly (v84).
- **Read-only mode** (BR-025, BR-026): viewing templates and reports keeps
  working; creating, copying, changing and deleting a template is refused in
  `Application`, not by hiding UI (AD-6).
- **Audit** (§5 v84, SC-11): creating, changing and deleting a template writes
  an audit row with the template id only. Viewing a report is not audited.
- **Authorization**: Admin and Dean only (§2); deny by default (SC-4); an
  allowed-role and a forbidden-role test for every endpoint (TC-5).
- **Validation** (§8, SC-10) of every template setting (name, marks, scale
  ranges — non-overlapping and covering 0–100 %, hours) and of the report's
  course, template and dates; the rejected payload is never logged.
- **Ukrainian and English** (NFR-073): screens, messages, the built-in
  template's name and its marks are program text and are translated. A name or a
  mark a Dean wrote into a template, and data from Google, are never
  translated.

## Out of scope

- Excel layout, Excel export — US-028; Word — US-029; assembling several
  templates into one printed journal — US-030.
- Filling "independent work", "teacher's signature" and real teaching hours from
  school-own data — Epics 14–16; integration of two streams — Epic 17.
- Any change to the journal page of US-025, to synchronization or to the Google
  mirror.
- Uploading a template file of any kind.
- Per-Dean private templates, binding a template to courses.

---

# Acceptance Criteria

## AC-001 The section and the built-in template

**Given** a freshly installed installation and a signed-in Dean or Admin

**When** they open "Reports and templates"

**Then** the built-in "Academic journal" is listed, and nothing in the program
lets anyone change or delete it.

## AC-002 Copy, create, change, delete

**Given** a signed-in Dean or Admin

**Then** they can copy the built-in template, create a template, change any
template created in the school and delete it; the author is recorded; another
Dean or the Admin sees and can change the same template.

## AC-003 Grading scale

**Given** a template with a scale of ranges and graded work with an assigned
grade

**Then** the report shows the school grade of the range the percent of the
maximum falls in; with "no conversion" it shows raw points out of the maximum;
an overlapping or incomplete range table is refused with a message.

## AC-004 The "Academic journal" report

**Given** a course and a period with graded work, ungraded work and materials

**Then** the "Grading" part has one column per coursework item with its lesson
date, materials excluded, students by BR-051; the "Lesson topics" part has one
row per coursework item with date, title, hours from the template (2 by
default), the course's teachers, and empty "independent work" and signature.

## AC-005 Template settings drive the report

**Given** a copy whose marks, materials setting, view or hours were changed

**Then** the report reflects each change, and the built-in template's report
does not.

## AC-006 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** templates and reports can be viewed; creating, copying, changing and
deleting a template are refused by `Application`; no Google API is called.

## AC-007 Audit

**Given** a template is created, changed or deleted

**Then** one audit row records the action, the actor and the template id — no
template content and no personal data; viewing a report writes none.

## AC-008 Only Admin and Dean

Anonymous requests are refused; every endpoint has an allowed-role and a
forbidden-role test (TC-5).

## AC-009 Invalid input

**Given** an invalid template setting, an unknown template or course, or a bad
period

**Then** the user sees a message naming the problem, nothing is saved, and the
rejected value is not written to the log (SC-10).

## AC-010 Bilingual

**Given** a user whose UI language is Ukrainian or English

**Then** screens, messages, the built-in template's name and marks appear in
that language; text a Dean wrote and data from Google appear as written.

## AC-011 Tests never reach Google

Every test builds data synthetically in PostgreSQL (TC-2, TC-4).

---

# Open Decisions

## OD-001 Which date is the lesson date, and which items fall in the period

The Owner chose the publication date as the lesson date (discovery 7.1). The
journal of US-025 places an item in a period by the BR-052 cascade
`scheduledTime` → `dueDate` → `updateTime` → `creationTime`, where the due date
comes second. An item published on 28 September and due on 2 October is in
October by BR-052 but its lesson date is 28 September.

Options: (a) **(recommended)** in a template report both the lesson date and the
period membership use the publication date (`scheduledTime` if set, otherwise
`creationTime`); the US-025 journal keeps BR-052 — the report is consistent with
itself; (b) the lesson date is the publication date, membership stays BR-052 —
a column dated outside the chosen period can appear; (c) both use BR-052 — the
"lesson date" is then often a due date.

**Resolution:** (a), by the Owner on 2026-10-04; written into `trebovaniya.md`
v84 §4 Epic 3.

## OD-002 The ranges of the preset scales

There is no single official "percent → 12 points" table; the presets must carry
the ranges the school uses (discovery 4.2a). The Owner provides the ranges for
the 12-, 5- and 100-point scales and ECTS.

**Resolution:** by the Owner on 2026-10-04 — (1) the even 12-point table
becomes the preset, the percent rounded to a whole number, 0.5 up: 1 — 0–8 %,
2 — 9–16, 3 — 17–25, 4 — 26–33, 5 — 34–41, 6 — 42–50, 7 — 51–58, 8 — 59–66,
9 — 67–75, 10 — 76–83, 11 — 84–91, 12 — 92–100 %; (2) v1 ships no other preset:
the 5- and 100-point scales and ECTS are entered by hand, and a preset is added
when a school asks for one. Written into `trebovaniya.md` v84 §4 Epic 3.

## OD-003 The built-in template's marks and scale

The built-in template cannot be edited, so its settings are fixed by the
program.

Options: (a) **(recommended)** it shows only grades, converted by the 12-point
preset of OD-002; every other state is empty, except "not assigned", shown as a
dash (BR-056 print rule); (b) the same, with "no conversion" — raw points, so it
is correct for any school, and a school's scale lives only in a copy; (c) other
marks the Owner names.

**Resolution:** (a), by the Owner on 2026-10-04. It depends on OD-002 for the
12-point ranges.

---

# Notes

- Templates are school-own data (v83): they reference nothing in the Google
  mirror and nothing in the mirror references them.
- A template's author is an `AppUser`; the retention purge may delete that
  account after N years (US-037) — the Specification decides what the template
  keeps then.
- The report page is a `GET` with template id, course id and dates in the query
  string — no personal data in the URL (US-039 INFO-1).
- The built-in template's definition lives in the program, not as a row an
  installation could change; how it is stored is for the Specification and DB
  design.
- US-028 must reuse this Story's report, so the Specification keeps report
  building independent of HTML rendering.
