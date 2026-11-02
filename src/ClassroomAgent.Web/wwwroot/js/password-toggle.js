// US-069 FR-005, FR-006, SC-2: the eye control of a password field. It only switches the input's type and the control's
// state: it never touches what was typed, sends nothing anywhere and stores nothing. A static file, so the
// Content-Security-Policy needs no 'unsafe-inline'. Until it runs the control stays hidden and the field works as usual.
(function () {
    "use strict";

    var fields = [];

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

    // FR-006.4: every shown field goes back to hidden before the page leaves, so the browser neither stores the text as
    // ordinary text nor restores it visible on "back".
    function hideAll() {
        fields.forEach(hide);
    }

    document.querySelectorAll(".password-field").forEach(function (wrapper) {
        var input = wrapper.querySelector("input");
        var control = wrapper.querySelector(".password-toggle");

        if (input === null || control === null) {
            return;
        }

        var field = { input: input, control: control };
        fields.push(field);

        control.addEventListener("click", function () {
            if (input.type === "password") {
                show(field);
            } else {
                hide(field);
            }
        });

        control.removeAttribute("hidden");
        wrapper.classList.add("password-field--ready");
    });

    // Any form on the page, the language switcher included.
    document.addEventListener("submit", hideAll);

    // Leaving with no form at all: a link, the address bar, a bookmark, the browser's own "back".
    window.addEventListener("pagehide", hideAll);
})();
