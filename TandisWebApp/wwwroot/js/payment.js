/* ============================================================
   پرداخت دو حالته (اعتبار / دستگاه POS) برای پنل عضو
   ------------------------------------------------------------
   openPaymentModal({
       amount: 50000,                // مبلغ به ریال
       creditType: 'sport',          // sport | buffet | service
       buyUrl: '/api/Shop/Buy',      // endpoint خرید
       payload: { items: [...] },    // بدنه اصلی درخواست خرید
       title: 'پرداخت خرید',         // اختیاری
       onDone: function (res) { }    // بعد از خرید موفق (اعتبار یا POS تاییدشده)
   });

   جریان POS:
     1) POST buyUrl {payload, method:'pos'}          → paymentPending + posTxnId
     2) poll  /api/PosPayment/Status?txnId=...       تا success / failed / expired
     3) POST buyUrl {payload, method:'pos', posTxnId} → نهایی‌سازی خرید
   ============================================================ */
(function () {
    var POLL_INTERVAL_MS = 2000;
    var MAX_POLLS = 150; // ≈ 5 دقیقه (TtlMinutes سرور)

    var st = {
        opts: null,
        txnId: null,
        timer: null,
        polls: 0,
        credit: null,   // null = هنوز نامعلوم
        busy: false
    };

    function fmtMoney(n) {
        return Number(n || 0).toLocaleString('en-US');
    }

    function ensureModal() {
        if (document.getElementById('posPayModal')) return;
        var html =
            '<div class="modal fade" id="posPayModal" tabindex="-1" data-bs-backdrop="static" data-bs-keyboard="false">' +
            '  <div class="modal-dialog modal-dialog-centered">' +
            '    <div class="modal-content">' +
            '      <div class="modal-header">' +
            '        <h5 class="modal-title" id="posPayTitle">پرداخت</h5>' +
            '        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>' +
            '      </div>' +
            '      <div class="modal-body text-center">' +
            // ---- مرحله انتخاب ----
            '        <div id="posPayStepChoose">' +
            '          <div class="text-muted">مبلغ قابل پرداخت</div>' +
            '          <div id="posPayAmount" style="font-size:1.7rem;font-weight:bold" class="mb-2">0 ریال</div>' +
            '          <div class="mb-3">اعتبار شما: <strong id="posPayCredit">...</strong></div>' +
            '          <div id="posPayCreditNote" class="alert alert-danger py-2" style="display:none">' +
            '            اعتبار کافی نیست — فقط پرداخت با دستگاه POS فعال است</div>' +
            '          <div class="d-grid gap-2">' +
            '            <button type="button" id="posPayBtnCredit" class="btn btn-success btn-lg">💳 پرداخت از اعتبار</button>' +
            '            <button type="button" id="posPayBtnPos" class="btn btn-primary btn-lg">🏧 پرداخت با دستگاه POS</button>' +
            '          </div>' +
            '        </div>' +
            // ---- مرحله انتظار POS ----
            '        <div id="posPayStepWaiting" style="display:none">' +
            '          <div class="spinner-border text-primary my-3" role="status"></div>' +
            '          <div id="posPayWaitingText" style="font-size:1.05rem">در حال ارسال مبلغ به دستگاه...</div>' +
            '          <div class="text-muted mt-2">مبلغ: <span id="posPayWaitAmount"></span> ریال</div>' +
            '          <div id="posPayQueueInfo" class="alert alert-info py-2 mt-2 mb-0" style="display:none"></div>' +
            '          <div class="small text-muted mt-1">دستگاه POS را به متصدی بدهید تا مبلغ تایید و کارت کشیده شود</div>' +
            '          <button type="button" id="posPayBtnBack" class="btn btn-outline-secondary mt-3" style="display:none">بازگشت</button>' +
            '        </div>' +
            '      </div>' +
            '    </div>' +
            '  </div>' +
            '</div>';
        document.body.insertAdjacentHTML('beforeend', html);

        $('#posPayBtnCredit').click(onCreditClick);
        $('#posPayBtnPos').click(onPosClick);
        $('#posPayBtnBack').click(function () {
            stopPoll();
            if (typeof st.retryFn === 'function') {
                var f = st.retryFn;
                st.retryFn = null;
                f();
            } else {
                showStep('choose');
            }
        });
        $('#posPayModal').on('hidden.bs.modal', function () {
            var wasPolling = st.timer !== null;
            stopPoll();
            // اگر هنوز منتظر بودیم و کاربر مودال را بست → انصراف ثبت شود
            if (wasPolling) cancelPending();
        });
    }

    function showStep(name) {
        if (name === 'choose') st.retryFn = null;
        $('#posPayStepChoose').toggle(name === 'choose');
        $('#posPayStepWaiting').toggle(name !== 'choose');
        if (name !== 'choose') {
            $('#posPayBtnBack').toggle(name === 'error');
            if (name === 'sending') {
                $('#posPayWaitingText').text('در حال ارسال مبلغ به دستگاه...');
                $('#posPayModal .spinner-border').show();
            } else if (name === 'waiting') {
                $('#posPayWaitingText').text('در صف پرداخت — لطفاً منتظر بمانید...');
                $('#posPayQueueInfo').hide();
                $('#posPayModal .spinner-border').show();
            } else if (name === 'confirming') {
                $('#posPayWaitingText').text('پرداخت انجام شد — در حال ثبت خرید...');
                $('#posPayModal .spinner-border').show();
            } else if (name === 'error') {
                $('#posPayModal .spinner-border').hide();
            }
        }
    }

    function setBusy(b) {
        st.busy = b;
        $('#posPayBtnCredit, #posPayBtnPos').prop('disabled', b || false);
        refreshCreditButton();
    }

    function refreshCreditButton() {
        if (st.busy) { $('#posPayBtnCredit').prop('disabled', true); return; }
        var enough = st.credit !== null && st.credit >= (st.opts ? st.opts.amount : 0);
        $('#posPayBtnCredit').prop('disabled', !enough);
        $('#posPayCreditNote').toggle(st.credit !== null && !enough);
    }

    function loadCredit() {
        st.credit = null;
        $('#posPayCredit').text('...');
        refreshCreditButton();
        apiCall('/api/Profile/GetProfile', 'GET', null, function (res) {
            if (!res.success || !res.data) return;
            var d = res.data;
            var c = 0;
            if (st.opts.creditType === 'buffet') c = d.buffetCredit;
            else if (st.opts.creditType === 'service') c = d.serviceCredit;
            else c = d.sportCredit;
            st.credit = Number(c || 0);
            $('#posPayCredit').text(fmtMoney(st.credit) + ' ریال');
            refreshCreditButton();
        }, function () {
            // در صورت خطا، دکمه اعتبار فعال می‌ماند (سرور خودش چک می‌کند)
            st.credit = st.opts ? st.opts.amount : 0;
            refreshCreditButton();
        });
    }

    // ============================================================
    //  پرداخت از اعتبار
    // ============================================================
    function onCreditClick() {
        if (st.busy) return;
        setBusy(true);
        var body = Object.assign({}, st.opts.payload, { method: 'credit', posTxnId: null });
        apiCall(st.opts.buyUrl, 'POST', body, function (res) {
            setBusy(false);
            if (res.success) {
                $('#posPayModal').modal('hide');
                if (typeof st.opts.onDone === 'function') st.opts.onDone(res);
            }
        }, function () { setBusy(false); });
    }

    // ============================================================
    //  پرداخت با POS
    // ============================================================
    function onPosClick() {
        if (st.busy) return;
        setBusy(true);
        showStep('sending');
        $('#posPayWaitAmount').text(fmtMoney(st.opts.amount));

        var body = Object.assign({}, st.opts.payload, { method: 'pos', posTxnId: null });
        apiCall(st.opts.buyUrl, 'POST', body, function (res) {
            if (res.paymentPending && res.posTxnId) {
                st.txnId = res.posTxnId;
                if (res.posAmount) {
                    st.opts.amount = Number(res.posAmount); // مبلغ مرجع = سرور
                    $('#posPayAmount').text(fmtMoney(st.opts.amount) + ' ریال');
                    $('#posPayWaitAmount').text(fmtMoney(st.opts.amount));
                }
                showStep('waiting');
                st.polls = 0;
                st.timer = setInterval(pollStatus, POLL_INTERVAL_MS);
                setBusy(true); // در حین انتظار غیرفعال
            } else {
                setBusy(false);
                showStep('choose');
            }
        }, function () {
            setBusy(false);
            showStep('choose');
        });
    }

    function stopPoll() {
        if (st.timer) { clearInterval(st.timer); st.timer = null; }
    }

    // وضعیت صف همزمان: روی دستگاه است / در نوبت است
    function updateWaiting(d) {
        if (d.processing) {
            $('#posPayWaitingText').text('مبلغ روی دستگاه نمایش داده شد — لطفاً کارت بکشید');
            $('#posPayQueueInfo').hide();
        } else if (d.queuePosition > 0) {
            $('#posPayWaitingText').text('در نوبت پرداخت هستید...');
            $('#posPayQueueInfo')
                .text(d.queuePosition + ' درخواست جلوتر از شما در صف است — تا اتمام آن‌ها صبر کنید')
                .show();
        } else {
            $('#posPayWaitingText').text('نوبت شماست — لحظاتی دیگر مبلغ روی دستگاه نمایش داده می‌شود');
            $('#posPayQueueInfo').hide();
        }
    }

    // انصراف تراکنش در انتظار (بدون انتظار پاسخ - به‌صورت fire-and-forget)
    function cancelPending() {
        if (!st.txnId) return;
        var id = st.txnId;
        st.txnId = null;
        fetch('/api/PosPayment/Cancel?txnId=' + id,
            { method: 'POST', credentials: 'include' }).catch(function () { });
    }

    function pollStatus() {
        st.polls++;
        if (st.polls > MAX_POLLS) {
            stopPoll();
            failAndBack('مهلت پرداخت به پایان رسید. لطفاً دوباره تلاش کنید.');
            return;
        }
        fetch('/api/PosPayment/Status?txnId=' + st.txnId, { credentials: 'include' })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                var d = res && res.data;
                if (!d) return; // شبکه/پاسخ نامعتبر → دور بعدی دوباره
                if (d.status === 'pending') { updateWaiting(d); return; }
                stopPoll();
                if (d.status === 'success') doConfirm();
                else failAndBack(d.message || 'پرداخت ناموفق بود');
            })
            .catch(function () { /* خطای موقت شبکی → ادامه poll */ });
    }

    function doConfirm() {
        showStep('confirming');
        var body = Object.assign({}, st.opts.payload, { method: 'pos', posTxnId: st.txnId });
        apiCall(st.opts.buyUrl, 'POST', body, function (res) {
            setBusy(false);
            if (res.success) {
                $('#posPayModal').modal('hide');
                if (typeof st.opts.onDone === 'function') st.opts.onDone(res);
            } else {
                // پول کسر شده ولی ثبت ناموفق → تلاش مجدد (تراکنش هنوز مصرف نشده)
                showError('مبلغ از کارت کسر شد اما ثبت خرید ناموفق بود: ' +
                    (res.message || ''), doConfirm);
            }
        }, function (err) {
            setBusy(false);
            var m = (err && err.message) ? err.message : '';
            showError('پول کسر شد ولی ثبت خرید کامل نشد (' + m + ').', doConfirm);
        });
    }

    function failAndBack(msg) {
        setBusy(false);
        st.retryFn = null;
        cancelPending();
        showStep('choose');
        showToast(msg, 'error');
    }

    function showError(msg, retryFn) {
        setBusy(false);
        st.retryFn = retryFn || null;
        showStep('error');
        $('#posPayBtnBack').text(retryFn ? 'تلاش مجدد ثبت خرید' : 'بازگشت');
        $('#posPayWaitingText').text(msg);
        showToast(msg, 'error');
    }

    // ============================================================
    //  ورودی اصلی
    // ============================================================
    window.openPaymentModal = function (opts) {
        if (!opts || !opts.buyUrl) return;
        ensureModal();
        stopPoll();
        st.opts = opts;
        st.txnId = null;
        st.busy = false;

        $('#posPayTitle').text(opts.title || 'پرداخت');
        $('#posPayAmount').text(fmtMoney(opts.amount) + ' ریال');
        $('#posPayWaitAmount').text(fmtMoney(opts.amount));

        setBusy(false);
        showStep('choose');
        $('#posPayModal').modal('show');
        loadCredit();
    };
})();
