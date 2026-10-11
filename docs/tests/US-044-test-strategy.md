---
artifact_type: test_strategy
story: US-044
version: 1
status: DRAFT
created_at: 2026-10-11T07:40:00Z
updated_at: 2026-10-11T07:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-044-meet-reports-export.md
    version: null
  - path: docs/specifications/US-044-spec.md
    version: 1
  - path: docs/decisions/US-044-open-decisions.md
    version: 2
  - path: docs/designs/api/US-044-api-design.md
    version: 1
  - path: docs/designs/api/US-044-openapi.yaml
    version: 1
  - path: docs/designs/database/US-044-db-design.md
    version: 1
  - path: docs/designs/database/US-044-entity-model.md
    version: 1
supersedes: null
---

# US-044 Test Strategy — Export the built-in Meet reports to Excel

## 1. Scope

Two new `POST` endpoints (`/api/v1/exports/meet-reports-xlsx`,
`/api/v1/exports/meet-meeting-xlsx`), their Application commands, the opt-in
workbook-model and renderer additions, the file-name rule, two audit actions
with their migration, and the export action on the two US-033 pages. The
on-screen reports of US-033 and the journal export of US-028 are regression
scope only.

## 2. Levels

| Level | What | Where |
|---|---|---|
| Unit (TC-1) | commands over `MeetExportWorld` (fake report source, fake texts, fake renderer, recording unit of work): validation before any read, not-found, audit row, commit under `PermittedServiceWrite.AuditEvent`, order render → audit → commit, orientation, workbook-model flags; file name; audit factories | `Application/UseCases/` |
| Infrastructure | `ClosedXmlReportRenderer` with the new cell kinds, number format and autofilter; the `audit_event` CHECKs after `AddMeetExportAudit` on PostgreSQL | `Infrastructure/Export/`, `Infrastructure/Persistence/` |
| Host (TC-2, TC-3) | both exports over HTTP against PostgreSQL (Testcontainers): the workbook opened from the response bytes in memory; sheets, cells, cell types, autofilter, frozen rows, page setup, file name, headers, audit rows, refusals with the API-6 body | `Web/Pages/` |
| Security (TC-5) | protected and POST-only endpoints, Admin and Dean allowed, anonymous `401`, restricted Dean `403`, no token `400`, `405`, `415`, malformed body | `Web/Security/` |
| Logging, localization | success and refusal log lines without personal data (SC-10); every new text in uk and en | `Web/Logging/`, `Web/Localization/` |

The existing host-wide tests cover the new endpoints without change once they are
mapped: the anonymous-access enumeration (SC-4), the antiforgery enumeration
(TC-5) and the `Cache-Control: no-store` enumeration (SC-14), and the pending-model
test (the migration matches the configuration).

## 3. Fixtures

- `MeetExportWorld` (new, unit): a course with a teacher who organizes, two
  students, one October meeting (organizer, two students, one guest) and a
  meeting of another course; Kyiv; "now" 2026-10-15 12:00 UTC.
- `MeetReportsHostExtensions.SeedWorldAsync` (US-033, reused): the October world —
  meetings A1 (2026-10-05 10:00 Kyiv, 45 min, organizer teacher One) and B1
  (2026-10-20 09:00, 30 min, organizer teacher Two, not a course teacher), marked,
  unlinked, outside-the-period and other-course meetings that must never appear.
- `MeetExportHostExtensions` (new, host): bodies, posting with the token from the
  Meet reports page, cell readers (`Number`, `Serial`, `Text`, `AutoFilterRange`)
  that check the cell type without depending on how ClosedXML stores it, and the
  audit rows by raw SQL.
- Host clock: `InstallationTestHost.DefaultStart` (2026-09-17 08:00 UTC = 11:00
  Kyiv) — the "Exported" cell, and the effective default period September 2026.

**Expected values come from the page's own query.** "Rows equal the screen"
(AC-002, AC-003, spec FR-003) is checked by running `GetMeetReportsQuery` /
`GetMeetReportMeetingQuery` from the host's container with the same inputs and
comparing every row of the file to the DTO, with minutes as the page computes
them (`(seconds + 30) / 60`, US-033 I-5). A few hand-checked anchors (2 meetings,
3 students, report 4 of A1: 2 other participants) guard against both sides
being wrong together.

## 4. Scenarios

**Positive.** Dean and Admin export each workbook; three sheets in order;
header block on each sheet; Summary values; Meetings and Student participation
rows in the screen's order; report 4 rows, the "Other participants" line; file
name and `filename*`; one audit row each; English user.

**Negative.** Malformed course id, meeting id, dates, orientation; missing
required members; inverted period; unknown course; unknown meeting; meeting of
another course; anonymous; restricted Dean; no token; `GET`; form body;
malformed JSON body. Each: status, API-6 body, no file, no audit row, value not
echoed or logged.

**Boundary.** One-day period; years 2000 / 2100 rules via the US-033 parsers;
meeting id `0` and 64-bit overflow; `Europe/Kyiv` period bounds — a meeting at
2026-10-01 00:30 Kyiv is in, at 2026-11-01 00:10 out, one at 2026-10-31 23:30
ending 00:40 next day is in with an end date-time (US-033 I-7, AC-017); nobody
identified (header row only, I-6); course with no meetings and no students
(empty-state lines, no autofilter, counts 0).

**Validation.** All failing fields reported together; the reports' own messages
(`MeetReports.Validation.*`), `Export.Validation.OrientationInvalid`,
`Export.Validation.MeetingMalformed`, `Export.Validation.RequestMalformed`.

**Security.** TC-5 matrix per endpoint; read-only mode (three BR-025 causes)
exports and audits; zero calls to the substituted Google readers; no name, email
or meeting code in the audit row, the log, the file name or the request; formula-
like text stays text with the quote prefix; "other participants" never named.

**Persistence.** Audit rows of both actions with exactly the db-design §3.2
columns; the new CHECKs refuse each crafted bad row of db-design §7; journal rows
stay valid.

## 5. Excluded, with reason

- Phone-width usability (NFR-070) — a layout property; reviewed by hand, as in
  US-028 and US-033.
- Column widths and header wrapping — unchanged US-028 renderer behaviour,
  already covered by its tests.
- The page script's click behaviour — no browser test exists in this project;
  the page carries the values the script sends (data attributes) and the
  endpoint is tested directly.
- Live Google field names (§7 item 28) — synthetic data only (TC-4).

## 6. Choices this stage made

- **Program texts of the file are resolved through `ReportText`** (the US-028
  text port). The skeleton appends `Meet*` members for the texts US-033 has no
  key for (sheet names, header labels, Summary headings and labels, the new
  column headings, "Other participants"), so the tests do not fix key names; the
  texts US-033 already has (column headings of reports 2 and 3, marks, delay
  notice, empty-state lines) are read by their `MeetReports.*` keys, as spec
  FR-015 requires their reuse.
- **Result types** — the entity model names the outcome cases only; the skeleton
  declares `MeetExportResult(Outcome, File, Errors)` with `MeetExportOutcome`,
  `MeetExportField`, `MeetExportMessageKey` and `MeetExportFieldError`, the file
  as US-028's `JournalExportFile`.
- **Layout positions** — the header block is the rows of spec FR-004 in that
  order from row 1, label in column A, value from column B; the delay line is one
  text cell in column A; one empty row; the table header row follows (row 6 in
  the course workbook, row 9 in the meeting workbook). The Summary's
  Indicator/Value table starts there too.
- **Page contract** — the export action carries `data-course-id`, `data-from`,
  `data-to` (reports page) and `data-course-id`, `data-meeting-id` (meeting
  page), as the journal page's `data-course-id`; both script options of
  api-design §2.5 need the values on the page.

## 7. Known limitations

- Unit tests do not assert program texts (the fake text port marks them); host
  tests do, in both languages.
- A field error's field for an inverted period is not fixed by the contract; the
  tests accept `from` or `to`.
- For the meeting export, an unknown course answers `404`; the tests do not fix
  its message, only that it equals neither a `400` nor a file.

## 8. Open Decisions affecting testing

OD-014 (compile-only skeleton) — resolved (a), Owner, 2026-10-11. None open.
