using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// تسویه حساب - بستانکار (پرداخت‌ها، شارژها)
    /// معادل جدول dbo.Cash_CreditStatment
    /// </summary>
    [Table("Cash_CreditStatment")]
    public class Cash_CreditStatment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long CreditID { get; set; }

        public int? MemberID { get; set; }
        public long? TrafficID { get; set; }
        public byte? CreditTypeID { get; set; }
        public long? Amount { get; set; }
        public bool? IsPos { get; set; }
        public byte? PosID { get; set; }
        public bool? IsPcPos { get; set; }
        public bool? IsFische { get; set; }

        [MaxLength(50)]
        public string? ReceiptNo { get; set; }

        [MaxLength(300)]
        public string? CreditDesc { get; set; }

        public int? DebitID { get; set; }
        public short? UserID { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(8)]
        public string? CreationTime { get; set; }

        public short? Modifier { get; set; }
        public DateTime? ModificationTime { get; set; }
    }

    /// <summary>
    /// تسویه حساب - بدهکار (خریدها)
    /// معادل جدول dbo.Cash_DebitStatement
    /// </summary>
    [Table("Cash_DebitStatement")]
    public class Cash_DebitStatement
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DebitID { get; set; }

        public int? MemberID { get; set; }
        public byte? DebitTypeID { get; set; }
        public long? TrafficID { get; set; }
        public long? RefID { get; set; }
        public long? Amount { get; set; }

        [MaxLength(300)]
        public string? DebitDesc { get; set; }

        public short? UserID { get; set; }
        public DateTime? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? ModificationTime { get; set; }
    }

    /// <summary>
    /// بازپرداخت‌ها
    /// معادل جدول dbo.Cash_RefundStatement
    /// </summary>
    [Table("Cash_RefundStatement")]
    public class Cash_RefundStatement
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long RefundID { get; set; }

        public byte? RefundTypeID { get; set; }
        public int? MemberID { get; set; }
        public long? TrafficID { get; set; }
        public long? Amount { get; set; }

        [MaxLength(1000)]
        public string? RefundDesc { get; set; }

        public short? ShiftID { get; set; }
        public short? UserID { get; set; }
        public DateTime? CreationTime { get; set; }

        [MaxLength(10)]
        public string? PersianDate { get; set; }

        public short? Modifier { get; set; }
        public DateTime? ModificationTime { get; set; }

        [MaxLength(10)]
        public string? ModifyPersianDate { get; set; }
    }

    /// <summary>معادل dbo.Cash_CreditType - انواع بستانکار</summary>
    [Table("Cash_CreditType")]
    public class Cash_CreditType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public byte CreditTypeID { get; set; }

        [MaxLength(50)]
        public string? CreditType { get; set; }
    }

    /// <summary>معادل dbo.Cash_DebitType - انواع بدهکار</summary>
    [Table("Cash_DebitType")]
    public class Cash_DebitType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public byte DebitTypeID { get; set; }

        [MaxLength(100)]
        public string? DebitType { get; set; }
    }

    /// <summary>معادل dbo.Cash_RefundType - انواع بازپرداخت</summary>
    [Table("Cash_RefundType")]
    public class Cash_RefundType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public byte RefundTypeID { get; set; }

        [MaxLength(50)]
        public string? RefundType { get; set; }
    }

    /// <summary>معادل dbo.Gen_PosTbl - تعریف دستگاه‌های POS</summary>
    [Table("Gen_PosTbl")]
    public class Gen_PosTbl
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public byte PosID { get; set; }

        [MaxLength(150)]
        public string? PosName { get; set; }

        public long? TerminalID { get; set; }

        [MaxLength(50)]
        public string? PosIPaddrss { get; set; }

        [MaxLength(50)]
        public string? Psp { get; set; }
    }

    /// <summary>معادل dbo.Gen_Setting - تنظیمات کلی سیستم</summary>
    [Table("Gen_Setting")]
    public class Gen_Setting
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public byte id { get; set; }

        public int CardDelayMinute { get; set; }

        [MaxLength(50)]
        public string? ServerIP { get; set; }

        [MaxLength(1500)]
        public string? BackupPath { get; set; }

        public int? CardAmount { get; set; }
        public int? InsuranceAmount { get; set; }
        public bool? ContinuousMemberID { get; set; }
        public bool? WelcomeSMS { get; set; }
        public bool? RegisterSMS { get; set; }
        public bool? PrintTraffice { get; set; }
        public int? MinCashAmount { get; set; }
        public int? MaxDebit { get; set; }
        public bool? ActiveVoice { get; set; }
        public bool? ExtraSession { get; set; }
        public bool? TicketForExit { get; set; }
        public bool? AutoAddExtraDebit { get; set; }
        public bool? IsCommonBuffet { get; set; }

        [MaxLength]
        public string? WebGUID { get; set; }

        public bool? LockSystem { get; set; }
        public bool? BuffetDefault { get; set; }
        public bool? EditServiceAmount { get; set; }
        public bool? IsWelcomePopup { get; set; }
        public DateTime? LastFingerUpdateTime { get; set; }
        public bool? LockRegisterDate { get; set; }
        public bool? ShowSessionCountInMonitor2 { get; set; }
        public byte? Monitor2_Display { get; set; }
        public byte? SportPopup_Display { get; set; }
        public bool? ShowMultipleSportPopUp { get; set; }
        public TimeSpan? AutoExitTime { get; set; }
        public bool? CheckTimeForCoach { get; set; }
        public int? MembershipAmount { get; set; }

        [MaxLength(50)]
        public string? SMSWebservice { get; set; }

        [MaxLength(50)]
        public string? SMSUser { get; set; }

        [MaxLength(50)]
        public string? SMSPass { get; set; }

        [MaxLength(50)]
        public string? SMSNum { get; set; }

        [MaxLength(200)]
        public string? SMSToken { get; set; }

        public bool? SMSForEndSession { get; set; }
        public bool? HasLEDBox { get; set; }
        public bool? ShowClassInQuickReg { get; set; }
        public int? MemberIDForTrafficApp { get; set; }
        public int? TerminalIDForTrafficApp { get; set; }

        [MaxLength(50)]
        public string? ActionForTrafficApp { get; set; }

        public DateTime? ActionValidTime { get; set; }
        public bool? AutoSendBirthdaySms { get; set; }
        public DateTime? InsuranceAlertLastTime { get; set; }
        public bool? CheckDebitForEntry { get; set; }
        public bool? SetBoxForPersonel { get; set; }
        public bool? SetBoxForCoach { get; set; }
        public bool? SetBoxForUser { get; set; }
        public bool? RepeatTrafficForCoach { get; set; }
        public bool? RepeatTrafficForPersonel { get; set; }
    }

    /// <summary>معادل dbo.Sec_Systems - اطلاعات باشگاه</summary>
    [Table("Sec_Systems")]
    public class Sec_Systems
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int SystemID { get; set; }

        [MaxLength(100)]
        public string? ClubName { get; set; }

        public string? SystemCode { get; set; }

        [MaxLength(50)]
        public string? ActiveCardCode { get; set; }

        [MaxLength(50)]
        public string? ActiveCountCode { get; set; }

        [MaxLength(50)]
        public string? DebugCode { get; set; }

        [MaxLength(50)]
        public string? DeleteCardCode { get; set; }

        [MaxLength(100)]
        public string? ManagerName { get; set; }

        [MaxLength]
        public string? BgImage { get; set; }

        [MaxLength]
        public string? Logo { get; set; }
    }

    /// <summary>معادل dbo.ACC_Freez - فریز کردن دوره ورزشی</summary>
    [Table("ACC_Freez")]
    public class ACC_Freez
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long FreezID { get; set; }

        public long? SportMemberID { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public short? FreezDay { get; set; }

        [MaxLength(500)]
        public string? FreezDesc { get; set; }

        public short UserID { get; set; }
        public DateTime? CreationTime { get; set; }
        public bool? IsCancel { get; set; }
        public DateTime? CancelDate { get; set; }
        public short? Modifier { get; set; }
    }

    /// <summary>معادل dbo.CmbDays/CmbMonth/CmbYear - پرکننده دراپ‌داون تاریخ</summary>
    [Table("CmbDay")]
    public class CmbDay
    {
        [Key]
        [MaxLength(2)]
        public string Day { get; set; } = string.Empty;
    }

    [Table("CmbMonth")]
    public class CmbMonth
    {
        [Key]
        [MaxLength(2)]
        public string Month { get; set; } = string.Empty;
    }

    [Table("CmbYear")]
    public class CmbYear
    {
        [Key]
        public int Year { get; set; }
    }
}
