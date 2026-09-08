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
}

