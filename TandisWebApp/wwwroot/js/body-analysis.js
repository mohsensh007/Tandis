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
        if (!isFinite(x)) return '—';
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

    function latestRecord() {
        if (D && D.latest) return D.latest;
        var records = (D && D.records) || [];
        return records.length ? records[records.length - 1] : null;
    }

    /* ============================================================
       نمایش آخرین اندازه‌گیری کنار نتایج
       ============================================================ */
    function renderLatestMeasurement() {
        var latest = latestRecord();
        var empty = document.getElementById('baLatestEmpty');
        var grid = document.getElementById('baLatestGrid');
        var date = document.getElementById('baLatestDate');
        if (!grid) return;

        if (!latest) {
            grid.style.display = 'none';
            if (empty) empty.style.display = '';
            if (date) date.textContent = 'بدون رکورد';
            return;
        }

        grid.style.display = '';
        if (empty) empty.style.display = 'none';
        if (date) date.textContent = latest.recordDate ? 'تاریخ اندازه‌گیری: ' + fa(latest.recordDate) : 'تاریخ نامشخص';

        var values = {
            baLatestWeight: latest.weightKg,
            baLatestWaist: latest.waistCm,
            baLatestAbdomen: latest.abdomenCm,
            baLatestChest: latest.chestCm,
            baLatestArm: latest.armCm,
            baLatestThigh: latest.thighCm,
            baLatestHip: latest.hipCm
        };

        Object.keys(values).forEach(function (id) {
            var el = document.getElementById(id);
            if (el) el.textContent = fa(num(values[id], 1));
        });
    }

    /* توضیح‌ها عمداً برای هر کارت جدا نوشته شده‌اند تا راهنمایی عمومی و مبهم نباشند. */
    var METRIC_HELP = {
        bmi: {
            title: 'شاخص توده بدنی (BMI)', icon: 'bi-body-text',
            intro: 'BMI نسبت وزن به مجذور قد است و یک تصویر سریع از محدوده وزن نسبت به قد می‌دهد.',
            points: ['کمتر از ۱۸٫۵ معمولاً کمبود وزن، ۱۸٫۵ تا ۲۴٫۹ محدوده سالم و ۲۵ به بالا محدوده اضافه‌وزن در نظر گرفته می‌شود.', 'این شاخص حجم عضله، فرم بدن و محل تجمع چربی را جدا نمی‌کند؛ ورزشکاران عضلانی ممکن است BMI بالاتری داشته باشند.', 'عدد این کارت با وزن آخرین اندازه‌گیری و قد ثبت‌شده در اطلاعات پایه محاسبه شده است.']
        },
        bmr: {
            title: 'سوخت‌وساز پایه (BMR)', icon: 'bi-fire',
            intro: 'BMR مقدار تقریبی انرژی‌ای است که بدن در حالت استراحت برای عملکردهای حیاتی مثل تنفس و گردش خون مصرف می‌کند.',
            points: ['این عدد با فرمول Mifflin–St Jeor و بر حسب کیلوکالری در روز برآورد می‌شود؛ کالری کل روز یا هدف رژیم نیست.', 'فرمول محاسبه از وزن آخرین اندازه‌گیری، قد، سن و جنسیت استفاده می‌کند.', 'اثر فعالیت، تمرین و مصرف انرژی روزمره را در کارت TDEE ببینید.']
        },
        tdee: {
            title: 'کالری روزانه (TDEE)', icon: 'bi-lightning-charge-fill',
            intro: 'TDEE برآورد کالری مورد نیاز بدن در یک روز معمولی با توجه به سطح فعالیت شماست.',
            points: ['TDEE از BMR ضربدر ضریب فعالیت انتخاب‌شده در اطلاعات پایه به دست می‌آید.', 'این مقدار نقطه شروع است؛ شدت واقعی تمرین، شغل، خواب و تغییرات وزن می‌توانند نیاز شما را تغییر دهند.', 'ضریب فعالیتی که در محاسبه استفاده شده در برچسب همین کارت نمایش داده می‌شود.']
        },
        protein: {
            title: 'پروتئین روزانه', icon: 'bi-egg-fill',
            intro: 'این عدد مقدار تقریبی پروتئین روزانه برای حمایت از حفظ یا ساخت عضله است.',
            points: ['مقدار بر اساس وزن آخرین اندازه‌گیری و هدف ورزشی انتخاب‌شده محاسبه می‌شود.', 'برای چربی‌سوزی و عضله‌سازی مقدار بالاتری در نظر گرفته شده تا حفظ توده عضلانی بهتر پشتیبانی شود.', 'بیماری‌های کلیوی، بارداری یا شرایط پزشکی نیازمند نظر پزشک یا متخصص تغذیه هستند.']
        },
        goalCalories: {
            title: 'کالری هدف', icon: 'bi-bullseye',
            intro: 'کالری هدف پیشنهادی بر اساس هدف ورزشی فعلی شما انتخاب می‌شود.',
            points: ['برای چربی‌سوزی مقدار کارت کالری چربی‌سوزی و برای عضله‌سازی مقدار کارت کالری عضله‌سازی انتخاب می‌شود.', 'اگر هدف روی حفظ تناسب باشد، کالری هدف برابر با TDEE نمایش داده می‌شود.', 'هدف ورزشی را از کارت اطلاعات پایه تغییر دهید و ذخیره کنید تا نتیجه جدید محاسبه شود.']
        },
        cutCalories: {
            title: 'کالری چربی‌سوزی', icon: 'bi-arrow-down-right-circle',
            intro: 'این مقدار برآورد کالری روزانه برای ایجاد کسری ملایم و کمک به کاهش چربی است.',
            points: ['مقدار اولیه برابر TDEE منهای ۵۰۰ کیلوکالری است.', 'کف محاسبه، بزرگ‌ترِ BMR و ۱۵۰۰ برای مردان یا ۱۲۰۰ برای زنان است. بنابراین در بعضی شرایط، خروجی حتی می‌تواند کمتر از TDEE نباشد؛ آن را نسخه رژیم تلقی نکنید.', 'کاهش وزن پایدار معمولاً با پیگیری روند چند هفته‌ای و تنظیم برنامه توسط متخصص بهتر ارزیابی می‌شود.']
        },
        bulkCalories: {
            title: 'کالری عضله‌سازی', icon: 'bi-arrow-up-right-circle',
            intro: 'این عدد برآورد کالری روزانه با مازاد ملایم برای پشتیبانی از عضله‌سازی است.',
            points: ['مقدار پیشنهادی حدود ۱۰ درصد بیشتر از TDEE است.', 'افزایش کالری به‌تنهایی عضله نمی‌سازد؛ تمرین مقاومتی، پروتئین کافی و خواب نیز ضروری هستند.', 'روند وزن و اندازه‌ها را هفتگی بررسی کنید تا مقدار مازاد در صورت نیاز تنظیم شود.']
        },
        targetWeight: {
            title: 'وزن هدف پیشنهادی', icon: 'bi-flag',
            intro: 'وزن هدف یک پیشنهاد محاسباتی بر پایه محدوده سالم BMI و هدف انتخاب‌شده است.',
            points: ['اگر وزن از محدوده سالم فاصله داشته باشد، نزدیک‌ترین نقطه مناسب در محدوده سالم پیشنهاد می‌شود.', 'اگر در محدوده سالم باشید، هدف حفظ وزن یا حرکت به سمت میانه محدوده با توجه به هدف شماست.', 'این عدد هدف قطعی پزشکی نیست و باید با فرم بدن، عضله و نظر مربی تفسیر شود.']
        },
        trend: {
            title: 'تغییرات وزن', icon: 'bi-graph-up',
            intro: 'این کارت اختلاف وزن آخرین اندازه‌گیری و اولین اندازه‌گیری ثبت‌شده را نشان می‌دهد.',
            points: ['عدد مثبت یعنی وزن نسبت به اولین ثبت افزایش داشته و عدد منفی یعنی کاهش داشته است.', 'برای نمایش روند واقعی حداقل دو اندازه‌گیری لازم است.', 'مقایسه را با فاصله‌های زمانی مشابه و شرایط اندازه‌گیری نزدیک انجام دهید.']
        },
        waistToHeight: {
            title: 'نسبت دور کمر به قد', icon: 'bi-arrows-angle-expand',
            intro: 'این نسبت از تقسیم دور کمر بر قد به دست می‌آید و شاخصی ساده برای بررسی چربی مرکزی بدن است.',
            points: ['مقدار کمتر از ۰٫۵ معمولاً وضعیت مناسب‌تری را نشان می‌دهد.', 'مقادیر بالاتر می‌توانند نشان‌دهنده ریسک بیشتر چربی شکمی باشند و در کارت با هشدار نمایش داده می‌شوند.', 'دقت نتیجه به محل ثابت اندازه‌گیری دور کمر و ثبت درست قد وابسته است.']
        },
        bodyFat: {
            title: 'درصد چربی بدن', icon: 'bi-droplet-half',
            intro: 'این درصد یک تخمین ریاضی از چربی بدن است که از BMI، سن و جنسیت به دست می‌آید.',
            points: ['روش Deurenberg تخمینی است و با سنجش تخصصی مانند DEXA یا آنالیزور معتبر یکسان نیست.', 'تغییرات این عدد را در طول زمان و همراه با وزن و دور کمر بررسی کنید.', 'اگر سن یا جنسیت کامل نباشد، این شاخص محاسبه نمی‌شود.']
        },
        idealWeight: {
            title: 'بازه وزن سالم', icon: 'bi-heart-pulse',
            intro: 'این بازه محدوده وزنی حاصل از BMI حدود ۱۸٫۵ تا ۲۴٫۹ برای قد ثبت‌شده شماست.',
            points: ['بازه برای قد شما محاسبه می‌شود و برای همه افراد یکسان نیست.', 'برچسب دوین در صورت وجود جنسیت، یک تخمین تک‌مقداری دیگر برای وزن ایده‌آل است.', 'بازه سالم به‌تنهایی کیفیت ترکیب بدن یا مقدار عضله را مشخص نمی‌کند.']
        }
    };

    /* A real modal: background is inert, focus stays inside and returns to its trigger. */
    var helpModal = document.getElementById('baMetricHelpModal');
    var helpTrigger = null;
    var helpBackground = [];

    function closeMetricHelp() {
        if (!helpModal || helpModal.hidden) return;
        helpModal.hidden = true;
        helpModal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('ba-help-open');
        helpBackground.forEach(function (entry) {
            entry.element.inert = entry.inert;
        });
        helpBackground = [];
        if (helpTrigger && document.body.contains(helpTrigger)) helpTrigger.focus();
    }

    function openMetricHelp(trigger) {
        var card = trigger.closest('[data-help-key]');
        var help = card && METRIC_HELP[card.getAttribute('data-help-key')];
        if (!helpModal || !help) return;
        if (helpModal.parentNode !== document.body) document.body.appendChild(helpModal);
        document.getElementById('baHelpTitle').textContent = help.title;
        document.getElementById('baHelpIntro').textContent = help.intro;
        document.getElementById('baHelpIcon').innerHTML = '<i class="bi ' + help.icon + '" aria-hidden="true"></i>';
        document.getElementById('baHelpPoints').innerHTML = help.points.map(function (point) {
            return '<li>' + esc(point) + '</li>';
        }).join('');
        var value = card.querySelector('.ba-m-value').textContent;
        var unit = card.querySelector('.ba-m-unit').textContent;
        document.getElementById('baHelpValue').textContent = value === '—'
            ? 'برای این شاخص اطلاعات کافی ثبت نشده است.'
            : 'نتیجه فعلی شما: ' + value + ' ' + unit;
        helpTrigger = trigger;
        helpBackground = [];
        Array.prototype.forEach.call(document.body.children, function (element) {
            if (element === helpModal || element.tagName === 'SCRIPT' || element.tagName === 'STYLE') return;
            helpBackground.push({ element: element, inert: element.inert });
            element.inert = true;
        });
        helpModal.hidden = false;
        helpModal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('ba-help-open');
        helpModal.querySelector('.ba-help-close').focus();
    }

    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('.ba-help-btn');
        if (trigger) { openMetricHelp(trigger); return; }
        if (helpModal && !helpModal.hidden && event.target.closest('[data-ba-help-close]')) closeMetricHelp();
    });

    document.addEventListener('keydown', function (event) {
        if (!helpModal || helpModal.hidden) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            closeMetricHelp();
        } else if (event.key === 'Tab') {
            var controls = helpModal.querySelectorAll('button:not([disabled]), a[href], [tabindex="0"]');
            var first = controls[0];
            var last = controls[controls.length - 1];
            if (!helpModal.contains(document.activeElement) ||
                (event.shiftKey && document.activeElement === first) ||
                (!event.shiftKey && document.activeElement === last)) {
                event.preventDefault();
                (event.shiftKey ? last : first).focus();
            }
        }
    });

    document.addEventListener('focusin', function (event) {
        if (helpModal && !helpModal.hidden && !helpModal.contains(event.target)) {
            helpModal.querySelector('.ba-help-close').focus();
        }
    });

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

        var latest = latestRecord();
        var hint = document.getElementById('baUpdatedHint');
        if (hint) hint.textContent = latest ? 'بر اساس آخرین ورودی: ' + fa(latest.recordDate || '') : 'هنوز ورودی ثبت نشده';
        if (!latest) {
            wrap.style.display = 'none';
            if (empty) empty.style.display = '';
            if (note) note.style.display = 'none';
            return;
        }
        wrap.style.display = '';
        if (empty) {
            empty.style.display = m.bmi == null ? '' : 'none';
            empty.textContent = !D.profile || !D.profile.heightCm
                ? 'آخرین اندازه‌گیری شما محفوظ است. برای محاسبه شاخص‌های وابسته به قد، اطلاعات پایه را تکمیل و ذخیره کنید.'
                : 'وزن در آخرین ورودی ثبت نشده است. برای محاسبه شاخص‌های وابسته به وزن، یک اندازه‌گیری کامل ثبت کنید.';
        }

        // BMI
        var bmiTone = 'ok', bmiBadge = m.bmiCategory || '—';
        if (m.bmi == null) bmiTone = null;
        else if (m.bmi < 18.5) bmiTone = 'warn';
        else if (m.bmi < 25) bmiTone = 'ok';
        else if (m.bmi < 30) bmiTone = 'warn';
        else bmiTone = 'danger';
        setMetric('mBmi', fa(num(m.bmi, 1)), 'کیلوگرم بر مترمربع', bmiBadge, bmiTone);

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
        if (!m.hasEnoughData) adviceParts.push('برای شاخص‌های انرژی و تخمین چربی، قد، وزن، تاریخ تولد و جنسیت باید کامل باشند. شاخص‌های قابل محاسبه همچنان نمایش داده می‌شوند.');
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

    }

    /* ============================================================
       نمودار خطی SVG
       ============================================================ */
    var chartKey = 'weightKg';
    var chartLabel = 'وزن';
    var chartUnit = 'کیلوگرم';
    var chartObserver = null;
    var chartWidth = 0;

    function renderChart() {
        var box = document.getElementById('baChartBox');
        if (!box) return;
        if (chartObserver) chartObserver.disconnect();
        var records = (D.records || []).filter(function (r) {
            return r[chartKey] != null && r[chartKey] !== '' && isFinite(Number(r[chartKey]));
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
        var margin = Math.max(1, (max - min) * .18);
        min -= margin;
        max += margin;

        // Match the real container width, keeping labels readable instead of scaling a 700px SVG down.
        chartWidth = box.clientWidth || 360;
        var W = Math.max(250, Math.min(1000, chartWidth - 12));
        var H = W < 480 ? 250 : 300;
        var padR = 34, padL = 46, padT = 30, padB = 36;
        var iw = W - padL - padR, ih = H - padT - padB;

        function xAt(i) {
            if (records.length === 1) return padL + iw / 2;
            return padL + (i / (records.length - 1)) * iw;
        }
        function yAt(v) { return padT + ih - ((v - min) / (max - min)) * ih; }

        var svg = [];
        var description = fa(records.length) + ' ثبت؛ آخرین مقدار: ' + fa(num(vals[vals.length - 1], 1)) + ' ' + chartUnit;
        svg.push('<svg class="ba-chart-svg" viewBox="0 0 ' + W + ' ' + H + '" xmlns="http://www.w3.org/2000/svg" role="img" aria-labelledby="baChartTitle baChartDesc">' +
            '<title id="baChartTitle">نمودار ' + esc(chartLabel) + '</title><desc id="baChartDesc">' + esc(description) + '. جزئیات هر ثبت در تاریخچه اندازه‌گیری‌ها در دسترس است.</desc>');

        // پس‌زمینه گرادیان مسیر
        svg.push('<defs>' +
            '<linearGradient id="baLineGrad" x1="0%" y1="0%" x2="100%" y2="0%">' +
            '<stop offset="0%" stop-color="#66d3e8"/><stop offset="100%" stop-color="#a78bfa"/>' +
            '</linearGradient>' +
            '<linearGradient id="baAreaGrad" x1="0%" y1="0%" x2="0%" y2="100%">' +
            '<stop offset="0%" stop-color="#66d3e8" stop-opacity="0.34"/>' +
            '<stop offset="100%" stop-color="#66d3e8" stop-opacity="0"/>' +
            '</linearGradient>' +
            '</defs>');

        // خطوط شبکه افقی + برچسب محور Y
        var ticks = 4;
        for (var t = 0; t <= ticks; t++) {
            var vv = min + ((max - min) * t / ticks);
            var yy = yAt(vv);
            svg.push('<line class="ba-grid-line" x1="' + padL + '" y1="' + yy + '" x2="' + (W - padR) + '" y2="' + yy +
                '" stroke="rgba(255,255,255,.08)" stroke-width="1"/>');
            svg.push('<text x="' + (padL - 9) + '" y="' + (yy + 4) + '" text-anchor="end" direction="ltr" fill="#b9c2d4" font-size="12" font-family="Sahel,Tahoma">' +
                fa(num(vv, 1)) + '</text>');
        }

        // خط میانگین (بنفش‌کم‌رنگ)
        var avgY = yAt(avg);
        svg.push('<line class="ba-average-line" x1="' + padL + '" y1="' + avgY + '" x2="' + (W - padR) + '" y2="' + avgY +
            '" stroke="#a78bfa" stroke-width="1.4" stroke-dasharray="5 6" opacity="0.75"/>');

        // مسیر خط + ناحیه
        var pts = records.map(function (r, i) { return { x: xAt(i), y: yAt(Number(r[chartKey])), d: r.recordDate, v: Number(r[chartKey]) }; });
        // Horizontal control tangents keep the curve within each measured interval (no overshoot).
        var linePath = pts.map(function (p, i) {
            if (i === 0) return 'M' + p.x.toFixed(1) + ' ' + p.y.toFixed(1);
            var previous = pts[i - 1], mid = (previous.x + p.x) / 2;
            return 'C' + mid.toFixed(1) + ' ' + previous.y.toFixed(1) + ' ' + mid.toFixed(1) + ' ' + p.y.toFixed(1) + ' ' + p.x.toFixed(1) + ' ' + p.y.toFixed(1);
        }).join(' ');
        var areaPath = linePath + ' L' + pts[pts.length - 1].x.toFixed(1) + ' ' + (padT + ih) + ' L' + pts[0].x.toFixed(1) + ' ' + (padT + ih) + ' Z';
        svg.push('<path class="ba-chart-area" d="' + areaPath + '" fill="url(#baAreaGrad)"/>');
        svg.push('<path class="ba-chart-line" pathLength="1000" d="' + linePath + '" fill="none" stroke="url(#baLineGrad)" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>');

        // نقاط + برچسب تاریخ
        pts.forEach(function (p, i) {
            var last = i === pts.length - 1;
            var delayIndex = pts.length > 1 ? Math.round(i * 8 / (pts.length - 1)) : 0;
            svg.push('<circle class="ba-chart-point' + (last ? ' ba-chart-point-last' : '') + '" style="--ba-point-index:' + delayIndex + ';animation-delay:' + (0.42 + delayIndex * .07).toFixed(2) + 's" cx="' + p.x.toFixed(1) + '" cy="' + p.y.toFixed(1) + '" r="' + (last ? 6 : 4) + '" fill="' + (last ? '#66d3e8' : '#18233b') + '" stroke="' + (last ? '#d9fbff' : '#66d3e8') + '" stroke-width="2"><title>' + esc(fa(p.d) + ' — ' + fa(num(p.v, 1)) + ' ' + chartUnit) + '</title></circle>');
            var labelLimit = W < 480 ? 3 : 6;
            var showLbl = pts.length <= labelLimit || i === 0 || last || (i % Math.ceil((pts.length - 1) / (labelLimit - 1)) === 0 && i < pts.length - 2);
            if (showLbl) {
                svg.push('<text class="ba-chart-label" x="' + p.x.toFixed(1) + '" y="' + (p.y - 14).toFixed(1) + '" text-anchor="middle" direction="ltr" fill="#e7f4ff" font-size="13" font-weight="700" font-family="Sahel,Tahoma">' +
                    fa(num(p.v, 1)) + '</text>');
                svg.push('<text x="' + p.x.toFixed(1) + '" y="' + (H - 10) + '" text-anchor="middle" fill="#b9c2d4" font-size="12" font-family="Sahel,Tahoma" direction="ltr">' +
                    esc(fa(shortDate(p.d))) + '</text>');
            }
        });

        svg.push('</svg>');
        box.innerHTML = svg.join('');
        var chart = box.querySelector('svg');
        if (typeof IntersectionObserver !== 'undefined') {
            chartObserver = new IntersectionObserver(function (entries) {
                if (entries[0].isIntersecting) {
                    chart.classList.add('ba-chart-visible');
                    chartObserver.disconnect();
                }
            }, { threshold: .15 });
            chartObserver.observe(chart);
        } else chart.classList.add('ba-chart-visible');

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
            if (chartKey === chip.getAttribute('data-key')) return;
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

    var chartResizeTimer;
    window.addEventListener('resize', function () {
        clearTimeout(chartResizeTimer);
        chartResizeTimer = setTimeout(function () {
            var box = document.getElementById('baChartBox');
            if (box && box.clientWidth && Math.abs(box.clientWidth - chartWidth) > 8) renderChart();
        }, 120);
    });

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
        btn.disabled = true;
        apiCall('/api/BodyAnalysis/DeleteRecord', 'POST', { recordId: id }, function (res) {
            btn.disabled = false;
            if (res && res.success) {
                showToast(res.message || 'حذف شد', 'success');
                applyPage(res.data);
            } else showToast((res && res.message) || 'خطا در حذف', 'error');
        }, function () { btn.disabled = false; });
    });

    /* ============================================================
       اعمال داده صفحه (بعد از هر عملیات موفق)
       ============================================================ */
    function applyPage(page) {
        if (!page) return;
        closeMetricHelp();
        D = page;
        renderIdentity();
        renderLatestMeasurement();
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
            if (height !== null && (height <= 50 || height > 260)) {
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
    renderLatestMeasurement();
    renderMetrics();
    renderChart();
    renderTable();

    // A browser back/forward-cache restore must not resurrect an older calculation.
    window.addEventListener('pageshow', function (event) {
        if (!event.persisted) return;
        apiCall('/api/BodyAnalysis/Data', 'GET', null, function (res) {
            if (res && res.success) applyPage(res.data);
        });
    });
})();
