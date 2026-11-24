/* ============================================================
   خرید فروشگاه / بوفه / خدمات از میز خدمت
   همان UI پنل عضو، ولی برای «عضو انتخاب‌شده» و با APIهای /api/Desk/Commerce/*
   ============================================================ */
(function () {
    'use strict';

    var ctx = window.DESK_CTX || { kind: 'shop', isBuffet: false, isService: false, memberID: 0 };
    var root = document.querySelector('[data-commerce-page]');
    var toastEl = document.getElementById('dkToast');
    var toastTimer = null;

    function byId(id) { return document.getElementById(id); }

    function toast(msg, isErr) {
        if (!toastEl) return;
        toastEl.textContent = msg || '';
        toastEl.className = 'dk-toast show' + (isErr ? ' err' : '');
        clearTimeout(toastTimer);
        toastTimer = setTimeout(function () { toastEl.className = 'dk-toast'; }, 4200);
    }

    function esc(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function num(value, fallback) {
        var n = Number(value);
        return isFinite(n) ? n : (fallback || 0);
    }

    function int(value, fallback) {
        var n = parseInt(value, 10);
        return isFinite(n) ? n : (fallback || 0);
    }

    function money(value) { return num(value).toLocaleString('fa-IR') + ' ریال'; }

    function api(path, body) {
        return fetch(path, {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body || {})
        }).then(function (r) { return r.json(); });
    }

    function setState(target, kind, title, detail) {
        if (!target) return;
        var icon = kind === 'error' ? 'bi-exclamation-triangle' : kind === 'empty' ? 'bi-inbox' : 'bi-arrow-repeat';
        target.innerHTML = '<div class="commerce-status ' + esc(kind) + '">' +
            '<i class="bi ' + icon + '" aria-hidden="true"></i><strong>' + esc(title || '') + '</strong>' +
            (detail ? '<span>' + esc(detail) + '</span>' : '') + '</div>';
    }

    /* ------------------------------------------------------------
       اطلاعات پرداخت: کد ملی/رمز عضو (اختیاری) و نام مهمان
       ------------------------------------------------------------ */
    function payCreds() {
        var nc = (byId('dkPayNational') ? byId('dkPayNational').value : '').trim();
        var pw = (byId('dkPayPassword') ? byId('dkPayPassword').value : '');
        var name = (byId('dkPayGuest') ? byId('dkPayGuest').value : '').trim();
        return {
            nc: nc.replace(/[۰-۹]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'.indexOf(d); })
                   .replace(/[٠-٩]/g, function (d) { return '٠١٢٣٤٥٦٧٨٩'.indexOf(d); })
                   .replace(/\D/g, ''),
            pw: pw,
            name: name
        };
    }

    function clearPayCreds() {
        ['dkPayNational', 'dkPayPassword', 'dkPayGuest'].forEach(function (id) {
            var el = byId(id);
            if (el) el.value = '';
        });
    }

    /* ------------------------------------------------------------
       پرداخت: اعتبار عضو یا کارت‌خوان
       ------------------------------------------------------------ */
    var payState = { amount: 0, credit: null, pos: null, busy: false, receipt: null };
    var pollTimer = null;

    function payModal() { return byId('dkPayModal'); }

    function openPay(amount, onCredit, onPosPrepare, onPosConfirm, title, receipt) {
        payState = { amount: amount, credit: onCredit, pos: onPosConfirm, posPrepare: onPosPrepare, busy: false, receipt: receipt || null };
        byId('dkPayTitle').textContent = title || 'پرداخت';
        byId('dkPayAmount').textContent = money(amount);
        byId('dkPayNote').textContent = '';
        payModal().classList.add('open');
        var nc = byId('dkPayNational');
        if (nc) setTimeout(function () { nc.focus(); }, 120);
    }

    function closePay() {
        payModal().classList.remove('open');
        if (pollTimer) { clearTimeout(pollTimer); pollTimer = null; }
        setPayBusy(false);
        var posBtn = byId('dkPayPos');
        if (posBtn) posBtn.innerHTML = '<i class="bi bi-credit-card-2-front"></i> پرداخت با کارت‌خوان';
    }

    function setPayBusy(busy, note) {
        payState.busy = busy;
        byId('dkPayCredit').disabled = busy;
        byId('dkPayPos').disabled = busy;
        if (note !== undefined) byId('dkPayNote').textContent = note || '';
    }

    function payWithCredit() {
        if (payState.busy) return;

        var creds = payCreds();
        if (creds.nc.length !== 10) {
            byId('dkPayNote').textContent = 'برای پرداخت از اعتبار، کد ملی ۱۰ رقمی عضو را وارد کنید.';
            if (byId('dkPayNational')) byId('dkPayNational').focus();
            return;
        }
        if (!creds.pw) {
            byId('dkPayNote').textContent = 'رمز عبور عضو را وارد کنید.';
            if (byId('dkPayPassword')) byId('dkPayPassword').focus();
            return;
        }

        setPayBusy(true, 'در حال تأیید هویت عضو و ثبت خرید از اعتبار…');
        payState.credit().then(function (res) {
            setPayBusy(false);
            if (!res || !res.success) {
                toast((res && res.message) || 'خرید انجام نشد', true);
                byId('dkPayNote').textContent = (res && res.message) || '';
                return;
            }
            closePay();
            finish(res, 'اعتباری');
        }).catch(function () {
            setPayBusy(false);
            toast('اتصال به سرور برقرار نشد', true);
        });
    }

    function payWithPos() {
        if (payState.busy) return;
        setPayBusy(true, 'در حال ارسال مبلغ به کارت‌خوان…');
        payState.posPrepare().then(function (res) {
            if (!res || !res.success) {
                setPayBusy(false);
                toast((res && res.message) || 'ارسال به کارت‌خوان ناموفق بود', true);
                byId('dkPayNote').textContent = (res && res.message) || '';
                return;
            }
            if (res.paymentPending && res.posTxnId) {
                waitForCard(res.posTxnId);
                return;
            }
            setPayBusy(false);
            closePay();
            finish(res, 'کارتخوان');
        }).catch(function () {
            setPayBusy(false);
            toast('اتصال به سرور برقرار نشد', true);
        });
    }

    function waitForCard(txnId) {
        var tries = 0;
        setPayBusy(true, 'مبلغ روی کارت‌خوان ارسال شد؛ منتظر کشیدن کارت…');
        byId('dkPayPos').disabled = false;
        byId('dkPayPos').textContent = 'انصراف از پرداخت';

        function poll() {
            fetch('/api/Desk/Status?txnId=' + txnId, { credentials: 'include' })
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    var st = (res && res.data) || {};
                    if (st.status === 'success') {
                        byId('dkPayNote').textContent = 'پرداخت تأیید شد؛ در حال ثبت خرید…';
                        payState.pos(txnId).then(function (c) {
                            if (!c || !c.success) {
                                toast((c && c.message) || 'ثبت خرید ناموفق بود', true);
                                setPayBusy(false);
                                return;
                            }
                            closePay();
                            finish(c, 'کارتخوان');
                        });
                        return;
                    }
                    if (st.status === 'failed' || st.status === 'expired' || st.status === 'canceled') {
                        setPayBusy(false);
                        byId('dkPayNote').textContent = 'پرداخت انجام نشد: ' + (st.message || st.status);
                        toast('پرداخت ناموفق بود', true);
                        return;
                    }
                    if (++tries > 120) {
                        setPayBusy(false);
                        byId('dkPayNote').textContent = 'زمان انتظار پرداخت تمام شد';
                        return;
                    }
                    pollTimer = setTimeout(poll, 2500);
                })
                .catch(function () { pollTimer = setTimeout(poll, 2600); });
        }

        poll();
    }

    function afterSuccess() {
        if (state.basket) state.basket = [];
        clearPayCreds();
        setTimeout(function () { window.location.reload(); }, 600);
    }

    // ✅ نمایش فیش تراکنش + چاپ خودکار روی پرینتر POS (بعد از بستن فیش، صفحه بازخوانی می‌شود)
    function finish(res, method) {
        var builder = payState.receipt;
        if (builder && window.DeskReceipt) {
            var rc = typeof builder === 'function' ? builder(method) : builder;
            rc.message = (res && res.message) || 'خرید با موفقیت ثبت شد';
            window.DeskReceipt.show(rc, afterSuccess);
            return;
        }
        toast((res && res.message) || 'خرید ثبت شد');
        afterSuccess();
    }

    /* ------------------------------------------------------------
       ورود عضو برای پرداخت با کارت‌خوان (مرحلهٔ جداگانه)
       ------------------------------------------------------------ */
    function posAuthModal() { return byId('dkPosAuth'); }

    function openPosAuth() {
        if (!posAuthModal()) { payWithPos(); return; }
        var nc = byId('dkPosNational');
        var pw = byId('dkPosPassword');
        var note = byId('dkPosAuthNote');
        if (nc) nc.value = '';
        if (pw) pw.value = '';
        if (note) note.textContent = '';
        posAuthModal().classList.add('open');
        if (nc) setTimeout(function () { nc.focus(); }, 120);
    }

    function closePosAuth() {
        if (posAuthModal()) posAuthModal().classList.remove('open');
    }

    function posAuthSubmit(asGuest) {
        var note = byId('dkPosAuthNote');
        var ncEl = byId('dkPosNational');
        var pwEl = byId('dkPosPassword');
        var nc = (ncEl ? ncEl.value : '').replace(/\D/g, '');
        var pw = pwEl ? pwEl.value : '';

        if (asGuest) {
            if (byId('dkPayNational')) byId('dkPayNational').value = '';
            if (byId('dkPayPassword')) byId('dkPayPassword').value = '';
            closePosAuth();
            payWithPos();
            return;
        }

        if (nc.length !== 10) {
            if (note) note.textContent = 'کد ملی ۱۰ رقمی عضو را وارد کنید یا «ادامه به‌عنوان مهمان» را بزنید.';
            if (ncEl) ncEl.focus();
            return;
        }
        if (!pw) {
            if (note) note.textContent = 'رمز عبور عضو را وارد کنید.';
            if (pwEl) pwEl.focus();
            return;
        }

        if (note) note.textContent = 'در حال تأیید هویت عضو…';
        api('/api/Desk/Commerce/VerifyMember', { nationalCode: nc, password: pw }).then(function (res) {
            if (!res || !res.success) {
                if (note) note.textContent = (res && res.message) || 'کد ملی یا رمز صحیح نیست';
                return;
            }
            if (byId('dkPayNational')) byId('dkPayNational').value = nc;
            if (byId('dkPayPassword')) byId('dkPayPassword').value = pw;
            closePosAuth();
            payWithPos();
        }).catch(function () {
            if (note) note.textContent = 'اتصال به سرور برقرار نشد';
        });
    }

    function initPayModal() {
        if (!payModal()) return;
        byId('dkPayCredit').addEventListener('click', payWithCredit);
        byId('dkPayPos').addEventListener('click', function () {
            if (pollTimer) {
                clearTimeout(pollTimer); pollTimer = null;
                setPayBusy(false, '');
                closePay();
                return;
            }
            openPosAuth();
        });
        byId('dkPayCancel').addEventListener('click', closePay);

        if (posAuthModal()) {
            byId('dkPosAuthGo').addEventListener('click', function () { posAuthSubmit(false); });
            byId('dkPosAuthGuest').addEventListener('click', function () { posAuthSubmit(true); });
            byId('dkPosAuthCancel').addEventListener('click', closePosAuth);
            posAuthModal().addEventListener('click', function (e) { if (e.target === posAuthModal()) closePosAuth(); });
        }

        // ✅ خروج خودکار از پروفایل عضو پس از ۱ دقیقه بی‌کاری
        if (ctx.memberID) {
            var lastAct = Date.now();
            ['pointerdown', 'mousedown', 'touchstart', 'keydown', 'wheel', 'scroll'].forEach(function (ev) {
                document.addEventListener(ev, function () { lastAct = Date.now(); }, { passive: true });
            });
            setInterval(function () {
                if (payModal().classList.contains('open') || (posAuthModal() && posAuthModal().classList.contains('open'))) { lastAct = Date.now(); return; }
                if (Date.now() - lastAct < 60000) return;
                var url = new URL(window.location.href);
                url.searchParams.delete('member');
                window.location.replace(url.toString());
            }, 5000);
        }
    }

    /* ------------------------------------------------------------
       فروشگاه / بوفه
       ------------------------------------------------------------ */
    var state = { isBuffet: !!ctx.isBuffet, categoryId: 0, products: {}, basket: [] };

    function renderCategories(items) {
        var target = byId('catList');
        if (!target) return;
        var html = '<button type="button" class="commerce-category-button active" data-commerce-action="category" data-category-id="0">' +
            '<i class="bi bi-grid-1x2-fill ms-1" aria-hidden="true"></i> همه</button>';
        (items || []).forEach(function (item) {
            html += '<button type="button" class="commerce-category-button" data-commerce-action="category" data-category-id="' +
                int(item.stuffCategoryID) + '">' + esc(item.categoryDesc || '-') + '</button>';
        });
        target.innerHTML = html;
    }

    function renderProducts(items) {
        var target = byId('stuffList');
        state.products = {};
        if (!items || !items.length) {
            setState(target, 'empty', 'کالایی در این دسته پیدا نشد', 'از دسته دیگری انتخاب کنید.');
            return;
        }
        var html = '';
        items.forEach(function (item) {
            var id = int(item.stuffID);
            state.products[id] = item;
            var left = stockLeft(id);
            var out = !!item.trackStock && left <= 0;
            html += '<article class="commerce-product-card' + (out ? ' is-out' : '') + '">' +
                '<div class="commerce-product-top">' +
                    '<div class="commerce-product-icon"><i class="bi ' + (state.isBuffet ? 'bi-cup-hot' : 'bi-bag-heart') + '" aria-hidden="true"></i></div>' +
                    '<div class="commerce-product-body">' +
                        '<h3 class="commerce-product-title">' + esc(item.stuffDesc || '-') + '</h3>' +
                        '<strong class="commerce-price">' + esc(item.amountDisplay || money(item.stuffAmount)) + '</strong>' +
                        (item.stockDisplay ? '<span class="commerce-stock">' + esc(item.stockDisplay) + '</span>' : '') +
                    '</div>' +
                '</div>' +
                '<div class="commerce-product-action">' +
                    '<label class="commerce-visually-hidden" for="commerce-count-' + id + '">تعداد ' + esc(item.stuffDesc || 'کالا') + '</label>' +
                    '<input id="commerce-count-' + id + '" class="commerce-quantity" type="number" min="1" max="' + Math.max(1, left) + '" step="1" value="1" inputmode="numeric" data-commerce-count="' + id + '"' + (out ? ' disabled' : '') + '>' +
                    '<button type="button" class="commerce-add" data-commerce-action="add-product" data-id="' + id + '"' + (out ? ' disabled aria-disabled="true"' : '') + '>' +
                        (out ? '<i class="bi bi-x-circle" aria-hidden="true"></i> ناموجود'
                             : '<i class="bi bi-cart-plus" aria-hidden="true"></i> افزودن') + '</button>' +
                '</div></article>';
        });
        target.innerHTML = html;
    }

    // موجودی قابل خرید یک کالا (کالای بدون کنترل انبار: ۱۰۰)
    function stockLeft(id) {
        var product = state.products[id];
        if (!product || !product.trackStock) return 100;
        return Math.max(0, Math.min(100, int(product.availableCount, 0)));
    }

    function markCategoryActive(id) {
        [].forEach.call(document.querySelectorAll('#catList [data-category-id]'), function (b) {
            b.classList.toggle('active', int(b.getAttribute('data-category-id')) === id);
        });
    }

    function loadProducts(categoryId) {
        state.categoryId = int(categoryId);
        markCategoryActive(state.categoryId);
        var target = byId('stuffList');
        setState(target, 'loading', 'در حال بارگذاری کالاها…');
        api('/api/Desk/Commerce/Stuffs', { isBuffet: state.isBuffet, categoryID: state.categoryId || null })
            .then(function (res) {
                if (!res || !res.success) {
                    setState(target, 'error', (res && res.message) || 'دریافت کالاها ناموفق بود');
                    return;
                }
                var count = byId('productCount');
                if (count) count.textContent = res.data.length;
                renderProducts(res.data);
            })
            .catch(function () { setState(target, 'error', 'اتصال به سرور برقرار نشد'); });
    }

    function loadShop() {
        api('/api/Desk/Commerce/Categories', { isBuffet: state.isBuffet }).then(function (res) {
            if (res && res.success) renderCategories(res.data);
        });
        loadProducts(0);
    }

    function basketLine(id) {
        for (var i = 0; i < state.basket.length; i++) if (state.basket[i].stuffID === id) return state.basket[i];
        return null;
    }

    function renderBasket() {
        var target = byId('basketList');
        var footer = byId('basketTotal');
        var totalNode = byId('totalAmount');
        var countNode = byId('basketCount');
        if (!target) return;
        if (!state.basket.length) {
            target.innerHTML = '<div class="commerce-empty"><i class="bi bi-cart-x" aria-hidden="true"></i><span>سبد خرید خالی است</span></div>';
            if (footer) footer.style.display = 'none';
            if (countNode) countNode.textContent = '۰';
            return;
        }
        var total = 0, count = 0, html = '';
        state.basket.forEach(function (item) {
            var lineTotal = item.unitPrice * item.count;
            total += lineTotal;
            count += item.count;
            html += '<div class="commerce-cart-item"><div>' +
                '<div class="commerce-cart-item-name">' + esc(item.name) + '</div>' +
                '<div class="commerce-cart-item-meta">' + esc(money(item.unitPrice)) + ' × ' + esc(String(item.count)) + '</div></div>' +
                '<div class="commerce-cart-item-controls">' +
                    '<button type="button" class="commerce-step" aria-label="افزایش تعداد" data-commerce-action="increase" data-id="' + item.stuffID + '"><i class="bi bi-plus" aria-hidden="true"></i></button>' +
                    '<strong class="commerce-cart-item-meta">' + esc(String(item.count)) + '</strong>' +
                    '<button type="button" class="commerce-step" aria-label="کاهش تعداد" data-commerce-action="decrease" data-id="' + item.stuffID + '"><i class="bi bi-dash" aria-hidden="true"></i></button>' +
                    '<button type="button" class="commerce-remove" aria-label="حذف از سبد" data-commerce-action="remove" data-id="' + item.stuffID + '"><i class="bi bi-trash3" aria-hidden="true"></i></button>' +
                '</div></div>';
        });
        target.innerHTML = html;
        if (totalNode) totalNode.textContent = money(total);
        if (countNode) countNode.textContent = count;
        if (footer) footer.style.display = 'flex';
    }

    function addProduct(id) {
        var product = state.products[id];
        if (!product) return;
        var left = stockLeft(id);
        if (left <= 0) { toast('این کالا موجود نیست', true); return; }
        var input = document.querySelector('[data-commerce-count="' + id + '"]');
        var count = Math.max(1, Math.min(left, int(input && input.value, 1)));
        var existing = basketLine(id);
        if (existing) existing.count = Math.min(left, existing.count + count);
        else state.basket.push({ stuffID: id, name: product.stuffDesc || '-', unitPrice: num(product.stuffAmount), count: count });
        renderBasket();
        toast(left < 100 && existing && existing.count >= left ? 'بیشتر از موجودی انبار نمی‌توانید اضافه کنید' : 'کالا به سبد اضافه شد', left < 100 && existing && existing.count >= left);
    }

    function changeBasket(id, delta) {
        var item = basketLine(id);
        if (!item) return;
        item.count = Math.max(0, Math.min(stockLeft(id), item.count + delta));
        if (item.count === 0) state.basket = state.basket.filter(function (x) { return x.stuffID !== id; });
        renderBasket();
    }

    function checkout() {
        if (!state.basket.length) { toast('سبد خرید خالی است', true); return; }
        var total = 0;
        var items = state.basket.map(function (item) {
            total += item.unitPrice * item.count;
            return { stuffID: item.stuffID, unitPrice: item.unitPrice, count: item.count };
        });

        function body(method, posTxnId) {
            var creds = payCreds();
            return {
                memberID: ctx.memberID,
                nationalCode: creds.nc || null,
                password: creds.pw || null,
                guestName: creds.name || null,
                items: items,
                isBuffet: state.isBuffet,
                method: method,
                posTxnId: posTxnId || null
            };
        }

        function receiptFor(method) {
            return {
                title: state.isBuffet ? 'فاکتور بوفه' : 'فاکتور فروشگاه',
                party: payCreds().name || null,
                rows: state.basket.map(function (i) {
                    return { name: i.name, qty: i.count, amount: i.unitPrice * i.count };
                }),
                total: total,
                paymentMethod: method,
                section: state.isBuffet ? 'buffet' : 'shop',
                club: ctx.club || null
            };
        }

        openPay(total,
            function () { return api('/api/Desk/Commerce/Buy', body('credit')); },
            function () { return api('/api/Desk/Commerce/Buy', body('pos')); },
            function (txn) { return api('/api/Desk/Commerce/Buy', body('pos', txn)); },
            state.isBuffet ? 'پرداخت بوفه' : 'پرداخت فروشگاه',
            receiptFor);
    }

    /* ------------------------------------------------------------
       خدمات
       ------------------------------------------------------------ */
    function initService() {
        var target = byId('serviceList');
        if (!target) return;

        api('/api/Desk/Commerce/ServiceList', {}).then(function (res) {
            if (!res || !res.success) { setState(target, 'error', (res && res.message) || 'دریافت خدمات ناموفق بود'); return; }
            var items = res.data || [];
            var count = byId('serviceCount');
            if (count) count.textContent = items.length;
            if (!items.length) { setState(target, 'empty', 'سرویسی برای خرید وجود ندارد', 'بعداً دوباره بررسی کنید.'); return; }

            target.innerHTML = items.map(function (item) {
                return '<article class="commerce-card"><div class="commerce-card-top">' +
                    '<div class="commerce-card-icon"><i class="bi bi-stars" aria-hidden="true"></i></div>' +
                    '<div class="commerce-card-body">' +
                        '<h3 class="commerce-card-title">' + esc(item.serviceDesc || '-') + '</h3>' +
                        '<strong class="commerce-price">' + esc(item.amountDisplay || money(item.amount)) + '</strong>' +
                    '</div></div>' +
                    '<div class="commerce-card-action">' +
                        '<button type="button" class="commerce-buy" data-commerce-action="buy-service" data-id="' + int(item.serviceID) + '" data-amount="' + num(item.amount) + '" data-name="' + esc(item.serviceDesc || '') + '">' +
                            '<i class="bi bi-wallet2" aria-hidden="true"></i> خرید</button>' +
                    '</div></article>';
            }).join('');
        }).catch(function () { setState(target, 'error', 'اتصال به سرور برقرار نشد'); });
    }

    function buyService(id, amount, name) {
        function receiptFor(method) {
            return {
                title: 'رسید دریافت خدمات',
                party: payCreds().name || null,
                rows: [{ name: name || 'خدمت', qty: 1, amount: amount }],
                total: amount,
                paymentMethod: method,
                section: 'services',
                club: ctx.club || null
            };
        }

        function body(method, posTxnId) {
            var creds = payCreds();
            return {
                memberID: ctx.memberID,
                serviceID: id,
                nationalCode: creds.nc || null,
                password: creds.pw || null,
                guestName: creds.name || null,
                method: method,
                posTxnId: posTxnId || null
            };
        }
        openPay(amount,
            function () { return api('/api/Desk/Commerce/BuyService', body('credit')); },
            function () { return api('/api/Desk/Commerce/BuyService', body('pos')); },
            function (txn) { return api('/api/Desk/Commerce/BuyService', body('pos', txn)); },
            'پرداخت خدمات',
            receiptFor);
    }

    /* ------------------------------------------------------------
       راه‌اندازی
       ------------------------------------------------------------ */
    function actionOf(node) {
        while (node && node !== root) {
            if (node.getAttribute && node.getAttribute('data-commerce-action')) return node;
            node = node.parentNode;
        }
        return null;
    }

    if (!root) return;

    initPayModal();

    if (ctx.isService) {
        initService();
        root.addEventListener('click', function (e) {
            var btn = actionOf(e.target);
            if (btn && btn.getAttribute('data-commerce-action') === 'buy-service') {
                buyService(int(btn.getAttribute('data-id')), num(btn.getAttribute('data-amount')), btn.getAttribute('data-name') || '');
            }
        });
        return;
    }

    var buyBtn = byId('btnBuyBasket');
    if (buyBtn) buyBtn.addEventListener('click', checkout);

    root.addEventListener('click', function (e) {
        var btn = actionOf(e.target);
        if (!btn) return;
        var action = btn.getAttribute('data-commerce-action');
        var id = int(btn.getAttribute('data-id'));
        if (action === 'category') loadProducts(int(btn.getAttribute('data-category-id')));
        else if (action === 'add-product') addProduct(id);
        else if (action === 'increase') changeBasket(id, 1);
        else if (action === 'decrease') changeBasket(id, -1);
        else if (action === 'remove') {
            state.basket = state.basket.filter(function (x) { return x.stuffID !== id; });
            renderBasket();
        }
    });

    loadShop();
    renderBasket();
})();
