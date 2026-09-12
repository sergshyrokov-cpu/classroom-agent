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

## DC-9 What is not decided yet

A deployment or operations Story that needs one of these raises an Open Decision
and stops. It does not improvise.

| Gap | Where it is tracked |
|---|---|
| Backup and restore of installation databases | §7 item 8 |
| Service-account key rotation and compromise response | §7 item 9 |
| Application observability: logging, health checks, metrics | §7 item 12 |
| Behaviour when Control Plane and installation versions differ | §7 item 13 |
| Retention and deletion of student personal data | §7 item 5 |

Two of these bite at the first production deployment rather than later: without
item 12 a stalled synchronization is invisible until someone phones the school,
and without item 13 the first partial upgrade of ~10 installations has undefined
behaviour.
