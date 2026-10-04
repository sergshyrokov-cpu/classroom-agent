// US-027 api-design §2.8: add and remove rows of the grading-scale table and fill the 12-point preset.
//
// Progressive enhancement, and nothing more. The form is complete without it: the existing rows can still be
// edited and saved. A static same-origin file, never inline script, so a Content-Security-Policy needs no
// 'unsafe-inline'. The preset values are rendered by the server into data-preset (from Application); this file holds
// none of them. It contacts no service and sends nothing anywhere.
(function () {
    "use strict";

    var section = document.getElementById("scale-section");
    var body = document.getElementById("scale-rows");
    var mode = document.getElementById("scale-mode");
    var rowsSection = document.getElementById("scale-rows-section");
    var addButton = document.getElementById("scale-add-row");
    var presetButton = document.getElementById("scale-preset");

    if (section === null || body === null || mode === null || rowsSection === null
        || addButton === null || presetButton === null) {
        return;
    }

    var preset = [];
    try {
        preset = JSON.parse(section.getAttribute("data-preset") || "[]");
    } catch (error) {
        preset = [];
    }

    var removeLabel = body.getAttribute("data-remove-label") || "";

    function cell(name, value, numeric) {
        var td = document.createElement("td");
        var input = document.createElement("input");
        input.type = "text";
        if (numeric) {
            input.setAttribute("inputmode", "numeric");
        }
        input.name = name;
        input.value = value;
        td.appendChild(input);
        return td;
    }

    // Row indexes are kept dense (0, 1, 2 ...) after every change, so the posted names stay tidy.
    function renumber() {
        var rows = body.querySelectorAll("tr");
        for (var i = 0; i < rows.length; i++) {
            var inputs = rows[i].querySelectorAll("input");
            var members = ["from", "to", "label"];
            for (var j = 0; j < inputs.length && j < members.length; j++) {
                inputs[j].name = "scale[" + i + "]." + members[j];
            }
        }
    }

    function addRow(from, to, label) {
        var tr = document.createElement("tr");
        tr.appendChild(cell("scale[0].from", from, true));
        tr.appendChild(cell("scale[0].to", to, true));
        tr.appendChild(cell("scale[0].label", label, false));

        var action = document.createElement("td");
        var remove = document.createElement("button");
        remove.type = "button";
        remove.textContent = removeLabel;
        remove.addEventListener("click", function () {
            body.removeChild(tr);
            renumber();
        });
        action.appendChild(remove);
        tr.appendChild(action);

        body.appendChild(tr);
        renumber();
    }

    function clearRows() {
        while (body.firstChild !== null) {
            body.removeChild(body.firstChild);
        }
    }

    function showRowsOnlyForRanges() {
        var ranges = mode.value === "ranges";
        rowsSection.hidden = !ranges;
        addButton.hidden = !ranges;
        presetButton.hidden = !ranges;
    }

    // Rows rendered by the server get a remove button too.
    var existing = body.querySelectorAll("tr");
    for (var i = 0; i < existing.length; i++) {
        (function (tr) {
            var action = tr.lastElementChild;
            var remove = document.createElement("button");
            remove.type = "button";
            remove.textContent = removeLabel;
            remove.addEventListener("click", function () {
                body.removeChild(tr);
                renumber();
            });
            action.appendChild(remove);
        })(existing[i]);
    }

    addButton.addEventListener("click", function () {
        addRow("", "", "");
    });

    presetButton.addEventListener("click", function () {
        clearRows();
        for (var k = 0; k < preset.length; k++) {
            addRow(String(preset[k].from), String(preset[k].to), String(preset[k].label));
        }
    });

    mode.addEventListener("change", showRowsOnlyForRanges);
    showRowsOnlyForRanges();
})();
