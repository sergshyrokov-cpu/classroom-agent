---
artifact_type: implementation_report
story: US-010
version: 1
status: DRAFT
created_at: 2026-09-21T00:40:00Z
updated_at: 2026-09-21T00:40:00Z
produced_by: dotnet-implementor
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
  - path: docs/tests/US-010-test-strategy.md
    version: 1
  - path: docs/tests/US-010-ac-test-matrix.md
    version: 1
  - path: docs/evidence/US-010-test-generation-report.md
    version: 1
  - path: trebovaniya.md
    version: 79
supersedes: null
tests_status: PASS
build_status: PASS
format_status: PASS
security_sensitive: true
---

# US-010 Implementation Report — Connection instructions for the school super-admin

## 1. Summary

The admin panel now generates the text the school's super-admin carries out once
in the school's own Google console: this `Installation`'s service-account client
ID and domain from `LegitimacyState`, the six read-only scopes of
`trebovaniya.md` §6, and what the technical account must be. One `GET`, Admin
only, handed over from the page with a copy-to-clipboard affordance.

Status: **complete**. All ten Acceptance Criteria are implemented and green.

Validation: build 0 errors / 0 warnings; whole suite **1791 passed, 0 failed, 0
skipped**; `dotnet format --verify-no-changes` exit 0.

What the Story deliberately did **not** do, each verified rather than assumed:

- **no migration** and no schema change — the installation database keeps its
  three migrations and five tables, and `AppUserMigrationTests` passes untouched;
- **no Google port, no scope requested at runtime, no Google call** in any mode;
- **no audit row and no write of any kind** — `PermittedServiceWrite` keeps
  exactly its four members and the BR-026 closed list gains none;
- **no NuGet package**, no new project reference, no new namespace outside
  `package-map.md`;
- **no `POST`, no download endpoint**, so the SC-4 anonymous list and the
  antiforgery exemption list are both unchanged.

One deviation from a strictly additive change set is recorded in §7 (D-1): a
four-line method of US-009 now delegates to a shared rule, because spec I-1
requires the "known value" rule to be reused rather than restated.

## 2. Source Artifacts

| Artifact | Path | Version |
|---|---|---|
| Story | `docs/stories/US-010-connection-instructions.md` | authored |
| Specification | `docs/specifications/US-010-spec.md` | 1 (APPROVED) |
| Open Decisions | `docs/decisions/US-010-open-decisions.md` | 1 (both RESOLVED) |
| API design | `docs/designs/api/US-010-api-design.md` | 1 |
| OpenAPI | `docs/designs/api/US-010-openapi.yaml` | 1 |
| Database design | `docs/designs/database/US-010-db-design.md` | 1 (NOT_APPLICABLE) |
| Test strategy | `docs/tests/US-010-test-strategy.md` | 1 |
| AC → test matrix | `docs/tests/US-010-ac-test-matrix.md` | 1 |
| Test generation report | `docs/evidence/US-010-test-generation-report.md` | 1 |
| Requirements | `trebovaniya.md` | 79 |

No input is `SUPERSEDED`. `HUMAN_SPEC_APPROVAL` is recorded
(2026-09-20T19:54:41Z). There is deliberately no `entity_model`: DB_DESIGN
returned `NOT_APPLICABLE` and its §3 carries the column-to-DTO trace, which is
what was read in its place.

## 3. Implemented Acceptance Criteria

| AC | Implementation | Test | Status |
|---|---|---|---|
| AC-001 Only an Admin sees it | `InstallationPolicies.ViewConnectionInstruction`; registered in `InstallationSecurityServices`; `[Authorize]` on `ConnectionInstructionController` | `ConnectionInstructionAuthorizationTests` (11 methods), `ConnectionInstructionPageTests.TheLandingPage_LinksToTheInstruction`, `.TheSettingsSection_CarriesBothEntries` | PASS |
| AC-002 This school's own client ID | `GetConnectionInstructionQuery.ExecuteAsync`; `ConnectionInstructionView`; `Views/ConnectionInstruction/Index.cshtml` | `ConnectionInstructionPageTests.ThePage_ShowsThisSchoolsServiceAccountClientId`, `.ThePage_SaysTheClientIdBelongsToThisSchoolAlone`, `.ThePage_ShowsTheSchoolsDomain`, `.ThePage_DoesNotShowTheOAuthWebClientId`, `.ThePage_DoesNotShowTheInstallationIdentifier` | PASS |
| AC-003 The fixed scope list | `Domain.Rules.GoogleDelegationScopes` | `ConnectionInstructionScopeTests` (6 methods, 15 cases) | PASS |
| AC-004 What the technical account must be | `ConnectionInstructionTextKeys.TechnicalAccountStatements`; the `resx` entries; the view's statement loop | `ConnectionInstructionTechnicalAccountTests` (7 methods, 27 cases) | PASS |
| AC-005 Readable when nothing else works | `ConnectionInstructionController.Index` (no guard, no Google, no Control Plane); the read-only notice in the view | `ConnectionInstructionPageTests.InReadOnlyMode_*` (3 methods, 7 cases), `ConnectionInstructionAuthorizationTests.InReadOnlyMode_TheAnswerIsNeverAConflict` | PASS |
| AC-006 A rotated client ID appears | `GetConnectionInstructionQuery` reads `LegitimacyState` per request; nothing cached | `ConnectionInstructionPageTests.WhenTheClientIdIsRotated_TheNextRenderShowsTheNewOne` | PASS |
| AC-007 Reading changes nothing | `GetForReadAsync` only; no repository `Add`, no unit of work in the call path | `ConnectionInstructionPageTests.OpeningThePage*` (3 methods, 5 cases), `ConnectionInstructionBranchTests.TheQuery_WritesNothing` | PASS |
| AC-008 Before the first successful check | `ConfirmedLegitimacy`; `ConnectionInstructionState.InstallationNotConfirmed`; the view's `?? notConfirmed` fallback | `ConnectionInstructionPageTests.WithNoSuccessfulLegitimacyCheck_*`, `ConnectionInstructionBranchTests` (7 methods — **the DEFERRED row, now closed**) | PASS |
| AC-009 Ready to hand over | `<pre id="connection-instruction">` in the view; `wwwroot/js/copy-instruction.js`; no `POST`, no download route | `ConnectionInstructionHandoverTests` (11 methods, 14 cases) | PASS |
| AC-010 Every string translated | 18 keys in `SharedResource.uk.resx` and `SharedResource.en.resx`; `ConnectionInstructionTextKeys` | `ConnectionInstructionTranslationTests` (6 methods, 42 cases) | PASS |

**The DEFERRED row of the ac-test-matrix is closed.** TEST_WRITING could not
write the unit test for `InstallationNotConfirmed` with `readOnly = false` — it
must name `GetConnectionInstructionQuery`, which did not exist then, so the test
would not have compiled. `ConnectionInstructionBranchTests` adds it: a stored
state whose domain or client ID is empty is reported as not confirmed, and the DTO
carries `null` rather than an empty string, so the view's fallback engages instead
of rendering a blank value. The branch exists although
`ck_legitimacy_state_client_id_format` and `ck_legitimacy_state_domain_length`
prevent reaching it through the database — a constraint is a last line of defence,
never the control (db-design §2.1).

## 4. Change Set

### Created — production (10 files)

| File | Trace |
|---|---|
| `src/ClassroomAgent.Domain/Rules/GoogleDelegationScopes.cs` | FR-004, AC-003; spec I-2 places it in `Domain`, the only project both `Application` and `Infrastructure` reference, so US-011 needs no second copy |
| `src/ClassroomAgent.Application/UseCases/ConfirmedLegitimacy.cs` | FR-003, spec I-1 — the "known value" rule, shared with US-009 (see D-1) |
| `src/ClassroomAgent.Application/Models/ConnectionInstructionState.cs` | FR-002; openapi `ConnectionInstructionState` |
| `src/ClassroomAgent.Application/Models/Dtos/ConnectionInstructionView.cs` | FR-002; openapi `ConnectionInstructionPageModel` |
| `src/ClassroomAgent.Application/UseCases/GetConnectionInstructionQuery.cs` | FR-001, FR-002, FR-003, FR-010 |
| `src/ClassroomAgent.Web/Security/ConnectionInstructionTextKeys.cs` | FR-012, I-8 — the key spellings TEST_WRITING fixed |
| `src/ClassroomAgent.Web/Controllers/ConnectionInstructionController.cs` | FR-007, FR-011; openapi `getConnectionInstruction` |
| `src/ClassroomAgent.Web/Controllers/ConnectionInstructionPageModel.cs` | FR-007; AD-8 |
| `src/ClassroomAgent.Web/Views/ConnectionInstruction/Index.cshtml` | FR-001, FR-007, FR-008, FR-009 |
| `src/ClassroomAgent.Web/wwwroot/js/copy-instruction.js` | FR-008, OD-002, spec I-5 |

### Modified — production (8 files)

| File | Trace |
|---|---|
| `src/ClassroomAgent.Application/Authorization/InstallationPolicies.cs` | FR-011 — the §2 matrix cell, as its own constant (spec I-7) |
| `src/ClassroomAgent.Application/UseCases/GetWorkspaceConnectionQuery.cs` | spec I-1 (see D-1) |
| `src/ClassroomAgent.Application/Localization/SharedResource.uk.resx` | FR-012, AC-010 — 18 keys |
| `src/ClassroomAgent.Application/Localization/SharedResource.en.resx` | FR-012, AC-010 — the same 18 keys |
| `src/ClassroomAgent.Web/Security/SignInRoutes.cs` | the openapi path `/settings/connection-instruction` |
| `src/ClassroomAgent.Web/Security/InstallationSecurityServices.cs` | FR-011 — policy registration (supporting change) |
| `src/ClassroomAgent.Web/Configuration/InstallationServices.cs` | FR-016 — DI registration (supporting change) |
| `src/ClassroomAgent.Web/Views/Home/Index.cshtml` | FR-013 — the settings section's second entry |

### Created — tests (9 files)

Eight were written by TEST_WRITING and are unchanged by this stage:
`ConnectionInstructionTestData.cs`, `ConnectionInstructionHostExtensions.cs`,
`ConnectionInstructionPageTests.cs`, `ConnectionInstructionScopeTests.cs`,
`ConnectionInstructionTechnicalAccountTests.cs`,
`ConnectionInstructionHandoverTests.cs`,
`ConnectionInstructionAuthorizationTests.cs`,
`ConnectionInstructionTranslationTests.cs`.

Added by this stage:
`tests/ClassroomAgent.Tests/Application/UseCases/ConnectionInstructionBranchTests.cs`
— the row the `ac_test_matrix` marks DEFERRED for AC-008.

### Not in the change set

- **No migration.** No file under
  `src/ClassroomAgent.Infrastructure/Persistence/Migrations/` was added or
  changed; the model snapshot is untouched. FR-015 and db-design §6 make a fourth
  migration a defect.
- **No test was modified, weakened, disabled or deleted** — unlike US-009, which
  legitimately had to grow three closed lists. This Story grows none.
- No secret, no generated database file, no `.xlsx`, no IDE-local config.
- No prototype file was touched.

## 5. Validation Evidence

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build ClassroomAgent.sln` | **0 errors, 0 warnings** (`TreatWarningsAsErrors`) |
| Story tests | `dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~ConnectionInstruction"` | **140 / 140 passed**, 0 failed, 0 skipped |
| Whole suite | `dotnet test ClassroomAgent.sln` | **1791 total, 1791 passed, 0 failed, 0 skipped** |
| Format | `dotnet format --verify-no-changes` | exit **0**, no output |

Progress across the stage: **133 red → 0** on the first run after the production
code was in place, then **+7** cases from the branch tests this stage added. The
count grew 1784 → 1791 and no prior-Story case changed state.

Integration tests ran against real PostgreSQL in Testcontainers (TC-2). No test
called a live Google API or a real Control Plane (TC-4); this Story adds no Google
port to substitute.

## 6. Configuration Changes

**None.** No `appsettings*.json` change, no new setting, no environment variable.
The page needs nothing configurable: its two values come from `LegitimacyState`
and its scope list from code.

## 7. Deviations and Discovered Problems

### D-1 A US-009 method now delegates to a shared rule (the only non-additive change)

Spec **I-1** requires this Story to use the *same* "known value" rule as the
connection settings — "the existing rule is reused rather than restated, so an
`upgrade_required` answer cannot make an instruction look complete in one screen
and incomplete in another".

Taken literally that leaves two options: call `GetWorkspaceConnectionQuery` from
the new query, which would add a third database read per request and contradict
db-design §4 ("two reads per request"); or lift the predicate into one place both
call. The second was chosen:

- new `Application.UseCases.ConfirmedLegitimacy` holds the rule —
  `DomainOf(state)` and `ClientIdOf(state)`, each null unless a check has actually
  succeeded and the value is non-empty;
- `GetWorkspaceConnectionQuery.KnownDomainAsync` now delegates to it instead of
  repeating the predicate. Its behaviour is unchanged, which the US-009 tests
  confirm: all of them pass untouched.

This is a change to a file outside the additive set, so it is recorded here rather
than left to a reviewer to notice. It traces to spec I-1 and to nothing else; no
other US-009 behaviour was altered.

### D-2 The instruction is one `<pre>`, which is both the visible text and the copy

TEST_WRITING fixed `<pre id="connection-instruction">` and required the
instruction to be rendered **once**. The view therefore puts the whole handed-over
text — labels, both values, the two steps, the scope list and the five
technical-account statements — inside that element, and the copy button copies
that element's `textContent`. Nothing is duplicated for reading, so the visible
text and the copied text cannot disagree.

Consequence worth naming for SECURITY_REVIEW: because the copy is taken from the
DOM rather than assembled in script, the copied text can contain nothing the
server did not already render.

### D-3 The copy affordance removes itself when the clipboard is unavailable

OD-002 requires the affordance to "degrade safely" with the clipboard
unavailable. The script hides the button when `navigator.clipboard.writeText` is
missing and when a write is refused, rather than failing on a click or opening a
dialog. The text stays selectable either way, and with scripting off the page is
unchanged — which the tests assert by reading the instruction out of the markup
the server sent.

### D-4 No discovered conflict with an upstream artifact

Nothing in the Specification, the API design or the database design turned out to
be unimplementable. The api-design's request for DB_DESIGN to state the "no
schema change" conclusion in writing paid off here: there was no moment where a
migration looked necessary.

## 8. Open Decisions

**None open, and none newly required.**

- **OD-001** (RESOLVED, option 1) is implemented as written: the instruction
  states the requirement — the technical account must be able to read Classroom
  and Admin Reports and hold nothing beyond that — and **prints no Workspace
  admin-role name**. Nine role names are asserted absent from both translation
  files and from the rendered page, so a later helpful addition fails a test
  instead of reaching a school. `trebovaniya.md` §7 item 10 stays open and this
  Story does not depend on it.
- **OD-002** (RESOLVED, option 1) is implemented as written: the instruction is
  handed over from the page, with copy-to-clipboard, and no download endpoint
  exists — `.txt`, `.pdf`, `/download` and `/export` all answer `404`.

No security-sensitive decision is missing. The Story touches authorization (a new
policy) and reads values that cross from the Owner to a school, which is why
`security_sensitive: true`; SECURITY_REVIEW is the next stage.
