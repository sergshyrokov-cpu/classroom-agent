# Deployment & Operations Conventions

How a Control Plane and a school installation are brought up, configured,
upgraded and shut down. `dotnet-implementor` implements to these;
`security-reviewer` checks the secret- and channel-related ones.

Derived from `trebovaniya.md` sections 5, 6, 8 and 9, and from `architecture.md`
AD-1/AD-10, `persistence-conventions.md` PC-1/PC-2 and `security-conventions.md`
SC-6/SC-7/SC-9.

This document records what is **already decided elsewhere** and assembles it
into deployment order. Where a question is open it says so and names the item in
`trebovaniya.md` section 7 — it does not fill the gap.

## DC-1 What gets deployed

| Unit | Instances | Owns | Reachability |
|---|---|---|---|
| Control Plane (`ClassroomAgent.ControlPlane`) | one for the whole service | its own database: `Owner`, `Installation`, `AllowedAdmin`, `InstanceLicenseCheck` | Owner UI; plus the service channel to every installation |
| Installation (`ClassroomAgent.Web`) | one per school | its own database: all teaching data of that school | public HTTPS for school staff; private channel to the Control Plane |
| PostgreSQL | one database per unit above | — | reachable only by its own application |

All of it runs on the Owner's infrastructure — no school hosts its own copy
(`trebovaniya.md` §9). Two installations never share a database; that is an
architecture violation, not a tuning choice (AD-1, PC-1).

## DC-2 Bootstrap order

A new service, then a new school, come up in this order. Each step is blocked by
the previous one — the dependency is real, not stylistic
(`docs/product/epic-map.md`).

1. Deploy the Control Plane and its database; run its migrations (DC-4).
2. Owner first-run setup: the single Owner account is claimed once
   (`docs/stories/US-001-owner-first-run-setup.md`). Until it exists nothing
   else is configurable.
3. Owner registers the school as an `Installation`: name, Google Workspace
   domain, status.
4. Owner adds the school's administrator email(s) to `AllowedAdmin` for that
   `Installation`.
5. Owner creates the school's service account in the Owner's Cloud project and
   gives the school's super-admin its client ID and the scope list from
   `trebovaniya.md` §6. The super-admin authorizes domain-wide delegation in the
   school's own Google console — the Owner cannot do this step (BR-032).
6. Deploy the installation: its database, its migrations, its configuration
   (DC-3), and the service-account key placed in the secret store (DC-5).
7. The Admin signs in with Google OAuth, is matched against `AllowedAdmin`, and
   saves the `WorkspaceConnection` (domain + impersonation user). The domain
   must equal the one on the `Installation` or the save is refused (BR-020).
8. The Admin creates Dean accounts. The school is live.

## DC-3 Configuration

- Settings come from `appsettings.json`, `appsettings.{Environment}.json` and
  environment variables — never compile-time constants (AD-10). The prototype's
  hard-coded `admin@dac.ukr.education` is the defect this rule exists to prevent.
- Per-installation configuration is at minimum: its database connection string,
  the Control Plane service endpoint, and the secret-store reference for its
  service-account key.
- The Google Workspace domain and impersonation user are **not** deployment
  configuration: they are entered by the Admin and stored in
  `WorkspaceConnection`, constrained by the `Installation` record (BR-020).
- Connection strings come from configuration or environment variables only, and
  are never committed (PC-1, SC-7).
- Developer exception pages are enabled only in local development; no database
  browser, SQL console or entity explorer ships in any environment (SC-6).

## DC-4 Database migrations

- Schema is created and changed **only** by EF Core migrations committed with
  the code (PC-2). There is no hand-maintained SQL schema and no
  `EnsureCreated()` against a real database.
- Migrations are applied by an **explicit deployment step**, never by
  `db.Database.Migrate()` at application start: ~10 installations are upgraded by
  the Owner, and an automatic migration on boot would upgrade a school's
  production database as a side effect of a restart (PC-2).
- Each unit migrates its own database: the Control Plane project's migrations
  against the Control Plane database, the Infrastructure project's against the
  installation database.
- Schema stays backward compatible across releases (PC-2), which is what makes
  "migrate, then roll the application" safe: the previous version keeps running
  against the new schema while the rollout completes. A migration that drops or
  renames a column in use requires an approved decision.

## DC-5 The service-account key

- The Owner places the key at deployment time into the configured secret store
  (Key Vault, environment variable, or a mounted secret file) — never in the
  repository, never in the installation database, never through a UI (SC-7,
  PC-9, NFR-020).
- One Cloud project owned by the Owner, **a separate service account per
  school** (§6): a leaked key compromises one school, not all of them.
- `WorkspaceConnection` stores only a *reference* to the secret. An entity or
  migration adding a key column is a Critical finding (PC-9).
- The school never receives a key. It receives a client ID, which is public.
- Rotation and compromise response are **not defined** — `trebovaniya.md` §7
  item 9. Do not invent a rotation procedure during a deployment Story.

## DC-6 Network

- The Control Plane ↔ installation service channel runs over a private network
  unreachable from the public internet. This replaces a per-installation token
  and holds **only** while the Owner hosts every installation (SC-9, §9). A
  school hosting its own copy would require re-engineering that channel.
- The school-facing UI is an ordinary public HTTPS application. Do not confuse
  the two: exposing the service endpoint publicly removes the only protection
  the channel has.
- The channel is bidirectional by design: the installation calls the Control
  Plane every 6 hours for the legitimacy check, and the Control Plane pushes
  status changes to the installation's endpoint (HTTP POST, 3 retries with
  exponential backoff) (BR-024, NFR-014).

## DC-7 Suspending and resuming a school

- Suspension is performed by the Owner in the Control Plane, **without logging
  into the school's server** (§9). The push makes it effective immediately; the
  6-hourly check is the fallback if the push does not arrive.
- An installation enters read-only mode when its `Installation` is suspended, or
  when more than 7 days have passed since the last successful check (BR-025,
  NFR-013). Viewing and export keep working; synchronization, account
  management, connection settings and template edits stop (BR-026).
- Read-only mode is a normal operating state, not an outage: it needs no
  deployment action, and it ends by itself once checks succeed again.
- Revoking an `AllowedAdmin` entry is a *different* lever: it removes a person's
  right to configure, but the school keeps working (BR-022). Never conflate the
  two during an operational change.

## DC-8 Moving or retiring a school

- Moving a school to another Workspace domain means creating a **new**
  `Installation` with a new database, not editing the domain of an existing one
  (BR-021, NFR-051). The previous installation's data stays in its own database.
- Decommissioning an installation therefore means deciding what happens to that
  database — which is exactly the retention question that is still open
  (`trebovaniya.md` §7 item 5). Until it is settled, no Story may delete school
  data as part of an operational procedure.

## DC-10 Logging

- **Serilog**, structured, written to a rolling file on the installation: one
  JSON object per line, a new file per day, kept **30 days** with a size cap.
  The console sink is for local development only.
- Levels:
  - `Information` — application start and stop; synchronization start and finish
    with counters; legitimacy check result changes.
  - `Warning` — retried transient Google failures (`429`, `5xx`), a push from
    the Control Plane that had to be retried.
  - `Error` — permission failures (`403 unauthorized_client`, `access_denied`,
    missing scope), unhandled exceptions, a legitimacy check that failed after
    retries, an installation entering read-only mode.
  - `Debug` is disabled outside local development.
- Every log line written inside a request carries the request identifier; every
  line written by a synchronization run carries that run's identifier, so one
  run can be read end to end.
- **SC-10 binds every line without exception**: internal identifiers only —
  never a student or teacher name, email or grade; never a key, connection
  string or token; never a raw Google API error object. A log line may say
  *which* course or participant id was involved, never who they are.
- Logs are the Owner's diagnostic tool. What a Dean or Admin sees about
  synchronization comes from `SyncState`, not from logs (BR-044).

## DC-11 Health checks

- Two endpoints, both answering with status only and no diagnostic detail:
  - **liveness** — the process is up. No dependency is touched.
  - **readiness** — the database is reachable, the last successful legitimacy
    check is within the grace period (BR-025), and the synchronization
    background service is running.
- Both are reachable **only from the Owner's private network**, like the service
  channel (DC-6, SC-9). A publicly reachable endpoint that reports internal
  state would contradict SC-6.
- Readiness reports `Unhealthy` when the installation cannot serve its purpose.
  Read-only mode is **not** unhealthy: it is a defined operating state (DC-7),
  and reporting it as a failure would page the Owner for a school that is
  working exactly as designed.
- Metrics and centralized log collection are **out of scope for the first
  version**: for ~10 installations, readiness plus `SyncState` answer "is this
  school working". Shipping logs off a school's server is a privacy decision
  (`trebovaniya.md` section 5) that has not been taken — deliberately deferred,
  not forgotten.

## DC-12 Version compatibility between the two planes

Ten schools are never upgraded in the same minute, so a version gap is a normal
operating condition, not an incident (`trebovaniya.md` §8, decided in v16).

- **The installation reports its version on the legitimacy check.** Every
  6-hourly check carries the installation's application version (semantic
  versioning, NFR-061) and its contract version. There is no separate
  version-polling mechanism.
- **The Control Plane answers with a compatibility state**: `supported`,
  `upgrade_recommended` or `upgrade_required`, alongside the legitimacy verdict.
- **`upgrade_required` counts as an unsuccessful check.** It does not switch the
  school off: the existing grace period applies, so the installation keeps
  working for 7 days and only then enters read-only (BR-025, DC-7). The Owner
  gets a week; a school never stops mid-lesson because of a deployment.
- The reason is recorded and surfaced to the Admin the way a Google permission
  failure is (AD-5), and logged at `Error` (DC-10). `upgrade_recommended` is
  logged at `Warning` and changes nothing operationally.
- **The Control Plane is upgraded first and must keep working with older
  installations.** The reverse — an installation newer than the Control Plane —
  is not supported: keeping one shared service ahead is cheaper than keeping ten
  school servers ahead. DC-2 step 1 already puts the Control Plane first.
- **`ClassroomAgent.Contracts` evolves additively**: new optional fields are
  allowed; removing a field, renaming it or changing its meaning is not. Both
  sides ignore unknown fields. A breaking change requires a new Control Plane
  endpoint version and a window during which it serves both. `AC-1` governs the
  public REST API, not this channel (AC-7) — this rule is the channel's own.
- **An unparseable push breaks nothing**: the installation logs at `Error` and
  waits for the periodic check, which is the designed guarantee; the push is the
  optimization (NFR-014).

## DC-9 What is not decided yet

A deployment or operations Story that needs one of these raises an Open Decision
and stops. It does not improvise.

| Gap | Where it is tracked |
|---|---|
| Backup and restore of installation databases | §7 item 8 |
| Service-account key rotation and compromise response | §7 item 9 |
| Retention and deletion of student personal data | §7 item 5 |

None of the three blocks writing code today, but items 8 and 9 block the first
production deployment: a school hosted without a backup procedure or a key
rotation procedure is an operational risk the Owner carries personally.
Centralized log collection (DC-11) is a further decision, deliberately deferred
rather than open by omission.
