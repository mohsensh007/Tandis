// ========== پیام‌های پنل عضو ==========

function loadMemberMessages(showModal) {
    apiCall('/api/Account/Messages', 'GET', null, function (res) {
        if (!res.success) return;
        var rows = res.data || [];
        var box = $('#messagesList');

        if (!rows.length) {
            box.html('<div class="text-center text-muted py-4">پیامی وجود ندارد</div>');
        } else {
            var html = '';
            rows.forEach(function (m) {
                var isIn = m.direction === 'in';
                var needToggle = m.body.length > 120;
                var shortText = needToggle ? m.body.substring(0, 120) + '…' : m.body;

                html += '<div class="msg-card p-3 ' + (isIn ? 'msg-in' : 'msg-out') + '">' +
                    '<div class="d-flex justify-content-between align-items-center mb-1">' +
                    '<strong class="small">' + (isIn ? '📥 از مدیریت' : '📤 به مدیریت') + '</strong>' +
                    '<span class="text-muted" style="font-size:11px;">' + esc(m.creationDate || '') + ' ' + esc(m.creationTime || '') + '</span>' +
                    '</div>' +
                    (m.title ? '<div class="fw-bold mb-1">' + esc(m.title) + '</div>' : '') +
                    '<div class="small msg-body">' +
                    '<span class="msg-short">' + esc(shortText) + '</span>' +
                    (needToggle
                        ? '<span class="msg-full" style="display:none;">' + esc(m.body) + '</span>' +
                        '<a href="#" class="msg-toggle d-block mt-1">مشاهده بیشتر ▼</a>'
                        : '') +
                    '</div>' +
                    (isIn && !m.isRead ? '<span class="badge bg-danger mt-2">خوانده نشده</span>' : '') +
                    '</div>';
            });
            box.html(html);
        }

        if (showModal) {
            bootstrap.Modal.getOrCreateInstance(document.getElementById('memberMessagesModal')).show();
        }
    });
}

function openMemberMessages() {
    loadMemberMessages(true);
}

$(function () {
    // باز کردن مودال از منوی زنگوله
    $(document).on('click', '.lnk-open-msgs', function (e) {
        e.preventDefault();
        openMemberMessages();
    });
    // ✅ باز و بسته کردن متن بلند در کارت‌های پیام عضو
    $(document).on('click', '.msg-toggle', function (e) {
        e.preventDefault();
        var body = $(this).closest('.msg-body');
        var isFull = body.hasClass('show-full');

        if (isFull) {
            body.removeClass('show-full');
            body.find('.msg-full').hide();
            body.find('.msg-short').show();
            $(this).text('مشاهده بیشتر ▼');
        } else {
            body.addClass('show-full');
            body.find('.msg-full').show();
            body.find('.msg-short').hide();
            $(this).text('بستن ▲');
        }
    });

    // رفتن به مودال ارسال
    $('#btnOpenSend').on('click', function () {
        bootstrap.Modal.getOrCreateInstance(document.getElementById('memberMessagesModal')).hide();
        bootstrap.Modal.getOrCreateInstance(document.getElementById('sendToAdminModal')).show();
    });

    // خواندن همه
    $('#btnMarkAllRead').on('click', function () {
        apiCall('/api/Account/Messages', 'GET', null, function (res) {
            if (!res.success) return;
            var unread = (res.data || []).filter(function (m) {
                return m.direction === 'in' && !m.isRead;
            });
            if (!unread.length) { showToast('پیام خوانده‌نشده‌ای ندارید', 'info'); return; }

            var done = 0;
            unread.forEach(function (m) {
                apiCall('/api/Account/Messages/Read', 'POST', m.messageID, function () {
                    done++;
                    if (done === unread.length) {
                        showToast('همه پیام‌ها خوانده شد', 'success');
                        $('#memberBell .badge').remove();
                        loadMemberMessages(false);   // ✅ فقط لیست رفرش بشه، مودال دست‌نخورده
                    }
                });
            });
        });
    });

    // ارسال پیام به مدیریت
    $('#btnSendToAdmin').on('click', function () {
        var body = $('#txtMemberMsgBody').val().trim();
        if (!body) { showToast('متن پیام خالی است', 'warning'); return; }

        apiCall('/api/Account/Messages/Send', 'POST', {
            title: $('#txtMemberMsgTitle').val().trim() || null,
            body: body,
            targetType: 0
        }, function (res) {
            if (res.success) {
                showToast('پیام شما برای مدیریت ارسال شد', 'success');
                $('#txtMemberMsgTitle, #txtMemberMsgBody').val('');
                bootstrap.Modal.getOrCreateInstance(document.getElementById('sendToAdminModal')).hide();
            } else {
                showToast(res.message || 'خطا در ارسال', 'error');
            }
        });
    });

    // ✅ توری امنیتی: پاکسازی backdrop جامانده بعد از بسته شدن هر مودال (ضد فریز)
    $('#memberMessagesModal, #sendToAdminModal').on('hidden.bs.modal', function () {
        setTimeout(function () {
            if ($('.modal.show').length === 0) {
                $('.modal-backdrop').remove();
                $('body').removeClass('modal-open').css('padding-right', '');
            }
        }, 300);
    });
});