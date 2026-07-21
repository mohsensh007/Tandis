using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;

namespace TandisWebApp.Services
{
    /// <summary>
    /// سرویس کمکی مشترک - معادل MyProvider در پروژه کیوسک
    /// کارهای تاریخ شمسی، جداکننده اعداد، محاسبه اعتبار/بدهی
    /// </summary>
    public class CommonHelperService
    {
        private readonly FullSportDbContext _db;
        private readonly PersianCalendar _persian;

        public CommonHelperService(FullSportDbContext db)
        {
            _db = db;
            _persian = new PersianCalendar();
        }

        // ============================================================
        //  تاریخ و زمان شمسی
        // ============================================================

        /// <summary>تاریخ امروز به فرمت شمسی YYYY/MM/DD</summary>
        public string GetToday()
        {
            var now = DateTime.Now;
            return $"{_persian.GetYear(now):0000}/{_persian.GetMonth(now):00}/{_persian.GetDayOfMonth(now):00}";
        }

        /// <summary>ساعت الان به فرمت HH:MM:SS</summary>
        public string GetThisTime()
        {
            var now = DateTime.Now;
            return $"{now.Hour:00}:{now.Minute:00}:{now.Second:00}";
        }

        /// <summary>تبدیل تاریخ شمسی به میلادی</summary>
        public DateTime ToGregorian(string persianDate)
        {
            // پذیرش YYYY/MM/DD یا YYYY-MM-DD
            var parts = persianDate.Replace("-", "/").Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                throw new ArgumentException("فرمت تاریخ شمسی نامعتبر است. نمونه صحیح: 1405/03/20");
            int y = int.Parse(parts[0]);
            int m = int.Parse(parts[1]);
            int d = int.Parse(parts[2]);
            return _persian.ToDateTime(y, m, d, 0, 0, 0, 0);
        }

        /// <summary>تبدیل میلادی به شمسی YYYY/MM/DD</summary>
        public string ToPersian(DateTime gregorian)
        {
            return $"{_persian.GetYear(gregorian):0000}/{_persian.GetMonth(gregorian):00}/{_persian.GetDayOfMonth(gregorian):00}";
        }

        /// <summary>افزودن روز به تاریخ شمسی و بازگشت به شمسی</summary>
        public string AddDaysToPersian(string persianDate, int days)
        {
            var g = ToGregorian(persianDate).AddDays(days);
            return ToPersian(g);
        }

        // ============================================================
        //  اعداد و مبالغ
        // ============================================================

        /// <summary>جداکننده سه‌رقمی (مانند SetSeprator در کیوسک)</summary>
        public string SetSeprator(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "0";
            if (decimal.TryParse(value, out var n))
                return n.ToString("#,##0", CultureInfo.InvariantCulture);
            return value;
        }

        public string SetSeprator(long value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

        // ============================================================
        //  اعتبار / بدهی اعضا - معادل توابع MyProvider
        // ============================================================

        // شماره انواع Credit/Debit (دقیقاً مطابق کیوسک)
        public const byte CR_PAY_SHAHRIE = 1;
        public const byte CR_PAY_SHOP = 2;
        public const byte CR_PAY_SERVICE = 3;
        public const byte CR_FREE_SESSION = 10;
        public const byte CR_RIALI = 11;

        /// <summary>
        /// بدهی کلی عضو (SP_GetDebitAmount).
        /// مثبت = بدهکار، منفی = بستانکار.
        /// توجه: بدهی‌ها و بستانکارهای فروشگاه(2) و خدمات(3) مستثنی می‌شوند
        /// چون این دو دارای حساب اعتباری جداگانه هستند.
        /// </summary>
        public async Task<long> GetDebitAmountAsync(int memberID)
        {
            long debit = 0;
            long credit = 0;
            long refund = 0;

            // بدهکارها (به جز فروشگاه و خدمات)
            debit += await _db.Cash_DebitStatements
                .Where(x => x.MemberID == memberID && (x.DebitTypeID != CR_PAY_SHOP && x.DebitTypeID != CR_PAY_SERVICE))
                .SumAsync(x => (long?)x.Amount) ?? 0;

            // بستانکارها (به جز فروشگاه و خدمات)
            credit += await _db.Cash_CreditStatments
                .Where(x => x.MemberID == memberID && (x.CreditTypeID != CR_PAY_SHOP && x.CreditTypeID != CR_PAY_SERVICE))
                .SumAsync(x => (long?)x.Amount) ?? 0;

            // بازپرداخت‌ها (به جز فروشگاه و خدمات)
            refund += await _db.Cash_RefundStatements
                .Where(x => x.MemberID == memberID && (x.RefundTypeID != CR_PAY_SHOP && x.RefundTypeID != CR_PAY_SERVICE))
                .SumAsync(x => (long?)x.Amount) ?? 0;

            return debit - credit + refund;
        }

        /// <summary>
        /// اعتبار ورزشی (ریالی + جلسه آزاد + شهریه) - کف 0
        /// نکته: شهریه(1) هم شامل می‌شود تا ثبت‌نام‌های وب‌اپ بدون پرداخت POS،
        /// از اعتبار ورزشی کسر شوند (درست مثل کیوسک که ثبت‌نام با POS پرداخت می‌شود
        /// و در آن CreditTypeID=PayShahrie ثبت می‌گردد و خنثی می‌شود).
        /// </summary>
        public async Task<long> GetSportCreditAmountAsync(int memberID)
        {
            var credit = await _db.Cash_CreditStatments
                .Where(x => x.MemberID == memberID
                         && (x.CreditTypeID == CR_RIALI
                          || x.CreditTypeID == CR_FREE_SESSION
                          || x.CreditTypeID == CR_PAY_SHAHRIE))
                .SumAsync(x => (long?)x.Amount) ?? 0;

            var debit = await _db.Cash_DebitStatements
                .Where(x => x.MemberID == memberID
                         && (x.DebitTypeID == CR_RIALI
                          || x.DebitTypeID == CR_FREE_SESSION
                          || x.DebitTypeID == CR_PAY_SHAHRIE))
                .SumAsync(x => (long?)x.Amount) ?? 0;

            var refund = await _db.Cash_RefundStatements
                .Where(x => x.MemberID == memberID
                         && (x.RefundTypeID == CR_RIALI
                          || x.RefundTypeID == CR_FREE_SESSION
                          || x.RefundTypeID == CR_PAY_SHAHRIE))
                .SumAsync(x => (long?)x.Amount) ?? 0;

            var result = credit - debit - refund;
            return result < 0 ? 0 : result;
        }

        /// <summary>
        /// اعتبار فروشگاه/بوفه - می‌تواند منفی (بدهکار) باشد
        /// </summary>
        public async Task<long> GetBuffetCreditAmountAsync(int memberID)
        {
            var credit = await _db.Cash_CreditStatments
                .Where(x => x.MemberID == memberID && x.CreditTypeID == CR_PAY_SHOP)
                .SumAsync(x => (long?)x.Amount) ?? 0;

            var buffetFactorSum = await _db.Acc_BuffetFactors
                .Where(x => x.MemberID == memberID)
                .SumAsync(x => (long?)x.BuffetFactorTotalAmount) ?? 0;

            var debit = await _db.Cash_DebitStatements
                .Where(x => x.MemberID == memberID && x.DebitTypeID == CR_PAY_SHOP)
                .SumAsync(x => (long?)x.Amount) ?? 0;

            var refund = await _db.Cash_RefundStatements
                .Where(x => x.MemberID == memberID && x.RefundTypeID == CR_PAY_SHOP)
                .SumAsync(x => (long?)x.Amount) ?? 0;

            return credit - buffetFactorSum - debit - refund;
        }

        /// <summary>
        /// اعتبار سرویس - می‌تواند منفی باشد
        /// </summary>
        public async Task<long> GetServiceCreditAmountAsync(int memberID)
        {
            var credit = await _db.Cash_CreditStatments
                .Where(x => x.MemberID == memberID && x.CreditTypeID == CR_PAY_SERVICE)
                .SumAsync(x => (long?)x.Amount) ?? 0;

            var serviceSum = await _db.ACC_MemberServices
                .Where(x => x.TrafficID != null
                         && x.TrafficID == _db.ACC_Traffics
                            .Where(t => t.MemberID == memberID).Select(t => t.TrafficID).FirstOrDefault())
                .SumAsync(x => (long?)x.ServiceAmount) ?? 0;

            // روش ساده‌تر: جمع خرید سرویس از طریق جدول بدهی
            var debit = await _db.Cash_DebitStatements
                .Where(x => x.MemberID == memberID && x.DebitTypeID == CR_PAY_SERVICE)
                .SumAsync(x => (long?)x.Amount) ?? 0;

            var refund = await _db.Cash_RefundStatements
                .Where(x => x.MemberID == memberID && x.RefundTypeID == CR_PAY_SERVICE)
                .SumAsync(x => (long?)x.Amount) ?? 0;

            return credit - debit - refund;
        }

        // ============================================================
        //  اطلاعات کلی
        // ============================================================

        /// <summary>دریافت نام باشگاه از جدول Sec_Systems</summary>
        public async Task<string> GetClubNameAsync()
        {
            var sys = await _db.Sec_Systems.FirstOrDefaultAsync();
            return sys?.ClubName ?? "باشگاه";
        }

        /// <summary>دریافت آخرین MemberID برای ساخت عضو جدید</summary>
        public async Task<int> GetNextMemberIDAsync(bool continuous, short? shiftID)
        {
            if (continuous)
            {
                var max = await _db.Gen_Members.MaxAsync(x => (int?)x.MemberID) ?? 0;
                return max + 1;
            }
            else
            {
                var max = await _db.Gen_Members
                    .Where(x => x.ShiftID == shiftID)
                    .MaxAsync(x => (int?)x.MemberID) ?? 0;
                return max + 1;
            }
        }
    }
}
