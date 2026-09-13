# Business Glossary

> **Normative for terminology.** Use these terms in specifications, designs,
> code and tests; see "Terms to avoid" at the end.

Terms used consistently across specifications, designs, code and tests. Russian
terms from `trebovaniya.md` are given so the two documents can be read together.

## Roles

| Term | Русский | Meaning |
|---|---|---|
| **Owner** | Владелец | The one person who owns, hosts and supports the service for all schools. Lives in the Control Plane; has no account in any installation. |
| **Admin** | Админ | A Google Workspace domain administrator at one school, who is also the administrator of that school's installation. One email serves as OAuth login, `AllowedAdmin` entry, `AppUser`, and impersonation user. |
| **Dean** | Декан, учебная часть | The primary user: views courses, gradebooks and Meet activity statistics, exports reports, triggers synchronization. |
| **Teacher** | Преподаватель | A person teaching a course. **Data only in the first version** — not an account (Epic 7). |
| **Student** | Студент | A person enrolled in a course. **Data only** (Epic 7). |
| **Super-admin** | суперАдмин | The school's Google Workspace super-administrator. Authorizes domain-wide delegation once, in the school's own Google console. Not a role in this system. |

## Deployment

| Term | Meaning |
|---|---|
| **Installation** | One school's deployment: its own backend process and its own database. Also the Control Plane entity recording that school — name, domain, status, assigned service-account client ID. |
| **Data Plane** | The per-school application. One per installation. |
| **Control Plane** | The single shared service holding Owner authentication, the `Installation` registry, the `AllowedAdmin` list and legitimacy checks. Separate database. |
| **Read-only mode** | The state an installation enters when the grace period expires or the Owner suspends it: viewing and export keep working, everything else is blocked. |
| **Grace period** | How long an installation keeps working without a successful legitimacy check — 7 days from the last success. |

## Google Workspace

| Term | Meaning |
|---|---|
| **Service account** | A machine identity in the Owner's Cloud project used to read a school's data. One per school. Its key never leaves the Owner's infrastructure. |
| **Client ID** | The service account's public identifier. Given to the school so its super-admin can authorize it. Not a secret. |
| **Domain-wide delegation (DWD)** | The Google mechanism by which a school's super-admin authorizes a service account to read that domain's data, for a named list of scopes. Authorized only by the domain owner — the Owner cannot do it for them. |
| **Impersonation user** | The school account the service account acts as when calling Google APIs. The same account as the Admin's. |
| **Scope** | A named Google permission. Every scope this system uses is read-only. |

## Domain entities

| Term | Русский | Meaning |
|---|---|---|
| **AppUser** | — | An account inside one installation: Admin or Dean. |
| **ClassroomParticipant** | участник Classroom | A person synced from Classroom — Google user id, personal email in the school domain, name. Teachers and students are both this; the Classroom role lives on `CourseMembership`, not on the person. Only personal domain accounts are subjects of the teaching process — group addresses and other accounts are not. |
| **Course** | курс | **Always a Google Classroom course**, never a year of study: name, section, owner, state, calendar id. Schools use courses differently — one per class per year, one reused across years, one per specialty with students of all years — and the system assumes none of them. |
| **CourseMembership** | участие в курсе | A person's membership of one course, with their Classroom role (`teacher` / `student`). The same person can teach one course and study on another. |
| **CourseWork** | задание / материал | A course item: either graded work (`courseWork`) or an ungraded material (`material`). |
| **Submission** | сдача задания | A student's submission of a `CourseWork`, with its grade if any. |
| **MeetSession** | встреча Meet | One Google Meet meeting (conference): meeting code, organizer, start, end. A reconnect after a dropped call is a separate meeting. |
| **MeetParticipation** | подключение к встрече | One participant's connection to a meeting: account (or external / no account), join time, duration in seconds. |
| **MeetingCodeLink** | привязка кода встречи | Links a Meet meeting code to a course — suggested automatically or confirmed by a Dean or Admin. A course may have several codes, because a reset Classroom link gets a new one. |
| **SyncState** | статус синхронизации | Status, counters, last error and last successful run of the background synchronization. |
| **WorkspaceConnection** | подключение к Workspace | One installation's connection settings: domain, impersonation user, and a *reference* to the service-account secret. |
| **ReportTemplate** | шаблон отчёта | A configurable journal/report layout: full, short, or matching the school's paper academic journal. |
| **AllowedAdmin** | разрешённый Админ | A Control Plane record: an email permitted to be Admin of one `Installation`. Several per installation are allowed. |
| **InstanceLicenseCheck** | проверка легитимности | The result of the periodic check of one `Installation`. |

## Reporting

| Term | Русский | Meaning |
|---|---|---|
| **Journal / gradebook** | журнал успеваемости | Student × (work or material, with date) × grade, over a chosen period. |
| **Full form** | полный объём | Every synchronized field. |
| **Short form** | сокращённый | The layout approximating the school's paper academic journal. |
| **Meet activity statistics** | статистика Meet-занятий | Factual reports on meetings held in Meet for one course and period, for the Dean's oversight of teaching. Not an attendance register: a lesson held in class leaves no Meet data, and that is not an absence. |
| **Unassigned meetings** | непривязанные встречи | Meetings whose code is not linked to a course yet; a Dean or Admin picks the course. |
| **Not a teacher of this course** | не преподаватель этого курса | Mark on a meeting whose organizer is not among the course's teachers. |
| **Not on the course list** | не в списке курса | Mark on a domain account that joined a course meeting without being on the course roster. |
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
