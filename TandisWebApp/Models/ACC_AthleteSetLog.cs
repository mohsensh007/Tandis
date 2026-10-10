using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>دفترچه تمرین ورزشکار (لاگ تیک + زمان هر حرکت در یک روز).</summary>
    [Table("ACC_AthleteSetLog")]
    public class ACC_AthleteSetLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogID { get; set; }

        public int MemberID { get; set; }
        public int PrgID { get; set; }
        public int ItemID { get; set; }

        /// <summary>تاریخ شمسی به فرمت رشته (مثل 1405/07/16) — هم‌فرمت با CommonHelperService.GetToday()</summary>
        [StringLength(10)]
        public string LogDate { get; set; } = string.Empty;

        public int DurationSec { get; set; }
        public bool IsDone { get; set; }

        [StringLength(200)]
        public string? Note { get; set; }

        public DateTime CreationDateTime { get; set; }
    }
}