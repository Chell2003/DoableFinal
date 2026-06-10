// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

/**
 * Mobile table labels
 * Reads each table's <thead> column text and writes it as data-label
 * on every matching <td> so the CSS ::before pseudo-element can display it.
 * Runs on DOM ready and again after any pagination change.
 */
(function applyMobileTableLabels() {
    function labelTable(table) {
        var ths = Array.from(table.querySelectorAll('thead th'));
        if (!ths.length) return;

        var labels = ths.map(function (th) {
            // Strip sort-arrow text (span.sort-asc / span.sort-desc children)
            var clone = th.cloneNode(true);
            clone.querySelectorAll('.sort-asc, .sort-desc, i, svg').forEach(function (el) {
                el.remove();
            });
            return clone.textContent.trim();
        });

        Array.from(table.querySelectorAll('tbody tr')).forEach(function (tr) {
            Array.from(tr.querySelectorAll('td')).forEach(function (td, i) {
                if (labels[i]) {
                    td.setAttribute('data-label', labels[i]);
                }
            });
        });
    }

    function labelAllTables() {
        document.querySelectorAll('table.table').forEach(labelTable);
    }

    // Run on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', labelAllTables);
    } else {
        labelAllTables();
    }

    // Re-run after pagination renders new rows (MutationObserver)
    var observer = new MutationObserver(function (mutations) {
        mutations.forEach(function (m) {
            m.addedNodes.forEach(function (node) {
                if (node.nodeType !== 1) return;
                // If a tbody or tr was added, re-label its parent table
                var table = node.closest
                    ? node.closest('table.table')
                    : null;
                if (table) labelTable(table);
                // Also check if a table was added inside (e.g. dynamic widgets)
                node.querySelectorAll && node.querySelectorAll('table.table').forEach(labelTable);
            });
        });
    });

    document.addEventListener('DOMContentLoaded', function () {
        observer.observe(document.body, { childList: true, subtree: true });
    });
})();
