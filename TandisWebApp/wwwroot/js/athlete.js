/* ============================================================
   athlete.js — صفحه‌ی «شروع تمرین» پنل عضو
   ✅ ورود باز → کمد + جدول تمرین | خروج → دکمه‌ی خلاصه
   ✅ بعد از تیک‌زدن همه حرکات → دکمه «پایان تمرین و مشاهده خلاصه»
   ✅ خلاصه از endpoint زنده /Athlete/Summary (بدون رفرش)
   ============================================================ */
(function () {
    'use strict';

    var D = {};
    try { D = JSON.parse(document.getElementById('athlete-data').textContent) || {}; } catch (e) { D = {}; }

    /* ---------- کمکی‌ها ---------- */
    function fa(n) { return String(n).replace(/[0-9]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; }); }
    function p2(x) { return (x < 10 ? '0' : '') + x; }
    function hhmmss(sec) { return p2(Math.floor(sec / 3600)) + ':' + p2(Math.floor(sec / 60) % 60) + ':' + p2(sec % 60); }
    function mmss(sec) { return p2(Math.floor(sec / 60)) + ':' + p2(sec % 60); }
    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function post(url, body) {
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify(body || {})
        }).then(function (r) { return r.json(); });
    }
    function toast(msg, ok) {
        var t = document.createElement('div');
        t.textContent = msg;
        t.style.cssText = 'position:fixed;top:14px;right:50%;transform:translateX(50%);z-index:2000;' +
            'padding:10px 18px;border-radius:12px;font-weight:700;font-size:.85rem;color:#fff;' +
            'background:' + (ok ? 'linear-gradient(135deg,#22c55e,#15803d)' : 'linear-gradient(135deg,#ef4444,#b91c1c)') +
            ';box-shadow:0 10px 26px rgba(0,0,0,.4);transition:opacity .4s;';
        document.body.appendChild(t);
        setTimeout(function () { t.style.opacity = '0'; }, 2600);
        setTimeout(function () { t.remove(); }, 3100);
    }

    /* ============================================================
       کامپوبوکس سفارشی
       ============================================================ */
    function Combo(el, items, selectedValue, onChange) {
        if (!el) return;
        this.el = el; this.items = items || []; this.value = null;
        this.onChange = onChange || function () { };
        this.el.classList.add('acmb');
        this.el.innerHTML =
            '<button type="button" class="acmb-btn"><span class="acmb-val text-truncate">—</span><i class="bi bi-chevron-down"></i></button>' +
            '<div class="acmb-menu"><input type="text" class="acmb-search" placeholder="جستجو…" /><div class="acmb-list"></div></div>';
        var self = this;
        this.btn = el.querySelector('.acmb-btn'); this.valEl = el.querySelector('.acmb-val');
        this.menu = el.querySelector('.acmb-menu'); this.search = el.querySelector('.acmb-search'); this.list = el.querySelector('.acmb-list');
        this.btn.addEventListener('click', function (e) {
            e.stopPropagation();
            var open = el.classList.contains('open');
            document.querySelectorAll('.acmb.open').forEach(function (a) { a.classList.remove('open'); });
            if (!open) { el.classList.add('open'); self.search.value = ''; self.renderList(); self.search.focus(); }
        });
        this.menu.addEventListener('click', function (e) { e.stopPropagation(); });
        this.search.addEventListener('input', function () { self.renderList(); });
        document.addEventListener('click', function () { el.classList.remove('open'); });
        this.setValue(selectedValue !== undefined ? selectedValue : (this.items[0] ? this.items[0].value : null), true);
    }
    Combo.prototype.setItems = function (items, selectedValue) {
        this.items = items || [];
        this.setValue(selectedValue !== undefined ? selectedValue : (this.items[0] ? this.items[0].value : null), true);
    };
    Combo.prototype.setValue = function (value, silent) {
        var found = null;
        for (var i = 0; i < this.items.length; i++) if (this.items[i].value === value) { found = this.items[i]; break; }
        if (!found && this.items.length) found = this.items[0];
        this.value = found ? found.value : null;
        this.valEl.textContent = found ? found.text : '—';
        this.renderList();
        if (!silent && found) this.onChange(this.value, found);
    };
    Combo.prototype.renderList = function () {
        var q = this.search.value.trim().toLowerCase(), self = this;
        var shown = this.items.filter(function (it) { return !q || it.text.toLowerCase().indexOf(q) > -1; });
        if (!shown.length) { this.list.innerHTML = '<div class="acmb-empty">موردی پیدا نشد</div>'; return; }
        this.list.innerHTML = shown.map(function (it) {
            return '<div class="acmb-item' + (it.value === self.value ? ' sel' : '') + '" data-v="' + esc(it.value) + '">' +
                '<span class="text-truncate">' + esc(it.text) + '</span>' +
                (it.badge ? '<span class="badge ' + esc(it.badgeClass || 'bg-secondary') + '">' + esc(it.badge) + '</span>' : '') + '</div>';
        }).join('');
        this.list.querySelectorAll('.acmb-item').forEach(function (node) {
            node.addEventListener('click', function () {
                var v = node.getAttribute('data-v'), item = null;
                for (var i = 0; i < self.items.length; i++) if (String(self.items[i].value) === v) { item = self.items[i]; break; }
                if (!item) return;
                self.el.classList.remove('open'); self.setValue(item.value);
            });
        });
    };

    /* ============================================================
       ۱) وضعیت ورود امروز
       ============================================================ */
    (function () {
        var elapsedEl = document.getElementById('elapsed');
        if (!elapsedEl) return;
        var v = D.visit;
        if (!v || !v.enterTime) { elapsedEl.textContent = '--:--:--'; return; }
        var parts = String(v.enterTime).split(':');
        var start = new Date(); start.setHours(parseInt(parts[0], 10) || 0, parseInt(parts[1], 10) || 0, 0, 0);
        var end = null;
        if (v.exitTime) { var pe = String(v.exitTime).split(':'); end = new Date(); end.setHours(parseInt(pe[0], 10) || 0, parseInt(pe[1], 10) || 0, 0, 0); }
        var tick = function () {
            var target = end || new Date();
            elapsedEl.textContent = hhmmss(Math.max(0, Math.floor((target.getTime() - start.getTime()) / 1000)));
        };
        tick(); if (!end) setInterval(tick, 1000);
    })();

    var tips = ['قبل، حین و بعد از تمرین آب بنوشید.', 'بین ست‌ها استراحت تعیین‌شده را رعایت کنید ⏱',
        'با گرم‌کردن شروع کنید و در پایان بدن را سرد کنید.', 'وزنه‌ی مناسب را انتخاب کنید؛ فرم صحیح مهم‌تر از وزن است.',
        'هر حرکت را با تیک و تایمر ثبت کنید تا پیشرفتتان ثبت شود.'];
    var tipEl = document.getElementById('athTip'), tipIdx = 0;
    if (tipEl) setInterval(function () { tipIdx = (tipIdx + 1) % tips.length; tipEl.textContent = tips[tipIdx]; }, 7000);

    /* ============================================================
       ۲) جدول تمرین
       ============================================================ */
    (function () {
        var exListBox = document.getElementById('exList');
        var btnStart = document.getElementById('btnStart');
        var cmbProgramEl = document.getElementById('cmbProgram');
        if (!exListBox || !btnStart || !cmbProgramEl) return;   // ✅ خروج زده → کرش نکن

        var dayInfo = document.getElementById('dayInfo');
        var sessionBar = document.getElementById('sessionBar');
        var cmbDay = new Combo(document.getElementById('cmbDay'), []);

        var programs = (D.programs || []).map(function (p) {
            return { value: p.PrgID, text: 'برنامه‌ی #' + p.PrgID + (p.CoachName ? ' — ' + p.CoachName : ''), days: p.days || [] };
        });
        var cmbProgram = new Combo(cmbProgramEl, programs.map(function (p) { return { value: p.value, text: p.text }; }));

        var dayItems = [], states = {}, timers = {};
        var runningId = null, runInt = null;
        var sessionStarted = false, sessionStartAt = null, sessionInt = null;
        var pendingData = null;

        function findProgram(id) { for (var i = 0; i < programs.length; i++) if (programs[i].value === id) return programs[i]; return null; }
        function fillDays(prgId) {
            var prg = findProgram(prgId), days = prg ? prg.days : [], wd = D.weekday || '', def = null;
            days.forEach(function (d) { if (!def && wd && d.indexOf(wd.substring(0, 4)) > -1) def = d; });
            cmbDay.setItems(days.map(function (d) { return { value: d, text: d, badge: d === def ? 'امروز' : null, badgeClass: 'bg-primary' }; }), def || (days[0] || null));
        }
        function techBadges(it) {
            var b = [];
            if (it.ExtraItems && it.ExtraItems.length) b.push('ترکیبی ×' + (it.ExtraItems.length + 1));
            if (it.DropCount) b.push('دراپ‌ست ×' + it.DropCount);
            if (it.PyramidDir) b.push(it.PyramidDir == 1 ? 'هرمی صعودی' : 'هرمی نزولی');
            if (it.PauseCount) b.push('مکث ×' + it.PauseCount);
            if (it.Tempo) b.push('تمپو ' + it.Tempo);
            return b;
        }

        function renderDay(data) {
            pendingData = data; dayItems = data.Items || []; states = {}; timers = {};
            (data.Logs || []).forEach(function (l) { states[l.ItemID] = { done: l.IsDone, seconds: l.DurationSec }; });
            dayInfo.innerHTML =
                (data.CoachName ? 'مربی: <b style="color:#ddd6fe;">' + esc(data.CoachName) + '</b> • ' : '') +
                'روز: <b style="color:#ddd6fe;">' + esc(data.DayTitle || '—') + '</b> • ' + fa(dayItems.length) + ' حرکت';
            if (!dayItems.length) { exListBox.innerHTML = '<div class="ath-empty">برای این روز حرکتی ثبت نشده است.</div>'; updateProgress(); return; }
            exListBox.innerHTML = dayItems.map(function (it, idx) {
                var st = states[it.ItemID] || { done: false, seconds: 0 };
                timers[it.ItemID] = Math.max(timers[it.ItemID] || 0, st.seconds || 0);
                var meta = [];
                if (it.SetCount) meta.push(fa(it.SetCount) + ' ست');
                if (it.RepCount) meta.push(fa(it.RepCount) + ' تکرار');
                if (it.WCount) meta.push('وزن ' + fa(it.WCount));
                if (it.RestSeconds) meta.push('استراحت ' + fa(it.RestSeconds) + '″');   // ✅ undefined حذف شد
                var badges = techBadges(it).map(function (t) { return '<span class="ex-tech">' + esc(t) + '</span>'; }).join('');
                return '' +
                    '<div class="ex-row' + (st.done ? ' done' : '') + '" data-item="' + it.ItemID + '" data-rest="' + (it.RestSeconds || 0) + '">' +
                    '  <div class="ex-check" title="انجام شد"><i class="bi bi-check-lg"></i></div>' +
                    '  <div class="ex-main">' +
                    '    <div class="ex-name">' + fa(idx + 1) + '. ' + esc(it.ItemDesc || 'حرکت بدون نام') + '</div>' +
                    '    <div class="ex-meta">' + esc(meta.join(' • ')) + badges + '</div>' +
                    (it.Note ? '<div class="ex-meta">💡 ' + esc(it.Note) + '</div>' : '') +
                    '  </div>' +
                    '  <div class="ex-timer">' +
                    '    <span class="ex-time">' + mmss(timers[it.ItemID] || 0) + '</span>' +
                    '    <button type="button" class="btn-timer" title="شروع/توقف تایمر"><i class="bi bi-play-fill"></i></button>' +
                    '  </div>' +
                    '</div>';
            }).join('');
            exListBox.querySelectorAll('.ex-row').forEach(bindRow);
            markNext(); updateProgress();
        }

        function markNext() {
            var nextId = null;
            for (var i = 0; i < dayItems.length; i++) { var st = states[dayItems[i].ItemID]; if (!st || !st.done) { nextId = dayItems[i].ItemID; break; } }
            document.querySelectorAll('.ex-row.ex-next').forEach(function (r) { r.classList.remove('ex-next'); });
            if (nextId != null) { var row = rowOf(nextId); if (row) row.classList.add('ex-next'); }
            return nextId;
        }
        function allDone() {
            if (!dayItems.length) return false;
            for (var i = 0; i < dayItems.length; i++) { var st = states[dayItems[i].ItemID]; if (!st || !st.done) return false; }
            return true;
        }
        function rowOf(id) { return document.querySelector('.ex-row[data-item="' + id + '"]'); }

        function bindRow(row) {
            var id = parseInt(row.getAttribute('data-item'), 10);
            row.querySelector('.ex-check').addEventListener('click', function () {
                var done = !row.classList.contains('done');
                row.classList.toggle('done', done);
                if (done) stopTimer(id);
                states[id] = states[id] || {}; states[id].done = done;
                saveSet(id); updateProgress();
                if (done) {
                    var rest = parseInt(row.getAttribute('data-rest'), 10) || 0;
                    if (rest > 0) startRest(rest);
                    if (allDone()) { toast('همه‌ی حرکات انجام شد 🎉 برای دیدن خلاصه، دکمه‌ی سبز را بزنید', true); return; }
                    var nextId = markNext();
                    if (nextId != null) {
                        var nr = rowOf(nextId);
                        if (nr) {
                            nr.classList.add('ex-pop'); setTimeout(function () { nr.classList.remove('ex-pop'); }, 900);
                            var r = nr.getBoundingClientRect();
                            if (r.top < 70 || r.bottom > window.innerHeight - 40) { try { nr.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch (e) { nr.scrollIntoView(); } }
                        }
                        toast('ثبت شد ✔ سراغ حرکت بعدی بروید', true);
                    }
                }
            });
            row.querySelector('.btn-timer').addEventListener('click', function () {
                if (runningId === id) { stopTimer(id); saveSet(id); } else { stopTimer(runningId); startTimer(id); }
            });
        }

        function startTimer(id) {
            if (!id || isNaN(id)) return;
            stopTimer(runningId); runningId = id;
            var row = rowOf(id);
            if (row) { row.classList.add('running'); row.querySelector('.btn-timer i').className = 'bi bi-pause-fill'; }
            runInt = setInterval(function () { timers[id] = (timers[id] || 0) + 1; var r = rowOf(id); if (r) r.querySelector('.ex-time').textContent = mmss(timers[id]); }, 1000);
        }
        function stopTimer(id) {
            if (runInt) { clearInterval(runInt); runInt = null; }
            if (id) { var row = rowOf(id); if (row) { row.classList.remove('running'); row.querySelector('.btn-timer i').className = 'bi bi-play-fill'; } }
            if (runningId === id) runningId = null;
        }
        function saveSet(id) {
            if (!id || isNaN(id) || id <= 0) return;
            var st = states[id] || { done: false, seconds: 0 };
            post('/Athlete/SaveSet', { prgID: cmbProgram.value, itemID: id, done: !!st.done, seconds: timers[id] || st.seconds || 0 }).catch(function () { });
        }
        function updateProgress() {
            var total = dayItems.length, done = 0;
            for (var i = 0; i < total; i++) { var st = states[dayItems[i].ItemID]; if (st && st.done) done++; }
            var pct = total ? Math.round(done * 100 / total) : 0;
            var pb = document.getElementById('progressBar'); if (pb) pb.style.width = pct + '%';
            var pt = document.getElementById('progressText'); if (pt) pt.textContent = fa(pct) + '٪';
            var ss = document.getElementById('stSets'); if (ss) ss.textContent = fa(done);
            var dc = document.getElementById('doneCount'); if (dc) dc.textContent = fa(done) + ' از ' + fa(total);
            // ✅ تغییر دکمه بر اساس وضعیت
            if (allDone()) { btnStart.innerHTML = '<i class="bi bi-flag-fill"></i> پایان تمرین و مشاهده خلاصه'; btnStart.classList.add('finish'); }
            else if (sessionStarted) { btnStart.innerHTML = '<i class="bi bi-stop-fill"></i> پایان تمرین'; btnStart.classList.remove('finish'); }
            else { btnStart.innerHTML = '<i class="bi bi-play-fill"></i> شروع تمرین'; btnStart.classList.remove('finish'); }
        }

        var restInt = null;
        function startRest(sec) {
            stopRest();
            var box = document.getElementById('restBox'), t = document.getElementById('restTime');
            if (!box || !t) return;
            box.classList.add('show'); var left = sec; t.textContent = mmss(left);
            restInt = setInterval(function () { left--; t.textContent = mmss(Math.max(0, left)); if (left <= 0) { stopRest(); toast('پایان استراحت — ست بعدی! ⏱', true); } }, 1000);
        }
        function stopRest() { if (restInt) { clearInterval(restInt); restInt = null; } var box = document.getElementById('restBox'); if (box) box.classList.remove('show'); }
        var restSkip = document.getElementById('restSkip'); if (restSkip) restSkip.addEventListener('click', stopRest);

        function startSession() {
            if (!pendingData) return;
            sessionStarted = true; sessionStartAt = new Date();
            if (sessionBar) sessionBar.style.display = 'flex';
            var st = document.getElementById('startTime'); if (st) st.textContent = fa(p2(sessionStartAt.getHours()) + ':' + p2(sessionStartAt.getMinutes()));
            if (sessionInt) clearInterval(sessionInt);
            sessionInt = setInterval(function () { var sec = Math.floor((Date.now() - sessionStartAt.getTime()) / 1000); var el = document.getElementById('sessionTime'); if (el) el.textContent = fa(mmss(sec)); }, 1000);
            renderDay(pendingData);
        }
        function endSession() {
            sessionStarted = false;
            if (sessionInt) { clearInterval(sessionInt); sessionInt = null; }
            stopTimer(runningId);
            if (sessionBar) sessionBar.style.display = 'none';
        }

        btnStart.addEventListener('click', function () {
            if (!cmbProgram.value) { toast('ابتدا برنامه‌ی تمرینی را انتخاب کنید', false); return; }
            if (allDone()) { endSession(); openSummary(); return; }          // ✅ همه done → خلاصه
            if (sessionStarted) { endSession(); updateProgress(); toast('تمرین پایان یافت ✔', true); return; }
            if (pendingData) { startSession(); return; }
            fetch('/Athlete/Day?prgID=' + cmbProgram.value + '&day=' + encodeURIComponent(cmbDay.value || ''), { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
                .then(function (r) { return r.json(); })
                .then(function (data) { pendingData = data; startSession(); })
                .catch(function () { toast('خطا در دریافت برنامه', false); });
        });

        function prepareDay() {
            stopTimer(runningId); sessionStarted = false;
            if (sessionInt) { clearInterval(sessionInt); sessionInt = null; }
            if (sessionBar) sessionBar.style.display = 'none';
            pendingData = null; dayItems = []; states = {}; timers = {};
            if (!cmbProgram.value) { exListBox.innerHTML = '<div class="ath-empty">هنوز برنامه‌ی تمرینی فعالی ندارید. با مربی خود هماهنگ کنید.</div>'; dayInfo.textContent = ''; updateProgress(); return; }
            dayInfo.innerHTML = 'روز انتخابی: <b style="color:#ddd6fe;">' + esc(cmbDay.value || '—') + '</b>';
            exListBox.innerHTML = '<div class="ath-empty">برای دیدن حرکات این روز، «شروع تمرین» را بزنید.</div>';
            updateProgress();
        }
        cmbProgram.onChange = function (v) { fillDays(v); prepareDay(); };
        cmbDay.onChange = function () { prepareDay(); };

        if (programs.length) {
            var wd = D.weekday || '', defPrg = null;
            programs.forEach(function (p) { if (!defPrg && wd && (p.days || []).some(function (d) { return d.indexOf(wd.substring(0, 4)) > -1; })) defPrg = p.value; });
            cmbProgram.setValue(defPrg !== null ? defPrg : programs[0].value, true);
            fillDays(cmbProgram.value);
        } else { exListBox.innerHTML = '<div class="ath-empty">هنوز برنامه‌ی تمرینی فعالی ندارید. با مربی خود هماهنگ کنید.</div>'; }
        prepareDay();
    })();

    /* ============================================================
       ۳) کمد من
       ============================================================ */
    (function () {
        var lockerNo = document.getElementById('lockerNo');
        if (lockerNo && D.myLocker) lockerNo.textContent = 'کمد ' + fa(D.myLocker.BoxNo);
        var btnOpenLocker = document.getElementById('btnOpenLocker');
        if (!btnOpenLocker) return;
        var lockerMsg = document.getElementById('lockerMsg');
        btnOpenLocker.addEventListener('click', function () {
            var btn = this; btn.disabled = true;
            if (lockerMsg) { lockerMsg.className = 'ath-locker-msg'; lockerMsg.textContent = 'در حال ارسال فرمان به کنترلر…'; }
            post('/Athlete/OpenMyLocker', {})
                .then(function (r) {
                    if (lockerMsg) { lockerMsg.className = 'ath-locker-msg ' + (r && r.Success ? 'ok' : 'err'); lockerMsg.textContent = (r && r.Message) || 'نتیجه‌ای دریافت نشد.'; }
                    toast((r && r.Message) || 'انجام شد', !!(r && r.Success));
                })
                .catch(function () { if (lockerMsg) { lockerMsg.className = 'ath-locker-msg err'; lockerMsg.textContent = 'خطا در ارتباط با سرور.'; } })
                .finally(function () { btn.disabled = false; });
        });
    })();

    /* ============================================================
       ۴) ✅ خلاصه تمرین امروز — fetch زنده + نمودار دایره‌ای
       ============================================================ */
    /* ---- ✅ خلاصه تمرین: حلقه‌ی مجموع + بندهای انیمیشن‌دار هر حرکت ---- */
    var SUM_COLORS = [
        ['#66d3e8', '#3bc9db'], ['#667eea', '#764ba2'], ['#38d9a9', '#2bbfa0'],
        ['#ffa94d', '#ff922b'], ['#ff6b81', '#fa5252'], ['#8ab6ff', '#5c7cfa'],
        ['#f783ac', '#e64980'], ['#99e9f2', '#3bc9db']
    ];

    function sumCountUp(el, target, ms) {
        if (!el) return;
        var start = null, done = false;
        function finalText() {
            return fa(target % 1 ? Number(target).toFixed(1) : Math.round(target));
        }
        function finish() {
            if (done) return;
            done = true;
            el.textContent = finalText();
        }
        function step(t) {
            if (done) return;
            if (start === null) start = t;
            var p = Math.min(1, (t - start) / ms);
            var eased = 1 - Math.pow(1 - p, 3);
            var val = target * eased;
            el.textContent = fa(target % 1 ? val.toFixed(1) : Math.round(val));
            if (p < 1) requestAnimationFrame(step);
            else finish();
        }
        requestAnimationFrame(step);
        // ⏱ نگهبان: اگر rAF اجرا نشد (تب پس‌زمینه / مرورگر کم‌مصرف) عدد نهایی ست می‌شود
        setTimeout(finish, ms + 150);
    }

    function renderSummary(data) {
        var hero = document.getElementById('sumHero'),
            list = document.getElementById('sumList'),
            empty = document.getElementById('summaryEmpty');
        if (!list) return;
        list.innerHTML = '';
        if (!data || !data.length) {
            if (hero) hero.style.display = 'none';
            if (empty) empty.style.display = 'block';
            return;
        }
        if (hero) hero.style.display = '';
        if (empty) empty.style.display = 'none';

        var total = 0;
        data.forEach(function (x) { total += Number(x.minutes) || 0; });

        // حلقه‌ی مجموع (انیمیشن کشیده‌شدن)
        var ring = document.querySelector('.sum-ring-fg');
        if (ring) {
            var C = 2 * Math.PI * 52;
            ring.style.transition = 'none';
            ring.style.strokeDasharray = C;
            ring.style.strokeDashoffset = C;
            void ring.getBoundingClientRect();
            ring.style.transition = 'stroke-dashoffset 1.2s cubic-bezier(.22,1,.36,1)';
            ring.style.strokeDashoffset = '0';
        }
        sumCountUp(document.getElementById('sumTotalNum'), Math.round(total), 1200);
        sumCountUp(document.getElementById('sumMoveCount'), data.length, 900);

        data.forEach(function (x, i) {
            var mins = Number(x.minutes) || 0;
            var pct = total > 0 ? (mins / total) * 100 : 0;
            var c = SUM_COLORS[i % SUM_COLORS.length];
            var row = document.createElement('div');
            row.className = 'sum-row';
            row.style.setProperty('--d', (0.12 + i * 0.08) + 's');
            row.style.setProperty('--c1', c[0]);
            row.style.setProperty('--c2', c[1]);
            row.innerHTML =
                '<div class="sum-row-head">' +
                '  <span class="sum-rank">' + fa(i + 1) + '</span>' +
                '  <span class="sum-name"></span>' +
                '  <span class="sum-min"><b class="sum-min-val">۰</b> دقیقه</span>' +
                '</div>' +
                '<div class="sum-bar"><i></i></div>' +
                '<div class="sum-pct">' + fa(Math.round(pct)) + '٪ از کل تمرین امروز</div>';
            row.querySelector('.sum-name').textContent = x.name || '—';
            list.appendChild(row);

            var bar = row.querySelector('.sum-bar i');
            var target = Math.max(5, pct);
            setTimeout(function () { bar.style.width = target + '%'; }, 80);
            sumCountUp(row.querySelector('.sum-min-val'), mins, 1000);
        });
    }
    function openSummary() {
        var modalEl = document.getElementById('summaryModal');
        if (!modalEl) return;
        fetch('/Athlete/Summary', { headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                renderSummary((res && res.summary) || []);
                bootstrap.Modal.getOrCreateInstance(modalEl).show();
            })
            .catch(function () { toast('خطا در دریافت خلاصه تمرین', false); });
    }
    // ✅ دکمه‌ی حالت خروج
    var btnShow = document.getElementById('btnShowSummary');
    if (btnShow) btnShow.addEventListener('click', openSummary);

    /* ---- اعداد فارسی آمار ---- */
    ['stWeek', 'stMinutes', 'stStreak', 'stSets'].forEach(function (id) {
        var el = document.getElementById(id); if (el) el.textContent = fa(el.textContent);
    });
})();