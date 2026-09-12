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

                // ۲. بررسی ثبت‌نام فعال
                var hasActiveRegister = await _db.Acc_MemberSports
                    .AnyAsync(ms => ms.MemberID == memberID && ms.IsActive == true);

                if (!hasActiveRegister)
                    return Ok(new { success = false, message = "شما ثبت‌نام فعال ندارید. لطفاً ابتدا تمدید کنید." });

                // ۳. ثبت ورود (تردد)
                var now = DateTime.Now;
                var traffic = new ACC_Traffic
                {
                    MemberID = memberID,
                    EntryDateTime = now,
                    EntryDate = _helper.GetToday(),
                    EntryTime = _helper.GetThisTime(),
                    TrafficStatus = 1, // ورود
                    EntryDesc = "ورود با QR Code",
                    IsManual = false,
                    ShiftID = short.Parse(User.FindFirstValue("ShiftID") ?? "1")
                };

                _db.ACC_Traffics.Add(traffic);
                await _db.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "✅ ورود شما با موفقیت ثبت شد. خوش آمدید!"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ValidateAndEnter");
                return Ok(new { success = false, message = "خطا در پردازش درخواست" });
            }
        }
    }

    public class ValidateQrRequest
    {
        public string QrData { get; set; } = "";
    }
}