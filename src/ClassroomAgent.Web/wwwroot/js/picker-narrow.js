// US-070 FR-009, BR-087, SC-15: narrows a long picker as the user types. It reads the rows already in the page, sends
// nothing and stores nothing. The field has no name, so its text is never submitted. A static file, so the
// Content-Security-Policy needs no 'unsafe-inline'. Until it runs the picker is what it is without scripts.
(function () {
    "use strict";

    // FR-001 rules 4, 5: case does not matter, and the three apostrophes count as one.
    function fold(text) {
        return text.toLowerCase().replace(/[’ʼ]/g, "'");
    }

    // FR-001 rules 1 to 3, 7: every word is a contiguous, literal part of the row's text.
    function matches(words, text) {
        var folded = fold(text);
        return words.every(function (word) {
            return folded.indexOf(word) !== -1;
        });
    }

    function wordsOf(value) {
        return fold(value).split(/\s+/).filter(function (word) {
            return word.length > 0;
        });
    }

    // FR-009 items 1, 6: a label and a text field above the picker, with a polite message when nothing matches.
    function buildField(owner, id, place) {
        var wrapper = document.createElement("p");
        var label = document.createElement("label");
        var input = document.createElement("input");
        var message = document.createElement("span");

        label.className = "form-label";
        label.htmlFor = id;
        label.textContent = owner.dataset.narrowLabel;

        input.id = id;
        input.type = "search";
        input.className = "form-control";
        input.maxLength = 100;
        input.setAttribute("autocomplete", "off");
        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
            }
        });

        message.className = "form-text";
        message.setAttribute("aria-live", "polite");

        wrapper.appendChild(label);
        wrapper.appendChild(input);
        wrapper.appendChild(message);
        place(wrapper);

        return {
            input: input,
            show: function (nothingFound) {
                message.textContent = nothingFound ? owner.dataset.narrowNone : "";
            },
        };
    }

    // FR-009 items 2, 4: rows are removed, not styled, as some phones ignore a style on an option. Re-appending in the
    // original order brings them back in that order. The empty choice and the selected row always stay.
    function narrowSelect(select) {
        var options = Array.prototype.slice.call(select.options);
        var field = buildField(select, select.id + "-narrow", function (wrapper) {
            select.parentNode.parentNode.insertBefore(wrapper, select.parentNode);
        });

        field.input.addEventListener("input", function () {
            var words = wordsOf(field.input.value);
            var selected = select.value;
            var found = 0;

            options.forEach(function (option) {
                var isMatch = option.value !== "" && matches(words, option.textContent);
                found += isMatch ? 1 : 0;
                if (isMatch || option.value === "" || option.value === selected) {
                    select.appendChild(option);
                } else {
                    option.remove();
                }
            });

            field.show(words.length > 0 && found === 0);
        });
    }

    // FR-009 items 3 to 5, 8: one field for both blocks; a block with no row left is hidden, and while no row is left to
    // choose the submit button is disabled, so the form cannot be sent without a course.
    function narrowRadios(form) {
        var blocks = Array.prototype.map.call(form.querySelectorAll("fieldset"), function (block) {
            return {
                element: block,
                rows: Array.prototype.slice.call(block.querySelectorAll("[data-narrow-row]")),
            };
        });
        var submit = form.querySelector("#meet-code-choice-submit");
        var field = buildField(form, form.id + "-narrow", function (wrapper) {
            form.insertBefore(wrapper, blocks[0].element);
        });

        field.input.addEventListener("input", function () {
            var words = wordsOf(field.input.value);
            var found = 0;
            var left = 0;

            blocks.forEach(function (block) {
                var shown = 0;
                block.rows.forEach(function (row) {
                    var isMatch = matches(words, row.dataset.narrowText);
                    var radio = row.querySelector("input[type=radio]");
                    found += isMatch ? 1 : 0;
                    if (isMatch || radio.checked) {
                        block.element.appendChild(row);
                        shown += 1;
                    } else {
                        row.remove();
                    }
                });
                block.element.hidden = shown === 0;
                left += shown;
            });

            submit.disabled = left === 0;
            field.show(words.length > 0 && found === 0);
        });
    }

    document.querySelectorAll("[data-narrow]").forEach(function (owner) {
        if (owner.dataset.narrow === "select") {
            narrowSelect(owner);
        } else if (owner.dataset.narrow === "radios") {
            narrowRadios(owner);
        }
    });
})();
