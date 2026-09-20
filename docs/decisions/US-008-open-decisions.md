---
artifact_type: open_decisions
story: US-008
version: 4
status: DRAFT
created_at: 2026-09-19T17:13:12Z
updated_at: 2026-09-20T07:40:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-008-admin-google-sign-in.md
    version: null
  - path: trebovaniya.md
    version: 78
  - path: docs/specifications/US-008-spec.md
    version: 2
  - path: docs/designs/api/US-008-api-design.md
    version: 1
  - path: docs/designs/database/US-008-db-design.md
    version: 1
supersedes: null
---

# US-008 Open Decisions

Story-level Open Decisions for US-008 (Admin sign-in via Google OAuth with
`AllowedAdmin` verification). Every item is resolved only by a human; the
resolution is written next to the item and nothing is deleted.

| Id | Raised by | Status |
|---|---|---|
| OD-001 The OAuth client and its secret | the Story | RESOLVED 2026-09-19 (option 2), in `trebovaniya.md` v78 |
| OD-002 The account-picker domain hint | the Story | RESOLVED 2026-09-19 (option 1), in `trebovaniya.md` v78 |
| OD-003 The Google authentication handler package | SPECIFICATION | RESOLVED 2026-09-19 (option 1) |
| OD-004 How the OAuth client secret reference is resolved | TEST_WRITING | RESOLVED 2026-09-20 (option 1) |

`trebovaniya.md` section 7 has no open item this Story depends on. Item 10 (the
minimum Google Workspace roles for the technical account) concerns
domain-wide delegation and the reading of teaching data — a different mechanism,
which this Story does not touch; item 14 (submissions of a removed student) is a
Classroom question; item 26 is documentation. The Admin identification rules
themselves are closed: §2 ("Модель авторизации"), §9 ("Идентификация Админа") and,
since v78, the OAuth client and the account-picker hint.

---

## OD-001 Where the installation's Google OAuth client and its secret come from

**Carried from the Story, keeping its id.**

`trebovaniya.md` fixed that the Admin signs in through Google OAuth but, up to
v77, never said where the OAuth **web client** of an installation came from. The
per-installation configuration list of DC-3 had no entry for it, and
`Installation.ClientId` in the Control Plane is the *service account's* numeric
client id for domain-wide delegation (BR-020, v43) — a different thing, not
reusable here.

Undecided at the time: one client for all installations or one per school; where
the client id and secret live; how the redirect URI is set per school; who
creates it.

Options as recorded in the Story:

1. One OAuth client in the Owner's Cloud project for all installations, with
   every school's redirect URI registered on it. Fewest moving parts, but one
   leaked secret touches every school.
2. One OAuth client per school in the Owner's Cloud project, mirroring the
   per-school service account of SC-7; one more step at deployment (DC-2), and a
   leak compromises one school.
3. The school's own Cloud project creates it. Rejected on sight: it hands a
   school control over its own Admins' sign-in and contradicts BR-006.

**Resolution:** option 2, decided by the Owner on 2026-09-19. Each school gets
its own OAuth web client in the Owner's Cloud project. The client is of Google's
"External" user type — an "Internal" client would admit only the Owner's own
Workspace domain, never a school's Admin — and, since the sign-in requests
identity scopes only (`openid`, `email`, `profile`) and no data scope, it
publishes without Google's app verification.

Settled with it: the **client secret** lives in the configured secret store with
only the *reference* in the installation's configuration, exactly like the
service-account key (SC-7); the **client id** is not a secret and may sit in
configuration; the **redirect URI** is built from a configured public base
address rather than inferred from a request behind a reverse proxy, which made
that address a required setting too.

**Carried into the requirements before this Specification was written:**
`trebovaniya.md` v78 (§5 two new required settings, §6 the per-school client and
the identity scopes, §9 the sign-in), with `deployment-conventions.md` DC-3 and
DC-2 and `security-conventions.md` SC-7 following — commit `0e31e50`. The
Specification is therefore grounded in an approved requirement, not in this
resolution alone.

**Impact:** none outstanding. Specification FR-001, VR-001, VR-002, VR-003 and
S-08 are written against it.

---

## OD-002 Whether the sign-in restricts the account picker to the school's domain

**Carried from the Story, keeping its id.**

Google's authorization request can carry the school's domain so the picker offers
only accounts in it. The installation knows the domain — it is in
`LegitimacyState` from the legitimacy check (US-005, v54).

`trebovaniya.md` did not mention it up to v77. The `AllowedAdmin` check is the
real gate either way: an account outside the list is refused with an audit row.
So the question was about clarity for the Admin and noise in the audit, not about
security.

Options as recorded in the Story:

1. Restrict the picker to the installation's domain when it is known, and do not
   restrict while no legitimacy check has ever succeeded and the domain is
   therefore unknown.
2. Never restrict.

**Resolution:** option 1, decided by the Owner on 2026-09-19. School staff
routinely keep a personal and a work Google account in the same browser; without
the hint they pick the wrong one, receive a refusal whose reason is opaque to
them, and leave behind an audit row that reads like an outsider's attempt. The
hint is omitted while no legitimacy check has ever succeeded, so a freshly
deployed school can still admit its first Admin.

The domain is never the access decision in either branch. `trebovaniya.md` v78 §9
states that explicitly, and `security-conventions.md` SC-7 makes treating the
hint as a control a finding.

**Impact:** none outstanding. Specification FR-006 and S-07 are written against
it, and a test asserts that an account outside the school's domain is refused by
the `AllowedAdmin` check with its own audit category, not by the hint.

---

## OD-003 The Google authentication handler is a NuGet package no project references

**Raised by the Specification. RESOLVED by the Owner on 2026-09-19 (option 1).**

`AGENTS.md` (Technology Stack): *"Use only packages already referenced in the
target `.csproj`. Adding a NuGet package requires an approved Open Decision."*

No project in the solution references a Google or OpenID Connect authentication
handler. The seven `.csproj` files carry only EF Core, Npgsql,
`EFCore.NamingConventions`, `Microsoft.EntityFrameworkCore.Design` and Serilog.
The Identity **components** the Control Plane uses (`IPasswordHasher<T>`,
`ILookupNormalizer`) and cookie authentication come from the ASP.NET Core shared
framework and needed no package — which is why US-001 added none. A Google OAuth
handler is different: `Microsoft.AspNetCore.Authentication.Google`,
`.OpenIdConnect` and `.OAuth` are all standalone NuGet packages, outside the
shared framework.

So FR-006 and FR-007 cannot be implemented without either a package or a
hand-written authorization-code flow. This is a decision for the Owner, not for
an agent.

Options:

1. **Add `Microsoft.AspNetCore.Authentication.Google` to `ClassroomAgent.Web`.**
   A first-party Microsoft package, versioned and serviced with the framework,
   which is exactly what `trebovaniya.md` §2 means by "external login в терминах
   ASP.NET Core Identity". It brings `state`, the correlation cookie, the token
   exchange and ID-token handling — the parts of FR-007 that are security code.
   One package, one provider.
2. **Add `Microsoft.AspNetCore.Authentication.OpenIdConnect`** and configure
   Google as a generic OIDC provider. Also first-party, also one package, and
   provider-neutral if a school ever needed a different identity provider — which
   no requirement asks for. More configuration, and the account-picker hint of
   OD-002 has to be passed as a raw protocol parameter.
3. **Write the authorization-code flow by hand** over `HttpClient`, adding no
   package. It avoids the decision but means writing `state` handling, the
   correlation cookie, the token exchange and ID-token signature and claim
   validation ourselves — security code with a long history of subtle mistakes,
   for no benefit this project needs.

**Recommendation:** option 1. It matches what `trebovaniya.md` §2 already
describes, keeps the security-critical parts of the flow in first-party code, and
adds one package from the same vendor and release train as the framework. Option
3 is not recommended at any effort level: hand-rolled OAuth is the kind of code
`security-conventions.md` exists to keep out of this system.

**Resolution:** option 1, decided by the Owner on 2026-09-19.
`Microsoft.AspNetCore.Authentication.Google` is added to
`ClassroomAgent.Web` — and to no other project, since only the host authenticates
anyone. This is the approved Open Decision that `AGENTS.md` requires for a NuGet
package; no other package is added by this Story.

Conditions carried with the resolution:

- **One package, one project.** The package appears in
  `src/ClassroomAgent.Web/ClassroomAgent.Web.csproj` only. It must not reach
  `Application` or `Domain`, which would break AD-3 and AD-4: the sign-in
  decision (FR-010) stays free of any authentication type.
- **Version discipline.** It is pinned to the same major version as the framework
  and the other first-party packages already referenced, as `EFCore` and Serilog
  are today. A version that drifts from the framework is a defect, not a
  preference.
- **Scopes stay identity-only.** The handler's default scope set is stated
  explicitly rather than inherited, so a future default cannot silently widen
  what the school's Admin is asked to consent to (FR-006, S-06).
- The `AppUser` side is unaffected: I-2 stands, no Identity NuGet package is
  added, and `Domain` keeps its zero package dependencies.

**Impact:** none outstanding. FR-006 and FR-007 are implementable; FR-021 records
the package reference as the Story's one supporting dependency change. API_DESIGN
and DB_DESIGN were unaffected either way.

**Consequence for `TEST_WRITING`:** TC-4 forbids any test calling a live Google
endpoint. The tests replace the Google handler with a synthetic authentication
scheme registered in the test host, so no test performs a real authorization-code
exchange. The substitution point is now fixed by this resolution.

---

## OD-004 How the OAuth client secret reference is resolved into the secret

**Raised by `TEST_WRITING` on 2026-09-20. RESOLVED by the Owner on 2026-09-20 (option 1).**

**Gap.** FR-001 says the OAuth client secret "is read from the secret store
through the reference, by the same `Infrastructure/Secrets` mechanism the
service-account key uses (SC-7)". **That mechanism does not exist.** There is no
`Infrastructure/Secrets` namespace in the solution: the service-account key
arrives with `WorkspaceConnection` in US-009 and later, and nothing has yet had to
read a secret. So this Story is the first to need one, and no artifact says what a
reference looks like or where it points.

What the authorities do fix:

- `trebovaniya.md` §5 v78 — the client secret lives in the secret store, only the
  *reference* is in configuration, the database holds neither, and **"без
  идентификатора или без ссылки на секрет инсталляция не запускается"**;
- §5 (non-functional requirements) — the service-account key comes "через
  secrets/Key Vault/переменные окружения", placed by the Owner at deployment;
- §3 v33 — the reference is "имя секрета или путь";
- SC-7 — "the configured secret store (Key Vault, environment variable, mounted
  secret file)";
- DC-3 — the reference is required configuration; VR-002 — it must be non-empty
  after trimming and is never validated against Google at start-up (spec I-5).

What nobody has decided: **which store an installation actually uses, and how one
configuration string names a secret in it.** A single
`GoogleOAuth:ClientSecretReference` value has to be interpretable, and the three
candidate stores need three different interpretations — an environment variable
name, a file path, or a vault secret name. Choosing one, or inventing a scheme
prefix, is a design decision no artifact supports, and `AGENTS.md` forbids
inventing a rule no artifact defines.

**Impact.**

- **`TEST_WRITING`: not blocked.** Everything the requirements fix about the
  reference is tested — the setting is required, a blank value stops the start, the
  start-up refusal names the key and not the value, neither the client id nor the
  reference is ever logged, no configuration key holds the secret itself, and no
  database column holds one. No test drives the resolution, because there is
  nothing to drive it against, and no test needs to: TC-4 forbids a real token
  exchange, so no test uses the secret's value.
- **`IMPLEMENTATION`: blocked.** The host has to hand the Google handler a real
  `ClientSecret`, and it cannot obtain one without this decision.

**Options.**

1. *(Recommended)* **Environment variable only, in the first version.** The
   reference is the name of an environment variable the Owner sets on the server
   at deployment (`DC-2`); the installation reads it once at start-up and fails to
   start when it is absent. Simplest thing that satisfies SC-7 — the secret is
   outside the repository, outside the database and outside configuration files —
   needs no new package, and matches how the Owner already hosts the installations
   himself (BR-006). Cost: moving to a managed vault later is a change of
   mechanism, though not of the configuration shape.
2. **A scheme-prefixed reference** — `env:NAME`, `file:/run/secrets/NAME`,
   `vault:NAME` — resolved by a small `Infrastructure/Secrets` abstraction with one
   provider per scheme. Most flexible and the shape the service-account key will
   want in US-009 anyway. Cost: it is a subsystem this Story does not need, and
   `trebovaniya.md` §5 explicitly says the first version leaves out "всю
   подсистему управления ключами".
3. **A mounted secret file, path in the reference.** Fits containers well. Cost:
   the Owner's current deployment is not described as containerised in DC-2, so
   this presumes a change there.

Whatever is chosen also settles two smaller questions, and the Specification
should record the answers next to FR-001:

- **When it is read.** FR-001 permits reading at start-up to fail fast, and I-5
  says a wrong *value* must not stop the host. Those are compatible only if a
  *missing* secret stops the start while a *wrong* one does not — which is worth
  stating explicitly rather than leaving to the reader.
- **Whether the same mechanism is US-009's.** If it is, the resolution belongs in
  `Infrastructure/Secrets` from the start; if not, US-009 will raise this question
  again for the service-account key.

**Resolution:** *Resolved 2026-09-20 by the human (the Owner): **option 1 —
environment variable only, in the first version.***

`GoogleOAuth:ClientSecretReference` holds the **name of an environment variable**.
The Owner sets that variable on the installation's server at deployment (DC-2), and
the installation reads it through a resolver in `Infrastructure/Secrets` — the
namespace `package-map.md` already designates for "reads … from the configured
secret store, never from the database". The secret itself is never in
configuration, never in the database, never in a DTO, a view or a log (SC-7, S-08).

Two details settled with it, because option 1 states them:

- **When it is read, and what a failure means.** The reference is resolved **once
  at start-up**. A reference naming a variable that is **absent or empty stops the
  start**, naming the configuration key and never its value — this is the "fail
  fast on a missing secret" FR-001 permits. A reference naming a variable that
  *exists* but holds a **wrong** secret still starts the host: that is I-5, and it
  matters, because a mistyped secret must not take the school's read-only views
  down with it. It surfaces later as a failed sign-in.
- **Scope.** This resolution covers **this Story's OAuth client secret only**.
  US-009 brings the service-account key and will decide for itself whether to reuse
  the same environment-variable resolver or move both to a managed store; the
  reference *shape* — one configuration string naming a secret — is unchanged
  either way, so that later decision is a change of mechanism, not of configuration.

**What this does not change, deliberately:**

- **`trebovaniya.md` needs no new version.** §5 already lists "переменные
  окружения" among the stores, §3 v33 already calls the reference "имя секрета или
  путь", and §5 v78 already requires the reference and refuses to start without it.
  Option 1 selects from what the requirements permit; it adds nothing to them.
- **The Specification needs no amendment.** FR-001 says the secret is read "by the
  same `Infrastructure/Secrets` mechanism the service-account key uses". That
  mechanism did not exist, which is why this item was raised — but the wording is
  about **placement**, and option 1 satisfies it: the resolver lives in
  `Infrastructure/Secrets`, and US-009's service-account key reader joins it there.
  FR-001, VR-002 and S-08 are implementable as written.

**Impact:** none outstanding. `IMPLEMENTATION` is unblocked. `TEST_WRITING` gains
three cases it had deliberately left out — the resolved secret reaching the
handler, an absent variable stopping the start, and the secret's value never
reaching a log — which are now in
`tests/ClassroomAgent.Tests/Web/Configuration/InstallationOAuthSettingsTests.cs`.
