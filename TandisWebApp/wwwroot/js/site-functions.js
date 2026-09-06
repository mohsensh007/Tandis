// ============================
// توابع کمکی سایت (بدون تداخل با تقویم)
// ============================

function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) {
        var div = document.createElement('div');
        div.id = 'toastContainer';
        div.className = 'toast-container';
        document.body.appendChild(div);
        container = div;
    }
    var toast = document.createElement('div');
    toast.className = 'toast ' + type;
    toast.innerHTML = '<div class="toast-content"><div class="toast-message">' + message + '</div></div>';
    container.appendChild(toast);
    setTimeout(function () { if (toast.parentNode) toast.remove(); }, 5000);
}

function showLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = 'spinnerOverlay';
        overlay.className = 'spinner-overlay';
        overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
        document.body.appendChild(overlay);
    }
    overlay.style.display = 'flex';
}

function hideLoading() {
    // پیدا کردن تمام المان‌های لودینگ
    var overlays = document.querySelectorAll('#spinnerOverlay, .spinner-overlay');

    // حذف کامل از DOM (نه فقط مخفی کردن)
    overlays.forEach(function (overlay) {
        if (overlay && overlay.parentNode) {
            overlay.parentNode.removeChild(overlay);
        }
    });
}

function apiCall(url, method, data, onSuccess, onFail) {
    showLoading();
    var token = localStorage.getItem('token') || sessionStorage.getItem('token');
    var headers = { 'Content-Type': 'application/json' };
    if (token) {
        headers['Authorization'] = 'Bearer ' + token;
    }

    fetch(url, {
        method: method || 'GET',
        headers: headers,
        credentials: 'include',
        body: data ? JSON.stringify(data) : null,
    })
        .then(async function (response) {
            var text = await response.text();
            var res;
            try {
                res = JSON.parse(text);
            } catch (e) {
                res = { message: text || 'Invalid response' };
            }
            hideLoading();
            if (response.ok) {
                if (onSuccess) onSuccess(res);
            } else {
                throw res;
            }
        })
        .catch(function (error) {
            hideLoading();
            var msg = error.message || 'خطا در ارتباط با سرور';
            showToast(msg, 'error');
            if (onFail) onFail(error);
        });
}

window.showToast = showToast;
window.showLoading = showLoading;
window.hideLoading = hideLoading;
window.apiCall = apiCall;

console.log('✅ Site functions loaded');