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
       انتخاب عضو (کد ملی / موبایل)
       ------------------------------------------------------------ */
    function initMemberPicker() {
        var input = byId('dkFindInput');
        var btn = byId('dkFindBtn');
        if (!input || !btn) return;

        function search() {
            var q = (input.value || '').trim();
            if (q.length < 10) { toast('کد ملی یا موبایل را کامل وارد کنید', true); return; }
            btn.disabled = true;
            api('/api/Desk/Commerce/FindMember', { query: q }).then(function (res) {
                btn.disabled = false;
                if (!res || !res.success) { toast((res && res.message) || 'عضو یافت نشد', true); return; }
                var url = new URL(window.location.href);
                url.searchParams.set('member', res.data.memberID);
                window.location.href = url.toString();
            }).catch(function () {
                btn.disabled = false;
                toast('اتصال به سرور برقرار نشد', true);
            });
        }

        btn.addEventListener('click', search);
        input.addEventListener('keydown', function (e) { if (e.key === 'Enter') search(); });
        input.focus();
    }

    /* ------------------------------------------------------------
       پرداخت: اعتبار عضو یا کارت‌خوان
       ------------------------------------------------------------ */
    var payState = { amount: 0, credit: null, pos: null, busy: false };
    var pollTimer = null;

    function payModal() { return byId('dkPayModal'); }

    function openPay(amount, onCredit, onPosPrepare, onPosConfirm, title) {
        payState = { amount: amount, credit: onCredit, pos: onPosConfirm, posPrepare: onPosPrepare, busy: false };
        byId('dkPayTitle').textContent = title || 'پرداخت';
        byId('dkPayAmount').textContent = money(amount);
        byId('dkPayNote').textContent = '';
        payModal().classList.add('open');
    }

    function closePay() {
        payModal().classList.remove('open');
        if (pollTimer) { clearTimeout(pollTimer); pollTimer = null; }
    }

    function setPayBusy(busy, note) {
        payState.busy = busy;
        byId('dkPayCredit').disabled = busy;
        byId('dkPayPos').disabled = busy;
        if (note !== undefined) byId('dkPayNote').textContent = note || '';
    }

    function payWithCredit() {
        if (payState.busy) return;
        setPayBusy(true, 'در حال ثبت خرید از اعتبار…');
        payState.credit().then(function (res) {
            setPayBusy(false);
            if (!res || !res.success) {
                toast((res && res.message) || 'خرید انجام نشد', true);
                byId('dkPayNote').textContent = (res && res.message) || '';
                return;
            }
            closePay();
            toast(res.message || 'خرید ثبت شد');
            afterSuccess();
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
            toast(res.message || 'خرید ثبت شد');
            closePay();
            afterSuccess();
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
                            toast(c.message || 'خرید ثبت شد');
                            afterSuccess();
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
        setTimeout(function () { window.location.reload(); }, 1400);
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
            payWithPos();
        });
        byId('dkPayCancel').addEventListener('click', closePay);
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
            html += '<article class="commerce-product-card">' +
                '<div class="commerce-product-top">' +
                    '<div class="commerce-product-icon"><i class="bi ' + (state.isBuffet ? 'bi-cup-hot' : 'bi-bag-heart') + '" aria-hidden="true"></i></div>' +
                    '<div class="commerce-product-body">' +
                        '<h3 class="commerce-product-title">' + esc(item.stuffDesc || '-') + '</h3>' +
                        '<strong class="commerce-price">' + esc(item.amountDisplay || money(item.stuffAmount)) + '</strong>' +
                    '</div>' +
                '</div>' +
                '<div class="commerce-product-action">' +
                    '<label class="commerce-visually-hidden" for="commerce-count-' + id + '">تعداد ' + esc(item.stuffDesc || 'کالا') + '</label>' +
                    '<input id="commerce-count-' + id + '" class="commerce-quantity" type="number" min="1" max="100" step="1" value="1" inputmode="numeric" data-commerce-count="' + id + '">' +
                    '<button type="button" class="commerce-add" data-commerce-action="add-product" data-id="' + id + '">' +
                        '<i class="bi bi-cart-plus" aria-hidden="true"></i> افزودن</button>' +
                '</div></article>';
        });
        target.innerHTML = html;
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
        var input = document.querySelector('[data-commerce-count="' + id + '"]');
        var count = Math.max(1, Math.min(100, int(input && input.value, 1)));
        var existing = basketLine(id);
        if (existing) existing.count = Math.min(100, existing.count + count);
        else state.basket.push({ stuffID: id, name: product.stuffDesc || '-', unitPrice: num(product.stuffAmount), count: count });
        renderBasket();
        toast('کالا به سبد اضافه شد');
    }

    function changeBasket(id, delta) {
        var item = basketLine(id);
        if (!item) return;
        item.count = Math.max(0, Math.min(100, item.count + delta));
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
            return { memberID: ctx.memberID, items: items, isBuffet: state.isBuffet, method: method, posTxnId: posTxnId || null };
        }

        openPay(total,
            function () { return api('/api/Desk/Commerce/Buy', body('credit')); },
            function () { return api('/api/Desk/Commerce/Buy', body('pos')); },
            function (txn) { return api('/api/Desk/Commerce/Buy', body('pos', txn)); },
            state.isBuffet ? 'پرداخت بوفه' : 'پرداخت فروشگاه');
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
                        '<button type="button" class="commerce-buy" data-commerce-action="buy-service" data-id="' + int(item.serviceID) + '" data-amount="' + num(item.amount) + '">' +
                            '<i class="bi bi-wallet2" aria-hidden="true"></i> خرید</button>' +
                    '</div></article>';
            }).join('');
        }).catch(function () { setState(target, 'error', 'اتصال به سرور برقرار نشد'); });
    }

    function buyService(id, amount) {
        function body(method, posTxnId) {
            return { memberID: ctx.memberID, serviceID: id, method: method, posTxnId: posTxnId || null };
        }
        openPay(amount,
            function () { return api('/api/Desk/Commerce/BuyService', body('credit')); },
            function () { return api('/api/Desk/Commerce/BuyService', body('pos')); },
            function (txn) { return api('/api/Desk/Commerce/BuyService', body('pos', txn)); },
            'پرداخت خدمات');
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

    if (!ctx.memberID || !root) {
        initMemberPicker();
        return;
    }

    initPayModal();

    if (ctx.isService) {
        initService();
        root.addEventListener('click', function (e) {
            var btn = actionOf(e.target);
            if (btn && btn.getAttribute('data-commerce-action') === 'buy-service') {
                buyService(int(btn.getAttribute('data-id')), num(btn.getAttribute('data-amount')));
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
