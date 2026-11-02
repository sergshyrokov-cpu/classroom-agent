---
id: US-071
epic: EPIC-2
title: One search field in the header, including students with their courses
slug: search-field-in-the-header
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-02 from the Owner's decisions on search.
# Aligned with trebovaniya.md v107 (section 1), v108 (Epic 2 "Поиск", section 5), v109 (section 8),
# BR-088, NFR-075, NFR-078, SC-14. Before Deans start work in the pilot.
---

# User Story

As an **Admin or Dean**

I want one search field at the top of every page that finds students, teachers, courses and Meet
codes

So that I can find something without first knowing which screen holds it, and can answer "which
courses is this student in?".

---

# Business Value

Today a Dean has to know the right screen before searching, and cannot find a student at all
without knowing the course. The Owner named search one of the program's main advantages (v107).

---

# Scope

**In scope:**

1. **«Пошук» / "Search" field in the header** of every installation page after sign-in, except the
   forced temporary password change and the error page; `autocomplete="off"`.
2. **Results page** (GET, `?q=`), from 2 characters, with the BR-087 matching rules. Groups in a fixed
   order, empty ones hidden: students, teachers, courses, Meet codes.
3. **Students:** surname, given name, email of anyone who is a student in at least one course, departed
   included. A row shows name, email and courses with links to their cards; a course the student has
   left carries the «вибув(-ла)» mark (BR-068).
4. **Teachers:** name, email, link to the course list filtered by that teacher.
5. **Courses:** name, section, status, link to the card.
6. **Meet codes:** as in US-070; a row shows the code, its list and a link.
7. **Limits:** up to 20 per group, alphabetical; above that «показано 20 з N, уточніть запит» / "showing
   20 of N, refine your search"; groups other than students link to the whole list with this search.
8. **Nothing found** and **the browser history warning:** the §4 Epic 2 v108 texts.
9. **Hints:** the final §5 v108 texts on all hint pages, ordinary pages and paged lists.

**Out of scope:** a student card with grades, typo tolerance, search in the Control Plane, any new role
right, export of results, removing final periods from existing texts (by the base version, v109).

---

# Acceptance Criteria

## AC-001 The field is where it should be

**Given** each installation page after sign-in

**When** opened by an Admin or Dean, in Ukrainian and English

**Then** the header holds the field, except on the forced password change and the error page; pages
before sign-in and the Control Plane do not hold it.

## AC-002 Groups and order

**Given** a text matching a student, a teacher, a course and a Meet code

**When** searched

**Then** the four groups appear in the fixed order; with no match in a group it is hidden; a person in
both roles appears in both groups.

## AC-003 A student and their courses

**Given** a student on one roster who has left another course

**When** found

**Then** the row shows both courses with links to their cards, and only the left course carries the
mark, as the journal words it (BR-068).

## AC-004 Limits

**Given** more than 20 matches in a group

**When** searched

**Then** 20 are shown alphabetically with the count line, and the link to the whole list opens it with
this search (not for students).

## AC-005 Too short, invalid, nothing found

**Given** a text of 1 character, an invalid text, and a text matching nothing

**When** searched

**Then** the first two read no data and show a message from translation files, the invalid text is not
echoed; the third repeats the text and shows the v108 reminder.

## AC-006 What must not be found

**Given** an erased or excluded person, and a meeting with such an organizer

**When** searched by their name or email

**Then** nothing is found, by a test on stored rows; a former surname is not found either (BR-085).

## AC-007 Access and logs

**Given** the field and the results page

**When** used by each role, signed out, and in read-only mode

**Then** Admin and Dean get results including in read-only mode; other cases are refused by the
protected endpoint's rules; no Google port is called, and no audit event or log line carries the text.

## AC-008 The warning

**Given** the results page, with or without matches

**When** shown

**Then** the one-line browser history warning is shown in small text from translation files.

## AC-009 Hints

**Given** every page of the §5 hint list, in both languages

**When** opened

**Then** the hint is the v108 text for its kind, without a final period, naming the same labels as the
fields.

## AC-010 Speed on the expected size

**Given** about 1000 students, 300 teachers and their courses, synthetic

**When** the search runs on real PostgreSQL

**Then** the page is usable with no special index; the Specification states what was measured.

---

# Open Decisions

None open. The Owner decided the behaviour on 2026-11-02 (v108): student search as the second Story,
text in the URL with the browser history warning, departed students found and marked, no student card.

---

# Notes

- **Depends on US-070** (matching rules, Meet code search).
- **Personal data in a query string:** this is the first screen with students' names in `?q=`. Raise
  [US-039 INFO-1] at SPECIFICATION and SECURITY_REVIEW: the switcher copies the validated query into the
  page, served only to the same user (decided acceptable in v108). `Referrer-Policy` is same-origin
  (`SecurityHeadersMiddleware.cs:44`), and SC-14 keeps results out of the browser cache but not the address
  out of its history.
- **Erasure screen search** is Admin-only and stays separate (`ErasureController.cs:25`).
- **Folding** must use the explicit collation, as US-070 does, not the default in
  `ParticipantErasureReadSource.cs:26-29`.
- Privacy policy: `trebovaniya.md` section 7 item 30; not part of this Story.
