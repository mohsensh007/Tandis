using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    /// <summary>
    /// ✅ رله‌ی فرمان باز کردن کمد.
    /// کنترلر کمد فقط بسته‌های UDP با مبدأ IP سیستمِ ServerIP (در جدول Gen_LlockerRoom) را قبول می‌کند؛
    /// بنابراین وقتی عضو از سیستم/گوشی دیگری وارد می‌شود، آن نمونه‌ی برنامه فرمان را با POST به این
    /// endpoint روی سرور می‌فرستد و سرور UDP را از IP خودش (192.168.1.172) ارسال می‌کند.
    /// امنیت: اگر LockerRelay:Secret تنظیم شده باشد، هدر X-Locker-Relay-Key باید برابر باشد.
    /// </summary>
    public class LockerRelayController : Controller
    {
        private readonly LockerService _locker;
        private readonly IConfiguration _cfg;
        private readonly ILogger<LockerRelayController> _log;

        public LockerRelayController(LockerService locker, IConfiguration cfg, ILogger<LockerRelayController> log)
        {
            _locker = locker;
            _cfg = cfg;
            _log = log;
        }

        [HttpPost("/api/LockerRelay/Open")]
        public async Task<IActionResult> Open([FromBody] LockerRelayRequestDto? req)
        {
            var secret = _cfg["LockerRelay:Secret"];
            if (!string.IsNullOrWhiteSpace(secret)
                && !string.Equals(Request.Headers["X-Locker-Relay-Key"], secret, StringComparison.Ordinal))
            {
                return new JsonResult(new LockerOpenResultDto { Success = false, Message = "کلید رله (X-Locker-Relay-Key) نادرست است." },
                    new JsonSerializerOptions { PropertyNamingPolicy = null });
            }

            if (req == null || req.BoxNo <= 0 || req.LockerRoomID <= 0)
                return BadRequest();

            _log.LogInformation("🔁 رله‌ی باز کردن کمد: رختکن {Room} کمد {Box} از {Remote}",
                req.LockerRoomID, req.BoxNo, HttpContext.Connection.RemoteIpAddress);

            var result = await _locker.OpenRelayedAsync(req.LockerRoomID, req.BoxNo, req.Duration);

            return new JsonResult(result,
                new JsonSerializerOptions { PropertyNamingPolicy = null });
        }
    }
}
