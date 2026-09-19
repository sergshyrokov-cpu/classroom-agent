---
artifact_type: specification
story: US-007
version: 2
status: APPROVED
created_at: 2026-09-19T11:54:48Z
updated_at: 2026-09-19T12:17:40Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-007-read-only-mode.md
    version: null
  - path: trebovaniya.md
    version: 77
  - path: docs/decisions/US-007-open-decisions.md
    version: 2
supersedes: null
---

# US-007 Specification — Read-only mode enforcement

## 1. Overview

US-005 taught the installation **whether** it is in read-only mode
(`GetLegitimacyModeQuery`, `LegitimacyMode`, `LegitimacyModeReason`). This Story
makes something act on the answer, and makes it act by default:

- **one enforcement point** in `ClassroomAgent.Application` — `IReadOnlyModeGuard`
  — that every write use case and every use case reaching a Google port calls
  before it touches a repository or a port;
- **a typed refusal** — `ReadOnlyModeException` in `Application/Exceptions` —
  carrying the reason as data (BR-025) and, where one exists, the time of the last
  successful check;
- **the closed list of permitted service writes** of BR-026, expressed once as an
  enum with a registry of the use cases that perform them — not as exemptions
  scattered across use cases;
- **a commit backstop** — an `IUnitOfWork` decorator in `Application` that refuses,
  in read-only mode, any commit no use case declared permitted, so a forgotten
  guard call cannot write;
- **the rule binding Google ports to the guard**, with the marker that lets it be
  enforced the day the first Google port arrives;
- **a structural test** that enumerates the write and Google paths and fails when
  one exists that neither passes the guard nor is declared a permitted service
  write — proven against synthetic violations, not merely green today;
- **the one live permitted case**: the `LegitimacyState` write of US-005, which
  must keep working in read-only mode or the installation could never leave it.

Sources: `trebovaniya.md` v77 §2 ("В режиме «только чтение»", the closed list of
service writes), §3 (`LegitimacyState`), §4 (Epic 8), §5, §8 (log levels), §9
(suspension, grace period); `architecture.md` AD-3, AD-4, AD-6, AD-7, AD-9;
`package-map.md` (`Application.UseCases` — "orchestration, transaction boundary,
read-only-mode check"; `Application.Exceptions`); `security-conventions.md` SC-5,
SC-8, SC-10, SC-11, SC-13; `deployment-conventions.md` DC-10;
`testing-conventions.md` TC-2, TC-5, TC-8; `api-conventions.md` API-5;
BR-024, BR-025, BR-026; NFR-073. US-005 is reused, not re-specified: nothing about
the determination of the mode is re-decided here.

**This Story is a mechanism Story.** It adds no endpoint, no screen, no entity and
no migration. After it, **API-5 is not satisfied end to end**: the refusal stops at
`Application` and the mapping to HTTP `409` with the translated message arrives
with US-008 (OD-001, resolved).

**Open Decisions:** OD-001 (carried from the Story, RESOLVED — option 1) and
OD-002 (RESOLVED 2026-09-19 — option 3: `Application` throws and a decorator in
`Infrastructure` writes the refusal log line; no package is added to
`Application`, no new port, and the US-005 architecture test stays unchanged).
FR-009 is written against that option. Behaviour not literally fixed by the Story
or `trebovaniya.md` is stated as interpretations I-1 … I-12 (section 11), all
accepted by the human at `HUMAN_SPEC_APPROVAL` on 2026-09-19.

## 2. Business Goal

`trebovaniya.md` §9 makes the `Installation` status the lever that decides whether
a school works at all, and §2 says what "works" shrinks to when it is pulled:
viewing and exporting, nothing else, and no call to Google. Until something
enforces that, suspending a school changes a row in the Owner's database and
nothing on the school's server, and the 7-day grace period is a number nobody
applies.

The second goal is durability. Every later installation Story adds writes and
Google calls (US-008 … US-039). If each carried its own read-only check, one
forgotten use case would be a Critical finding (SC-5) discovered by a security
review rather than by the compiler. This Story therefore delivers an enforcement
point that exists **before** those use cases are written, refuses by default, and
is guarded by a test that tells a later author what to do.

## 3. Business Flow

### 3.1 A school that has never been confirmed

A freshly deployed installation has no `LegitimacyState` row, so it is in read-only
mode with reason "never confirmed" (BR-025, US-005 FR-008). Its first legitimacy
check runs and writes `LegitimacyState` — a permitted service write (BR-026) — and
the school leaves read-only mode without anything else having been allowed to
write in the meantime.

### 3.2 The Owner suspends a school

The Owner suspends the `Installation`; the push (US-006) or the next scheduled
check records status `suspended` in `LegitimacyState`. The next write use case
calls the guard, the guard reads the current state and the current clock, and the
write refuses with reason `SuspendedByOwner`. No restart is needed: the mode is
read at the moment of the write, never cached (AC-008).

### 3.3 Contact with the Owner is lost

Checks keep failing. For 7 days nothing changes — the school works. Strictly more
than 7 days after the last success the grace period has expired (US-005 I-4) and
the next write refuses with reason `GracePeriodExpired`, carrying the time of the
last successful check. Deans keep reading and exporting everything already synced.

### 3.4 Contact is restored

A later check succeeds with status active (US-005 AC-007, US-006). The state row it
wrote was permitted throughout, so the school can leave the mode at all. The next
write finds `IsReadOnly == false` and succeeds — no restart, no manual step.

### 3.5 A later Story adds a write

An author adds a use case that writes. They take `IReadOnlyModeGuard` in its
constructor and call it first, or — if and only if the write is on the BR-026 list
— they register the use case in `PermittedServiceWrites` with the member it
performs. Doing neither fails the structural test with a message saying which of
the two to do.

## 4. Functional Requirements

### FR-001 The mode the enforcement point acts on

The guard and the backstop determine read-only mode **exclusively** through
`GetLegitimacyModeQuery` (US-005 FR-008): the stored `LegitimacyState` and the
injected `TimeProvider`. This Story re-implements none of it and changes none of
it.

- The determination is made at the moment the guard or the backstop runs. No value
  is cached in a field, a static, a singleton or for the process lifetime; a mode
  change takes effect on the next write without a restart (AC-008).
- `LegitimacyMode.IsReadOnly` alone decides whether to refuse. The three reasons
  produce one behaviour, differing only in the data the refusal carries (AC-001).
- Time comes from the injectable `TimeProvider` already registered by US-005, so
  the grace-period boundary is tested without waiting.
- If the state cannot be read (the database is unreachable), the guard does **not**
  swallow it: the exception propagates as it does today from
  `ILegitimacyStateRepository.GetForReadAsync`. A write does not proceed on an
  unknown mode (I-6).

### FR-002 The enforcement point

`Application/UseCases` gains `IReadOnlyModeGuard` with a single asynchronous
member, and `ReadOnlyModeGuard` implementing it over `GetLegitimacyModeQuery`:

- `Task EnsureAllowedAsync(string operation, CancellationToken cancellationToken)`.
- `operation` is a constant name of the refused action, supplied by the caller
  (for example `"SynchronizationRun"`). It is a compile-time constant of the
  calling use case, never built from user input or from data (VR-001, S-03).
- When the installation is **not** in read-only mode the call returns and the use
  case proceeds unchanged.
- When it **is**, the call throws `ReadOnlyModeException` (FR-003). It throws
  before the use case has reached a repository, a port or a transaction (AC-001),
  which is why every write use case calls it as its **first** statement, before
  any other await.
- The guard makes no decision about *which* operations are permitted beyond
  read-only mode itself: role permissions are `Application/Authorization`
  (`trebovaniya.md` §2 matrix) and are not this Story's concern.
- The guard is used by write use cases **and** by any use case that would reach a
  Google port (FR-007). It is not called by read or export use cases (FR-008).

Namespace placement follows `package-map.md`, which already assigns the
"read-only-mode check" to `Application.UseCases`; no change to `package-map.md` is
required by this Story.

### FR-003 The refusal

`Application/Exceptions` is created (it is listed in `package-map.md` and has had
no member until now) and gains `ReadOnlyModeException`:

- it carries `LegitimacyModeReason Reason`, `DateTimeOffset? LastSuccessfulCheckAt`
  and `string Operation`;
- `LastSuccessfulCheckAt` is non-null for `SuspendedByOwner` and
  `GracePeriodExpired` and **null** for `NotYetConfirmed`, where no successful
  check has ever happened (AC-002);
- the reason is carried as **data**, not as a formatted sentence: a caller renders
  it in Ukrainian or English later without parsing a message string (NFR-073,
  AC-002);
- its `Message` is a fixed English diagnostic for the log and for a developer. It
  is never shown to a user, never localized, and carries no stack trace, SQL,
  entity name, path or secret (SC-10, AD-9);
- it carries **no HTTP concept** — no status code, no problem-details body
  (AD-9). The mapping to `409` belongs to the `IExceptionHandler` of the
  installation host, which arrives with US-008 (OD-001).

Throwing is deliberate and is what AC-002 requires: a refusal returned as a status
value can be ignored by a caller that does not know about read-only mode, and the
rule must not depend on every future caller remembering it. This is consistent
with `AGENTS.md` ("exceptions signal failures, not expected outcomes"): from the
use case's point of view a refused write is a failure of the operation, not one of
its expected results.

### FR-004 The closed list of permitted service writes

`Application/UseCases` gains the enum `PermittedServiceWrite` with exactly one
member per bullet of the BR-026 closed list:

| Member | BR-026 bullet | Exists today |
|---|---|---|
| `AuditEvent` | audit rows | no (US-008) |
| `SignInBookkeeping` | Identity failed-attempt counting and lockout, the time of the last successful sign-in, creating the `AppUser` of an approved Admin at first sign-in, a Dean changing their own password, a user choosing their UI language | no (US-008, US-012) |
| `LegitimacyCheckState` | last successful check time, last known status, last compatibility state, the `Installation`'s domain and client ID | **yes** (US-005) |
| `RetentionPurge` | the retention purge and its audit event | no (EPIC-10) |

How the list stays bound to BR-026 (Story Notes):

- the enum is the **single** place the list is expressed in code; no use case
  carries a private exemption;
- each member carries a comment naming BR-026 and `trebovaniya.md` §2, and the
  type carries the sentence that a new service write is permitted only by
  extending the list **in `trebovaniya.md` §2 first**;
- a test asserts the enum members are exactly these four names, so adding a fifth
  in code without the rule fails the suite (FR-010).

The grouping of the five sign-in writes into one member is an interpretation
(I-2): BR-026 states them as one bullet, and none of them exists yet.

### FR-005 Declaring a permitted service write

A use case that performs a write on the FR-004 list declares it, once, in two
places that must agree:

1. **statically** — an entry in `PermittedServiceWrites`, a registry in
   `Application/UseCases` mapping the use-case type to the `PermittedServiceWrite`
   member it performs. This is what the structural test reads (FR-010);
2. **at run time** — the use case opens a declaration scope around its commit:
   `using var _ = writeScope.Declare(PermittedServiceWrite.LegitimacyCheckState);`
   where `writeScope` is the scoped `ServiceWriteScope` of `Application/UseCases`.
   The backstop (FR-006) reads it.

The scope is scoped to the DI scope, holds at most one declaration at a time, and
is cleared on dispose. It holds a `PermittedServiceWrite` value only — never a
request, a payload or personal data.

**The one live case.** `CheckLegitimacyUseCase` (US-005) is registered in
`PermittedServiceWrites` as `LegitimacyCheckState` and opens that scope around
`IUnitOfWork.SaveChangesAsync`. It does **not** call the guard: its whole purpose
is to run while the installation is in read-only mode (AC-003, AC-009). Its
existing behaviour, outcomes and tests are otherwise unchanged.

### FR-006 The commit backstop

`Application/UseCases` gains `ReadOnlyModeUnitOfWork`, an `IUnitOfWork` decorator
registered in front of the `Infrastructure` implementation:

- on `SaveChangesAsync` it determines the mode (FR-001);
- **not** in read-only mode → it delegates to the inner unit of work. Normal
  operation is unchanged;
- in read-only mode **with** an open declaration scope naming a
  `PermittedServiceWrite` member → it delegates. The permitted write commits
  (AC-003);
- in read-only mode **without** one → it throws `ReadOnlyModeException` with
  `Operation` naming the backstop and the current reason, and **nothing is
  committed** (AC-001, AC-004). This is the default: a write use case that forgot
  the guard still cannot write, and no declaration makes refusal the fallback
  rather than permission.

The backstop is defence in depth, not the primary enforcement: it fires after a
repository has staged changes, while AC-001 requires the refusal to happen before a
repository or port is reached. The guard (FR-002) is the primary point; the
backstop exists so that "refused by default" is a property of the system rather
than of an author's memory.

Both layers live in `Application`. The decorator holds no `DbContext` and no
persistence knowledge — it wraps the port (AD-3, AD-6, AD-7).

### FR-007 No call to Google in read-only mode

`Application/Ports` gains the marker interface `IGoogleDataPort`. Every port whose
implementation calls a Google API implements it — `IClassroomReader` and
`IMeetReportsReader` when they arrive (US-009, US-011, US-013). The marker is
empty: it carries no method and no Google SDK type (AD-4).

The rule, enforced by FR-010: **a use case whose constructor takes a port marked
`IGoogleDataPort` must also take `IReadOnlyModeGuard` and call it first.** The
refusal therefore happens before the port would be called, so nothing leaves the
process in read-only mode (AC-006, SC-5, SC-8) — synchronization, the Meet pull,
"check access" and the startup access self-check alike.

No Google port exists in the solution today. This Story fixes the marker, the rule
and the test; the first Story adding such a port proves its own refusal there
(TC-5).

### FR-008 Reads and exports are never affected

- The guard, the declaration scope and the backstop apply to writes and to Google
  calls only. A read use case takes none of them and is not made to depend on the
  legitimacy state in any way (AC-005).
- Exporting already-synced data is a read of the installation database plus
  rendering; it is untouched by this Story and keeps working in read-only mode.
- `GetLegitimacyModeQuery` and `GetReadinessQuery` (US-005) are reads and stay
  reads. Readiness keeps reporting "degraded" in read-only mode and keeps
  answering HTTP 200 (US-005 FR-013) — read-only is a normal mode, not an outage.

### FR-009 Logging a refused write

One log line per refusal, at `Warning` (DC-10, AC-010):

- it names the refused **operation** (the FR-002 constant) and the **reason
  category** (`LegitimacyModeReason`), both as structured properties;
- it carries no personal data, no request body, no rejected payload, no exception
  message built from data, and no stack trace (SC-10, S-03);
- written inside a request it carries the request identifier through the existing
  Serilog log context (DC-10); written by a background service it carries whatever
  that service's scope already provides;
- it is written once per refusal. Entering and leaving read-only mode keep being
  logged once each by US-005 FR-010; a refused write never re-logs the mode change
  (AC-010);
- **no audit row is written.** A refused write is not on the SC-11 audited list,
  and this Story creates no `AuditEvent` table (AC-010, S-04).

**Mechanism.** `ClassroomAgent.Application` holds no logging abstraction and may
hold no NuGet package. OD-002 is resolved (2026-09-19, option 3):
`Infrastructure` registers `LoggingReadOnlyModeGuard`, a decorator around
`IReadOnlyModeGuard` that catches `ReadOnlyModeException`, writes the line through
`ILogger<>` and rethrows unchanged. The decorator observes the refusal; it never
decides it (AC-011). The backstop's refusal (FR-006) is logged the same way, by a
decorator around `IUnitOfWork` in `Infrastructure`.

The two options not taken are recorded next to the decision: option 1 would have
replaced the decorators with a port in `Application/Ports` and a `package-map.md`
change; option 2 would have let `ReadOnlyModeGuard` log directly and required
Story AC-011 and the US-005 architecture test to be amended.

### FR-010 Proof that the enforcement cannot be bypassed

A structural test in `tests/ClassroomAgent.Tests/Architecture` (AC-007):

- **What it enumerates.** Every public type in `ClassroomAgent.Application.UseCases`
  whose constructor takes `IUnitOfWork` (a write path) or a parameter whose type
  is marked `IGoogleDataPort` (a Google path).
- **The rule.** Each such type must either take `IReadOnlyModeGuard` in the same
  constructor, or appear in `PermittedServiceWrites` with the member it performs.
  Neither → a violation.
- **Its failure message** names the offending type and states both remedies
  verbatim: add `IReadOnlyModeGuard` to the constructor and call
  `EnsureAllowedAsync` first, **or**, only if the write is on the BR-026 list,
  register the type in `PermittedServiceWrites` with its member — and that a new
  service write is permitted only by extending the list in `trebovaniya.md` §2
  first. A later author is told how to comply rather than tempted to delete the
  test.
- **It is proven to detect the violation.** The rule is a pure function over a set
  of types; it is exercised against synthetic types declared in the test project —
  a compliant write path, a guarded Google path, a registered permitted write, and
  a write path with neither — and asserted to flag exactly the last one. Running it
  over the real `Application` assembly must then report no violation (AC-007).

Three further tests in the same place:

- `PermittedServiceWrite` has exactly the four members of FR-004 (bound to BR-026);
- the DI registration puts the logging decorators in front of `IReadOnlyModeGuard`
  and `IUnitOfWork`, so "every refusal is logged" is asserted, not assumed (FR-009);
- the enforcement point exists only in `Application`: no type outside
  `ClassroomAgent.Application` implements `IReadOnlyModeGuard` or references
  `LegitimacyModeReason` to make a second decision about the mode; `Web` holds no
  copy of the rule (AC-011, AD-3, AD-6). The US-005 `ProjectReferenceTests` keep
  passing unchanged.

### FR-011 Wiring

In `Web/Configuration/InstallationServices`:

- `IReadOnlyModeGuard` → `ReadOnlyModeGuard`, scoped, behind its logging decorator;
- `ServiceWriteScope`, scoped;
- `IUnitOfWork` → `ReadOnlyModeUnitOfWork` wrapping the `Infrastructure`
  `UnitOfWork`, scoped, behind its logging decorator. The `Infrastructure`
  implementation stays registered as the inner instance;
- lifetimes are **scoped**, never singleton: a singleton guard would outlive the
  `DbContext` it reads through and would invite the cached mode AC-008 forbids.
  The background check service already creates a scope per run (US-005).

No new NuGet package is added to any project by this Story, in any option of
OD-002 except option 2.

## 5. Acceptance Criteria

Carried from the Story with the same ids; the Story is the authority for wording.

| AC | Title | Specified by |
|---|---|---|
| AC-001 | A write refuses while the installation is in read-only mode | FR-001, FR-002, FR-006 |
| AC-002 | The refusal carries the reason | FR-003 |
| AC-003 | The closed list of permitted service writes still runs | FR-004, FR-005, FR-006 |
| AC-004 | Anything not on the list is refused by default | FR-004, FR-006 |
| AC-005 | Reading and exporting are never blocked | FR-008 |
| AC-006 | No call to Google is made in read-only mode | FR-002, FR-007 |
| AC-007 | The enforcement cannot be bypassed | FR-010 |
| AC-008 | The mode is evaluated at the moment of the write | FR-001, FR-011 |
| AC-009 | Leaving read-only mode restores writes | FR-001, FR-005 |
| AC-010 | A refused write is logged, not audited | FR-009 |
| AC-011 | Enforcement lives in Application only | FR-002, FR-006, FR-010 |

## 6. Validation Rules

This Story exposes no endpoint, accepts no request body and reads no external
input: it has no user-supplied data to validate (SC-10's "external input" rules
have nothing to bind to here). What takes their place are the internal invariants
the enforcement point depends on; each is a test, not a `DataAnnotations`
attribute.

### VR-001 The operation name passed to the guard

| Rule | Valid | Invalid |
|---|---|---|
| Non-empty | `"SynchronizationRun"` | `""`, `" "`, `null` |
| A compile-time constant of the calling use case | a `const string` member | a value built from a request, an entity or user input |
| Carries no data | an action name | an email, a name, an id, a payload fragment |

An invalid operation name is a programming error: the guard throws
`ArgumentException` and nothing is written. It never reaches a log line as data
(S-03).

### VR-002 The permitted-service-write declaration

| Rule | Valid | Invalid |
|---|---|---|
| Names a defined member of `PermittedServiceWrite` | one of the four of FR-004 | an undefined enum value, a cast integer |
| At most one declaration open per DI scope | one `using` around one commit | a second `Declare` before the first is disposed |
| The use case is also registered statically | an entry in `PermittedServiceWrites` | run-time declaration only |

The first two throw `ArgumentException` / `InvalidOperationException`; the third
fails the structural test (FR-010).

### VR-003 The closed list

`PermittedServiceWrite` has exactly the four members of FR-004, with those names.
Any other set fails the suite — the mechanism that keeps the code bound to BR-026
(FR-004, FR-010).

## 7. Security Requirements

- **S-01 (SC-5, AD-6, BR-026).** In read-only mode every write use case refuses
  except the BR-026 closed list, and viewing and exporting keep working. A write
  path that bypasses the guard, or a service write not on that list, is a Critical
  finding. FR-002, FR-006 and FR-010 are what make this checkable rather than
  aspirational.
- **S-02 (SC-5, SC-8).** In read-only mode no Google port is invoked at all —
  synchronization, the Meet pull, "check access" and the startup access self-check
  included. The refusal happens before the port is reached, so nothing leaves the
  process (FR-007). A Google call in read-only mode is a Critical finding.
- **S-03 (SC-10, DC-10).** The refusal log line carries an operation constant and
  a reason category only: no personal data, no request body, no rejected payload,
  no stack trace, no entity or connection detail. `ReadOnlyModeException.Message`
  is a fixed English string (FR-003, FR-009).
- **S-04 (SC-11).** A refused write writes **no** audit row: it is not an audited
  action, and this Story creates no `AuditEvent` table. The table and the audited
  actions arrive with US-008.
- **S-05 (AD-3, AD-6, AC-011).** The rule is decided in `Application` only. `Web`
  holds no copy and no second decision; `Domain` still depends on nothing;
  `Application` still holds no `Infrastructure` reference. Hiding a button is not
  enforcement and is not part of this Story.
- **S-06 (BR-026, `trebovaniya.md` §2).** The permitted list is closed. Extending
  it in code without extending it in `trebovaniya.md` §2 first is a requirements
  violation; FR-004 and FR-010 make the divergence fail the suite.
- **S-07 (SC-13).** No outbound destination is added. This Story sends nothing
  anywhere: the Control Plane channel of US-005 and US-006 is untouched.
- **S-08 (AD-9, API-5).** The refusal is an application exception carrying no HTTP
  concept. Its mapping to `409` is US-008 (OD-001); until then a refusal reaching
  the installation's public port would surface as an unhandled error, which no
  endpoint can trigger because the installation still maps nothing there (US-005
  I-13).
- **S-09 (SC-7).** The service-account key is not touched, read or referenced by
  this Story.

## 8. Error Handling

| Situation | Behaviour |
|---|---|
| Write refused by the guard | `ReadOnlyModeException` with the reason and the operation; nothing staged, nothing committed; one `Warning` line (FR-009) |
| Write refused by the backstop | `ReadOnlyModeException` naming the backstop as the operation; the staged changes are **not** committed and the DI scope ends without a commit; one `Warning` line |
| Google path refused | as the guard refusal; no port is constructed, called or reached |
| `LegitimacyState` cannot be read when the mode is determined | the repository's exception propagates; the write does not proceed on an unknown mode (I-6); no refusal line is written, the failure is logged by the host as an unhandled exception (DC-10 `Error`) |
| Invalid operation name or declaration | `ArgumentException` / `InvalidOperationException` — a programming error, caught by tests, never by a user |
| A permitted service write fails for its own reasons | unchanged from its own Story; US-005 `CheckLegitimacyUseCase` keeps returning `CheckOutcome.Failed(SaveFailed)` and keeps not throwing |
| A refusal reaches HTTP | out of scope: no installation endpoint exists. US-008 maps it to `409` with the API-6 body or the translated error page (OD-001) |

No error surface of US-005 or US-006 changes.

## 9. Non-Functional Requirements

- **NFR-073.** The refusal carries the reason as data, never as a rendered
  sentence, so the message a user eventually sees is chosen and translated by the
  presentation layer (US-008) in Ukrainian or English.
- **Cost.** The guard and the backstop each read the single `LegitimacyState` row —
  a primary-key read of a one-row table — per write. Two reads per write is
  accepted in exchange for a correct answer at the moment of the write (I-5, I-7).
- **NFR-062 / coding conventions.** Every added member is asynchronous with a
  `CancellationToken`, nullable-enabled, one public type per file, constructor
  injection only, no static mutable state. `TreatWarningsAsErrors` applies.
- **TC-2, TC-8.** The story-level tests are unit tests over `Application` plus the
  structural tests; the existing PostgreSQL integration tests of US-005 are reused
  where a real commit must be observed. The EF Core InMemory provider is not used.
- **No migration, no schema change.** This Story adds no entity and no column
  (PC-2 has nothing to require here).

## 10. Out of Scope

- The determination of read-only mode and its logging — US-005 AC-009, AC-010.
- The HTTP `409` mapping, the API-6 error body, the error page and the translated
  message — US-008 (OD-001). **API-5 is not satisfied end to end after this
  Story.**
- The `AuditEvent` table and any audit row — US-008.
- Showing the mode or its reason to a user: banners, disabled buttons, hidden
  menus. Nobody can sign in to an installation yet, and hiding UI is not
  enforcement (AD-6).
- The write use cases themselves — synchronization, Meet code linking, Dean
  accounts, connection settings, "check access", report templates. Each arrives
  with its own Story and proves its own refusal there (TC-5).
- The Google ports and any Google SDK code — US-009, US-011, US-013. Only the
  marker and the rule are delivered here.
- The retention purge — EPIC-10. Its member exists in the list; its use case does
  not.
- The startup access self-check skipped in read-only mode — US-011.
- Identity, `AppUser`, sign-in bookkeeping and the UI-language choice — US-008,
  US-012.
- The two minor findings of the US-006 security review (F-1 Control Plane
  `System.Net.Http` log level, F-2 push size limit counted in characters): Control
  Plane files, belonging to the next Story that touches them (Story Notes).

## 11. Open Decisions

Full text: `docs/decisions/US-007-open-decisions.md`.

| Id | Subject | Status | Impact |
|---|---|---|---|
| OD-001 | Where the HTTP `409` mapping and the user-visible message belong | RESOLVED (2026-09-19): option 1 | the refusal stops at `Application`; API-5 not satisfied end to end after this Story (sections 1, 8, 10) |
| OD-002 | How `Application` writes the refusal log line without a NuGet package | RESOLVED (2026-09-19): option 3 | FR-009 logs the refusal from a decorator in `Infrastructure`; nothing is added to `Application` and the US-005 architecture test stays unchanged |

### Interpretations

*Accepted 2026-09-19 by the human (the Owner) at `HUMAN_SPEC_APPROVAL`: I-1 … I-12.*
A rejected interpretation would have become an Open Decision.

- **I-1** The guard is an interface (`IReadOnlyModeGuard`) rather than a concrete
  class, so the refusal can be observed by a decorator (FR-009) and substituted in
  a unit test. It is not a port: it reaches nothing outside the process (AD-4).
- **I-2** The five sign-in writes of BR-026 become one enum member
  (`SignInBookkeeping`) because BR-026 states them as one bullet and none exists
  yet. If a later Story needs them separated, the list is split then — in
  `trebovaniya.md` §2 first.
- **I-3** The declaration is **two-part** — a static registry read by the test and a
  run-time scope read by the backstop — because neither alone satisfies both
  AC-004 (default refusal at run time) and AC-007 (a structural test that can see
  the declaration without running the use case).
- **I-4** `CheckLegitimacyUseCase` does not call the guard. Calling it and then
  ignoring the result would be a second decision about the mode; being registered
  as a permitted service write is the declaration the design asks for (FR-005).
- **I-5** The mode is read twice per permitted-write path (guard and backstop) and
  once per refused path. No memoization is introduced: correctness at the moment of
  the write (AC-008) outranks saving a one-row read.
- **I-6** When `LegitimacyState` cannot be read, the write does not proceed. The
  alternative — assuming "not read-only" on a database failure — would let a
  suspended school write during an outage; assuming "read-only" would be a second
  determination this Story is not allowed to make (US-005 owns it).
- **I-7** The backstop refuses at `SaveChangesAsync`, not by inspecting the staged
  entities: entity inspection needs the `DbContext` and would move the rule into
  `Infrastructure`, which AD-6 and AC-011 forbid.
- **I-8** "Write path" is defined for the structural test as *a use case whose
  constructor takes `IUnitOfWork`*. A use case that writes without the unit of work
  would violate AD-7 and is caught by that convention, not by this test.
- **I-9** "Google path" is defined as *a constructor parameter marked
  `IGoogleDataPort`*. The marker is introduced now so the rule is enforceable the
  day the first Google port lands, rather than being re-derived then.
- **I-10** `Application/Exceptions` is created by this Story with one member. It
  is already in `package-map.md`; nothing there changes.
- **I-11** The structural test lives with the US-005 architecture tests
  (`tests/ClassroomAgent.Tests/Architecture`) rather than beside the use-case
  tests: it is a rule about the solution's shape, not about one use case.
- **I-12** No test asserts the refusal of a use case that does not exist yet. The
  live proof is the permitted case (`CheckLegitimacyUseCase` committing in
  read-only mode) plus synthetic use cases in the test project for the refused
  paths; every real write use case proves its own refusal in its own Story (TC-5,
  Story Notes).

## 12. Traceability

| AC | Functional requirements | Validation rules | Security | Open Decisions |
|---|---|---|---|---|
| AC-001 | FR-001, FR-002, FR-006 | VR-001 | S-01, S-05 | — |
| AC-002 | FR-003 | — | S-03, S-08 | OD-001 |
| AC-003 | FR-004, FR-005, FR-006 | VR-002, VR-003 | S-01, S-06 | — |
| AC-004 | FR-004, FR-006 | VR-002 | S-01, S-06 | — |
| AC-005 | FR-008 | — | S-01 | — |
| AC-006 | FR-002, FR-007 | VR-001 | S-02 | — |
| AC-007 | FR-010 | VR-002, VR-003 | S-01, S-06 | — |
| AC-008 | FR-001, FR-011 | — | S-01 | — |
| AC-009 | FR-001, FR-005 | VR-002 | S-01 | — |
| AC-010 | FR-009 | VR-001 | S-03, S-04 | OD-002 |
| AC-011 | FR-002, FR-006, FR-010 | — | S-05 | OD-002 |

Requirements with no single AC but required by conventions the Story cites:
FR-011 wiring and lifetimes (AD-3, AD-7, AC-008), S-07 (SC-13), S-09 (SC-7).
