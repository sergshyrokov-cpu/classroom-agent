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
attendance per school. Queries backing the journal and attendance reports are
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

## Security and privacy

**NFR-020** The service-account key is never in the repository, never in the
installation database, and never uploadable through the UI. It is placed by the
Owner at deployment in a secret store. *(§5, `security-conventions.md` SC-7)*

**NFR-021** Every Google OAuth scope is read-only. *(§1)*

**NFR-022** Journals and attendance contain personal data of students who may be
minors; access is limited to the Admin and Dean roles. *(§5)*

**NFR-023** Personal data never appears in application logs or in an HTTP error
body. *(§5, `security-conventions.md` SC-10)*

**NFR-024** Retention and deletion policy for student personal data is **not yet
defined** and must be settled before production. *(§5, §7 item 5)*

**NFR-025** Audit requirements — who synchronized, who exported personal data,
who managed accounts — are **not yet defined**. *(§7 item 7)*

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

**NFR-034** Backup and restore of installation databases is **not yet
specified**, although the Owner carries hosting responsibility for ~10 schools.
*(§7 item 8)*

## Export

**NFR-040** Excel and Word export are both supported, including the school's
existing Excel templates. *(§5, §6)*

**NFR-041** Report templates are a configurable resource, not hard-coded — the
opposite of the prototype. *(§5)*

## Portability

**NFR-050** The Google Workspace domain, the impersonation user and the
service-account reference are configuration, never compile-time constants. The
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

**NFR-062** Target framework **.NET 10 (LTS)**, supported to November 2028.
.NET 9 is out of support and .NET 8 LTS ends in November 2026. *(§8)*

## User interface

**NFR-070** Responsive web UI that works on a phone. No native mobile
application. *(§5)*

**NFR-071** Server-rendered Razor with a REST API behind it; not Blazor. *(§8)*

**NFR-072** The session token lives in an `httpOnly` cookie. No password and no
Google credential is stored client-side. *(§8)*
