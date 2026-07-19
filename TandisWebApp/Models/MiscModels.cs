using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>معادل dbo.Gen_MembershipType - نوع عضویت (میان‌مدت/نامحدود و ...)</summary>
    [Table("Gen_MembershipType")]
    public class Gen_MembershipType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public short MembershipTypeID { get; set; }

        [MaxLength(50)]
        public string? MembershipTypeDesc { get; set; }
    }

    /// <summary>معادل dbo.Gen_Period - دوره‌ها (یک‌ماهه، سه‌ماهه، ...)</summary>
    [Table("Gen_Period")]
    public class Gen_Period
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public short PeriodID { get; set; }

        [MaxLength(50)]
        public string? Description { get; set; }

        public int? DayCount { get; set; }
    }

    /// <summary>معادل dbo.Gen_Salon - سالن‌ها</summary>
    [Table("Gen_Salon")]
    public class Gen_Salon
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short SalonID { get; set; }

        [MaxLength(100)]
        public string? SalonDesc { get; set; }

        public bool? IsAcitve { get; set; }
        public short? Creator { get; set; }
        public DateTime? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }

    /// <summary>معادل dbo.Gen_Shift - شیفت‌ها (مردانه/زنانه)</summary>
    [Table("Gen_Shift")]
    public class Gen_Shift
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short ShiftID { get; set; }

        [MaxLength(50)]
        public string? ShiftDesc { get; set; }
    }

    /// <summary>معادل dbo.Gen_Contract - قراردادها</summary>
    [Table("Gen_Contract")]
    public class Gen_Contract
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ContractID { get; set; }

        [MaxLength(50)]
        public string? ContractName { get; set; }

        public byte? ContractDiscountRate { get; set; }
        public int? DiscountAmount { get; set; }
        public bool? ContractStatus { get; set; }
        public short? UserID { get; set; }
        public short? ShiftID { get; set; }
        public string? CreationDate { get; set; }
        public string? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }

    /// <summary>معادل dbo.Gen_San - سانس‌ها (صبح/ظهر/عصر)</summary>
    [Table("Gen_Sans")]
    public class Gen_San
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short SansID { get; set; }

        [MaxLength(100)]
        public string? Sans { get; set; }

        public bool? IsActive { get; set; }
        public short? ShiftID { get; set; }
        public short? UserID { get; set; }
        public DateTime? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }

    /// <summary>معادل dbo.Gen_Tarefe - تعرفه‌های بلیط</summary>
    [Table("Gen_Tarefe")]
    public class Gen_Tarefe
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short TarefeID { get; set; }

        public short? SansID { get; set; }

        [MaxLength(100)]
        public string? Tarefe { get; set; }

        public long? Amount { get; set; }
        public bool? IsActive { get; set; }
        public int? SportSanseID { get; set; }
        public short? ShiftID { get; set; }
        public short? UserID { get; set; }
        public DateTime? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }


        [ForeignKey(nameof(SansID))]
        public virtual Gen_San? Gen_San { get; set; }
    }

    /// <summary>معادل dbo.Gen_Service - سرویس‌ها (ماساژ، مربیگری شخصی، ...)</summary>
    [Table("Gen_Service")]
    public class Gen_Service
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short ServiceID { get; set; }

        [MaxLength(200)]
        public string? ServiceDesc { get; set; }

        public long? Amount { get; set; }
        public byte? Refpercent { get; set; }
        public int? RefMemberID { get; set; }
        public short? UserID { get; set; }
        public short? ShiftID { get; set; }

        [MaxLength]
        public string? PicAddress { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(10)]
        public string? CreationTime { get; set; }

        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }

    /// <summary>معادل dbo.Gen_Stuff_Category - دسته‌بندی اجناس فروشگاه/بوفه</summary>
    [Table("Gen_Stuff_Category")]
    public class Gen_Stuff_Category
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int StuffCategoryID { get; set; }

        [Required]
        [MaxLength(200)]
        public string CategoryDesc { get; set; } = string.Empty;

        [MaxLength]
        public string? PicAddress { get; set; }
    }

    /// <summary>معادل dbo.Gen_Stuff - اجناس فروشگاه/بوفه</summary>
    [Table("Gen_Stuff")]
    public class Gen_Stuff
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public short StuffID { get; set; }

        public int? StuffCategoryID { get; set; }

        [MaxLength(50)]
        public string? StuffDesc { get; set; }

        public long? StuffAmount { get; set; }

        public bool IsBuffet { get; set; }

        [MaxLength(1000)]
        public string? PicAddress { get; set; }

        public short? ShiftID { get; set; }
        public short? UserID { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(10)]
        public string? CreationTime { get; set; }

        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }

    /// <summary>معادل dbo.Gen_SportSanseDetail - ساعات کلاس‌ها در هر روز هفته</summary>
    [Table("Gen_SportSanseDetail")]
    public class Gen_SportSanseDetail
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SportSanseDetailID { get; set; }

        public int? SportSanseID { get; set; }
        public byte? DayID { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public bool? IsActive { get; set; }
        public short? UserID { get; set; }
        public DateTime? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }

    /// <summary>معادل dbo.Gen_DayOfWeek - روزهای هفته</summary>
    [Table("Gen_DayOfWeek")]
    public class Gen_DayOfWeek
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public byte DayID { get; set; }

        [MaxLength(50)]
        public string? DayName { get; set; }

        [MaxLength(50)]
        public string? LatinName { get; set; }
    }
}
