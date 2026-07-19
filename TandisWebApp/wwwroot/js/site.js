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

// --- ایجاد کارت لینک ---
function createDashCard(icon, title, color, href) {
    return '<a href="' + href + '" class="col-6 col-md-3 text-decoration-none">' +
        '<div class="card dash-card"><div class="icon" style="color:' + color + '">' + icon + '</div>' +
        '<div class="title">' + title + '</div></div></a>';
}
