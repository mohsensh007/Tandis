using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// اطلاعات شخصی افراد (عضو، مربی، کارمند)
    /// معادل جدول dbo.Gen_Person در دیتابیس FullSportDB
    /// </summary>
    [Table("Gen_Person")]
    public class Gen_Person
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PersonID { get; set; }

        [MaxLength(50)]
        public string? FirstName { get; set; }

        [MaxLength(50)]
        public string? LastName { get; set; }

        [MaxLength(102)]
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        public string? FullName { get; set; }

        [MaxLength(50)]
        public string? FatherName { get; set; }

        /// <summary>
        /// true = مرد، false = زن
        /// </summary>
        public bool? Gender { get; set; }

        [MaxLength(10)]
        public string? NationalCode { get; set; }

        [MaxLength(10)]
        public string? Nidentity { get; set; }

        public byte[]? PersonImage { get; set; }
        public byte[]? ThumbnailImage { get; set; }

        [MaxLength(10)]
        public string? BirthDate { get; set; }

        [MaxLength(50)]
        public string? Tel { get; set; }

        [MaxLength(50)]
        public string? Mobile { get; set; }

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(150)]
        public string? Education { get; set; }

        [MaxLength(150)]
        public string? Job { get; set; }

        public bool? HasInsurance { get; set; }

        [MaxLength(50)]
        public string? InsuranceNo { get; set; }

        [MaxLength(10)]
        public string? InsStartDate { get; set; }

        [MaxLength(10)]
        public string? InsEndDate { get; set; }

        [MaxLength(200)]
        public string? PAddress { get; set; }

        public bool? HasParrent { get; set; }

        [MaxLength(150)]
        public string? TeamName { get; set; }

        public short? ShiftID { get; set; }
        public short? UserID { get; set; }

        [MaxLength(10)]
        public string? CreationDate { get; set; }

        [MaxLength(8)]
        public string? CreationTime { get; set; }

        public short? Modifier { get; set; }
        public DateTime? ModificationTime { get; set; }
    }
}
