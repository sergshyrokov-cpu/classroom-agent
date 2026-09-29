---
artifact_type: test_strategy
story: US-015
version: 1
status: DRAFT
created_at: 2026-09-28T13:22:00Z
updated_at: 2026-09-28T15:03:58Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-015-sync-coursework-and-submissions.md
    version: null
  - path: docs/specifications/US-015-spec.md
    version: 2
  - path: docs/designs/api/US-015-api-design.md
    version: 2
  - path: docs/designs/database/US-015-db-design.md
    version: 1
  - path: docs/designs/database/US-015-entity-model.md
    version: 1
  - path: docs/decisions/US-015-open-decisions.md
    version: 2
supersedes: null
---

# US-015 Test Strategy — Sync coursework and submissions

## 1. Scope

What these tests must prove: a synchronization run imports each course's
coursework, materials and submissions; stores exactly the facts PC-13 and §3
allow and nothing more; honours the §5 age rule for a course the database does
not yet hold; creates the BR-051 off-roster membership for a submitter never
seen on a roster; and writes none of it — least of all a grade — into a log.

Out of scope for tests as for the Story: journal cells (BR-056 — US-025), retry
and error classification (US-017), incremental behaviour (US-018), the purge
(US-037), and any screen.

**There is no HTTP surface** (FR-019, api-design v2 `NOT_APPLICABLE`), so this
Story writes **no contract tests**: no endpoint, no antiforgery case, no
allowed-role / forbidden-role pair. TC-5's endpoint rules are satisfied by the
existing enumeration test continuing to pass with **no new row** — which is
itself asserted.

## 2. Test levels

| Level | Used for | Why |
|---|---|---|
| Unit (use case, ports substituted — TC-1) | the import step, the age rule, attribution, the off-roster membership, refusals | the behaviour lives in `RunSynchronizationUseCase`; substituting `IClassroomReader` is also what keeps TC-4 true |
| Unit (domain) | truncation, the derived kind, the state/raw-state pair | invariants of `CourseWork` and `Submission`, testable without a host |
| Integration (Testcontainers, TC-2) | every constraint and index of db-design §10 | the InMemory provider enforces none of them and is forbidden |
| Integration (host) | the refusal to start without `Retention:Years`; log content | both are properties of the running host, not of a use case |
| Security | read-only refusal, log content, the unchanged endpoint enumeration, no audit row | SC-5, SC-10, SC-4, SC-11 |

## 3. Test classes

New:

| Class | Location | Covers |
|---|---|---|
| `CourseWorkImportTests` | `Application/UseCases` | AC-001, AC-003 — both resources, paging, the `PUBLISHED` filter, the date cascade, idempotent upsert |
| `SubmissionImportTests` | `Application/UseCases` | AC-002, AC-003 — one call per course, attribution, the stored facts, the unrecognised state |
| `CourseAgeRuleTests` | `Application/UseCases` | AC-010 — the §5 rule, the already-imported exemption, the uncounted skip |
| `SubmitterWithoutRosterTests` | `Application/UseCases` | AC-004 — BR-051 v56 |
| `CourseWorkRefusalTests` | `Application/UseCases` | AC-006 — read-only reaches neither new port member and writes nothing |
| `CourseWorkFailureTests` | `Application/UseCases` | AC-007 — a failure part-way leaves only complete courses |
| `CourseWorkInvariantTests` | `Application/UseCases` | truncation, `Kind`, the unimportable cases |
| `SubmissionInvariantTests` | `Application/UseCases` | the `SetState` biconditional, grades stored as given |
| `CourseWorkSchemaTests` | `Web/Persistence` | db-design §10 items 1, 2, 3, 6, 7, 8, 11, 13 |
| `SubmissionSchemaTests` | `Web/Persistence` | db-design §10 items 4, 5, 8, 9, 10, 12, 13 |
| `RetentionConfigurationTests` | `Web/Configuration` | VR-008 — the installation refuses to start |
| `SubmissionImportLoggingTests` | `Web/Logging` | AC-008 — no grade, name, email or title in any line |

Modified: `AppUserMigrationTests` (seven → eight migrations, nine → eleven
tables) — an **expected** change db-design §10 item 14 foresaw, keeping its own
assertions and growing only the sample set.

New fixtures: `CourseWorkTestData` (synthetic titles, ids, states — TC-4) and
`CourseWorkRows` (direct inserts that bypass the entities, so a database
constraint is tested separately from a domain guard), beside the existing
`CourseTestData` / `CourseRows`.

## 4. Scenarios

### 4.1 Positive

- both Classroom resources are imported for a course, each paged to the end;
- a `courseWork` with maximum points, one without, and a material — all three
  stored, the kind derived rather than stored;
- the date cascade resolves at each of its four levels;
- submissions are read **once per course** and attributed by their own
  `courseWorkId`;
- `assignedGrade`, `draftGrade`, `late` and the last turn-in are stored exactly
  as Google gave them;
- a second identical run changes nothing and duplicates nothing;
- a course whose last activity is within N is imported in full;
- a person known only through submissions receives a `student` membership off
  the roster, both dates at the run's instant.

### 4.2 Negative

- a course not yet in the database whose last activity is older than N leaves
  **nothing** behind — no course, participant, membership, coursework or
  submission;
- a `DRAFT` or `DELETED` item is not imported;
- a material is never queried for submissions;
- in read-only mode neither new port member is called and no row is written;
- a Classroom failure part-way leaves only complete courses and a failed
  `SyncState`;
- an item missing its Google id, its resource or every cascade date is refused.

### 4.3 Boundary

- paging with exactly two pages for coursework, for materials and for
  submissions — the US-014 limitation is inherited and restated: a token
  mishandled only after the second page would not be caught;
- a course whose last activity is **exactly** N years before the run instant,
  and one a day either side;
- a title one character over `MaxTitleLength`;
- `max_points` absent, zero and positive;
- a submission whose history carries no turn-in at all.

### 4.4 Validation

- `Retention:Years` absent, non-numeric, zero and negative — each refuses to
  start; a valid value starts (VR-008);
- a state outside the six recognised values is stored with the marker and the
  raw string (VR-004), and the run still completes;
- negative points or grades are rejected by the database (VR-001).

### 4.5 Security

- **read-only**: the substituted reader records **no** call, and the same seeded
  world allowed to run **does** call it and **does** write — the control that
  keeps the refusal test from being vacuous;
- **logs**: a run that imported graded submissions writes no grade, no name, no
  email, no Google person id and no title, asserted at host level with a control
  proving the import really happened;
- the OD-005 Warning line carries the raw state string and the submission id and
  nothing else;
- the endpoint enumeration test gains **no** row (FR-019, SC-4);
- `audit_event` gains no row from a scheduled run (FR-020, SC-11);
- fixtures are wholly synthetic, `school-one.example.test` (TC-4).

### 4.6 Persistence

Every item of db-design §10, against real PostgreSQL. The three that carry the
most weight, because each is a **positive** assertion guarding a decision:

1. the same Google id **in two different courses** must succeed
   (Specification v2);
2. the same submission id **under a different piece of work** must succeed (v2);
3. **two submissions of one item by one student** must succeed (db-design §4.3).

Each fails the moment someone narrows the corresponding index, which is exactly
what they exist for.

## 5. Required fixtures

- `CourseWorkTestData` — synthetic ids, titles, resources, states, the
  unrecognised state string, and the `Retention:Years` values;
- `CourseWorkRows` — direct `INSERT` helpers for `course_work` and `submission`,
  bypassing the entities;
- the existing `SyncWorld` / `SyncTestData` / `CourseTestData`, extended with
  coursework and submissions;
- `FakeClassroomReader` — extended with the two new members; already registered
  for every host test by US-014's D-4, which is what keeps host tests offline.

## 6. Excluded scenarios, with justification

| Excluded | Why |
|---|---|
| Journal cell computation | BR-056 belongs to US-025; this Story stores facts and computes no cell |
| Retry, backoff, failure classification | US-017 (OD-008); a failure here simply propagates |
| The purge | US-037; this Story stores the dates it needs and deletes nothing |
| Any HTTP behaviour | no endpoint exists (FR-019) |
| A live Classroom call | forbidden by TC-4 |

## 7. Known limitations

These are limits, not gaps, and must not be mistaken for coverage:

- **Google's real shapes are only as accurate as the fixtures.** A renamed field
  would pass here and fail in production. US-011's live "check access" is the
  mitigation, not a test.
- **Paging is proved with two pages.**
- **OD-010 is unresolved in the world, not in the code.** Whether Classroom
  returns the submissions of a student removed from a roster is
  `trebovaniya.md` §7 item 14, still open. AC-004's test proves the **program's**
  rule on synthetic data; it cannot prove the path ever fires on a live domain.
- **The due-date fallback is untestable against Google.** db-design §3.4 stores a
  date without a time as *no due date*; the fixture asserts the program's
  behaviour, not that Classroom can produce that shape.
- **`STUDENT_EDITED_AFTER_TURN_IN` may not exist in the API** (OD-011). The test
  asserts it is a permitted stored value, which is all this Story can know.
- **Until the migration exists**, the schema tests fail on **missing tables**
  rather than on a wrong constraint (OD-012), so in the red phase they cannot
  distinguish a correct constraint from an absent one.

## 8. Open Decisions affecting testing

- **OD-012** (resolved) — the compile-only skeleton is what lets these tests
  compile at all. Its deliberate exclusions (EF configurations, `DbSet`s,
  migration) are the reason for the last limitation above.
- **OD-005 / OD-011** (resolved) — fix the vocabulary these tests assert.
- **OD-010** (resolved as "proceed") — bounds what AC-004's test may claim.

## 9. Red-phase expectation

Every new behaviour test must fail before implementation, and **fail for the
right reason**: `NotImplementedException` from the skeleton, or a missing table.
Two shapes get special attention, because US-014 found six vacuous passes of
exactly these kinds:

1. **"X did not happen" is free when nothing happens.** Every refusal test —
   read-only, the age skip, the material not queried — is paired with a
   **control**: the same world, allowed to proceed, must reach the port and
   write. Without the control, deleting the guard later would keep the test
   green.
2. **An absence asserted inside an absence.** A schema test that queries
   `information_schema` for a table that does not exist returns nothing and
   "passes". Each such test asserts the table's **presence** first.

Any new test that passes before implementation is investigated and its
conclusion recorded in the test-generation report, never left unexplained.
