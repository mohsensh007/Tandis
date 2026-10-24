var faDigits = ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'];
function toFa(n) { return String(n).replace(/[0-9]/g, function(d){ return faDigits[d]; }); }
function fmtNum(n) { return Number(n || 0).toLocaleString('fa-IR'); }

function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// ========== بارگذاری تعداد برنامه‌های فعال ==========
function loadMyProgramCount() {
    apiCall('/api/MyProgram/ActiveCount', 'GET', null, function (res) {
        var $c = $('#myProgramCount');
        var $sub = $('#mpEntrySub');
        if (!res || !res.success) return;
        var cnt = Number(res.count || 0);
        if (cnt > 0) {
            $c.text(toFa(cnt) + ' برنامه فعال').css('display', 'inline-block');
            $sub.text('برای مشاهده برنامه‌ها بزنید');
        } else {
            $c.hide();
            $sub.text('هنوز برنامه‌ای از مربی دریافت نکرده‌اید');
        }
    });
}
loadMyProgramCount();

// ============================================================
//  ✅ مودال تک جلسه
// ============================================================
var singleSessionModal = null;
var ssMode = 'today';
var ssConfirmCallback = null;

function initSingleSessionModal() {
    var modalEl = document.getElementById('singleSessionModal');
    if (!modalEl) return null;
    if (!singleSessionModal) {
        singleSessionModal = new bootstrap.Modal(modalEl);
        modalEl.addEventListener('shown.bs.modal', function () { loadSingleSessionList(); });
    }
    return singleSessionModal;
}
function openSingleSessionModal() {
    var modal = initSingleSessionModal();
    if (modal) modal.show();
}

function showSsConfirm(html, onOk) {
    $('#ssConfirmText').html(html);
    ssConfirmCallback = onOk;
    $('#ssConfirmOverlay').addClass('open');
}
function hideSsConfirm() {
    $('#ssConfirmOverlay').removeClass('open');
    ssConfirmCallback = null;
}
$(document).on('click', '#ssConfirmCancel', hideSsConfirm);
$(document).on('click', '.ss-confirm-backdrop', hideSsConfirm);
$(document).on('click', '#ssConfirmOk', function () {
    var cb = ssConfirmCallback;
    hideSsConfirm();
    if (cb) cb();
});

var ssWeekData = [];
var ssSelectedDate = '';

function loadSingleSessionList() {
    var $list = $('#singleSessionList');
    var $days = $('#ssDays');
    var loadingHtml = '<div class="text-center py-4" style="color:#9fb3c8;">' +
        '<div class="spinner-border spinner-border-sm" role="status"></div> ' +
        'در حال بارگذاری...</div>';

    if (ssMode === 'today') {
        $days.hide().empty();
        ssWeekData = [];
        ssSelectedDate = '';
        $list.html(loadingHtml);
        apiCall('/api/Ticket/SingleSessionList?days=0', 'GET', null, function (res) {
            renderSsList(res && res.success ? res.data : [], false);
        });
        return;
    }
    $list.html(loadingHtml);
    $days.hide().empty();
    apiCall('/api/Ticket/SingleSessionList?days=7', 'GET', null, function (res) {
        ssWeekData = (res && res.success && res.data) ? res.data : [];
        buildSsDayChips(ssWeekData);
        if (ssWeekData.length === 0) { renderSsList([], true); return; }
        selectSsDay(ssWeekData[0].sessionDateShamsi);
    });
}

function buildSsDayChips(data) {
    var $days = $('#ssDays');
    var groups = [];
    (data || []).forEach(function (s) {
        var d = s.sessionDateShamsi || '';
        var g = null;
        for (var i = 0; i < groups.length; i++) { if (groups[i].date === d) { g = groups[i]; break; } }
        if (!g) { g = { date: d, dayName: s.dayName || '', count: 0 }; groups.push(g); }
        g.count++;
    });
    groups.sort(function (a, b) { return a.date < b.date ? -1 : (a.date > b.date ? 1 : 0); });
    if (!groups.length) { $days.hide().empty(); return; }
    var html = '';
    groups.forEach(function (g) {
        html += '<button type="button" class="ss-day" data-date="' + esc(g.date) + '">' +
            '<span class="ss-day-name">' + esc(g.dayName) + '</span>' +
            '<span class="ss-day-date">' + esc(g.date) + '</span>' +
            '<span class="ss-day-count">' + toFa(g.count) + ' سانس</span>' +
        '</button>';
    });
    $days.html(html).show();
}

function selectSsDay(date) {
    ssSelectedDate = date || '';
    $('.ss-day').removeClass('active').filter('[data-date="' + date + '"]').addClass('active');
    var rows = (ssWeekData || []).filter(function (s) { return s.sessionDateShamsi === date; });
    renderSsList(rows, true);
}
$(document).on('click', '.ss-day', function () { selectSsDay($(this).attr('data-date')); });

function renderSsList(rows, isWeek) {
    var $list = $('#singleSessionList');
    if (!rows || rows.length === 0) {
        var emptyMsg;
        if (isWeek) {
            emptyMsg = ssSelectedDate
                ? 'در تاریخ ' + ssSelectedDate + ' هیچ سانس تک‌جلسه‌ای وجود ندارد'
                : 'تا ۷ روز آینده هیچ سانس تک‌جلسه‌ای ثبت نشده است';
        } else {
            emptyMsg = 'امروز هیچ سانس تک‌جلسه‌ای در دسترس نیست';
        }
        $list.html('<div class="ss-empty">' +
            '<div class="ss-empty-icon"><i class="bi bi-inbox" aria-hidden="true"></i></div>' +
            '<div class="fw-bold text-white mb-1">' + emptyMsg + '</div>' +
            '<div class="small">فقط سانس‌هایی که جلسه دارن و <strong>SessionAmount &gt; 0</strong> باشن نمایش داده می‌شن.</div>' +
            '</div>');
        return;
    }
    var html = '';
    $.each(rows, function (i, s) {
        var timeText = (s.startTime || '') + (s.endTime ? ' تا ' + s.endTime : '');
        html += '<div class="ss-card">' +
            '<div class="ss-title">' + esc(s.sportName) + ' - ' + esc(s.sanseName) + '</div>' +
            '<div class="ss-sub"><i class="bi bi-person-fill"></i> ' + esc(s.coachName || '-') + '</div>' +
            '<div class="ss-row">' +
                '<div class="d-flex gap-2 flex-wrap">' +
                    (timeText ? '<span class="ss-time-pill"><i class="bi bi-clock"></i> ' + esc(timeText) + '</span>' : '') +
                '</div>' +
                '<div class="ss-price">' + esc(s.sessionAmountDisplay) + '</div>' +
            '</div>' +
            '<div class="ss-row" style="margin-top:12px;">' +
                '<div></div>' +
                '<button class="ss-btn" onclick="buySingleSession(' + s.sportSanseID + ', \'' + esc(s.sessionDateShamsi) + '\', \'' + esc(s.sessionAmountDisplay) + '\', this)">' +
                    '<i class="bi bi-lightning-charge-fill"></i> ثبت تک جلسه' +
                '</button>' +
            '</div>' +
        '</div>';
    });
    $list.html(html);
}

$(document).on('click', '.ss-tab', function () {
    var mode = $(this).attr('data-mode');
    if (mode === ssMode) return;
    ssMode = mode;
    $('.ss-tab').removeClass('active');
    $(this).addClass('active');
    loadSingleSessionList();
});

window.buySingleSession = function (sanseID, sessionDate, amountDisplay, btn) {
    var dateLabel = (!sessionDate || sessionDate === '') ? 'امروز' : sessionDate;
    var amount = parseInt(String(amountDisplay || '0').replace(/[^0-9]/g, ''), 10) || 0;
    openPaymentModal({
        amount: amount,
        creditType: 'sport',
        buyUrl: '/api/Ticket/BuySingleSession',
        payload: { sportSanseID: sanseID, sessionDateShamsi: sessionDate || null },
        title: 'پرداخت تک جلسه (' + dateLabel + ')',
        onDone: function (res) {
            if (typeof showToast === 'function') showToast(res.message, 'success');
            else alert(res.message);
            if (singleSessionModal) singleSessionModal.hide();
            loadDashboardData();
            setTimeout(function () { location.reload(); }, 1500);
        }
    });
};

$(document).on('click', '#navSingleSession', function (e) { e.preventDefault(); openSingleSessionModal(); });

// ============================================================
//  ✅ مودال تعرفه‌ها (۴ تب + فیلتر چک‌باکسی شهریه)
// ============================================================
var tarefeModal = null;
var tfTab = 'shahriye';
var tfCategories = [];
var tfSelectedCatIDs = [];
var tfDataCache = { shahriye: null, ticket: null, service: null, single: null };

function initTarefeModal() {
    var modalEl = document.getElementById('tarefeModal');
    if (!modalEl) return null;
    if (!tarefeModal) {
        tarefeModal = new bootstrap.Modal(modalEl);
        modalEl.addEventListener('shown.bs.modal', function () { loadTarefeTab(); });
    }
    return tarefeModal;
}
function openTarefeModal() {
    var modal = initTarefeModal();
    if (modal) modal.show();
}
$(document).on('click', '#navTarefe', function (e) { e.preventDefault(); openTarefeModal(); });

$(document).on('click', '.tf-tab', function () {
    var tab = $(this).attr('data-tf');
    if (tab === tfTab) return;
    tfTab = tab;
    $('.tf-tab').removeClass('active');
    $(this).addClass('active');
    $('#tfFilterBar').toggle(tab === 'shahriye');
    $('#tfFilterPanel').hide();
    loadTarefeTab();
});

$(document).on('click', '#btnTfFilter', function () {
    $('#tfFilterPanel').slideToggle(180);
});
$(document).on('change', '#tfFilterList input[type="checkbox"]', function () {
    var id = Number($(this).val());
    var idx = tfSelectedCatIDs.indexOf(id);
    if (this.checked && idx === -1) tfSelectedCatIDs.push(id);
    if (!this.checked && idx !== -1) tfSelectedCatIDs.splice(idx, 1);
    updateTfFilterLabel();
    renderTarefeShahriye();
});
$(document).on('click', '#tfSelAll', function () {
    tfSelectedCatIDs = tfCategories.map(function (c) { return c.sportCatID; });
    $('#tfFilterList input[type="checkbox"]').prop('checked', true);
    updateTfFilterLabel();
    renderTarefeShahriye();
});
$(document).on('click', '#tfSelNone', function () {
    tfSelectedCatIDs = [];
    $('#tfFilterList input[type="checkbox"]').prop('checked', false);
    updateTfFilterLabel();
    renderTarefeShahriye();
});
$(document).on('click', '#btnTfApply', function () {
    $('#tfFilterPanel').slideUp(180);
});

function buildTfFilterPanel() {
    var $list = $('#tfFilterList');
    var html = '';
    $.each(tfCategories, function (i, c) {
        var checked = tfSelectedCatIDs.indexOf(c.sportCatID) !== -1;
        html += '<label class="tf-check">' +
            '<input type="checkbox" value="' + c.sportCatID + '"' + (checked ? ' checked' : '') + '>' +
            '<span class="tf-check-box"><i class="bi bi-check-lg"></i></span>' +
            '<span class="tf-check-name">' + esc(c.sportName) + '</span>' +
        '</label>';
    });
    $list.html(html);
}

function updateTfFilterLabel() {
    var $lbl = $('#tfFilterLabel');
    var $cnt = $('#tfFilterCount');
    if (tfSelectedCatIDs.length === 0) {
        $lbl.text('نمایش: همه رشته‌ها');
        $cnt.hide();
        $('#btnTfFilter').removeClass('has-filter');
    } else {
        var names = [];
        $.each(tfCategories, function (i, c) {
            if (tfSelectedCatIDs.indexOf(c.sportCatID) !== -1) names.push(c.sportName);
        });
        if (names.length <= 2) $lbl.text('نمایش: ' + names.join('، '));
        else $lbl.text('نمایش: ' + toFa(names.length) + ' رشته انتخاب‌شده');
        $cnt.text(toFa(names.length)).show();
        $('#btnTfFilter').addClass('has-filter');
    }
}

function loadTarefeTab() {
    var $list = $('#tarefeList');
    $('#tfFilterBar').toggle(tfTab === 'shahriye');
    $list.html('<div class="text-center py-4" style="color:#9fb3c8;">' +
        '<div class="spinner-border spinner-border-sm" role="status"></div> ' +
        'در حال بارگذاری...</div>');

    if (tfCategories.length === 0 && tfTab === 'shahriye') {
        apiCall('/api/Register/SportCategories', 'GET', null, function (res) {
            tfCategories = (res && res.success && res.data) ? res.data : [];
            buildTfFilterPanel();
            updateTfFilterLabel();
            loadTarefeData();
        });
        return;
    }
    loadTarefeData();
}

function loadTarefeData() {
    var tab = tfTab;
    if (tfDataCache[tab] !== null) { renderTarefeTab(tab, tfDataCache[tab]); return; }
    var url = '';
    if (tab === 'shahriye') url = '/api/Register/SansesForRenew';
    else if (tab === 'ticket') url = '/api/Ticket/TarefeList';
    else if (tab === 'service') url = '/api/Service/List';
    else if (tab === 'single') url = '/api/Ticket/SingleSessionList?days=0';
    apiCall(url, 'GET', null, function (res) {
        var data = (res && res.success && res.data) ? res.data : [];
        tfDataCache[tab] = data;
        renderTarefeTab(tab, data);
    });
}

function renderTarefeTab(tab, data) {
    if (tab === 'shahriye') renderTarefeShahriye();
    else if (tab === 'ticket') renderTarefeTicket(data);
    else if (tab === 'service') renderTarefeService(data);
    else if (tab === 'single') renderTarefeSingle(data);
}

function renderTarefeShahriye() {
    var all = tfDataCache.shahriye || [];
    var data = (tfSelectedCatIDs.length === 0) ? all : all.filter(function (s) {
        return tfSelectedCatIDs.indexOf(Number(s.sportCatID)) !== -1;
    });
    renderTarefeGenericList(data, 'shahriye', 'bi-journal-text',
        tfSelectedCatIDs.length === 0
            ? 'هیچ سانس ورزشی تعریف نشده است'
            : 'هیچ سانسی در رشته‌های انتخاب‌شده یافت نشد');
}

function renderTarefeTicket(data) {
    renderTarefeGenericList(data, 'ticket', 'bi-ticket-perforated-fill', 'هیچ تعرفه بلیطی تعریف نشده است');
}
function renderTarefeService(data) {
    renderTarefeGenericList(data, 'service', 'bi-flower1', 'هیچ خدمتی تعریف نشده است');
}
function renderTarefeSingle(data) {
    renderTarefeGenericList(data, 'single', 'bi-lightning-charge-fill', 'هیچ سانس تک‌جلسه‌ای تعریف نشده است');
}

function renderTarefeGenericList(data, tab, emptyIcon, emptyMsg) {
    var $list = $('#tarefeList');
    if (!data || data.length === 0) {
        $list.html('<div class="tf-empty">' +
            '<div class="tf-empty-icon"><i class="bi ' + emptyIcon + '" aria-hidden="true"></i></div>' +
            '<div class="fw-bold text-white mb-1">' + emptyMsg + '</div>' +
            '</div>');
        return;
    }
    var html = '';
    $.each(data, function (i, item) {
        if (tab === 'shahriye') {
            html +=
                '<div class="tf-card">' +
                    '<div class="tf-title">' + esc(item.sportName) + ' - ' + esc(item.sanseName) + '</div>' +
                    '<div class="tf-detail"><i class="bi bi-person-fill"></i> ' + esc(item.coachName || '-') + '</div>' +
                    (item.membershipTypeDesc ? '<div class="tf-detail"><i class="bi bi-tag-fill"></i> ' + esc(item.membershipTypeDesc) + '</div>' : '') +
                    '<div class="tf-row" style="margin-top:8px;">' +
                        '<div class="d-flex gap-2 flex-wrap">' +
                            '<span class="tf-pill"><i class="bi bi-list-ol"></i> ' + toFa(item.sessionCountInPeriod || 0) + ' جلسه</span>' +
                            (item.periodDesc ? '<span class="tf-pill"><i class="bi bi-stopwatch-fill"></i> ' + esc(item.periodDesc) + '</span>' : '') +
                        '</div>' +
                        '<div class="tf-price">' + esc(item.totalAmountDisplay) + '</div>' +
                    '</div>' +
                '</div>';
        } else if (tab === 'ticket') {
            html +=
                '<div class="tf-card">' +
                    '<div class="tf-title">' + esc(item.tarefe || 'تعرفه') + '</div>' +
                    '<div class="tf-detail"><i class="bi bi-tag-fill"></i> سانس: ' + esc(item.sans || '-') + '</div>' +
                    '<div class="tf-row" style="margin-top:8px;">' +
                        '<div></div>' +
                        '<div class="tf-price">' + esc(item.amountDisplay) + '</div>' +
                    '</div>' +
                '</div>';
        } else if (tab === 'service') {
            html +=
                '<div class="tf-card">' +
                    '<div class="tf-title">' + esc(item.serviceDesc) + '</div>' +
                    '<div class="tf-row" style="margin-top:8px;">' +
                        '<div></div>' +
                        '<div class="tf-price">' + esc(item.amountDisplay) + '</div>' +
                    '</div>' +
                '</div>';
        } else if (tab === 'single') {
            var timeText = (item.startTime || '') + (item.endTime ? ' تا ' + item.endTime : '');
            html +=
                '<div class="tf-card">' +
                    '<div class="tf-title">' + esc(item.sportName) + ' - ' + esc(item.sanseName) + '</div>' +
                    '<div class="tf-detail"><i class="bi bi-person-fill"></i> ' + esc(item.coachName || '-') + '</div>' +
                    '<div class="tf-row" style="margin-top:8px;">' +
                        '<div class="d-flex gap-2 flex-wrap">' +
                            '<span class="tf-pill"><i class="bi bi-calendar3"></i> ' + esc(item.dayName) + '</span>' +
                            (timeText ? '<span class="tf-pill"><i class="bi bi-clock"></i> ' + esc(timeText) + '</span>' : '') +
                        '</div>' +
                        '<div class="tf-price">' + esc(item.sessionAmountDisplay) + '</div>' +
                    '</div>' +
                '</div>';
        }
    });
    $list.html(html);
}

// ========== منوی «نمایش همه» ==========
(function () {
    var overlay = document.getElementById('allMenuOverlay');
    if (!overlay) return;
    document.body.appendChild(overlay);
    var menuTrigger = document.getElementById('btnAllMenu');
    var previousFocus = null;
    var closeTimer = null;
    var $grid = $('#allMenuGrid');
    $('#navMenu .nav-card').each(function (i) {
        var href = $(this).attr('href');
        var icon = $(this).find('.nav-icon').html();
        var title = $(this).find('.nav-title').text();
        var action = $(this).attr('data-action');
        var itemHtml = '<a class="allmenu-item" style="transition-delay:' + (i * 50) + 'ms" href="' + href + '"';
        if (action) itemHtml += ' data-action="' + action + '"';
        itemHtml += '><span class="am-icon">' + icon + '</span><span>' + title + '</span></a>';
        $grid.append(itemHtml);
    });
    $grid.on('click', '.allmenu-item[data-action="singleSession"]', function (e) { e.preventDefault(); closeMenu(); setTimeout(openSingleSessionModal, 300); });
    $grid.on('click', '.allmenu-item[data-action="tarefe"]', function (e) { e.preventDefault(); closeMenu(); setTimeout(openTarefeModal, 300); });
    function openMenu() {
        clearTimeout(closeTimer);
        previousFocus = document.activeElement;
        document.body.classList.add('menu-open');
        overlay.style.display = 'block';
        overlay.setAttribute('aria-hidden', 'false');
        menuTrigger.setAttribute('aria-expanded', 'true');
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                if (!document.body.classList.contains('menu-open')) return;
                overlay.classList.add('open');
                document.getElementById('btnCloseMenu').focus();
            });
        });
    }
    function closeMenu() {
        if (overlay.style.display !== 'block') return;
        overlay.classList.remove('open');
        document.body.classList.remove('menu-open');
        menuTrigger.setAttribute('aria-expanded', 'false');
        if (previousFocus && document.body.contains(previousFocus)) previousFocus.focus();
        overlay.setAttribute('aria-hidden', 'true');
        closeTimer = setTimeout(function () { overlay.style.display = 'none'; }, 350);
    }
    $('#btnAllMenu').on('click', openMenu);
    $('#btnCloseMenu').on('click', closeMenu);
    $('.allmenu-backdrop').on('click', closeMenu);
    $(document).on('keydown', function (e) {
        if (!document.body.classList.contains('menu-open')) return;
        if (e.key === 'Escape') { e.preventDefault(); closeMenu(); return; }
        if (e.key !== 'Tab') return;
        var focusable = overlay.querySelectorAll('a[href], button:not([disabled])');
        var first = focusable[0], last = focusable[focusable.length - 1];
        if (e.shiftKey && (document.activeElement === first || !overlay.contains(document.activeElement))) {
            e.preventDefault(); last.focus();
        } else if (!e.shiftKey && (document.activeElement === last || !overlay.contains(document.activeElement))) {
            e.preventDefault(); first.focus();
        }
    });
})();

// --- ساخت دایره درصدی SVG ---
function buildCircle(pct, used, total, remaining) {
    var r = 30, cx = 36, cy = 36;
    var c = 2 * Math.PI * r;
    var offset = c - (pct / 100) * c;
    var color = pct >= 90 ? '#ff6b81' : pct >= 70 ? '#ffa94d' : '#38d9a9';
    return '<div class="d-flex flex-column align-items-center" style="min-width:86px">' +
        '<svg viewBox="0 0 72 72" width="84" height="84">' +
        '<circle cx="' + cx + '" cy="' + cy + '" r="' + r + '" fill="none" stroke="rgba(255,255,255,.12)" stroke-width="7"/>' +
        '<circle cx="' + cx + '" cy="' + cy + '" r="' + r + '" fill="none" stroke="' + color + '" stroke-width="7" stroke-linecap="round" stroke-dasharray="' + c + '" stroke-dashoffset="' + offset + '" transform="rotate(-90 ' + cx + ' ' + cy + ')" style="transition:stroke-dashoffset 1s ease"/>' +
        '<text x="' + cx + '" y="' + (cy - 2) + '" text-anchor="middle" dominant-baseline="middle" fill="#ffffff" font-size="15" font-weight="800" font-family="Sahel, Tahoma">' + toFa(pct) + '٪</text>' +
        '<text x="' + cx + '" y="' + (cy + 13) + '" text-anchor="middle" fill="#9fb3c8" font-size="7" font-family="Sahel, Tahoma">مصرف شده</text>' +
        '</svg>' +
        '<div class="sans-remain mt-1" style="font-size:.8rem;">' + toFa(remaining) + ' جلسه مانده</div>' +
        '<div style="font-size:.68rem; color:#9fb3c8;">مصرف: ' + toFa(used) + ' از ' + toFa(total) + '</div>' +
        '</div>';
}

// --- بارگذاری پروفایل و آمار مالی ---
function loadDashboardCredits() {
    apiCall('/api/Profile/GetProfile', 'GET', null, function (res) {
        if (!res.success) return;
        var d = res.data;
        $('#dashFullName').text(d.fullName);
        $('#dashMemberId').text('کد عضویت: ' + toFa(d.memberID));
        $('#statSport').text(fmtNum(d.sportCredit));
        $('#statBuffet').text(fmtNum(d.buffetCredit));
        $('#statService').text(fmtNum(d.serviceCredit));
        var $box = $('#debitAmountBox');
        if (d.totalDebit > 0) {
            $('#statDebit').text(fmtNum(d.totalDebit));
            $('#debitLabel').text('بدهی');
            $box.removeClass('is-credit').addClass('is-debit');
        } else if (d.totalDebit < 0) {
            $('#statDebit').text(fmtNum(Math.abs(d.totalDebit)));
            $('#debitLabel').text('بستانکار');
            $box.removeClass('is-debit').addClass('is-credit');
        } else {
            $('#statDebit').text('۰');
            $('#debitLabel').text('بدهی');
            $box.removeClass('is-debit is-credit');
        }
        var faceImg = document.getElementById('homeFaceImg');
        var avatar = document.getElementById('homeAvatar');
        if (avatar && (!faceImg || faceImg.style.display === 'none' || !faceImg.complete)) {
            if (d.personImageBase64) {
                avatar.innerHTML = '<img src="data:image/png;base64,' + d.personImageBase64 + '" style="width:100%;height:100%;object-fit:cover" />';
            }
        }
    });
}

function updateActiveSansesSummary(sanses) {
    var activeCount = sanses.length;
    var totalRemain = 0;
    $.each(sanses, function (i, s) {
        totalRemain += Number(s.remainingSessions || 0);
    });
    $('#dashActiveCount').text(toFa(activeCount) + ' سانس فعال');
    $('#dashRemainSessions').text(toFa(totalRemain) + ' جلسه مانده');
}

function renderActiveSanses(sanses) {
    var $box = $('#activeSanses');
    if (!sanses || sanses.length === 0) {
        $box.html('<div class="empty-state"><div class="empty-icon"><i class="bi bi-ticket-perforated" aria-hidden="true"></i></div>شما در حال حاضر سانس فعالی ندارید.<br><a href="/Register/Renew" class="btn btn-sm btn-primary mt-2">تمدید دوره</a></div>');
        $('#activeSansesCount').text('۰');
        return;
    }
    $('#activeSansesCount').text(toFa(sanses.length));
    var html = '';
    $.each(sanses, function (i, s) {
        var total = Number(s.totalSessions || 0);
        var used = Number(s.usedSessions || 0);
        var remaining = Number(s.remainingSessions || 0);
        var pct = total > 0 ? Math.round((used / total) * 100) : 0;
        html += '<div class="col-lg-6">' +
            '<div class="sans-card h-100">' +
                '<div class="d-flex justify-content-between align-items-start gap-3 flex-wrap">' +
                    '<div style="min-width:0">' +
                        '<div class="sans-name">' + esc(s.sportName) + ' - ' + esc(s.sanseName) + '</div>' +
                        '<div class="sans-coach"><i class="bi bi-person-fill"></i> مربی: ' + esc(s.coachName || '-') + '</div>' +
                    '</div>' +
                    '<div class="flex-shrink-0">' + buildCircle(pct, used, total, remaining) + '</div>' +
                '</div>' +
                '<div class="d-flex flex-wrap gap-2 mt-3">' +
                    (s.startDate ? '<span class="sans-pill"><i class="bi bi-calendar3"></i> شروع: ' + esc(s.startDate) + '</span>' : '') +
                    '<span class="sans-pill"><i class="bi bi-calendar3"></i> پایان: ' + esc(s.endDate || '-') + '</span>' +
                '</div>' +
                '<div class="sans-meta mt-2">' +
                    (s.membershipTypeDesc ? '<span><i class="bi bi-tag-fill"></i>' + esc(s.membershipTypeDesc) + '</span>' : '') +
                    (s.periodDesc ? '<span><i class="bi bi-stopwatch-fill"></i>' + esc(s.periodDesc) + '</span>' : '') +
                '</div>' +
            '</div>' +
        '</div>';
    });
    $box.html(html);
}

// یک پاسخ مشترک، هم چیپ‌های داشبورد را پر می‌کند و هم کارت‌های سانس را رندر می‌کند.
function loadActiveSanses() {
    apiCall('/api/Register/ActiveSanses', 'GET', null, function (res) {
        var sanses = (res && res.success && res.data) ? res.data : [];
        updateActiveSansesSummary(sanses);
        renderActiveSanses(sanses);
    });
}

function loadDashboardData() {
    loadDashboardCredits();
    loadActiveSanses();
}

loadDashboardData();

// ========== کاروسل سه‌بعدی ==========
(function () {
    var items = [];
    $('#navMenu .nav-card').each(function () {
        items.push({
            href: $(this).attr('href'),
            icon: $(this).find('.nav-icon').html(),
            title: $(this).find('.nav-title').text(),
            action: $(this).attr('data-action') || null
        });
    });
    if (!items.length) return;
    var n = items.length;
    var half = Math.floor(n / 2);
    var active = 0;
    var $stage = $('#cfStage');
    var $dots = $('#cfDots');
    items.forEach(function (it, i) {
        var $c = $('<a class="cf-card" data-index="' + i + '" href="' + it.href + '"></a>');
        if (it.action) $c.attr('data-action', it.action);
        $c.append('<div class="cf-icon">' + it.icon + '</div>');
        $c.append('<div class="cf-title">' + it.title + '</div>');
        $stage.append($c);
        $dots.append('<button type="button" class="cf-dot" data-index="' + i + '" aria-label="' + esc(it.title) + '" aria-pressed="false"></button>');
    });
    function layout() {
        $stage.find('.cf-card').each(function () {
            var i = +$(this).attr('data-index');
            var rel = ((i - active) % n + n) % n;
            var d = Math.min(rel, n - rel);
            var side = (rel === 0) ? 0 : (rel <= half ? -1 : 1);
            var t, o, z = 100 - d;
            if (d === 0) { t = 'translateX(0) translateZ(0) rotateY(0deg) scale(1)'; o = 1; }
            else if (d === 1) { t = 'translateX(' + (side * 62) + '%) translateZ(-140px) rotateY(' + (-side * 42) + 'deg)'; o = .75; }
            else if (d === 2) { t = 'translateX(' + (side * 85) + '%) translateZ(-260px) rotateY(' + (-side * 52) + 'deg)'; o = .45; }
            else if (d === 3) { t = 'translateX(' + (side * 95) + '%) translateZ(-360px) rotateY(' + (-side * 58) + 'deg)'; o = .22; }
            else { t = 'translateX(0) translateZ(-460px)'; o = 0; }
            $(this).css({ transform: t, opacity: o, zIndex: z });
            $(this).toggleClass('cf-active', d === 0);
            $(this).attr('tabindex', d === 0 ? '0' : '-1');
            $(this).attr('aria-hidden', d > 3 ? 'true' : 'false');
        });
        $('#cfLabel').text(items[active].title);
        $dots.find('.cf-dot').removeClass('on').attr('aria-pressed', 'false')
            .filter('[data-index="' + active + '"]').addClass('on').attr('aria-pressed', 'true');
    }
    function go(delta) { active = ((active + delta) % n + n) % n; layout(); }
    $stage.on('click', '.cf-card', function (e) {
        var i = +$(this).attr('data-index');
        var action = $(this).attr('data-action');
        if (i !== active) {
            e.preventDefault();
            var rel = ((i - active) % n + n) % n;
            go(rel <= half ? rel : rel - n);
        } else {
            if (action === 'singleSession') { e.preventDefault(); openSingleSessionModal(); }
            else if (action === 'tarefe') { e.preventDefault(); openTarefeModal(); }
        }
    });
    $dots.on('click', '.cf-dot', function () {
        var i = +$(this).attr('data-index');
        var rel = ((i - active) % n + n) % n;
        go(rel <= half ? rel : rel - n);
    });
    $stage.on('keydown', '.cf-card', function (e) {
        if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
            e.preventDefault();
            go(e.key === 'ArrowLeft' ? 1 : -1);
            $stage.find('.cf-active').trigger('focus');
        }
    });
    var sx = 0, sy = 0, tracking = false;
    var stage = $stage[0];
    stage.addEventListener('touchstart', function (e) { sx = e.touches[0].clientX; sy = e.touches[0].clientY; tracking = true; }, { passive: true });
    stage.addEventListener('touchend', function (e) {
        if (!tracking) return;
        tracking = false;
        var dx = e.changedTouches[0].clientX - sx;
        var dy = e.changedTouches[0].clientY - sy;
        if (Math.abs(dx) > 45 && Math.abs(dx) > Math.abs(dy)) { go(dx > 0 ? 1 : -1); }
    }, { passive: true });
    layout();
})();
