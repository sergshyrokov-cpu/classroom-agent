---
artifact_type: security_review
story: US-062
version: 1
status: APPROVED
created_at: 2026-10-28T07:28:21Z
updated_at: 2026-10-28T07:28:21Z
produced_by: security-reviewer
inputs:
  - path: docs/stories/US-062-data-health-banner-and-page.md
    version: null
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
  - path: docs/designs/api/US-062-api-design.md
    version: 1
  - path: docs/designs/api/US-062-openapi.yaml
    version: 1
  - path: docs/designs/database/US-062-db-design.md
    version: 1
  - path: docs/designs/database/US-062-entity-model.md
    version: 1
  - path: docs/tests/US-062-test-strategy.md
    version: 1
  - path: docs/tests/US-062-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-062-implementation-report.md
    version: 1
  - path: docs/reviews/implementation/US-062-implementation-verification.md
    version: 1
supersedes: null
critical_findings: 0
major_findings: 0
minor_findings: 0
informational_findings: 3
security_sensitive: true
runtime_checks: FULL
---

# US-062 Security Review — data-health banner and «Стан даних»

## 1. Executive Summary

**Result: PASS — 0 Critical, 0 Major, 0 Minor, 3 Informational.**

The Story adds one read page for Admin and Dean (`GET /workspace/data-health`), a banner in the
installation layout, and one Admin-only write (`POST /workspace/data-health/course-deletion-confirmation`)
that turns «не виден техническому аккаунту» marks into «нет в Classroom» and writes an audit row.
Principal controls verified: role policies matching `trebovaniya.md` §2 rows 749/750; the read-only
guard in Application before any input is parsed; antiforgery on the POST; strict server-side VR-001
parsing (1–1000 positive integers); all-or-nothing confirmation under a `FOR UPDATE` lock; the Dean's
model carries no technical details (restriction in the DTO, not the markup); log lines with account
ids, counts and rule names only; audit row shape enforced by CHECK constraints.

Limitation: the full suite never ran green in one pass on this machine (35 environment timeouts at
~1.9 GB free RAM); every failing class passed when rerun alone, and no Story class failed (INFO-1).

## 2. Reviewed Artifacts

See front matter `inputs`. All mandatory inputs are current; none `SUPERSEDED`. `HUMAN_SPEC_APPROVAL`
recorded in `history.jsonl`. OD-001…OD-005 resolved by the Owner (2026-10-27).

## 3. Security-Relevant Scope

| Host | Surface | Policy |
|---|---|---|
| Web (installation) | `GET /workspace/data-health` | `ViewDataHealth` — Admin, Dean |
| Web | `POST /workspace/data-health/course-deletion-confirmation` | `ConfirmCoursesDeleted` — Admin |
| Web | banner in `_Layout.cshtml` (every page) | computed only for a signed-in Admin/Dean, not on the forced password change (OD-001 a) |

Assets: student names and grades above the task maximum (shown to both roles, FR-002.2); course names
and states; technical diagnosis (Admin only); audit rows. Trust boundaries: browser → Web; controller →
Application use cases; Application → PostgreSQL. No Google port, no Control Plane call, no new outbound
flow. Persistence: `sync_state.course_list_empty_since`, `audit_event.confirmed_course_ids bigint[]`,
two partial indexes, CHECK constraints (migration `20261027202639_AddDataHealth`).

## 4. Environment and Tools

.NET SDK 10.0.401; Docker 29.8.0 running (Testcontainers). Delegated to a cheap-worker (Sonnet), read
only: `dotnet build ClassroomAgent.sln`, `dotnet test ClassroomAgent.sln`, per-class reruns,
`dotnet list ClassroomAgent.sln package --vulnerable --include-transitive`, `dotnet --version`. Code,
configuration, views, migration and tests were read by the reviewer directly.

| Command | Result |
|---|---|
| build | 0 warnings, 0 errors |
| full test | 6619 total, 6584 passed, 35 failed, 0 skipped (22 min; ~1.9 GB free RAM) — 33 Npgsql timeouts / transient failures, 2 sign-in `500`s in the same window |
| 15 failing classes rerun one by one | all green (e.g. RevokedAdminTests 8/8, AccessCheckRefusalTests 17/17, ReadsInReadOnlyModeTests 10/10) |
| vulnerable packages | none in any of the 7 projects (nuget.org reachable) |

No Story class (DataHealth*, CourseDeletionConfirmation*, ConfirmCoursesDeleted*, CourseListEmptySince*,
PermissionMatrix*) appears among the failures. The implementation report records a green build and a
6618/6619 run with the one timeout green on rerun.

## 5. Project Security Checklist

| SC | Status | Evidence |
|---|---|---|
| SC-1 Roles | PASS | Only Admin/Dean in the two new policies (`InstallationSecurityServices.cs`); cells match §2 rows 749/750; `PermissionMatrixTests` rows added. |
| SC-2 Authentication | NOT_APPLICABLE | No sign-in, password or cookie change. |
| SC-3 AllowedAdmin | NOT_APPLICABLE | Sign-in path untouched. |
| SC-4 Authorization | PASS | Both actions declare `[Authorize(Policy=…)]`; no `[AllowAnonymous]`, no `IgnoreAntiforgeryToken`; POST validated by the global antiforgery filter (`WithoutTheAntiforgeryToken_ThePostIs400_AndNothingChanges`); GET changes nothing (TempData read only); banner returns null for anonymous users and the forced-change path. |
| SC-5 Read-only | PASS | `ConfirmCoursesDeletedUseCase` calls `IReadOnlyModeGuard.EnsureAllowedAsync` first; `ReadOnlyModeException` → `409` by the host handler; only the refused audit row is written under `PermittedServiceWrite.AuditEvent` (BR-026 list); no Google port. Tested in `ConfirmCoursesDeletedUseCaseTests` and `InReadOnlyMode_TheAdminIsRefusedWithTheReason`. |
| SC-6 No DB UI | PASS | No diagnostic endpoint added. |
| SC-7 Key | NOT_APPLICABLE | Untouched. |
| SC-8 Google | NOT_APPLICABLE | No Google call; reads the stored mirror only. |
| SC-9 Channel | NOT_APPLICABLE | Untouched. |
| SC-10 Hygiene | PASS | `DataHealthLog`: account id, count, rule name, exception type name only; no request value logged; refusals re-render the page with a translated one-line message, no internals. |
| SC-11 Audit | PASS | `AuditAction.CoursesConfirmedDeleted`; succeeded row carries actor + internal course ids (no personal data); refused row in read-only mode (API m-1 accepted); CHECK constraints `ck_audit_event_course_deletion_shape`, `ck_audit_event_confirmed_course_ids_absent`; no update/delete path. |
| SC-12 Owner | PASS | No `Contracts`/ControlPlane change. |
| SC-13 Outbound | PASS | No new destination. |

## 6. Authentication and Authorization

Dean POST → `403`, nothing changes (`ADean_IsForbidden_AndNothingChanges`); partial sessions redirect
(`WithoutAFullSession_ThePostRedirects_AndChangesNothing`). The actor id comes from the session claim,
never from the request. Course ids from the form only select rows that must already be marked
`NotVisibleToTechnicalAccount`; an unknown or differently marked id refuses the whole request (`409`,
nothing written) — a foreign or arbitrary id cannot change a course in another state. One installation
= one database (AD-1), so no cross-tenant id exists.

## 7. Credentials, Key and Google Access

Not touched. No password, key or Google scope in the change set.

## 8. Sensitive Data Exposure

`GetDataHealthQuery` shapes the model by role: for the Dean every `TechnicalProblem.Details` is null, so
the diagnosis, the X3/X5 course lists and the confirmation form are absent from the model, not hidden
(view renders the form only inside `Details`). Student names and grades above maximum go to both roles
as FR-002.2 and §2 row 749 allow. Razor output is HTML-encoded; no `Html.Raw`, no script. Banner
failures are logged by exception type only (I-4).

## 9. Input Validation

VR-001 in Application (`ConfirmCoursesDeletedUseCase.Parse`): 1–1000 distinct ids after de-duplication,
ASCII digits only, no leading zero or sign, ≤ 19 digits, `long.TryParse` with `NumberStyles.None`.
Malformed → `400` with the page (`AMalformedForm_Is400_…`). Domain factory re-checks count and
positivity; the database CHECK bounds cardinality. TempData count parsed with `NumberStyles.None`, > 0.

## 10. API Security

Only the two endpoints of the approved openapi exist (`PermissionMatrixTests` cells). Methods match
API-3/API-4. Responses: `302` (PRG), `400`, `403`, `409` as designed (api-design §2.6).

## 11. Persistence and Configuration

Schema change only via the migration; `FromSql` with an interpolated array is parameterized by EF Core
(no string concatenation). The backfill `UPDATE` is a constant statement. No configuration change, no
connection string, no generated file tracked.

## 12. Logging, Audit and Telemetry

Covered in §5 SC-10/SC-11. Hook telemetry untouched.

## 13. Dependencies

No `.csproj` changed; no package or project reference added. Vulnerability scan: none found.

## 14. Security Test Coverage

| Requirement | Test |
|---|---|
| allowed/forbidden role per endpoint (TC-5) | `PermissionMatrixTests` (both cells), `ADean_IsForbidden_AndNothingChanges`, `DataHealthPageTests` |
| read-only refusal in Application | `ConfirmCoursesDeletedUseCaseTests`, `InReadOnlyMode_TheAdminIsRefusedWithTheReason` |
| antiforgery | `WithoutTheAntiforgeryToken_ThePostIs400_AndNothingChanges` |
| malformed input | `AMalformedForm_Is400_WithThePage_AndChangesNothing` |
| audit written / shape | `ConfirmCoursesDeletedUseCaseTests`, `InstallationAuditEventSchemaTests` |
| Dean sees no details | `DataHealthQueryTests`, `DataHealthPageTests` (AC-006) |

## 15. Abuse Case Review

| Scenario | Protection | Status |
|---|---|---|
| Dean posts the confirmation directly | policy → 403 | PASS |
| Admin posts in read-only mode | guard → 409, refused audit row | PASS |
| Admin posts ids of visible/foreign/unknown courses | all-or-nothing state check under lock → 409 | PASS |
| 10 000 ids or junk values | VR-001 → 400 before any query | PASS |
| Cross-site POST | global antiforgery → 400 | PASS |
| Dean reads technical diagnosis via the page | absent from the model | PASS |

## 16. Repository Hygiene

`google_credentials.json` git-ignored; no `.db`, `.env` or generated `.xlsx` tracked; no secret-like
file in the change set.

## 17. Deviations

None security-relevant. Implementation-verification m-1 (label expression duplicated) has no security
effect: same columns, same roles.

## 18. Findings

- **INFO-1 (TEST_COVERAGE / environment).** The full suite ran with 35 timeout-class failures at
  ~1.9 GB free RAM; each of the 15 classes passed alone. Not a code defect; a single green full run on
  a machine with more free memory would remove the caveat. No loop-back.
- **INFO-2 (DATA_EXPOSURE, no action).** For the Dean an archived X3 entry without a course name may
  sit next to a teaching entry of the same course that carries the name (implementation INFO-2). This is
  not a confidentiality leak — the Dean sees every course name elsewhere (FR-002.1); the restriction is
  about details, not secrecy. The open functional finding (no test, D-8) is left OPEN for its owner.
- **INFO-3 (AUDIT, by design).** In read-only mode every POST writes a refused audit row before input is
  validated (API m-1 accepted by the Owner); an Admin can therefore add rows by repeating the request.
  Bounded to signed-in Admins and covered by the retention purge.

## 19. Positive Controls

Role policies with matrix tests; read-only guard first in Application; antiforgery; strict VR-001;
row lock + all-or-nothing; DTO-level Dean restriction; PII-free structured logs; CHECK-enforced audit
shape; parameterized SQL.

## 20. Open Decisions

No blocking security Open Decisions were identified.

## 21. Review Limitations

No penetration testing; review of code, configuration and tests only. Full suite green only in
aggregate across a full run plus per-class reruns (INFO-1).

## 22. Verdict Rationale

No Critical, Major or Minor finding; every touched SC item PASS; security tests green; build clean; no
vulnerable or new dependency. Verdict **PASS**, next stage `HUMAN_PR_APPROVAL`.

```yaml
result:
  verdict: PASS
  stage: SECURITY_REVIEW
  story: US-062
  artifact_status: APPROVED
  artifacts:
    - docs/reviews/security/US-062-security-review.md
  next_stage: HUMAN_PR_APPROVAL
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - "INFO-1: full suite had 35 environment timeouts at ~1.9 GB free RAM; all 15 classes green on rerun"
    - "INFO-2: Dean's archived X3 entry may sit beside a named entry of the same course; no confidentiality impact"
    - "INFO-3: in read-only mode each confirmation POST writes a refused audit row before validation (by design, API m-1)"
  closed_findings: []
```
