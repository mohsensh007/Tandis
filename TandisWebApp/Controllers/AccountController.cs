using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TandisWebApp.DTOs;
using TandisWebApp.Services;

namespace TandisWebApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly MemberAuthService _auth;
        private readonly CommonHelperService _helper;

        public AccountController(MemberAuthService auth, CommonHelperService helper)
        {
            _auth = auth;
            _helper = helper;
        }

        // ============================================================
        // Views
        // ============================================================

        /// <summary>صفحه لاگین</summary>
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        /// <summary>خروج</summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // حذف هر دو کوکی تا session کاملاً پاک شود
            Response.Cookies.Delete("X-Access-Token");
            Response.Cookies.Delete("X-Admin-Token");
            return RedirectToAction("Login");
        }

        // ============================================================
        // API
        // ============================================================

        /// <summary>ورود با کد ملی و رمز</summary>
        [HttpPost]
        [Route("api/Account/Login")]
        public async Task<IActionResult> LoginApi([FromBody] LoginRequest req)
        {
            var result = await _auth.LoginAsync(req);
            if (!result.Success)
                return BadRequest(result);

            // حذف کوکی ادمین (اگر کاربر قبلاً به‌عنوان مدیر وارد شده بوده)
            // تا توکن عضو اولویت پیدا کند و Session مدیر با عضو تداخل نکند.
            Response.Cookies.Delete("X-Admin-Token");

            // ذخیره توکن در کوکی
            Response.Cookies.Append("X-Access-Token", result.Data!.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // در production true کنید
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(24)
            });

            return Ok(result);
        }

        /// <summary>تغییر رمز عبور</summary>
        [Authorize]
        [HttpPost]
        [Route("api/Account/ChangePassword")]
        public async Task<IActionResult> ChangePasswordApi([FromBody] ChangePasswordRequest req)
        {
            var memberID = int.Parse(User.FindFirstValue("MemberID") ?? "0");
            var result = await _auth.ChangePasswordAsync(memberID, req);
            if (!result.Success)
                return BadRequest(result);
            return Ok(result);
        }
    }
}
