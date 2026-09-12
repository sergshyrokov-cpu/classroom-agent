# Security Conventions

The security policy for classroom-agent. `security-reviewer` enforces this file;
`dotnet-implementor` implements to it; `openapi-designer` and `db-designer`
design within it.

Derived from `trebovaniya.md` sections 1, 2, 5, 6 and 9. Where this file is
silent, `trebovaniya.md` governs. Nothing here may be weakened without a
human-approved Open Decision.

## Why this is not a training-project policy

This system stores **personal data of school students, potentially minors** —
grades, attendance, group membership, email addresses. It authenticates against
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
- The Owner has no access to a school's teaching data. Whether the Control Plane
  may read Data Plane data at all is still an open question
  (`trebovaniya.md` section 7, item 6) — the assumed answer is no. Do not build
  a path that would allow it.

## SC-2 Authentication

- **Dean** — local login and password via ASP.NET Core Identity. The account is
  created manually by an Admin.
- **Admin** — Google OAuth external login. **There is no local password for an
  Admin**, no password column, no password reset flow. A migration or entity
  adding one is a Critical finding.
- **Owner** — login and password via ASP.NET Core Identity in the Control Plane,
  set once at first-run setup. Stored in the Control Plane database only, never
  in an installation's `AppUser` table.
- Session state lives in an `httpOnly` cookie. No password and no Google
  credential is ever stored client-side (`trebovaniya.md` section 8).

## SC-3 Admin identity and the AllowedAdmin check

One email is simultaneously the Admin's OAuth login, the `AllowedAdmin` entry in
the Control Plane, the `AppUser` with role Admin, and the impersonation user in
`WorkspaceConnection` (`trebovaniya.md` section 9).

- The `AppUser` with role Admin is created **just-in-time** on the first
  successful OAuth login with an email present in `AllowedAdmin` for this
  `Installation`.
- **`AllowedAdmin` is checked on every login, not only the first.** Checking it
  once would make the Owner's revocation meaningless — the `AppUser` row already
  exists. On revocation the row is kept (history and audit) but login is
  refused. Implementing this as a first-login-only check is a Critical finding.
- Several Admins per installation are permitted and expected; one Admin must not
  be a single point of failure.

## SC-4 Authorization

- Every API endpoint and every Razor page declares its required role as an
  authorization policy. There is no default-allow: an endpoint reachable without
  a declared policy is a Critical finding (`api-conventions.md` AC-9).
- Policies are defined in `Application/Authorization` and registered in the
  host's `Security` namespace, so the matrix lives in one place and can be
  compared against `trebovaniya.md` section 2.
- Authorization is enforced server-side in the Application layer. Hiding a
  control in Razor is not enforcement.

## SC-5 Read-only mode

When the grace period has expired or the Owner has suspended the `Installation`,
every write use case refuses with `409` (`architecture.md` AD-6,
`api-conventions.md` AC-5). Viewing and exporting already-synced data continue
to work. A write path that bypasses the check is a Critical finding.

## SC-6 No database admin or diagnostic UI

No database browser, SQL console, entity explorer, or diagnostic endpoint that
exposes schema or row data is shipped in any environment. Developer exception
pages are disabled outside local development.

## SC-7 Secrets and the service-account key

- **The service-account key never reaches a school and never enters the
  database.** The Owner places it at deployment time in the configured secret
  store (Key Vault, environment variable, mounted secret file).
  `WorkspaceConnection` stores a *reference* to it, never the material
  (`persistence-conventions.md` PC-9).
- There is **no UI to upload a key**. An Admin configures the domain and the
  impersonation user only. Adding an upload form is a Critical finding.
- One Cloud project owned by the Owner, **a separate service account per
  school** (`trebovaniya.md` section 6). A key leak must compromise one school,
  not all of them.
- Never commit a key, a client secret, a connection string with a password, or a
  token. The prototype's committed-looking artifacts
  (`dac-classroom-agent-*.json`, `google_credentials.json`) are git-ignored and
  must stay so.
- Key rotation is still an open question (`trebovaniya.md` section 7, item 9) —
  do not invent a rotation mechanism; record an Open Decision.

## SC-8 Google API access is read-only

- Every requested OAuth scope is a `readonly` scope. The program never writes to
  Google Workspace (`trebovaniya.md` section 1). A design requesting a write
  scope is a Critical finding.
- The scope list is fixed in `trebovaniya.md` section 6 and is what a school's
  super-admin authorizes. Adding a scope is a requirements change, not an
  implementation detail: it forces every school to re-authorize.
- Data is read through impersonation of the school's domain administrator.
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
  isolation applies to the service channel only. Do not confuse the two.
- The domain an installation may work with comes from the Control Plane. Saving
  a `WorkspaceConnection` whose domain differs from the `Installation` domain
  must be refused — this is the control that stops the program from being
  pointed at a domain the Owner never approved.

## SC-10 Error and log hygiene

- No stack trace, SQL, entity or namespace name, file path, connection string,
  service-account identifier, or raw Google API error reaches an HTTP response
  (`api-conventions.md` AC-6).
- Personal data of students is never written to application logs. A log line may
  reference a course or a participant by internal id, never by name, email or
  grade.
- Telemetry (`docs/hooks/tool-usage.jsonl`) records metadata only and is
  git-ignored.

## SC-11 Audit

Audit requirements are still open (`trebovaniya.md` section 7, item 7) — who
started a synchronization, who exported a journal containing personal data, who
created or removed a Dean account. Do not invent an audit scheme; if a Story
needs one, record an Open Decision. Do not remove the question by ignoring it
either: exporting personal data without a trace is the gap this item names.
