---
artifact_type: test_strategy
story: US-071
version: 1
status: DRAFT
created_at: 2026-11-03T17:11:55Z
updated_at: 2026-11-03T17:11:55Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-071-search-field-in-the-header.md
    version: null
  - path: docs/specifications/US-071-spec.md
    version: 2
  - path: docs/decisions/US-071-open-decisions.md
    version: 1
  - path: docs/designs/api/US-071-api-design.md
    version: 1
  - path: docs/designs/api/US-071-openapi.yaml
    version: 1
  - path: docs/designs/database/US-071-db-design.md
    version: 1
  - path: docs/designs/database/US-071-entity-model.md
    version: 1
supersedes: null
---

# US-071 Test Strategy

## 1. Scope

The header field and the results page (`GET /workspace/search`), the student and Meet code reads behind it, the error page's header (FR-010), the final hint texts (FR-011), the grey disabled button rule (FR-012), and the tests left over from US-070 (FR-013). No schema change, so no migration test.

## 2. Levels

| Level | Where | What |
|---|---|---|
| Unit, ports substituted | `Application/UseCases/GetSearchResultsQueryTests` | states, what is read in each, mapping, the BR-068 mark in Kyiv time, totals, return path |
| Integration on real PostgreSQL | `Infrastructure/Persistence/StudentSearchSourceTests` | who is a student of the search, matching, the courses' read, Meet first matches, what is not found, bound parameter, command counts |
| HTTP on real PostgreSQL | `Web/Pages/SearchResultsPageTests`, `HeaderSearchFieldTests`, `ErrorPageHeaderTests`, `MarkupTextSurvivesActionsTests`, `PageSearchHintTests` | markup hooks, groups, links, messages, encoding, the frame |
| Security | `Web/Security/SearchAccessTests`, `PermissionMatrixTests` | roles, anonymous, restricted session, read-only mode, no Google, no audit, logs, headers |
| Source scan | `Web/Localization/SearchTranslationTests`, `PageSearchHintTranslationTests`, `Architecture/DisabledButtonRuleTests`, `PickerNarrowScriptTests` | exact texts in both files, the CSS rule in both hosts, the script's behaviour in code |
| Measurement | `Web/UseCases/SearchMeasurementTests` | AC-010's data and texts; asserts what is found, records the times |
| Manual | Owner's walk, implementation report | AC-011: grey button and header field on a computer and a phone |

## 3. Scenarios

**Positive.** All four groups in order; each row's fields and links; both roles; both languages; a person in both roles; a 2-character text; two one-letter words; read-only mode.

**Negative.** A group with no match hidden; a teacher who left every roster; only a teacher is no student; a teacher's course not listed for a student; `full_name` not searched; no link builds a code, name or email into a query string; no field on the forced change, the error page, pages before sign-in and the Control Plane.

**Boundary.** 20 and 21 matches; 1 and 2 characters; 100 and 101 characters (via `CourseListQueryRules`, existing tests, plus the 101 refusal here); equal first and last sightings; 22:30 UTC as the next day in Kyiv; a Meet merge cut across lists.

**Validation.** VR-001's three refusals: `400`, no read, no echo, no text in the field or return path; VR-003's prompt: `200`, no read; VR-004: unknown parameters never echoed.

**Security.** Allowed and forbidden cases (TC-5); the permission matrix row; the text in no log line in any state; no audit, no write, no Google call; `no-store` and `Referrer-Policy`; markup characters encoded; `%`, `_`, `\` literal; the text as a bound parameter; the error page copies no raw query (AC-012).

**Persistence.** No schema change. Reads only, proved by the fingerprint; fixed commands (section 9); no read for no shown student.

**What must not be found (AC-006).** A real erasure through `EraseParticipantUseCase`, with a before-erasure control, at source and over HTTP; an exclusion entered in advance; a former surname and email after an update; an organizer stored as null, with a survivor as control.

## 4. Fixtures

`SearchTestData` (hooks, keys, readers), `SearchHostExtensions` (seeds), `FakeStudentSearchSource`, `FakeMeetCodesSearchSource`, and the existing `FakeTeacherListSource`, `FakeCourseListSource`, `InstallationTestHost`, `JournalHostExtensions.StartAsync`. Testcontainers, one database per test class; all data synthetic (TC-2, TC-4).

## 5. Decisions of this stage

- **"Readable text" is measured as 3:1** between the text and the background as shown with the rule's opacity (`DisabledButtonRuleTests.TheText_IsReadable`). FR-012 sets no number. This is a test's reading for a control that cannot be used, not a product rule; the person's walk is the final check. Bootstrap's default opacity of 0.65 fails it, so the rule must set its opacity too.
- **Greyness is at most 20 of 255 between channels**, the spread of Bootstrap's own bluish greys.
- **No row hook.** The contract gives no element for a row, so rows are read as text in the group's `<section>`, and a mark belongs to the course whose link precedes it.
- **The two `CourseListPageTests` reads by input name now read by id.** After the Story the first input named `q` is the header field; `InputValue(…, "q")` would read the wrong one, and on the refusal test would pass for free.

## 6. Excluded

| Scenario | Why |
|---|---|
| A timing assertion | Spec section 9: a recorded measurement |
| Rendering in a browser, phone width | No browser in the suite; AC-011's walk |
| Live Google, live Control Plane | TC-4 |
| The Control Plane error page's switcher | Out of scope (FR-010 is the installation's); `US-071/API_DESIGN/m-1` stays OPEN |
| `MeetCodesController.ListUrl`, `PageHeaderRules` by name | Behaviour is proved over HTTP; names are the implementor's to complete |

## 7. Known limitations

- `FakeTeacherListSource` ignores the text, so that the query passes it on to teachers is proved over HTTP, not in the unit tests.
- A proved source reading of the script remains text: its behaviour in a browser stays with the person (US-070 AC-009).

## 8. Open Decisions

None affect testing.
