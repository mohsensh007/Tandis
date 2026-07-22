using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.Attributes;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    [AdminAuthorize]
    public class AdminController : Controller
    {
        private readonly AdminAuthService _auth;
        private readonly AdminReportService _reports;

        public AdminController(AdminAuthService auth, AdminReportService reports)
        {
            _auth = auth;
            _reports = reports;
        }

        private short AdminShiftID => User.GetAdminShiftID();

        // ============================================================
        //  Views
        // ============================================================

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult TrafficReport()
        {
            return View();
        }

        [HttpGet]
        public IActionResult RegisterReport()
        {
            return View();
        }

        [HttpGet]
        public IActionResult OneSessionReport()
        {
            return View();
        }

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

        [HttpGet]
        [Route("api/Admin/TrafficReport")]
        public async Task<IActionResult> TrafficReportApi(string? from, string? to)
        {
            var response = await _reports.GetTrafficReportAsync(AdminShiftID, from, to);
            return Ok(new { success = true, data = response.Data, summary = response.Summary });
        }

        [HttpGet]
        [Route("api/Admin/RegisterReport")]
        public async Task<IActionResult> RegisterReportApi(string? from, string? to, string mode = "both")
        {
            var response = await _reports.GetRegisterReportAsync(AdminShiftID, from, to, mode);
            return Ok(new { success = true, data = response.Data, summary = response.Summary });
        }

        [HttpGet]
        [Route("api/Admin/OneSessionReport")]
        public async Task<IActionResult> OneSessionReportApi(string? from, string? to)
        {
            var response = await _reports.GetOneSessionReportAsync(AdminShiftID, from, to);
            return Ok(new { success = true, data = response.Data, summary = response.Summary });
        }

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

        [HttpGet]
        [Route("Admin/Logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("X-Admin-Token");
            return RedirectToAction("Login", "Account");
        }
    }
}
