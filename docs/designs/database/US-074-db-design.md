---
artifact_type: database_design
story: US-074
version: 1
status: DRAFT
created_at: 2026-11-06T19:59:31Z
updated_at: 2026-11-06T19:59:31Z
produced_by: db-designer
inputs:
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/designs/api/US-074-api-design.md
    version: 1
  - path: docs/designs/api/US-074-openapi.yaml
    version: 1
  - path: docs/decisions/US-074-open-decisions.md
    version: 1
  - path: docs/designs/database/US-070-db-design.md
    version: 1
supersedes: null
---

# US-074 Database Design — organizer names on the Meet code lists and in their search

Verdict **PASS**. **No schema change**: no table, column, constraint, index or migration (spec section 1: no new
stored data). This design fixes how the existing tables are read, so that display and search share one name rule
(spec FR-001, FR-005, AC-008, AC-009). Companion: `docs/designs/database/US-074-entity-model.md`.

Delegation: none. The designer read `MeetCodesReadSource.cs`, `SearchMatch.cs`, `MeetReportSource.GetPeopleAsync`,
`MeetReportRules.Name`/`Person`, `GetMeetCodesQuery.cs`, `MeetSessionConfiguration.cs` and
`ClassroomParticipantConfiguration.cs` at commit `8278c3b` directly.

## 1. Tables read (unchanged)

| Table | Columns used | Existing index used |
|---|---|---|
| `meet_session` | `meeting_code`, `organizer_email` (nullable; null after erasure/exclusion, US-038) | `ix_meet_session_meeting_code_started_at`; the `lower(organizer_email)` expression index (AddParticipantErasure migration) |
| `classroom_participant` | `id`, `email` (nullable, stored trimmed and lower-cased), `full_name` (nullable) | `ix_classroom_participant_email_lower` (expression index on `lower(email)`, AddMeetingCodeLinks migration) |

## 2. The name rule, stated in its exact order (closes US-074/SPEC_REVIEW/m-1)

For one organizer email `e`, the **chosen name** is computed in this order, the same order as
`MeetReportSource.GetPeopleAsync` (lines 146–152) followed by `MeetReportRules.Name`:

1. **Candidates.** `classroom_participant` rows with `email IS NOT NULL`, `lower(email) = lower(e)` and
   `full_name IS NOT NULL`. A blank `full_name` (empty or white space only) **is** a candidate at this step.
2. **Choice.** The candidate with the lowest `id`. Its `full_name` is the stored name. No candidate → no stored name.
3. **Blank test, after the choice.** If the chosen `full_name` is empty or white space only, the organizer has **no
   name**. A later participant of the same email with a real name is **not** used instead.

So: lowest id `full_name = '  '`, next id `full_name = 'Петренко Іван'` → no name; the row shows the email alone,
exactly as the Meet reports show that email. Step 3 runs in Application (`MeetReportRules.Name`) for display. In
search it needs no code of its own: a search word never contains white space (`SearchMatch.Words`), so a blank name
holds no word.

## 3. One query definition: `OrganizerNames`

A new private member of `MeetCodesReadSource` (spec FR-001: one definition for display and search):

```
IQueryable<OrganizerNameRow> OrganizerNames() =
  classroom_participant p
  WHERE p.email IS NOT NULL AND p.full_name IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM classroom_participant q
                    WHERE q.email IS NOT NULL AND q.full_name IS NOT NULL
                      AND lower(q.email) = lower(p.email) AND q.id < p.id)
  SELECT lower(p.email) AS lower_email, p.full_name
```

In LINQ: `db.ClassroomParticipants.AsNoTracking().Where(p => p.Email != null && p.FullName != null &&
!db.ClassroomParticipants.Any(q => q.Email != null && q.FullName != null && q.Email.ToLower() == p.Email.ToLower()
&& q.Id < p.Id)).Select(p => new OrganizerNameRow(p.Email!.ToLower(), p.FullName!))`. At most one row per
`lower_email` (steps 1–2 of section 2). `OrganizerNameRow` is a private record of the read source; it never leaves
Infrastructure.

The Meet reports keep their own in-memory grouping (`MeetReportSource`, unchanged: no opportunistic refactoring).
The two implementations are held equal by the AC-009 test (spec FR-001 allows "or proves by test").

## 4. Display: `LoadOrganizersAsync` (one or two commands per page, never per row)

Changed to return, per code, a list of `MeetOrganizerRecord(Email, StoredFullName)` (entity model):

1. **Command 1 (as today):** distinct `(meeting_code, organizer_email)` of the page's codes with
   `organizer_email IS NOT NULL`.
2. **In memory:** per code, group the emails by `ToLowerInvariant()`; keep one email per group, the ordinally
   smallest stored spelling (spec I-1); order the groups by that email, ordinal.
3. **Command 2:** `OrganizerNames()` filtered by `lower_email = ANY(<the page's distinct lower-cased emails>)` (one
   array parameter; served by `ix_classroom_participant_email_lower` through the correlated `NOT EXISTS`). Skipped
   when the page has no organizer.
4. Join in memory by lower-cased email: `StoredFullName` is the row's `full_name`, or null when absent.

At most two commands per list page regardless of the number of rows (spec FR-005, AC-008). The Unassigned page's
connection count (`organizerSet`, case-insensitive) keeps using the emails, now taken from the records.

**Case of `lower()` vs `ToLowerInvariant()`.** The in-memory grouping and the SQL join agree for ASCII emails, which
is what Google Workspace addresses are; the existing code already relies on the same equivalence
(`MeetReportSource`, `organizerSet`). No new risk.

## 5. Search: `CodesByOrganizer(word)`

Today: sessions whose `organizer_email` holds the word. After: sessions whose `organizer_email` holds the word **or**
whose organizer's chosen name holds it:

```
db.MeetSessions.AsNoTracking()
  .Where(s => s.OrganizerEmail != null && (
       <SearchMatch.InField(s.OrganizerEmail, word)>
    || names.Any(n => n.LowerEmail == s.OrganizerEmail.ToLower())))
  .Select(s => s.MeetingCode)
where names = OrganizerNames().Where(SearchMatch.InField<OrganizerNameRow>(n => n.FullName, word))
```

- The match condition is `SearchMatch.InField` — the one definition of a match (BR-087, US-070 db-design §2); no new
  folding, the word stays a bound parameter (spec VR-003).
- `names` is a subquery, evaluated in the database together with the other fields (spec FR-005); participants are
  never loaded into memory for search.
- A null `organizer_email` (erased/excluded) matches neither branch (spec AC-004).
- Because `CodesByOrganizer` is the single organizer term in `UnassignedCodes`, `LinkedLinks` and `MarkedLinks`, the
  three lists, their counters, `GetFirstMatchesAsync` and therefore the header search (spec FR-004) all gain the
  name field with this one change. Per word the term stays "any organizer of the code", so two words may match two
  organizers of one code (spec I-2).

## 6. Index decision: none added

Reason: the name condition is a `strpos` over a lowered, collated, apostrophe-folded `full_name`, which no B-tree
index can serve; the only index that could (trigram, `pg_trgm`) needs a new database extension, which is not justified
at the expected volume. `classroom_participant` holds about 1300 people (1000 students, 300 teachers, `trebovaniya.md`
§5), so `names` is a sequential scan of ~1300 rows per word, each with an index probe for `NOT EXISTS` on
`ix_classroom_participant_email_lower`; the join to `meet_session` by `lower(organizer_email)` uses the existing
expression index. The existing email branch already scans `meet_session` the same way (US-070). Search by name stays
in the same cost class as search by email today.

## 7. Sensitive data

Names are personal data already shown to the same roles in the Meet reports (spec section 7). Nothing new is stored.
No name or search word is logged (SC-10); the queries use parameters only.

## 8. Schema initialization and migration

None. No `EnsureCreated`, no migration (PC-2 not triggered: no entity change).

## 9. Findings handled

- **US-074/SPEC_REVIEW/m-1 — FIXED** by section 2 (exact order: candidates with non-null name, lowest id, blank test
  after the choice) and the AC-002 case named there. TEST_WRITING proves it (entity model section 4).
