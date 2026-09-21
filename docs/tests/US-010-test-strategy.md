---
artifact_type: test_strategy
story: US-010
version: 1
status: DRAFT
created_at: 2026-09-20T20:40:00Z
updated_at: 2026-09-20T20:40:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-010-connection-instructions.md
    version: null
  - path: docs/specifications/US-010-spec.md
    version: 1
  - path: docs/decisions/US-010-open-decisions.md
    version: 1
  - path: docs/designs/api/US-010-api-design.md
    version: 1
  - path: docs/designs/api/US-010-openapi.yaml
    version: 1
  - path: docs/designs/database/US-010-db-design.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-010 Test Strategy — Connection instructions for the school super-admin

## 1. Scope

One `GET` and its content. The Story renders the instruction the school's
super-admin carries out: this school's service-account client ID and domain from
`LegitimacyState`, the six fixed scopes of `trebovaniya.md` §6, and what the
technical account must be. It writes nothing, calls nothing outside the
installation, and changes no schema.

That shape decides the whole strategy: **almost everything is provable over
HTTP against the running host and the real migrated database**, because the page
is a pure function of stored state plus code constants. There is no request to
validate, no transaction to prove, no audit row to check for — only a rendered
response, and absences that must be asserted rather than assumed.

In scope:

- what the instruction shows, in all four state/mode combinations;
- that it shows **this** school's client ID and not one of the two identifiers
  §6 (v78) warns against confusing it with;
- the scope list, positively (all six, in order) and negatively (the two §6
  excludes, the three identity scopes of the Admin's own sign-in);
- the five technical-account statements, and the absence of any Workspace
  admin-role name (OD-001);
- authorization: the new policy, the Admin, the Dean, the anonymous visitor;
- the hand-over affordance of OD-002 and the absences it implies (no download
  route, no secret, no inline script);
- translation completeness in Ukrainian and English, and that data is not
  translated;
- that rendering writes nothing to any table, in any mode.

Out of scope, with reasons in §7: the schema (nothing changes), Google (no call
is made), audit (no row is written), antiforgery (no state-changing request
exists).

## 2. Test levels

| Level | Use here |
|---|---|
| Integration over HTTP (`WebApplicationFactory`, real PostgreSQL via Testcontainers, TC-2) | the bulk. The page is served by the real host, through the real authorization pipeline, from the real migrated database. |
| Security (TC-5) | the allowed-role and forbidden-role halves, the anonymous visitor, the SC-4 enumeration, method restriction, and the absence assertions of SC-7. |
| Localization (TC-8) | every key in both files, English rendering, data untranslated. |
| Unit with substituted ports (TC-1) | **one scenario only**, and it cannot be written at this stage — see §8. |

No test calls a live Google API or a real Control Plane (TC-4): the Control
Plane channel is the US-008 scripted handler answering the Admin login check
"allowed", and this Story adds no Google port to substitute.

## 3. Fixtures

Reused unchanged, so this Story adds no host plumbing:

- `InstallationTestHost` — the installation host over an isolated database;
- `ReadOnlyModeHost.Cause` — the three read-only causes of BR-025 plus normal
  operation, seeded into `legitimacy_state`;
- `WorkspaceConnectionHostExtensions.StartSignedInAsync` — a signed-in Admin
  against an approving Control Plane channel;
- `InstallationConfigurationKeys` — the synthetic OAuth client id and client
  secret, which the absence assertions compare against.

Added by this Story:

- `ConnectionInstructionTestData` — the path, the policy name, the six scope
  URIs in their fixed order, the forbidden scope fragments, the forbidden
  Workspace role names, and the eighteen translation keys;
- `ConnectionInstructionHostExtensions` — `OpenInstructionAsync`,
  `HandedOverText` (the content of `<pre id="connection-instruction">`),
  `ScriptSources` / `InlineScripts`, and `TableRowCountsAsync`.

### 3.1 Names this stage fixes

The Specification fixes that translation keys exist, not how they are spelled,
so — as US-009 established — TEST_WRITING fixes the spellings and IMPLEMENTATION
must honour them or correct them deliberately:

- the **eighteen keys** listed in `ConnectionInstructionTestData.TextKeys`, one
  per paragraph or statement (spec FR-012, I-8), including
  `Landing.Settings.ConnectionInstruction` for the navigation entry;
- **`<pre id="connection-instruction">`** as the element holding the text the
  Admin hands over. A `pre` because the text is copied verbatim and must keep its
  line breaks, and because it cannot nest, so a test can read exactly what a
  person would paste;
- **`id="copy-instruction"`** for the copy control;
- the copy script is served from **`/js/`**.

The path (`/settings/connection-instruction`) and the policy name
(`ViewConnectionInstruction`) are **not** this stage's choices — the api-design
fixes both.

### 3.2 One structural expectation worth stating

The instruction is rendered **once**, inside the `pre` the Admin copies, which is
also the visible text (OD-002: the text stays selectable and the affordance only
copies it). Several assertions depend on it — "exactly six scopes, in order"
counts occurrences across the page. A page that printed the instruction twice,
once for reading and once for copying, fails them. That is deliberate: two copies
can disagree, and the one that gets pasted would be the one nobody proof-read.

## 4. Positive scenarios

| # | Scenario | AC |
|---|---|---|
| P-1 | A signed-in Admin opens the instruction and sees this school's client ID, with its label | AC-002 |
| P-2 | The page states that the client ID belongs to this school alone | AC-002 |
| P-3 | The school's domain is shown with its label | AC-002 |
| P-4 | All six scopes are rendered, as full URIs | AC-003 |
| P-5 | The rendered scope list is exactly the six, in the contract's order | AC-003 |
| P-6 | Each of the five technical-account statements is on the page | AC-004 |
| P-7 | The two super-admin actions are stated | AC-004 |
| P-8 | The client ID rotated between two renders appears on the second | AC-006 |
| P-9 | The handed-over text carries the client ID, the domain, every scope and the technical-account requirements | AC-009 |
| P-10 | The copy affordance is present with its translated label | AC-009 |
| P-11 | The instruction renders in English for an English account | AC-010 |
| P-12 | The settings section carries this entry next to US-009's | AC-001 |

## 5. Negative and boundary scenarios

| # | Scenario | Expected | AC |
|---|---|---|---|
| N-1 | A Dean principal against the policy | refused | AC-001 |
| N-2 | An anonymous principal against the policy | refused | AC-001 |
| N-3 | An anonymous visitor requests the page | `302` to `/sign-in`, and no part of the instruction in the body | AC-001 |
| N-4 | A `POST` to the path | not `200`, not a redirect | AC-009 |
| N-5 | `…/connection-instruction.txt`, `.pdf`, `/download`, `/export` | `404` — no download route exists | AC-009 |
| N-6 | A forbidden scope fragment anywhere on the page | absent | AC-003 |
| N-7 | A Workspace admin-role name in either translation file, or on the page | absent | AC-004, VR-005 |
| N-8 | The OAuth web client id in the response | absent | AC-002, FR-006 |
| N-9 | The `Installation` UUID in the response | absent | AC-002, FR-006 |
| N-10 | The OAuth client secret or its reference, anywhere | absent | AC-009, S-03 |
| N-11 | A session cookie name or antiforgery token inside the handed-over text | absent | AC-009 |
| N-12 | The signed-in Admin's own address on the page | absent — no personal data | S-12 |
| N-13 | The saved technical account (US-009) on this page | absent — the instruction invents no address | AC-004 |
| N-14 | An inline `<script>` with a body | none — the affordance is a static file | AC-009, I-5 |
| B-1 | No legitimacy check has ever succeeded | `200`, the not-confirmed statement, no client ID, no domain, and the school-independent parts complete | AC-008 |
| B-2 | An `upgrade_required` answer recorded a domain and client ID without a successful check | treated as unknown | AC-005, I-1 |
| B-3 | Each of the three read-only causes | `200` with the reason, instruction complete, never `409` | AC-005 |

## 6. Validation, security and persistence scenarios

**Validation (VR-001 … VR-005).** There is no user input, so validation is
mostly about what the page must ignore and how it renders data:

- a query string (`?clientId=999&domain=attacker.example.test`) changes neither
  the answer nor the handed-over text, and writes nothing (VR-001);
- a value counts as known only from a check that actually succeeded (VR-002,
  VR-003, proven by B-1 and B-2);
- the client ID is rendered **exactly as stored** — no grouping separator
  variant of the digits appears anywhere (VR-004, db-design §2.2). This is the
  assertion that keeps the value pasteable into the Google console;
- no Workspace admin-role name appears in either translation file (VR-005).

**Security (S-01 … S-12).** The TC-5 checklist, minus what this Story does not
have: the policy admits an Admin and refuses a Dean and an anonymous principal;
the endpoint declares a policy and allows no anonymous access; the SC-4 closed
list gains nothing; the path accepts only `GET`, so no antiforgery scenario
exists; no secret reaches the response; no Google port exists to receive a call.

**Persistence.** One assertion, applied four ways: `TableRowCountsAsync` before
and after, in normal mode, in each read-only cause, and with no
`legitimacy_state` row at all. Nothing is added to any table and the audit table
is unchanged (spec FR-010, AC-007). The migration list is deliberately **not**
re-asserted here — `AppUserMigrationTests` already pins it at three migrations
and five tables (db-design §6), and this Story must leave that test untouched and
passing rather than add a second copy of it.

## 7. Excluded scenarios, with justification

| Excluded | Why |
|---|---|
| Schema tests | no table, column, constraint or migration changes (spec FR-015, db-design). The existing `AppUserMigrationTests` is the guard. |
| Audit-row tests | no audited action is introduced (spec I-6). "No row is written" is covered by the persistence assertion instead. |
| Antiforgery tests | no state-changing request exists; antiforgery never validates a `GET` (SC-4, v64). |
| Google port tests | this Story adds no Google port, requests no scope at runtime and makes no call (spec S-05). US-011 brings the first real one and inherits the US-007 finding F-5. |
| Read-only refusal tests in `Application` | nothing is written, so no guard is invoked. What must be proven is the opposite — that the page is **served** in read-only mode — and B-3 does that. |
| `400` / `409` contract tests | the contract documents neither (api-design §2.1); N-4 proves the path takes no `POST`. |
| Control Plane tests | the Control Plane gains no endpoint and is not called while rendering. |
| A test that the page is printable | browser behaviour, outside the reach of an HTTP test. The relevant part — that the text is in the markup — is P-9. |
| Time-zone boundary tests (TC-8) | no message on this page depends on a date or period (spec I-10). |

## 8. Known limitations

1. **The fourth state combination cannot be tested at this stage.** DB_DESIGN
   §2.1 established that `InstallationNotConfirmed` with `readOnly = false` —
   a stored row whose domain or client ID is empty — **cannot be produced in
   PostgreSQL**: `ck_legitimacy_state_domain_length` requires at least three
   characters, `ck_legitimacy_state_client_id_format` requires 10–32 digits, and
   neither column is nullable. It must therefore be proven by a unit test with
   `ILegitimacyStateRepository` substituted.

   That test cannot be written now: it must name the query type the
   Specification describes (spec FR-002), and no production type exists yet, so a
   test referencing it would not compile and would break the whole suite. This is
   the same limitation US-009 recorded for its `DomainNotConfirmed` branch, which
   IMPLEMENTATION then satisfied (US-009 finding D-2).

   **IMPLEMENTATION must add it**, in the shape of
   `SaveWorkspaceConnectionBranchTests`: an in-memory repository returning a
   state whose client ID is empty, asserting the state is
   `InstallationNotConfirmed` and that the page does not fall over. The branch
   must exist in the code even though the database prevents it — a constraint is
   a last line of defence, never the control.

2. **The forbidden-role half is a synthetic Dean principal**, not a signed-in
   Dean, because no Dean can sign in until US-012. The endpoint is separately
   shown to declare the policy and to allow no anonymous access, so the HTTP path
   is covered by construction. This is US-009 finding F-1, carried forward
   unchanged; US-012 must add the HTTP-level forbidden-role test for both
   settings pages.

3. **"No Workspace admin-role name" is tested against a list of role
   vocabulary**, not against one guessed answer (`Super Admin`, `Groups Admin`,
   `Reports Admin`, …). A role name outside that list would slip through. The
   list covers the names a helpful change would plausibly reach for, and the
   translation-file assertion makes the check cheap to extend when
   `trebovaniya.md` §7 item 10 closes.

4. **The copy itself is not executed.** No test drives a clipboard: what is
   proven is that the text to copy is complete in the markup, that the control is
   present, that the script is an external file which is really served, and that
   no inline script exists. Whether the browser's clipboard API succeeds is
   outside an HTTP test, and OD-002 requires the page to work without it anyway.

5. **`AnonymousVisitor_SeesNoPartOfTheInstruction`** asserts absence in a `302`
   body. It would also pass if the redirect body were empty for an unrelated
   reason; it is kept because the failure it guards against — a page rendered
   before the challenge — is the one that matters.

## 9. Open Decisions affecting testing

None open. OD-001 and OD-002 arrived resolved, and both shaped tests rather than
blocking them:

- **OD-001** produced N-7, the assertion that no Workspace admin-role name is
  printed. Without the resolution this stage would have had to guess a role list,
  which is exactly what the Owner declined.
- **OD-002** produced N-4, N-5 and N-14: no `POST`, no download route, no inline
  script.

`trebovaniya.md` §7 item 10 stays open and is not needed by any test: the
assertions are about the **absence** of a role name, which is what the resolution
requires while the item is open.
