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
                // جستجوی عضو با کد ملی و رمز
                var member = await (
                    from m in _db.Gen_Members
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where p.NationalCode == req.NationalCode
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

                // تولید توکن JWT
                var token = _jwt.GenerateToken(
                    member.m.MemberID,
                    member.p.FullName ?? $"{member.p.FirstName} {member.p.LastName}",
                    member.p.Mobile,
                    member.m.ShiftID
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
