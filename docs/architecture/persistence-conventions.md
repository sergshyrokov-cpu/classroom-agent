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
  `userId`, `CourseWork.id`, `Submission.id`) are **natural keys from an external system**: they
  are stored in their own column with a unique index and used as the upsert key
  during synchronization. They are never the primary key — Google ids are
  strings owned by someone else.
- Coursework and materials are separate Classroom resources, so a `CourseWork`
  row is unique on (Classroom resource — `courseWork` or `courseWorkMaterials` —,
  Google id) rather than on the Google id alone. Graded versus ungraded work is
  **not** part of the key and is not stored: it follows from whether maximum
  points are set, so a teacher adding or removing points updates the same row
  (`trebovaniya.md` section 3, v32).
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
  non-null. For `AuditEvent`, which is never updated (PC-9), `updated_at` always
  equals `created_at`.
- **All time is stored in UTC.** Presentation converts for display into the
  school's time zone — a required installation setting (DC-3), which also sets
  the day boundaries of a selected period (`trebovaniya.md` section 5, v40).
  Journals and Meet statistics span academic periods and Meet events come from
  Google in UTC; mixing local time in storage would corrupt the reports.

## PC-7 Indexes

- Index every foreign key column.
- Index every column used as a lookup key by a repository query. A unique index
  already covers uniqueness and lookups on that column.
- Required by the synchronization design: a unique index on every Google-side
  identifier column (PC-3), because upsert matches on it.
- Required by the reporting design: composite indexes supporting the journal
  query (course + period), the Meet statistics queries (meeting code + date
  range, participant email + date range — PC-12), and roster-on-a-date lookups
  on `CourseMembership` (course + `first_seen_at` / `last_seen_at`, PC-8).
  Several years of courses and grades is the stated growth expectation
  (`trebovaniya.md` section 5).
- `db-designer` lists the required indexes; the migration creates them.

## PC-8 Relationships

- Declare cardinality explicitly via Fluent API alongside navigation properties.
- `CourseMembership` is an explicit entity between `Course` and
  `ClassroomParticipant` carrying the Classroom role (`teacher` / `student`) — the
  role belongs to the membership, not the person (`trebovaniya.md` section 3,
  v23). Unique on (course, participant). It also carries `first_seen_at`,
  `last_seen_at` and `on_roster` — what synchronization observed, since Classroom
  gives no join or leave dates (BR-051). Leaving the roster clears `on_roster`;
  the row is never deleted by synchronization. Journal and Meet queries filter by
  these dates, so they are indexed with the course (PC-7).
- `MeetingCodeLink` maps a meeting code to one `Course`: unique on the code,
  several codes per course (a reset Classroom link gets a new code). `MeetSession`
  reaches its course only through this link and has no course column of its own,
  so re-linking a code moves all its meetings at once.
- No lazy-loading proxies package. Navigation properties are loaded explicitly
  per query via `.Include()` / `.ThenInclude()` in the repository.
- Cascade behavior is explicit and minimal: `.OnDelete(DeleteBehavior.Restrict)`
  unless a stated reason justifies `Cascade`. Student grades must not
  disappear because a parent row was removed.

## PC-9 Sensitive data

- **The service-account key is never stored in the database.**
  `WorkspaceConnection` holds only the domain and the impersonation user. The key
  lives in the configured secret store, placed there by the Owner at deployment,
  and the reference to it (name or path) lives in the installation's
  configuration (DC-3) — not in the database (`trebovaniya.md` sections 3, 5 and
  9, v33; `security-conventions.md` SC-7). A migration or entity adding a key,
  credential or secret-reference column is a Critical finding.
- Dean passwords are stored only as an ASP.NET Core Identity password hash.
  Admin has no local password column at all — Admin authenticates through Google
  OAuth (external login).
- **`AuditEvent` is never updated and is deleted only by the retention purge**
  (PC-11): no other use case updates or deletes a row, and the entity exposes no
  way to (SC-11). It carries internal identifiers only — a
  migration adding a name, email or grade column to it is a Critical finding. Its
  actor and target ids have **no foreign key** to `AppUser` or any other entity:
  the rows outlive what they name (PC-11).
- Journals, grades and Meet participation are personal data of students, potentially
  minors. `db-designer` marks such columns and states their handling rules.
  Retention is decided (`trebovaniya.md` section 5, v19) and enforced only by
  the purge of PC-11.

## PC-10 Synchronization is idempotent

- Every sync writes through an upsert keyed on the Google-side identifier
  (PC-3). Re-running a sync must not create duplicates
  (`trebovaniya.md` Epic 1).
- Incremental behavior: already-known participants are not re-fetched.
- A course not yet in the database whose last activity in Google — the course,
  its `CourseWork`, its `Submission` rows — is already more than N years ago is
  not imported, so the purge and the next sync never undo each other. A course
  already in the database is always updated until the purge deletes it
  (`trebovaniya.md` section 5, v36, v55).
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
  its meeting codes — are deleted from the installation database when the
  course's **last activity** is more than N years ago, whatever the course's
  state (`trebovaniya.md` section 5, v36). Nothing is deleted in Google.
- **Last activity** is the latest of: the course's own update time; creation or
  update of any of its `CourseWork` rows; update of any of its `Submission` rows;
  the start of any `MeetSession` reached through its meeting codes.
- **A leaver has their own expiry.** A `CourseMembership` with `on_roster` false
  and `last_seen_at` more than N years ago is deleted together with that person's
  `Submission` rows in the course and their `MeetParticipation` rows (matched by
  email, PC-12) in meetings reached through the course's meeting codes — even if
  the course itself is still kept (`trebovaniya.md` section 5, v31).
- **Known limitation (v55):** `Submission` rows of people still on the roster
  have no expiry of their own; they stay until the course or the person as a
  leaver expires.
- A `ClassroomParticipant` is deleted when no remaining `CourseMembership`
  references it. Submissions of a student already off the roster at the first
  synchronization, who therefore has no membership, are open
  (`trebovaniya.md` §7 item 14).
- **Deletion is physical** and happens in one transaction per course, so a
  course is never left half-deleted (AD-7). Rows are removed child-first by the
  purge use case; foreign keys stay `Restrict` (PC-8) — the purge does not rely
  on cascades.
- **Synchronization never deletes teaching data.** A participant who disappears
  from Google stays until their courses or their own leaver period expire; only
  the purge deletes.
- Installation `AuditEvent` rows are purged when their own timestamp is more than
  N years old, independently of the courses they mention (SC-11). This is the only
  deletion of audit rows, and a test proves it removes rows older than N and
  nothing newer. Control Plane audit rows are never purged (SC-11, v45).
- **`AppUser` rows** (Admin and Dean) are deleted when their last successful
  sign-in — or creation, if they never signed in — is more than N years ago,
  whether or not disabled; the installation cannot know an Admin was revoked.
  Audit rows and other rows that name an account (e.g. who confirmed a
  `MeetingCodeLink`) keep the internal id without a foreign key, so the deletion
  never cascades or blocks. Audit rows naming the account may be newer than its
  last sign-in — refused sign-ins, an Admin disabling it — and stay until their
  own expiry (`trebovaniya.md` section 5, v45, v53).
- **Every `MeetSession`** — whether its meeting code is linked to a course or
  not — is purged with its `MeetParticipation` rows when its own date is more
  than N years old, even if the course is still kept (`trebovaniya.md` section 5,
  v23, v55).
- Each purge run writes one `AuditEvent`: actor `system`, counts of courses,
  leavers' memberships, Meet meetings, participants, accounts and audit
  rows removed, no personal data.
- **The purge runs in read-only mode** — one of the service writes permitted
  there (BR-026, BR-075). It runs in the Web host's background services, once a
  day.

## PC-12 Meet data

Decided in `trebovaniya.md` sections 3 and 4 (v23).

- A Meet `call_ended` event carries about 60 fields, most of them network
  telemetry. Only what the reports need is stored. Telemetry is discarded at
  ingestion.
  - `MeetSession`: conference id, meeting code, organizer email, start and end.
    Google sends no session start or end: they are computed at ingestion from the
    connections (earliest join, latest join + duration) and stored.
  - `MeetParticipation`: `endpoint_id`, the domain account's email — or, for
    external guests and connections without an account, only an "other
    participant" flag with no address or name — join time and duration.
- **`MeetParticipation` has no foreign key to `ClassroomParticipant`.** Whether a
  person was a student or teacher of the course is resolved when a report is
  built, by matching the email against the roster on the meeting's date
  (BR-051). A migration adding such a foreign key, or storing an external guest's
  identity, is a finding (`trebovaniya.md` section 3, v37).
- **Durations are stored in seconds, never as percentages.** A future version
  relates them to lesson length from a timetable (Epic 11); stored percentages
  would have to be recomputed.
- **Each Google meeting is its own `MeetSession`.** A reconnect after a dropped
  call is a new conference in Google and a new row here; nothing is merged.
- Meet data is pulled regularly and kept locally beyond Google's 180-day window.
- A `MeetSession` whose meeting code has no `MeetingCodeLink` is still stored and
  appears in the unassigned-meetings list. Every `MeetSession`, linked or not,
  is purged N years after its own date (PC-11, v55).

## PC-13 Coursework and submission data

Decided in `trebovaniya.md` sections 3 and 4 (v24).

- A `Submission` row stores only: the Classroom state, `assignedGrade`,
  `draftGrade`, the date of the last turn-in, and Google's `late` flag.
  `CourseWork` stores its due date and `maxPoints`, each only if set.
- **Grades are stored as raw points** together with the coursework's maximum.
  Conversion to a school scale belongs to report templates, never to stored data.
- The last turn-in date is read from `submissionHistory` during synchronization;
  **the history itself is not stored** (Epic 12).
- **Submission content is never stored** — no files, answers or attachments. Only
  the fact of submission reaches the database.
- Rubric grades are not stored (Epic 12).
- Materials are read through `courseWorkMaterials` and have no submissions.
