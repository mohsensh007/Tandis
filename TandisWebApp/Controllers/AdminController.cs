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
        private readonly ProfileService _profile;

        private short? CurrentAdminUserID
        {
            get
            {
                var value = User.FindFirst("UserID")?.Value
                         ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                return short.TryParse(value, out var id) ? id : (short?)null;
            }
        }

        public AdminController(AdminAuthService auth, AdminReportService reports, MessageService message, ProfileService profile)
        {
            _auth = auth;
            _reports = reports;
            _messages = message;
            _profile = profile;
        }

        private short AdminShiftID => User.GetAdminShiftID();

        // ============================================================
        //  Views - need admin auth
        // ============================================================

        [AdminAuthorize]
        [HttpGet]
        public IActionResult Index() => View();

        [AdminAuthorize]
        [HttpGet]
        public IActionResult TrafficReport() => View();

        [AdminAuthorize]
        [HttpGet]
        public IActionResult RegisterReport() => View();

        [AdminAuthorize]
        [HttpGet]
        public IActionResult OneSessionReport() => View();

        [AdminAuthorize]
        [HttpGet]
        public IActionResult FinanceReport() => View();

        // ============================================================
        //  API — احراز هویت مدیر (با قفل + پیام عمومی)
        // ============================================================

        [HttpPost]
        [Route("api/Admin/Login")]
        [AllowAnonymous]
        public async Task<IActionResult> LoginApi([FromBody] AdminLoginRequest req)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var result = await _auth.LoginAsync(req, ip);

            if (!result.Success)
                return Ok(result);

            Response.Cookies.Delete("X-Access-Token");

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
        //  تغییر رمز عبور ادمین (با DTO خودت: OldPassword + ConfirmPassword)
        // ============================================================

      

        [AdminAuthorize]
        [HttpPost]
        [Route("api/Admin/ChangePassword")]
        public async Task<IActionResult> ChangePasswordApi([FromBody] ChangePasswordRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.OldPassword) ||
                string.IsNullOrWhiteSpace(req.NewPassword))
            {
                return Ok(new { success = false, message = "رمز قدیم و جدید را وارد کنید." });
            }

            // ✅ چک کردن مطابقت رمز جدید و تکرار (علاوه بر [Compare] که روی ModelState کار می‌کنه)
            if (req.NewPassword != req.ConfirmPassword)
            {
                return Ok(new { success = false, message = "رمز جدید و تکرار آن مطابقت ندارد." });
            }

            var uid = CurrentAdminUserID;
            if (uid == null)
                return Ok(new { success = false, message = "شناسه کاربر یافت نشد." });

            var (ok, msg) = await _auth.ChangePasswordAsync(uid.Value, req.OldPassword, req.NewPassword, req.ConfirmPassword);
            return Ok(new { success = ok, message = msg });
        }

        // ============================================================
        //  خروج مدیر
        // ============================================================

        [AdminAuthorize]
        [HttpGet]
        [Route("Admin/Logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("X-Admin-Token");
            Response.Cookies.Delete("X-Access-Token");
            return RedirectToAction("Login", "Account");
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

        // ============================================================
        //  API — پیام‌ها
        // ============================================================

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

        [AdminAuthorize]
        [Route("Admin/Messages")]
        public IActionResult Messages()
        {
            ViewData["Title"] = "پیام‌ها";
            return View();
        }

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/Roles")]
        public async Task<IActionResult> RolesApi()
            => Ok(new { success = true, data = await _messages.GetRoleOptionsAsync() });

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/SportCategories")]
        public async Task<IActionResult> AdminSportCategoriesApi()
            => Ok(new { success = true, data = await _messages.GetSportOptionsAsync() });

        [AdminAuthorize]
        [HttpGet]
        [Route("api/Admin/SearchMember")]
        public async Task<IActionResult> SearchMemberApi(string q)
            => Ok(new { success = true, data = await _messages.SearchMembersAsync(AdminShiftID, q) });

        [AdminAuthorize]
        [Route("Admin/Messages/View/{messageID:long}")]
        public async Task<IActionResult> MessageView(long messageID)
        {
            await _messages.MarkSeenAsync(messageID);
            var msg = await _messages.GetMessageDetailAsync(messageID);
            if (msg == null) return RedirectToAction("Messages");
            return View(msg);
        }

        [AdminAuthorize]
        [HttpGet]
        [Route("Admin/Face/{personID:int}")]
        public async Task<IActionResult> Face(int personID)
        {
            var bytes = await _profile.GetPersonFaceAsync(personID);
            if (bytes == null || bytes.Length == 0)
                return NotFound();

            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(bytes, ProfileService.DetectImageType(bytes));
        }

        [AdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> CoachMessages(string? from, string? to)
        {
            var shiftID = AdminShiftID;

            if (string.IsNullOrEmpty(from))
            {
                var pc = new System.Globalization.PersianCalendar();
                var thirtyDaysAgo = DateTime.Now.AddDays(-30);
                from = $"{pc.GetYear(thirtyDaysAgo):0000}/{pc.GetMonth(thirtyDaysAgo):00}/{pc.GetDayOfMonth(thirtyDaysAgo):00}";
            }
            if (string.IsNullOrEmpty(to))
            {
                var pc = new System.Globalization.PersianCalendar();
                var now = DateTime.Now;
                to = $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00}";
            }

            ViewBag.FromDate = from;
            ViewBag.ToDate = to;

            var model = await _reports.GetCoachStudentMessagesReportAsync(shiftID, from, to);
            return View(model);
        }
    }
}