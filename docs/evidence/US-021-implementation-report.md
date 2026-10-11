---
artifact_type: implementation_report
story: US-021
version: 1
status: DRAFT
created_at: 2026-10-11T17:03:23Z
updated_at: 2026-10-11T17:03:23Z
produced_by: dotnet-implementor
inputs:
  - path: docs/stories/US-021-course-detail.md
    version: null
  - path: docs/specifications/US-021-spec.md
    version: 1
  - path: docs/decisions/US-021-open-decisions.md
    version: 2
  - path: docs/designs/api/US-021-api-design.md
    version: 1
  - path: docs/designs/api/US-021-openapi.yaml
    version: 1
  - path: docs/designs/database/US-021-db-design.md
    version: 1
  - path: docs/designs/database/US-021-entity-model.md
    version: 1
  - path: docs/tests/US-021-test-strategy.md
    version: 1
  - path: docs/tests/US-021-ac-test-matrix.md
    version: 1
supersedes: null
attempt: 1
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-021 Implementation Report — Course detail with roster

## 1. Summary

The course card `GET /workspace/courses/{courseId}` is implemented as specified:
the course's details, current teachers (owner marked), current students and
leavers, each section sorted under `course_name_uk` with its count, links to the
journal, Meet reports and course list. The US-020 list now links each course
name to its card. A malformed or unknown id gets the host's ordinary 404 page.
The page is read-only (no guard, no write, no Google call), and the
`ViewCourses` policy protects it.

Build 0 warnings. Full suite 5 201/5 201 green. Format check clean. No schema
change, no migration, no new package, no new policy.

`security_sensitive: true` — the card shows personal data (names and emails of
students). Logging configuration also changed (§7 D-2).

## 2. Source Artifacts

As in the front matter: Story; Specification v1 (APPROVED at
`HUMAN_SPEC_APPROVAL`); Open Decisions v2 (OD-001 … OD-013 resolved); API design
and OpenAPI v1; DB design and entity model v1; test strategy and AC test matrix
v1.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Tests (class — method) | Status |
|---|---|---|---|
| AC-001 | `GetCourseCardQuery.ExecuteAsync` (details, `Given`, `LocalTime`); `CourseCardSource.GetCourseAsync`; `Views/Courses/Card.cshtml` details `<dl>` | Q `TheDetails_…`, `ABlankOptionalValue_IsNotGiven`; S `TheCourse_IsReadWithItsDisplayableValues`, `ACourseWithoutOptionalValues_HasNulls`; P `TheCard_ShowsTheDetails`, `AbsentDetails_AreOmitted_WithTheirLabels` | PASS |
| AC-002 | section split in `GetCourseCardQuery`; `GetMembersAsync`; three `<section>`s with `data-count` | Q `TheMembers_AreSplit…`; S `TheMembers_AreEveryMembershipOfTheCourse…`; P `TheSections_…` | PASS |
| AC-003 | `IsOwner` in SQL (`CourseCardSource`), kept only for current teachers (query); `data-owner` mark | Q `OnlyACurrentTeacher_…`, `AnOwnerWhoIsNotACurrentTeacher_…`; S `TheOwner_…`, `ACourseWhoseOwnerIsNotInTheRoster_…`; P owner tests | PASS |
| AC-004 | off-roster → `CourseCardLeaver` with role and `LocalDate`; leavers table | Q `ASubmitterOnlyStudent_…`; S `ASubmitterOnlyStudent_ComesBackOffTheRoster…`; P `TheLeavers_…` | PASS |
| AC-005 | `CoursePersonNameRule.NameOf`; email column; `Courses.Card.NoName` | Q `TheName_FollowsTheRule_InEverySection`, `APersonWithoutAnEmail_HasNoEmail`; P `APerson_…` | PASS |
| AC-006 | `GetMembersAsync` ORDER BY (label nulls last, collated label, email none first, collated email, id) | S `TheMembers_AreInTheUkrainianOrder_NamelessLast`, `EqualNames_…`; P `EachSection_IsInNameOrder_OnOnePage` | PASS |
| AC-007 | card links in `Card.cshtml`; list name link in `Index.cshtml`; return path in query | P `TheCard_Links…`, `TheCourseList_LinksEachCourseNameToItsCard`, `TheAddress_…`; Q `TheReturnPath_IsTheCardUrl`, `LeadingZeros_…` | PASS |
| AC-008 | `GetCourseCardQuery.Parse`; route `SignInRoutes.CourseCard`; `CoursesController.Card` → `NotFound()`; `CourseCardLog.Refused`; `RefusedValueRedaction` | Q `AMalformedId_…`, `AnUnknownId_…`, `TheLargestId_…`; S `NoCourse_IsNull`; P `AMalformedOrUnknownId_IsTheOrdinaryNotFoundPage`; L all three | PASS |
| AC-009 | `data-empty` messages, no table | Q `ACourseWithNobody_…`; P `EmptySections_…`, `OnlyTheEmptySection_…` | PASS |
| AC-010 | query depends on port + time zone only; source untracked | Q `TheQuery_DependsOnTheReadPortAndTheTimeZoneOnly`; RO (8 cases); S `Reading_WritesNothing` | PASS |
| AC-011 | `[Authorize(Policy = ViewCourses)]` on `CoursesController` (class level) | A (all) | PASS |
| AC-012 | 26 `Courses.Card.*` keys in uk and en; `CoursesTextKeys.Card*` | T (both) | PASS |
| AC-013 | no Google port anywhere in the path | P `TheCard_ShowsTheDetails`, RO | PASS |
| AC-014 | `LocalTime` / `LocalDate` in `SchoolTimeZone` | Q, P as matrix | PASS |
| AC-015 | collation `course_name_uk`, nameless last | S, P as matrix | PASS |
| AC-016 | Razor encoding; `.course-card-description { white-space: pre-line }` | P `TheDescription_IsText_WithItsLineBreak`, `GoogleData_IsHtmlEncoded` | PASS |
| AC-017 | two port calls, one command each | Q `ACard_IsTwoReads_…`; S `EachRead_IsOneDatabaseCommand_…` | PASS |

## 4. Change Set

Production — created:

| File | Trace |
|---|---|
| `Application/UseCases/CoursePersonNameRule.cs` | entity model §4 (shared name rule), FR-005 |
| `Web/Security/CourseCardLog.cs` | FR-013 (Information + Warning lines) |
| `Web/Configuration/RefusedValueRedaction.cs` | FR-011, FR-013, S-05 — see §7 D-2 |
| `Web/Views/Courses/Card.cshtml` | FR-002 … FR-010; test strategy §5 markup |

Production — skeleton completed (created at TEST_WRITING, OD-013):
`Application/UseCases/GetCourseCardQuery.cs` (FR-001 … FR-011, FR-014),
`Infrastructure/Persistence/CourseCardSource.cs` (db-design §2 Q1, Q2). The
records, request, outcome, result and DTOs under `Application/Models/` and the
port `ICourseCardSource` are unchanged from the skeleton (entity model §2, §3, §5).

Production — modified:

| File | Trace |
|---|---|
| `Application/UseCases/GetCourseListQuery.cs` | entity model §4: `NameOf` delegates to `CoursePersonNameRule` (behaviour unchanged, US-020 tests green) |
| `Application/Localization/SharedResource.uk.resx`, `.en.resx` | FR-016, test strategy §6 — 26 keys |
| `Web/Security/SignInRoutes.cs` | api-design §2.1, §2.2 — `CourseCard`, `CourseCardIdParameter` |
| `Web/Security/CoursesTextKeys.cs` | FR-016 — card key constants, `CardRole` |
| `Web/Controllers/CoursesController.cs` | FR-001, FR-011, FR-013 — `Card` action |
| `Web/Views/Courses/Index.cshtml` | FR-009, OD-009 — course name links to the card |
| `Web/Configuration/InstallationServices.cs` | supporting: DI of `ICourseCardSource`, `GetCourseCardQuery` |
| `Web/Configuration/InstallationLogging.cs` | supporting: registers `RefusedValueRedaction` (§7 D-2) |
| `Web/wwwroot/css/site.css` | FR-002, NFR-070 — description keeps line breaks and wraps |

Tests: the TEST_WRITING files (see the test generation report) are unchanged
by this stage. No test was edited.

No secret, generated database file, `.xlsx` or IDE-local config is in the change
set.

## 5. Validation Evidence

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | exit 0; 0 warnings, 0 errors |
| `dotnet test tests/ClassroomAgent.Tests --no-build --filter "FullyQualifiedName~CourseCard\|FullyQualifiedName~CourseList"` (first run) | 311 total, 308 passed, 3 failed — the D-1 and D-2 defects below, fixed |
| same filter (after the fixes) | exit 0; 311 total, 311 passed, 0 skipped |
| `dotnet test ClassroomAgent.sln --no-build` | exit 0; 5 201 total, 5 201 passed, 0 failed, 0 skipped; 5 m 48 s |
| `dotnet format ClassroomAgent.sln --verify-no-changes` | exit 0; no files listed |

Delegated: every build, test and format run went to a Sonnet `cheap-worker`
that reported results verbatim. The totals were checked against its raw logs.
All code, the view, the translations and this report were written inline. They
encode the API contract, the logging rule and the markup contract, and the
edits were small enough that handing them off would have cost more than writing
them.

## 6. Configuration Changes

No `appsettings` change. The Serilog pipeline gains one enricher,
`RefusedValueRedaction` (§7 D-2). Its only effect is that the card's
refusal event (id 5463) no longer carries `RequestPath`.

## 7. Deviations and Discovered Problems

- **D-1 Route constraint written without braces.** api-design §2.2 fixes
  `^[0-9]{1,19}$`. In a route template the braces must be doubled
  (`{{1,19}}`), and the test helper `HostEndpoint.Pattern` cannot normalise a
  template with doubled braces. The binding endpoint test then found no
  endpoint. The constraint is therefore written as
  `regex(^[[0-9]]+$):length(1,19)`. It matches exactly the same set of
  values, 1 to 19 ASCII digits, and keeps the `1,19` the test asserts. The
  behaviour matches the design; only the template's spelling differs.
- **D-2 Request path on the refusal log line.** Serilog picks up ASP.NET Core's
  request log scope, so every in-request event carries `RequestPath`. For a
  refused card that path holds the refused value, which contradicts FR-011,
  FR-013 and S-05 ("the value never reaches … a log line"), and the logging
  tests caught it. The fix is narrow: `RefusedValueRedaction` drops
  `RequestPath` from event 5463 only. Every other event keeps its request path
  unchanged. **For SECURITY_REVIEW:** the same host-wide behaviour already puts
  other path values into logs, for example Meet codes under
  `/workspace/meet-codes/{meetingCode}/…` and the meeting id. Those are outside
  this Story and were left as they are. A host-wide policy on `RequestPath`
  would need its own decision.
- The course list controller's class summary still describes the list's own
  400/404 page. The card's 404 behaviour is documented on the `Card` action.

## 8. Open Decisions

None touched and none newly required. OD-001 … OD-013 are resolved. No
`TODO`, `TBD` or `FIXME` in the changed code.
