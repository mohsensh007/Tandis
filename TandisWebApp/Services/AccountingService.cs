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
        /// لیست تراکنش‌های مالی عضو (بستانکار و بدهکار)
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
                result.Add(new FinanceDocDto
                {
                    CreationTime = DateTime.MinValue,
                    CreationDateDisplay = c.CreationDate ?? "",
                    Amount = c.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(c.Amount ?? 0) + " ریال",
                    DocType = "بستانکار",
                    DocDesc = c.CreditDesc ?? ""
                });
            }

            // بدهکارها
            var debits = await _db.Cash_DebitStatements
                .Where(x => x.MemberID == memberID)
                .OrderByDescending(x => x.DebitID)
                .ToListAsync();
            foreach (var d in debits)
            {
                result.Add(new FinanceDocDto
                {
                    CreationTime = d.CreationTime ?? DateTime.MinValue,
                    CreationDateDisplay = "",
                    Amount = d.Amount ?? 0,
                    AmountDisplay = _helper.SetSeprator(d.Amount ?? 0) + " ریال",
                    DocType = "بدهکار",
                    DocDesc = d.DebitDesc ?? ""
                });
            }

            return result.OrderByDescending(x => x.CreationTime).ToList();
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
                    BoxID = t.BoxID
                }
            ).ToListAsync();
        }

        /// <summary>
        /// شارژ اعتبار (فقط ثبت - بدون درگاه آنلاین)
        /// منطق معادل FinalizePayment در UscAddMoney
        /// </summary>
        public async Task<SimpleResponse> AddMoneyAsync(int memberID, AddMoneyRequest req, short shiftID)
        {
            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var credit = new Cash_CreditStatment
                {
                    MemberID = memberID,
                    CreditTypeID = req.CreditTypeID,
                    Amount = req.Amount,
                    CreditDesc = "شارژ اعتبار از وب‌اپ",
                    UserID = WEB_USER_ID,
                    IsPos = false,
                    PosID = 0,
                    IsPcPos = false,
                    IsFische = false,
                    ReceiptNo = "web",
                    CreationDate = _helper.GetToday(),
                    CreationTime = _helper.GetThisTime()
                };
                _db.Cash_CreditStatments.Add(credit);
                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return new SimpleResponse
                {
                    Success = true,
                    Message = $"اعتبار شما با مبلغ {_helper.SetSeprator(req.Amount)} ریال شارژ شد"
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطا در شارژ اعتبار");
                return new SimpleResponse { Success = false, Message = "خطا در ثبت اطلاعات" };
            }
        }

        /// <summary>لیست انواع اعتبار برای شارژ</summary>
        public async Task<List<object>> GetCreditTypesAsync()
        {
            // 11=ریالی، 2=فروشگاه، 3=سرویس
            byte[] wanted = { 11, 2, 3 };
            return await _db.Cash_CreditTypes
                .Where(x => wanted.Contains(x.CreditTypeID))
                .Select(x => new { x.CreditTypeID, x.CreditType })
                .Cast<object>()
                .ToListAsync();
        }
    }
}
