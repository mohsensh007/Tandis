using TandisWebApp.DTOs;

namespace TandisWebApp.Services
{
    // ============================================================
    //  لایه ارائه‌دهنده مدیران — قابل تعویض با دیتابیس در آینده
    // ============================================================

    /// <summary>
    /// ارائه‌دهنده مدیران سیستم.
    /// فعلاً پیاده‌سازی hardcoded دارد اما رابط ثابت است؛
    /// در آینده فقط کافی است یک پیاده‌سازی جدید (مثلاً DbAdminUserProvider)
    /// در DI ثبت شود و این پیاده‌سازی حذف گردد.
    /// </summary>
    public interface IAdminUserProvider
    {
        /// <summary>جستجوی مدیر بر اساس نام کاربری و رمز عبور. null = یافت نشد.</summary>
        Task<AdminUserInfo?> FindAsync(string username, string password);
    }

    /// <summary>مدل اطلاعات یک مدیر (از منبع داده خوانده می‌شود).</summary>
    public sealed class AdminUserInfo
    {
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public short ShiftID { get; set; }
    }

    /// <summary>
    /// پیاده‌سازی ثابت (hardcoded) مدیران.
    /// بعداً با یک پیاده‌سازی مبتنی بر دیتابیس جایگزین می‌شود.
    /// </summary>
    public class HardcodedAdminUserProvider : IAdminUserProvider
    {
        // لیست ثابت مدیران — در آینده از جدول Sec_Users یا مشابه آن خوانده می‌شود.
        private static readonly AdminUserInfo[] _admins =
        {
            new() { Username = "admin",  DisplayName = "مدیر شیفت ۱ (آقایان)", ShiftID = 1 },
            new() { Username = "adminb", DisplayName = "مدیر شیفت ۲ (بانوان)",  ShiftID = 2 }
        };

        // رمز عبور فعلی ثابت — بعداً از دیتابیس خوانده می‌شود.
        private const string DefaultPassword = "admin";

        public Task<AdminUserInfo?> FindAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || password == null)
                return Task.FromResult<AdminUserInfo?>(null);

            var match = _admins.FirstOrDefault(a =>
                string.Equals(a.Username, username.Trim(), StringComparison.OrdinalIgnoreCase)
                && password == DefaultPassword);

            return Task.FromResult(match);
        }
    }

    // ============================================================
    //  سرویس احراز هویت مدیر
    // ============================================================

    /// <summary>
    /// احراز هویت مدیران و صدور توکن JWT مجزا از توکن اعضا.
    /// توکن ادمین دارای Claim های IsAdmin و AdminShiftID است.
    /// </summary>
    public class AdminAuthService
    {
        private readonly IAdminUserProvider _provider;
        private readonly JwtService _jwt;
        private readonly ILogger<AdminAuthService> _logger;

        public AdminAuthService(
            IAdminUserProvider provider,
            JwtService jwt,
            ILogger<AdminAuthService> logger)
        {
            _provider = provider;
            _jwt = jwt;
            _logger = logger;
        }

        /// <summary>ورود مدیر و تولید توکن JWT.</summary>
        public async Task<ApiResponse<AdminLoginResponse>> LoginAsync(AdminLoginRequest req)
        {
            try
            {
                var admin = await _provider.FindAsync(req.Username, req.Password);
                if (admin == null)
                {
                    return new ApiResponse<AdminLoginResponse>
                    {
                        Success = false,
                        Message = "نام کاربری یا رمز عبور مدیر صحیح نیست"
                    };
                }

                var token = _jwt.GenerateAdminToken(admin.Username, admin.DisplayName, admin.ShiftID);

                return new ApiResponse<AdminLoginResponse>
                {
                    Success = true,
                    Message = "ورود مدیر موفقیت‌آمیز بود",
                    Data = new AdminLoginResponse
                    {
                        Token = token,
                        Username = admin.Username,
                        DisplayName = admin.DisplayName,
                        ShiftID = admin.ShiftID
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ورود مدیر {Username}", req.Username);
                return new ApiResponse<AdminLoginResponse>
                {
                    Success = false,
                    Message = "خطا در پردازش درخواست"
                };
            }
        }
    }
}
