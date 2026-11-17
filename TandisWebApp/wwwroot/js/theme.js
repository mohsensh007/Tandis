/* ============================================================
   سوییچ تم روشن/تاریک — Arctic Blue  <->  Midnight Blue
   - ذخیره در localStorage
   - تشخیص حالت سیستم در اولین بازدید
   - دکمهٔ خورشید/ماه در بالای سمت چپ همهٔ صفحه‌ها
   ============================================================ */
(function () {
    'use strict';

    var KEY = 'tandis-theme';
    var root = document.documentElement;

    function systemTheme() {
        try {
            return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        } catch (e) { return 'light'; }
    }

    function stored() {
        try { return localStorage.getItem(KEY); } catch (e) { return null; }
    }

    function apply(theme, animate) {
        var t = theme === 'dark' ? 'dark' : 'light';
        root.setAttribute('data-theme', t);

        if (animate) {
            root.classList.add('theme-anim');
            window.setTimeout(function () { root.classList.remove('theme-anim'); }, 420);
        }

        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', t === 'dark' ? '#0B1220' : '#EFF6FF');

        document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
            btn.setAttribute('aria-pressed', t === 'dark' ? 'true' : 'false');
            btn.setAttribute('title', t === 'dark' ? 'حالت روشن' : 'حالت تاریک');
            btn.setAttribute('aria-label', t === 'dark' ? 'تغییر به حالت روشن' : 'تغییر به حالت تاریک');
            var icon = btn.querySelector('i');
            if (icon) icon.className = 'bi ' + (t === 'dark' ? 'bi-moon-stars-fill' : 'bi-sun-fill');
        });
    }

    function current() {
        var t = root.getAttribute('data-theme');
        if (t === 'dark' || t === 'light') return t;
        return stored() || systemTheme();
    }

    function toggle() {
        var next = current() === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem(KEY, next); } catch (e) { }
        apply(next, true);
    }

    /* دکمهٔ شناور برای صفحاتی که دکمهٔ آماده ندارند */
    function ensureButton() {
        var existing = document.querySelectorAll('[data-theme-toggle]');
        if (existing.length) return;

        var btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'theme-toggle theme-toggle-floating';
        btn.setAttribute('data-theme-toggle', '');
        btn.innerHTML = '<i class="bi bi-sun-fill" aria-hidden="true"></i>';
        btn.addEventListener('click', toggle);
        document.body.appendChild(btn);
    }

    function init() {
        apply(current(), false);
        ensureButton();
        document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
            if (btn.getAttribute('data-theme-bound') === '1') return;
            btn.setAttribute('data-theme-bound', '1');
            btn.addEventListener('click', toggle);
        });
        apply(current(), false);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    window.TandisTheme = { toggle: toggle, apply: function (t) { apply(t, true); } };
})();
