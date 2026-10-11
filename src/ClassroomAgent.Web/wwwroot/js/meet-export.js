// US-044: the Meet export button on the Meet reports page and on the meeting page.
//
// Posts the validated values the server rendered into data attributes as JSON, with the antiforgery token in the
// RequestVerificationToken header, and saves the answer under the name in Content-Disposition. A static file, so
// the Content-Security-Policy needs no 'unsafe-inline'. Nothing is sent anywhere but the installation itself.
(function () {
    "use strict";

    var button = document.getElementById("meet-export-button");
    var errors = document.getElementById("meet-export-error");
    var tokenMeta = document.querySelector("meta[name='request-verification-token']");

    if (button === null || errors === null || tokenMeta === null) {
        return;
    }

    var defaultName = "meet.xlsx";

    function showErrors(messages) {
        errors.textContent = messages.join(" ");
    }

    function selectedOrientation() {
        var checked = document.querySelector("input[name='orientation']:checked");
        return checked === null ? "landscape" : checked.value;
    }

    // RFC 6266: filename*=UTF-8''... is preferred, filename="..." is the ASCII fallback.
    function fileNameOf(header) {
        if (header === null) {
            return defaultName;
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
        return plain !== null ? plain[1].trim() : defaultName;
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

    function requestOf() {
        var orientation = selectedOrientation();
        if (button.getAttribute("data-kind") === "meeting") {
            return {
                url: "/api/v1/exports/meet-meeting-xlsx",
                payload: {
                    courseId: button.getAttribute("data-course-id"),
                    meetingId: button.getAttribute("data-meeting-id"),
                    orientation: orientation
                }
            };
        }
        return {
            url: "/api/v1/exports/meet-reports-xlsx",
            payload: {
                courseId: button.getAttribute("data-course-id"),
                from: button.getAttribute("data-from"),
                to: button.getAttribute("data-to"),
                orientation: orientation
            }
        };
    }

    button.addEventListener("click", function () {
        showErrors([]);
        button.disabled = true;

        var request = requestOf();

        fetch(request.url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": tokenMeta.getAttribute("content")
            },
            body: JSON.stringify(request.payload)
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
