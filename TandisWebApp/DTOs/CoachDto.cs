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
}