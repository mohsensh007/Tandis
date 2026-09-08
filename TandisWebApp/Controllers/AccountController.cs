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
        private readonly MessageService _messages;

        


        public AccountController(MemberAuthService auth, CommonHelperService helper ,MessageService messages)
        {
            _auth = auth;
            _helper = helper;
            _messages = messages;
        }

        // ============================================================
        // Views
        // ============================================================

        /// <summary>صفحه لاگین</summary>
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl ?? "/Home";
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
        public async Task<IActionResult> LoginApi([FromBody] LoginRequest req, string? returnUrl = null)
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

            // returnUrl برای ریدایرکت در فرانت‌اند
            result.Data.ReturnUrl = string.IsNullOrEmpty(returnUrl) ? "/Home" : returnUrl;

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
        private Task<(int memberID, int roleID)> GetCurrentMemberAsync()
        => _messages.ResolveCurrentMemberAsync(
        User.FindFirst("MemberID")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
        User.Identity?.Name);


        [Authorize]
        [HttpGet]
        [Route("api/Account/Messages")]
        public async Task<IActionResult> MyMessagesApi()
        {
            var (memberID, roleID) = await GetCurrentMemberAsync();
            if (memberID == 0) return Ok(new { success = false, message = "عضو پیدا نشد" });
            return Ok(new { success = true, data = await _messages.GetMemberMessagesAsync(memberID, roleID) });
        }

        [Authorize]
        [HttpGet]
        [Route("api/Account/Messages/UnreadCount")]
        public async Task<IActionResult> MyUnreadApi()
        {
            var (memberID, roleID) = await GetCurrentMemberAsync();
            if (memberID == 0) return Ok(new { success = true, count = 0 });
            return Ok(new { success = true, count = await _messages.GetMemberUnreadCountAsync(memberID, roleID) });
        }

        [Authorize]
        [HttpPost]
        [Route("api/Account/Messages/Read")]
        public async Task<IActionResult> MarkReadApi([FromBody] long messageID)
        {
            var (memberID, _) = await GetCurrentMemberAsync();
            if (memberID == 0) return Ok(new { success = false });
            await _messages.MarkReadAsync(messageID, memberID);
            return Ok(new { success = true });
        }

        [Authorize]
        [HttpPost]
        [Route("api/Account/Messages/Send")]
        public async Task<IActionResult> SendToAdminApi([FromBody] SendMessageRequest req)
        {
            var (memberID, _) = await GetCurrentMemberAsync();
            if (memberID == 0) return Ok(new { success = false, message = "عضو پیدا نشد" });
            if (string.IsNullOrWhiteSpace(req.Body))
                return Ok(new { success = false, message = "متن پیام خالی است" });
            await _messages.SendFromMemberAsync(memberID, req.Title, req.Body);
            return Ok(new { success = true, message = "پیام شما برای مدیریت ارسال شد" });
        }
    }
}
