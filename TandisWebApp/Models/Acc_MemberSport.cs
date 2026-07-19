using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// ثبت‌نام اعضا در دوره‌های ورزشی
    /// معادل جدول dbo.Acc_MemberSports
    /// </summary>
    [Table("Acc_MemberSports")]
    public class Acc_MemberSport
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long SportMemberID { get; set; }

        public int? MemberID { get; set; }
        public int? SportSanseID { get; set; }
        public short? MembershipTypeID { get; set; }
        public int? ContractID { get; set; }
        public short? SessionCount { get; set; }
        public long? Amount { get; set; }
        public int? Tax { get; set; }
        public long? DiscountAmount { get; set; }
        public long? RegDiscountPercent { get; set; }
        public long? RegDiscountAmount { get; set; }
        public long? FinalPayment { get; set; }

        public byte? CoachPercent { get; set; }
        public long? CoachAmount { get; set; }
        public byte? CoachPercentForRevival { get; set; }
        public long? CoachRevivalAmount { get; set; }

        public short? PeriodID { get; set; }

        [MaxLength(10)]
        public string? StartDate { get; set; }

        [MaxLength(10)]
        public string? EndDate { get; set; }

        public bool? IsActive { get; set; }
        public bool? IsRevival { get; set; }

        public string? CommentText { get; set; }
        public short? UserID { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(10)]
        public string? CreationTime { get; set; }

        public short? Modifier { get; set; }
        public DateTime? ModificationTime { get; set; }
        public bool? ReserveByMember { get; set; }


        // Navigation
        [ForeignKey(nameof(MemberID))]
        public virtual Gen_Member? Gen_Member { get; set; }

        [ForeignKey(nameof(SportSanseID))]
        public virtual Gen_SportSanse? Gen_SportSanse { get; set; }
    }
}
