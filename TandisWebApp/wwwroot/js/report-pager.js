/* ============================================================
   report-pager.js — صفحه‌بندی خودکار لیست‌های بلند گزارش
   ✅ وقتی تعداد ردیف‌ها زیاد باشد لیست به صورت خودکار صفحه‌بندی
   می‌شود (بدون تغییر در کد رندر هر صفحه گزارش).
   نحوه کار: روی کانتینرها MutationObserver می‌گذاریم؛ هر بار که
   محتوای لیست عوض شد (رندر گزارش / جستجو / مرتب‌سازی) دوباره
   اعمال می‌کنیم. نوار صفحه‌بندی به عنوان برادر بعد از لیست درج
   می‌شود تا حلقهٔ observer ایجاد نشود.
   ============================================================ */
(function () {
    'use strict';

    var CFG = [
        { sel: '#repList',      item: '.rep-card', perPage: 10, search: '#searchInput' },
        { sel: '#trafficRows',  item: '.trf-card', perPage: 10, search: null },
        { sel: '#registerRows', item: '.trf-card', perPage: 10, search: null },
        { sel: '#financeRows',  item: 'tr',        perPage: 15, search: null }
    ];

    var state = {};

    function faNum(n) {
        return String(n).replace(/[0-9]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; });
    }

    function st(cfg) {
        if (!state[cfg.sel]) state[cfg.sel] = { page: 1, nav: null, anchor: null, pending: 0 };
        return state[cfg.sel];
    }

    function childrenOf(container, itemSel) {
        var out = [];
        for (var i = 0; i < container.children.length; i++) {
            if (container.children[i].matches && container.children[i].matches(itemSel)) {
                out.push(container.children[i]);
            }
        }
        return out;
    }

    /* نوار صفحه‌بندی را (یک بار) بعد از لیست می‌سازد */
    function ensureNav(cfg) {
        var s = st(cfg);
        if (s.nav && s.nav.isConnected) return s.nav;
        var container = document.querySelector(cfg.sel);
        if (!container) return null;
        var anchor = container.tagName === 'TBODY'
            ? (container.closest('table') || container)
            : container;
        if (!anchor.parentNode) return null;
        var nav = document.createElement('div');
        nav.className = 'rp-nav';
        nav.style.display = 'none';
        anchor.parentNode.insertBefore(nav, anchor.nextSibling);
        nav.addEventListener('click', function (e) {
            var b = e.target.closest && e.target.closest('[data-rp-page]');
            if (!b || b.disabled) return;
            var p = parseInt(b.getAttribute('data-rp-page'), 10);
            if (!isNaN(p)) goto(cfg, p);
        });
        s.nav = nav;
        s.anchor = anchor;
        return nav;
    }

    /* دکمه‌های شمارهٔ صفحه با «…» وقتی تعداد زیاد است */
    function pageButtons(cur, total) {
        if (total <= 7) {
            var all = [];
            for (var i = 1; i <= total; i++) all.push(i);
            return all;
        }
        var pages = [1];
        var from = Math.max(2, cur - 1);
        var to = Math.min(total - 1, cur + 1);
        if (from > 2) pages.push('…');
        for (var j = from; j <= to; j++) pages.push(j);
        if (to < total - 1) pages.push('…');
        pages.push(total);
        return pages;
    }

    function renderNav(cfg, totalMatched, pages) {
        var nav = ensureNav(cfg);
        if (!nav) return;
        var s = st(cfg);
        if (pages <= 1) { nav.style.display = 'none'; nav.innerHTML = ''; return; }

        var startIdx = (s.page - 1) * cfg.perPage + 1;
        var endIdx = Math.min(totalMatched, s.page * cfg.perPage);

        var html = '';
        html += '<button type="button" class="rp-btn" data-rp-page="' + (s.page - 1) + '"' +
                (s.page <= 1 ? ' disabled' : '') + '><i class="bi bi-chevron-double-right"></i> قبلی</button>';
        pageButtons(s.page, pages).forEach(function (p) {
            if (p === '…') { html += '<span class="rp-gap">…</span>'; return; }
            html += '<button type="button" class="rp-btn' + (p === s.page ? ' rp-active' : '') +
                    '" data-rp-page="' + p + '">' + faNum(p) + '</button>';
        });
        html += '<button type="button" class="rp-btn" data-rp-page="' + (s.page + 1) + '"' +
                (s.page >= pages ? ' disabled' : '') + '>بعدی <i class="bi bi-chevron-double-left"></i></button>';
        html += '<span class="rp-info">صفحه ' + faNum(s.page) + ' از ' + faNum(pages) +
                ' • نمایش ' + faNum(startIdx) + '–' + faNum(endIdx) + ' از ' + faNum(totalMatched) + '</span>';

        nav.innerHTML = html;
        nav.style.display = 'flex';
    }

    /* اعمال صفحه‌بندی روی کانتینر فعلی */
    function apply(cfg, resetPage) {
        var container = document.querySelector(cfg.sel);
        var s = st(cfg);
        if (!container) { if (s.nav) s.nav.style.display = 'none'; return; }

        var items = childrenOf(container, cfg.item);
        var q = '';
        if (cfg.search) {
            var inp = document.querySelector(cfg.search);
            q = inp ? String(inp.value || '').trim().toLowerCase() : '';
        }
        var matched = items.filter(function (el) {
            return !q || String(el.textContent || '').toLowerCase().indexOf(q) > -1;
        });

        var pages = Math.max(1, Math.ceil(matched.length / cfg.perPage));
        if (resetPage) s.page = 1;
        if (s.page > pages) s.page = pages;
        if (s.page < 1) s.page = 1;

        var onPage = new Set(matched.slice((s.page - 1) * cfg.perPage, s.page * cfg.perPage));
        var mset = new Set(matched);
        items.forEach(function (el) {
            el.style.display = (mset.has(el) && onPage.has(el)) ? '' : 'none';
        });

        renderNav(cfg, matched.length, pages);
    }

    function goto(cfg, p) {
        var s = st(cfg);
        s.page = p;
        apply(cfg, false);
        var a = s.anchor;
        if (a) {
            var r = a.getBoundingClientRect();
            if (r.top < 0 || r.top > window.innerHeight * 0.5) {
                try { a.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
                catch (e) { a.scrollIntoView(); }
            }
        }
    }

    function init() {
        CFG.forEach(function (cfg) {
            var container = document.querySelector(cfg.sel);
            if (!container) return;
            st(cfg);
            var mo = new MutationObserver(function () {
                var s = st(cfg);
                if (s.pending) cancelAnimationFrame(s.pending);
                s.pending = requestAnimationFrame(function () {
                    s.pending = 0;
                    apply(cfg, true);   // محتوای جدید → برگشت به صفحهٔ ۱
                });
            });
            mo.observe(container, { childList: true });
            apply(cfg, false);
        });

        /* جستجوی صفحات گزارش: بعد از هندلر خود صفحه اجرا می‌شود
           (phase target صفحه، bubble اینجا) پس همیشه حرف آخر را
           صفحه‌بندی می‌زند و نتیجهٔ نهایی درست می‌ماند */
        document.addEventListener('keyup', function (e) {
            var t = e.target;
            if (!t || t.id !== 'searchInput') return;
            CFG.forEach(function (cfg) {
                if (cfg.search && document.querySelector(cfg.sel)) {
                    st(cfg).page = 1;
                    apply(cfg, false);
                }
            });
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
