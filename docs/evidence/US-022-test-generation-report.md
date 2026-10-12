---
artifact_type: test_generation_report
story: US-022
version: 1
status: DRAFT
created_at: 2026-10-12T10:00:00Z
updated_at: 2026-10-12T10:00:00Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-022-spec.md
    version: 1
  - path: docs/designs/api/US-022-api-design.md
    version: 1
  - path: docs/designs/api/US-022-openapi.yaml
    version: 1
  - path: docs/designs/database/US-022-db-design.md
    version: 1
  - path: docs/designs/database/US-022-entity-model.md
    version: 1
  - path: docs/tests/US-022-test-strategy.md
    version: 1
  - path: docs/tests/US-022-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-022-open-decisions.md
    version: 2
supersedes: null
---

# US-022 Test Generation Report — Teachers and their courses

## 1. Result

**Red phase verified.** The solution builds with 0 warnings and 0 errors. The
full suite ran: 5411 tests, 5309 passed, 102 failed. Every failure is a new
US-022 test failing for missing production behaviour. No existing test failed.

## 2. Production skeleton (OD-010 a, Owner 2026-10-12)

Compile-only. Nothing is registered in DI. There is no route, view, translation
or migration, and no existing behaviour changed. The members that would do work
throw `NotImplementedException`.

- `Application/Ports/ITeacherListSource.cs`
- `Application/Models/TeacherListRecord.cs`, `TeacherListPageOutcome.cs`,
  `TeacherListPageResult.cs`
- `Application/Models/Requests/TeacherListRequest.cs`
- `Application/Models/Dtos/TeacherListPageModel.cs`, `TeacherListResult.cs`,
  `TeacherListRow.cs`, `TeacherListMessageKey.cs`
- `Application/UseCases/TeacherListUrl.cs` (`Path` set, `Build` throws)
- `Application/UseCases/GetTeacherListQuery.cs` (`PageSize = 50`, `ExecuteAsync`
  throws)
- `Infrastructure/Persistence/TeacherListSource.cs` (both reads throw)

Signatures are those of entity model §2–§4. `SignInRoutes.Teachers` was not
added; it belongs with the route at IMPLEMENTATION.

## 3. Files

**Test sources created** (under `tests/ClassroomAgent.Tests/`):

| File | Tests |
|---|---|
| `TestInfrastructure/TeacherListTestData.cs` | fixture |
| `TestInfrastructure/TeacherListHostExtensions.cs` | fixture |
| `TestInfrastructure/FakeTeacherListSource.cs` | fixture |
| `Application/UseCases/TeacherListQueryTests.cs` | 46 cases |
| `Application/UseCases/TeacherListUrlTests.cs` | 8 cases |
| `Infrastructure/Persistence/TeacherListSourceTests.cs` | 10 |
| `Web/Pages/TeacherListPageTests.cs` | 17 cases |
| `Web/Security/TeacherListAuthorizationTests.cs` | 5 cases |
| `Web/UseCases/TeacherListReadOnlyTests.cs` | 8 cases |
| `Web/Localization/TeacherListTranslationTests.cs` | 6 cases |
| `Web/Logging/TeacherListLoggingTests.cs` | 4 cases |

**Test sources modified:** none.

**Production skeleton:** §2.

**Artifacts:** `docs/tests/US-022-test-strategy.md`,
`docs/tests/US-022-ac-test-matrix.md`, this report;
`docs/decisions/US-022-open-decisions.md` v2 adds OD-010.

**Delegation.** The test design, the skeleton, the three fixtures, the unit and
integration classes (`TeacherListQueryTests`, `TeacherListUrlTests`,
`TeacherListSourceTests`), the strategy, the matrix and the red-phase verdict
were done inline. One `cheap-worker` agent wrote the five HTTP classes (page,
authorization, read-only, translation, logging) against a fixed scenario brief.
Its output was checked here by build, by the full run and by reading the
address test. It made one choice: the archived-only empty case became a
separate test, `OnlyArchivedCourses_ShowTheMessage_UnderTheDefaultStatus`.

## 4. Execution

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | 0 warnings, 0 errors |
| `dotnet test --solution ClassroomAgent.sln --no-build --report-xunit-trx --report-xunit-trx-filename us022-red.trx` | 5411 total, 5309 passed, 102 failed, 0 skipped, 6 m 05 s |

All 102 failures are in the US-022 classes:

| Class | Failed | Cause |
|---|---|---|
| `TeacherListQueryTests` | 46 | `NotImplementedException` from the skeleton |
| `TeacherListUrlTests` | 7 | `NotImplementedException` from the skeleton |
| `TeacherListSourceTests` | 10 | `NotImplementedException` from the skeleton |
| `TeacherListPageTests` | 17 | 12 × `404` where `200`/`400` is expected; 3 × home page has no `/workspace/teachers` link at position 2; 2 × no rows or row links on the `404` page |
| `TeacherListAuthorizationTests` | 4 | `404` where `200`/`302` is expected; no endpoint enumerated for `workspace/teachers` |
| `TeacherListReadOnlyTests` | 8 | 4 × `GetTeacherListQuery` not registered in DI; 4 × `404` |
| `TeacherListTranslationTests` | 6 | `Teachers.*` keys missing; `404` |
| `TeacherListLoggingTests` | 4 | `404` where `200`/`400` is expected, so no event |

Existing regression tests: 5309 passed. This includes the US-020 course-list
classes, the home-page tests and the endpoint-enumerating security tests.

## 5. Tests passing before implementation

- `TeacherListUrlTests.ThePath_IsTheTeacherListRoute` is structural. The
  skeleton sets the path constant from entity model §3, so it passes.
- `TeacherListAuthorizationTests.ADeanWithATemporaryPassword_IsSentToTheForcedChange`
  passes because the forced-password-change redirect (US-012) runs before
  routing for every path. It is not vacuous: it asserts the `302` target and
  that no teacher data is in the response. Once the route exists, it proves the
  page is not exempt from that redirect.

No other new test passes.

## 6. Untested Acceptance Criteria

None. Every AC-001 … AC-016 maps to at least one test in the matrix.

## 7. Open Decisions

OD-010 (compile-only skeleton) was resolved (a) by the Owner on 2026-10-12. No
Open Decision is open.

## 8. Notes for IMPLEMENTATION

- **Markup hooks and translation keys** in strategy §4 and §5 are binding. The
  tests read `data-participant-id`, `teacher-list-*` ids, `form-error` and
  `nav#workspace-section`.
- **Home page links.** "Teachers" must be the second link of
  `nav#workspace-section`. The US-020 test still requires "Courses" first.
- **Row link.** It must equal the literal form in api-design §2.6: `status`
  first, omitted for active, then `teacherId`.
- **Course counts.** `TeacherListSourceTests.TheOrder_EqualsTheCourseListsTeacherChoice`
  and `TheCount_AgreesWithTheCourseListsTeacherFilter` compare against
  `CourseListSource`. Share the label key with it rather than copying it
  (db-design §3.3).
- **Address test.** `TeacherListPageTests.TheAddress_…` fails on any link
  containing `@`, a teacher surname or `google`, whatever its target. The
  current shared layout has none.
- **Registration.** `GetTeacherListQuery` and `TeacherListSource` must be
  registered (entity model §2, §5).
