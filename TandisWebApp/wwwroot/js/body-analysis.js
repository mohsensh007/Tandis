/* ============================================================
   body-analysis.js — صفحه «تحلیل بدن» پنل عضو
   ✅ رندر نتایج محاسبات از سرور
   ✅ نمودار خطی SVG (بدون وابستگی خارجی)
   ✅ جدول تاریخچه + ثبت/ویرایش/حذف اندازه‌گیری
   ============================================================ */
(function () {
    'use strict';

    var D = {};
    try { D = JSON.parse(document.getElementById('ba-data').textContent) || {}; } catch (e) { D = {}; }

    /* ---------- کمکی‌ها ---------- */
    function fa(n) { return String(n).replace(/[0-9]/g, function (d) { return '۰۱۲۳۴۵۶۷۸۹'[d]; }); }
    function num(v, digits) {
        if (v === null || v === undefined || v === '') return '—';
        var x = Number(v);
        if (isNaN(x)) return '—';
        return x.toFixed(digits === undefined ? 0 : digits).replace(/\.0+$/, '');
    }
    function esc(s) {
        return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }
    function shortDate(shamsi) {
        // "1404/07/22" → "07/22"
        var p = String(shamsi || '').split('/');
        return p.length === 3 ? p[1] + '/' + p[2] : (shamsi || '');
    }
    function toNum(v) {
        if (v === null || v === undefined || v === '') return null;
        var x = Number(v);
        return isNaN(x) ? null : x;
    }

    /* ============================================================
       رندر چیپ‌های هویتی
       ============================================================ */
    function renderIdentity() {
        var el;
        if ((el = document.getElementById('baAge'))) el.textContent = D.age ? fa(D.age) + ' سال' : '—';
        if ((el = document.getElementById('baGender'))) el.textContent = D.gender === true ? 'مرد' : (D.gender === false ? 'زن' : '—');
    }

    /* ============================================================
       رندر نتایج محاسبات
       ============================================================ */
    function setMetric(id, value, unit, badge, tone) {
        var box = document.getElementById(id);
        if (!box) return;
        box.classList.remove('ba-ok', 'ba-warn', 'ba-danger');
        if (tone) box.classList.add('ba-' + tone);
        var vEl = box.querySelector('.ba-m-value');
        var uEl = box.querySelector('.ba-m-unit');
        var bEl = box.querySelector('.ba-m-badge');
        if (vEl) vEl.textContent = value;
        if (uEl && unit !== undefined && unit !== null) uEl.textContent = unit;
        if (bEl) {
            if (badge) { bEl.textContent = badge; bEl.style.display = ''; }
            else bEl.style.display = 'none';
        }
    }

    function renderMetrics() {
        var m = (D && D.metrics) || {};
        var wrap = document.getElementById('baMetrics');
        var empty = document.getElementById('baNoData');
        var note = document.getElementById('baAdviceNote');
        if (!wrap) return;

        if (m.bmi === null || m.bmi === undefined) {
            wrap.style.display = 'none';
            if (empty) empty.style.display = '';
            if (note) note.style.display = 'none';
            return;
        }
        wrap.style.display = '';
        if (empty) empty.style.display = 'none';

        // BMI
        var bmiTone = 'ok', bmiBadge = m.bmiCategory || '—';
        if (m.bmi < 18.5) bmiTone = 'warn';
        else if (m.bmi < 25) bmiTone = 'ok';
        else if (m.bmi < 30) bmiTone = 'warn';
        else bmiTone = 'danger';
        setMetric('mBmi', num(m.bmi, 1), 'کیلوگرم بر مترمربع', bmiBadge, bmiTone);

        // BMR / TDEE
        setMetric('mBmr', fa(num(m.bmr)), null, null, null);
        setMetric('mTdee', fa(num(m.tdee)), 'کالری / روز', m.activityFactor ? 'ضریب ' + num(m.activityFactor, 3) : null, null);

        // Protein
        setMetric('mProtein', fa(num(m.proteinG)), 'گرم / روز', m.goalLabel ? m.goalLabel : null, 'ok');

        // Goal calories
        setMetric('mGoalKcal', fa(num(m.goalCalories)), 'کالری / روز', m.goalLabel || null, 'highlight');
        var gk = document.getElementById('mGoalKcal');
        if (gk) { gk.classList.add('ba-highlight'); }

        setMetric('mCut', fa(num(m.cutCalories)), 'کالری / روز', null, null);
        setMetric('mBulk', fa(num(m.bulkCalories)), 'کالری / روز', null, null);

        // Target weight
        setMetric('mTarget', num(m.targetWeight, 1), 'کیلوگرم', m.targetWeight ? 'بر اساس هدف شما' : null, null);

        // Trend
        var trendTone = null;
        if (m.weightDelta !== null && m.weightDelta !== undefined) {
            if (m.weightDelta < -0.1) trendTone = 'ok';
            else if (m.weightDelta > 0.1) trendTone = 'warn';
        }
        setMetric('mTrend',
            (m.weightDelta === null || m.weightDelta === undefined) ? '—' : (m.weightDelta > 0 ? '+' : '') + num(m.weightDelta, 1),
            'کیلوگرم', m.trendLabel || null, trendTone);

        // Waist-to-height
        var whTone = 'ok';
        if (m.waistToHeight !== null && m.waistToHeight !== undefined) {
            if (m.waistToHeight >= 0.65) whTone = 'danger';
            else if (m.waistToHeight >= 0.5) whTone = 'warn';
        }
        setMetric('mWhtr',
            m.waistToHeight === null || m.waistToHeight === undefined ? '—' : num(m.waistToHeight, 2),
            'کمر / قد', m.waistToHeightCategory || 'با ثبت دور کمر', whTone);

        // Body fat (estimate)
        setMetric('mBodyFat', m.bodyFatPct === null || m.bodyFatPct === undefined ? '—' : num(m.bodyFatPct, 1),
            'درصد (تخمینی)', 'Deurenberg', null);

        // Healthy range
        var idealTxt = (m.idealWeightMin && m.idealWeightMax)
            ? num(m.idealWeightMin, 1) + ' – ' + num(m.idealWeightMax, 1)
            : '—';
        setMetric('mIdeal', idealTxt, 'کیلوگرم', m.devineWeight ? 'دوین: ' + num(m.devineWeight, 1) : null, 'ok');

        // نکته راهنما
        var adviceParts = [];
        if (!m.hasEnoughData) adviceParts.push('برای محاسبه BMR، کالری و پروتئین، تاریخ تولد و جنسیت لازم است — از بخش پروفایل کامل کنید.');
        if (m.bmiAdvice) adviceParts.push(m.bmiAdvice);
        if (m.proteinAdvice) adviceParts.push('پروتئین: ' + m.proteinAdvice);
        if (m.activityLabel) adviceParts.push('فعالیت روزانه: ' + m.activityLabel);
        if (m.firstWeight && m.lastWeight && m.recordCount >= 2) {
            adviceParts.push('از اولین ثبت تاکنون ' + Math.abs(Number(m.weightDelta)).toFixed(1) + ' کیلوگرم ' +
                (Number(m.weightDelta) < 0 ? 'کاهش' : (Number(m.weightDelta) > 0 ? 'افزایش' : 'تغییر')) + ' داشته‌اید.');
        }
        if (note) {
            if (adviceParts.length) {
                document.getElementById('baAdviceText').textContent = adviceParts.join('  •  ');
                note.style.display = '';
            } else note.style.display = 'none';
        }

        var hint = document.getElementById('baUpdatedHint');
        if (hint) hint.textContent = D.records && D.records.length ? 'آخرین اندازه‌گیری: ' + fa((D.latest && D.latest.recordDate) || '') : '';
    }

    /* ============================================================
       نمودار خطی SVG
       ============================================================ */
    var chartKey = 'weightKg';
    var chartLabel = 'وزن';
    var chartUnit = 'کیلوگرم';

    function renderChart() {
        var box = document.getElementById('baChartBox');
        if (!box) return;
        var records = (D.records || []).filter(function (r) {
            return r[chartKey] !== null && r[chartKey] !== undefined && r[chartKey] !== '';
        });

        var legend = document.getElementById('baLegend');
        var stat = document.getElementById('baChartStat');

        if (!records.length) {
            box.innerHTML = '<div class="ba-chart-empty"><i class="bi bi-bar-chart-line"></i>برای «' + esc(chartLabel) +
                '» داده‌ای ثبت نشده است. با ثبت اندازه‌گیری، نمودار اینجا رسم می‌شود.</div>';
            if (legend) legend.style.display = 'none';
            return;
        }

        var vals = records.map(function (r) { return Number(r[chartKey]); });
        var min = Math.min.apply(null, vals);
        var max = Math.max.apply(null, vals);
        var avg = vals.reduce(function (a, b) { return a + b; }, 0) / vals.length;
        if (min === max) { min -= 1; max += 1; }

        var W = 700, H = 300;
        var padR = 18, padL = 44, padT = 26, padB = 34;
        var iw = W - padL - padR, ih = H - padT - padB;

        function xAt(i) {
            if (records.length === 1) return padL + iw / 2;
            return padL + (i / (records.length - 1)) * iw;
        }
        function yAt(v) { return padT + ih - ((v - min) / (max - min)) * ih; }

        var svg = [];
        svg.push('<svg viewBox="0 0 ' + W + ' ' + H + '" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="نمودار ' + esc(chartLabel) + '">');

        // پس‌زمینه گرادیان مسیر
        svg.push('<defs>' +
            '<linearGradient id="baLineGrad" x1="0" y1="0" x2="1" y2="0">' +
            '<stop offset="0%" stop-color="#66d3e8"/><stop offset="100%" stop-color="#8b5cf6"/>' +
            '</linearGradient>' +
            '<linearGradient id="baAreaGrad" x1="0" y1="0" x2="0" y2="1">' +
            '<stop offset="0%" stop-color="#66d3e8" stop-opacity="0.34"/>' +
            '<stop offset="100%" stop-color="#66d3e8" stop-opacity="0"/>' +
            '</linearGradient>' +
            '</defs>');

        // خطوط شبکه افقی + برچسب محور Y
        var ticks = 4;
        for (var t = 0; t <= ticks; t++) {
            var vv = min + ((max - min) * t / ticks);
            var yy = yAt(vv);
            svg.push('<line x1="' + padL + '" y1="' + yy + '" x2="' + (W - padR) + '" y2="' + yy +
                '" stroke="rgba(255,255,255,.08)" stroke-width="1"/>');
            svg.push('<text x="' + (padL - 7) + '" y="' + (yy + 4) + '" text-anchor="end" fill="#8a93a6" font-size="11" font-family="Vazirmatn,Tahoma">' +
                num(vv, 1) + '</text>');
        }

        // خط میانگین (بنفش‌کم‌رنگ)
        var avgY = yAt(avg);
        svg.push('<line x1="' + padL + '" y1="' + avgY + '" x2="' + (W - padR) + '" y2="' + avgY +
            '" stroke="#8b5cf6" stroke-width="1.4" stroke-dasharray="6 5" opacity="0.75"/>');

        // مسیر خط + ناحیه
        var pts = records.map(function (r, i) { return { x: xAt(i), y: yAt(Number(r[chartKey])), d: r.recordDate, v: Number(r[chartKey]) }; });
        var linePath = pts.map(function (p, i) { return (i === 0 ? 'M' : 'L') + p.x.toFixed(1) + ' ' + p.y.toFixed(1); }).join(' ');
        var areaPath = linePath + ' L' + pts[pts.length - 1].x.toFixed(1) + ' ' + (padT + ih) + ' L' + pts[0].x.toFixed(1) + ' ' + (padT + ih) + ' Z';
        svg.push('<path d="' + areaPath + '" fill="url(#baAreaGrad)"/>');
        svg.push('<path d="' + linePath + '" fill="none" stroke="url(#baLineGrad)" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>');

        // نقاط + برچسب تاریخ
        pts.forEach(function (p, i) {
            svg.push('<circle cx="' + p.x.toFixed(1) + '" cy="' + p.y.toFixed(1) + '" r="5" fill="#101426" stroke="#66d3e8" stroke-width="2.5"/>');
            var showLbl = pts.length <= 8 || i === 0 || i === pts.length - 1 || i === Math.floor(pts.length / 2);
            if (showLbl) {
                svg.push('<text x="' + p.x.toFixed(1) + '" y="' + (p.y - 11).toFixed(1) + '" text-anchor="middle" fill="#dce9ff" font-size="11" font-weight="700" font-family="Vazirmatn,Tahoma">' +
                    num(p.v, 1) + '</text>');
            }
            if (pts.length <= 10 || i % Math.ceil(pts.length / 8) === 0 || i === pts.length - 1) {
                svg.push('<text x="' + p.x.toFixed(1) + '" y="' + (H - 10) + '" text-anchor="middle" fill="#8a93a6" font-size="10.5" font-family="Vazirmatn,Tahoma" direction="ltr">' +
                    shortDate(p.d) + '</text>');
            }
        });

        svg.push('</svg>');
        box.innerHTML = svg.join('');

        if (legend) {
            legend.style.display = '';
            if (stat) {
                var diff = vals[vals.length - 1] - vals[0];
                stat.innerHTML = 'تعداد: ' + fa(vals.length) + ' • میانگین: ' + fa(num(avg, 1)) + ' ' + esc(chartUnit) +
                    (vals.length > 1 ? ' • تغییر: <b style="color:' + (diff < 0 ? '#86efac' : (diff > 0 ? '#fcd34d' : '#b9c2d4')) + ';">' +
                        (diff > 0 ? '+' : '') + fa(num(diff, 1)) + '</b>' : '');
            }
        }
    }

    // انتخاب سری نمودار
    (function () {
        var bar = document.getElementById('baChartToolbar');
        if (!bar) return;
        bar.addEventListener('click', function (e) {
            var chip = e.target.closest('.ba-chip');
            if (!chip) return;
            bar.querySelectorAll('.ba-chip').forEach(function (c) {
                c.classList.remove('active');
                c.setAttribute('aria-pressed', 'false');
            });
            chip.classList.add('active');
            chip.setAttribute('aria-pressed', 'true');
            chartKey = chip.getAttribute('data-key');
            chartLabel = chip.getAttribute('data-label');
            chartUnit = chip.getAttribute('data-unit');
            renderChart();
        });
    })();

    /* ============================================================
       جدول تاریخچه
       ============================================================ */
    function renderTable() {
        var body = document.getElementById('baTableBody');
        var wrap = document.getElementById('baTableWrap');
        var empty = document.getElementById('baTableEmpty');
        var count = document.getElementById('baRecCount');
        if (!body) return;

        var records = (D.records || []).slice().reverse(); // جدیدترین اول
        if (count) count.textContent = records.length ? fa(records.length) + ' رکورد' : '';

        if (!records.length) {
            body.innerHTML = '';
            if (wrap) wrap.style.display = 'none';
            if (empty) empty.style.display = '';
            return;
        }
        if (wrap) wrap.style.display = '';
        if (empty) empty.style.display = 'none';

        var html = '';
        records.forEach(function (r) {
            html += '<tr>' +
                '<td data-label="تاریخ">' + esc(r.recordDate) + '</td>' +
                '<td data-label="وزن">' + num(r.weightKg, 1) + '</td>' +
                '<td data-label="کمر">' + num(r.waistCm, 1) + '</td>' +
                '<td data-label="شکم">' + num(r.abdomenCm, 1) + '</td>' +
                '<td data-label="سینه">' + num(r.chestCm, 1) + '</td>' +
                '<td data-label="بازو">' + num(r.armCm, 1) + '</td>' +
                '<td data-label="ران">' + num(r.thighCm, 1) + '</td>' +
                '<td data-label="باسن">' + num(r.hipCm, 1) + '</td>' +
                '<td data-label="عملیات"><button type="button" class="ba-row-del" data-id="' + r.recordId + '">حذف اندازه‌گیری</button></td>' +
                '</tr>';
        });
        body.innerHTML = html;
    }

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('.ba-row-del');
        if (!btn) return;
        var id = Number(btn.getAttribute('data-id'));
        if (!id) return;
        if (!confirm('این اندازه‌گیری حذف شود؟')) return;
        apiCall('/api/BodyAnalysis/DeleteRecord', 'POST', { recordId: id }, function (res) {
            if (res && res.success) {
                showToast(res.message || 'حذف شد', 'success');
                applyPage(res.data);
            } else showToast((res && res.message) || 'خطا در حذف', 'error');
        });
    });

    /* ============================================================
       اعمال داده صفحه (بعد از هر عملیات موفق)
       ============================================================ */
    function applyPage(page) {
        if (!page) return;
        D = page;
        renderIdentity();
        renderMetrics();
        renderChart();
        renderTable();
    }

    /* ============================================================
       ذخیره پروفایل
       ============================================================ */
    (function () {
        var btn = document.getElementById('btnSaveProfile');
        if (!btn) return;
        btn.addEventListener('click', function () {
            var height = toNum(document.getElementById('baHeight').value);
            if (height !== null && (height < 50 || height > 260)) {
                showToast('قد باید بین ۵۰ تا ۲۶۰ سانتی‌متر باشد', 'error'); return;
            }
            var payload = {
                heightCm: height,
                activityLevel: Number(document.getElementById('baActivity').value) || 2,
                sportHistory: Number(document.getElementById('baHistory').value) || 0,
                goal: Number(document.getElementById('baGoal').value) || 3,
                sessionsPerWeek: Number(document.getElementById('baSessions').value) || 0
            };
            btn.disabled = true;
            apiCall('/api/BodyAnalysis/SaveProfile', 'POST', payload, function (res) {
                btn.disabled = false;
                if (res && res.success) {
                    showToast(res.message || 'ذخیره شد', 'success');
                    applyPage(res.data);
                } else showToast((res && res.message) || 'خطا در ذخیره', 'error');
            }, function () { btn.disabled = false; });
        });
    })();

    /* ============================================================
       ثبت اندازه‌گیری
       ============================================================ */
    (function () {
        var btn = document.getElementById('btnSaveRecord');
        if (!btn) return;
        btn.addEventListener('click', function () {
            var date = (document.getElementById('baDate').value || '').trim();
            var weight = toNum(document.getElementById('baWeight').value);
            if (weight === null) {
                showToast('وزن را وارد کنید', 'error'); return;
            }
            var payload = {
                recordDate: date || null,
                weightKg: weight,
                waistCm: toNum(document.getElementById('baWaist').value),
                abdomenCm: toNum(document.getElementById('baAbdomen').value),
                chestCm: toNum(document.getElementById('baChest').value),
                armCm: toNum(document.getElementById('baArm').value),
                thighCm: toNum(document.getElementById('baThigh').value),
                hipCm: toNum(document.getElementById('baHip').value)
            };
            btn.disabled = true;
            apiCall('/api/BodyAnalysis/SaveRecord', 'POST', payload, function (res) {
                btn.disabled = false;
                if (res && res.success) {
                    showToast(res.message || 'ثبت شد', 'success');
                    applyPage(res.data);
                } else showToast((res && res.message) || 'خطا در ثبت', 'error');
            }, function () { btn.disabled = false; });
        });
    })();

    /* ---------- رندر اولیه ---------- */
    renderIdentity();
    renderMetrics();
    renderChart();
    renderTable();
})();
