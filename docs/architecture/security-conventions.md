# Security Conventions

The security policy for classroom-agent. `security-reviewer` enforces this file;
`dotnet-implementor` implements to it; `openapi-designer` and `db-designer`
design within it.

Derived from `trebovaniya.md` sections 1, 2, 5, 6 and 9. Where this file is
silent, `trebovaniya.md` governs. Nothing here may be weakened without a
human-approved Open Decision.

## Why this is not a training-project policy

This system stores **personal data of school students, potentially minors** —
grades, participation in Meet meetings, email addresses. It authenticates against
a school's Google Workspace using a service account with domain-wide delegation,
and it is deployed across ~10 schools that the Owner hosts. A leaked key or a
missing authorization check has a real-world victim. Treat every finding in this
area as production severity.

## SC-1 Roles

- The first version has exactly three roles: **Owner** (Control Plane),
  **Admin** and **Dean** (Data Plane). `AppRole` has two members: `Admin`,
  `Dean`.
- **Teacher and Student are Epic 7.** They are not accounts, cannot log in, and
  must not appear in `AppRole`, in an authorization policy, or in a seed. They
  exist only as synced data (`ClassroomParticipant`).
- The permission matrix in `trebovaniya.md` section 2 is authoritative. Do not
  invent a cell. Admin and Dean see the same data; they differ in the right to
  configure and to manage accounts.
- The Owner has no access to a school's teaching data **through the
  application**, and only operational access on the servers (SC-12).

## SC-2 Authentication

- **Dean** — local login and password via ASP.NET Core Identity. The account is
  created manually by an Admin, who may also disable, re-enable and reset its
  password; the account is never deleted (BR-014). After an Admin reset the Dean
  must change the password at the next login, so only the Dean knows it. A reset
  flow that lets the Admin keep a working password is a finding.
- **Admin** — Google OAuth external login. **There is no local password for an
  Admin**, no password column, no password reset flow. A migration or entity
  adding one is a Critical finding.
- **Owner** — login and password via ASP.NET Core Identity in the Control Plane,
  set once at first-run setup. Stored in the Control Plane database only, never
  in an installation's `AppUser` table.
- **First-run setup requires a one-time setup code.** While no Owner account
  exists, the Control Plane generates a random code at startup and prints it to
  the server console only — never to the log file (SC-10). Setup succeeds only
  with that code; the code is void once the account exists, and a restart
  without an Owner generates a new one. A setup path that works without the code
  is a Critical finding (`trebovaniya.md` §9, v35).
- Session state lives in an `httpOnly` cookie. No password and no Google
  credential is ever stored client-side (`trebovaniya.md` section 8).

## SC-3 Admin identity and the AllowedAdmin check

One email is simultaneously the Admin's OAuth login, the `AllowedAdmin` entry in
the Control Plane, and the `AppUser` with role Admin (`trebovaniya.md` section 9).
It is a domain administrator account, never a super-admin. The impersonation user
in `WorkspaceConnection` is a separate technical account of the school, not any
Admin's account (BR-015, SC-8).

- The `AppUser` with role Admin is created **just-in-time** on the first
  successful OAuth login with an email present in `AllowedAdmin` for this
  `Installation`.
- **`AllowedAdmin` is checked on every login, not only the first.** Checking it
  once would make the Owner's revocation meaningless — the `AppUser` row already
  exists. On revocation the row is kept (history and audit; purged
  later under PC-11) but login is refused. Implementing this as a first-login-only check is a Critical finding.
- **The check is a call to the Control Plane on every Admin login.** The
  installation stores no copy of `AllowedAdmin` and caches no answer. If the
  Control Plane does not answer, the Admin login is refused with a plain message;
  Dean logins and synchronization are unaffected (`trebovaniya.md` §2, v26).
  Falling back to a cached or earlier answer is a Critical finding.
- Several Admins per installation are permitted and expected; one Admin must not
  be a single point of failure.

## SC-4 Authorization

- Every API endpoint and every Razor page declares its required role as an
  authorization policy. There is no default-allow: an endpoint reachable without
  a declared policy is a Critical finding (`api-conventions.md` AC-9).
- **Deny by default.** Both hosts set a fallback policy requiring an
  authenticated user, so a forgotten attribute closes an endpoint rather than
  opening it.
- **Anonymous access is a closed list.** Only these endpoints may allow
  anonymous access, each with the protection that replaces a role:

  | Endpoint | Host | Protected by |
  |---|---|---|
  | Dean sign-in page | installation | Identity lockout after failed attempts |
  | Google OAuth start and callback | installation | Google OAuth, then the `AllowedAdmin` check (SC-3) |
  | Status-change push receiver | installation | private network (SC-9) |
  | Liveness and readiness | installation | private network (DC-11) |
  | Owner sign-in | Control Plane | private network, Identity lockout |
  | First-run setup | Control Plane | private network and the one-time setup code (SC-2) |
  | Legitimacy check and Admin login check | Control Plane | private network (SC-9) |

  An anonymous endpoint not on this list is a Critical finding; adding one
  requires extending the list.
- Policies are defined in `Application/Authorization` and registered in the
  host's `Security` namespace, so the matrix lives in one place and can be
  compared against `trebovaniya.md` section 2.
- Authorization is enforced server-side in the Application layer. Hiding a
  control in Razor is not enforcement.

## SC-5 Read-only mode

When the grace period has expired or the Owner has suspended the `Installation`,
every write use case refuses with `409` (`architecture.md` AD-6,
`api-conventions.md` AC-5). Viewing and exporting already-synced data continue
to work, and only the closed list of service writes in BR-026 still runs. A write
path that bypasses the check, or a service write not on that list, is a Critical
finding.

## SC-6 No database admin or diagnostic UI

No database browser, SQL console, entity explorer, or diagnostic endpoint that
exposes schema or row data is shipped in any environment. Developer exception
pages are disabled outside local development.

## SC-7 Secrets and the service-account key

- **The service-account key never reaches a school and never enters the
  database.** The Owner places it at deployment time in the configured secret
  store (Key Vault, environment variable, mounted secret file).
  The *reference* to it (secret name or path) lives only in the installation's
  configuration, set by the Owner (DC-3); the database holds neither the key nor
  the reference (`persistence-conventions.md` PC-9, `trebovaniya.md` §3, v33).
- There is **no UI to upload a key**. An Admin configures the domain and the
  impersonation user only. Adding an upload form is a Critical finding.
- One Cloud project owned by the Owner, **a separate service account per
  school** (`trebovaniya.md` section 6). A key leak must compromise one school,
  not all of them.
- Never commit a key, a client secret, a connection string with a password, or a
  token. The prototype's committed-looking artifacts
  (`dac-classroom-agent-*.json`, `google_credentials.json`) are git-ignored and
  must stay so.
- **Keys are rotated every 90 days**, and a suspected leak is answered by deleting
  the key first and investigating second. Rotation needs no action from the
  school, because delegation is bound to the client ID, not the key. The full
  procedure is `deployment-conventions.md` DC-5.

## SC-8 Google API access is read-only

- Every requested OAuth scope is a `readonly` scope. The program never writes to
  Google Workspace (`trebovaniya.md` section 1). A design requesting a write
  scope is a Critical finding.
- The scope list is fixed in `trebovaniya.md` section 6 and is what a school's
  super-admin authorizes. Adding a scope is a requirements change, not an
  implementation detail: it forces every school to re-authorize.
- Data is read through impersonation of the school's technical account (BR-015):
  no person behind it, not a super-admin, read-only roles for Classroom and
  Admin Reports only. A design that impersonates an Admin's or a super-admin's
  account is a finding.
  The Admin's own OAuth session is **never** used to call a Google data API —
  it authenticates the human, nothing more.
- Permission failures (`403 unauthorized_client`, `access_denied`, missing
  scope) are never retried and never silently swallowed: they mean the school
  has not completed domain-wide delegation, and they surface to the Admin with a
  diagnosable message (`architecture.md` AD-5).

## SC-9 Control Plane channel

- The legitimacy-check and push channel between Control Plane and Data Plane is
  protected by **network isolation** — a private network unreachable from the
  public internet — rather than a per-installation token
  (`trebovaniya.md` section 9). This is a deliberate, recorded decision that
  holds only while the Owner hosts every installation.
- The school-facing web UI is an ordinary public HTTPS application; the
  isolation applies to the service channel and to the whole Control Plane,
  including the Owner UI, which is never reachable from the public internet
  (v35). Do not confuse the two.
- The domain an installation may work with comes from the Control Plane. Saving
  a `WorkspaceConnection` whose domain, or whose impersonation user's email
  domain, differs from the `Installation` domain must be refused (BR-020) — this is the control that stops the program from being
  pointed at a domain the Owner never approved.

## SC-10 Error and log hygiene

- No stack trace, SQL, entity or namespace name, file path, connection string,
  service-account identifier, or raw Google API error reaches an HTTP response
  (`api-conventions.md` AC-6).
- Personal data of students is never written to application logs. A log line may
  reference a course or a participant by internal id, never by name, email or
  grade.
- Application logging is defined in `deployment-conventions.md` DC-10; the
  constraints in this section bind every line it writes.
- Telemetry (`docs/hooks/tool-usage.jsonl`) is harness tooling, not application
  logging: it records metadata only and is git-ignored.

## SC-11 Audit

Decided in `trebovaniya.md` section 5 (v17). Audit answers "who did this, and
when" — above all, who took personal data out of the system.

- Audit lives in an **`AuditEvent` table**, in the installation database and,
  for Owner actions, in the Control Plane database. Not in log files: logs rotate
  after 30 days (DC-10) and are not queryable per school.
- **Audited in an installation:** sign-in and refused sign-in (Dean by password,
  Admin by OAuth, including a refusal because the email is not in
  `AllowedAdmin`); an Admin creating, disabling, re-enabling or resetting the
  password of a Dean account, and a Dean changing their own password; saving or
  changing `WorkspaceConnection`; running the "check access" diagnostic; starting
  a synchronization by hand; linking a Meet meeting code to a course or
  re-linking it; **exporting a journal or report**.
- **Audited in the Control Plane:** Owner sign-in, creating an `Installation`,
  changing its service-account client ID, suspending and resuming one, adding and revoking an `AllowedAdmin`.
- **A row carries:** UTC timestamp, actor (internal account id and role —
  `AppUser` in an installation, `Owner` in the Control Plane — or `system` for
  background work), action, target (entity type and internal id), outcome
  (succeeded / refused), and the request identifier that links it to the logs.
- **A refused sign-in** names the existing account's id as actor (wrong
  password, disabled account, email no longer in `AllowedAdmin`); with no
  account, the actor is "anonymous" with no identifier, and the login or email
  typed is never recorded. Either way the row carries the refusal category:
  unknown login, wrong password, account disabled, not in `AllowedAdmin`,
  Control Plane unavailable (`trebovaniya.md` §5, v45).
- **A row never carries personal data** — SC-10 binds it exactly as it binds
  logs: no names, no email addresses, no grades. An export row records course
  ids, the period, the template id and the row count, never the file's contents.
- **Never updated; deleted only by the retention purge.** An audit row is never
  changed. The only path that deletes one is the retention purge (PC-11); no
  user-facing use case, endpoint or screen can edit or delete a row. Any other
  update or delete path is a Critical finding (`trebovaniya.md` §5, v27).
- **There is no audit screen in the first version** — rows are written, not
  shown. Viewing is deferred to EPIC-9 (`trebovaniya.md` section 4,
  `docs/product/epic-map.md`), because it requires a new cell in the permission
  matrix (section 2), which is a decision nobody may invent (SC-1).
- **Retention**: audit rows are themselves personal data. They are purged after
  the installation's retention period N counted from each row's own timestamp —
  not together with the course they mention, so deleting a course never erases
  the trace of who exported its journal (PC-11). **Control Plane audit rows are
  kept indefinitely**: they record only the Owner's own actions and internal
  ids, with no third-party personal data (v45).
- The table and its writing path are created by the first Story that introduces
  an audited action; every later Story that introduces one writes its event and
  proves it with a test.

## SC-12 The Owner and teaching data

Decided in `trebovaniya.md` section 9 (v20). Two levels are kept apart: the
application, where the Owner has no access, and the servers, which the Owner
hosts and therefore can reach.

**Application — no path exists, and that is checkable:**

- `ClassroomAgent.Contracts` carries no teaching-data type. The legitimacy check
  and the status push carry the installation id, application and contract
  versions, status and compatibility state (DC-12); the Admin login check carries
  the installation id, the email being checked and a yes/no answer (SC-3) —
  nothing else.
- **No school statistics reach the Control Plane**, not even anonymous counts of
  courses or participants. Adding any is a separate decision, not an
  implementation detail.
- `ClassroomAgent.ControlPlane` never references `ClassroomAgent.Domain`
  (`package-map.md`). A Control Plane endpoint, query or contract field that
  exposes Data Plane content is a Critical finding.
- The Owner has no account in any installation (BR-005), and readiness reports
  state only (DC-11).

**Servers — operational access only:**

- The Owner touches a school's database only to apply migrations (DC-4),
  decommission the school (DC-8), erase one person's data on the school's
  written request (BR-076), and back it up and restore it (DC-13).
  Never to look at teaching data for the Owner's own purposes.
- The rules of that access are part of the written agreement with the school,
  the same one that fixes the retention period (PC-11).
- **Every operational access is recorded in the Owner's operations journal**:
  date, school, task, and a reference to the school's request where there is
  one. The journal is kept outside the application — an organizational control,
  not a feature — and is shown to the school on request.
