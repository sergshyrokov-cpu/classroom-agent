---
artifact_type: open_decisions
story: US-008
version: 2
status: DRAFT
created_at: 2026-09-19T17:13:12Z
updated_at: 2026-09-19T17:21:40Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-008-admin-google-sign-in.md
    version: null
  - path: trebovaniya.md
    version: 78
supersedes: null
---

# US-008 Open Decisions

Story-level Open Decisions for US-008 (Admin sign-in via Google OAuth with
`AllowedAdmin` verification). Every item is resolved only by a human, at
`HUMAN_SPEC_APPROVAL`; the resolution is written next to the item and nothing is
deleted.

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
