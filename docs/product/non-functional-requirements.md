# Non-Functional Requirements

> **Normative.** A Specification cites these by id.

From `trebovaniya.md` section 5, plus the parameters fixed in sections 8 and 9.
A Specification cites these by id.

## Performance and responsiveness

**NFR-001** Synchronization never blocks a web request. It runs in a
`BackgroundService`; a request that starts a sync returns immediately.
*(§5)*

**NFR-002** Any endpoint returning a collection that can grow unbounded is
paginated from day one — default page size 20, maximum 100.
*(`api-conventions.md` AC-8)*

**NFR-003** The system is expected to hold several years of courses, grades and
Meet data per school. Queries backing the journal and Meet reports are
supported by explicit indexes rather than left to a table scan.
*(§5, `persistence-conventions.md` PC-7)*

## Reliability

**NFR-010** Transient Google API failures (`429`, `5xx`) are retried with
exponential backoff. *(Epic 1)*

**NFR-011** Permission failures are not retried: they are a configuration
problem, not a transient one, and are surfaced to the Admin with a diagnosable
message. *(Epic 1, Epic 6)*

**NFR-012** Synchronization is idempotent — repeated runs upsert and never
duplicate. *(Epic 1)*

**NFR-013** An installation keeps working for **7 days** without a successful
Control Plane check before entering read-only mode. A weekend outage must not
take a school down. *(§9)*

**NFR-014** The legitimacy check runs every **6 hours**; status changes are also
pushed immediately by the Control Plane, with 3 retries and exponential backoff.
The periodic check is the guarantee; the push is the optimization. *(§9)*

**NFR-015** Each installation writes structured logs (Serilog, JSON, one file
per day, retained 30 days) and exposes liveness and readiness endpoints reachable
only from the Owner's private network. Readiness distinguishes "cannot serve"
(`Unhealthy`) from read-only or a failing legitimacy check within the grace
period (`Degraded`, still HTTP 200). *(§8, `deployment-conventions.md` DC-10,
DC-11)*

**NFR-016** Metrics and centralized log collection are out of scope for the
first version. Readiness plus `SyncState` are how the Owner sees that a school is
working. *(§8, DC-11)*

**NFR-017** An installation reports its application and contract version on
every legitimacy check. A Control Plane answer of `upgrade_required` counts as an
unsuccessful check, so the 7-day grace period applies before read-only — a
version gap never stops a school immediately. The Control Plane is always
upgraded first and supports older installations. *(§8,
`deployment-conventions.md` DC-12)*

## Security and privacy

**NFR-020** The service-account key is never in the repository, never in the
installation database, and never uploadable through the UI. It is placed by the
Owner at deployment in a secret store; the reference to it lives in installation
configuration, not in the database. *(§5, v33, `security-conventions.md` SC-7)*

**NFR-021** Every Google OAuth scope is read-only. *(§1)*

**NFR-022** Journals and Meet statistics contain personal data of students who may be
minors; access is limited to the Admin and Dean roles. *(§5)*

**NFR-023** Personal data never appears in application logs or in an HTTP error
body. *(§5, `security-conventions.md` SC-10)*

**NFR-024** Student personal data is kept for a retention period N set per
installation by the Owner from the school's written agreement; an installation
without it does not start. Expired data is physically deleted by a daily purge
that also runs in read-only mode. *(§5, v47, `persistence-conventions.md` PC-11)*

**NFR-025** Audited actions are recorded in an `AuditEvent` table whose rows are
never updated and are deleted only by the retention purge — for example sign-ins
and refusals, account management, connection changes, manual synchronization,
and every export of a journal or report; the full list is
`security-conventions.md` SC-11. A row identifies the
actor, action, target and outcome by internal id only and never carries personal
data. A refused sign-in with no account records an anonymous actor and the
refusal category, never the login typed. There is no audit screen in the first
version; installation rows are purged after the retention period counted from
their own timestamp, Control Plane rows are kept indefinitely. *(§5,
`security-conventions.md` SC-11)*

**NFR-026** The Control Plane has no path to a school's teaching data and
receives no school statistics; the service channel carries installation id,
versions, status and compatibility state, the `Installation`'s domain and client
ID in the legitimacy check response (v54), plus the email checked at an Admin
login. The whole Control Plane, the Owner UI included, is reachable only from
the Owner's private network, and the Owner account is created only with a
one-time setup code printed to the server console (v35). The Owner's
server-level access is operational, governed by the written agreement with the
school and recorded in an operations journal. *(§9, `security-conventions.md`
SC-2, SC-9, SC-12)*

**NFR-027** Each school's service-account key is replaced every 90 days without
interrupting Google access and without action from the school; the installation
restarts briefly, outside teaching hours, and its startup access self-check
confirms the new key in the log (DC-5, v54). On a suspected leak the key is
deleted immediately, a new one issued, usage reviewed, and the school informed
without delay. *(§9, `deployment-conventions.md` DC-5)*

**NFR-028** School data leaves the installation only for Google (read-only, plus
Google OAuth for Admin sign-in, authentication only) and the Control Plane
service channel. Encrypted backups stay on the Owner's own infrastructure, never
with an external storage provider (v54). No AI, speech-recognition, analytics,
telemetry or other external service receives school data in the first version.
*(§6, v52, `business-rules.md` BR-078, `security-conventions.md` SC-13)*

## Data storage

**NFR-030** PostgreSQL with EF Core (Npgsql). No licence cost and no database
size ceiling, because the Owner hosts and pays for ~10 installations. *(§5)*

**NFR-031** Each school has its own database. A shared multi-tenant database is
forbidden. *(§9, `architecture.md` AD-1)*

**NFR-032** Schema is managed exclusively through EF Core migrations, applied by
an explicit deployment step, never automatically at application startup.
*(`persistence-conventions.md` PC-2)*

**NFR-033** Schema changes stay backward compatible across releases; dropping or
renaming a column in use requires an approved decision. *(§5)*

**NFR-034** Every database — each installation and the Control Plane — is
dumped nightly, kept 30 days off the database server and encrypted, and a restore
is tested every quarter. Worst-case loss is one day of local changes. *(§9,
`deployment-conventions.md` DC-13)*

## Export

**NFR-040** Excel and Word export are both supported, including the school's
existing Excel templates. *(§5, §6)*

**NFR-041** Report templates are a configurable resource, not hard-coded — the
opposite of the prototype. *(§5)*

## Portability

**NFR-050** The Google Workspace domain and the impersonation user are settings
entered by the Admin; the service-account reference is installation
configuration. None of them is a compile-time constant (v33). The
prototype's hard-coded `admin@dac.ukr.education` is a defect being fixed.
*(§5)*

**NFR-051** One active Workspace domain per installation. Moving a school to a
different domain means a new `Installation`, not an edit. *(§6, §9)*

## Maintainability

**NFR-060** Layered structure with enforced boundaries — Domain → Application →
Infrastructure → Presentation — so new functionality does not require rewriting
the core. *(§5, `architecture.md` AD-3)*

**NFR-061** Semantic versioning of releases and versioned EF Core migrations.
*(§5)*

**NFR-062** Target framework **.NET 10 (LTS)**, supported to 14 November 2028.
.NET 9 (STS) and .NET 8 (LTS) both reach end of support on 10 November 2026.
*(§8, v49)*

## User interface

**NFR-070** Responsive web UI that works on a phone. No native mobile
application. *(§5)*

**NFR-071** Server-rendered Razor with a REST API behind it; not Blazor. *(§8)*

**NFR-072** The session token lives in an `httpOnly` cookie. No password and no
Google credential is stored client-side. *(§8)*

**NFR-073** The installation UI is available in Ukrainian and English. The
school's default language is an installation setting set by the Owner, Ukrainian
if unset; each Admin and Dean may choose their own, stored on their account.
Screens, error messages and hints, the super-admin connection instructions, and
the labels the system writes into exports (in the exporting user's language) are
translated; text a Dean wrote into a template and data from Google are shown as
is. Date and number formats follow the language. The Control Plane UI is
likewise in Ukrainian and English: Ukrainian by default, the Owner's choice
stored on the Owner account. A new language is added through translation files
without code changes. Time zone handling is NFR-074. *(§5, v51)*

**NFR-074** The school's time zone is a required installation setting set by the
Owner at deployment; an installation without it does not start. All time is
stored in UTC; dates are shown, and the day boundaries of a selected period are
set, in the school's time zone. Due dates are compared as exact UTC instants.
*(§5, v40, `persistence-conventions.md` PC-6, `deployment-conventions.md` DC-3)*
