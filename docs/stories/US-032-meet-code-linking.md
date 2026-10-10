---
id: US-032
epic: EPIC-4
title: Link meeting codes to courses
slug: meet-code-linking
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# Aligned with trebovaniya.md v88.
# Scope decided with the Owner on 2026-10-10 in discovery (OD-001 … OD-008 below).
---

# User Story

As a **Dean** or an **Admin**

I want every Meet meeting code the installation has loaded to be linked to its
Classroom course — automatically where the match is clear, by me where it is
not — and codes that belong to no course to be set aside

So that the Meet reports of US-033 show each course exactly its own lessons.

---

# Business Value

`trebovaniya.md` §4 Epic 4, §3 `MeetingCodeLink`, BR-065, BR-083: the Classroom
API does not return a course's Meet link (checked on the prototype's domain,
v23), so the only way to tell which meetings belong to which course is the
meeting code. In most courses the teacher creates the Meet link in the
Classroom course and it stays the same for every lesson, so one link per code
usually covers a whole school year. US-031 already stores the meetings; without
this Story none of them belongs to a course and no Meet report is possible.

---

# Scope

## In scope

- **Automatic linking** (§4 Epic 4, BR-065) — a step of synchronization that
  runs **right after the Meet step** of the same run, for every code that is
  neither linked nor marked "not a course":
  - for each candidate course, the share of distinct domain accounts across all
    meetings of the code (organizers excluded) who were students of the course
    on the date of at least one of their meetings of that code;
  - a course is a candidate only if at least one organizer of the code's
    meetings was a teacher of the course on the date of their meeting;
  - the code is linked when the best share is at least the **upper threshold**
    and the next best is lower by at least the **gap**; otherwise it stays
    unlinked and the share is recomputed on the next Meet load;
  - an automatic link takes effect at once and is never revised by the system;
  - the step does not run when the Meet step of that run failed.
- **Thresholds** — installation configuration set by the Owner at deployment,
  default **60 %** and **30 percentage points**; not shown or edited in the UI.
  Invalid values stop the installation from starting, like any other invalid
  required setting (DC-3).
- **A "Meet meetings" page** in the Google Workspace section, open to Dean and
  Admin, with three lists:
  - **Unassigned** — codes neither linked nor marked: code, organizer(s), first
    and last meeting date, number of meetings, number of participants, and the
    candidate courses with their share in percent. Codes with no candidate are
    shown too, marked "no candidates", at the end of the list.
    Actions: pick a course; mark "not a course".
  - **Linked** — code, course, how the link was made (automatically / by whom),
    confirmed or not (by whom and when). Actions: confirm; re-link to another
    course; mark "not a course".
  - **Not a course** (v88, BR-083) — code, who marked it and when. Action: pick a
    course (removes the mark).
- **Picking a course** (for an unassigned or marked code, and when re-linking)
  offers **any loaded course**, candidates first with their share.
- **Re-linking** moves all meetings of the code to the other course at once —
  meetings reach a course only through the link (PC-12).
- **Confirmation** is optional and only records that a person checked the link.
- **"Not a course" mark** (v88, BR-083): the code's meetings count in no
  course; automatic linking never touches it again; it is removed only by
  picking a course.
- **A course may have several codes** (a reset Classroom link gets a new one);
  a code belongs to at most one course.
- **Audit** (SC-11): automatic link (actor `system`), picking a course for an
  unassigned code, confirmation, re-linking, setting and removing the "not a
  course" mark. Each row records the meeting code and the course id(s) involved
  (old and new on re-linking) — no personal data.
- **Read-only mode** (AD-6, BR-026): every action above is refused in
  `Application`; the lists stay viewable. No linking step runs, because
  synchronization as a whole is refused.
- **Roles**: Dean and Admin both, for every action (§2 matrix). Every endpoint
  has an allowed-role and a forbidden-role test (TC-5).
- **Retention purge extended** (PC-11, US-037, US-031):
  - a course's **last activity** includes the start of every meeting reached
    through its codes;
  - an expiring course takes its `MeetingCodeLink` rows and the meetings reached
    through them, with their participations;
  - a leaver's expiry deletes their `MeetParticipation` rows (matched by email,
    PC-12) in meetings reached through the course's codes;
  - a "not a course" mark is deleted once its code has no meeting left;
  - the purge's audit row gains the count of links removed.
- **Translations** for the page, actions, statuses and errors (NFR-073); codes,
  emails and course names appear as Google holds them.
- **The EF Core migration** for `MeetingCodeLink` and any new audit columns
  (PC-2).

## Out of scope

- Any report or screen showing meetings of a course — US-033.
- Changing the thresholds from the UI, or showing them.
- Unlinking a code back to "unassigned" — the "not a course" mark covers the
  case (v88); there is no plain unlink.
- Distinguishing staff meetings automatically.
- Using a course's Meet link from Classroom — the API does not return it (v23).

---

# Acceptance Criteria

## AC-001 An unambiguous code is linked automatically

**Given** a code whose meetings were organized by a teacher of course A, with a
share of 85 % for A and 20 % for B

**When** synchronization runs and the Meet step succeeds

**Then** the code is linked to A as an automatic, unconfirmed link, leaves the
unassigned list, and an audit row with actor `system` records the code and A.

## AC-002 Share and candidates follow the roster on the meeting date

**Then** a participant counts toward a course only if they were its student on
the date of at least one of their meetings of that code; organizers are not
counted; a course none of whose teachers organized a meeting of the code on its
date is not a candidate.

## AC-003 An ambiguous code waits for a person

**Given** shares of 70 % and 55 %, or a best share below 60 %

**Then** the code stays unassigned and appears with its candidates and shares.

## AC-004 The share is recomputed while the code is unassigned

**Given** an ambiguous code that becomes unambiguous after new meetings load

**Then** the next run links it automatically.

## AC-005 An automatic link is never revised by the system

**Given** a code linked automatically to A, after which the shares change in
favour of B

**Then** the link stays on A.

## AC-006 Thresholds come from configuration

**Given** thresholds set in installation configuration

**Then** linking uses them; with none set it uses 60 % and 30 points; invalid
values stop the installation from starting.

## AC-007 No linking after a failed Meet step

**Given** a run whose Meet step fails

**Then** no automatic link is created in that run.

## AC-008 Picking a course

**Given** an unassigned code

**When** a Dean or Admin picks any loaded course

**Then** the code is linked to it, all its meetings belong to that course, and
the action is audited.

## AC-009 Confirming

**When** a Dean or Admin confirms an automatic link

**Then** the link shows who confirmed it and when, and the action is audited;
nothing else changes.

## AC-010 Re-linking

**When** a Dean or Admin re-links a code from A to B

**Then** all its meetings move to B at once, and the audit row records A and B.

## AC-011 Marking "not a course"

**Given** an unassigned code, or a code linked automatically or by a person

**When** a Dean or Admin marks it "not a course"

**Then** its meetings belong to no course, it appears only in the "Not a
course" list with who marked it and when, automatic linking never links it
again, and the action is audited.

## AC-012 Removing the mark

**When** a Dean or Admin picks a course for a marked code

**Then** the mark is gone, the code is linked to that course, and the action is
audited.

## AC-013 Codes with no candidate

**Given** a code with no candidate course

**Then** it is listed as unassigned, marked "no candidates", after the codes
that have candidates.

## AC-014 Roles

**Then** a Dean and an Admin can view the page and perform every action; an
unauthenticated request is refused. Both v1 roles are allowed here, so the
forbidden case of TC-5 is the unauthenticated request.

## AC-015 Read-only mode

**Given** an installation in read-only mode (any reason, BR-025)

**Then** the lists are viewable and every action is refused in `Application`,
with no change and no audit row of a link change.

## AC-016 Retention purge

**Given** an expired course with linked codes, a leaver past N years in a kept
course, a recent meeting linked to an otherwise old course, and a marked code
whose last meeting expired

**When** the purge runs

**Then** the expired course's links, meetings and participations are deleted;
the leaver's participations in the course's meetings are deleted; the recent
linked meeting keeps its course alive; the orphaned mark is deleted; the purge
audit row records the counts.

## AC-017 Bilingual

**Then** the page, actions, statuses and errors are in the UI language; codes,
emails and course names appear as Google holds them.

## AC-018 Tests never reach Google

Every test uses substituted Google ports and synthetic data (TC-4).

---

# Open Decisions

All resolved by the Owner on 2026-10-10, before activation:

- **OD-001** Scope — (a) one Story: automatic linking, the lists, picking,
  confirming, re-linking, the "not a course" mark and the purge extension.
- **OD-002** Where linked codes are managed — (a) one "Meet meetings" page with
  separate lists; with v88 the lists are Unassigned, Linked and Not a course.
- **OD-003** Plain unlink — superseded by `trebovaniya.md` v88: no plain unlink;
  the "not a course" mark is added instead (BR-083).
- **OD-004** Picking a course — (a) any loaded course, candidates first.
- **OD-005** Codes with no candidate — (a) shown in the unassigned list, marked
  "no candidates", at the end.
- **OD-006** Thresholds — (a) installation configuration, default 60 % / 30
  points, not in the UI.
- **OD-007** When linking runs — (a) a step right after the Meet step of the same
  run; skipped when the Meet step fails; never in read-only mode.
- **OD-008** Audit content — (a) the meeting code and the course id(s); old and
  new on re-linking.

---

# Notes

- **Two courses with the same group and the same teacher** (for example
  Mathematics and Physics of one class) give near-equal shares, so neither code
  links automatically; the Dean picks each course once, and every later meeting
  of that code follows. This is expected, not a defect: nothing else tells the
  courses apart.
- Known limitation (v41): a non-Classroom link (Calendar, a permanent link)
  reused in several courses can be linked to one course only.
- Known limitation (v55): a course with no Classroom activity for more than N
  years is not loaded into a new installation, so its meetings stay unassigned.
- How the "not a course" mark is stored (a status of `MeetingCodeLink`, or a
  link without a course) is a DB_DESIGN decision.
- The thresholds are a new installation setting; the Specification adds them to
  DC-3's configuration list.
