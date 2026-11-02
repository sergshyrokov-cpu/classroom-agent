---
id: US-069
epic: EPIC-6
title: Small interface text fixes and a show-password control
slug: small-interface-texts-and-show-password
priority: MEDIUM
source:
  type: authored
# Lifecycle status is owned by docs/catalog/stories.yaml (not this file).
# DRAFT prepared by the agent on 2026-11-02 from the Owner's remarks during the
# US-068 AC-007 look check (2026-11-01 and 2026-11-02), which US-068 could not
# take because its FR-009 forbids any text change or new script behaviour.
# Aligned with trebovaniya.md v106 (§8, NFR-077, SC-2). OD-001...OD-005 resolved a)
# by the Owner 2026-11-02.
---

# User Story

As an **Admin or Dean** (and the Owner in the Control Plane)

I want the small rough edges the look check found to be gone: a role that says it
is a role, one page title, named blocks, a clear course prompt, and a way to see
the password I am typing

So that a first-time user understands each page without asking and makes fewer
mistakes when typing a password.

---

# Business Value

The look check before the first demo found these on real pages. Each is small; together
they are what a first-time dean notices. They are one Story because they touch the same
kind of thing (views and translation files) and each is too small to carry a workflow alone.

---

# Scope

**In scope:**

1. **Role label.** The installation's landing page shows the role name alone (`Декан`,
   `Адміністратор школи`). It reads «Роль: Декан» (OD-002 a).
2. **One page title.** Four installation views render their own `<h1>` under the one the
   layout renders from `ViewData["Title"]`, with the same text: `MeetCodes/Index`,
   `MeetCodes/CourseChoice`, `Erasure/Index`, `Erasure/Confirmation`. The view's own `<h1>`
   goes; the layout's stays.
3. **`/database` block headings.** The three blocks get headings: «Курси», «Дані»,
   «Синхронізація» (en: Courses, Data, Synchronization). The numbers, their labels and the
   layout (three in one row, stacked on a phone) stay.
4. **Meet reports course prompt.** The empty first option of the course field shows
   «— оберіть курс —», as the journal and the report do; its value stays empty, so what
   submitting it does is unchanged.
5. **Show-password control** on all 9 password fields in 6 views (OD-005 a): Control Plane
   `Setup/Index` (2), `SignIn/Index`; installation `SignIn/Index`, `DeanPassword/Forced`,
   `DeanPassword/Own` (2), `DeanAccounts/Index` (2). It is an eye icon inside the right edge
   of the field (OD-004 a). A press shows the password as text, a second press hides it again.

**Out of scope:**

- search in the teacher list and the Meet codes list (a separate discussion);
- the language labels: they stay `УКР` and `ENG` (OD-003 a);
- any change of password rules, sign-in checks, lockout, what is stored or logged;
- any route, policy, form field name, export file, audit event or business rule;
- any change of the security headers or the CSP string;
- any other wording. The look of pages stays as US-068 left it.

---

# Acceptance Criteria

## AC-001 The landing page says what the role is

**Given** a signed-in Admin or Dean, in Ukrainian and in English

**When** the landing page opens

**Then** the role line reads the word for role followed by the role name, from the
translation files, in the language of the page.

## AC-002 Every page has exactly one page title

**Given** the four views named in Scope item 2

**When** each is opened by a role allowed to open it

**Then** the page holds exactly one `<h1>`, with the same text as before; a test fails
if any view of either host renders its own `<h1>`.

## AC-003 The database page names its blocks

**Given** the `/database` page, in both languages

**Then** each of the three blocks has its heading above its numbers; every counter and
synchronization line keeps its label and value.

## AC-004 The Meet reports course field prompts for a course

**Given** the Meet reports page with at least one course, in both languages

**Then** the first option of the course field shows the same prompt as the journal and
the report, with an empty value; a request with no course behaves as it does today.

## AC-005 A password can be shown and hidden

**Given** any of the 9 password fields, with a script-capable browser

**When** the user presses the control

**Then** the field shows the typed text and the control's accessible name and state say
so; a second press hides it; the typed text is kept; the control does not submit the form
and is reachable by keyboard.

## AC-006 A shown password never leaves the page differently

**Given** a password field currently shown as text

**When** the form is submitted

**Then** the field returns to hidden before the page leaves, so the browser neither stores
the value as ordinary text nor restores it visible on "back"; the request is the same as
with a hidden field. A field shown as text has spell checking and auto-capitalisation off
(a browser's enhanced spell check can send field text to an outside service, SC-13).

## AC-007 Nothing depends on the script

**Given** a browser with scripts off

**Then** the page shows no dead control, and every password field works as it does today.

## AC-008 Security headers and inline-code rules are unchanged

**Then** the CSP string of both hosts is unchanged and the existing header tests pass
unchanged; `InlineCodeRuleTests` passes: the behaviour lives in a script file in each
host's `wwwroot/js`, with no inline handler or style; nothing is loaded from another site;
a server-side test proves that the control adds no field to any form.

## AC-009 Sign-in and password rules are unchanged

**Then** the existing sign-in, lockout and password tests of both hosts pass without a
change of expectation; no password value appears in a log or in a response.

## AC-010 Every new text is translated

**Then** every new text (role word, three block headings, the control's accessible names,
the Meet prompt if it gets its own key) exists in both languages of the host that uses it;
the existing translation tests pass.

## AC-011 The control looks right (manual)

**Given** the application running locally with synthetic data

**When** a person opens, at desktop width and at about 375 px, Dean sign-in, Control Plane
sign-in, the Dean-accounts page and the landing and `/database` pages, in Microsoft Edge

**Then** each field shows one control only (Edge's own reveal button does not stand next to
it), the control sits inside or next to its field without covering text, and the browser
console shows no Content-Security-Policy violation. The result is recorded in the
implementation report and confirmed by the Owner at `HUMAN_PR_APPROVAL`.

---

# Open Decisions

Proposed by the agent 2026-11-02; all resolved a) by the Owner the same day («всё по умолчанию»).

## OD-001 Where the show-password rule is decided

`trebovaniya.md` says nothing about showing a typed password. AGENTS.md forbids a security
behaviour no artifact defines, and §8 v105 says styling adds no visible text.

- **a) (recommended)** — a short new version of `trebovaniya.md` first (v106, §5/§8: the
  control, hidden again on submit, no change to password rules; the texts of Scope 1, 3, 4),
  in its own commit, then the derived documents (SC-2, NFR), then this Story.
- b) — the Story alone, with no new version: faster, but a security-relevant behaviour
  would live only in a Story, against the order of authority.

**Resolved 2026-11-02 by the Owner: a)** (v106).

## OD-002 The role line

- **a) (recommended)** — add the word: «Роль: Декан», en "Role: Dean".
- b) — leave the line as it is and drop Scope item 1.

**Resolved 2026-11-02 by the Owner: a).**

## OD-003 Language labels

Today all four translation files already show `УКР` and `ENG`; the idea of short labels was
noted during the look check without this fact.

- **a) (recommended)** — leave `УКР` and `ENG`: already short, and three letters each read as
  a pair.
- b) — change to `УКР` and `EN`, in both hosts.

**Resolved 2026-11-02 by the Owner: a).**

## OD-004 What the control looks like

- **a) (recommended)** — an eye icon inside the right edge of the field: a small same-host SVG
  (two states), with translated accessible names; no visible new word, so narrow fields
  (Dean accounts, phone) stay tidy.
- b) — a small text button «Показати» / «Сховати» next to the field: clearer to a first-time
  user, but it needs room in the Dean accounts lines fixed in US-068.

**Resolved 2026-11-02 by the Owner: a).**

## OD-005 Which fields

- **a) (recommended)** — all 9, including the temporary password an Admin sets for a Dean: one
  rule, and that is where a typing mistake costs a support call.
- b) — all but the Admin's temporary password fields, so a Dean's password is not shown on an
  Admin's screen.

**Resolved 2026-11-02 by the Owner: a).**

---

# Notes

- **Code today:** role line `src/ClassroomAgent.Web/Views/Home/Index.cshtml:6`
  (`Landing.Role.*`); layout title `Views/Shared/_Layout.cshtml:25`; `/database`
  `Views/Database/Index.cshtml`, blocks are `dl` in `#database-counters` and
  `#database-synchronization`, styled in `site.css`; prompt key `Journal.Form.ChooseCourse`
  used by `Journal/Index` and `Report/Index`, blank option at `MeetReports/Index.cshtml:35`;
  labels `Layout.Language.*` in both hosts' `SharedResource.*.resx`, pinned by
  `tests/.../TestInfrastructure/UiLanguageTestData.cs`.
- **Tests that may need adapting:** `PageSearchHintTestData` finds the hint relative to `</h1>`;
  helpers matching landing and Meet reports markup.
- **Edge's own reveal button:** Edge draws one until the field is refilled (`::-ms-reveal`); the
  look check saw only that one. Hide it in `site.css` so a field never shows two.
- **Script on anonymous pages:** sign-in and first-run setup would load a same-host script
  file for the first time; allowed by SC-15 (`script-src 'self'`), but SECURITY_REVIEW should
  look at it specifically, since these pages take the password.
- **The control starts hidden** in the markup and the script reveals it, which is what makes
  AC-007 hold. A rendered check in a real browser is manual (AC-011): the project has no browser
  test tool, and none is added.
- Expected stages: SPECIFICATION, API_DESIGN and DB_DESIGN NOT_APPLICABLE (no endpoint, no schema).
- No new NuGet package, no new tool; requirements version v106.
