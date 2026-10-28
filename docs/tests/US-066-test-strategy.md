---
artifact_type: test_strategy
story: US-066
version: 1
status: DRAFT
created_at: 2026-10-28T18:42:40Z
updated_at: 2026-10-28T18:42:40Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-066-namesakes-shown-with-email.md
    version: null
  - path: docs/specifications/US-066-spec.md
    version: 1
  - path: docs/decisions/US-066-open-decisions.md
    version: 1
  - path: docs/designs/api/US-066-api-design.md
    version: 1
  - path: docs/designs/api/US-066-openapi.yaml
    version: 1
  - path: docs/designs/database/US-066-db-design.md
    version: 1
supersedes: null
---

# US-066 Test Strategy — Students with the same name are shown with their email

## 1. Scope

The student row's name on the journal page (US-025), the report page (US-027/US-042), the program-layout
export of the report and the «Бланк для групових занять» built from it (US-043): a namesake row shows
`<name> (<email>)`, every other row is unchanged (spec FR-001 … FR-005). No endpoint, policy, error,
translation key, schema or audit event changes (api-design §1, §4, §5; db-design NOT_APPLICABLE).

## 2. Test level

**Integration over HTTP against PostgreSQL** (TC-2), as US-060 tested the same four outputs: a Dean signed
in, the course seeded by SQL (no synchronization, no Google — TC-4; every test also asserts zero Google
calls), the page read as HTML or the file read back with ClosedXML. Every assertion reads what the user
sees — the whole text of a row heading or a name cell — so the tests survive any internal shape of the
`namesakeEmail` field (api-design D-1 … D-3).

No unit test of the builders: the decision "namesake or not" is observable only through the outputs, and
the HTTP tests cover each output; where it is made (`Application`, FR-003.4, AD-3) is an architecture rule
for IMPLEMENTATION_VERIFICATION, not a behaviour. Hence **no compile-only skeleton** — no test references
a declaration that does not exist.

## 3. Fixture

`TestInfrastructure/NamesakeWorld`, `NamesakeStudent`, `NamesakeHostExtensions.SeedNamesakeCourseAsync`:
one course, one teacher (or two teachers with the same name), one graded item (maximum 12), and the
students a test lists, in that order (so participant ids, the tie-breaker of FR-005, are known); every
student has the same grade 10. Invented students:

| Student | Journal label (full name) | Report profile label | Email |
|---|---|---|---|
| PetrenkoA | Іван Петренко | Петренко Іван | ivan.petrenko.a@school |
| PetrenkoB | Іван Петренко | Петренко Іван | ivan.petrenko.b@school |
| Koval | Олена Коваль | Коваль Олена | olena.koval@school |
| PetrenkoLower | іван петренко | петренко іван | ivan.petrenko.c@school |
| PetrenkoNoEmail | Іван Петренко | Петренко Іван | — (null) |
| PetrenkoOtherDomain | Іван Петрук | Петрук Іван | ivan.petrenko.a@other.example |
| MailUpper / MailLower | — (full email) | — (email part) | Same.Mail@school / same.mail@school |

A leaver variant (`Leaving()`) is off the roster since 2026-09-09 21:45 UTC — 10.09.2026 in Kyiv (TC-8),
reusing the US-060 "left" texts (`DataMarksTexts`).

## 4. Scenarios

**Positive.** Two namesakes show their own email, in participant order, the third name alone — journal
(AC-001), report (AC-002), program export (AC-003), blank (AC-004); in Ukrainian and English, the format
identical in both (FR-004.2, I-5).

**Negative.** No namesakes → no email on any output, no `@` in any cell of either file (AC-006);
names from the email with distinct parts before `@` → no email (AC-007); a namesake without an email →
name alone (AC-008); two teachers with the same name → no teacher email anywhere on the report page
(FR-001, OD-002 a); journal rows already labelled by their full email → no second email (FR-002.5, I-2).

**Boundary.** Names differing only in letter case coincide (AC-005, FR-002.3, both languages); the same
part before `@` at two domains coincides in the email variant only (I-3); namesakes split across two blank
copies (two students per copy) both show their email (FR-002.1, I-4); switching the name variant back and
forth on the same course (AC-007).

**Coexistence with US-060.** A leaving namesake: on screen «name (email)» then «вибув(-ла) 10.09.2026»
(FR-004.3); in the program export and the blank the cell holds «name (email)» and the note keeps the
"left" text (FR-004.4, AC-009).

**Layout unchanged (AC-003, FR-004.4).** One course exported twice — names from the profile (namesakes)
and from the email (none): same sheets, used ranges, merged ranges, row heights, every non-name cell's
value and note, and the name cells' style and note. The test ends by asserting the profile file really
holds an email, so the comparison cannot pass vacuously.

**Validation.** No new input (spec §6). VR-001 is AC-008; VR-002 (Razor encoding) is the default
encoding and the emails here hold no markup characters — not tested separately.

**Security.** No new endpoint, policy or role (spec §7): the existing allowed/forbidden-role and
antiforgery suites of these endpoints stand unchanged. The teacher test proves the email reaches no
teacher line. Logging and audit content (SC-10, SC-11) are unchanged code paths — left to SECURITY_REVIEW.

**Persistence.** None (db-design NOT_APPLICABLE).

## 5. Excluded scenarios

- **Unnamed rows never coincide** (FR-002.2): an unnamed student has neither a name nor an email in the
  report (no profile, no email) or in the journal; it could carry no email anyway, so a test would pass
  whatever the rule — vacuous, not written.
- **Read-only mode** (FR-006): the Story adds no write; viewing and export in read-only mode are covered
  by US-060's `DataMarksReadOnlyTests` and earlier Stories.
- **Order of rows** (FR-005) is asserted inside the positive tests (exact sequences), not separately.
- **Column widths** of the program export: the renderer fits them to the content today
  (`ClosedXmlReportRenderer` `AdjustToContents`), so a longer name widens column A as any long name does;
  FR-004.4's "no column changes" is read as the set and position of columns, which the layout test pins.

## 6. Known limitations

- Ordering of Cyrillic names in the expected sequences relies on ICU culture ordering, as the existing
  report tests do (`І` before `О`, `Коваль` before `Петренко`, `Петренко` before `Петрук`).
- The case-only pair's relative order is not asserted (case-sensitive ordering differs by culture).

## 7. Open Decisions affecting testing

None. OD-001 a) and OD-002 a) are resolved; no item of `trebovaniya.md` §7 applies.
