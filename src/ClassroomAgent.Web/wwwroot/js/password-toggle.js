// US-069 FR-005, FR-006, US-073 FR-002 to FR-008, SC-2: the eye control of a password field. It appears only for text the
// person entered into an empty field by typing or pasting, never for a password the browser filled in, and in doubt it
// stays hidden. The script compares the field with the empty string in one helper and otherwise never touches what was
// typed; it sends nothing anywhere and stores nothing. A static file, so the Content-Security-Policy needs no
// 'unsafe-inline'. Until it runs the control stays hidden and the field works as usual.
(function () {
    "use strict";

    // FR-002: one state per field, in memory only.
    var EMPTY = "EMPTY", ENTERED = "ENTERED", FILLED = "FILLED";

    // FR-003: a closed list. Anything not named here, such as an undo, a drop or dictation, is not entry.
    var ENTRY_KINDS = ["insertText", "insertCompositionText", "insertFromComposition", "insertFromPaste"];
    var ENTER_KIND = "insertLineBreak";

    var fields = [];

    // FR-008.1: the only use of the field's text.
    function isEmpty(input) {
        return input.value === "";
    }

    // FR-003: a browser that does not know the selector answers "filled": in doubt, no control.
    function isFilled(input) {
        try { return input.matches(":autofill"); } catch (e) { return true; }
    }

    function isEntryKind(kind) {
        return ENTRY_KINDS.indexOf(kind) !== -1;
    }

    function isDeletionKind(kind) {
        return typeof kind === "string" && kind.indexOf("delete") === 0;
    }

    function show(field) {
        field.input.type = "text";
        field.control.setAttribute("aria-pressed", "true");
        field.control.setAttribute("aria-label", field.control.dataset.hideName);
    }

    function hide(field) {
        field.input.type = "password";
        field.control.setAttribute("aria-pressed", "false");
        field.control.setAttribute("aria-label", field.control.dataset.showName);
    }

    // FR-006.4 of US-069: every shown field goes back to hidden before the page leaves, so the browser neither stores the
    // text as ordinary text nor restores it visible on "back".
    function hideAll() {
        fields.forEach(hide);
    }

    // FR-002: leaving ENTERED hides the field first, and the control is visible in ENTERED only. FR-007.3: a keyboard user
    // whose control has just gone stays on the field, not at the top of the page.
    function setState(field, state) {
        var hadFocus = document.activeElement === field.control;
        if (state !== ENTERED) { hide(field); }
        field.state = state;
        field.control.hidden = state !== ENTERED;
        if (hadFocus && state !== ENTERED) {
            field.input.focus();
        }
    }

    // FR-004.2: text already there when the script starts, or restored on "back", is never ENTERED.
    function settle(field) {
        setState(field, isEmpty(field.input) && !isFilled(field.input) ? EMPTY : FILLED);
    }

    // FR-005: never cancels the event. Any other kind goes to FILLED here, so an undo paints no frame of a revealed value.
    function onBeforeInput(field, event) {
        var kind = event.inputType;
        if (isEntryKind(kind)) {
            if (isEmpty(field.input) && !isFilled(field.input)) {
                setState(field, EMPTY);
                field.awaitingEntry = true;
            } else if (field.state === EMPTY) {
                setState(field, FILLED);
            }
            return;
        }
        if (!isDeletionKind(kind) && kind !== ENTER_KIND) {
            setState(field, FILLED);
        }
    }

    // FR-006, in this order: a fill that arrives with an empty-looking value must not read as EMPTY. FR-006 step 3 is the
    // only route to ENTERED.
    function onInput(field, event) {
        var kind = event.inputType;
        if (isFilled(field.input) || kind === ENTER_KIND || !(isEntryKind(kind) || isDeletionKind(kind))) {
            setState(field, FILLED);
        } else if (isEmpty(field.input)) {
            setState(field, EMPTY);
        } else if (isEntryKind(kind) && field.state === EMPTY) {
            setState(field, field.awaitingEntry ? ENTERED : FILLED);
        } else if (isDeletionKind(kind) && field.state === EMPTY) {
            setState(field, FILLED);
        }
        field.awaitingEntry = false;
    }

    // FR-007.1
    function onChange(field) {
        if (isFilled(field.input)) {
            setState(field, FILLED);
        }
    }

    // FR-007.2: the last barrier against a fill that raised no event.
    function onPress(field) {
        if (isFilled(field.input)) {
            setState(field, FILLED);
            return;
        }
        if (isEmpty(field.input)) {
            setState(field, EMPTY);
            return;
        }
        if (field.state !== ENTERED) {
            return;
        }
        if (field.input.type === "password") {
            show(field);
        } else {
            hide(field);
        }
    }

    document.querySelectorAll(".password-field").forEach(function (wrapper) {
        var input = wrapper.querySelector("input");
        var control = wrapper.querySelector(".password-toggle");

        if (input === null || control === null) {
            return;
        }

        var field = { input: input, control: control, state: EMPTY, awaitingEntry: false };
        fields.push(field);

        input.addEventListener("beforeinput", function (event) { onBeforeInput(field, event); });
        input.addEventListener("input", function (event) { onInput(field, event); });
        input.addEventListener("change", function () { onChange(field); });
        control.addEventListener("click", function () { onPress(field); });

        // FR-004.1: whatever the state, so filling brings neither a jump in the text nor Edge's own reveal button.
        wrapper.classList.add("password-field--ready");
        settle(field);
    });

    // Any form on the page, the language switcher included.
    document.addEventListener("submit", hideAll);

    // Leaving with no form at all: a link, the address bar, a bookmark, the browser's own "back".
    window.addEventListener("pagehide", hideAll);

    // FR-004: a page restored from the browser's cache runs no script again, so its states are worked out anew.
    window.addEventListener("pageshow", function (event) {
        if (event.persisted) { fields.forEach(settle); }
    });
})();
