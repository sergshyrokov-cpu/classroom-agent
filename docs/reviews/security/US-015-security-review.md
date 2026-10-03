---
artifact_type: security_review
story: US-015
version: 1
status: APPROVED
created_at: 2026-10-03T07:07:31Z
updated_at: 2026-10-03T07:07:31Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-015-sync-coursework-and-submissions.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/decisions/US-015-open-decisions.md
    version: 2
  - path: docs/designs/api/US-015-api-design.md
    version: 2
  - path: docs/designs/database/US-015-db-design.md
    version: 1
  - path: docs/designs/database/US-015-entity-model.md
    version: 1
  - path: docs/tests/US-015-test-strategy.md
    version: 1
  - path: docs/tests/US-015-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-015-implementation-report.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 1
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-015 Security Review — Sync coursework and submissions

## 1. Executive Summary

**Result: PASS.** No Critical or Major finding. One Minor finding, three
Informational ones.

US-015 adds two Google reads (coursework/materials and submissions), two tables
holding students' grades and submission facts, two log lines, and one required
configuration key. It adds no endpoint, page, policy, route, audit action,
package or scope.

The principal controls hold, checked in the code and in tests:
- the read-only guard runs before any Google call;
- every Google call impersonates the technical account and requests only
  read-only scopes from §6;
- the imported data has no read path;
- no grade, name, email or title reaches a log line;
- every check SC-10 requires is met.

The Minor finding has the same shape as the open US-014 F-1. A value Google
sends is written to a log line without a length bound. The log is JSON, so
this affects only the log's size, not its integrity.

**Next:** `HUMAN_PR_APPROVAL`.

## 2. Reviewed Artifacts

All inputs are in the front matter. None of them is `SUPERSEDED`.
`HUMAN_SPEC_APPROVAL` for Specification v2 was recorded on 2026-09-28. The
implementation report records PASS with concrete evidence: 2428/2428 tests,
0 build warnings and a clean format check.

## 3. Security-Relevant Scope

- **Exposed functionality:** none new on either host. The change is
  background-only: `RunSynchronizationUseCase`, run by the US-013 service.
- **Assets:** students' grades (`assigned_grade`, `draft_grade`), submission
  state, turn-in date and `late` flag, and work titles. Together with
  US-014's names and addresses, these are personal data of potentially minor
  students. The service-account key is also an asset.
- **Trust boundaries crossed:**
  - Application → `IClassroomReader` port → Google (data returned by Google is
    external input);
  - Application → PostgreSQL;
  - host → log file.
- **Components:**
  - `RunSynchronizationUseCase`;
  - `GoogleClassroomReader`;
  - `CourseWork` and `Submission`, with their configurations and the migration;
  - `SynchronizationBackgroundService` (logging);
  - `InstallationSettingsReader` (`Retention:Years`).

## 4. Environment and Tools

| Item | Value |
|---|---|
| .NET SDK | 10.0.401 |
| Docker / Testcontainers | available |
| `dotnet test ClassroomAgent.sln` | 2428 total, 2428 passed, 0 failed, 0 skipped (run at IMPLEMENTATION on this working tree; no source changed since) |
| `dotnet build` | 0 errors, 0 warnings |
| `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive` | exit 0, no vulnerable package in any of the 7 projects |
| repository hygiene scan | `git status`, `git check-ignore`, `git ls-files`, pattern scan of the 26 changed `src/` + `tests/` files |

This was a code, configuration and test review. No penetration testing was
performed.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | no role, policy or seed touched; submitters exist only as synced data (`ClassroomParticipant`) |
| SC-2 Authentication | NOT_APPLICABLE | no sign-in path touched |
| SC-3 AllowedAdmin | NOT_APPLICABLE | not touched |
| SC-4 Authorization | PASS | no controller, page, DTO or view model names `CourseWork` or `Submission` (searched `Controllers`, `Pages`, `Models/Dtos`, `ControlPlane`, `Contracts`: none); the existing endpoint-enumeration test is unchanged and green |
| SC-5 Read-only mode | PASS | `RunSynchronizationUseCase.ExecuteAsync` calls `EnsureAllowedAsync` before reading the connection or any port; the new reads are inside `ImportCoursesAsync`, reachable only after it; `PermittedServiceWrites` unchanged (`CourseWorkRefusalTests.PermittedServiceWrites_DoesNotGrow`); `ReadOnlyMode_DoesNotReadCourseWork` / `_DoesNotReadSubmissions` / `_WritesNoCourseWorkOrSubmissionRow` each paired with a control |
| SC-6 No DB UI | PASS | nothing added |
| SC-7 Key | PASS | `GoogleClassroomReader.LoadKey` resolves the key from `ISecretStore` per call, unchanged from US-014; nothing about it is stored, logged or returned; no key, reference or secret in the change set |
| SC-8 Google | PASS | coursework calls request exactly `classroom.coursework.students.readonly` + `classroom.courseworkmaterials.readonly` (`TheCourseWorkToken_AsksOnlyForTheReadOnlyCourseWorkScopes`); courses/rosters keep their three; all on §6's list, no seventh; subject is the technical account; client retry off (`ExponentialBackOffPolicy.None`); failures propagate, not swallowed |
| SC-9 Channel | NOT_APPLICABLE | not touched |
| SC-10 Hygiene | PASS, with F-1 | no grade, name, email, person id or title in any log line (`SubmissionImportLoggingTests`, anchored by `TheImportReallyHappened`); a failure becomes `RunFailed:<type>` only; log sink is `CompactJsonFormatter` |
| SC-11 Audit | PASS | a scheduled run writes no audit row and the vocabulary does not grow (`AScheduledRun_WritesNoAuditRow`) |
| SC-12 Owner | PASS | `Contracts` and `ControlPlane` untouched; nothing from the new tables reaches the Control Plane |
| SC-13 Outbound | PASS | the adapter's only transport is the injected handler; every request goes to `*.googleapis.com` (asserted in the new scope test) |

## 6. Authentication and Authorization

No endpoint was added. The only path to the new data is the background service,
which has no caller identity. Its authority is the read-only guard plus the
installation's own legitimacy. No identifier from a request can select a course
or submission: there is no request.

## 7. Credentials, Key and Google Access

- No password is handled.
- Key handling is the US-014 code path, unchanged.
- Impersonation uses the address from `WorkspaceConnection`, passed to the port
  per call (BR-015).
- Scopes are narrowed per call, which is stricter than before.

## 8. Sensitive Data Exposure

- **Responses and views:** none new.
- **Logs:** two new lines.
  - `SyncSubmissionStateUnrecognised` (Warning) carries the run id, the
    submission's Google id and the raw state.
  - `SyncCourseSkippedByAge` (Information) carries the run id and the course's
    Google id.
  - A submission id identifies a record, not a person. Neither line carries a
    person's id, a name, a grade or a title. See F-1 for the missing bound.
- **Audit:** none written.
- **Exceptions:** a failing course produces only `RunFailed:<ExceptionTypeName>`
  in `SyncState`. The entity guards throw `ArgumentException` with a parameter
  name only.
- **Exports and telemetry:** none.
- **Test fixtures:** all synthetic, with the domain `school-one.example.test`.
  The one token string is `ya29.synthetic-access-token`.

## 9. Input Validation

Data from Google is validated before it is stored:

- **Refused** (the course's transaction fails and the run records it):
  - a blank Google id of an item or a submission;
  - a blank `userId`;
  - a blank title;
  - an item with no date from the cascade.
- **Truncated:**
  - ids to 64 characters;
  - titles to 3000;
  - the raw state to 64.
- **Closed vocabularies:** the state is enforced by a check constraint, plus
  OD-005's marker for an unrecognised value. The resource is limited to two
  values.
- **Grades:** stored as `numeric(10,4)`. A value outside that range fails the
  course rather than being stored corrupted.
- **Due date:** a date without a time is stored as absent, never guessed.

## 10. API Security

Not applicable: no API surface is added (API design v2 is NOT_APPLICABLE).

## 11. Persistence and Configuration

- **Schema:** one migration, `AddCourseWorkAndSubmissions`. Every column has an
  explicit length, nullability and check. Keys are scoped by parent per
  Specification v2. Foreign keys are `Restrict`, so nothing cascades deletes
  outside the future purge.
- **Forbidden calls:** no `EnsureCreated` or `EnsureDeleted`.
- **Database separation:** the Control Plane schema is unchanged and is still
  asserted unchanged by `AppUserMigrationTests`.
- **`Retention:Years`:** required, validated as an integer of 1 or more.
  Without a valid value the installation refuses to start. See I-1 for the
  missing upper bound.
- **Generated files:** no `appsettings*.json` exists to carry a secret, and no
  generated database file is in the change set.

## 12. Logging, Audit and Telemetry

- **Logging:** see §8 and F-1.
- **Audit:** no new audited action.
- **Hook telemetry:** `docs/hooks/tool-usage.jsonl` is still git-ignored
  (`.gitignore:63`).

## 13. Dependencies

- No package or project reference changed: no diff in any `*.csproj`,
  `Directory.*.props` or `global.json`.
- The vulnerability scan reported nothing for all 7 projects.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| S-02 read-only before any read or write | `CourseWorkRefusalTests` (4 with controls + the `PermittedServiceWrites` invariant) |
| S-03 / S-04 / S-11 impersonation, read-only scopes | `GoogleClassroomReaderTests.TheCourseWorkToken_AsksOnlyForTheReadOnlyCourseWorkScopes`, `TheTokenRequest_ImpersonatesTheTechnicalAccount` |
| S-06 no personal data in logs | `SubmissionImportLoggingTests` (5, anchored) |
| S-07 / SC-13 only Google reached | `EveryRequest_GoesToGoogle`, new scope test |
| S-09 validation of Google data | `CourseWorkInvariantTests`, `SubmissionInvariantTests`, schema tests |
| S-10 no audit row | `SubmissionSchemaTests.AScheduledRun_WritesNoAuditRow` |
| VR-008 refusal to start | `RetentionConfigurationTests` (5) |
| TC-4 offline | the host uses `FakeClassroomReader`; the adapter runs on a scripted transport with a key generated at run time |

The six adapter tests were added during IMPLEMENTATION (its D-2), not
TEST_WRITING. They are not vacuous: each would fail against the skeleton,
which threw, and against an adapter that reads only one page. See I-3.

## 15. Abuse Case Review

| Scenario | Expected protection | Evidence | Status |
|---|---|---|---|
| Run starts while read-only | no Google call, no write | guard first; three refusal tests with controls | PASS |
| Google returns an enormous title or id | bounded storage | truncation in entities, column lengths | PASS |
| Google returns an unknown or long state | stored with marker, bounded in DB | `SetState` truncates to 64; log is unbounded | F-1 |
| Google returns a submission for an unknown person | no profile lookup, no extra scope | participant from `userId` only (OD-006) | PASS |
| Inspect logs for grades or names | none | `SubmissionImportLoggingTests` | PASS |
| Misconfigured retention | refuse to start | `RetentionConfigurationTests`; no upper bound | I-1 |

## 16. Repository Hygiene

- `google_credentials.json`, `classroom_cache.db` and the telemetry file are
  ignored.
- No `dac-classroom-agent-*.json` file is tracked.
- The only tracked `.xlsx` is `docs/product/report-templates/school-journal-full.xlsx`,
  which is expected.
- Nothing in the change set matches a key, client secret, connection-string
  password, real domain address, `.env`, database file or `bin`/`obj` path.
- The live credential files were not opened.

## 17. Deviations

The implementation report's D-1 … D-4 are test-side and carry no security
impact. The per-call scope narrowing is a tightening.

No undocumented security behaviour was found.

## 18. Findings

### F-1 — Minor — LOGGING (SC-10)

- **File:** `src/ClassroomAgent.Web/BackgroundServices/SynchronizationBackgroundService.cs`,
  `LogSubmissionStateUnrecognised`; the value comes from
  `RunSynchronizationUseCase.ImportSubmissionsAsync`.
- **Observed:** `UnrecognisedSubmission` is built from `snapshot.GoogleId` and
  `snapshot.State` as Google sent them. Truncation to 64 happens only inside
  `Submission`, so the log line receives both values unbounded. This is the same
  shape as the open US-014 F-1 on the skipped-course line.
- **Expected:** a value from outside the system is bounded before it reaches a
  log line, as it is before it reaches the database.
- **Risk:** low. The sink is JSON, so a newline or control character cannot
  forge a line; the effect is log growth only. Separately, OD-005 requires one
  line per unrecognised submission. If Google introduced a new state, every run
  would write one Warning for each affected submission. That volume is by
  design, but it multiplies any unbounded value.
- **Correction:** pass the truncated values. Either take them from the entity
  after `SetState`, or cut them where `UnrecognisedSubmission` is built. Ideally
  fix it together with US-014 F-1, here or in US-017.
- **Loop-back:** none required to proceed (Minor). If the human wants it fixed in
  this Story: `IMPLEMENTATION`.
- **Verify after correction:** a test feeding an oversized state asserts that the
  logged `RawState` is at most 64 characters.

### I-1 — Informational — CONFIGURATION

`Retention:Years` is validated as ≥ 1 but has no upper bound. A value above
about 2000 makes `observedAt.AddYears(-years)` throw, and every run then fails
as `RunFailed:ArgumentOutOfRangeException`. Only the Owner sets this value at
deployment, and the effect is the installation's own availability, not
exposure. A bound such as 100 in `RetentionYears` would make the
misconfiguration refuse at startup, as VR-008 intends for invalid values.

### I-2 — Informational — TEST_COVERAGE

US-011 F-2 still applies, now to two more port members: no test resolves the
real `GoogleClassroomReader` from the composition root. This is carried, not
introduced.

### I-3 — Informational — TEST_COVERAGE

The TEST_WRITING paging tests ran against the in-memory reader and could not
have caught a single-page adapter. IMPLEMENTATION closed the gap (its D-2). The
lesson for future Google reads: the paging proof belongs at the adapter.

## 19. Positive Controls

- Read-only guard first, proven with paired controls.
- Per-call minimal read-only scopes, asserted on the token request.
- The submission history never crosses the port (structural, by the port's
  types).
- Raw `late` and grades stored without conversion; the state classification
  happens in Application, where the Warning is written.
- Parent-scoped natural keys: one Google-side repetition cannot fail a school's
  import.
- `Restrict` foreign keys.
- JSON log sink.
- `RunFailed:<type>` diagnosis, never a Google error text.

## 20. Open Decisions

No blocking security Open Decisions were identified. OD-010 leaves
`trebovaniya.md` §7 item 14 open, and no security behaviour depends on its
answer.

## 21. Review Limitations

- Google's real response shapes are verified only against synthetic fixtures
  (TC-4). A renamed field would pass here and fail in production.
- The test suite was not re-run by this stage. The IMPLEMENTATION run on the
  same working tree is used, and no file under `src/` or `tests/` has changed
  since.
- No dynamic or penetration testing was performed.

## 22. Verdict Rationale

The implementation report records a green build and green tests. There is no
Critical or Major finding, and every SC item the Story touches is PASS. F-1 is
the only deviation, and it is Minor. The security-sensitive Acceptance Criteria
AC-006, AC-008 and AC-009 are verified by passing tests, and no
security-sensitive Open Decision is unresolved. Verdict: **PASS**, with F-1 and
I-1 … I-3 as non-blocking findings for the human at `HUMAN_PR_APPROVAL`.
