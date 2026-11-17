/* ============================================================
   biometric.js — ثبت و استفاده از اثر انگشت / چهره (WebAuthn)
   • اندروید: اثر انگشت (Platform Authenticator)
   • آیفون / آیپد: Face ID یا Touch ID
   • ویندوز: Windows Hello
   همهٔ بررسی‌های امنیتی روی سرور انجام می‌شود؛ این فایل فقط پل مرورگر است.
   ============================================================ */
(function () {
    'use strict';

    function b64url(buf) {
        var bytes = new Uint8Array(buf);
        var s = '';
        for (var i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
        return btoa(s).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function fromB64url(str) {
        var s = String(str || '').replace(/-/g, '+').replace(/_/g, '/');
        while (s.length % 4) s += '=';
        var bin = atob(s);
        var bytes = new Uint8Array(bin.length);
        for (var i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
        return bytes.buffer;
    }

    function fail(message, extra) {
        var e = new Error(message);
        if (extra) for (var k in extra) e[k] = extra[k];
        return e;
    }

    function post(url, data) {
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify(data || {})
        }).then(function (r) {
            if (r.status === 401) return { success: false, message: 'نشست منقضی شده است؛ دوباره وارد شوید.', authExpired: true };
            return r.json();
        });
    }

    function toast(msg, isErr) {
        if (typeof window.showToast === 'function') { window.showToast(msg, isErr ? 'error' : 'success'); return; }
        var el = document.getElementById('bioToast');
        if (!el) {
            el = document.createElement('div');
            el.id = 'bioToast';
            el.className = 'bio-notification';
            el.style.cssText = 'position:fixed;left:50%;bottom:24px;transform:translateX(-50%);z-index:1300;' +
                'padding:12px 20px;border-radius:16px;font-size:.84rem;font-weight:700;display:none;' +
                'max-width:92vw;text-align:center;';
            document.body.appendChild(el);
        }
        el.textContent = msg || '';
        el.classList.toggle('bio-error', !!isErr);
        el.style.display = 'block';
        clearTimeout(el._t);
        el._t = setTimeout(function () { el.style.display = 'none'; }, 4200);
    }

    /* ---------- پنجرهٔ گرفتن رمز عبور (برای میز خدمت) ---------- */
    function promptPassword(title, hint) {
        return new Promise(function (resolve, reject) {
            var wrap = document.createElement('div');
            wrap.className = 'bio-backdrop';
            wrap.style.cssText = 'position:fixed;inset:0;z-index:1400;display:flex;align-items:center;justify-content:center;padding:16px;';
            wrap.innerHTML =
                '<div class="bio-dialog" style="width:100%;max-width:420px;border-radius:24px;padding:20px">' +
                '<div style="font-weight:800;font-size:1rem;margin-bottom:6px">' + (title || 'تأیید هویت') + '</div>' +
                '<div class="text-muted" style="font-size:.76rem;margin-bottom:12px;line-height:1.8">' +
                (hint || 'برای ثبت اثر انگشت، رمز عبور خود را وارد کنید.') + '</div>' +
                '<input id="bioPw" class="form-control" type="password" inputmode="text" placeholder="رمز عبور" ' +
                'style="width:100%;padding:11px 13px;font-size:.9rem;margin-bottom:12px" />' +
                '<div style="display:flex;gap:8px">' +
                '<button type="button" id="bioPwOk" class="btn btn-primary" style="flex:1;padding:11px">تأیید</button>' +
                '<button type="button" id="bioPwNo" class="btn btn-glass" style="flex:1;padding:11px">انصراف</button>' +
                '</div></div>';
            document.body.appendChild(wrap);

            var input = wrap.querySelector('#bioPw');
            input.focus();

            function done(v) { wrap.remove(); resolve(v); }
            wrap.querySelector('#bioPwOk').onclick = function () {
                var v = input.value || '';
                if (!v) { input.focus(); return; }
                done(v);
            };
            wrap.querySelector('#bioPwNo').onclick = function () {
                wrap.remove();
                reject(fail('ثبت بیومتریک لغو شد', { cancelled: true }));
            };
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') wrap.querySelector('#bioPwOk').click();
                if (e.key === 'Escape') wrap.querySelector('#bioPwNo').click();
            });
        });
    }

    /* ---------- پشتیبانی دستگاه ---------- */
    async function available() {
        try {
            if (!window.isSecureContext) return false;
            if (!window.PublicKeyCredential) return false;
            if (typeof PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable !== 'function') return false;
            return await PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
        } catch (e) {
            return false;
        }
    }

    function status() {
        return fetch('/api/Biometric/Status', { credentials: 'same-origin' })
            .then(function (r) { return r.json(); });
    }

    /* ---------- ثبت эффекت انگشت ---------- */
    async function enrollWithOptions(opt) {
        var cred = await navigator.credentials.create({ publicKey: opt });
        var resp = cred.response;

        // کلید عمومی (SPKI) — مرورگرهای مدرن این متد را دارند
        var spki = (typeof resp.getPublicKey === 'function') ? resp.getPublicKey() : null;
        var alg = (typeof resp.getPublicKeyAlgorithm === 'function') ? resp.getPublicKeyAlgorithm() : -7;

        if (!spki) {
            throw fail('مرورگر شما از ثبت اثر انگشت در این حالت پشتیبانی نمی‌کند. لطفاً از Chrome/Edge (ویندوز یا اندروید) یا Safari 16+ استفاده کنید.');
        }

        var res = await post('/api/Biometric/EnrollComplete', {
            webAuthnId: b64url(cred.rawId),
            clientDataJson: b64url(resp.clientDataJSON),
            publicKeySpki: b64url(spki),
            alg: alg,
            deviceLabel: (navigator.platform || '') + ' ' + (navigator.userAgent || '').slice(0, 80)
        });

        if (!res.success) throw fail(res.message || 'ثبت اثر انگشت ناموفق بود');
        toast('اثر انگشت / چهره ثبت شد');
        return true;
    }

    async function enroll() {
        if (!(await available()))
            throw fail('این دستگاه اثر انگشت / چهره برای ورود ندارد یا مرورگر آن را پشتیبانی نمی‌کند');

        var begin = await post('/api/Biometric/EnrollBegin', {});
        if (!begin.success) throw fail(begin.message || 'شروع ثبت ناموفق بود', { needPassword: !!begin.needPassword });
        return enrollWithOptions(begin.data);
    }

    /* ---------- ثبت برای کاربر میز خدمت: هر بار رمز عبور ---------- */
    async function enrollRequirePassword() {
        if (!(await available()))
            throw fail('این دستگاه اثر انگشت / چهره برای ورود ندارد یا مرورگر آن را پشتیبانی نمی‌کند');

        var pw = await promptPassword('ثبت اثر انگشت — میز خدمت', 'برای ثبت اثر انگشت یا چهره، رمز عبور حساب خود را وارد کنید.');
        var begin = await post('/api/Biometric/EnrollBeginDesk', { password: pw });
        if (!begin.success) throw fail(begin.message || 'رمز عبور صحیح نیست');
        return enrollWithOptions(begin.data);
    }

    /* ---------- تأیید با اثر انگشت (جای رمز عبور) ---------- */
    async function verify(purpose) {
        if (!(await available()))
            throw fail('این دستگاه اثر انگشت / چهره برای تأیید ندارد');

        var begin = await post('/api/Biometric/AssertBegin', { purpose: purpose || '' });
        if (!begin.success) throw fail(begin.message || 'شروع تأیید ناموفق بود');

        var opt = begin.data;
        opt.challenge = fromB64url(opt.challenge);
        if (opt.allowCredentials) {
            opt.allowCredentials = opt.allowCredentials.map(function (c) {
                return { type: 'public-key', id: fromB64url(c.id), transports: c.transports };
            });
        }

        var assertion;
        try {
            assertion = await navigator.credentials.get({ publicKey: opt });
        } catch (e) {
            throw fail('تأیید اثر انگشت / چهره انجام نشد', { cancelled: e && e.name === 'NotAllowedError' });
        }

        var res = await post('/api/Biometric/AssertComplete', {
            webAuthnId: b64url(assertion.rawId),
            clientDataJson: b64url(assertion.response.clientDataJSON),
            authenticatorData: b64url(assertion.response.authenticatorData),
            signature: b64url(assertion.response.signature),
            purpose: purpose || ''
        });

        if (!res.success) throw fail(res.message || 'تأیید ناموفق بود');
        return res.ticket;
    }

    /* ---------- حذف ثبت ---------- */
    async function remove(password) {
        if (document.body.dataset.bioType === 'desk' && !password) {
            password = await promptPassword('حذف اثر انگشت — میز خدمت', 'برای حذف اثر انگشت ثبت‌شده، رمز عبور خود را وارد کنید.');
        }
        var res = await post('/api/Biometric/Delete', { password: password || '' });
        if (!res.success) throw fail(res.message || 'حذف ناموفق بود');
        toast('ثبت اثر انگشت حذف شد');
        return true;
    }

    window.TandisBio = {
        available: available,
        status: status,
        enroll: enroll,
        enrollRequirePassword: enrollRequirePassword,
        verify: verify,
        remove: remove,
        toast: toast
    };

    /* ---------- اتصال خودکار به آیکن‌های هدر (data-bio-enroll) ---------- */
    function refreshBioButtons() {
        document.querySelectorAll('[data-bio-enroll]').forEach(function (btn) {
            status().then(function (s) {
                if (!s || !s.success) return;
                var on = !!s.enrolled;
                btn.classList.toggle('bio-on', on);
                btn.title = on
                    ? 'اثر انگشت / چهرهٔ ثبت‌شده — برای ثبت دوباره کلیک کنید'
                    : 'ثبت اثر انگشت / چهره';
            }).catch(function () { });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('[data-bio-enroll]').forEach(function (btn) {
            btn.addEventListener('click', async function () {
                if (btn.dataset.bioBusy === '1') return;
                btn.dataset.bioBusy = '1';
                try {
                    var s = await status();
                    if (s && s.success && s.enrolled) {
                        if (!window.confirm('اثر انگشت / چهره قبلاً برای این حساب ثبت شده است.\nبرای ثبت مجدد، ثبت قبلی حذف و دستگاه جدید ثبت می‌شود. ادامه می‌دهید؟')) return;
                        await remove();
                    }
                    if (btn.dataset.bioEnroll === 'password') await enrollRequirePassword();
                    else await enroll();
                    refreshBioButtons();
                } catch (e) {
                    if (e && e.needPassword) {
                        try { await enrollRequirePassword(); refreshBioButtons(); }
                        catch (e2) { if (!(e2 && e2.cancelled)) toast((e2 && e2.message) || 'ثبت بیومتریک انجام نشد', true); }
                    } else if (!(e && e.cancelled)) {
                        toast((e && e.message) || 'ثبت بیومتریک انجام نشد', true);
                    }
                } finally {
                    btn.dataset.bioBusy = '';
                }
            });
        });
        refreshBioButtons();
    });
})();
