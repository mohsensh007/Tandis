using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TandisWebApp.Data;
using TandisWebApp.DTOs;
using TandisWebApp.Models;

namespace TandisWebApp.Services
{
    /// <summary>
    /// تحلیل بدن عضو:
    ///  ۱) BMI   ۲) BMR (Mifflin-St Jeor)   ۳) TDEE (ضریب فعالیت)
    ///  ۴) پروتئین روزانه (ISSN)   ۵) کالری هدف (کاهش چربی / عضله‌سازی)
    ///  ۶) وزن هدف (بازه BMI سالم + فرمول Devine)   ۷) روند تغییرات وزن
    ///  ۸) نسبت دور کمر به قد
    /// منابع: WHO (BMI) — Academy of Nutrition and Dietetics (Mifflin-St Jeor)
    ///        ISSN (پروتئین) — NHS (نسبت کمر به قد)
    /// </summary>
    public class BodyAnalysisService
    {
        private readonly FullSportDbContext _db;
        private readonly CommonHelperService _helper;
        private readonly ILogger<BodyAnalysisService> _logger;

        public BodyAnalysisService(FullSportDbContext db, CommonHelperService helper, ILogger<BodyAnalysisService> logger)
        {
            _db = db;
            _helper = helper;
            _logger = logger;
        }

        // ============================================================
        //  ضرایب فعالیت (TDEE)
        // ============================================================
        private static readonly (byte Level, decimal Factor, string Label)[] ActivityLevels =
        {
            (1, 1.200m,  "کم‌تحرک — ورزش ندارد"),
            (2, 1.375m,  "سبک — ورزش ۱ تا ۳ روز در هفته"),
            (3, 1.550m,  "متوسط — ورزش ۳ تا ۵ روز در هفته"),
            (4, 1.725m,  "زیاد — ورزش ۶ تا ۷ روز در هفته"),
            (5, 1.900m,  "خیلی زیاد — ورزش سنگین یا کار فیزیکی")
        };

        private static readonly string[] GoalLabels = { "", "چربی‌سوزی", "عضله‌سازی", "حفظ تناسب" };
        private static readonly string[] SportHistoryLabels =
        {
            "بدون سابقه", "کمتر از ۶ ماه", "۶ ماه تا ۱ سال", "۱ تا ۳ سال", "۳ تا ۵ سال", "بیش از ۵ سال"
        };

        public static string ActivityLabel(byte level)
            => ActivityLevels.FirstOrDefault(a => a.Level == level).Label ?? ActivityLevels[1].Label;

        public static string GoalLabel(byte goal)
            => goal >= 1 && goal <= 3 ? GoalLabels[goal] : GoalLabels[3];

        public static string SportHistoryLabel(byte h)
            => h < SportHistoryLabels.Length ? SportHistoryLabels[h] : SportHistoryLabels[0];

        // ============================================================
        //  خواندن کل صفحه
        // ============================================================
        public async Task<BodyAnalysisPageDto?> GetPageAsync(int memberID)
        {
            var member = await (
                from m in _db.Gen_Members
                join p in _db.Gen_Persons on m.PersonID equals p.PersonID
                where m.MemberID == memberID
                select new { p }
            ).FirstOrDefaultAsync();

            if (member == null) return null;

            var profile = await _db.Ath_BodyProfiles.AsNoTracking()
                .FirstOrDefaultAsync(x => x.MemberID == memberID);

            var records = await _db.Ath_BodyRecords.AsNoTracking()
                .Where(r => r.MemberID == memberID)
                .OrderBy(r => r.RecordDate)
                .ToListAsync();

            var page = new BodyAnalysisPageDto
            {
                MemberID = memberID,
                FullName = member.p.FullName ?? $"{member.p.FirstName} {member.p.LastName}",
                BirthDate = member.p.BirthDate,
                Gender = member.p.Gender,
                Age = CalcAge(member.p.BirthDate),
                Profile = profile == null ? new BodyProfileDto() : new BodyProfileDto
                {
                    HeightCm = profile.HeightCm,
                    ActivityLevel = profile.ActivityLevel,
                    SportHistory = profile.SportHistory,
                    Goal = profile.Goal,
                    SessionsPerWeek = profile.SessionsPerWeek
                },
                Records = records.Select(ToDto).ToList(),
                Latest = records.Count > 0 ? ToDto(records[records.Count - 1]) : null
            };

            page.Metrics = Compute(page, records);
            return page;
        }

        // ============================================================
        //  ذخیره پروفایل (قد و غیره)
        // ============================================================
        public async Task<string> SaveProfileAsync(int memberID, BodyProfileDto dto)
        {
            if (dto.HeightCm is <= 50 or > 260)
                return "قد باید بین ۵۰ تا ۲۶۰ سانتی‌متر باشد.";

            var profile = await _db.Ath_BodyProfiles
                .FirstOrDefaultAsync(x => x.MemberID == memberID);

            if (profile == null)
            {
                profile = new Ath_BodyProfile { MemberID = memberID, CreationDate = DateTime.Now };
                _db.Ath_BodyProfiles.Add(profile);
            }

            profile.HeightCm = dto.HeightCm;
            profile.ActivityLevel = (byte)Math.Clamp((int)dto.ActivityLevel, 1, 5);
            profile.SportHistory = (byte)Math.Clamp((int)dto.SportHistory, 0, 5);
            profile.Goal = (byte)Math.Clamp((int)dto.Goal, 1, 3);
            profile.SessionsPerWeek = (byte)Math.Clamp((int)dto.SessionsPerWeek, 0, 14);
            profile.ModificationTime = DateTime.Now;

            await _db.SaveChangesAsync();
            return "";
        }

        // ============================================================
        //  ثبت اندازه‌گیری (ثبت مجدد در همان تاریخ = ویرایش)
        // ============================================================
        public async Task<string> SaveRecordAsync(int memberID, BodyRecordSaveRequest req)
        {
            // اعتبارسنجی
            if (req.WeightKg is <= 20 or > 400) return "وزن باید بین ۲۰ تا ۴۰۰ کیلوگرم باشد.";
            foreach (var (v, name) in new[]
                     {
                         (req.WaistCm, "دور کمر"), (req.AbdomenCm, "دور شکم"), (req.ChestCm, "دور سینه"),
                         (req.ArmCm, "دور بازو"), (req.ThighCm, "دور ران"), (req.HipCm, "دور باسن")
                     })
            {
                if (v is <= 10 or > 300) return $"{name} باید بین ۱۰ تا ۳۰۰ سانتی‌متر باشد.";
            }

            var date = string.IsNullOrWhiteSpace(req.RecordDate) ? _helper.GetToday() : req.RecordDate!.Trim();
            try { _helper.ToGregorian(date); }
            catch { return "تاریخ اندازه‌گیری معتبر نیست (مثال: 1404/07/22)"; }

            // پروفایل باید وجود داشته باشد (FK)
            var hasProfile = await _db.Ath_BodyProfiles.AnyAsync(x => x.MemberID == memberID);
            if (!hasProfile)
            {
                _db.Ath_BodyProfiles.Add(new Ath_BodyProfile
                {
                    MemberID = memberID,
                    CreationDate = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }

            var rec = await _db.Ath_BodyRecords
                .FirstOrDefaultAsync(r => r.MemberID == memberID && r.RecordDate == date);

            if (rec == null)
            {
                rec = new Ath_BodyRecord { MemberID = memberID, RecordDate = date };
                _db.Ath_BodyRecords.Add(rec);
            }

            rec.RecordDateTime = DateTime.Now;
            rec.WeightKg = req.WeightKg;
            rec.WaistCm = req.WaistCm;
            rec.AbdomenCm = req.AbdomenCm;
            rec.ChestCm = req.ChestCm;
            rec.ArmCm = req.ArmCm;
            rec.ThighCm = req.ThighCm;
            rec.HipCm = req.HipCm;

            await _db.SaveChangesAsync();
            return "";
        }

        /// <summary>حذف یک اندازه‌گیری متعلق به همین عضو</summary>
        public async Task<string> DeleteRecordAsync(int memberID, int recordID)
        {
            var rec = await _db.Ath_BodyRecords
                .FirstOrDefaultAsync(r => r.RecordID == recordID && r.MemberID == memberID);
            if (rec == null) return "رکورد یافت نشد.";
            _db.Ath_BodyRecords.Remove(rec);
            await _db.SaveChangesAsync();
            return "";
        }

        // ============================================================
        //  ✅ محاسبات — بر اساس فرمول‌های تحقیق‌شده
        // ============================================================
        public BodyMetricsDto Compute(BodyAnalysisPageDto page, List<Ath_BodyRecord> recordsAsc)
        {
            var m = new BodyMetricsDto
            {
                Age = page.Age,
                Gender = page.Gender,
                RecordCount = recordsAsc.Count
            };

            var heightCm = page.Profile.HeightCm;
            var weightKg = recordsAsc.Count > 0 ? recordsAsc[recordsAsc.Count - 1].WeightKg : null;
            var waistCm = recordsAsc.Count > 0 ? recordsAsc[recordsAsc.Count - 1].WaistCm : null;
            var age = page.Age;
            var male = page.Gender == true;

            m.HasEnoughData = heightCm != null && weightKg != null && age != null && page.Gender != null;

            // با داده ناقص هم بخش‌های ممکن محاسبه می‌شوند (مثلاً BMI بدون سن)
            if (heightCm == null || weightKg == null)
            {
                AppendTrend(m, recordsAsc);   // روند وزن به قد/سن نیاز ندارد
                return m;
            }

            decimal h = heightCm!.Value;
            decimal w = weightKg!.Value;
            decimal hm = h / 100m;
            int a = age ?? 0;
            bool hasAgeGender = age != null && page.Gender != null;

            // ---- ۱) BMI = وزن / قد² ----
            var bmi = w / (hm * hm);
            m.Bmi = Math.Round(bmi, 1);
            m.BmiCategory = BmiCategory(bmi, out var advice);
            m.BmiAdvice = advice;

            // ---- ۶) وزن هدف (فقط قد لازم دارد) ----
            //      بازه سالم BMI (18.5–24.9) × قد²  +  فرمول Devine
            decimal bmiMin = 18.5m * hm * hm;
            decimal bmiMax = 24.9m * hm * hm;
            m.IdealWeightMin = Math.Round(bmiMin, 1);
            m.IdealWeightMax = Math.Round(bmiMax, 1);

            if (page.Gender != null)
            {
                // Devine — قد بر حسب اینچ، بالاتر از ۱۵۲ سانت
                decimal inches = h / 2.54m;
                decimal over5ft = Math.Max(0m, inches - 60m);
                decimal devine = male ? 50m + 2.3m * over5ft : 45.5m + 2.3m * over5ft;
                m.DevineWeight = Math.Round(devine, 1);
            }

            // وزن هدف پیشنهادی: اگر خارج از بازه سالمه → لبه‌ی نزدیک‌تر بازه، داخل بازه → میانه بازه برای چربی‌سوزی
            decimal target;
            if (w > bmiMax) target = page.Profile.Goal == 2 ? bmiMax : Math.Max(bmiMin, bmiMax);
            else if (w < bmiMin) target = bmiMin;
            else target = page.Profile.Goal == 1
                ? Math.Max(bmiMin, Math.Min(w, (bmiMin + bmiMax) / 2m))
                : w;
            m.TargetWeight = Math.Round(target, 1);

            // ---- ۸) نسبت دور کمر به قد (WtHR) — NHS: هدف < 0.5 ----
            if (waistCm != null)
            {
                var ratio = waistCm.Value / h;
                m.WaistToHeight = Math.Round(ratio, 2);
                m.WaistToHeightCategory = ratio < 0.5m ? "نرمال ✅"
                    : ratio < 0.6m ? "کمی بالا ⚠️"
                    : ratio < 0.65m ? "بالا ⚠️"
                    : "خیلی بالا 🔴";
            }

            // ---- ۷) روند تغییرات وزن ----
            AppendTrend(m, recordsAsc);

            // ---- ۴) پروتئین روزانه — فقط وزن لازم دارد (ISSN: ۱٫۴ تا ۲٫۰ گرم/کیلو) ----
            //      چربی‌سوزی: بالای بازه (حفظ عضله در کسری کالری) | عضله‌سازی: ۲٫۰ | حفظ: ۱٫۶
            decimal perKg = page.Profile.Goal switch { 1 => 2.0m, 2 => 2.0m, _ => 1.6m };
            m.ProteinG = Math.Round(w * perKg);
            m.ProteinAdvice = page.Profile.Goal == 3
                ? $"{perKg:0.0} گرم به ازای هر کیلوگرم وزن (روزانه)"
                : $"{perKg:0.0} گرم به ازای هر کیلوگرم — بالای بازه پیشنهادی ISSN برای حفظ عضله";

            // از اینجا به بعد به سن/جنسیت نیاز است
            if (!hasAgeGender) return m;

            // ---- ۲) BMR — Mifflin-St Jeor ----
            decimal bmr = 10m * w + 6.25m * h - 5m * a + (male ? 5m : -161m);
            m.Bmr = Math.Round(bmr);

            // ---- ۳) TDEE = BMR × ضریب فعالیت ----
            var act = ActivityLevels.FirstOrDefault(x => x.Level == page.Profile.ActivityLevel);
            if (act.Label == null) act = ActivityLevels[1];
            m.ActivityFactor = act.Factor;
            m.ActivityLabel = act.Label;
            var tdee = bmr * act.Factor;
            m.Tdee = Math.Round(tdee);

            // ---- ۵) کالری هدف ----
            //      کاهش چربی: TDEE − 500 (کف: BMR و ۱۲۰۰/۱۵۰۰) | عضله‌سازی: TDEE + 10٪
            var cut = tdee - 500m;
            var cutFloor = Math.Max(bmr, male ? 1500m : 1200m);
            if (cut < cutFloor) cut = cutFloor;
            var bulk = tdee * 1.10m;

            m.CutCalories = Math.Round(cut);
            m.BulkCalories = Math.Round(bulk);
            m.GoalLabel = GoalLabel(page.Profile.Goal);
            m.GoalCalories = page.Profile.Goal switch
            {
                1 => m.CutCalories,
                2 => m.BulkCalories,
                _ => m.Tdee
            };

            // ---- مکمل: تخمین چربی بدن — Deurenberg ----
            //      %BF = 1.20×BMI + 0.23×سن − 10.8×جنس − 5.4   (جنس: مرد=1، زن=0)
            var bf = 1.20m * bmi + 0.23m * a - 10.8m * (male ? 1m : 0m) - 5.4m;
            if (bf > 3 && bf < 70) m.BodyFatPct = Math.Round(bf, 1);

            return m;
        }

        /// <summary>روند تغییرات وزن — بدون نیاز به قد/سن</summary>
        private static void AppendTrend(BodyMetricsDto m, List<Ath_BodyRecord> recordsAsc)
        {
            var withWeight = recordsAsc.Where(r => r.WeightKg != null).ToList();
            if (withWeight.Count >= 2)
            {
                var first = withWeight[0].WeightKg!.Value;
                var last = withWeight[withWeight.Count - 1].WeightKg!.Value;
                m.FirstWeight = first;
                m.LastWeight = last;
                m.WeightDelta = Math.Round(last - first, 1);
                m.TrendLabel = last < first - 0.1m ? "رو به کاهش 📉"
                    : last > first + 0.1m ? "رو به افزایش 📈"
                    : "ثابت ➖";
            }
            else if (withWeight.Count == 1)
            {
                m.FirstWeight = m.LastWeight = withWeight[0].WeightKg;
                m.WeightDelta = 0;
                m.TrendLabel = "برای نمایش روند، حداقل ۲ اندازه‌گیری لازم است";
            }
        }

        private static string BmiCategory(decimal bmi, out string advice)
        {
            if (bmi < 18.5m) { advice = "وزن کمتر از حد سالم — تغذیه کافی توصیه می‌شود."; return "کمبود وزن"; }
            if (bmi < 23.0m) { advice = "در محدوده سالم قرار دارید."; return "نرمال"; }
            if (bmi < 25.0m) { advice = "مرز هشدار برای افراد آسیایی — مراقب تغذیه باشید."; return "نرمال (بالا)"; }
            if (bmi < 30.0m) { advice = "اضافه وزن — ورزش و کنترل تغذیه پیشنهاد می‌شود."; return "اضافه وزن"; }
            if (bmi < 35.0m) { advice = "چاقی درجه ۱ — با مربی مشورت کنید."; return "چاقی درجه ۱"; }
            if (bmi < 40.0m) { advice = "چاقی درجه ۲ — نیاز به برنامه جدی."; return "چاقی درجه ۲"; }
            advice = "چاقی شدید — حتماً با پزشک مشورت کنید."; return "چاقی درجه ۳";
        }

        /// <summary>محاسبه سن از تاریخ تولد شمسی/میلادی ذخیره‌شده در Gen_Person</summary>
        private int? CalcAge(string? birthDate)
        {
            if (string.IsNullOrWhiteSpace(birthDate)) return null;
            try
            {
                var pc = new PersianCalendar();
                var parts = birthDate.Replace('-', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3) return null;
                int y = int.Parse(parts[0]), mo = int.Parse(parts[1]), d = int.Parse(parts[2]);
                DateTime b = y > 1600
                    ? new DateTime(y, mo, d)                    // میلادی
                    : pc.ToDateTime(y, mo, d, 0, 0, 0, 0);      // شمسی
                var today = DateTime.Today;
                int age = today.Year - b.Year;
                if (today < b.AddYears(age)) age--;
 return age is > 0 and < 130 ? age : null;
            }
            catch { return null; }
        }

        private static BodyRecordDto ToDto(Ath_BodyRecord r) => new()
        {
            RecordID = r.RecordID,
            RecordDate = r.RecordDate,
            WeightKg = r.WeightKg,
            WaistCm = r.WaistCm,
            AbdomenCm = r.AbdomenCm,
            ChestCm = r.ChestCm,
            ArmCm = r.ArmCm,
            ThighCm = r.ThighCm,
            HipCm = r.HipCm
        };
    }
}
