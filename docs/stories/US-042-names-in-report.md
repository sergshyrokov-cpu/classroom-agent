---
id: US-042
epic: EPIC-3
title: Names of students and teachers in a template report
slug: names-in-report
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v86.
# Scope decided with the Owner on 2026-10-05 in discovery (US-028 discussion).
---

# User Story

As a **Dean** or an **Admin**

I want a template to say where the names of students and teachers come from —
"Surname Name" from the Google profile, or the part of the email before `@` —
and to switch between the two on the report page without changing the template

So that the printed journal lists people the way a paper journal does, sorted
by surname, and I can spot a wrong name before I print.

---

# Business Value

`trebovaniya.md` v86 (§3 `ClassroomParticipant` and `ReportTemplate`, §4 Epic 3
"Имена учеников и преподавателей в отчёте"): Google does not know a person's
ПІБ. The profile has no patronymic, the full name is usually "Name Surname", it
may be written in Latin letters, and it can change. A paper academic journal
lists students by surname.

The first version does not check or correct names — Google data is a mirror
(v83). It gives the school two sources to choose from and a quick way to compare
them; the real ПІБ with patronymic comes from the school's own data in Epic 14.

US-028 (Excel), US-029 (Word) and US-030 export the report this Story changes,
so it is delivered before them.

---

# Scope

## In scope

- **Synchronization stores the surname and the given name separately**, as
  Google returns them in the profile, next to the full name already stored
  (§3 v86). Existing participants receive them on the next synchronization;
  until then they have none (below).
- **A template setting "names"** (§3 v86) with two values:
  - **from the Google profile** — "Surname Name"; when the profile has neither,
    the part of the email before `@`;
  - **from the email** — the part of the email before `@`, as it is.
- **One setting for every person in the report**: students and the course's
  teachers ("ПІБ Викладача" on "Lesson topics").
- **The built-in "Academic journal"** uses "from the Google profile"; it stays
  unchangeable; a copy can switch.
- **Ordering**: students in the report are ordered by the name shown — with
  "from the Google profile" that is by surname.
- **A switch on the report page** (§4 Epic 3 v86): the user rebuilds the report
  with the other source without changing the template. It is viewing: nothing
  is saved, no audit row, and it works in read-only mode (BR-026). The chosen
  value travels with the page, so an export started from it (US-028) uses the
  same source and the file never differs from the screen.
- **Templates created before this Story** keep working and use "from the Google
  profile".
- **Template audit, authorization, validation and translations** follow US-027:
  changing the setting is a template change (audit row with the template id
  only); Admin and Dean only; an unknown value of the switch is refused without
  being logged (SC-10); the setting's labels are translated, names never are
  (NFR-073).
- **The EF Core migration** for the new participant columns and the template
  setting (PC-2).

## Out of scope

- Checking names — highlighting Latin letters, a missing surname or similar.
- Correcting names in the program; a wrong name is corrected by the domain
  administrator in Google and arrives with the next synchronization.
- ПІБ with patronymic from the school's own data, matching by Google userId —
  Epic 14 (§4 v86).
- The journal page of US-025 and the Meet pages: they keep showing names as
  today.
- Export of any kind — US-028, US-029, US-030.

---

# Acceptance Criteria

## AC-001 Surname and given name are synchronized

**Given** Google returns a participant whose profile has a surname and a given
name

**When** synchronization runs

**Then** both are stored separately; a later change in Google replaces them on
the next run; a profile without them stores none.

## AC-002 "From the Google profile"

**Given** a template with "from the Google profile"

**Then** each student and each teacher in the report appears as "Surname Name";
a person whose profile has neither appears as the part of their email before
`@`.

## AC-003 "From the email"

**Given** a template with "from the email"

**Then** each student and each teacher appears as the part of their email
before `@`, exactly as written.

## AC-004 Ordering

**Then** students are ordered by the name shown, in the collation of the UI
language; with "from the Google profile" the order is by surname.

## AC-005 The built-in template and its copies

**Given** a fresh installation

**Then** the built-in "Academic journal" uses "from the Google profile" and its
setting cannot be changed; a copy can be switched to "from the email" and back;
a template created before this Story uses "from the Google profile".

## AC-006 The switch on the report page

**Given** the report of any template

**When** the user switches the name source on the report page

**Then** the report is rebuilt with that source; the template is unchanged; no
audit row is written; the chosen source is part of the page's address, so a
reload or an export from that page uses it.

## AC-007 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** the switch works; changing the template setting is refused by
`Application` (US-027); no Google API is called.

## AC-008 Audit of the setting

**Given** a template's name setting is changed and saved

**Then** one audit row records a template change with the template id only.

## AC-009 Only Admin and Dean

Anonymous requests are refused; every new or changed endpoint has an
allowed-role and a forbidden-role test (TC-5).

## AC-010 Invalid input

**Given** an unknown value for the name source in a template form or in the
report page's address

**Then** it is refused with a message, nothing is saved, and the rejected value
is not written to the log (SC-10).

## AC-011 Bilingual

**Then** the setting's labels and the switch are in the UI language; names
appear as Google holds them.

## AC-012 Tests never reach Google

Every test builds data synthetically in PostgreSQL; the Google ports are
substituted (TC-2, TC-4).

---

# Open Decisions

None at authoring. The Specification raises any it finds.

---

# Notes

- The report page is a `GET`; the name source joins template id, course id and
  dates in the query string. It names a source, not a person — no personal data
  in the URL (US-039 INFO-1).
- The part of the email before `@` is personal data like the email itself; it
  is shown only where the email could be shown today.
- "No checks" is the Owner's decision for the first version (2026-10-05); the
  switch is the tool for spotting wrong names.
- The full name stays stored: the US-025 journal and the Meet pages still use
  it.
