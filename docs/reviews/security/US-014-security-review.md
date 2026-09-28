---
artifact_type: security_review
story: US-014
version: 1
status: APPROVED
created_at: 2026-09-28T10:20:00Z
updated_at: 2026-09-28T10:20:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-014-sync-courses-and-rosters.md
    version: null
  - path: docs/specifications/US-014-spec.md
    version: 2
  - path: docs/evidence/US-014-implementation-report.md
    version: 1
  - path: docs/designs/api/US-014-api-design.md
    version: 2
  - path: docs/designs/database/US-014-db-design.md
    version: 1
  - path: docs/designs/database/US-014-entity-model.md
    version: 2
  - path: docs/tests/US-014-test-strategy.md
    version: 1
  - path: docs/tests/US-014-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-014-open-decisions.md
    version: 2
  - path: trebovaniya.md
    version: 79
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 5
security_sensitive: true
runtime_checks: PARTIAL
---

# US-014 Security Review — Sync courses and rosters

## 1. Executive Summary

**Result: PASS.** No Critical and no Major finding. One Minor finding (an unbounded
Google-supplied string written into a log line) and five Informational observations.

This is the Story that makes the installation's database hold **personal data of
students, potentially minors**, so the review was held to the production standard of
`security-conventions.md`. Three properties carried the most weight and all three are
verified by machine, not by claim:

1. **Nothing reaches Google and nothing is written in read-only mode.**
   `RunSynchronizationUseCase` now takes a port marked `IGoogleDataPort`, which makes it
   a *protected path* under the US-007 rule, and
   `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath` applies
   that rule by reflection over the **real** `ClassroomAgent.Application` assembly —
   7/7 green. `PermittedServiceWrites` did not grow (no diff), so the run is protected
   rather than permitted. The Application-layer refusal tests each carry a **control
   case** proving the port is reachable when the installation is not read-only, so the
   zeros are refusals and not an absent pipeline.
2. **The imported personal data has no read path at all.** No endpoint, no Razor page,
   no policy and no export was added; the change set contains no controller, page or
   `.cshtml` file, and the endpoint/anonymous-access tests are green. The data is
   therefore unreachable from a web request in this Story (FR-018, S-08).
3. **No personal data reaches a log.** Proved at **host** level, where the lines are
   actually written: after a run that imported a course and two people,
   `CourseImportLoggingTests.TheLogOfAnImport_CarriesNoCourseNameAndNoPersonalData`
   asserts the absence of the course name, the description, both people's names, an
   address and a Google user id from every log file — with a control asserting the two
   membership rows really exist, so the absences are not the absence of a run. This is
   also runtime evidence that EF Core's command logging does not leak parameters
   (`EnableSensitiveDataLogging` appears nowhere in `src`).

Principal risks accepted by approved artifacts, not by this review: retry, backoff and
the Admin-facing diagnosis are US-017 (OD-008); a Classroom failure propagates and the
run is recorded failed with a category and a type name only; nothing is re-used between
runs (OD-007).

**Recommended next action:** proceed to `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-014-sync-courses-and-rosters.md` | — |
| Specification (APPROVED) | `docs/specifications/US-014-spec.md` | 2 |
| Implementation report (PASS) | `docs/evidence/US-014-implementation-report.md` | 1 |
| API design (`NOT_APPLICABLE`) | `docs/designs/api/US-014-api-design.md` | 2 |
| Database design | `docs/designs/database/US-014-db-design.md` | 1 |
| Entity model | `docs/designs/database/US-014-entity-model.md` | 2 |
| Test strategy / AC matrix | `docs/tests/US-014-test-strategy.md`, `…-ac-test-matrix.md` | 1, 1 |
| Open Decisions (OD-001 … OD-012, all resolved) | `docs/decisions/US-014-open-decisions.md` | 2 |
| Requirements | `trebovaniya.md` | 79 |

No input is `SUPERSEDED`; `HUMAN_SPEC_APPROVAL` is recorded (2026-09-27T17:20:48Z).
There is deliberately no `openapi` artifact — its absence is the recorded API_DESIGN
decision, not a missing input. The four design artifacts record `open_decisions` at
version 1 while the file is at version 2; version 2 adds **OD-012 only** (the
compile-only skeleton), which changes no security requirement, so the chain is not
materially stale.

## 3. Security-Relevant Scope

**Exposed functionality: none added, on either host.** The whole Story runs inside the
existing `BackgroundService` of US-013.

Protected assets this Story touches:

- **personal data of students and staff** — `classroom_participant.email` and
  `full_name`, and `course_membership.role` + dates, which reveal who teaches or studies
  where (new, and the first of their kind in this database);
- school data — course names, sections, descriptions, rooms;
- the **service-account key**, resolved from `ISecretStore` per Classroom call;
- the `WorkspaceConnection` technical account, used as the impersonated subject.

Trust boundaries crossed: Application → the new Google port (`IClassroomReader`),
Application → PostgreSQL, and nothing else. No browser boundary (no surface), no Control
Plane boundary (`Contracts` untouched, `ContractVersion` unchanged).

Affected security components: the read-only enforcement point (unchanged, newly
*covered* by the marker rule), the Google credential path, the installation schema, and
the run's log lines.

## 4. Environment and Tools

| | |
|---|---|
| .NET SDK | 10.0.401 |
| Docker / Testcontainers | available (server 29.8.0) — integration and schema tests really ran |
| Commands run | `dotnet build ClassroomAgent.sln`; `dotnet test ClassroomAgent.sln`; targeted `dotnet test --filter` runs for the rule, schema, adapter and logging classes; `dotnet list package --vulnerable`; `dotnet format --verify-no-changes`; `git status --porcelain`; ripgrep over `src` and `tests` |
| Not performed | penetration testing; any call to a live Google API or a live Control Plane (forbidden, TC-4); any connection to a database other than the Testcontainers instances |

No secret value was opened, printed or copied. `google_credentials.json` and
`dac-classroom-agent-*.json` were confirmed git-ignored **by reading `.gitignore` only**.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | **PASS** | `ClassroomRole` is `Teacher, Student` only and is never read by authorization code (grep: its only consumers are the entity, the configuration and the use case). `AppRole` untouched; no permission cell added — no surface exists to permit. |
| SC-2 Authentication | **NOT_APPLICABLE** | No sign-in path, password, cookie or Identity option is touched; no file under `Security/` changed. |
| SC-3 AllowedAdmin | **NOT_APPLICABLE** | No Admin login path touched. |
| SC-4 Authorization | **PASS** | No endpoint, page, policy or route added (change set has no controller/page/`.cshtml`); the anonymous list is unchanged and the endpoint/anonymous tests are green (7/7 in the targeted run, and in the full suite). |
| SC-5 Read-only mode | **PASS** | `IReadOnlyModeGuard.EnsureAllowedAsync` is the first statement of `ExecuteAsync` (`RunSynchronizationUseCase.cs:41`); `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath` proves the rule over the real assembly; `PermittedServiceWrites` unchanged; `CourseImportRefusalTests` (6 facts) asserts zero port calls, zero writes, zero commits and zero transactions, each against a control that does write. |
| SC-6 No DB UI | **PASS** | No diagnostic or database endpoint added; `EnsureCreated`, `EnsureDeleted` and `ExecuteSqlRaw` appear nowhere in `src`. |
| SC-7 Key | **PASS** | `GoogleClassroomReader.LoadKey()` resolves the key from `ISecretStore` by its configuration **reference** for every call and holds it only for that call; no key, and no reference, is written to any of the three new tables (migration reviewed column by column); nothing about a defective key is echoed — the `catch` returns `null` without touching the content; the adapter has no logger at all. |
| SC-8 Google | **PASS** | Exactly three scopes, all read-only, all already on the `GoogleDelegationScopes` list (`CoursesReadonly`, `RostersReadonly`, `ProfileEmails`); `User = impersonationUser` is the `WorkspaceConnection` technical account the use case read; only `List()` calls exist — no create, patch or delete anywhere in the adapter; `GoogleClassroomReaderTests.TheTokenRequest_ImpersonatesTheTechnicalAccount` decodes the JWT and asserts the `sub` claim. |
| SC-9 Channel | **PASS (unchanged)** | The import runs only when `WorkspaceConnectionView.IsUsable`, which US-009 makes true **only** when the saved domain equals the domain of the last successful legitimacy check — so a connection for another domain cannot be read from Google at all (`RunSynchronizationUseCase.cs:54`). Nothing else about the channel is touched; `Contracts` and the Control Plane are untouched. |
| SC-10 Hygiene | **FINDING (Minor, F-1)** | No personal data in any line — proved at host level (§12). Rejected values are not logged: truncation logs nothing, and a refused course id or name surfaces only as `RunFailed:<type>`. F-1 concerns the **length** of the two Google-supplied strings OD-010 requires in the Warning line, not their kind. |
| SC-11 Audit | **PASS** | No audit member, no vocabulary change, no constraint-amending migration; `SyncLoggingTests.AScheduledRun_WritesNoAuditRow` is green, and `CourseMembershipSchemaTests` / `AppUserMigrationTests` confirm `audit_event` is untouched. |
| SC-12 Owner | **PASS** | `ClassroomAgent.Contracts` unchanged; `ControlPlane` untouched and still holds no reference to `Domain`; no school statistic leaves the installation. |
| SC-13 Outbound | **PASS** | The adapter's only destination is Google — `GoogleClassroomReaderTests.EveryRequest_GoesToGoogle` asserts every recorded request host ends in `googleapis.com`; the new outcome fields (`SkippedCourses`, `MembershipsMarkedOffRoster`) never leave the host process (their only consumers are the use case and the background service). |

## 6. Authentication and Authorization

Nothing to enforce and nothing enforced: the Story adds no authenticated or anonymous
surface. The only actor is the host's own background service, which reaches the use case
through a DI scope, not through HTTP.

Checked for the bypass that would matter: the three new repositories are injected
**only** into `RunSynchronizationUseCase` (grep over `src` — their other references are
their own interfaces, implementations and the DI registration), so there is no second,
unguarded path that can write a course, a person or a membership.

The Classroom role is not a permission: `ClassroomRole` is read by the entity, its EF
configuration and the use case, and by no authorization code (SC-1, §3 of
`trebovaniya.md`).

## 7. Credentials, Key and Google Access

- **Key handling.** Resolved per call from `ISecretStore` through
  `GoogleServiceAccountSettings.KeyReference`; never stored in an entity, never accepted
  through a UI (no UI exists here), never logged. A key that cannot be parsed yields
  `null` and then an `InvalidOperationException` whose message names no content — and
  the run records only `RunFailed:InvalidOperationException` (SC-10). **Note:** an
  unavailable key therefore fails the run rather than producing a diagnosis for the
  Admin; that is exactly the split OD-008 defers to US-017, and it errs on the safe side
  (no partial import, nothing leaked).
- **Impersonation.** The address comes from the saved `WorkspaceConnection` via
  `WorkspaceConnectionView.SavedImpersonationUserEmail`, and the run refuses to proceed
  when it is absent. The prototype's impersonation of a super-admin has no counterpart
  here.
- **Scopes.** Three, all read-only, all pre-existing. No seventh scope was needed and
  none was added.
- **Retry.** The client library's own back-off is `ExponentialBackOffPolicy.None` on both
  the credential and the service, so nothing silently repeats a permission failure
  (SC-8's "neither retried nor swallowed" — it propagates, and US-017 will classify it).
  Unlike US-011's probe, this adapter's transport deliberately does **not** convert a 5xx
  or 429 into a transport failure: that conversion is US-011's classification, and the
  absence of it here means nothing is swallowed.

## 8. Sensitive Data Exposure

| Channel | Result |
|---|---|
| API responses / Razor views | none exist for this data (FR-018, S-08) |
| DTOs | none added; no domain entity crosses a presentation boundary (AD-8 not exercised) |
| Logs | no name, address, course name or Google user id — asserted at host level (§12) |
| Audit rows | none written |
| Exceptions | diagnosis is `RunFailed:<exception type>`; no Google error text, no payload, no stack trace (`CourseImportTests.AFailedImport_StoresADiagnosisWithNoPayload` seeds an address into the exception message and asserts it is absent from the stored diagnosis) |
| Exports | none (Epic 5) |
| Telemetry | `docs/hooks/tool-usage.jsonl` untouched by this Story and git-ignored |
| Test fixtures | synthetic only — every person, course, address and domain is invented (`school-one.example.test`, `other.example`, "Test Person N"); grep for `dac.ukr.education`, `admin@dac` and `@gmail` in the new fixtures returns nothing (TC-4) |

Storage form: `email` and `full_name` are stored in plain text, which is what db-design
§7 and §4.4 require — the product exists to show a person by name to the Dean, and
neither value is a credential. No column is hashed and none needs to be.

## 9. Input Validation

Every value Classroom returns is treated as external input before it reaches the domain
(VR-001, S-09):

- **required identifiers** — `Course.Import` and `ClassroomParticipant.Import` refuse a
  blank Google id / `userId` (`ArgumentException`, message names the parameter and never
  the value); `CourseInvariantTests` proves both;
- **bounds** — every string is cut to the db-design bound in the entity, before the
  write, so an overlong value can never reach the column and fail a commit;
  `CourseInvariantTests.AnOverlongDescription_IsCutNotRefused` and
  `…AnOverlongName_IsCutOnImportAndOnUpdate` prove it, and the schema tests prove the
  columns' own limits;
- **closed vocabularies** — an unrecognised `courseState` is skipped *before* it can
  become an entity (OD-010); the database's `ck_course_course_state` is the second layer,
  and `CourseSchemaTests.AStateOutsideTheVocabulary_IsRejected` proves it still bites;
- **normalisation** — an address is trimmed and lower-cased through the one
  normalisation the system has (`WorkspaceConnection.NormalizeEmail`), and a blank one
  becomes `null`, so "no address" has a single representation;
- **no format validation** of the address, by design: OD-006 imports an entry whatever
  its domain, and VR-003 fixes no format rule (see I-3 for what EPIC-2 inherits).

No HTTP input exists, so `[ApiController]`, `ModelState` and the API-6 body are not
exercised (VR-007). All database access is EF Core LINQ — no raw SQL, no string
concatenation, so the imported values are parameters and never statement text.

## 10. API Security

Nothing exposed, nothing to review: no endpoint, no route, no method, no request or
response body, no content type. The Story's own Specification (FR-018), the API design
(`NOT_APPLICABLE`) and the change set agree, and the endpoint/anonymous-access tests are
green.

## 11. Persistence and Configuration

- **Schema by migration only.** One migration, `AddCoursesAndRosters`, reviewed column
  by column against db-design §3 … §5; installation migrations 6 → 7 and the table set
  6 → 9, asserted by `AppUserMigrationTests`, whose
  `TheControlPlaneSchema_IsUnchangedByThisStory` is also green. No `EnsureCreated`,
  `EnsureDeleted` or raw schema change exists anywhere in `src`.
- **Constraints are explicit and enforced by the database**, not by code alone: unique
  `google_id`, unique `google_user_id`, unique `(course_id, participant_id)`,
  `role IN ('teacher','student')`, the five-value course state,
  `last_seen_at >= first_seen_at`, and `Restrict` on both foreign keys — 32 schema tests
  against real PostgreSQL, green.
- **The deliberate non-uniqueness of the address** (OD-011) is positively asserted:
  `TwoParticipantsSharingAnEmailAddress_AreBothStored` fails the moment someone adds
  `IsUnique()`, which is the guard that keeps a reused school address from failing a
  whole school's import.
- **No secret, token, key or key reference in any of the three tables** (PC-9, SC-7).
- **Separation** — one installation database; the Control Plane's is untouched (AD-1).
- **Configuration: nothing changed.** No `appsettings` key, no environment variable, no
  DC-3 entry; the Classroom page size is a constant in the adapter (I-10), so no school
  can be made to page differently by configuration.

## 12. Logging, Audit and Telemetry

Host-level evidence, not inspection of format strings:

- `TheLogOfAnImport_CarriesNoCourseNameAndNoPersonalData` — after a real run that
  imported one course, two people and two memberships, no log file contains the course
  name, the description, either person's name, the seeded address or the Google user id.
  The control (two membership rows in the database) rules out a vacuous pass.
- `TheFinishLine_CarriesTheCourseCounterAndTheOffRosterCount` — the finish line carries
  `ProcessedCount` and `MarkedOffRoster` and nothing else.
- `ASkippedCourse_IsLoggedAtWarningWithTheStateAndTheGoogleId` — the skip is `Warning`
  (not `Information`, so it cannot sink into a run's ordinary lines), carries the state
  string and the course's **Google id**, and the course **name** is absent from every
  file.
- `ADepartureFromARoster_IsCountedAndTheMembershipSurvives` — the departure is counted
  in the line and the membership survives with its last sighting unmoved.

Audit: no row, no member, no vocabulary growth — as FR-019 requires, since
`trebovaniya.md` §5 audits the *manual* start (US-019).

Telemetry: `docs/hooks/tool-usage.jsonl` is untouched by this Story and remains
git-ignored.

## 13. Dependencies

No package was added and no `.csproj` changed (`git diff --stat -- '*.csproj'` is
empty). No project reference changed: `ClassroomAgent.Application` still references only
`ClassroomAgent.Domain`, and ripgrep finds **no** `Google.Apis` type in `Application`,
`Domain` or `Web` — the SDK stays inside `Infrastructure` (AD-4, package-map).

`dotnet list package --vulnerable` reports no vulnerable package in any of the seven
projects, against nuget.org as of this review. That is a scan result for the current
advisory database, not a guarantee.

## 14. Security Test Coverage

| Requirement | Test | Result |
|---|---|---|
| S-01 read-only refusal in Application, no Google call | `CourseImportRefusalTests` (6 facts, each with a control), `ReadOnlyEnforcementTests.TheApplicationAssembly_HasNoUnprotectedWritePath` | PASS |
| S-02 key from the store, never logged or stored | `GoogleClassroomReaderTests` (synthetic key generated at run time), migration review | PASS |
| S-03 read as the technical account | `GoogleClassroomReaderTests.TheTokenRequest_ImpersonatesTheTechnicalAccount`; `CourseImportRefusalTests.TheGuard_IsConsultedBeforeTheClassroomPort` asserts the impersonated address of an allowed run | PASS |
| S-04 read-only scopes only | code review of the scope list; no write call exists in the adapter | PASS |
| S-05 / S-13 nothing leaves but Google | `GoogleClassroomReaderTests.EveryRequest_GoesToGoogle` | PASS |
| S-06 no personal data in a log | `CourseImportLoggingTests` (4 facts, with controls) | PASS |
| S-07 diagnosis carries no payload | `CourseImportTests.AFailedImport_StoresADiagnosisWithNoPayload` | PASS |
| S-08 no read path for the stored personal data | change set contains no controller, page or export; endpoint/anonymous tests green | PASS |
| S-09 Google data validated before business logic | `CourseInvariantTests` (11 facts) + 32 schema tests | PASS |
| S-10 no audit row | `SyncLoggingTests.AScheduledRun_WritesNoAuditRow` | PASS |
| TC-4 no live Google, synthetic fixtures | `FakeClassroomReader` registered for **every** host test in `InstallationFactory`; `ScriptedHttpHandler` for the adapter; fixtures grepped for real identifiers | PASS |
| TC-2 real PostgreSQL, no InMemory provider | Testcontainers; 32 schema tests plus the host tests | PASS |

Quality note: the refusal tests are the strongest part of this suite, because each pairs
"the port was not called" with a control run that *does* call it — the vacuity
TEST_WRITING found and fixed in its own stage. The two tests IMPLEMENTATION added follow
the same pattern.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| The installation is read-only (suspended, grace expired, never checked) and a run is due | No Classroom call — not even the token request — and no row written | `CourseImportRefusalTests` (port call count 0, commits 0, transactions 0, `sync_state` absent), with controls | Protected |
| A connection saved for a domain the Owner never approved | No Google read at all | The run proceeds only when `IsUsable`, which requires the saved domain to equal the confirmed `Installation` domain (US-009); `WithNoSavedConnection_…` proves the refusal shape | Protected |
| Google returns a course state nobody has seen before | That one course is skipped, the run completes, one `Warning` line, no invented sixth state | `CourseImportTests.ACourseWithAnUnrecognisedState_…`, `ASkippedCourse_IsNotCounted`, `ASkippedCourse_IsLoggedAtWarning…`, plus `ck_course_course_state` as the second layer | Protected |
| Google returns the same person on both rosters of one course | One membership, role `teacher`, no unique-index violation and no failed import | `RosterImportTests.APersonOnBothRostersOfOneCourse_IsOneTeacherMembership`; the unique index is the guard behind it | Protected |
| A school address is deleted in Google and later reused by a new account | The import must not fail for the whole school | The email index is not unique; `TwoParticipantsSharingAnEmailAddress_AreBothStored` | Protected |
| An oversized value (a 30 000+ character description, a 750+ character name) | Cut, not refused; no commit failure | `CourseInvariantTests` | Protected |
| A roster read fails half-way through a school | No membership of that course marked off the roster; committed courses kept whole | `AFailedRosterRead_MarksNoMembershipOffTheRoster`, `ARunThatFailsPartWay_KeepsTheCoursesAlreadyCommitted` (2 transactions) | Protected |
| Someone later tries to read this personal data | No path exists until EPIC-2 designs one with its policies | No surface added | Protected |
| A future migration makes the address unique, or gives `MeetParticipation` a foreign key to a participant | Prohibited by OD-011 / PC-12 | The positive test above; PC-12 recorded, table does not exist yet | Guarded by test / recorded |

Rate limiting and denial-of-service protections are not invented as requirements: the
only caller is the host's own scheduler.

## 16. Repository Hygiene

`git status --porcelain` shows 26 modified and 10 untracked files, all of them source,
test, migration or artifact files named in the implementation report. Specifically:

- **no** secret-like file, `.env`, key or token;
- **no** generated database file and **no** `.xlsx`;
- `.gitignore` still covers `google_credentials.json`, `dac-classroom-agent-*.json`,
  `classroom_cache.db` and `*.xlsx` (with the documented exception for
  `docs/product/report-templates/*.xlsx`);
- no connection string with a password in any changed file — the test hosts build theirs
  from the Testcontainers instance at run time;
- the Python prototype is untouched.

## 17. Deviations

The four deviations the implementation report declares (§7 D-1 … D-6) were checked
independently; none is a security defect:

| Deviation | Security assessment |
|---|---|
| **D-1** `SynchronizationRunOutcome` gained `SkippedCourses` and `MembershipsMarkedOffRoster` | Accepted. The Application layer has no logger and gaining one would add a package (FR-020 forbids it). The two values are a Google course id, a state code and a count — no personal data — and the type never leaves the host process (grep: two consumers). This is the correct shape: the layer that knows the fact reports it; the layer allowed to log writes it. |
| **D-2** `TimestampInterceptor` gained the three entities | Correct per PC-6. The interceptor stamps `created_at` on insert and `updated_at` on modify and touches nothing else; `AuditEvent` remains on the list, so SC-11's immutability is unaffected. The recorded gap for `SyncState` / `WorkspaceConnection` is I-4 below. |
| **D-3** the adapter takes no `ILogger` | **Security-positive.** It removes the only place a Google error text could have been written, which is precisely what SC-10 forbids. |
| **D-4** `FakeClassroomReader` registered for every host test | **Security-positive and necessary.** Before this Story the host registered the real adapter and nothing called it; with the step implemented a host test would have attempted a live Google call, which TC-4 forbids outright. The production registration is unchanged. |
| **D-5** one test expectation corrected on the Owner's decision | Not security-relevant. The assertion keeps its strength (one person, two memberships, one of each role); the enum's declared order is the design's (entity model §5). |
| **D-6** a failed run still stores `processed_count = 0` | Not security-relevant; it understates work done and never overstates it. US-013 owns that line. |

No undocumented security behaviour, no omitted control, no permissive default and no
false claim in the implementation report was found. Every claim spot-checked (the
`PermittedServiceWrites` non-change, the absence of Google SDK types outside
`Infrastructure`, the absence of a new package, the migration's contents, the test
counts) held.

## 18. Findings

### F-1 — Minor — LOGGING (SC-10)

**File:** `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs`
(`LogCourseSkipped`), fed by `src/ClassroomAgent.Application/Models/SkippedCourse.cs`.

**Observed:** the `Warning` line for a skipped course writes two strings that came
straight from Google and were **not bounded first** — `snapshot.State` and
`snapshot.GoogleId`. The skip happens before `Course.Import`, which is where truncation
lives, so a pathological Classroom response (a very long `courseState`, or a course id
far beyond the 64-character column) would be written to the log file at its full length,
once per such course per run.

**Expected:** SC-10 requires a log line to carry controlled content. OD-010 explicitly
requires *these two values* in the line, so their **kind** is correct; only their
**length** is unconstrained.

**Risk:** low and availability-only. The values are not attacker-controlled — they come
from Google's own API, not from a user request — and no personal data is involved
(`courseState` is a code and the course id is an identifier SC-10 permits). The worst
realistic outcome is log-file growth on a malformed Google response; DC-10's rolling file
bounds it further.

**Required correction:** none before `HUMAN_PR_APPROVAL`. Recommended as defense in
depth, in this Story or in US-017 when it takes over error policy: bound both strings
before logging (for example to `Course.MaxGoogleIdLength` and a short state bound),
reusing the truncation the entity already performs.

**Loop-back target:** none (Minor). **Verification after correction:** extend
`ASkippedCourse_IsLoggedAtWarningWithTheStateAndTheGoogleId` with an overlong state
string and assert the logged property's length.

### Informational observations

- **I-1 — TEST_COVERAGE.** `TheLogOfAnImport_CarriesNoCourseNameAndNoPersonalData` seeds
  two people but asserts the absence of only one of the two addresses (`person1`). Both
  travel the same code path, so the property is proved; the second assertion would cost
  one line. No correction required.
- **I-2 — LOGGING.** `MarkedOffRoster` counts only memberships that were **on** the
  roster before this run, so a repeated absence is not re-counted. That is the intended
  reading of FR-016 and is recorded here because the requirement does not say so
  explicitly.
- **I-3 — DATA_EXPOSURE, for EPIC-2 and Epic 5.** An imported name or address is stored
  exactly as Google gave it (no format validation, by OD-006 / VR-003), and this Story
  gives it no read path. The Story that first *renders* or *exports* it owns the output
  side: Razor encodes by default, but a spreadsheet cell beginning `=`, `+`, `-` or `@`
  is interpreted as a formula by Excel, so US-024's export must neutralise it. Recorded
  now because that value enters the database here.
- **I-4 — PERSISTENCE.** `TimestampInterceptor` names the entities it stamps explicitly,
  and `SyncState` and `WorkspaceConnection` are **not** on that list, so their
  `created_at` / `updated_at` hold the CLR default `0001-01-01`. A pre-existing defect of
  US-009 / US-013 with no security impact (neither column is personal data and no audit
  row is affected), correctly left outside this Story's scope, recorded so it is not
  lost.
- **I-5 — INPUT_VALIDATION.** A course whose `name` is blank makes `Course.Import` throw,
  which fails the **whole run** rather than skipping that one course — unlike an
  unrecognised state, which OD-010 handles by skipping. Specification §8 calls such a
  course "unimportable" without saying which of the two it means. Consistent with FR-014
  (a failure propagates and US-013 records it) and harmless today, because Classroom
  always returns a name; US-017 owns the choice when it defines error policy.

### Carried from earlier Stories, not US-014's to correct

US-011 F-2 (no test resolves a real adapter from the composition root) still stands, and
now applies to `GoogleClassroomReader` as well. The Informational observations of US-012
(five, on the sign-in surface) and US-013 (five, incl. the deliberately coarse
`RunFailed:` diagnosis) are unchanged.

## 19. Positive Controls

Independently observed and verified, not taken from the implementation report:

1. **The read-only rule now covers Google reads by construction.** Because
   `IClassroomReader : IGoogleDataPort`, `WritePathRule.IsProtectedPath` returns true for
   `RunSynchronizationUseCase`, and the assembly-wide test demands it take
   `IReadOnlyModeGuard`. A future Story that removes the guard, or adds a second
   unguarded use case holding this port, fails that test — the rule is unbypassable
   rather than remembered.
2. **`PermittedServiceWrites` did not grow**, so the import is a protected write path and
   not an addition to BR-026's closed list.
3. **The Google SDK is confined to `Infrastructure`** — verified by search over the three
   other projects and by the marker test asserting the Application assembly references no
   `Google*` assembly.
4. **Every Classroom request goes to Google and nowhere else**, asserted over recorded
   request hosts.
5. **The database enforces the constraints the design promised**, including the two that
   protect against a defect rather than a path: the unique `(course, participant)` and
   the non-unique email index.
6. **No personal data in the log**, asserted at host level with a control that the import
   really happened.
7. **No new surface, no new package, no new project reference, no secret and no generated
   artifact** in the change set.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-001 … OD-012 are resolved; the
two that shape security behaviour are implemented as resolved — OD-010 skips an
unrecognised state before it becomes an entity, and OD-011 keeps the address index
non-unique with a positive test behind it.

The Owner's in-conversation decision recorded in the implementation report §7 D-5
corrected a test expectation; it changed no security requirement and needs no `OD-`.

## 21. Review Limitations

- **No penetration testing.** This is a code, configuration and test review.
- **No live Google call and no live Control Plane call** (TC-4 forbids them). Google's
  real response shapes are therefore only as accurate as the synthetic fixtures: a
  renamed field would pass here and fail in production. US-011's live "check access" is
  the mitigation, not a test — and this Story's import has **no** live check of its own,
  which is worth remembering the first time a real school is synchronized.
- **Paging is proved with two pages**, so a continuation token mishandled only after the
  second page would not be caught.
- **The both-rosters tie-break tests the program's rule, not Google's behaviour** —
  whether Classroom can return one person on both rosters was never verified on a live
  domain (spec I-9).
- **Vulnerability scanning reflects the current advisory database only**; it is not a
  statement that the dependencies are free of unknown vulnerabilities.
- **The retention purge does not exist yet** (US-037), so the *deletion* half of the
  personal-data lifecycle could not be reviewed — only that the columns the purge needs
  exist and that nothing else deletes a row.

## 22. Verdict Rationale

`verdict: PASS`.

The implementation report records a green build (0 errors, 0 warnings) and a green suite
(2337 passed, 0 failed, 0 skipped), and the targeted re-runs performed for this review
confirm the security-bearing classes independently. No Critical and no Major finding
exists: every SC item in scope is `PASS` except SC-10, whose finding is Minor, concerns
the length of a Google-supplied code rather than its kind, and is required content by
OD-010.

The single question worth a human's attention is not a defect but a consequence of what
this Story does: from the moment it is deployed, a school's installation holds the names
and addresses of its students, and nothing in the product can show them yet. The controls
that keep that data safe today are the absence of a read path and the read-only
enforcement point — both verified here — and the retention purge that will eventually
delete it does not exist (US-037). That is the risk balance to accept at
`HUMAN_PR_APPROVAL`.
