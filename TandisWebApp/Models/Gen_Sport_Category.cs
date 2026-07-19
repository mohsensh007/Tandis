using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// دسته‌بندی ورزش‌ها (فوتسال، بدنسازی، شنا و ...)
    /// معادل جدول dbo.Gen_Sport_Category
    /// </summary>
    [Table("Gen_Sport_Category")]
    public class Gen_Sport_Category
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int SportCatID { get; set; }

        [MaxLength(50)]
        public string? SportName { get; set; }

        public int? ColorRgb { get; set; }
        public bool? SetBox { get; set; }
        public bool? PrintTicket { get; set; }
        public short? UserID { get; set; }
        public short? ShiftID { get; set; }
        public short? LockerRoomID { get; set; }
        public short? StartBoxNo { get; set; }
        public short? EndBoxNo { get; set; }
        public bool? IsActive { get; set; }

        /// <summary>
        /// نمایش در کیوسک
        /// </summary>
        public bool? ShowInKiosk { get; set; }

        public DateTime? CreationDate { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }
}
