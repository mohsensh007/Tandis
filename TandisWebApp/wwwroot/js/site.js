// ============================================================
// تندیس — توابع سراسری (نسخه ادغام‌شده site.js + site-functions.js)
// ============================================================
if (typeof window.__SITE_LOADED__ === 'undefined') {
    window.__SITE_LOADED__ = true;
    window.__SITE_FUNCTIONS_LOADED__ = true; // ✅ اگه فایل قدیمی site-functions هم لود شد، غیرفعال بشه
    window.__SITE_JS_VERSION__ = "5.0.0-unified";

    // ===== ✅ تزریق پوسته توست (یک‌بار در هر صفحه) =====
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
                '#toastContainer .tandis-toast .tandis-toast-icon{flex:0 0 auto !important;width:22px !important;height:22px !important;margin-top:1px;}' +
                '#toastContainer .tandis-toast.success .tandis-toast-icon{stroke:#38d9a9 !important;}' +
                '#toastContainer .tandis-toast.error .tandis-toast-icon{stroke:#ff6b81 !important;}' +
                '#toastContainer .tandis-toast.warning .tandis-toast-icon{stroke:#ffd200 !important;}' +
                '#toastContainer .tandis-toast.info .tandis-toast-icon{stroke:#66d3e8 !important;}' +
                '#toastContainer .tandis-toast-content{flex:1 1 auto !important;min-width:0 !important;text-align:center !important;}' +
                '#toastContainer .tandis-toast-message{white-space:normal !important;word-break:break-word !important;line-height:1.8 !important;font-size:.86rem !important;font-weight:700 !important;color:#fff !important;}' +
                '#toastContainer .tandis-toast-close{flex:0 0 auto !important;background:rgba(255,255,255,.12) !important;border:none !important;color:#e6dcff !important;width:26px !important;height:26px !important;border-radius:8px !important;display:flex !important;align-items:center !important;justify-content:center !important;padding:0 !important;margin:0 !important;cursor:pointer !important;transition:all .2s !important;}' +
                '#toastContainer .tandis-toast-close:hover{background:rgba(255,107,129,.25) !important;color:#fff !important;}' +
                '@keyframes toastPop{from{transform:scale(.85);opacity:0}to{transform:scale(1);opacity:1}}' +
                '@keyframes slideIn{from{transform:scale(.85);opacity:0}to{transform:scale(1);opacity:1}}';
            var st = document.createElement('style');
            st.id = 'toastSkin';
            st.textContent = css;
            (document.head || document.documentElement).appendChild(st);
        })();
    }

    // ===== اعداد فارسی / فرمت / escape =====
    var FaDigits = ['\u06F0', '\u06F1', '\u06F2', '\u06F3', '\u06F4', '\u06F5', '\u06F6', '\u06F7', '\u06F8', '\u06F9'];

    function toFa(n) {
        return String(n).replace(/[0-9]/g, function (d) { return FaDigits[d]; });
    }

    function fmtNum(n) {
        return Number(n || 0).toLocaleString('fa-IR');
    }

    function esc(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // ===== آیکن‌های توست با رنگ تم تندیس =====
    var ICON_SUCCESS = '<svg class="tandis-toast-icon" viewBox="0 0 24 24" fill="none" stroke="#38d9a9" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg>';
    var ICON_ERROR = '<svg class="tandis-toast-icon" viewBox="0 0 24 24" fill="none" stroke="#ff6b81" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>';
    var ICON_WARNING = '<svg class="tandis-toast-icon" viewBox="0 0 24 24" fill="none" stroke="#ffd200" stroke-width="2.5"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>';
    var ICON_INFO = '<svg class="tandis-toast-icon" viewBox="0 0 24 24" fill="none" stroke="#66d3e8" stroke-width="2.5"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';
    var ICON_CLOSE = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>';

    var ICONS = { success: ICON_SUCCESS, error: ICON_ERROR, warning: ICON_WARNING, info: ICON_INFO };

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
        toast.innerHTML =
            (ICONS[type] || ICONS.info) +
            '<div class="tandis-toast-content"><div class="tandis-toast-message">' + esc(message) + '</div></div>' +
            '<button class="tandis-toast-close" aria-label="Close">' + ICON_CLOSE + '</button>';
        container.appendChild(toast);

        var dismissTimer = setTimeout(function () { dismissToast(toast); }, 5000);
        var btn = toast.querySelector('.tandis-toast-close');
        if (btn) {
            btn.addEventListener('click', function () {
                clearTimeout(dismissTimer);
                dismissToast(toast);
            });
        }
    }

    function dismissToast(toast) {
        toast.style.animation = 'toastPop 0.25s ease reverse';
        setTimeout(function () { if (toast.parentNode) toast.remove(); }, 250);
    }

    // ===== Loading =====
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
        var overlays = document.querySelectorAll('#spinnerOverlay, .spinner-overlay');
        overlays.forEach(function (overlay) {
            if (overlay && overlay.parentNode) {
                overlay.parentNode.removeChild(overlay);
            }
        });
    }

    function forceHideLoading() {
        hideLoading();
    }

    // ===== فراخوانی API =====
    function apiCall(url, method, data, onSuccess, onFail) {
        forceHideLoading();
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
                    var err = new Error(res.message || res.Message || 'خطا در عملیات');
                    err.response = res;
                    throw err;
                }
            })
            .catch(function (error) {
                hideLoading();
                var msg = error.message || 'خطا در ارتباط با سرور';
                showToast(msg, 'error');
                if (typeof onFail === 'function') {
                    onFail(error);
                }
            });
    }

    // ===== پروفایل / لاگین =====
    function loadUserProfile() {
        if (window.location.pathname.includes('login') ||
            window.location.pathname.includes('register')) {
            return;
        }
        var token = localStorage.getItem('token') || sessionStorage.getItem('token');
        if (!token) return;

        apiCall('/api/user/profile', 'GET', null,
            function (data) {
                hideLoading();
                displayUserData(data);
                setTimeout(function () { forceHideLoading(); }, 500);
            },
            function (error) {
                hideLoading();
                forceHideLoading();
                if (error.response && error.response.status !== 404) {
                    showToast('خطا در دریافت اطلاعات کاربر', 'error');
                }
            }
        );
    }

    function displayUserData(data) {
        try {
            if (document.getElementById('userName')) {
                document.getElementById('userName').textContent = data.name || data.fullName || data.username || 'کاربر';
            }
            if (document.getElementById('userEmail')) {
                document.getElementById('userEmail').textContent = data.email || '';
            }
            if (document.getElementById('userPhone')) {
                document.getElementById('userPhone').textContent = data.phone || data.mobile || '';
            }
        } catch (e) { }
    }

    function handleLogin(username, password) {
        apiCall('/api/login', 'POST', { username: username, password: password },
            function (response) {
                if (response.token) {
                    localStorage.setItem('token', response.token);
                    sessionStorage.setItem('token', response.token);
                }
                if (response.user || response.data) {
                    var user = response.user || response.data;
                    displayUserData(user);
                    hideLoading();
                    window.location.href = '/dashboard';
                } else {
                    loadUserProfile();
                }
            },
            function (error) {
                hideLoading();
                showToast('ورود ناموفق بود', 'error');
            }
        );
    }

    function checkAuthStatus() {
        // احراز هویت کوکی‌محور هست — نیازی به چک localStorage نیست
    }

    // ===== اجرای خودکار =====
    document.addEventListener('DOMContentLoaded', function () {
        hideLoading();

        if (!window.location.pathname.includes('login') &&
            !window.location.pathname.includes('register')) {
            checkAuthStatus();
        }

        var loginForm = document.getElementById('loginForm');
        if (loginForm) {
            loginForm.addEventListener('submit', function (e) {
                e.preventDefault();
                var username = document.getElementById('username') ? document.getElementById('username').value : '';
                var password = document.getElementById('password') ? document.getElementById('password').value : '';
                if (username && password) {
                    handleLogin(username, password);
                } else {
                    showToast('لطفاً نام کاربری و رمز عبور را وارد کنید', 'warning');
                }
            });
        }
    });

    // ===== Export سراسری =====
    window.showToast = showToast;
    window.dismissToast = dismissToast;
    window.showLoading = showLoading;
    window.hideLoading = hideLoading;
    window.forceHideLoading = forceHideLoading;
    window.apiCall = apiCall;
    window.loadUserProfile = loadUserProfile;
    window.handleLogin = handleLogin;
    window.checkAuthStatus = checkAuthStatus;
    window.displayUserData = displayUserData;
    window.toFa = toFa;
    window.fmtNum = fmtNum;
    window.esc = esc;
}