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
    ///  ۱) ثبت ورود/خروج باشگاه (با ساعت ورود)
    ///  ۲) جدول تمرین امروز: انتخاب برنامه + روز، تیک هر حرکت با زمان سپری‌شده
    ///  ۳) باز کردن کمد رختکن (ارتباط با کنترلر از اطلاعات Gen_LlockerRoom)
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
            var model = new AthleteIndexDto
            {
                TodayShamsi = _helper.GetToday(),
                WeekdayFa = WeekdayFa(DateTime.Now.DayOfWeek),
                // ✅ ورود/خروج از داده‌ی دستگاه تردد باشگاه خوانده می‌شود (ثبت توسط عضو حذف شد)
                TodayVisit = await _athlete.GetTodayVisitAsync(memberID),
                RecentVisits = await _athlete.GetVisitsAsync(memberID, 8),
                Programs = await _programs.GetMemberProgramsAsync(memberID),
                MyLocker = await GetMyLockerAsync(memberID)
            };
            model.Stats = await _athlete.GetStatsAsync(memberID, await _athlete.CountDoneTodayAsync(memberID));
            return View(model);
        }

        /// <summary>✅ کمد اختصاص‌یافته به عضو در بازدید امروز (از روی کمد ثبت‌شده در تردد)</summary>
        private async Task<MyLockerDto?> GetMyLockerAsync(int memberID)
        {
            var visit = await _athlete.GetTodayVisitAsync(memberID);
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
                BoxNo = box.BoxNo ?? 0,
                IsOnline = room.IsOnline == true,
                HasController = room.ControllerID != null,
                Transport = string.IsNullOrWhiteSpace(room.IpAddress) ? "Serial" : "UDP"
            };
        }

        // ============================================================
        //  کمد رختکن — فقط کمدِ خودِ عضو
        // ============================================================
        /// <summary>✅ باز کردن کمد اختصاص‌یافته به خودِ عضو در بازدید امروز.
        /// عضو رختکن/شماره کمد را انتخاب نمی‌کند؛ کمدِ خودش را باز می‌کند.</summary>
        [HttpPost("/Athlete/OpenMyLocker")]
        public async Task<IActionResult> OpenMyLocker()
        {
            var memberID = MemberID;
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
        /// <summary>آیتم‌های یک روز از برنامه + لاگ‌های همان روز</summary>
        [HttpGet("/Athlete/Day")]
        public async Task<IActionResult> Day(int prgID, string? day, string? date)
        {
            var memberID = MemberID;
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

            return Json(new AthleteDayDto
            {
                PrgID = prgID,
                CoachName = prg.CoachName,
                DayTitle = dayTitle,
                Days = days,
                Items = items,
                Logs = logs
            });
        }

        /// <summary>ذخیره وضعیت یک حرکت (تیک + زمان سپری‌شده)</summary>
        [HttpPost("/Athlete/SaveSet")]
        public async Task<IActionResult> SaveSet([FromBody] AthleteSaveSetRequest req)
        {
            if (req == null || req.PrgID <= 0 || req.ItemID <= 0)
                return Json(new { success = false, message = "درخواست نامعتبر است." });

            await _athlete.SaveSetAsync(MemberID, req.PrgID, req.ItemID, req.Done,
                Math.Clamp(req.Seconds, 0, 86400), req.Note, req.Date);
            return Json(new { success = true });
        }

        // ============================================================
        //  کمد رختکن
        // ============================================================
        [HttpPost("/Athlete/OpenLocker")]
        public async Task<IActionResult> OpenLocker([FromBody] AthleteOpenLockerRequest req)
        {
            if (req == null || req.BoxNo <= 0)
                return Json(new LockerOpenResultDto { Success = false, Message = "رختکن و شماره‌ی کمد را انتخاب کنید." });

            var result = await _locker.OpenBoxAsync(MemberID, req.LockerRoomID, req.BoxNo);
            return Json(result);
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
