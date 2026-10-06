// US-028: the journal export button on the report page (api-design §2.6, §2.7).
//
// Posts the validated values the server rendered into data attributes as JSON, with the antiforgery token in the
// RequestVerificationToken header, and saves the answer under the name in Content-Disposition. A static file, so
// the Content-Security-Policy needs no 'unsafe-inline'. Nothing is sent anywhere but the installation itself.
(function () {
    "use strict";

    var button = document.getElementById("report-export-button");
    var errors = document.getElementById("report-export-error");
    var tokenMeta = document.querySelector("meta[name='request-verification-token']");

    if (button === null || errors === null || tokenMeta === null) {
        return;
    }

    function showErrors(messages) {
        errors.textContent = messages.join(" ");
    }

    function selectedOrientation() {
        var checked = document.querySelector("input[name='orientation']:checked");
        return checked === null ? "portrait" : checked.value;
    }

    // RFC 6266: filename*=UTF-8''... is preferred, filename="..." is the ASCII fallback.
    function fileNameOf(header) {
        if (header === null) {
            return "journal.xlsx";
        }
        var star = /filename\*\s*=\s*UTF-8''([^;]+)/i.exec(header);
        if (star !== null) {
            try {
                return decodeURIComponent(star[1].trim());
            } catch (e) {
                // fall through to the plain name
            }
        }
        var plain = /filename\s*=\s*"?([^";]+)"?/i.exec(header);
        return plain !== null ? plain[1].trim() : "journal.xlsx";
    }

    function save(blob, name) {
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    }

    function messagesOf(body) {
        var messages = [];
        if (body !== null && typeof body === "object") {
            if (typeof body.message === "string") {
                messages.push(body.message);
            }
            if (Array.isArray(body.fieldErrors)) {
                body.fieldErrors.forEach(function (error) {
                    if (error !== null && typeof error.message === "string") {
                        messages.push(error.message);
                    }
                });
            }
        }
        return messages;
    }

    button.addEventListener("click", function () {
        showErrors([]);
        button.disabled = true;

        var payload = {
            template: button.getAttribute("data-template"),
            courseId: button.getAttribute("data-course-id"),
            from: button.getAttribute("data-from"),
            to: button.getAttribute("data-to"),
            names: button.getAttribute("data-names"),
            orientation: selectedOrientation()
        };

        fetch("/api/v1/exports/journal-xlsx", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": tokenMeta.getAttribute("content")
            },
            body: JSON.stringify(payload)
        }).then(function (response) {
            if (response.ok) {
                var name = fileNameOf(response.headers.get("Content-Disposition"));
                return response.blob().then(function (blob) {
                    save(blob, name);
                });
            }
            return response.json().then(
                function (body) {
                    showErrors(messagesOf(body));
                },
                function () {
                    showErrors([]);
                });
        }).catch(function () {
            showErrors([]);
        }).then(function () {
            button.disabled = false;
        });
    });
})();
