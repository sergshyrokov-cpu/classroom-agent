---
artifact_type: implementation_report
story: US-022
version: 1
status: DRAFT
created_at: 2026-10-12T10:15:49Z
updated_at: 2026-10-12T10:15:49Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-022-teachers-and-their-courses.md
    version: null
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
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-022 Implementation Report — Teachers and their courses

## 1. Summary

The teacher list `GET /workspace/teachers` is implemented. It is policy
`ViewCourses` (Admin, Dean), takes `status` and `page`, and shows 50 teachers per
page in the course pages' label order. Each row shows the name, the email and the
course count of the chosen status, and links to the course list filtered by that
teacher. "Teachers" is the second link of the home page's Google Workspace
section. The page reads only: there is no guard, no write, no audit and no Google
port. No schema change and no migration (db-design §1).

Build 0 warnings / 0 errors; full suite 5411/5411 green; format check clean. The
102 red-phase US-022 tests now pass.

`security_sensitive: true` because the page shows personal data of synced
teachers (names, emails) and adds a protected endpoint. It adds no credential,
role, policy or outbound flow.

## 2. Source Artifacts

As listed in `inputs`. HUMAN_SPEC_APPROVAL was recorded on 2026-10-12. OD-001 …
OD-010 are resolved; no Open Decision is open.

## 3. Implemented Acceptance Criteria

Tests are named in `docs/tests/US-022-ac-test-matrix.md`. Classes:
Q = `TeacherListQueryTests`, U = `TeacherListUrlTests`,
S = `TeacherListSourceTests`, P = `TeacherListPageTests`,
A = `TeacherListAuthorizationTests`, RO = `TeacherListReadOnlyTests`,
T = `TeacherListTranslationTests`, L = `TeacherListLoggingTests`.

| AC | Implementation | Tests | Status |
|---|---|---|---|
| AC-001, AC-002 | `TeacherListSource.Matching` / `Listed` (teacher, on roster, state); `GetTeacherListQuery` status → `CourseListQueryRules.StateOf` | Q, S, P | PASS |
| AC-003 | `GetTeacherListQuery` row mapping (`CoursePersonNameRule.NameOf`, email null when empty); `Views/Teachers/Index.cshtml` | Q, S, P | PASS |
| AC-004 | `CourseListUrl.Build(status, participantId, null, 1)` in `GetTeacherListQuery` | Q, P | PASS |
| AC-005 | `TeacherListSource.GetTeacherPageAsync` (order, `Skip`/`Take`); clamping in `GetTeacherListQuery`; paging nav in the view | Q, S, P | PASS |
| AC-006 | `TeacherListUrl.Build`; `ReturnPath` from validated values; the view sets `UiLanguageController.ReturnPathViewDataKey` | U, Q, P | PASS |
| AC-007 | `Views/Home/Index.cshtml` link after "Courses" | P | PASS |
| AC-008 | `IsEmpty` when T1 = 0, so T2 is not run; `#teacher-list-empty`, no table | Q, S, P | PASS |
| AC-009 | no `IReadOnlyModeGuard`, no write, no Google port in `GetTeacherListQuery` / `TeacherListSource` | RO, S | PASS |
| AC-010 | `TeachersController` `[Authorize(Policy = InstallationPolicies.ViewCourses)]` | A | PASS |
| AC-011 | 9 `Teachers.*` keys in both resx; reused `Courses.*` keys; `TeachersTextKeys` | T | PASS |
| AC-012 | no Google port touched; synthetic fixtures | RO, P | PASS |
| AC-013 | `CourseListQueryRules.TryReadStatus` / `TryReadPage`; `Invalid` → 400 and the page; `TeacherListLog.Refused` (parameter + rule) | Q, P, L | PASS |
| AC-014 | `ParticipantLabel.Projection` + `OrderByLabel`, shared with `CourseListSource.GetTeacherChoiceAsync` | Q, S | PASS |
| AC-015 | T1 `CountAsync`, T2 one command with a correlated count after paging; two reads at most | Q, S, P | PASS |
| AC-016 | same membership predicate as the course list's teacher filter | S, P | PASS |

## 4. Change Set

| File | Change | Trace |
|---|---|---|
| `src/ClassroomAgent.Application/UseCases/GetTeacherListQuery.cs` | skeleton → implementation | entity model §3–§4; FR-001…FR-010, FR-013 |
| `src/ClassroomAgent.Application/UseCases/TeacherListUrl.cs` | `Build` implemented | entity model §3; api-design §2.7; FR-009 |
| `src/ClassroomAgent.Application/UseCases/CourseListQueryRules.cs` | **new**, internal: status/page rules and `StateOf`, shared | entity model §3 ("extracting … in scope as a supporting change"); api-design §2.2 |
| `src/ClassroomAgent.Application/UseCases/GetCourseListQuery.cs` | `ReadStatus` / `ReadPage` / `StateOf` delegate to `CourseListQueryRules`; behaviour unchanged | supporting change, same source; US-020 tests green |
| `src/ClassroomAgent.Infrastructure/Persistence/TeacherListSource.cs` | skeleton → T1, T2 | db-design §2–§3; entity model §2 |
| `src/ClassroomAgent.Infrastructure/Persistence/ParticipantLabel.cs` | **new**, internal: label key projection and order | db-design §3.3 (share rather than copy) |
| `src/ClassroomAgent.Infrastructure/Persistence/LabeledParticipant.cs` | **new**, internal: query shape for the above | db-design §3.3 |
| `src/ClassroomAgent.Infrastructure/Persistence/CourseListSource.cs` | `GetTeacherChoiceAsync` uses `ParticipantLabel`; same SQL semantics | db-design §3.3; `TheOrder_EqualsTheCourseListsTeacherChoice` |
| `src/ClassroomAgent.Web/Controllers/TeachersController.cs` | **new**: `GET /workspace/teachers` | api-design §2.1–§2.4, §3; entity model §5 |
| `src/ClassroomAgent.Web/Views/Teachers/Index.cshtml` | **new**: form, table, paging, empty, messages | test strategy §4; FR-003, FR-006, FR-008, FR-009 |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | `Teachers` constant | entity model §5 |
| `src/ClassroomAgent.Web/Security/TeacherListLog.cs` | **new**: events 5464, 5465 | FR-016; api-design §2.9 |
| `src/ClassroomAgent.Web/Security/TeachersTextKeys.cs` | **new**: translation key constants | FR-015; test strategy §5 |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | DI: `ITeacherListSource`, `GetTeacherListQuery` (scoped) | entity model §2, §5 |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | "Teachers" link, second | FR-011; OD-007 |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx`, `.en.resx` | 9 `Teachers.*` keys each | FR-015; test strategy §5 |

No test file was changed. No migration, no generated database file, no secret
and no IDE-local file is in the change set.

## 5. Validation Evidence

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | succeeded, 0 warnings, 0 errors |
| `dotnet test --solution ClassroomAgent.sln --no-build --filter-class "*TeacherList*" --filter-class "*CourseList*"` | 304 total, 304 passed, 0 failed, 0 skipped (36 s) |
| `dotnet test --solution ClassroomAgent.sln --no-build` | exit 0; 5411 total, 5411 passed, 0 failed, 0 skipped (6 m 01 s) |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0, no diagnostics |

Red phase baseline (test generation report §4): 5411 total, 5309 passed, 102
failed, all US-022. All 102 now pass, and no existing test regressed. The
course-list tests still pass after the shared-rule refactor.

**Delegation.** Every build, test and format run went to a `cheap-worker` agent
(three runs; the first was stopped before it ran because a file had not been
written yet). The code, view, translations and this report were written inline.
The change set is small and every file carries a contract decision: a shared rule,
a query shape, the markup hooks or the translation texts.

## 6. Configuration Changes

None. No `appsettings` or `Program.cs` change; the DI registration is in
`InstallationServices` (§4).

## 7. Deviations and Discovered Problems

- **Shared label key.** db-design §3.3 prefers sharing the label expression with
  the course list's teacher choice. It is now `ParticipantLabel.Projection` +
  `OrderByLabel`, used by both sources. `CourseListSource.GetTeacherChoiceAsync`
  changed in code but not in behaviour; the US-020 order tests and the US-022
  equality test pass.
- **T2 shape.** The course count is a correlated count projected *after*
  `Skip`/`Take`, so it is computed only for the page's 50 rows. It is still one
  command (`EveryRead_IsOneDatabaseCommand`). db-design §3.2 allows any shape
  that keeps one command with filter, count, order and paging in the database.
- **Translation texts** were chosen here (names fixed by test strategy §5):
  uk "Викладачі", "Ім'я", "Електронна пошта", "Кількість курсів", "Посилання",
  "Курси викладача", "Знайдено викладачів: {0}", "Жоден викладач не викладає
  курс обраного статусу."; en "Teachers", "Name", "Email", "Number of courses",
  "Link", "Teacher's courses", "Teachers found: {0}", "No teacher teaches a
  course of the chosen status."
- **Log event ids** 5464 and 5465 follow the course card's 5463.

## 8. Open Decisions

None touched or required. OD-010 (compile-only skeleton) is fulfilled: no
`NotImplementedException` remains in the US-022 types.

```yaml
result:
  verdict: PASS
  stage: IMPLEMENTATION
  story: US-022
  artifact_status: DRAFT
  artifacts:
    - docs/evidence/US-022-implementation-report.md
  next_stage: SECURITY_REVIEW
  loop_back_stage: null
  blocking_issues: []
  non_blocking_findings:
    - CourseListSource.GetTeacherChoiceAsync and GetCourseListQuery refactored onto shared helpers (ParticipantLabel, CourseListQueryRules); behaviour unchanged, US-020 tests green.
    - Translation texts chosen at implementation; key names as test strategy §5.
```
