namespace TandisWebApp.DTOs
{
    // ===== لیست برنامه‌های مربی =====
    public class CoachProgramRowDto
    {
        public int PrgID { get; set; }
        public int MemberID { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StartDateShamsi { get; set; } = string.Empty;
        public string EndDateShamsi { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public bool IsActive { get; set; }
    }

    // ===== آیتم حرکت =====
    public class ProgramItemEditDto
    {
        public int ItemID { get; set; }
        public string ItemDesc { get; set; } = string.Empty;
        public int? SetCount { get; set; }
        public int? WCount { get; set; }
        public int? RepCount { get; set; }
        public int? RestSeconds { get; set; }
        public byte? ExerciseType { get; set; }
        public string? DayTitle { get; set; }
        public string? Note { get; set; }
    }

    // ===== فرم ویرایش برنامه =====
    public class CoachProgramEditDto
    {
        public int PrgID { get; set; }
        public int MemberID { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StartDateShamsi { get; set; } = string.Empty;
        public string EndDateShamsi { get; set; } = string.Empty;
        public List<ProgramItemEditDto> Items { get; set; } = new();
    }

    public class StudentOptionDto
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public string SanseName { get; set; } = string.Empty;
    }

    // ===== درخواست ذخیره =====
    public class SaveProgramRequest
    {
        public int PrgID { get; set; }
        public int MemberID { get; set; }
        public string StartDateShamsi { get; set; } = string.Empty;
        public string EndDateShamsi { get; set; } = string.Empty;
        public List<SaveProgramItemRequest> Items { get; set; } = new();
    }

    public class SaveProgramItemRequest
    {
        public int ItemID { get; set; }
        public string? NewItemDesc { get; set; }
        public int? SetCount { get; set; }
        public int? WCount { get; set; }
        public int? RepCount { get; set; }
        public int? RestSeconds { get; set; }
        public byte? ExerciseType { get; set; }
        public string? DayTitle { get; set; }
        public string? Note { get; set; }
    }

    // ===== نمایش در پنل عضو =====
    public class MemberProgramDto
    {
        public int PrgID { get; set; }
        public string CoachName { get; set; } = string.Empty;
        public string StartDateShamsi { get; set; } = string.Empty;
        public string EndDateShamsi { get; set; } = string.Empty;
        public List<ProgramItemEditDto> Items { get; set; } = new();
    }
    // ===== جزئیات برنامه (گروه‌بندی بر اساس روز) =====
    public class CoachProgramDetailsDto
    {
        public int PrgID { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StartDateShamsi { get; set; } = string.Empty;
        public string EndDateShamsi { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public List<ProgramDayGroupDto> Days { get; set; } = new();
    }

    public class ProgramDayGroupDto
    {
        public string DayTitle { get; set; } = string.Empty;
        public List<ProgramItemEditDto> Items { get; set; } = new();
    }
    // ===== ردیف شاگرد (لیست همه شاگردان مربی) =====
    public class CoachStudentRowDto
    {
        public int MemberID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string SportName { get; set; } = string.Empty;
        public string SanseName { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
    }
}