---
artifact_type: test_strategy
story: US-020
version: 1
status: DRAFT
created_at: 2026-10-11T16:00:00Z
updated_at: 2026-10-11T16:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-020-course-list.md
    version: null
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
  - path: docs/designs/api/US-020-api-design.md
    version: 1
  - path: docs/designs/database/US-020-db-design.md
    version: 1
  - path: docs/designs/database/US-020-entity-model.md
    version: 1
  - path: docs/decisions/US-020-open-decisions.md
    version: 2
supersedes: null
---

# US-020 Test Strategy — Course list with filters and search

## 1. Scope

The tests cover the read-only page `GET /workspace/courses`, the query behind it
(`GetCourseListQuery`), the canonical URL rule (`CourseListUrl`), the read port
`ICourseListSource` against PostgreSQL, the migration `AddCourseListSupport`
(collation `course_name_uk`, index `ix_course_membership_current_teacher`), the
home page entry, authorization, read-only mode, translations and logging.

## 2. Levels

| Level | What | Where |
|---|---|---|
| Unit (TC-1) | VR-001 … VR-005, 400 before 404, defaults, return path, filter handed to the port, row mapping, Kyiv time, paging and the page past the end, empty states, the FR-007 name rule, I-3 emails, bounded calls, the query's dependencies | `Application/UseCases/CourseList{RequestValidation,Query,PersonName,Url}Tests.cs`, over `FakeCourseListSource` |
| Integration (TC-2) | Q1–Q6 on real PostgreSQL: state, current-teacher and search filters, Ukrainian case folding, literal `%` `_` `\`, Ukrainian order with Cyrillic before Latin, section/id ties, database paging, one command per read, nothing written | `Infrastructure/Persistence/CourseListSourceTests.cs` |
| Schema (PC-2, PC-7) | migration name, ICU deterministic `uk-UA` collation on no column, its folding and order, the partial index; the existing "exactly these indexes" test gains the new index | `Infrastructure/Persistence/CourseListSchemaTests.cs`, `Web/Persistence/CourseMembershipSchemaTests.cs` |
| Host | the page end to end: row columns, filters, choice, paging links, journal / Meet reports links, no Classroom link, address and switcher, empty states, home page, 400/404 pages, HTML encoding, `no-store` | `Web/Pages/CourseListPageTests.cs` |
| Security (TC-5) | Admin ✔, Dean ✔, anonymous `302 /sign-in`, temporary-password Dean `302`, endpoint protected with `ViewCourses`, policy registered | `Web/Security/CourseListAuthorizationTests.cs` |
| Read-only (TC-5, AC-014) | use case and page in all three BR-025 causes and outside them: list shown, nothing written, no Google call | `Web/UseCases/CourseListReadOnlyTests.cs` |
| Localization (NFR-073) | every key in uk and en; page in both languages; status choice order and labels; Google data unchanged; "teacher without a name" | `Web/Localization/CourseListTranslationTests.cs` |
| Logging (SC-10, FR-018) | Information line with the total and no personal data; search text never logged; Warning naming the parameter, never the value | `Web/Logging/CourseListLoggingTests.cs` |

## 3. Scenarios

**Positive.** The default ACTIVE list; each state and "all"; owner and
co-teacher both filtering; search by name and section; filters combined; 120
courses over three pages; links into the journal and the Meet reports.

**Negative.** Former teacher (off roster) and student not teachers; unknown
teacher → 404; malformed status, teacher id, search, page → 400; unknown
parameter not echoed; anonymous and temporary-password Dean refused; no
Classroom link even when `alternate_link` is stored.

**Boundary.** Search of 100 vs 101 characters after trimming; page 1 and
100 000 vs 0 and 100 001; the largest 64-bit id; 50 vs 51 courses; page past
the last; Kyiv time on both sides of summer time (31 March 22:30 UTC → 1 April
01:30; 31 October 21:30 UTC → 23:30).

**Validation.** Case-insensitive untrimmed status tokens; digits-only ids and
pages (non-ASCII digits refused); repeated parameters; `q` rule order repeated
→ too long → control character; every failing parameter reported in order;
existence checked only when the shape passes.

**Security.** Rejected values never in the model, page, links, return path or
log; no name or email in any URL; HTML encoding of Google data; no write, no
audit row, no Google call; the query takes no guard, scope, unit of work or
Google port.

**Persistence.** Q1–Q6 against migrations-built PostgreSQL; one database
command per read (AC-020 — a `DbCommandInterceptor` counts them); fingerprint of
the US-014 tables unchanged.

## 4. Fixtures and markup

- `FakeCourseListSource` — in-memory port applying db-design §3 (filters, order,
  label key), recording every call and its arguments.
- `CourseListTestData` — URLs, parameter names, the closed translation-key list
  and the markup hooks.
- `CourseListHostExtensions` — SQL seeding (`SeedCourseListAsync`: Фізика,
  Хімія, Історія України, Алгебра; Шевченко, Українка, Франко (former), Коваль;
  students on and off the roster), the real `CourseListSource` over the host's
  database, the write fingerprint and HTML readers.
- **Markup hooks IMPLEMENTATION renders** (binding):
  `<form id="course-list-form" method="get" action="/workspace/courses">`;
  `<select name="status">` with option values `active` … `all` in FR-004 order
  and the effective one `selected`; `<select name="teacherId">` whose first
  option has value `""` and the "all teachers" text, then one option per
  teacher (value = participant id, label = FR-007 name, the I-3 email in
  parentheses when set); `<input name="q">`; one
  `<tr data-course-id="{id}">` per listed course in list order; the total in
  the element `id="course-list-total"`; paging links inside
  `<nav id="course-list-paging">` (none when there is one page); an empty state
  as the only text of the element `id="course-list-empty"` with no `<table>`;
  validation messages as elements with class `form-error`, each holding only
  its message. The update time is rendered with the UI culture's short date
  pattern and `HH:mm`, as the Meet reports render times.

## 5. Interpretations under test

I-2 (full email, `FullName` unused), I-3, I-4, I-5, I-7 (Cyrillic before Latin
as db-design §2.2 records), I-8, I-9.

## 6. Translation keys

The closed list is `CourseListTestData.Keys.All` (34 keys under `Courses.*`).
IMPLEMENTATION adds exactly these keys to both resource files. A key the page
needs but the list lacks is a test defect, to be raised, not silently added.

## 7. Excluded, with reason

- Query plans and timing: no performance assertion; bounded reading is proved
  by one command per port call and a fixed number of calls per request.
- The journal and Meet reports pages with a `courseId`: unchanged (OD-007),
  already covered by US-025 / US-033 tests.
- Phone layout (NFR-070): visual, not automated.

## 8. Known limitations

- The ICU ordering is asserted on PostgreSQL 17 Alpine (the Testcontainers
  image); a server built without ICU fails the migration (db-design §2.3), which
  no test exercises.
- `TheSearchText_IsNeverLogged` waits for any Information event after the
  request; other Information lines in that window could let it check before the
  page's line is written. The Warning/value tests do not share this weakness.

## 9. Open Decisions

OD-012 (compile-only skeleton, a) — resolved 2026-10-11. None open.
