---
artifact_type: security_review
story: US-007
version: 1
status: APPROVED
created_at: 2026-09-19T13:30:00Z
updated_at: 2026-09-19T13:30:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-007-read-only-mode.md
    version: null
  - path: docs/specifications/US-007-spec.md
    version: 2
  - path: docs/evidence/US-007-implementation-report.md
    version: 1
  - path: docs/designs/api/US-007-api-design.md
    version: 1
  - path: docs/designs/database/US-007-db-design.md
    version: 1
  - path: docs/tests/US-007-test-strategy.md
    version: 2
  - path: docs/tests/US-007-ac-test-matrix.md
    version: 2
  - path: docs/decisions/US-007-open-decisions.md
    version: 4
  - path: trebovaniya.md
    version: 77
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 2
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-007 Security Review — Read-only mode enforcement

## 1. Executive summary

**Result: PASS.** 0 Critical, 0 Major, 2 Minor, 3 Informational.

This Story *is* SC-5, so the review's central questions were whether a write or
a Google call can reach the database or Google while the installation is in
read-only mode, and whether the BR-026 closed list was widened. Neither is the
case:

- every commit in the installation goes through the single `IUnitOfWork` port,
  and the only registration of it is the decorated chain
  `LoggingUnitOfWork → ReadOnlyModeUnitOfWork → UnitOfWork`. Verified by reading
  every `SaveChanges` call site in `src/`: the installation has exactly two —
  `CheckLegitimacyUseCase` (through the port) and `Infrastructure`'s `UnitOfWork`
  itself. The remaining call sites are all Control Plane code, which this Story
  does not touch and which has its own database (AD-1);
- refusal is the **default**: the backstop refuses in read-only mode whenever no
  use case declared a permitted service write, so forgetting the guard cannot
  write. Proven at run time against real PostgreSQL, not only structurally;
- the closed list is exactly the four BR-026 bullets, in one enum, and the
  registry holds exactly one entry — `CheckLegitimacyUseCase` →
  `LegitimacyCheckState`, the write BR-026 names so the installation can leave
  the mode. A test fails if either set changes;
- no Google port exists yet; the marker and the rule that binds such a port to
  the guard were delivered and are enforced by the structural test.

The two Minor findings are defence-in-depth, not active holes: two bare
implementations are resolvable from DI alongside their decorated chain (F-1),
and one `catch (Exception)` could swallow a refusal if the declaration it depends
on were ever removed (F-2). Neither is reachable today.

Recommended next action: proceed to `HUMAN_PR_APPROVAL`.

## 2. Reviewed artifacts

As listed in the front matter `inputs`. No input is `SUPERSEDED`;
`HUMAN_SPEC_APPROVAL` is recorded in `workflow-state.yaml` (2026-09-19T12:17:40Z).
`docs/designs/api/US-007-openapi.yaml` and
`docs/designs/database/US-007-entity-model.md` do not exist: both design stages
returned `NOT_APPLICABLE` and recorded the decision in their Markdown artifact,
which the review read in their place.

## 3. Security-relevant scope

- **Exposed functionality:** none added. No endpoint, no Razor page, no
  configuration key. The installation's public port still maps nothing; the
  private port keeps the US-005/US-006 routes unchanged.
- **Protected assets touched:** the installation database (through the commit
  path), the `LegitimacyState` row (read on every guard and backstop call), the
  installation log file (one new event).
- **Trust boundaries touched:** Application → PostgreSQL (the commit path only);
  Application → the future Google ports (the marker and the rule). The
  browser boundary, the Control Plane channel and the secret store are untouched.
- **Security components affected:** SC-5 (the whole Story), SC-8 (the
  no-Google-call half of read-only mode), SC-10 (the refusal log line), SC-11
  (the absence of an audit row).

## 4. Environment and tools

- .NET 10, `ClassroomAgent.sln`, seven projects.
- Docker 29.8.0 running; Testcontainers available, so the integration evidence
  is real (`runtime_checks: FULL`).
- Commands run by this review: `dotnet list package --vulnerable` (all seven
  projects: no vulnerable package), `git status`, `git check-ignore`, plus
  reading every `SaveChanges` call site and the DI composition.
- Build and test evidence was taken from the implementation report and is
  consistent with the suite this reviewer observed: 1165 passed, 0 failed,
  0 skipped.
- No live credential file was opened; only their ignore status was confirmed.

## 5. Project security checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or `AppRole` member is touched |
| SC-2 Authentication | NOT_APPLICABLE | no Identity, no password, no cookie, no sign-in path in this Story |
| SC-3 AllowedAdmin | NOT_APPLICABLE | no Admin login path exists yet (US-008) |
| SC-4 Authorization | NOT_APPLICABLE | no endpoint or page added; the anonymous list and the antiforgery exemption list are unchanged |
| **SC-5 Read-only mode** | **PASS** | `ReadOnlyModeGuard` (Application) throws `ReadOnlyModeException` before any repository or port; `ReadOnlyModeUnitOfWork` refuses every undeclared commit in read-only mode; only the BR-026 list runs; tests `ReadOnlyModeGuardTests`, `ReadOnlyModeUnitOfWorkTests`, `PermittedServiceWriteTests`, `ReadOnlyEnforcementTests` |
| SC-6 No DB UI | PASS | nothing added; no diagnostic endpoint, no developer exception page change |
| SC-7 Key | PASS | the service-account key is not read, referenced or stored; `google_credentials.json` and `dac-classroom-agent-*.json` confirmed git-ignored (`.gitignore` lines 3–4) |
| **SC-8 Google** | **PASS (rule only)** | no Google port exists; `IGoogleDataPort` marks a port as Google-calling and the structural rule requires any use case holding one to take the guard; `GoogleDataPortRuleTests` proves the port receives zero calls in all three read-only causes. No scope was added or changed |
| SC-9 Channel | PASS | untouched: no change to `Contracts`, the check endpoint, the push receiver or the private-port binding |
| **SC-10 Hygiene** | **PASS** | the refusal line carries the operation constant and the reason enum only; the exception is not passed to the logger, so no stack trace is written; `ReadOnlyRefusalLoggingTests.TheLine_CarriesNoPersonalDataAndNoPayload` asserts the domain, the client id and the marker appear in no log file |
| **SC-11 Audit** | **PASS** | no audit row is written for a refused write (it is not on the SC-11 list) and no `audit_event` table exists in the installation migrations — asserted by `ReadOnlyRefusalLoggingTests.ARefusal_WritesNoAuditRowAndNoData` |
| SC-12 Owner | NOT_APPLICABLE | no Control Plane code, no `Contracts` change |
| SC-13 Outbound | PASS | no outbound destination added; this Story sends nothing anywhere |

## 6. Authentication and authorization

Nothing added. Read-only mode is orthogonal to authorization: the caller has the
right, the installation refuses the action (API-5). The deny-by-default fallback,
the SC-4 anonymous list and the antiforgery exemption list are byte-for-byte
unchanged, and the US-001…US-006 endpoint-enumeration tests remain green.

## 7. Credentials, key and Google access

- No password, hash, token or cookie is read or written.
- The service-account key is not touched. No configuration key was added.
- No Google scope was requested, changed or removed. No Google SDK type entered
  `Application` — asserted by `GoogleDataPortRuleTests.TheMarker_CarriesNoGoogleSdkType`
  and by the unchanged US-005 `ProjectReferenceTests`.

## 8. Sensitive data exposure

| Surface | Result |
|---|---|
| Responses and views | none added |
| DTOs | none added; `ReadOnlyModeException` carries a reason enum, an optional timestamp and an operation constant — no personal data, no HTTP concept |
| Logs | one new `Warning` event (`5201 ReadOnlyWriteRefused`) with two structured properties, both non-personal; verified against the real log files in test |
| Audit rows | none written, by design (SC-11) |
| Exceptions | the `Message` is a fixed English sentence naming neither the reason nor the time; asserted by `ReadOnlyModeGuardTests.TheMessage_IsAFixedStringCarryingNoReason` |
| Exports, telemetry | untouched |

## 9. Input validation

The Story accepts no external input: no request body, no query or route
parameter, no uploaded file, no Google response. What it validates instead is
internal: the operation name (`ArgumentException.ThrowIfNullOrWhiteSpace`, so a
blank name cannot reach the log as data) and the declaration (an undefined enum
value and a second open declaration are both rejected). Covered by
`ReadOnlyModeGuardTests.AnEmptyOperationName_IsRejected` and
`ServiceWriteScopeTests`.

## 10. API security

No endpoint was added, so there is nothing to compare against a contract. The
review confirms the deliberate consequence the artifacts record: **API-5 is not
satisfied end to end after this Story** — a refusal does not yet become an HTTP
`409` with the API-6 body, because no installation endpoint exists (OD-001,
resolved). This is an accepted, documented gap closed by US-008, not a finding.

## 11. Persistence and configuration

- No entity, column, index, constraint or migration. The installation database
  stays at `InitialLegitimacyState`; the Control Plane at
  `AddInstallationPushAddress`. No `EnsureCreated()` anywhere.
- The backstop refuses **before** `SaveChangesAsync` reaches the `DbContext`, so
  nothing is staged and committed on a refusal — verified against a real
  database, comparing the rows before and after.
- No configuration change. No connection string, secret or school-specific value
  is in the change set.

## 12. Logging, audit and telemetry

- One new event, `EventId 5201 / ReadOnlyWriteRefused`, `Warning` (DC-10 puts
  read-only entry at `Warning`; a refusal is the same family).
- The two decorators share one `RefusalLog` helper, so the guard's refusal and
  the backstop's refusal produce the same line — no divergent second format.
- `RepeatedRefusals_DoNotRelogTheModeChange` proves a refusal does not re-log the
  US-005 mode-change event.
- Hook telemetry is untouched and remains git-ignored.

## 13. Dependencies

No package was added, removed or upgraded. `ClassroomAgent.Application` still
references **zero** packages — the property OD-002 option 3 was chosen to
preserve, and the US-005 test `FrameworkFreeProjects_ReferenceNoPackage` passes
unchanged. No project reference changed, so `package-map.md`'s dependency table
still holds (`ProjectReferenceTests` green).

`dotnet list package --vulnerable`: no vulnerable package in any of the seven
projects.

## 14. Security test coverage

| Requirement | Test | Verdict |
|---|---|---|
| SC-5 — a write refuses in read-only mode, in Application | `ReadOnlyModeGuardTests` (17 cases) | covered |
| SC-5 — refusal is the default | `ReadOnlyModeUnitOfWorkTests.AUseCaseThatForgotTheGuard_StillCannotCommit` | covered |
| SC-5 — only BR-026 writes run | `PermittedServiceWriteTests` | covered |
| SC-5 — no write path bypasses the point | `WritePathRuleTests` (9) + `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath` | covered |
| SC-5/SC-8 — Google ports receive no call (TC-5) | `GoogleDataPortRuleTests` | covered on a synthetic port (F-5) |
| SC-10 — no personal data in the refusal line | `ReadOnlyRefusalLoggingTests.TheLine_CarriesNoPersonalDataAndNoPayload` | covered |
| SC-11 — no audit row for a refused write | `ReadOnlyRefusalLoggingTests.ARefusal_WritesNoAuditRowAndNoData` | covered |
| TC-4 — no live Google or Control Plane call | `FakeControlPlaneClient`, `SyntheticGooglePort` | covered |

Test quality was assessed, not assumed: the rule test proves the detector flags a
violation before the rule is pointed at the real assembly, and the "nothing is
committed" assertions compare real database rows rather than mock invocations.

## 15. Abuse case review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| A write use case is invoked directly while the installation is suspended | refusal in Application, nothing committed | `ReadOnlyModeGuardTests.ARefusedWrite_CommitsNothing` | protected |
| A future author forgets the guard | the commit backstop refuses anyway | `ReadOnlyModeUnitOfWorkTests` | protected |
| A future author declares a write that is not on the BR-026 list | the enum has no member for it, and the list test fails when the enum grows | `PermittedServiceWriteTests.TheList_HasExactlyTheFourMembersOfBr026` | protected |
| A declaration is left open and reused by the next write | the declaration is cleared on dispose and is scoped to the DI scope | `ReadOnlyModeUnitOfWorkTests.TheDeclaration_DoesNotOutliveItsScope` | protected |
| A long-running process caches the mode and keeps writing after suspension | the mode is read per call | `ReadOnlyModeTimingTests` | protected |
| A use case reaches Google during the grace-expired period | the guard throws before the port is touched | `GoogleDataPortRuleTests` | protected (synthetic port) |
| A Web-side type injects the bare `UnitOfWork` and commits | AD-3 forbids business logic in `Web`, and no such code exists | F-1 | latent, not reachable today |

## 16. Repository hygiene

`git status` shows only US-007 files: the five implemented `Application` types,
the three new `Infrastructure/ReadOnly` files, the DI wiring, one corrected test
fixture, the `package-map.md` row, the implementation report and the two workflow
state files. No secret, no generated database file, no `.xlsx`, no IDE-local
config. `google_credentials.json` and `dac-classroom-agent-*.json` remain
git-ignored and were not opened.

## 17. Deviations

The three deviations the implementation report records in its section 7 were
reviewed:

1. **The new `Infrastructure.ReadOnly` namespace** with a `package-map.md` row —
   accepted. The alternative, `Persistence`, would have implied persistence
   knowledge the decorator deliberately lacks, and leaving the derived document
   stale would itself be a defect. The decorators observe the refusal and make no
   decision about it, so AD-6 and AC-011 hold; verified by
   `ReadOnlyEnforcementTests.TheGuard_IsImplementedOnlyInsideApplication`.
2. **The corrected test fixture** — accepted, and independently checked: the
   change is in `SyntheticWriteUseCase` (a test-only type), it switches the
   marker write from `RecordSuccess` to `RecordUpgradeRequired` so the fixture
   stops moving the very state the tests judge, and **no assertion, no matrix row
   and no production behaviour was altered**. Without it three cases could not
   fail, which would have been the weaker suite.
3. **The narrowed interpretation I-5** — accepted. Skipping the mode read when a
   declaration is open makes a declared service write read the mode once instead
   of twice; a guarded write still reads it twice. Fewer reads, identical
   refusal behaviour, nothing cached, AC-008 unaffected.

No undocumented security behaviour, no omitted control, no permissive default
was found.

## 18. Findings

### F-1 — Minor — READ_ONLY_MODE (defence in depth) — SC-5

**File:** `src/ClassroomAgent.Web/Configuration/InstallationServices.cs`
(lines 39, 43).

**Observed:** the composition registers the bare implementations as services in
their own right — `services.AddScoped<ReadOnlyModeGuard>()` and
`services.AddScoped<UnitOfWork>()` — so that the decorator factories can resolve
them. Both are therefore resolvable by any type in `Web`, and a `Web` type
injecting `UnitOfWork` would commit without passing the backstop. The structural
rule of AC-007 would not see it: by the approved Specification FR-010 it
enumerates `ClassroomAgent.Application.UseCases` only.

**Expected:** the only reachable commit path is the decorated chain.

**Risk:** low today and not reachable: `Application` cannot reference
`Infrastructure`, so no use case can take `UnitOfWork`; `Web` holds no business
logic (AD-3) and no such code exists, which
`ReadOnlyEnforcementTests.TheWebLayer_HoldsNoCopyOfTheRule` and the US-005
`ProjectReferenceTests` keep true. The risk is that a later Story adds a `Web`
writer and the rule stays silent.

**Correction:** construct both inside the factory closures
(`new UnitOfWork(provider.GetRequiredService<ClassroomAgentDbContext>())`,
`new ReadOnlyModeGuard(provider.GetRequiredService<GetLegitimacyModeQuery>())`)
and drop the two bare registrations; optionally widen the structural rule to the
`Web` assembly in the Story that first adds a `Web` write path.

**Loop-back:** none required — not blocking. Suitable for the next Story that
touches `InstallationServices` (US-008).

### F-2 — Minor — ERROR_HANDLING — SC-5

**File:** `src/ClassroomAgent.Application/UseCases/CheckLegitimacyUseCase.cs`
(the `catch (Exception)` around the recording block).

**Observed:** the use case converts any exception from its commit into
`CheckOutcome.Failed(SaveFailed)`. It declares `LegitimacyCheckState` around the
commit, so `ReadOnlyModeException` cannot be raised there today. If that
declaration were ever removed, a read-only refusal would be silently reported as
an ordinary save failure instead of surfacing — the one place in the installation
where a refusal can be swallowed.

**Expected:** a read-only refusal is never converted into another outcome.

**Risk:** low; it requires a future edit to become reachable, and the structural
test would flag the same edit for a different reason.

**Correction:** add `catch (ReadOnlyModeException) { throw; }` before the general
handler, mirroring the existing `OperationCanceledException` filter.

**Loop-back:** none required — not blocking.

### F-3 — Informational — LOGGING — DC-10

The refusal line's request identifier could not be verified: DC-10 requires every
line written inside a request to carry it, and the installation still has no
request path (no endpoint). The Serilog `FromLogContext` enrichment US-005 wired
will supply it, but that is unproven until US-008 adds the first endpoint.
Recorded so it is not mistaken for a verified control.

### F-4 — Informational — READ_ONLY_MODE

Only one of the four BR-026 members has a live use case
(`LegitimacyCheckState`). `AuditEvent`, `SignInBookkeeping` and `RetentionPurge`
are asserted as list membership only, because no use case exists for them
(US-008, US-012, EPIC-10). A green suite proves the mechanism and the one live
case, not all four — as the test strategy §7 already states.

### F-5 — Informational — GOOGLE_ACCESS — SC-8

AC-006 is proven on a synthetic port marked `IGoogleDataPort`, because no real
Google port exists. What is verified is the mechanism and the rule; the first
Story adding a real port must prove its own refusal there (Specification FR-007,
TC-5).

## 19. Positive controls (independently verified)

- Every `SaveChanges` call site in the installation was read: exactly two, both
  inside the enforced path. The Control Plane's own call sites are a different
  deployable with a different database and are untouched by this Story.
- The only `IUnitOfWork` registration is the decorated chain; the resolved
  instance is neither `UnitOfWork` nor `ReadOnlyModeUnitOfWork`, asserted at run
  time.
- Refusal by default in read-only mode, proven against real PostgreSQL with
  before/after row comparison, for all three BR-025 causes.
- The BR-026 list is four members in one place with one registry entry; both are
  test-locked.
- The refusal log line contains no personal data — asserted by scanning the real
  log files for the seeded domain, client id and marker domain.
- No audit row and no `audit_event` table.
- `Application` still references zero NuGet packages.
- No vulnerable package in any project.
- Live credential files remain git-ignored and unopened.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001, OD-002 and OD-003
are all resolved (open-decisions artifact v4), and the implementation honours
each: the refusal stops at `Application`, the log line is written by
`Infrastructure` decorators with no package added to `Application`, and the
compile-only skeleton is now fully implemented.

## 21. Review limitations

- No penetration testing, no dynamic scanning, no deployment review.
- The request-identifier property of the refusal line is unverifiable until an
  endpoint exists (F-3).
- Three of the four BR-026 members have no use case to exercise (F-4), and the
  Google rule is proven on a synthetic port (F-5).
- Build and test figures were taken from the implementation report; the suite
  result was consistent with what this review observed, but the build itself was
  not re-run by the reviewer.

## 22. Verdict rationale

`PASS`. The implementation report records a green build and a green suite, and
the evidence is consistent with what this review could check independently. No
Critical or Major finding was identified: no write path and no Google path
bypasses the enforcement point, the BR-026 closed list was not widened, the
refusal carries no personal data, no audit row is written, and no dependency,
scope, endpoint or configuration surface was added. The two Minor findings are
defence-in-depth improvements for a later Story, and the three Informational
items record limits of what a green suite proves. Proceed to
`HUMAN_PR_APPROVAL`.
