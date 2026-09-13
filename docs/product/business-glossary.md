# Business Glossary

> **Normative for terminology.** Use these terms in specifications, designs,
> code and tests; see "Terms to avoid" at the end.

Terms used consistently across specifications, designs, code and tests. Russian
terms from `trebovaniya.md` are given so the two documents can be read together.

## Roles

| Term | Русский | Meaning |
|---|---|---|
| **Owner** | Владелец | The one person who owns, hosts and supports the service for all schools. Lives in the Control Plane; has no account in any installation as Owner (a person who is also a school's employee may be that school's Admin, BR-013). |
| **Admin** | Админ | A Google Workspace domain administrator at one school, who is also the administrator of that school's installation. Not a super-admin. One email serves as OAuth login, `AllowedAdmin` entry and `AppUser`; the impersonation user is a separate technical account. |
| **Dean** | Декан, учебная часть | The primary user: views courses, gradebooks and Meet activity statistics, exports reports, triggers synchronization. |
| **Teacher** | Преподаватель | A person teaching a course. **Data only in the first version** — not an account (Epic 7). |
| **Student** | Студент | A person enrolled in a course. **Data only** (Epic 7). |
| **Super-admin** | суперАдмин | The school's Google Workspace super-administrator. Authorizes domain-wide delegation once, in the school's own Google console, and creates the school's technical account. Not a role in this system. |

## Deployment

| Term | Meaning |
|---|---|
| **Installation** | One school's deployment: its own backend process and its own database. Also the Control Plane entity recording that school — name, domain, status, assigned service-account client ID. |
| **Data Plane** | The per-school application. One per installation. |
| **Control Plane** | The single shared service holding Owner authentication, the `Installation` registry, the `AllowedAdmin` list, the Admin login check and legitimacy checks. Separate database. |
| **Setup code** | The one-time code the Control Plane prints to the server console at first start; the Owner account can be created only with it (SC-2). |
| **Retention period N** | How many years a school's data is kept, agreed with the school and set by the Owner at deployment. A required installation setting (PC-11). |
| **School time zone** | A required installation setting: dates are shown, and period day boundaries set, in this zone; everything is stored in UTC (NFR-074). |
| **UI language** | Ukrainian or English. The school's default is an installation setting; each user's choice is stored on their account (NFR-073). |
| **Operations journal** | The Owner's record, kept outside the application, of every operational access to a school's database or keys: date, school, task, request reference (SC-12). |
| **Read-only mode** | The state an installation enters when the grace period expires or the Owner suspends it: viewing and export keep working, everything else is blocked except a closed list of service writes (BR-026). |
| **Grace period** | How long an installation keeps working without a successful legitimacy check — 7 days from the last success. |

## Google Workspace

| Term | Meaning |
|---|---|
| **Service account** | A machine identity in the Owner's Cloud project used to read a school's data. One per school. Its key never leaves the Owner's infrastructure. The project lives outside every school's domain, so no school controls the others' access. |
| **Client ID** | The service account's public identifier. Given to the school so its super-admin can authorize it. Not a secret. |
| **Domain-wide delegation (DWD)** | The Google mechanism by which a school's super-admin authorizes a service account to read that domain's data, for a named list of scopes. Authorized only by the domain owner — the Owner cannot do it for them. |
| **Impersonation user** | The school account the service account acts as when calling Google APIs: a **technical account** created by the school's super-admin, with no person behind it, not a super-admin, read-only roles only. Never an Admin's account (BR-015). |
| **Technical account** | See *impersonation user*. |
| **Scope** | A named Google permission. Every scope this system uses is read-only. |

## Domain entities

| Term | Русский | Meaning |
|---|---|---|
| **AppUser** | — | An account inside one installation: Admin or Dean. |
| **ClassroomParticipant** | участник Classroom | A person synced from Classroom — Google user id, personal email in the school domain, name. Teachers and students are both this; the Classroom role lives on `CourseMembership`, not on the person. Only personal domain accounts are subjects of the teaching process — group addresses and other accounts are not. |
| **Course** | курс | **Always a Google Classroom course**, never a year of study: name, section, owner, state, calendar id. Schools use courses differently — one per class per year, one reused across years, one per specialty with students of all years — and the system assumes none of them. |
| **CourseMembership** | участие в курсе | A person's membership of one course, with their Classroom role (`teacher` / `student`), when synchronization first and last saw them on the roster, and whether they are on it now. The same person can teach one course and study on another. |
| **CourseWork** | задание / материал | A course item of one of three kinds: graded work (`courseWork` with maximum points), ungraded work (`courseWork` without maximum points — submissions but no grades), or a material (`material` — no submissions). Materials come from a separate Classroom resource. |
| **Submission** | сдача задания | A student's submission of a `CourseWork`: Classroom state, assigned and draft grade in raw points, date of the last turn-in, Google's late flag. Its content is not stored. |
| **MeetSession** | встреча Meet | One Google Meet meeting (conference): meeting code, organizer email, start and end (computed from its connections). A reconnect after a dropped call is a separate meeting. |
| **MeetParticipation** | подключение к встрече | One connection to a meeting: endpoint id, the domain account's email (or only an "other participant" flag for external guests and connections without an account), join time, duration in seconds. Matched to the course roster by email when a report is built — no link to `ClassroomParticipant`. |
| **MeetingCodeLink** | привязка кода встречи | Links a Meet meeting code to a course — suggested automatically or confirmed by a Dean or Admin. A course may have several codes, because a reset Classroom link gets a new one. |
| **SyncState** | статус синхронизации | Status, counters, last error and last successful run of the background synchronization. |
| **WorkspaceConnection** | подключение к Workspace | One installation's connection settings: domain and impersonation user (the technical account). The reference to the service-account secret is installation configuration, not part of it. |
| **ReportTemplate** | шаблон отчёта | A configurable journal/report layout of one of two kinds by data scope: full (every synchronized field, with draft grades) or short, matching the school's paper academic journal (assigned grades only). The template itself sets the layout. |
| **AllowedAdmin** | разрешённый Админ | A Control Plane record: an email permitted to be Admin of one `Installation`. Several per installation are allowed. |
| **InstanceLicenseCheck** | проверка легитимности | The Control Plane's record of checks from one `Installation`: last call, versions reported, answer given. Holds no grace period. |
| **LegitimacyState** | состояние легитимности | The installation's own record: last successful check, last known status, last compatibility state. Drives read-only mode and readiness; survives restarts. |
| **AuditEvent** | событие аудита | An audit row, in both the installation and the Control Plane database: time, actor, action, target, outcome, request id — no personal data. Never updated; installation rows are purged after N years, Control Plane rows are kept (SC-11). |

## Reporting

| Term | Русский | Meaning |
|---|---|---|
| **Journal / gradebook** | журнал успеваемости | Student × (work or material, with date) × grade, over a chosen period. |
| **Full form** | полный объём | Every synchronized field. |
| **Short form** | сокращённый | The layout approximating the school's paper academic journal; assigned grades only. |
| **Journal cell** | клетка журнала | One state per student × coursework: grade, turned in not graded, returned without a grade, not turned in, not due yet, or not turned in with no due date — plus an optional late mark. Ungraded work has no grade or "not graded" state (BR-056). |
| **Draft grade** | черновик оценки | A grade the teacher set but has not returned; the student cannot see it. Shown only in the full journal, marked "draft". |
| **Meet activity statistics** | статистика Meet-занятий | Factual reports on meetings held in Meet for one course and period, for the Dean's oversight of teaching. Not an attendance register: a lesson held in class leaves no Meet data, and that is not an absence. |
| **Unassigned meetings** | непривязанные встречи | Meetings whose code is not linked to a course yet; a Dean or Admin picks the course. |
| **Not a teacher of this course** | не преподаватель этого курса | Mark on a meeting whose organizer is not among the course's teachers on the meeting's date. |
| **Not on the course list** | не в списке курса | Mark on a domain account that joined a course meeting without being on the course roster on the meeting's date. |
| **Other participants** | прочие участники | External guests and connections without a school account, shown apart from students. |

## Terms to avoid

- **Tenant** — this system is not multi-tenant. Say *installation*.
- **Teacher role** — there is no teacher role in the first version. Say
  *`ClassroomParticipant` with the Classroom role `teacher`* when referring to
  data.
- **User** without qualification — say Admin, Dean or Owner.
- **Credentials** when meaning the client ID — the client ID is public. Say
  *service-account key* only for the actual secret.
- **Attendance / посещаемость / прогул** for Meet data — Meet statistics say who
  joined a meeting, not who attended the lesson. Say *Meet activity statistics*.
- **Курс** meaning a year of study — in this system a course is always a
  Classroom course.
- **Group** for a Google group email — group addresses are outside the system's
  logic. A course's students are its roster (`CourseMembership`).
