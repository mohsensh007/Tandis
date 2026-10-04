using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// حسابداری و گزارش مالی اعضا
    /// منطق معادل UscAccounting در کیوسک
    /// </summary>
    public class AccountingService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<AccountingService> _logger;
        private const short WEB_USER_ID = 1;

        public AccountingService(FullSportDbContext db, CommonHelperService helper, ILogger<AccountingService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        /// <summary>
        /// دریافت خلاصه مالی عضو (بدهی و اعتبارها)
        /// </summary>
        public async Task<object> GetFinanceSummaryAsync(int memberID)
        {
            var debit = await _helper.GetDebitAmountAsync(memberID);
            var sportCredit = await _helper.GetSportCreditAmountAsync(memberID);
            var buffetCredit = await _helper.GetBuffetCreditAmountAsync(memberID);
            var serviceCredit = await _helper.GetServiceCreditAmountAsync(memberID);

            return new
            {
                TotalDebit = debit,
                TotalDebitDisplay = _helper.SetSeprator(debit) + " ریال",
                IsDebtor = debit > 0,
                SportCredit = sportCredit,
                SportCreditDisplay = _helper.SetSeprator(sportCredit) + " ریال",
                BuffetCredit = buffetCredit,
                BuffetCreditDisplay = _helper.SetSeprator(buffetCredit) + " ریال",
                ServiceCredit = serviceCredit,
                ServiceCreditDisplay = _helper.SetSeprator(serviceCredit) + " ریال"
            };
        }

        /// <summary>
        /// لیست تراکنش‌های مالی عضو (بستانکار و بدهکار) — مرتب‌شده بر اساس تاریخ+ساعت (جدیدترین اول)
        /// </summary>
        public async Task<List<FinanceDocDto>> GetFinanceDocsAsync(int memberID)
        {
            var result = new List<FinanceDocDto>();

            // بستانکارها
            var credits = await _db.Cash_CreditStatments
                .Where(x => x.MemberID == memberID)
                .OrderByDescending(x => x.CreditID)
                .ToListAsync();
            foreach (var c in credits)
            {
                var dateStr = (c.CreationDate ?? "").Trim();
                var timeStr = (c.CreationTime ?? "").Trim();

                result.Add(new FinanceDocDto
                {
                    CreationTime = ParseShamsiDateTime(dateStr, timeStr),
                    CreationDateDisplay = dateStr,
                    Amount = c.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(c.Amount ?? 0) + " ریال",
                    DocType = "بستانکار",
                    DocDesc = c.CreditDesc ?? "",
                    // ✅ کلید مرتب‌سازی: تاریخ شمسی + ساعت (رشته صفرپر → مرتب‌سازی درست)
                    SortKey = (dateStr + " " + timeStr).Trim()
                });
            }

            // بدهکارها
            var debits = await _db.Cash_DebitStatements
                .Where(x => x.MemberID == memberID)
                .OrderByDescending(x => x.DebitID)
                .ToListAsync();
            foreach (var d in debits)
            {
                string dateDisplay = "";
                if (d.CreationTime.HasValue)
                    dateDisplay = _helper.ToPersian(d.CreationTime.Value);

                var debitTimeStr = d.CreationTime.HasValue ? d.CreationTime.Value.ToString("HH:mm:ss") : "";

                result.Add(new FinanceDocDto
                {
                    CreationTime = d.CreationTime ?? DateTime.MinValue,
                    CreationDateDisplay = dateDisplay,
                    Amount = d.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(d.Amount ?? 0) + " ریال",
                    DocType = "بدهکار",
                    DocDesc = d.DebitDesc ?? "",
                    SortKey = (dateDisplay + " " + debitTimeStr).Trim()
                });
            }

            // ✅ مرتب‌سازی بر اساس تاریخ + ساعت (جدیدترین اول) — صرف‌نظر از نوع سند
            return result.OrderByDescending(x => x.SortKey, StringComparer.Ordinal).ToList();
        }

        /// <summary>تبدیل تاریخ/ساعت شمسی متنی به DateTime میلادی — در صورت خطا DateTime.MinValue</summary>
        private DateTime ParseShamsiDateTime(string? date, string? time)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(date)) return DateTime.MinValue;
                var d = _helper.ToGregorian(date.Trim());
                if (TimeSpan.TryParse((time ?? "").Trim(), out var t))
                    d = d.Add(t);
                return d;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>
        /// لیست ترددهای عضو
        /// منطق معادل UscTrafficReport
        /// </summary>
        public async Task<List<TrafficReportDto>> GetTrafficReportAsync(int memberID)
        {
            return await (
                from t in _db.ACC_Traffics
                where t.MemberID == memberID
                orderby t.TrafficID descending
                select new TrafficReportDto
                {
                    TrafficID = t.TrafficID,
                    EntryDate = t.EntryDate,
                    EntryTime = t.EntryTime,
                    ExitDate = t.ExitDate,
                    ExitTime = t.ExitTime,
                    PersonName = t.PersonName,
                    EntryDesc = t.EntryDesc,
                    BoxID = t.BoxID,
                    TrafficStatus = t.TrafficStatus
                }
            ).ToListAsync();
        }


    }
}
