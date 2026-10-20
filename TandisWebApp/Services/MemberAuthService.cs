using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// احراز هویت اعضا - ورود، تغییر رمز
    /// منطق ورود معادل UscLoginByCode در کیوسک
    /// </summary>
    public class MemberAuthService
    {
        private readonly FullSportDbContext _db;
        private readonly JwtService _jwt;
        private readonly CommonHelperService _helper;
        private readonly ILogger<MemberAuthService> _logger;

        public MemberAuthService(
            FullSportDbContext db,
            JwtService jwt,
            CommonHelperService helper,
            ILogger<MemberAuthService> logger)
        {
            _db = db;
            _jwt = jwt;
            _helper = helper;
            _logger = logger;
        }

        /// <summary>
        /// ورود با کد ملی + رمز (مثل کیوسک)
        /// </summary>
        public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest req)
        {
            try
            {
                if (req == null || string.IsNullOrWhiteSpace(req.NationalCode) || string.IsNullOrEmpty(req.Password))
                {
                    return new ApiResponse<LoginResponse>
                    {
                        Success = false,
                        Message = "کد ملی و رمز عبور را وارد کنید"
                    };
                }

                // ورودی موبایل ممکن است با ارقام فارسی/عربی ارسال شود.
                // کد ملی را فقط برای جست‌وجو normalize می‌کنیم؛ رمز عبور trim نمی‌شود.
                var rawNationalCode = req.NationalCode.Trim();
                var nationalCode = NormalizeDigits(rawNationalCode);

                // جستجوی عضو با کد ملی و رمز
                var member = await (
                    from m in _db.Gen_Members
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where (p.NationalCode == nationalCode || p.NationalCode == rawNationalCode)
                       && m.KioskPass == req.Password
                    select new { m, p }
                ).FirstOrDefaultAsync();

                if (member == null)
                {
                    return new ApiResponse<LoginResponse>
                    {
                        Success = false,
                        Message = "اطلاعات وارد شده صحیح نیست"
                    };
                }

                // آیا قبلاً رمز را تغییر داده است؟
                bool hasChangedPass = await _db.Kiosk_ChangePassLogs
                    .AnyAsync(x => x.MemberID == member.m.MemberID);
                var roleID = (int?)member.m.RoleID ?? 1;
                // تولید توکن JWT
                var token = _jwt.GenerateToken(
                    member.m.MemberID,
                    member.p.FullName ?? $"{member.p.FirstName} {member.p.LastName}",
                    member.p.Mobile,
                    member.m.ShiftID,
                    roleID
                );

                return new ApiResponse<LoginResponse>
                {
                    Success = true,
                    Message = "ورود موفقیت‌آمیز بود",
                    Data = new LoginResponse
                    {
                        Token = token,
                        MemberID = member.m.MemberID,
                        FullName = member.p.FullName ?? $"{member.p.FirstName} {member.p.LastName}",
                        Mobile = member.p.Mobile,
                        ShiftID = member.m.ShiftID ?? 1,
                        RoleID = roleID,
                        MustChangePassword = !hasChangedPass
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ورود کاربر با کد ملی {NationalCode}", req.NationalCode);
                return new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = "خطا در پردازش درخواست"
                };
            }
        }

        private static string NormalizeDigits(string value)
        {
            return value.Replace('۰', '0').Replace('۱', '1').Replace('۲', '2')
                .Replace('۳', '3').Replace('۴', '4').Replace('۵', '5')
                .Replace('۶', '6').Replace('۷', '7').Replace('۸', '8')
                .Replace('۹', '9').Replace('٠', '0').Replace('١', '1')
                .Replace('٢', '2').Replace('٣', '3').Replace('٤', '4')
                .Replace('٥', '5').Replace('٦', '6').Replace('٧', '7')
                .Replace('٨', '8').Replace('٩', '9');
        }

        /// <summary>
        /// تغییر رمز عبور
        /// منطق معادل BtnChangePass_Click در UscUserProfile
        /// </summary>
        public async Task<SimpleResponse> ChangePasswordAsync(int memberID, ChangePasswordRequest req)
        {
            try
            {
                var member = await _db.Gen_Members
                    .FirstOrDefaultAsync(x => x.MemberID == memberID);

                if (member == null)
                    return new SimpleResponse { Success = false, Message = "عضو یافت نشد" };

                // بررسی رمز قدیمی
                if (!string.Equals(member.KioskPass, req.OldPassword, StringComparison.Ordinal))
                    return new SimpleResponse { Success = false, Message = "رمز عبور قبلی صحیح نیست" };

                // تغییر رمز
                member.KioskPass = req.NewPassword;
                await _db.SaveChangesAsync();

                // ثبت لاگ تغییر رمز
                _db.Kiosk_ChangePassLogs.Add(new Kiosk_ChangePassLog
                {
                    MemberID = memberID,
                    ChangeDate = DateTime.Now
                });
                await _db.SaveChangesAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = "رمز عبور با موفقیت تغییر کرد"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تغییر رمز عضو {MemberID}", memberID);
                return new SimpleResponse { Success = false, Message = "خطا در پردازش درخواست" };
            }
        }

    }
}
