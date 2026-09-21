---
artifact_type: test_generation_report
story: US-010
version: 1
status: DRAFT
created_at: 2026-09-21T00:00:00Z
updated_at: 2026-09-21T00:00:00Z
produced_by: test-writer
inputs:
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
  - path: docs/tests/US-010-test-strategy.md
    version: 1
  - path: docs/tests/US-010-ac-test-matrix.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
---

# US-010 Test Generation Report

## 1. Story

US-010 — Connection instructions for the school super-admin. Stage
`TEST_WRITING`, run before any production code exists (TC-1).

## 2. Files created

Test infrastructure (2 files):

| File | Contents |
|---|---|
| `tests/ClassroomAgent.Tests/TestInfrastructure/ConnectionInstructionTestData.cs` | the path, the policy name, the six scope URIs in fixed order, the forbidden scope fragments, the nine forbidden Workspace role names, the eighteen translation keys, and the element ids |
| `tests/ClassroomAgent.Tests/TestInfrastructure/ConnectionInstructionHostExtensions.cs` | `OpenInstructionAsync`, `HandedOverText`, `HasElement`, `ScriptSources`, `InlineScripts`, `TableRowCountsAsync` |

Test classes (6 files, 56 test methods, 133 cases):

| File | Methods | Cases |
|---|---|---|
| `Web/Pages/ConnectionInstructionPageTests.cs` | 16 | 22 |
| `Web/Pages/ConnectionInstructionScopeTests.cs` | 6 | 15 |
| `Web/Pages/ConnectionInstructionTechnicalAccountTests.cs` | 7 | 27 |
| `Web/Pages/ConnectionInstructionHandoverTests.cs` | 11 | 14 |
| `Web/Security/ConnectionInstructionAuthorizationTests.cs` | 11 | 13 |
| `Web/Localization/ConnectionInstructionTranslationTests.cs` | 6 | 42 |

## 3. Files modified

**None.** No production source file was created or changed, and no existing test
was modified, disabled or deleted. This Story needed no compile-only skeleton and
therefore no Open Decision for one — unlike US-005 (OD-002) and US-007 (OD-003) —
because everything provable at this stage is driven through the running host over
HTTP, the real migrated database and the real authorization policy object.

## 4. Commands

| Command | Result |
|---|---|
| `dotnet build ClassroomAgent.sln` | **0 errors, 0 warnings** (`TreatWarningsAsErrors`) |
| `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~ConnectionInstruction"` | the red-phase run of §5 |
| `dotnet test ClassroomAgent.sln` | the whole-suite regression run of §6 |

Integration tests ran against real PostgreSQL in Testcontainers (TC-2); the
Docker daemon was available and no test fell back to another provider.

## 5. Red-phase verification

Three runs were needed, and the first two are the substance of this report.

| Run | Total | Failed | Passed | Verdict |
|---|---|---|---|---|
| 1 — as first written | 133 | 102 | **31** | 31 passes before any implementation: investigated, not accepted |
| 2 — after strengthening the absence assertions | 133 | 128 | **5** | 5 remained; investigated again |
| 3 — after strengthening the persistence assertions | 133 | **133** | 0 | clean red phase |

### 5.1 Why 31 tests passed at first, and what was wrong with them

Every one of them asserted an **absence** — no forbidden scope, no secret, no
Workspace role name, no download route, no personal data — and an absence is
trivially true of a path that answers `404`. They would have gone green for the
wrong reason and then never failed again, which is the failure mode the red phase
exists to catch.

The fix was not to delete them but to give each one a precondition that the page
is really being served: `Assert.Equal(HttpStatusCode.OK, page.Status)` before the
absence, `Assert.NotEmpty(ScopeUris(...))` before asserting a forbidden scope is
missing from the list, `Assert.Equal(6, rendered.Count)` before an `Assert.All`
that would hold vacuously over an empty list, the challenge status before
asserting an anonymous visitor sees nothing, and — in
`TheAnonymousList_GainsNothing` — an assertion that the endpoint **exists** at
all, since "not on the anonymous list" is also true of an endpoint nobody wrote.

**No assertion was weakened.** Eleven methods became strictly stronger.

### 5.2 Why 5 more passed after that, and what was wrong with them

The three AC-007 "writes nothing" methods —
`OpeningThePage_WritesNothingAtAll`, `OpeningThePageWithNoLegitimacyState_WritesNothing`
and `OpeningThePageInReadOnlyMode_WritesNothing` (3 cases) — compared table row
counts before and after. A `404` writes nothing either, so they passed while
proving nothing. Each now asserts the page was served with `200` first.

### 5.3 Every failure in run 3 names missing production behaviour

| Failures | Message | Missing behaviour |
|---|---|---|
| 15 | `Assert.Equal() Failure: Values differ` (expected `OK`, actual `NotFound`) | the endpoint does not exist |
| 42 | `Translation key '…' is missing for 'uk'/'en'` | the eighteen translation entries |
| 6 | `The page carries no <pre id="connection-instruction">` | the instruction element |
| 6 | `Assert.Contains() Failure: Sub-string not found` | the rendered statements |
| 3 | `No policy found: ViewConnectionInstruction` | the authorization policy |
| 3 | `Assert.Equal() Failure: Collections differ` | the scope list |
| 3 | `Assert.NotEmpty() Failure: Collection was empty` | the scope list |
| 1 | `The page carries no copy-to-clipboard control` | the affordance |
| 1 | `Assert.Single() Failure: no matching items` | the `/js/` script reference |
| 1 | `Assert.NotNull()` / `Assert.Contains() Failure: Item not found in collection` | the endpoint and the policy object |

No failure is a syntax error, an invalid import, a missing test dependency, a
broken host configuration, an invalid fixture or a contradiction with an approved
artifact. Every message names a thing IMPLEMENTATION must build.

## 6. Existing tests

The whole suite was re-run. The result is recorded in §7 below; the requirement
is that the 1651 cases of US-001 … US-009 stay green and that
`AppUserMigrationTests` — which pins the installation database at **three**
migrations and **five** tables (db-design §6) — is untouched and passing.

Both hold. `1651` passed is exactly the prior baseline, no failure line in the
run names a test outside the new `ConnectionInstruction*` classes, and
`AppUserMigrationTests` appears nowhere among the failures: this Story changed no
schema, so the list it pins is unchanged.

## 7. Whole-suite result

`dotnet test ClassroomAgent.sln`:

- **total 1784** — the 1651 of US-001 … US-009 plus the 133 new US-010 cases;
- **passed 1651** — every prior-Story case, with no regression;
- **failed 133** — exactly the new US-010 cases, all for missing production
  behaviour (§5.3);
- **skipped 0** — no test is skipped, ignored or commented out.

No prior-Story test needed correcting. This differs from US-009, which had to
correct three prior-Story tests whose closed lists it legitimately grew; US-010
grows no list, adds no table and adds no migration, so nothing existing had to
change.

## 8. Untested Acceptance Criteria

None untested. **One scenario of AC-008 is deferred**, and it is deferred for a
compile constraint, not for lack of coverage:

> A stored row whose domain or client ID is empty — the combination
> `InstallationNotConfirmed` with `readOnly = false` — cannot be produced in
> PostgreSQL. DB_DESIGN §2.1 proved it from the constraints:
> `ck_legitimacy_state_domain_length` requires at least three characters,
> `ck_legitimacy_state_client_id_format` requires 10–32 digits, and neither column
> is nullable. It therefore needs a unit test with `ILegitimacyStateRepository`
> substituted — and such a test must name the query type spec FR-002 describes,
> which does not exist yet, so writing it now would break compilation of the whole
> test project.

**IMPLEMENTATION owes that test**, in the shape of the existing
`SaveWorkspaceConnectionBranchTests`: an in-memory repository returning a state
whose client ID is empty, asserting the state is `InstallationNotConfirmed` and
that the page does not fall over. This is the same limitation US-009 recorded and
then satisfied as its finding D-2. The branch must exist in the code although the
database prevents reaching it: a constraint is a last line of defence, never the
control.

## 9. Names this stage fixed, which IMPLEMENTATION must honour

The Specification fixes that translation keys exist, not how they are spelled, so
these are this stage's choices (US-009 set the precedent):

- the **eighteen keys** of `ConnectionInstructionTestData.TextKeys`, one per
  paragraph or statement (spec FR-012, I-8), including
  `Landing.Settings.ConnectionInstruction`;
- **`<pre id="connection-instruction">`** as the element holding the handed-over
  text — a `pre` because the text is copied verbatim, must keep its line breaks,
  and cannot nest, so a test reads exactly what a person would paste;
- **`id="copy-instruction"`** for the copy control;
- the copy script is served from **`/js/`**;
- the instruction is rendered **once**, inside that `pre`, which is also the
  visible text. Several assertions count occurrences across the page, so a version
  that printed the instruction twice — once to read, once to copy — fails. That is
  deliberate: two copies can disagree, and the one that gets pasted is the one
  nobody proof-read.

The path `/settings/connection-instruction` and the policy name
`ViewConnectionInstruction` are **not** this stage's choices — the api-design fixes
both.

## 10. Open Decisions

None open, and none was raised by this stage. OD-001 and OD-002 arrived resolved
and both produced tests rather than blocking them:

- **OD-001** produced the strongest negative assertions in the Story: no Google
  Workspace admin-role name appears in either translation file or on the page,
  checked against nine role names. Without the resolution this stage would have
  had to guess a role list — exactly what the Owner declined.
- **OD-002** produced the absence of a `POST`, the absence of a download route
  (`.txt`, `.pdf`, `/download`, `/export` all `404`), and the absence of inline
  script.

`trebovaniya.md` §7 item 10 stays open and no test needs it: the assertions are
about the **absence** of a role name, which is what the resolution requires while
the item is open.

## 11. Overall result

**PASS.** 56 test methods in 6 classes, 133 cases, 0 build errors and 0 warnings;
red phase verified on real PostgreSQL after two rounds of strengthening that
removed every vacuous pass; the whole suite shows no regression in the 1651 cases
of US-001 … US-009; all ten Acceptance Criteria are mapped, with one scenario of
AC-008 explicitly owed by IMPLEMENTATION.
