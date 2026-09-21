// US-010 OD-002: copy the super-admin instruction to the clipboard.
//
// Progressive enhancement, and nothing more. The instruction is already complete, selectable and printable in
// the markup the server sent; this only saves the Admin a selection. Loaded as a static file rather than inline
// script, so a Content-Security-Policy added later needs no 'unsafe-inline' (US-010 spec I-5).
//
// It contacts no service and sends nothing anywhere: what the school's super-admin receives is what the Admin
// themselves sends (SC-13). The copied text is exactly what the page shows — the same element, read once.
(function () {
    "use strict";

    var button = document.getElementById("copy-instruction");
    var instruction = document.getElementById("connection-instruction");

    if (button === null || instruction === null) {
        return;
    }

    // With no clipboard available the affordance removes itself rather than failing on a click: the text stays
    // selectable, so the Admin is never left without it (OD-002).
    if (navigator.clipboard === undefined || typeof navigator.clipboard.writeText !== "function") {
        button.hidden = true;
        return;
    }

    button.addEventListener("click", function () {
        navigator.clipboard.writeText(instruction.textContent).then(
            function () {
                var confirmation = button.getAttribute("data-confirmation");
                if (confirmation !== null) {
                    button.textContent = confirmation;
                }
            },
            function () {
                // A refused clipboard is not an error worth a dialog: the text is still on the page.
                button.hidden = true;
            });
    });
})();
