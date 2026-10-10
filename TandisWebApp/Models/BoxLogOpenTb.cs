using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// لاگ باز کردن کمدها (جدول خود باشگاه — از سال ۱۴۰۰).
    /// ✅ دقیقاً مطابق ساختار واقعی dbo.BoxLogOpenTb.
    /// </summary>
    [Table("BoxLogOpenTb")]
    public class BoxLogOpenTb
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long LogID { get; set; }

        /// <summary>شناسه‌ی کمد (FK به Gen_Box) — nullable</summary>
        public short? BoxID { get; set; }

        /// <summary>شناسه‌ی عضو — nullable</summary>
        public int? MemberID { get; set; }

        /// <summary>شناسه‌ی دستگاه/ترمینال — nullable</summary>
        public int? TerminalID { get; set; }

        /// <summary>زمان باز شدن — nullable</summary>
        public DateTime? OpenDate { get; set; }

        /// <summary>شناسه‌ی رختکن — nullable</summary>
        public short? LockerRoomID { get; set; }

        /// <summary>شماره‌ی کمد — nullable</summary>
        public short? BoxNo { get; set; }

        /// <summary>آیا باز شدن موفق بود؟ (NOT NULL)</summary>
        public bool IsSuccess { get; set; }

        /// <summary>پیام نتیجه (موفق/خطا) — nullable</summary>
        [StringLength(200)]
        public string? Message { get; set; }

        /// <summary>زمان ثبت رکورد — nullable</summary>
        public DateTime? CreationDateTime { get; set; }
    }
}