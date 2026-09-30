using System.ComponentModel.DataAnnotations;

namespace TandisWebApp.DTOs
{
    // ============================================================
    //  احراز هویت مدیر
    // ============================================================

    public class AdminLoginRequest
    {
        [Required(ErrorMessage = "نام کاربری را وارد کنید")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور را وارد کنید")]
        public string Password { get; set; } = string.Empty;
    }

    public class AdminLoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public short ShiftID { get; set; }
    }

    // ============================================================
    //  خلاصه گزارش‌ها (Summary DTOs)
    // ============================================================

    public class TrafficReportSummaryDto
    {
        public int TotalCount { get; set; }
        public int MemberCount { get; set; }
        public int GuestCount { get; set; }
    }

    public class RegisterReportSummaryDto
    {
        public int TotalCount { get; set; }
        public int RegisterCount { get; set; }
        public int RenewalCount { get; set; }
        public long TotalAmount { get; set; }
        public string TotalAmountDisplay { get; set; } = string.Empty;
    }

    public class OneSessionReportSummaryDto
    {
        public int TotalCount { get; set; }
        public long TotalAmount { get; set; }
        public string TotalAmountDisplay { get; set; } = string.Empty;
    }

    public class FinanceReportSummaryDto
    {
        public int TotalCount { get; set; }
        public int CreditCount { get; set; }
        public int DebitCount { get; set; }
        public long TotalCredit { get; set; }
        public long TotalDebit { get; set; }
        public long Balance { get; set; }
        public string TotalCreditDisplay { get; set; } = string.Empty;
        public string TotalDebitDisplay { get; set; } = string.Empty;
        public string BalanceDisplay { get; set; } = string.Empty;
    }

    // ============================================================
    //  گزارش ترددها
    // ============================================================

    public class AdminTrafficRowDto
    {
        public long TrafficID { get; set; }
        public string? PersonName { get; set; }
        public string? MemberCode { get; set; }
        public string? EntryDate { get; set; }
        public string? EntryTime { get; set; }
        public string? ExitDate { get; set; }
        public string? ExitTime { get; set; }
        public string? EntryDesc { get; set; }
        public bool? IsGuest { get; set; }
        public int? BoxID { get; set; }
    }

    // ============================================================
    //  گزارش ثبت‌نام و تمدید
    // ============================================================

    public class AdminRegisterRowDto
    {
        public long SportMemberID { get; set; }
        public string? PersonName { get; set; }
        public string? MemberCode { get; set; }
        public string? SportName { get; set; }
        public string? SanseName { get; set; }
        public string? CoachName { get; set; }
        public string? MembershipTypeDesc { get; set; }
        public string? PeriodDesc { get; set; }
        public long FinalPayment { get; set; }
        public string FinalPaymentDisplay { get; set; } = string.Empty;
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? CreationDate { get; set; }
        public string? CreationTime { get; set; }
        public bool IsRevival { get; set; }
        public string TypeDesc => IsRevival ? "تمدید" : "ثبت‌نام";
    }

    // ============================================================
    //  گزارش تک‌جلسه‌ها
    // ============================================================

    public class AdminOneSessionRowDto
    {
        public int TicketID { get; set; }
        public string? PersonName { get; set; }
        public string? MemberCode { get; set; }
        public string? SansName { get; set; }
        public string? TarefeName { get; set; }
        public long Amount { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
        public string? CreationDate { get; set; }
        public string? CreationTime { get; set; }
        public string? TicketDesc { get; set; }
    }

    // ============================================================
    //  گزارش بدهی‌ها و دریافت‌ها
    // ============================================================

    public class AdminFinanceRowDto
    {
        public long RowID { get; set; }
        public string RowType { get; set; } = string.Empty; // "دریافتی" or "پرداختی"
        public string? TypeDesc { get; set; }
        public long Amount { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? PersonName { get; set; }
        public string? DateDisplay { get; set; }
    }

    // ============================================================
    //  پاسخ گزارش با خلاصه (Generic wrapper)
    // ============================================================

    public class AdminReportResponse<TRow, TSummary>
    {
        public List<TRow> Data { get; set; } = new();
        public TSummary Summary { get; set; } = default!;
    }


    // ============================================================
    //  داشبورد مدیریت
    // ============================================================

    public class DashboardStatsResult
    {
        public int InsideCount { get; set; }
        public int CurrentRegister { get; set; }
        public int CurrentRenew { get; set; }
        public int PreviousRegister { get; set; }
        public int PreviousRenew { get; set; }
        public string CurrentLabel { get; set; } = string.Empty;
        public string PreviousLabel { get; set; } = string.Empty;
        public long CurrentCredit { get; set; }
        public int CurrentCreditCount { get; set; }
        public long PreviousCredit { get; set; }
        public int PreviousCreditCount { get; set; }
    }

    public class AdminInsideRowDto
    {
        public long TrafficID { get; set; }
        public string PersonName { get; set; } = string.Empty;
        public string? MemberCode { get; set; }
        public string EntryTime { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public bool IsGuest { get; set; }
    }

    // ============================================================
    //  نمودارهای سری-زمانی داشبورد
    // ============================================================

    /// <summary>یک نقطه سری‌زمانی (یک روز شمسی با مقادیر همه بخش‌ها)</summary>
    public class ChartSeriesPointDto
    {
        /// <summary>کلید باکِت: «1405/07/07» یا «1405/07»</summary>
        public string BucketKey { get; set; } = string.Empty;
        /// <summary>برچسب کوتاه محور X (ساعت/روز هفته/روز ماه/نام ماه)</summary>
        public string ShortLabel { get; set; } = string.Empty;
        /// <summary>برچسب کامل برای تولتیپ</summary>
        public string FullLabel { get; set; } = string.Empty;
        /// <summary>تعداد تردد (ورودها)</summary>
        public int TrafficCount { get; set; }
        /// <summary>تعداد ثبت‌نام + تمدید</summary>
        public int RegisterCount { get; set; }
        /// <summary>تعداد بلیط تک‌جلسه فروخته‌شده</summary>
        public int TicketCount { get; set; }
        /// <summary>تعداد خدمات ارائه‌شده</summary>
        public int ServiceCount { get; set; }
        /// <summary>جمع مبلغ دریافت‌ها (ریال)</summary>
        public long FinanceAmount { get; set; }
    }

    /// <summary>پاسخ API نمودار — نقاط + خلاصه هر سری برای کارت‌های کناری</summary>
    public class ChartSeriesResponseDto
    {
        /// <summary>نقاط دوره جاری</summary>
        public List<ChartSeriesPointDto> Points { get; set; } = new();
        /// <summary>نقاط دوره قبل (خط مقایسه‌ای خاکستری)</summary>
        public List<ChartSeriesPointDto> PointsPrev { get; set; } = new();
        /// <summary>متن بازه جاری — مثل «۱۴۰۵/۰۶/۲۴ تا ۱۴۰۵/۰۷/۰۷»</summary>
        public string CurrentRangeLabel { get; set; } = string.Empty;
        /// <summary>متن بازه قبل — مثل «۱۴۰۵/۰۶/۱۷ تا ۱۴۰۵/۰۶/۲۳»</summary>
        public string PreviousRangeLabel { get; set; } = string.Empty;
        /// <summary>آیا امکان رفتن به دوره بعدی وجود دارد</summary>
        public bool CanGoNext { get; set; }
        public ChartSerieSummaryDto Traffic { get; set; } = new();
        public ChartSerieSummaryDto Register { get; set; } = new();
        public ChartSerieSummaryDto Ticket { get; set; } = new();
        public ChartSerieSummaryDto Service { get; set; } = new();
        public ChartSerieSummaryDto Finance { get; set; } = new();
        /// <summary>سهم سانس‌ها در هر بخش (نمودار دایره‌ای)</summary>
        public ChartShareDto TrafficShare { get; set; } = new();
        public ChartShareDto RegisterShare { get; set; } = new();
        public ChartShareDto TicketShare { get; set; } = new();
        public ChartShareDto ServiceShare { get; set; } = new();
        public ChartShareDto FinanceShare { get; set; } = new();
    }

    /// <summary>خلاصه یک سری برای کارت آماری: جمع دوره + جمع دوره قبل</summary>
    public class ChartSerieSummaryDto
    {
        public long Total { get; set; }
        public long PreviousTotal { get; set; }
        public string TotalDisplay { get; set; } = string.Empty;
    }

    /// <summary>یک برش نمودار دایره‌ای: سانس + تعداد/مبلغ + درصد</summary>
    public class ChartShareSliceDto
    {
        /// <summary>نام سانس (یا ترکیب سانس+تارفه)</summary>
        public string Label { get; set; } = string.Empty;
        public long Value { get; set; }
        public double Percent { get; set; }
        /// <summary>درصد برای نمایش (رشته فارسی)</summary>
        public string PercentDisplay { get; set; } = string.Empty;
    }

    /// <summary>سهم سانس‌ها از یک بخش برای نمودار دایره‌ای</summary>
    public class ChartShareDto
    {
        public List<ChartShareSliceDto> Slices { get; set; } = new();
        /// <summary>سایر موارد (برش‌های کوچک زیر ۲٪ که ادغام شده‌اند)</summary>
        public long OthersValue { get; set; }
        public string OthersPercentDisplay { get; set; } = string.Empty;
    }
    // ============================================================
    //  شرایط باشگاه (فقط ادمین)
    // ============================================================

    /// <summary>درخواست ذخیره متن شرایط باشگاه — فقط ادمین دسترسی دارد</summary>
    public class AdminSaveRulesRequest
    {
        [Required(ErrorMessage = "متن شرایط نمی‌تواند خالی باشد")]
        [MaxLength(5000, ErrorMessage = "حداکثر ۵۰۰۰ کاراکتر")]
        public string RulesText { get; set; } = "";
    }
}

