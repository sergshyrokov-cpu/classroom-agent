# Epic Map

> **Planning, non-normative.** Candidate Stories are proposals, not
> commitments; `docs/catalog/stories.yaml` owns Story lifecycle status.

Epics follow `trebovaniya.md` section 4. Epic numbering is kept identical to the
requirements document so the two can be cross-read; the ordering below is
delivery order, which is not the same thing.

Candidate Stories are proposals, not commitments. A Story enters the workflow
only once it is written into `docs/stories/` and registered in
`docs/catalog/stories.yaml`.

---

## EPIC-8

Owner and Control Plane

### Goal

Give the Owner control over which schools run the system and with which domain,
and let an installation prove it is legitimate.

### Why first

Nothing else can be configured until an `Installation` exists with a domain and
an approved Admin email: the Admin cannot complete the Workspace connection
without it, and the Admin is the only way the first user appears in a new
installation. This epic is the bootstrap.

### Candidate User Stories

- US-001 Owner first-run setup (Control Plane account)
- US-002 Register an Installation (school, domain, status)
- US-003 Manage AllowedAdmin entries for an Installation
- US-004 Suspend and resume an Installation
- US-005 Installation legitimacy check and grace period
- US-006 Control Plane push on status change
- US-007 Read-only mode enforcement

---

## EPIC-6

Program settings / Admin panel

### Goal

Let an approved Admin connect the installation to the school's Google Workspace
and manage Dean accounts.

### Candidate User Stories

- US-008 Admin sign-in via Google OAuth with AllowedAdmin verification
- US-009 Configure WorkspaceConnection (domain, impersonation user)
- US-010 Connection instructions for the school's super-admin (client ID + scopes)
- US-011 "Check access" diagnostic against Classroom, Directory and Reports APIs
- US-012 Create and manage Dean accounts

---

## EPIC-1

Synchronization with Google Classroom

### Goal

Bring courses, participants, groups, coursework and grades into the local
database, in the background, repeatably.

### Candidate User Stories

- US-013 Background synchronization service
- US-014 Sync courses and rosters
- US-015 Sync coursework and submissions
- US-016 Sync Workspace groups and their membership
- US-017 Retry, backoff and permission-error handling
- US-018 Incremental synchronization
- US-019 Trigger a synchronization from the UI

---

## EPIC-2

Courses, participants, groups

### Goal

Let the Dean see what exists: courses with filters, teachers and their courses,
students including those enrolled through a group.

### Candidate User Stories

- US-020 Course list with filters and search
- US-021 Course detail with roster
- US-022 Teachers and their courses
- US-023 Groups and their membership

---

## EPIC-5

Database management

### Goal

Show the state of the local database and let it be refreshed.

### Candidate User Stories

- US-024 Database statistics and last synchronization view

---

## EPIC-3

Gradebook and printable templates

### Goal

Produce the journal the school actually uses, on paper and in Excel/Word.

### Candidate User Stories

- US-025 Journal view: student × coursework × grade for a period
- US-026 Distinguish graded coursework from ungraded material
- US-027 Configurable report templates (full and short forms)
- US-028 Export a journal to Excel using a school template
- US-029 Export a journal to Word
- US-030 Assemble several templates into one printed journal

---

## EPIC-4

Attendance (Google Meet)

### Goal

Report who was actually present at video lessons, from Google's own records.

### Candidate User Stories

- US-031 Sync Meet attendance from Admin Reports API
- US-032 Link a Meet session to a course
- US-033 Attendance report for a period, by course and by student

---

## EPIC-7 (future)

Teacher role and student access

### Goal

Out of scope for the first version. Recorded so nobody implements it by
accident.

### Deferred

- Teacher accounts, `AppUser` ↔ `ClassroomParticipant` linkage, visibility
  scoped to "own" courses by Classroom roster. Deferred because roster-scoped
  visibility kept producing contradictions in the role model while the only
  readers are Admin and Dean.
- Student access — a separate epic entirely.

---

## Dependencies worth knowing before sequencing

- **EPIC-6 depends on EPIC-8.** An Admin cannot sign in, and cannot save a
  `WorkspaceConnection`, until an `Installation` with a domain and an
  `AllowedAdmin` entry exists.
- **EPIC-1 depends on EPIC-6.** No connection, no data.
- **EPIC-2, 3, 5 depend on EPIC-1.** They read what synchronization produced.
- **US-032 (link Meet session to course) is blocked** by an open question —
  `trebovaniya.md` §7 item 1: `MeetSession` has no course link, and how it is
  derived (calendar id, meeting code) is not yet decided. EPIC-4 cannot be
  specified past US-031 until that is settled.
- **US-016 (group membership)** requires Admin SDK Directory API scopes that the
  prototype never used and no school has authorized yet
  (`trebovaniya.md` §6). Adding them forces every school to re-authorize.
- **US-025 onward** depend on `Submission` gaining state, submission date and
  late flag — `trebovaniya.md` §7 item 2. Without them a journal cannot tell
  "submitted, ungraded" from "not submitted".
