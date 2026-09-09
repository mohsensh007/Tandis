var replyToMsgID = 0;

function loadInbox() {
    apiCall('/api/Admin/Messages/Inbox', 'GET', null, function (res) {
        if (!res.success) return;
        var rows = res.data || [];
        var tb = $('#tbodyInbox');

        if (!rows.length) {
            tb.html('<tr><td colspan="5" class="text-center py-3">هیچ پیامی وجود ندارد</td></tr>');
            return;
        }

        var html = '';
        rows.forEach(function (m) {
            html += '<tr class="' + (m.isSeen ? '' : 'table-light fw-bold') + '">' +
                // ستون ۱: فرستنده + کد عضویت
                '<td>' + esc(m.senderName) + '<br><small class="text-muted">' + esc(m.memberCode || '') + '</small></td>' +
                // ستون ۲: موبایل
                '<td>' + esc(m.mobile || '-') + '</td>' +
                // ستون ۳: ✅ عنوان جدا + تکه‌ای از متن + لینک مشاهده کامل
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
                // ستون ۴: تاریخ و ساعت
                '<td class="text-nowrap small">' + esc(m.creationDate || '') + '<br>' + esc(m.creationTime || '') + '</td>' +
                // ستون ۵: عملیات (پاسخ + دیده‌شدن)
                '<td>' +
                '<button class="btn btn-sm btn-primary" onclick="openReply(' + m.messageID + ', \'' + esc(m.senderName) + '\', \'' + esc(m.body.replace(/'/g, "\\'")).substring(0, 100) + '\')"><i class="bi bi-reply"></i></button>' +
                (!m.isSeen ? ' <button class="btn btn-sm btn-outline-success" onclick="markSeen(' + m.messageID + ')"><i class="bi bi-check2"></i></button>' : '') +
                '</td>' +
                '</tr>';
        });
        tb.html(html);
    });
}

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

// ارسال گروهی
$('#selTargetType').on('change', function () {
    var v = $(this).val();
    $('#divRole, #divSport, #divMember').hide();
    if (v === '2') $('#divRole').show();
    if (v === '3') $('#divSport').show();
    if (v === '4') $('#divMember').show();
});

function loadRoles() {
    apiCall('/api/Admin/Roles', 'GET', null, function (res) {
        if (!res.success) return;
        var html = '<option value="">-- انتخاب نقش --</option>';
        res.data.forEach(function (r) {
            html += '<option value="' + r.roleID + '">' + esc(r.roleDesc) + '</option>';
        });
        $('#selRole').html(html);
    });
}

function loadSports() {
    apiCall('/api/Admin/SportCategories', 'GET', null, function (res) {
        if (!res.success) return;
        var html = '<option value="">-- انتخاب رشته --</option>';
        res.data.forEach(function (s) {
            html += '<option value="' + s.sportCatID + '">' + esc(s.sportName) + '</option>';
        });
        $('#selSport').html(html);
    });
}

$('#txtMemberSearch').on('input', function () {
    var q = $(this).val().trim();
    if (q.length < 2) { $('#selMember').hide(); return; }
    apiCall('/api/Admin/SearchMember?q=' + encodeURIComponent(q), 'GET', null, function (res) {
        if (!res.success || !res.data || !res.data.length) { $('#selMember').hide(); return; }
        var html = '<option value="">-- انتخاب عضو --</option>';
        res.data.forEach(function (m) {
            html += '<option value="' + m.memberID + '">' + esc(m.fullName) + ' (' + esc(m.memberCode) + ')</option>';
        });
        $('#selMember').html(html).show();
    });
});

$('#btnSendBroadcast').on('click', function () {
    var targetType = parseInt($('#selTargetType').val());
    var body = $('#txtBody').val().trim();
    if (!body) return showToast('متن پیام خالی است', 'warning');

    var payload = {
        title: $('#txtTitle').val().trim() || null,
        body: body,
        targetType: targetType
    };
    if (targetType === 2) payload.targetRoleID = parseInt($('#selRole').val());
    if (targetType === 3) payload.targetSportCatID = parseInt($('#selSport').val());
    if (targetType === 4) payload.targetMemberID = parseInt($('#selMember').val());

    if (targetType === 2 && !payload.targetRoleID) return showToast('نقش را انتخاب کنید', 'warning');
    if (targetType === 3 && !payload.targetSportCatID) return showToast('رشته را انتخاب کنید', 'warning');
    if (targetType === 4 && !payload.targetMemberID) return showToast('عضو را انتخاب کنید', 'warning');

    apiCall('/api/Admin/Messages/Send', 'POST', payload, function (res) {
        if (res.success) {
            showToast('پیام با موفقیت ارسال شد', 'success');
            $('#txtTitle, #txtBody').val('');
        } else {
            showToast(res.message || 'خطا', 'error');
        }
    });
});

$('#btnRefreshInbox').on('click', loadInbox);

$(function () {
    loadInbox();
    loadRoles();
    loadSports();
    setInterval(loadInbox, 30000);
});