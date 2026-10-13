---
artifact_type: test_strategy
story: US-024
version: 1
status: DRAFT
created_at: 2026-10-13T10:00:00Z
updated_at: 2026-10-13T10:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-024-database-statistics.md
    version: null
  - path: docs/specifications/US-024-spec.md
    version: 1
  - path: docs/decisions/US-024-open-decisions.md
    version: 3
  - path: docs/designs/api/US-024-api-design.md
    version: 1
  - path: docs/designs/api/US-024-openapi.yaml
    version: 1
  - path: docs/designs/database/US-024-db-design.md
    version: 1
  - path: docs/designs/database/US-024-entity-model.md
    version: 1
supersedes: null
---

# US-024 Test Strategy — Database statistics and last synchronization

## 1. Scope

The new `GET /database` page and the new `POST /database/synchronization-requests`
press (OD-011 a), the home page's new `database-section`, the query
`GetDatabaseStatisticsQuery`, the read port `IDatabaseStatisticsSource` and its
adapter `DatabaseStatisticsReadSource`. The US-019 use case is reused unchanged.
Its own tests stay as they are, and one regression test proves that the US-019
endpoint still returns to the role's page.

Out of scope, per spec §10: diagnosis on this page (only its absence is tested),
auto-refresh, disk size, export, history of runs.

## 2. Test levels

| Level | Class | What it proves |
|---|---|---|
| Unit (TC-1) | `Application/UseCases/DatabaseStatisticsQueryTests` | mapping of counters, total = sum, state line, three times in Europe/Kyiv (winter and summer), guidance by role, no diagnosis field, unknown status throws, one read of each port, no guard/unit of work/Google dependency |
| Integration, PostgreSQL (TC-2) | `Infrastructure/Persistence/DatabaseStatisticsReadSourceTests` | each of the eleven counters against seeded rows, empty school, AC-003 four people, AC-015 person, any course state, AC-018 equality with `MeetCodesReadSource.GetCountsAsync().Unassigned`, fixed command count with 1 and 200 courses, no write |
| HTTP page | `Web/Pages/DatabaseStatisticsPageTests` | home-page section placement, every counter rendered, never-run, times formatted in both cultures, same block for both roles, failed run with guidance and without cause, query string ignored, form in every mode, no personal data |
| HTTP security (TC-5) | `Web/Security/DatabaseStatisticsAuthorizationTests` | allowed Admin/Dean; anonymous and restricted-session refused for GET and POST; antiforgery `400`; endpoint enumeration with the two policies |
| HTTP use case | `Web/UseCases/DatabaseSynchronizationRequestTests` | accepted press → `302 /database`, one request, one audit row, one-time message; after-current-work message; `returnUrl` ignored; connection refusal `409` on the Database page; read-only `409` in all three causes; read-only + unconfigured; US-019 regression |
| Read-only (TC-5) | `Web/UseCases/DatabaseStatisticsReadOnlyTests` | the query resolved from the host and the page work in all three BR-025 causes and outside them, write nothing, call no Google |
| Localization (TC-8) | `Web/Localization/DatabaseStatisticsTranslationTests` | every new and reused key in uk and en; the page and guidance render in the UI language |
| Logging (SC-10) | `Web/Logging/DatabaseStatisticsLoggingTests` | one Information line with the actor id; no school data or query value in logs; the press logs the US-019 event |

## 3. Scenarios

### 3.1 Seeded world (`DatabaseStatisticsHostExtensions.SeedStatisticsWorldAsync`)

The seeded world is 15 courses: 5 active, 4 archived, 3 provisioned, 2 declined
and 1 suspended. The counts differ, so a state swap cannot pass.

| Person | Memberships | Counted as |
|---|---|---|
| Шевченко | teacher of active 1 | teacher |
| Мельник | student of active 1 | student |
| Українка | teacher of active 2, student of active 1 | teacher and student |
| Франко | off-roster teacher of active 1, off-roster student of active 2 | neither (leaver) |
| Коваль | teacher of active 1 and of archived 1, student of active 3 | one teacher, one student |
| Бондар | teacher of archived 1 only | teacher |

Totals: 4 teachers, 3 students.

The other counters:
- **Coursework:** 5 items — 3 in active 1, 2 in archived 1 (I-4).
- **Submissions:** 7, one of them by the leaver (I-4).
- **Meetings:** 6 in total. The code `aaa-bbbb-ccc` has two meetings; `ddd-eeee-fff` has one with no link; one code is linked automatically, one is linked by a person, one is marked "not a course". One mark exists for a code that has no meeting.
- **Codes awaiting a decision:** 2.

### 3.2 Positive

- Every counter shows its value, for both roles: AC-002, AC-003, AC-004.
- A completed full run shows three times and the state, the same for both roles: AC-005.
- Time-zone conversion: 2026-03-01 22:30 UTC is shown as 02.03.2026 00:30 (uk) and in the en short-date pattern (AC-014). The summer offset is +3.
- Accepted press: the user returns to `/database` with the US-019 message, which is shown once (AC-008).

### 3.3 Negative

- **Failed run.** A Dean sees the hint and no link; an Admin sees the link and no hint. The stored diagnosis, every `LastSync.Diagnosis.*` text and every `LastSync.Step.*` text appear nowhere on the page (AC-007, AC-013).
- **Connection not usable.** `409`, the Database page with the role's message, nothing enqueued, one refusal audit row (AC-016).
- **Read-only.** `409` with the reason, nothing enqueued, refusal audit row `read_only_mode`. This holds for all three causes, and also when the school is read-only and unconfigured at once (AC-009).
- **Unknown stored status.** The query throws, and the generic error page answers (VR-003).

### 3.4 Boundary

- **Empty school, never synchronized:** every counter is 0 and every line reads "not yet" (AC-006).
- **A state with no courses:** shows 0, and the total is still the sum.
- **A first run that failed:** "failed", with no times.
- **Running and completed runs:** neither the hint nor the link (I-7).
- **AC-017:** 1 and 200 courses take the same number of commands. The counters are one command, the page at most two.

### 3.5 Validation

- **AC-019, VR-001:** unknown query parameters (a marker value, `status`, `returnUrl`) are ignored and appear nowhere on the page; the counters are the same as without them.
- **VR-002, S-07:** a `returnUrl` field and query value on the press are ignored; `Location` is exactly `/database`.
- **VR-002, API-7:** a press without an antiforgery token gets `400`, with no request and no audit row.

### 3.6 Security

- **TC-5:** the page and the press are open to Admin and Dean. They are refused for anonymous visitors (`302 /sign-in`) and for a Dean on the forced password change (`302 /sign-in/change-password`).
- **Endpoint enumeration:** `database` has policy `ViewDatabaseStatistics`, `database/synchronization-requests` has `StartSynchronization`; neither allows anonymous access.
- **S-04:** read-only behaviour is proven on the query resolved from the host. Its constructor takes only the two read ports and the time zone (unit test).
- **S-08, S-10:** no personal data on the page or in the logs.
- **S-11:** the DTOs have no string member and no member whose name suggests error, diagnosis, step, processed count, run id or start/finish times.

### 3.7 Persistence

The counters are covered by `DatabaseStatisticsReadSourceTests` over real PostgreSQL.
A fingerprint shows that opening the page writes nothing: course, participant,
membership, coursework, submission, meeting, link and sync-state rows are compared
row by row, plus the audit count.
There is no schema change, so this Story has no constraint tests.

## 4. Markup hooks (binding for IMPLEMENTATION)

| Hook | Where |
|---|---|
| `<nav id="database-section">` with an `<h2>` and exactly one `<a href="/database">` | home page, after `report-templates-section`, before `admin-settings`; both roles; read-only too |
| `data-counter="<name>"` on an element whose only text is the number | `courses-active`, `courses-archived`, `courses-provisioned`, `courses-declined`, `courses-suspended`, `courses-total`, `teachers`, `students`, `course-work-items`, `submissions`, `meet-meetings`, `meet-codes-awaiting-decision` |
| `data-sync-line="<name>"` on an element whose only text is the value | `last-successful-run`, `state`, `last-full-run`, `meet-loaded-up-to`. A time is shown in the culture's short date plus ` HH:mm`; a missing value shows the `Database.Sync.NotYet` text |
| `id="database-failure-hint"` | Dean, failed run only; its text is `Database.Failure.AskAdmin` |
| `<a id="database-connection-link" href="/settings/workspace-connection">` | Admin, failed run only |
| `<form id="database-synchronization-form" method="post" action="/database/synchronization-requests">` with the antiforgery token | always |
| `id="synchronization-request-message"` (US-019) | the one-time message, also on the `409` re-render |

## 5. Translation keys (binding for IMPLEMENTATION)

**New keys:**
- **Page and navigation:** `Database.Title`, `Database.Section`, `Database.NavigationEntry`.
- **Counters:** `Database.Counter.Total`, `.Teachers`, `.Students`, `.CourseWorkItems`, `.Submissions`, `.MeetMeetings`, `.MeetCodesAwaitingDecision`.
- **Synchronization block:** `Database.Sync.LastSuccessfulRun`, `.State`, `.LastFullRun`, `.MeetLoadedUpTo`, `.InProgress`, `.Completed`, `.Failed`, `.NotYet`.
- **Failed run:** `Database.Failure.AskAdmin`, `Database.Failure.ConnectionPageLink`.

**Reused keys:** `Courses.Status.Active` … `Courses.Status.Suspended`, and the five `Synchronization.Request.*` keys of US-019.

## 6. Fixtures

- `DatabaseStatisticsTestData`: paths, patterns, policy, markup, keys and the markup readers.
- `DatabaseStatisticsHostExtensions`:
  - the seeded world and the sized school;
  - the `sync_state` seeder covering every column;
  - the fingerprint;
  - the real `DatabaseStatisticsReadSource` and `MeetCodesReadSource` over the host's database.
- `FakeDatabaseStatisticsSource`: the counters port, substituted.

Reused from earlier stories:
- `SyncWorld` (in-memory `ISyncStateRepository`);
- `SynchronizationRequestHostExtensions` (`FakeSynchronizationRequests`, audit rows);
- `ReadOnlyModeHost`, `JournalHostExtensions` and `MeetReportsHostExtensions` (UI language);
- `HostEndpoint`, `HostLogs`.

No live Google call is made and no Google port is reached (TC-4). All names, emails and codes are invented.

## 7. Excluded scenarios

- **Phone layout (NFR-070):** not automatable meaningfully here; it is checked at review.
- **Indexes (PC-7):** no index is added (db-design §4), so there is nothing to assert.
- **Concurrency between counters (I-5):** it is not required, so it is not tested.

## 8. Known limitations

- **Two GET refusal tests may pass vacuously.** The anonymous and restricted-session GET tests can pass before the route exists, because the redirect runs before routing. They become meaningful once the route exists. The endpoint-enumeration test proves the route and its policy.
- **The structural tests pass on the skeleton.** These are the two reflection tests (DTO members, constructor dependencies). That is by design: they guard the shape fixed in the entity model.

## 9. Open Decisions affecting testing

OD-012 (compile-only skeleton) was resolved (a) by the Owner on 2026-10-13. No
other Open Decision is open.
