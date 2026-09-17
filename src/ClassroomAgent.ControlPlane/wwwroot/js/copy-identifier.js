// Copy button of the installation detail page (US-002 FR-005, spec I-6). Copies the text of the
// element named by data-copy-target with the Clipboard API and shows the button's data-copied-text
// beside it. Every visible text comes from the page; without this script the value stays selectable.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('button[data-copy-target]').forEach(function (button) {
        var targetId = button.getAttribute('data-copy-target');
        button.addEventListener('click', function () {
            var target = document.getElementById(targetId);
            if (!target || !navigator.clipboard) {
                return;
            }

            navigator.clipboard.writeText(target.textContent.trim()).then(function () {
                var status = document.querySelector('[data-copy-status="' + targetId + '"]');
                if (status) {
                    status.textContent = button.getAttribute('data-copied-text') || '';
                }
            });
        });
    });
});
