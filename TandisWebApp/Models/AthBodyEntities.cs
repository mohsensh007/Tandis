using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>
    /// پروفایل تحلیل بدن عضو — هر عضو یک ردیف.
    /// اطلاعات «نام / سن / جنسیت» از Gen_Person خوانده می‌شود (اینجا ذخیره نمی‌شود).
    /// معادل جدول dbo.Ath_BodyProfile
    /// </summary>
    [Table("Ath_BodyProfile")]
    public class Ath_BodyProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int MemberID { get; set; }

        /// <summary>قد (سانتی‌متر)</summary>
        public decimal? HeightCm { get; set; }

        /// <summary>میزان فعالیت روزانه: 1=کم‌تحرک … 5=خیلی زیاد</summary>
        public byte ActivityLevel { get; set; } = 2;

        /// <summary>سابقه ورزشی: 0=بدون سابقه … 5=بیش از ۳ سال</summary>
        public byte SportHistory { get; set; }

        /// <summary>هدف ورزشی: 1=چربی‌سوزی، 2=عضله‌سازی، 3=حفظ تناسب</summary>
        public byte Goal { get; set; } = 3;

        /// <summary>تعداد جلسات تمرین در هفته</summary>
        public byte SessionsPerWeek { get; set; } = 3;

        public DateTime? CreationDate { get; set; }
        public DateTime? ModificationTime { get; set; }
    }

    /// <summary>
    /// اندازه‌گیری‌های بدن عضو در طول زمان (برای نمودار تغییرات).
    /// یک عضو می‌تواند چندین ردیف داشته باشد؛ ثبت مجدد در همان تاریخ، همان ردیف را به‌روز می‌کند.
    /// معادل جدول dbo.Ath_BodyRecord
    /// </summary>
    [Table("Ath_BodyRecord")]
    public class Ath_BodyRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RecordID { get; set; }

        public int MemberID { get; set; }

        /// <summary>تاریخ اندازه‌گیری — شمسی YYYY/MM/DD</summary>
        [MaxLength(10)]
        public string RecordDate { get; set; } = string.Empty;

        public DateTime? RecordDateTime { get; set; }

        /// <summary>وزن (کیلوگرم)</summary>
        public decimal? WeightKg { get; set; }

        /// <summary>دور کمر (سانتی‌متر)</summary>
        public decimal? WaistCm { get; set; }

        /// <summary>دور شکم (سانتی‌متر)</summary>
        public decimal? AbdomenCm { get; set; }

        /// <summary>دور سینه (سانتی‌متر)</summary>
        public decimal? ChestCm { get; set; }

        /// <summary>دور بازو (سانتی‌متر)</summary>
        public decimal? ArmCm { get; set; }

        /// <summary>دور ران (سانتی‌متر)</summary>
        public decimal? ThighCm { get; set; }

        /// <summary>دور باسن (سانتی‌متر)</summary>
        public decimal? HipCm { get; set; }

        [ForeignKey(nameof(MemberID))]
        public virtual Ath_BodyProfile? Profile { get; set; }
    }
}
