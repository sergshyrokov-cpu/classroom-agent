---
id: US-070
epic: EPIC-2
title: Search in the teacher and Meet code lists, shared matching rules, pickers that narrow as you type
slug: search-in-lists-and-pickers
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-02 from the Owner's decisions on search.
# Aligned with trebovaniya.md v107 (section 1) and v108 (Epic 2 "Поиск", section 5 hint),
# BR-087, NFR-075. Before Deans start work in the pilot.
---

# User Story

As an **Admin or Dean**

I want to find a teacher, a Meet code or a course by typing part of what I remember, in the
list I am working with and in every long picker

So that I do not page through a list or scroll a dropdown of hundreds of rows.

---

# Business Value

Finding information easily is a main product goal (v107). The pilot school has about 300
teachers. Today the teacher list and Meet codes have no search and browser search sees only the
page shown; the journal, report and Meet report forms start from a plain dropdown of every course.

---

# Scope

**In scope:**

1. **Shared matching rules** (BR-087): words in any order, each as part of a word; any case;
   apostrophe variants equal; hyphens in Meet codes ignored; no typo tolerance. The existing
   course list search moves to these rules.
2. **Teacher list search field** «Пошук за ім'ям або email» / "Search by name or email": surname,
   given name, email, over the whole list, combined with the status filter.
3. **Meet codes search field** «Пошук за кодом, організатором або курсом» / "Search by code, organizer
   or course": code, organizer email, name and section of the linked course. It runs over all three
   lists, and the counters next to the list names show the matches.
4. **The search stays** across pages, after a row action (link, confirm, mark as not a course) and
   after a language switch.
5. **Pickers narrow as you type:** the course field of the journal, template report and Meet reports,
   the teacher filter on the course list, and the full course list on the Meet code course-choice
   page. Without scripts each works as it does today.
6. **Browser does not remember the fields:** `autocomplete="off"` on the three list search fields.
7. **Hint on the three paged lists** becomes the first half of the §5 v108 text: «У цьому списку — поле
   «<назва поля>» нижче (Ctrl+F бачить лише показані рядки)» / "In this list: the "<field name>" field
   below (Ctrl+F sees only the rows shown)". US-071 adds the second half with the header field.

**Out of scope (US-071):** the header search field, student search, the ordinary page hint.
Removing final periods from existing texts waits for the base version (v109).

**Also out of scope:** typo tolerance, a database extension for search, any new role right, any change
to the erasure screen's search, a student card.

---

# Acceptance Criteria

## AC-001 Matching rules

**Given** a list holding "Іваненко Петро", a course named with one apostrophe variant and a code
`abc-defg-hij`

**When** the search text is `петро іван`, the name with another apostrophe variant, or `abcdefg`

**Then** each is found; a text matching none of the record's words finds nothing; a misspelled word
finds nothing.

## AC-002 Course list search follows the same rules

**Given** the course list

**When** a search is made

**Then** AC-001 holds there, and the status and teacher filters still combine with it by AND.

## AC-003 Teacher list search

**Given** more teachers than fit on one page

**When** a teacher on another page is searched by surname, given name or email

**Then** the teacher is shown, with the status filter applied, and the total counts the matches only.

## AC-004 Meet code search over three lists

**Given** matching codes in different lists

**When** the search runs on any of the lists

**Then** the counters show the matches of each list and each list shows only its matches.

## AC-005 What must not be found

**Given** a meeting whose organizer was erased or excluded (Epic 10, US-038 I-9)

**When** the Meet codes are searched by that email

**Then** that email finds nothing, through the same path the screen uses to show organizers.

## AC-006 The search survives work on the list

**Given** a searched teacher or Meet code list on page 2

**When** the user changes page, acts on a row, or switches language

**Then** the same search is shown afterwards, validated, never the raw text.

## AC-007 Validation

**Given** a search text over the course list's length limit, repeated, or with a control character

**When** the page opens

**Then** it behaves as the course list does today: the message from translation files, nothing read
with the rejected text, the text not echoed and not logged.

## AC-008 Access, read-only mode, nothing outbound

**Given** the search on each list

**When** used by an allowed and a forbidden role, and in read-only mode

**Then** allowed roles get results, forbidden roles are refused, read-only mode does not block it; no
Google port is called; no audit event and no log line carries the text.

## AC-009 Pickers narrow as you type

**Given** each picker of Scope item 5

**When** the user types, with scripts on

**Then** only rows matching BR-087 remain and the choice submits as before. With scripts off, the
picker works as before. A person checks this on a computer and a phone.

## AC-010 Fields and hint

**Given** the three lists, in Ukrainian and English

**When** opened

**Then** each field's label comes from translation files and has `autocomplete="off"`; the hint names
the field by its label and ends without a period (NFR-078).

---

# Open Decisions

None open. The Owner decided the behaviour on 2026-11-02 (v108). Technical choices, such as where
apostrophes are folded, belong to the Specification and designs.

---

# Notes

- **Code today:** course search `src/ClassroomAgent.Infrastructure/Persistence/CourseListSource.cs:110`,
  validation `Application/UseCases/GetCourseListQuery.cs:140`; teacher list
  `Application/UseCases/GetTeacherListQuery.cs`; Meet codes `Application/UseCases/GetMeetCodesQuery.cs`,
  organizer rule `Infrastructure/Persistence/MeetCodesReadSource.cs`; pickers at
  `Views/Journal/Index.cshtml:34`, `Views/Report/Index.cshtml:44`, `Views/MeetReports/Index.cshtml:34`,
  `Views/Courses/Index.cshtml:32`, `Views/MeetCodes/CourseChoice.cshtml`.
- **Collation:** `course_name_uk` is deterministic, so apostrophe variants differ today. Reuse the
  explicit collation rather than the database default, whose case folding depends on how the cluster was
  created.
- **Meet code in a query string:** US-032 spec I-14 keeps a code in the path, never in a query string,
  and the switcher returns to the validated query. A searched text may hold a code or an email, so the
  Specification must reconcile this: `?q=` is allowed by v108, and the switcher memo
  [US-039 INFO-1] applies (`docs/reviews/security/US-039-security-review.md`).
- **Server check:** the application logs no query string; check any reverse proxy on the pilot server.
- **Scripts:** own same-host script only, no CSP change (SC-15, as in US-069); `AGENTS.md` allows no new
  package without an Open Decision.
- Depends on nothing unbuilt. US-071 reuses the matching rules.
