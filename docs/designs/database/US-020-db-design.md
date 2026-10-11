---
artifact_type: database_design
story: US-020
version: 1
status: DRAFT
created_at: 2026-10-11T14:40:00Z
updated_at: 2026-10-11T14:40:00Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-020-spec.md
    version: 1
  - path: docs/designs/api/US-020-api-design.md
    version: 1
  - path: docs/designs/api/US-020-openapi.yaml
    version: 1
  - path: docs/decisions/US-020-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 88
supersedes: null
---

# US-020 Database Design — Course list with filters and search

Delegated: nothing to subagents. This stage read `CourseConfiguration`,
`CourseMembershipConfiguration`, `ClassroomParticipantConfiguration`, the
`Course` entity, the migration list and the Infrastructure package references
directly, and ran two empirical checks:

- a throw-away `postgres:17-alpine` container (the Testcontainers image of
  `PostgreSqlFixture`) — ICU is present (908 ICU collations), `CREATE COLLATION
  … (provider = icu, locale = 'uk-UA')` succeeds, and §2.2's ordering and case
  folding behave as stated there;
- a scratch compile of the port and read-model signatures of the entity model
  (nullable enabled, warnings as errors) — 0 errors.

## 1. Summary

| Change | Kind |
|---|---|
| Collation `course_name_uk` (ICU, `uk-UA`, deterministic) | **new** database object |
| Index `ix_course_membership_current_teacher` | **new** partial index |
| Tables, columns, constraints | **none** — no table, no column, no audit action (spec FR-016) |

One migration, `AddCourseListSupport`, creates both and drops both in `Down`.
No entity changes; no `EnsureCreated()` (PC-2). No database extension and no
NuGet package.

## 2. The collation `course_name_uk`

### 2.1 Definition

```
modelBuilder.HasCollation("course_name_uk", locale: "uk-UA", provider: "icu", deterministic: true);
```

Declared in `ClassroomAgentDbContext.OnModelCreating` (Npgsql's model-level
collation API), so the migration's `CREATE COLLATION` / `DROP COLLATION` come
from the model and the snapshot carries it. It is **not** applied to any column:
`course.name` and `course.section` keep the database default collation, so the
upsert key, existing queries and existing indexes are untouched. The collation is
used only inside the course-list queries (§3), by
`EF.Functions.Collate(..., "course_name_uk")`.

Deterministic, because PostgreSQL refuses `LIKE` / pattern matching under a
non-deterministic collation, and because ties must be broken by the explicit
`section`, `id` keys (spec I-7), not by an equality the collation invents.

### 2.2 Verified behaviour (postgres:17-alpine)

Ordering of a mixed sample under `course_name_uk`:

```
10-А | Алгебра | Біологія | Географія | Ґрунтознавство | Економіка | Єдність |
Иван | Інформатика | Історія | Їжак | Яблуко | algebra | Physics
```

- `ґ` after `г`, `є` after `е`, `і` and `ї` after `и` (spec FR-008); AC-019's
  five names come out Алгебра, Географія, Ґрунтознавство, Інформатика, Історія.
- **Cyrillic sorts before Latin** (CLDR's Ukrainian tailoring reorders the
  Cyrillic script first); digits come first; Latin letters keep Latin order
  among themselves, case-insensitively at the primary level. The spec requires
  Latin letters "in Latin order" and does not fix Latin-vs-Cyrillic precedence;
  Cyrillic first suits a Ukrainian school's mostly Ukrainian names. Recorded so
  TEST_WRITING asserts it rather than guessing.
- `lower('ІСТОРІЯ ЇЖАК ҐАНОК ЄВА' COLLATE course_name_uk)` →
  `історія їжак ґанок єва`; under `"C"` the same call leaves the text upper-case.
  Case folding of Cyrillic therefore must not depend on the database's default
  ctype — §3.2 always lowers under `course_name_uk`.

### 2.3 Deployment prerequisite

The installation's PostgreSQL server must be built with ICU. The official
`postgres` images (Debian and Alpine, version 16 and later) are; the migration
fails loudly at `CREATE COLLATION` on a server without ICU rather than sorting
wrongly. DC: noted for the deployment runbook; no configuration value is added.

## 3. Queries of the course list

`ICourseListSource` (entity model §2) runs these, read-only, with
`AsNoTracking()`, no transaction, no write. Bounded: at most six round trips per
request whatever the data size (spec FR-016, AC-020).

Let **current teacher** mean `course_membership.role = 'teacher' AND on_roster =
true`; **current student** likewise with `'student'`.

| # | Query | When | Serves |
|---|---|---|---|
| Q1 | teacher choice — participants with ≥ 1 current-teacher membership | every answer (`200`, `400`, `404`) | FR-005 |
| Q2 | count of courses matching the filter | query valid and teacher known | FR-008 total |
| Q3 | any course stored | only when Q2 = 0 | FR-011 |
| Q4 | one page of matching courses | Q2 > 0 | FR-003, FR-008 |
| Q5 | current teachers of the page's courses | Q4 non-empty | FR-003 |
| Q6 | current-student counts of the page's courses | Q4 non-empty | FR-003 |

The page number is clamped by `Application` between Q2 and Q4
(`page = min(requested, pageCount)`), so the page past the end costs no extra
query.

### 3.1 Filter (Q2, Q4)

- **State** — `course.course_state = @state` (stored lower-case code); omitted
  for `all`.
- **Teacher** — `EXISTS (SELECT 1 FROM course_membership m WHERE m.course_id =
  c.id AND m.participant_id = @teacher AND m.role = 'teacher' AND m.on_roster)`.
- **Search** — §3.2.

All three combine with `AND` (AC-007).

### 3.2 Search (Q2, Q4)

```
strpos(lower(c.name    COLLATE course_name_uk), lower(@q COLLATE course_name_uk)) > 0
OR (c.section IS NOT NULL AND
 strpos(lower(c.section COLLATE course_name_uk), lower(@q COLLATE course_name_uk)) > 0)
```

- `@q` is the trimmed text, bound as a **parameter** — never concatenated into
  SQL (S-09).
- **Literal by construction**: `strpos` has no wildcard or escape character, so
  `%`, `_` and `\` match themselves (spec FR-006, I-4; AC-019 "%" case). The
  implementation may express it in LINQ as
  `EF.Functions.Collate(c.Name, "course_name_uk").ToLower().Contains(<term lowered the same way>)`
  only if the generated SQL is `strpos`, or a `LIKE` whose pattern the provider
  escapes; the AC-019 `%` test is the guard either way.
- Both sides are lowered under `course_name_uk` (§2.2), so `ІСТ` finds
  `Історія` and `Ї`/`ї`, `Є`/`є`, `І`/`і`, `Ґ`/`ґ` fold. No other folding
  (spec FR-006).

### 3.3 Order (Q4)

```
ORDER BY c.name COLLATE course_name_uk,
         c.section COLLATE course_name_uk NULLS FIRST,
         c.id
OFFSET (@page - 1) * 50 LIMIT 50
```

Total and stable between pages (spec FR-008, I-7); the same for every UI
language.

### 3.4 Teacher ordering (Q1, Q5)

Teacher labels use the same alphabet (spec FR-008). The database orders by the
**label key** — the FR-007 rule expressed in SQL:

```
COALESCE(
  NULLIF(concat_ws(' ', NULLIF(p.surname, ''), NULLIF(p.given_name, '')), ''),
  NULLIF(p.email, '')
) COLLATE course_name_uk  NULLS LAST,
p.id
```

Q5 orders by `m.course_id` first, then the same key and `p.id`. Participants
with neither name nor email (`NoName`) come last. The entity model's
`PersonName` rule in `Application` treats an empty string as absent, exactly as
the `NULLIF`s do, so the order and the rendered labels agree.

## 4. Indexes

PC-7 / NFR-003 require an explicit index for every query lookup. Per query:

| Query | Access path | Index |
|---|---|---|
| Q1 | current-teacher memberships → distinct participants → `classroom_participant` by id | **new** `ix_course_membership_current_teacher`; `pk_classroom_participant` |
| teacher filter (Q2, Q4 `EXISTS`) | (course, participant) | existing `uq_course_membership_course_participant`; for a selective teacher the planner can drive from the new index (participant → its courses) |
| Q5, Q6 | memberships of ≤ 50 courses | existing `uq_course_membership_course_participant` (`course_id` prefix) |
| state, search, order (Q2, Q4) | `course` | **none new** — §4.2 |
| Q3 | `course` | `pk_course` (any row) |

### 4.1 `ix_course_membership_current_teacher` (new)

```
builder.HasIndex(m => new { m.ParticipantId, m.CourseId })
    .HasFilter("role = 'teacher' AND on_roster = true")
    .HasDatabaseName("ix_course_membership_current_teacher");
```

In `CourseMembershipConfiguration`. Partial on current teachers: a school's
memberships are overwhelmingly students (tens per course against one to three
teachers), so Q1 — which runs on **every** request, including `400` and `404` —
reads only teacher rows instead of the whole table, and can be answered as an
index-only scan. `(participant_id, course_id)` also serves the teacher filter
when driven from the participant. Fixed-width `bigint` keys; no size risk.

Existing `ix_course_membership_participant_id` is kept: the leaver purge and
US-014's sync use it for all roles.

### 4.2 No new index on `course` — rationale

- **Search** is a substring match; no B-tree can serve it. A trigram GIN index
  would need the `pg_trgm` extension — a new database dependency for every
  installation — for a table the spec sizes at a few thousand rows (spec §9). A
  sequential scan of a few thousand rows per request is milliseconds. Not
  adopted; revisit only if a school's course count grows by orders of magnitude.
- **Order** by `name COLLATE course_name_uk`: an index on it would let the
  default view (ACTIVE, page 1) skip the sort, but `name` is up to 750
  characters — up to 3 000 bytes in UTF-8 — which can exceed PostgreSQL's B-tree
  entry limit (~2 700 bytes). One such name would then fail the **whole
  synchronization** of the school at insert time. The sort is a top-N heap of
  `offset + 50` rows over a few thousand candidates; cheap. Not adopted.
- **State**: five values, low selectivity; the planner would not use a plain
  index on `course_state` over a few thousand rows. Not adopted.

This is the "explicit index choice against US-020's real queries" US-014
db-design §3.4 deferred to this Story; the prototype's `idx_courses_state` /
`idx_courses_owner` / `idx_courses_update` are not carried (the list never
filters by owner or update time).

## 5. Migration `AddCourseListSupport`

Generated by `dotnet ef migrations add AddCourseListSupport`, from the model:

- `Up`: `CREATE COLLATION course_name_uk (provider = icu, locale = 'uk-UA',
  deterministic = true)` (Npgsql emits it from `HasCollation`); `CREATE INDEX
  ix_course_membership_current_teacher ON course_membership (participant_id,
  course_id) WHERE role = 'teacher' AND on_roster = true`.
- `Down`: drop the index, then the collation.
- No data change; no raw `migrationBuilder.Sql`. The model snapshot changes in
  the same commit (PC-2).

## 6. Sensitive data and security

- Reads only. No write, no audit row (SC-11, S-12), no secret.
- Participant names and emails leave the database only into the DTOs of the
  page; no query result is logged (SC-10, S-08). The search text is a bound
  parameter and is not logged (I-4).
- No per-Dean or roster-based row filter exists or is added (Hard Stop, S-03).
- `AlternateLink` and every column the row does not show are not selected
  (OD-008; minimal projection).

## 7. Testing notes for TEST_WRITING

- Integration tests run on the Testcontainers PostgreSQL with the migration
  applied (TC-2) — the collation and the partial index exist only through it.
- AC-019: order of the five names; `ІСТ` finds `Історія`; `%` finds only a name
  or section containing `%`. Add a Latin name to pin §2.2's Cyrillic-first rule.
- AC-020: assert the per-request query count is identical for page 1 and page 3
  of 120 courses (Q1, Q2, Q4, Q5, Q6 → 5; Q3 only on an empty result).
- Assert `ix_course_membership_current_teacher` and `course_name_uk` exist after
  migration (schema test style of earlier Stories).
