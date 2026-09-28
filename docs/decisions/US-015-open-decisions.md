---
artifact_type: open_decisions
story: US-015
version: 1
status: DRAFT
created_at: 2026-09-28T11:28:43Z
updated_at: 2026-09-28T12:30:56Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-015-sync-coursework-and-submissions.md
    version: null
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-015 Open Decisions — Sync coursework and submissions

Nine decisions (OD-001 … OD-009) were written into the Story by the author and
**resolved by the Owner on 2026-09-28, before activation**. They are carried here
with their ids and resolutions unchanged. Eight were resolved as option 1;
**OD-005 was resolved as option 4**, deliberately not the answer US-014 gave to
its equivalent question.

Two further gaps were found while writing the Specification — **OD-010** and
**OD-011** — and both were **resolved by the Owner on 2026-09-28 at the
`HUMAN_SPEC_APPROVAL` gate**, before the Specification was approved. Each affects
what the program stores or what a check constraint allows, so neither could reach
`DB_DESIGN` open. The Specification was corrected at version 1, while it was
still `DRAFT` and the gate had not passed — the US-014 precedent.

---

## Resolved before activation

### OD-001 The "last activity older than N" rule

**Question.** `trebovaniya.md` §5 (v36, v55) requires that synchronization not
import a course **not yet in the database** whose last activity in Google is
already older than the retention period N, while a course already in the
database is always updated until the purge deletes it. §5 v55 fixes that for a
not-yet-imported course the check uses Google's data only — the course, its
coursework and materials, its submissions — because such a course has no linked
meetings yet. US-014 OD-001 deferred the rule with the words "US-015 at the
earliest".

**Resolution (Owner, 2026-09-28): option 1 — the rule is applied here.** For a
course not yet in the database the run reads its coursework, materials and
submissions first, computes the last activity as the latest of those dates and
the course's own update time, and imports **nothing** for that course if the
latest is more than N years old. A course already in the database is always
updated.

**Consequences recorded with the decision:**

- this Story is the **first to read the retention setting N**; by DC-3 and PC-11
  an installation without it refuses to start, so a synchronization Story carries
  a deployment-affecting change;
- the import order inverts for an unknown course — read its work, then decide —
  which is why the rule lands here rather than in US-037, which would otherwise
  have to rewrite the pipeline this Story builds;
- the known limitation of §5 v55 stands: a course silent in Classroom for more
  than N years but still holding lessons in Meet is not imported into a new
  installation, and its meetings stay unlinked.

### OD-002 How submissions are requested

**Question.** `studentSubmissions.list` accepts a concrete `courseWorkId` or the
literal `-` for all work in a course. The prototype calls it per assignment
(`google_api.py:210`), which for 140 courses × 50 assignments is ~7 000 calls per
run.

**Resolution (Owner, 2026-09-28): option 1.** Submissions are requested **once
per course** with `courseWorkId = "-"`, paged to the end, each submission
attributed by its own `courseWorkId`. Recorded with the decision: the call shape
is **unverified against a live domain**, and a failure of that one call leaves a
whole course's submissions unread — the boundary OD-003 governs.

### OD-003 The transaction boundary now that submissions exist

**Question.** US-014 OD-009 fixed one transaction per course. This Story adds up
to (assignments × students) rows per course, and PC-1 prefers short transactions.

**Resolution (Owner, 2026-09-28): option 1.** **One transaction per course**,
through `IUnitOfWork.ExecuteInTransactionAsync`, now covering the course, its
participants, its memberships, its coursework and materials and its submissions
together. A run that fails part-way leaves only complete courses behind and the
next run finishes the job (BR-041). Accepted cost: a large course holds one long
transaction. If that ever becomes a real problem the answer is a documented batch
size, never a silent split that weakens the guarantee.

### OD-004 Coursework whose Classroom state is `DRAFT` or `DELETED`

**Question.** Classroom returns `PUBLISHED`, `DRAFT` or `DELETED` on a
`courseWork`. §3 does not list the field and no business rule mentions it.

**Resolution (Owner, 2026-09-28): option 1.** Only `PUBLISHED` coursework and
materials are imported, and the Classroom state is **not stored**. A draft is a
teacher's editing artefact, not part of the teaching process; when published, the
next run imports it. A piece of work deleted in Google keeps the row it already
has — exactly as US-014 leaves a course Google no longer returns — and only the
purge removes it. The filter is applied on **import**, so no later Story has to
remember to filter drafts out.

### OD-005 An unrecognised submission state

**Question.** BR-056 names the states a journal cell is computed from. The list
is not identical to the enumeration the Classroom API documents, and nothing in
this repository verifies it: the prototype reads only `userId` and
`assignedGrade` from a submission (`google_api.py:224-225`) and never touched a
state. US-014 OD-010 had already decided the shape of the answer for an
unfamiliar **course** state — skip that course, log a Warning, keep the run
successful.

**Resolution (Owner, 2026-09-28): option 4 — deliberately NOT the US-014
answer.** The failure modes are not comparable: a skipped course is visibly
absent from the Dean's list, while a skipped submission is not absent at all,
because BR-056 reads a missing submission as **«не сдано»** — a false statement
about one child's work.

The submission row is therefore **always stored**. Its state is a closed
vocabulary with a check constraint for every recognised value, **plus an explicit
"unrecognised" marker carrying the raw string Google sent**, and one Warning line
holding that string and the submission id and nothing else (SC-10).

**Three obligations follow:**

1. the Specification must list the **exact** vocabulary values — see **OD-011**,
   which is where doing so turned out to be impossible without a decision;
2. the Specification must record that BR-056's list — including
   `STUDENT_EDITED_AFTER_TURN_IN` — is **unverified** against a live Classroom
   response;
3. **US-025 inherits a rule BR-056 does not describe**: an unrecognised state is
   rendered as its own thing, never as «не сдано» and never as a grade.

### OD-006 A submitter who is on no roster

**Question.** BR-051 (v56) requires a student membership, off the roster, for a
person known only through their submissions. A submission gives only the Google
`userId` — no email, no name.

**Resolution (Owner, 2026-09-28): option 1.** The participant is created from the
`userId` alone, name and email absent — the schema US-014 built already allows
both. No profile call is made, so no unverified Workspace role is relied on
(§7 item 10). Accepted cost: such a person appears without a name until a roster
sighting fills it in, and if they never return to a roster, never. The membership
is `student`, off the roster, first and last seen on this run's date, and §3 v56's
limitation stands — N is counted from the day of this synchronization, not from
the day the person left.

### OD-007 Does a `Submission` store Google's update time?

**Question.** PC-13 lists what a `Submission` stores and **omits** Google's
`updateTime`; PC-11, derived from the same §5, requires "update of any of its
`Submission` rows" for a course's last activity.

**Resolution (Owner, 2026-09-28): option 1.** A `Submission` stores Google's
`updateTime`, because it is the only value that means what §5 says — when the
submission changed **in Google**. PC-6's local `updated_at` records when this
program wrote the row; using it would tie a school's retention to the
installation's sync history, so reimporting a school would refresh every row and
postpone every expiry — a purge wrong in a way nobody could see.

**`persistence-conventions.md` PC-13 is the document that must be corrected.**
§5 v36 already says «изменение любой сдачи», so the requirement is unchanged and
only the derived convention is wrong (AGENTS.md).

### OD-008 One entity for coursework and materials, or two

**Question.** §3 models one concept with a type field; PC-3 notes two Classroom
resources whose ids may collide and fixes the key as (resource, Google id).

**Resolution (Owner, 2026-09-28): option 1.** One `CourseWork` entity and one
table, with a stored field naming the Classroom resource and PC-3's unique key on
(resource, Google id). The BR-052 kind — graded work, ungraded work, material —
is **derived and never stored**: it follows from the resource and from whether
maximum points are set, so a teacher adding or removing points updates the same
row. `package-map.md` already describes exactly this, so no convention changes.

### OD-009 Reading the last turn-in date

**Question.** §3 defines the submission date as the last transition to
`TURNED_IN` in the submission history; PC-13 confirms the history is read during
synchronization but never stored (Epic 12, BR-059).

**Resolution (Owner, 2026-09-28): option 1.** The history is taken from the
submission resource itself, the **latest** transition to `TURNED_IN` is kept
(BR-058), and everything else is discarded in memory. A submission whose history
Google omits or truncates has **no date**, stored as absent and never invented.
Recorded with the decision: the inline availability of the history is unverified
against a live domain; if a live run shows a separate request is needed, that is
a fact for US-017/US-018 to absorb, not a licence to substitute `updateTime`,
which changes when a teacher enters a grade.

---

## Raised while writing the Specification — resolved at the gate

### OD-010 Does Classroom return the submissions of a student removed from a roster?

**This is `trebovaniya.md` §7 item 14, still open.** Per AGENTS.md a Story that
depends on an item open in §7 has an Open Decision, and it is raised, not
resolved here.

**Why it matters.** The Story's AC-004 and BR-051 (v56) require that a person
with submissions whom synchronization never saw on a roster receives a student
membership marked off the roster. That rule can only ever fire if Classroom
actually returns such a person's submissions after they are removed from the
roster. §7 item 14 states both branches and says the question must be checked on
a live domain:

- **if Classroom does not return them** — already-stored submissions are left
  alone (§5) and no new ones arrive, so the AC-004 path is dead code on a live
  domain and the off-roster membership it creates never appears;
- **if Classroom does return them** — the rule applies exactly as written.

**Impact on this Specification.** The requirement is stated for both branches,
because §7 itself does: the program creates the membership **when** such a
submission arrives, and does nothing when none does. No behaviour is invented
either way. What cannot be settled here is whether TEST_WRITING is testing a path
that exists in production, so the test for it is written against synthetic
fixtures and proves the program's rule, not Google's behaviour (TC-4).

**Options for the Owner:**

1. Proceed as specified — implement the rule for both branches and leave §7
   item 14 to be verified at onboarding, exactly as US-014 OD-003 left §7 item 10
   (recommended by precedent, not by this Skill's authority to decide).
2. Verify on the live domain first and resolve §7 item 14 by a new version of
   `trebovaniya.md`, then activate the affected requirement.
3. Defer the rule to a later Story. Cost: BR-051 v56 stays unimplemented and a
   leaver's expiry has no date to count from, which PC-11 needs.

**Resolution (Owner, 2026-09-28, at `HUMAN_SPEC_APPROVAL`): option 1.** The rule
is implemented for both branches and §7 item 14 stays a
verify-at-onboarding item, as §7 itself intends and as US-014 OD-003 already did
with §7 item 10. **§7 item 14 is not closed by this decision** — only the Story's
dependence on it is.

The decisive point, recorded so it is not re-litigated: **the implementation is
identical either way.** The off-roster membership is created when such a
submission arrives and nothing happens when none does, so no line of code depends
on the answer. What the answer changes is only what we may claim about coverage —
therefore the Specification and `TEST_WRITING` must state plainly that the test
for AC-004 uses synthetic fixtures and proves the **program's** rule, never
Google's behaviour (TC-4), and that an installation which never produces such a
membership on a live domain is evidence about Classroom, not a defect.

### OD-011 The exact vocabulary of submission states

**Question.** OD-005 requires the Specification to list the exact recognised
values. Writing that list showed it cannot be done from the documents without a
decision.

BR-056 (from §4 Epic 3, v32) names five values and how a journal reads them:
`TURNED_IN` and `STUDENT_EDITED_AFTER_TURN_IN` count as turned in, `CREATED` and
`RECLAIMED_BY_STUDENT` count as not turned in, and `RETURNED` is its own case.
Two problems:

- the enumeration the Classroom API documents for a student submission also has
  **`NEW`** — the state of work a student has never opened — and BR-056 does not
  mention it. Under OD-005 an unlisted value is stored with the "unrecognised"
  marker, so on a real school the **most common** state of untouched work would
  be marked unrecognised and would reach the journal as "its own thing" rather
  than as "не сдано". That is not what §4 Epic 3 describes;
- conversely, `STUDENT_EDITED_AFTER_TURN_IN` may not exist in the API at all.
  Nothing in this repository can tell: the prototype never read a state.

Adding `NEW` to the vocabulary **and deciding that it means "not turned in"** is
a business rule about journal cells, which is BR-056's territory — this Skill may
not write it.

**Impact.** The vocabulary is a closed list with a check constraint (OD-005), so
it is fixed in the database by `DB_DESIGN` and changing it later costs a
migration. The decision must therefore be made before `DB_DESIGN`.

**Options for the Owner:**

1. Vocabulary = exactly BR-056's five values; anything else, including `NEW`, is
   stored with the unrecognised marker. Faithful to the approved rule, and
   noisy in a way that will be visible on the first live run.
2. Vocabulary = BR-056's five values **plus `NEW`**, with `NEW` meaning "not
   turned in" for the journal, recorded as a correction to §4 Epic 3 / BR-056 in
   a new version of `trebovaniya.md`. Matches what Classroom actually returns;
   requires a requirements change, which only the Owner makes.
3. Store the state as Google's raw string with no vocabulary and no check
   constraint, and let US-025 interpret. Rejected by OD-005's own reasoning, but
   recorded for completeness.

**Either way the Specification must record that BR-056's list is unverified
against a live Classroom response**, because this is the first Story that reads a
submission state at all.

**Resolution (Owner, 2026-09-28, at `HUMAN_SPEC_APPROVAL`): the vocabulary is
six values, and the cell rule for `NEW` belongs to US-025.** The recognised
values are the five the Classroom API documents — `NEW`, `CREATED`, `TURNED_IN`,
`RETURNED`, `RECLAIMED_BY_STUDENT` — plus `STUDENT_EDITED_AFTER_TURN_IN`, which
BR-056 names. The "unrecognised" marker of OD-005 therefore catches only a value
Google genuinely adds later, not the ordinary state of untouched work.

Why the split: **what to store is this Story's question; what a cell shows is
BR-056's.** Storing `NEW` faithfully needs no requirements change, while deciding
that a `NEW` cell reads «не сдано» is a change to §4 Epic 3 and only the Owner
makes it — and it is better made when the whole journal is designed.
`STUDENT_EDITED_AFTER_TURN_IN` stays in the vocabulary even though it may not
exist in the API: an unused permitted value costs nothing, while omitting it
would flag a real value as unrecognised.

**Carried obligations, recorded here so they are not lost:**

- **US-025 owns the cell rule for `NEW`.** BR-056 does not describe it, and a
  journal that renders `NEW` as «не сдано» without that rule being written is
  making the decision silently. This joins the OD-005 obligation — an
  unrecognised state is rendered as its own thing, never as «не сдано» and never
  as a grade.
- **BR-056's list is unverified** against a live Classroom response, in both
  directions: `NEW` is absent from it, and `STUDENT_EDITED_AFTER_TURN_IN` may be
  absent from the API. The first live run is where that is learned, and a
  correction belongs in a new version of `trebovaniya.md`, never in a Story.
