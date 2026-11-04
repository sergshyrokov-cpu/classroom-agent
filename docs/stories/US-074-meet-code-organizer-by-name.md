---
id: US-074
epic: EPIC-4
title: Meet code lists show the organizer by name and find the code by that name
slug: meet-code-organizer-by-name
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-04 from rehearsal finding P-15 and the Owner's decisions.
# Aligned with trebovaniya.md v110 (Epic 2, Epic 4, BR-087). Before Deans start work in the pilot.
---

# User Story

As a **Dean or Admin**

I want the Meet code lists to show a code's organizer by name, and to find the code by that name

So that I can recognise the teacher when I decide which course a code belongs to.

---

# Business Value

Linking unassigned codes is daily Dean work in the first weeks. An email alone is hard to recognise, as the
Owner found on the real domain (P-15). The Meet reports already show the name with the email; the codes page
is the one place left with the email alone.

---

# Scope

**In scope:**

1. **All three lists of the Meet codes page** (unassigned, linked, "not a course"): the organizer column shows
   the name as Google gave it with the email beside it, as `_MeetPerson` does in the Meet reports; the email
   alone when the name is not known. Several organizers of one code each follow this rule.
2. **The name comes from Classroom participants already loaded**, by the rule the Meet reports use for the same
   email. There is no new call to Google and no new stored data.
3. **Search by name.** The field on the Meet codes page finds a code by its organizer's name as well as by the
   email, under the shared rules of BR-087, in all three lists, with their counters. The header's search field
   finds codes the same way, since it searches them "as the code list does".

**Out of scope:** the Meet reports and their exports, which already show the name; the wording of the search
field, which already says "organizer"; the course-choice page; former names, which search does not find
(BR-085); any correction of a name (Google data is a mirror).

---

# Acceptance Criteria

## AC-001 Name with email

**Given** a code whose organizer is a known participant with a name, in each of the three lists

**When** a Dean or an Admin opens the list

**Then** the row shows the name with the email beside it, encoded by Razor and never translated.

## AC-002 Email alone

**Given** an organizer who is not a known participant, or one with no name

**Then** the row shows the email alone.

## AC-003 Several organizers

**Given** a code with several organizers, some with a name and some without

**Then** each appears by its own rule, once.

## AC-004 An erased or excluded organizer stays invisible

**Given** a meeting whose organizer email was cleared by an erasure or an exclusion (Epic 10)

**Then** no name or email of that person is shown, and search by their former name or email does not find the
code through them.

## AC-005 Search by name in the list

**Given** the Meet codes page

**When** the user searches for part of an organizer's name, including words in any order, a different case or
another apostrophe variant

**Then** the code is found in its list, the three counters show the matches, the filter stays across pages and
after a row action; search by email, code and course keeps working.

## AC-006 Search by name in the header

**Given** the header's search field

**When** the user searches for an organizer's name

**Then** the "Meet codes" group lists the code, with the link to its list, under the 20-result rule; the group's
link to the whole list carries the search.

## AC-007 Read-only and rights

**Given** read-only mode

**Then** the lists and search work and call no Google API; roles allowed and forbidden are as before, each proved
by a test (TC-5); the search text reaches no log and no audit event.

## AC-008 A full page stays one set of queries

**Given** a page of codes

**Then** names are loaded for the page at once, not one query per row; a search stays usable at the expected
volume of about 1000 students and 300 teachers.

---

# Open Decisions

None open. The Owner decided on 2026-11-04: name with the email beside it; all three lists; search by name both
on the page and in the header.

---

# Notes

- **Reference:** `MeetCodesReadSource.LoadOrganizersAsync` and `CodesByOrganizer` read only `OrganizerEmail` today;
  `MeetReportSource` shows how a name is chosen for one email; the view is `MeetCodes/Index.cshtml`.
- **For the Specification:** how an email is compared between `MeetSession` and `ClassroomParticipant`, so the
  list and the search cannot disagree about who the organizer is.
- No new endpoint or schema is expected, though the API design records the changed response items. If the database
  design finds that search by name needs an index, it says so with its reason.
