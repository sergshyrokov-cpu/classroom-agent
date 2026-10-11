---
artifact_type: test_strategy
story: US-033
version: 1
status: DRAFT
created_at: 2026-10-10T20:15:00Z
updated_at: 2026-10-10T20:15:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-033-meet-reports.md
    version: null
  - path: docs/specifications/US-033-spec.md
    version: 1
  - path: docs/designs/api/US-033-openapi.yaml
    version: 1
  - path: docs/designs/api/US-033-api-design.md
    version: 1
  - path: docs/designs/database/US-033-db-design.md
    version: 1
  - path: docs/designs/database/US-033-entity-model.md
    version: 1
  - path: docs/decisions/US-033-open-decisions.md
    version: 2
supersedes: null
---

# US-033 Test Strategy — Built-in Meet reports for a course and period

## 1. Scope

The tests cover the two read-only pages (`GET /workspace/meet-reports`,
`GET /workspace/meet-reports/meetings/{meetingId}`), the queries behind them
(`GetMeetReportsQuery`, `GetMeetReportMeetingQuery`), the read port
`IMeetReportSource` against PostgreSQL, the home page entry, authorization,
read-only mode, translations and logging. There is no schema change, so there
are no migration or constraint tests (db-design §1).

## 2. Levels

| Level | What | Where |
|---|---|---|
| Unit (TC-1) | all report arithmetic and classification: meetings of the course and period, FR-005 classes and precedence, FR-006 capping and rounding inputs, reports 1–4, validation, defaults, delay notice, people display | `Application/UseCases/MeetReports*Tests.cs`, over `FakeMeetReportSource` + `SyncWorld.StateRepository` |
| Integration (TC-2) | `MeetReportSource` R1–R6 on real PostgreSQL: course filter through links, "not a course" mark, half-open period, case-insensitive email, tie rule, personal-account flag | `Infrastructure/Persistence/MeetReportSourceTests.cs` |
| Integration (host) | pages end to end: status codes, rendered values, minutes text, empty states, back link, `no-store`, read-only mode | `Web/Pages/MeetReportsPageTests.cs`, `MeetReportMeetingPageTests.cs` |
| Security (TC-5) | Admin ✔, Dean ✔, anonymous `302`; restricted Dean session; enumeration test classifies both paths as protected | `Web/Security/MeetReportsAuthorizationTests.cs` |
| Localization (NFR-073) | every key in uk and en; «Участь студентів»; Google data not translated; number formats | `Web/Localization/MeetReportsTranslationTests.cs` |
| Logging (SC-10) | validation refusal logs no value; a built page logs no email, name or code | `Web/Logging/MeetReportsLoggingTests.cs` |

## 3. Scenarios

**Positive.** Reports for a course with linked codes; report 4 for a row; the
default period; the delay notice with a watermark.

**Negative.** A "not a course" code; an unlinked code; a meeting of another
course in report 4; a malformed, unknown, repeated or inverted input; an
anonymous request.

**Boundary.** Starts at 23:30 on the last day and at 00:10 on the next day
(AC-003). Kyiv midnight on both sides of the 2026-10-25 change from summer
time (AC-018). Two devices exceeding the meeting's duration (AC-009). A
one-day period. Year 2000 and 2100 limits. Zero meetings and zero students
(AC-012).

**Validation.** VR-001 … VR-004: digits only, positive 64-bit, single
occurrence; `yyyy-MM-dd`; `from` ≤ `to`; report 4 `courseId` required; a
non-digit `meetingId` gets the catch-all `404`; zero or an overflowing
`meetingId` gets `MeetingNotFound`; invalid `from`/`to` on report 4 are dropped
from the back link.

**Security.** Both roles allowed; anonymous refused; no state change by `GET`;
no Google port called (the substituted `IClassroomReader` / `IMeetReportsReader`
record no call); `no-store`; no personal data in logs; other participants
shown without identity (I-11); query strings carry only ids and dates.

**Persistence.** R1–R6 of db-design §3 against migrations-built PostgreSQL;
nothing written (row counts unchanged after a page request).

## 4. Fixtures

- `FakeMeetReportSource` — in-memory port applying db-design §3/§4 rules;
  registers courses into an inner `FakeJournalSource`.
- `MeetReportsTestData` — domain, the October 2026 Kyiv period (crossing the
  end of summer time), URLs and the translation-key list.
- `SyncWorld.StateRepository` — the watermark for the delay notice.
- Host tests reuse `InstallationTestHost`, `JournalHostExtensions` (sign-in as
  Admin / Dean) and the existing course, participant and Meet seeding helpers
  (`CourseTestData`, `MeetTestData`, `MeetLinkRows`).

## 5. Interpretations under test

I-3, I-4, I-5 (seconds are tested in the DTO; the rendered "N of M min" is
tested once on the page with half-up rounding), I-6, I-7, I-8, I-10, I-11 and
I-12, plus OD-012 (`PersonRef.Email` null, `MeetReports.Person.Unnamed`).

## 6. Translation keys

The closed list is `MeetReportsTestData.Keys.All`. IMPLEMENTATION adds exactly
these keys to both resource files. A key the page needs but the list lacks is
a test defect, to be raised, not silently added.

## 7. Excluded, with reason

- The export — US-044.
- Query plans and timing (db-design §7): no performance assertion; bounded
  reads are proved by call counts on the fake and by the fixed round-trip count
  not depending on data size.
- Concurrency between a synchronization run and a page read (db-design §5):
  accepted, nothing to assert.

## 8. Known limitations

- The `call_ended` field names (§7 item 28) are not exercised: data is seeded
  directly as stored rows.
- Collation-dependent ordering is checked for Ukrainian and English only.

## 9. Open Decisions

OD-011 (skeleton, a) and OD-012 (person without an email, a) — both resolved.
None open.
