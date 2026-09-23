// Borrow facility — inline return without leaving the page.
// The row's little form posts to the page's "Return" handler; the server validates, writes the audit event and
// re-renders the whole body region (summary, status counts, bill totals, item rows). This script only swaps that
// HTML in and restores focus. Without JavaScript the same form posts normally and the handler redirects back here,
// so nothing depends on the browser: quantities, statuses and the actor are always computed on the server.
(function () {
    'use strict';
    var body = document.getElementById('borrow-body');
    if (!body || !window.fetch) { return; }

    var BUSY = 'กำลังบันทึก…';

    function setBusy(form, busy) {
        var button = form.querySelector('button[type="submit"]');
        var qty = form.querySelector('.borrow-quick-qty');
        if (button) {
            if (busy) { button.dataset.label = button.textContent; button.textContent = BUSY; }
            else if (button.dataset.label) { button.textContent = button.dataset.label; }
            button.disabled = busy;
        }
        if (qty) { qty.disabled = busy; }
        form.classList.toggle('is-busy', busy);
    }

    function focusAfterSwap(itemId) {
        var row = itemId ? document.getElementById('item-' + itemId) : null;
        var target = (row && row.querySelector('.borrow-quick-qty')) || body.querySelector('.borrow-live-notice');
        if (target && target.focus) { target.setAttribute('tabindex', target.tabIndex < 0 ? '-1' : target.tabIndex); target.focus({ preventScroll: true }); }
    }

    body.addEventListener('submit', function (e) {
        var form = e.target.closest('.borrow-quick-return');
        if (!form || !body.contains(form)) { return; }
        if (typeof form.reportValidity === 'function' && !form.reportValidity()) { return; }
        e.preventDefault();

        var itemId = (form.querySelector('[name="Quick.ItemRecordNumber"]') || {}).value;
        var scroll = window.scrollY;
        setBusy(form, true);

        fetch(form.action, {
            method: 'POST',
            body: new FormData(form),
            headers: { 'X-Requested-With': 'fetch', 'Accept': 'text/html' },
            credentials: 'same-origin',
        })
            .then(function (r) { if (!r.ok) { throw new Error('HTTP ' + r.status); } return r.text(); })
            .then(function (html) {
                body.innerHTML = html;
                window.scrollTo(0, scroll);
                focusAfterSwap(itemId);
            })
            .catch(function () {
                setBusy(form, false);
                var notice = document.createElement('div');
                notice.className = 'ds-error borrow-live-notice';
                notice.setAttribute('role', 'alert');
                notice.textContent = 'บันทึกไม่สำเร็จ กรุณาลองใหม่ หรือใช้หน้า “ระบุวันที่/หมายเหตุ…”';
                body.insertBefore(notice, body.firstChild);
            });
    });
})();
