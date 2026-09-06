// ============================
// جلوگیری از اجرای دوباره
// ============================
if (typeof window.__SITE_LOADED__ === 'undefined') {

    window.__SITE_LOADED__ = true;
    window.__SITE_JS_VERSION__ = "3.1.0";

    // --- همه متغیرها با var (نه const) ---
    /*var FaDigits = ['\u06F0', '\u06F1', '\u06F2', '\u06F3', '\u06F4', '\u06F5', '\u06F6', '\u06F7', '\u06F8', '\u06F9'];*/

    function toFa(n) {
        return String(n).replace(/[0-9]/g, function (d) { return FaDigits[d]; });
    }

    function fmtNum(n) {
        return Number(n || 0).toLocaleString('fa-IR');
    }

    function esc(str) {
        if (str === null || str === undefined) return '';
        return String(str)
            .replace(/&/g, '\u0026\u0061\u006D\u0070\u003B')
            .replace(/</g, '\u0026\u006C\u0074\u003B')
            .replace(/>/g, '\u0026\u0067\u0074\u003B')
            .replace(/"/g, '\u0026\u0071\u0075\u006F\u0074\u003B')
            .replace(/'/g, '\u0026\u0023\u0033\u0039\u003B');
    }

    var ICON_SUCCESS = '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>';
    var ICON_ERROR = '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>';
    var ICON_WARNING = '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>';
    var ICON_INFO = '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';
    var ICON_CLOSE = '<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>';

    var ICONS = {
        success: ICON_SUCCESS,
        error: ICON_ERROR,
        warning: ICON_WARNING,
        info: ICON_INFO
    };

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
        toast.innerHTML = (ICONS[type] || ICONS.info) + '<div class="toast-content"><div class="toast-message">' + esc(message) + '</div></div><button class="toast-close" aria-label="Close">' + ICON_CLOSE + '</button>';
        container.appendChild(toast);
        var dismissTimer = setTimeout(function () { dismissToast(toast); }, 5000);
        toast.querySelector('.toast-close').addEventListener('click', function () {
            clearTimeout(dismissTimer);
            dismissToast(toast);
        });
    }

    function dismissToast(toast) {
        toast.style.animation = 'slideIn 0.2s ease reverse';
        setTimeout(function () { if (toast.parentNode) toast.remove(); }, 200);
    }

    function showLoading() {
        // اول هر loading قدیمی رو کامل حذف کن
        hideLoading();

        var overlay = document.createElement('div');
        overlay.id = 'spinnerOverlay';
        overlay.className = 'spinner-overlay';
        overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
        document.body.appendChild(overlay);
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

    // ============================================================
    // API CALL
    // ============================================================
    function apiCall(url, method, data, onSuccess, onFail) {
        showLoading();
        console.log(' API Call:', url);

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
                    // ✅ اصلاح: throw کردن یک Error object به جای res خالی
                    var err = new Error(res.message || res.Message || 'خطا در عملیات');
                    err.response = res;
                    throw err;
                }
            })
            .catch(function (error) {
                // ✅ اول لودینگ رو مخفی کن (مهم!)
                try {
                    hideLoading();
                } catch (e) {
                    console.error('hideLoading error:', e);
                }

                console.error(' API Error:', error);

                // ✅ استخراج پیام از حالت‌های مختلف
                var msg = 'خطا در ارتباط با سرور';
                if (error && error.message) {
                    msg = error.message;
                } else if (error && error.response) {
                    msg = error.response.message || error.response.Message || msg;
                }

                console.log('📢 نمایش پیام به کاربر:', msg);

                // ✅ فقط یک بار پیام نشون بده (نه alert، نه showToast تکراری)
                if (typeof showToast === 'function') {
                    showToast(msg, 'error');
                }

                // ✅ اجرای onFail اگر تعریف شده باشد
                if (typeof onFail === 'function') {
                    onFail(error);
                }
            });
    }

    function loadUserProfile() {
        console.log(' Loading user profile...');

        // فقط اگر در صفحه لاگین یا ثبت‌نام نیستیم
        if (window.location.pathname.includes('login') ||
            window.location.pathname.includes('register')) {
            return;
        }

        // چک کن که آیا API وجود داره یا نه
        var token = localStorage.getItem('token') || sessionStorage.getItem('token');
        if (!token) {
            console.log('ℹ️ No token, skipping profile load');
            return;
        }

        showLoading();

        apiCall('/api/user/profile', 'GET', null,
            function (data) {
                console.log('✅ Profile loaded:', data);
                hideLoading();
                displayUserData(data);
            },
            function (error) {
                console.error('❌ Profile load failed:', error);
                // ✅ حتماً hideLoading رو صدا بزن حتی اگر API خطا داد
                hideLoading();
                // فقط اگر خطای 404 بود، showToast نده (یعنی API وجود نداره)
                if (error.response && error.response.status !== 404) {
                    showToast('خطا در دریافت اطلاعات کاربر', 'error');
                }
            }
        );
    }
    function displayUserData(data) {
        console.log('👤 Displaying user data:', data);
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
        } catch (e) {
            console.error('Error displaying user data:', e);
        }
    }

    function handleLogin(username, password) {
        console.log('🔐 Login started');
        showLoading();
        apiCall('/api/login', 'POST', { username: username, password: password },
            function (response) {
                console.log('✅ Login success:', response);
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
                console.error('❌ Login failed:', error);
                hideLoading();
                showToast('ورود ناموفق بود', 'error');
            }
        );
    }

    function checkAuthStatus() {
        console.log('🔍 Checking auth status...');
        var token = localStorage.getItem('token') || sessionStorage.getItem('token');
        if (token) {
            console.log('✅ Token found, loading profile...');
            loadUserProfile();
        } else {
            console.log('ℹ️ No token found');
        }
    }

    // --- Calendar Helpers ---
    var calendarDate = { y: 1403, m: 1, d: 1 };
    var calendarCallback = null;

    function initCalendar(inputId, initialDate, allowPast) {
        calendarCallback = inputId;
    }

    function renderCalendar() {
        console.log('📅 Calendar rendered');
    }

    function openCalendar(inputId, title, allowPast) {
        $('#calendarTitle').text(title || 'Select Date');
        initCalendar(inputId, null, allowPast || false);
        new bootstrap.Modal('#datePickerModal').show();
    }

    // --- Auto-run ---
    document.addEventListener('DOMContentLoaded', function () {
        console.log('🚀 Site JS loaded v3.1');
        // ✅ حذف هرگونه loading باقی‌مانده از صفحه قبل
        hideLoading();

        if (!window.location.pathname.includes('login') &&
            !window.location.pathname.includes('register')) {
            checkAuthStatus();
        }

        var loginForm = document.getElementById('loginForm');
        if (loginForm) {
            loginForm.addEventListener('submit', function (e) {
                e.preventDefault();
                var username = document.getElementById('username')?.value || '';
                var password = document.getElementById('password')?.value || '';
                if (username && password) {
                    handleLogin(username, password);
                } else {
                    showToast('لطفاً نام کاربری و رمز عبور را وارد کنید', 'warning');
                }
            });
        }
    });

    // --- Export ---
    window.showToast = showToast;
    window.showLoading = showLoading;
    window.hideLoading = hideLoading;
    window.apiCall = apiCall;
    window.loadUserProfile = loadUserProfile;
    window.handleLogin = handleLogin;
    window.checkAuthStatus = checkAuthStatus;
    window.displayUserData = displayUserData;
    window.toFa = toFa;
    window.fmtNum = fmtNum;
    window.esc = esc;

    console.log('✅ All functions exported');

    // ============================
    // بستن IF
    // ============================
} // پایان if