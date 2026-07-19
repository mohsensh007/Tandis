using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// بلیت‌ها (خرید سانس تک‌بار برای میهمان یا عضو)
    /// معادل جدول dbo.ACC_Ticket
    /// </summary>
    [Table("ACC_Ticket")]
    public class ACC_Ticket
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TicketID { get; set; }

        [MaxLength(50)]
        public string? FullName { get; set; }

        public int? TicketNo { get; set; }
        public short? SansID { get; set; }
        public short? TarefeID { get; set; }
        public byte? PersonCount { get; set; }
        public long? Amount { get; set; }
        public bool? IsPos { get; set; }
        public byte? PosID { get; set; }
        public short? UserID { get; set; }
        public short? ShiftID { get; set; }
        public DateTime? CreationTime { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        public byte? KidCount { get; set; }

        [MaxLength(50)]
        public string? TicketDesc { get; set; }

        public byte? CardTypeID { get; set; }
        public short? CardCount { get; set; }
        public short? Modifier { get; set; }
        public DateTime? ModificationTime { get; set; }


        [ForeignKey(nameof(SansID))]
        public virtual Gen_San? Gen_San { get; set; }

        [ForeignKey(nameof(TarefeID))]
        public virtual Gen_Tarefe? Gen_Tarefe { get; set; }
    }

    /// <summary>
    /// تردد (ورود/خروج) اعضا
    /// معادل جدول dbo.ACC_Traffic
    /// </summary>
    [Table("ACC_Traffic")]
    public class ACC_Traffic
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long TrafficID { get; set; }

        public int? MemberID { get; set; }
        public long? SportMemberID { get; set; }
        public int? TicketID { get; set; }
        public int? FreeSportSansID { get; set; }
        public byte? TrafficStatus { get; set; }

        [MaxLength(50)]
        public string? PersonName { get; set; }

        public decimal? Amount { get; set; }
        public int? ContractID { get; set; }
        public byte? DiscountRate { get; set; }
        public byte? CoachPercent { get; set; }
        public long? CoachAmount { get; set; }
        public bool? IsGuest { get; set; }
        public bool? IsPos { get; set; }
        public byte? PosID { get; set; }

        [MaxLength(200)]
        public string? EntryDesc { get; set; }

        public short? UserID { get; set; }
        public short? ShiftID { get; set; }
        public DateTime? EntryDateTime { get; set; }

        [MaxLength(10)]
        public string? EntryDate { get; set; }

        [MaxLength(8)]
        public string? EntryTime { get; set; }

        public DateTime? ExitDateTime { get; set; }

        [MaxLength(10)]
        public string? ExitDate { get; set; }

        [MaxLength(10)]
        public string? ExitTime { get; set; }

        public bool? IsManual { get; set; }
        public short? BoxID { get; set; }
        public int? ExtraMinute { get; set; }
        public long? ExtraAmount { get; set; }
        public bool? IsExtra { get; set; }
        public int? EnterGateDeviceID { get; set; }
        public int? ExitGateDeviceID { get; set; }

        [MaxLength(50)]
        public string? CardSerialNo { get; set; }
    }

    /// <summary>
    /// خرید سرویس (ماساژ، مربیگری و ...)
    /// معادل جدول dbo.ACC_MemberService
    /// </summary>
    [Table("ACC_MemberService")]
    public class ACC_MemberService
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MemberServiceID { get; set; }

        public long? TrafficID { get; set; }
        public short? ServiceID { get; set; }
        public short? SalonID { get; set; }
        public short? UserID { get; set; }
        public short? ShiftID { get; set; }
        public long? ServiceAmount { get; set; }
        public bool? IsReceipt { get; set; }
        public bool? IsPosReceipt { get; set; }

        [MaxLength(200)]
        public string? ServiceDesc { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(10)]
        public string? CreationTime { get; set; }

        public DateTime? AcceptedDate { get; set; }
        public int? AgentMemberID { get; set; }
        public byte? CommissionPercent { get; set; }
        public long? CommissionAmount { get; set; }


        [ForeignKey(nameof(ServiceID))]
        public virtual Gen_Service? Gen_Service { get; set; }
    }

    /// <summary>
    /// فاکتور فروشگاه/بوفه (هدر)
    /// معادل جدول dbo.Acc_BuffetFactor
    /// </summary>
    [Table("Acc_BuffetFactor")]
    public class Acc_BuffetFactor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long BuffetFactorID { get; set; }

        public long? TrafficID { get; set; }
        public int? MemberID { get; set; }
        public long? DiscountAmount { get; set; }
        public long? BuffetFactorTotalAmount { get; set; }

        [MaxLength(50)]
        public string? PersonName { get; set; }

        public bool? IsReceipt { get; set; }
        public bool? IsPosReceipt { get; set; }
        public bool? IsClose { get; set; }
        public short? ShiftID { get; set; }
        public short? UserID { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(10)]
        public string? CreationTime { get; set; }
    }

    /// <summary>
    /// ردیف‌های فاکتور فروشگاه/بوفه
    /// معادل جدول dbo.Acc_BuffetFactorDetail
    /// </summary>
    [Table("Acc_BuffetFactorDetail")]
    public class Acc_BuffetFactorDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long BuffetFactorDetailID { get; set; }

        public long? BuffetFactorID { get; set; }
        public short? StuffID { get; set; }
        public long? Amount { get; set; }
        public int? SCount { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(10)]
        public string? CreationTime { get; set; }


        [ForeignKey(nameof(StuffID))]
        public virtual Gen_Stuff? Gen_Stuff { get; set; }
    }

    /// <summary>
    /// تراکنش POS (کارخوان)
    /// معادل جدول dbo.ACC_PosTransaction
    /// </summary>
    [Table("ACC_PosTransaction")]
    public class ACC_PosTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long PcPosTransactionID { get; set; }

        public byte? PosID { get; set; }
        public decimal? MainAmount { get; set; }
        public decimal? AffectiveAmount { get; set; }

        [MaxLength(150)]
        public string? ResponseCode { get; set; }

        [MaxLength(150)]
        public string? Message { get; set; }

        [MaxLength(150)]
        public string? CardNumberMask { get; set; }

        [MaxLength(150)]
        public string? CardNumberHash { get; set; }

        [MaxLength(150)]
        public string? TerminalID { get; set; }

        [MaxLength(150)]
        public string? TraceNumber { get; set; }

        [MaxLength(150)]
        public string? SerialID { get; set; }

        [MaxLength(150)]
        public string? RRN { get; set; }

        [MaxLength(150)]
        public string? TransactionDate { get; set; }

        public long? RefID { get; set; }

        [MaxLength(50)]
        public string? RefType { get; set; }
    }

    /// <summary>
    /// ثبت تغییر رمز عبور کیوسک
    /// معادل جدول dbo.Kiosk_ChangePassLog
    /// </summary>
    [Table("Kiosk_ChangePassLog")]
    public class Kiosk_ChangePassLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ChangePassLogID { get; set; }

        public int MemberID { get; set; }
        public DateTime? ChangeDate { get; set; }
    }

    /// <summary>معادل dbo.DiscountCardType</summary>
    [Table("DiscountCardType")]
    public class DiscountCardType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public byte CardTypeID { get; set; }

        [MaxLength(250)]
        public string? CardType { get; set; }

        public bool? IsActive { get; set; }
    }
}
