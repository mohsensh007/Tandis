using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TandisWebApp.Attributes
{
    /// <summary>
    /// فقط مدیران اجازه دسترسی دارند.
    /// Claim «IsAdmin» باید «true» باشد.
    /// </summary>
    public class AdminAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Skip admin check for login API path (case-insensitive)
            var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant();
            if (path != null && (path.Contains("/api/admin/login") || path.Equals("/api/admin/login")))
                return;

            // اگر AllowAnonymous روی اکشن یا کنترلر تنظیم شده، بررسی ادمین را رد کن
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
                return;

            // اگر کاربر اصلاً احراز هویت نشده، [Authorize] خودش هندل می‌کند
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
                return;

            var isAdmin = context.HttpContext.User.FindFirstValue("IsAdmin");
            if (isAdmin != "true")
            {
                // کاربر عضو عادی است — اجازه ورود به پنل ادمین ندارد
                // استفاده از StatusCodeResult به جای ForbidResult تا Challenge (ریدایرکت به لاگین) فعال نشود
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            }
        }
    }

    /// <summary>
    /// فقط اعضا (کاربران عضو) اجازه دسترسی دارند.
    /// مدیران (IsAdmin == true) به این بخش‌ها دسترسی ندارند.
    /// </summary>
    public class MemberAuthorizeAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // اگر AllowAnonymous روی اکشن یا کنترلر تنظیم شده، بررسی عضو را رد کن
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
                return;

            // اگر کاربر اصلاً احراز هویت نشده، [Authorize] خودش هندل می‌کند (چالش/چالش‌دهی)
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
                return;

            // اگر کاربر مدیر است، اجازه دسترسی به صفحات عضو را نده
            var isAdmin = context.HttpContext.User.FindFirstValue("IsAdmin");
            if (isAdmin == "true")
            {
                // مدیر در حال تلاش برای دسترسی به بخش اعضا است
                // برگرداندن 403 برای درخواست‌های API
                var isApi = context.HttpContext.Request.Path.StartsWithSegments("/api") ||
                            context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                            context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json");

                if (isApi)
                {
                    context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                }
                else
                {
                    // برای درخواست‌های MVC، ریدایرکت به پنل ادمین
                    context.Result = new RedirectResult("/Admin");
                }
                return;
            }

            // بررسی وجود MemberID (برای اطمینان از اینکه کاربر عضو واقعی است)
            var memberID = context.HttpContext.User.FindFirstValue("MemberID");
            if (string.IsNullOrEmpty(memberID) || memberID == "0")
            {
                // توکن معتبر دارد اما MemberID ندارد (احتمالاً توکن ادمین اشتباه خوانده شده)
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            }
        }
    }

    /// <summary>
    /// Extension methods برای Claims ادمین
    /// </summary>
    public static class AdminClaimExtensions
    {
        /// <summary>خواندن ShiftID مدیر از Claim (پیش‌فرض: 1)</summary>
        public static short GetAdminShiftID(this ClaimsPrincipal user)
        {
            var val = user.FindFirstValue("AdminShiftID");
            return short.TryParse(val, out var id) ? id : (short)1;
        }

        /// <summary>خواندن نام کاربری مدیر از Claim</summary>
        public static string GetAdminUsername(this ClaimsPrincipal user)
        {
            return user.FindFirstValue("AdminUsername") ?? "";
        }
    }

    /// <summary>
    /// Extension methods برای Claims اعضا
    /// </summary>
    public static class MemberClaimExtensions
    {
        /// <summary>خواندن MemberID از Claim (پیش‌فرض: 0)</summary>
        public static int GetMemberID(this ClaimsPrincipal user)
        {
            var val = user.FindFirstValue("MemberID");
            return int.TryParse(val, out var id) ? id : 0;
        }

        /// <summary>خواندن ShiftID عضو از Claim (پیش‌فرض: 1)</summary>
        public static short GetShiftID(this ClaimsPrincipal user)
        {
            var val = user.FindFirstValue("ShiftID");
            return short.TryParse(val, out var id) ? id : (short)1;
        }

        /// <summary>خواندن نام کامل عضو از Claim</summary>
        public static string GetFullName(this ClaimsPrincipal user)
        {
            return user.FindFirstValue("FullName") ?? "";
        }
    }
}
