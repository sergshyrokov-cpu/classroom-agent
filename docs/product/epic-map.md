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
- US-011 "Check access" diagnostic against Classroom and Reports APIs
- US-012 Create and manage Dean accounts

---

## EPIC-1

Synchronization with Google Classroom

### Goal

Bring courses, participants with their course roles, coursework and grades into the local
database, in the background, repeatably.

### Candidate User Stories

- US-013 Background synchronization service
- US-014 Sync courses and rosters
- US-015 Sync coursework and submissions
- US-017 Retry, backoff and permission-error handling
- US-018 Incremental synchronization
- US-019 Trigger a synchronization from the UI

---

## EPIC-2

Courses and participants

### Goal

Let the Dean see what exists: courses with filters, teachers and their courses,
the students of each course.

### Candidate User Stories

- US-020 Course list with filters and search
- US-021 Course detail with roster
- US-022 Teachers and their courses

---

## EPIC-5

Database management

### Goal

Show the state of the local database and let it be refreshed.

### Candidate User Stories

- US-024 Database statistics and last synchronization view
- US-037 Retention purge: daily deletion of expired courses, orphaned
  participants and old audit rows (`persistence-conventions.md` PC-11)

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

Meet activity statistics

### Goal

Show the Dean how lessons actually run in Google Meet for a chosen Classroom
course and period — facts only. Not an attendance register: a lesson held in
class leaves no Meet data (BR-060).

### Candidate User Stories

- US-031 Pull Meet `call_ended` events daily, keep only the fields reports need,
  and keep history beyond Google's 180 days (PC-12)
- US-032 Link meeting codes to courses: automatic suggestion by organizer and
  participant overlap, unassigned-meetings list, confirmation and re-linking by a
  Dean or Admin (BR-065)
- US-033 Built-in Meet reports for a course and period: summary, meeting list,
  student participation, one meeting in detail (BR-062…BR-064)

---

## EPIC-9 (future)

Audit log viewing

### Goal

Out of scope for the first version. `AuditEvent` rows are written from v1
(`security-conventions.md` SC-11); only the screen that reads them is deferred.

### Candidate User Stories

Next version, not the first. Registered in `docs/catalog/stories.yaml` so the
work is queued rather than remembered.

- US-034 Audit log view in an installation: list `AuditEvent` rows with filters
  by period, actor and action type, paged per AC-8. Internal ids are resolved to
  readable names **in the view only** — nothing is written back into the rows.
  Read-only: no endpoint, control or query path may edit or delete a row.
- US-035 Audit log view in the Control Plane: the same for Owner actions —
  sign-in, `Installation` created, suspended or resumed, `AllowedAdmin` added or
  revoked.
- US-036 Export of an audit extract for a period. An export of the audit log is
  itself an audited action, so this Story must not create a blind spot.

### Blocked until decided

- Who may see the audit log — a new cell in the permission matrix
  (`trebovaniya.md` §2). The likely answer is the installation's Admin, with the
  Owner seeing only Control Plane audit, but that is a decision, not a given.
- Whether a Dean sees their own actions, given that Dean exports are the main
  content of the log.

---

## EPIC-10 (future)

Erasing one person's data on request

### Goal

Out of scope for the first version, where the Owner performs erasure by hand on
the school's written request (`trebovaniya.md` §5, BR-076). This epic turns the
procedure into a function.

### Candidate User Stories

- US-038 Erase all data of one `ClassroomParticipant` — grades, submissions,
  Meet participation, course membership — in one transaction, with an audit event that
  carries no personal data of the erased person.

### Blocked until decided

- Who may trigger erasure — a new cell in the permission matrix
  (`trebovaniya.md` §2): the Admin on the school's request, or the Owner only.
- How to keep synchronization from re-importing a person who is still in Google.
  That needs an exclusion list, and the list itself retains an identifier of the
  erased person — a trade-off to accept explicitly, not by accident.
- What the school is told about data already exported into reports: it is
  outside the system and cannot be erased by it.

---

## EPIC-11 (future)

Meet statistics beyond facts

### Goal

Out of scope for the first version, which shows facts for one course. Recorded
so nothing is lost (`trebovaniya.md` §4, Epic 11).

### Deferred

- Electronic timetable integration — schools have none in electronic form yet;
  it brings lesson length and the lesson plan.
- Share of lessons held in Meet against the plan; meeting length against lesson
  length.
- School norms (share of Meet lessons, duration, "stayed the whole lesson"
  threshold) — possibly unregulated in a school and dependent on teaching style.
- Merging meetings split by a dropped connection into one lesson.
- Comparing teachers.
- Dean-configurable Meet reports.
- Views wider than one course: a teacher across courses, an academic group,
  school averages.

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
- **EPIC-4 depends on course roles.** Suggesting a course for a meeting needs
  each course's teachers and students (`CourseMembership`, US-014).
- **US-025 onward** depend on `Submission` gaining state, submission date and
  late flag — `trebovaniya.md` §7 item 2. Without them a journal cannot tell
  "submitted, ungraded" from "not submitted".
