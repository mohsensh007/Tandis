using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// اطلاعات اعضای باشگاه
    /// معادل جدول dbo.Gen_Members در دیتابیس FullSportDB
    /// </summary>
    [Table("Gen_Members")]
    public class Gen_Member
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int MemberID { get; set; }

        [MaxLength(16)]
        public string? CardNo { get; set; }

        public int? PersonID { get; set; }
        public int? RoleID { get; set; }
        public short? UserID { get; set; }
        public short? ShiftID { get; set; }
        public bool? IsBlackList { get; set; }
        public byte? BoxRadifNo { get; set; }
        public bool? HasFinger { get; set; }

        [MaxLength(10)]
        public string? MembershipDate { get; set; }

        [MaxLength(8)]
        public string? MembershipTime { get; set; }

        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
        public bool? IsFamily { get; set; }
        public int? MaxDebit { get; set; }

        public byte[]? Minutiae { get; set; }
        public byte[]? Minutiae2 { get; set; }
        public byte[]? Minutiae3 { get; set; }

        public int? Salary { get; set; }

        public byte[]? FaceTmpl1 { get; set; }
        public byte[]? FaceTmpl2 { get; set; }
        public byte[]? FaceTmpl3 { get; set; }
        public byte[]? FaceTmpl4 { get; set; }
        public byte[]? FaceTmpl5 { get; set; }

        public bool? RemoveDeviceFlag { get; set; }

        /// <summary>
        /// رمز عبور کیوسک / وب‌اپ
        /// </summary>
        [MaxLength(50)]
        public string? KioskPass { get; set; }

        /// <summary>
        /// تخفیف مدیریتی (درصد)
        /// </summary>
        public byte? RegDiscount { get; set; }


        // Navigation
        [ForeignKey(nameof(PersonID))]
        public virtual Gen_Person? Gen_Person { get; set; }
    }
}
