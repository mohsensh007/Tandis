using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// گزارش‌های مدیریتی — هر گزارش فقط داده‌های شیفت مربوطه را برمی‌گرداند.
    /// تمام کوئری‌ها بر اساس ShiftID فیلتر می‌شوند.
    /// </summary>
    public class AdminReportService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<AdminReportService> _logger;

        public AdminReportService(FullSportDbContext db, CommonHelperService helper, ILogger<AdminReportService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        // ============================================================
        //  گزارش ترددها
        // ============================================================

        public async Task<List<AdminTrafficRowDto>> GetTrafficReportAsync(short shiftID, string? from, string? to)
        {
            var query = _db.ACC_Traffics
                .Where(t => t.ShiftID == shiftID);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(to) <= 0);

            var rows = await (
                from t in query
                orderby t.TrafficID descending
                select new AdminTrafficRowDto
                {
                    TrafficID = t.TrafficID,
                    PersonName = t.PersonName,
                    EntryDate = t.EntryDate,
                    EntryTime = t.EntryTime,
                    ExitDate = t.ExitDate,
                    ExitTime = t.ExitTime,
                    EntryDesc = t.EntryDesc,
                    IsGuest = t.IsGuest
                }
            ).ToListAsync();

            // اضافه کردن کد عضویت برای اعضا (نه مهمان)
            foreach (var row in rows)
            {
                if (row.IsGuest != true && row.TrafficID > 0)
                {
                    var member = await (
                        from t2 in _db.ACC_Traffics
                        join m in _db.Gen_Members on t2.MemberID equals m.MemberID
                        where t2.TrafficID == row.TrafficID
                        select m.MemberID
                    ).FirstOrDefaultAsync();

                    if (member > 0)
                        row.MemberCode = _helper.SetSeprator(member);
                }
            }

            return rows;
        }

        // ============================================================
        //  گزارش ثبت‌نام و تمدید
        // ============================================================

        public async Task<List<AdminRegisterRowDto>> GetRegisterReportAsync(short shiftID, string? from, string? to, string mode)
        {
            // فقط سانس‌های مربوط به این شیفت
            var sanseIDs = await _db.Gen_SportSanses
                .Where(s => s.ShiftID == shiftID)
                .Select(s => s.SportSanseID)
                .ToListAsync();

            var query = _db.Acc_MemberSports
                .Where(ms => sanseIDs.Contains(ms.SportSanseID ?? 0));

            // فیلتر بازه زمانی بر اساس CreationDate (شمسی)
            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(ms => ms.CreationDate != null && ms.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(ms => ms.CreationDate != null && ms.CreationDate.CompareTo(to) <= 0);

            // فیلتر نوع: ثبت‌نام / تمدید / هر دو
            if (mode == "register")
                query = query.Where(ms => ms.IsRevival != true);
            else if (mode == "renew")
                query = query.Where(ms => ms.IsRevival == true);

            var rows = await (
                from ms in query
                orderby ms.SportMemberID descending
                select new { ms, sanse = ms.Gen_SportSanse }
            ).ToListAsync();

            var result = new List<AdminRegisterRowDto>();
            foreach (var item in rows)
            {
                var s = item.sanse;
                var personName = "";
                string? memberCode = null;

                // نام و کد عضو
                if (item.ms.MemberID > 0)
                {
                    var info = await (
                        from m in _db.Gen_Members
                        join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                        where m.MemberID == item.ms.MemberID
                        select new { p.FullName, m.MemberID }
                    ).FirstOrDefaultAsync();

                    if (info != null)
                    {
                        personName = info.FullName ?? "";
                        memberCode = _helper.SetSeprator(info.MemberID);
                    }
                }

                result.Add(new AdminRegisterRowDto
                {
                    SportMemberID = item.ms.SportMemberID,
                    PersonName = personName,
                    MemberCode = memberCode,
                    SportName = s != null ? (s.Gen_Sport_Category?.SportName ?? "") : "",
                    SanseName = s?.SanseName ?? "",
                    CoachName = s != null ? (s.Gen_Member?.Gen_Person?.FullName ?? "") : "",
                    MembershipTypeDesc = s != null ? (s.Gen_MembershipType?.MembershipTypeDesc ?? "") : "",
                    PeriodDesc = s != null ? (s.Gen_Period?.Description ?? "") : "",
                    FinalPayment = item.ms.FinalPayment ?? 0,
                    FinalPaymentDisplay = _helper.SetSeprator(item.ms.FinalPayment ?? 0) + " ریال",
                    StartDate = item.ms.StartDate,
                    EndDate = item.ms.EndDate,
                    CreationDate = item.ms.CreationDate,
                    CreationTime = item.ms.CreationTime,
                    IsRevival = item.ms.IsRevival ?? false
                });
            }

            return result;
        }

        // ============================================================
        //  گزارش تک‌جلسه‌ها
        // ============================================================

        public async Task<List<AdminOneSessionRowDto>> GetOneSessionReportAsync(short shiftID, string? from, string? to)
        {
            var query = _db.ACC_Tickets
                .Where(t => t.ShiftID == shiftID);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(t => t.CreationDate != null && t.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(t => t.CreationDate != null && t.CreationDate.CompareTo(to) <= 0);

            var rows = await (
                from t in query
                orderby t.TicketID descending
                select new { t, tarefe = t.Gen_Tarefe, san = t.Gen_Tarefe != null ? t.Gen_Tarefe.Gen_San : null }
            ).ToListAsync();

            var result = new List<AdminOneSessionRowDto>();
            foreach (var item in rows)
            {
                result.Add(new AdminOneSessionRowDto
                {
                    TicketID = item.t.TicketID,
                    PersonName = item.t.FullName,
                    SansName = item.san?.Sans ?? "",
                    TarefeName = item.tarefe?.Tarefe ?? "",
                    Amount = item.t.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(item.t.Amount ?? 0) + " ریال",
                    CreationDate = item.t.CreationDate,
                    CreationTime = item.t.CreationTime != null
                        ? item.t.CreationTime.Value.ToString(@"hh\:mm\:ss")
                        : "",
                    TicketDesc = item.t.TicketDesc
                });
            }

            return result;
        }

        // ============================================================
        //  گزارش بدهی‌ها و دریافت‌ها
        // ============================================================

        public async Task<List<AdminFinanceRowDto>> GetFinanceReportAsync(short shiftID, string? from, string? to)
        {
            var result = new List<AdminFinanceRowDto>();

            // --- بستانکارها ---
            var creditQuery = _db.Cash_CreditStatments.AsQueryable();

            if (!string.IsNullOrWhiteSpace(from))
                creditQuery = creditQuery.Where(c => c.CreationDate != null && c.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                creditQuery = creditQuery.Where(c => c.CreationDate != null && c.CreationDate.CompareTo(to) <= 0);

            // فیلتر بر اساس ShiftID: از طریق Member → Gen_Members
            var shiftMembers = await _db.Gen_Members
                .Where(m => m.ShiftID == shiftID)
                .Select(m => m.MemberID)
                .ToListAsync();

            var credits = await creditQuery
                .Where(c => c.MemberID != null && shiftMembers.Contains(c.MemberID.Value))
                .OrderByDescending(c => c.CreditID)
                .ToListAsync();

            foreach (var c in credits)
            {
                var personName = await GetPersonNameAsync(c.MemberID ?? 0);

                result.Add(new AdminFinanceRowDto
                {
                    RowID = c.CreditID,
                    RowType = "بستانکار",
                    TypeDesc = GetCreditTypeDesc(c.CreditTypeID),
                    Amount = c.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(c.Amount ?? 0) + " ریال",
                    Description = c.CreditDesc,
                    PersonName = personName,
                    DateDisplay = c.CreationDate
                });
            }

            // --- بدهکارها ---
            var debitQuery = _db.Cash_DebitStatements.AsQueryable();

            if (!string.IsNullOrWhiteSpace(from))
                debitQuery = debitQuery.Where(d => d.CreationTime != null
                    && _helper.ToPersian(d.CreationTime.Value).CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                debitQuery = debitQuery.Where(d => d.CreationTime != null
                    && _helper.ToPersian(d.CreationTime.Value).CompareTo(to) <= 0);

            var debits = await debitQuery
                .Where(d => d.MemberID != null && shiftMembers.Contains(d.MemberID.Value))
                .OrderByDescending(d => d.DebitID)
                .ToListAsync();

            foreach (var d in debits)
            {
                var personName = await GetPersonNameAsync(d.MemberID ?? 0);

                result.Add(new AdminFinanceRowDto
                {
                    RowID = d.DebitID,
                    RowType = "بدهکار",
                    TypeDesc = GetDebitTypeDesc(d.DebitTypeID),
                    Amount = d.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(d.Amount ?? 0) + " ریال",
                    Description = d.DebitDesc,
                    PersonName = personName,
                    DateDisplay = d.CreationTime.HasValue ? _helper.ToPersian(d.CreationTime.Value) : ""
                });
            }

            // مرتب‌سازی نزولی بر اساس تاریخ
            return result.OrderByDescending(r => r.DateDisplay).ToList();
        }

        // ============================================================
        //  متدهای کمکی
        // ============================================================

        private async Task<string?> GetPersonNameAsync(int memberID)
        {
            if (memberID <= 0) return null;
            return await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == memberID
                select p.FullName
            ).FirstOrDefaultAsync();
        }

        private static string GetCreditTypeDesc(byte? typeId)
        {
            return typeId switch
            {
                1 => "شهریه دوره",
                2 => "خرید از فروشگاه",
                3 => "خرید خدمات",
                10 => "جلسه آزاد",
                11 => "اعتبار ریالی",
                _ => $"نوع {typeId}"
            };
        }

        private static string GetDebitTypeDesc(byte? typeId)
        {
            return typeId switch
            {
                1 => "شهریه دوره",
                2 => "خرید از فروشگاه",
                3 => "خرید خدمات",
                7 => "مالیات",
                10 => "جلسه آزاد",
                11 => "استفاده از اعتبار",
                _ => $"نوع {typeId}"
            };
        }
    }
}
