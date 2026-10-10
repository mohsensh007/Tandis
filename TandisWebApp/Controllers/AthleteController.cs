using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TandisWebApp.Attributes;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Services;
using Microsoft.EntityFrameworkCore;

namespace TandisWebApp.Controllers
{
    /// <summary>
    /// گوشه‌ی «ورزشکاران» پنل عضو:
    ///  ۱) وضعیت ورود/خروج امروز — فقط خواندنی از دستگاه تردد باشگاه (dbo.ACC_Traffic)
    ///  ۲) کنترل دسترسی: تا وقتی داخل باشگاهه → «کمد من» + «جدول تمرین من»
    ///     بعد از خروج → اون دو بسته می‌شن و فقط «خلاصه تمرین امروز» (نمودار دایره‌ای) باز می‌مونه
    ///  ۳) جدول تمرین: انتخاب برنامه + روز، تیک هر حرکت با زمان سپری‌شده (Web_AthleteSetLog)
    ///  ۴) باز کردن کمد رختکن (ارتباط با کنترلر از اطلاعات Gen_LockerRoom / Gen_Box)
    /// </summary>
    [MemberAuthorize]
    public class AthleteController : Controller
    {
        private readonly AthleteService _athlete;
        private readonly CoachProgramService _programs;
        private readonly LockerService _locker;
        private readonly CommonHelperService _helper;
        private readonly FullSportDbContext _db;

        public AthleteController(
            AthleteService athlete,
            CoachProgramService programs,
            LockerService locker,
            CommonHelperService helper,
            FullSportDbContext db)
        {
            _athlete = athlete;
            _programs = programs;
            _locker = locker;
            _helper = helper;
            _db = db;
        }

        private int MemberID => int.Parse(User.FindFirstValue("MemberID") ?? "0");

        // ============================================================
        //  صفحه اصلی
        // ============================================================
        [HttpGet("/Athlete")]
        public async Task<IActionResult> Index()
        {
            var memberID = MemberID;

            // ✅ وضعیت حضور: ورودِ بازِ امروز از دستگاه تردد (ExitTime خالی = داخل باشگاه)
            var todayVisit = await _athlete.GetTodayVisitAsync(memberID);
            var isInside = todayVisit != null && todayVisit.IsOpen;

            var model = new AthleteIndexDto
            {
                TodayShamsi = _helper.GetToday(),
                WeekdayFa = WeekdayFa(DateTime.Now.DayOfWeek),
                TodayVisit = todayVisit,
                IsInside = isInside,
                RecentVisits = await _athlete.GetVisitsAsync(memberID, 8),
                Programs = await _programs.GetMemberProgramsAsync(memberID),

               
                // ✅ خلاصه‌ی نمودار فقط وقتی خارج شده پر می‌شه
                TodaySummary = isInside
                    ? new List<AthleteSummaryItemDto>()
                    : await _athlete.GetTodaySummaryAsync(memberID)
            };

            model.Stats = await _athlete.GetStatsAsync(memberID, await _athlete.CountDoneTodayAsync(memberID));
            return View(model);
        }

        // ============================================================
        //  نگهبان دسترسی: آیا عضو الان داخل باشگاهه؟
        // ============================================================
        /// <summary>✅ ورودِ بازِ امروز = داخل باشگاه. بعد از خروج همه‌ی endpointهای تمرین/کمد بسته می‌شن.</summary>
        private async Task<bool> IsInsideAsync(int memberID)
        {
            var v = await _athlete.GetTodayVisitAsync(memberID);
            return v != null && v.IsOpen;
        }

        private async Task<MyLockerDto?> GetMyLockerAsync(int memberID, AthleteVisitDto? visit = null)
        {
            visit ??= await _athlete.GetTodayVisitAsync(memberID);
            if (visit == null || visit.BoxID == null) return null;

            var box = await _db.Gen_Boxes.AsNoTracking()
                .FirstOrDefaultAsync(b => b.BoxID == visit.BoxID);
            if (box == null) return null;

            var room = await _db.Gen_LockerRooms.AsNoTracking()
                .FirstOrDefaultAsync(r => r.LockerRoomID == box.LockerRoomID);
            if (room == null) return null;

            return new MyLockerDto
            {
                LockerRoomID = room.LockerRoomID,
                LockerRoomName = room.LockerRoomName ?? $"رختکن {room.LockerRoomID}",
                BoxNo = box.BoxNo ?? 0,          // ✅ حتماً BoxNo، نه BoxID / RadifNo
                IsOnline = room.IsOnline == true,
                HasController = room.ControllerID != null,
                Transport = string.IsNullOrWhiteSpace(room.IpAddress) ? "Serial" : "UDP"
            };
        }

        // ============================================================
        //  کمد رختکن — فقط کمدِ خودِ عضو
        // ============================================================
        /// <summary>✅ باز کردن کمد اختصاص‌یافته به خودِ عضو در بازدید امروز.
        /// عضو رختکن/شماره کمد را انتخاب نمی‌کند؛ کمدِ خودش را باز می‌کند.
        /// 🔒 فقط وقتی عضو داخل باشگاهه.</summary>
        [HttpPost("/Athlete/OpenMyLocker")]
        public async Task<IActionResult> OpenMyLocker()
        {
            var memberID = MemberID;

            // 🔒 نگهبان دسترسی
            if (!await IsInsideAsync(memberID))
                return Json(new LockerOpenResultDto
                {
                    Success = false,
                    Message = "برای باز کردن کمد باید داخل باشگاه باشید."
                });

            var my = await GetMyLockerAsync(memberID);
            if (my == null || my.BoxNo <= 0)
                return Json(new LockerOpenResultDto
                {
                    Success = false,
                    Message = "برای شما کمدی در باشگاه ثبت نشده است."
                });

            var result = await _locker.OpenBoxAsync(memberID, my.LockerRoomID, my.BoxNo);
            return Json(result);
        }

        // ✅ موقتاً غیرفعال شد (درخواست کاربر): ثبت ورود/خروج از داخل اپ حذف شد و
        //    ورود/خروج با دستگاه تردد باشگاه ثبت می‌شود. برای برگرداندن، کامنت را بردارید.
        //
        // [HttpPost("/Athlete/CheckIn")]
        // public async Task<IActionResult> CheckIn([FromBody] AthleteCheckInRequest req)
        // {
        //     var v = await _athlete.CheckInAsync(MemberID, req?.EnterTime, req?.SessionName);
        //     return Json(new { success = true, visit = v });
        // }
        //
        // [HttpPost("/Athlete/CheckOut")]
        // public async Task<IActionResult> CheckOut()
        // {
        //     var v = await _athlete.CheckOutAsync(MemberID);
        //     if (v == null) return Json(new { success = false, message = "ورودی ثبت نشده است." });
        //     return Json(new { success = true, visit = v });
        // }

        // ============================================================
        //  جدول تمرین
        // ============================================================
        /// <summary>آیتم‌های یک روز از برنامه + لاگ‌های همان روز. 🔒 فقط وقتی عضو داخل باشگاهه.</summary>
        [HttpGet("/Athlete/Day")]
        public async Task<IActionResult> Day(int prgID, string? day, string? date)
        {
            var memberID = MemberID;

            // 🔒 نگهبان دسترسی: بعد از خروج، جدول تمرین بسته است
            if (!await IsInsideAsync(memberID))
                return new JsonResult(
                    new { success = false, message = "بعد از خروج، دسترسی به جدول تمرین بسته است." },
                    new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });

            var all = await _programs.GetMemberProgramsAsync(memberID);
            var prg = all.FirstOrDefault(p => p.PrgID == prgID);
            if (prg == null) return NotFound();

            var days = prg.Items.Select(i => i.DayTitle ?? "بدون روز")
                                 .Where(d => !string.IsNullOrWhiteSpace(d))
                                 .Distinct()
                                 .ToList();
            var dayTitle = !string.IsNullOrWhiteSpace(day) && days.Contains(day!)
                ? day!
                : (days.FirstOrDefault(d => d.Contains(WeekdayAbbr(DateTime.Now.DayOfWeek))) ?? days.FirstOrDefault() ?? "");

            var items = prg.Items.Where(i => (i.DayTitle ?? "بدون روز") == dayTitle).ToList();
            var logs = await _athlete.GetLogsAsync(memberID, prgID, date);

            // ✅ حفظ حالت PascalCase — چون athlete.js با data.Items / it.ItemID می‌خونه
            return new JsonResult(new AthleteDayDto
            {
                PrgID = prgID,
                CoachName = prg.CoachName,
                DayTitle = dayTitle,
                Days = days,
                Items = items,
                Logs = logs
            },
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = null,          // بدون تبدیل به camelCase
                PropertyNameCaseInsensitive = true
            });
        }

        /// <summary>ذخیره وضعیت یک حرکت (تیک + زمان سپری‌شده). 🔒 فقط وقتی عضو داخل باشگاهه.</summary>
        [HttpPost("/Athlete/SaveSet")]
        public async Task<IActionResult> SaveSet([FromBody] AthleteSaveSetRequest req)
        {
            if (req == null || req.PrgID <= 0 || req.ItemID <= 0)
                return Json(new { success = false, message = "درخواست نامعتبر است." });

            // 🔒 نگهبان دسترسی: بعد از خروج، ثبت تمرین بسته است
            if (!await IsInsideAsync(MemberID))
                return Json(new { success = false, message = "بعد از خروج، دسترسی به ثبت تمرین بسته است." });

            await _athlete.SaveSetAsync(MemberID, req.PrgID, req.ItemID, req.Done,
                Math.Clamp(req.Seconds, 0, 86400), req.Note, req.Date);
            return Json(new { success = true });
        }

        // ============================================================
        //  کمد رختکن (انتخاب دستی — برای سازگاری با کلاینت‌های قدیمی)
        // ============================================================
        /// <summary>🔒 فقط وقتی عضو داخل باشگاهه.</summary>
        [HttpPost("/Athlete/OpenLocker")]
        public async Task<IActionResult> OpenLocker([FromBody] AthleteOpenLockerRequest req)
        {
            // 🔒 نگهبان دسترسی
            if (!await IsInsideAsync(MemberID))
                return Json(new LockerOpenResultDto
                {
                    Success = false,
                    Message = "برای باز کردن کمد باید داخل باشگاه باشید."
                });

            if (req == null || req.BoxNo <= 0)
                return Json(new LockerOpenResultDto { Success = false, Message = "رختکن و شماره‌ی کمد را انتخاب کنید." });

            var result = await _locker.OpenBoxAsync(MemberID, req.LockerRoomID, req.BoxNo);
            return Json(result);
        }
        // ============================================================
        //  ✅ خلاصه تمرین امروز (نمودار دایره‌ای) — زنده، بدون رفرش صفحه
        // ============================================================
        [HttpGet("/Athlete/Summary")]
        public async Task<IActionResult> Summary()
        {
            var data = await _athlete.GetTodaySummaryAsync(MemberID);
            // Json() پیش‌فرض camelCase می‌ده → name / minutes (مطابق athlete.js)
            return Json(new { summary = data });
        }

        // ============================================================
        //  کمکی
        // ============================================================
        private static string WeekdayFa(DayOfWeek d) => d switch
        {
            DayOfWeek.Saturday => "شنبه",
            DayOfWeek.Sunday => "یکشنبه",
            DayOfWeek.Monday => "دوشنبه",
            DayOfWeek.Tuesday => "سه‌شنبه",
            DayOfWeek.Wednesday => "چهارشنبه",
            DayOfWeek.Thursday => "پنجشنبه",
            _ => "جمعه"
        };

        private static string WeekdayAbbr(DayOfWeek d) => WeekdayFa(d) switch
        {
            "شنبه" => "شنب",
            "یکشنبه" => "یکشنبه",
            "دوشنبه" => "دوشنبه",
            "سه‌شنبه" => "سه‌شنبه",
            "چهارشنبه" => "چهارشنبه",
            "پنجشنبه" => "پنجشنبه",
            _ => "جمعه"
        };
    }

    // ===== درخواست‌های JSON =====
    public class AthleteCheckInRequest
    {
        public string? EnterTime { get; set; }
        public string? SessionName { get; set; }
    }

    public class AthleteSaveSetRequest
    {
        public int PrgID { get; set; }
        public int ItemID { get; set; }
        public bool Done { get; set; }
        public int Seconds { get; set; }
        public string? Note { get; set; }
        public string? Date { get; set; }
    }

    public class AthleteOpenLockerRequest
    {
        public short LockerRoomID { get; set; }
        public short BoxNo { get; set; }
    }
}