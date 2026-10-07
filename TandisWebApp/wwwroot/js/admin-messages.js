var replyToMsgID = 0;

// ============================================================
//  اینباکس مدیریت (با فیلتر جستجو + فقط خوانده‌نشده)
// ============================================================
var inboxRows = [];
var inboxFilter = { q: '', unreadOnly: false };

function renderInbox() {
    var rows = inboxRows;
    var q = (inboxFilter.q || '').trim().toLowerCase();
    if (q) {
        rows = rows.filter(function (m) {
            return (m.senderName || '').toLowerCase().indexOf(q) !== -1
                || (m.memberCode || '').indexOf(q) !== -1
                || (m.mobile || '').indexOf(q) !== -1
                || (m.title || '').toLowerCase().indexOf(q) !== -1
                || (m.body || '').toLowerCase().indexOf(q) !== -1
                || (m.creationDate || '').indexOf(q) !== -1;
        });
    }
    if (inboxFilter.unreadOnly) {
        rows = rows.filter(function (m) { return !m.isSeen; });
    }

    var tb = $('#tbodyInbox');

    if (!rows.length) {
        var anyFilter = inboxFilter.unreadOnly || (inboxFilter.q || '').trim().length > 0;
        tb.html('<tr><td colspan="5" class="text-center py-3">' +
            (anyFilter ? 'پیامی مطابق فیلتر پیدا نشد' : 'هیچ پیامی وجود ندارد') + '</td></tr>');
        return;
    }

    var html = '';
    rows.forEach(function (m) {
        html += '<tr class="' + (m.isSeen ? '' : 'table-light fw-bold') + '">' +
            '<td>' + esc(m.senderName) + '<br><small class="text-muted">' + esc(m.memberCode || '') + '</small></td>' +
            '<td>' + esc(m.mobile || '-') + '</td>' +
            '<td>' +
            '<a href="/Admin/Messages/View/' + m.messageID + '" class="link-light text-decoration-none d-block">' +
            (m.title
                ? '<div class="fw-bold mb-1">' + esc(m.title) + '</div>'
                : '<div class="fw-bold mb-1 text-muted">(بدون عنوان)</div>') +
            '<div class="text-muted small text-break">' +
            esc(m.body.substring(0, 60)) + (m.body.length > 60 ? '…' : '') +
            '</div>' +
            '<div class="small mt-1" style="color:#7ab8ff;">🔍 مشاهده کامل پیام</div>' +
            '</a>' +
            '</td>' +
            '<td class="text-nowrap small">' + esc(m.creationDate || '') + '<br>' + esc(m.creationTime || '') + '</td>' +
            '<td>' +
            '<button class="btn btn-sm btn-primary" onclick="openReply(' + m.messageID + ', \'' + esc(m.senderName) + '\', \'' + esc(m.body.replace(/'/g, "\\'")).substring(0, 100) + '\')"><i class="bi bi-reply"></i></button>' +
            (!m.isSeen ? ' <button class="btn btn-sm btn-outline-success" onclick="markSeen(' + m.messageID + ')"><i class="bi bi-check2"></i></button>' : '') +
            '</td>' +
            '</tr>';
    });
    tb.html(html);
}

function loadInbox() {
    apiCall('/api/Admin/Messages/Inbox', 'GET', null, function (res) {
        if (!res.success) return;
        inboxRows = res.data || [];
        renderInbox();
    });
}

// ✅ فیلترهای اینباکس (task 4)
$('#txtInboxSearch').on('input', debounce(function () {
    inboxFilter.q = $(this).val() || '';
    renderInbox();
}, 250));

$('#chkInboxUnread').on('change', function () {
    inboxFilter.unreadOnly = !!this.checked;
    renderInbox();
});

function markSeen(id) {
    apiCall('/api/Admin/Messages/MarkSeen', 'POST', id, function () { loadInbox(); });
}

function openReply(id, name, body) {
    replyToMsgID = id;
    $('#replyTo').text(name);
    $('#origBody').text(body);
    $('#txtReplyBody').val('');
    new bootstrap.Modal('#replyModal').show();
}

$('#btnSendReply').on('click', function () {
    var body = $('#txtReplyBody').val().trim();
    if (!body) return showToast('متن پاسخ خالی است', 'warning');
    apiCall('/api/Admin/Messages/Reply', 'POST', { replyToMessageID: replyToMsgID, body: body }, function (res) {
        if (res.success) {
            showToast('پاسخ ارسال شد', 'success');
            bootstrap.Modal.getInstance(document.getElementById('replyModal')).hide();
            loadInbox();
        } else {
            showToast(res.message || 'خطا', 'error');
        }
    });
});

// ============================================================
//  انتخاب گیرندگان (مودال روی همان صفحه پیام‌ها)
// ============================================================
var rec = {
    mode: 'all',      // all | role | sport
    roleId: 0,
    roleName: '',
    sportIds: {},     // sportCatID -> name
    scope: 'all',     // all | ids
    selected: {}      // memberID -> true
};
var target = { mode: 'all' };   // چیزی که در نهایت ارسال می‌شود
var sportsAll = [];

function faNum(n) { return Number(n || 0).toLocaleString('fa-IR'); }
function selectedIds() { return Object.keys(rec.selected).map(Number); }

function loadRoles() {
    apiCall('/api/Admin/Roles', 'GET', null, function (res) {
        if (!res.success) return;
        var html = '<option value="">-- انتخاب نقش --</option>';
        res.data.forEach(function (r) {
            html += '<option value="' + r.roleID + '">' + esc(r.roleDesc) + '</option>';
        });
        $('#selRecRole').html(html);
    });
}

function loadSports() {
    apiCall('/api/Admin/SportCategories', 'GET', null, function (res) {
        if (!res.success) return;
        sportsAll = res.data || [];
        renderSportChips('');
    });
}

function renderSportChips(q) {
    q = (q || '').trim();
    var list = sportsAll.filter(function (s) {
        return !q || (s.sportName || '').indexOf(q) !== -1;
    });
    if (!list.length) {
        $('#sportChips').html('<div class="rec-hint">رشته‌ای پیدا نشد</div>');
        return;
    }
    var html = '';
    list.forEach(function (s) {
        var on = rec.sportIds[s.sportCatID] ? ' on' : '';
        html += '<label class="rec-chip' + on + '" data-id="' + s.sportCatID + '">' +
            '<input type="checkbox" ' + (on ? 'checked' : '') + ' />' +
            '<i class="bi bi-check2"></i>' + esc(s.sportName) +
            '</label>';
    });
    $('#sportChips').html(html);
}

// ---------- رندر لیست اعضا (صفحه‌ای / اسکرول مرحله‌ای) ----------
var PAGE_SIZE = 300;
var listState = {
    role:     { skip: 0, total: 0, hasMore: false, loading: false, q: '' },
    sport:    { skip: 0, total: 0, hasMore: false, loading: false, q: '' },
    birthday: { skip: 0, total: 0, hasMore: false, loading: false, q: '' }
};

function memberRowHtml(m) {
    var on = rec.selected[m.memberID] ? 'checked' : '';
    return '<label class="rec-item">' +
        '<input type="checkbox" value="' + m.memberID + '" ' + on + ' />' +
        '<div class="rec-item-body">' +
        '<div class="rec-name">' + esc(m.fullName) + '</div>' +
        (m.mobile ? '<div class="rec-sub">' + esc(m.mobile) + '</div>' : '') +
        '</div></label>';
}

function paintList(selector, rows, append, st) {
    var box = $(selector);
    if (!append) box.empty();

    if (!rows.length && !append) {
        box.html('<div class="rec-hint">عضوی پیدا نشد</div>');
    } else {
        box.find('.rec-more').remove();
        box.append(rows.map(memberRowHtml).join(''));
        if (st.hasMore) {
            box.append('<div class="rec-more">نمایش ' + faNum(st.skip) + ' از ' + faNum(st.total) +
                ' نفر — برای دیدن بقیه اسکرول کنید</div>');
        }
    }

    var counterMap = { '#roleList': '#roleCounter', '#sportMemberList': '#sportCounter', '#birthdayList': '#birthdayCounter' };
    var counter = counterMap[selector];
    if (counter) {
        $(counter).text(st.total ? ('کل ' + faNum(st.total) + ' نفر — نمایش ' + faNum(st.skip)) : '');
    }
    updateScopeUI();
}

function loadMembers(url, stateKey, selector, q, append) {
    var st = listState[stateKey];
    if (st.loading) return;
    if (append && !st.hasMore) return;

    if (!append) {
        st.skip = 0;
        st.q = (q || '').trim();
        st.hasMore = false;
        $(selector).html('<div class="rec-hint">در حال بارگذاری...</div>');
    }

    st.loading = true;
    var sep = url.indexOf('?') >= 0 ? '&' : '?';
    apiCall(url + sep + 'q=' + encodeURIComponent(st.q) + '&skip=' + st.skip + '&take=' + PAGE_SIZE,
        'GET', null, function (res) {
            st.loading = false;
            if (!res.success) return showToast(res.message || 'خطا', 'error');

            var rows = res.data || [];
            st.total = res.total || 0;
            st.hasMore = !!res.hasMore;
            st.skip = (res.skip || 0) + rows.length;
            paintList(selector, rows, append, st);
        }, function () { st.loading = false; });
}

function loadRoleMembers(q, append) {
    if (!rec.roleId) {
        $('#roleList').html('<div class="rec-hint">ابتدا یک نقش انتخاب کنید</div>');
        $('#roleCounter').text('');
        listState.role.total = 0; listState.role.skip = 0; listState.role.hasMore = false;
        updateScopeUI();
        return;
    }
    loadMembers('/api/Admin/MembersByRole?roleID=' + rec.roleId, 'role', '#roleList', q, append);
}

function loadSportMembers(q, append) {
    var ids = Object.keys(rec.sportIds);
    $('#sportScopeCount').text(faNum(ids.length) + ' رشته');
    if (!ids.length) {
        $('#sportMemberList').html('<div class="rec-hint">ابتدا حداقل یک رشته را تیک بزنید</div>');
        $('#sportCounter').text('');
        listState.sport.total = 0; listState.sport.skip = 0; listState.sport.hasMore = false;
        updateScopeUI();
        return;
    }
    loadMembers('/api/Admin/MembersBySports?sportIds=' + ids.join(','), 'sport', '#sportMemberList', q, append);
}

// ---------- تولد امروز ----------
function loadBirthdayMembers(q, append) {
    loadMembers('/api/Admin/MembersBornToday', 'birthday', '#birthdayList', q, append);
}

// ✅ نوار رشته‌های انتخاب‌شده (task 3) — همیشه در دید می‌ماند تا در لیست بلند گم نشود
function renderSelectedSportsBar() {
    var ids = Object.keys(rec.sportIds).map(Number);
    var bar = $('#selectedSportsBar');
    if (!ids.length) { bar.hide().empty(); return; }

    var html = '<span class="rsb-label"><i class="bi bi-check2-all"></i> رشته‌های انتخاب‌شده (' + faNum(ids.length) + '):</span>';
    ids.forEach(function (id) {
        var s = null;
        for (var i = 0; i < sportsAll.length; i++) {
            if (Number(sportsAll[i].sportCatID) === id) { s = sportsAll[i]; break; }
        }
        html += '<span class="rsb-chip">' + esc(s ? s.sportName : ('رشته ' + id)) +
            '<button type="button" class="rsb-x" data-id="' + id + '" title="حذف از انتخاب">×</button></span>';
    });
    bar.html(html).show();
}

$('#selectedSportsBar').on('click', '.rsb-x', function () {
    var id = parseInt($(this).data('id'), 10);
    delete rec.sportIds[id];
    rec.selected = {};
    rec.scope = 'all';
    $('#txtSportMemberSearch').val('');
    renderSportChips($('#txtSportSearch').val() || '');
    renderSelectedSportsBar();
    loadSportMembers('');
    updateScopeUI();
});

// اسکرول مرحله‌ای: رسیدن به انتهای لیست → بارگذاری صفحه بعد
$('.rec-list').on('scroll', function () {
    var el = this;
    if (el.scrollTop + el.clientHeight < el.scrollHeight - 40) return;
    var mode = el.getAttribute('data-mode');
    if (mode === 'role') loadRoleMembers(null, true);
    if (mode === 'sport') loadSportMembers(null, true);
    if (mode === 'birthday') loadBirthdayMembers(null, true);
});

// ---------- دامنه ارسال (همهٔ فیلتر / فقط تیک‌خورده‌ها) ----------
function updateScopeUI() {
    var count = selectedIds().length;
    $('#recCount').text(faNum(count));

    var box = $('#recScopeBox');
    if (rec.mode === 'all') { box.hide(); return; }
    box.show();

    var allRadio = box.find('input[value="all"]');
    var idsRadio = box.find('input[value="ids"]');

    // در حالت رشته بدون انتخاب رشته، «همهٔ این فیلتر» بی‌معناست
    var noFilter = (rec.mode === 'sport' && Object.keys(rec.sportIds).length === 0);
    allRadio.closest('.rec-radio').toggleClass('disabled', noFilter);
    idsRadio.closest('.rec-radio').toggleClass('disabled', count === 0 && !noFilter);

    if (noFilter) rec.scope = 'ids';
    else if (rec.scope === 'ids' && count === 0) rec.scope = 'all';

    box.find('input[value="' + rec.scope + '"]').prop('checked', true);
}

// ---------- تعویض تب ----------
function setMode(mode) {
    if (rec.mode === mode) return;
    rec.mode = mode;
    rec.scope = 'all';
    rec.selected = {};          // انتخاب‌ها مربوط به همان تب هستند

    $('.rec-tab').removeClass('active');
    $('.rec-tab[data-mode="' + mode + '"]').addClass('active');

    $('#panelAll, #panelRole, #panelSport, #panelBirthday').hide();
    if (mode === 'all') $('#panelAll').show();
    if (mode === 'role') { $('#panelRole').show(); loadRoleMembers($('#txtRoleSearch').val() || ''); }
    if (mode === 'sport') { $('#panelSport').show(); loadSportMembers($('#txtSportMemberSearch').val() || ''); renderSelectedSportsBar(); }
    if (mode === 'birthday') { $('#panelBirthday').show(); loadBirthdayMembers($('#txtBirthdaySearch').val() || ''); }

    updateScopeUI();
}

$('.rec-tab').on('click', function () { setMode($(this).data('mode')); });

$('#btnOpenRecipients').on('click', function () {
    // getOrCreateInstance → جلوگیری از ساخت چند instance و باقی ماندن backdrop
    bootstrap.Modal.getOrCreateInstance('#recipientModal').show();
});

// ---------- نقش ----------
$('#selRecRole').on('change', function () {
    rec.roleId = parseInt($(this).val(), 10) || 0;
    rec.roleName = $(this).find('option:selected').text() || '';
    rec.selected = {};
    rec.scope = 'all';
    $('#txtRoleSearch').val('');
    loadRoleMembers('');
    updateScopeUI();
});

// ---------- رشته‌ها ----------
$('#sportChips').on('change', 'input[type="checkbox"]', function () {
    var chip = $(this).closest('.rec-chip');
    var id = parseInt(chip.data('id'), 10);
    if (this.checked) { rec.sportIds[id] = true; chip.addClass('on'); }
    else { delete rec.sportIds[id]; chip.removeClass('on'); }
    rec.selected = {};
    rec.scope = 'all';
    $('#txtSportMemberSearch').val('');
    renderSelectedSportsBar();
    loadSportMembers('');
    updateScopeUI();
});

// ---------- تیک افراد ----------
$(document).on('change', '.rec-item input[type="checkbox"]', function () {
    var id = parseInt($(this).val(), 10);
    if (this.checked) rec.selected[id] = true; else delete rec.selected[id];
    var n = selectedIds().length;
    if (n > 0) rec.scope = 'ids';
    else if (rec.mode !== 'sport' || Object.keys(rec.sportIds).length > 0) rec.scope = 'all';
    updateScopeUI();
});

$('input[name="recScope"]').on('change', function () {
    rec.scope = $(this).val();
    updateScopeUI();
});

// ---------- جستجو (debounce) ----------
function debounce(fn, ms) {
    var t;
    return function () {
        var self = this, args = arguments;
        clearTimeout(t);
        t = setTimeout(function () { fn.apply(self, args); }, ms);
    };
}

$('#txtRoleSearch').on('input', debounce(function () { loadRoleMembers($(this).val()); }, 300));
$('#txtSportSearch').on('input', function () { renderSportChips($(this).val()); });
$('#txtSportMemberSearch').on('input', debounce(function () { loadSportMembers($(this).val()); }, 300));
$('#txtBirthdaySearch').on('input', debounce(function () { loadBirthdayMembers($(this).val()); }, 300));

// ---------- پاک کردن / تأیید ----------
$('#btnRecClear').on('click', function () {
    rec.selected = {};
    rec.sportIds = {};
    rec.roleId = 0;
    rec.scope = 'all';
    // ✅ task 1: انتخابِ اعمال‌شدهٔ قبلی هم پاک می‌شود تا خلاصهٔ گیرندگان و ارسال از حالت قبلی نماند
    target = { mode: 'none' };
    $('#recSummary').text('هیچ گیرنده‌ای انتخاب نشده');
    $('#selRecRole').val('');
    $('#txtRoleSearch, #txtSportSearch, #txtSportMemberSearch, #txtBirthdaySearch').val('');
    renderSelectedSportsBar();
    if (rec.mode === 'sport') { renderSportChips(''); loadSportMembers(''); }
    if (rec.mode === 'role') loadRoleMembers('');
    if (rec.mode === 'birthday') loadBirthdayMembers('');
    updateScopeUI();
    showToast('انتخاب‌ها پاک شد', 'success');
});

function summarize() {
    if (rec.mode === 'all') return 'همه اعضا';

    if (rec.mode === 'role') {
        if (!rec.roleId) return 'نقشی انتخاب نشده';
        var rq = (listState.role.q || '').trim();
        var rPart = rq ? ' — جستجوی «' + rq + '»' : '';
        if (rec.scope === 'all') return 'همه اعضا با نقش «' + rec.roleName + '»' + rPart;
        return faNum(selectedIds().length) + ' نفر از نقش «' + rec.roleName + '»';
    }

    if (rec.mode === 'birthday') {
        var bq = (listState.birthday.q || '').trim();
        var bPart = bq ? ' — جستجوی «' + bq + '»' : '';
        if (rec.scope === 'all') return 'همه متولدین امروز' + bPart;
        return faNum(selectedIds().length) + ' نفر از متولدین امروز';
    }

    var ids = Object.keys(rec.sportIds);
    if (!ids.length) return 'رشته‌ای انتخاب نشده';
    var sq = (listState.sport.q || '').trim();
    var sPart = sq ? ' — جستجوی «' + sq + '»' : '';
    if (rec.scope === 'all') return 'همه اعضای ' + faNum(ids.length) + ' رشته انتخابی' + sPart;
    return faNum(selectedIds().length) + ' نفر از رشته‌های انتخابی';
}

$('#btnRecApply').on('click', function () {
    if (rec.mode === 'role' && !rec.roleId)
        return showToast('ابتدا یک نقش را انتخاب کنید', 'warning');
    if (rec.mode === 'sport' && !Object.keys(rec.sportIds).length)
        return showToast('ابتدا حداقل یک رشته را انتخاب کنید', 'warning');
    if (rec.scope === 'ids' && selectedIds().length === 0)
        return showToast('هیچ فردی تیک نخورده است', 'warning');

    target = {
        mode: rec.mode,
        roleId: rec.roleId,
        roleName: rec.roleName,
        sportIds: Object.keys(rec.sportIds).map(Number),
        scope: rec.scope,
        ids: selectedIds(),
        // ✅ task 2: متن جستجوی فعلی لیست هم ثبت می‌شود تا «ارسال به همهٔ افراد فیلترشده» واقعاً فیلتر را بفرستد
        q: rec.mode === 'role' ? (listState.role.q || '')
            : rec.mode === 'sport' ? (listState.sport.q || '')
                : rec.mode === 'birthday' ? (listState.birthday.q || '') : ''
    };

    $('#recSummary').text(summarize());
    var modalEl = document.getElementById('recipientModal');
    (bootstrap.Modal.getInstance(modalEl) || bootstrap.Modal.getOrCreateInstance(modalEl)).hide();
    showToast(target.scope === 'all'
        ? 'گیرندگان ثبت شد: ارسال به همهٔ افراد فیلترشده'
        : 'گیرندگان ثبت شد: فقط ' + faNum(target.ids.length) + ' نفر تیک‌خورده', 'success');
});

// ============================================================
//  ارسال پیام
// ============================================================
function sendToMembers(title, body, ids, done) {
    apiCall('/api/Admin/Messages/SendToMembers', 'POST',
        { title: title, body: body, memberIDs: ids }, function (res) {
            if (!res.success) return showToast(res.message || 'خطا', 'error');
            done(res);
        });
}

// ✅ task 2: ارسال به همهٔ افراد فیلترشده — فیلتر روی سرور حل و پیام به تک‌تک آن‌ها می‌رود
function sendToFilter(title, body, filt, done) {
    apiCall('/api/Admin/Messages/SendToFilter', 'POST',
        { title: title, body: body, mode: filt.mode, roleID: filt.roleID || 0, sportIDs: filt.sportIDs || [], q: filt.q || '' },
        function (res) {
            if (!res.success) return showToast(res.message || 'خطا', 'error');
            done(res);
        });
}

function sendBulkList(title, body, payloads, done) {
    if (!payloads.length) return done(null);
    var i = 0;
    var last = null;
    function next() {
        if (i >= payloads.length) return done(last);
        var p = { title: title, body: body, targetType: payloads[i].targetType };
        if (p.targetType === 2) p.targetRoleID = payloads[i].targetRoleID;
        if (p.targetType === 3) p.targetSportCatID = payloads[i].targetSportCatID;
        i++;
        apiCall('/api/Admin/Messages/Send', 'POST', p, function (res) {
            if (!res.success) return showToast(res.message || 'خطا', 'error');
            last = res;
            next();
        });
    }
    next();
}

$('#btnSendBroadcast').on('click', function () {
    var body = $('#txtBody').val().trim();
    if (!body) return showToast('متن پیام خالی است', 'warning');
    var title = $('#txtTitle').val().trim() || null;
    var t = target || { mode: 'all' };

    function success(res) {
        showToast((res && res.message) || 'پیام با موفقیت ارسال شد', 'success');
        $('#txtTitle, #txtBody').val('');
    }

    // ✅ task 1: بعد از «پاک کردن» هیچ گیرنده‌ای ثبت نیست — ارسال متوقف می‌شود
    if (t.mode === 'none')
        return showToast('گیرنده‌ای انتخاب نشده است. ابتدا روی «انتخاب گیرندگان» بزنید', 'warning');

    if (t.mode === 'all') {
        return sendBulkList(title, body, [{ targetType: 1 }], success);
    }

    var fq = (t.q || '').trim();

    if (t.mode === 'role') {
        if (!t.roleId) return showToast('گیرنده‌ای انتخاب نشده است', 'warning');
        if (t.scope === 'ids') {
            if (!t.ids || !t.ids.length) return showToast('هیچ گیرنده‌ای تیک نخورده است', 'warning');
            return sendToMembers(title, body, t.ids, success);
        }
        // ارسال به همهٔ فیلترشده‌ها
        if (fq) return sendToFilter(title, body, { mode: 'role', roleID: t.roleId, q: fq }, success);
        return sendBulkList(title, body, [{ targetType: 2, targetRoleID: t.roleId }], success);
    }

    if (t.mode === 'sport') {
        if (!t.sportIds || !t.sportIds.length) return showToast('رشته‌ای انتخاب نشده است', 'warning');
        if (t.scope === 'ids') {
            if (!t.ids || !t.ids.length) return showToast('هیچ گیرنده‌ای تیک نخورده است', 'warning');
            return sendToMembers(title, body, t.ids, success);
        }
        // ارسال به همهٔ فیلترشده‌ها
        if (fq) return sendToFilter(title, body, { mode: 'sport', sportIDs: t.sportIds, q: fq }, success);
        var payloads = t.sportIds.map(function (id) { return { targetType: 3, targetSportCatID: id }; });
        return sendBulkList(title, body, payloads, success);
    }

    if (t.mode === 'birthday') {
        if (t.scope === 'ids') {
            if (!t.ids || !t.ids.length) return showToast('هیچ گیرنده‌ای تیک نخورده است', 'warning');
            return sendToMembers(title, body, t.ids, success);
        }
        return sendToFilter(title, body, { mode: 'birthday', q: fq }, success);
    }
});

// ============================================================
//  راه‌اندازی
// ============================================================
$('#btnRefreshInbox').on('click', loadInbox);

$(function () {
    loadInbox();
    loadRoles();
    loadSports();
    updateScopeUI();
    setInterval(loadInbox, 30000);
});
