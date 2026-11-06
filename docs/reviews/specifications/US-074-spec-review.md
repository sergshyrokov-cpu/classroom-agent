---
artifact_type: specification_review
story: US-074
version: 1
status: APPROVED
created_at: 2026-11-06T19:39:22Z
updated_at: 2026-11-06T19:39:22Z
produced_by: spec-verifier
inputs:
  - path: docs/stories/US-074-meet-code-organizer-by-name.md
    version: null
  - path: trebovaniya.md
    version: 112
  - path: docs/specifications/US-074-spec.md
    version: 1
  - path: docs/specifications/US-074-spec-digest.ru.md
    version: 1
  - path: docs/decisions/US-074-open-decisions.md
    version: 1
supersedes: null
---

# US-074 Specification Review

## Summary

Verdict **PASS**. The Specification is faithful to `trebovaniya.md` v112 (section 4 Epic 4 «организатор в списках
кодов» v110; section 5 «Поиск» incl. «Что не находится» v110 and the Meet codes field; BR-085, BR-087, BR-088), covers
all eight Story Acceptance Criteria, invents no rule, right or data, and names its three interpretations in both the
Specification (section 13) and the digest (section 2). Two Minor findings.

Nothing was delegated: the reviewer read the cited `trebovaniya.md` lines (changelog v110, section 5 «Поиск» lines
~1300–1350, Epic 4 lines ~1532–1538), `business-rules.md` BR-085/BR-087 and the code the Specification cites
(`MeetReportSource.GetPeopleAsync`, `MeetReportRules.Person`/`Name`/`Lower`, `MeetCodesReadSource.LoadOrganizersAsync`
and `CodesByOrganizer`) at commit `eff66cb`.

## Reviewed Artifacts

| Artifact | Path | Version |
|---|---|---|
| story | docs/stories/US-074-meet-code-organizer-by-name.md | (no version field) |
| requirements | trebovaniya.md | 112 |
| specification | docs/specifications/US-074-spec.md | 1 (DRAFT) |
| spec_digest | docs/specifications/US-074-spec-digest.ru.md | 1 (DRAFT; input spec v1 — current) |
| open_decisions | docs/decisions/US-074-open-decisions.md | 1 |

## Fidelity

- FR-001/FR-002 match Epic 4 v110: «имя, как его отдал Google, и рядом email; у человека, чьего имени программа не
  знает, только email … имя берётся из уже загруженных участников Classroom, в Google программа за ним не обращается».
- The name rule (lowest `Id` among participants of the lower-cased email with non-null `FullName`; blank → no name)
  matches the code: `MeetReportSource.cs:146-152` and `MeetReportRules.cs:108-115`. The email comparison
  (`ToLowerInvariant` / `lower()`) matches `MeetReportRules.Lower` and `GetPeopleAsync`.
- FR-003 matches section 5 «коды встреч Meet — … код, имя и email организатора (v110) …» and BR-087 («an organizer's
  name and email only where the screen shows them»). Erased/excluded organizers: `CodesByOrganizer` and
  `LoadOrganizersAsync` read `MeetSession.OrganizerEmail`, null after erasure/exclusion — consistent with FR-001.
- FR-004 matches section 5 header group «коды встреч Meet — как в поиске в списке кодов».
- Section 7 (security) and section 9 (no new strings, volume ~1000/300) cite their sources correctly. No invented
  endpoint, schema, right or audit event.

## Story Coverage

Story AC-001…AC-008 map one-to-one to Specification AC-001…AC-008 (traceability table, section 12). AC-009 is an
addition justified by the Story note («so the list and the search cannot disagree») and Epic 4 «как в отчётах Meet»;
it narrows, not widens, scope. Out-of-scope list matches the Story's and adds two clarifying exclusions (header result
rows, accounts that linked a code) that are already the status quo.

## Consistency

- US-070 FR-001/FR-004/FR-005 and VR-001…VR-004 are reused, not changed; US-071 FR-006 (header group) reused.
- Permission matrix: no new cell; `ViewMeetCodes` (Admin, Dean) and `LinkMeetCodes` unchanged.
- Read-only mode (BR-026/AD-6): views only, no Google port. NFR-073: no new string. SC-11: viewing not audited,
  nothing added. SC-10: search text and names not logged.
- US-038 I-9 (erased organizer stored as null) respected.

## Open Items

`trebovaniya.md` section 7 holds no item on organizer names; item 28 (`call_ended` field names) is untouched. No
Open Decision is needed; the Story carries none.

## Interpretations

1. **I-1** Case-only email duplicates merge into one organizer (today ordinal distinct) — named in spec §13 and
   digest §2.
2. **I-2** Search words may match across two organizers of one code — named.
3. **I-3** Only the chosen (shown) name is searched; `Surname`/`GivenName` and other same-email participants not
   searched — named.
4. Lowest-id name choice reused from the Meet reports for the codes page — named in digest §2 first bullet.
5. AC-009 (one name rule shared with the Meet reports) — named in digest §2 first bullet.

No unnamed interpretation found.

## Digest Fidelity

Every digest statement traces to the Specification. All three interpretations and the name rule are in section 2,
not softened; «Открытые вопросы: нет» is true. Plain Russian. See m-2 for one imprecise wording.

## Findings

- **m-1 (Minor)** — Specification FR-001 «The organizer's name» and AC-002. The rule takes the lowest-id participant
  with a non-null `FullName` and *then* treats a blank value as no name (as the code does, `MeetReportSource.cs:151`
  then `MeetReportRules.Name`). So when the lowest-id participant has a blank name and a later one has a real name, the
  result is "no name". The text can be read either way, and AC-002 («whose participants have no name or only a blank
  name») does not state the mixed case. Advisory: the database design and tests should state the order explicitly;
  AC-009 already forces equality with the Meet reports.
- **m-2 (Minor)** — Digest section 2, first bullet: «берётся имя участника, загруженного первым». The rule is the
  lowest database id among participants *with a name*, which is close to, but not literally, "loaded first" (a
  participant without a name is skipped). Advisory wording only; no decision is misstated.

## Verdict

PASS — no Critical or Major finding. Ready for `HUMAN_SPEC_APPROVAL`.
