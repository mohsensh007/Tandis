namespace TandisWebApp.DTOs
{
    public class CoachDashboardDto
    {
        public string CoachName { get; set; } = "";
        public int ActiveStudents { get; set; }
        public int MyClassCount { get; set; }
        public int TodayAttendance { get; set; }
        public List<CoachClassDto> TodayClasses { get; set; } = new();
    }

    public class CoachClassDto
    {
        public int SportSanseID { get; set; }
        public string SportName { get; set; } = "";
        public string SanseName { get; set; } = "";
        public string DayName { get; set; } = "";
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public int? ClassCapacity { get; set; }
        public int? FreeNow { get; set; }
        public int RegCount { get; set; }
        public int RegValidCount { get; set; }
        public int TodayAttendance { get; set; }
        public string Status { get; set; } = "";      // ongoing / upcoming / done
        public string StatusDesc { get; set; } = "";  // متن فارسی
    }
    // ... بقیه کدهای فعلی (CoachDashboardDto و CoachClassDto)

    /// <summary>اطلاعات یک کلاس برای لیست کلاس‌ها</summary>
    public class CoachClassListDto
    {
        public int SportSanseID { get; set; }
        public string SportName { get; set; } = "";
        public string SanseName { get; set; } = "";
        public string ScheduleSummary { get; set; } = "";   // "شنبه و دوشنبه ۱۸-۲۰"
        public int TotalStudents { get; set; }
        public int ActiveStudents { get; set; }
        public int? ClassCapacity { get; set; }
        public int? FreeNow { get; set; }
    }

    /// <summary>اطلاعات یک شاگرد در کلاس</summary>
    public class CoachStudentDto
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = "";
        public string? Mobile { get; set; }
        public string? StartDate { get; set; }   // شمسی
        public string? EndDate { get; set; }     // شمسی
        public int TotalSessions { get; set; }
        public int UsedSessions { get; set; }
        public int RemainingSessions { get; set; }
        public bool IsActive { get; set; }
        public string StatusLabel { get; set; } = "";   // "فعال" / "منقضی" / "تمام"
        public string StatusClass { get; set; } = "";   // success/warning/danger
        public long SportMemberID { get; set; }   // ✅ جدید
    }
    /// <summary>یک ردیف برنامه هفتگی سانس</summary>
    public class CoachSchedulePartDto
    {
        public int SportSanseID { get; set; }
        public string? DayName { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
    }
    /// <summary>یک تردد در تاریخچه حضور شاگرد</summary>
    public class CoachAttendanceDto
    {
        public long TrafficID { get; set; }
        public string? EntryDate { get; set; }
        public string? EntryTime { get; set; }
        public string? ExitDate { get; set; }
        public string? ExitTime { get; set; }
        public bool IsOpen { get; set; }   // هنوز خروج نزده
    }

    /// <summary>کارت کامل شاگرد</summary>
    public class CoachStudentDetailsDto
    {
        public string ClassName { get; set; } = "";
        public int MemberID { get; set; }
        public string FullName { get; set; } = "";
        public string? Mobile { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public int TotalSessions { get; set; }
        public int UsedSessions { get; set; }
        public int RemainingSessions { get; set; }
        public bool IsActive { get; set; }
        public string StatusLabel { get; set; } = "";
        public string StatusClass { get; set; } = "";
        public List<CoachAttendanceDto> Attendances { get; set; } = new();
    }
    /// <summary>خلاصه پورسانت مربی</summary>
    public class CoachCommissionSummaryDto
    {
        public string FromDate { get; set; } = "";  
        public string ToDate { get; set; } = "";
        public long ThisMonthAmount { get; set; }
        public int ThisMonthCount { get; set; }
        public long TotalAmount { get; set; }
        public int TotalCount { get; set; }
        public List<CoachCommissionRowDto> Rows { get; set; } = new();
    }

    /// <summary>یک ردیف گزارش پورسانت</summary>
    public class CoachCommissionRowDto
    {
        public long SportMemberID { get; set; }
        public string StudentName { get; set; } = "";
        public string? Mobile { get; set; }
        public string SportName { get; set; } = "";
        public string SanseName { get; set; } = "";
        public string? StartDate { get; set; }
        public long Amount { get; set; }
        public long CoachAmount { get; set; }
        public long CoachRevivalAmount { get; set; }
        public long TotalCommission => CoachAmount + CoachRevivalAmount;
        public bool IsRevival { get; set; }
    }
}