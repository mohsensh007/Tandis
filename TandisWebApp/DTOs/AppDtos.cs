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
        public int SportCatID { get; set; }  
    }

    public class RegisterRequest
    {
        public int SportSanseID { get; set; }

        [Required(ErrorMessage = "تاریخ شروع را وارد کنید")]
        public string StartDate { get; set; } = string.Empty;

        // credit = پرداخت از اعتبار | pos = پرداخت با دستگاه کارخوان
        public string Method { get; set; } = "credit";
        public long? PosTxnId { get; set; }
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

        // credit = پرداخت از اعتبار | pos = پرداخت با دستگاه کارخوان
        public string Method { get; set; } = "credit";
        public long? PosTxnId { get; set; }
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

        // credit = پرداخت از اعتبار | pos = پرداخت با دستگاه کارخوان
        public string Method { get; set; } = "credit";
        public long? PosTxnId { get; set; }
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

        // credit = پرداخت از اعتبار | pos = پرداخت با دستگاه کارخوان
        public string Method { get; set; } = "credit";
        public long? PosTxnId { get; set; }
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
        public bool IsRevival { get; set; }
        public string TypeDesc => IsRevival ? "تمدید" : "ثبت‌نام";
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
        public byte? TrafficStatus { get; set; }
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
    // ============================================================
    //  تک جلسه (Single Session)
    // ============================================================

    public class SingleSessionSanseDto
    {
        public int SportSanseID { get; set; }
        public string SportName { get; set; } = string.Empty;
        public string SanseName { get; set; } = string.Empty;
        public string? CoachName { get; set; }
        public long SessionAmount { get; set; }
        public string SessionAmountDisplay { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public string LatinName { get; set; } = string.Empty;          
        public string SessionDateShamsi { get; set; } = string.Empty;  
    }

    public class SingleSessionBuyRequest
    {
        [Required]
        public int SportSanseID { get; set; }
        public string? SessionDateShamsi { get; set; }  

        // credit = پرداخت از اعتبار | pos = پرداخت با دستگاه کارخوان
        public string Method { get; set; } = "credit";
        public long? PosTxnId { get; set; }
    }

    /// <summary>وضعیت تراکنش POS وب‌اپ (برای poll کردن از کلاینت)</summary>
    public class PosPaymentStatusDto
    {
        // pending | success | failed | expired | notfound
        public string Status { get; set; } = "pending";
        public string Message { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string TraceNumber { get; set; } = string.Empty;
        public long Amount { get; set; }

        /// <summary>true = Agent این تراکنش را هم‌روی دستگاه فرستاده → کارت بکشید</summary>
        public bool Processing { get; set; }

        /// <summary>تعداد تراکنش‌های در انتظار جلوتر از این تراکنش در صف</summary>
        public int QueuePosition { get; set; }
    }
    
    // ============================================================
    //  ثبت‌نام عضو جدید
    // ============================================================

    public class RegisterMemberRequest
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string NationalCode { get; set; } = "";
        public string BirthDate { get; set; } = "";   // 1375/04/12
        public string Mobile { get; set; } = "";
        public bool Gender { get; set; }              // true = آقا، false = خانم
    }

    public class RegisterMemberResponse
    {
        public int MemberID { get; set; }
        public string NationalCode { get; set; } = "";
        public string DefaultPassword { get; set; } = "";
    }

    // ============================================================
    //  ارسال دستور ثبت چهره به دستگاه تشخیص چهره
    // ============================================================

    public class FaceCommandRequest
    {
        public int MemberID { get; set; }
    }

    public class FaceCommandResponse
    {
        public int MemberID { get; set; }
        public int GateDeviceID { get; set; }
        public string? DeviceIp { get; set; }
        public string? DevicePort { get; set; }
        public string? SerialNo { get; set; }
        public short? TerminalNo { get; set; }
        public bool DeviceReachable { get; set; }
        public DateTime? ValidUntil { get; set; }

        /// <summary>آیا قبل از ارسال دستور، چهره این عضو قبلاً ثبت شده بود؟</summary>
        public bool HadFaceBefore { get; set; }

        /// <summary>آیا Traffic.exe (یا پورت 8085) در دسترس بود؟</summary>
        public bool TrafficRunning { get; set; }

        /// <summary>آیا FullSport.exe در حال اجرا بود؟</summary>
        public bool FullSportRunning { get; set; }

        /// <summary>آیا پورت 8085 روی سرور باز بود؟</summary>
        public bool Port8085Open { get; set; }

        /// <summary>سروری که دستگاه/Traffic روی آن است (ServerIP دستگاه)</summary>
        public string? ServiceHost { get; set; }
    }

    /// <summary>وضعیت ثبت چهره (برای بررسی نتیجه بعد از ارسال دستور)</summary>
    public class FaceStatusResponse
    {
        public int MemberID { get; set; }
        public bool MemberFound { get; set; }

        /// <summary>چهره در دیتابیس ثبت شده (FaceTmpl1..3 پر باشد)</summary>
        public bool HasFace { get; set; }

        /// <summary>دستور addface هنوز در Gen_Setting پابرجاست (هنوز برداشته نشده)</summary>
        public bool CommandPending { get; set; }

        /// <summary>دستور قبلاً برداشته شده (فلگ پاک شده یا مال عضو دیگری است)</summary>
        public bool CommandConsumed { get; set; }

        /// <summary>زمان ثبت روی دستگاه (TmpMembersTbl.RegisterToDeviceTime) در صورت وجود</summary>
        public DateTime? RegisteredToDeviceAt { get; set; }
    }
}
