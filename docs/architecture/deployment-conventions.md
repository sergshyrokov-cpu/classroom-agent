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
| Control Plane (`ClassroomAgent.ControlPlane`) | one for the whole service | its own database: `Owner`, `Installation`, `AllowedAdmin`, `InstanceLicenseCheck`, `AuditEvent` | private network only, HTTPS only: the Owner UI and the service channel to every installation (DC-6) |
| Installation (`ClassroomAgent.Web`) | one per school | its own database: all teaching data of that school | public HTTPS with HSTS for school staff; private port over HTTP for the Control Plane channel and health checks (DC-6) |
| PostgreSQL | one database per unit above | — | reachable only by its own application |

All of it runs on the Owner's infrastructure — no school hosts its own copy
(`trebovaniya.md` §9). Two installations never share a database; that is an
architecture violation, not a tuning choice (AD-1, PC-1).

## DC-2 Bootstrap order

A new service, then a new school, come up in this order. Each step is blocked by
the previous one — the dependency is real, not stylistic
(`docs/product/epic-map.md`).

1. Issue the Control Plane's certificate — from the Owner's internal certificate
   authority or from Let's Encrypt via a DNS challenge (DC-6). If it comes from the
   internal authority, add that authority's root certificate to the trusted roots
   of every device the Owner uses to reach the Control Plane — a browser
   certificate warning is never clicked through (`trebovaniya.md` §9, v65). Create its Data
   Protection key directory on a persistent volume (DC-3). Deploy the Control
   Plane and its database; run its migrations (DC-4).
2. Owner first-run setup: the Control Plane prints a one-time setup code to the
   server console, and the single Owner account is claimed once with it
   (`docs/stories/US-001-owner-first-run-setup.md`). Until it exists nothing
   else is configurable.
3. Owner creates the school's service account in the Owner's Cloud project, which
   lives outside every school's domain (DC-5).
4. Owner registers the school as an `Installation`: name, Google Workspace
   domain, status, and that service account's client ID — required
   (`trebovaniya.md` §3, v43).
5. Owner adds the school's administrator email(s) to `AllowedAdmin` for that
   `Installation` — at least two Admins (BR-013), each ideally on a separate
   admin account rather than the one used for daily mail and teaching — and
   gives the school's super-admin the client ID and the scope list from
   `trebovaniya.md` §6. The super-admin authorizes domain-wide delegation in the
   school's own Google console and creates the technical account with read-only
   roles (BR-015) — the Owner cannot do either step (BR-032).
6. Deploy the installation: its database, its migrations, its configuration
   (DC-3) — including the retention period agreed with the school and the
   school's time zone, both required — and the service-account key placed in
   the secret store (DC-5). Create its Data Protection key directory on a
   persistent volume. If the Control Plane certificate comes from the internal
   certificate authority, add that authority's root certificate to the trusted
   roots of the installation's server (DC-6).
7. The Admin signs in with Google OAuth, is matched against `AllowedAdmin`, and
   saves the `WorkspaceConnection` (domain + the technical account as
   impersonation user). The domain, and the domain of that account's email, must
   equal the one on the `Installation` or the save is refused (BR-020).
8. The Admin creates Dean accounts. The school is live.

## DC-3 Configuration

- Settings come from `appsettings.json`, `appsettings.{Environment}.json` and
  environment variables — never compile-time constants (AD-10). The prototype's
  hard-coded `admin@dac.ukr.education` is the defect this rule exists to prevent.
- Per-installation configuration is at minimum: its database connection string,
  the Control Plane service endpoint (an `https://` address, DC-6), the
  secret-store reference for its service-account key, the Data Protection key
  directory (SC-7), the retention period N (PC-11), and the school's time zone
  (an IANA id such as `Europe/Kyiv`, PC-6). The retention period and the time
  zone are required: an installation without either refuses to start. Optional:
  the school's default UI language (`uk` or `en`, `uk` if unset — NFR-073).
- The Control Plane's own configuration includes its Data Protection key
  directory as well (SC-7).
- Trust in the Control Plane's certificate is server configuration, not
  application configuration: an internal certificate authority's root is
  installed on the installation's server (DC-2).
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
- **The Owner's Cloud project lives outside every school's domain** — owned by
  the Owner's own account or organisation, never created inside a school's
  Workspace organisation — with a second owner or a recovery path and two-factor
  authentication (SC-12, `trebovaniya.md` §6, v50). The prototype's project
  `dac-classroom-agent` sits in DAC's organisation and is not this project.
- The *reference* to the secret lives only in the installation's configuration
  (DC-3); `WorkspaceConnection` holds neither key nor reference. An entity or
  migration adding a key or reference column is a Critical finding (PC-9).
- The school never receives a key. It receives a client ID, which is public.
- **Delegation is bound to the service account's client ID, not to a key.**
  Replacing a key therefore needs nothing from the school's super-admin. A service account can hold several keys at once, so
  Google access is never interrupted: the old key stays valid until the new one
  is checked. The installation itself restarts for a few seconds, outside
  teaching hours (`trebovaniya.md` §9, v22, v44).
- **Planned rotation every 90 days** per school — conveniently all at once each
  quarter, together with the restore test (DC-13). Steps: create a new key → put
  it in the secret store under the **same reference** → restart the installation
  outside teaching hours → confirm in the installation log that the startup
  access self-check succeeded → delete the old key in Google Cloud → record it in
  the operations journal (SC-12). The school takes no part: the self-check runs
  the same test calls as "check access" on every start and logs the result
  (DC-10, `trebovaniya.md` Epic 6, v54). No code and no database change. If the
  installation is in read-only mode, the self-check does not run (BR-026): after
  the mode ends, restart the installation again outside teaching hours and keep
  the old key until the self-check succeeds.
- **Suspected leak:** (1) delete the key at once, before investigating —
  access tokens already issued with it live at most an hour; (2) issue a new key,
  restart the installation and confirm the startup self-check in the log; (3) review Google Cloud audit logs for what the key was used
  for; (4) tell the school without delay — it is the data controller and decides
  on further notification; (5) record all of it in the operations journal.
- **Recreate the service account itself** only if the account, not just a key,
  is compromised: a new account has a new client ID, and the school must
  authorize delegation again. The Owner updates the client ID on the
  `Installation` (an audited Control Plane action, SC-11).
- **No copy of a key exists outside the secret store** — no downloaded files, no
  "just in case" copies.
- Keyless access (Workload Identity Federation) is Google's recommended path but
  needs an identity provider of the Owner's own outside Google Cloud. Not used in
  the first version; a possible later improvement.

## DC-6 Network

- The Control Plane ↔ installation service channel runs over a private network
  unreachable from the public internet. This replaces a per-installation token
  and holds **only** while the Owner hosts every installation (SC-9, §9). A
  school hosting its own copy would require re-engineering that channel.
- The school-facing UI is an ordinary public HTTPS application. Do not confuse
  the two: exposing the service endpoint publicly removes the only protection
  the channel has. Its public port redirects HTTP to HTTPS and sends HSTS
  (SC-2, `trebovaniya.md` §8, v61).
- **The installation's private endpoints listen on a separate port.** The
  status-change push receiver, liveness and readiness are served by
  `ClassroomAgent.Web` on a second Kestrel endpoint bound only to the private
  network interface. The public port, and any reverse proxy in front of it, does
  not serve those paths; a test asserts that on the public port they answer
  `404`. Otherwise anyone on the internet could post a status to the push
  receiver — lifting a suspension or forcing read-only mode. The private routes
  are bound to that port by host matching (`RequireHost("*:<private port>")`), so
  the split is part of routing and the test can prove it through the `Host`
  header without real ports (TC-5, v65).
- **The private port speaks plain HTTP** (SC-2, `trebovaniya.md` §8, v64). It
  carries status only, and HTTPS without client authentication would not stop a
  forged push — network isolation does. HTTPS redirection and HSTS apply to the
  public port only: on the private port they would break the push.
- **The whole Control Plane is private**, the Owner UI included: the Owner
  reaches it through a VPN or tunnel, never from the public internet
  (`trebovaniya.md` §9, v35).
- **The Control Plane is served over HTTPS as well**, inside the private network,
  with a certificate from the Owner's internal certificate authority or from
  Let's Encrypt via a DNS challenge; installations trust that certificate. A VPN
  encrypts traffic only up to its gateway — beyond it the Owner's password and
  the session cookie of the account that governs every school would travel in
  plain text. Its cookies are `Secure` (SC-2, `trebovaniya.md` §9, v61). It has
  no HTTP port, so there is nothing to redirect and no HSTS (v64).
- The channel is bidirectional by design: the installation calls the Control
  Plane every 6 hours for the legitimacy check, and the Control Plane pushes
  status changes to the installation's endpoint (HTTP POST, 3 retries with
  exponential backoff) (BR-024, NFR-014). On every Admin login the installation
  also asks the Control Plane whether the email is in `AllowedAdmin`; while the
  channel is down, Admin logins are refused (BR-012). Both calls to the Control
  Plane are POST, and the email travels in the body (SC-4, v64).

## DC-7 Suspending and resuming a school

- Suspension is performed by the Owner in the Control Plane, **without logging
  into the school's server** (§9). The push makes it effective immediately; the
  6-hourly check is the fallback if the push does not arrive.
- An installation enters read-only mode when its `Installation` is suspended, or
  when more than 7 days have passed since the last successful check (BR-025,
  NFR-013). Viewing and export keep working, and so does the closed list of
  service writes in BR-026; everything else stops.
- Read-only mode is a normal operating state, not an outage: it needs no
  deployment action, and it ends by itself once checks succeed again.
- Revoking an `AllowedAdmin` entry is a *different* lever: it removes a person's
  right to configure, but the school keeps working (BR-022). Never conflate the
  two during an operational change.

## DC-8 Moving or retiring a school

- Moving a school to another Workspace domain means creating a **new**
  `Installation` with a new database, not editing the domain of an existing one
  (BR-021, NFR-051). The previous installation's data stays in its own database.
- **Decommissioning an installation:** the school itself takes the full export
  of its journals — a Dean or Admin through the ordinary export, which works in
  read-only mode too; the Owner does not open the data for it. Only after the
  school confirms it has the export is the installation database deleted,
  together with its backups, immediately (DC-13; `trebovaniya.md` §5, v53), and
  with it the installation's Data Protection key directory (SC-7, v65).
- **When a person leaves a school**, the Owner revokes their `AllowedAdmin`
  entry and the school disables their Google account. Nothing else is needed:
  data is read by the technical account (BR-015), and every school has at least
  two Admins (BR-013). Their `AppUser` is purged later under PC-11.
- **Erasing one person's data on the school's written request** is, in the first
  version, an operational procedure performed by the Owner — there is no function
  for it yet (EPIC-10). It is executed **only after the school has removed that
  person from Google Workspace**: synchronization would otherwise re-import them
  on its next run. Like every operational access to a school database, it is
  recorded in the Owner's operations journal (SC-12).

- **Retiring the Python prototype.** It may be retired once Epics 3 and 4 are
  delivered and the Open Decisions it answers are resolved; until then it is the
  reference for empirical Google API questions. `dac-classroom-agent-*.json` in the
  repository root is a *live* service-account key; a copy outside the secret
  store is forbidden (DC-5). The prototype's Cloud project `dac-classroom-agent`
  sits **inside the dac.ukr.education organisation** (owner
  `admin@dac.ukr.education`; verified 2026-09-13) — it belongs to the school and
  is not the Owner's project of the .NET system. The order, performed by a human:
  1. delete the key of `classroom-agent@dac-classroom-agent.iam.gserviceaccount.com`
     in Google Cloud Console, then the local key file;
  2. DAC's super-admin removes the domain-wide delegation for client ID
     `110112929094683821680` in Google Admin console (Security → API controls →
     Domain-wide delegation) — this also drops `drive.file` and
     `classroom.profile.photos`, which only the prototype requested
     (`trebovaniya.md` section 6, v25);
  3. decide whether the project `dac-classroom-agent` is deleted or left to the
     school.

## DC-9 What is not decided yet

Every policy and operations question from `trebovaniya.md` section 7 is decided
(the last two, key checks after rotation and where backups live, in v54). What
remains is **verification at onboarding**, not a decision:

| To verify | Where it is tracked |
|---|---|
| Minimum Google Workspace roles the impersonation user needs | §7 item 10 |
| Whether Classroom still returns submissions of a student removed from a course | §7 item 14 |

A deployment or operations Story that finds a new gap raises an Open Decision
and stops; it does not improvise. Two things are deliberately deferred rather
than open: centralized log collection (DC-11) and keyless access to Google
without long-lived keys (DC-5).

## DC-10 Logging

- **Serilog**, structured, written to a rolling file on the installation: one
  JSON object per line, a new file per day, kept **30 days** with a size cap.
  The console sink is for local development only.
- Levels:
  - `Information` — application start and stop; synchronization start and finish
    with counters; legitimacy check result changes; a successful startup access
    self-check (DC-5).
  - `Warning` — retried transient Google failures (`429`, `5xx`), an
    installation entering read-only mode (with the reason).
  - `Error` — permission failures (`403 unauthorized_client`, `access_denied`,
    missing scope), unhandled exceptions, every unsuccessful legitimacy check, a
    failed startup access self-check.
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
  - **readiness** — three states (`trebovaniya.md` §8, v34):
    - `Unhealthy` — the database is unreachable or the synchronization
      background service is not running;
    - `Degraded` — the installation is in read-only mode for any reason, or the
      last legitimacy check failed while the grace period still runs (BR-025) —
      an early warning to the Owner;
    - `Healthy` — otherwise.
- Both are reachable **only from the Owner's private network**, like the service
  channel (DC-6, SC-9). A publicly reachable endpoint that reports internal
  state would contradict SC-6.
- Readiness reports `Unhealthy` only when the installation cannot serve users.
  Read-only mode is **not** unhealthy: it is a defined operating state (DC-7), so
  it reports `Degraded`, which answers HTTP 200 and keeps users able to view and
  export. Reporting it as a failure would cut viewing off and page the Owner for
  a school that is working exactly as designed.
- Metrics and centralized log collection are **out of scope for the first
  version**: for ~10 installations, readiness plus `SyncState` answer "is this
  school working". Shipping logs off a school's server is a privacy decision
  (`trebovaniya.md` section 5) that has not been taken — deliberately deferred,
  not forgotten. Until it is, such shipping is an outbound data flow forbidden by
  SC-13.

## DC-12 Version compatibility between the two planes

Ten schools are never upgraded in the same minute, so a version gap is a normal
operating condition, not an incident (`trebovaniya.md` §8, decided in v16).

- **The installation reports its version on the legitimacy check.** Every
  6-hourly check carries the installation's application version (semantic
  versioning, NFR-061) and its contract version. There is no separate
  version-polling mechanism.
- **The Control Plane answers with a compatibility state**: `supported`,
  `upgrade_recommended` or `upgrade_required`, alongside the legitimacy verdict
  and the `Installation`'s domain and client ID, which the installation keeps in
  `LegitimacyState` (SC-12, v54).
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
  endpoint version and a window during which it serves both. `API-1` governs the
  public REST API, not this channel (API-7) — this rule is the channel's own.
- **An unparseable push breaks nothing**: the installation logs at `Error` and
  waits for the periodic check, which is the designed guarantee; the push is the
  optimization (NFR-014).

## DC-13 Backup and restore

Decided in `trebovaniya.md` section 9 (v21). Most teaching data can be rebuilt
by re-synchronizing from Google; Dean accounts, report templates, connection
settings, the audit trail, history of courses already deleted in Google, and
Meet data older than Google's 180 days cannot. The Control Plane database is the
most critical one: losing it sends every school to read-only after 7 days.

- **Every database is backed up** — each installation and the Control Plane.
  Data Protection key directories are not: a backup together with the keys would
  let anyone forge a session, and losing them only signs users out (SC-7, v64).
- **A nightly logical dump** (`pg_dump`). Worst-case loss is one day of local
  changes; synchronization recovers teaching data.
- **Backups are kept 30 days**, rolling. Data purged by retention (PC-11) or
  erased on request (BR-076) therefore leaves the backups within 30 days.
- **Backups live off the database server and are encrypted.** The encryption
  key is held in the Owner's secret store, never next to the backups. They live
  only on the Owner's own infrastructure — never with an external storage
  provider (`trebovaniya.md` §9, v54, SC-13).
- **A restore test every quarter**: one database, rotating, is restored into an
  isolated environment and the application is started against it. An untested
  backup is not counted as a backup.
- **Restoring is operational access** and goes into the operations journal
  (SC-12). After a restore: apply pending migrations (DC-4), let synchronization
  catch up with Google, let the purge remove what has expired.
- **Erasures must not come back.** After any restore, every erasure of a
  person's data recorded in the operations journal after the backup's date is
  re-applied before the installation is returned to the school.
- **Decommissioning deletes that school's backups immediately**, without waiting
  for the 30-day window to roll over, and its Data Protection key directory with
  the database (DC-8, v65).
