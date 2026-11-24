/* ============================================================================
   فیش تراکنش میز خدمت
   - بعد از هر تراکنش موفق (شارژ اعتبار، بلیت، تک جلسه، تمدید/ثبت‌نام، فروشگاه، بوفه، خدمات)
     فیش روی صفحه نمایش داده می‌شود و به‌صورت خودکار روی پرینتر حرارتی POS
     (POS-80 / POS-80C / POS-58 / POS-58C که روی سیستم نصب باشد) چاپ می‌شود.
   ============================================================================ */
(function () {
    'use strict';
    if (window.DeskReceipt) return;

    var STYLE_ID = 'desk-receipt-style';
    var overlay = null, statusEl = null, againEl = null, closeEl = null;
    var lastData = null, onCloseCb = null, printing = false;
    // ✅ تنظیمات چاپ هر بخش (از صفحهٔ «تنظیمات پنل») — یک‌بار واکشی می‌شود
    var panel = null, panelReq = null;

    function loadPanel(cb) {
        if (panel) { cb(); return; }
        if (window.DESK_PANEL) { panel = window.DESK_PANEL; cb(); return; }
        if (!panelReq) {
            panelReq = fetch('/api/Desk/Panel', { credentials: 'same-origin' })
                .then(function (r) { return r.json(); })
                .then(function (res) { if (res && res.success && res.data) panel = res.data; })
                .catch(function () { });
        }
        panelReq.then(function () { cb(); });
    }

    function printAllowed(section) {
        if (!section) return true;
        var p = panel || window.DESK_PANEL;
        if (!p || !p.print) return true;
        return p.print.indexOf(section) >= 0;
    }

    function injectCss() {
        if (document.getElementById(STYLE_ID)) return;
        var st = document.createElement('style');
        st.id = STYLE_ID;
        st.textContent = [
            '.dr-overlay{position:fixed;inset:0;z-index:1400;display:flex;align-items:center;justify-content:center;padding:14px;',
            '  background:color-mix(in srgb, #000 34%, transparent);-webkit-backdrop-filter:blur(5px);backdrop-filter:blur(5px);}',
            '.dr-card{width:100%;max-width:380px;max-height:92vh;overflow:auto;border-radius:22px;padding:16px 16px 12px;',
            '  background:var(--g-card-solid,#fff);color:var(--g-text,#111);border:1px solid color-mix(in srgb,var(--g-text,#111) 16%,transparent);',
            '  box-shadow:0 24px 60px rgba(0,0,0,.28);font-family:inherit;}',
            '.dr-head{text-align:center;margin-bottom:10px;}',
            '.dr-ok{width:46px;height:46px;margin:0 auto 8px;border-radius:50%;display:flex;align-items:center;justify-content:center;',
            '  font-size:1.5rem;color:#fff;background:linear-gradient(135deg,var(--g-primary,#3F5F8E),color-mix(in srgb,var(--g-primary,#3F5F8E) 62%,#0b2c4d));}',
            '.dr-title{font-size:1rem;font-weight:800;margin:0;}',
            '.dr-sub{font-size:.74rem;opacity:.72;margin-top:2px;}',
            '.dr-paper{margin-top:10px;border:1px dashed color-mix(in srgb,var(--g-text,#111) 28%,transparent);border-radius:14px;',
            '  padding:12px 12px 10px;background:color-mix(in srgb,var(--g-primary,#3F5F8E) 4%,transparent);}',
            '.dr-club{text-align:center;font-size:.86rem;font-weight:800;}',
            '.dr-doc{text-align:center;font-size:.78rem;font-weight:700;margin-top:2px;opacity:.9;}',
            '.dr-meta{display:flex;justify-content:space-between;gap:8px;font-size:.7rem;opacity:.75;margin:6px 0 8px;}',
            '.dr-line{border-top:1px dashed color-mix(in srgb,var(--g-text,#111) 22%,transparent);margin:8px 0;}',
            '.dr-row{display:flex;justify-content:space-between;gap:10px;font-size:.78rem;padding:3px 0;}',
            '.dr-row .n{flex:1;min-width:0;word-break:break-word;}',
            '.dr-row .a{white-space:nowrap;font-variant-numeric:tabular-nums;font-weight:700;}',
            '.dr-row .q{opacity:.7;font-size:.7rem;}',
            '.dr-total{display:flex;justify-content:space-between;align-items:center;gap:10px;font-size:.9rem;font-weight:800;margin-top:4px;}',
            '.dr-total b{font-size:1rem;}',
            '.dr-kv{display:flex;justify-content:space-between;font-size:.72rem;opacity:.85;padding:2px 0;}',
            '.dr-note{font-size:.72rem;opacity:.75;margin-top:6px;line-height:1.8;}',
            '.dr-print{display:flex;align-items:center;gap:8px;justify-content:center;font-size:.74rem;font-weight:700;margin:12px 0 8px;',
            '  padding:9px 10px;border-radius:12px;background:color-mix(in srgb,var(--g-primary,#3F5F8E) 10%,transparent);}',
            '.dr-print.warn{background:color-mix(in srgb,#b45309 14%,transparent);color:#b45309;}',
            '.dr-print.ok{background:color-mix(in srgb,#15803d 12%,transparent);color:#15803d;}',
            '.dr-actions{display:flex;gap:8px;}',
            '.dr-btn{flex:1;border:1px solid color-mix(in srgb,var(--g-text,#111) 26%,transparent);border-radius:14px;',
            '  padding:11px;font-size:.84rem;font-weight:800;background:transparent;color:inherit;cursor:pointer;}',
            '.dr-btn.primary{border-color:transparent;color:#fff;background:linear-gradient(135deg,var(--g-primary,#3F5F8E),color-mix(in srgb,var(--g-primary,#3F5F8E) 70%,#0b2c4d));}',
            '.dr-btn:disabled{opacity:.6;cursor:default;}'
        ].join('');
        document.head.appendChild(st);
    }

    function esc(v) {
        return String(v == null ? '' : v)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function money(n) { return Number(n || 0).toLocaleString('en-US'); }

    function faDate() {
        try {
            return new Date().toLocaleDateString('fa-IR', { year: 'numeric', month: '2-digit', day: '2-digit' });
        } catch (e) { return ''; }
    }

    function faTime() {
        try {
            return new Date().toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit' });
        } catch (e) { return ''; }
    }

    function build(data) {
        var rows = data.rows || [];
        var rowsHtml = '';
        rows.forEach(function (r) {
            var qty = Number(r.qty || 1);
            rowsHtml += '<div class="dr-row"><span class="n">' + esc(r.name) +
                (qty > 1 ? ' <span class="q">× ' + money(qty) + '</span>' : '') +
                '</span><span class="a">' + money(r.amount) + ' ریال</span></div>';
        });

        var kv = '';
        if (data.paymentMethod) kv += '<div class="dr-kv"><span>روش پرداخت</span><b>' + esc(data.paymentMethod) + '</b></div>';
        if (data.refNo) kv += '<div class="dr-kv"><span>کد سند</span><b>' + esc(data.refNo) + '</b></div>';

        var html =
            '<div class="dr-card" role="dialog" aria-modal="true" aria-label="فیش تراکنش">' +
                '<div class="dr-head">' +
                    '<div class="dr-ok"><i class="bi bi-check-lg"></i></div>' +
                    '<p class="dr-title">تراکنش با موفقیت انجام شد</p>' +
                    (data.message ? '<div class="dr-sub">' + esc(data.message) + '</div>' : '') +
                '</div>' +
                '<div class="dr-paper">' +
                    '<div class="dr-club">' + esc(data.club || 'ایمن تردد تندیس') + '</div>' +
                    '<div class="dr-doc">' + esc(data.title || 'فیش خرید') + '</div>' +
                    '<div class="dr-meta"><span>تاریخ: ' + esc(faDate()) + '</span><span>ساعت: ' + esc(faTime()) + '</span></div>' +
                    (data.party ? '<div class="dr-row"><span class="n">نام</span><span class="a">' + esc(data.party) + '</span></div>' : '') +
                    (data.partySub ? '<div class="dr-row"><span class="n">' + esc(data.partySub) + '</span><span></span></div>' : '') +
                    (rowsHtml ? '<div class="dr-line"></div>' + rowsHtml : '') +
                    '<div class="dr-line"></div>' +
                    '<div class="dr-total"><span>' + esc(data.totalLabel || 'مبلغ کل') + '</span><b>' + money(data.total) + ' ریال</b></div>' +
                    (kv ? '<div style="margin-top:6px">' + kv + '</div>' : '') +
                    (data.note ? '<div class="dr-note">' + esc(data.note) + '</div>' : '') +
                '</div>' +
                '<div class="dr-print" id="drStatus"><i class="bi bi-printer"></i><span>در حال بررسی پرینتر…</span></div>' +
                '<div class="dr-actions">' +
                    '<button type="button" class="dr-btn" id="drAgain" style="display:none">چاپ دوباره</button>' +
                    '<button type="button" class="dr-btn primary" id="drClose">پایان</button>' +
                '</div>' +
            '</div>';

        overlay.innerHTML = html;
        statusEl = overlay.querySelector('#drStatus');
        againEl = overlay.querySelector('#drAgain');
        closeEl = overlay.querySelector('#drClose');

        againEl.addEventListener('click', function () { doPrint(); });
        closeEl.addEventListener('click', close);
        overlay.addEventListener('click', function (e) { if (e.target === overlay) close(); });
        document.addEventListener('keydown', onKey);
    }

    function setStatus(text, cls) {
        if (!statusEl) return;
        statusEl.className = 'dr-print' + (cls ? ' ' + cls : '');
        statusEl.innerHTML = '<i class="bi ' + (cls === 'ok' ? 'bi-check2-circle' : (cls === 'warn' ? 'bi-exclamation-triangle' : 'bi-printer')) + '"></i><span>' + esc(text) + '</span>';
    }

    function doPrint() {
        if (!lastData || printing || !printAllowed(lastData.section)) return;
        printing = true;
        if (againEl) { againEl.disabled = true; if (againEl.style.display === 'none') againEl.style.display = 'inline-flex'; }
        setStatus('در حال ارسال فیش به پرینتر…');

        var payload = {
            title: lastData.title,
            party: lastData.party,
            partySub: lastData.partySub,
            rows: lastData.rows || [],
            total: Number(lastData.total || 0),
            totalLabel: lastData.totalLabel,
            refNo: lastData.refNo,
            paymentMethod: lastData.paymentMethod,
            note: lastData.note
        };

        fetch('/api/Desk/PrintReceipt', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        }).then(function (r) { return r.json(); }).then(function (res) {
            printing = false;
            if (againEl) againEl.disabled = false;

            if (res && res.printed) {
                setStatus(res.message || 'فیش چاپ شد', 'ok');
            } else {
                setStatus((res && res.message) || 'پرینتر POS روی سیستم یافت نشد', 'warn');
                if (againEl) againEl.style.display = 'inline-flex';
            }
        }).catch(function () {
            printing = false;
            if (againEl) { againEl.disabled = false; againEl.style.display = 'inline-flex'; }
            setStatus('ارتباط با سرور برای چاپ برقرار نشد', 'warn');
        });
    }

    function onKey(e) {
        if (e.key === 'Escape') close();
    }

    function close() {
        document.removeEventListener('keydown', onKey);
        if (overlay) {
            overlay.remove();
            overlay = null;
        }
        statusEl = againEl = closeEl = null;
        lastData = null;
        var cb = onCloseCb;
        onCloseCb = null;
        if (typeof cb === 'function') cb();
    }

    function show(data, onClose) {
        injectCss();
        lastData = data || {};
        onCloseCb = onClose || null;

        if (overlay) { overlay.remove(); overlay = null; }

        overlay = document.createElement('div');
        overlay.className = 'dr-overlay';
        document.body.appendChild(overlay);

        build(lastData);
        loadPanel(function () {
            if (printAllowed(lastData && lastData.section)) {
                doPrint();
            } else {
                // ✅ چاپ این بخش در تنظیمات پنل خاموش است: فیش فقط روی صفحه نمایش داده می‌شود
                setStatus('چاپ فیش برای این بخش غیرفعال است');
                if (againEl) againEl.style.display = 'none';
            }
        });
    }

    window.DeskReceipt = {
        show: show,
        close: close,
        /** printer info: {available, printer} */
        printerInfo: function (cb) {
            fetch('/api/Desk/Printer').then(function (r) { return r.json(); })
                .then(function (res) { if (cb) cb(res && res.data); })
                .catch(function () { if (cb) cb(null); });
        }
    };
})();
