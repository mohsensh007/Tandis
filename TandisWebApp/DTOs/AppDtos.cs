using System.ComponentModel.DataAnnotations;

namespace TandisWebApp.DTOs
{
    // ============================================================
    //  ثبت‌نام / تمدید
    // ============================================================

    public class SportCategoryDto
    {
        public int SportCatID { get; set; }
        public string SportName { get; set; } = string.Empty;
    }

    public class SportSanseDto
    {
        public int SportSanseID { get; set; }
        public string SportName { get; set; } = string.Empty;
        public string SanseName { get; set; } = string.Empty;
        public string? CoachName { get; set; }
        public string? MembershipTypeDesc { get; set; }
        public int SessionCountInPeriod { get; set; }
        public long TotalAmount { get; set; }
        public string TotalAmountDisplay { get; set; } = string.Empty;
        public string? PeriodDesc { get; set; }
        public bool HasActiveRegister { get; set; }
        public string? ActiveStatus { get; set; }
        public string? EndDate { get; set; }
        public int? RemainingSessions { get; set; }
    }

    public class RegisterRequest
    {
        public int SportSanseID { get; set; }

        [Required(ErrorMessage = "تاریخ شروع را وارد کنید")]
        public string StartDate { get; set; } = string.Empty;
    }

    public class RegisterResponse
    {
        public long SportMemberID { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public long FinalPayment { get; set; }
        public string FinalPaymentDisplay { get; set; } = string.Empty;
    }

    /// <summary>اطلاعات یک سانس فعال عضو (برای داشبورد)</summary>
    public class ActiveSanseDto
    {
        public long SportMemberID { get; set; }
        public int SportSanseID { get; set; }
        public string SportName { get; set; } = string.Empty;
        public string SanseName { get; set; } = string.Empty;
        public string? CoachName { get; set; }
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int TotalSessions { get; set; }
        public int UsedSessions { get; set; }
        public int RemainingSessions { get; set; }
        public string? MembershipTypeDesc { get; set; }
        public string? PeriodDesc { get; set; }
    }

    // ============================================================
    //  بلیط / جلسه آزاد / سرویس / فروشگاه
    // ============================================================

    public class TicketTarefeDto
    {
        public short TarefeID { get; set; }
        public string? Sans { get; set; }
        public string Tarefe { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
    }

    public class TicketBuyRequest
    {
        [Required]
        public short TarefeID { get; set; }
    }

    public class ServiceDto
    {
        public short ServiceID { get; set; }
        public string ServiceDesc { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
        public string? PicAddress { get; set; }
    }

    public class ServiceBuyRequest
    {
        [Required]
        public short ServiceID { get; set; }
    }

    public class StuffDto
    {
        public short StuffID { get; set; }
        public string StuffDesc { get; set; } = string.Empty;
        public long StuffAmount { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
        public string? PicAddress { get; set; }
        public int? StuffCategoryID { get; set; }
        public int AvailableCount { get; set; }
    }

    public class StuffCategoryDto
    {
        public int StuffCategoryID { get; set; }
        public string CategoryDesc { get; set; } = string.Empty;
        public string? PicAddress { get; set; }
    }

    public class BasketItem
    {
        public short StuffID { get; set; }
        public string Name { get; set; } = string.Empty;
        public long UnitPrice { get; set; }
        public int Count { get; set; }
        public long TotalPrice => UnitPrice * Count;
    }

    public class ShopBuyRequest
    {
        [Required]
        public List<BasketItem> Items { get; set; } = new();
        public bool IsBuffet { get; set; } = false;
    }

    public class AddMoneyRequest
    {
        [Required]
        [Range(10000, long.MaxValue, ErrorMessage = "حداقل مبلغ ۱۰٬۰۰۰ ریال است")]
        public long Amount { get; set; }

        [Required]
        public byte CreditTypeID { get; set; } // 11=ریالی، 2=فروشگاه، 3=سرویس
    }

    // ============================================================
    //  پروفایل / گزارش‌ها
    // ============================================================

    public class MemberProfileDto
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? NationalCode { get; set; }
        public string? Mobile { get; set; }
        public string? BirthDate { get; set; }
        public bool? Gender { get; set; }
        public string? PersonImageBase64 { get; set; }
        public long SportCredit { get; set; }
        public long BuffetCredit { get; set; }
        public long ServiceCredit { get; set; }
        public long TotalDebit { get; set; }
    }

    public class RegisterHistoryDto
    {
        public long SportMemberID { get; set; }
        public string SportSanseName { get; set; } = string.Empty;
        public string? CoachName { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public bool IsActive { get; set; }
        public long FinalPayment { get; set; }
        public string FinalPaymentDisplay { get; set; } = string.Empty;
        public short SessionCount { get; set; }
        public int? RemainingSessions { get; set; }
        public string CreationDate { get; set; } = string.Empty;
    }

    public class TrafficReportDto
    {
        public long TrafficID { get; set; }
        public string? EntryDate { get; set; }
        public string? EntryTime { get; set; }
        public string? ExitDate { get; set; }
        public string? ExitTime { get; set; }
        public string? PersonName { get; set; }
        public string? EntryDesc { get; set; }
        public short? BoxID { get; set; }
    }

    public class FinanceDocDto
    {
        public DateTime CreationTime { get; set; }
        public string CreationDateDisplay { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string AmountDisplay { get; set; } = string.Empty;
        public string DocType { get; set; } = string.Empty;
        public string DocDesc { get; set; } = string.Empty;
    }

    // ============================================================
    //  پروفایل - درخواست‌های اضافی
    // ============================================================

    public class PhotoUpdateRequest
    {
        public string PhotoBase64 { get; set; } = string.Empty;
    }

    public class ProfileUpdateRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Mobile { get; set; }
        public string? BirthDate { get; set; }
    }
}
