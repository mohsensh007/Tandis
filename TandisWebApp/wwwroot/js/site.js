// ============================
// تندیس - جاوااسکریپت مشترک
// ============================

// --- نمایش Toast ---
function showToast(message, type) {
    type = type || 'success';
    var container = $('.toast-container');
    if (container.length === 0) {
        $('body').prepend('<div class="toast-container"></div>');
    }
    var cls = type === 'error' ? 'error' : type === 'warning' ? 'warning' : 'success';
    var $msg = $('<div class="alert-msg ' + cls + '"></div>').text(message);
    $('.toast-container').append($msg);
    setTimeout(function () { $msg.fadeOut(400, function () { $msg.remove(); }); }, 4000);
}

// --- Loading ---
function showLoading() {
    if ($('#spinnerOverlay').length === 0) {
        $('body').append('<div id="spinnerOverlay" class="spinner-overlay"><div class="spinner-border text-primary" role="status"></div></div>');
    }
    $('#spinnerOverlay').show();
}
function hideLoading() { $('#spinnerOverlay').hide(); }

// --- API Helper ---
function apiCall(url, method, data, onSuccess, onFail) {
    showLoading();
    $.ajax({
        url: url,
        type: method || 'GET',
        contentType: 'application/json',
        data: data ? JSON.stringify(data) : null,
        success: function (res) {
            hideLoading();
            if (onSuccess) onSuccess(res);
        },
        error: function (xhr) {
            hideLoading();
            var msg = 'خطا در ارتباط با سرور';
            try { var r = JSON.parse(xhr.responseText); msg = r.message || r.Message || msg; } catch(e) {}
            showToast(msg, 'error');
            if (onFail) onFail(xhr);
        }
    });
}

// --- جداکننده اعداد ---
function formatNumber(n) {
    return Number(n).toLocaleString('fa-IR');
}

// --- بروزرسانی اعتبارها در تمام صفحات ---
function refreshDashboardCredits() {
    apiCall('/api/Profile/GetProfile', 'GET', null, function (res) {
        if (!res.success) return;
        var d = res.data;
        // داشبورد
        var f = function(n) { return Number(n || 0).toLocaleString('fa-IR'); };
        $('#statSport').text(f(d.sportCredit) + ' ریال');
        $('#statBuffet').text(f(d.buffetCredit) + ' ریال');
        $('#statService').text(f(d.serviceCredit) + ' ریال');
        $('#statDebit').text(f(d.totalDebit) + ' ریال');
        $('#dashSportCredit').text('ورزشی: ' + f(d.sportCredit));
        $('#dashBuffetCredit').text('فروشگاه: ' + f(d.buffetCredit));
        $('#dashServiceCredit').text('خدمات: ' + f(d.serviceCredit));
        // پروفایل
        $('#creditSport').text(Number(d.sportCredit).toLocaleString() + ' ریال');
        $('#creditBuffet').text(Number(d.buffetCredit).toLocaleString() + ' ریال');
        $('#creditService').text(Number(d.serviceCredit).toLocaleString() + ' ریال');
        $('#totalDebit').text(Number(d.totalDebit).toLocaleString() + ' ریال');
    });
}

// --- ایجاد کارت لینک ---
function createDashCard(icon, title, color, href) {
    return '<a href="' + href + '" class="col-6 col-md-3 text-decoration-none">' +
        '<div class="card dash-card"><div class="icon" style="color:' + color + '">' + icon + '</div>' +
        '<div class="title">' + title + '</div></div></a>';
}
