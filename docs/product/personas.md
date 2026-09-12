# Personas

> **Orientation, non-normative.** Explains who the system is designed for.
> Not a source of requirements — an annoyance listed here is context, not an
> acceptance criterion.
>
> Roles are defined normatively in `business-rules.md` (BR-001, BR-002) and
> `business-glossary.md`. Where this description disagrees with them, they win.

Three people use the first version. Teachers and students are data, not users
(Epic 7).

## Dean — учебная часть

**The primary user.** Runs the teaching process at one school.

- Opens the system most working days to check courses, gradebooks and
  attendance.
- Exports reports: the full data dump, and the short form that matches the
  school's paper academic journal.
- Triggers a synchronization when data looks stale — does not care how it works,
  only that the button returns quickly and tells the truth afterwards.
- Creates and edits report templates. This is their working tool; needing an
  Admin for every layout tweak would make the Admin an operator.
- Logs in with a login and password issued by the Admin.

**Cannot:** configure the Google Workspace connection, create accounts.

**What annoys them:** a synchronization that blocks the screen; a report that
silently omits a student; attendance that disagrees with what they saw in the
lesson; being told "ask the administrator" for a layout change.

## Admin — администратор программы

A **Google Workspace domain administrator at the school** — the same person,
the same account. Not a separate IT role invented by this system.

- Present mainly at the beginning: connects the installation to the school's
  Workspace (domain, impersonation user), creates Dean accounts.
- After setup, appears rarely — to fix a connection, add a Dean, or check why
  synchronization is failing.
- Logs in through Google OAuth with the domain administrator account. Has no
  password in this system at all.
- Sees all the same data as the Dean. This is deliberate: they are technically
  able to see it anyway, and denying it would only hamper diagnosis.

**Cannot:** become an Admin without the Owner approving their email first;
upload a service-account key (there is no such screen); point the installation
at a domain the Owner has not registered.

**What annoys them:** an opaque Google error with no hint about which permission
is missing; being asked for super-admin rights the system does not actually
need.

## Owner — владелец сервиса

The person who **builds, hosts and supports** the system for about ten schools.
One Owner for the whole service, not per school.

- Works in the Control Plane, not in any school's installation.
- Registers each school as an `Installation`: name, Google Workspace domain,
  status.
- Maintains the list of emails allowed to be Admin at each school, and revokes
  them when someone leaves or a school stops cooperating.
- Suspends a whole school when the relationship ends — without logging into that
  school's server.
- Places the service-account key on each installation at deployment. Schools
  never handle keys.

**Does not:** look at any school's courses, grades or attendance. Whether the
Control Plane may read Data Plane data at all is an open question, and the
assumed answer is no.

**Has no access to** a school's Google Admin console, and does not need it: the
school's own super-admin authorizes the service account once, using a client ID
and scope list the Owner supplies.

**What annoys them:** having to SSH into a school's server to change anything;
a school able to quietly repoint the program at another domain; one person's
departure bricking an installation.

## Not users in the first version

**Teacher** — appears as a `ClassroomParticipant` with the teacher role on a
course roster. Named in course listings and gradebooks. No account, no login, no
visibility scoping. Epic 7.

**Student** — appears as a `ClassroomParticipant`, in rosters, gradebooks and
attendance records. Personal data of a possibly-minor person, which is why
access is limited to Admin and Dean. No account. Epic 7.
