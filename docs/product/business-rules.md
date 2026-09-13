# Business Rules

> **Normative.** A Specification cites these by id instead of restating them.

Stable, cross-story rules. A Specification cites these by id rather than
restating them. Each rule names its source in `trebovaniya.md`.

## Roles and access

**BR-001** The first version has exactly three roles: Owner, Admin, Dean.
Teacher and Student are not accounts and cannot log in; they exist only as
synced `ClassroomParticipant` data. *(§2, Epic 7)*

**BR-002** Admin and Dean see exactly the same teaching data — all courses, all
journals, all Meet statistics of the installation. They differ only in the right to
configure the Workspace connection and to manage accounts. *(§2, permission
matrix)*

**BR-003** The Dean creates and edits report templates. Requiring an Admin for a
layout change is not acceptable. *(§2)*

**BR-004** Both Admin and Dean may start a synchronization. *(§2)*

**BR-005** The Owner is never stored in an installation's `AppUser` table and
has no access to a school's teaching data through the application: the Control
Plane receives only installation id, version and status — no statistics. *(§3,
§9, SC-12)*

**BR-006** The Owner's access to a school's database on the servers is
operational only — migrations, decommissioning, erasure on the school's request,
and backup and restore — governed by the written agreement with the school and
recorded in the Owner's operations journal. *(§9, SC-12)*

## Admin identity

**BR-010** One email identifies an Admin everywhere: OAuth login, `AllowedAdmin`
entry in the Control Plane, `AppUser` with role Admin, and impersonation user in
`WorkspaceConnection`. Only the service-account email is separate. *(§9)*

**BR-011** An `AppUser` with role Admin is created automatically on the first
successful Google OAuth login by an email present in `AllowedAdmin` for this
`Installation`. No manual seeding. *(§2)*

**BR-012** `AllowedAdmin` is verified on **every** Admin login, not only the
first. On revocation the `AppUser` row is kept for history but login is refused.
*(§2)*

**BR-013** An installation may have several Admins. One person must not be a
single point of failure. *(§2, §9)*

**BR-014** A Dean account is created manually by an Admin. Owner approval is not
involved. *(§2)*

## Installation and the Owner's control

**BR-020** An installation may only work with the Google Workspace domain
recorded on its `Installation` in the Control Plane. Saving a
`WorkspaceConnection` with a different domain is refused. *(§9)*

**BR-021** Moving a school to another domain means creating a **new**
`Installation` with a new database — not editing the domain of an existing one.
The previous installation's data stays in its own database. *(§6, §9)*

**BR-022** Removing an `AllowedAdmin` entry removes that person's right to
configure the installation. It does **not** stop the school from working:
synchronization runs on the service account, and Deans keep working. *(§9)*

**BR-023** Only suspending the `Installation` stops a school. That is a separate
lever from revoking an Admin and must not be conflated with it. *(§9)*

**BR-024** An installation confirms its legitimacy with the Control Plane every
6 hours, and the Control Plane pushes status changes immediately. The periodic
check is the fallback when a push does not arrive. *(§9)*

**BR-025** An installation enters read-only mode when more than **7 days**
(the grace period) have passed since the last successful check, or when its
`Installation` status is suspended. *(§9)*

**BR-027** A check answered `upgrade_required` — the installation's version is
no longer supported by the Control Plane — is an unsuccessful check. It therefore
consumes the grace period instead of stopping the school at once, and the reason
is surfaced to the Admin. *(§8, `deployment-conventions.md` DC-12)*

**BR-026** In read-only mode, viewing and exporting already-synced data keep
working; synchronization, account management, connection settings and report
template edits are blocked. The retention purge is the one write that still runs
(BR-075). *(§2, §5, §9)*

## Google Workspace access

**BR-030** All Google access is read-only. The system never writes anything to
Google Workspace. *(§1)*

**BR-031** Teaching data is read exclusively through the service account with
domain-wide delegation, impersonating the school's domain administrator. The
Admin's own OAuth session authenticates the human and is never used to call a
Google data API. Synchronization therefore works while no one is logged in.
*(§9)*

**BR-032** Domain-wide delegation is authorized by the school's own super-admin
in the school's Google console. The Owner has no access to it. The Owner
supplies only the client ID and the scope list. *(§1, §9)*

**BR-033** The service account for a school lives in the Owner's Cloud project;
each school has its own. Its key is placed by the Owner at deployment and never
reaches the school, the UI, or the database. *(§5, §6)*

**BR-035** Replacing a service-account key never involves the school: delegation
is authorized for the account's client ID, which a new key does not change. Only
recreating the service account itself forces the school to authorize again.
*(§9, DC-5)*

**BR-036** A suspected key leak is a personal-data incident: the key is deleted
before anything else, and the school is informed without delay as the data
controller. *(§9, DC-5)*

**BR-034** A Google permission failure (`403 unauthorized_client`,
`access_denied`, missing scope) means delegation is not configured and is never
retried. It is recorded and surfaced to the Admin with a diagnosable message.
*(Epic 1, Epic 6)*

## Synchronization

**BR-040** Synchronization runs in the background and never blocks a web
request. *(§5)*

**BR-041** Synchronization is idempotent: repeated runs upsert on the Google-side
identifier and never duplicate rows. *(Epic 1)*

**BR-042** Synchronization is incremental — already-known participants are not
refetched. *(Epic 1)*

**BR-043** Transient Google failures (`429`, `5xx`) are retried with exponential
backoff. *(Epic 1)*

**BR-044** `SyncState` is the only source of synchronization progress, status,
last error and last successful run. *(§3, Epic 5)*

## Data model

**BR-050** A person's Classroom role (`teacher` / `student`) belongs to their
membership of a course (`CourseMembership`), not to the person: the same person
can teach one course and study on another. *(§3, v23 — was §7 item 4)*

**BR-054** The subjects of the teaching process are teachers and students, each
identified by a personal account in the school's domain. Group addresses and
other accounts are conveniences, not subjects. *(§3)*

**BR-055** A teacher can grade only a course participant with a personal domain
account, so grades exist only for such participants. *(§4 Epic 3)*

**BR-056** A journal cell shows exactly one state: a grade (raw points out of the
coursework's maximum); "turned in, not graded"; "returned without a grade"; "not
turned in" once the due date has passed; "not due yet"; or "not turned in, no due
date". Any of them may carry a "late" mark, taken from Google's `late` flag as
is. `TURNED_IN` and `STUDENT_EDITED_AFTER_TURN_IN` count as turned in; `CREATED`
and `RECLAIMED_BY_STUDENT` count as not turned in. *(§4 Epic 3)*

**BR-057** The short journal shows only assigned grades; the full journal also
shows a draft grade, marked "draft". *(§4 Epic 3)*

**BR-058** The submission date is the last turn-in: work turned in, reclaimed
and turned in again shows the second date. *(§3, §4 Epic 3)*

**BR-059** Only the fact of submission is kept — never its content, the history
of grade changes, or rubric grades (Epic 12). *(§3)*

**BR-052** A `CourseWork` is either graded work (`courseWork`) or an ungraded
material (`material`); journals must distinguish them. Its date follows the
cascade `scheduledTime` → `dueDate` → `updateTime` → `creationTime`. Materials
are a separate Classroom resource with their own read-only scope and have no
submissions. *(§3, §6, Epic 3)*

**BR-053** All timestamps are stored in UTC. *(persistence-conventions.md PC-6)*

## Meet activity statistics

**BR-060** Meet statistics serve the Dean's oversight of how teaching actually
runs — they are not an attendance register. A lesson may be held in class or
offline with materials in Classroom; missing Meet data never means an absence.
*(§4 Epic 4)*

**BR-061** Meet data arrives from Google with a delay of up to ~24 hours and is
kept by Google for 180 days, so the system pulls it regularly and keeps its own
history. The product states the delay rather than presenting data as live. *(§3,
§6)*

**BR-062** In the first version Meet reports are built for one selected
Classroom course and period and show facts only — no lesson plan, lesson length,
norms or comparison of teachers (EPIC-11). *(§4 Epic 4)*

**BR-063** Each Google meeting is its own row; a reconnect after a dropped call
is not merged. *(§3, §4 Epic 4)*

**BR-064** Only personal domain accounts are counted. An organizer who is not a
teacher of the course is marked "not a teacher of this course"; a domain account
not on the course list is shown apart, marked "not on the course list"; external
guests and connections without an account are shown as "other participants".
*(§4 Epic 4)*

**BR-065** A meeting is linked to a course through its meeting code. The system
links unambiguous matches itself and keeps them editable; ambiguous codes wait in
the unassigned-meetings list, where a Dean or Admin picks the course. A code is
linked once and covers every meeting of that course until the Classroom link is
reset; re-linking moves all its meetings and is audited. Linking is blocked in
read-only mode. *(§2, §4 Epic 4)*

**BR-066** A meeting whose code is linked to no course is deleted N years after
its own date, so it cannot outlive the retention period. *(§5)*

## Privacy

**BR-070** Journals and Meet statistics contain personal data of students who may be
minors. Access is limited to the Admin and Dean roles. *(§5)*

**BR-071** Personal data of students is never written to application logs, and
never appears in an HTTP error body. *(§5,
`security-conventions.md` SC-10)*

**BR-073** Every export of a journal or report is audited: who, when, which
courses, which period, which template, how many rows. The exported content itself
is never stored in the audit trail. *(§5, `security-conventions.md` SC-11)*

**BR-072** Student personal data is kept for a retention period N agreed
between the school and the Owner and set at deployment. The unit is the course: a
course and everything under it is deleted once it is archived or gone from Google
and its last activity is more than N years ago; a participant is deleted once no
remaining course references them. Deletion is physical. *(§5, PC-11)*

**BR-074** A student who disappears from Google is not deleted by
synchronization. Their grades stay in the journals of the period they studied
until the course itself expires — a journal is a record, not a live view. *(§5)*

**BR-075** The retention purge runs in read-only mode too — the only write that
does. Retention is an obligation, not a feature: a suspended school must not keep
data indefinitely. *(§5)*

**BR-076** In the first version, erasing one person's data on request is done by
the Owner on the school's written request, and only after the school has removed
that person from Google — otherwise synchronization re-imports them. *(§5,
EPIC-10)*

**BR-077** An erasure never comes back: after a database is restored from a
backup, every erasure recorded in the operations journal after the backup's date
is re-applied before the school gets the installation back. *(§9, DC-13)*
