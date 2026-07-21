// ============================
// تقویم شمسی مشترک - تقویم-jalali.js
// ============================

var FaDigits = ['۰','۱','۲','۳','۴','۵','۶','۷','۸','۹'];
var FaMonths = ['فروردین','اردیبهشت','خرداد','تیر','مرداد','شهریور','مهر','آبان','آذر','دی','بهمن','اسفند'];

function gregorianToJalali(gy, gm, gd) {
    var g_d_m = [0,31,59,90,120,151,181,212,243,273,304,334];
    var jy = (gy <= 1600) ? 0 : 979;
    gy -= (gy <= 1600) ? 621 : 1600;
    var gy2 = (gm > 2) ? (gy + 1) : gy;
    var days = (365*gy) + (parseInt((gy2+3)/4)) - (parseInt((gy2+99)/100)) + (parseInt((gy2+399)/400)) - 80 + gd + g_d_m[gm-1];
    jy += 33 * parseInt(days/12053); days %= 12053;
    jy += 4 * parseInt(days/1461); days %= 1461;
    if (days > 365) { jy += parseInt((days-1)/365); days = (days-1) % 365; }
    var jm = (days < 186) ? 1 + parseInt(days/31) : 7 + parseInt((days-186)/30);
    var jd = 1 + ((days < 186) ? (days%31) : ((days-186)%30));
    return [jy, jm, jd];
}

function jalaliToGregorian(jy, jm, jd) {
    jy -= 979; gy = (jy <= 0) ? 621 : 1600; jy += (jy <= 0) ? 0 : 979;
    var days = (365*jy) + (parseInt(jy/33)*8) + (parseInt((jy%33)/4)) + 78 + jd + ((jm<7)?(jm-1)*31:((jm-7)*30)+186);
    gy += 400*parseInt(days/146097); days %= 146097;
    if (days > 36524) { gy += 100*parseInt(--days/36524); days %= 36524; if (days>=365) days++; }
    gy += 4*parseInt(days/1461); days %= 1461;
    if (days > 365) { gy += parseInt((days-1)/365); days = (days-1)%365; }
    var sal_a = [0,31,((gy%4===0&&gy%100!==0)||(gy%400===0))?29:28,31,30,31,30,31,31,30,31,30,31];
    var gm = 0;
    for (gm=0; gm<13 && days>=sal_a[gm]; gm++) days -= sal_a[gm];
    return [gy, gm+1, days+1];
}

function pad2(n) { return (n<10?'0':'')+n; }

function todayJalaliStr() {
    var now = new Date();
    var j = gregorianToJalali(now.getFullYear(), now.getMonth()+1, now.getDate());
    return j[0]+'/'+pad2(j[1])+'/'+pad2(j[2]);
}

function toFa(n) { return String(n).replace(/[0-9]/g, function(d){ return FaDigits[d]; }); }

// === تقویم ===
var calendarDate = null;
var calendarCallback = null;
var calendarReturnModal = null; // مودالی که بعد از انتخاب تاریخ باید دوباره باز شود
var calendarAllowPast = false;  // آیا روزهای گذشته قابل انتخاب باشند؟ (پیش‌فرض: خیر)

function initCalendar(targetInputId, returnModalId, allowPast) {
    if (!calendarDate) {
        var t = todayJalaliStr().split('/');
        calendarDate = { y: parseInt(t[0]), m: parseInt(t[1]) };
    }
    calendarCallback = targetInputId;
    calendarReturnModal = returnModalId || null;
    calendarAllowPast = (allowPast === true);
    renderCalendar();
}

function renderCalendar(allowPast) {
    // اگر پارامتر صریح پاس شده، از آن استفاده شود؛ در غیر این صورت از متغیر سراسری
    var canSelectPast = (allowPast === true) ? true : calendarAllowPast;

    $('#currentMonthLabel').text(FaMonths[calendarDate.m - 1] + ' ' + toFa(calendarDate.y));

    var g = jalaliToGregorian(calendarDate.y, calendarDate.m, 1);
    var firstDay = new Date(g[0], g[1]-1, g[2]);
    // شنبه=0
    var startOffset = (firstDay.getDay() + 1) % 7;

    var isLeap = ((((calendarDate.y % 33) * 8) + 4) % 33) < 8;
    var daysInMonth = calendarDate.m <= 6 ? 31 : (calendarDate.m <= 11 ? 30 : (isLeap ? 30 : 29));

    var todayParts = todayJalaliStr().split('/');
    var todayY = parseInt(todayParts[0]), todayM = parseInt(todayParts[1]), todayD = parseInt(todayParts[2]);

    var html = '';
    var day = 1;
    for (var w = 0; w < 7; w++) {
        html += '<tr>';
        for (var d = 0; d < 7; d++) {
            var cellIdx = w * 7 + d;
            if (cellIdx < startOffset || day > daysInMonth) {
                html += '<td class="cal-empty"></td>';
            } else {
                // فقط روزهای گذشته غیرفعال هستند؛ مگر اینکه allowPast فعال باشد
                var isPast = !canSelectPast && (
                    (calendarDate.y < todayY) ||
                    (calendarDate.y === todayY && calendarDate.m < todayM) ||
                    (calendarDate.y === todayY && calendarDate.m === todayM && day < todayD)
                );

                if (isPast) {
                    html += '<td class="cal-disabled">' + toFa(day) + '</td>';
                } else {
                    html += '<td class="cal-active" onclick="pickDay(' + day + ')">' + toFa(day) + '</td>';
                }
                day++;
            }
        }
        html += '</tr>';
        if (day > daysInMonth) break;
    }
    $('#calendarBody').html(html);
}

function pickDay(day) {
    var dateStr = calendarDate.y + '/' + pad2(calendarDate.m) + '/' + pad2(day);
    $('#' + calendarCallback).val(dateStr);
    var dpModal = bootstrap.Modal.getInstance('#datePickerModal');
    if (dpModal) dpModal.hide();
    // بازگشت به مودال قبلی (مثلاً مودال تمدید) اگر وجود دارد
    if (calendarReturnModal) {
        // یک تأخیر کوچک تا بسته شدن مودال تقویم کامل شود
        setTimeout(function () {
            var ret = document.querySelector(calendarReturnModal);
            if (ret) {
                var bs = bootstrap.Modal.getInstance(ret) || new bootstrap.Modal(ret);
                bs.show();
            }
        }, 250);
    }
}
