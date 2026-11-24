/* ============================================================================
   فیش تراکنش میز خدمت
   - بعد از هر تراکنش موفق (شارژ اعتبار، بلیت، تک جلسه، تمدید/ثبت‌نام، فروشگاه، بوفه، خدمات)
     فیش روی صفحه نمایش داده می‌شود و به‌صورت خودکار روی پرینتر حرارتی POS
     (POS-80 / POS-80C / POS-58 / POS-58C که روی سیستم نصب باشد) چاپ می‌شود.
   - چیدمان فیش روی صفحه، مطابق همان فیش چاپی حرارتی است.
   ============================================================================ */
(function () {
    'use strict';
    if (window.DeskReceipt) return;

    var STYLE_ID = 'desk-receipt-style';
    var overlay = null, statusEl = null, closeEl = null;
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
            '.dr-card{width:100%;max-width:360px;max-height:92vh;overflow:auto;border-radius:20px;padding:14px 14px 12px;',
            '  background:var(--g-card-solid,#fff);color:var(--g-text,#111);border:1px solid color-mix(in srgb,var(--g-text,#111) 16%,transparent);',
            '  box-shadow:0 24px 60px rgba(0,0,0,.28);font-family:inherit;}',
            '.dr-head{text-align:center;margin-bottom:10px;}',
            '.dr-ok{width:40px;height:40px;margin:0 auto 6px;border-radius:50%;display:flex;align-items:center;justify-content:center;',
            '  font-size:1.3rem;color:#fff;background:linear-gradient(135deg,var(--g-primary,#3F5F8E),color-mix(in srgb,var(--g-primary,#3F5F8E) 62%,#0b2c4d));}',
            '.dr-title{font-size:.92rem;font-weight:800;margin:0;}',
            '.dr-sub{font-size:.72rem;opacity:.72;margin-top:2px;}',
            /* ===== کاغذ فیش (شبیه پرینت حرارتی) ===== */
            '.dr-paper{background:#fff;color:#111;border:1px solid #cfcfcf;border-radius:6px;padding:12px 11px 10px;',
            '  box-shadow:inset 0 0 0 1px #f3f3f3;font-family:Tahoma,"IRANSans","Segoe UI",sans-serif;}',
            '.dr-club{text-align:center;font-size:.74rem;font-weight:700;color:#333;letter-spacing:.2px;}',
            '.dr-doc{text-align:center;font-size:1.05rem;font-weight:800;margin-top:3px;color:#111;}',
            '.dr-dash{border-top:1px dashed #9e9e9e;margin:9px 0;}',
            /* ساعت و تاریخ در یک خط: ساعت سمت راست، تاریخ سمت چپ */
            '.dr-meta{display:flex;flex-direction:row;align-items:baseline;justify-content:space-between;gap:10px;flex-wrap:nowrap;}',
            '.dr-meta div{display:flex;align-items:baseline;gap:5px;font-size:.82rem;font-weight:700;color:#111;white-space:nowrap;}',
            '.dr-meta span{font-size:.72rem;font-weight:700;opacity:.85;}',
            '.dr-name{display:flex;justify-content:space-between;gap:10px;font-size:.76rem;color:#111;padding:2px 0;word-break:break-word;}',
            '.dr-name b{font-weight:800;}',
            '.dr-tbl{width:100%;border-collapse:collapse;border:1px solid #333;font-size:.7rem;color:#111;}',
            '.dr-tbl th{border-bottom:1px solid #333;padding:5px 3px;font-weight:800;text-align:center;background:#f7f7f7;}',
            '.dr-tbl td{border-bottom:1px dotted #a8a8a8;padding:5px 3px;text-align:center;font-weight:600;',
            '  font-variant-numeric:tabular-nums;direction:ltr;}',
            '.dr-tbl tr:last-child td{border-bottom:0;}',
            '.dr-tbl td.nm{text-align:right;direction:rtl;font-weight:700;}',
            '.dr-sum{border:1px solid #333;border-radius:4px;margin-top:8px;padding:7px 9px;display:flex;flex-direction:column;gap:4px;}',
            '.dr-sum div{display:flex;justify-content:space-between;align-items:baseline;gap:10px;font-size:.84rem;font-weight:800;color:#111;}',
            '.dr-sum span{font-size:.76rem;font-weight:700;opacity:.85;}',
            '.dr-note{text-align:center;font-size:.72rem;color:#333;margin:8px 0 6px;line-height:1.7;}',
            '.dr-footbox{border:1px solid #333;border-radius:4px;padding:7px 8px;text-align:center;font-size:.64rem;',
            '  line-height:1.8;color:#222;}',
            '.dr-print{display:flex;align-items:center;gap:8px;justify-content:center;font-size:.72rem;font-weight:700;margin:10px 0 8px;',
            '  padding:9px 10px;border-radius:12px;background:color-mix(in srgb,var(--g-primary,#3F5F8E) 10%,transparent);}',
            '.dr-print.warn{background:color-mix(in srgb,#b45309 14%,transparent);color:#b45309;}',
            '.dr-print.ok{background:color-mix(in srgb,#15803d 12%,transparent);color:#15803d;}',
            '.dr-actions{display:flex;}',
            '.dr-btn{flex:1;border:1px solid color-mix(in srgb,var(--g-text,#111) 26%,transparent);border-radius:14px;',
            '  padding:11px;font-size:.84rem;font-weight:800;background:transparent;color:inherit;cursor:pointer;}',
            '.dr-btn.primary{border-color:transparent;color:#fff;background:linear-gradient(135deg,var(--g-primary,#3F5F8E),color-mix(in srgb,var(--g-primary,#3F5F8E) 70%,#0b2c4d));}'
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
            return new Date().toLocaleTimeString('fa-IR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
        } catch (e) { return ''; }
    }

    function build(data) {
        var rows = data.rows || [];
        var body = '';
        rows.forEach(function (r) {
            var qty = Number(r.qty || 1) || 1;
            var unit = Number(r.amount || 0);
            body += '<tr>' +
                        '<td class="nm">' + esc(r.name) + '</td>' +
                        '<td>' + money(unit) + '</td>' +
                        '<td>' + money(qty) + '</td>' +
                        '<td>' + money(unit * qty) + '</td>' +
                    '</tr>';
        });

        var table = body
            ? '<table class="dr-tbl"><thead><tr><th>کالا</th><th>فی</th><th>تعداد</th><th>مبلغ</th></tr></thead><tbody>' +
              body + '</tbody></table>'
            : '';

        var totalLabel = data.totalLabel ? String(data.totalLabel).replace(/:$/, '') : 'مبلغ کل';
        var sum = '<div><span>' + esc(totalLabel) + ':</span><b>' + money(data.total) + ' ریال</b></div>';
        if (data.refNo) sum += '<div><span>شماره فاکتور:</span><b>' + esc(data.refNo) + '</b></div>';
        if (data.paymentMethod) sum += '<div><span>روش پرداخت:</span><b>' + esc(data.paymentMethod) + '</b></div>';

        var who = '';
        if (data.party) who += '<div class="dr-name"><b>نام:</b><span>' + esc(data.party) + '</span></div>';
        if (data.partySub) who += '<div class="dr-name"><span>' + esc(data.partySub) + '</span></div>';

        var club = data.club || window.CLUB || '';

        var html =
            '<div class="dr-card" role="dialog" aria-modal="true" aria-label="فیش تراکنش">' +
                '<div class="dr-head">' +
                    '<div class="dr-ok"><i class="bi bi-check-lg"></i></div>' +
                    '<p class="dr-title">تراکنش با موفقیت انجام شد</p>' +
                '</div>' +
                '<div class="dr-paper">' +
                    (club ? '<div class="dr-club">' + esc(club) + '</div>' : '') +
                    '<div class="dr-doc">' + esc(data.title || 'فیش خرید') + '</div>' +
                    '<div class="dr-dash"></div>' +
                    '<div class="dr-meta">' +
                        '<div><span>ساعت:</span><b>' + esc(faTime()) + '</b></div>' +
                        '<div><span>تاریخ:</span><b>' + esc(faDate()) + '</b></div>' +
                    '</div>' +
                    (who ? '<div class="dr-dash"></div>' + who : '') +
                    (table ? '<div class="dr-dash"></div>' + table : '') +
                    '<div class="dr-sum">' + sum + '</div>' +
                    (data.note ? '<div class="dr-note">' + esc(data.note) + '</div>' : '<div class="dr-dash"></div>') +
                    '<div class="dr-footbox">' +
                        'ایمن تردد تندیس مرکز تخصصی<br>' +
                        'هوشمندسازی اماکن ورزشی<br>' +
                        'www.ittandis.com<br>' +
                        '۰۲۱-۳۷۵۴۹۰۰۰' +
                    '</div>' +
                '</div>' +
                '<div class="dr-print" id="drStatus"><i class="bi bi-printer"></i><span>در حال بررسی پرینتر…</span></div>' +
                '<div class="dr-actions">' +
                    '<button type="button" class="dr-btn primary" id="drClose">پایان</button>' +
                '</div>' +
            '</div>';

        overlay.innerHTML = html;
        statusEl = overlay.querySelector('#drStatus');
        closeEl = overlay.querySelector('#drClose');

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
            if (res && res.printed) {
                setStatus(res.message || 'فیش چاپ شد', 'ok');
            } else {
                setStatus((res && res.message) || 'پرینتر POS روی سیستم یافت نشد', 'warn');
            }
        }).catch(function () {
            printing = false;
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
        statusEl = closeEl = null;
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
