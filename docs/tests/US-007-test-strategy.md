---
artifact_type: test_strategy
story: US-007
version: 2
status: DRAFT
created_at: 2026-09-19T12:48:23Z
updated_at: 2026-09-19T13:12:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-007-read-only-mode.md
    version: null
  - path: docs/specifications/US-007-spec.md
    version: 2
  - path: docs/decisions/US-007-open-decisions.md
    version: 4
  - path: docs/designs/api/US-007-api-design.md
    version: 1
  - path: docs/designs/database/US-007-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 77
supersedes: null
---

# US-007 Test Strategy — Read-only mode enforcement

OD-003 was resolved on 2026-09-19 (option 1) and the stage completed: the
executable sources of section 4 exist, compile, and the red phase is verified on
real PostgreSQL. The evidence is `docs/evidence/US-007-test-generation-report.md`
(v2) and the mapping is `docs/tests/US-007-ac-test-matrix.md` (v2).

## 1. Scope

Every Acceptance Criterion of US-007 is in-process: `Application` refuses a write
or a Google call, carries the reason, keeps the BR-026 closed list working, and is
proven unbypassable by a structural test. There is no endpoint, no screen, no
entity and no migration (Specification sections 1, 9; both design stages returned
`NOT_APPLICABLE`).

Two consequences shape everything below:

- **No contract tests.** There is no HTTP surface to assert. TC-3 (status codes
  and error-body shape per endpoint) has nothing to bind to in this Story; it
  applies to US-008, which maps the refusal to `409`.
- **The tests are the verification.** There is no `IMPLEMENTATION_VERIFICATION`
  stage in this variant, so a green suite must by itself be sufficient evidence
  that AC-001 … AC-011 hold.

Out of scope for testing, because the Specification puts them out of scope: the
HTTP `409` mapping and its message (US-008), the `AuditEvent` table (US-008), any
UI, the real write use cases (each proves its own refusal in its own Story, TC-5),
and the Google ports themselves (US-009, US-011, US-013).

## 2. Test levels (TC-1, TC-2, TC-5)

| Level | Used for | Why |
|---|---|---|
| Integration over the installation host + real PostgreSQL (Testcontainers) | the guard, the backstop, the permitted write, reads in read-only mode, the refusal log line | the mode is read from the database through the real repository, and "nothing is committed" is only meaningful against a real database (TC-2; db-design §2) |
| Unit (no host) | `ServiceWriteScope` rules, `PermittedServiceWrite` membership, the structural rule as a pure function | deterministic logic with no I/O (TC-1) |
| Structural / architecture | the write-path and Google-path rule over the real `Application` assembly, the layering assertions, the DI composition | AC-007 and AC-011 are requirements about the shape of the solution, so the assertion is on the shape (TC-6 style, beside the US-005 `ProjectReferenceTests`) |
| Security-focused | no Google port called, no personal data in the refusal line, no audit row, refusal in `Application` not in UI | TC-5 requires read-only mode to be tested in the Application layer with substituted Google ports receiving no call |

The EF Core InMemory provider and SQLite are not used. No test calls a live
Google API or a live Control Plane (TC-4): `IControlPlaneClient` is the existing
`FakeControlPlaneClient` seam, and the Google path is exercised through a
synthetic port marked `IGoogleDataPort` that records whether it was called.

## 3. Substitution seams and fixtures

Reused from US-005 and US-006 unchanged:

- `InstallationTestHost` — one host over its own migrated PostgreSQL database,
  with `ManualTimeProvider` and `FakeControlPlaneClient`; `InsertLegitimacyStateAsync`
  seeds the single row, `LegitimacyStatesAsync` reads it back, `ReadLogEventsAsync`
  returns the parsed Serilog lines, `Time.Advance` moves the clock.
- `PostgreSqlFixture`, `LegitimacyStateRow`, `LogEvent`, `InstallationFactory`.

New, all in `tests/ClassroomAgent.Tests/TestInfrastructure`. They are test-only;
the production types the tests name exist as the compile-only skeleton OD-003
allowed, listed in the test-generation report §3:

| Fixture | Purpose |
|---|---|
| `SyntheticWriteUseCase` | takes `IUnitOfWork` and `IReadOnlyModeGuard`, calls the guard, then stages a row and commits — a compliant write path |
| `UnguardedWriteUseCase` | takes `IUnitOfWork` only and commits — the violation the backstop and the structural rule must catch |
| `SyntheticGooglePort` | implements the `IGoogleDataPort` marker and records every call; asserted to have received none |
| `SyntheticGoogleUseCase` | takes the marked port and the guard — the Google path |
| `WritePathRule` | the structural rule as a pure function over a set of types, so it can be run against both the synthetic types and the real assembly |

Three seed states cover the three reasons of BR-025, and each is produced by
seeding `legitimacy_state` and moving the manual clock — never by waiting:

- **never confirmed** — no row, or a row with `last_successful_check_at` null;
- **suspended** — a recent success with `status = 'suspended'`;
- **grace expired** — a success more than 7 days before the clock (strictly; the
  boundary itself is US-005 I-4 and is re-asserted here only where the guard's
  answer depends on it).

## 4. Scenarios

### 4.1 Positive (the refusal happens) — AC-001, AC-002

- The guard throws `ReadOnlyModeException` for each of the three reasons; the
  refusal is the same type in all three, differing only in payload.
- The exception carries `Reason` matching the seeded cause, `Operation` equal to
  the constant the caller passed, and `LastSuccessfulCheckAt` equal to the seeded
  success — **null** for `NotYetConfirmed`, non-null for the other two.
- The guard throws before the use case reaches a repository or a port: the
  synthetic write use case's row is absent afterwards and its port recorded no
  call.

### 4.2 Negative (the write proceeds) — AC-003, AC-005, AC-009

- Not in read-only mode: the guard returns and the synthetic write commits.
- In read-only mode, the permitted service write commits: `CheckLegitimacyUseCase`
  records `LegitimacyState` while the installation is suspended, past its grace
  period, and never confirmed — the three cases that would otherwise trap the
  installation (BR-026, AC-009).
- A read in read-only mode succeeds unchanged: `GetLegitimacyModeQuery` and
  `GetReadinessQuery` answer, and readiness stays HTTP `200` "degraded" (US-005
  FR-013) — read-only is a normal mode, not an outage.
- Leaving read-only mode: a suspended installation is seeded, a check answers
  active, and the next write succeeds **in the same host** — no restart.

### 4.3 Default refusal (the backstop) — AC-004

- In read-only mode, a commit with no open declaration throws and **nothing is
  committed**: the row count before and after is identical, asserted against the
  real database.
- In read-only mode, a commit inside a declaration scope naming a
  `PermittedServiceWrite` member commits.
- Not in read-only mode, a commit with no declaration commits — normal operation
  is unchanged, so the backstop cannot break every existing write.
- The backstop fires even when the use case never called the guard: this is the
  "forgot the guard" case and is the whole point of AC-004.

### 4.4 Boundary and timing — AC-008

- The mode is re-read per call: the same host refuses, the seeded row is updated
  to active, and the **next** write succeeds without a restart and without a new
  DI scope being required to see it.
- The reverse: a write succeeds, the clock advances past the grace period, and the
  next write refuses — no value cached for the process lifetime.
- Time comes from `ManualTimeProvider`; no test sleeps or waits on a real clock.

### 4.5 Validation — VR-001, VR-002, VR-003

- An empty or whitespace operation name is an `ArgumentException` and nothing is
  written.
- A second `Declare` before the first is disposed is an `InvalidOperationException`.
- An undefined `PermittedServiceWrite` value (a cast integer) is an
  `ArgumentException`.
- `PermittedServiceWrite` has exactly the four members of Specification FR-004,
  by name — the test that fails when the code list drifts from BR-026.
- `PermittedServiceWrites` maps `CheckLegitimacyUseCase` to `LegitimacyCheckState`.

### 4.6 Security — AC-006, AC-010, AC-011, S-01 … S-05

- **No Google call**: the synthetic use case holding a marked port runs in
  read-only mode; the port records zero calls. Asserted for each of the three
  reasons.
- **The marker**: `IGoogleDataPort` is an interface in `Application.Ports` with no
  members and no Google SDK type.
- **The refusal line**: exactly one `Warning` event per refusal, carrying the
  operation constant and the reason category as structured properties.
- **Log hygiene**: the line carries no email, no domain, no client id, no payload
  and no stack trace. Asserted positively (the expected properties are present)
  and negatively (a seeded recognisable value never appears in any log file).
- **No audit row, no write of any kind**: after a refusal the database is
  byte-for-byte unchanged, and no `AuditEvent` table is created by this Story.
- **The mode change is logged once**: two refusals in a row produce two refusal
  lines but no second "entered read-only mode" line (US-005 FR-010 keeps owning
  that event).
- **Layering**: no type outside `ClassroomAgent.Application` implements
  `IReadOnlyModeGuard`; `Web` holds no second decision about the mode; the US-005
  `ProjectReferenceTests` are re-run unchanged and must stay green — including
  `FrameworkFreeProjects_ReferenceNoPackage`, which OD-002's resolution was chosen
  to preserve.

### 4.7 The structural proof — AC-007

The rule: a type in `ClassroomAgent.Application.UseCases` whose constructor takes
`IUnitOfWork`, or a parameter marked `IGoogleDataPort`, must also take
`IReadOnlyModeGuard` **or** appear in `PermittedServiceWrites`.

- The rule is a pure function in the test project, exercised against four
  synthetic types — a compliant write path, a guarded Google path, a registered
  permitted write, and a write path with neither — and asserted to flag exactly
  the last one. This is what proves the detector detects.
- The same function over the real `Application` assembly must report **no**
  violation.
- The failure message is asserted to name the offending type and to state both
  remedies, including that a new service write is permitted only by extending the
  list in `trebovaniya.md` §2 first — so a later author is told how to comply.
- The DI composition is asserted: the resolved `IReadOnlyModeGuard` is not the
  bare `ReadOnlyModeGuard` and the resolved `IUnitOfWork` is not the bare
  `Infrastructure` `UnitOfWork`, so "every refusal is logged" is proven by
  registration and not assumed (FR-009, FR-011).

## 5. Required fixtures

- A running Docker daemon for Testcontainers (TC-2). It was down on the first
  attempt of this stage and the red phase was verified after it was started
  (Docker 29.8.0).
- No new NuGet package, in the test project or anywhere else.

## 6. Excluded scenarios, with justification

| Excluded | Why |
|---|---|
| HTTP `409`, the API-6 body, the translated message | OD-001 (resolved): they arrive with US-008. API-5 is knowingly not satisfied end to end after this Story |
| An audit row for a refused write | a refused write is not on the SC-11 audited list, and no `AuditEvent` table exists yet |
| The real write use cases (synchronization, Meet linking, Dean accounts, connection settings, "check access", templates) | none exists; each proves its own refusal in its own Story (TC-5, Story Notes) |
| The real Google ports | none exists; the marker and the rule are what this Story delivers (Specification I-9) |
| The retention purge and sign-in bookkeeping members of the list | no use case exists for either (US-008, US-012, EPIC-10); they are asserted as list membership only |
| Role and antiforgery tests (TC-5 endpoint rules) | this Story adds no endpoint; the existing host-wide tests of US-005 and US-006 keep covering the hosts |
| A non-UTC school time zone (TC-8) | this Story has no date shown to a user and no period boundary of its own; the grace boundary is US-005's and is tested there |

## 7. Known limitations

- **The permitted list cannot be exercised end to end.** Only
  `LegitimacyCheckState` has a use case today; `AuditEvent`, `SignInBookkeeping`
  and `RetentionPurge` are asserted as membership plus their comment binding to
  BR-026. Recorded so a green suite is not mistaken for proof that all four work.
- **AC-006 is proven on a synthetic port.** No real Google port exists, so what is
  proven is the mechanism and the rule, not any real Google call being blocked.
  The first Story adding a port proves its own refusal (Specification FR-007).
- **The backstop's "nothing committed" is proven for one staged write shape.** A
  use case that writes outside `IUnitOfWork` would bypass it, which AD-7 already
  forbids and Specification I-8 records.

## 8. Open Decisions affecting testing

| Id | Subject | Status | Effect on this stage |
|---|---|---|---|
| OD-001 | HTTP `409` mapping location | RESOLVED (option 1) | removes every contract test from this Story |
| OD-002 | Where the refusal log line is written | RESOLVED (option 3) | §4.6 asserts the line through the host's log files and the DI composition, not through a type in `Application` |
| OD-003 | How the red phase compiles when every new type is in `Application` | RESOLVED (2026-09-19): option 1 | a compile-only skeleton in `src/` lets the sources compile and fail with a missing-behaviour failure; `IMPLEMENTATION` owns those files |

**OD-003 in one paragraph.** Every scenario above named a type that did not exist
yet — `IReadOnlyModeGuard`, `ReadOnlyModeException`, `PermittedServiceWrite`,
`PermittedServiceWrites`, `ServiceWriteScope`, `ReadOnlyModeUnitOfWork`,
`IGoogleDataPort`. Unlike US-006, this Story has no HTTP, no new table and no new
setting, so there is no black-box seam through which the behaviour could be
asserted without referencing those types. Writing the sources now would leave a
suite that does not compile, which the Red-Phase rules of this stage forbid, and
this Skill may not create production files on its own judgement. US-001 OD-007 and
US-005 OD-002 settled the same question for their Stories by letting TEST_WRITING
declare compile-only skeletons; nothing generalises those decisions to this one.
The full options are in the open-decisions artifact.
