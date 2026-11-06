---
id: US-076
epic: EPIC-6
title: A «?» beside the title opens short help on the journal, Meet codes, Meet reports and template list
slug: page-help
priority: HIGH
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-06 from the Owner's decisions of 2026-11-06.
# Aligned with trebovaniya.md v112 (section 5 «Справка на странице», NFR-079).
# Before the pilot; built after US-074, which changes the Meet code page.
---

# User Story

As a **Dean** who works at school and at home, often on a phone, with nobody nearby to ask

I want a short explanation on the pages whose logic is not obvious

So that I understand where the marks, grades and presence come from and what to do when something looks wrong.

---

# Business Value

The Owner cannot explain every page to every Dean in person. The journal, the Meet codes, the Meet reports
and the report templates raise the most questions: why a student is marked «выбыл» or «не виден», why a Meet
code must be linked to a course, why a person is missing from a Meet report. Two to four lines on the page
itself answer the first question without a call. Help on the other pages waits for the Deans' real questions
in the pilot.

---

# Scope

**In scope**, on five installation pages, for Admin and Dean alike:

1. **The journal** (with its export): where grades and marks come from, what the student marks («выбыл»,
   «не виден») mean, what goes into the export.
2. **The Meet codes**: why a code is linked to a course and what happens to a code without a link.
3. **The Meet reports**, on the meeting list (`MeetReports/Index`) and on the one-meeting page
   (`MeetReports/Meeting`, v112): what the program counts as presence and why a person may be missing from a
   report.
4. **The report template list**: how a template becomes a report for a course.

On each of these five pages:

- a **«?» control beside the page title**; pressing it expands the help in place under the title, pressing
  again collapses it; no pop-up;
- the help is **collapsed on every page load**; nothing is stored, nothing is sent;
- the text is **two to four lines on one scheme**: what the page is for; where its data comes from (Google,
  refreshed by synchronization, not edited on this page); what to do if something looks wrong;
- the text comes from **translation files**, Ukrainian and English, in the user's language (NFR-073), and
  keeps its sentence periods (NFR-078 binds one line only);
- **easy to tap and easy to read on a phone** (NFR-070); restrained in look, like the rest of the program
  (NFR-076);
- the same help in **read-only mode**: it changes nothing and grants no right.

**Out of scope:** help on any other page of the installation and anywhere in the Control Plane; any change to
what the five pages show or do; any new rule — the help describes the program as it is (`trebovaniya.md`
section 5: where text and program differ, the text is wrong); remembering whether a person opened the help;
links from the help to other pages, unless the Specification shows one is needed and the Owner approves it.

---

# Acceptance Criteria

## AC-001 The control is on the five pages

**Given** an Admin or a Dean, in Ukrainian and in English, in normal and in read-only mode

**When** they open the journal, the Meet codes, the Meet report list, the one-meeting page or the report
template list

**Then** a «?» control stands beside the page title, with an accessible name from the translation files, and
the help text is collapsed.

## AC-002 Pressing expands and collapses

**Given** one of the five pages with the help collapsed

**When** the person presses the control, by mouse, by touch or by keyboard

**Then** the help appears under the title, on the page itself, with no pop-up; pressing again collapses it.
The page's other content and the page-search hint (NFR-075) stay as they are.

## AC-003 Collapsed again on every page load

**Given** a person who expanded the help

**When** they open the page again, reload it, or change the language

**Then** the help is collapsed. Nothing about it is stored on the server, in a cookie or in the browser.

## AC-004 The text follows the scheme, in both languages

**Given** the exact texts approved by the Owner at HUMAN_SPEC_APPROVAL

**Then** each page shows its own text, two to four lines: what the page is for, where its data comes from,
what to do if something looks wrong, with the meaning `trebovaniya.md` section 5 gives for that page. The
texts come from the translation files in both languages; no help text is in a view or in code.

## AC-005 No help elsewhere

**Given** every other installation page and every Control Plane page

**Then** there is no help control and no help text.

## AC-006 Nothing else changes

**Given** the five pages

**Then** what they show, their forms, links, permissions, read-only behaviour and the export are unchanged;
the help sends no request when opened; the browser security headers are not loosened (SC-15). Without scripts
the page still works and the help is either reachable or absent, as the Specification decides; it never hides
page content.

## AC-007 A person checks the look

**Given** the five pages on a computer and on a phone, in both languages

**When** the person opens and closes the help

**Then** the control is easy to tap, the text is easy to read and does not push the page sideways, and the look
is restrained. The walk is recorded in the implementation report.

---

# Open Decisions

- **OD-001 Sub-pages of the pages with help.** Resolved by the Owner 2026-11-06: option (b) — the
  one-meeting page of the Meet reports gets help too, since presence is read there and some Deans need
  teaching; `trebovaniya.md` v112. The other sub-pages (`MeetCodes/CourseChoice`, the template form and
  delete page) have no help.

---

# Notes

- **The texts are written in the Specification** in both languages and approved by the Owner at
  HUMAN_SPEC_APPROVAL (`trebovaniya.md` section 5). The spec-writer drafts them from the requirements and the
  code, citing the BR- rule behind each sentence, so SPEC_REVIEW can check that no sentence adds a rule.
- **Hints for the Specification, not requirements:** the page title is the layout's `<h1>`
  (`Views/Shared/_Layout.cshtml`), so the control needs the layout's cooperation, for example a view-data flag
  set by the five views; a native `<details>`/`<summary>` element gives collapse-on-load and keyboard access
  with no script and no CSP change; the page-search hint (`_PageSearchHint`) sits directly under the title,
  and the Specification places the help text relative to it.
- **Tests** read the rendered pages of both roles and both languages, as the page-search hint tests do; the
  look on a phone is a person's check (AC-007). The look-check stand can serve the walk.
- **US-074** changes the Meet code page; this Story is built after it so the help describes the organizer
  shown by name.
- No new endpoint or schema is expected: API and database design should be empty deltas.
