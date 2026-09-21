---
artifact_type: specification
story: US-010
version: 1
status: APPROVED
created_at: 2026-09-20T19:33:23Z
updated_at: 2026-09-20T19:54:41Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-010-connection-instructions.md
    version: null
  - path: trebovaniya.md
    version: 79
  - path: docs/decisions/US-010-open-decisions.md
    version: 1
supersedes: null
---

# US-010 Specification — Connection instructions for the school super-admin

## 1. Overview

US-009 let the Admin record *which* technical account the installation reads the
school's data as. This Story produces the text that makes that account exist in
the first place: the instruction the school's **super-admin** carries out once, in
the school's own Google console, outside this program — authorise domain-wide
delegation for this school's service account, and create the technical account.

Four properties shape everything below.

- **It is individual to one school.** `trebovaniya.md` §6 is explicit — the
  instruction carries *that school's* service-account client ID, and "одной
  бумажки на всех быть не может". The admin panel generates it for its own
  `Installation` (§4, Epic 6).
- **It is a read, and only a read.** The page renders what `LegitimacyState`
  already holds. No table is written, no audit row is added, no Google call and no
  Control Plane call is made while rendering — in any mode. That is what lets it
  stay available when nothing else does (BR-026: "инструкцию по подключению
  посмотреть можно").
- **It states what must be true; it does not check it.** Confirming that
  delegation is actually in place is "Проверить доступ" — **US-011**, which brings
  the first real Google port. This Story adds no port, requests no scope at
  runtime and calls nothing.
- **Nothing in it is a secret.** Exactly two non-secret values cross the boundary
  from the Owner to a school: the service account's **client ID** and the **scope
  list** (§1, BR-032). The service-account key, its reference and the OAuth client
  secret appear nowhere in this Story — not in a DTO, not in a view, not in the
  copied text. A field or column carrying any of them is a Critical finding, not a
  design choice (SC-7, a Hard Stop).

One more property is a trap worth naming at the top: §6 lists **three
identifiers** that must not be confused — the service account's numeric client ID
(for DWD), the OAuth web client id of the installation's configuration (for the
Admin's sign-in), and the `Installation` UUID of the service channel. **Only the
first belongs on this page.** Printing another would send a school to authorise
the wrong client, and the delegation would silently not work.

## 2. Business Goal

Until the admin panel generates this text, onboarding depends on the Owner
emailing values by hand — exactly the error-prone step DC-2 exists to remove — and
DC-2's delegation step cannot be handed to a school with confidence.

It is also how a **key rotation that recreated the service account** reaches a
school without the Owner touching the school's server: the Owner updates the
client ID on the `Installation`, and the school's instruction shows the new one no
later than the next legitimacy check, within six hours (§9, BR-035, v43, v54).

And it is the screen that must work when the school is in trouble: a school that
cannot synchronise — suspended, past its grace period, never legitimated, or
simply cut off from the Control Plane — is precisely the school whose super-admin
may need to fix delegation.

## 3. Business Flow

### 3.1 Onboarding a new school

The Owner has created the `Installation` with the school's domain and the client
ID of that school's service account (US-002). The installation has checked its
legitimacy at least once, so `LegitimacyState` holds both values (US-005). The
Admin signs in (US-008), opens the instruction from the settings section US-009
established, reads the school's own client ID, the six scopes and the
technical-account requirements, copies the text and sends it to the school's
super-admin. The super-admin authorises delegation and creates the technical
account; the Admin then enters that account's address in the connection settings
(US-009) and checks access (US-011).

### 3.2 A school that has not yet confirmed its legitimacy

No check has ever succeeded, so neither the domain nor the client ID is known. The
page is still served. The parts that do not depend on the school — the scope list
and the technical-account requirements — are shown in full; where the client ID
and the domain would be, the page states that the installation has not yet
confirmed its legitimacy and that the values appear once it has. Nothing is
invented, guessed or read from configuration in their place.

### 3.3 A school in read-only mode

Suspended by the Owner, past its grace period, or never legitimated (BR-025). The
instruction is served with everything `LegitimacyState` knows, and the read-only
reason is stated as on the other admin screens — without implying that the
instruction itself is unavailable, because it is not.

### 3.4 An unreachable Control Plane

Nothing changes for this page: it reads the installation's own database. The last
recorded answer is what it shows; it is never fetched at render time.

### 3.5 The Owner recreated the service account

The new client ID arrives with the next successful legitimacy check and
`LegitimacyState` is updated (US-005). The next render shows it. There is no
cache, no restart and no action by the Owner on the school's server.

### 3.6 A Dean tries to open it

Refused. The permission-matrix row "Просмотр инструкции по подключению" is `✔`
Admin, `✘` Dean (§2, v39). An anonymous visitor is sent to sign in and never sees
the page.

## 4. Functional Requirements

### FR-001 What the instruction contains

The instruction is assembled from six parts, and from nothing else:

1. **Which school it is for** — the installation's Workspace domain (FR-003), so
   the reader can see that this instruction is not another school's.
2. **The service account's client ID** of *this* `Installation` (FR-003), with the
   statement that the value belongs to this school alone and a neighbouring
   school's instruction carries a different one (§6).
3. **The scope list** — exactly the six scopes of §6 (FR-004).
4. **What the technical account must be** — the five statements of FR-005
   (BR-015, §9, OD-001).
5. **What the super-admin actually does** — authorise domain-wide delegation for
   that client ID with those scopes in the school's own Google console, and create
   the technical account. Stated as the two actions §1 and BR-032 fix, without
   inventing console menu paths, which are Google's and change (I-3).
6. **The read-only reason**, when the installation is in read-only mode (FR-009).

No part of the instruction is editable, and none of it comes from a setting a
school could change: every value is either a requirement fixed in code (FR-004,
FR-005) or a value the Control Plane supplied (FR-003).

### FR-002 The instruction state

A query in `Application` returns the instruction as one DTO with a state of
exactly two members:

- `Complete` — a legitimacy check has succeeded at least once and both the domain
  and the client ID are non-empty; every part of FR-001 is present.
- `InstallationNotConfirmed` — no check has ever succeeded, or the stored domain
  or client ID is empty; parts 1 and 2 of FR-001 are replaced by the statement of
  FR-003, and parts 3, 4 and 5 are unchanged and complete.

There is no third state. In particular, read-only mode is **not** a state of the
instruction: an installation can be read-only and its instruction `Complete`, and
the two are reported independently (FR-009).

### FR-003 The school-specific values come from `LegitimacyState`

The domain and the service-account client ID are read from `LegitimacyState`,
which the legitimacy check writes even in read-only mode (US-005, §3, v54).

- They are read through a port from `Application` on every request. The Control
  Plane is **never** asked at render time, and neither value is copied into any
  other table or cached in memory between requests (AC-002, AC-006).
- A value counts as known only when a check has actually **succeeded**: the same
  rule US-009 FR-003 applies, so an `upgrade_required` answer — which records a
  domain and a client ID without moving the last successful check — licenses
  nothing here either. The existing "known domain" rule is reused, not restated in
  a second place (I-1).
- When a value is not known, the page says so in the user's language and shows
  nothing in its place. Configuration is not consulted: the installation's own
  OAuth client id is a different identifier and must not be substituted (FR-006).
- This Story **writes nothing** to `LegitimacyState` (FR-010).

### FR-004 The scope list is a fixed requirement in code

The six scopes of §6 are a **requirement, not a configuration**: an installation
cannot change them and no school may authorise a different set.

- They live as an ordered, immutable constant in `Domain` (I-2), and are rendered
  from it:
  `https://www.googleapis.com/auth/classroom.courses.readonly`,
  `https://www.googleapis.com/auth/classroom.rosters.readonly`,
  `https://www.googleapis.com/auth/classroom.profile.emails`,
  `https://www.googleapis.com/auth/classroom.coursework.students.readonly`,
  `https://www.googleapis.com/auth/classroom.courseworkmaterials.readonly`,
  `https://www.googleapis.com/auth/admin.reports.audit.readonly`.
  Each is rendered as its full URI, which is what the Google console expects; the
  §6 table names them by their last segment (I-4).
- A test asserts the rendered list against the requirement list, so a scope added
  in code without a requirements change fails (NFR-021, SC-8).
- `drive.file` and `classroom.profile.photos` — the two the prototype requested,
  which no Epic uses and which §6 excludes (v25) — appear nowhere, and a test
  asserts their absence.
- The identity scopes of the Admin's own sign-in (`openid`, `email`, `profile`)
  are **not** in this list: §6 (v78) states that delegation and sign-in are
  different mechanisms, and a test asserts that none of the three is rendered.
- Every scope is read-only. No scope in the list grants a write, and no scope
  outside the list is named anywhere on the page (BR-030, SC-8).

### FR-005 What the instruction says about the technical account

The technical-account part makes exactly these five statements, in the reader's
language (BR-015, §9, OD-001):

1. the school's super-admin creates the account at onboarding, together with
   domain-wide delegation;
2. no person stands behind it, it is **not** a super-admin, and nobody signs in to
   the program with it;
3. it must be able to **read** Classroom and Admin Reports, and must hold nothing
   beyond that — stated as the requirement, with **no Workspace admin-role name**
   printed while `trebovaniya.md` §7 item 10 is unverified (OD-001);
4. an account with write access to Google Workspace, and a super-admin account,
   are **not** acceptable (§9, BR-030);
5. its address is entered by the Admin in the connection settings (US-009), and
   its domain must be the school's domain (BR-020).

VR-005 makes statement 3 enforceable: no Workspace admin-role name may appear in
any translation entry this Story adds.

### FR-006 The three identifiers

The page shows the **service account's client ID** and nothing else that could be
mistaken for it (§6, v78):

- the **OAuth web client id** from the installation's configuration is not read,
  not passed to the view and not rendered;
- the **`Installation` identifier** (the UUID of the service channel) is not
  rendered;
- the service account's own email address (`…@….iam.gserviceaccount.com`) is not
  rendered either: §9 (v53) keeps it a machine account known from the key in the
  secret store, and the console step needs the client ID.

A test asserts that the configured OAuth client id does not appear in the
response, so a later convenience change cannot leak the wrong identifier onto the
page.

### FR-007 The instruction page

One server-rendered `GET` in the admin panel's settings section, Admin only
(FR-011). It takes no parameter; any query string is ignored (VR-001).

- The view receives a view model built from the `Application` DTO of FR-002 and
  holds no rule of its own (AD-3, AD-8). No domain entity reaches it.
- The rendered HTML is **complete without scripting**: every value and every
  sentence of FR-001 is in the markup, and the text is selectable, so the
  instruction is usable with JavaScript disabled and prints from the browser
  (OD-002).
- Every value is rendered as data through the view engine's HTML encoding, never
  as raw markup (VR-004).
- No `POST` exists in this Story, so no antiforgery token, no exemption and no new
  entry in the closed lists of US-008 FR-005 (S-08).

The exact path and the operation belong to API_DESIGN; the Specification fixes
only that it is a single `GET` inside the settings section US-009 established
(spec I-10 there) and that no second endpoint is added (OD-002).

### FR-008 The hand-over affordance

The instruction is handed over **from the page**, with a copy-to-clipboard
affordance (OD-002).

- The copied text is assembled from the **same rendered values** — the client ID,
  the domain, the scope list and the technical-account statements of FR-001. No
  second endpoint is called and no value is fetched again (S-09).
- It carries **no secret**: not the service-account key, not its reference, not
  the OAuth client secret, not a session identifier or antiforgery token (SC-7, a
  Hard Stop). A test asserts the copied region against the page's own values.
- It is **progressive enhancement**. The affordance is a static script file served
  from `wwwroot`, not an inline script, so a Content-Security-Policy added later
  needs no `unsafe-inline` (I-5). With scripting or the clipboard unavailable the
  page is unchanged and the text stays selectable — the Admin is never left
  without it (AC-009).
- Its label comes from the translation files like every other string (FR-012).
- Copying contacts no outside service and writes nothing (S-09, S-10).

### FR-009 Read-only mode and an unreachable Control Plane

The page is served in **all three** read-only causes of BR-025 — suspended, grace
period expired, never legitimated — and when the Control Plane is unreachable.

- The read-only reason is shown in the user's language, as on the landing page and
  the connection settings, and phrased so it does not suggest the instruction is
  unavailable (BR-026, AC-005).
- No read-only guard is invoked, because nothing is written: the guard governs
  writes (US-007), and this Story performs none (FR-010). The instruction is on
  the *permitted* side of BR-026 by being a read, not by an exemption, so the
  BR-026 closed list of service writes gains **no member** and
  `PermittedServiceWrite` keeps exactly its four members.
- No Google call is made in any mode (BR-026, SC-5, SC-8): this Story adds no
  Google port at all.
- The existing read-only mode query (`GetLegitimacyModeQuery`, US-007/US-009) is
  reused; no second way of deciding read-only mode is introduced.

### FR-010 Rendering writes nothing

- **No audit row.** §5 lists the audited actions and viewing this page is not one
  of them; SC-11 is satisfied by adding nothing (I-6).
- **No table is written**, in any mode, including when the state is incomplete
  (FR-002, AC-007, AC-008).
- No entity is loaded for update: every read uses the read path the repositories
  already expose.
- A test proves it: the page is rendered repeatedly, in read-only and in normal
  mode, and the audit table and every other table are unchanged.

### FR-011 Authorization

`InstallationPolicies` gains **`ViewConnectionInstruction`**, the permission-matrix
row "Просмотр инструкции по подключению" — `✔` Admin, `✘` Dean (§2, v39).

- It is a **separate** policy from `ConfigureWorkspaceConnection`: §2 (v39) splits
  the rows deliberately, and SC-1 requires the matrix to be implemented as
  written. Reusing the US-009 policy would merge two cells (I-7).
- A Dean is refused with `403` and the error page; an anonymous visitor is sent to
  sign in and never sees the page (SC-4).
- Both the allowed-role and the forbidden-role case are tested (TC-5).
- The SC-4 closed list of anonymous endpoints gains nothing, and the US-008
  endpoint-enumeration test passes unchanged.
- No other matrix cell is implemented speculatively (SC-1).

### FR-012 Localization

Every string this Story adds exists in both `SharedResource.uk.resx` and
`SharedResource.en.resx` (NFR-073, TC-8): the title, the labels, each paragraph of
the instruction, the technical-account statements of FR-005, the
not-yet-confirmed statement of FR-003, the copy affordance's label and its
confirmation, and the navigation entry. Ukrainian is the default for a school that
set no language.

- The instruction is the longest translated text in the program so far, so it is
  broken into **one key per paragraph or statement**, never one key for the whole
  instruction: a change to one sentence must not invalidate both translations
  wholesale (I-8).
- The client ID, the domain and the scope identifiers are **data**: rendered as
  stored, never translated and never re-cased (AC-010).
- A test fails on a key present in one file and missing from the other, and a test
  asserts that every key this Story's view uses exists in both (TC-8).

### FR-013 The admin panel

The settings section US-009 created (its spec I-10) gains a second entry, leading
to the instruction, visible to an Admin and not to a Dean (FR-011). The section
itself is not redefined and gains no business rule; US-011 and US-012 add theirs
next to these two.

### FR-014 Logging

- A rendered instruction is **not** logged: it is an ordinary page view, and the
  request log of the host already records that a request happened.
- No log line carries the client ID, the domain, an account address or any part of
  the instruction (SC-10, NFR-023).
- No new log event is introduced by this Story.

### FR-015 Persistence

**This Story changes no schema.** It adds no table, no column, no constraint and
therefore **no EF Core migration** — it reads `legitimacy_state`, which US-005
created. A migration appearing in this Story is a defect (PC-2). The Control Plane
database gains nothing (AD-1).

### FR-016 Wiring

The query and the page's dependencies are registered in the installation's
composition root next to the US-009 registrations, and the new policy is
registered where `ConfigureWorkspaceConnection` is.

- The scope constant needs no registration (FR-004).
- **No NuGet package is added.** `Application` still references none and `Domain`
  keeps zero package references (AD-3); the existing structural tests that assert
  this pass unchanged.
- No port is added to `Application/Ports`: the two the page needs
  (`ILegitimacyStateRepository`, and the read-only mode query's own dependencies)
  already exist.

## 5. Acceptance Criteria

| Story AC | Satisfied by |
|---|---|
| AC-001 Only an Admin sees the instruction | FR-007, FR-011, FR-013 |
| AC-002 The instruction carries this school's own client ID | FR-001, FR-003, FR-006 |
| AC-003 The scope list is exactly the one the requirements fix | FR-004 |
| AC-004 The instruction states what the technical account must be | FR-005, FR-012 |
| AC-005 The instruction is readable when nothing else works | FR-009, FR-003 |
| AC-006 A rotated client ID appears without anyone visiting the school | FR-003 |
| AC-007 Reading the instruction changes nothing | FR-010, FR-015 |
| AC-008 Before the first successful check the instruction says what is missing | FR-002, FR-003 |
| AC-009 The instruction is ready to hand over | FR-008 |
| AC-010 Every string is translated | FR-012 |

## 6. Validation Rules

Framework defaults are not relied on. This Story accepts no user input at all,
which narrows the rules but does not remove them: what is rendered is data that
arrived from outside the installation, and SC-10 requires it to be handled as
such.

### VR-001 The request carries no input

The operation takes no route parameter, no query parameter and no body. A query
string, if present, is ignored and never echoed into the response or a log. No
model binding takes place, so there is nothing to validate and nothing to reject —
and no path by which a caller can influence the rendered text.

### VR-002 A client ID counts as present

The client ID is rendered only when the stored value is non-null and not
whitespace, and a legitimacy check has succeeded at least once (FR-003).
Otherwise the not-yet-confirmed statement is rendered in its place. No length or
format rule is imposed on it here: the value is whatever the Control Plane
recorded on the `Installation`, validated by US-005 on arrival, and this Story
must not silently reject a client ID a school needs (I-9).

### VR-003 A domain counts as present

The same rule as VR-002, applied to the stored domain, and identical to the rule
US-009 FR-003 already uses — the installation's domain is known only from a
successful check.

### VR-004 Every rendered value is encoded as data

The client ID, the domain and the scope identifiers are written through the view
engine's HTML encoding. `Html.Raw` (or any equivalent bypass) is used for none of
them, and no part of the instruction is assembled as markup from a stored value.
A stored value containing markup is therefore rendered as text (SC-10).

### VR-005 The instruction names no Workspace admin role

No translation entry this Story adds contains a Google Workspace admin-role name,
while `trebovaniya.md` §7 item 10 is unverified (OD-001). A test asserts the
prohibition against both language files, so a later "helpful" addition fails a
test instead of reaching a school.

## 7. Security Requirements

| Id | Requirement | Source |
|---|---|---|
| S-01 | The page declares the `ViewConnectionInstruction` policy; anonymous access is impossible and the SC-4 closed list gains nothing. | SC-4, API-9, §2 |
| S-02 | A Dean is refused (`403`), and both the allowed-role and the forbidden-role case are tested. | SC-1, TC-5, §2 |
| S-03 | No secret appears in the response, the view model, the copied text or a log: not the service-account key, not its reference, not the OAuth client secret. | SC-7 (Hard Stop), PC-9 |
| S-04 | The only identifiers rendered are the service account's client ID and the school's domain — both non-secret values §1 and BR-032 allow to cross to a school. The OAuth web client id and the `Installation` UUID are not rendered. | §6 (v78), BR-032 |
| S-05 | No Google call is made, in any mode; no Google port, scope request or SDK type is added. | SC-5, SC-8, BR-026 |
| S-06 | No Control Plane call is made while rendering; the values come from the installation's own `LegitimacyState`. | SC-9, §3 (v54) |
| S-07 | Rendering writes nothing: no audit row, no table, no state. `PermittedServiceWrite` keeps its four members and the BR-026 closed list is unchanged. | SC-11, BR-026 |
| S-08 | No `POST` and no download endpoint is added, so the antiforgery and SC-4 closed lists of US-008 are untouched. | SC-4, OD-002 |
| S-09 | Nothing leaves the installation on its own. The copy affordance is local to the browser and contacts no service; what the super-admin receives is what the Admin themselves sends. SC-13's two destinations are unchanged. | SC-13 (Hard Stop) |
| S-10 | No log line carries the client ID, the domain, an account address or any part of the instruction. | SC-10, NFR-023 |
| S-11 | Every rendered value is HTML-encoded as data; no stored value is rendered as markup. | SC-10, VR-004 |
| S-12 | No teaching data and no personal data of a student appears on the page — the instruction is about configuration only. | SC-12, §5 |

## 8. Error Handling

| Situation | Answer |
|---|---|
| Anonymous request | Redirected to sign-in; the page is never rendered (SC-4). |
| Signed-in Dean | `403` with the error page (FR-011, API-5). |
| A wrong HTTP method on the path | Whatever the host's existing rules produce; this Story adds no method and changes no handler. The US-008 finding F-5 (a wrong method on an anonymous route answering `404` where the contract documents `405`) is not re-opened here and not inherited by a new route. |
| The domain or the client ID is not known | Not an error: the page renders the not-yet-confirmed statement (FR-002, FR-003). |
| Read-only mode | Not an error: the page renders with the reason (FR-009). |
| An unreachable Control Plane | Not an error for this page: nothing is fetched at render time (FR-003). |
| An unexpected failure | The single exception handler answers the error page, with no exception text and no stack trace reaching the user (AD-9, API-10, SC-10). |

Because nothing is submitted, this Story produces **no** `400` and **no** `409`:
there is no request to malform and nothing for the installation's state to refuse.

## 9. Non-Functional Requirements

- **NFR-073** — the whole instruction is translated (Ukrainian, English); it is
  explicitly named there as translated text (FR-012).
- **NFR-021** — every scope rendered is read-only, asserted by a test (FR-004).
- **NFR-070** — the page is usable on a phone; the instruction is long, so it is
  laid out to stay readable at narrow widths and printable.
- **NFR-023 / SC-10** — no value from the page reaches a log.
- **NFR-062** — .NET 10, C#, nullable enabled, warnings as errors.
- The page performs at most two reads of the installation's database per request
  (the legitimacy state and the read-only mode decision) and no network call, so
  it stays the cheapest page in the admin panel — which is what makes it
  dependable when the school is in trouble.

## 10. Out of Scope

- **"Проверить доступ"** and the startup access self-check — US-011, which also
  brings the first real Google port and therefore inherits the US-007
  security-review finding F-5.
- **Any Google call**, including verifying that delegation is in place.
- **Changing the client ID or the domain** — the Owner's action in the Control
  Plane (US-002, US-004, BR-021). No screen of any installation offers it.
- **The connection settings themselves** — US-009, delivered.
- **Dean accounts** — US-012. The forbidden-role half of this Story's
  authorization tests therefore still uses a synthetic Dean principal, as US-009
  finding F-1 records.
- **A user's UI language choice** — US-039.
- **A download, a generated document or an email** — rejected by OD-002.
- **Documentation beyond this screen** — `trebovaniya.md` §7 item 26 is open and
  must not be pre-empted (see the `open_decisions` artifact).
- **Naming concrete Workspace admin roles** — blocked by §7 item 10 (OD-001).

## 11. Open Decisions

Full text, options and resolutions in
`docs/decisions/US-010-open-decisions.md`.

| Id | Status | Impact if not resolved |
|---|---|---|
| OD-001 What the instruction says about the technical account's Workspace roles | **RESOLVED** by the Owner on 2026-09-20 (option 1) | none — FR-005, VR-005 and AC-004 are written against the resolution |
| OD-002 How the instruction is handed over | **RESOLVED** by the Owner on 2026-09-20 (option 1) | none — FR-007, FR-008 and S-08 are written against the resolution |

`trebovaniya.md` §7 item 10 stays **open** and this Story is written so that it
does not need it: OD-001 prints the requirement instead of a role name. Item 26
(documentation) is open and untouched. SPECIFICATION raised no new Open Decision.

### Interpretations

Stated because neither the Story nor `trebovaniya.md` fixes them literally. Each
is a decision this Specification makes explicit so it can be corrected at the
gate rather than discovered in code.

- **I-1** "Known" means the same here as in US-009 FR-003: a value from a check
  that actually **succeeded**. The existing rule is reused rather than restated,
  so an `upgrade_required` answer cannot make an instruction look complete in one
  screen and incomplete in another.
- **I-2** The scope constant lives in **`Domain`** (a `Rules` invariant that holds
  regardless of use case, `package-map.md`), not in `Infrastructure/Google`.
  US-011 will call Google with the same list from `Infrastructure`, and this page
  renders it through `Application` — only `Domain` is referenced by both, so any
  other location would force a second copy. It holds plain strings and no Google
  SDK type, so AD-4 is untouched.
- **I-3** The instruction states the two **actions** §1 and BR-032 fix — authorise
  domain-wide delegation for this client ID with these scopes, and create the
  technical account — and does not reproduce Google console menu paths. Those are
  Google's, they change without notice, and a stale click-path in a translated
  file would be the first thing to mislead a super-admin.
- **I-4** Scopes are rendered as **full URIs**
  (`https://www.googleapis.com/auth/…`), because that is the form the Google
  console accepts. §6 tabulates them by their last segment; the test that ties the
  rendered list to the requirement list compares the segments, so the two cannot
  drift.
- **I-5** The copy affordance is a **static script file**, not inline script. The
  program has no JavaScript today; starting with an external file means a
  Content-Security-Policy added later needs no `unsafe-inline`. The page is correct
  without it (FR-008).
- **I-6** Viewing the instruction is **not** audited. §5's list of audited actions
  does not include it, and a row per page view would fill the trail with
  duplicates while telling no one anything — and in read-only mode it would write
  where nothing was attempted. This mirrors US-009 I-5.
- **I-7** The instruction gets its **own** authorization policy rather than
  reusing `ConfigureWorkspaceConnection`. §2 (v39) split the two matrix rows on
  purpose: one is a write the read-only mode blocks, the other a read it permits.
  One policy for both cells would tie them together in code exactly where the
  requirements separate them.
- **I-8** The instruction is broken into **one translation key per paragraph or
  statement**, as the Story's note asks, and the keys are named for what the
  paragraph says rather than numbered, so a reordering does not rename them.
- **I-9** No format rule is imposed on the stored client ID at render time. It is
  the Control Plane's value, checked when it arrived (US-005); a shape check here
  could only ever refuse to show a school the value it needs.
- **I-10** No message on this page depends on the school time zone, which is still
  not a setting (US-008 I-12, US-009 I-9 continue to hold). The read-only reason
  is rendered as the other screens render it.

## 12. Traceability

| Story AC | Functional requirement | Validation | Security |
|---|---|---|---|
| AC-001 | FR-007, FR-011, FR-013 | VR-001 | S-01, S-02 |
| AC-002 | FR-001, FR-003, FR-006 | VR-002 | S-04, S-06, S-11 |
| AC-003 | FR-004 | — | S-05 |
| AC-004 | FR-005, FR-012 | VR-005 | S-03 |
| AC-005 | FR-003, FR-009 | VR-002, VR-003 | S-05, S-06, S-07 |
| AC-006 | FR-003 | VR-002 | S-06 |
| AC-007 | FR-010, FR-015 | — | S-07 |
| AC-008 | FR-002, FR-003 | VR-002, VR-003 | S-06 |
| AC-009 | FR-008 | — | S-03, S-08, S-09 |
| AC-010 | FR-012 | VR-004, VR-005 | S-11 |
| — (logging, wiring, no schema change) | FR-014, FR-015, FR-016 | — | S-10, S-12 |

Requirement sources: `trebovaniya.md` v79 §1, §2, §3, §4 (Epic 6), §5, §6, §7
(items 10 and 26, both open), §9; BR-015, BR-020, BR-021, BR-025, BR-026, BR-030,
BR-032, BR-033, BR-035; NFR-021, NFR-023, NFR-062, NFR-070, NFR-073; AD-1, AD-3,
AD-4, AD-6, AD-8, AD-9; API-3, API-4, API-5, API-9, API-10; SC-1, SC-4, SC-5,
SC-7, SC-8, SC-9, SC-10, SC-11, SC-12, SC-13; PC-2, PC-9; DC-2, DC-5, DC-10;
TC-2, TC-3, TC-4, TC-5, TC-8.
