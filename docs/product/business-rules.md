# Business Rules

Stable, cross-story rules. A Specification cites these by id rather than
restating them. Each rule names its source in `trebovaniya.md`.

## Roles and access

**BR-001** The first version has exactly three roles: Owner, Admin, Dean.
Teacher and Student are not accounts and cannot log in; they exist only as
synced `ClassroomParticipant` data. *(§2, Epic 7)*

**BR-002** Admin and Dean see exactly the same teaching data — all courses, all
journals, all attendance of the installation. They differ only in the right to
configure the Workspace connection and to manage accounts. *(§2, permission
matrix)*

**BR-003** The Dean creates and edits report templates. Requiring an Admin for a
layout change is not acceptable. *(§2)*

**BR-004** Both Admin and Dean may start a synchronization. *(§2)*

**BR-005** The Owner is never stored in an installation's `AppUser` table and
does not access a school's teaching data. *(§3, §9)*

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

**BR-026** In read-only mode, viewing and exporting already-synced data keep
working; synchronization, account management, connection settings and report
template edits are blocked. *(§2, §9)*

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
membership of a course, not to the person: the same person can be a teacher on
one course and a student on another. *(§7, item 4 — open question; treat as a
constraint on any design that touches rosters)*

**BR-051** A `Group` is a Workspace group with its own email. Attaching that
email to a course roster enrolls all of the group's current and future members.
`Course` ↔ `Group` is many-to-many. *(§3)*

**BR-052** A `CourseWork` is either graded work (`courseWork`) or an ungraded
material (`material`); journals must distinguish them. Its date follows the
cascade `scheduledTime` → `dueDate` → `updateTime` → `creationTime`. *(§3,
Epic 3)*

**BR-053** All timestamps are stored in UTC. *(persistence-conventions.md PC-6)*

## Attendance

**BR-060** Attendance comes from Google Meet attendance records, not from manual
marking. *(Epic 4)*

**BR-061** Meet data arrives from Google with a delay of up to ~24 hours. The
product states this rather than presenting attendance as live. *(§3, §6)*

## Privacy

**BR-070** Journals and attendance contain personal data of students who may be
minors. Access is limited to the Admin and Dean roles. *(§5)*

**BR-071** Personal data of students is never written to application logs, and
never appears in an HTTP error body. *(§5,
`security-conventions.md` SC-10)*

**BR-072** A retention and deletion policy for student personal data is **not
yet defined** (`trebovaniya.md` §7, item 5). A Story that needs one raises an
Open Decision; nobody invents a period.
