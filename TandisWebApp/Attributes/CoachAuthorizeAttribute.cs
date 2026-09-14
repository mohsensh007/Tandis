using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using TandisWebApp.Data;

namespace TandisWebApp.Attributes
{
    /// <summary>
    /// فقط به مربی‌ها (RoleID = 2) اجازه دسترسی می‌ده
    /// </summary>
    public class CoachAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            var memberID = int.Parse(user.FindFirstValue("MemberID") ?? "0");

            if (memberID == 0)
            {
                context.Result = new RedirectResult("/Account/Login");
                return;
            }

            int roleID = 0;

            // ۱) اول از Claim (اگه موقع لاگین ست شده باشه)
            var roleClaim = user.FindFirstValue("RoleID");
            if (!string.IsNullOrEmpty(roleClaim))
            {
                int.TryParse(roleClaim, out roleID);
            }
            else
            {
                // ۲) وگرنه از دیتابیس (Fallback مطمئن)
                var db = context.HttpContext.RequestServices.GetRequiredService<FullSportDbContext>();
                roleID = db.Gen_Members
                    .Where(m => m.MemberID == memberID)
                    .Select(m => m.RoleID)
                    .FirstOrDefault() ?? 0;
            }

            // فقط نقش مربی
            if (roleID != 2)
            {
                context.Result = new RedirectResult("/Home");
                return;
            }
        }
    }
}