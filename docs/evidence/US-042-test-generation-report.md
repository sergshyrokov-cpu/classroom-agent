---
artifact_type: test_generation_report
story: US-042
version: 1
status: DRAFT
created_at: 2026-10-05T12:49:19Z
updated_at: 2026-10-05T12:49:19Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-042-spec.md
    version: 1
  - path: docs/designs/api/US-042-openapi.yaml
    version: 1
  - path: docs/designs/database/US-042-entity-model.md
    version: 1
  - path: docs/tests/US-042-test-strategy.md
    version: 1
  - path: docs/tests/US-042-ac-test-matrix.md
    version: 1
  - path: docs/decisions/US-042-open-decisions.md
    version: 2
supersedes: null
---

# US-042 Test Generation Report

## 1. Result

**PASS — red phase verified.** `dotnet build ClassroomAgent.sln`: 0 warnings,
0 errors. Full suite (`dotnet test ClassroomAgent.sln`, Docker running):
**3597 total, 3486 passed, 111 failed, 0 skipped**, 3 m 06 s. After that run
three new tests were found green for the wrong reason and strengthened (§4);
re-run of their classes: all red. Final expected state therefore **115 red**,
every one in a class listed in §3, each failing for missing production
behaviour. **No unrelated existing test regressed.**

## 2. Production skeleton (OD-001 a, Owner 2026-10-05)

Compile-only; nothing registered in DI; no migration; no EF mapping change;
displayed names unchanged. IMPLEMENTATION owns and completes it.

- **Domain:** enum `ReportNameSource`; `ClassroomParticipant`:
  `MaxSurnameLength`, `MaxGivenNameLength` (750), throwing `Surname` /
  `GivenName`, throwing 5-argument `Import` and 4-argument `UpdateFrom`;
  `ReportTemplate.NameSource` (throws); `ReportTemplateSettings` gains
  `NameSource = Profile` as the last, optional parameter.
- **Application:** `RosterEntry` and `JournalCourseMemberRecord` gain optional
  `Surname`, `GivenName`; `ReportRequest` gains optional `Names`; throwing
  `Report.NameSource`, `Report.NameSourceOrigin`, `ReportPageModel.NameSwitch`,
  `ReportTemplateFormValues.Names`; new `ReportNameKind`, `NameSourceOrigin`,
  `NameSourceSwitch`, `NameSourceSwitchOption`; `ReportMessageKey.NameSourceMalformed`,
  `ReportTemplateFieldErrorKey.NameSourceInvalid`.
- **Changed to compile:** `PersonName.NameKind` is `ReportNameKind`
  (openapi US-042); `GetReportQuery.SkeletonKind` maps the US-027 label kind
  onto it, and `Views/Report/Index.cshtml` compares with `ReportNameKind.Unnamed`.

**Binding notes for IMPLEMENTATION**

1. Remove `GetReportQuery.SkeletonKind`; implement the FR-003 rule (entity model §3.3).
2. When `FullName` leaves `JournalCourseMemberRecord` (entity model §3.2),
   delete the argument `FullName: null,` in
   `TestInfrastructure/FakeJournalFieldSource.AddMember` — the only test edit
   the implementation needs.
3. Keep `ReportRequest.Names` optional (null or empty = absent): existing tests
   construct it with four arguments.
4. The page test expects the form's source as radio inputs `name="names"`
   with `value="profile"` / `value="email"`, the stored one `checked`
   (openapi `ReportTemplateForm.names`: "Radio group, two options").
5. The logging tests expect the effective source (`profile`/`email`) and its
   origin (`Page`/`Template`) as separate structured properties of the
   `ReportBuilt` event (spec FR-011 does not name them).

## 3. Files

Created:
- `Application/UseCases/ReportPersonNameTests.cs`, `ReportNameSourceTests.cs`,
  `ReportTemplateNameSourceTests.cs`, `ParticipantNamePartsTests.cs`
- `Infrastructure/Persistence/NamePartsPersistenceTests.cs`
- `Web/Pages/ReportNameSourcePageTests.cs`,
  `Web/Security/ReportNameSourceAuthorizationTests.cs`,
  `Web/Logging/ReportNameSourceLoggingTests.cs`,
  `Web/Localization/ReportNameSourceTranslationTests.cs`

Modified (existing tests re-pointed at the changed requirement — red until
IMPLEMENTATION):
- `Application/UseCases/ReportContentTests.cs` — names by FR-003, `ReportNameKind`.
- `Web/Pages/ReportPageTests.cs` — markers are the email parts (`student.one`,
  `teacher.one`): the seeded people have no profile parts, and the full name
  is no longer a report source (spec I-6).
- `Infrastructure/Persistence/JournalFieldSourceTests.cs` — members carry the parts.
- `Web/Persistence/ClassroomParticipantSchemaTests.cs` — 8 columns.
- `Web/Persistence/ReportTemplateSchemaTests.cs` — `name_source` in the column
  list; its insert helper writes `name_source` once the column exists.
- `Infrastructure/Google/GoogleClassroomReaderTests.cs` — one test added.
- Unchanged file, red: `Web/Localization/ReportTemplateTranslationTests.cs` —
  its key list (`ReportTemplateTestData.TextKeys.All`) now includes the two new
  keys, which do not exist yet.

Fixtures modified: `FakeJournalFieldSource`, `ReportTemplateTestData`,
`ReportTemplateFormBuilder`, `ReportHostExtensions`, `CourseRows`, `SyncWorld`,
`Application/UseCases/ReportTemplateExpectations` (now also compares
`NameSource`). Production files: see §2.

## 4. Classification

- **Red, expected (115):** `NotImplementedException` from the skeleton; missing
  columns `surname` / `given_name` / `name_source` (PostgreSQL 42703); missing
  migration; names still from the full name; no switch, no `names` parameter
  handling, no field error; missing translation keys; `ReportBuilt` without the
  source. Each message matches its scenario.
- **Green by design (14):** 12 cases of `ReportNameSourceAuthorizationTests`
  (the policies and redirects already hold; they guard the changed endpoints,
  TC-5); `TheBuiltIn_UsesTheProfile` (the skeleton's default parameter is
  `Profile`; guards D-7); `TheBuiltInsSetting_CannotBeChanged` (US-027 refusal;
  guards AC-005).
- **Green for the wrong reason, fixed:** two "nameless teacher" theory cases
  (Unnamed is also what the full-name rule gives) — now also assert the
  effective source; `NoIndex_CoversTheNewColumns` (passed because the columns
  did not exist) — now asserts they exist first; `EqualShownNames_FollowTheInternalId`
  (both rows were unnamed) — now asserts the shown name.
- **Unexpected failures:** none.

## 5. Commands

`dotnet build ClassroomAgent.sln`; `dotnet test ClassroomAgent.sln`;
`dotnet test ClassroomAgent.sln --filter "FullyQualifiedName~…"`; for the TRX,
the test executable with `-result-trx` (the Microsoft.Testing.Platform runner
does not accept `--logger`). `--nologo` never used.

## 6. Untested Acceptance Criteria

None. Not automated: the switch's layout at phone width (NFR-070) — for the
human at HUMAN_PR_APPROVAL.

## 7. Open Decisions

OD-001 (compile-only skeleton) — raised by this stage, resolved (a) by the
Owner on 2026-10-05. None open.
