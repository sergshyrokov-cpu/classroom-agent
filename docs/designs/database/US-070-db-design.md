---
artifact_type: database_design
story: US-070
version: 1
status: DRAFT
created_at: 2026-11-02T20:27:28Z
updated_at: 2026-11-02T20:27:28Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-070-spec.md
    version: 4
  - path: docs/designs/api/US-070-api-design.md
    version: 1
  - path: docs/designs/api/US-070-openapi.yaml
    version: 1
  - path: docs/decisions/US-070-open-decisions.md
    version: 2
supersedes: null
---

# US-070 Database Design — read shapes of the shared search

**Verdict: PASS.** No table, column, constraint, index or migration changes. The reads change, and how they match text is where BR-087 can go wrong, so they are
designed here (spec section 13). Port and row signatures: `docs/designs/database/US-070-entity-model.md`.

Delegation: none. Read directly: `MeetCodesReadSource`, `CourseListSource`, `TeacherListSource`, `IMeetCodesReadSource`, `ITeacherListSource`, the row records, the `meet_session`
configuration, `persistence-conventions.md` PC-7. Translation of the expressions below by the provider is not proven by this stage: the tests prove it against real PostgreSQL (TC-2).

## 1. Schema

Nothing changes, so no migration (PC-2). Section 9 of the Specification accepts a scan at query time: about 300 teachers, a few hundred courses, and the Meet codes of one school. A contains-match cannot use a
B-tree, and a trigram index would be the database extension the Story excludes. PC-7 is not engaged: the search is a filter, not a lookup key. `ix_meet_session_meeting_code_started_at` already serves the code lookups in §4.

## 2. One fold, one match

All server-side matching goes through one Infrastructure helper, `SearchMatch` (entity model §3). Nothing else builds a search condition.

**Fold of a field or word** (FR-001 rules 4, 5):

```sql
replace(replace(lower(<value> COLLATE course_name_uk), '’', ''''), 'ʼ', '''')
```

- `lower` under the explicit collation, on **both** sides, in SQL. A word must not be lowered in .NET: its folding could differ from the database's, and this is the reason the Story's Notes give for avoiding the database default.
- Exactly three apostrophes meet (U+2019 and U+02BC become U+0027). Others stay literal (I-2).
- `course_name_uk` is deterministic, so `strpos` and `replace` are allowed on it. In LINQ: `EF.Functions.Collate(x, Collation).ToLower().Replace("’", "'").Replace("ʼ", "'")`.

**Found in a field:** `strpos(fold(field), fold(word)) > 0`, which `string.Contains` with a non-constant argument gives today. It is literal: `%`, `_` and `\` mean nothing (rule 7, course list S-09). No pattern is ever built.

**Meet code field** (rule 6): `replace(fold(code), '-', '')` against the word with its hyphens removed. A word that is empty after removal **adds no term for this field**; `strpos(x, '')` is 1 and would match every code. The check is in C#, so a hyphen-only word still reaches the record's other fields.

**A record matches** when, for every word, at least one of its fields has the word (rules 1, 2): one `Where` per word (AND), each an OR over the fields. A null field gives no term's truth: `strpos(NULL, …)` is NULL, and the helper adds an explicit not-null test so the OR is plain.

**Words** come from `text.Split((char[]?)null, RemoveEmptyEntries)`, on any white space (rule 1). VR-001 already bounds the text at 100 characters, so at most 50 words.

**The word is a bound parameter** (spec section 7), including where the expression is built by hand: it is read from a captured holder object, which EF parameterizes. `Expression.Constant(word)` would put it in the SQL text as a literal, and is not allowed. A test asserts that the word is not in the command text.

## 3. Course and teacher lists

| Read | Searchable fields | Change |
|---|---|---|
| `CourseListSource.Matching` | `name`, `section` | replaces the single-text condition (`CountCoursesAsync`, `GetCoursePageAsync` share it) |
| `TeacherListSource.Listed` | `surname`, `given_name`, `email` | gains the search; `CountTeachersAsync` and `GetTeacherPageAsync` share it, so the total counts matches only |

Order, paging, the status and teacher filters, and one command per call are unchanged. `matching` for the course count stays as it is: a searched teacher's count is still their courses in the status.
An erased participant holds nulls, so nothing is found in them.

## 4. Meet codes

**For each word, the codes that hold it** (two sets, both untracked subqueries):

- by organizer: `meet_session` rows with `organizer_email IS NOT NULL` and the word in it, projected to `meeting_code`. This is the same column the screen reads, where an erased or excluded organizer is stored as NULL (US-038 I-9), so that email cannot match (AC-005). Rows of every session of a code count, as the screen lists all of them;
- by linked course (Linked only): `course` rows with the word in `name` or `section`, projected to `id`, tested against `meeting_code_link.course_id`. Candidates are never joined (FR-004), and `linked_by`, `confirmed_by`, `marked_by` are never searched.

| List | A code matches a word when |
|---|---|
| Unassigned | code field, or by organizer |
| Linked | code field, or by organizer, or by linked course |
| Not a course | code field, or by organizer |

**Reads.**

1. `GetCountsAsync(search)`: three counts, as today, each filtered as above. Three commands, whatever the number of words.
2. `GetLinkedPageAsync`, `GetMarkedPageAsync`: the existing query with the same filter, then **one** more command for the page's organizers (§5).
3. `GetUnassignedPageAsync`: stays raw SQL, because its order needs the candidate test (US-032 db-design §5.2). The search does not enter the SQL text. With a search, the LINQ filter of the Unassigned row first gives the list of matching codes (one command), passed as one array parameter:

   ```sql
   WHERE NOT EXISTS (SELECT 1 FROM meeting_code_link l WHERE l.meeting_code = s.meeting_code)
     AND (NOT {hasSearch} OR s.meeting_code = ANY({matchingCodes}))
   ```

   `{matchingCodes}` is a `string[]`, empty rather than null with no search, so its type stays `text[]`. This keeps one definition of a match (§2) with no second copy of the fold in SQL, and the raw path stays fully parameterized (spec section 7). If the list is empty, no page read is needed.

The number of commands depends on the list and on whether a search is present, never on the number of words (spec section 9).

**Page clamp (API D-4).** `GetMeetCodesQuery` takes the shown list's total from the counts and computes the effective page before asking for rows. The sources do not clamp. The total in `PagedRows` stays what it is today, under the same search.

## 5. Organizers on the Linked and Not-a-course rows (OD-001 a)

The block in `GetUnassignedPageAsync` that loads and orders the organizers becomes one private method, used by all three pages:

- the page's codes → `meet_session` (`meeting_code`, `organizer_email`), distinct;
- drop nulls, order by `StringComparer.Ordinal`, group by code. A code with no non-null email gets an empty list, which is an empty cell;
- it runs only when the page has rows.

A Linked or Not-a-course code with no session left has no organizers and an empty list. `LinkedCodeRow` and `MarkedCodeRow` gain `OrganizerEmails`; the orders of the pages do not change.

## 6. Sensitive data, transactions

Reads only, with no transaction and no write (AD-7 not engaged). The search text is in no log, exception message or audit row (SC-10, SC-14). `SearchMatch` logs nothing. The array of matching codes holds stored codes only, and a code is not personal data under US-032; it stays out of logs as well.

## 7. Traceability

| Requirement | Section |
|---|---|
| FR-001 rules 1 to 5, 7, 8 | §2 |
| FR-001 rule 6 | §2, §4 |
| FR-001 rule 9 (BR-085) | §3, §4: only stored current values |
| FR-002, FR-003 | §3 |
| FR-004, AC-004, AC-005, OD-001 | §4, §5 |
| Spec section 7 (injection) | §2, §4 |
| Spec section 9 (reads, size, collation) | §1, §2, §4 |
