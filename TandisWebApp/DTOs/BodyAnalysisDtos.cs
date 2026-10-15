namespace TandisWebApp.DTOs
{
    // ============================================================
    //  تحلیل بدن عضو — پنل عضو
    // ============================================================

    /// <summary>پروفایل ثابت عضو (قد، فعالیت روزانه، سابقه، هدف، جلسات هفتگی)</summary>
    public class BodyProfileDto
    {
        public decimal? HeightCm { get; set; }
        public byte ActivityLevel { get; set; } = 2;
        public byte SportHistory { get; set; }
        public byte Goal { get; set; } = 3;
        public byte SessionsPerWeek { get; set; } = 3;
    }

    /// <summary>یک اندازه‌گیری (ردیف نمودار)</summary>
    public class BodyRecordDto
    {
        public int RecordID { get; set; }
        public string RecordDate { get; set; } = string.Empty;   // شمسی
        public DateTime? RecordDateTime { get; set; }
        public decimal? WeightKg { get; set; }
        public decimal? WaistCm { get; set; }
        public decimal? AbdomenCm { get; set; }
        public decimal? ChestCm { get; set; }
        public decimal? ArmCm { get; set; }
        public decimal? ThighCm { get; set; }
        public decimal? HipCm { get; set; }
    }

    /// <summary>درخواست ثبت/ویرایش اندازه‌گیری</summary>
    public class BodyRecordSaveRequest
    {
        public string? RecordDate { get; set; }                  // خالی = امروز
        public decimal? WeightKg { get; set; }
        public decimal? WaistCm { get; set; }
        public decimal? AbdomenCm { get; set; }
        public decimal? ChestCm { get; set; }
        public decimal? ArmCm { get; set; }
        public decimal? ThighCm { get; set; }
        public decimal? HipCm { get; set; }
    }

    /// <summary>شاخص‌های محاسبه‌شده تحلیل بدن</summary>
    public class BodyMetricsDto
    {
        // ۱) شاخص توده بدنی
        public decimal? Bmi { get; set; }
        public string? BmiCategory { get; set; }                 // مثلاً «نرمال»
        public string? BmiAdvice { get; set; }                   // توضیح کوتاه

        // ۲) سوخت‌وساز پایه (Mifflin-St Jeor)
        public decimal? Bmr { get; set; }

        // ۳) کالری روزانه (TDEE)
        public decimal? Tdee { get; set; }
        public decimal ActivityFactor { get; set; }
        public string? ActivityLabel { get; set; }

        // ۴) پروتئین روزانه (گرم)
        public decimal? ProteinG { get; set; }
        public string? ProteinAdvice { get; set; }

        // ۵) کالری هدف بر اساس هدف ورزشی
        public decimal? GoalCalories { get; set; }               // کالری پیشنهادی برای هدف انتخابی
        public decimal? CutCalories { get; set; }                // کالری کاهش چربی
        public decimal? BulkCalories { get; set; }               // کالری عضله‌سازی
        public string? GoalLabel { get; set; }

        // ۶) وزن هدف
        public decimal? IdealWeightMin { get; set; }             // بازه سالم BMI
        public decimal? IdealWeightMax { get; set; }
        public decimal? DevineWeight { get; set; }               // فرمول دوین (تک‌مقدار)
        public decimal? TargetWeight { get; set; }               // وزن هدف پیشنهادی بر اساس هدف

        // ۷) روند تغییرات وزن
        public decimal? FirstWeight { get; set; }
        public decimal? LastWeight { get; set; }
        public decimal? WeightDelta { get; set; }                // آخرین − اولین
        public string? TrendLabel { get; set; }                  // «رو به کاهش» / «رو به افزایش» / «ثابت»

        // ۸) نسبت دور کمر به قد
        public decimal? WaistToHeight { get; set; }
        public string? WaistToHeightCategory { get; set; }

        // اطلاعات مکمل
        public int? Age { get; set; }
        public bool? Gender { get; set; }                        // true=مرد
        public decimal? BodyFatPct { get; set; }                 // تخمینی (Deurenberg)
        public int? RecordCount { get; set; }
        public bool HasEnoughData { get; set; }                  // قد/وزن/سن/جنسیت کامل است؟
    }

    /// <summary>کل صفحه تحلیل بدن</summary>
    public class BodyAnalysisPageDto
    {
        public string FullName { get; set; } = string.Empty;
        public int MemberID { get; set; }
        public int? Age { get; set; }
        public string? BirthDate { get; set; }
        public bool? Gender { get; set; }

        public BodyProfileDto Profile { get; set; } = new();
        public BodyRecordDto? Latest { get; set; }
        public List<BodyRecordDto> Records { get; set; } = new();
        public BodyMetricsDto Metrics { get; set; } = new();
    }
}
