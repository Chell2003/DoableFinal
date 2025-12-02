/* Simple client-side table sorter
   - Automatically adds ▲ (asc) and ▼ (desc) controls to every table header cell
   - Click an arrow to sort by that column (text: A→Z / Z→A, numbers/dates: asc/desc)
   - Skip headers that have class 'no-sort'
   - Works on <table> elements with a <thead> and <tbody>
*/
(function () {
    'use strict';

    function detectType(value) {
        if (value === null || value === undefined) return { type: 'string', value: '' };
        var v = value.trim();
        if (v.length === 0) return { type: 'string', value: '' };

        // Date detection
        var date = Date.parse(v);
        if (!isNaN(date)) return { type: 'date', value: date };

        // Numeric detection (allow commas and currency symbols)
        var numeric = v.replace(/[^0-9.\-]/g, '');
        if (numeric !== '' && !isNaN(parseFloat(numeric))) return { type: 'number', value: parseFloat(numeric) };

        return { type: 'string', value: v.toLowerCase() };
    }

    function getCellText(cell) {
        if (!cell) return '';
        // Prefer textContent to include link text, ignore hidden elements
        return (cell.textContent || '').trim();
    }

    function addArrowsToHeader(th) {
        // Prevent double injection
        if (th.dataset.sortableAttached === '1') return;
        th.dataset.sortableAttached = '1';

        var container = document.createElement('span');
        container.className = 'table-sort-controls';
        container.style.marginLeft = '6px';
        container.style.fontSize = '0.9em';

        var up = document.createElement('a');
        up.href = 'javascript:void(0)';
        up.className = 'sort-asc';
        up.title = 'Sort A → Z / Ascending';
        up.style.marginRight = '2px';
        up.innerText = '▲';

        var down = document.createElement('a');
        down.href = 'javascript:void(0)';
        down.className = 'sort-desc';
        down.title = 'Sort Z → A / Descending';
        down.innerText = '▼';

        container.appendChild(up);
        container.appendChild(down);
        th.appendChild(container);

        th.style.cursor = 'pointer';
    }

    function clearActive(ths) {
        ths.forEach(function (h) {
            var asc = h.querySelector('.sort-asc');
            var desc = h.querySelector('.sort-desc');
            if (asc) asc.classList.remove('active');
            if (desc) desc.classList.remove('active');
        });
    }

    function sortTable(table, columnIndex, order) {
        var tbody = table.tBodies[0];
        if (!tbody) return;

        var rows = Array.from(tbody.rows);

        var sample = getCellText(rows[0] ? rows[0].cells[columnIndex] : null);
        var detected = detectType(sample);

        var mapped = rows.map(function (row, idx) {
            var cell = row.cells[columnIndex];
            var raw = getCellText(cell);
            var d = detectType(raw);
            var sortVal = d.value;
            // For mixed types, coerce to string if needed
            return { idx: idx, row: row, val: sortVal, type: d.type };
        });

        mapped.sort(function (a, b) {
            // if both numbers or dates
            if (a.type === 'number' && b.type === 'number') {
                return (a.val - b.val) * order;
            }
            if (a.type === 'date' && b.type === 'date') {
                return (a.val - b.val) * order;
            }

            // fallback to string compare
            var av = String(a.val);
            var bv = String(b.val);
            var res = av.localeCompare(bv, undefined, { numeric: true, sensitivity: 'base' });
            return res * order;
        });

        // Re-append rows in sorted order
        var frag = document.createDocumentFragment();
        mapped.forEach(function (m) { frag.appendChild(m.row); });
        tbody.appendChild(frag);
    }

    function attachToTable(table) {
        var thead = table.tHead;
        var tbody = table.tBodies[0];
        if (!thead || !tbody) return;

        var headers = Array.from(thead.rows[0].cells);
        headers.forEach(function (th, i) {
            // Skip if explicitly marked as no-sort
            if (th.classList.contains('no-sort')) return;

            // Skip columns that are clearly action columns (labelled "Action" or "Actions")
            var headerText = getCellText(th).toLowerCase();
            if (headerText === 'actions' || headerText === 'action') return;
            addArrowsToHeader(th);

            var asc = th.querySelector('.sort-asc');
            var desc = th.querySelector('.sort-desc');

            if (!asc || !desc) return;

            asc.addEventListener('click', function (e) {
                e.preventDefault();
                var allTh = Array.from(table.tHead.rows[0].cells);
                clearActive(allTh);
                asc.classList.add('active');
                sortTable(table, i, 1);
            });

            desc.addEventListener('click', function (e) {
                e.preventDefault();
                var allTh = Array.from(table.tHead.rows[0].cells);
                clearActive(allTh);
                desc.classList.add('active');
                sortTable(table, i, -1);
            });
        });
        // Initialize pagination for the table (skip if explicitly disabled)
        if (!table.classList.contains('no-paginate')) {
            setupPagination(table);
        }
    }

    /* Pagination functions */
    function createPageSizeSelector(table) {
        var select = document.createElement('select');
        select.className = 'table-page-size form-select form-select-sm';
        select.style.width = 'auto';
        select.style.display = 'inline-block';
        var options = [10, 25, 50, 100, 'All'];
        options.forEach(function (opt) {
            var o = document.createElement('option');
            o.value = opt === 'All' ? '0' : String(opt);
            o.text = opt === 'All' ? 'All' : String(opt);
            select.appendChild(o);
        });
        return select;
    }

    function setupPagination(table) {
        var tbody = table.tBodies[0];
        if (!tbody) return;

        // default page size (10)
        table._pageSize = parseInt(table.dataset.pageSize || '10', 10) || 10;
        table._currentPage = 0;

        // wrapper for controls
        var wrapper = document.createElement('div');
        wrapper.className = 'table-pagination-wrapper d-flex align-items-center justify-content-between mt-2';

        var left = document.createElement('div');
        left.className = 'table-page-left';
        var showText = document.createElement('span');
        showText.innerText = 'Show: ';
        left.appendChild(showText);

        var selector = createPageSizeSelector(table);
        // set initial value
        selector.value = table._pageSize === 0 ? '0' : String(table._pageSize);
        left.appendChild(selector);

        var right = document.createElement('div');
        right.className = 'table-page-right';
        var prev = document.createElement('button');
        prev.className = 'btn btn-sm btn-outline-secondary me-1 table-page-prev';
        prev.type = 'button';
        prev.innerText = '<';
        prev.setAttribute('aria-label', 'Previous page');

        var next = document.createElement('button');
        next.className = 'btn btn-sm btn-outline-secondary ms-1 table-page-next';
        next.type = 'button';
        next.innerText = '>';
        next.setAttribute('aria-label', 'Next page');

        var info = document.createElement('span');
        info.className = 'table-page-info ms-2';

        // Put the page info between the Prev and Next buttons (Prev [Page X of Y] Next)
        right.appendChild(prev);
        right.appendChild(info);
        right.appendChild(next);

        wrapper.appendChild(left);
        wrapper.appendChild(right);

        // place wrapper after the table
        table.parentNode.insertBefore(wrapper, table.nextSibling);

        // store references
        table._pagination = { wrapper: wrapper, selector: selector, prev: prev, next: next, info: info };

        selector.addEventListener('change', function () {
            var val = parseInt(selector.value || '10', 10);
            table._pageSize = isNaN(val) ? 0 : val; // 0 => All
            table._currentPage = 0;
            applyPagination(table);
        });

        prev.addEventListener('click', function () {
            if (table._currentPage > 0) {
                table._currentPage -= 1;
                applyPagination(table);
            }
        });

        next.addEventListener('click', function () {
            var rows = table.tBodies[0].rows.length;
            var pageCount = table._pageSize === 0 ? 1 : Math.ceil(rows / table._pageSize);
            if (table._currentPage < pageCount - 1) {
                table._currentPage += 1;
                applyPagination(table);
            }
        });

        // initial apply
        applyPagination(table);
    }

    function applyPagination(table) {
        var tbody = table.tBodies[0];
        var rows = Array.from(tbody.rows);
        var pageSize = table._pageSize || 0; // 0 means All
        var page = table._currentPage || 0;

        if (pageSize === 0) {
            // show all => present page as 1 of 1
            rows.forEach(function (r) { r.style.display = ''; });
            var totalPages = 1;
            table._currentPage = 0;
            if (table._pagination) {
                table._pagination.info.innerText = (table._currentPage + 1) + ' of ' + totalPages;
            }
            return;
        }

        var totalPages = Math.max(1, Math.ceil(rows.length / pageSize));
        if (page >= totalPages) page = totalPages - 1;
        table._currentPage = page;

        var start = page * pageSize;
        var end = start + pageSize;

        rows.forEach(function (r, idx) {
            if (idx >= start && idx < end) r.style.display = '';
            else r.style.display = 'none';
        });

        if (table._pagination) {
            table._pagination.info.innerText = (page + 1) + ' of ' + totalPages;
        }
    }

    // When sorting, reset to first page and reapply pagination if present
    var originalSortTable = sortTable;
    sortTable = function (table, columnIndex, order) {
        originalSortTable(table, columnIndex, order);
        if (table._pagination) {
            table._currentPage = 0;
            applyPagination(table);
        }
    };

    function init() {
        var tables = document.querySelectorAll('table');
        tables.forEach(function (t) { attachToTable(t); });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
