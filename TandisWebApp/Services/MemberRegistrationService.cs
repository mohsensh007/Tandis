using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس ثبت‌نام عضو جدید
    /// منطق معادل ثبت‌نام در کیوسک (UscMemberInfo)
    /// </summary>
    public class MemberRegistrationService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<MemberRegistrationService> _logger;

        public MemberRegistrationService(
            FullSportDbContext db,
            CommonHelperService helper,
            ILogger<MemberRegistrationService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        /// <summary>ثبت‌نام کامل عضو جدید</summary>
        public async Task<ApiResponse<RegisterMemberResponse>> RegisterAsync(RegisterMemberRequest req)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // === ۱. ولیدیشن کد ملی: فقط ۱۰ رقم + تکراری نبودن ===
                var nc = (req.NationalCode ?? "").Trim();

                if (nc.Length != 10 || !nc.All(char.IsDigit))
                    return new ApiResponse<RegisterMemberResponse> { Success = false, Message = "کد ملی باید دقیقاً ۱۰ رقم باشد" };

                if (await _db.Gen_Persons.AnyAsync(p => p.NationalCode == nc))
                    return new ApiResponse<RegisterMemberResponse> { Success = false, Message = "این کد ملی قبلاً ثبت شده است" };

                if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
                    return new ApiResponse<RegisterMemberResponse> { Success = false, Message = "نام و نام خانوادگی الزامی است" };

                if (string.IsNullOrWhiteSpace(req.Mobile) || req.Mobile.Length != 11 || !req.Mobile.StartsWith("09"))
                    return new ApiResponse<RegisterMemberResponse> { Success = false, Message = "شماره موبایل نامعتبر است" };

                if (string.IsNullOrWhiteSpace(req.BirthDate))
                    return new ApiResponse<RegisterMemberResponse> { Success = false, Message = "تاریخ تولد الزامی است" };

                // === ۲. پیدا کردن ShiftID بر اساس جنسیت ===
                short shiftID;
                if (req.Gender) // آقا
                {
                    shiftID = await _db.Gen_Shifts
                        .Where(s => s.ShiftDesc != null && s.ShiftDesc.Contains("مرد"))
                        .Select(s => s.ShiftID)
                        .FirstOrDefaultAsync();
                    if (shiftID == 0) shiftID = 1; // fallback
                }
                else // خانم
                {
                    shiftID = await _db.Gen_Shifts
                        .Where(s => s.ShiftDesc != null && s.ShiftDesc.Contains("زن"))
                        .Select(s => s.ShiftID)
                        .FirstOrDefaultAsync();
                    if (shiftID == 0) shiftID = 2; // fallback
                }

                // === ۳. ساخت Gen_Person ===
                var person = new Gen_Person
                {
                    FirstName = req.FirstName.Trim(),
                    LastName = req.LastName.Trim(),
                    NationalCode = nc,
                    BirthDate = req.BirthDate.Trim(),
                    Mobile = req.Mobile.Trim(),
                    Gender = req.Gender,
                    ShiftID = shiftID,
                    UserID = 1,
                    CreationDate = _helper.GetToday(),
                    CreationTime = _helper.GetThisTime()
                };
                _db.Gen_Persons.Add(person);
                await _db.SaveChangesAsync();  // PersonID تولید می‌شه (Identity)

                // === ۴. ساخت Gen_Member ===
                // MemberID با DatabaseGeneratedOption.None تعریف شده → باید دستی Max+1 بشه
                var maxMemberID = await _db.Gen_Members
                    .AsNoTracking()
                    .MaxAsync(m => (int?)m.MemberID) ?? 0;
                var newMemberID = maxMemberID + 1;

                var member = new Gen_Member
                {
                    MemberID = newMemberID,
                    PersonID = person.PersonID,
                    RoleID = 1,              // عضو عادی
                    ShiftID = shiftID,
                    UserID = 1,
                    KioskPass = nc,  // رمز پیش‌فرض = کد ملی
                    MembershipDate = _helper.GetToday(),
                    MembershipTime = _helper.GetThisTime(),
                    IsBlackList = false,
                    HasFinger = false,
                    RegDiscount = 0,     
                    BoxRadifNo = 0
                    
                };
                _db.Gen_Members.Add(member);

                // ⚠️ Kiosk_ChangePassLogs رکورد اضافه نمی‌کنیم → mustChangePassword خودکار true می‌شه
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                _logger.LogInformation("عضو جدید ثبت شد: MemberID={MemberID}, NationalCode={NC}",
                     newMemberID, nc);

                return new ApiResponse<RegisterMemberResponse>
                {
                    Success = true,
                    Message = "ثبت‌نام با موفقیت انجام شد",
                    Data = new RegisterMemberResponse
                    {
                        MemberID = newMemberID,
                        NationalCode = nc,
                        DefaultPassword = nc
                    }
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در ثبت‌نام عضو جدید با کد ملی {NC}", req.NationalCode);
                return new ApiResponse<RegisterMemberResponse> { Success = false, Message = "خطا در ثبت اطلاعات. لطفاً دوباره تلاش کنید." };
            }
        }
    }
}