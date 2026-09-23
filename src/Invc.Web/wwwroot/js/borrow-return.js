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

    function swap(html, itemId) {
        body.innerHTML = html;
        focusAfterSwap(itemId);
    }

    function token() {
        var input = body.querySelector('#borrow-antiforgery input[name="__RequestVerificationToken"]')
            || body.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function post(url, data, scroll, itemId, onError) {
        return fetch(url, {
            method: 'POST',
            body: data,
            headers: { 'X-Requested-With': 'fetch', 'Accept': 'text/html' },
            credentials: 'same-origin',
        })
            .then(function (r) { if (!r.ok) { throw new Error('HTTP ' + r.status); } return r.text(); })
            .then(function (html) { swap(html, itemId); window.scrollTo(0, scroll); })
            .catch(onError);
    }

    function failure(message) {
        return function () {
            var notice = document.createElement('div');
            notice.className = 'ds-error borrow-live-notice';
            notice.setAttribute('role', 'alert');
            notice.textContent = message;
            body.insertBefore(notice, body.firstChild);
        };
    }

    // bill-level "คืนครบทั้งบิล": confirm, then record every open item of the bill in one transaction, in place.
    // Without JavaScript the same link opens /Borrow/ReturnBill, which asks for confirmation on its own page.
    body.addEventListener('click', function (e) {
        var link = e.target.closest('.borrow-bill-return');
        if (!link || !body.contains(link)) { return; }
        var receiveNo = link.getAttribute('data-receive-no') || '';
        var outstanding = link.getAttribute('data-outstanding') || '';
        var items = link.getAttribute('data-items') || '';
        var question = 'ยืนยันบันทึกคืนครบทั้งบิล ' + receiveNo + '\n' + 'จำนวนคงค้างทั้งหมด ' + outstanding + ' (' + items + ' รายการ)';
        if (!window.confirm(question)) {
            e.preventDefault();
            return;
        }
        e.preventDefault();
        var data = new FormData();
        data.append('__RequestVerificationToken', token());
        data.append('billRecordNumber', link.getAttribute('data-bill'));
        data.append('clientRequestId', (window.crypto && window.crypto.randomUUID) ? window.crypto.randomUUID() : String(Date.now()) + '-' + Math.random().toString(16).slice(2));
        link.classList.add('is-busy');
        var url = window.location.pathname + window.location.search + (window.location.search ? '&' : '?') + 'handler=ReturnBill';
        post(url, data, window.scrollY, null, function () {
            link.classList.remove('is-busy');
            failure('บันทึกไม่สำเร็จ กรุณาลองใหม่ หรือใช้หน้ายืนยันคืนครบทั้งบิล')();
        });
    });

    body.addEventListener('submit', function (e) {
        var form = e.target.closest('.borrow-quick-return');
        if (!form || !body.contains(form)) { return; }
        if (typeof form.reportValidity === 'function' && !form.reportValidity()) { return; }
        e.preventDefault();

        var itemId = (form.querySelector('[name="Quick.ItemRecordNumber"]') || {}).value;
        var scroll = window.scrollY;
        setBusy(form, true);

        post(form.action, new FormData(form), scroll, itemId, function () {
            setBusy(form, false);
            failure('บันทึกไม่สำเร็จ กรุณาลองใหม่ หรือใช้หน้า “ระบุวันที่/หมายเหตุ…”')();
        });
    });
})();
