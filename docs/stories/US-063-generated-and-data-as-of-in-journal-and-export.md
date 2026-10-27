---
id: US-063
epic: EPIC-3
title: «Сформирован» and «Данные из Google на» in the journal, the report and the Excel export
slug: generated-and-data-as-of-in-journal-and-export
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-10-27 from trebovaniya.md v103 (`5cea77b`,
# §4 Epic 3; derived rule BR-084 in `bcc5447`). Item 1 of the v103 agenda. The
# data exists since US-058 (`Course.LastCompleteReadAt`). Before the pilot.
---

# User Story

As a **Dean**

I want the journal and the report to tell me when they were built and as of when
the course's data was read from Google — on screen in the header, in an exported
file in a note that is not printed

So that I can tell whether a page or a file I am holding is stale, and the paper
academic journal printed from the export carries no service lines.

---

# Business Value

An exported file is a snapshot: a grade a teacher changes afterwards is not marked
anywhere, because the previous value is not kept (mirror, v83). Today only the two
Meet exports carry a generation time; the journal, the report and the blank carry
neither it nor the moment the data was read. The Dean cannot tell an old file from
a fresh one, nor whether a course's data was last read an hour or a week ago.

---

# Scope

**In scope (BR-084):**

- **two values:**
  - «Сформирован: <дата, время>» — the moment the cells are built for, the same
    instant "due" is judged against (BR-056); for an export, the moment of export;
  - «Данные из Google на: <дата, время>» — the course's last successful read
    (`Course.LastCompleteReadAt`, US-058);
- both in the school's time zone (`SchoolTimeZone`, NFR-074), labels in the
  user's language (NFR-073);
- **on screen:** both lines in the header of the journal and of the report;
- **in the Excel export** — the journal / report by template and the blanks built
  from it («Бланк для групових занять») — both lines in a **note on the
  course-name cell**, never in a printed cell; the header rows and the printed
  layout stay as they are;
- visible and exported in read-only mode (export works there, BR-026); nothing is
  written to the database;
- tests with synthetic data and a synthetic clock (TC-4).

**Out of scope:**

- the Meet exports — they already carry their generation time (US-044), and BR-084
  covers the journal and the report only;
- a "grade changed" mark — the previous value is not kept (BR-059, mirror);
- the course list and the course card (BR-084 names the journal and the report);
- Word export (not in the code; US-029).

---

# Acceptance Criteria

## AC-001 The journal and the report header show both lines

**Given** a course last read successfully at R1, the school time zone
Europe/Kyiv, and the clock at G1

**When** the Dean opens its journal and its report

**Then** each header shows «Сформирован: G1» and «Данные из Google на: R1», both
in Kyiv time.

## AC-002 "Generated" is the instant "due" is judged against

**Given** an item due between two page loads

**When** the Dean opens the journal before and after the due moment

**Then** the cell's state and «Сформирован» agree each time: the line shows the
instant the cells were built for.

## AC-003 The export carries both lines in a note on the course-name cell

**Given** the data of AC-001, the export at G2

**When** the Dean exports the journal by template and the «Бланк для групових
занять»

**Then** the course-name cell of each file has a note with «Сформирован: G2» and
«Данные из Google на: R1»; no printed cell contains either line; the cells and the
header rows are otherwise the same as before this Story.

## AC-004 A note already on the course-name cell is kept

**Given** a course whose course-name cell already carries a note in the export

**When** the Dean exports it

**Then** the note holds that text and both lines.

## AC-005 The labels follow the user's language

**Given** a user with English chosen

**When** they open the journal and export it

**Then** the header lines and the note are in English; dates and times in the
school's time zone; course names as Google returned them.

## AC-006 Unknown last read

**Given** a course with no last successful read recorded (`LastCompleteReadAt`
null)

**When** the Dean opens its journal and exports it

**Then** the line reads «Данные из Google на: невідомо» (English: unknown), on
screen and in the note alike (OD-001).

## AC-007 Read-only mode

**Given** read-only mode

**When** the Dean opens the journal and exports it

**Then** both lines are on screen and in the note, and nothing is written to the
database.

---

# Open Decisions

All resolved by the Owner on 2026-10-27 before activation (option a) each).

## OD-001 «Данные из Google на» when the last read is not known

`Course.LastCompleteReadAt` exists only since US-058; a course not read completely
since then — an unchanged archived course, a course skipped ever since — has none.

- **a) (recommended)** — show the line with «невідомо» / «unknown»: the line is
  always there, so its absence is never mistaken for fresh data; the same answer
  as US-060 OD-002 («данные не обновлялись» without a date).
- b) — leave the line out for such a course.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

## OD-002 Which instant is «Данные из Google на» for an unchanged archived course

Incremental synchronization (US-018) does not re-read an archived course whose
`updateTime` has not changed; it only sees it on the list (`LastListedAt`). Its
last complete read can therefore be months old although Google reported "no
change" this morning.

- **a) (recommended)** — the last complete read, as v103 says literally («время
  последнего успешного чтения курса»), and the same field US-060 uses for «данные
  не обновлялись с»: an old date on a closed course is honest — that is when its
  data was last actually read.
- b) — the later of the last complete read and the last listing that confirmed the
  course unchanged — closer to "the data is current", but it reads more into v103
  than it says and would need a sentence in a new requirements version first.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

## OD-003 The course-name cell of a blank with several copies

The «Бланк для групових занять» can repeat the `{{course}}` marker (several copies,
US-043).

- **a) (recommended)** — the note goes on every course-name cell: whichever copy
  or sheet the Dean looks at, the note is there; it is not printed anyway.
- b) — only on the first course-name cell of the file.

**Resolved 2026-10-27 by the Owner: a)** («все как рекомендуешь»).

---

# Notes

- **Data already stored:** `Course.LastCompleteReadAt` (US-058, FR-008) is already
  read into `JournalCourseRecord` for US-060's «данные не обновлялись с». No schema
  change expected — DB_DESIGN likely NOT_APPLICABLE; API_DESIGN NOT_APPLICABLE
  unless the export contract changes (it should not).
- **Code today:** header — `ReportWorkbookMapper.HeaderBlock` (~174), no
  generation time; notes — `Infrastructure/Export/ExportMarks.cs` (`cell.Note` →
  Excel comment, used by US-060 for «вибув(-ла)»); blank — `GroupLessonsBlankMapper`
  → `ClosedXmlBlankFormRenderer` (`{{course}}` → `BlankFormMarker.Course`); the
  Meet generation time — `MeetMeetingWorkbookMapper.cs:42` as a precedent for the
  format.
- **"Generated" and the clock:** the instant must be the one the report is built
  for, taken once, not a second `now` read when the header is rendered — otherwise
  AC-002 can disagree at a boundary.
- **Restrained tone** (BR-047): plain header lines, no emphasis.
- No new NuGet package, no new requirements version.
