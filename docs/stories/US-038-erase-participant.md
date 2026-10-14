---
id: US-038
epic: EPIC-10
title: Erase all data of one participant on request
slug: erase-participant
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v93.
---

# User Story

As an **Admin** of a school

I want to erase all data of one person — a student or a teacher — when the
school receives a written request to do so

So that the school can honour the request itself, at once, without asking the
Owner to work in its database, and the person does not come back with the next
synchronization.

---

# Business Value

`trebovaniya.md` v93 (§2, §3, §4 Epic 10, §5, §9): the school is the controller
of personal data of minors. Until v93 an erasure request was an Owner's manual
procedure that also required the school to delete the person from Google first.
v93 makes it an Admin function with an exclusion that holds while the person
remains in Google.

---

# Scope

**In scope:**

- an Admin-only page to find a participant (by email or part of the name)
  across all courses and open the erasure confirmation for them;
- the confirmation: what will be erased (the person's courses, the number of
  submissions, of grades, of Meet participations, of meetings they organized),
  a warning that files already exported cannot be erased by the program, and a
  mandatory "erasure is irreversible" mark;
- erasure in one transaction: the `ClassroomParticipant`, all its
  `CourseMembership` rows, its `Submission` rows, the `MeetParticipation` rows
  with its email; clearing its mentions in other records — the organizer email
  of a `MeetSession`, the owner of a `Course`;
- the `ErasedParticipant` exclusion created in the same transaction: the
  Google user id and a salted fingerprint of the email (salt per installation),
  plus the erasure date;
- synchronization does not create a participant with an excluded Google user
  id, nor store memberships or submissions for it, nor record it as a course
  owner; the Meet load stores no participation whose email fingerprint matches
  an exclusion and stores a meeting organized by such a person without the
  organizer email;
- lifting an exclusion: the Admin enters an email; if its fingerprint matches,
  the exclusion is removed; otherwise the Admin is told there is no exclusion
  for that address;
- audit events for both actions with internal ids and counts only, and one
  `Information` log line per erasure with the participant's internal id and the
  counts;
- refusal in read-only mode and for the Dean.

**Out of scope:**

- any write to Google (all scopes are read-only);
- erasing audit rows (they carry no personal data), backups (they age out in
  30 days, `trebovaniya.md` §9), files already exported;
- the re-application of erasures after a restore — an organizational step of
  the school and the Owner (`trebovaniya.md` §9, BR-077), not code;
- the Owner's manual procedure for read-only mode (DC-8);
- erasing an `AppUser` (Admin or Dean) — accounts have their own retention
  (`trebovaniya.md` §5).

---

# Acceptance Criteria

## AC-001 The Admin sees what will be erased

**Given** a participant who is a student in two courses, with submissions,
grades and Meet participations

**When** the Admin finds them on the erasure page and opens the confirmation

**Then** the page shows the person's name and email, their courses, the number
of submissions, of grades, of Meet participations and of meetings they
organized, and a warning that exported files are outside the program; nothing
is erased yet.

## AC-002 Erasure removes the person's data in one transaction

**Given** the confirmation of AC-001 with the "erasure is irreversible" mark set

**When** the Admin confirms

**Then** the participant, all their memberships, submissions and Meet
participations are gone; meetings they organized remain without the organizer
email; courses they owned remain without the owner; other people's data is
unchanged; and if any step fails, nothing is erased.

## AC-003 The mark is required

**Given** the confirmation without the "erasure is irreversible" mark

**When** the Admin confirms

**Then** the erasure is refused with a validation message and nothing changes.

## AC-004 Synchronization does not bring the person back

**Given** an erased participant who is still in a course in Google

**When** synchronization runs

**Then** no participant with their Google user id is created and none of their
memberships, submissions or course ownership is stored; everyone else in the
course is synchronized as usual.

## AC-005 The Meet load does not bring the person back

**Given** an erased participant who still attends and organizes meetings in the
school's domain

**When** the Meet load runs, including the 3 days it re-reads

**Then** none of their participations is stored, and a meeting they organized
is stored without the organizer email; other participants are stored as usual.

## AC-006 The exclusion holds no readable personal data

**Given** an erasure has been made

**When** the exclusion is inspected in the database

**Then** it holds the Google user id, the email fingerprint and the erasure
date only — no name and no email; and the same email in two installations gives
different fingerprints.

## AC-007 The Admin lifts an exclusion

**Given** an exclusion for a person who has returned to the school

**When** the Admin enters that person's email (in any letter case)

**Then** the exclusion is removed and the next synchronization loads the person
as new; **and when** the Admin enters an email with no exclusion, the page says
so and nothing changes.

## AC-008 Only the Admin, and not in read-only mode

**Given** a Dean signed in, or an installation in read-only mode

**When** the erasure page, an erasure or the lifting of an exclusion is
requested

**Then** the Dean gets `403` and does not see the page in the menu; in
read-only mode both actions are refused by the Application layer with `409`
and nothing changes.

## AC-009 Audit and log carry no personal data

**Given** an erasure and a lifted exclusion

**When** they complete

**Then** each writes one audit event — the erasure with the participant's
internal id and the count of erased records of each kind, the lifting with the
exclusion's id — and the erasure writes one `Information` log line with the
participant's internal id and the counts; neither contains a name, an email or
a Google user id.

---

# Open Decisions

None. Decided by the Owner on 2026-10-14 before the Story was written and
recorded in `trebovaniya.md` v93: the Admin erases (1a); an exclusion with the
Google user id and a salted email fingerprint (2a, 2b-a); students and teachers
(3a); not in read-only mode (4a); a confirmation with counts and an
"irreversible" mark (5a).

---

# Notes

- **Where the salt lives** is a design matter (DB_DESIGN): generated by the
  installation, never a configuration value or a secret the Owner sets, never
  shown or logged.
- **Fingerprint input** is the normalized email (`WorkspaceConnection`
  normalization), so letter case does not matter (AC-007).
- **A running synchronization or Meet load** may hold data read before the
  erasure; the exclusion must be checked where the data is written, so an
  in-flight run cannot re-insert the person (Specification).
- **The participant's courses on the confirmation page** are names of courses,
  which are not the person's data; the page itself shows the person's name and
  email to the Admin, as other pages already do.
- **Journal, Meet reports and exports** built after the erasure simply no longer
  contain the person; no change to them is expected beyond that.
- Retention purge does not touch `ErasedParticipant` (`trebovaniya.md` §3, v93).
