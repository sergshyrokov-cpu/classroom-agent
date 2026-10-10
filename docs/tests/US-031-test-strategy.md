---
artifact_type: test_strategy
story: US-031
version: 1
status: DRAFT
created_at: 2026-10-10T06:31:21Z
updated_at: 2026-10-10T06:31:21Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-031-meet-events-pull.md
    version: null
  - path: docs/specifications/US-031-spec.md
    version: 1
  - path: docs/designs/api/US-031-api-design.md
    version: 1
  - path: docs/designs/api/US-031-openapi.yaml
    version: 1
  - path: docs/designs/database/US-031-db-design.md
    version: 1
  - path: docs/designs/database/US-031-entity-model.md
    version: 1
  - path: docs/decisions/US-031-open-decisions.md
    version: 2
supersedes: null
---

# US-031 Test Strategy — Pull Meet events and keep history beyond 180 days

## 1. Scope

The Meet step of a synchronization run (window, watermark, per-conference
storage decision, idempotent upsert, validation of Google's events, failures
with the step that stopped the run), the `IMeetReportsReader` adapter, the two
new tables and the changed `sync_state` / `audit_event` columns, the retention
purge's meeting step and its two audit counts, and the Admin's "Last
synchronization" block (watermark line in the school's time zone, failed step).
Story AC-001 … AC-015 (spec §5).

Not in scope (spec §10): meeting-code linking, Meet screens and reports,
leaver expiry through links (US-032, US-033).

## 2. Levels

| Level | What it proves | Where |
|---|---|---|
| Unit (U) | Run use case with every port in memory (`SyncWorld`), domain entities and rules, the "Last synchronization" query (TC-1) | `Application/UseCases/Meet*`, `SchoolDomainAccountTests`, `SyncStateMeetTests`, `LastSynchronizationMeetQueryTests`, `RetentionPurgeMeetAuditEventTests` |
| Adapter (A) | `GoogleMeetReportsReader` over a scripted transport: request shape, paging, mapping, scope, impersonation, retry and classification (TC-4, AC-015) | `Infrastructure/Google/GoogleMeetReportsReader*Tests` |
| Integration (I) | Schema, constraints, indexes, repository round trip and the purge on real PostgreSQL from the migration (TC-2) | `Infrastructure/Persistence/MeetSchemaTests`, `MeetSessionRepositoryTests`, `Application/UseCases/RetentionPurgeMeetTests` |
| Host (H) | The Meet step through the real composition root, background service and database with the Meet port substituted (FR-015) | `Web/BackgroundServices/MeetPullHostTests` |
| HTTP / security (S) | The block on the Admin's page in uk/en; Dean refused (TC-5) | `Web/Pages/LastSynchronizationMeetBlockTests` |
| Logging / localization (L) | FR-012 lines with no event content; FR-014 keys in both languages | `Web/Logging/MeetPullLoggingTests`, `Web/Localization/MeetPullTranslationTests` |

## 3. Scenarios by kind

**Positive.** A domain meeting is stored with its values and one participation
per endpoint (AC-001); start = earliest join, end = latest leave, recomputed by
a later run (AC-002); a per-conference decision stores every connection of a
conference that has one domain-organizer event (I-4); first window
`now − 180 d + 1 h`, later `T − 3 d` (AC-006); a completed run moves the
watermark; the block shows the watermark in Europe/Kyiv local time (AC-009).

**Negative.** Other-domain, subdomain, malformed and missing organizers store
nothing (AC-004); only domain accounts' connections hold an email (AC-005);
a failed Meet step, a Classroom stop, a host stop and read-only mode never move
the watermark (AC-006, AC-007, AC-010); a stored meeting's code and organizer
are never overwritten (I-4); synchronization never deletes a meeting (PC-11).

**Boundary.** Window clamp at the 180-day horizon and no clamp just inside it;
VR-001 limits 128 / 64 / 128 characters, duration 0 and 86 400 s, event time
23 h outside the window accepted and 1 day + 1 min refused; 254-character email
limit; purge cutoff equality kept, one second older deleted; a purge of 501
meetings crosses the 500-meeting batch; Kyiv summer (UTC+3) and winter (UTC+2)
conversion across midnight.

**Validation (VR-001).** Each rule, missing / blank / too long / out of range,
is skipped and counted under its `MeetEventRejection`; the pull continues; the
adapter's unreadable events are counted as skipped; whitespace trimmed.

**Security.** Read-only mode per BR-025 reason: no `IMeetReportsReader` call
(TC-5, AC-010); technical account impersonated and only the reports scope
requested, GET only (BR-015, SC-8); no email, meeting code, conference id or
endpoint id in any log line (SC-10, AC-012); the page refuses a Dean (TC-5);
the stored diagnosis is a code, never Google text or an exception message.

**Persistence.** Exact column sets of `meet_session` / `meet_participation`
(AC-013), unique keys, check constraints, `RESTRICT` foreign key, indexes;
`sync_state` and `audit_event` additions with their constraints, pre-Story rows
staying valid; repository round trip and upsert; host run writes one row per
conference and per (conference, endpoint) across two runs (AC-003).

## 4. Fixtures (`tests/ClassroomAgent.Tests/TestInfrastructure/`)

- `MeetTestData` — synthetic conferences, codes, endpoints, accounts of the test
  domain, the `email_address` identifier type, window constants, and the names
  the tests fix for IMPLEMENTATION (log events and properties, translation keys,
  constraint and index names, the local date format of the watermark).
- `FakeMeetReportsReader` (+ `MeetReadCall`) — the Meet port of both the
  in-memory world and the host: seeded pages, unreadable counts, recorded calls
  with their windows, scripted failures and a per-page hook for a host stop.
- `SyncWorld` — gains the Meet port, an in-memory `IMeetSessionRepository` and
  a configurable read-only reason.
- `InstallationFactory` / `InstallationTestHost` — substitute `IMeetReportsReader`
  in every host test (`host.Meet`, TC-4, FR-015).

## 5. Production skeleton (OD-011 a)

Compile-only declarations, every new behaviour throwing
`NotImplementedException`; no EF mapping, no migration, no view or translation.
Listed in the test generation report §2.

## 6. Excluded, with reason

- A live Google call or a recorded live response — forbidden (TC-4); the
  `call_ended` field names are verified by the Owner on a live domain
  (OD-009, `trebovaniya.md` §7 item 28).
- Concurrent writers of the same conference — runs never overlap (US-013);
  db-design §7 makes a violation an `Unexpected` failure, nothing to assert.
- Course-linked purge of meetings, leaver Meet expiry — US-032.
- Which values win when one endpoint appears twice in one run — the spec fixes
  only "one row" (PC-10), so only the row count is asserted.

## 7. Known limitations

- The in-memory world cannot roll back; "what was committed before a failure
  stays" (FR-009) is asserted on what the use case wrote before the failing page.
- Whether `NotOfTheSchool` counts events or meetings is not fixed by FR-012;
  every test that asserts it seeds exactly one event per such meeting.
- The adapter's handling of an event whose parameters cannot be parsed depends on
  the SDK's deserialisation; the use case's handling of `UnreadableCount` is
  tested instead.

## 8. Open Decisions affecting testing

- OD-009 (resolved a): synthetic fixtures; mapping kept in one adapter place.
- OD-010 (resolved a): the watermark line in the school's time zone, the rest UTC.
- OD-011 (resolved a, Owner 2026-10-10): compile-only skeleton.
No open decision blocks testing.
