namespace TandisWebApp.DTOs
{
    // ===== ورود/خروج باشگاه (ثبت حضور عضو) =====
    public class AthleteVisitDto
    {
        public int VisitID { get; set; }
        public string VisitDate { get; set; } = string.Empty;   // شمسی
        public string EnterTime { get; set; } = string.Empty;   // HH:mm
        public string? ExitTime { get; set; }
        public string? SessionName { get; set; }
        public bool IsOpen { get; set; }
        public int? DurationMinutes { get; set; }
    }

    // ===== آمار کارت «وضعیت من» =====
    public class AthleteStatsDto
    {
        public int VisitsThisWeek { get; set; }
        public int VisitsThisMonth { get; set; }
        public int TotalMinutesThisMonth { get; set; }
        public int StreakDays { get; set; }
        public int SetsDoneToday { get; set; }
    }

    // ===== لاگ یک حرکت در یک روز =====
    public class AthleteSetLogDto
    {
        public int ItemID { get; set; }
        public string LogDate { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public int DurationSec { get; set; }
        public string? Note { get; set; }
    }

    // ===== خروجی روز انتخابی برنامه (برای رندر در صفحه ورزشکاران) =====
    public class AthleteDayDto
    {
        public int PrgID { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public string DayTitle { get; set; } = string.Empty;
        public List<string> Days { get; set; } = new();
        public List<ProgramItemEditDto> Items { get; set; } = new();
        public List<AthleteSetLogDto> Logs { get; set; } = new();
    }

    // ===== مدل کامل صفحه «ورزشکاران» =====
    public class AthleteIndexDto
    {
        public string TodayShamsi { get; set; } = string.Empty;
        public string WeekdayFa { get; set; } = string.Empty;
        public AthleteVisitDto? TodayVisit { get; set; }
        public List<AthleteVisitDto> RecentVisits { get; set; } = new();
        public AthleteStatsDto Stats { get; set; } = new();
        public List<string> Sessions { get; set; } = new();
        public List<TandisWebApp.Models.Gen_San> SansList { get; set; } = new();
        public List<MemberProgramDto> Programs { get; set; } = new();
        public List<LockerRoomOptionDto> LockerRooms { get; set; } = new();
    }

    // ===== کمد =====
    public class LockerRoomOptionDto
    {
        public short LockerRoomID { get; set; }
        public string LockerRoomName { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
        public bool HasController { get; set; }
        public string Transport { get; set; } = string.Empty; // UDP / Serial
        public List<LockerBoxOptionDto> Boxes { get; set; } = new();
    }

    public class LockerBoxOptionDto
    {
        public short BoxID { get; set; }
        public short BoxNo { get; set; }
        public byte? RadifNo { get; set; }
    }

    public class LockerOpenResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Detail { get; set; }
    }

    // ===== کل صفحه: پاسخ آماده نمایش =====
    public class AthletePageModel
    {
        public AthleteIndexDto Data { get; set; } = new();
    }
}
