---
artifact_type: test_strategy
story: US-038
version: 1
status: DRAFT
created_at: 2026-10-15T07:00:00Z
updated_at: 2026-10-15T07:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-038-erase-participant.md
    version: null
  - path: docs/specifications/US-038-spec.md
    version: 1
  - path: docs/decisions/US-038-open-decisions.md
    version: 3
  - path: docs/designs/api/US-038-api-design.md
    version: 1
  - path: docs/designs/api/US-038-openapi.yaml
    version: 1
  - path: docs/designs/database/US-038-db-design.md
    version: 1
  - path: docs/designs/database/US-038-entity-model.md
    version: 1
supersedes: null
---

# US-038 Test Strategy — Erase all data of one participant on request

## 1. Scope

Everything the Specification adds: the Admin-only erasure page with search and
lifting, the confirmation, the erasure transaction, the exclusion and its salt,
the exclusion check in synchronization and in the Meet load (with the write
fence of db-design §7.3), audit, log, read-only refusal, translations and the
schema of migration `AddParticipantErasure`.

Not in scope: anything the Specification puts out of scope (§10) — writes to
Google, exports already made, restores, a JSON API, a list of exclusions.

## 2. Test levels

| Level | What it proves | Classes |
|---|---|---|
| Unit (Application, every port substituted, TC-1) | evaluation order (guard first, mark next, fence first in the transaction), what is written and what is not, validation VR-001 … VR-003, read-only refusal and its audit (TC-5), domain invariants of the fingerprint, salt, exclusion, counts and audit factories | `EraseParticipantUseCaseTests`, `LiftErasureExclusionUseCaseTests`, `ErasureQueryTests`, `ErasureInvariantTests` |
| Integration (host + PostgreSQL via Testcontainers, TC-2) | the real erasure: what is deleted and cleared, others unchanged, rollback, salt, exclusion columns, audit row and counts, concurrency; search and confirmation over the real read source; synchronization and Meet load skipping excluded people, including the fence race (AC-010) | `ParticipantErasureTests`, `ErasureSearchTests`, `ErasureExclusionSyncTests`, `ErasureExclusionMeetTests` |
| Persistence (migrated schema, TC-2) | every column, unique index, check and expression index of db-design §2 … §6 and §9 | `ParticipantErasureSchemaTests`; amended: `MeetSchemaTests`, `AppUserMigrationTests`, `InstallationAuditEventSchemaTests`, `DeanAccountAuditSchemaTests`, `CourseSchemaTests` |
| HTTP contract and security (`WebApplicationFactory`, TC-3, TC-5) | the five operations' status codes, redirects and markup; Admin allowed, Dean `403`, anonymous and restricted Dean redirected; antiforgery `400`; `404` for unknown and malformed ids; read-only `409` with one refused audit row; home-page link | `ErasureAuthorizationTests`, `ErasurePageTests`, `ErasureReadOnlyHttpTests` |
| Translations and log (TC-8, SC-10) | every key in `uk` and `en`; pages render in both; one `Information` line per erasure with id and counts; no personal datum, term or email in any log line | `ErasureTranslationTests`, `ErasureLoggingTests` |

There is no API-6 JSON error body to assert: every response is HTML (api-design
§5). TC-3's error-body rule is applied as "the translated error page with no
detail" for `403`, `404`, `409`.

## 3. Scenarios

### 3.1 Seeded world (`ErasureHostExtensions.SeedErasureWorldAsync`)

Inserted by SQL so the tests do not depend on the code under test:

- courses Фізика 9-А (owner the teacher), Хімія 9-А (owner the target),
  Історія 9-А (owner the other student);
- the **target** Олена Коваль (`o.koval@…`), a student of Фізика and Хімія,
  three submissions — assigned grade, draft grade, none;
- the **other** student Петро Мельник, in Фізика, one graded submission; the
  **teacher** Тарас Шевченко teaches Фізика;
- meeting 1 by the teacher: the target twice (once as `O.Koval@SCHOOL-ONE…`),
  the other, the teacher; meeting 2 organized by the target (organizer stored
  upper-cased): the target, the other; meeting 3 by the other: the teacher.

Expected for the target: courses 2, submissions 3, grades 2, Meet
participations 3, organized meetings 1, owned courses 1. Erasure counts:
memberships 2, submissions 3, Meet participations 3, organizers cleared 1,
owners cleared 1.

### 3.2 Positive

- Search by part of surname, given name, full name or email, any letter case;
  a person off every roster is found; ordering by full name, email, id.
- Confirmation shows name, email, courses with role, the five counts, the export
  warning, the mark and the button.
- Erasure with the mark: participant, memberships, submissions, Meet
  participations (any letter case) gone; organizer email and course owner
  cleared; one exclusion (Google user id, fingerprint with the stored salt,
  date); one audit row with counts; one `Information` log line; PRG to the page
  with the one-time message.
- Lifting by the email in any case removes every matching exclusion with one
  audit row each; the next synchronization loads the person as new.
- Synchronization and Meet load skip excluded people; others are stored as usual.

### 3.3 Negative

- Mark unset or not exactly `true` → `400`, nothing changes, no audit row.
- Unknown id → `404`; malformed id → `404`; a second or concurrent erasure of
  one id → not found, one exclusion only.
- Lifting with an email that has no exclusion → `200` "no exclusion", the email
  kept in the field, nothing changes.
- Failure inside the erasure transaction → nothing erased, no exclusion, no
  audit row.
- Dean `403`; anonymous → sign-in; Dean with a temporary password → forced
  change; GET to the erasure address erases nothing.

### 3.4 Boundary

- Term 1 character (after trimming) refused, 2 accepted; 254 accepted, 255
  refused; blank refused.
- Email 254 accepted (after trimming), 255 refused.
- 50 matches not truncated; 51 → 50 shown and the refine message.
- Salt and fingerprint exactly 32 bytes; Google user id up to 64 characters.

### 3.5 Validation

- VR-001: literal term — `%`, `_`, `\` match only themselves.
- VR-002: `true`, `True`, `TRUE` set the mark; absent, empty, `false`, `on`,
  `1`, `yes` do not.
- VR-003: one `@`, non-empty parts, no whitespace; any domain.
- VR-004: fingerprint and Meet matching ignore letter case and surrounding
  spaces.
- VR-005: antiforgery on the three `POST`s; route id digits only.

### 3.6 Security

- Admin allowed / Dean forbidden on all five operations (TC-5); policy
  `EraseParticipantData` admits Admin only; every route declares it; none is
  anonymous.
- Read-only mode, all three BR-025 causes: erasure and lifting refused with
  `ReadOnlyModeException` in Application and `409` over HTTP, even with an
  invalid body; one refused audit row under the `AuditEvent` service write;
  page, search and confirmation still open with the notice; the button disabled.
- No audit row, no log line contains the name, email, Google user id,
  fingerprint, search term or email to lift (S-06).
- The exclusion table has no name or email column (S-05); the fingerprint's and
  salt's text show nothing of their value.
- The term and email never travel in an address (S-09): search answered `200`
  without `Location`; a query-string term is ignored; redirects carry no email.

### 3.7 Persistence

- `erased_participant` and `erasure_salt` columns, unique and check
  constraints, immutability, no foreign key; migration seeds no salt.
- `meet_session.organizer_email` nullable, short value still rejected.
- Expression indexes `lower(organizer_email)`, `lower(email)`; owner index.
- Audit: five count columns and five checks accept the designed rows and reject
  missing counts, counts on other rows, negatives, a Dean actor, a wrong target.
- Retention purge leaves exclusions and the salt untouched (FR-017).
- The write fence: a synchronization course transaction and a Meet page
  transaction that reach their write while an erasure holds the exclusive
  advisory lock (`0x4552415345`) wait for it and see its exclusion (AC-010).

## 4. Markup hooks (binding for IMPLEMENTATION)

Defined in `TestInfrastructure/ErasureTestData.Markup`; IMPLEMENTATION renders
them exactly.

| Hook | Where |
|---|---|
| `nav#admin-settings` gains `<a href="/settings/erasure">` | home page, Admin only |
| `#erasure-search-form` (input `term`), `#erasure-lift-form` (input `email`) | erasure page |
| `#erasure-results`; each result carries `data-participant-id="<id>"` and links to its confirmation | after a search |
| `#erasure-truncated`, `#erasure-none-found`, `#erasure-search-error` | search outcomes |
| `#erasure-no-exclusion`, `#erasure-lift-error` | lifting outcomes |
| `#erasure-message` | one-time message after a redirect |
| `#erasure-read-only-notice` | page and confirmation in read-only mode |
| `#erasure-confirmation`, `#erasure-courses` (one `li` per membership) | confirmation |
| `data-erasure-count="submissions|grades|meet-participations|organized-meetings|owned-courses"` on the element whose only text is the number | confirmation |
| `#erasure-export-warning`, `#erasure-no-email-notice`, `#erasure-mark-error` | confirmation |
| `#erasure-form` with the checkbox `irreversibleConfirmed` and the submit button, `disabled` in read-only mode | confirmation |

## 5. Translation keys (binding for IMPLEMENTATION)

`ErasureTestData.Keys.All` — 37 keys under `Erasure.*`: title and navigation
entry; search label, button, none found, truncated, no name, no email, teacher
and student course counts, three term errors; lifting title, label, button, no
exclusion, three email errors; the two one-time messages; the read-only notice;
the confirmation title, courses, five count labels, export warning, no-email
notice, mark, button, mark required; the two role names. Each exists in `uk`
and `en`, non-empty, and the two differ.

## 6. Log event (binding for IMPLEMENTATION)

The Specification names none, so the tests fix it (the US-031 precedent): one
`Information` event `ParticipantErased` per erasure, after commit, with
properties `ParticipantId`, `Memberships`, `Submissions`, `MeetParticipations`,
`ClearedMeetOrganizers`, `ClearedCourseOwners`. Written by the host from the
counts the use case returns (Application has no logger).

## 7. Fixtures

| Fixture | Purpose |
|---|---|
| `ErasureTestData` | paths, policy, patterns, fields, markup hooks, keys, log event, stored codes, schema names, fence key, a known salt and an independent HMAC-SHA-256 fingerprint |
| `ErasureHostExtensions` | the seeded world; use cases and queries resolved from the host container; exclusion seeding by SQL; row readers; the "nothing changed" fingerprint (absent tables tolerated); HTTP journeys |
| `ErasureUnitWorld` | every port in memory with one ordered call log |
| `FakeClassroomReader.WithCourse` | gains an optional `ownerGoogleId` (additive) |

## 8. Excluded scenarios

- Migration `Up` over existing Meet rows keeping their emails (db-design §14):
  the test database is created by the migrations, so there is no pre-existing
  data to carry; the column change only drops `NOT NULL`. Covered by review.
- The "erasure waits for an in-flight writer" direction of the fence: the same
  lock serves both directions; the writer-waits direction is the one the
  Specification demands (FR-011) and is tested.
- Phone layout (NFR-070): checked at review.
- `Down` of the migration: one-way once erasures happened (db-design §11).

## 9. Known limitations

- `SyncWorld` constructs `RunSynchronizationUseCase` positionally; when
  IMPLEMENTATION adds the fence and exclusion ports to that constructor it must
  pass them in `SyncWorld` (a supporting test-fixture change). The exclusion
  behaviour of synchronization is therefore tested through the host, not with
  `SyncWorld`.
- Case-insensitive Cyrillic search relies on the database's `lower()`; the
  Testcontainers PostgreSQL image uses a UTF-8 locale.
- The fence tests wait for an ungranted advisory lock in `pg_locks`; no other
  code takes advisory locks today.

## 10. Open Decisions affecting testing

- OD-001 (resolved a): the no-email cases (AC-019) are tested as resolved.
- OD-002 (resolved a, raised here): the compile-only skeleton the tests compile
  against.

No open decision remains.
