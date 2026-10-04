---
artifact_type: test_strategy
story: US-025
version: 1
status: DRAFT
created_at: 2026-10-04T11:30:00Z
updated_at: 2026-10-04T11:30:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-025-journal-view-for-period.md
    version: null
  - path: docs/specifications/US-025-spec.md
    version: 1
  - path: docs/designs/api/US-025-api-design.md
    version: 1
  - path: docs/designs/api/US-025-openapi.yaml
    version: 1
  - path: docs/designs/database/US-025-db-design.md
    version: 1
  - path: docs/designs/database/US-025-entity-model.md
    version: 1
  - path: docs/decisions/US-025-open-decisions.md
    version: 3
supersedes: null
---

# US-025 Test Strategy — Journal view for a period

## 1. Scope

The journal page `GET /workspace/journal`, the `GetJournalQuery` use case, the
`IJournalSource` read port and its EF Core implementation, the required school
time zone setting, the home page's journal link, the translations and the log
lines of spec FR-016. The scenario list is `docs/tests/US-025-ac-test-matrix.md`.

Out of scope (spec §10): export, templates, the printed journal, other screens'
time zone.

## 2. Levels

| Level | What it proves | Classes |
|---|---|---|
| Unit (TC-1) — `GetJournalQuery` over `FakeJournalSource` | the cell function (FR-006, OD-008), the row rule (FR-005, BR-051, I-1), column selection and order (FR-004), validation and defaults (§6, FR-007), period conversion (FR-003), bounded reads (FR-014) | `JournalCellTests`, `JournalRowsTests`, `JournalColumnsTests`, `JournalRequestValidationTests`, `JournalPeriodTests` |
| Integration, PostgreSQL (TC-2) | Q1–Q4 of db-design §2: scope, half-open period, duplicates, `Kind`, one command per method, no tracking, no schema change | `JournalSourceTests` |
| Integration, host | read-only mode in all three BR-025 causes (Application and HTTP), the time zone setting at start-up, the page over HTTP, authorization, translations, logging | `JournalReadOnlyTests`, `TimeZoneConfigurationTests`, `JournalPageTests`, `JournalAuthorizationTests`, `JournalTranslationTests`, `JournalLoggingTests` |

The unit layer carries the business rules because they are pure functions of
records; the fake applies the same filters as the real port (course, half-open
period, student role), and `JournalSourceTests` proves the real port matches it.
Page tests assert observable output only — status, translated texts, form input
values, presence or absence of strings — never element ids or CSS classes,
which no artifact defines.

## 3. Scenarios by kind

- **Positive:** every cell state, row condition and column kind; the default
  period; both views; both languages; both roles; all four legitimacy states.
- **Negative:** every VR-001 … VR-004 malformation; unknown course; anonymous
  and restricted-session access; teacher and out-of-period data never shown.
- **Boundary:** `DueAt == B`; item at `start` and at `end`; `FirstSeenAt == end`;
  `LastSeenAt == start`; years 2000 and 2100; 29 February; a one-day period; a
  25-hour DST day; B at 00:30 local on the first of a month.
- **Validation:** all failures collected in parameter order; malformed values not
  echoed; valid values kept; validation before any journal read.
- **Security:** S-01/S-02 (AUTH), S-04 (RO: no write, no Google call, in every
  cause), S-05 (query carries ids, dates, view only — the page tests use only
  those), S-06/S-07 (LOG), S-08 (HTML encoding), S-11 (no audit row — covered by
  the fingerprint plus "no write").
- **Persistence:** `JournalSourceTests` (db-design §7 items 1–8).

## 4. Fixtures

- `JournalTestData` — path, parameters, `Installation:TimeZone`, `Europe/Kyiv`,
  the September period and its UTC bounds, the translation-key families of the
  openapi enums.
- `FakeJournalSource` — in-memory port with call counters and recorded bounds.
- `JournalHostExtensions` + `SeededJournal` — actors (the US-019 pattern), a
  seeded September journal, the real `JournalSource` over the host database, a
  `count + sum(xmin)` fingerprint of the five teaching tables, the pending-model
  check.
- `InstallationTestHost` gains `Installation:TimeZone = Europe/Kyiv` in its
  default settings (spec FR-010 makes it required; `TimeZoneConfigurationTests`
  removes or corrupts it).
- Production skeleton (OD-009 a): the port, its records, the request, result and
  DTO types, `GetJournalQuery` and `JournalSource` throwing
  `NotImplementedException`. Nothing registered in DI; no behaviour changed.

## 5. Excluded scenarios

- **Exact markup** of the table, switch and messages — not defined by any
  artifact; asserted by text and links instead.
- **"B is read once per request"** — not observable separately from "every cell
  uses the same B"; the cell tests fix B through `ManualTimeProvider`.
- **Late and draft mark wording, page title, navigation label** — no translation
  key name is fixed by the design; covered by the existing parity test (a key in
  one file only fails it) and by the page tests on the fixed families.
- **Concurrent synchronization between Q2 and Q4** (db-design §2.1) — the
  tolerance is exercised by unit tests (a submission to a non-column, a submitter
  without membership) rather than by a real race.

## 6. Known limitations

- Ukrainian collation depends on ICU; .NET on Windows and Linux both use ICU by
  default, and the tests run there. Invariant globalization mode would break the
  ordering tests, which is the intended signal.
- Two refusal tests (anonymous, restricted session) and the valid time zone and
  pending-model tests are green before implementation by design; the report
  explains each.

## 7. Open Decisions affecting testing

- OD-008 (a) — resolved; the duplicate-submission tests encode it.
- OD-009 (a) — resolved; the skeleton above.
No open decision remains.
