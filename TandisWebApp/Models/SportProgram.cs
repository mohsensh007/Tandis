using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TandisWebApp.Models
{
    /// <summary>بانک حرکات (کتالوگ آیتم‌های تمرینی)</summary>
    [Table("Gen_PrgmItem")]
    public class Gen_PrgmItem
    {
        [Key]
        public int ItemID { get; set; }
        public string? ItemDesc { get; set; }
        public short? ShiftID { get; set; }
        public short? UserID { get; set; }
        public DateTime? CreationTime { get; set; }
    }

    /// <summary>سربرگ برنامه تمرینی (به ازای هر شاگرد)</summary>
    [Table("SportPrg")]
    public class SportPrg
    {
        [Key]
        public int PrgID { get; set; }
        public int? MemberID { get; set; }
        public int? CoachID { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public short? UserID { get; set; }
        public long? Amount { get; set; }
        public byte? CoachPercent { get; set; }
        public long? CoachMoney { get; set; }
        public DateTime? CreationTime { get; set; }
        public short? Modifier { get; set; }
        public DateTime? Modificationtime { get; set; }
    }

    /// <summary>جزئیات برنامه (حرکات)</summary>
    [Table("SportPrgDtl")]
    public class SportPrgDtl
    {
        [Key]
        public int SportPrgDtlID { get; set; }
        public int? PrgID { get; set; }
        public int? ItemID { get; set; }
        public int? SetCount { get; set; }
        public int? WCount { get; set; }
        public int? RepCount { get; set; }
        public int? RestSeconds { get; set; }
        public byte? ExerciseType { get; set; }
        public string? DayTitle { get; set; }
        public string? Note { get; set; }
        public int? SortOrder { get; set; }
        public int? ComboGroup { get; set; }
        public byte? SeqInCombo { get; set; }
        public byte? DropCount { get; set; }
        public byte? DropWeightPct { get; set; }
        public byte? PyramidDir { get; set; }
        public int? WeightStep { get; set; }
        public byte? PauseCount { get; set; }
        public int? PauseRest { get; set; }
        public string? Tempo { get; set; }
    }
}