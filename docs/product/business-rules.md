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
configure the Workspace connection and to manage accounts, plus the few
Admin-only actions of the permission matrix: erasing a person's data (BR-076) and
confirming that courses hidden from the technical account were deleted (BR-049,
v101). *(§2, permission matrix, v93, v101)*

**BR-003** The Dean creates and edits report templates. Requiring an Admin for a
layout change is not acceptable. *(§2)*

**BR-004** Both Admin and Dean may start a synchronization. *(§2)*

**BR-005** The Owner is never stored in an installation's `AppUser` table and,
as Owner, has no access to a school's teaching data through the application (a
person who also works at a school may be its Admin — BR-013): the service
channel carries no teaching data and no statistics — only installation id,
versions, status and compatibility state, the `Installation`'s domain and client
ID in the legitimacy check response (v54), and, at an Admin login, the email being
checked with a yes/no answer. *(§3, §9, SC-12)*

**BR-006** The Owner's access to a school's database on the servers is
operational only — migrations, decommissioning, erasure on the school's request
while the school is in read-only mode (v93), and backup and restore — governed by the written agreement with the school and
recorded in the Owner's operations journal. *(§9, SC-12)*

## Admin identity

**BR-010** One email identifies an Admin everywhere in the program: OAuth login,
`AllowedAdmin` entry in the Control Plane, and `AppUser` with role Admin. It is a
domain administrator account, never a super-admin. The impersonation user is
**not** an Admin's account (BR-015). *(§1, §2, §9, v30)*

**BR-011** An `AppUser` with role Admin is created automatically on the first
successful Google OAuth login by an email present in `AllowedAdmin` for this
`Installation`. No manual seeding. The Google sign-in admits only an Admin
account: an existing Dean account with that email is refused even when the email
is in `AllowedAdmin`, and is never converted — one account, one role; a person who
needs both roles uses two addresses. *(§2, v96)*

**BR-012** `AllowedAdmin` is verified on **every** Admin login, not only the
first. On revocation the `AppUser` row is kept for history (until the retention purge,
PC-11) but login is refused.
The installation asks the Control Plane on each login and keeps no copy of the
list; if the Control Plane does not answer, the Admin login is refused. Dean
logins and synchronization are unaffected. Known limitation (v103): revocation does
not end a session already open — it ends on its own expiry, after 60 minutes of
inactivity and at most 8 hours after sign-in; immediate revocation would need a
Control Plane call on every Admin request. *(§2, §9, v26, v103)*

**BR-013** An installation may have several Admins, and onboarding requires at
least two, so one person is never a single point of failure. Admins are school
staff on personal domain administrator accounts. A person who is also the Owner
may be one of a school's Admins only as that school's employee, through
`AllowedAdmin` like anyone else and recorded in the agreement with that school;
the Owner role itself still grants no access to teaching data. *(§2, §9, v50)*

**BR-014** A Dean account is created manually by an Admin. Owner approval is not
involved. A Dean's login is their work email in the school's domain — a personal
address or a free-form string is not accepted (v55). An Admin may disable, re-enable and reset the password of a Dean
account, but never deletes it — like a revoked Admin's `AppUser`, it is kept for
history and audit, and removed only by the retention purge N years after its last
successful sign-in, or creation if never signed in (PC-11, v45). A Dean may change their own password. The password an Admin sets when creating the
account or resetting it is temporary: the Dean must change it at the first login,
and the new password may not equal it (v64). The password and lockout policy is
`security-conventions.md` SC-2; an Admin reset clears a sign-in lockout but does
not re-enable a disabled account, and a disabled Dean who enters the correct
password is told the account is disabled (v64) — unless sign-in is locked out,
which is checked first (v65). Disabling takes effect at once (v103): it rotates the
account's security stamp, which the session cookie is checked against on every
request (v79), so a Dean who is working at that moment cannot open the next page or
download an export and is sent to the sign-in page; a request already in progress
completes. *(§2, v38, v62, v64, v65, v103)*

**BR-015** Teaching data is read on behalf of a **technical account** of the
school — the impersonation user in `WorkspaceConnection`. The school's
super-admin creates it at onboarding; no person stands behind it. It holds the
super-admin role, because lesser Classroom roles give the Classroom API no view of
the domain's courses; what the program can read is bounded by the read-only
domain-wide delegation scopes, not by the account's roles (v97). Interactive
sign-in to it is blocked by mandatory measures: a random password stored nowhere;
enforced 2-step verification with no enrolment grace and no factor, backup code,
recovery email or phone enrolled; a separate organizational unit; an alert on any
sign-in; the role granted only immediately before the first synchronization (v97).
Nobody signs in to the program with it and it is not in `AllowedAdmin`. The Admin
enters its email in the connection settings. Staff leaving or an Admin being
revoked therefore never stops synchronization. *(§9, v30, v97)*

## Installation and the Owner's control

**BR-020** An installation may only work with the Google Workspace domain
recorded on its `Installation` in the Control Plane. Saving a
`WorkspaceConnection` whose domain, or whose impersonation user's email domain,
differs from it is refused. *(§3, §9)*

**BR-021** Moving a school to another domain means creating a **new**
`Installation` with a new database — not editing the domain of an existing one.
The previous installation's data stays in its own database. A domain is unique
across all `Installation` records, suspended ones included: a school returning to
its former domain has its record resumed, not a new one created. The service-account
client ID is unique as well. An `Installation` is created active and is never
deleted in the first version; its name may be corrected, its domain never.
*(§3, §6, §9, v69)*

**BR-022** Removing an `AllowedAdmin` entry removes that person's right to
configure the installation. It does **not** stop the school from working:
synchronization runs on the service account, and Deans keep working. *(§9)*

**BR-079** An `AllowedAdmin` email must be in exactly the domain of its
`Installation`; it is stored and compared in lower case and appears at most once
per `Installation`. There is no limit on the number of entries and no editing: a
wrong email is revoked and added again, and a revoked email may be added again as
a new entry. Entries may be added and revoked whatever the `Installation` status,
suspended included. Revoking deletes the entry with its email and requires the
Owner's confirmation. Fewer than two entries is allowed — the last Admin may be
revoked — but the school's page warns the Owner while there are fewer than two
(BR-013). *(§3, §4, §9, v70)*

**BR-023** Only suspending the `Installation` stops a school. That is a separate
lever from revoking an Admin and must not be conflated with it. *(§9)*

**BR-080** The Owner suspends an active `Installation` and resumes a suspended
one from its page in the Control Plane, each after a confirmation that shows the
school's name and domain and what the action does; nothing changes until the
Owner confirms. A status change deletes no data and leaves the `AllowedAdmin`
entries, name, domain and client ID untouched. No reason is recorded and the
time of the change is not stored on the `Installation` — the audit row is the
trace. An action that would not change the status (the school is already in it)
writes nothing, audit included. *(§3, §4, §9, v71)*

**BR-024** An installation confirms its legitimacy with the Control Plane every
6 hours, and the Control Plane pushes status changes immediately. The periodic
check is the fallback when a push does not arrive. *(§9)*

**BR-082** A status-change push is only a signal to check now. It carries the
installation id and nothing else; the installation answers `202` and runs its
ordinary legitimacy check in the background — at most once a minute, never two
at once — and the status comes from that check's answer. A push that cannot start a
check yet is not lost: the installation remembers one pending check, however many
such pushes arrive, and runs it as soon as it is allowed (v77). A push is sent only when
suspending or resuming actually changed the status, to the push address on the
school's page (`http://`, host and port; optional — no address, no push, and the
page warns). Delivery waits 10 seconds, retries 3 times after 5 s, 30 s and 2 min
in memory, and a newer push to the same school replaces an unfinished one. A `404`
(another installation's id) is not retried. The Owner does not see the push
result; it is logged, not audited. *(§3, §8, §9, v76, v77)*

**BR-081** An installation checks its legitimacy at startup, then 6 hours after
a successful check, or 15 minutes after an unsuccessful one until a check
succeeds. A check succeeds when the Control Plane answers within 30 seconds with
a parseable answer, knows the installation id and does not answer
`upgrade_required`; an answer "suspended" is a successful check. A successful
check records time, status, compatibility state, domain and client ID in
`LegitimacyState`; an unsuccessful one leaves the last success time unchanged
(on `upgrade_required` it still records the status, compatibility state, domain
and client ID). The Control Plane derives compatibility from the minimum
supported and recommended application versions in its configuration and from
the contract versions it still supports; it keeps only the last check per
`Installation` (`InstanceLicenseCheck`) and shows it on the school's page.
Checks are not audited. *(§3, §5, §8, §9, v73)*

**BR-025** An installation enters read-only mode when more than **7 days**
(the grace period) have passed since the last successful check, when its
`Installation` status is suspended, or while no check has ever succeeded.
*(§3, §9, v73)*

**BR-026** In read-only mode, viewing and exporting already-synced data keep
working and everything else is blocked for both roles — for example
synchronization (including pulling Meet data), report template edits, linking
Meet meeting codes, Dean account management, confirming that courses hidden
from the technical account were deleted (BR-049, v101), connection settings and
"check access". In read-only mode the system makes no call to Google at all; the
connection instructions stay viewable (v39). The only
writes permitted are a closed list of service writes: audit rows; sign-in
bookkeeping (Identity failed-attempt counting and lockout, recording the time of
the last successful sign-in, which the retention period of an account counts
from (PC-11), creating the
`AppUser` of an approved Admin at their first login, a Dean changing their
own password, a user choosing their UI language, and rotating an account's security
stamp at sign-out — the session cookie is checked against it on every request, so
without the rotation a captured cookie would stay valid until its own expiry, and
without the permission a user of a suspended school could not end a session
(v79)); the legitimacy-check state
(last successful check time, last known status, last compatibility state, and the
`Installation`'s domain and client ID); and the retention purge with
its audit event (BR-075). Any other write is refused; a new service write is
permitted only by extending this list in `trebovaniya.md` §2. *(§2, §5, §9, v28, v79)*

**BR-027** A check answered `upgrade_required` — the installation's version is
no longer supported by the Control Plane — is an unsuccessful check. It therefore
consumes the grace period instead of stopping the school at once, and the reason
is surfaced to the Admin. *(§8, `deployment-conventions.md` DC-12)*

## Google Workspace access

**BR-030** All Google access is read-only. The system never writes anything to
Google Workspace. *(§1)*

**BR-031** Teaching data is read exclusively through the service account with
domain-wide delegation, impersonating the school's technical account (BR-015). The
Admin's own OAuth session authenticates the human and is never used to call a
Google data API. Synchronization therefore works while no one is logged in.
*(§9)*

**BR-032** Domain-wide delegation is authorized by the school's own super-admin
in the school's Google console, who also creates the school's technical account
(BR-015). The Owner, as Owner, has no access to that console — a person who is
also the school's Admin has it as the school's employee (BR-013). The Owner
supplies only the client ID and the scope list. *(§1, §9, v30)*

**BR-033** The service account for a school lives in the Owner's Cloud project;
each school has its own. Its key is placed by the Owner at deployment and never
reaches the school, the UI, or the database. The Owner's Cloud project lives
outside every school's domain, so no school controls the others' access. *(§5, §6,
v50)*

**BR-034** A Google permission failure (`403 unauthorized_client`,
`access_denied`, missing scope) means delegation is not configured and is never
retried. It is recorded and surfaced to the Admin with a diagnosable message on the
connection page, worded as "Check access" words the same problem. *(Epic 1, Epic 6,
v80)*

**BR-035** Replacing a service-account key never involves the school's
super-admin: delegation is authorized for the account's client ID, which a new
key does not change. The new key is confirmed by the installation's startup access
self-check in its log, so the school takes no part (DC-5, v54). Only
recreating the service account itself forces the school to authorize again; the
Owner then updates the client ID on the `Installation`. *(§9, DC-5, v43)*

**BR-036** A suspected key leak is a personal-data incident: the key is deleted
before anything else, and the school is informed without delay as the data
controller. *(§9, DC-5)*

## Synchronization

**BR-040** Synchronization runs in the background and never blocks a web
request. *(§5)*

**BR-041** Synchronization is idempotent: repeated runs upsert on the Google-side
identifier and never duplicate rows. *(Epic 1)*

**BR-042** Synchronization is incremental: an `ARCHIVED` course whose Google
`updateTime` equals the one stored at its last complete read is not re-read.
Courses in any other state are read in full every run, and once a week a run
reads every course in full. A manual run follows the same rules; the number of
skipped courses goes to the run's log only. *(Epic 1, v90)*

**BR-043** Transient Google failures (`429`, `5xx`, timeout, dropped connection)
are retried with exponential backoff: at most 4 attempts, pauses of about 2 s, 8 s
and 30 s with jitter, or Google's own requested delay capped at 2 minutes. When the
attempts run out, or on a permission failure (BR-034), the run ends as failed; work
already loaded stays and the next run keeps the normal schedule. A course that
disappears mid-run (`404`) is skipped. So is a course where Google returns points,
a grade or maximum points larger than the system can store (above about 7.9·10²⁸),
or negative (v100): its already loaded data stays as it was, the run's log records
the course id without the values, and the next run reads the course again.
*(Epic 1, v80, v98, v100)*

**BR-044** `SyncState` is the only source of synchronization progress, status,
last error and last successful run. *(§3, Epic 5)*

**BR-045** A stored coursework item or material that Google no longer returns in a
full read of its course is never deleted: synchronization marks it "not in
Classroom" with the date it noticed, and its submissions and grades stay. Google's
answer cannot tell a deleted item from one returned to draft, hence the wording. If
the item comes back, the mark is removed. A skipped course and an unchanged archived
course are not re-read, so their items neither gain nor lose the mark. The mark is
a synchronization record, not an edit of the mirror. *(§3, Epic 1, v99)*

**BR-046** A course that synchronization skips (BR-043, or a course state the
system does not know) stays recorded in `SyncState` — Google course id, the name
from Google's course list, the reason and since when — until a run reads it
successfully. A course never loaded is shown by that name; a loaded one is marked
in its journal and report as "data not updated since" the date of its last
successful read. *(§3, Epic 1, v99)*

**BR-047** Data-health notices tell Admin and Dean about problems synchronization
left behind. Two kinds:
- **teaching** — fixed by a teacher in Classroom, shown to the Dean in detail:
  points above the coursework's maximum (BR-067), items "not in Classroom"
  (BR-045), a course skipped for out-of-range or negative values (BR-043), with
  the hint to contact the teacher; a course "not in Classroom" (BR-048), with
  the hint that it was deleted or hidden in Classroom and to ask the teacher;
- **technical** — fixed by the Admin: the last run failed (a permission error is
  one such case); no successful run for more than 24 hours, or none yet; a course
  skipped for an unknown state; Google returned an empty course list while
  courses are stored (BR-048), with the date since which it has (v102); the technical account does not see courses the
  previous one saw (BR-049) — one problem for all such courses, archived ones
  included, the Admin seeing their list (v101); **no connection with the Control
  Plane** (v103) — the last successful legitimacy check (BR-081) was more than 24
  hours ago while the grace period (BR-025) still runs, shown with the date of the
  last successful check and the date read-only mode will start if the connection is
  not restored. A shorter outage raises nothing (the check retries every 15
  minutes); once read-only mode starts the problem disappears and the read-only
  message replaces it. The Dean sees that the problem exists and the hint
  to tell the Admin; the Admin sees the details, worded as in the synchronization
  state (Epic 6) — for the Control Plane problem the hint is to tell the Owner, who
  fixes it, and the `upgrade_required` reason when that is the cause (BR-027). In
  read-only mode the staleness notice is not raised.

A banner at the top of every installation page, while any problem exists, shows
"N technical problems, M teaching" and links to the «Стан даних» page with the
details of each, linking to the course's journal. One course × one kind of problem
counts as one; a problem without a course (failed run, staleness) counts as one.
Notices show the current state only: a notice disappears when its problem is gone,
with no expiry and no "read" mark; history is in the log and audit. Archived
courses are not counted — Google allows no change there — but keep their marks and
are listed separately on «Стан даних». The Dean sees the problems of the courses
visible to them (all courses in the first version) and technical ones without
details; the Admin sees all, technical ones with details. Notices are built from
stored data, never by calling Google, and are visible in read-only mode. The banner
is restrained — colour only, no sound, pop-up or blinking. Next to the teaching
problems on «Стан даних» the Dean sees the hint to contact the Admin if the
problem could not be solved with the teacher (v100). *(§2, Epic 1, v99, v100, v102, v103)*

**BR-048** A stored course missing from a course list read to its end is never
deleted: synchronization marks it "not in Classroom" with the date it noticed.
Google's answer cannot tell a deleted course from one that left the technical
account's view, hence the wording. Its roster, items, submissions and grades stay,
it counts in reports as before, and its status is the last one stored; its items
get no mark of their own, since the course is no longer read. If the course comes
back on the list, the mark is removed. A course that disappears mid-run (`404`) is
only skipped (BR-043) and is marked only if the next course list lacks it. The
mark is a synchronization record, not an edit of the mirror. If Google returns an
empty course list without an error while courses are stored, nothing is marked —
the technical account has most likely lost its super-admin role — and the run
records a technical problem (BR-047) worded "Google has returned no course since
<date>", the date being the run since which the lists have come back empty in a
row (v102). Such a run counts as successful, so the staleness notice is not raised;
this problem takes its place. A partial loss is not told apart from a real
deletion and there is no threshold: it would be hard to explain and would hide a
real year-end deletion of archived courses; the worst case, a one-run Google
glitch, leaves false marks until the next run. The mark is shown next to the
status in the course list and course card (no separate filter), and in the
header of the journal and report on screen and in the export.
*(§3, Epic 1, Epic 2, Epic 3, v100, v102)*

**BR-049** A new technical account may see fewer courses than the previous one,
and Google's answer cannot tell that from a deletion. So a stored course missing
from a course list read to its end, which synchronization last saw on the list
**before** the technical account was changed (`WorkspaceConnection` records when
it last changed — a different email saved), does not get the "not in Classroom"
mark of BR-048: it is marked "not visible to the technical account" with the
date. Its data stays and it counts in reports as before; the header of its journal
and report, on screen and in the export, shows "data not updated since <its last
successful read>", as for a skipped course (BR-046), and the course list and course
card show "not visible to the technical account since <date>" next to the status,
with no separate filter (v102). It is a technical problem, not a teaching one (BR-047). A
course the new account has seen on the list at least once and then lost is marked
"not in Classroom" as before; an empty list follows BR-048 and marks nothing. The
mark is removed when the course is on the list again — for example, the Admin
restored the previous account or gave the new one the needed role. If the course
really was deleted, the Admin selects such courses on «Стан даних» and confirms
"deleted in Classroom": they get the "not in Classroom" mark dated by the
confirmation and follow BR-048 from then on. Only the Admin confirms — they
changed the account and know why the courses vanished; the confirmation is
audited (SC-11) and blocked in read-only mode (BR-026). Both the last time a course
was seen on the list and this mark are synchronization records, not edits of the
mirror. *(§2, §3, Epic 1, Epic 2, Epic 3, §5, v101, v102)*

## Data model

**BR-050** A person's Classroom role (`teacher` / `student`) belongs to their
membership of a course (`CourseMembership`), not to the person: the same person
can teach one course and study on another. *(§3, v23 — was §7 item 4)*

**BR-051** Classroom gives no join or leave dates, so a `CourseMembership` records
what synchronization observed: when the person was first and last seen on the
roster, and whether they are on it now. A membership is never deleted when the
person leaves the roster. Someone with submissions who was never seen on the
roster (they left before the first synchronization) gets a student membership
marked off the roster, first and last seen on that run's date, so the leaver
expiry applies from that date (v56). A membership's start — the day from which
the person counts as a course member in journals and Meet reports — is the
course's Google `creationTime` for everyone on the roster or found through
submissions in the run that first loaded the course (that run's date when Google
gave no `creationTime`), and the first-seen date for everyone who appears later;
this applies to every course and to memberships stored before v98. Its end is the
last-seen date. Known limitation: in a course that lives for years with changing
students, current members count from the course's creation (v98); a student who
left and came back has one membership spanning the break, so they count as a
member during it and the break is visible nowhere (v102). A period's
journal lists students who were members for at least part of the period plus
anyone with submissions in it; in Meet reports, a course's students and teachers
are its members on the meeting's date. *(§3, §4 Epic 3, Epic 4, v31, v98, v102)*

**BR-052** A course item is one of three kinds, and journals distinguish them:
graded work (a `courseWork` with maximum points — submissions and grades),
ungraded work (a `courseWork` without maximum points — submissions, no grades),
and a material (`material` — no submissions). Its date follows the cascade
`scheduledTime` → `dueDate` → `updateTime` → `creationTime`. Materials are a
separate Classroom resource with their own read-only scope. *(§3, §6, Epic 3,
v32)*

**BR-053** All timestamps are stored in UTC; dates are shown, and period day
boundaries set, in the school's time zone (NFR-074). *(§5, v40,
persistence-conventions.md PC-6)*

**BR-054** The subjects of the teaching process are teachers and students, each
identified by a personal account in the school's domain. Group addresses and
other accounts are conveniences, not subjects. *(§3)*

**BR-055** A teacher can grade only a course participant with a personal domain
account, so grades exist only for such participants. *(§4 Epic 3)*

**BR-056** A journal cell shows exactly one state: a grade (raw points out of the
coursework's maximum); "turned in, not graded"; "returned without a grade"; "not
turned in" once the due date has passed; "not due yet"; or "not turned in, no due
date". Any of them may carry a "late" mark, taken from Google's `late` flag as
is. `TURNED_IN` and `STUDENT_EDITED_AFTER_TURN_IN` count as turned in; `NEW` (v82),
`CREATED` and `RECLAIMED_BY_STUDENT` count as not turned in. A cell of ungraded work never
shows a grade: "turned in", "returned", "not turned in" once due, "not due yet",
or "not turned in, no due date", with the same optional "late" mark — it has no
"grade" or "not graded" state (BR-052). *(§4 Epic 3, v32)*
How a cell is computed (v57): "due" is judged against the moment the journal is
built, not the end of the period, because the stored submission state is current.
An assigned grade wins over the work's state — the cell shows it even if the work
was later reclaimed or turned in again; without one, the state decides. A draft
grade never replaces the state: the full journal shows it next to the state, the
short journal omits it. A material has its own column, marked as a material, with
empty cells; a template may hide materials. Coursework with no submission for a
student (assigned to some students only) shows "not assigned", printed as a dash —
never "not turned in".

**BR-057** The short journal shows only assigned grades; the full journal also
shows a draft grade, marked "draft". *(§4 Epic 3)*

**BR-058** The submission date is the last turn-in: work turned in, reclaimed
and turned in again shows the second date. *(§3, §4 Epic 3)*

**BR-059** Only the fact of submission is kept — never its content, the history
of grade changes, or rubric grades (Epic 12). *(§3)*
**BR-067** A grade or draft grade above the coursework's maximum points —
usually a teacher's entry error — is highlighted in the on-screen journal and
report and labelled "above maximum". In the school's scale it stays 100 %
(v98), so an export keeps the scale value in the cell, highlights the cell with a
colour and adds a note with the raw points and maximum. An item "not in
Classroom" (BR-045) keeps its column with its grades in the journal, report and
export, marked in the header and, on the «Теми занять» sheet, next to the topic.
*(§4 Epic 3, v99)*

**BR-068** A student who is not on the course's roster now is marked "left
<last-seen date>" next to their name in the journal and report on screen (BR-051;
precise to the synchronization frequency). In the export the name stays as it is
and the name cell gets a note with the same mark, so the printed form does not
break (as for BR-067). A student known only through submissions — they left before
the first synchronization — is marked "left" with no date, because the system does
not know when they left. The mark is current state: back on the roster, no mark.
*(§4 Epic 3, v101)*

**BR-069** A course with no teacher on its roster now shows "no teacher in
Classroom" in the course list in place of its teachers — for example, the teacher
left and their account was deleted. The course card lists the teachers who left.
Known limitation: a teacher whose data was erased on request (BR-076) is unknown
to the system, so their course gets the mark too. *(§4 Epic 2, v101)*

**BR-084** A journal and a report show "generated: <date, time>" — the moment the
cells are built for, the same moment "due" is judged against (BR-056) — and "data
from Google as of: <date, time of the course's last successful read>", in the
school's time zone, labels in the user's language. On screen both lines are in the
journal and report header. In an Excel export — journal, report by template and
the blanks built from it — both go into a **note on the course-name cell**, not
into a printed cell: the export replaces the paper academic journal and this
information is not printed. A downloaded file thus shows how old it is; a grade
changed later is not marked anywhere, because the previous value is not kept
(BR-059, mirror). *(§4 Epic 3, v103)*

**BR-085** A course and a person are identified by their Google id (BR-041), so
after a course, surname or email is renamed in Google the next synchronization
shows the new name on every screen and in new exports — the same course or person,
with the same grades and links. Known limitation: the previous name is not kept
(mirror, v83), so a person cannot be found by a former surname or email, by search
or on a page; files already exported keep the old name. *(§4 Epic 2, v103)*

## Meet activity statistics

**BR-060** Meet statistics serve the Dean's oversight of how teaching actually
runs — they are not an attendance register. A lesson may be held in class or
offline with materials in Classroom; missing Meet data never means an absence.
*(§4 Epic 4)*

**BR-061** Meet data arrives from Google with a delay of up to ~24 hours and is
kept by Google for 180 days, so the system pulls it regularly and keeps its own
history. The product states the delay rather than presenting data as live. *(§3,
§4 Epic 4, §6, v47)*

**BR-062** In the first version Meet reports are built for one selected
Classroom course and period and show facts only — no lesson plan, lesson length,
norms or comparison of teachers (EPIC-11). Meet reports of a course marked "not in
Classroom" (BR-048) or "not visible to the technical account" (BR-049) show the
mark with its date and "course membership as of <its last successful read>", on
screen and in the export: the course is no longer read, so students who joined
later are unknown and those who left still count as its students (v102).
*(§4 Epic 4, v102)*

**BR-063** Each Google meeting is its own row; a reconnect after a dropped call
is not merged. *(§3, §4 Epic 4)*

**BR-064** Only personal domain accounts are counted. A personal account is a
domain account the installation knows as a Classroom participant — on the roster
of any of its courses, in any role; every other domain account (class accounts,
group addresses, staff without courses) counts as an "other participant". Known
limitations: a shared account enrolled in a course counts as a person; staff
without Classroom courses fall into "other participants" (v58). An organizer who
is not a teacher of the course on the meeting's date is marked "not a teacher of
this course"; a domain account not on the course roster on that date is shown
apart, marked "not on the course list" (BR-051); external guests and connections
without an account are shown as "other participants". The organizer is named in
their own column and never counted in the numbers; other teachers of the course on
the meeting's date are counted in a separate "course teachers" column of the
meeting list, never as students or "not on the course list". The one-meeting
report marks everyone the same way: organizer, course teacher, course student, not
on the course list, other participant (v58). *(§4 Epic 4)*

**BR-065** A meeting is linked to a course through its meeting code. The system
links unambiguous matches itself and keeps them editable. A match is scored per
candidate course as the share of distinct domain accounts across all meetings of
the code (organizers excluded) who were students of the course on the date of at
least one of their meetings of that code; at least one organizer of the code's
meetings must be a teacher of the course on the date of their meeting (v55).
An automatic link takes effect at once — its meetings count in the course's
reports and last activity, and the code leaves the unassigned-meetings list;
confirming it is optional and only marks that a person checked it, and the UI
shows which links are automatic and which confirmed (v55). It links when the best share
is at least 60% and the next is at least 30 points lower, is recomputed on each
Meet ingestion while the code is unlinked, and an automatic link is never revised
by the system (v41). A course marked "not in Classroom" (BR-048) or "not visible
to the technical account" (BR-049) is no longer read and its roster stays as it
was, so a course re-created in place of a deleted one would tie with it and never
link itself; such a course is therefore neither linked automatically nor offered
as a candidate, while a Dean or Admin may still pick it by hand and links already
made to it stay (v102). Ambiguous codes wait in
the unassigned-meetings list, where a Dean or Admin picks the course. A code is
linked once and covers every meeting of that course until the Classroom link is
reset; re-linking moves all its meetings. Every change to a link is audited: the
automatic link (actor `system`), picking a course for an unassigned code,
confirmation and re-linking (v55). Linking is blocked in
read-only mode. Known limitation: a non-Classroom link reused in several courses
is linked to one course only, and meetings of the other courses under that code
count there. *(§2, §4 Epic 4, v41, v102)*

**BR-066** Every meeting, whether its code is linked to a course or not, is
deleted with its participations N years after its own date, so it cannot outlive
the retention period — even in a course that is still kept. *(§5, v55)*

**BR-083** Not every meeting code has a course — a staff meeting, a parents'
meeting, a consultation for several groups. A Dean or Admin may mark any code,
unlinked or linked (automatically or by a person), as "not a course": its
meetings then count in no course's reports or last activity, the code leaves the
unassigned-meetings list for a separate list of marked codes, its share is no
longer computed and automatic linking never touches it. The mark is always set
by a person and records who and when; it is removed by picking a course for the
code. Setting and removing it are audited and blocked in read-only mode; the mark
is purged once the code has no meetings left. *(§2, §3, §4 Epic 4, §5, v88)*

## Privacy

**BR-070** Journals and Meet statistics contain personal data of students who may be
minors. Access is limited to the Admin and Dean roles. *(§5)*

**BR-071** Personal data of students is never written to application logs, and
never appears in an HTTP error body. *(§5,
`security-conventions.md` SC-10)*

**BR-072** Student personal data is kept for a retention period N agreed
between the school and the Owner and set at deployment. Deletion is from the
installation's own database only — nothing is ever deleted in Google. The unit is
the course: a course and everything under it is deleted once its last activity is
more than N years ago, whatever its state — a course "not in Classroom" (BR-048)
or "not visible to the technical account" (BR-049) included. Last activity is the latest of: a change
to the course, any coursework or material created or changed, any submission
changed, any Meet meeting linked to the course. Synchronization does not import a
new course whose last activity in Google (course, coursework and materials,
submissions) is already older than N; a course already imported is always updated
until the purge deletes it (v36, v55). Known limitation: a course with no Classroom
activity for N years but live Meet lessons is not imported by a new installation
(v55). A person who has left a course's
roster loses that membership, with their submissions and Meet participation in
that course, once they were last seen more than N years ago — even while the
course is active. Every Meet meeting, linked to a course or not, is deleted with
all its participations once its own date is more than N years ago, even while the
course is active (v55). Known limitation: submissions of people still on the roster
are not deleted by their own date — they stay with the course's journal until the
course or the person as a leaver expires (v55). A participant is deleted once no remaining
membership references them. Deletion is physical. *(§5, PC-11, v31)*

**BR-073** Every export of a journal or report is audited: who, when, which
courses, which period, which template, how many rows. The exported content itself
is never stored in the audit trail. *(§5, `security-conventions.md` SC-11)*

**BR-074** A student who disappears from Google is not deleted by
synchronization. Their grades stay in the journals of the period they studied
until the retention period expires — the course's, or their own as a leaver
(BR-072) — a journal is a record, not a live view. *(§5)*

**BR-075** The retention purge runs in read-only mode too — it is one of the
service writes permitted there (BR-026). Retention is an obligation, not a feature: a suspended school must not keep
data indefinitely. *(§5)*

**BR-076** Erasing one person's data on the school's written request is an Admin
function (v93): every record of one `ClassroomParticipant` — memberships,
submissions with grades, Meet participations — goes in one transaction, mentions
in other records (a meeting's organizer email, a course's owner) are cleared, and
an `ErasedParticipant` exclusion keeps synchronization from re-importing the
person while they remain in Google: the Google user id and a salted fingerprint
of the email, never a readable name or address — an explicitly accepted
trade-off. The Admin lifts an exclusion by entering the person's email. Not
available in read-only mode; there, an urgent request is done by the Owner by
hand, only after the school has removed the person from Google. *(§2, §3, §5,
EPIC-10, v19, v93)*

**BR-077** An erasure never comes back: after a database is restored from a
backup, every erasure made after the backup's date is re-applied before the
school's users get the installation back. The Owner's erasures come from the
operations journal; an Admin's erasures (v93) are rolled back together with their
exclusions and audit rows, so the school repeats them from its written requests,
and the Owner cross-checks their number against the installation log, which
records every erasure with the participant's internal id. *(§9, DC-13, v47, v93)*

**BR-078** School data leaves the installation only for two destinations: Google,
read-only (plus Google OAuth for Admin sign-in, authentication only), and the
Control Plane service channel, which carries no teaching data (BR-005). Encrypted
backups stay on the Owner's own infrastructure, never with an external storage
provider (v54). No AI, speech-recognition, analytics or other external service receives
school data in the first version; an AI assistant is future Epic 13. *(§6, §4 Epic 13,
v52, `security-conventions.md` SC-13)*
