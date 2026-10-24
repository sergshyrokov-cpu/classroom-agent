---
artifact_type: specification
story: US-054
version: 1
status: APPROVED
created_at: 2026-10-24T19:40:00Z
updated_at: 2026-10-24T20:20:00Z
produced_by: spec-writer
inputs:
  - path: docs/stories/US-054-technical-account-superadmin-texts.md
    version: null
  - path: trebovaniya.md
    version: 97
  - path: docs/decisions/US-054-open-decisions.md
    version: 1
supersedes: null
---

# US-054 Specification — The program's texts describe the technical account as a super-admin with blocked sign-in

## 1. Overview

`trebovaniya.md` v97 (§1 "Админ ≠ super-admin", §9 "Технический аккаунт", §7
item 10 closed) makes the school's technical account — the impersonation user in
`WorkspaceConnection` — a **super-admin** whose interactive sign-in is closed by
five mandatory measures. The program's user-visible texts still say the
opposite: the super-admin instruction (US-010) forbids a super-admin and asks
for read-only roles, the connection-page hint (US-009) says "not a super-admin …
read-only rights", and the `TechnicalAccountCannotRead` diagnosis (US-011,
US-017) tells the school to give the account read-only rights.

This Story changes **texts only**: translation entries in both language files,
the list of technical-account statements the instruction renders, and the
comments and tests that pin the old meaning. No endpoint, schema, scope,
access-check step, failure classification or authorization rule changes.

## 2. Business Goal

A school that follows the instruction sets up the one account configuration
that works (§9, verified on a live domain 2026-10-24) and closes sign-in under
it; the program's own diagnosis no longer sends the school back to a role that
sees no courses. Needed before pilot stage 5 (connecting the pilot school).

## 3. Business Flow

1. The Admin opens "Інструкція з підключення" (US-010) and copies it to the
   school's super-admin.
2. The super-admin authorises delegation (step 1, unchanged) and creates the
   technical account (step 2), following the technical-account requirements
   this Story rewrites: no person behind it, super-admin role with the reason,
   the five mandatory measures, the role granted last.
3. The Admin enters the account's address on the connection page (US-009),
   whose hint now matches v97, and — immediately after the super-admin grants
   the role — runs "Перевірити доступ" (US-011, OD-002 a).
4. If a Google read is refused because the account lacks the right
   (`TechnicalAccountCannotRead`), the access check and the "Last
   synchronization" block tell the Admin that the super-admin must give the
   account the super-admin role, as the instruction says.

## 4. Functional Requirements

### FR-001 Technical-account requirements of the instruction

The technical-account part of the instruction (under
`ConnectionInstruction.TechnicalAccount.Label`, after step 2) states, in this
order, one translation key per statement (US-010 I-8), in both languages:

| # | Key | Status | Meaning (§9 v97) |
|---|---|---|---|
| 1 | `ConnectionInstruction.TechnicalAccount.CreatedBySuperAdmin` | unchanged | the school's super-admin creates it at onboarding, together with the delegation |
| 2 | `ConnectionInstruction.TechnicalAccount.NoPersonBehindIt` | value reworded, key kept (I-1) | no person stands behind it, and nobody signs in to the program with it |
| 3 | `ConnectionInstruction.TechnicalAccount.SuperAdminRole` | new | it gets the super-admin role; lesser Classroom roles do not see the domain's courses; the program still only reads, because its access is bounded by the delegation scopes, not by the account's role |
| 4 | `ConnectionInstruction.TechnicalAccount.MeasuresLead` | new | sign-in under it is closed by the mandatory measures that follow |
| 4.1 | `ConnectionInstruction.TechnicalAccount.Measure.RandomPassword` | new | a random password that is stored nowhere |
| 4.2 | `ConnectionInstruction.TechnicalAccount.Measure.TwoStepVerification` | new | enforced 2-step verification with no enrolment grace period and no method enrolled — no backup codes, no recovery email, no recovery phone — so signing in is impossible |
| 4.3 | `ConnectionInstruction.TechnicalAccount.Measure.SeparateOrgUnit` | new | a separate organizational unit carrying these settings |
| 4.4 | `ConnectionInstruction.TechnicalAccount.Measure.SignInAlert` | new | an alert rule for any sign-in to the account, emailed to a domain administrator |
| 4.5 | `ConnectionInstruction.TechnicalAccount.Measure.RoleGrantedLast` | new | the super-admin role is granted last — after the other measures, immediately before the Admin runs "Перевірити доступ"; not earlier (OD-002 a) |
| 5 | `ConnectionInstruction.TechnicalAccount.EnteredInSettings` | unchanged | the Admin enters its address in the connection settings, in the school's domain |

The measures 4.1–4.5 are rendered as a nested list under statement 4 (one
level deeper than statements 1–5), inside the same `<pre id="connection-instruction">`
(I-3). No Google Admin console menu path, screenshot or URL is printed
(OD-004 a).

The binding wording is §4.1 below. Key spellings in this table are binding;
the two text-key classes (`ConnectionInstructionTextKeys` in `Web/Security`,
`ConnectionInstructionTestData.TextKeys` in tests) name them once each.

### FR-002 Statements removed

`ConnectionInstruction.TechnicalAccount.ReadOnlyRoles` and
`ConnectionInstruction.TechnicalAccount.NoWriteAccess` are removed from both
translation files, from `ConnectionInstructionTextKeys` and from the rendered
instruction. Their names no longer match any v97 statement, so they are not
reworded under the old name (Story scope).

### FR-003 Connection-page hint

`WorkspaceConnection.ImpersonationUser.Hint` (key kept — its name still
matches) says: the school's super-admin creates the account; it is not the
Admin's account; no person stands behind it and sign-in under it is blocked; it
holds the super-admin role, but the program only reads, bounded by the
delegation scopes. It lists no measures (decision 6 — they are in the
instruction).

### FR-004 "Cannot read" diagnosis

`AccessCheck.Outcome.TechnicalAccountCannotRead` (key kept) keeps its first
sentence (access is authorised, but the account has no right to this read) and
replaces the advice: the school's super-admin must give the technical account
the super-admin role, as the connection instruction says (OD-003 a). The same
entry is the access-check outcome and the synchronization diagnosis (§4 Epic 6
"Состояние синхронизации", v80: one problem, one name;
`Views/WorkspaceConnection/Index.cshtml` maps `SyncDiagnosis` to
`AccessCheck.Outcome.*`), so one change serves both.

### FR-005 Copied instruction

The copy affordance (US-010 OD-002) copies the `<pre>` itself, so the copied
text carries exactly the page's statements, in the same order and nesting. No
change to `copy-instruction.js`.

### FR-006 Comments and tests follow the new meaning

Supporting changes the Story cannot be correct without (Git Policy):

- the XML comments in `ConnectionInstructionTextKeys` that cite "not a
  super-admin", read-only roles or §7 item 10 as unverified are rewritten to
  §9 v97;
- the comment on `SaveWorkspaceConnectionRequest` (impersonation user "not a
  super-admin (BR-015)") and the summary of
  `GoogleClassroomReaderTests.TheTokenRequest_ImpersonatesTheTechnicalAccount`
  ("never a super-admin") are rewritten: the account is the school's technical
  account, no person; it is a super-admin with blocked sign-in (§9 v97). Comment
  text only — no code or assertion in those files changes (I-4);
- tests pinning the old meaning are rewritten (I-2):
  `ConnectionInstructionTechnicalAccountTests.ThePage_SaysASuperAdminAccountIsNotAcceptable`,
  `NoWorkspaceAdminRoleName_ReachesThePage` (asserts the removed `ReadOnlyRoles`),
  the class summary citing §7 item 10, and `ConnectionInstructionTestData`
  (`TechnicalAccountStatements`, `All`, `ForbiddenRoleNames`, comments).

### 4.1 Binding wording

Ukrainian and English are equivalent; neither is a literal gloss of the other.
Terms follow the existing entries ("super-admin", "domain-wide delegation",
"scope", "Адміністратор"/"Administrator", the button label of
`AccessCheck.Run.Button`).

| Key | uk | en |
|---|---|---|
| `…TechnicalAccount.NoPersonBehindIt` | за ним не стоїть людина, і в програму під ним ніхто не входить; | no person stands behind it, and nobody signs in to the program with it; |
| `…TechnicalAccount.SuperAdminRole` | він отримує роль super-admin: з меншими ролями Classroom курсів домену не видно, а програма однаково лише читає, бо її доступ обмежують scope у domain-wide delegation, а не роль акаунта; | it gets the super-admin role: lesser Classroom roles do not see the domain's courses, and the program still only reads, because its access is bounded by the scopes in domain-wide delegation, not by the account's role; |
| `…TechnicalAccount.MeasuresLead` | вхід під ним закривають обов'язкові заходи: | sign-in under it is closed by these mandatory measures: |
| `…Measure.RandomPassword` | випадковий пароль, який ніде не зберігається; | a random password that is stored nowhere; |
| `…Measure.TwoStepVerification` | примусова двоетапна перевірка без відстрочки реєстрації і без жодного зареєстрованого способу підтвердження — без резервних кодів, резервної пошти й телефону, тож увійти під акаунтом неможливо; | enforced 2-Step Verification with no enrolment grace period and no method enrolled — no backup codes, no recovery email, no recovery phone — so that signing in to the account is impossible; |
| `…Measure.SeparateOrgUnit` | окремий організаційний підрозділ, у якому діють ці правила; | a separate organizational unit that carries these settings; |
| `…Measure.SignInAlert` | правило сповіщень про будь-який вхід в акаунт; лист отримує адміністратор домену; | an alert rule for any sign-in to the account, emailed to a domain administrator; |
| `…Measure.RoleGrantedLast` | роль super-admin видається останньою — після всіх інших заходів, безпосередньо перед тим, як Адміністратор натисне «Перевірити доступ», і не раніше; | the super-admin role is granted last — after all the other measures, immediately before an Administrator clicks "Check access", and not earlier; |
| `WorkspaceConnection.ImpersonationUser.Hint` | Технічний акаунт створює super-admin школи. Це не ваш акаунт: за ним не стоїть людина, і вхід під ним заблоковано. Він має роль super-admin, але програма лише читає — її доступ обмежують scope у domain-wide delegation. | The technical account is created by the school's super-admin. It is not your own account: no person stands behind it, and sign-in under it is blocked. It holds the super-admin role, but the program only reads — its access is bounded by the scopes in domain-wide delegation. |
| `AccessCheck.Outcome.TechnicalAccountCannotRead` | доступ дозволено, але технічний обліковий запис не має прав на це читання. Super-admin школи має надати йому роль super-admin, як сказано в інструкції з підключення. | access is authorised, but the technical account has no right to this read. The school's super-admin must give it the super-admin role, as the connection instructions say. |

`CreatedBySuperAdmin`, `EnteredInSettings` and every other instruction entry
keep their current values. The Owner may correct wording at
`HUMAN_SPEC_APPROVAL`; meaning changes go through a new Specification version.

## 5. Acceptance Criteria

Story AC-001…AC-008, unchanged in meaning; tests reference these ids.

- **AC-001** The instruction, in uk and en, says the account gets the
  super-admin role, that lesser Classroom roles do not see the domain's
  courses, and that the program still only reads because its access is bounded
  by the delegation scopes. → FR-001 #3.
- **AC-002** The instruction, in uk and en, states each of the five measures as
  its own statement, under the lead statement, in the order of FR-001
  (4.1–4.5), the last being "role granted last, immediately before the access
  check". → FR-001 #4, 4.1–4.5.
- **AC-003** In neither language does the instruction or the connection page
  say the account must not be a super-admin, must hold only read-only roles, or
  must have nothing beyond reading Classroom and Admin Reports; `…ReadOnlyRoles`
  and `…NoWriteAccess` exist in neither file. → FR-001 #2, FR-002, FR-003.
- **AC-004** The instruction still says the super-admin creates the account at
  onboarding with the delegation; no person stands behind it and nobody signs in
  to the program with it; the Admin enters its address in the connection
  settings, in the school's domain; it prints no technical-account address. →
  FR-001 #1, #2, #5.
- **AC-005** The copied text carries the same technical-account statements, in
  the same order (and nesting), as the page. → FR-005.
- **AC-006** The connection settings page, in uk and en, shows the FR-003 hint
  under the technical-account field. → FR-003.
- **AC-007** A `TechnicalAccountCannotRead` outcome — on the access-check page
  and in the "Last synchronization" block — says the super-admin must give the
  technical account the super-admin role, as the instruction says; not
  read-only rights. → FR-004.
- **AC-008** The existing connection-instruction, access-check,
  connection-page, synchronization and translation tests stay green apart from
  the assertions this Story deliberately rewrites; every new key exists, non-empty,
  in both translation files. → FR-006, NFR-073.

## 6. Validation Rules

No input is added or changed. The texts are static resources.

- VR-001 Every key of FR-001 and FR-003/FR-004 exists in both
  `SharedResource.uk.resx` and `SharedResource.en.resx`, with a non-empty value
  (NFR-073; existing completeness tests extended to the new keys).
- VR-002 No instruction entry, in either language, contains a Google Workspace
  admin-role name other than the super-admin role (I-2: `ForbiddenRoleNames`
  keeps the delegated role names, drops "Super Admin").
- VR-003 No instruction entry contains a Google Admin console URL or menu path
  (OD-004 a). Checked at review against §4.1; the wording above contains none.
- VR-004 Rendered values stay HTML-encoded by Razor (unchanged, US-010 VR-004).

## 7. Security Requirements

- No authorization change: the instruction stays Admin-only
  (`ViewConnectionInstruction`, US-010 FR-011), the connection page and access
  check keep their policies (SC-4, deny by default).
- No Google scope changes; the program writes nothing to Google Workspace (Hard
  Stop; §6). The texts state this fact; they grant nothing.
- No secret, key, key reference, client secret or technical-account address
  appears in the instruction (unchanged, SC-7; AC-004).
- No personal data is added to any page or log (SC-10). No new outbound
  destination (SC-13).
- The program does not and cannot check the account's role, 2-step
  verification or organizational unit (Out of Scope); the measures are the
  school's responsibility, stated by the instruction as §9 requires.

## 8. Error Handling

No new error paths. FR-004 changes only the advice text of an existing
outcome; classification (`GoogleFailureClassifier`), the access-check step
results and the sync diagnosis codes are unchanged.

## 9. Non-Functional Requirements

- NFR-073: both languages, no hard-coded user-visible string; Google data and
  the client ID / scopes stay untranslated data.
- The instruction remains readable and printable without JavaScript (US-010 I-5).
- Read-only mode: the instruction and hint remain viewable (BR-026, unchanged).

## 10. Out of Scope

- What the program reads, its scopes, the access-check steps, the
  classification of Google failures (§6).
- Any program check of the account's role, 2-step verification or
  organizational unit.
- Google Admin console menu paths or screenshots (OD-004 a).
- The instruction's title, intro, client ID, scope list, the two steps and the
  copy button — their texts already match v97.
- `trebovaniya.md` §7 item 29 — verified by the first live access check after
  this Story (OD-001 a; manual check, §12).
- The deployment kit README and `classroom-agent-ops` notes.
- `trebovaniya.md` itself, including its residual §3 wording (Finding F-1, §12).
- No new requirements version, NuGet package, endpoint, schema or migration.

## 11. Open Decisions

None unresolved. OD-001…OD-004 are carried from the Story with the Owner's
resolutions of 2026-10-24 (`docs/decisions/US-054-open-decisions.md`).
`trebovaniya.md` §7 item 29 is open but does not block this Story: OD-001 (a)
decided to write the texts now; if item 29 fails, measure 2 changes through a
new requirements version and its own Story.

## 12. Interpretations, findings and notes

- **I-1 `NoPersonBehindIt` reworded, not replaced.** Its name still matches the
  v97 meaning once "він не super-admin" is dropped, and AC-004 lists it as a
  statement that stays. The Story's "replace, don't reword" rule applies to keys
  whose *name* no longer matches — `ReadOnlyRoles` and `NoWriteAccess` (FR-002).
  The same reasoning keeps `ImpersonationUser.Hint` and
  `AccessCheck.Outcome.TechnicalAccountCannotRead`.
- **I-2 Role-name guard.** US-010's `ForbiddenRoleNames` existed because §7
  item 10 was unverified. v97 closed it: the only role the instruction names is
  super-admin, and delegated role names must still not appear (they see no
  courses). "Super Admin" leaves the list; the other entries and both guard
  tests stay, with comments citing §9 v97. `NoWorkspaceAdminRoleName_ReachesThePage`
  asserts the `SuperAdminRole` statement instead of the removed `ReadOnlyRoles`.
  `ThePage_SaysASuperAdminAccountIsNotAcceptable` is replaced by tests for
  AC-001/AC-002/AC-003.
- **I-3 Nested measures.** A lead statement plus a nested list keeps "each
  measure its own statement" (AC-002) and the instruction readable as plain
  text. It needs a small change in `Views/ConnectionInstruction/Index.cshtml`;
  the copy script is untouched (FR-005).
- **I-4 Two stale comments outside the instruction.** They state the opposite of
  v97 in code read by future maintainers; fixing their text is a supporting
  change. No assertion in `GoogleClassroomReaderTests` changes.
- **I-5 Testing AC-003 by content.** Besides key absence, the tests assert that
  the rendered instruction and connection page, in both languages, no longer
  contain the four old statement values (the `NoPersonBehindIt`,
  `ReadOnlyRoles`, `NoWriteAccess` and hint values as they are today), held as
  literals in test data.
- **F-1 (non-blocking, project-level).** `trebovaniya.md` §3, `WorkspaceConnection`
  entry (line ~837), still says the impersonation user is "не аккаунт Админа и
  не super-admin", contradicting §1 and §9 v97. §9 is the explicit, newer rule
  and this Specification follows it. The fix is a new requirements version, not
  this Story; raised for the Owner.
- **Manual check.** The first live access check on the pilot server, after the
  super-admin follows this instruction, is also the verification of §7 item 29;
  record it in the implementation report's manual section (as US-047, US-049,
  US-050).
- **Expected stages.** API_DESIGN and DB_DESIGN are NOT_APPLICABLE: no
  endpoint, contract or schema change; translations are resource files.
- **Delegation.** All lookups were done directly in this stage (targeted greps
  of `trebovaniya.md` §1, §4 Epic 6, §7, §9, of the resx files, text-key
  classes, views and tests); nothing was delegated.

## 13. Traceability

| AC | FR / VR | Story scope item |
|---|---|---|
| AC-001 | FR-001 #3, VR-001, VR-002 | super-admin role + why (decision 3) |
| AC-002 | FR-001 #4, 4.1–4.5, VR-001, VR-003 | five measures, role last (OD-002 a, OD-004 a) |
| AC-003 | FR-001 #2, FR-002, FR-003, I-5 | contradicting statements leave |
| AC-004 | FR-001 #1, #2, #5 | unchanged statements |
| AC-005 | FR-005, I-3 | copied instruction |
| AC-006 | FR-003 | connection-page hint (decision 6) |
| AC-007 | FR-004 | diagnosis (OD-003 a) |
| AC-008 | FR-006, VR-001, I-2 | tests rewritten, completeness |
