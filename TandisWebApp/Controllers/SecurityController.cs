using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Security.Claims;
using TandisWebApp.Attributes;
using TandisWebApp.Data;
using TandisWebApp.Models;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [Authorize]
    public class SecurityController : Controller
    {
        private readonly QrService _qrService;
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<SecurityController> _logger;

        public SecurityController(
            QrService qrService,
            FullSportDbContext db,
            CommonHelperService helper,
            ILogger<SecurityController> logger)
        {
            _qrService = qrService;
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("Kiosk/QrDisplay")]
        public IActionResult QrDisplay()
        {
            ViewData["Title"] = "QR Code ورود";
            return View("~/Views/QRCode/QrDisplay.cshtml");
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("api/Security/QrCode")]
        public async Task<IActionResult> GetQrCode([FromQuery] short shiftID = 1)
        {
            try
            {
                var payload = await _qrService.GenerateQrPayloadAsync(shiftID);

                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20);

                // اگه به اینجا رسیدیم، یعنی تصویر درست تولید شده
                return File(qrCodeImage, "image/png");
            }
            catch (Exception ex)
            {
                // برگردوندن خطای کامل
                var errorMsg = $"Error: {ex.Message}\nInner: {ex.InnerException?.Message}\nStack: {ex.StackTrace}";
                return Content(errorMsg, "text/plain");
            }
        }

        [HttpPost]
        [Route("api/Security/ValidateQr")]
        [AllowAnonymous]
        public async Task<IActionResult> ValidateQr([FromBody] ValidateQrRequest req)
        {
            var (isValid, message) = await _qrService.ValidateQrPayloadAsync(req.QrData);
            return Ok(new { success = isValid, message });
        }

        [HttpPost]
        [Route("api/Security/ValidateAndEnter")]
        public async Task<IActionResult> ValidateAndEnter([FromBody] ValidateQrRequest req)
        {
            try
            {
                var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
                if (memberID == 0)
                    return Ok(new { success = false, message = "کاربر لاگین نیست" });

                // ۱. اعتبارسنجی QR
                var (isValid, message) = await _qrService.ValidateQrPayloadAsync(req.QrData);
                if (!isValid)
                    return Ok(new { success = false, message });

                // ۲. اطلاعات عضو + ثبت‌نام فعال + تنظیمات کمد
                var memberInfo = await (
                    from m in _db.Gen_Members
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where m.MemberID == memberID
                    select new { m.ShiftID, Name = p.FullName }
                ).FirstOrDefaultAsync();

                var activeReg = await _db.Acc_MemberSports
                    .Where(ms => ms.MemberID == memberID && ms.IsActive == true)
                    .OrderByDescending(ms => ms.SportMemberID)
                    .Select(ms => new
                    {
                        ms.SportMemberID,
                        ms.SportSanseID,
                        SetBox = ms.Gen_SportSanse.Gen_Sport_Category.SetBox,
                        LockerRoomID = ms.Gen_SportSanse.Gen_Sport_Category.LockerRoomID
                    })
                    .FirstOrDefaultAsync();

                if (activeReg == null)
                    return Ok(new { success = false, message = "شما ثبت‌نام فعال ندارید. لطفاً ابتدا تمدید کنید." });

                // ========== چک ۱: ساعت مجاز کلاس (Gen_SportSanseDetail) ==========
                var now = DateTime.Now;
                var todayLatin = now.DayOfWeek.ToString();

                var todayDayID = await _db.Set<Gen_DayOfWeek>()
                    .Where(d => d.LatinName == todayLatin)
                    .Select(d => d.DayID)
                    .FirstOrDefaultAsync();

                var schedules = await _db.Set<Gen_SportSanseDetail>()
                    .Where(x => x.SportSanseID == activeReg.SportSanseID
                                && (x.IsActive == true || x.IsActive == null))
                    .Select(x => new { x.DayID, x.StartTime, x.EndTime })
                    .ToListAsync();

                // ✅ حالت سخت‌گیرانه: بدون زمان‌بندی = بدون ورود
                if (schedules.Count == 0)
                    return Ok(new { success = false, message = "⚠️ برای این سانس هیچ زمان‌بندی تعریف نشده است. ورود امکان‌پذیر نیست. لطفاً به باشگاه مراجعه کنید." });

                var todaySchedules = schedules.Where(x => x.DayID == todayDayID).ToList();

                if (todaySchedules.Count == 0)
                    return Ok(new { success = false, message = "⚠️ امروز کلاسی برای این سانس تعریف نشده است." });

                var nowTime = now.TimeOfDay;
                var isInTime = todaySchedules.Any(x =>
                    x.StartTime != null && x.EndTime != null &&
                    nowTime >= x.StartTime.Value && nowTime <= x.EndTime.Value);

                if (!isInTime)
                {
                    var times = string.Join(" و ", todaySchedules
                        .Where(x => x.StartTime != null && x.EndTime != null)
                        .Select(x => $"{x.StartTime.Value:hh\\:mm} تا {x.EndTime.Value:hh\\:mm}"));

                    return Ok(new { success = false, message = $"⚠️ ساعت ورود شما خارج از زمان مجاز کلاس است.\nساعات مجاز امروز: {times}" });
                }

                // ========== چک ۲: جلوگیری از ورود دوباره ==========
                var hasOpenTraffic = await _db.ACC_Traffics
                    .AnyAsync(t => t.MemberID == memberID && t.ExitDate == null && t.ExitDateTime == null);

                if (hasOpenTraffic)
                    return Ok(new { success = false, message = "⚠️ شما قبلاً وارد شده‌اید و خروج‌تان ثبت نشده است." });

                // ========== چک ۳: اختصاص کمد تصادفی (اگه SetBox روشن باشه) ==========
                short? assignedBoxID = null;
                short? assignedBoxNo = null;

                if (activeReg.SetBox == true)
                {
                    // کمدهای اشغال (تردد باز)
                    var busyBoxIds = await _db.ACC_Traffics
                        .Where(t => t.BoxID != null && t.ExitDate == null && t.ExitDateTime == null)
                        .Select(t => t.BoxID)
                        .ToListAsync();

                    var lockerRoom = activeReg.LockerRoomID;

                    // ✅ حالا از DbSet استفاده می‌کنیم
                    var candidateBoxes = await _db.Gen_Boxes
                        .Where(b => b.IsActive == true)
                        .Where(b => lockerRoom == null || b.LockerRoomID == lockerRoom)
                        .Select(b => new { b.BoxID, b.BoxNo })
                        .ToListAsync();

                    var freeBoxes = candidateBoxes.Where(b => !busyBoxIds.Contains(b.BoxID)).ToList();

                    if (freeBoxes.Count == 0)
                        return Ok(new { success = false, message = "⚠️ هیچ کمد خالی‌ای موجود نیست. لطفاً به مسئول باشگاه مراجعه کنید." });

                    var picked = freeBoxes[new Random().Next(freeBoxes.Count)];
                    assignedBoxID = picked.BoxID;
                    assignedBoxNo = picked.BoxNo;
                }

                // ========== ثبت ورود ==========
                var shiftID = memberInfo?.ShiftID ?? short.Parse(User.FindFirstValue("ShiftID") ?? "1");
                short webUserID = 1;
                byte trafficStatus = 1;
                bool isManual = false;
                var entryDesc = "ورود با QR Code";
                var personName = memberInfo?.Name ?? "";
                long? sportMemberID = activeReg.SportMemberID;

                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                 INSERT INTO ACC_Traffic (MemberID, SportMemberID, TrafficStatus, EntryDesc, ShiftID, UserID, PersonName, EntryDateTime, IsManual, BoxID)
                 VALUES ({memberID}, {sportMemberID}, {trafficStatus}, {entryDesc}, {shiftID}, {webUserID}, {personName}, {now}, {isManual}, {assignedBoxID})");

                var successMsg = assignedBoxNo != null
                    ? $"✅ ورود شما ثبت شد. کمد شماره: {assignedBoxNo}"
                    : "✅ ورود شما با موفقیت ثبت شد. خوش آمدید!";

                return Ok(new { success = true, message = successMsg });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ValidateAndEnter");
                return Ok(new { success = false, message = $"خطا در ثبت ورود: {ex.Message}" });
            }
        }
        /// <summary>
        /// وضعیت تردد باز عضو (داخل باشگاه هست یا نه)
        /// </summary>
        [HttpGet]
        [Route("api/Security/OpenTraffic")]
        public async Task<IActionResult> GetOpenTraffic()
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            if (memberID == 0)
                return Ok(new { success = false, message = "کاربر لاگین نیست" });

            var open = await _db.ACC_Traffics
                .Where(t => t.MemberID == memberID && t.ExitDate == null && t.ExitDateTime == null)
                .OrderByDescending(t => t.TrafficID)
                .Select(t => new { t.TrafficID, t.EntryDate, t.EntryTime, t.BoxID })
                .FirstOrDefaultAsync();

            if (open == null)
                return Ok(new { success = true, isInside = false });

            // شماره کمد (اگه داشته باشه)
            short? boxNo = await _db.Gen_Boxes
                .Where(b => b.BoxID == open.BoxID)
                .Select(b => b.BoxNo)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                success = true,
                isInside = true,
                entryDate = open.EntryDate,
                entryTime = open.EntryTime,
                boxNo
            });
        }

        /// <summary>
        /// ثبت خروج عضو از باشگاه
        /// </summary>
        [HttpPost]
        [Route("api/Security/Exit")]
        public async Task<IActionResult> Exit()
        {
            try
            {
                var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
                if (memberID == 0)
                    return Ok(new { success = false, message = "کاربر لاگین نیست" });

                var open = await _db.ACC_Traffics
                    .Where(t => t.MemberID == memberID && t.ExitDate == null && t.ExitDateTime == null)
                    .OrderByDescending(t => t.TrafficID)
                    .Select(t => t.TrafficID)
                    .FirstOrDefaultAsync();

                if (open == 0)
                    return Ok(new { success = false, message = "⚠️ شما تردد بازی ندارید (داخل باشگاه نیستید)." });

                // ثبت خروج با SQL خام → ExitDate/ExitTime با توابع خود دیتابیس پر می‌شن (دقیقاً مثل دسکتاپ)
                // TrafficStatus = 0 یعنی خروج (قرارداد دیتابیس: Enter=1 / Exit=0)
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                  UPDATE ACC_Traffic
                  SET TrafficStatus = 0,
                  ExitDateTime = GETDATE(),
                  ExitDate = dbo.MiladiToShamsi(GETDATE()),
                  ExitTime = dbo.GetThisTime()
                  WHERE TrafficID = {open}");

                return Ok(new { success = true, message = "✅ خروج شما با موفقیت ثبت شد. خدانگهدار!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ثبت خروج");
                return Ok(new { success = false, message = $"خطا در ثبت خروج: {ex.Message}" });
            }
        }
    }

    public class ValidateQrRequest
    {
        public string QrData { get; set; } = "";
    }
}