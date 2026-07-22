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

        public async Task<AdminReportResponse<AdminTrafficRowDto, TrafficReportSummaryDto>> GetTrafficReportAsync(short shiftID, string? from, string? to)
        {
            var query = _db.ACC_Traffics
                .Where(t => t.ShiftID == shiftID);

            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(t => t.EntryDate != null && t.EntryDate.CompareTo(to) <= 0);

            // Join با Gen_Members و Gen_Persons برای دریافت نام کامل و کد عضویت
            // ابتدا داده‌های خام را می‌گیریم، بعد در حافظه فرمت می‌کنیم
            var rawRows = await (
                from t in query
                join m in _db.Gen_Members on t.MemberID equals m.MemberID into memJoin
                from m in memJoin.DefaultIfEmpty()
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID into personJoin
                from p in personJoin.DefaultIfEmpty()
                orderby t.TrafficID descending
                select new
                {
                    t.TrafficID,
                    t.PersonName,
                    t.EntryDate,
                    t.EntryTime,
                    t.ExitDate,
                    t.ExitTime,
                    t.EntryDesc,
                    t.IsGuest,
                    MemberFullName = p.FullName,
                    MemberID = m.MemberID
                }
            ).ToListAsync();

            var rows = rawRows.Select(x => new AdminTrafficRowDto
            {
                TrafficID = x.TrafficID,
                PersonName = x.IsGuest == true ? x.PersonName : (x.MemberFullName ?? x.PersonName ?? "-"),
                MemberCode = (x.MemberID > 0) ? _helper.SetSeprator(x.MemberID) : null,
                EntryDate = x.EntryDate,
                EntryTime = x.EntryTime,
                ExitDate = x.ExitDate,
                ExitTime = x.ExitTime,
                EntryDesc = x.EntryDesc,
                IsGuest = x.IsGuest
            }).ToList();

            // محاسبه خلاصه
            var summary = new TrafficReportSummaryDto
            {
                TotalCount = rows.Count,
                MemberCount = rows.Count(r => r.IsGuest != true),
                GuestCount = rows.Count(r => r.IsGuest == true)
            };

            return new AdminReportResponse<AdminTrafficRowDto, TrafficReportSummaryDto>
            {
                Data = rows,
                Summary = summary
            };
        }

        // ============================================================
        //  گزارش ثبت‌نام و تمدید
        // ============================================================

        public async Task<AdminReportResponse<AdminRegisterRowDto, RegisterReportSummaryDto>> GetRegisterReportAsync(short shiftID, string? from, string? to, string mode)
        {
            // استفاده از Join به جای Contains برای جلوگیری از خطای SQL
            var query = _db.Acc_MemberSports
                .Where(ms => ms.Gen_SportSanse != null && ms.Gen_SportSanse.ShiftID == shiftID);

            // فیلتر بازه زمانی بر اساس CreationDate (شمسی)
            if (!string.IsNullOrWhiteSpace(from))
                query = query.Where(ms => ms.CreationDate != null && ms.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                query = query.Where(ms => ms.CreationDate != null && ms.CreationDate.CompareTo(to) <= 0);

            // فیلتر نوع: ثبت‌نام / تمدید / هر دو
            if (mode == "register")
                query = query.Where(ms => ms.IsRevival == null || ms.IsRevival == false);
            else if (mode == "renew")
                query = query.Where(ms => ms.IsRevival == true);

            var rows = await (
                from ms in query
                orderby ms.SportMemberID descending
                select new { ms, sanse = ms.Gen_SportSanse }
            ).ToListAsync();

            var result = new List<AdminRegisterRowDto>();
            long totalAmount = 0;
            int registerCount = 0;
            int renewalCount = 0;

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

                bool isRevival = item.ms.IsRevival ?? false;
                if (isRevival) renewalCount++; else registerCount++;
                totalAmount += item.ms.FinalPayment ?? 0;

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
                    IsRevival = isRevival
                });
            }

            var summary = new RegisterReportSummaryDto
            {
                TotalCount = result.Count,
                RegisterCount = registerCount,
                RenewalCount = renewalCount,
                TotalAmount = totalAmount,
                TotalAmountDisplay = _helper.SetSeprator(totalAmount) + " ریال"
            };

            return new AdminReportResponse<AdminRegisterRowDto, RegisterReportSummaryDto>
            {
                Data = result,
                Summary = summary
            };
        }

        // ============================================================
        //  گزارش تک‌جلسه‌ها
        // ============================================================

        public async Task<AdminReportResponse<AdminOneSessionRowDto, OneSessionReportSummaryDto>> GetOneSessionReportAsync(short shiftID, string? from, string? to)
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
            long totalAmount = 0;

            foreach (var item in rows)
            {
                totalAmount += item.t.Amount ?? 0;
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

            var summary = new OneSessionReportSummaryDto
            {
                TotalCount = result.Count,
                TotalAmount = totalAmount,
                TotalAmountDisplay = _helper.SetSeprator(totalAmount) + " ریال"
            };

            return new AdminReportResponse<AdminOneSessionRowDto, OneSessionReportSummaryDto>
            {
                Data = result,
                Summary = summary
            };
        }

        // ============================================================
        //  گزارش صندوق (فقط دریافتی‌ها)
        // ============================================================

        public async Task<AdminReportResponse<AdminFinanceRowDto, FinanceReportSummaryDto>> GetFinanceReportAsync(short shiftID, string? from, string? to)
        {
            var result = new List<AdminFinanceRowDto>();

            // --- فقط بستانکارها (دریافتی‌ها) ---
            var creditQuery = _db.Cash_CreditStatments.AsQueryable();

            if (!string.IsNullOrWhiteSpace(from))
                creditQuery = creditQuery.Where(c => c.CreationDate != null && c.CreationDate.CompareTo(from) >= 0);
            if (!string.IsNullOrWhiteSpace(to))
                creditQuery = creditQuery.Where(c => c.CreationDate != null && c.CreationDate.CompareTo(to) <= 0);

            // به جای Contains با لیست در حافظه، از Join استفاده می‌کنیم تا خطای SQL و محدودیت پارامتر پیش نیاید
            var credits = await (
                from c in creditQuery
                join m in _db.Gen_Members.Where(m => m.ShiftID == shiftID) on c.MemberID equals m.MemberID
                orderby c.CreditID descending
                select c
            ).ToListAsync();

            long totalCredit = 0;
            foreach (var c in credits)
            {
                var personName = await GetPersonNameAsync(c.MemberID ?? 0);
                totalCredit += c.Amount ?? 0;

                result.Add(new AdminFinanceRowDto
                {
                    RowID = c.CreditID,
                    RowType = "دریافتی",
                    TypeDesc = GetCreditTypeDesc(c.CreditTypeID),
                    Amount = c.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(c.Amount ?? 0) + " ریال",
                    Description = c.CreditDesc,
                    PersonName = personName,
                    DateDisplay = c.CreationDate
                });
            }

            // مرتب‌سازی نزولی بر اساس تاریخ
            var finalResult = result.OrderByDescending(r => r.DateDisplay).ToList();

            var summary = new FinanceReportSummaryDto
            {
                TotalCount = finalResult.Count,
                CreditCount = credits.Count,
                DebitCount = 0,
                TotalCredit = totalCredit,
                TotalDebit = 0,
                Balance = totalCredit,
                TotalCreditDisplay = _helper.SetSeprator(totalCredit) + " ریال",
                TotalDebitDisplay = "۰ ریال",
                BalanceDisplay = _helper.SetSeprator(totalCredit) + " ریال"
            };

            return new AdminReportResponse<AdminFinanceRowDto, FinanceReportSummaryDto>
            {
                Data = finalResult,
                Summary = summary
            };
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