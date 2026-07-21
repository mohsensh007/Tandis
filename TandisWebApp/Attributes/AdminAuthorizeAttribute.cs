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
            // اگر کاربر اصلاً احراز هویت نشده، [Authorize] خودش هندل می‌کند
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
                return;

            var isAdmin = context.HttpContext.User.FindFirstValue("IsAdmin");
            if (isAdmin != "true")
            {
                // کاربر عضو عادی است — اجازه ورود به پنل ادمین ندارد
                context.Result = new ForbidResult();
            }
        }
    }

    /// <summary>
    /// Extension: خواندن AdminShiftID از Claims
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
}
