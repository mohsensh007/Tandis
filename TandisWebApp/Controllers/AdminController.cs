using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly AdminAuthService _auth;
        private readonly AdminReportService _reports;
        private readonly MessageService _messages;

        private short? CurrentAdminUserID
        {
            get
            {
                var value = User.FindFirst("UserID")?.Value
                         ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                return short.TryParse(value, out var id) ? id : (short?)null;
            }
        }

        public AdminController(AdminAuthService auth, AdminReportService reports , MessageService message)
        {
            _auth = auth;
            _reports = reports;
            _messages = message;
        }

        private short AdminShiftID => User.GetAdminShiftID();

        // ============================================================
        //  Views - need admin auth
        // ============================================================

        [AdminAuthorize]
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult TrafficReport()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult RegisterReport()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult OneSessionReport()
        {
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        public IActionResult FinanceReport()
        {
            return View();
        }

        // ============================================================
        //  API — احراز هویت مدیر
        // ============================================================

        [HttpPost]
        [Route("api/Admin/Login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginApi([FromBody] AdminLoginRequest req)
        {
            var result = await _auth.LoginAsync(req);
            if (!result.Success)
                return Ok(result);

            // حذف کوکی عضو (اگر قبلاً با کد ملی وارد شده بوده)
            // تا توکن ادمین اولویت پیدا کند و session عضو با مدیر تداخل نکند.
            Response.Cookies.Delete("X-Access-Token");

            // ذخیره توکن ادمین در کوکی جدا
            Response.Cookies.Append("X-Admin-Token", result.Data!.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(24)
            });

            return Ok(result);
        }

        // ============================================================
        //  API — گزارش‌ها
        // ============================================================

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/TrafficReport")]
        public async Task<IActionResult> TrafficReportApi(string? from, string? to)
        {
            var response = await _reports.GetTrafficReportAsync(AdminShiftID, from, to);
            return Ok(new { success = true, data = response.Data, summary = response.Summary });
        }

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/RegisterReport")]
        public async Task<IActionResult> RegisterReportApi(string? from, string? to, string mode = "both")
        {
            var response = await _reports.GetRegisterReportAsync(AdminShiftID, from, to, mode);
            return Ok(new { success = true, data = response.Data, summary = response.Summary });
        }

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/OneSessionReport")]
        public async Task<IActionResult> OneSessionReportApi(string? from, string? to)
        {
            var response = await _reports.GetOneSessionReportAsync(AdminShiftID, from, to);
            return Ok(new { success = true, data = response.Data, summary = response.Summary });
        }

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/FinanceReport")]
        public async Task<IActionResult> FinanceReportApi(string? from, string? to)
        {
            var response = await _reports.GetFinanceReportAsync(AdminShiftID, from, to);
            return Ok(new { success = true, data = response.Data, summary = response.Summary });
        }

        // ============================================================
        //  خروج مدیر
        // ============================================================

        [AdminAuthorize]
        [HttpGet]
        [Route("Admin/Logout")]
        public IActionResult Logout()
        {
            // حذف هر دو کوکی تا session کاملاً پاک شود
            Response.Cookies.Delete("X-Admin-Token");
            Response.Cookies.Delete("X-Access-Token");
            return RedirectToAction("Login", "Account");
        }
        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/DashboardStats")]
        public async Task<IActionResult> DashboardStatsApi(string period = "day")
        {
            var stats = await _reports.GetDashboardStatsAsync(AdminShiftID, period);
            return Ok(new { success = true, data = stats });
        }

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/InsideList")]
        public async Task<IActionResult> InsideListApi()
        {
            var list = await _reports.GetInsideListAsync(AdminShiftID);
            return Ok(new { success = true, data = list });
        }
        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/Messages/Inbox")]
        public async Task<IActionResult> MessagesInboxApi()
    => Ok(new { success = true, data = await _messages.GetAdminInboxAsync(AdminShiftID) });

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/Messages/UnreadCount")]
        public async Task<IActionResult> MessagesUnreadApi()
            => Ok(new { success = true, count = await _messages.GetAdminUnreadCountAsync(AdminShiftID) });

        [AdminAuthorize]
        [HttpPost]
        [Route("api/Admin/Messages/Send")]
        public async Task<IActionResult> MessagesSendApi([FromBody] SendMessageRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Body))
                return Ok(new { success = false, message = "متن پیام خالی است" });
            await _messages.SendFromAdminAsync(CurrentAdminUserID, req);
            return Ok(new { success = true, message = "پیام با موفقیت ارسال شد" });
        }

        [AdminAuthorize]
        [HttpPost]
        [Route("api/Admin/Messages/Reply")]
        public async Task<IActionResult> MessagesReplyApi([FromBody] ReplyMessageRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Body))
                return Ok(new { success = false, message = "متن پاسخ خالی است" });
            await _messages.ReplyAsync(CurrentAdminUserID, req);
            return Ok(new { success = true, message = "پاسخ ارسال شد" });
        }

        [AdminAuthorize]
        [HttpPost]
        [Route("api/Admin/Messages/MarkSeen")]
        public async Task<IActionResult> MessagesMarkSeenApi([FromBody] long messageID)
        {
            await _messages.MarkSeenAsync(messageID);
            return Ok(new { success = true });
        }
    }
}