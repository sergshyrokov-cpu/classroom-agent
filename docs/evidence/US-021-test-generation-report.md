---
artifact_type: test_generation_report
story: US-021
version: 1
status: DRAFT
created_at: 2026-10-11T16:30:00Z
updated_at: 2026-10-11T17:05:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-021-spec.md
    version: 1
  - path: docs/designs/api/US-021-api-design.md
    version: 1
  - path: docs/designs/api/US-021-openapi.yaml
    version: 1
  - path: docs/designs/database/US-021-db-design.md
    version: 1
  - path: docs/designs/database/US-021-entity-model.md
    version: 1
  - path: docs/decisions/US-021-open-decisions.md
    version: 2
  - path: docs/tests/US-021-test-strategy.md
    version: 1
  - path: docs/tests/US-021-ac-test-matrix.md
    version: 1
supersedes: null
---

# US-021 Test Generation Report

## 1. Result

**PASS — red phase verified.** The build has 0 warnings and 0 errors. The US-021
classes run **108 tests: 2 passed, 106 failed, 0 skipped**, every failure caused
by missing production behaviour (§4). The full suite ran **5 201 tests: 5 105
passed, 96 failed, 0 skipped** (9 min 50 s); 94 failures were US-021 tests (from
the run before the 404 tests got their control request, §4), and 2 were
connection timeouts in unrelated classes that pass on a re-run (§4). **No
existing test regressed.**

## 2. Production skeleton (OD-013 a, Owner 2026-10-11)

Declarations only, exactly as entity model §2, §3, §5 names them; members throw
`NotImplementedException`. Nothing registered in DI; no route, controller, view,
translation or migration; no existing file changed.

- `Application/Ports/ICourseCardSource.cs`
- `Application/Models/` — `CourseCardCourseRecord`, `CourseCardMemberRecord`,
  `CourseCardOutcome`, `CourseCardResult`
- `Application/Models/Requests/CourseCardRequest.cs`
- `Application/Models/Dtos/` — `CourseCardPageModel`, `CourseCardDetails`,
  `CourseCardPerson`, `CourseCardTeacher`, `CourseCardLeaver`
- `Application/UseCases/GetCourseCardQuery.cs` (throws)
- `Infrastructure/Persistence/CourseCardSource.cs` (throws)

Not created, left to IMPLEMENTATION: `CoursePersonNameRule` (entity model §4),
`SignInRoutes.CourseCard`, the controller action and view, DI registration, the
26 translation keys.

**Binding on IMPLEMENTATION** beyond the entity model: the markup hooks of test
strategy §5, the 26 keys of §6 (`CourseCardTestData.Keys.All`), and
`CourseCardPageModel.ReturnPath` = `/workspace/courses/{id}` in invariant
decimal form.

## 3. Files

Created (tests), under `tests/ClassroomAgent.Tests/`:
`Application/UseCases/CourseCardQueryTests.cs`;
`Infrastructure/Persistence/CourseCardSourceTests.cs`;
`Web/Pages/CourseCardPageTests.cs`; `Web/Security/CourseCardAuthorizationTests.cs`;
`Web/UseCases/CourseCardReadOnlyTests.cs`;
`Web/Localization/CourseCardTranslationTests.cs`;
`Web/Logging/CourseCardLoggingTests.cs`;
`TestInfrastructure/CourseCardTestData.cs`, `FakeCourseCardSource.cs`,
`CourseCardHostExtensions.cs`.

Modified (tests): `TestInfrastructure/CourseRows.cs` — `InsertCourseAsync` gains
optional `room` and `descriptionHeading` (existing callers unchanged).

Modified (artifacts): `docs/decisions/US-021-open-decisions.md` v2 — OD-013.

Created / modified (production skeleton): §2.

Delegated: the five host-level classes (`CourseCardPageTests`,
`CourseCardAuthorizationTests`, `CourseCardReadOnlyTests`,
`CourseCardTranslationTests`, `CourseCardLoggingTests`) were written by a Sonnet
worker against a fixed brief and reviewed here. It found a seed defect
(leavers' `first_seen_at` later than `last_seen_at`, violating
`ck_course_membership_seen_order`), fixed inline. The design, the skeleton, the
fixtures, the unit and integration tests, the 404 control request and the
red-phase verdict were done inline.

## 4. Execution

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `dotnet test ClassroomAgent.sln --no-build` (Docker running) | total 5 201, passed 5 105, failed 96, skipped 0; 9 m 50 s |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~NoStoreResponseTests\|FullyQualifiedName~DeanAccountTranslationTests"` | total 74, passed 74 |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~CourseCard"` (final) | total 108, passed 2, failed 106; 1 m 09 s |

**The two unrelated failures** of the full run —
`NoStoreResponseTests.AMissingFileUnderAStaticPath_CarriesNoStore` and one case
of `DeanAccountTranslationTests.EveryKey_IsTranslatedInBothLanguages` — were
`NpgsqlException: The operation has timed out` after 3 min 50 s, a Testcontainers
load timeout (US-033 security note INFO-3). Both classes pass on a re-run.

**Why the 106 fail** — all for missing production behaviour:

- 47 unit tests and 12 integration tests: `NotImplementedException` from the
  skeleton (`GetCourseCardQuery`, `GetCourseAsync`, `GetMembersAsync`);
- host tests expecting `200` get `404` — the card route does not exist; the
  anonymous test expects `302` and gets `404` for the same reason; the course
  list has no link to a card yet;
- the 12 not-found cases now fail at their control request (the card must
  answer `200` before the bad value is tried);
- read-only use-case tests: `No service for type GetCourseCardQuery` (not
  registered);
- the endpoint test finds no endpoint; translation tests find no
  `Courses.Card.*` key (`Translation key 'Courses.Card.Title' is missing`);
  logging tests fail at the page's `404`.

**Why 2 pass**, each legitimately green before implementation:

- `CourseCardQueryTests.TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly` —
  structural, fixed by the skeleton's binding constructor.
- `CourseCardAuthorizationTests.ADeanWithATemporaryPassword_IsSentToTheForcedChange`
  — the US-012 restricted session redirects every path; it must stay green once
  the route exists.

The first run had 12 more green tests: the not-found cases passed vacuously
because a missing route also answers `404`. Each now opens a real card first
and requires `200`, so it can pass only against the implemented route.

## 5. Untested Acceptance Criteria

None. Every AC-001 … AC-017 maps to tests (`US-021-ac-test-matrix.md`). Excluded
by design (test strategy §7): migration, audit, API-6 body, antiforgery, phone
layout, the CSS of the description's line break.

## 6. Open Decisions

OD-013 (compile-only skeleton) — raised here, resolved (a) by the Owner,
2026-10-11. None open.

## 7. Notes for IMPLEMENTATION and SECURITY_REVIEW

- The language switcher's return path must be built from the parsed id, not the
  request path (tested with `00{id}?evil=…`).
- The `Information` log line must carry the three counts as structured
  properties (the test waits for property values `7` and `3`).
- Keep `CourseRows` defaults: leavers need an explicit `firstSeenAt` not later
  than `lastSeenAt` (`ck_course_membership_seen_order`).
