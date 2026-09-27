---
artifact_type: test_strategy
story: US-014
version: 1
status: DRAFT
created_at: 2026-09-27T19:14:37Z
updated_at: 2026-09-27T19:14:37Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-014-sync-courses-and-rosters.md
    version: null
  - path: docs/specifications/US-014-spec.md
    version: 2
  - path: docs/designs/api/US-014-api-design.md
    version: 2
  - path: docs/designs/database/US-014-db-design.md
    version: 1
  - path: docs/designs/database/US-014-entity-model.md
    version: 2
  - path: docs/decisions/US-014-open-decisions.md
    version: 2
supersedes: null
---

# US-014 Test Strategy — Sync courses and rosters

## 1. Scope

This Story adds the first step of an existing background run: a Google Classroom port,
three entities with one migration, an upsert, the BR-051 observation rule and a
transaction per course. It adds **no endpoint, page, policy or route** (spec FR-018,
api-design v2 `NOT_APPLICABLE`), so there is no contract test level here at all — the
first Story since US-007 and US-013 for which that is true.

What must be proved:

- the data that arrives (AC-001 … AC-004);
- the data that must **not** move — a course Google stopped returning, a membership of
  someone who left (AC-004, AC-005);
- the refusals, in the Application layer with the port substituted (AC-006);
- consistency when a run fails part-way (AC-007);
- that nothing personal reaches a log and nothing leaves the installation (AC-008);
- that the tests themselves never touch Google and that the schema is checked against
  real PostgreSQL (AC-009).

## 2. Test levels and why each is used

| Level | Used for | Why not a cheaper level |
|---|---|---|
| **Application / unit** (`SyncWorld`, ports in memory) | the import's behaviour: what is written, in what order, what is skipped, what is left alone | the rules live in the use case; a host test would prove the same thing more slowly and less precisely |
| **Integration** (Testcontainers, real PostgreSQL) | every constraint, index and nullability of db-design §3 … §5 | the InMemory provider enforces none of them (TC-2), so an in-memory test would assert the fixture rather than the schema |
| **Adapter** (scripted `HttpMessageHandler`) | paging, impersonation, the state passed through as a string, and that every request goes to Google | paging is a property of the adapter: at the port it is invisible by design (VR-005), so no Application test can see it |
| **Security** | read-only refusal, SC-13 destinations, SC-10 diagnosis, absence of an audit row | each is a requirement, not a side effect |

There is no contract level, and no antiforgery, cookie, role or anonymous-access test:
this Story adds no HTTP surface (FR-018, VR-007), so the TC-5 items that apply to
endpoints have nothing to attach to. The existing endpoint-enumeration test must keep
passing **unchanged** — that is the assertion that no surface was added.

## 3. Positive scenarios

Courses imported with the §3 fields; both rosters imported with the role on the
membership; co-teachers imported; one person on two courses stored once with two
memberships; the first sighting recording both instants and the flag; a return to a
roster reusing the row; a second run changing nothing; the counter reporting courses.

## 4. Negative scenarios

A duplicate Google course id and a duplicate Google user id rejected by the database; a
second membership of the same (course, person) pair rejected; a role outside the
vocabulary rejected; a course state outside the five rejected; observation instants out
of order rejected; deleting a course or a participant that still has memberships
refused.

## 5. Boundary scenarios

Equal observation instants — the legitimate "seen in this very run" shape, which is why
the constraint is `>=`; an empty course list, which is success and not an error; an
empty roster, which marks memberships off, against a **failed** roster read, which must
not; a course with no owner and no Google instants, which is storable by design.

## 6. Validation scenarios

The state vocabulary and the role vocabulary, each as a closed list with a check
constraint; the string bounds of db-design §3.1 asserted through
`information_schema`; the optional address stored lower-cased; a profile with no
address carried through rather than replaced by a placeholder (the prototype's
`ID: <id>` substitution is not the requirement).

## 7. Security scenarios

- read-only mode: no call through `IClassroomReader` — not even the token request — and
  no row written, proved in the Application layer (SC-5, AD-6, TC-5);
- no saved connection: the same, for the US-013 AC-005 reason;
- the guard consulted **first**, before the port or any repository;
- the token impersonating the school's technical account, never a person and never a
  super-admin (BR-015, S-03) — the prototype's `with_subject` of a super-admin is what
  this asserts against;
- every adapter request going to a `googleapis.com` host and nowhere else (SC-13);
- the stored failure diagnosis carrying a category and a type name and **not** the
  exception's message (SC-10);
- no audit row and no audit vocabulary change (FR-019), asserted by the existing audit
  tests continuing to pass unchanged.

## 8. Persistence scenarios

The twelve guards of db-design §9, all on real PostgreSQL: the two unique indexes; the
non-unique email index **and** the positive case of two people sharing an address; the
membership uniqueness on (course, person); the two check constraints; the nullable
columns; the two `Restrict` refusals; the roster-on-a-date composite and its column
order; the migration count six → seven and the table set gaining three.

## 9. Required fixtures

| Fixture | Purpose |
|---|---|
| `SyncWorld` (extended) | the run use case with every port in memory, now including the Classroom reader and the three repositories; empty by default so every US-013 test stays true |
| `SyncWorld.ClassroomReader` | seeds courses and rosters, records impersonation and roster reads, and can fail a named roster or fail after the courses |
| `CourseTestData` | table, constraint and index names; the state and role codes; synthetic ids, names and addresses |
| `CourseRows` | direct SQL inserts with constraint-satisfying defaults, so a schema test asserts the **database** rather than an entity guard |
| `ScriptedHttpHandler` (existing) | the adapter's offline transport |
| `DictionarySecretStore`, `SyntheticServiceAccountKey` (existing) | a key generated at run time; no real key anywhere (TC-4) |
| `PostgreSqlFixture`, `InstallationTestHost` (existing) | an isolated database per test class, schema from the migrations |

**Every person, course, address and domain in these fixtures is invented.** This is the
first Story whose fixtures describe people at all, so TC-4's prohibition on a real
roster, name, address or school domain binds here for the first time.

## 10. Excluded scenarios, with justification

| Excluded | Why |
|---|---|
| The "course older than N years" rule | deferred by OD-001; there is no behaviour to test. The nullability test records what the future rule inherits |
| Retry, backoff, permission-failure classification | US-017 (OD-008); this Story records a failure and schedules nothing |
| Incremental behaviour (BR-042) | US-018 (OD-007) |
| The purge's queries and its delete order | US-037; the two `Restrict` tests record the constraint it must respect |
| An off-roster membership for a student with submissions but never seen on a roster | needs `Submission`, so US-015 (BR-051 v56) |
| Any HTTP-level test | no surface exists (FR-018) |
| A live Google call or a live domain check | forbidden (TC-4); verifying the technical account's Workspace roles is a deployment task (OD-003, §7 item 10) |

## 11. Known limitations

- **A real Classroom response shape is only as accurate as the fixture.** The adapter
  tests assert against synthetic JSON written from Google's documented shapes; a field
  Google renames would pass here and fail in production. The mitigation is US-011's
  live "check access", not a test.
- **Paging is asserted with two pages.** A defect that appears only at the third page —
  a token mishandled after the second — would not be caught.
- **The both-rosters tie-break is tested through the port, not against Classroom.**
  Whether Classroom can return a person on both rosters of one course was not verified
  on a live domain (spec I-9 records this), so the test proves the program's rule, not
  Google's behaviour.
- **`ImpersonatedAs` proves the address reaches the port, not that Google honours it.**
  The adapter test asserts the JWT `sub` claim, which is as far as an offline test can
  go.
- **The schema tests bypass the entities deliberately.** They prove the database
  enforces its constraints; that the entities also refuse bad states is asserted
  separately, in the Application tests.

## 12. Open Decisions affecting testing

- **OD-012** (resolved) — the compile-only skeleton. Tests are written against the
  approved artifacts, so the types they name exist only as declarations whose bodies
  throw. Every new behaviour test therefore fails for that one reason until
  IMPLEMENTATION, and the schema tests fail on missing tables rather than on a wrong
  constraint (the EF configurations and the migration are deliberately outside the
  skeleton).
- **OD-010** (resolved) — an unrecognised course state is tested twice: the database
  rejects the value, and the Application skips the course while the run completes. The
  second is the one that must hold; the first must never be what stops a run.
- **OD-011** (resolved) — the non-unique address is asserted **positively**. That test
  is the guard against a later migration adding `IsUnique()`.

No Open Decision remains unresolved, and none blocks test creation.
