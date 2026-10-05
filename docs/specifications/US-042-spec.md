---
artifact_type: specification
story: US-042
version: 1
status: APPROVED
created_at: 2026-10-05T11:11:02Z
updated_at: 2026-10-05T11:22:06Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-042-names-in-report.md
    version: null
  - path: trebovaniya.md
    version: 86
  - path: docs/specifications/US-027-spec.md
    version: 2
  - path: docs/specifications/US-025-spec.md
    version: 1
supersedes: null
---

# US-042 Specification — Names of students and teachers in a template report

## 1. Overview

Google does not know a person's ПІБ: the profile has no patronymic, its full name
is usually "Name Surname", may be in Latin letters and may change
(`trebovaniya.md` v86, §3 `ClassroomParticipant`, §3 `ReportTemplate`, §4 Epic 3
"Имена учеников и преподавателей в отчёте").

This Story:

- stores a participant's **surname** and **given name** separately, as the
  Google profile returns them, next to the full name already stored;
- adds a template setting **"names"** with two values — **from the Google
  profile** ("Surname Name", falling back to the part of the email before `@`)
  and **from the email** (the part before `@`);
- applies that setting to every person in the report of US-027 — the students of
  the "Grading" part and the teachers of the header and of "Lesson topics";
- orders the students by the name shown;
- adds a **switch** on the report page that rebuilds the report with the other
  source without changing the template; the chosen source is part of the page
  address, so the export of US-028 can use it.

The program neither checks nor corrects names (v83, v86). The US-025 journal and
the Meet pages keep showing the full name as today.

## 2. Business Goal

A printed academic journal lists students by surname. With the profile option
the report reads like a paper journal; with the email option a school whose
profiles are unreliable still gets a stable list. The switch lets a Dean see
before printing which names are wrong, so the domain administrator can correct
them in Google; the next synchronization brings the correction (v86).

## 3. Business Flow

1. Synchronization (US-014) reads the roster; for each roster entry it now
   also stores the surname and the given name of the profile.
2. A Dean or Admin creates or changes a template and picks the "names" setting
   (default for a new template: from the Google profile). The built-in "Academic
   journal" always uses the profile; a copy may switch.
3. On the report page the user chooses template, course and period (US-027
   FR-011). The report shows names by the template's setting.
4. The user flips the switch; the page reloads with the other source in its
   address; the template is not changed. Choosing another template through the
   report form returns to that template's own setting (I-4).
5. An export started from that page (US-028) takes the source from the address.

## 4. Functional Requirements

### FR-001 Surname and given name of a participant

- `ClassroomParticipant` gains two optional values: **surname** (Google
  `name.familyName`) and **given name** (Google `name.givenName`). Both are
  personal data, like the email and the full name.
- They travel exactly the path the full name travels today: the roster entry of
  the Google read port carries them (`Application/Ports`; no Google SDK type
  crosses into `Application`, AD-4), and the synchronization's participant upsert
  writes them on insert and on update.
- Each is trimmed; a blank value is stored as none; a value longer than the
  stored bound is cut as the full name is (bound fixed by DB_DESIGN, I-7).
- On every run the stored values are **replaced** by what the roster entry
  carries — a changed name replaces the old one; a profile without a surname or
  given name leaves none (AC-001). Normalisation is exactly as for the full name;
  the program does not correct case, alphabet or order (v86).
- A participant imported only as a submitter (off the roster, US-015 OD-006) has
  no profile today and therefore no surname or given name; nothing changes in
  that path.
- Existing participants have none until the next synchronization (Story Scope);
  the migration does not invent them.
- The full name stays stored and keeps being written; the US-025 journal and the
  Meet pages use it.

### FR-002 The template setting "names"

The settings of US-027 FR-003 gain one row (§3 v86):

| Setting | Values |
|---|---|
| Names | `profile` — from the Google profile; `email` — from the email |

- The template form shows it as a required choice of two, with translated
  labels.
- **New template** (US-027 FR-007): defaults to `profile` (I-2).
- **Copy** (US-027 FR-008): takes the source's value; a copy of the built-in
  template gets `profile`.
- **Change** (US-027 FR-009): saving replaces it with the other settings; the
  usual `ReportTemplateChanged` audit row (FR-007).
- **The built-in "Academic journal"** (US-027 FR-006): `profile`; it cannot be
  changed, as all its settings.
- **Templates created before this Story** use `profile` (AC-005): the migration
  sets that value on every existing row (FR-009).

### FR-003 The name shown for a person

One function in `Application` gives the displayed name of a person of the report
from the person's journal fields (FR-006) and the effective name source (FR-004).
It is the same function for students and for teachers (v86: "Правило одно").

| Source | Surname | Given name | Email | Shown |
|---|---|---|---|---|
| `profile` | yes | yes | any | `Surname Name` — the two joined by one space |
| `profile` | yes | no | any | `Surname` (I-1) |
| `profile` | no | yes | any | `Name` (I-1) |
| `profile` | no | no | yes | the part of the email before `@` |
| `email` | any | any | yes | the part of the email before `@` |
| either | — | — | no, and nothing above applies | the translated "student without a name" / "teacher without a name" of US-025 / US-027 (I-3) |

- "The part of the email before `@`" is the stored address up to its first `@`
  — as stored (synchronization keeps addresses trimmed and lower-cased, US-014
  VR-003), never otherwise changed (I-5). An address without `@` or with nothing
  before it counts as no email.
- Names are shown as Google holds them, never translated, never corrected
  (NFR-073, v86).
- The full name is not a source for the report any more (I-6).
- The function depends on nothing of the report: its input is the surname, the
  given name, the email and the source; its output is the displayed name or "no
  name" (the caller chooses the label). A later report — for example of Meet
  attendance — can reuse it unchanged (I-10). This Story uses it in the template
  report only.

### FR-004 The effective name source

For a report request the effective name source is:

1. the value of the report page's name-source parameter (FR-005), when present;
2. otherwise the template's setting (FR-002).

It is resolved once per request, after validation (VR-002), and used for every
person in that report.

### FR-005 The switch on the report page

- The report page of US-027 FR-011 gains one optional query parameter, the name
  source (name indicative `names`; API_DESIGN fixes it), with the values of
  FR-002.
- When a report is shown, the page shows a switch with the two sources and the
  effective one (FR-004) marked. Choosing the other source loads the same page
  with the same template, course and period and the chosen source — a `GET`
  (I-8). The report is rebuilt; nothing is saved.
- The report form (template, course, period) does **not** carry the parameter:
  submitting it shows the chosen template with its own setting (I-4).
- The switch is **viewing**: no template change, no audit row, no write of any
  kind; it works for Admin and Dean under the `UseReportTemplates` policy and in
  read-only mode for all three BR-025 causes (BR-026); it opens no Google port.
- The page includes the effective parameter in its own return path for the
  language switcher, built from validated values only (US-027 S-05; US-025 IMPL
  D-1), so changing the language keeps the chosen source.
- The parameter names a source, never a person — no personal data enters the
  address (Story Notes; US-039 INFO-1).
- Export (US-028) is out of scope; this Story only guarantees that the source is
  part of the address the page was built from, and the report DTO states the
  effective source (FR-006), so an export from the page can reproduce the screen.

### FR-006 Journal fields and the report

US-027 FR-004 and FR-005 change as follows; everything else in them stands.

- **Journal fields.** The fields "Teachers" and "Student" supply, instead of a
  display name, each person's internal participant id, **surname**, **given
  name** and **email** (from `ClassroomParticipant`). The display name is
  computed in `Application` by FR-003, never by the port or a view. The port keeps
  its fixed number of queries per report (US-027 FR-004).
- **Rows of "Grading"** (US-027 FR-005.3): the same students of the period
  (BR-051); each row shows the FR-003 name.
- **Order** (AC-004): rows with a displayed name — every case of FR-003 except
  the "without a name" label — by that name in the collation of the UI language,
  ties broken by internal participant id; then the unnamed rows by internal
  participant id (US-025 FR-005 rule, now applied to the FR-003 name). With
  `profile` this orders by surname.
- **Teachers** (US-027 FR-005.5): the same teachers of the period; each shown
  by FR-003 with the "teacher without a name" label; ordered as the rows; joined
  with ", ". This applies wherever the teachers appear — the header and the
  "Teacher" column of "Lesson topics".
- **Report DTO** carries the effective name source (FR-004) and the computed
  names; no entity reaches the view (AD-8).
- The US-025 journal page and the Meet pages are not changed (Story Out of
  scope).

### FR-007 Audit

- Saving a template — the names setting changed or not — writes the existing
  `ReportTemplateChanged` / `ReportTemplateCreated` row of US-027 FR-016 with the
  template id only. No new audit action. The row carries no setting value.
- A read-only refusal of the save is audited as in US-027 FR-016.
- Using the switch, viewing a report and a validation failure write no audit
  row.

### FR-008 Authorization and read-only mode

- No new page or endpoint: the template form stays under `EditReportTemplates`,
  the report page under `UseReportTemplates` (US-027 FR-012). Anonymous requests
  are sent to sign in; the SC-4 anonymous list gains nothing.
- Every changed endpoint — the report page with the new parameter, the template
  create / copy / change saves with the new field — has allowed-role (Admin,
  Dean) and forbidden-role (anonymous; a Dean on the forced password change)
  tests (TC-5, AC-009).
- Saving a template is refused in `Application` in read-only mode as in US-027
  FR-013, guard first; the switch is not (AC-007).
- No use case of this Story calls a Google API; only the synchronization (which
  is already blocked in read-only mode, BR-026) reads the new profile values.

### FR-009 Persistence

- `ClassroomParticipant`: two nullable text columns for the surname and the
  given name (DB_DESIGN names and bounds them).
- `ReportTemplate`: one non-null column for the name source; existing rows get
  `profile` in the migration; the stored representation is chosen by DB_DESIGN
  (enum stored as in PC conventions).
- One EF Core migration ships both (PC-2). No index is needed: the report reads
  participants by the memberships it already reads (I-9).

### FR-010 Localization

New keys in `SharedResource.uk.resx` and `SharedResource.en.resx` (NFR-073): the
setting's label, the two values' labels (on the form and on the switch), the
switch's caption, and the messages of §6. The "without a name" labels are reused.
Names, emails and email parts are never translated. The existing missing-key test
covers the new keys.

### FR-011 Logging

- The existing "built report" `Information` line (US-027 FR-019) also records the
  effective name source (`profile` / `email`) and whether it came from the page
  or the template — no names.
- A rejected name-source value: one `Warning` naming the parameter and the rule,
  never the value (SC-10).
- No log line carries a surname, given name, email or its part (SC-10,
  NFR-023). Synchronization logging is unchanged: it never logged names.

## 5. Acceptance Criteria

| Id | Criterion (Story) | Covered by |
|---|---|---|
| AC-001 | Surname and given name are synchronized; replaced on change; none when the profile has none | FR-001 |
| AC-002 | "From the Google profile": "Surname Name", else the part before `@` — students and teachers | FR-003, FR-006 |
| AC-003 | "From the email": the part before `@` exactly as stored — students and teachers | FR-003, FR-006 |
| AC-004 | Students ordered by the name shown, UI-language collation; by surname with `profile` | FR-006 |
| AC-005 | Built-in uses `profile`, unchangeable; a copy switches both ways; a pre-existing template uses `profile` | FR-002, FR-009 |
| AC-006 | The switch rebuilds the report; template unchanged; no audit; source in the address — reload keeps it | FR-004, FR-005, FR-007 |
| AC-007 | Read-only mode: the switch works; the setting change is refused in `Application`; no Google API | FR-005, FR-008 |
| AC-008 | Saving a changed setting writes one template-change audit row with the template id only | FR-007 |
| AC-009 | Admin and Dean only; allowed- and forbidden-role tests for every changed endpoint | FR-008 |
| AC-010 | Unknown name source in the form or the address refused with a message; nothing saved; value not logged | VR-001, VR-002, FR-011 |
| AC-011 | Labels and the switch translated; names as Google holds them | FR-003, FR-010 |
| AC-012 | Synthetic data in PostgreSQL; Google ports substituted | §9, TC-2, TC-4 |
| AC-013 (derived) | Partial profile and nameless person shown per FR-003 (one part only; the "without a name" label) | FR-003 |
| AC-014 (derived) | Changing the language on the report page keeps the chosen source | FR-005 |

## 6. Validation Rules

Applied in `Application` (`Validation`) before anything is read or written
(§8, `package-map.md`).

### VR-001 The template form's "names"

Exactly `profile` or `email`. Missing, empty, repeated or any other value is
malformed (a tampered form: the form always sends one of the two) and refused
with the form again and a translated message; nothing saved (US-027 VR-002
precedent).

### VR-002 The report page's name source

- Absent: the template's setting (FR-004).
- Present: exactly `profile` or `email`, compared exactly (case-sensitive, no
  trimming — the page writes it). Empty, repeated or any other value is
  malformed: the page answers as for a malformed report query (US-027 FR-011 —
  the form with a translated message, no report data read, status per
  API_DESIGN, expected `400`).
- The rejected value is never echoed into the page, a log line or an audit row
  (SC-10).

### VR-003 Google values

The surname and the given name from Google are trimmed, blank becomes none, and
they are cut to the stored bound (FR-001); control characters are treated as the
full name is today. They are rendered HTML-encoded (US-027 VR-009).

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | No new endpoint; the changed pages keep `UseReportTemplates` / `EditReportTemplates`; deny by default; SC-4 list unchanged. | SC-4, §2 |
| S-02 | Allowed-role and forbidden-role tests for every changed endpoint. | SC-1, TC-5 |
| S-03 | The address carries the template reference, course id, dates and the name source only — no personal data; the switcher return path is built from validated values. | Story Notes, US-039 INFO-1, US-027 S-05 |
| S-04 | Template save refused in read-only mode in `Application`, guard first, all three BR-025 causes; the switch writes nothing and works in read-only mode. | AD-6, BR-025, BR-026 |
| S-05 | No Google API call outside the existing synchronization; the sync's scopes are unchanged — the profile name is part of the roster read already authorised (§6). | SC-5, SC-8 |
| S-06 | Input validated before business logic; rejected value never logged or echoed. | §8, SC-10 |
| S-07 | Surname, given name and email part are personal data: shown only where the email or full name may be shown today (report page, Admin and Dean); never in a log, an audit row or an address. | SC-10, NFR-023, Story Notes |
| S-08 | Output HTML-encoded. | SC-10 |
| S-09 | No entity in a view model; DTOs mapped in `Application`. | AD-8 |
| S-10 | No new outbound destination. | SC-13 |
| S-11 | Audit per FR-007 — template id only, no setting value. | SC-11 |
| S-12 | Every `POST` keeps the antiforgery token. | §8 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Unknown name source in the report address | The report page with the form and a translated message; no report; expected `400` (VR-002). |
| Missing or unknown "names" in a template form | The form again with the values and a message; nothing saved; expected `400` (VR-001). |
| Template save in read-only mode | `ReadOnlyModeException` → the US-008 mapping; audit `Refused` / `ReadOnlyMode` (US-027). |
| Change of the built-in template's setting | Refused as every change of it (US-027 FR-006). |
| Participant with no surname, given name or email | Not an error: FR-003 label. |
| Anonymous; Dean on forced password change | As US-027 §8. |
| Unexpected failure | The single exception handler (AD-9). |

## 9. Non-Functional Requirements

- **Bounded queries** — the report keeps a fixed number of round trips (US-027
  FR-004); the new columns come with the participants already read.
- **NFR-070** — the switch is usable at phone width.
- **NFR-073** — bilingual labels; collation of the UI language for ordering.
- **TC-2, TC-4** — integration tests on PostgreSQL via Testcontainers; the
  Google read port substituted with synthetic roster entries carrying surname
  and given name (AC-012).
- **NFR-062** — .NET 10, nullable enabled, warnings as errors.

## 10. Out of Scope

- Checking names (Latin letters, missing surname…) and correcting them in the
  program (v86, Owner's decision 2026-10-05).
- ПІБ with patronymic from the school's own data — Epic 14.
- The US-025 journal page and the Meet pages.
- Export of any kind (US-028, US-029, US-030); this Story only puts the source
  into the page address and the report DTO.
- A per-request source other than the two values; a third "full name" option.

## 11. Open Decisions

None. `docs/decisions/US-042-open-decisions.md` records that the Story carried
none and that writing this Specification raised none; no item of
`trebovaniya.md` §7 concerns this Story.

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally; each
can be corrected at `HUMAN_SPEC_APPROVAL`, and any may be turned into an Open
Decision there.

- **I-1 One part of the profile name.** v86 falls back to the email only "если
  фамилии и имени в профиле нет" — when both are missing. A profile with only one
  of them shows that one; the email is not used.
- **I-2 A new template starts with `profile`.** It matches the built-in template
  and the paper journal; a new template's other defaults (US-027 I-2) mirror the
  journal, but the journal page's full name is not a report source any more.
- **I-3 A person with nothing to show** (no profile name with `profile` and no
  email; or no email with `email`) gets the existing "student / teacher without
  a name" label and sorts last — the US-025 / US-027 rule, unchanged.
- **I-4 The report form resets the switch.** Choosing another template shows its
  own setting; only the switch sets the parameter. Otherwise a value flipped for
  one template would silently override the next.
- **I-5 "As it is" means as stored.** Addresses are stored lower-cased (US-014
  VR-003); Google addresses are case-insensitive, so the part before `@` is shown
  as stored.
- **I-6 The full name leaves the report.** v86 names only two sources; the full
  name ("Name Surname") is exactly what the profile option replaces.
- **I-7 Bounds.** Google documents no length for `familyName` / `givenName`;
  DB_DESIGN fixes a bound no smaller than any realistic name, as for the full
  name (750).
- **I-8 The switch is a link or a `GET` form.** It changes no state, so no
  `POST` and no antiforgery token; API_DESIGN chooses the markup.
- **I-9 No index.** The new columns are read with participant rows already
  selected by membership; nothing filters or sorts on them in the database —
  ordering is done in `Application` in the UI-language collation, as today.
- **I-10 Reuse, not scope.** A Meet report is likely to need the same rule
  (Owner, 2026-10-05), but `trebovaniya.md` v86 sets it for the template report
  only and the Story excludes the Meet pages. FR-003 is therefore written as a
  report-independent function; applying it elsewhere — and how a Meet attendee
  known only by email gets a surname — belongs to a future Story.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-001, FR-009 | VR-003 | S-05, S-07 |
| AC-002 | FR-003, FR-006 | — | S-07, S-08 |
| AC-003 | FR-003, FR-006 | — | S-07, S-08 |
| AC-004 | FR-006 | — | — |
| AC-005 | FR-002, FR-009 | VR-001 | — |
| AC-006 | FR-004, FR-005, FR-007 | VR-002 | S-03 |
| AC-007 | FR-005, FR-008 | — | S-04, S-05 |
| AC-008 | FR-007 | — | S-11 |
| AC-009 | FR-008 | — | S-01, S-02, S-12 |
| AC-010 | FR-011 | VR-001, VR-002 | S-06 |
| AC-011 | FR-003, FR-010 | — | S-08 |
| AC-012 | §9 | — | S-05 |
| AC-013 (derived) | FR-003 | — | — |
| AC-014 (derived) | FR-005 | VR-002 | S-03 |
