// ============================
// تندیس - جاوااسکریپت مشترک
// ============================

// --- ارقام فارسی ---
const FaDigits = ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'];

// --- Toast ---
function showToast(message, type) {
    type = type || 'success';
    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    const icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    const container = document.getElementById('toastContainer') || document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>') || document.getElementById('toastContainer');

    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        ${icons[type] || icons.info}
        <div class="toast-content">
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" aria-label="بستن">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
        </button>
    `;

    container.appendChild(toast);

    const dismissTimer = setTimeout(() => dismissToast(toast), 5000);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(dismissTimer);
        dismissToast(toast);
    });
}

function dismissToast(toast) {
    toast.style.animation = 'slideIn 0.2s ease reverse';
    setTimeout(() => toast.remove(), 200);
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}

// --- Toast ---
function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        ${icons[type] || icons.info}
        <div class="toast-content">
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" aria-label="بستن">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
        </button>
    `;

    container.appendChild(toast);

    var dismissTimer = setTimeout(() => dismissToast(toast), 5000);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(dismissTimer);
        dismissToast(toast);
    });
}

function dismissToast(toast) {
    toast.style.animation = 'slideIn 0.2s ease reverse';
    setTimeout(() => toast.remove(), 200);
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}

// --- Toast ---
function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        ${icons[type] || icons.info}
        <div class="toast-content">
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" aria-label="بستن">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
        </button>
    `;

    container.appendChild(toast);

    var dismissTimer = setTimeout(() => dismissToast(toast), 5000);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(dismissTimer);
        dismissToast(toast);
    });
}

function dismissToast(toast) {
    toast.style.animation = 'slideIn 0.2s ease reverse';
    setTimeout(() => toast.remove(), 200);
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}

// --- Toast ---
function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        ${icons[type] || icons.info}
        <div class="toast-content">
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" aria-label="بستن">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
        </button>
    `;

    container.appendChild(toast);

    var dismissTimer = setTimeout(() => dismissToast(toast), 5000);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(dismissTimer);
        dismissToast(toast);
    });
}

function dismissToast(toast) {
    toast.style.animation = 'slideIn 0.2s ease reverse';
    setTimeout(() => toast.remove(), 200);
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}

// --- Toast ---
function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        ${icons[type] || icons.info}
        <div class="toast-content">
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" aria-label="بستن">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
        </button>
    `;

    container.appendChild(toast);

    var dismissTimer = setTimeout(() => dismissToast(toast), 5000);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(dismissTimer);
        dismissToast(toast);
    });
}

function dismissToast(toast) {
    toast.style.animation = 'slideIn 0.2s ease reverse';
    setTimeout(() => toast.remove(), 200);
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}

// --- Toast ---
function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        ${icons[type] || icons.info}
        <div class="toast-content">
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" aria-label="بستن">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
        </button>
    `;

    container.appendChild(toast);

    var dismissTimer = setTimeout(() => dismissToast(toast), 5000);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(dismissTimer);
        dismissToast(toast);
    });
}

function dismissToast(toast) {
    toast.style.animation = 'slideIn 0.2s ease reverse';
    setTimeout(() => toast.remove(), 200);
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}

// --- Toast ---
function showToast(message, type) {
    type = type || 'info';
    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var icons = {
        success: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#00b296" stroke-width="2"><polyline points="20 6 9 17 4 12"></polyline></svg>',
        error: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#da1e28" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
        warning: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#e6ac00" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2z"/></svg>',
        info: '<svg class="toast-icon" viewBox="0 0 24 24" fill="none" stroke="#0062ff" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
    };

    var container = document.getElementById('toastContainer');
    if (!container) {
        document.body.insertAdjacentHTML('afterbegin', '<div id="toastContainer" class="toast-container"></div>');
        container = document.getElementById('toastContainer');
    }

    var toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        ${icons[type] || icons.info}
        <div class="toast-content">
            <div class="toast-message">${message}</div>
        </div>
        <button class="toast-close" aria-label="بستن">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
        </button>
    `;

    container.appendChild(toast);

    var dismissTimer = setTimeout(() => dismissToast(toast), 5000);
    toast.querySelector('.toast-close').addEventListener('click', () => {
        clearTimeout(dismissTimer);
        dismissToast(toast);
    });
}

function dismissToast(toast) {
    toast.style.animation = 'slideIn 0.2s ease reverse';
    setTimeout(() => toast.remove(), 200);
}

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

// --- تبدیل اعداد به فارسی ---
function faNum(n) {
    return String(n).replace(/[0-9]/g, function(d) { return ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'][d]; });
}

// --- Escape HTML ---
function esc(str) {
    if (str === null || str === undefined) return '';
    return String(str).replace(/&/g,'&').replace(/</g,'<').replace(/>/g,'>').replace(/"/g,'"').replace(/'/g,''');
}

// --- Loading ---
function showLoading() {
    if (document.getElementById('spinnerOverlay')) return;
    var overlay = document.createElement('div');
    overlay.id = 'spinnerOverlay';
    overlay.className = 'spinner-overlay';
    overlay.innerHTML = '<div class="spinner-border text-primary" role="status"></div>';
    document.body.appendChild(overlay);
}

function hideLoading() {
    var overlay = document.getElementById('spinnerOverlay');
    if (overlay) overlay.remove();
}