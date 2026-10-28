---
artifact_type: ac_test_matrix
story: US-062
version: 1
status: DRAFT
created_at: 2026-10-27T20:04:09Z
updated_at: 2026-10-27T20:04:09Z
produced_by: test-writer
inputs:
  - path: docs/specifications/US-062-spec.md
    version: 1
  - path: docs/decisions/US-062-open-decisions.md
    version: 1
  - path: docs/designs/api/US-062-api-design.md
    version: 1
  - path: docs/designs/database/US-062-db-design.md
    version: 1
  - path: docs/designs/database/US-062-entity-model.md
    version: 1
  - path: docs/tests/US-062-test-strategy.md
    version: 1
supersedes: null
---

# US-062 Acceptance Criteria → Test Matrix

Classes (namespace `ClassroomAgent.Tests`):

| Code | Class | Level |
|---|---|---|
| **INV** | `Application.UseCases.DataHealthInvariantTests` | domain unit |
| **RUN** | `Application.UseCases.CourseListEmptySinceTests` | Application unit (`SyncWorld`) |
| **PER** | `Infrastructure.Persistence.DataHealthPersistenceTests` | persistence |
| **QRY** | `Web.UseCases.DataHealthQueryTests` | Application over PostgreSQL |
| **UC** | `Web.UseCases.ConfirmCoursesDeletedUseCaseTests` | Application over PostgreSQL, security |
| **BAN** | `Web.Pages.DataHealthBannerTests` | HTTP |
| **PAG** | `Web.Pages.DataHealthPageTests` | HTTP, contract, security |
| **CNF** | `Web.Pages.CourseDeletionConfirmationHttpTests` | HTTP, contract, security |
| **PM** | `Web.Security.PermissionMatrixTests` (adapted) | security |

Status: all **Red (expected)** before IMPLEMENTATION unless marked **Green\*** (explained in the test
generation report §5).

| AC | Scenario | Level | Class | Test method | Expected result | Status |
|---|---|---|---|---|---|---|
| AC-001 | AC-001 data → 1 technical, 3 teaching (both roles) | App | QRY | `TheAc001Data_CountsOneTechnicalAndThreeTeaching` | counts (1, 3); X1 only; A has 2 grades + 1 item, B dated | Red |
| AC-001 | the Dean's banner on `/`, a teaching page and «Стан даних» | HTTP | BAN | `TheBanner_CountsTheProblems_OnEveryPage` | «1 технічна проблема, 3 навчальні», one link to the page | Red |
| AC-001 | the Admin's banner | HTTP | BAN | `TheAdmin_SeesTheBanner` | same text | Red |
| AC-001 | banner = page totals over every kind | App | QRY | `TheBannerCounts_EqualThePageTotals` | (5, 5) and equal to the page's lists | Red |
| AC-002 | no problem → no banner, null model, page says so | App | QRY | `WithNoProblem_TheBannerIsNull_AndThePageSaysSo` | null; `HasNoProblem`; empty lists | Red |
| AC-002 | no banner element on any layout page | HTTP | BAN | `WithNoProblem_ThereIsNoBanner` | no hook | Green\* (route 404 for the page row → Red) |
| AC-002 | «Проблем із даними немає» | HTTP | PAG | `WithNoProblem_ThePageSaysSo` | text present | Red |
| AC-003 | archived T1 only: not counted, listed apart with details | App | QRY | `AnArchivedCourse_IsListedApart_WithItsDetails_AndNotCounted` | null counts; `Archived` holds the grade | Red |
| AC-003 | no banner for the archived-only problem | HTTP | BAN | `AnArchivedCoursesProblemAlone_ShowsNoBanner` | no hook | Green\* |
| AC-003 | the archived section with its heading and details | HTTP | PAG | `AnArchivedCourse_IsListedUnderItsOwnHeading` | heading, course, item, student, points; not in teaching | Red |
| AC-004 | the corrected grade removes T1 | App | QRY | `ACorrectedGrade_RemovesTheProblem_AtTheNextRequest` | (0, 1) then null | Red |
| AC-005 | every teaching kind with details, links, variants | App | QRY | `EveryTeachingKind_CarriesItsDetails` | values per FR-001.6 / OD-003; count 7 | Red |
| AC-005 | the Dean's page: texts, dates, hints H1, H2, H4, links | HTTP | PAG | `TheDeansTeachingSection_ShowsEveryKindWithItsDetailsAndHints` | pinned uk texts; 4 journal links | Red |
| AC-005 | kinds in the order T4, T3, T2, T1 | HTTP | PAG | `WithinACourse_TheKindsAreInOrder` | ascending positions | Red |
| AC-005 | courses in culture order | App | QRY | `TeachingCourses_AreInCultureOrder` | А, Ґ, І | Red |
| AC-005 | H4 for the Dean only (I-8) | HTTP | PAG | `TheAdmin_DoesNotSeeTheHintToContactTheAdmin` | no H4 for the Admin | Red |
| AC-006 | Dean: titles, no details; Admin: diagnosis, step, finish, course | App | QRY | `TechnicalProblems_ReachTheDeanWithoutDetails_AndTheAdminWithThem` | `Details` null for the Dean | Red |
| AC-006 | the Dean's page: two titles, two H3, no diagnosis, no name | HTTP | PAG | `TheDean_SeesTechnicalTitlesWithTheHint_AndNoDetails` | as stated | Red |
| AC-006 | the Admin's page: diagnosis text, connection link, name, no H3 | HTTP | PAG | `TheAdmin_SeesTheDiagnosisAndTheCourseName` | as stated | Red |
| AC-006 | unknown stored error → `Unexpected` (VR-004) | App, HTTP | QRY, PAG | `AnUnknownStoredError_IsReportedAsUnexpected`, `AnUnknownStoredError_IsShownWithTheUnexpectedText` | `Unexpected` / its text | Red |
| AC-006 | a run in progress raises no X1 (I-1) | App | QRY | `ARunInProgress_RaisesNoFailure` | null counts | Red |
| AC-007 | 25 h raised, 24 h / 23 h not | App | QRY | `NoRecentSuccess_IsRaisedStrictlyAfter24Hours` (×3) | as stated, with the last success | Red |
| AC-007 | none ever: no row / no success | App | QRY | `NoSuccessEver_IsNoneYet` (×2) | `NoSuccessYet` | Red |
| AC-007 | read-only: neither X2 nor X6, any cause | App | QRY | `InReadOnlyMode_NeitherStalenessNorTheControlPlaneProblemIsRaised` (×3) | none; null counts | Red |
| AC-007 | wording of both | HTTP | PAG | `Staleness_IsWorded` (×2) | pinned texts | Red |
| AC-008 | three empty runs keep E1, each completes | Unit | RUN | `ThreeEmptyRuns_KeepTheFirstRunsStart_AndEachCompletes` | date E1; completed; success advances | Red |
| AC-008 | a non-empty run clears the date | Unit | RUN | `ANonEmptyList_ClearsTheDateAndTheFlag` | null, false | Red |
| AC-008 | X4 with E1 for both roles, gone when cleared | App | QRY | `AnEmptyCourseListSince_IsOneProblemWithItsDate` (×2) | one problem, then none | Red |
| AC-008 | wording with the Kyiv date, no X2 | HTTP | PAG | `AnEmptyCourseList_IsWordedWithItsDate` | pinned text | Red |
| AC-009 | one X5, Admin lists three, Dean no details, not in archived | App | QRY | `CoursesNotVisible_AreOneProblem_ListedForTheAdminOnly` | counts (1, 0) | Red |
| AC-009 | the Admin's page: names, checkboxes = ids, button, token | HTTP | PAG | `TheAdmin_SeesTheNotVisibleCourses_WithTheConfirmationForm` | as stated | Red |
| AC-009 | the Dean's page: title + H3, no names, no form | HTTP | PAG | `TheDean_SeesTheNotVisibleProblem_WithoutNamesOrForm` | as stated | Red |
| AC-010 | two of three confirmed: marks, instant, audit ids, third kept | App | UC | `ConfirmingTwo_MarksThemNotInClassroom_AuditsTheirIds_AndLeavesTheThird` | `Confirmed`, 2 | Red |
| AC-010 | confirmed courses count as T4 unless archived | App | UC | `ConfirmedCourses_CountAsNotInClassroom_UnlessArchived` | counts (1, 1) | Red |
| AC-010 | duplicates confirmed once | App | UC | `DuplicateIds_AreConfirmedOnce` | 2 ids | Red |
| AC-010 | `302`, the message once, the list now one | HTTP | CNF | `TheAdminConfirmsTwo_IsRedirected_AndSeesTheMessageOnce` | as stated | Red |
| AC-010 | domain: confirm, keep while absent, clear when listed | Unit | INV | `ConfirmingANotVisibleCourse_…`, `ConfirmingACourseNotInClassroom_…`, `ConfirmingAnUnmarkedCourse_…`, `AConfirmedCourse_KeepsItsMarkWhileAbsent_AndLosesItWhenListed` | as stated | Red |
| AC-010 | audit factories | Unit | INV | `TheSucceededRow_…` (×5 methods), `TheRefusedRow_…` (×2) | shapes of entity model §1.4 | Red |
| AC-011 | read-only on the use case, any cause, before the shape | App | UC | `InReadOnlyMode_TheConfirmationIsRefused_AndOnlyTheRefusalIsAudited` (×4) | `ReadOnlyModeException`; one refused row, no ids | Red |
| AC-011 | Dean `403` | HTTP | CNF | `ADean_IsForbidden_AndNothingChanges` | `Error.Forbidden`; nothing | Red |
| AC-011 | Admin read-only `409` with the reason | HTTP | CNF | `InReadOnlyMode_TheAdminIsRefusedWithTheReason` (×3) | refusal text; one refused row | Red |
| AC-011 | antiforgery `400` (VR-003) | HTTP | CNF | `WithoutTheAntiforgeryToken_ThePostIs400_AndNothingChanges` | `Error.PageExpired` | Red |
| AC-011 | matrix rows 749 / 750 | Security | PM | `EveryEndpoint_DeclaresThePolicyOfItsMatrixRow_AndEveryRowIsAnEndpoint`, `EveryRolePolicy_AdmitsExactlyTheRolesOfItsMatrixRow` | no drift | Red |
| AC-012 | English banner in read-only mode | HTTP | BAN | `InReadOnlyMode_TheEnglishBanner_IsShown` | «1 technical problem, 1 teaching» | Red |
| AC-012 | English page in read-only mode, names as stored | HTTP | PAG | `InReadOnlyMode_TheEnglishPage_IsInEnglish_WithNamesAsStored` | en texts, Cyrillic names, no uk text | Red |
| AC-012 | plural forms in both languages | HTTP | BAN | `TheBannerText_UsesTheLanguagesPluralForms` (×9), `ThePinnedPluralRule_MatchesTheSpecificationsExamples` | pinned forms | Red; the rule check Green\* |
| AC-013 | 25 h raised with C1 and C1 + 7 d, 24 h / 23 h not | App | QRY | `NoConnectionWithTheControlPlane_IsRaisedStrictlyAfter24Hours` (×3) | as stated | Red |
| AC-013 | banner counts it | HTTP | BAN | `NoConnectionWithTheControlPlane_IsCountedInTheBanner` | «1 технічна проблема, 0 навчальних» | Red |
| AC-013 | wording with both Kyiv dates; Dean H3, Admin H5 | HTTP | PAG | `NoConnectionWithTheControlPlane_IsWordedWithBothDates_AndTheRolesHint` (×2) | as stated | Red |
| AC-014 | past grace: read-only notice per cause | App | QRY | `InReadOnlyMode_ThePageCarriesTheReadOnlyNotice` (×3) | reason | Red |
| AC-014 | past grace: message replaces X6 on the page | HTTP | PAG | `PastTheGracePeriod_TheReadOnlyMessageReplacesTheProblem` | `Landing.ReadOnly.*` texts first; no X6 | Red |
| AC-014 | no read-only message outside read-only mode | HTTP | PAG | `OutsideReadOnlyMode_ThereIsNoReadOnlyMessage` | no hook | Red (route 404) |
| AC-014 | `upgrade_required` named for the Admin only | App | QRY | `UpgradeRequired_IsNamedInTheAdminsDetails_Only` | true / null | Red |
| AC-014 | `upgrade_required` within a day raises nothing (I-9) | App | QRY | `UpgradeRequired_WithinADay_RaisesNothing` | null | Red |
| AC-015 | no banner on the forced password change | HTTP | BAN | `TheForcedPasswordChangePage_HasNoBanner` | no hook | Green\* |
| AC-016 | migration backfill | Persistence | PER | `TheMigration_DatesAFlaggedRowWithItsStart_AndLeavesAnUnflaggedOne` (×2) | `started_at` / null | Red |
| AC-016 | later empty keeps, non-empty clears (domain) | Unit | INV | `AnEmptyList_SetsTheDateToTheRunsStart`, `LaterEmptyLists_KeepTheFirstDate`, `ANonEmptyList_ClearsBoth_AndTheNextStreakStartsAgain`, `BeginningCompletingAndFailingARun_LeaveTheDate`, `ANewState_HasNoEmptyListDate` | as stated | Red; the last Green\* |
| AC-016 | no date when no course is stored | Unit | RUN | `AnEmptyListWithNoCourseStored_SetsNoDate` | null | Green\* |
| AC-016 | column, CHECK, round trip, migration name | Persistence | PER | `TheMigration_ExistsUnderTheDesignedName_AfterTheLastOne`, `TheDateColumn_IsANullableTimestamptz`, `AFlagAndDateThatDisagree_AreRejectedByTheCheck` (×2), `AFlagAndDateThatAgree_AreAccepted` (×2), `TheDate_IsStoredAndReadBack` | as stated | Red |
| AC-017 | stale / unmarked / unknown id → `ListChanged`, nothing, no audit | App | UC | `AStaleSelection_ChangesNothing_AndWritesNoAudit` (×3) | as stated | Red |
| AC-017 | `409` with «Список змінився — оновіть сторінку» | HTTP | CNF | `AStaleList_Is409_WithTheListChangedMessage_AndChangesNothing` | as stated | Red |

## Supporting rows (no single AC; FR / VR / design)

| Requirement | Scenario | Class | Test method | Status |
|---|---|---|---|---|
| VR-001 | malformed form → `Invalid`, nothing, no audit | UC | `AnInvalidForm_IsRefused_AndChangesNothing` (×9) | Red |
| VR-001, I-6 | 1000 distinct pass the shape | UC | `UpTo1000DistinctIds_PassTheShapeCheck` (×2) | Red |
| VR-001 | `400` with the page re-rendered | CNF | `AMalformedForm_Is400_WithThePage_AndChangesNothing` (×3) | Red |
| TC-5 | page: Dean, Admin `200`, no-store | PAG | `TheDeanAndTheAdmin_OpenThePage` (×2) | Red |
| TC-5 | page: anonymous, restricted → `302` | PAG | `WithoutAFullSession_ThePageRedirects` (×2) | Red |
| TC-5 | confirmation: anonymous, restricted → `302` | CNF | `WithoutAFullSession_ThePostRedirects_AndChangesNothing` (×2) | Green\* (fallback redirects) |
| FR-005 | ordinary page-search hint, uk and en | PAG | `ThePage_CarriesTheOrdinaryPageSearchHint` (×2) | Red |
| FR-004.2 | section order and «Немає» | PAG | `TheSections_AreInOrder_AndAnEmptyOneSaysNone` | Red |
| OD-004 a | archived X3 for the Dean: no name | QRY, PAG | `AnArchivedUnknownStateCourse_IsListedApart_WithoutItsNameForTheDean`, `AnArchivedUnknownStateCourse_ReachesTheDeanWithoutItsName` | Red |
| api-design §2.10 | technical order X1, X2, X6, X4, X5, X3 | QRY | `TechnicalProblems_AreInTheDesignedOrder` | Red |
| FR-001.1, SC-8 | the queries write nothing, no Google call | QRY | `TheQueries_WriteNothing_AndCallNoGoogle` | Red |
| FR-003.1 | no banner without a session / in `/api/v1` | BAN | `ThePageWithoutASession_HasNoBanner`, `AnApiResponse_HasNoBanner` | Green\* |
| FR-003.3 | restrained banner | BAN | `TheBanner_IsRestrained` | Red |
| §8, I-4 | failing computation → no banner, warning logged | BAN | `AFailingComputation_LeavesThePageWithoutTheBanner_AndIsLogged` | Red |
| db-design §3 | ids column, accepted and refused shapes, constraints, factory round trip | PER | `TheIdsColumn_IsANullableBigintArray`, `TheDesignedRows_AreAccepted`, `AConfirmationRowOutsideTheShape_IsRejected` (×7), `AnotherActionWithIds_IsRejected`, `TheNewConstraints_Exist`, `TheFactorysRow_IsStoredWithItsIds` | Red |
| db-design §4 | locked read: known ids, tracked; lock held | PER | `TheLockedRead_ReturnsKnownCoursesOnly_Tracked`, `TheLockedRead_HoldsARowLockUntilTheTransactionEnds` | Red |
| db-design §5 | fact query rule and rows; detail queries | PER | `TheAboveMaximumFact_FollowsTheDomainRule` (×7), `TheFactQuery_ReturnsExactlyTheCoursesWithAFact`, `TheAboveMaximumDetails_AreOneRowPerGrade_ByItemThenStudent`, `TheItemsGone_AreListedWithTheirInstant` | Red |
| db-design §5.2 | the two partial indexes | PER | `ThePartialIndex_Exists` (×2) | Red |

The Green\* statuses above are predictions from the test design; the test generation report records the
observed red-phase result of every class and explains each test that passes before implementation.
