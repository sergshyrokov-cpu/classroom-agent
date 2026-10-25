---
artifact_type: security_review
story: US-056
version: 1
status: APPROVED
created_at: 2026-10-25T23:00:00Z
updated_at: 2026-10-25T23:00:00Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-056-membership-starts-at-course-creation.md
    version: null
  - path: docs/specifications/US-056-spec.md
    version: 3
  - path: docs/decisions/US-056-open-decisions.md
    version: 1
  - path: docs/designs/api/US-056-api-design.md
    version: 1
  - path: docs/designs/database/US-056-db-design.md
    version: 1
  - path: docs/designs/database/US-056-entity-model.md
    version: 1
  - path: docs/tests/US-056-test-strategy.md
    version: 1
  - path: docs/tests/US-056-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-056-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-056-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: false
runtime_checks: FULL
---

# US-056 Security Review

## 1. Executive Summary

**PASS. 0 Critical, 0 Major, 0 Minor, 3 Informational.**

US-056 changes how a membership's start is computed (trebovaniya.md v98: a membership recorded by the
course's first load starts at the course's Google `creationTime`). The change is a read-side computation:

- one new pure Domain function, `MembershipStart.Of`;
- four EF Core readers that project two more `course` columns;
- one `has_candidate` subquery that gains a `JOIN course` and a `CASE`;
- renames from `FirstSeenAt` to `StartsAt`.

Nothing else changed: no endpoint, page, policy, role, Google call, outbound flow, log line, audit
event, schema, migration, configuration or package. I ran the build and the full test suite myself:
0 warnings, 6168/6168 passed. The vulnerability scan found nothing.

## 2. Reviewed Artifacts

The `inputs` above, all current. Spec v3 is APPROVED at HUMAN_SPEC_APPROVAL (history 2026-10-25). The
API design is NOT_APPLICABLE, so there is no `openapi`. Open Decisions v1 has none open.

## 3. Security-Relevant Scope

- **Exposed functionality.** The Data Plane journal, template report, Meet reports and Meet codes pages
  are unchanged as routes. What they show for a period can now include members whose start is the course
  creation date. The Control Plane is untouched.
- **Assets.** Personal data of students and teachers (names, emails, membership dates) in
  `course_membership`, `classroom_participant` and `course`.
- **Trust boundaries.**
  - Application → persistence port (the readers).
  - Application → PostgreSQL (the raw SQL in `MeetCodesReadSource`).
  - Background Meet linking reads `MeetCodeScoringSource`.

## 4. Environment and Tools

- .NET SDK 10.0.401. Docker 29.8.0 (Testcontainers available).
- Commands were delegated to `cheap-worker` (read only):
  - `dotnet build ClassroomAgent.sln`: 0 warnings, 0 errors.
  - `dotnet test ClassroomAgent.sln`: 6168 total, 6168 passed, 0 failed, 0 skipped, 8 min 02 s.
  - `dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`: no vulnerable package in
    any of the 7 projects.
  - `git diff --stat` over `*.csproj` / `Directory.*.props`: empty.
- The source diff, SQL and test fixtures were read by this reviewer.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | NOT_APPLICABLE | No role, policy or seed changed. |
| SC-2 Authentication | NOT_APPLICABLE | Untouched. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Untouched. |
| SC-4 Authorization | PASS | No endpoint or page added or changed (`git status`: no `Web`/`ControlPlane` file). Existing page policies apply. Full suite green, including the §2 matrix and forbidden-role tests (US-052). |
| SC-5 Read-only mode | PASS | No write use case added. Meet linking is a sync write already gated by read-only mode; only its reader changed. |
| SC-6 No DB UI | NOT_APPLICABLE | Untouched. |
| SC-7 Key | NOT_APPLICABLE | Untouched. |
| SC-8 Google | PASS | No Google call added. `FakeClassroomReader` only passes `creationTime`, a field already read. |
| SC-9 Channel | NOT_APPLICABLE | Untouched. |
| SC-10 Hygiene | PASS | No logger call, exception or response field added. |
| SC-11 Audit | NOT_APPLICABLE | No audited action introduced. |
| SC-12 Owner | NOT_APPLICABLE | `Contracts` / `ControlPlane` untouched. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

No access path changed. The readers stay scoped exactly as before:

- `JournalSource.GetStudentMembersAsync`, `JournalFieldSource.GetMembersAsync` and
  `MeetReportSource.GetCourseMembersAsync` are filtered by `courseId`;
- `MeetCodeScoringSource` is filtered by organizer email;
- the `has_candidate` subquery joins `course` by `cm.course_id` and does not widen the row set.

## 7. Credentials, Key and Google Access

Not touched.

## 8. Sensitive Data Exposure

- The readers project two more `course` columns (`CreatedAt`, `CreationTime`), use them only to compute
  `StartsAt`, and do not pass them on.
- The record types renamed one field and added none, so DTOs and views carry the same data as before.
- See INFO-1 for the changed population of a period.

## 9. Input Validation

No new external input. `creationTime` from Google was already validated and stored by sync.

## 10. API Security

No endpoint. The API design is NOT_APPLICABLE.

## 11. Persistence and Configuration

- **SQL.** `MeetCodesReadSource` uses `Database.SqlQuery<T>($"""…""")`, an interpolated
  `FormattableString` that EF parameterizes. The added `JOIN course` and `CASE` interpolate no value; the
  only interpolations (`{zoneId}`) existed before. No injection surface was added.
- **Schema.** No schema change and no migration (db-design §2), consistent with PC-2.
- **Configuration.** Unchanged.

## 12. Logging, Audit and Telemetry

- No logging or audit change.
- `docs/hooks/tool-usage.jsonl` is git-ignored (`.gitignore:66`).

## 13. Dependencies

- No package or project reference change.
- `Infrastructure → Domain` (`using ClassroomAgent.Domain.Rules`) is an existing, permitted reference
  (AD-3).
- The vulnerability scan is clean.

## 14. Security Test Coverage

- No new protected endpoint, so TC-5 adds no obligation.
- The existing authorization and read-only suites pass in the full run.
- Fixtures are synthetic: test hosts and fake reader, no live Google (TC-4).
- Integration tests run on Testcontainers PostgreSQL (TC-2).

## 15. Abuse Case Review

| Scenario | Expected | Evidence | Status |
|---|---|---|---|
| A Dean views another course's members through the new join | Rows stay limited to the requested course | Reader `Where(m => m.CourseId == courseId)` unchanged; join is on the membership's own course | PASS |
| Meet code auto-linked to a wrong course because of an earlier start | Only a teacher of that course on that date (per v98) is a candidate | `RosterOnDate.Covers` + SQL mirror; AC-004 tests; IMPL_VERIFICATION N-1 (boundary pinned in C# only) | PASS (INFO-2) |
| Linking runs in read-only mode | Blocked | Sync gating unchanged (BR-026); full suite green | PASS |

## 16. Repository Hygiene

- `google_credentials.json` and `classroom_cache.db` are git-ignored. No secret-like, `.db` or `.xlsx`
  file is in the change set.
- `.claude/settings.local.json` is not in `git status`.
- Two test files are LF in the working copy (IMPL_VERIFICATION N-3). This is cosmetic.

## 17. Deviations

None security-relevant. D-1 (a fixture given `firstLoadedAt`) is a test-meaning correction that
IMPL_VERIFICATION accepted.

## 18. Findings

- **INFO-1 (DATA_EXPOSURE).** For a course whose first load happened after its Google creation, journals,
  reports and Meet reports for earlier periods now list the first-load members. This is the intended
  effect of trebovaniya.md v98 and stays within Admin/Dean access. No correction is required.
- **INFO-2 (TEST_COVERAGE).** The SQL `<=` boundary of `has_candidate` is pinned only by the C# rule tests
  (IMPL_VERIFICATION N-1). The worst effect of a drift is a different ordering of unassigned codes, which
  is not a security control.
- **INFO-3 (OTHER).** The implementation report's single flaky failure (`PushCheckCoordinationTests`) did
  not recur. The full run in this review was 6168/6168.

## 19. Positive Controls

- Parameterized raw SQL.
- Course-scoped readers.
- A rule in one pure Domain function with no I/O.
- No new outbound or logging path.
- A clean vulnerability scan.
- Full suite green, including the authorization matrix and read-only tests.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

- This was a code, configuration and test review, not a penetration test.
- The application was not started.

## 22. Verdict Rationale

The build is green and the full suite is green, run independently. No Critical, Major or Minor finding.
Every touched SC item is PASS and no security Open Decision is open. **PASS → HUMAN_PR_APPROVAL.**

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-056
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-056-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "INFO-1 earlier periods now list first-load members (intended, v98, Admin/Dean only)"
    - "INFO-2 SQL <= boundary of has_candidate pinned only in C# (= IMPL_VERIFICATION N-1)"
    - "INFO-3 flaky PushCheckCoordinationTests did not recur; full suite 6168/6168"
```
