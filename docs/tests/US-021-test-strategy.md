---
artifact_type: test_strategy
story: US-021
version: 1
status: DRAFT
created_at: 2026-10-11T16:30:00Z
updated_at: 2026-10-11T16:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-021-course-detail.md
    version: null
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
supersedes: null
---

# US-021 Test Strategy — Course detail with roster

## 1. Scope

The course card `GET /workspace/courses/{courseId}` (spec FR-001 … FR-016) and
the US-020 course list's new link from each course name to its card (FR-009).
Seventeen criteria: the Story's AC-001 … AC-013 and the derived AC-014 …
AC-017. No schema change, no migration, no audit action, no JSON endpoint, no
Google call — so no migration test, no audit test and no API-6 error-body test
apply (the only error is the host's ordinary `404` page).

## 2. Test levels

| Level | Class (namespace `ClassroomAgent.Tests`) | What it proves |
|---|---|---|
| Unit (TC-1) | `Application.UseCases.CourseCardQueryTests` | parse of the path value (VR-001), malformed/unknown outcome, nothing read before the parse, details with blank values dropped, school-zone times, the three sections in the port's order, the owner mark on teachers only, last-seen date, the name rule, return path, two reads per card, dependencies (no guard, no Google) |
| Integration, PostgreSQL (TC-2) | `Infrastructure.Persistence.CourseCardSourceTests` | Q1 and Q2 of db-design §2: values, null for no course, every membership of one course only, Ukrainian order with nameless last and email/id tie-breaks, `is_owner` in SQL, submitter-only student, one command per read for 3 and 350 members, nothing written |
| Host (`WebApplicationFactory`, PostgreSQL) | `Web.Pages.CourseCardPageTests` | the rendered card through the markup hooks of §5: details and omissions, sections and counts, owner mark, leavers, name and email, order, links, list → card link, address and switcher, 404 page, empty sections, description as text, HTML encoding |
| Security (TC-5) | `Web.Security.CourseCardAuthorizationTests` | Admin and Dean allowed; anonymous → sign-in; Dean on the forced password change → change page; the endpoint is protected by `ViewCourses` with the `1,19` digit constraint |
| Read-only (TC-5) | `Web.UseCases.CourseCardReadOnlyTests` | the use case resolved from the host and the page work in all three BR-025 causes and outside them, write nothing, call no Google port |
| Localization (TC-8) | `Web.Localization.CourseCardTranslationTests` | every new key in uk and en; the page in both languages; Google data unchanged; dates in the UI culture |
| Logging (SC-10) | `Web.Logging.CourseCardLoggingTests` | Information line with the counts and no course or person data; Warning naming `courseId` and the outcome, never the value; a value the route does not match never logged |

## 3. Scenarios

**Positive.** A seeded course with every detail, two current teachers (owner
among them), seven current students (the five of AC-015, one email-only, one
nameless) and three leavers (former teacher, former student, submitter-only)
is shown completely and in order, for Admin and Dean, in uk and en, in every
read-only cause.

**Negative.** Malformed ids — empty, signs, spaces, decimal point, exponent,
non-ASCII digits, zero, overflow — are `CourseMalformed` with no read (unit) and
`404` (host); a well-formed unknown id and `Int64.MaxValue` are `CourseUnknown`
with only Q1 read; values the route does not match are the catch-all `404`;
anonymous and restricted sessions are redirected; the Google link, teacher
folder, calendar id and Google ids never reach the page; leavers never appear
among current members; an owner who is not a current teacher marks no one.

**Boundary.** `Int64.MaxValue` (well formed) vs `Int64.MaxValue + 1` and a
19-digit `999…9` (malformed, route-matching) vs 20 digits (no route); leading
zeros (I-6); 22:30 UTC on 31 March → 1 April in Kyiv for a course time and a
leaver date (AC-014); creation seconds truncated; blank values `""`, `" "`,
`"\t\n "` (I-1); 3 vs 300 + 50 members for bounded reads (AC-017).

**Validation.** VR-001 in `Application` (unit); route constraint at the host
(page and authorization tests); VR-002 HTML encoding of every Google string,
the description included (AC-016).

**Security.** S-01 … S-12 as mapped in the matrix: deny by default, allowed and
forbidden roles, no write and no Google call in any mode, no echo of the
requested value in page, switcher or log, no personal data or Google id in a
URL or log line.

**Persistence.** Q1/Q2 shape, ordering under `course_name_uk`, `is_owner`
computed in SQL, one command per call, nothing written. No schema test: the
design adds nothing (db-design §1).

## 4. Fixtures

- `TestInfrastructure/CourseCardTestData` — card URL builder, malformed and
  non-matching values, markup hooks, `data-field` names, translation keys.
- `TestInfrastructure/FakeCourseCardSource` — in-memory port recording every call.
- `TestInfrastructure/CourseCardHostExtensions` — SQL seed of the card world,
  readers of the markup hooks, the real `CourseCardSource` over the host's
  database.
- `TestInfrastructure/CourseRows.InsertCourseAsync` gains optional `room` and
  `descriptionHeading` (supporting change; existing callers unchanged).
- Shared: `PostgreSqlFixture` (Testcontainers), `InstallationTestHost`,
  `JournalHostExtensions.StartAsync` actors, `ReadOnlyModeHost`, `HostLogs`,
  `HostEndpoint`, `UiLanguageTestData`. All Google ports substituted; every
  name and email synthetic (TC-4).

## 5. Markup contract (binding on IMPLEMENTATION)

| Hook | Meaning |
|---|---|
| `data-field="name\|section\|status\|room\|description-heading\|description\|created-at\|updated-at"` | the element holding that detail's value only; an omitted field has neither the element nor its label |
| `<section id="course-card-teachers">`, `course-card-students`, `course-card-leavers` | the three sections, in this order |
| `data-count` | inside each section, the element holding its count |
| `<tr data-person>` | one row per person, in section order |
| `data-owner` | inside the owner's teacher row, the element holding the translated mark |
| `data-empty` | an empty section's message element; the section then has no `<table>` |

The language switcher's return path is the existing `ReturnPath` hidden input.

## 6. Translation keys (binding on IMPLEMENTATION)

`Courses.Card.Title`, `Courses.Card.Field.{Section,Status,Room,DescriptionHeading,Description,CreatedAt,UpdatedAt}`,
`Courses.Card.Section.{Teachers,Students,Leavers}`, `Courses.Card.Count`,
`Courses.Card.Column.{Name,Email,Role,LastSeen}`, `Courses.Card.Owner`,
`Courses.Card.Role.{Teacher,Student}`, `Courses.Card.NoName`,
`Courses.Card.Empty.{Teachers,Students,Leavers}`,
`Courses.Card.Link.{Journal,MeetReports,Courses}` — 26 keys, in uk and en with
different texts. Status names reuse the US-020 `Courses.Status.*` keys
(openapi). Names of keys the openapi left open were fixed here.

## 7. Excluded scenarios

- Migration and schema tests — no schema change (db-design §1).
- Audit — viewing is not audited (spec FR-015, S-11).
- API-6 JSON error body — no `/api/v1` endpoint (I-8); the `404` is the host's
  page, asserted by its text.
- Antiforgery — `GET` only (openapi).
- Phone layout (NFR-070) — visual; not asserted by automated tests.
- The visual line break of the description (`white-space`) — the tests prove
  the line break is kept in the text and no markup is inserted; the CSS that
  shows it is not asserted.
- The forced-password-change refusal for an anonymous non-matching value — the
  route is not matched before authentication, as for any unknown path
  (api-design §4).

## 8. Known limitations

- Ordering is proved against PostgreSQL ICU (`course_name_uk`); the unit tests
  only prove that the query keeps the port's order.
- Tests for values the route does not match pass before implementation (no
  route exists yet, so they already get the catch-all `404`); they guard the
  constraint after implementation.
- The `Information` log assertion waits for an event carrying the counts 7 and 3
  as property values; the exact message template is IMPLEMENTATION's.

## 9. Open Decisions affecting testing

OD-013 (compile-only skeleton) — raised here, resolved (a) by the Owner,
2026-10-11. None open.
