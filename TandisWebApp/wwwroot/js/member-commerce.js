/* ============================================================
   رفتار مشترک صفحات خرید عضو
   سرویس، بلیت، فروشگاه و بوفه — بدون inline onclick و با escape کامل
   ============================================================ */
(function () {
    'use strict';

    var root = document.querySelector('[data-commerce-page]');
    if (!root) return;

    var pageType = root.getAttribute('data-commerce-page') || '';
    var state = {
        isBuffet: pageType === 'buffet',
        categoryId: 0,
        products: {},
        basket: [],
        requestSerial: 0
    };

    function byId(id) { return document.getElementById(id); }

    function escapeHtml(value) {
        if (typeof window.esc === 'function') return window.esc(value);
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function numberValue(value, fallback) {
        var n = Number(value);
        return isFinite(n) ? n : (fallback || 0);
    }

    function money(value) {
        return numberValue(value).toLocaleString('fa-IR') + ' ریال';
    }

    function integer(value, fallback) {
        var n = parseInt(value, 10);
        return isFinite(n) ? n : (fallback || 0);
    }

    function setState(target, kind, title, detail) {
        if (!target) return;
        var icon = kind === 'error' ? 'bi-exclamation-triangle' :
            kind === 'empty' ? 'bi-inbox' : 'bi-arrow-repeat';
        target.innerHTML = '<div class="commerce-status ' + escapeHtml(kind) + '">' +
            '<i class="bi ' + icon + '" aria-hidden="true"></i>' +
            '<strong>' + escapeHtml(title || '') + '</strong>' +
            (detail ? '<span>' + escapeHtml(detail) + '</span>' : '') +
            '</div>';
    }

    function requestJson(url, onSuccess, target) {
        if (typeof window.apiCall !== 'function') {
            setState(target, 'error', 'امکان ارتباط با سرور وجود ندارد');
            return;
        }

        apiCall(url, 'GET', null, function (res) {
            if (!res || res.success !== true) {
                setState(target, 'error', (res && res.message) || 'دریافت اطلاعات ناموفق بود');
                return;
            }
            onSuccess(res.data || []);
        }, function (err) {
            setState(target, 'error', 'بارگذاری ناموفق بود', err && err.message ? err.message : 'لطفاً دوباره تلاش کنید.');
        });
    }

    function requestPurchase(options) {
        if (typeof window.openPaymentModal !== 'function') {
            showToast('ماژول پرداخت بارگذاری نشده است', 'error');
            return;
        }
        openPaymentModal(options);
    }

    function actionFromTarget(target) {
        while (target && target !== root) {
            if (target.getAttribute && target.getAttribute('data-commerce-action')) return target;
            target = target.parentNode;
        }
        return null;
    }

    function renderSimpleCards(target, items, config) {
        if (!items || !items.length) {
            setState(target, 'empty', config.emptyTitle, config.emptyDetail);
            return;
        }

        var html = '';
        items.forEach(function (item) {
            var id = integer(item[config.idKey]);
            var amount = numberValue(item[config.amountKey]);
            var title = item[config.titleKey] || '-';
            var subtitle = config.subtitleKey ? (item[config.subtitleKey] || '') : '';
            html += '<article class="commerce-card">' +
                '<div class="commerce-card-top">' +
                    '<div class="commerce-card-icon"><i class="bi ' + config.icon + '" aria-hidden="true"></i></div>' +
                    '<div class="commerce-card-body">' +
                        '<h3 class="commerce-card-title">' + escapeHtml(title) + '</h3>' +
                        (subtitle ? '<p class="commerce-card-subtitle">' + escapeHtml(subtitle) + '</p>' : '') +
                        '<strong class="commerce-price">' + escapeHtml(item[config.displayKey] || money(amount)) + '</strong>' +
                    '</div>' +
                '</div>' +
                '<div class="commerce-card-action">' +
                    '<button type="button" class="commerce-buy" data-commerce-action="' + config.action + '" data-id="' + id + '" data-amount="' + amount + '">' +
                        '<i class="bi bi-wallet2" aria-hidden="true"></i> خرید' +
                    '</button>' +
                '</div>' +
            '</article>';
        });
        target.innerHTML = html;
    }

    function initService() {
        var target = byId('serviceList');
        requestJson('/api/Service/List', function (items) {
            var count = byId('serviceCount');
            if (count) count.textContent = items.length;
            renderSimpleCards(target, items, {
                idKey: 'serviceID', amountKey: 'amount', titleKey: 'serviceDesc',
                displayKey: 'amountDisplay', icon: 'bi-stars', action: 'buy-service',
                emptyTitle: 'سرویسی برای خرید وجود ندارد', emptyDetail: 'بعداً دوباره بررسی کنید.'
            });
        }, target);
    }

    function initTicket() {
        var target = byId('ticketList');
        requestJson('/api/Ticket/TarefeList', function (items) {
            var count = byId('ticketCount');
            if (count) count.textContent = items.length;
            items.forEach(function (item) {
                item.commerceTitle = item.tarefe || '-';
                item.commerceSubtitle = item.sans || '';
            });
            renderSimpleCards(target, items, {
                idKey: 'tarefeID', amountKey: 'amount', titleKey: 'commerceTitle',
                subtitleKey: 'commerceSubtitle', displayKey: 'amountDisplay', icon: 'bi-ticket-perforated', action: 'buy-ticket',
                emptyTitle: 'تعرفه‌ای برای خرید وجود ندارد', emptyDetail: 'تعرفه‌های فعال این شیفت نمایش داده می‌شوند.'
            });
        }, target);
    }

    function renderCategories(items) {
        var target = byId('catList');
        if (!target) return;

        var html = '<button type="button" class="commerce-category-button active" data-commerce-action="category" data-category-id="0">' +
            '<i class="bi bi-grid-1x2-fill ms-1" aria-hidden="true"></i> همه</button>';
        (items || []).forEach(function (item) {
            var id = integer(item.stuffCategoryID);
            html += '<button type="button" class="commerce-category-button" data-commerce-action="category" data-category-id="' + id + '">' +
                escapeHtml(item.categoryDesc || '-') + '</button>';
        });
        target.innerHTML = html;
    }

    function markCategoryActive(id) {
        var buttons = document.querySelectorAll('#catList [data-category-id]');
        for (var i = 0; i < buttons.length; i++) {
            buttons[i].classList.toggle('active', integer(buttons[i].getAttribute('data-category-id')) === id);
        }
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
            var id = integer(item.stuffID);
            var price = numberValue(item.stuffAmount);
            state.products[id] = item;
            var left = stockLeft(id);
            var out = !!item.trackStock && left <= 0;
            html += '<article class="commerce-product-card' + (out ? ' is-out' : '') + '">' +
                '<div class="commerce-product-top">' +
                    '<div class="commerce-product-icon"><i class="bi ' + (state.isBuffet ? 'bi-cup-hot' : 'bi-bag-heart') + '" aria-hidden="true"></i></div>' +
                    '<div class="commerce-product-body">' +
                        '<h3 class="commerce-product-title">' + escapeHtml(item.stuffDesc || '-') + '</h3>' +
                        '<strong class="commerce-price">' + escapeHtml(item.amountDisplay || money(price)) + '</strong>' +
                        (item.stockDisplay ? '<span class="commerce-stock">' + escapeHtml(item.stockDisplay) + '</span>' : '') +
                    '</div>' +
                '</div>' +
                '<div class="commerce-product-action">' +
                    '<label class="commerce-visually-hidden" for="commerce-count-' + id + '">تعداد ' + escapeHtml(item.stuffDesc || 'کالا') + '</label>' +
                    '<input id="commerce-count-' + id + '" class="commerce-quantity" type="number" min="1" max="' + Math.max(1, left) + '" step="1" value="1" inputmode="numeric" data-commerce-count="' + id + '"' + (out ? ' disabled' : '') + '>' +
                    '<button type="button" class="commerce-add" data-commerce-action="add-product" data-id="' + id + '"' + (out ? ' disabled aria-disabled="true"' : '') + '>' +
                        (out ? '<i class="bi bi-x-circle" aria-hidden="true"></i> ناموجود'
                             : '<i class="bi bi-cart-plus" aria-hidden="true"></i> افزودن') +
                    '</button>' +
                '</div>' +
            '</article>';
        });
        target.innerHTML = html;
    }

    // موجودی قابل خرید یک کالا (کالای بدون کنترل انبار: ۱۰۰)
    function stockLeft(id) {
        var product = state.products[id];
        if (!product || !product.trackStock) return 100;
        return Math.max(0, Math.min(100, integer(product.availableCount, 0)));
    }

    function loadProducts(categoryId) {
        state.categoryId = integer(categoryId);
        markCategoryActive(state.categoryId);
        var target = byId('stuffList');
        setState(target, 'loading', 'در حال بارگذاری کالاها…');
        var serial = ++state.requestSerial;
        var url = '/api/Shop/Stuffs?isBuffet=' + (state.isBuffet ? 'true' : 'false');
        if (state.categoryId > 0) url += '&categoryID=' + state.categoryId;
        requestJson(url, function (items) {
            if (serial !== state.requestSerial) return;
            var count = byId('productCount');
            if (count) count.textContent = items.length;
            renderProducts(items);
        }, target);
    }

    function loadShop() {
        var categoryTarget = byId('catList');
        requestJson('/api/Shop/Categories?isBuffet=' + (state.isBuffet ? 'true' : 'false'), renderCategories, categoryTarget);
        loadProducts(0);
    }

    function basketLine(id) {
        for (var i = 0; i < state.basket.length; i++) {
            if (state.basket[i].stuffID === id) return state.basket[i];
        }
        return null;
    }

    function renderBasket() {
        var target = byId('basketList');
        var footer = byId('basketTotal');
        var totalNode = byId('totalAmount');
        var countNode = byId('basketCount');
        if (!state.basket.length) {
            target.innerHTML = '<div class="commerce-empty"><i class="bi bi-cart-x" aria-hidden="true"></i><span>سبد خرید خالی است</span></div>';
            footer.style.display = 'none';
            if (countNode) countNode.textContent = '۰';
            return;
        }

        var total = 0;
        var count = 0;
        var html = '';
        state.basket.forEach(function (item) {
            var lineTotal = item.unitPrice * item.count;
            total += lineTotal;
            count += item.count;
            html += '<div class="commerce-cart-item">' +
                '<div>' +
                    '<div class="commerce-cart-item-name">' + escapeHtml(item.name) + '</div>' +
                    '<div class="commerce-cart-item-meta">' + escapeHtml(money(item.unitPrice)) + ' × ' + escapeHtml(String(item.count)) + '</div>' +
                '</div>' +
                '<div class="commerce-cart-item-controls">' +
                    '<button type="button" class="commerce-step" aria-label="افزایش تعداد" data-commerce-action="increase" data-id="' + item.stuffID + '"><i class="bi bi-plus" aria-hidden="true"></i></button>' +
                    '<strong class="commerce-cart-item-meta" aria-label="تعداد">' + escapeHtml(String(item.count)) + '</strong>' +
                    '<button type="button" class="commerce-step" aria-label="کاهش تعداد" data-commerce-action="decrease" data-id="' + item.stuffID + '"><i class="bi bi-dash" aria-hidden="true"></i></button>' +
                    '<button type="button" class="commerce-remove" aria-label="حذف از سبد" data-commerce-action="remove" data-id="' + item.stuffID + '"><i class="bi bi-trash3" aria-hidden="true"></i></button>' +
                '</div>' +
            '</div>';
        });
        target.innerHTML = html;
        totalNode.textContent = money(total);
        if (countNode) countNode.textContent = count;
        footer.style.display = 'flex';
    }

    function addProduct(id) {
        var product = state.products[id];
        if (!product) return;
        var left = stockLeft(id);
        if (left <= 0) {
            showToast('این کالا موجود نیست', 'warning');
            return;
        }
        var input = document.querySelector('[data-commerce-count="' + id + '"]');
        var count = Math.max(1, Math.min(left, integer(input && input.value, 1)));
        var existing = basketLine(id);
        if (existing) existing.count = Math.min(left, existing.count + count);
        else state.basket.push({ stuffID: id, name: product.stuffDesc || '-', unitPrice: numberValue(product.stuffAmount), count: count });
        renderBasket();
        if (left < 100 && existing && existing.count >= left) showToast('بیشتر از موجودی انبار نمی‌توانید اضافه کنید', 'warning');
        else showToast('کالا به سبد خرید اضافه شد', 'success');
    }

    function changeBasket(id, delta) {
        var item = basketLine(id);
        if (!item) return;
        item.count = Math.max(0, Math.min(stockLeft(id), item.count + delta));
        if (item.count === 0) state.basket = state.basket.filter(function (x) { return x.stuffID !== id; });
        renderBasket();
    }

    function checkout() {
        if (!state.basket.length) {
            showToast('سبد خرید خالی است', 'warning');
            return;
        }
        var total = 0;
        state.basket.forEach(function (item) { total += item.unitPrice * item.count; });
        var items = state.basket.map(function (item) {
            return { stuffID: item.stuffID, unitPrice: item.unitPrice, count: item.count };
        });
        requestPurchase({
            amount: total,
            creditType: 'buffet',
            buyUrl: '/api/Shop/Buy',
            payload: { items: items, isBuffet: state.isBuffet },
            title: state.isBuffet ? 'پرداخت بوفه' : 'پرداخت فروشگاه',
            onDone: function (res) {
                showToast(res.message || 'خرید با موفقیت ثبت شد', 'success');
                state.basket = [];
                renderBasket();
                if (typeof window.refreshDashboardCredits === 'function') window.refreshDashboardCredits();
            }
        });
    }

    function handleAction(button) {
        var action = button.getAttribute('data-commerce-action');
        var id = integer(button.getAttribute('data-id'));
        if (action === 'buy-service') {
            requestPurchase({
                amount: numberValue(button.getAttribute('data-amount')),
                creditType: 'service', buyUrl: '/api/Service/Buy',
                payload: { serviceID: id }, title: 'پرداخت سرویس',
                onDone: function (res) {
                    showToast(res.message || 'خرید سرویس با موفقیت ثبت شد', 'success');
                    if (typeof window.refreshDashboardCredits === 'function') window.refreshDashboardCredits();
                }
            });
        } else if (action === 'buy-ticket') {
            requestPurchase({
                amount: numberValue(button.getAttribute('data-amount')),
                creditType: 'sport', buyUrl: '/api/Ticket/Buy',
                payload: { tarefeID: id }, title: 'پرداخت بلیت',
                onDone: function (res) {
                    showToast(res.message || 'بلیت با موفقیت صادر شد', 'success');
                    if (typeof window.refreshDashboardCredits === 'function') window.refreshDashboardCredits();
                }
            });
        } else if (action === 'category') {
            loadProducts(integer(button.getAttribute('data-category-id')));
        } else if (action === 'add-product') {
            addProduct(id);
        } else if (action === 'increase') {
            changeBasket(id, 1);
        } else if (action === 'decrease') {
            changeBasket(id, -1);
        } else if (action === 'remove') {
            state.basket = state.basket.filter(function (item) { return item.stuffID !== id; });
            renderBasket();
        }
    }

    root.addEventListener('click', function (event) {
        var button = actionFromTarget(event.target);
        if (button) handleAction(button);
    });

    if (pageType === 'service') initService();
    else if (pageType === 'ticket') initTicket();
    else if (pageType === 'shop' || pageType === 'buffet') {
        byId('btnBuyBasket').addEventListener('click', checkout);
        loadShop();
        renderBasket();
    }
})();
