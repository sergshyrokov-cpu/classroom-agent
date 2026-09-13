# Persistence Conventions

Explicit persistence decisions for this project. `db-designer` enforces these;
`dotnet-implementor` implements to them; `security-reviewer` checks against
them.

Derived from `trebovaniya.md` sections 3, 5 and 9.

## PC-1 Database & runtime mode

- **PostgreSQL** with the **Npgsql** EF Core provider. Chosen because the Owner
  hosts and pays for ~10 installations: no licence cost, no database size
  ceiling, runs in a container (`trebovaniya.md` section 5).
- Connection strings come from configuration or environment variables only —
  never hard-coded, never committed (SC-7).
- **Two separate databases**, never one:
  - the Data Plane database, one per school installation;
  - the Control Plane database, one for the whole service.
  A single database serving several schools is an architecture violation
  (`architecture.md` AD-1).
- Automated tests run against PostgreSQL — **Testcontainers** for integration
  tests, each test class getting an isolated database. The EF Core InMemory
  provider is **forbidden**: it does not enforce constraints, unique indexes or
  cascade rules, so it would hide exactly the defects these tests exist to
  catch.
- No database browser or admin UI is exposed through the application in any
  environment (`security-conventions.md` SC-6).

## PC-2 Schema initialization

- Schema is managed **exclusively through EF Core Migrations**
  (`dotnet ef migrations add <Name>`), generated from the model and committed
  under `src/ClassroomAgent.Infrastructure/Persistence/Migrations/` (Control
  Plane migrations live under its own project). There is no hand-maintained SQL
  schema file.
- Migrations are applied by an explicit deployment step, **not** by
  `db.Database.Migrate()` at application startup: ~10 installations are upgraded
  by the Owner, and an automatic migration on boot would upgrade a school's
  production database as a side effect of a restart.
- `EnsureCreated()` / `EnsureDeleted()` are **forbidden** against any real
  database — they bypass migrations and silently diverge the schema.
- Every entity change ships with the matching migration in the same Story.
  `db-designer` specifies the entity model and expected migration effect.
- No raw `ExecuteSqlRaw` schema changes outside a migration.
- Schema must stay backward compatible across releases (`trebovaniya.md`
  section 5, "Развиваемость"): a migration that drops or renames a column in use
  requires an approved decision.

## PC-3 Identifiers

- Surrogate primary key named `Id`, type `long`, `ValueGeneratedOnAdd()`
  (PostgreSQL `bigint` identity).
- Google-side identifiers (`Course.id`, `ClassroomParticipant.id` = Google
  `userId`, `CourseWork.id`) are **natural keys from an external system**: they
  are stored in their own column with a unique index and used as the upsert key
  during synchronization. They are never the primary key — Google ids are
  strings owned by someone else.
- Meet data is keyed the same way: `MeetSession` on Google's `conference_id`,
  `MeetParticipation` on (`conference_id`, `endpoint_id`), `MeetingCodeLink` on the
  meeting code.
- Natural keys such as email get a unique index, not a primary key.

## PC-4 Explicit column mapping (no EF Core convention defaults)

Every persistent property is configured explicitly via Fluent API in
`OnModelCreating` (entity configuration classes, one per entity):

- `.IsRequired()` matching the nullable reference type annotation;
- `.HasMaxLength(...)` — required for every `string` property;
- `.HasIndex(...).IsUnique()` for uniqueness;
- explicit column name when it differs from the snake_case-mapped property name.

`db-designer` states the exact constraints; the entity configuration and the
resulting migration must both match them.

## PC-5 Naming

- Tables: `snake_case`, singular (`app_user`, `classroom_participant`).
- Columns: `snake_case`.
- Constraints: `uq_<table>_<col>` (unique), `fk_<table>_<ref>` (foreign key),
  `ix_<table>_<col>` (index), `pk_<table>` (primary key).
- The `EFCore.NamingConventions` package
  (`UseSnakeCaseNamingConvention()` on `DbContextOptionsBuilder`) is a required
  dependency, so `EmailAddress` maps to `email_address` without per-property
  overrides.

## PC-6 Audit timestamps

- Every entity has `created_at` and `updated_at`, stored as UTC
  `DateTimeOffset` (PostgreSQL `timestamptz`).
- Populated by an `ISaveChangesInterceptor` registered on the `DbContext`, which
  stamps entities in `Added`/`Modified` state — never by hand in a use case.
- `created_at` is non-null and never updated after insert; `updated_at` is
  non-null.
- **All time is stored in UTC.** Presentation converts for display. Journals and
  Meet statistics span academic periods and Meet events come from Google in UTC;
  mixing local time in storage would corrupt the reports.

## PC-7 Indexes

- Index every foreign key column.
- Index every column used as a lookup key by a repository query. A unique index
  already covers uniqueness and lookups on that column.
- Required by the synchronization design: a unique index on every Google-side
  identifier column (PC-3), because upsert matches on it.
- Required by the reporting design: composite indexes supporting the journal
  query (course + period) and the Meet statistics queries (meeting code + date range, participant + date
  range).
  Several years of courses and grades is the stated growth expectation
  (`trebovaniya.md` section 5).
- `db-designer` lists the required indexes; the migration creates them.

## PC-8 Relationships

- Declare cardinality explicitly via Fluent API alongside navigation properties.
- `CourseMembership` is an explicit entity between `Course` and
  `ClassroomParticipant` carrying the Classroom role (`teacher` / `student`) — the
  role belongs to the membership, not the person (`trebovaniya.md` section 3,
  v23). Unique on (course, participant).
- `MeetingCodeLink` maps a meeting code to one `Course`: unique on the code,
  several codes per course (a reset Classroom link gets a new code). `MeetSession`
  reaches its course only through this link and has no course column of its own,
  so re-linking a code moves all its meetings at once.
- No lazy-loading proxies package. Navigation properties are loaded explicitly
  per query via `.Include()` / `.ThenInclude()` in the repository.
- Cascade behavior is explicit and minimal: `.OnDelete(DeleteBehavior.Restrict)`
  unless a stated reason justifies `Cascade`. Student grade history must not
  disappear because a parent row was removed.

## PC-9 Sensitive data

- **The service-account key is never stored in the database.**
  `WorkspaceConnection` holds the domain, the impersonation user and a
  *reference to a secret* (name or path); the key itself lives in the configured
  secret store, placed there by the Owner at deployment
  (`trebovaniya.md` sections 5 and 9, `security-conventions.md` SC-7). A
  migration or entity adding a key/credential column is a Critical finding.
- Dean passwords are stored only as an ASP.NET Core Identity password hash.
  Admin has no local password column at all — Admin authenticates through Google
  OAuth (external login).
- **`AuditEvent` is append-only**: no use case updates or deletes a row, and the
  entity exposes no way to (SC-11). It carries internal identifiers only — a
  migration adding a name, email or grade column to it is a Critical finding.
- Journals, grades and Meet participation are personal data of students, potentially
  minors. `db-designer` marks such columns and states their handling rules.
  Retention is decided (`trebovaniya.md` section 5, v19) and enforced only by
  the purge of PC-11.

## PC-10 Synchronization is idempotent

- Every sync writes through an upsert keyed on the Google-side identifier
  (PC-3). Re-running a sync must not create duplicates
  (`trebovaniya.md` Epic 1).
- Incremental behavior: already-known participants are not re-fetched.
- `SyncState` records status, counters, the last error and the last successful
  run. It is the only place sync progress is reported from — a use case never
  infers progress by counting rows.

## PC-11 Retention purge

Decided in `trebovaniya.md` section 5 (v19). The school is the data controller;
the product enforces the period the school agreed with the Owner.

- **The retention period N (years) is a required installation setting** (DC-3),
  set by the Owner at deployment from the written agreement with the school. An
  installation without it refuses to start — there is no default and no
  "keep forever".
- **The unit is the course.** A `Course` and everything that depends on it —
  `CourseMembership` rows, `MeetingCodeLink` rows, `CourseWork`, `Submission`
  with its grades, and the `MeetSession` / `MeetParticipation` rows reached through
  its meeting codes — are deleted when the course is
  archived in Google or no longer returned by it, **and** its most recent
  Google-side update time (of the course or anything under it) is more than N
  years ago.
- A `ClassroomParticipant` is deleted when no remaining course references it.
- **Deletion is physical** and happens in one transaction per course, so a
  course is never left half-deleted (AD-7). Rows are removed child-first by the
  purge use case; foreign keys stay `Restrict` (PC-8) — the purge does not rely
  on cascades.
- **Synchronization never deletes teaching data.** A participant who disappears
  from Google stays until their courses expire; only the purge deletes.
- `AuditEvent` rows are purged when their own timestamp is more than N years old,
  independently of the courses they mention (SC-11).
- A `MeetSession` whose meeting code is linked to no course is purged with its
  participations when its own date is more than N years old (`trebovaniya.md`
  section 5, v23).
- Each purge run writes one `AuditEvent`: actor `system`, counts of courses,
  participants and audit rows removed, no personal data.
- **The purge runs in read-only mode** — the single write permitted there
  (BR-075). It runs in the Web host's background services; once a day is enough.

## PC-12 Meet data

Decided in `trebovaniya.md` sections 3 and 4 (v23).

- A Meet `call_ended` event carries about 60 fields, most of them network
  telemetry. Only what the reports need is stored: conference id, meeting code,
  organizer, participant identifier and whether it is external, join time and
  duration. Telemetry is discarded at ingestion.
- **Durations are stored in seconds, never as percentages.** A future version
  relates them to lesson length from a timetable (Epic 11); stored percentages
  would have to be recomputed.
- **Each Google meeting is its own `MeetSession`.** A reconnect after a dropped
  call is a new conference in Google and a new row here; nothing is merged.
- Meet data is pulled regularly and kept locally beyond Google's 180-day window.
- A `MeetSession` whose meeting code has no `MeetingCodeLink` is still stored and
  appears in the unassigned-meetings list; it is purged N years after its own
  date (PC-11).
