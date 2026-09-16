---
artifact_type: test_strategy
story: US-001
version: 2
status: DRAFT
created_at: 2026-09-16T08:35:56Z
updated_at: 2026-09-16T10:37:11Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-001-owner-first-run-setup.md
    version: null
  - path: docs/specifications/US-001-spec.md
    version: 3
  - path: docs/designs/api/US-001-api-design.md
    version: 1
  - path: docs/designs/api/US-001-openapi.yaml
    version: 1
  - path: docs/designs/database/US-001-db-design.md
    version: 1
  - path: docs/designs/database/US-001-entity-model.md
    version: 1
  - path: docs/decisions/US-001-open-decisions.md
    version: 4
  - path: trebovaniya.md
    version: 68
supersedes: null
---

# US-001 Test Strategy — Owner first-run setup

Traceability: `docs/tests/US-001-ac-test-matrix.md`. Evidence:
`docs/evidence/US-001-test-generation-report.md`.

## 1. Scope

The Control Plane host (`ClassroomAgent.ControlPlane`) as far as US-001 builds
it: setup gate, one-time setup code, setup, sign-in, lockout, sign-out, home page,
error page and catch-all, static files, antiforgery, cookies, session lifetime,
Owner and AuditEvent persistence, translations. Nothing in an installation.

AC-001 … AC-012 are all in scope; each maps to at least one test.

## 2. Test levels

| Level | Tool | Used for |
|---|---|---|
| **HTTP integration** | `WebApplicationFactory<Program>` of the Control Plane (`Microsoft.AspNetCore.Mvc.Testing`) + PostgreSQL via Testcontainers | every page and form of the OpenAPI contract: status codes, redirects, form messages, cookies, audit rows after the request; translations resolved from the host |
| **Security enumeration** | same host, `EndpointDataSource` | anonymous endpoints vs the SC-4 list; antiforgery on every non-GET endpoint (TC-5) |
| **Service integration** | `ControlPlane.Services` resolved from a DI scope of the host, against PostgreSQL (TC-1: Control Plane services use the `DbContext` directly) | setup concurrency, sign-in sequence and lockout |
| **Persistence** | raw SQL and `ControlPlaneDbContext` against the migrated database | constraints, singleton, column catalogue, check constraints, audit immutability trigger and interceptor, migration content, model drift |
| **Unit** | plain xUnit | setup-code format and comparison (OD-005), project-reference rule |

Framework: xUnit v3 on Microsoft.Testing.Platform (`global.json`), packages of OD-006
only. No EF Core InMemory, no SQLite (TC-2). Schema only from the migrations,
applied by the fixture — never `EnsureCreated()`. No test calls any external
service (TC-4, SC-13).

## 3. Fixtures and test seams

| Fixture (`tests/ClassroomAgent.Tests/TestInfrastructure`) | Role |
|---|---|
| `PostgreSqlFixture` | assembly fixture: one `postgres:17-alpine` container (`max_connections=1000`) for the run; `CreateDatabaseAsync` makes a fresh database |
| `ControlPlaneTestHost` | per test: a new database, migrations applied (never `EnsureCreated()`), then a started `ControlPlaneFactory`; temp key and log directories; raw-SQL, audit-row, owner-row and translation helpers; `Restart` starts a second host on the same database; disposal clears the connection pool and deletes the temp directories. Isolation is per test — stricter than TC-2's per class — so no test depends on order |
| `ControlPlaneFactory` | `WebApplicationFactory<Program>`, environment `Test` (not `Development`, S-17); sets the configuration keys below with `UseSetting`; replaces `TimeProvider`, `ISetupCodeGenerator`, `IOperatorConsole` |
| `TestTimeProvider` | hand-written `TimeProvider` the test advances (no extra package, OD-006), starting at the real UTC now truncated to seconds |
| `FixedSetupCodeGenerator` | returns a known code and counts calls |
| `CapturingOperatorConsole` | records operator-console lines |
| `FormClient` | HTTPS client that keeps cookies itself (read, copy, replay), remembers the last page's `__RequestVerificationToken`, never follows redirects |
| `PageResponse`, `Html`, `SetCookieHeader` | response snapshot (status, `Location` path, body, decoded text, `Set-Cookie`, headers); input values; token blanking; cookie attribute parsing |
| `HostEndpoint` | the host's `RouteEndpoint`s for the TC-5 enumerations: normalized pattern, methods, `IAllowAnonymous`, fallback (`Order == int.MaxValue`), static file (literal read-only path ending in a file name), antiforgery-exemption metadata |
| `StaticFiles` | locates a file under `wwwroot/css`, `js` or `images`, and the repository root |

**Seams the implementation must provide** — registered in DI so the factory can
replace them. The tests reference these names; the implementor may rename one only
together with the tests:

- `TimeProvider` — the only clock for lockout, audit `occurred_at` and session
  expiry (cookie authentication reads it from DI);
- `Services.ISetupCodeGenerator` (`string Generate()`) with the production
  `SetupCodeGenerator`, constructible without arguments (OD-005);
- `Services.IOperatorConsole` (`void WriteLine(string)`); the production
  implementation writes to standard output, not through logging (FR-002);
- `Services.SetupCodeComparer.Matches(expected, submitted)` — normalization and
  constant-time comparison (OD-005);
- `FirstRunSetupService.CreateOwnerAsync(login, password, setupCode, requestId, ct)`
  → `FirstRunSetupResult(Outcome, Session)` and
  `OwnerSignInService.SignInAsync(login, password, requestId, ct)` →
  `OwnerSignInResult(Outcome, Session)`, both resolvable from a DI scope (entity
  model §5);
- `Persistence.ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext>)`
  registered in DI; a context built with `UseNpgsql` alone applies the migrations;
- `Persistence.AuditEvent.OwnerSignInRefusedUnknownLogin(occurredAt, requestId)` and
  a readable `Id` (entity model §2.2);
- `Localization.SharedResource`: translations resolved as
  `IStringLocalizer<SharedResource>`, with keys defined for exactly `uk` and exactly
  `en` (`GetAllStrings(includeParentCultures: false)`);
- configuration keys `ConnectionStrings:ControlPlane`, `DataProtection:KeyDirectory`,
  `LogFile:Directory`;
- form field names `setupCode`, `login`, `password`, `passwordConfirmation`; token
  field `__RequestVerificationToken`; cookie names `__Host-cp-session`,
  `__Host-cp-antiforgery` (api-design §4, §6);
- at least one static file under `wwwroot/css`, `wwwroot/js` or `wwwroot/images`.

These are the "substitutable source" the Specification §9 leaves to the design;
they change no behaviour. Compile-only declarations of these types exist since
TEST_WRITING (OD-007).

## 4. Scenarios

### 4.1 Positive

- Setup with a valid code, login, password and confirmation creates one Owner,
  signs in, redirects to `/`, writes the success audit row (AC-002, AC-012).
- Sign-in with the correct login in a different letter case succeeds (AC-005).
- A signed-in Owner sees the home page; sign-out redirects to `/sign-in` and the
  old cookie no longer authenticates (AC-011).
- Every translation key exists in `uk` and `en` (AC-009).
- The error page, the catch-all and static files answer anonymously (AC-010).

### 4.2 Negative

- Any page before setup → `302 /setup` (AC-001).
- Setup page after setup → `302 /sign-in` anonymous, `302 /` signed in; POST →
  `409` with `Setup.AlreadyCreated` (AC-003).
- Missing code / wrong code → `400`, audit "wrong setup code"; the expected code
  is not in the response (AC-007, AC-012).
- Unknown login, wrong password, locked out → identical `401` responses with
  `SignIn.Refused`; each audited with its category (AC-005, AC-008).
- A `ReturnUrl` is ignored after sign-in (spec I-6).
- Form without antiforgery token → `400` error page, no account, no session; the
  same for sign-out, which keeps the session (AC-011).
- `GET /sign-out` → `404`, still signed in (AC-011).
- Anonymous endpoint not on SC-4 list → enumeration test fails (AC-010).
- `UPDATE` / `DELETE` on `audit_event` → database error; `Modified` / `Deleted`
  entity → `SaveChanges` throws (AC-008).

### 4.3 Boundary

| Rule | Just inside | Just outside |
|---|---|---|
| VR-001 login length | 4, 64 | 3, 65, empty |
| VR-001 characters | `a.b-c_9` | `own er`, `владелец`, `owner@x` |
| VR-002 password length (code points) | 15, 128 (also 15 Cyrillic letters; 15 and 128 emoji = 30 and 256 UTF-16 units → valid) | 14, 129; 129 emoji → invalid |
| VR-003 contains login | password without login | equals login in other case; contains login; contains login in other case |
| VR-007 confirmation | identical | differs by one character; differs only in case; trailing space; empty |
| Lockout | 4 failures → still signs in with correct password | 5 failures → locked, correct password refused |
| Lockout release | 15 min − 1 s → locked | 15 min + 1 s → correct password signs in |
| Session idle (OD-002) | 29 min since last request → authenticated | 30 min + 1 s → `302 /sign-in` |
| Session absolute | 7 h 59 min with activity → authenticated | 8 h 1 min with activity → `302 /sign-in` |

### 4.4 Validation

- All failed setup fields reported at once, each with its key (API design §4).
- `login` refilled on a validation error; `setupCode`, `password`,
  `passwordConfirmation` never refilled (AC-006).
- Sign-in with an empty field → `400`, no audit row, counter unchanged (spec I-2).
- Field-validation failure is not audited, even with a wrong code, and the code
  message is not shown (OD-004).

### 4.5 Security

- SC-4 anonymous enumeration; SC-4 antiforgery enumeration (anonymous forms
  posted anonymously, `/sign-out` posted as the signed-in Owner); no endpoint with
  antiforgery-exemption metadata.
- Cookie attributes: `__Host-cp-session` and `__Host-cp-antiforgery` are
  `Secure`, `HttpOnly`, `SameSite=Strict`, `Path=/`, no `Domain`; session cookie
  has no `Expires`/`Max-Age`; every other `Set-Cookie` is `Secure` (AC-011).
- Refusal indistinguishability: the three `401` responses are equal after blanking
  the token values and the typed login (AC-005).
- No response contains the submitted password, confirmation or setup code (AC-006,
  AC-007).
- The log files of the run contain neither the setup code, nor a typed login, nor a
  typed password (AC-007, v68).
- `ClassroomAgent.ControlPlane` references none of `ClassroomAgent.Domain`,
  `.Application`, `.Infrastructure`, `.Web` (AC-002, SC-12).
- Audit rows contain no login, password or code; anonymous rows have no actor id
  (AC-008, AC-012).
- A principal without the `Owner` role gets `403` and the error page, not a
  redirect (SC-4 v66).
- A real unhandled exception renders `500` without exception, type, namespace, SQL
  or path text (SC-10).
- Data Protection keys are written to the configured directory (SC-7).

### 4.6 Persistence

- Migration `*_InitialOwnerAndAudit` creates exactly `owner`, `audit_event` and
  `__EFMigrationsHistory`, and the trigger `trg_audit_event_immutable`; the model
  has no pending changes against the migrations.
- `owner`: columns, types, lengths, nullability and defaults exactly per db-design
  §3 (no email or phone); `uq_owner_singleton` rejects a second row with a different
  login; `uq_owner_normalized_user_name` and `uq_owner_singleton` are unique indexes;
  `ck_owner_ui_language`, `ck_owner_access_failed_count`, `ck_owner_singleton`.
- Stored password is not the plaintext and verifies with Identity's hasher
  (AC-002).
- `audit_event`: columns per §4, no foreign key; the five check constraints reject
  violating rows and accept the §4.1 shapes; the trigger rejects `UPDATE` and
  `DELETE`; `updated_at = created_at`.
- Concurrency: two setup submissions released together → one `owner` row, one
  success audit row, the other `409` / `AlreadyExists`; no refused-by-conflict audit
  row (AC-004).

## 5. Excluded scenarios

| Scenario | Why |
|---|---|
| Constant-time comparison of the setup code | not observable reliably in a test; enforced by review (SECURITY_REVIEW) |
| Randomness quality of the production code generator | only its format, length and variation are tested; the entropy source is reviewed |
| HTTPS-only listener, no HTTP port | Kestrel binding is deployment configuration (DC-6); the test host is in-process |
| Private-network reachability | deployment (SC-9) |
| Data Protection keys surviving a real process restart | covered by asserting the configured persistent directory receives the key ring; a real restart is a manual deployment check |
| Request culture `uk-UA` for date and number formats | US-001 renders no date or number, so the format culture is not observable; asserted by the first Story that displays one (TC-8) |
| The exact instant a lockout or an idle session ends | the Specification says "after 15 minutes" and "more than 30 minutes" without fixing equality; the tests assert one second on either side |

## 6. Known limitations

- **Greenfield red phase.** The production project is a compile-only skeleton
  (OD-007): nearly every test fails at its first request or DI resolution (no page
  → no antiforgery token, no localizer, no migration). This is the expected red
  phase; the test-generation report classifies every failure.
- **Unhandled-exception test** breaks the schema under the running host
  (`DROP TABLE owner`) to cause a real database exception, because the contract adds
  no endpoint that throws on demand.
- **403 test** re-protects the Owner's own authentication ticket without the role
  claim; it assumes the session cookie is not chunked.
- **Log-file test** stops the host and reads every file in the configured log
  directory; it requires at least one non-empty file there.
- **Test runner.** `dotnet test` runs on Microsoft.Testing.Platform (`global.json`),
  which xunit.v3 requires on the .NET 10 SDK; `dotnet test --filter
  FullyQualifiedName~<Class>` from `AGENTS.md` still works.

## 7. Open Decisions affecting testing

None open. Relied upon: OD-001 (password and lockout policy), OD-002 (session
lifetime), OD-003 (`409`), OD-004 (fields first), OD-005 (code format), OD-006
(test packages), OD-007 (compile-only skeleton created at TEST_WRITING, resolved by
the human on 2026-09-16).
