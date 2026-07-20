using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس ثبت‌نام و تمدید دوره‌های ورزشی
    /// منطق معادل UscRegister و UscRenewRegister در کیوسک
    /// </summary>
    public class RegisterService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<RegisterService> _logger;

        // UserID پیش‌فرض برای ثبت‌نام‌های وب
        private const short WEB_USER_ID = 1;

        public RegisterService(FullSportDbContext db, CommonHelperService helper, ILogger<RegisterService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        /// <summary>دریافت لیست دسته‌بندی ورزش‌ها (نمایش در کیوسک/وب)</summary>
        public async Task<List<SportCategoryDto>> GetSportCategoriesAsync(short shiftID)
        {
            return await _db.Gen_Sport_Categories
                .Where(c => c.ShiftID == shiftID && c.IsActive == true && c.ShowInKiosk == true)
                .OrderBy(c => c.SportName)
                .Select(c => new SportCategoryDto
                {
                    SportCatID = c.SportCatID,
                    SportName = c.SportName ?? ""
                })
                .ToListAsync();
        }

        /// <summary>
        /// لیست سانس‌های قابل ثبت‌نام (برای ثبت‌نام جدید)
        /// منطق معادل fillSportSanseList در UscRegister
        /// </summary>
        public async Task<List<SportSanseDto>> GetAvailableSansesAsync(short shiftID, int? sportCatID)
        {
            var query = from s in _db.Gen_SportSanses
                        where s.ShiftID == shiftID
                           && s.IsActive == true
                           && s.ShowInKiosk == true
                           && (sportCatID == null || sportCatID == 0 || s.SportCatID == sportCatID)
                        orderby s.SanseName
                        select new SportSanseDto
                        {
                            SportSanseID = s.SportSanseID,
                            SportName = s.Gen_Sport_Category.SportName ?? "",
                            SanseName = s.SanseName ?? "",
                            CoachName = s.Gen_Member.Gen_Person.FullName,
                            MembershipTypeDesc = s.Gen_MembershipType.MembershipTypeDesc,
                            SessionCountInPeriod = (int)(s.SessionCountInPeriod ?? 0),
                            TotalAmount = (long)(s.TotalAmount ?? 0),
                            PeriodDesc = s.Gen_Period.Description
                        };

            var list = await query.ToListAsync();
            foreach (var item in list)
                item.TotalAmountDisplay = _helper.SetSeprator(item.TotalAmount) + " ریال";

            return list;
        }

        /// <summary>
        /// لیست سانس‌ها برای تمدید (با نمایش وضعیت ثبت‌نام قبلی)
        /// منطق معادل fillSportSanseList در UscRenewRegister
        /// </summary>
        public async Task<List<SportSanseDto>> GetSansesForRenewAsync(int memberID, short shiftID, int? sportCatID)
        {
            // تخفیف مدیریتی عضو
            var member = await _db.Gen_Members.FirstOrDefaultAsync(m => m.MemberID == memberID);
            byte regDiscount = member?.RegDiscount ?? 0;

            // ثبت‌نام‌های قبلی این عضو
            var lastRegs = await (
                from ms in _db.Acc_MemberSports
                where ms.MemberID == memberID
                orderby ms.SportMemberID descending
                select ms
            ).ToListAsync();

            // مجموعه SportSanseIDهایی که ثبت‌نام فعال دارند (برای حذف ثبت‌نام‌های غیرفعال تکراری)
            var activeSanses = lastRegs.Where(r => r.IsActive == true).Select(r => r.SportSanseID ?? 0).ToHashSet();
            var toRemove = new HashSet<long>();
            foreach (var item in lastRegs)
            {
                if (item.IsActive == true)
                {
                    foreach (var d in lastRegs.Where(x => x.SportSanseID == item.SportSanseID && x.IsActive == false))
                        toRemove.Add(d.SportMemberID);
                }
            }

            // سانس‌های قابل نمایش
            var query = from s in _db.Gen_SportSanses
                        where s.ShiftID == shiftID
                           && s.IsActive == true
                           && s.ShowInKiosk == true
                           && s.Gen_Sport_Category.IsActive == true
                           && (sportCatID == null || sportCatID == 0 || s.SportCatID == sportCatID)
                        orderby s.SanseName
                        select new
                        {
                            s.SportSanseID,
                            SportName = s.Gen_Sport_Category.SportName ?? "",
                            SanseName = s.SanseName ?? "",
                            CoachName = s.Gen_Member.Gen_Person.FullName,
                            MembershipTypeDesc = s.Gen_MembershipType.MembershipTypeDesc,
                            s.SessionCountInPeriod,
                            TotalAmount = (long)(s.TotalAmount ?? 0),
                            PeriodDesc = s.Gen_Period.Description,
                            PeriodDayCount = (int)(s.Gen_Period.DayCount ?? 0)
                        };

            var sanses = await query.ToListAsync();

            var result = new List<SportSanseDto>();
            foreach (var s in sanses)
            {
                long totalAmount = regDiscount > 0
                    ? s.TotalAmount - (s.TotalAmount * regDiscount / 100)
                    : s.TotalAmount;

                var dto = new SportSanseDto
                {
                    SportSanseID = s.SportSanseID,
                    SportName = s.SportName,
                    SanseName = s.SanseName,
                    CoachName = s.CoachName,
                    MembershipTypeDesc = s.MembershipTypeDesc,
                    SessionCountInPeriod = s.SessionCountInPeriod ?? 0,
                    TotalAmount = totalAmount,
                    PeriodDesc = s.PeriodDesc,
                    TotalAmountDisplay = _helper.SetSeprator(totalAmount) + " ریال"
                };

                // بررسی ثبت‌نام قبلی
                var lastReg = lastRegs
                    .Where(r => !toRemove.Contains(r.SportMemberID) && r.SportSanseID == s.SportSanseID)
                    .FirstOrDefault();

                if (lastReg != null)
                {
                    dto.HasActiveRegister = lastReg.IsActive == true;
                    dto.ActiveStatus = lastReg.IsActive == true ? "فعال" : "غیرفعال";
                    dto.EndDate = lastReg.EndDate;
                    dto.RemainingSessions = lastReg.SessionCount;
                }

                result.Add(dto);
            }

            return result.OrderByDescending(o => o.HasActiveRegister).ToList();
        }

        /// <summary>
        /// ثبت‌نام جدید عضو در یک دوره (بدون پرداخت آنلاین - فقط ثبت)
        /// منطق معادل InsertNewRegister در UscRegister
        /// </summary>
        public async Task<ApiResponse<RegisterResponse>> RegisterAsync(int memberID, RegisterRequest req, short shiftID)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var sanse = await _db.Gen_SportSanses
                    .Include(s => s.Gen_Period)
                    .FirstOrDefaultAsync(s => s.SportSanseID == req.SportSanseID);

                if (sanse == null)
                    return new ApiResponse<RegisterResponse> { Success = false, Message = "سانس یافت نشد" };

                // محاسبه تاریخ پایان
                string endDate;
                try
                {
                    int dayCount = sanse.Gen_Period?.DayCount ?? 30;
                    endDate = _helper.AddDaysToPersian(req.StartDate, dayCount);
                }
                catch
                {
                    return new ApiResponse<RegisterResponse>
                    {
                        Success = false,
                        Message = "فرمت تاریخ شروع نامعتبر است. نمونه صحیح: 1405/03/20"
                    };
                }

                var rec = new Acc_MemberSport
                {
                    MemberID = memberID,
                    SportSanseID = sanse.SportSanseID,
                    MembershipTypeID = sanse.MembershipTypeID,
                    ContractID = 1, // بدون قرارداد
                    SessionCount = sanse.SessionCountInPeriod,
                    Amount = sanse.TotalAmount,
                    Tax = 0,
                    DiscountAmount = 0,
                    RegDiscountPercent = 0,
                    RegDiscountAmount = 0,
                    FinalPayment = sanse.TotalAmount,
                    PeriodID = 1,
                    StartDate = req.StartDate,
                    EndDate = endDate,
                    IsActive = true,
                    IsRevival = false,
                    UserID = WEB_USER_ID,
                    CreationDate = _helper.GetToday(),
                    CreationTime = _helper.GetThisTime(),
                    CommentText = "ثبت‌نام از طریق وب‌اپ"
                };

                // محاسبه پورسانت مربی
                if (sanse.CoachMemberID != null)
                {
                    rec.CoachPercent = sanse.CoachMoneyPercent;
                    long baseAmount = (rec.FinalPayment ?? 0) - (sanse.GeneralAmount ?? 0);
                    rec.CoachAmount = baseAmount * (sanse.CoachMoneyPercent ?? 0) / 100;
                    rec.CoachPercentForRevival = sanse.CoachPercentForRevival;
                    rec.CoachRevivalAmount = baseAmount * (sanse.CoachPercentForRevival ?? 0) / 100;
                }

                // بدلیل وجود Trigger روی جدول Acc_MemberSports، از ExecuteSqlInterpolated استفاده می‌کنیم
                // (EF Core به‌طور پیش‌فرض از OUTPUT INSERTED استفاده می‌کنه که با trigger سازگار نیست)
                // نکته: RegDiscountPercent و RegDiscountAmount باید 0 باشند (نه NULL) تا Trigger درست کار کنه
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO Acc_MemberSports (MemberID, SportSanseID, MembershipTypeID, ContractID, SessionCount,
                        Amount, Tax, DiscountAmount, RegDiscountPercent, RegDiscountAmount, FinalPayment,
                        CoachPercent, CoachAmount, CoachPercentForRevival, CoachRevivalAmount,
                        PeriodID, StartDate, EndDate, IsActive, IsRevival, CommentText, UserID, CreationDate, CreationTime)
                    VALUES ({rec.MemberID}, {rec.SportSanseID}, {rec.MembershipTypeID}, {rec.ContractID}, {rec.SessionCount},
                        {rec.Amount}, {rec.Tax}, {rec.DiscountAmount}, {rec.RegDiscountPercent ?? 0}, {rec.RegDiscountAmount ?? 0},
                        {rec.FinalPayment}, {rec.CoachPercent}, {rec.CoachAmount},
                        {rec.CoachPercentForRevival}, {rec.CoachRevivalAmount}, {rec.PeriodID}, {rec.StartDate}, {rec.EndDate},
                        {rec.IsActive}, {rec.IsRevival}, {rec.CommentText}, {rec.UserID}, {rec.CreationDate}, {rec.CreationTime})");

                // خواندن ID رکورد درج شده (داخل تراکنش، قبل از commit)
                var insertedId = await _db.Acc_MemberSports
                    .AsNoTracking()
                    .Where(x => x.MemberID == memberID && x.StartDate == req.StartDate && x.CommentText == "ثبت‌نام از طریق وب‌اپ")
                    .OrderByDescending(x => x.SportMemberID)
                    .Select(x => x.SportMemberID)
                    .FirstOrDefaultAsync();

                await tx.CommitAsync();

                return new ApiResponse<RegisterResponse>
                {
                    Success = true,
                    Message = "ثبت‌نام با موفقیت انجام شد",
                    Data = new RegisterResponse
                    {
                        SportMemberID = insertedId,
                        StartDate = rec.StartDate!,
                        EndDate = rec.EndDate!,
                        FinalPayment = rec.FinalPayment ?? 0,
                        FinalPaymentDisplay = _helper.SetSeprator(rec.FinalPayment ?? 0) + " ریال"
                    }
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در ثبت‌نام عضو {MemberID} در سانس {SanseID}", memberID, req.SportSanseID);
                return new ApiResponse<RegisterResponse>
                {
                    Success = false,
                    Message = "خطا در ثبت اطلاعات"
                };
            }
        }

        /// <summary>
        /// تمدید ثبت‌نام (غیرفعال کردن ثبت‌نام قبلی + ثبت جدید)
        /// منطق معادل RenewRegister در UscRenewRegister
        /// </summary>
        public async Task<ApiResponse<RegisterResponse>> RenewRegisterAsync(int memberID, RegisterRequest req, short shiftID)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var sanse = await _db.Gen_SportSanses
                    .Include(s => s.Gen_Period)
                    .FirstOrDefaultAsync(s => s.SportSanseID == req.SportSanseID);

                if (sanse == null)
                    return new ApiResponse<RegisterResponse> { Success = false, Message = "سانس یافت نشد" };

                // تخفیف مدیریتی
                var member = await _db.Gen_Members.FirstOrDefaultAsync(m => m.MemberID == memberID);
                byte regDiscount = member?.RegDiscount ?? 0;
                long finalPayment = sanse.TotalAmount ?? 0;
                if (regDiscount > 0)
                    finalPayment = finalPayment - (finalPayment * regDiscount / 100);

                // محاسبه تاریخ پایان
                int dayCount = sanse.Gen_Period?.DayCount ?? 30;
                string endDate = _helper.AddDaysToPersian(req.StartDate, dayCount);

                // غیرفعال کردن ثبت‌نام قبلی فعال در همین سانس
                var prevActiveIds = await _db.Acc_MemberSports
                    .Where(x => x.MemberID == memberID && x.SportSanseID == sanse.SportSanseID && x.IsActive == true)
                    .Select(x => x.SportMemberID)
                    .ToListAsync();

                bool isRevival = prevActiveIds.Any();

                if (isRevival)
                {
                    // UPDATE با SQL پارامتری (برای هماهنگی با تراکنش)
                    foreach (var prevId in prevActiveIds)
                    {
                        await _db.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE Acc_MemberSports SET IsActive = 0 WHERE SportMemberID = {prevId}");
                    }
                }

                var rec = new Acc_MemberSport
                {
                    MemberID = memberID,
                    SportSanseID = sanse.SportSanseID,
                    MembershipTypeID = sanse.MembershipTypeID,
                    ContractID = 1,
                    SessionCount = sanse.SessionCountInPeriod,
                    Amount = finalPayment,
                    Tax = 0,
                    DiscountAmount = 0,
                    RegDiscountPercent = 0,
                    RegDiscountAmount = 0,
                    FinalPayment = finalPayment,
                    PeriodID = 1,
                    StartDate = req.StartDate,
                    EndDate = endDate,
                    IsActive = true,
                    IsRevival = isRevival,
                    UserID = WEB_USER_ID,
                    CreationDate = _helper.GetToday(),
                    CreationTime = _helper.GetThisTime(),
                    CommentText = "تمدید با وب‌اپ"
                };

                if (sanse.CoachMemberID != null)
                {
                    rec.CoachPercent = sanse.CoachMoneyPercent;
                    long baseAmount = finalPayment - (sanse.GeneralAmount ?? 0);
                    rec.CoachAmount = baseAmount * (sanse.CoachMoneyPercent ?? 0) / 100;
                    rec.CoachPercentForRevival = sanse.CoachPercentForRevival;
                    rec.CoachRevivalAmount = baseAmount * (sanse.CoachPercentForRevival ?? 0) / 100;
                }

                // بدلیل وجود Trigger روی جدول Acc_MemberSports، از ExecuteSqlInterpolated استفاده می‌کنیم
                // (EF Core به‌طور پیش‌فرض از OUTPUT INSERTED استفاده می‌کنه که با trigger سازگار نیست)
                // نکته: RegDiscountPercent و RegDiscountAmount باید 0 باشند (نه NULL) تا Trigger درست کار کنه
                await _db.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO Acc_MemberSports (MemberID, SportSanseID, MembershipTypeID, ContractID, SessionCount,
                        Amount, Tax, DiscountAmount, RegDiscountPercent, RegDiscountAmount, FinalPayment,
                        CoachPercent, CoachAmount, CoachPercentForRevival, CoachRevivalAmount,
                        PeriodID, StartDate, EndDate, IsActive, IsRevival, CommentText, UserID, CreationDate, CreationTime)
                    VALUES ({rec.MemberID}, {rec.SportSanseID}, {rec.MembershipTypeID}, {rec.ContractID}, {rec.SessionCount},
                        {rec.Amount}, {rec.Tax}, {rec.DiscountAmount}, {rec.RegDiscountPercent ?? 0}, {rec.RegDiscountAmount ?? 0},
                        {rec.FinalPayment}, {rec.CoachPercent}, {rec.CoachAmount},
                        {rec.CoachPercentForRevival}, {rec.CoachRevivalAmount}, {rec.PeriodID}, {rec.StartDate}, {rec.EndDate},
                        {rec.IsActive}, {rec.IsRevival}, {rec.CommentText}, {rec.UserID}, {rec.CreationDate}, {rec.CreationTime})");

                // خواندن ID رکورد درج شده (داخل تراکنش، قبل از commit)
                var insertedId = await _db.Acc_MemberSports
                    .AsNoTracking()
                    .Where(x => x.MemberID == memberID && x.StartDate == req.StartDate && x.CommentText == "تمدید با وب‌اپ")
                    .OrderByDescending(x => x.SportMemberID)
                    .Select(x => x.SportMemberID)
                    .FirstOrDefaultAsync();

                await tx.CommitAsync();

                return new ApiResponse<RegisterResponse>
                {
                    Success = true,
                    Message = isRevival ? "تمدید با موفقیت انجام شد (ثبت‌نام قبلی غیرفعال گردید)" : "ثبت‌نام با موفقیت انجام شد",
                    Data = new RegisterResponse
                    {
                        SportMemberID = insertedId,
                        StartDate = rec.StartDate!,
                        EndDate = rec.EndDate!,
                        FinalPayment = rec.FinalPayment ?? 0,
                        FinalPaymentDisplay = _helper.SetSeprator(rec.FinalPayment ?? 0) + " ریال"
                    }
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در تمدید عضو {MemberID}", memberID);
                return new ApiResponse<RegisterResponse> { Success = false, Message = "خطا در ثبت اطلاعات" };
            }
        }

        /// <summary>تاریخچه ثبت‌نام‌های عضو</summary>
        public async Task<List<RegisterHistoryDto>> GetRegisterHistoryAsync(int memberID)
        {
            var list = await (
                from ms in _db.Acc_MemberSports
                where ms.MemberID == memberID
                orderby ms.SportMemberID descending
                select new RegisterHistoryDto
                {
                    SportMemberID = ms.SportMemberID,
                    SportSanseName = (ms.Gen_SportSanse.Gen_Sport_Category.SportName ?? "") + " - " + (ms.Gen_SportSanse.SanseName ?? ""),
                    CoachName = ms.Gen_SportSanse.Gen_Member.Gen_Person.FullName,
                    StartDate = ms.StartDate,
                    EndDate = ms.EndDate,
                    IsActive = ms.IsActive ?? false,
                    FinalPayment = ms.FinalPayment ?? 0,
                    SessionCount = ms.SessionCount ?? 0,
                    RemainingSessions = ms.SessionCount,
                    CreationDate = ms.CreationDate ?? ""
                }
            ).ToListAsync();

            foreach (var h in list)
                h.FinalPaymentDisplay = _helper.SetSeprator(h.FinalPayment) + " ریال";

            return list;
        }

        /// <summary>
        /// لیست سانس‌های فعال فعلی عضو (برای نمایش در داشبورد)
        /// منطق معادل VI_MemberSportsList.RESTSesssion
        /// </summary>
        public async Task<List<ActiveSanseDto>> GetActiveSansesAsync(int memberID)
        {
            var raw = await (
                from ms in _db.Acc_MemberSports
                where ms.MemberID == memberID && ms.IsActive == true
                orderby ms.SportMemberID descending
                select new
                {
                    ms.SportMemberID,
                    ms.SportSanseID,
                    SportName = ms.Gen_SportSanse.Gen_Sport_Category.SportName ?? "",
                    SanseName = ms.Gen_SportSanse.SanseName ?? "",
                    CoachName = ms.Gen_SportSanse.Gen_Member.Gen_Person.FullName,
                    ms.StartDate,
                    ms.EndDate,
                    SessionCount = ms.SessionCount ?? 0,
                    TotalInPeriod = ms.Gen_SportSanse.SessionCountInPeriod ?? 0,
                    MembershipTypeDesc = ms.Gen_SportSanse.Gen_MembershipType.MembershipTypeDesc,
                    PeriodDesc = ms.Gen_SportSanse.Gen_Period.Description
                }
            ).ToListAsync();

            var result = new List<ActiveSanseDto>();
            foreach (var s in raw)
            {
                // محاسبه جلسات مصرف شده از روی رکوردهای تردد (معادل فرمول VI_MemberSportsList)
                int usedSessions = await _db.ACC_Traffics
                    .CountAsync(t => t.MemberID == memberID && t.SportMemberID == s.SportMemberID);

                int total = s.SessionCount;
                int remaining = total - usedSessions;
                if (remaining < 0) remaining = 0;

                result.Add(new ActiveSanseDto
                {
                    SportMemberID = s.SportMemberID,
                    SportSanseID = s.SportSanseID ?? 0,
                    SportName = s.SportName,
                    SanseName = s.SanseName,
                    CoachName = s.CoachName,
                    StartDate = s.StartDate ?? "",
                    EndDate = s.EndDate ?? "",
                    TotalSessions = total,
                    UsedSessions = usedSessions,
                    RemainingSessions = remaining,
                    MembershipTypeDesc = s.MembershipTypeDesc,
                    PeriodDesc = s.PeriodDesc
                });
            }

            return result;
        }
    }
}
