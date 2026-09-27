---
artifact_type: entity_model
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-27T17:30:24Z
updated_at: 2026-09-27T17:30:24Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-014-spec.md
    version: 2
  - path: docs/designs/api/US-014-api-design.md
    version: 2
  - path: docs/decisions/US-014-open-decisions.md
    version: 1
  - path: docs/designs/database/US-014-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-014 Entity Model — Sync courses and rosters

Three new entities, two new enums, three new repository ports, and the Application
models the Classroom port speaks in. Column-level detail lives in the db-design; this
document fixes the types, their behaviour and the invariants the database cannot
express.

The governing principle, inherited from `SyncState`: **an entity holds no clock and
no policy.** Every instant arrives as an argument, so the run's single observation
instant (I-8) is passed in rather than read (`TimeProvider` lives in the use case).

## 1. `Course` — new entity

`Domain/Entities/Course.cs`. The school's Classroom course (§3).

### 1.1 Properties

| Property | Type | Notes |
|---|---|---|
| `Id` | `long` | surrogate key |
| `GoogleId` | `string` | the upsert key; never changes once set |
| `Name` | `string` | required |
| `Section` | `string?` | |
| `DescriptionHeading` | `string?` | |
| `Description` | `string?` | |
| `Room` | `string?` | |
| `OwnerGoogleId` | `string?` | a value, not a reference (OD-004) |
| `CreationTime` | `DateTimeOffset?` | Google's, stored as given (I-1) |
| `UpdateTime` | `DateTimeOffset?` | Google's |
| `State` | `CourseState` | closed enum (§2) |
| `AlternateLink` | `string?` | |
| `TeacherFolderId` | `string?` | |
| `TeacherFolderTitle` | `string?` | |
| `CalendarId` | `string?` | |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | PC-6, interceptor-stamped |

Every setter is private; the entity is changed only through the two members below.

### 1.2 Behaviour

- **`static Course Import(string googleId, CourseState state, CourseDetails details)`**
  — creates the row for a course not seen before. `googleId` is required and
  non-blank.
- **`void UpdateFrom(CourseState state, CourseDetails details)`** — the upsert's
  update half. The surrogate identity and `GoogleId` are untouched, so every
  reference to the course survives (FR-008).
- **Truncation happens here.** Each string is bounded by a public constant
  (`MaxNameLength` = 750, and so on, matching db-design §3.1) and a longer value is
  **cut, not refused** (VR-002) — the `SyncState.LastError` precedent: a verbose
  course description must not make a run fail at the commit.
- There is deliberately **no `MarkMissing`** and no `Delete`: a course Google stopped
  returning is left untouched (FR-011, I-4), and deletion belongs to the purge
  (PC-11, US-037).

`CourseDetails` is a `record` in `Application/Models` carrying the optional fields as
one parameter, so neither member grows a twelve-argument signature. It holds no
Google SDK type (AD-4).

## 2. `CourseState` — new enum

`Domain/Enums/CourseState.cs`: `Active`, `Archived`, `Provisioned`, `Declined`,
`Suspended`.

**Exactly five members. No `Unknown`, no `None`, no `Unspecified`.** OD-010 skips a
course whose state Classroom reports outside the five *before* it becomes an entity,
so there is no state for the enum to represent — and a sixth member would be a
business rule no artifact defines (`AGENTS.md` Hard Stops). The mapping to the
lower-case database codes is a value converter in the configuration, exactly as
`SyncRunStatus` does it.

Where the unrecognised value is handled: the Classroom adapter reports the state as
the string Google sent (§6), and the use case skips the course with one `Warning`
line. The enum never sees it.

## 3. `ClassroomParticipant` — new entity

`Domain/Entities/ClassroomParticipant.cs`. A person who came from synchronization
(§3).

### 3.1 Properties

| Property | Type | Notes |
|---|---|---|
| `Id` | `long` | surrogate key |
| `GoogleUserId` | `string` | the upsert key and the person's **only** identity (OD-011) |
| `Email` | `string?` | optional (OD-006), non-unique (OD-011); **personal data** |
| `FullName` | `string?` | one string (I-2); **personal data** |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | PC-6 |

### 3.2 Behaviour

- **`static ClassroomParticipant Import(string googleUserId, string? email, string? fullName)`**
- **`void UpdateFrom(string? email, string? fullName)`** — the upsert's update half.
  A person who changed their address in Google keeps their row, because identity is
  the `GoogleUserId`.
- The address is normalised on the way in — trimmed and lower-cased, reusing
  `WorkspaceConnection.NormalizeEmail` so there is one normalisation in the system
  (VR-003). A blank string becomes `null`, so "no address" has one representation.
- **No role member of any kind.** The role belongs to the membership (BR-050, FR-006);
  a `Role` property here would be a modelling defect, and db-design §4.3 says the
  same about the column.
- No `Delete`: removal is the purge's (PC-11).

## 4. `CourseMembership` — new entity

`Domain/Entities/CourseMembership.cs`. One person's participation in one course
(§3) — the entity PC-8 requires between the other two.

### 4.1 Properties

| Property | Type | Notes |
|---|---|---|
| `Id` | `long` | surrogate key |
| `CourseId` / `Course` | `long` / `Course` | FK and navigation |
| `ParticipantId` / `Participant` | `long` / `ClassroomParticipant` | FK and navigation |
| `Role` | `ClassroomRole` | a **field**, not part of identity (FR-007, I-9) |
| `FirstSeenAt` | `DateTimeOffset` | never rewritten |
| `LastSeenAt` | `DateTimeOffset` | advances while the person is seen |
| `OnRoster` | `bool` | whether they are on it now |
| `CreatedAt` / `UpdatedAt` | `DateTimeOffset` | PC-6 |

### 4.2 Behaviour — where BR-051 actually lives

- **`static CourseMembership FirstSeen(Course course, ClassroomParticipant participant, ClassroomRole role, DateTimeOffset seenAt)`**
  — `FirstSeenAt` = `LastSeenAt` = `seenAt`, `OnRoster` = true.
- **`void SeenAgain(ClassroomRole role, DateTimeOffset seenAt)`** — `LastSeenAt` =
  `seenAt`, `OnRoster` = true, and the role is refreshed in case Classroom moved the
  person between rosters. **`FirstSeenAt` is never touched**, including after an
  absence (FR-009, §3.4 of the Specification). `seenAt` earlier than `LastSeenAt` is
  rejected — time does not run backwards within a run sequence.
- **`void NotOnRoster()`** — `OnRoster` = false and **`LastSeenAt` is left where it
  was** (FR-010). It takes no instant on purpose: the moment we noticed an absence is
  not a fact §3 records, and passing one would invite writing it into `LastSeenAt`,
  which would postpone the leaver's expiry for ever (PC-11).
- **No `Delete`.** "Участие не удаляется, когда человек исчезает из ростера" (§3,
  v31) — only the purge deletes it, on its own `last_seen_at` (PC-11).

The invariant the database cannot express is here: **`LastSeenAt` advances only while
the person is on the roster.** `ck_course_membership_seen_order` can check the order
of the two instants but not which method was allowed to move them (db-design §5.2) —
the same division US-013 made for `SyncState.LastSuccessfulRunAt`, which only
`CompleteRun` may write.

### 4.3 The both-rosters tie-break

If Classroom returns the same person on a course's teacher **and** student roster,
one membership is written with `Role.Teacher` (Specification v2 FR-007, I-9). The
resolution happens **in the use case, before the write** — it compares the two rosters
of one course, which is knowledge no single entity has. The unique index
`uq_course_membership_course_participant` is then a guard against a defect rather than
a path the import takes (db-design §5.3).

`SeenAgain` refreshing the role is what makes a genuine role change survive: a person
who stopped being a student and became a teacher of the same course keeps one
membership, with its original `FirstSeenAt`.

## 5. `ClassroomRole` — new enum

`Domain/Enums/ClassroomRole.cs`: `Teacher`, `Student`. Exactly two members (§3),
mapped to the codes `teacher` / `student` by a value converter, with
`ck_course_membership_role` behind it.

It is **not** an application permission: §3 is explicit that the Classroom role "не
совпадает с ролью в приложении (AppUser) и в первой версии служит признаком в
статистике, а не правом доступа". Any authorization code reading this enum is a
finding.

## 6. `IClassroomReader` — new port

`Application/Ports/IClassroomReader.cs`, implemented in `Infrastructure/Google`
(AD-4, TC-4, `package-map.md`). It carries the `IGoogleDataPort` marker, so US-007
FR-007 binds it: a use case holding it also takes `IReadOnlyModeGuard` and calls it
first (FR-015).

Shape, in the Application's own vocabulary — no Google SDK type crosses the boundary
(AD-4, FR-002):

- **`IAsyncEnumerable<CourseSnapshot> ReadCoursesAsync(CancellationToken)`** — the
  school's courses, the adapter following the continuation token so the caller never
  sees a page (VR-005). Streaming rather than a list keeps one course's transaction
  independent of the whole school being in memory (FR-012).
- **`Task<CourseRoster> ReadRosterAsync(string courseGoogleId, CancellationToken)`** —
  both rosters of one course, each fully paged. One call returning both is what lets
  FR-010 distinguish "read succeeded, roster empty" from "read failed, roster
  unknown" (I-6, I-7): a returned `CourseRoster` means success, an exception means
  unknown.

Application models beside it (`Application/Models`):

- **`CourseSnapshot`** — the Google id, the **state as the string Google sent**, and
  `CourseDetails`. The state stays a string here precisely so the use case can apply
  OD-010 and skip an unrecognised value; parsing into `CourseState` happens in the
  use case, not the adapter.
- **`CourseRoster`** — the teacher entries and the student entries.
- **`RosterEntry`** — the Google `userId`, the email and the name, each as Classroom
  gave them (OD-006).

The adapter follows `GoogleAccessProbe`: one injected `HttpMessageHandler` so tests
run offline (TC-4) and nothing but Google is reachable (SC-13), the client library's
own retry switched **off** (retry is US-017, OD-008), the key resolved from
`ISecretStore` per request, and the technical account of `WorkspaceConnection`
impersonated (BR-015, S-03). It classifies nothing and logs no Google error text
(SC-10).

## 7. Repository ports — new

In `Application/Ports`, implemented in `Infrastructure/Persistence.Repositories`.
They stage changes and never call `SaveChangesAsync`; the use case commits (AD-7).

- **`ICourseRepository`** — `GetByGoogleIdAsync(string, CancellationToken)`, `Add`.
- **`IClassroomParticipantRepository`** —
  `GetByGoogleUserIdsAsync(IReadOnlyCollection<string>, CancellationToken)` (one query
  per course rather than one per person), `Add`.
- **`ICourseMembershipRepository`** — `GetByCourseAsync(long courseId,
  CancellationToken)` (the course's current memberships, needed to decide which ones
  this run did not see — FR-010), `Add`.

None of them exposes a delete member: nothing in this Story deletes a row, and the
purge is US-037. `ISyncStateRepository` is unchanged.

## 8. Existing types this Story changes

| Type | Change |
|---|---|
| `RunSynchronizationUseCase` | gains the pipeline step at the marked place: reads courses, reads each course's rosters, upserts inside one transaction per course, counts courses (FR-001, FR-012, FR-013). Its guard-first order is untouched |
| `ClassroomAgentDbContext` | three new `DbSet`s and three configuration classes |
| `PermittedServiceWrites` | **no change** — the new writes are inside a guarded use case, so they are a protected write path (FR-001, US-013 FR-006) |
| `SyncState` | **no change** — the counter's meaning changes, not its shape (FR-013, OD-005) |
| `AuditAction`, `AuditTargetType` | **no change** — a scheduled run writes no audit row (FR-019) |
| `GoogleDelegationScopes` | **no change** — courses and rosters are covered by three scopes already on the fixed list (FR-002) |

## 9. Mapping to API DTOs

None. `API_DESIGN` is `NOT_APPLICABLE` (api-design v2): no endpoint, page or export
exists, so no DTO maps to any of these entities in this Story (FR-018, S-08).

Recorded for EPIC-2, from api-design §0: because a person has **one** membership per
course, a roster DTO carries **one role per person per course**, not a collection of
roles. DTOs are mapped in `Application`, and a domain entity never appears in a
controller signature or a Razor view model (AD-8).

## 10. Mapping to business concepts

| Entity / member | Business concept | Source |
|---|---|---|
| `Course` | курс Classroom — always a Classroom course, never a year of study | §3 |
| `Course.OwnerGoogleId` | the teacher who created the course, per Classroom | §3, OD-004 |
| `Course.State` | ACTIVE / ARCHIVED / PROVISIONED / DECLINED / SUSPENDED | §3 |
| `ClassroomParticipant` | человек из Classroom, приходит из синхронизации | §3 |
| `ClassroomParticipant` without `Email` | a roster entry with no personal address — imported, not discarded | OD-006 |
| `CourseMembership` | участие человека в курсе с ролью в Classroom | §3, BR-050 |
| `CourseMembership.Role` | teacher / student — a statistic, never a permission | §3, BR-050 |
| `FirstSeenAt` / `LastSeenAt` / `OnRoster` | what synchronization observed, because Classroom gives no join or leave dates | BR-051 |
| `NotOnRoster()` keeping `LastSeenAt` | the leaver's own expiry starts from the last sighting | BR-051, PC-11 |
| `SeenAgain` keeping `FirstSeenAt` | "когда синхронизация впервые видела человека в ростере" survives an absence | BR-051, FR-009 |
| one transaction per course | a run that fails leaves whole courses, never a half-written one | OD-009, FR-012 |
