using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// سانس‌های ورزشی (تعریف دوره‌های ورزشی با قیمت و مربی)
    /// معادل جدول dbo.Gen_SportSanse
    /// </summary>
    [Table("Gen_SportSanse")]
    public class Gen_SportSanse
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SportSanseID { get; set; }

        public int? SportCatID { get; set; }

        [MaxLength(200)]
        public string? SanseName { get; set; }

        public short? MembershipTypeID { get; set; }
        public int? CoachMemberID { get; set; }
        public byte? CoachMoneyPercent { get; set; }
        public byte? CoachPercentForRevival { get; set; }
        public short? SalonID { get; set; }
        public short? PeriodID { get; set; }
        public short? SessionCountInPeriod { get; set; }
        public int? ClassCapacity { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public int? FreeCapacity { get; set; }

        public long? TotalAmount { get; set; }
        public byte? TaxRate { get; set; }
        public long? SessionAmount { get; set; }
        public bool? IsActive { get; set; }

        public TimeSpan? DueTime { get; set; }
        public int? ExtraAmountPerMin { get; set; }
        public long? GeneralAmount { get; set; }
        public short? LockerRoomID { get; set; }
        public short? StartBoxNo { get; set; }
        public short? EndBoxNo { get; set; }
        public bool? ShowInKiosk { get; set; }
        public short? UserID { get; set; }
        public short? ShiftID { get; set; }
        public DateTime? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }


        // Navigation
        [ForeignKey(nameof(SportCatID))]
        public virtual Gen_Sport_Category? Gen_Sport_Category { get; set; }

        [ForeignKey(nameof(CoachMemberID))]
        public virtual Gen_Member? Gen_Member { get; set; }

        [ForeignKey(nameof(MembershipTypeID))]
        public virtual Gen_MembershipType? Gen_MembershipType { get; set; }

        [ForeignKey(nameof(PeriodID))]
        public virtual Gen_Period? Gen_Period { get; set; }

        [ForeignKey(nameof(SalonID))]
        public virtual Gen_Salon? Gen_Salon { get; set; }
    }
}
