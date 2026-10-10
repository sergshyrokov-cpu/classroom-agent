---
artifact_type: test_strategy
story: US-032
version: 1
status: DRAFT
created_at: 2026-10-10T16:14:25Z
updated_at: 2026-10-10T16:14:25Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-032-meet-code-linking.md
    version: null
  - path: docs/specifications/US-032-spec.md
    version: 1
  - path: docs/designs/api/US-032-openapi.yaml
    version: 1
  - path: docs/designs/api/US-032-api-design.md
    version: 1
  - path: docs/designs/database/US-032-db-design.md
    version: 1
  - path: docs/designs/database/US-032-entity-model.md
    version: 1
  - path: docs/decisions/US-032-open-decisions.md
    version: 2
supersedes: null
---

# US-032 Test Strategy — Link meeting codes to courses

## 1. Scope

Every Acceptance Criterion AC-001 … AC-018 of the Story, through the approved
Specification, the OpenAPI contract and the database design. In scope: the
scoring and decision rules, the linking step of a synchronization run, the
thresholds configuration, the "Meet meetings" page and its three lists, the
course-choice form, the three writes, read-only mode, authorization and
antiforgery, audit rows, the purge extension, the schema and migration,
translations and logging.

## 2. Levels

| Level | What | Where |
|---|---|---|
| Unit (pure) | `RosterOnDate`, `MeetCodeScorer` — every counting, candidate and decision rule; exact-fraction boundaries; Kyiv date boundaries (TC-8) | `Application/MeetLinking/*` |
| Unit (domain) | `MeetingCodeLink` shapes and transitions; `AuditEvent` link-change factories and the purge count | `Application/UseCases/MeetingCodeLinkInvariantTests`, `MeetCodeAuditEventTests` |
| Unit (use case, ports in memory, TC-1) | the three writes: outcomes, expected state, evaluation order, read-only guard first with the refused row (TC-5), malformed input, concurrency answered as stale | `Application/UseCases/MeetCodeChangeTests` + `TestInfrastructure/MeetCodeWorld` |
| Integration, host + PostgreSQL (TC-2) | the linking step inside a real run with the Meet port substituted (TC-4); configuration; logging | `Web/BackgroundServices/MeetLinkingHostTests`, `Web/Configuration/MeetLinkingConfigurationTests`, `Web/Logging/MeetLinkingLoggingTests` |
| Integration, HTTP (contract) | the page, the form, the writes: status codes, redirects, messages, DB effect and audit rows per the OpenAPI contract (TC-3) | `Web/Pages/MeetCodesPageTests`, `Web/Pages/MeetCodeActionsHttpTests` |
| Security | allowed roles (Dean, Admin) and the forbidden case (unauthenticated; restricted session) per operation (TC-5); antiforgery `400`; read-only `409` per BR-025 cause | `Web/Security/MeetCodesAuthorizationTests`, `MeetCodeActionsHttpTests` |
| Persistence | schema, constraints, indexes, migration; repository round trip and concurrency | `Infrastructure/Persistence/MeetingCodeLinkSchemaTests`, `MeetingCodeLinkRepositoryTests` |
| Integration, purge | last activity, course deletion, leaver expiry, orphaned marks, counts | `Application/UseCases/RetentionPurgeMeetLinkTests` |
| Localization | keys in uk and en; data shown verbatim | `Web/Localization/MeetCodesTranslationTests` |

The existing host-wide suites — endpoint enumeration (anonymous list), the
antiforgery sweep and the error page tests — pick up the new endpoints once they
exist; no change to them.

## 3. Scenarios

**Positive.** 85 %/20 % auto-link with its system audit row; re-scoring links an
ambiguous code later; pick, re-link, confirm, mark, remove the mark — each with
its state, redirect, message and audit row; lists show codes, organizers, dates,
counts, candidates with shares, makers, confirmers, markers; pagination;
purge deletes what PC-11 says and keeps the rest.

**Negative.** 70 %/55 % and best < 60 % stay unassigned; no candidate; an
automatic link is never revised; a mark and a person's link are never touched
by the run; no linking after a failed Meet step; stale state `409` for each write
and for a concurrency failure at save; unknown code / course `404`; same course
`400`; malformed form `400`; invalid query `400`; read-only `409` with a refused
row and no change; anonymous refused; no token `400`.

**Boundary.** Exactly 60 % with exactly 30 points links; 179/300 (59.67 %) and a
29.67-point gap do not, though rounded percents would (I-4); single candidate vs
next-best 0 (I-5); equal best shares; rounding down for display; roster dates on
the day, the day before and after, and at local midnight in Kyiv (TC-8); code
length 64/65; threshold values 1, 100, 0, 101; `returnPage` limits; page size
1/100/0/101; the purge cutoff.

**Validation.** VR-001 … VR-005 in `MeetCodeChangeTests`, `MeetCodesPageTests`,
`MeetLinkingConfigurationTests`.

**Security.** §2 table; no personal data in audit rows (factories accept none) or
logs (`MeetLinkingLoggingTests`).

**Persistence.** Every constraint and index of db-design §2 … §4; RESTRICT; the
unique code; the concurrency stamp; migration presence.

## 4. Fixtures

- `MeetCodeWorld` — in-memory ports for the write use cases, conflict injection.
- `MeetLinkingTestData`, `MeetCodesHostExtensions`, `MeetLinkRows` — SQL and fake
  seeding of courses, participants, memberships with explicit first/last seen,
  meetings, participations, link rows, app users.
- Existing: `InstallationTestHost`, `SyncHostExtensions`, `FakeMeetReportsReader`,
  `FakeClassroomReader`, `RetentionPurgeHost`, `ReadOnlyModeHost`, `CourseRows`,
  `JournalTestData.Kyiv`, `PostgreSqlFixture`.

Seeded memberships carry `first_seen_at` before the meetings: a membership
created by the run itself starts at the run (spec I-1), which would leave every
earlier meeting without a roster.

## 5. Names the tests fix (spec and designs leave them open)

- Translation keys `MeetCodes.*` (full list in the test-generation report §2) and
  `LastSync.Step.Linking`.
- Log event `SyncLinkingStepCompleted` (Information; `RunId`, `CodesScored`,
  `LinksCreated`).
- Use-case signature `ExecuteAsync(actorId, actorRole, meetingCode,
  MeetCodeFormInput, requestId, ct)` returning `MeetCodeChangeResult(Outcome,
  List, ReturnPage)`; one raw-pairs form type for the three writes (as US-027),
  so a repeated field is detectable.
- `IMeetingCodeLinkRepository.CourseExistsAsync` on the link port (entity model
  §5 had it on the read source).
- Blank configuration value is **invalid** (spec FR-005 "empty"), unlike
  `Sync:IntervalMinutes`, where blank means default.

## 6. Excluded, with reason

- The linking step's order of processing and batch size (200): internal; the host
  tests prove the outcome.
- A foreign-key race on the course during a write (db-design §7): not
  reproducible deterministically; the stale-state and not-found paths are covered.
- The SQL "has a candidate" against the in-memory rule on every date: covered by
  one Kyiv boundary through the page and the unit boundaries of `RosterOnDate`.
- Live Google data: never (TC-4).

## 7. Known limitations

- The watermark after a linking failure is not asserted: spec FR-006 says the
  Meet step's results "stay", while US-031 moves the watermark only in
  `CompleteRun`; either reading keeps the next run correct (overlap re-read).
  Recorded for IMPLEMENTATION in the test-generation report.

## 8. Open Decisions

OD-001 … OD-008 resolved before activation; OD-009 (compile-only skeleton)
resolved (a) by the Owner on 2026-10-10. None open.
