using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Helpers;

namespace TandisWebApp.Services
{
    // ============================================================
    //  لایه ارائه‌دهنده مدیران
    // ============================================================

    public interface IAdminUserProvider
    {
        Task<AdminUserInfo?> FindAsync(string username, string password);
    }

    public sealed class AdminUserInfo
    {
        public int UserID { get; set; }
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public short ShiftID { get; set; }
        public bool IsValid { get; set; }
        public bool MustChangePassword { get; set; }
    }

    // ============================================================
    //  پیاده‌سازی مبتنی بر دیتابیس (Sec_Users)
    // ============================================================
    public class DbAdminUserProvider : IAdminUserProvider
    {
        private readonly FullSportDbContext _db;
        public DbAdminUserProvider(FullSportDbContext db) { _db = db; }

        public async Task<AdminUserInfo?> FindAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            username = username.Trim();

            var user = await _db.Sec_Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName == username);
            if (user == null) return null;

            var passOk = SecurityHelper.VerifyPassword(password, user.UPassword);

            // اجبار تغییر رمز برای admin/admin (پیش‌فرض ناامن)
            var mustChange = passOk
                             && username.Equals("admin", StringComparison.OrdinalIgnoreCase)
                             && password == "admin";

            return new AdminUserInfo
            {
                UserID = user.UserID,
                Username = user.UserName ?? "",
                DisplayName = user.UserName ?? "",
                ShiftID = user.ShiftID ?? 0,
                IsValid = passOk && user.IsActive == true && user.IsAdmin == true,
                MustChangePassword = mustChange
            };
        }
    }

    // ============================================================
    //  سرویس احراز هویت مدیر
    // ============================================================

    public class AdminLoginResponse
    {
        public string Token { get; set; } = "";
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public short ShiftID { get; set; }
        public bool MustChangePassword { get; set; }
    }

    public class AdminAuthService
    {
        private readonly IAdminUserProvider _provider;
        private readonly JwtService _jwt;
        private readonly ILogger<AdminAuthService> _logger;
        private readonly FullSportDbContext _db;

        public AdminAuthService(
            IAdminUserProvider provider,
            JwtService jwt,
            ILogger<AdminAuthService> logger,
            FullSportDbContext db)
        {
            _provider = provider;
            _jwt = jwt;
            _logger = logger;
            _db = db;
        }

        /// <summary>ورود مدیر و تولید توکن JWT (با قفل + پیام عمومی)</summary>
        public async Task<ApiResponse<AdminLoginResponse>> LoginAsync(AdminLoginRequest req, string ip = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrEmpty(req.Password))
                {
                    return new ApiResponse<AdminLoginResponse>
                    {
                        Success = false,
                        Message = "نام کاربری و رمز عبور را وارد کنید."
                    };
                }

                // ✅ استفاده از SecurityHelper برای قفل
                if (SecurityHelper.IsLocked(req.Username, ip))
                {
                    return new ApiResponse<AdminLoginResponse>
                    {
                        Success = false,
                        Message = $"به دلیل تلاش‌های ناموفق متعدد، حساب به مدت {SecurityHelper.RemainingMinutes(req.Username, ip)} دقیقه قفل است."
                    };
                }

                var admin = await _provider.FindAsync(req.Username, req.Password);

                // ✅ پیام عمومی (جلوگیری از User Enumeration)
                if (admin == null || !admin.IsValid)
                {
                    SecurityHelper.RecordFail(req.Username, ip);
                    return new ApiResponse<AdminLoginResponse>
                    {
                        Success = false,
                        Message = "نام کاربری یا رمز عبور اشتباه است."
                    };
                }

                SecurityHelper.ClearFails(req.Username, ip);

                // Cast به short برای GenerateAdminToken
                var token = _jwt.GenerateAdminToken((short)admin.UserID, admin.Username, admin.DisplayName, admin.ShiftID);

                return new ApiResponse<AdminLoginResponse>
                {
                    Success = true,
                    Message = "ورود مدیر موفقیت‌آمیز بود",
                    Data = new AdminLoginResponse
                    {
                        Token = token,
                        Username = admin.Username,
                        DisplayName = admin.DisplayName,
                        ShiftID = admin.ShiftID,
                        MustChangePassword = admin.MustChangePassword
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

        /// <summary>تغییر رمز عبور مدیر</summary>
        public async Task<(bool ok, string msg)> ChangePasswordAsync(int userID, string oldPass, string newPass, string confirmPass)
        {
            if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < 8)
                return (false, "رمز جدید باید حداقل ۸ کاراکتر باشد.");
            if (newPass == oldPass)
                return (false, "رمز جدید نباید با رمز فعلی یکسان باشد.");
            if (newPass != confirmPass)
                return (false, "رمز جدید و تکرار آن یکسان نیست.");

            var user = await _db.Sec_Users.FirstOrDefaultAsync(u => u.UserID == userID);
            if (user == null) return (false, "کاربر یافت نشد.");
            if (!SecurityHelper.VerifyPassword(oldPass, user.UPassword))
                return (false, "رمز فعلی اشتباه است.");

            user.UPassword = SecurityHelper.Encrypt(newPass);
            await _db.SaveChangesAsync();
            return (true, "رمز عبور با موفقیت تغییر کرد.");
        }
    }
}