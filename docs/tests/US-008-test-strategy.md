---
artifact_type: test_strategy
story: US-008
version: 2
status: DRAFT
created_at: 2026-09-19T20:14:02Z
updated_at: 2026-09-20T07:55:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-008-admin-google-sign-in.md
    version: null
  - path: docs/specifications/US-008-spec.md
    version: 2
  - path: docs/decisions/US-008-open-decisions.md
    version: 4
  - path: docs/designs/api/US-008-api-design.md
    version: 1
  - path: docs/designs/api/US-008-openapi.yaml
    version: 1
  - path: docs/designs/database/US-008-db-design.md
    version: 1
  - path: docs/designs/database/US-008-entity-model.md
    version: 1
  - path: trebovaniya.md
    version: 78
supersedes: null
---

# US-008 Test Strategy — Admin sign-in via Google OAuth with AllowedAdmin verification

## 1. Scope

US-008 is the largest Story so far and the first with a **public-port surface**.
Its tests therefore split into four groups, and the split is what keeps them
readable:

| Group | What it proves | Where it runs |
|---|---|---|
| **Control Plane channel** | `POST /service/v1/admin-login-checks` answers, refuses and writes nothing (AC-005, AC-006) | Control Plane host + real PostgreSQL |
| **Installation host baseline** | deny by default, antiforgery, cookies, session, error page, localization, configuration (AC-001, AC-002, AC-013, AC-015, AC-016) | installation host + real PostgreSQL |
| **The sign-in itself** | OAuth start and callback, the `AllowedAdmin` question at every sign-in, JIT provisioning, refusals, audit (AC-003, AC-004, AC-007 … AC-011, AC-018) | installation host, real Google handler driven offline |
| **Schema and the carried findings** | `app_user` and `audit_event` constraints, the migration, the two US-007 findings (AC-007, AC-010, FR-021) | installation host + real PostgreSQL |

Out of scope for these tests, because it is out of scope for the Story: any Dean
account or password path (US-012), a personal UI-language choice (US-039),
`WorkspaceConnection` (US-009), any Google **data** call (EPIC-1), the audit
viewer (EPIC-9) and the retention purge (EPIC-10).

## 2. The two decisions that shape every test

### 2.1 No production source file is created by this stage

US-005 (OD-002) and US-007 (OD-003) both had to authorise a compile-only
skeleton in `src/`, because every scenario named a type that did not exist yet.
**US-008 does not need one**, and that is a deliberate design of this strategy
rather than luck: almost every Acceptance Criterion of this Story is observable
through a seam that already exists — an HTTP request on the public port, a row
read with raw SQL, a line in the Serilog file, a configuration key, a routed
endpoint in `EndpointDataSource`.

The two places where a new production type would otherwise have been referenced
are handled without one:

- **The Control Plane question** is not asked through a substituted
  `IControlPlaneClient` (which would require the new port method to exist at
  compile time). It is asked through the installation's **real** client, whose
  primary `HttpMessageHandler` the test scripts — see 2.2. This is stronger
  evidence, not weaker: the `ClientClassification` table of api-design 2.4 has
  to be proven anyway, and this proves it on the real code.
- **The translations** are resolved through `IStringLocalizerFactory.Create(baseName, location)`
  with the base name and location as **strings** (`Localization.SharedResource`,
  `ClassroomAgent.Application`), exactly as `package-map.md` places them. No
  marker type is referenced.

Consequence to state plainly: there is **no unit test that names the sign-in use
case type**. `CompleteGoogleSignInUseCase` is the entity model's name for it, and
naming it in a test would mean creating it here. Every branch of FR-010 is
instead covered at the integration level, deterministically, with the Control
Plane reply scripted and the clock manual. Section 7 records this as a known
limitation.

### 2.2 Google and the Control Plane are driven offline, through their real code

TC-4 forbids any test reaching a live Google API or a deployed Control Plane. It
does **not** require the framework's handler to be replaced by a stub — and
replacing it would destroy the evidence AC-003 asks for, because the OAuth
`state` parameter and the correlation cookie are the *handler's* behaviour. A
stub that validated `state` itself would only be testing the stub.

So:

- **Google.** The real `Microsoft.AspNetCore.Authentication.Google` handler runs.
  After the host starts, the test fixture `GoogleSignInStub` reaches the named
  options instance and points `AuthorizationEndpoint`, `TokenEndpoint`,
  `UserInformationEndpoint` at local URLs and `Backchannel` at a scripted
  `HttpMessageHandler` that returns a synthetic token response and a synthetic
  userinfo document. It does this **by reflection over the options type resolved
  by name**, so the test project needs no reference to the package — OD-003
  added it to `ClassroomAgent.Web` and to no other project, and this strategy
  keeps that true. The reflection is confined to the fixture; not one assertion
  uses it.
- **The Control Plane.** `InstallationTestHost.StartWithControlPlaneHttpAsync(...)`
  removes the `FakeControlPlaneClient` substitution, leaves the real
  `ControlPlaneClient` in place and replaces the primary handler of its typed
  `HttpClient` (logical name `IControlPlaneClient`) with a scripted handler. The
  test then controls status code, body, delay and connection failure, which is
  exactly the input the classification table is written against.
- A **second**, narrower mode also exists for one contract test: the
  installation's real client against the **in-process Control Plane**
  (`ControlPlaneTestHost.CreateServerHandler()`), as `LegitimacyChannelContractTests`
  already does for US-005. It proves both sides of the new wire contract agree.

Neither mode reaches a network. Every fixture value is synthetic (TC-4): the
domain is `school-one.example.test`, emails are in it, and no real school,
client id or key appears anywhere.

## 3. Required fixtures

New, all in `tests/ClassroomAgent.Tests/TestInfrastructure/`:

| Fixture | Purpose |
|---|---|
| `ScriptedHttpHandler` | records requests and answers from a script; shared by the Google backchannel and the Control Plane channel |
| `GoogleSignInStub` | the reflection seam over the Google handler's options; builds the token and userinfo answers; the verified-email flag togglable (VR-005, I-6) |
| `SignInTestData` | paths (`/sign-in`, `/sign-out`, `/signin-google`), cookie names, translation keys, synthetic emails |
| `AdminLoginCheckTestData` | the channel path and request/response JSON builders |
| `AdminLoginCheckHostExtensions` | `PostAdminLoginCheckAsync` over the Control Plane host |
| `AppUserRow`, `InstallationAuditRow` | the two new installation tables read with raw SQL |
| `InstallationSignInExtensions` | a whole sign-in: start, assert the 302, complete the callback, return the client |
| `ReadOnlyRefusalOverHttp` | puts a `ReadOnlyModeException` through the host's own registered `IExceptionHandler` and reports what a caller receives (see the note below) |
| `InstallationSchemaQueries` | the catalogue queries of the Control Plane's `SchemaQueries`, over the installation host |

Modified (nothing removed, nothing weakened):

| Fixture | Change |
|---|---|
| `InstallationConfigurationKeys` | the four new keys of FR-001 and their test values |
| `InstallationTestHost` | the new settings by default; `KeyDirectory`; a public-port `FormClient`; `SendPublicHttpAsync`; `AppUsersAsync`, `AuditRowsAsync`, `AuditRowsAsJsonAsync`, `InsertAppUserAsync`, `TableNamesAsync`; `Text` / `AllTexts`; `StartWithControlPlaneHttpAsync`; `UseGoogleStub` |
| `InstallationFactory` | honours the two new seams (real client with a scripted handler; extra startup filters) |
| `AnonymousEndpointTests.IsSc4Entry`, `AntiforgeryTests.IsSc4Exemption` (Control Plane) | the new endpoint is added to the SC-4 whitelists so the existing enumeration tests stay correct once it exists; both stay green today |
| `Web/Configuration/InstallationConfigurationTests.cs` (US-005) | the required-settings count 6 → 10, because FR-001 makes four more required; the language stays optional |

### 3.1 How the `409` mapping is proven

api-design 2.5 suggested a test-only probe endpoint under `/api/v1`. That is not
achievable from outside the host: FR-002 puts an anonymous catch-all last in the
endpoint pipeline, so it matches every unmatched path and a test middleware
appended after the application's pipeline never runs, while one inserted ahead of
it would sit outside the exception handler being tested. The tests therefore hand
the exception to the host's **registered `IExceptionHandler`** directly — the same
production code, nothing simulated — and assert the status, the content type, the
API-6 body shape and the translated message in both languages. The deviation is
recorded in the test-generation report, section 5.

## 4. Test levels

| Level | Used for |
|---|---|
| **Contract** | every operation of `US-008-openapi.yaml`: status codes, bodies, `ServiceOutcome`, the API-6 `ApiError` shape, the view-model fields rendered (TC-3) |
| **Integration** | the whole sign-in through the public port against real PostgreSQL, with the Google handler and the Control Plane channel scripted (TC-2) |
| **Security** | the SC-4 anonymous and antiforgery enumerations, role tests per endpoint, cookie attributes, session limits, read-only mode, "no personal datum" assertions (TC-5) |
| **Unit** | only where no host is needed: the classification table rows reached through the sign-in path, and the DI-shape assertions of FR-021 |
| **Schema** | `information_schema` / `pg_indexes` assertions of db-design 3, 4 and the migration (PC-2 … PC-9) |

## 5. Scenarios

### 5.1 Positive

- an approved Admin signs in: `AppUser` created with role `admin`, sign-in method
  `google`, `password_hash` NULL, `ui_language` = the school default,
  `is_disabled` false, `last_successful_sign_in_at` = the manual clock's now; one
  `audit_event` row `admin_sign_in` / `succeeded`; the session cookie issued; the
  landing page shows the stored email and the role (AC-003, AC-007, AC-017);
- the same Admin signs in again: the same row reused, only the stamp changes, and
  the Control Plane is asked **again** (AC-004);
- the authorization request carries `scope=openid email profile`, the
  `redirect_uri` built from `Installation:PublicBaseAddress`, a `state`, the
  correlation cookie, and `hd` = the installation's domain when `LegitimacyState`
  knows it (AC-003);
- the Control Plane answers a known installation with the single `allowed`
  property and nothing else; a suspended installation is answered identically
  (AC-005);
- sign-out clears the session and the replayed cookie no longer authenticates
  (AC-014);
- the landing page shows the legitimacy status with the reason and the time of
  the last successful check (AC-017);
- every page renders in Ukrainian and in English (AC-015).

### 5.2 Negative

- `state` missing / unknown / replayed, and the callback without the correlation
  cookie: refused, nothing created, no Control Plane call, audit
  `callback_failed` (AC-003, AC-010);
- Google reports the email unverified, or returns no email: the same refusal and
  the same category, **not** `not_in_allowed_admin` (VR-005, I-6);
- the email is not in `AllowedAdmin`: refused with a message that reveals nothing,
  the `AppUser` row kept and its stamp unchanged (AC-008);
- the Control Plane is unreachable / times out / answers `500` / answers
  unparseable JSON / answers `404` **without** the `unknown_installation` body:
  every one refuses, audit `control_plane_unavailable`, and the last case is
  asserted **not** to become "not allowed" (AC-009, api-design 2.4);
- the Control Plane answers `404` **with** that body: audit
  `unknown_installation` (AC-009);
- a `GET` to `/sign-in/google` and to `/sign-out`: `405`, nothing happens
  (AC-003, AC-014);
- a `POST` without the antiforgery token: `400` with the translated
  `Error.PageExpired`, nothing started and no session (AC-013, AC-016);
- an anonymous request to `/`: `302 /sign-in` with **no** return-URL parameter
  (AC-002);
- a signed-in user denied by a policy: `403` with the error page, not a redirect
  and not `404` (AC-002);
- the Control Plane channel with a malformed body, an unknown installation id, a
  wrong content type, another installation's `AllowedAdmin` email (AC-006);
- a missing or malformed required setting: the host does not start and the log
  names the key, never its value (AC-001).

### 5.3 Boundary

- session: a request at 59 minutes idle succeeds, at 60 minutes + 1 second it
  does not; activity every 20 minutes for 8 hours succeeds, past 8 hours it does
  not — all on the manual clock (AC-013);
- `Installation:PublicBaseAddress`: `https://host`, `https://host:9443`,
  `https://host/` accepted; `http://`, a path, a query, a fragment, user info
  rejected (VR-001);
- `Ui:DefaultLanguage`: absent gives Ukrainian; `uk`, `en`, `UK`, `EN` accepted;
  `ru`, `uk-UA`, empty rejected (VR-004);
- the login-check email: 254 characters accepted, 255 rejected, empty rejected,
  surrounding whitespace trimmed, upper case matched against the stored lower
  case (VR-006);
- a mixed-case email from Google is lower-cased before the call, the lookup and
  the insert (BR-079).

### 5.4 Validation

Every rule of Specification section 6 has its own case: VR-001 … VR-007. The
channel's rejected body is asserted **absent from the log** (SC-10), and a
rejected request is asserted to have written nothing.

### 5.5 Security

Driven by the twenty S-requirements of Specification section 7. The ones that get
a test of their own beyond the scenarios above:

- **S-01** an enumeration over `EndpointDataSource`: the fallback policy exists
  and the only anonymous endpoints are `/sign-in`, `/sign-in/google`,
  `/signin-google`, `/error/{statusCode}`, the catch-all and static files;
- **S-02** two sign-ins give two channel requests; a revocation after a success
  refuses the next sign-in; no table, column or in-memory copy of `AllowedAdmin`
  (asserted as: no installation table other than the three known ones, and the
  second sign-in still calls);
- **S-04** `app_user` rejects an Admin row with any `password_hash`, empty string
  included, at the **database** level;
- **S-06** the authorization request carries no `classroom` and no `admin.reports`
  scope — asserted by substring over the whole 302 `Location`;
- **S-07** an account outside the school's domain is refused by the
  `AllowedAdmin` check with `not_in_allowed_admin`, proving the domain hint is not
  the decision;
- **S-09** no column anywhere in the installation database holds a Google token,
  refresh token or subject id, and no response body carries one;
- **S-11** cookie attributes per host: the session cookie `Secure`, `HttpOnly`,
  `SameSite=Lax`, no `Expires` and no `Max-Age`; the antiforgery cookie `Secure`,
  `HttpOnly`, `SameSite=Strict`; every other cookie `Secure`;
- **S-13** every stored audit row as JSON contains no email, no name and no
  Google subject id; an update through EF Core fails;
- **S-14** the refusal text is identical for "email unknown to the school" and
  "email known but revoked";
- **S-15** the whole log of every scenario is asserted free of the email, the
  authorization code, the `state`, the client id, the secret reference and any
  response body;
- **S-17** the Control Plane writes no audit row and no `InstanceLicenseCheck` for
  a login check, and the `installation` row is unchanged;
- **S-19** the `409` and `500` bodies carry no stack trace, SQL, type name or file
  path;
- **S-20** resolving `IUnitOfWork` yields the decorated chain and no bare
  `UnitOfWork` or `ReadOnlyModeGuard` registration exists; `CheckLegitimacyUseCase`
  rethrows `ReadOnlyModeException` instead of folding it into `SaveFailed`.

### 5.6 Persistence

Against real PostgreSQL via Testcontainers (TC-2); the schema comes from the
migrations (PC-2), never `EnsureCreated()`:

- the single migration `InitialAppUserAndAuditEvent` creates `app_user` and
  `audit_event`, seeds nothing, and adds no index to `audit_event` and none on
  `app_user.last_successful_sign_in_at` (db-design 3.2, 4.3, 7.1);
- every column's type, length, nullability and default as db-design 3 and 4 fix
  them;
- all nine `app_user` check constraints and all eleven `audit_event` check
  constraints rejected on the values they exist to reject — each one its own
  case;
- `uq_app_user_normalized_email` rejects a second row, and a concurrent first
  sign-in is turned into a re-read rather than an error (I-10);
- `ck_audit_event_immutable` rejects an update that passes through EF Core, and
  the test **states in its name** that it does not defend against raw SQL
  (db-design 4.2);
- no foreign key exists on either table (section 5 of the design);
- the **Control Plane's** model snapshot and migration list are unchanged by this
  Story (7.2).

## 6. Excluded scenarios, with justification

| Excluded | Why |
|---|---|
| A live Google authorization or token exchange | TC-4. The handler runs; only its endpoints are local |
| A deployed Control Plane | TC-4. Either a scripted handler or the in-process host |
| The Dean sign-in, lockout, temporary password | US-012. Nothing anonymous is opened for it, which **is** tested (AC-002) |
| A user choosing their language | US-039. Only the school default is exercised |
| A real secret store | see section 7 and OD-004 — no artifact fixes the reference syntax |
| The retention purge reading either new table | EPIC-10. The columns it needs exist, which is tested |
| Browser rendering, CSS, phone width (NFR-070) | not automatable here; a manual check by the Owner |
| HSTS behaviour of a real browser | the header is asserted; enforcement is the browser's |

## 7. Known limitations

1. **No unit test names the sign-in use case.** Section 2.1 explains why. Every
   branch of FR-010 is covered through the callback endpoint with the reply
   scripted, which is deterministic but one level coarser than TC-1's preferred
   shape. If `IMPLEMENTATION` wants a direct unit test of
   `CompleteGoogleSignInUseCase` it may add one; the integration coverage is what
   this Story's Definition of Done rests on.
2. **The Google seam is reflective.** `GoogleSignInStub` resolves the handler's
   options type by name. Until `ClassroomAgent.Web` references the package
   (FR-021), the fixture throws a clear "the Google authentication handler is not
   configured" failure — a legitimate missing-behaviour red-phase failure. After
   implementation it binds to the real type. A rename inside the framework would
   break the fixture rather than a test's meaning; the fixture names the type in
   exactly one place.
3. **`ck_audit_event_immutable` is proven only through EF Core**, as db-design
   4.2 states. The test says so in its name so nobody reads it as protection
   against raw SQL.
4. **The domain-hint parameter name is an interpretation.** The artifacts say
   "the account-picker hint carries the school's domain"; `hd` is Google's only
   parameter with that meaning, so the test asserts `hd`. If IMPLEMENTATION uses
   another mechanism the test must be corrected, not the requirement.
5. **The secret store is exercised only as far as option 1 goes.** The resolution
   of OD-004 fixes an environment variable as the store, and the tests drive exactly
   that. A later move to a managed vault (US-009's decision to make) changes the
   mechanism and these three cases with it; the reference *shape* — one configuration
   string naming a secret — does not change.

## 8. Open Decisions affecting testing

| Id | State | Effect on these tests |
|---|---|---|
| OD-001, OD-002 | RESOLVED before activation (`trebovaniya.md` v78) | none; the tests are written against the resolutions |
| OD-003 | RESOLVED 2026-09-19 | none; the Google handler package is what section 2.2 drives |
| OD-004 | RAISED by this stage; **RESOLVED by the Owner on 2026-09-20 (option 1)** | none outstanding. The reference names an environment variable, resolved once at start-up through `Infrastructure/Secrets`; an absent or empty variable stops the start, a wrong secret does not (I-5). Three cases were added on the resolution: the resolved secret reaching the handler, the fail-fast start-up refusal, and the secret's value never reaching a log |

OD-004 is recorded in `docs/decisions/US-008-open-decisions.md` (v4). It needed no
change to `trebovaniya.md` — §5 already lists environment variables among the
stores — and none to the Specification, because FR-001's reference to
`Infrastructure/Secrets` is about placement, which the resolution satisfies.
