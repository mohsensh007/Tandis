// ============================
// توابع کمکی سایت (نسخه نهایی - توست بالای صفحه)
// ============================

if (typeof window.__SITE_FUNCTIONS_LOADED__ === 'undefined') {
    window.__SITE_FUNCTIONS_LOADED__ = true;

    // ===== ✅ تزریق استایل توست (کلاس‌های جدید تا CSS قدیمی نبینتشون) =====
    if (typeof window.__TOAST_SKIN__ === 'undefined') {
        window.__TOAST_SKIN__ = true;
        (function () {
            var css = '' +
                '#toastContainer.tandis-toasts{position:fixed !important;top:calc(16px + env(safe-area-inset-top)) !important;left:0 !important;right:0 !important;margin:0 auto !important;width:calc(100% - 32px) !important;max-width:420px !important;display:flex !important;flex-direction:column !important;gap:10px !important;z-index:4000 !important;pointer-events:none !important;background:transparent !important;border:none !important;padding:0 !important;}' +
                '#toastContainer .tandis-toast{display:flex !important;align-items:flex-start !important;gap:10px !important;width:100% !important;max-width:100% !important;background:linear-gradient(135deg,rgba(102,126,234,.30),rgba(118,75,162,.26)) !important;backdrop-filter:blur(14px) !important;-webkit-backdrop-filter:blur(14px) !important;border:1px solid rgba(255,255,255,.22) !important;border-radius:16px !important;padding:12px 16px !important;color:#fff !important;box-shadow:0 16px 40px rgba(0,0,0,.5) !important;opacity:1 !important;pointer-events:auto !important;position:relative !important;overflow:hidden !important;animation:toastPop .35s cubic-bezier(.22,.9,.35,1.2) !important;}' +
                '#toastContainer .tandis-toast::before{content:"";position:absolute;top:-40px;right:-40px;width:120px;height:120px;background:radial-gradient(circle,rgba(102,211,232,.30),transparent 70%);pointer-events:none;}' +
                '#toastContainer .tandis-toast::after{content:"";position:absolute;bottom:-50px;left:-30px;width:110px;height:110px;background:radial-gradient(circle,rgba(118,75,162,.35),transparent 70%);pointer-events:none;}' +
                '#toastContainer .tandis-toast.success{border-right:4px solid #38d9a9 !important;}' +
                '#toastContainer .tandis-toast.error{border-right:4px solid #ff6b81 !important;}' +
                '#toastContainer .tandis-toast.warning{border-right:4px solid #ffd200 !important;}' +
                '#toastContainer .tandis-toast.info{border-right:4px solid #66d3e8 !important;}' +
                '#toastContainer .tandis-toast-content{flex:1 1 auto !important;min-width:0 !important;text-align:center !important;}' +
                '#toastContainer .tandis-toast-message{white-space:normal !important;word-break:break-word !important;line-height:1.8 !important;font-size:.86rem !important;font-weight:700 !important;color:#fff !important;}' +
                '@keyframes toastPop{from{transform:scale(.85);opacity:0}to{transform:scale(1);opacity:1}}' +
                '@keyframes slideIn{from{transform:scale(.85);opacity:0}to{transform:scale(1);opacity:1}}';
            var st = document.createElement('style');
            st.id = 'toastSkin';
            st.textContent = css;
            (document.head || document.documentElement).appendChild(st);
        })();
    }

    // ===== نمایش توست =====
    function showToast(message, type) {
        type = type || 'info';
        var container = document.getElementById('toastContainer');
        if (!container) {
            var div = document.createElement('div');
            div.id = 'toastContainer';
            div.className = 'tandis-toasts';
            document.body.appendChild(div);
            container = div;
        }
        var toast = document.createElement('div');
        toast.className = 'tandis-toast ' + type;
        toast.innerHTML = '<div class="tandis-toast-content"><div class="tandis-toast-message">' + message + '</div></div>';
        container.appendChild(toast);
        setTimeout(function () {
            toast.style.animation = 'toastPop .25s ease reverse';
            setTimeout(function () { if (toast.parentNode) toast.remove(); }, 250);
        }, 5000);
    }

    // ===== لودینگ =====
    function showLoading() {
        hideLoading();
        var overlay = document.createElement('div');
        overlay.id = 'spinnerOverlay';
        overlay.className = 'spinner-overlay';
        overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
        document.body.appendChild(overlay);
    }

    function hideLoading() {
        var overlays = document.querySelectorAll('#spinnerOverlay, .spinner-overlay');
        overlays.forEach(function (overlay) {
            if (overlay && overlay.parentNode) {
                overlay.parentNode.removeChild(overlay);
            }
        });
    }

    // ===== فراخوانی API =====
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
    console.log('✅ Site functions loaded (tandis toast v2)');
}