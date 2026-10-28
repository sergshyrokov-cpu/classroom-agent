---
artifact_type: specification
story: US-066
version: 1
status: APPROVED
created_at: 2026-10-28T18:18:37Z
updated_at: 2026-10-28T18:30:28Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-066-namesakes-shown-with-email.md
    version: null
  - path: trebovaniya.md
    version: 104
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
supersedes: null
---

# US-066 Specification — Students with the same name are shown with their email in the journal, the report and the export

Delegation: none. The spec writer read `trebovaniya.md` §4 Epic 3 lines 1320–1340 (name variant,
page switcher, v104 namesakes), the v104 changelog, §7, and BR-086 in `docs/product/business-rules.md`,
and checked the code facts in §1.1 in the files named there.

## 1. Overview

Two students of one course who show the same name are two identical rows today on the journal page,
on the report page, in the report's Excel export and in the blanks built from the report. v104
(BR-086) adds the student's email next to the name — in the same cell — on exactly those rows. Every
other row, and the printed journal of a course without namesakes, stays as it is.

### 1.1 Code facts this Specification must not contradict

- Journal page (US-025): `GetJournalQuery` labels a row by `JournalCellRule.Label(fullName, email)` —
  the Google full name, else the **full** email (`JournalNameKind.Email`), else unnamed. Rows are
  ordered unnamed-last, then by label with `StringComparer.Create(uiCulture, ignoreCase: false)`,
  then by participant id. Row DTO `JournalRow(DisplayName, NameKind, Cells, Left)`; the row carries
  no email today. View: `Views/Journal/Index.cshtml` line 114 (name, then `<br/>` and the "left" mark).
- Report (US-042): `ReportBuilder` labels every person by `ReportPersonNameRule.Label(surname,
  givenName, email, nameSource)` — the profile name «Прізвище Ім'я» (or one part), else the email part
  before `@` (`ReportNameKind.EmailLocalPart`), else unnamed; one name source for the whole report
  (page switcher, else the template's setting). Students are ordered as in the journal with the same
  case-sensitive culture comparer. Row DTO `GradingRow(PersonName Student, Cells, Left)`,
  `PersonName(DisplayName, NameKind)`. View: `Views/Report/Index.cshtml` line 182.
- Export by template and blanks: both are built from the same `Report`. The student name cell is
  `ReportWorkbookMapper.StudentCell` → `StudentText`, with the "left" mark in the cell's note (US-060);
  `GroupLessonsBlankMapper` calls the same `StudentCell` for every copy of the blank.
- The member records both builders read already carry the email (`m.Email`). No new data is read.

## 2. Business Goal

A Dean puts a grade, a remark or a signature against the right person — on screen and on the printed
journal — without opening the course card to compare emails (Story; BR-086).

## 3. Business Flow

1. A Dean or Admin opens a journal or a report, or exports the report or a blank built from it.
2. The system builds the student rows as today (name variant, order, cells, "left" mark).
3. Among the student rows of that one journal or report, it finds the rows whose displayed name
   coincides with another row's (FR-002).
4. Those rows carry the student's email; the page shows it next to the name, the file writes it into
   the name cell (FR-003, FR-004). All other rows are unchanged.

## 4. Functional Requirements

**FR-001 Scope of the rule.** The rule applies to the student rows of: (1) the journal page; (2) the
report page; (3) the Excel export of the report by template; (4) every blank built from the report.
It does not apply to teachers («ПІБ Викладача», the "Lesson topics" teachers) — OD-002 a) — nor to
Meet reports or the course card.

**FR-002 When a row is a namesake.**
1. The comparison runs over the student rows of one journal or one report, as built — the rows of
   the chosen period, after the name variant of that report (page switcher or template setting).
   For a blank it runs over the whole report the blank is built from, not over one copy of the blank.
2. A row's *displayed name* is the label the row already gets: journal — full name or email;
   report — profile name or email part before `@`. Unnamed rows (no label) never coincide (Story,
   out of scope).
3. Two displayed names coincide when they are equal under the culture comparer of the request's UI
   language — the one the rows are ordered by — **with letter case ignored**
   (`StringComparer.Create(uiCulture, ignoreCase: true)`; AC-005). Names are compared as stored: no
   trimming, no other normalisation (the mirror, v83).
4. A row is a *namesake* when at least one other row of the same journal or report has a coinciding
   displayed name.
5. Journal rows whose displayed name **is** the full email (`JournalNameKind.Email`) never get an
   additional email: the address is already shown.

**FR-003 What a namesake row carries.**
1. A namesake row carries the student's stored email when it has one (non-empty); a namesake without
   an email carries none and is shown with the name only (AC-008).
2. A row that is not a namesake carries no email.
3. The email is Google data: shown as stored, never translated, never corrected.
4. The DTOs carry the email as an optional field on the student row (journal `JournalRow`, report
   student `PersonName` or `GradingRow`); the exact shape is API_DESIGN's. The decision "namesake or
   not" is made in `Application` (the builders), not in the view or the workbook writer (AD-3).

**FR-004 How the email is written** (OD-001 a).
1. On screen and in the file, in the same cell as the name, on the same line, in parentheses after the
   name, separated by one space: `<name> (<email>)`, e.g. «Петренко Іван (ivan.petrenko@school.example)».
2. The form is the same in Ukrainian and English. It is a fixed composition of data, like the
   «<course> — <section>» join in `ReportWorkbookMapper.CourseText`; no new translatable text is added
   (NFR-073 is unaffected: no new label, the email is data).
3. On screen the "left" mark keeps its place after the name cell content (today a line break and the
   mark): `<name> (<email>)`, then the mark (AC-009).
4. In the export and in blanks the name cell's text becomes `<name> (<email>)`; the cell's note with
   the "left" mark (US-060) is unchanged; no other cell, row, column, height, style or sheet of the
   layout changes (AC-003, AC-004, AC-009).

**FR-005 Order unchanged.** Rows keep today's order — by displayed name (case-sensitive culture
comparer, as today), then by participant id. The email does not take part in ordering (Story, out of
scope).

**FR-006 Read-only mode.** Viewing and export work in read-only mode (BR-026); this Story adds no
write and nothing to restrict.

## 5. Acceptance Criteria

Carried from the Story with their ids.

- **AC-001** Journal page: two students with full name «Іван Петренко» and one «Олена Коваль» → both
  «Іван Петренко» rows show their email next to the name; «Олена Коваль» shows the name only.
- **AC-002** Report page with names from the profile: both «Петренко Іван» rows show their email; the
  other row does not.
- **AC-003** Excel export of that report: the two namesakes' name cells hold `<name> (<email>)`, the
  other holds the name only; the sheet layout is otherwise unchanged.
- **AC-004** A blank built from that report: the namesakes' name cells as in AC-003.
- **AC-005** «Петренко Іван» and «петренко іван» in one report → both rows show their email.
- **AC-006** No coinciding displayed names → no row shows an email on the journal page, the report
  page, the export or a blank; the file is the same as before this Story.
- **AC-007** Same profile name, different emails; report switched to names from the email → no row
  shows an additional email; switching back to the profile shows it again.
- **AC-008** Two namesakes, one without an email in Google → the one with an email shows it; the
  other is shown with the name only.
- **AC-009** Two namesakes, one a leaver → on screen the leaver's row shows the email and «вибув
  <дата>»; in the file the email is in the name cell and the "left" text stays in the cell's note.

## 6. Validation Rules

- No new input. The page switcher and export parameters are validated as today (US-042, US-043).
- **VR-001** The email added to a row is taken from the stored participant record only; an empty or
  null email adds nothing (FR-003.1). No format check: the address is Google data (mirror, v83).
- **VR-002** On screen the email is HTML-encoded like every Google value (Razor default encoding;
  `PersonName.displayName` in the US-042 OpenAPI is already "HTML-encoded").

## 7. Security Requirements

- Authorization is unchanged: the journal, report and export endpoints keep their policies (Admin
  and Dean, SC-4 deny by default). No new endpoint.
- The email is personal data already shown to the same roles on the course card and in the report's
  email variant; it reaches no new role and no new destination (SC-13: data stays in the installation
  and the file the user downloads).
- The pages keep their `no-store` caching (SC-14, as today).
- No email is written to a log or an audit event by this Story (SC-10, SC-11); the export's existing
  audit event is unchanged and carries no personal data.

## 8. Error Handling

No new error. A missing email is not an error (FR-003.1). Existing errors of the journal, report and
export are unchanged.

## 9. Non-Functional Requirements

- The coincidence check is one pass over the rows already built (grouping by the comparer) — no new
  query, no change to the existing per-request cost (NFR performance as today).
- NFR-073: no new user-visible label (FR-004.2).

## 10. Out of Scope

- Teachers («ПІБ Викладача», "Lesson topics" teachers) — OD-002 a).
- Meet reports and the course card — they already show the email.
- Unnamed rows («без імені») — never namesakes, shown as today.
- Order of rows — unchanged (FR-005).
- Any new requirements version, NuGet package or database change.

## 11. Interpretations to confirm at HUMAN_SPEC_APPROVAL

- **I-1 Case ignored, ordering not changed** (FR-002.3, FR-005). The Story says namesakes are judged
  "with the comparison the rows are already ordered by", but that comparison is case-sensitive, while
  AC-005 requires «Петренко Іван» and «петренко іван» to coincide. The Specification follows AC-005:
  the same culture comparer with case ignored for coincidence; ordering stays as today.
- **I-2 A journal row named by its email gets no second email** (FR-002.5): it would read
  «a@b (a@b)».
- **I-3 An email-part name gets the full email when it coincides** (FR-002.2, Story notes): with
  names from the email, two addresses from different domains with the same part before `@` (or a
  profile name equal to another's email part) are namesakes and show the full address. v104 says no
  coincidences occur in this variant; the rule is the same and simply does not fire in the usual case
  (AC-007).
- **I-4 Blanks judge over the whole report** (FR-002.1): two namesakes in different copies of a blank
  still show their email, so a copy reads the same as the report it comes from.
- **I-5 The format is not translated** (FR-004.2): «<name> (<email>)» is identical in both languages
  and needs no translation entry.

## 12. Open Decisions

Both Story Open Decisions are resolved (`docs/decisions/US-066-open-decisions.md`): OD-001 a) — email
in parentheses on the same line (FR-004); OD-002 a) — students only (FR-001). `trebovaniya.md` §7
holds no open item this Story depends on. No new Open Decision.

## 13. Traceability

| AC | Requirements |
|---|---|
| AC-001 | FR-001(1), FR-002, FR-003, FR-004.1 |
| AC-002 | FR-001(2), FR-002, FR-003, FR-004.1 |
| AC-003 | FR-001(3), FR-004.4 |
| AC-004 | FR-001(4), FR-002.1, FR-004.4 |
| AC-005 | FR-002.3 |
| AC-006 | FR-002.4, FR-003.2 |
| AC-007 | FR-002.1, FR-002.2 |
| AC-008 | FR-003.1, VR-001 |
| AC-009 | FR-004.3, FR-004.4 |
