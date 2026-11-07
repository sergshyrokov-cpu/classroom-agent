---
artifact_type: test_strategy
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T20:09:38Z
updated_at: 2026-11-06T20:09:38Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-074-meet-code-organizer-by-name.md
    version: null
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/designs/api/US-074-api-design.md
    version: 1
  - path: docs/designs/api/US-074-openapi.yaml
    version: 1
  - path: docs/designs/database/US-074-db-design.md
    version: 1
  - path: docs/designs/database/US-074-entity-model.md
    version: 1
  - path: docs/decisions/US-074-open-decisions.md
    version: 1
supersedes: null
---

# US-074 Test Strategy — organizer names on the Meet code lists and in their search

## 1. Scope

What the Story changes and the tests prove:

- **Who an organizer is and what they are called** (spec FR-001, db-design §2): distinct non-null `organizer_email` of the
  code's sessions, compared lower-cased; case-only duplicates merged with the ordinally first spelling (I-1); the chosen
  name is the lowest-id participant of that email with a non-null `full_name`, and a blank chosen name is no name.
- **Display** on the three lists (FR-002): name then email as `_MeetPerson.cshtml` renders it, email alone without a name,
  each organizer once in ordinal email order, Razor-encoded.
- **Search** by the chosen name (FR-003) on the three lists, their counters, paging and row actions, and through the same
  read in the header's "Meet codes" group (FR-004).
- **Bounded reads** (FR-005): names for a page in a number of commands independent of the number of rows; a name search
  measured at the expected volume.
- **The same name as the Meet reports** (AC-009).
- **Unchanged rights, read-only mode, no log, no audit** (AC-007).

Not in scope (spec section 10): the Meet reports and exports, the course-choice page, the search field wording, the
accounts that acted on a code, former names.

## 2. Test levels

| Level | Where | Why |
|---|---|---|
| Integration, read source | `Infrastructure/Persistence/MeetCodeOrganizerNameSourceTests` | The name rule and the name search are SQL over real PostgreSQL (TC-2); the read source is where display and search share one definition, and where the commands can be counted |
| Integration, HTTP | `Web/Pages/MeetCodeOrganizerNamePageTests` | What the Dean and the Admin see: markup, encoding, counters, paging, row action, header group (openapi `getMeetCodes`, `getSearchResults`) |
| Security, HTTP | `Web/Security/MeetCodeOrganizerNameAccessTests` | TC-5 allowed/forbidden callers for both pages under a name search, read-only mode, no Google call, no log line, no audit row |
| Measurement | `Web/UseCases/MeetCodeNameSearchMeasurementTests` | AC-008 volume: asserts what is found, records times, as US-071 did |

No unit test of `GetMeetCodesQuery` mapping alone: the mapping is a single call to `MeetReportRules.Name` (internal, not
visible to tests), and its observable effect (a blank chosen name shows the email alone) is proved over HTTP, which is
stronger evidence.

## 3. Scenarios

### Positive
- A named organizer shows name + email on each list, for Dean and Admin, in Ukrainian and English (AC-001).
- Search by a part of the name, words in any order, other case, repeated white space, apostrophe variants (AC-005).
- Words across fields: a name word with a code, course or email word; two words in the names of two organizers of one
  code (I-2).
- Header search by name lists the code with its list link; 21 matches give the limit line and the whole-list link (AC-006).

### Negative
- No participant / null name / blank chosen name → email alone (AC-002).
- A non-chosen name of the same email, a surname or given name stored apart, finds nothing (I-3, BR-085).
- Another organizer's code is not found; an unmatched word excludes the code.
- An erased or excluded (cleared) organizer: no name, no email shown, not found by name or email (AC-004).

### Boundary
- The six cases of entity model §4, including blank-then-named (blank wins, then no name) and null-then-named.
- Case-only duplicate emails become one organizer; an organizer email in another case than the participant's still finds the
  name.
- Paging at 25 matches with a page of 20; the header at 21 matches.

### Validation
- The search text's validation is unchanged (US-070 VR-001…VR-004 tests stay); a name is output only and is encoded
  (VR-002): a name holding `<b>&"'` is rendered as text.

### Security
- Admin and Dean allowed, anonymous sent to sign-in, a Dean on the forced password change sent to the change, on the codes
  page and the header search, under a name search (TC-5).
- Read-only mode: both work and no Google port is called (AD-6, BR-026).
- The name word is a bound parameter (VR-003); no log line and no audit row holds it (SC-10, SC-11).

### Persistence
- No schema change (db-design §8): no migration test. Reading and searching write nothing (participant count and link
  fingerprint unchanged).
- Command count: a page of ten codes with two named organizers each takes as many commands as a page of one (FR-005).

## 4. Fixtures

- `PostgreSqlFixture` + `InstallationTestHost` / `JournalHostExtensions.StartAsync` with migrations applied (TC-2).
- Participants inserted with an exact `full_name` through `CourseRows.InsertParticipantAsync`; ids grow in insertion order,
  which the lowest-id rule relies on.
- Organizers are emails from `MeetCodesHostExtensions.Email`, never roster teachers seeded by `SeedRosterCourseAsync` (those
  get the name "Test Person …").
- Real erasure through `EraseAsync` for AC-004 over HTTP; a direct `UPDATE … SET organizer_email = NULL` at the source level,
  which is the stored state both erasure and exclusion leave (`MeetSession.ClearOrganizer`, US-038 I-9).
- Google ports are the existing substitutes; `GoogleCallCount()` proves no call (TC-4).

## 5. Excluded scenarios

- **Exclusion as a separate HTTP flow.** Exclusion and erasure leave the same stored state (null `organizer_email`); the read
  under test sees only that state. Erasure is driven end to end; exclusion is covered by the cleared-state source test.
- **Non-ASCII emails** (db-design §4 `lower()` vs `ToLowerInvariant()`): Google Workspace addresses are ASCII; the existing code
  relies on the same equivalence.
- **A timing threshold** for AC-008: the suite records times, it does not fail on them (as US-071).

## 6. Known limitations

- Existing tests that compare an organizer cell to a bare email still pass because their organizers are not participants.
  A test whose organizer is a roster teacher (named "Test Person …") would now see a name; IMPLEMENTATION must re-run the
  Meet-code and header-search classes and report any such change as a consequence of the Story, not weaken an assertion.
- Some new tests pass before implementation by construction — those whose expectation is "email alone" or "not found", and
  the command-count comparison. Each is paired with a control in the same test or class that fails in the red phase; the
  test generation report lists them.

## 7. Open Decisions affecting testing

None. `docs/decisions/US-074-open-decisions.md` v1 holds no open item; SPEC_REVIEW m-1 was closed by db-design §2, whose six
cases are tested here.
