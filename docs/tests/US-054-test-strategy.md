---
artifact_type: test_strategy
story: US-054
version: 1
status: DRAFT
created_at: 2026-10-24T21:00:00Z
updated_at: 2026-10-24T21:00:00Z
produced_by: test-writer
inputs:
  - path: docs/stories/US-054-technical-account-superadmin-texts.md
    version: null
  - path: docs/specifications/US-054-spec.md
    version: 1
  - path: docs/decisions/US-054-open-decisions.md
    version: 1
  - path: docs/designs/api/US-054-api-design.md
    version: 1
  - path: docs/designs/database/US-054-db-design.md
    version: 1
  - path: docs/reviews/specifications/US-054-spec-review.md
    version: 1
supersedes: null
---

# US-054 Test Strategy — The program's texts describe the technical account as a super-admin with blocked sign-in

## 1. Scope

Texts only (spec §1): the technical-account statements of the instruction (FR-001, FR-002), the
connection-page hint (FR-003), the `TechnicalAccountCannotRead` advice on the access check and the
"Last synchronization" block (FR-004), the copied instruction (FR-005), and the tests and comments
that pinned the pre-v97 meaning (FR-006). No endpoint, schema, scope, access-check step or
authorization rule changes, so no contract, persistence or authorization scenario is new.

## 2. Test levels

All scenarios are **integration** tests through the real installation host over PostgreSQL
(Testcontainers, TC-2), signed in as an Admin, Google ports substituted (TC-4) — the same level the
US-009/010/011/017 tests of these pages use. Translation-file scenarios read the host's localizer
(`InstallationTestHost.Text` / `AllTexts`), the way the existing translation tests do.

No unit test: there is no branching logic to isolate; the observable behaviour is rendered text.

## 3. Scenarios

### 3.1 Positive

- Every new or reworded entry (10 keys × uk/en) equals the binding wording of spec §4.1 exactly —
  held as literals in `TechnicalAccountTextsTestData.Binding` (AC-001, AC-002, AC-006, AC-007).
- The handed-over `<pre>` text makes all ten technical-account statements in FR-001 order
  (1, 2, 3, 4, 4.1–4.5, 5), each rendered exactly once on the page — copy and page cannot differ
  (AC-002, AC-004, AC-005), in uk and en.
- The five measures sit one indentation level deeper than statements 1–5 in the handed-over text
  (AC-002, I-3), in uk and en.
- The connection page shows the v97 hint (AC-006), uk and en.
- The access check's MeetActivity step with `TechnicalAccountCannotRead` and the "Last synchronization"
  block for a run failed with that diagnosis show the v97 advice (AC-007), uk and en.
- Existing tests extended by data: `ConnectionInstructionTestData.TextKeys.All` and
  `TechnicalAccountStatements` now list the new keys, so the existing completeness, "really
  translated", ≤600-character, on-the-page and role-name guards cover them (AC-008, VR-001).

### 3.2 Negative

- `…ReadOnlyRoles` and `…NoWriteAccess` exist in neither resource file (AC-003, FR-002).
- None of the eight pre-v97 values (`NoPersonBehindIt`, `ReadOnlyRoles`, `NoWriteAccess`, hint;
  uk + en; spec I-5) appears on the instruction or the connection page (AC-003).
- The pre-v97 "read-only rights" advice appears on neither the access check nor the sync block
  (AC-007).
- No delegated Workspace admin-role name in any instruction or access-check entry, or on the
  rendered instruction (VR-002; existing guards with "Super Admin" dropped from the list, I-2).

### 3.3 Boundary / validation

No input is added (spec §6). The existing ≤600-character-per-entry guard (US-010 I-8) covers the
longest new entry (`Measure.TwoStepVerification`).

### 3.4 Security

No authorization change (spec §7): the existing allowed/forbidden-role tests of the instruction,
connection page and access check (US-010, US-009, US-011, US-052) stand unchanged. The instruction
still prints no technical-account address (existing
`ThePage_DoesNotInventATechnicalAccountAddress`, AC-004) and no personal data
(`ThePage_ShowsNoPersonalData`).

### 3.5 Persistence

None (DB_DESIGN NOT_APPLICABLE). The sync-block scenario seeds `sync_state` directly with the
existing helper.

## 4. Fixtures

- `TechnicalAccountTextsTestData` (new): binding wording, removed keys, retired values.
- `ConnectionInstructionTestData` (rewritten per FR-006/I-2): new keys, `Measures`, the ten-statement
  order, `ForbiddenRoleNames` without "Super Admin".
- Existing host helpers: `LastSynchronizationHostExtensions.StartAdminAsync` (Admin in a chosen
  language), `AccessCheckHostExtensions` (fake probe), `ConnectionInstructionHostExtensions.HandedOverText`.

## 5. Rewritten tests and comments (FR-006, Owner's m-1)

- `ConnectionInstructionTechnicalAccountTests`: class summary; `ThePage_SaysASuperAdminAccountIsNotAcceptable`
  replaced by `ThePage_SaysTheAccountGetsTheSuperAdminRoleWithSignInClosed`;
  `NoWorkspaceAdminRoleName_ReachesThePage` asserts `SuperAdminRole`; guard comments cite §9 v97.
- `ConnectionInstructionTranslationTests.TheInstruction_RendersInEnglishForAnEnglishAccount` asserts
  `SuperAdminRole` instead of the removed `ReadOnlyRoles`.
- Comments only: `WorkspaceConnectionTestData.TechnicalAccount` (line 19),
  `AccessCheckTranslationTests.NoMessage_NamesAWorkspaceAdminRole` (line 53),
  `GoogleClassroomReaderTests.TheTokenRequest_ImpersonatesTheTechnicalAccount` summary.
- Production comments (`ConnectionInstructionTextKeys`, `SaveWorkspaceConnectionRequest`) are
  IMPLEMENTATION's: TEST_WRITING does not edit production code beyond a skeleton.

## 6. Excluded scenarios

- Clipboard behaviour of `copy-instruction.js` — unchanged (FR-005); the copied text *is* the `<pre>`,
  already proven by the US-010 handover tests.
- VR-003 (no console URL/menu path) — a review check against §4.1 per the spec; the binding-wording
  test pins the exact text, which contains none.
- `trebovaniya.md` §7 item 29 — manual, on the pilot server (spec §12).

## 7. Known limitations

- The nesting test asserts relative indentation, not a bullet character; a nested list using the same
  indentation as its parent would fail, which is the intent.
- Exact-wording tests make any wording change a test change — deliberate: spec §4.1 is binding.

## 8. Open Decisions affecting testing

None. OD-001…OD-004 resolved (a); no skeleton needed — tests name keys through test data, not new
production declarations.
