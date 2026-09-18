// Dashboard — monthly movement category expander. The month cell is the single expand control:
// clicking it toggles the "แยกหมวดเวชภัณฑ์" row directly below (aria-expanded / aria-controls / hidden).
// Presentation only: every number is rendered server-side.
(function () {
    'use strict';
    var table = document.querySelector('.dashboard-movement-table');
    if (!table) { return; }

    function toggle(button) {
        var panel = document.getElementById(button.getAttribute('aria-controls'));
        if (!panel) { return; }
        var open = button.getAttribute('aria-expanded') === 'true';
        panel.hidden = open;
        button.setAttribute('aria-expanded', open ? 'false' : 'true');
        button.closest('tr').classList.toggle('is-open', !open);
    }

    table.addEventListener('click', function (e) {
        var button = e.target.closest('.dashboard-month-toggle');
        if (button && table.contains(button)) { toggle(button); }
    });
})();
