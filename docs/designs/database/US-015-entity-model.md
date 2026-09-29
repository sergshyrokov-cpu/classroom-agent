---
artifact_type: entity_model
story: US-015
version: 1
status: DRAFT
created_at: 2026-09-28T13:10:02Z
updated_at: 2026-09-28T13:10:02Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/designs/api/US-015-api-design.md
    version: 2
  - path: docs/designs/database/US-015-db-design.md
    version: 1
  - path: docs/decisions/US-015-open-decisions.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-015 Entity Model — Sync coursework and submissions

Two entities, two enums, the extension of one port with its Application models,
two repository ports, and one narrow settings record.

**Governing principle, inherited from `SyncState` and `CourseMembership`:** the
entity holds no clock and no policy. The run's single instant (I-1) and the
retention period arrive as arguments.

## 1. `CourseWork` — new entity

`Domain/Entities/CourseWork.cs`.

### 1.1 Properties

| Property | Type | Notes |
|---|---|---|
| `Id` | `long` | surrogate, generated (PC-3) |
| `CourseId` | `long` | the owning `Course` |
| `GoogleId` | `string` | Classroom's id for the item |
| `Resource` | `CourseWorkResource` | which Classroom resource (§2) |
| `Title` | `string` | truncated at `MaxTitleLength` |
| `ItemDate` | `DateTimeOffset` | the one date of the FR-008 cascade |
| `DueAt` | `DateTimeOffset?` | absent unless Google set both date and time (db-design §3.4) |
| `MaxPoints` | `decimal?` | absent means ungraded work |
| `CreationTime` | `DateTimeOffset?` | Google's, as given |
| `UpdateTime` | `DateTimeOffset?` | Google's, as given; PC-11 reads it |
| `CreatedAt`, `UpdatedAt` | `DateTimeOffset` | PC-6, interceptor-stamped |

`public const int MaxTitleLength = 3000;` — public, so the test asserting
truncation names the same bound the configuration does (the US-014 `Course`
precedent).

### 1.2 Behaviour

- **`static CourseWork Import(long courseId, CourseWorkDetails details)`** —
  creates the row, truncating `Title` at `MaxTitleLength` rather than refusing
  (VR-002), and refusing outright only what §3.3 of the db-design names
  unimportable: a missing Google id, a missing resource, or no date at all from
  the cascade.
- **`void UpdateFrom(CourseWorkDetails details)`** — the upsert half. Applies the
  same truncation. It exists because FR-010 keeps the surrogate identity of a row
  Google changed, so anything referring to it still does.
- **`CourseWorkKind Kind`** — a **computed property, never a column** (PC-3,
  §3 v32): `Material` when the resource is `CourseWorkMaterial`, otherwise
  `GradedWork` when `MaxPoints` has a value and `UngradedWork` when it does not.
  A teacher adding points later therefore changes the kind without changing the
  row's identity.
- **No `Delete`**. Nothing in this Story deletes a row; the purge is US-037.

`CourseWorkDetails` is a **`Domain`** type, not an `Application` one — the
mistake US-014 made and corrected at its DB_DESIGN attempt 3: `Domain` references
nothing (AD-3), so a `Domain` factory cannot take an `Application` parameter.

## 2. `CourseWorkResource` — new enum

`Domain/Enums/CourseWorkResource.cs`: `CourseWork`, `CourseWorkMaterial`.

Exactly two members, no `Unknown`: the adapter reads two named Classroom
endpoints and knows which one answered, so an unrecognised value cannot arise —
unlike a course state or a submission state, which arrive inside a payload.

Stored through a value converter as `course_work` / `course_work_material`, the
`SyncStateConfiguration` pattern.

## 3. `CourseWorkKind` — new enum (computed, never stored)

`Domain/Enums/CourseWorkKind.cs`: `GradedWork`, `UngradedWork`, `Material` —
BR-052's three kinds, already named in `package-map.md` as "computed from the
Classroom resource and maximum points, never stored".

There is no column, no converter and no constraint for it. A test asserts the
three derivations, and a schema test asserts the **absence** of a kind column, so
a later Story cannot quietly persist it.

## 4. `Submission` — new entity

`Domain/Entities/Submission.cs`.

### 4.1 Properties

| Property | Type | Notes |
|---|---|---|
| `Id` | `long` | surrogate, generated |
| `CourseWorkId` | `long` | the owning `CourseWork` |
| `ParticipantId` | `long` | the `ClassroomParticipant`, matched by Google `userId` (I-7) |
| `GoogleId` | `string` | Classroom's submission id |
| `State` | `SubmissionState` | closed vocabulary plus the marker (§5) |
| `RawState` | `string?` | the string Google sent, **only** when `State` is `Unrecognised` |
| `AssignedGrade` | `decimal?` | raw points as given |
| `DraftGrade` | `decimal?` | raw points as given |
| `TurnedInAt` | `DateTimeOffset?` | the latest transition to `TURNED_IN`; absent when the history carries none |
| `Late` | `bool` | Google's flag, `false` when Google omits it |
| `UpdateTime` | `DateTimeOffset?` | Google's, per OD-007 |
| `CreatedAt`, `UpdatedAt` | `DateTimeOffset` | PC-6 |

### 4.2 Behaviour — where OD-005 actually lives

- **`static Submission Import(long courseWorkId, long participantId,
  SubmissionDetails details)`** and **`void UpdateFrom(SubmissionDetails
  details)`**.
- The pair `State` / `RawState` is set **only** through
  `SetState(SubmissionState state, string? rawState)`, which enforces the
  biconditional the database also checks (db-design §4.2): `Unrecognised`
  **requires** a raw string, and any other state **forbids** one. Two places
  enforce it because one of them — the constraint — can only fail a write, while
  the entity can refuse to construct the contradiction at all.
- **Grades are stored as given** (VR-007, I-8): no conversion to a school scale,
  no ceiling against the coursework's maximum, no discarding a grade on ungraded
  work. Those are report decisions (BR-056) or Google's own.
- **No `Delete`**, and **no history**: the entity never receives the submission
  history, only the one instant extracted from it (§6).

## 5. `SubmissionState` — new enum

`Domain/Enums/SubmissionState.cs`, **seven** members:

| Member | Code | Source |
|---|---|---|
| `New` | `new` | Classroom |
| `Created` | `created` | Classroom, BR-056 |
| `TurnedIn` | `turned_in` | Classroom, BR-056 |
| `Returned` | `returned` | Classroom, BR-056 |
| `ReclaimedByStudent` | `reclaimed_by_student` | Classroom, BR-056 |
| `StudentEditedAfterTurnIn` | `student_edited_after_turn_in` | BR-056 (may not exist in the API — OD-011) |
| `Unrecognised` | `unrecognised` | OD-005's marker, not a Classroom value |

**This enum deliberately has a member US-014's `CourseState` deliberately does
not.** `CourseState` has no `Unknown`, because an unrecognised course is skipped
before it can become an entity (US-014 OD-010). Here the opposite decision was
taken for a stated reason: skipping a submission would make BR-056 read the gap
as «не сдано» and state something false about a child's work (OD-005). The two
enums differ because the failure modes differ, and a future reader should not
"fix" the inconsistency.

Ordering note, learned from US-014's D-5: the member order is the one above and
tests must not assume it matches the alphabetical order of the stored codes.

## 6. `IClassroomReader` — the port gains two members

`Application/Ports/IClassroomReader.cs`, implemented in `Infrastructure/Google`.
It already carries `IGoogleDataPort`, so US-007 FR-007 keeps binding the use case
that holds it — extending the existing port preserves that automatically, where a
new port would have to carry the marker or `ReadOnlyEnforcementTests` would fail.

Both new members take the impersonation address first, exactly as the US-014
members do (BR-015):

- **`Task<CourseWorkPage> ReadCourseWorkAsync(string impersonationUser, string
  courseGoogleId, CancellationToken)`** — **both** Classroom resources of one
  course, each fully paged (FR-003, VR-005). One call returning both keeps the
  pair atomic for the caller in the same way US-014's `ReadRosterAsync` did for
  the two rosters: a returned value means both reads succeeded, an exception means
  the course's items are unknown.
- **`Task<IReadOnlyList<SubmissionSnapshot>> ReadSubmissionsAsync(string
  impersonationUser, string courseGoogleId, CancellationToken)`** — the whole
  course's submissions in one paged read, `courseWorkId = "-"` (OD-002). Each
  snapshot carries its own `courseWorkGoogleId`, which is how the use case
  attributes it.

Application models beside it (`Application/Models`):

- **`CourseWorkPage`** — the items of both resources, each as a
  `CourseWorkSnapshot`.
- **`CourseWorkSnapshot`** — the Google id, the resource, and the `Domain`
  `CourseWorkDetails`. The cascade of FR-008 is applied **in the adapter**, because
  it reduces four Google fields to one and those fields are Google's shapes; only
  the resulting instant crosses the boundary.
- **`SubmissionSnapshot`** — the Google submission id, its `courseWorkGoogleId`,
  the Google `userId`, the **state as the string Google sent**, the two grades,
  `late`, Google's `updateTime`, and the single `turnedInAt` instant.

**Two things the port deliberately does not expose**, both of which keep a rule
structural rather than remembered:

1. **The submission history never crosses the port.** The adapter reduces it to
   the latest `TURNED_IN` instant and discards the rest in memory (FR-009,
   PC-13). Since no Application or Domain type can hold it, "the history is never
   stored" cannot be violated by a later change in the use case.
2. **The state stays a string** at the boundary, exactly as US-014 kept a course
   state a string, so that VR-004's classification — recognised code or
   `Unrecognised` plus the raw value — happens in the use case where OD-005's
   Warning line is written, never in the adapter.

The adapter continues the `GoogleAccessProbe` pattern: one injected
`HttpMessageHandler` so tests run offline (TC-4) and nothing but Google is
reachable (SC-13), the client library's own retry switched **off** (US-017 owns
retry), the key resolved from `ISecretStore` per request, the technical account
impersonated (BR-015), and **no Google error text logged or returned** (SC-10).

## 7. Repository ports — new

In `Application/Ports`, implemented in `Infrastructure/Persistence.Repositories`.
They stage changes and never call `SaveChangesAsync`; the use case commits (AD-7).

- **`ICourseWorkRepository`** — `GetByCourseAsync(long courseId,
  CancellationToken)` (the course's current items, so the upsert matches by
  `(resource, google_id)` within that course without a query per item), `Add`.
- **`ISubmissionRepository`** — `GetByCourseWorkIdsAsync(IReadOnlyCollection<long>
  courseWorkIds, CancellationToken)` (one query per course rather than one per
  item), `Add`.

Neither exposes a delete member: nothing in this Story deletes a row, and the
purge is US-037.

## 8. `RetentionSettings` — new settings record

`Application/Models/RetentionSettings.cs`: `sealed record RetentionSettings(int
Years)`.

- It lives in `Application` because FR-011's age rule runs there, inside
  `RunSynchronizationUseCase`, and `Application` cannot reference `Web` (AD-3).
  `Application/Models/SchoolDefaults` is the existing precedent for a narrow
  settings record consumed by a use case.
- `Web.Configuration.InstallationSettingsReader` reads `Retention:Years`,
  validates it (VR-008) and registers this record; an invalid or absent value
  makes the installation **refuse to start** (DC-3, PC-11). That is the same
  shape as `SyncScheduleSettings`, and deliberately **not** the whole
  `InstallationSettings` record, which SC-7 keeps out of the container (the
  US-013 security review's F-1).

## 9. Existing types this Story changes

| Type | Change |
|---|---|
| `RunSynchronizationUseCase` | gains the coursework and submission step **inside the existing per-course transaction**, and the FR-011 age decision **before** the write for a course with no row yet. Its guard-first order is untouched (FR-001, FR-013) |
| `ClassroomAgentDbContext` | two new `DbSet`s and two configuration classes |
| `IClassroomReader` | two new members (§6) |
| `InstallationSettingsReader` | reads and validates `Retention:Years`; registers `RetentionSettings` |
| `PermittedServiceWrites` | **no change** — the new writes are inside a guarded use case, so they are a protected write path (FR-001, US-013 FR-006) |
| `SyncState` | **no change** — the counter still counts courses (FR-014, US-014 OD-005) |
| `AuditAction`, `AuditTargetType` | **no change** — a scheduled run writes no audit row (FR-020) |
| `GoogleDelegationScopes` | **no change** — both needed scopes are already on the fixed list (FR-002) |
| `Course`, `ClassroomParticipant`, `CourseMembership` | **no change to their shape.** The use case creates an off-roster `student` membership through the existing `CourseMembership` behaviour when FR-007 applies |

## 10. Mapping to API DTOs

None. The Story exposes no endpoint, page or view model (FR-019, api-design v2),
so neither entity is mapped to a DTO here.

Recorded for EPIC-3, from api-design v2 §0: a future endpoint must not address
either entity by its **Google** id, because those ids are unique only within
their parent — it uses the surrogate `Id` (PC-3, API-3) or a course-nested path.

## 11. Mapping to business concepts

| Entity / member | Business concept |
|---|---|
| `CourseWork` | §3 «CourseWork — задание или учебный материал курса» |
| `CourseWork.ItemDate` | §3's date cascade; the journal's column date (§4 Epic 3) |
| `CourseWorkKind` | BR-052's three kinds — graded work, ungraded work, material |
| `Submission` | §3 «Submission — сдача задания студентом» |
| `Submission.TurnedInAt` | BR-058, the **last** turn-in |
| `Submission.AssignedGrade` / `DraftGrade` | BR-056, BR-057 — the cell's grade and the draft the short journal omits |
| `Submission.Late` | BR-056's «с опозданием», as Google computed it |
| `Submission.State` | BR-056's cell states; `Unrecognised` is OD-005's marker, and **US-025 owns how it is rendered** |
| `Submission.UpdateTime`, `CourseWork.CreationTime` / `UpdateTime` | PC-11's «последняя активность» of a course |
| `RetentionSettings.Years` | §5's retention period N |
