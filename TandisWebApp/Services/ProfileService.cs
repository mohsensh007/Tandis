using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس پروفایل عضو - نمایش و ویرایش اطلاعات
    /// معادل UscUserProfile در کیوسک
    /// </summary>
    public class ProfileService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<ProfileService> _logger;

        public ProfileService(FullSportDbContext db, CommonHelperService helper, ILogger<ProfileService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        /// <summary>دریافت پروفایل کامل عضو</summary>
        public async Task<ApiResponse<MemberProfileDto>> GetProfileAsync(int memberID)
        {
            try
            {
                var member = await (
                    from m in _db.Gen_Members
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where m.MemberID == memberID
                    select new { m, p }
                ).FirstOrDefaultAsync();

                if (member == null)
                    return new ApiResponse<MemberProfileDto> { Success = false, Message = "عضو یافت نشد" };

                // اعتبارات و بدهی
                var sportCredit = await _helper.GetSportCreditAmountAsync(memberID);
                var buffetCredit = await _helper.GetBuffetCreditAmountAsync(memberID);
                var serviceCredit = await _helper.GetServiceCreditAmountAsync(memberID);
                var totalDebit = await _helper.GetDebitAmountAsync(memberID);

                var dto = new MemberProfileDto
                {
                    MemberID = memberID,
                    FullName = member.p.FullName ?? $"{member.p.FirstName} {member.p.LastName}",
                    NationalCode = member.p.NationalCode,
                    Mobile = member.p.Mobile,
                    BirthDate = member.p.BirthDate,
                    Gender = member.p.Gender,
                    SportCredit = sportCredit,
                    BuffetCredit = buffetCredit,
                    ServiceCredit = serviceCredit,
                    TotalDebit = totalDebit
                };

                return new ApiResponse<MemberProfileDto> { Success = true, Message = "", Data = dto };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در دریافت پروفایل عضو {MemberID}", memberID);
                return new ApiResponse<MemberProfileDto> { Success = false, Message = "خطا در پردازش درخواست" };
            }
        }

        /// <summary>آپلود عکس پروفایل (Base64)</summary>
        public async Task<SimpleResponse> UpdatePhotoAsync(int memberID, string photoBase64)
        {
            try
            {
                var member = await (
                    from m in _db.Gen_Members
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where m.MemberID == memberID
                    select p
                ).FirstOrDefaultAsync();

                if (member == null)
                    return new SimpleResponse { Success = false, Message = "عضو یافت نشد" };

                // حذف پیشوند data:image/...;base64,
                var commaIdx = photoBase64.IndexOf(',');
                if (commaIdx >= 0)
                    photoBase64 = photoBase64.Substring(commaIdx + 1);

                member.PersonImage = Convert.FromBase64String(photoBase64);
                await _db.SaveChangesAsync();

                return new SimpleResponse { Success = true, Message = "عکس پروفایل با موفقیت ذخیره شد" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود عکس پروفایل عضو {MemberID}", memberID);
                return new SimpleResponse { Success = false, Message = "خطا در ذخیره عکس" };
            }
        }

        /// <summary>ویرایش اطلاعات اولیه (نام، موبایل، تاریخ تولد)</summary>
        public async Task<SimpleResponse> UpdateInfoAsync(int memberID, string? firstName, string? lastName, string? mobile, string? birthDate)
        {
            try
            {
                var person = await (
                    from m in _db.Gen_Members
                    join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                    where m.MemberID == memberID
                    select p
                ).FirstOrDefaultAsync();

                if (person == null)
                    return new SimpleResponse { Success = false, Message = "عضو یافت نشد" };

                if (!string.IsNullOrWhiteSpace(firstName))
                    person.FirstName = firstName.Trim();
                if (!string.IsNullOrWhiteSpace(lastName))
                    person.LastName = lastName.Trim();
                if (!string.IsNullOrWhiteSpace(mobile))
                    person.Mobile = mobile.Trim();
                if (!string.IsNullOrWhiteSpace(birthDate))
                    person.BirthDate = birthDate.Trim();

                await _db.SaveChangesAsync();

                return new SimpleResponse { Success = true, Message = "اطلاعات پروفایل با موفقیت ذخیره شد" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ویرایش پروفایل عضو {MemberID}", memberID);
                return new SimpleResponse { Success = false, Message = "خطا در ذخیره اطلاعات" };
            }
        }
        // ========== تصویر چهره عضو ==========
        public async Task<byte[]?> GetMemberFaceAsync(int memberID)
        {
            return await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == memberID
                select p.ThumbnailImage
            ).FirstOrDefaultAsync();
        }

        // ========== تصویر چهره هر شخص (برای ادمین) ==========
        public async Task<byte[]?> GetPersonFaceAsync(int personID)
        {
            return await _db.Gen_Persons
                .Where(p => p.PersonID == personID)
                .Select(p => p.ThumbnailImage)
                .FirstOrDefaultAsync();
        }

        // ========== تشخیص نوع تصویر از روی بایت‌های اول ==========
        public static string DetectImageType(byte[] b)
        {
            if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
            if (b.Length > 7 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
            if (b.Length > 1 && b[0] == 0x42 && b[1] == 0x4D) return "image/bmp";
            return "image/jpeg";
        }
        // ========== آیا عضو عکس چهره دارد؟ ==========
        public async Task<bool> HasMemberFaceAsync(int memberID)
        {
            var bytes = await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == memberID
                select p.ThumbnailImage
            ).FirstOrDefaultAsync();

            return bytes != null && bytes.Length > 0;
        }
    }
}
