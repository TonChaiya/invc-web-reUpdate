// Inventory Status — lazy lot expander ("ดูล็อต" per row, "แสดงล็อตทั้งหมด / ซ่อนล็อตทั้งหมด" for the whole list).
// Per row: the first click fetches that medicine's lot partial (GET ?handler=Lots&workingCode=…) and marks the slot
// loaded; later clicks only toggle visibility. Show-all: ONE request (GET ?handler=AllLots&q=…) fills every slot,
// then the button toggles all panels. Without JavaScript the list works unchanged (buttons are inert / hidden).
(function () {
    'use strict';
    var table = document.querySelector('.inventory-table');
    if (!table) { return; }

    var LABEL_OPEN = 'ดูล็อต', LABEL_CLOSE = 'ซ่อนล็อต', LABEL_LOADING = 'กำลังโหลด…';
    var ALL_OPEN = 'แสดงล็อตทั้งหมด', ALL_CLOSE = 'ซ่อนล็อตทั้งหมด', ALL_LOADING = 'กำลังโหลดล็อต…';
    var ERROR_TEXT = 'ไม่สามารถโหลดข้อมูลล็อตได้ กรุณาลองใหม่';
    var basePath = window.location.pathname;

    function toggles() { return Array.prototype.slice.call(table.querySelectorAll('.inventory-lot-toggle')); }
    function panelRow(button) { return document.getElementById(button.getAttribute('aria-controls')); }
    function slotOf(row) { return row.querySelector('[data-lot-slot]'); }

    function setState(button, expanded) {
        button.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        button.querySelector('.inventory-lot-toggle-text').textContent = expanded ? LABEL_CLOSE : LABEL_OPEN;
        button.closest('tr').classList.toggle('inventory-row--open', expanded);
        syncAllButton();
    }

    function show(button, visible) {
        var row = panelRow(button);
        if (!row) { return; }
        row.hidden = !visible;
        setState(button, visible);
    }

    function errorPanel() {
        return '<div class="inventory-lot-panel inventory-lot-panel--error" role="alert">' + ERROR_TEXT + '</div>';
    }

    function loadOne(button) {
        var row = panelRow(button), slot = slotOf(row);
        var code = button.getAttribute('data-working-code');
        button.disabled = true;
        button.querySelector('.inventory-lot-toggle-text').textContent = LABEL_LOADING;
        slot.innerHTML = '<div class="inventory-lot-panel inventory-lot-panel--loading" role="status">กำลังโหลดข้อมูลล็อต…</div>';
        row.hidden = false;
        return fetch(basePath + '?handler=Lots&workingCode=' + encodeURIComponent(code), { headers: { 'Accept': 'text/html' }, credentials: 'same-origin' })
            .then(function (r) { if (!r.ok) { throw new Error('HTTP ' + r.status); } return r.text(); })
            .then(function (html) {
                slot.innerHTML = html;
                slot.setAttribute('data-loaded', 'true');
            })
            .catch(function () {
                slot.innerHTML = errorPanel();
                slot.setAttribute('data-loaded', 'false');   // a retry re-fetches
            })
            .then(function () { button.disabled = false; setState(button, true); });
    }

    // ---- per-row toggle ----
    table.addEventListener('click', function (e) {
        var button = e.target.closest('.inventory-lot-toggle');
        if (!button) { return; }
        var row = panelRow(button);
        if (!row) { return; }
        if (button.getAttribute('aria-expanded') === 'true') { show(button, false); return; }
        if (slotOf(row).getAttribute('data-loaded') === 'true') { show(button, true); return; }
        loadOne(button);
    });

    // ---- show all / hide all ----
    var allButton = document.getElementById('inventory-lots-all');
    if (!allButton) { return; }
    allButton.hidden = false;                       // only useful with JavaScript
    var allText = allButton.querySelector('.inventory-lots-all-text');
    var allLoaded = false;

    function syncAllButton() {
        if (!allButton || !allText) { return; }
        var list = toggles();
        if (!list.length) { return; }
        var anyClosed = list.some(function (b) { return b.getAttribute('aria-expanded') !== 'true'; });
        allText.textContent = anyClosed ? ALL_OPEN : ALL_CLOSE;
        allButton.setAttribute('aria-pressed', anyClosed ? 'false' : 'true');
    }

    function loadAll() {
        allButton.disabled = true;
        allText.textContent = ALL_LOADING;
        var q = allButton.getAttribute('data-q') || '', loc = allButton.getAttribute('data-loc') || '';
        return fetch(basePath + '?handler=AllLots&q=' + encodeURIComponent(q) + '&loc=' + encodeURIComponent(loc), { headers: { 'Accept': 'text/html' }, credentials: 'same-origin' })
            .then(function (r) { if (!r.ok) { throw new Error('HTTP ' + r.status); } return r.text(); })
            .then(function (html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                toggles().forEach(function (button) {
                    var row = panelRow(button), slot = slotOf(row);
                    var block = doc.querySelector('[data-lots-for="' + button.getAttribute('data-working-code') + '"]');
                    if (block) { slot.innerHTML = block.innerHTML; slot.setAttribute('data-loaded', 'true'); }
                    else if (slot.getAttribute('data-loaded') !== 'true') { slot.innerHTML = errorPanel(); }
                });
                allLoaded = true;
            })
            .catch(function () {
                toggles().forEach(function (button) {
                    var slot = slotOf(panelRow(button));
                    if (slot.getAttribute('data-loaded') !== 'true') { slot.innerHTML = errorPanel(); }
                });
            })
            .then(function () { allButton.disabled = false; });
    }

    allButton.addEventListener('click', function () {
        var list = toggles();
        var anyClosed = list.some(function (b) { return b.getAttribute('aria-expanded') !== 'true'; });
        if (!anyClosed) { list.forEach(function (b) { show(b, false); }); return; }
        var ready = allLoaded ? Promise.resolve() : loadAll();
        ready.then(function () { list.forEach(function (b) { show(b, true); }); });
    });
})();
